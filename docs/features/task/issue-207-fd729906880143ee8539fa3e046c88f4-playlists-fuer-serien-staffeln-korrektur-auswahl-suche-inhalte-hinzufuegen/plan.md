# Umsetzungsplan: Unicode-korrekte Namenssuche für Playlist-Medienauswahl

## Übersicht

Die case-insensitive Namenssuche beim Hinzufügen von Medieninhalten zu Playlists (über `GET /api/items?search=...`) muss für deutsche Umlaute (Ä, Ö, Ü) und das Eszett (ß) korrigiert werden. Das Problem: Der Suchbegriff wird in C# mit `.ToLower()` vollständig gefaltet, aber SQLite vergleicht mit seiner `lower()`-Funktion, die ohne ICU-Erweiterung Umlaute nicht korrekt verarbeitet. Die Lösung registriert eine benutzerdefinierte Datenbankfunktion, die `.ToLowerInvariant()` nutzt und in der LINQ-Such­filter-Methode verwendet wird.

Betroffene Bereiche: Such-Endpoint (`ItemsController.Get()`), zentrale Such­filter-Methode (`ApplySearchFilter<T>()`), Datenbankkontext (`ApplicationDbContext`), Tests (`ItemsControllerTests_Search`).

## Designentscheidungen

| Komponente / Bereich | Gewählter Ansatz | Begründung |
|----------------------|-----------------|------------|
| Unicode-sichere Datenbankfunktion | EF Core `DbFunction`-Attribut mit statischer Hilfsmethode `LowerInvariant()` in einer `AppDbFunctions`-Klasse, registriert über `ModelBuilder.HasDbFunction()` | Moderner Ansatz, vollständig typsicher, integriert sich nahtlos in LINQ-Expressions, ist wartbar und folgt EF Core Best Practices. Alternativet Option `SqliteConnection.CreateFunction()` ist zu manuell und Framework-abhängig. |
| Funktionsplatzierung | Neue Helferklasse `AppDbFunctions` im `Data`-Verzeichnis | Logische Separation, Wiederverwendbarkeit, Testbarkeit. |
| Such-Filter-Anpassung | Ersetze `.ToLower().Contains()` durch `.LowerInvariant().Contains()` in `ApplySearchFilter<T>()`, wobei `LowerInvariant` die DbFunction aufruft | Minimale Änderung an zentraler Stelle mit maximaler Auswirkung auf alle fünf Medientypen. |

## Programmabläufe

### Such-Anfrage mit Unicode-Zeichen

1. Benutzer reicht an `/api/items?search=ÜBER` eine Anfrage ein
2. `ItemsController.Get()` empfängt die Anfrage mit `search="ÜBER"`
3. Methode ruft für jeden Medientyp die Hilfsmethode `Get*EntriesAsync()` auf (z. B. `GetMovieEntriesAsync()`)
4. Hilfsmethode konstruiert LINQ-Query und ruft `ApplySearchFilter<T>(query, "ÜBER")` auf
5. `ApplySearchFilter<T>()` führt `query.Where(e => EF.Functions.Like(AppDbFunctions.LowerInvariant(e.Name), ..., lowered))` aus
   - `AppDbFunctions.LowerInvariant(e.Name)` wird zu SQL-Aufruf der benutzerdefinierten Funktion `lower_invariant(Name)`
   - Diese Funktion führt `.ToLowerInvariant()` auf dem Wert in der Datenbank aus (z. B. "Über den Wolken" → "über den wolken")
6. LIKE-Vergleich findet Match zwischen generiertem `"über"` (aus Suchbegriff) und `"über den wolken"` (aus DB)
7. Treffer wird als `MediaEntryDto` zurückgegeben

Beteiligte Klassen/Komponenten: `ItemsController`, `ApplySearchFilter<T>()`, `AppDbFunctions`, `ApplicationDbContext`, `EF.Functions.Like()`

## Neue Klassen

| Klasse | Typ | Zweck |
|--------|-----|-------|
| `AppDbFunctions` | Helferklasse (statisch) | Registriert und deklariert die benutzerdefinierte Datenbankfunktion `LowerInvariant()`, die `.ToLowerInvariant()` für case-insensitive Vergleiche in SQLite aufruft. |

## Änderungen an bestehenden Klassen

### `ApplicationDbContext` (DbContext)

