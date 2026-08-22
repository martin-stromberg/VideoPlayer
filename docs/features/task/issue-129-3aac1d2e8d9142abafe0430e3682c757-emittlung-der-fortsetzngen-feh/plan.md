# Umsetzungsplan: Fehlerhafte Ermittlung der nächsten Episode

## Übersicht

Die Funktion `GetNextEpisodeAsync()` des `ContinueWatchingService` ermittelt derzeit fehlerhaft die nächste Episode nach einer abgeschlossenen Episode, was zu Endlosschleifen führt. Die Refaktorierung wird die Logik auf eine episodennummernbasierte Sortierung umstellen, explizites NULL-Handling implementieren und die Staffelnavigation vereinfachen. Dies betrifft den `ContinueWatchingService`, dessen Testabdeckung sowie möglicherweise auch die `GetNextMovieAsync()`-Methode als Referenzansatz.

---

## Designentscheidungen

| Komponente / Bereich | Gewählter Ansatz | Begründung |
|----------------------|-----------------|------------|
| Primäre Sortierung in `GetNextEpisodeAsync()` | Episodennummer (`Number`) | `Number` ist die zuverlässigste und eindeutige Ordnung innerhalb einer Staffel; unabhängig von Datierungsfeldern, die NULL sein können |
| Sekundäre Sortierung für ReleaseDate | Nur als Fallback/Tiebreaker | ReleaseDate kann NULL sein und ist weniger zuverlässig als die explizite Episodennummer |
| NULL-Handling für ReleaseDate/PremieredAt | Explizite Behandlung, keine SQL-Vergleiche mit NULL | Verhindert unerwartete SQL-Semantik; Episoden ohne Datum werden über ihre Nummer ermittelt |
| Staffel-Sortierung | Nach `TVShowSeason.Name` (lexikographisch) | Folgt bestehendem Muster in `GetNextEpisodeAsync()` (Zeile 75) |
| Testabdeckung | Unit-Tests in neuer Testklasse `ContinueWatchingServiceGetNextEpisodeTests` | Fehlende Testabdeckung für Kernlogik erfordert separate Testklasse; Szenarien: Happy Path, NULL-ReleaseDate, Lücken, Staffelwechsel, Edge Cases |
| Referenzlogik | `GetNextMovieAsync()` als Muster überprüfen | Diese Methode sortiert nach ReleaseDate → PremieredAt → Name; prüfen, ob ähnliche NULL-Probleme existieren |

---

## Programmabläufe

### Ablauf: Nächste Episode ermitteln (Happy Path)

1. `GetNextEpisodeAsync()` wird mit `currentEpisodeId` aufgerufen
2. Aktuelle Episode wird aus Datenbank geladen
3. Aktuelle Staffel wird aus Datenbank geladen
4. Suche nach Episode mit `Number > current.Number` in gleicher Staffel (`TVShowSeasonId == current.TVShowSeasonId`)
5. Episoden werden nach `Number` aufsteigend sortiert, erste Treffer-Episode zurückgeben
6. Falls gefunden: Episode zurückgeben und beenden
7. Falls nicht gefunden: Fortfahren mit Staffelwechsel-Ablauf

Beteiligte Klassen/Komponenten: `ContinueWatchingService`, `TVShowEpisode`, `TVShowSeason`

### Ablauf: Staffelwechsel (wenn keine nächste Episode in aktueller Staffel)

1. Alle Staffeln der Serie werden in alphabetischer Reihenfolge geladen (`TVShowId == season.TVShowId`)
2. Aktuelle Staffel wird in der Liste located
3. Nächste Staffel wird ermittelt (nächste in alphabetischer Ordnung nach der aktuellen)
4. Falls nächste Staffel existiert: Erste Episode dieser Staffel wird gesucht (`OrderBy(Number)`)
5. Erste Episode wird zurückgegeben
6. Falls keine nächste Staffel existiert: `null` zurückgeben

Beteiligte Klassen/Komponenten: `ContinueWatchingService`, `TVShowSeason`, `TVShowEpisode`

### Ablauf: NULL-Handling in Sortierung

1. Beim Sortieren von Episoden: `OrderBy(e => e.Number)` wird immer durchgeführt
2. `ReleaseDate` wird nicht in WHERE-Klauseln für Vergleiche verwendet
3. Falls `ReleaseDate` später als optionales Feld hinzugefügt werden muss (z.B. für Tiebreaker): Nur als `.ThenBy()`, nicht in Filterbedingungen
4. NULL-Werte in `ReleaseDate` oder `PremieredAt` beeinflussen die Episode-Auswahl nicht

Beteiligte Klassen/Komponenten: `ContinueWatchingService`, Entity Framework Core

---

## Neue Klassen

