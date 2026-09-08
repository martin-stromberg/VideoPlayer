# Anforderung: Unicode-korrekte Namenssuche für Playlist-Medienauswahl

## Fachliche Zusammenfassung

Die case-insensitive Namenssuche beim Hinzufügen von Medieninhalten zu Playlists (über `GET /api/items?search=...`) findet Titel mit großgeschriebenen deutschen Umlauten (Ä, Ö, Ü) und dem Eszett (ß) nicht, selbst bei exakter Eingabe des Titels. Das Kern­problem ist die Diskrepanz zwischen der vollständigen Unicode-Faltung in C# und der unzureichenden `lower()`-Funktion von SQLite ohne ICU-Erweiterung: Der Suchbegriff wird in C# mit `.ToLower()` korrekt zu `ü` gefaltet, aber der Spaltenwert in der Datenbank bleibt in seiner Original­schreibweise (z. B. `Über den Wolken`), sodass die LIKE-/INSTR-Vergleich fehlschlägt. Dies ist für eine deutschsprachige Anwendung praxisrelevant und keine Rand­fälligkeit.

Die Lösung muss Umlaute unabhängig von Groß-/Kleinschreibung korrekt finden, ohne die Faltung der SQLite-Engine zu überlassen. Eine bestehende, über `SqliteConnection.CreateFunction()` registrierte benutzerdefinierte Funktion oder ein via EF Core ([`DbFunction`], `ModelBuilder.HasDbFunction()`) im LINQ-Ausdruck verfügbarer Mechanismus ist erforderlich, der intern `.ToLowerInvariant()` (volle .NET-Unicode-Faltung) nutzt.

## Betroffene Klassen und Komponenten

### Datenmodellklassen
- `MediaBaseEntry` (Basisklasse für alle Medientypen; `Name`-Eigenschaft)
- Abgeleitete Klassen: `Movie`, `TVShow`, `TVShowSeason`, `TVShowEpisode`, `MovieCollection`

### Logikklassen / Services
- `ItemsController.ApplySearchFilter<T>()` — Die Such­filter-Methode (aktuell in Zeile 209–216), die die LINQ-where-Klausel konstruiert
- `ApplicationDbContext` — Databankkontext, wo ggf. benutzerdefinierte Funktionen registriert werden

### Schnittstellen / Abstraktion
- Falls eine `DbFunction` eingeführt wird: neuer, über Dependency Injection erreichbarer Dienst oder statische Hilfsklasse für die Funktion

### Tests
- `VideoWebPlayer.Tests/Controllers/ItemsControllerTests.cs` (oder entsprechende Test­klasse) — Benötigt neue Test­fälle mit deutschen Umlauten in verschiedenen Schreibweisen (z. B. Titel in DB: "Über den Wolken", "Der Mörder ist da", "Ärzte"; Suchbegriffe: "über", "ÜBER", "Über", "mörder", "MÖRDER", "ärzte", "ÄRZTE")
- Eventuell: Spezielle Test­suite für die Unicode-Faltungs­logik

## Implementierungsansatz

1. **Bestehende Stelle ermitteln**: Die `ApplySearchFilter<T>`-Methode (Zeile 209–216 in `ItemsController.cs`) erzeugt aktuell einen LINQ-where-Ausdruck mit `.ToLower().Contains()`, der von EF Core zu SQLites `lower()`-Funktion übersetzt wird.

2. **Benutzerdefinierte Funktion registrieren**: 
   - Option A: Statische Hilfsklasse `AppDbFunctions` (o. ä.) mit einer öffentlichen static-Methode `LowerInvariant(string input)`, die `.ToLowerInvariant()` aufruft.
   - Option B: Registrierung via `SqliteConnection.CreateFunction()` in `ApplicationDbContext.OnConfiguring()` oder einem Initializer.
   - Option C: EF Core 7+ `DbFunction`-Attribut auf einer statischen Methode, kombiniert mit `ModelBuilder.HasDbFunction()`.

3. **LINQ-Ausdruck anpassen**: `ApplySearchFilter<T>` so umschreiben, dass statt `.ToLower()` die neue Funktion (`EF.Functions.Like()` mit benutzerdefinierter Funktion oder Datenbankfunktionsaufruf) genutzt wird.

4. **Tests ergänzen**: 
   - Unit-Tests in der vorhandenen Test­klasse für `ItemsController` / Such­filter
   - Test­daten mit deutschen Umlauten in verschiedenen Schreibweisen
   - Abdeckung aller fünf Medientypen (`Movie`, `TVShow`, `TVShowSeason`, `TVShowEpisode`, `MovieCollection`)

## Konfiguration

Keine externe Konfiguration erforderlich. Die Lösung ist rein technisch (Datenbankfunktion + LINQ-Ausdruck) und hat keine benutzer­seitigen Einstellungen.

## Offene Fragen

1. Welcher technische Ansatz ist für dieses Projekt am saubersten?
   - EF Core `DbFunction`-Attribut (modern, typsicher)?
   - Manuelle Registrierung via `SqliteConnection.CreateFunction()` (direkter, weniger Framework-abhängig)?
   - Ein bestehendes Pattern im Projekt, das bereits für SQLite-Funktionen verwendet wird?

2. Sollte die Lösung auch auf andere Such-/Filter-Szenarien im Projekt übertragen werden (z. B. Medienbib­liotheks-Suche in `ItemsController.Get()` für andere Medienquellen)?

3. Reicht ein `:contains`-Vergleich (aktuell via `instr()`), oder sind andere Matching-Strategien erforderlich (z. B. Präfix-Match, Phrase-Match)?

4. Sollen ältere Testdaten (ASCII-only) beibehalten oder durch deutsche Umlaute-Testdaten erweitert werden?

## Validierung der Lösung

Die Lösung muss nachweislich die folgenden Szenarien bestehen:

- **Titel "Über den Wolken":**
  - Suche nach "über", "ÜBER", "Über" → Fund
  - Suche nach "den", "DEN", "Den" → Fund
  
- **Titel "Ärzte":**
  - Suche nach "ärzte", "ÄRZTE", "Ärzte" → Fund
  
- **Titel "Der Mörder ist da":**
  - Suche nach "mörder", "MÖRDER", "Mörder" → Fund
  
- **Titel "Das Eszett ß Zeichen":**
  - Suche nach "eszett", "ESZETT", "Eszett" → Fund

- **Rückwärtskompatibilität:**
  - ASCII-Titel (z. B. "Breaking Bad") funktionieren weiterhin korrekt bei case-insensitiver Suche