- **Neue Methode:** `OnModelCreating()` — Bereits vorhanden, muss um `modelBuilder.HasDbFunction(typeof(AppDbFunctions).GetMethod(nameof(AppDbFunctions.LowerInvariant)))` erweitert werden, um die benutzerdefinierte Funktion zu registrieren.
- **Geänderte Methode:** `OnModelCreating()` — Nach dem existierenden `ApplyConfigurationsFromAssembly()` wird die DbFunction-Registrierung hinzugefügt.

### `ItemsController` (Controller)

- **Geänderte Methode:** `ApplySearchFilter<T>()` (Zeile 209–216) — Die aktuelle where-Klausel `.Where(e => e.Name.ToLower().Contains(lowered))` wird umgeschrieben zu `.Where(e => EF.Functions.Like(AppDbFunctions.LowerInvariant(e.Name), $"%{lowered}%"))`, um die Unicode-sichere Funktion zu nutzen.
  - Grund für Änderung: Nutzung der benutzerdefinierten Funktion `AppDbFunctions.LowerInvariant()`, die in der Datenbank `.ToLowerInvariant()` ausführt, statt SQLites unzureichender `lower()`-Funktion.

## Datenbankmigrationen

Keine. Die Lösung ändert keine Tabellenstruktur oder Spalten — nur die Logik bei Such-Vergleichen.

## Validierungsregeln

Keine. Der Suchparameter akzeptiert bereits beliebige Strings; keine neue Validierung erforderlich.

## Konfigurationsänderungen

Keine. Die Lösung ist rein technisch (Datenbankfunktion + LINQ-Ausdruck) und benötigt keine Konfigurationseinträge.

## Seiteneffekte und Risiken

- **Bereich: Such-Endpoints:** Die Änderung in `ApplySearchFilter<T>()` wirkt sich auf **alle Suchen über alle fünf Medientypen** aus (Movie, TVShow, TVShowSeason, TVShowEpisode, MovieCollection), die der öffentliche Endpoint `/api/items` nutzt. Dies ist gewünscht, aber kritisch zu testen.
- **Bereich: Performance:** Die Nutzung einer benutzerdefinierten Datenbankfunktion kann in großen Datenbeständen zu geringen Performance-Unterschieden führen. SQLite-`lower()` ist primitiver, aber `LowerInvariant()` ist vollständiger. Dies ist akzeptabel, da funktionale Korrektheit Vorrang hat.
- **Bereich: ASCII-Kompatibilität:** Die neue Implementierung muss für ASCII-Titel weiterhin korrekt funktionieren (Rückwärts­kompatibilität). Bestehende ASCII-Tests müssen alle bestehen.
- **Bereich: Abhängigkeit EF Core DbFunction:** Die Lösung hängt von EF Core's DbFunction-Mechanismus ab. Diese API ist stabil (EF Core 5.0+), daher kein Risiko.

## Umsetzungsreihenfolge

1. **Helferklasse `AppDbFunctions` erstellen**
   - Voraussetzungen: .NET 10.0+ (vorhanden), Entity Framework Core (vorhanden)
   - Beschreibung: Neue Datei `VideoWebPlayer/Data/AppDbFunctions.cs` erstellen. Klasse enthält statische Methode `LowerInvariant(string? input)`, die `.ToLowerInvariant()` aufruft und mit dem `[DbFunction("lower_invariant")]`-Attribut deklariert wird (so dass EF Core die Methode zur benutzerdefinierten SQLite-Funktion `lower_invariant` zuordnet).

2. **DbFunction im `ApplicationDbContext` registrieren**
   - Voraussetzungen: `AppDbFunctions` existiert (Schritt 1 abgeschlossen)
   - Beschreibung: In `ApplicationDbContext.OnModelCreating()` nach dem existierenden `ApplyConfigurationsFromAssembly()`-Aufruf die Zeile `modelBuilder.HasDbFunction(typeof(AppDbFunctions).GetMethod(nameof(AppDbFunctions.LowerInvariant)))` hinzufügen.

3. **`ItemsController.ApplySearchFilter<T>()` anpassen**
   - Voraussetzungen: `AppDbFunctions` existiert und ist registriert (Schritte 1–2 abgeschlossen)
   - Beschreibung: Die Methode wird von `.Where(e => e.Name.ToLower().Contains(lowered))` zu `.Where(e => EF.Functions.Like(AppDbFunctions.LowerInvariant(e.Name), $"%{lowered}%"))` umgeschrieben. Der Suchbegriff `lowered` wird weiterhin in C# mit `.ToLower()` gefaltet.

