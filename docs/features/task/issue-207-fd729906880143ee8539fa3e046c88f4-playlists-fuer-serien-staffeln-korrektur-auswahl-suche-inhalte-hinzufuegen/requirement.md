# Kundenanforderung: Korrektur der Medienauswahl-Suche für Playlists

## Fachliche Zusammenfassung

Bei der Abnahmeprüfung der Playlist-Medienauswahl wurden zwei kritische Abweichungen identifiziert:
1. Die Namenssuche in `ItemsController.Get` ist case-sensitiv und blockiert damit Benutzer, die mit abweichender Groß-/Kleinschreibung suchen, obwohl das Ziel-Material existiert.
2. Die Erweiterung des `ItemsController.Get`-Endpunkts um die drei neuen Medientypen (`Movie`, `TVShowSeason`, `TVShowEpisode`) hat unbeabsichtigt die bisherige Verhalten des Quellen-Browsings (`mediaSourceId` gesetzt) verändert und liefert dort jetzt doppelte oder redundante Inhalte.

Beide Fehler müssen korrigiert werden: Das Suchverhalten muss case-insensitiv werden, und die Playlist-Namenssuche muss sauber vom bestehenden Quellen-Browsing-Verhalten getrennt werden.

---

## Betroffene Klassen und Komponenten

### Server
- **`ItemsController.cs`** (Endpunkt `/api/items`)
  - Methode `Get(long? mediaSourceId, int page, int size, string? search, long? genreId)` — derzeit verbindet Playlist-Suche und Quellen-Browsing
  - Hilfsmethoden: `GetMovieCollectionEntriesAsync`, `GetTVShowEntriesAsync`, `GetMovieEntriesAsync`, `GetSeasonEntriesAsync`, `GetEpisodeEntriesAsync` — verwenden alle `Contains(filter.Search)` (case-sensitiv)
  - `MediaEntryFilter` Record

- **Datenbankabfragen (EF Core LINQ)**
  - Alle fünf `Get*EntriesAsync`-Methoden verwenden `.Where(e => e.Name.Contains(filter.Search))`, was vom SQLite-Provider zu case-sensitiv `instr(...)` übersetzt wird
  - Umstellung erforderlich auf case-insensitive Vergleich (z. B. `EF.Functions.Like` oder `ToLower()`-Vergleich beiderseits)

### Client
- **`VideoWebPlayerClient.cs`**
  - Methode `RequestItemsCoreAsync(long? mediaSourceId, int page, int size, string? search, long genreId, CancellationToken)` — wird von beiden Usecases verwendet
  - Methode `RequestSourceItems(long mediaSourceId, ...)` — für Quellen-Browsing-Detailseite
  - Methode `RequestItemsAsync(string? search, ...)` — für Playlist-Medienauswahl

- **`VideoWebPlayerClient.Playlists.cs`** (falls vorhanden)
  - Playlist-API-Client-Methoden, die ggf. angepasst werden müssen

- **`MediaSearchSelector.razor`**
  - Komponente für Playlist-Medienauswahl, ruft `Client.RequestItemsAsync` auf (line 105)
  - Nutzt Suchergebnisse, um Benutzer bei der Auswahl zu unterstützen

### ViewModel
- **`MediaSourceDetailsViewModel.cs`**
  - Methode `LoadNextPageAsync` ruft `_client.RequestSourceItems` auf (line 100)
  - Stellt die Detailseite einer Medienquelle dar (Quellen-Browsing use case)
  - Erwartet derzeit nur 2 Ergebnistypen, bekommt aber nach der Änderung 5 Typen

### Tests
- Existing search tests in `VideoWebPlayer.Tests` — verwenden keine abweichenden Groß-/Kleinschreibung
- Neue Tests erforderlich für case-insensitive Suche
- Regressionstests erforderlich für Quellen-Browsing-Verhalten mit `mediaSourceId`

---

## Implementierungsansatz

### Korrektur 1: Case-insensitive Namenssuche

**Problem:** `Contains(search)` wird von EF Core mit SQLite-Provider zu `instr(...)` übersetzt, was case-sensitiv ist.

**Lösung:** Implementiere datenbankseitig case-insensitive Vergleich. Konkrete Optionen:
- **Option A:** `EF.Functions.Like(e.Name, $"%{search}%", "i")` — wenn SQLite `LIKE` mit case-insensitive Flag unterstützt
- **Option B:** `.Where(e => e.Name.ToLower().Contains(filter.Search.ToLower()))` — konvertiert beide Seiten zu Kleinbuchstaben vor Vergleich
- **Option C:** Nutze `EF.Functions.Collate` mit case-insensitive Collation, falls verfügbar

**Empfehlung:** Teste konkret mit dem SQLite-Provider, welche Variante zuverlässig funktioniert (nicht nur theoretisch). Die Implementierung sollte in allen fünf `Get*EntriesAsync`-Methoden einheitlich erfolgen.