Keine neuen Klassen erforderlich. Die Refaktorierung betrifft nur die Logik der bestehenden `ContinueWatchingService`-Methode.

---

## Änderungen an bestehenden Klassen

### `ContinueWatchingService` (Service)

- **Geänderte Methoden:** 
  - `GetNextEpisodeAsync(long currentEpisodeId, CancellationToken ct)` — Logik umstrukturiert:
    - Primär nach `e.Number > current.Number` filtert (statt `ReleaseDate >= current.ReleaseDate`)
    - Sortierung nach `Number` (nicht nach `ReleaseDate`)
    - Explizites NULL-Handling: Keine SQL-Vergleiche mit NULL
    - Staffelwechsel-Logik beibehalten, aber mit korrektem Datenflusss
    - Rückgabetyp bleibt `Task<TVShowEpisode?>`

### `TVShowEpisode` (Datenmodellklasse)

Keine Änderungen erforderlich. Bestehendes Modell wird als-is verwendet.

### `TVShowSeason` (Datenmodellklasse)

Keine Änderungen erforderlich. Bestehendes Modell wird als-is verwendet.

---

## Datenbankmigrationen

Keine Migrationen erforderlich. Die Refaktorierung ist eine reine Logik-Änderung ohne Schemaänderungen.

---

## Validierungsregeln

Keine neuen Validierungsregeln erforderlich. Die Eingabevalidierung (z.B. Überprüfung, dass `currentEpisodeId` existiert) ist bereits in `GetNextEpisodeAsync()` implementiert.

---

## Konfigurationsänderungen

Keine Konfigurationsänderungen erforderlich.

---

## Seiteneffekte und Risiken

- **Betroffene Logik: `ProcessBufferedEntryAsync()`** — Diese Methode ruft `GetNextEpisodeAsync()` auf, wenn eine Episode als abgeschlossen markiert wird. Mit der Fix wird die korrekte nächste Episode ermittelt, was das Verhalten der Continue-Watching-Funktionalität verbessert (Seiteneffekt = gewünscht).
- **Betroffene Logik: Staffelwechsel** — Die Methode `SkipWhile()` zum Finden der nächsten Staffel bleibt erhalten; potentielles Risiko: Falls Staffel-Namen nicht eindeutig oder nicht konsistent sortierbar sind, könnte die Navigation fehlschlagen. Mitigation: Unit-Tests mit verschiedenen Staffel-Konfigurationen.
- **Betroffene Tests: `ContinueWatchingServiceSignalRTests`** — Diese Tests prüfen SignalR-Integration, nicht die Logik von `GetNextEpisodeAsync()`. Sie sollten nicht direkt betroffen sein, aber es empfiehlt sich, sie auszuführen, um zu bestätigen.
- **Regression-Risiko: Alte Logik basierte auf ReleaseDate** — Wenn Clients oder Datenquellen explizit auf dieses Verhalten angewiesen sind, könnten sie nach der Fix betroffen sein. Dies ist unwahrscheinlich, da das alte Verhalten fehlerhaft war. Tests werden dies abdecken.

---

## Umsetzungsreihenfolge

1. **Neue Testklasse `ContinueWatchingServiceGetNextEpisodeTests` mit Happy-Path-Test**
   - Voraussetzungen: Testinfrastruktur (Basis-Testklasse, InMemory-Datenbank, Fixtures) — bereits vorhanden in `ContinueWatchingServiceSignalRTests`
   - Beschreibung: Neue Testklasse mit einfachstem Test: Zwei Episoden (1, 2) in einer Staffel; Aufruf mit Episode 1 → erwartetes Ergebnis Episode 2. Dies validiert die neue Logik.

2. **Logik in `GetNextEpisodeAsync()` refaktorieren: Primäre Sortierung nach `Number`**
   - Voraussetzungen: Testklasse aus Schritt 1 vorhanden
   - Beschreibung: WHERE-Klausel ändern von `e.ReleaseDate >= current.ReleaseDate` zu `e.Number > current.Number`. Sortierung ändern von `OrderBy(e => e.ReleaseDate).ThenBy(e => e.Number)` zu `OrderBy(e => e.Number)`. Happy-Path-Test sollte grün werden.

3. **Erweiterung der Tests: NULL-ReleaseDate Szenarien**
   - Voraussetzungen: Refaktorierte Logik aus Schritt 2 vorhanden, Happy-Path-Test grün
   - Beschreibung: Tests hinzufügen für Episoden mit `ReleaseDate = NULL`, gemischte NULL/nicht-NULL-Szenarien. Validiert explizites NULL-Handling.