4. **Neue Unit-Tests für Umlaut-Szenarien schreiben**
   - Voraussetzungen: `ItemsControllerTests_Search` existiert (vorhanden), `ApplySearchFilter()` ist angepasst (Schritt 3 abgeschlossen)
   - Beschreibung: Neue Testmethoden in `ItemsControllerTests_Search` hinzufügen:
     - `Get_SearchMovies_UnicodeUmlaut_UpperCase_Ueber()` — Suche nach "ÜBER", erwartet Treffer für "Über den Wolken"
     - `Get_SearchMovies_UnicodeUmlaut_LowerCase_ueber()` — Suche nach "über", erwartet Treffer für "Über den Wolken"
     - `Get_SearchMovies_UnicodeUmlaut_MixedCase_Ueber()` — Suche nach "Über", erwartet Treffer
     - `Get_SearchMovies_UnicodeEszett_UpperCase()` — Suche nach "ESZETT", erwartet Treffer für Titel mit Eszett
     - `Get_SearchMovies_UnicodeEszett_LowerCase()` — Suche nach "eszett", erwartet Treffer
     - `Get_SearchMovies_UnicodeUmlaut_Aerzte()` — Suche nach "ärzte" / "ÄRZTE" / "Ärzte"
     - `Get_SearchMovies_UnicodeUmlaut_Moerder()` — Suche nach "mörder" / "MÖRDER" / "Mörder"
     - Zusätzlich: Testmethoden für alle fünf Medientypen (TVShow, TVShowSeason, TVShowEpisode, MovieCollection) mit jeweils mindestens einem Umlaut-Szenario.
   - Verwendung existierender Hilfsmethoden: `CreateMovieAsync()`, `CreateTVShowAsync()`, `CreateSeasonAsync()`, `CreateEpisodeAsync()` — können direkt mit Umlaut-Titeln aufgerufen werden.

5. **Rückwärts­kompatibilität validieren**
   - Voraussetzungen: Alle bisherigen Schritte abgeschlossen
   - Beschreibung: Existierende 16 ASCII-Tests in `ItemsControllerTests_Search` ausführen und verifizieren, dass alle bestehen. Keine Code-Änderungen an Tests nötig, nur Ausführung und Bestätigung.

## Tests

### Neue Tests

| Test / Hilfsmethode | Testklasse | Was wird geprüft / bereitgestellt? |
|--------------------|------------|-------------------------------------|
| `Get_SearchMovies_UnicodeUmlaut_UpperCase_Ueber()` | `ItemsControllerTests_Search` | Suche nach "ÜBER" findet "Über den Wolken" |
| `Get_SearchMovies_UnicodeUmlaut_LowerCase_ueber()` | `ItemsControllerTests_Search` | Suche nach "über" findet "Über den Wolken" |
| `Get_SearchMovies_UnicodeUmlaut_MixedCase_Ueber()` | `ItemsControllerTests_Search` | Suche nach "Über" findet "Über den Wolken" |
| `Get_SearchMovies_UnicodeEszett_UpperCase()` | `ItemsControllerTests_Search` | Suche nach "ESZETT" findet Titel mit ß |
| `Get_SearchMovies_UnicodeEszett_LowerCase()` | `ItemsControllerTests_Search` | Suche nach "eszett" findet Titel mit ß |
| `Get_SearchMovies_UnicodeUmlaut_Aerzte_UpperCase()` | `ItemsControllerTests_Search` | Suche nach "ÄRZTE" findet "Ärzte" |
| `Get_SearchMovies_UnicodeUmlaut_Aerzte_LowerCase()` | `ItemsControllerTests_Search` | Suche nach "ärzte" findet "Ärzte" |
| `Get_SearchMovies_UnicodeUmlaut_Moerder_UpperCase()` | `ItemsControllerTests_Search` | Suche nach "MÖRDER" findet "Der Mörder ist da" |
| `Get_SearchMovies_UnicodeUmlaut_Moerder_LowerCase()` | `ItemsControllerTests_Search` | Suche nach "mörder" findet "Der Mörder ist da" |
| `Get_SearchTVShows_UnicodeUmlaut()` | `ItemsControllerTests_Search` | TVShow-Suche mit Umlauten funktioniert |
| `Get_SearchSeasons_UnicodeUmlaut()` | `ItemsControllerTests_Search` | TVShowSeason-Suche mit Umlauten funktioniert |
| `Get_SearchEpisodes_UnicodeUmlaut()` | `ItemsControllerTests_Search` | TVShowEpisode-Suche mit Umlauten funktioniert |
| `Get_SearchMovieCollections_UnicodeUmlaut()` | `ItemsControllerTests_Search` | MovieCollection-Suche mit Umlauten funktioniert |

