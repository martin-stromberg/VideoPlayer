# Anforderung: Fehlerhafte Ermittlung der nächsten Episode

**Aufgaben-ID:** 3aac1d2e-8d91-42ab-afe0-430e3682c757  
**Branch:** task/issue-129-3aac1d2e8d9142abafe0430e3682c757-emittlung-der-fortsetzngen-feh  
**Erstellt:** 2026-08-22

---

## Fachliche Zusammenfassung

Die Funktion `GetNextEpisodeAsync()` des `ContinueWatchingService` ermittelt fehlerhaft die nächste Episode nach einer abgeschlossenen Episode. Das führt zu Endlosschleifen, bei denen abwechselnd zwei Episoden als "nächste Episode" ermittelt werden, anstatt fortlaufend zur tatsächlich nächsten Episode zu wechseln. Die Fehlerquelle liegt in der unzuverlässigen Verarbeitung verschiedener Episodenangaben (fehlende oder NULL-Releasedaten, unterschiedliche Episodennummern) und bei Staffelwechseln.

---

## Betroffene Klassen und Komponenten

### Service-Klassen
- **`ContinueWatchingService`** (Datei: `VideoWebPlayer/Services/ContinueWatchingService.cs`)
  - Methode `GetNextEpisodeAsync(long currentEpisodeId, CancellationToken ct)` — **problematisch**
  - Methode `GetNextMovieAsync(long currentMovieId, CancellationToken ct)` — als Referenz untersuchen
  - Methode `ProcessBufferedEntryAsync()` — nutzt `GetNextEpisodeAsync()`

### Datenmodell-Klassen
- **`TVShowEpisode`** (Datei: `VideoWebPlayer/Data/TVShowEpisode.cs`)
  - Eigenschaft `int Number` — Episodennummer
  - Eigenschaft `DateTime? ReleaseDate` — kann NULL sein
  - Eigenschaft `DateTime? PremieredAt` — alternatives Datierungsfeld
  - Eigenschaft `long TVShowSeasonId` — Staffel-Zuordnung

- **`TVShowSeason`** (Referenz)
  - Eigenschaft `long TVShowId` — Show-Zuordnung
  - Eigenschaft `string Name` — Staffel-Name (für Sortierung)

### Tests
- **`ContinueWatchingServiceSignalRTests`** (Datei: `VideoWebPlayer.Tests/Services/ContinueWatchingServiceSignalRTests.cs`)
  - **Existierende Tests:** Nur SignalR-Integration, KEINE Tests für `GetNextEpisodeAsync()`
  - **Neue Test-Klasse erforderlich:** `ContinueWatchingServiceGetNextEpisodeTests` oder ähnlich

---

## Implementierungsansatz

### Problem-Analyse

Die aktuelle Implementierung von `GetNextEpisodeAsync()` (Zeilen 346–379) hat folgende Schwachstellen:

1. **NULL-Handling für ReleaseDate:**
   - WHERE-Klausel: `e.ReleaseDate >= current.ReleaseDate`
   - Wenn `current.ReleaseDate` = NULL: Vergleich mit NULL in SQL liefert unerwartete Ergebnisse
   - Episoden ohne Releasedatum werden nicht zuverlässig behandelt

2. **Sortierreihenfolge:**
   - `OrderBy(e => e.ReleaseDate).ThenBy(e => e.Number)` 
   - NULL-Werte in ReleaseDate werden in EF Core standardmäßig am Anfang oder Ende sortiert (DB-abhängig)
   - Wenn mehrere Episoden NULL-ReleaseDate haben, ist die Reihenfolge unklar

3. **Logik bei fehlenden Episoden:**
   - Die WHERE-Klausel filtert nur Episoden mit `ReleaseDate >= current.ReleaseDate`
   - Fehlende Episoden (z.B. die nächste Episodennummer) werden möglicherweise übersprungen oder doppelt verarbeitet

4. **Staffelwechsel:**
   - Die Logik zum Sprung zur nächsten Staffel könnte durch fehlerhafte Episode-Ermittlung in der aktuellen Staffel nicht richtig ausgelöst werden

### Lösungsansatz

**Refaktorierung von `GetNextEpisodeAsync()` mit folgenden Prinzipien:**

1. **Episodennummer als Primärsortierung:**
   - Sortiere innerhalb einer Staffel primär nach `Number` (aufsteigend)
   - `ReleaseDate` als Sekundärsortierung nur als Tiebreaker verwenden
   - Episodennummer ist die zuverlässigste Quelle für die Episode-Reihenfolge

2. **NULL-Handling explizit:**
   - Behandle `ReleaseDate = NULL` und `PremieredAt = NULL` explizit
   - Definiere klare Regeln: z.B. "Wenn ReleaseDate NULL, verwende PremieredAt oder nutze Episodennummer als Fallback"
   - Verhindere Vergleiche mit NULL in WHERE-Klauseln; nutze stattdessen `Number`-basierte Logik

3. **Einfachere, lineareLogik:**
   - Suche nach der Episode mit der nächsten Nummer in der gleichen Staffel: `Number > current.Number`
   - Falls nicht gefunden: Springe zur nächsten Staffel und hole die erste Episode

4. **Konsistenz mit `GetNextMovieAsync()`:**
   - `GetNextMovieAsync()` sortiert nach `ReleaseDate` und `PremieredAt`, dann nach `Name`
   - Eventuell müssen auch dort ähnliche Probleme behoben werden (als Annahme mitberücksichtigen)

### Betroffene Services/Komponenten