4. **Erweiterung der Tests: Fehlende/Lückenhafte Episoden**
   - Voraussetzungen: Schritte 1–3 abgeschlossen
   - Beschreibung: Tests für Episoden-Lücken (z.B. [1, 2, 4, 5]), erste Episode fehlt, letzte Episode fehlt. Validiert korrekte Filterung und Sortierung.

5. **Erweiterung der Tests: Staffelwechsel und Edge Cases**
   - Voraussetzungen: Schritte 1–4 abgeschlossen
   - Beschreibung: Tests für Staffelwechsel (letzte Episode einer Staffel → erste Episode der nächsten), nur eine Episode, nur eine Staffel, mehrere Staffeln. Validiert Staffelnavigation.

6. **Referenzlogik `GetNextMovieAsync()` prüfen**
   - Voraussetzungen: Hauptlogik in `GetNextEpisodeAsync()` stabilisiert (Schritte 1–5 grün)
   - Beschreibung: `GetNextMovieAsync()` überprüfen auf ähnliche NULL-Probleme (derzeit: `OrderBy(e => e.ReleaseDate).ThenBy(e => e.PremieredAt).ThenBy(e => e.Name)`). Falls Probleme identifiziert, notieren für zukünftige Issue. Nicht im Scope dieser Task, aber als Vorbeugemassnahme dokumentieren.

7. **Bestehende Tests ausführen: `ContinueWatchingServiceSignalRTests`**
   - Voraussetzungen: Refaktorierte Logik aus Schritt 2 vorhanden, neue Tests aus Schritten 1–5 alle grün
   - Beschreibung: Bestehende SignalR-Tests ausführen, um zu bestätigen, dass keine Regression eingetreten ist. Alle Tests sollten weiterhin grün sein.

8. **Code Review der refaktorierten `GetNextEpisodeAsync()`-Methode**
   - Voraussetzungen: Alle Tests grün (Schritte 1–7)
   - Beschreibung: Code Review durchführen, um Lesbarkeit, Fehlerbehandlung und Konsistenz mit Codebase-Konventionen zu prüfen. Spezifische Punkte: NULL-Handling explizit, Sortierung korrekt, Staffelwechsel-Logik robust.

9. **Abschluss und Dokumentation**
   - Voraussetzungen: Code Review abgeschlossen, alle Tests grün
   - Beschreibung: Alle Tests grün, Code Review abgeschlossen. Branch ist bereit zum Merge in `main`. Dokumentation (Anforderung, Bestandsaufnahme, Plan) wird mit dem Merge archiviert.

---

## Tests

### Neue Tests

| Test / Hilfsmethode | Testklasse | Was wird geprüft / bereitgestellt? |
|--------------------|------------|-------------------------------------|
| `HappyPath_SimpleEpisodeSequence_ReturnsNextEpisode()` | `ContinueWatchingServiceGetNextEpisodeTests` | Zwei Episoden (1, 2) in einer Staffel; Episode 1 → Episode 2 |
| `AllEpisodesWithoutReleaseDate_SortsByNumber()` | `ContinueWatchingServiceGetNextEpisodeTests` | Alle Episoden mit `ReleaseDate = NULL`; Sortierung nach Episodennummer |
| `AllEpisodesWithReleaseDate_SortsCorrectly()` | `ContinueWatchingServiceGetNextEpisodeTests` | Alle Episoden mit unterschiedlichen Release-Daten; Sortierung nach Nummer (unabhängig von Datum) |
| `MixedReleaseDate_NullAndNonNull_SortsConsistently()` | `ContinueWatchingServiceGetNextEpisodeTests` | Gemischte NULL/nicht-NULL ReleaseDate-Werte; Sortierung nach Nummer bleibt korrekt |
| `EpisodeGaps_SkipsGappedEpisodes_FindsNext()` | `ContinueWatchingServiceGetNextEpisodeTests` | Episoden [1, 2, 4, 5]; von Episode 2 → Episode 4 |
| `FirstEpisodeMissing_SkipsTo_NextAvailable()` | `ContinueWatchingServiceGetNextEpisodeTests` | Episoden [2, 3, 4]; von Episode 2 → Episode 3 |
| `LastEpisodeOfSeason_ReturnsNull_InSameSeason()` | `ContinueWatchingServiceGetNextEpisodeTests` | Episoden [1, 2]; von Episode 2 → `null` (in gleicher Staffel) |
| `SeasonTransition_LastEpisodeOfSeason_JumpsToNextSeason()` | `ContinueWatchingServiceGetNextEpisodeTests` | Staffel 1 mit Episode 2 (letzte), Staffel 2 mit Episode 1; von S1E2 → S2E1 |
| `NoNextSeason_ReturnsNull()` | `ContinueWatchingServiceGetNextEpisodeTests` | Nur eine Staffel, letzte Episode; → `null` |
| `NextSeasonEmpty_ReturnsNull()` | `ContinueWatchingServiceGetNextEpisodeTests` | Staffel 1 mit Episode 2 (letzte), Staffel 2 ohne Episoden; → `null` |
| `SingleEpisodeInSeason_ReturnsNull()` | `ContinueWatchingServiceGetNextEpisodeTests` | Eine Staffel mit einer Episode; von dieser Episode → `null` |
| `MultipleEpisodesWithIdenticalReleaseDate_SortsByNumber()` | `ContinueWatchingServiceGetNextEpisodeTests` | Mehrere Episoden mit gleichem `ReleaseDate` und aufsteigenden Nummern; Sortierung nach Nummer |
| `RegressionTest_LoopScenario_NoInfiniteLoop()` | `ContinueWatchingServiceGetNextEpisodeTests` | Episoden [A (Num=1), B (Num=2), C (Num=3)]; von A → B → C → `null` (kein Zurück zu A oder B) |
| `OffByOne_PositionNotConfusedWithId()` | `ContinueWatchingServiceGetNextEpisodeTests` | Episoden mit unterschiedlichen IDs und Nummern; Position im Array wird nicht mit ID verwechselt |
| `CreateTestShowWithSeasons()` — Hilfsmethode | `ContinueWatchingServiceGetNextEpisodeTests` | Erstellt Test-Datenstruktur: TV-Show mit Staffeln und Episoden mit konfigurierbaren Nummern/Daten |
| `AssertEpisodeEquals()` — Hilfsmethode | `ContinueWatchingServiceGetNextEpisodeTests` | Vergleicht zwei Episoden auf Gleichheit (ID, Nummer, ReleaseDate) für Assertions |