### Betroffene bestehende Tests

| Test / Testklasse | Grund der Anpassung |
|-------------------|---------------------|
| Keine | Alle 16 existierenden ASCII-basierten Tests in `ItemsControllerTests_Search` benötigen keine Änderungen; sie werden mit der neuen Implementierung weiterhin bestehen (Rückwärts­kompatibilität). |

### E2E-Tests (primärer Funktionsnachweis)

Die vorliegende Anforderung betrifft den Such-Endpoint `/api/items?search=...`, der über HTTP erreichbar ist. Eine end-to-end Validierung über den Netzwerk-Endpoint ist erforderlich, um sicherzustellen, dass:
1. Der Such-String korrekt übertragen wird
2. Die Datenbankabfrage korrekt konstruiert wird
3. Die Antwort mit den erwarteten Treffern vorliegt

Eine bloße Unit- oder Integrationstests deckt nicht ab, dass die HTTP-Schicht, die Datenbank­verbindung und die LINQ-Übersetzung zusammen korrekt funktionieren.

| Priorität | Szenario | Testdatei / Testklasse | Abgedecktes Akzeptanzkriterium | Warum E2E nötig ist |
|-----------|----------|------------------------|-------------------------------|-------------------|
| Pflicht | Suche nach "ÜBER" findet "Über den Wolken"-Film über `/api/items?search=ÜBER` | `ItemsControllerTests_Search` (Integration) | Suche nach uppercase Umlaut findet Titel | Der Such-Request muss über den echten Controller-Endpoint laufen, um zu validieren, dass HTTP-Parsing, LINQ-Konstruktion und DB-Abfrage zusammenarbeiten. |
| Pflicht | Suche nach "über" findet "Über den Wolken"-Film | `ItemsControllerTests_Search` (Integration) | Suche nach lowercase Umlaut findet Titel | Validiert, dass die Unicode-Normalisierung bidirektional funktioniert. |
| Pflicht | Suche nach "eszett" findet Titel mit ß | `ItemsControllerTests_Search` (Integration) | Eszett-Matching funktioniert | Spezialfall des deutschen Zeichensatzes; muss explizit getestet sein. |
| Pflicht | Suche mit Umlauten findet Treffer in allen 5 Medientypen (Movie, TVShow, Season, Episode, MovieCollection) | `ItemsControllerTests_Search` (Integration) | Alle Medientypen unterstützen Unicode-Suche | Anforderung nennt alle fünf Typen explizit; alle müssen funktionieren. |
| Standard | ASCII-Titel ("Breaking Bad") werden mit bestehenden Tests weiterhin gefunden | `ItemsControllerTests_Search` (16 existierende Tests) | Rückwärts­kompatibilität | Einschränkungen durch neue Logik darf es nicht geben. |

**Anmerkung:** Alle Tests sind in der Testklasse `ItemsControllerTests_Search` integriert (nicht separat als E2E-Suite ausgelagert), da die Testinfrastruktur bereits in-memory SQLite-Datenbanken mit kontrollierten Testdaten nutzt. Dies ist für diese Anforderung ausreichend und schneller als volle Browser-E2E-Tests.

Welche bestehenden E2E-Tests müssen angepasst werden?

| Test / Testklasse | Grund der Anpassung |
|-------------------|---------------------|
| Keine | Keine anderen E2E-Testsuiten sind betroffen. `ItemsControllerTests_Search` deckt die Such-Funktionalität ab. |

## Offene Punkte

Keine. Alle technischen und fachlichen Entscheidungen sind geklärt:

1. **Gewählter Implementierungsansatz:** EF Core `DbFunction`-Attribut (gelöst durch Anforderungsdokument und Best Practice)
2. **Funktionsregistrierung:** In `ApplicationDbContext.OnModelCreating()` (gelöst durch EF Core-Standard)
3. **Änderungsumfang:** Zentrale Methode `ApplySearchFilter<T>()` ändert sich minimal; Auswirkung auf alle fünf Medientypen ist gewünsch (gelöst durch Analyse der Bestandsaufnahme)
4. **Test-Abdeckung:** Neue Tests mit Umlauten für alle fünf Medientypen; Rückwärts­kompatibilität validieren (gelöst durch Anforderungsdokument und Testplan)