**Vorbedingung zum Testen:** Unit-Tests hinzufügen, die eine Suche mit abweichender Groß-/Kleinschreibung explizit abdecken (z. B. Titel „Breaking Bad" in der DB, Suche nach „breaking bad" oder „BREAKING" muss dasselbe Ergebnis liefern).

---

### Korrektur 2: Trennung von Playlist-Suche und Quellen-Browsing

**Problem:** Der Endpunkt `GET /api/items` wurde um drei Medientypen erweitert, ohne zu berücksichtigen, dass er von zwei unterschiedlichen Use Cases verwendet wird:
1. **Playlist-Medienauswahl** (neue Funktion): Benötigt alle 5 Typen
2. **Quellen-Browsing** (alte Funktion, `mediaSourceId` gesetzt): Sollte nur die bisherigen 2 Typen (TVShow, MovieCollection) liefern

Mit `mediaSourceId` gesetzt liefert der Aufruf jetzt 5 statt 2 Typen, was die Detailseite einer Medienquelle mit redundanten Einträgen überflutet (einzelne Filme neben ihrer Sammlung, jede Staffel und Episode einzeln).

**Lösungsoptionen:**

**Option A – Expliziter Opt-in-Parameter:**
- Neuer Query-Parameter `includeIndividualMediaTypes` (boolean, Standard: `false`)
- Nur gesetzt, wenn von `MediaSearchSelector` aufgerufen; Quellen-Browsing und andere bestehende Aufrufer setzen diesen Parameter nicht
- Implementierung: `Get*EntriesAsync`-Methoden werden nur aufgerufen, wenn `includeIndividualMediaTypes: true`
- Vorteil: Minimale Änderung am Controller, kein neuer Endpunkt
- Nachteil: Semantisch nicht selbstdokumentierend

**Option B – Neuer dedizierter Endpunkt:**
- Neuer Endpunkt `GET /api/items/search` speziell für Playlist-Medienauswahl
- Alte Endpunkt `GET /api/items` behält sein bisheriges Verhalten bei
- `MediaSearchSelector.razor` ruft den neuen Endpunkt auf
- Vorteil: Klare semantische Trennung, keine Breaking Changes für Benutzer des alten Endpunkts
- Nachteil: Duplizierung von Code (zwei separate Implementierungen)

**Empfehlung:** Option A mit explizitem Opt-in-Parameter, da sie technisch sauberer ist und keine Code-Duplizierung erfordert. Der Parameter sollte nur vom neuen `MediaSearchSelector` gesetzt werden.

**Vorbedingung zum Testen:** Regression-Test hinzufügen, der das bisherige Verhalten explizit absichert (Aufruf mit `mediaSourceId` **ohne** den neuen Opt-in-Parameter sollte weiterhin nur 2 Typen liefern).

---

### Implementierungsschritte (technisch)

1. **Case-insensitive Suche implementieren:**
   - Alle fünf `Get*EntriesAsync`-Methoden anpassen (Search-Filter)
   - Teste konkret mit SQLite-Provider, welche Technik funktioniert (ToLower, Like, Collate)
   - Unit-Tests für case-insensitive Suche hinzufügen
   - Existing Tests prüfen, ob sie durch die case-insensitive Änderung beeinflusst werden

2. **Opt-in-Parameter oder neuen Endpunkt implementieren:**
   - Entscheide zwischen Option A und B (Empfehlung: Option A)
   - Optional-Parameter `includeIndividualMediaTypes` zu `Get`-Methode hinzufügen (oder neuer Endpunkt)
   - Logik: Nur `GetMovieEntriesAsync` und `GetSeasonEntriesAsync` aufrufen, wenn dieser Parameter `true` ist
   - Client-Methode `RequestItemsCoreAsync` anpassen, um den Parameter zu setzen (oder neuer Endpunkt dort)
   - `MediaSearchSelector.razor` updaten, den Parameter zu setzen

3. **Regressionstests hinzufügen:**
   - Test mit `mediaSourceId` gesetzt und **ohne** Opt-in sollte weiterhin nur 2 Typen liefern
   - Test mit `mediaSourceId` gesetzt und **mit** Opt-in sollte alle 5 Typen liefern
   - Test mit `mediaSourceId` **nicht** gesetzt sollte alle 5 Typen liefern (Search-Modus)

4. **Dokumentation aktualisieren:**
   - `docs/help/playlists-api.md`: Sektion „Medien-Suche für die Auswahl-Oberfläche" präzisieren, dass Suche case-insensitiv ist
   - Controller XML-Kommentare bei `Get`-Methode aktualisieren (falls Opt-in-Parameter)
   - Ggf. `docs/features.md` oder andere Docs mit Hinweis auf die Korrektur

---

## Konfiguration

Keine neue Konfiguration erforderlich.

---

## Offene Fragen / Entscheidungspunkte

1. **Implementierungstechnik für case-insensitive Suche:**
   - Soll `ToLower()` beiderseits, `EF.Functions.Like`, oder `Collate` verwendet werden?
   - Muss vor der Implementierung konkret mit dem SQLite-Provider getestet werden.

2. **Opt-in-Parameter vs. neuer Endpunkt:**
   - Option A (Opt-in-Parameter) wird empfohlen, zur Bestätigung vor Implementierung.
   - Option B (neuer Endpunkt) ist alternativ machbar, erfordert aber Duplizierung.

3. **Rückwärtskompatibilität:**
   - Falls andere interne oder externe Aufrufer von `ItemsController.Get` existieren, muss sichergestellt werden, dass deren Verhalten unverändert bleibt (Opt-in sollte standardmäßig `false` sein).
   - Code-Suche nach allen Aufruern dieser Methode sinnvoll vor Implementation.

4. **Testabdeckung der neuen Suche:**
   - Bestehende Tests verwenden alle genau passendes Case → sollten weiterhin grün sein
   - Neue Tests mit abweichender Groß-/Kleinschreibung sind erforderlich
   - Regressionstests für Quellen-Browsing-Verhalten sind erforderlich

---

## Beziehung zu anderen Anforderungen

- Diese Korrektur betrifft die in Schritt 3 (issue-207) eingeführte Medienauswahl-Suche für Playlists
- Sie muss vor der Abnahme abgeschlossen werden, um die Usability-Blockade (case-sensitive Suche) zu beheben
- Quellen-Browsing (Detailseite einer Medienquelle) ist keine neuen Anforderung, sondern bestehendes Feature → muss in bisheriger Form beibehalten werden