### Betroffene bestehende Tests

| Test / Testklasse | Grund der Anpassung |
|-------------------|---------------------|
| `ContinueWatchingServiceSignalRTests` | Keine Anpassung erforderlich. Diese Tests prüfen nur SignalR-Integration, nicht `GetNextEpisodeAsync()`-Logik. Sollten weiterhin grün sein nach der Refaktorierung. |

### E2E-Tests (Pflicht)

| Szenario | Testdatei / Testklasse | Abgedecktes Akzeptanzkriterium |
|----------|------------------------|-------------------------------|
| Benutzer startet Episode 1, spult zu Ende, wird zur Episode 2 weitergeleitet | E2E-Test in `VideoWebPlayer.Tests` (z.B. `ContinueWatchingE2ETests.cs`) | Happy Path: Fortlaufender Episode-Wechsel |
| Benutzer schaut Episode 2 einer Staffel, springt zu Episode 5 (Lücke), wird korrekt zu Episode 5 weitergeleitet | E2E-Test `ContinueWatchingE2ETests.cs` | Korrekte Navigation trotz Lücken |
| Benutzer schaut letzte Episode einer Staffel, wird zur ersten Episode der nächsten Staffel weitergeleitet | E2E-Test `ContinueWatchingE2ETests.cs` | Staffelwechsel funktioniert |
| Benutzer schaut letzte Episode der letzten Staffel, wird nicht weitergeleitet (Vorschlag/UI zeigt "Serie abgeschlossen") | E2E-Test `ContinueWatchingE2ETests.cs` | Korrekte Behandlung von Serienende |

**Welche bestehenden E2E-Tests sind betroffen?**

Falls vorhanden, sind möglicherweise Tests im `VideoWebPlayer.Tests`-Projekt betroffen, die Continue-Watching-Szenarien testen. Diese müssen überprüft werden auf Kompatibilität mit der neuen Logik. Derzeit nicht identifiziert; wird während Implementierung überprüft.

Falls keine bestehenden E2E-Tests vorhanden: Neue E2E-Tests werden als Pflicht-Abdeckung für diese Task implementiert.

---

## Offene Punkte

Keine. Alle technischen Anforderungen sind in der Anforderungsdatei und Bestandsaufnahme geklärt:

1. **Episodennummern-Formate** — Annahme: Ganzzahlig, aufsteigend, ohne Lücken (wird durch Tests validiert; falls Lücken auftreten, ist die Logik robust genug)
2. **ReleaseDate-Semantik** — Klarstellung: `ReleaseDate` wird nicht als Primärsortierungskriterium verwendet; Episodennummer ist primär
3. **Staffel-Namenskonvention** — Annahme: Lexikographische Sortierung nach `TVShowSeason.Name` ist ausreichend (bestehendes Muster)
4. **Referenzlogik `GetNextMovieAsync()`** — Überprüfung ist als Schritt 6 in der Umsetzungsreihenfolge dokumentiert (nicht kritisch für diese Task, aber empfohlen)