- **`ContinueWatchingService`** — zentraler Service, der bei Episode-Wiedergabe-Ende die nächste Episode ermittelt
- **`ContinueWatchingWorker`** — Background-Worker, der gepufferte Einträge verarbeitet; nutzt `ProcessBufferedEntryAsync()`
- **`ContinueWatchingController`** (falls vorhanden) — API-Endpunkte für die "Weiterschauen"-Funktionalität

---

## Konfiguration

**Nicht erforderlich.** Die Funktion wird ohne benutzerseitige Konfiguration fest implementiert.

---

## Offene Fragen und Annahmen

### Zu klärende Punkte

1. **Episodennummern-Formate:**
   - Sind Episodennummern immer ganzzahlig und aufsteigend?
   - Gibt es spezielle Formate (z.B. Spezial-Episoden mit Nummer 0 oder höher als reguläre Episoden)?
   - Können Nummern in einer Staffel Lücken aufweisen (z.B. Episoden 1, 2, 4 — Episode 3 fehlt)?

2. **ReleaseDate-Semantik:**
   - Ist `ReleaseDate` das zuverlässigste Feld oder kann es fehlerhaft sein?
   - Wann sollte auf `PremieredAt` ausgewichen werden?
   - Sind beide Felder in der Regel gefüllt oder häufig leer?

3. **Staffel-Namenskonvention:**
   - Wie werden Staffeln benannt und sortiert? (z.B. "Staffel 01", "Season 1", numerische Sortierung?)
   - Gibt es Staffeln mit ungültigen oder fehlenden Nummern?

### Annahmen für die Implementierung

1. **Episodennummern als Primär-Sortierungskriterium:**
   - Annahme: `TVShowEpisode.Number` ist die zuverlässigste und eindeutige Ordnung
   - Die nächste Episode ist die mit `Number > current.Number` in der gleichen Staffel

2. **Staffel-Sortierung nach Name:**
   - Annahme: `TVShowSeason.Name` enthält sortierbare Informationen (z.B. "01", "1", "Staffel 1")
   - Staffeln werden lexikographisch sortiert; die nächste Staffel ist die nächste in alphabetischer Ordnung

3. **NULL-Handling:**
   - Annahme: `ReleaseDate` und `PremieredAt` können beide NULL sein
   - Episoden ohne Datum werden anhand ihrer Nummer ermittelt

4. **Keine Loops durch fehlerhafte Daten:**
   - Annahme: Das System enthält keine Daten-Korruptionen, die zu zirkulären Referenzen führen

---

## Test-Anforderungen

### Unit-Test-Abdeckung (erforderlich)

Neue Test-Klasse: **`ContinueWatchingServiceGetNextEpisodeTests`**

#### Test-Szenarios

**1. Happy Path — Einfacher Fall:**
- Episode 1 (ReleaseDate=2020-01-01, Number=1) → nächste: Episode 2 (Number=2)

**2. Verschiedene Episodenangaben:**
- a) ReleaseDate NULL für alle Episoden → Sortierung nach Number
- b) ReleaseDate vorhanden für alle → Sortierung nach ReleaseDate, ThenBy Number
- c) Gemischt (einige ReleaseDate, einige NULL) → konsistente Sortierung

**3. Fehlende Episoden:**
- a) Episoden-Lücken: [1, 2, 4, 5] → Von Episode 2 zur Episode 4
- b) Erste Episode fehlt: [2, 3, 4] → nächste nach 2 ist 3
- c) Letzte Episode fehlt: [1, 2, 3] → nach 3 keine Episode in dieser Staffel

**4. Staffelwechsel:**
- a) Aktuelle Staffel hat keine nächste Episode → Sprung zur nächsten Staffel, erste Episode
- b) Nächste Staffel existiert nicht → NULL zurückgeben
- c) Nächste Staffel hat keine Episoden → NULL zurückgeben

**5. Edge Cases:**
- a) Nur eine Episode in Staffel → keine nächste Episode in dieser Staffel
- b) Nur eine Staffel in Serie → keine nächste Staffel
- c) Mehrere Episoden mit identischem ReleaseDate und aufsteigenden Nummern

**6. Regression-Tests:**
- a) Loop-Szenario: Episode A ↔ Episode B wiederholt → nach Fix sollte fortlaufend weitergehen
- b) Off-by-One: Position im Array nicht mit Episode-ID verwechselt

#### Test-Daten-Setup

- TV Show mit mehreren Staffeln erstellen
- Jede Staffel mit variierten Episoden-Kombinationen füllen:
  - Vollständige Episodenlisten
  - Episoden mit Lücken
  - Unterschiedliche ReleaseDate-Konfigurationen
  - NULL-Werte an verschiedenen Stellen

#### Assertions

- Ermittelte nächste Episode ist korrekt
- Kein NULL zurück, wenn nächste Episode existiert
- NULL zurück, wenn keine nächste Episode existiert
- Keine Duplikate oder Loops
- Korrekte Staffel-Navigation

---

## Implementierungs-Checkliste

- [ ] Analysiere aktuelle `GetNextEpisodeAsync()`-Logik und finde Fehlerquelle
- [ ] Refaktoriere Logik mit Number-basierter Sortierung
- [ ] Implementiere explizites NULL-Handling für ReleaseDate
- [ ] Implementiere Staffel-Navigation
- [ ] Schreibe Unit-Tests (alle Szenarios oben)
- [ ] Führe manuelles Testing durch (kontinuierliche Episoden-Wiedergabe)
- [ ] Validiere gegen existierende SignalR-Tests
- [ ] Code Review
- [ ] Merge in main
