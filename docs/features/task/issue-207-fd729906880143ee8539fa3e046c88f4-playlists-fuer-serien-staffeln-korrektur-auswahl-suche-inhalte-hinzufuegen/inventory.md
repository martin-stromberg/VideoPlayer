# Bestandsaufnahme: Unicode-korrekte Namenssuche für Playlist-Medienauswahl

Diese Bestandsaufnahme analysiert die bestehende Code-Basis bezüglich der Anforderung, die case-insensitive Namenssuche beim Hinzufügen von Medieninhalten zu Playlists (über `GET /api/items?search=...`) für deutsche Umlaute und das Eszett korrekt zu unterstützen. Das Problem: Der Suchbegriff wird in C# mit `.ToLower()` korrekt zu `ü` gefaltet, aber die Datenbankwerte in SQLite bleiben in ihrer Original­schreibweise, sodass der Vergleich fehlschlägt.

## Zusammenfassung

### Vorhandene Komponenten

- **Such-Logik:** Die zentrale Such-Filter-Methode `ItemsController.ApplySearchFilter<T>()` (Zeile 209–216) implementiert derzeit eine case-insensitive Namenssuche mit `.ToLower().Contains()`, die zu SQLites `lower()`-Funktion übersetzt wird.
- **Datenmodell:** Fünf Medientypen erben alle die `Name`-Eigenschaft von `MediaBaseEntry`: `Movie`, `TVShow`, `TVShowSeason`, `TVShowEpisode`, `MovieCollection`.
- **Endpoints:** Die Suche wird von `ItemsController.Get()` aufgerufen und findet über alle fünf Medientypen statt (wenn `includeIndividualMediaTypes=true`).
- **Zugriffskontrolle:** Berechtigungen und Freischaltungen sind bereits implementiert und funktionieren.
- **Tests:** Testklasse `ItemsControllerTests_Search` mit 16 bestehenden Testfällen, alle für ASCII-Titel.

### Was fehlt

- **Keine Unicode-Tests:** Keine Testfälle mit deutschen Umlauten (Ä, Ö, Ü, ß).
- **Keine Lösung für das Core-Problem:** Keine benutzerdefinierte Datenbankfunktion für Unicode-sichere `lower()`-Faltung.
- **Keine Mechanismen zur Behebung:** Die Anforderung schlägt mehrere technische Ansätze vor (EF Core `DbFunction`, `SqliteConnection.CreateFunction()`, statische Hilfsklasse), aber kein Ansatz ist implementiert.

### Test-Ausgangszustand

Baseline: 16 bestandene Tests in `ItemsControllerTests_Search`, 0 Fehler, 0 übersprungen.
Fehlende Tests: Alle Szenarien mit Umlauten und Eszett.

Siehe [Test-Details](inventory/tests.md) für Nachweis.

## Details

- [Datenmodell](inventory/models.md) — Beschreibung der `MediaBaseEntry`-Hierachie und der `Name`-Eigenschaft als Such-Ziel
- [Logik](inventory/logic.md) — Beschreibung von `ItemsController.ApplySearchFilter<T>()` und `ApplicationDbContext`
- [Tests](inventory/tests.md) — Test-Ausgangszustand, bestehende Testfälle und Testlücken

## Kritische Erkenntnisse

1. **Zentrale Stelle:** `ItemsController.ApplySearchFilter<T>()` (Zeile 209–216) ist der einzige Ort, an dem die Such-Filter-Logik implementiert ist. Eine Änderung dort wirkt sich automatisch auf alle fünf Medientypen aus.

2. **Datenbank-Diskrepanz:** Der Suchbegriff wird in C# mit `.ToLower()` verarbeitet (vollständige Unicode-Faltung nach .NET-Standard), aber die Vergleiche laufen in SQLite mit `lower()` (unvollständig für Umlaute ohne ICU-Erweiterung).

3. **Keine bestehenden Custom Functions:** `ApplicationDbContext.OnModelCreating()` ruft nur `ApplyConfigurationsFromAssembly()` auf. Keine benutzerdefinierten Datenbankfunktionen sind registriert.

4. **Test-Basis stabil:** Alle 16 ASCII-basierten Tests bestehen. Die Test-Suite bietet eine gute Grundlage für neue Unicode-Tests; Hilfsmethoden wie `CreateMovieAsync()` können direkt mit Umlaut-Titeln genutzt werden.

5. **Volle Medientyp-Abdeckung erforderlich:** Die Anforderung nennt alle fünf Medientypen; `ApplySearchFilter<T>()` wird derzeit von allen fünf `Get*EntriesAsync`-Hilfsmethoden aufgerufen, was die volle Abdeckung sichert.
