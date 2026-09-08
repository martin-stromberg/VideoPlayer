# Bestandsaufnahme: Namenssuche für Playlist-Einträge

Diese Bestandsaufnahme analysiert die bestehende Implementierung der Playlist-Funktionalität bezogen auf die Anforderung, die manuelle Medienauswahl (aktuell: Dropdown + ID-Eingabefeld) durch eine Namenssuche/Auswahl-Oberfläche zu ersetzen, die alle fünf Medientypen unterstützt.

## Zusammenfassung

### Wesentliche Befunde

**Vorhandene Komponenten und Funktionalität:**
- PlaylistEntriesList.razor zeigt derzeit ein Medientyp-Dropdown und ein ID-Eingabefeld (Zeilen 22–30) für die manuelle Eintragserfassung
- ItemsController.Get durchsucht aktuell nur MovieCollections und TVShows; Movies, TVShowSeasons und TVShowEpisodes sind nicht enthalten
- MediaEntryDto existiert mit den notwendigen Feldern (Type, Id, Title, PictureId) für Such/Anzeige
- PlaylistService.AddMediaToPlaylistAsync verarbeitet alle 5 Medientypen korrekt, inklusive Kaskaden-Auflösung und Duplikat-Handling
- IUnlockedMediaService.IsAccessible prüft bereits den Zugriff für alle Medientypen
- MediaBox.razor bietet OnActionSelected EventCallback für Benutzerinteraktionen
- Actors.razor demonstriert ein bewährtes Suchmuster: oninput-Bindung, Lade-Indikator, Live-Suche

**Identifizierte Lücken:**
1. ItemsController.Get unterstützt nur 2 von 5 Medientypen – Movie, TVShowSeason, TVShowEpisode fehlen
2. VideoWebPlayerClient hat keine Suchmethode speziell für Playlist-Medien-Suche
3. MediaSearchSelector.razor Komponente existiert nicht – muss neu erstellt werden
4. Keine bestehenden Tests für die erweiterte ItemsController.Get-Funktionalität mit Movies/Seasons/Episodes

### Test-Ausgangszustand

**Gesamttest-Zusammenfassung:**
- **Alle Tests bestanden:** 460 von 460 (391 Unit + 69 E2E)
- **Betroffene E2E Tests für diese Anforderung:**
  - PlaylistDetailE2ETests: ~20 Tests (testen Eingabe-UI via PlaylistEntriesList.razor)
  - PlaylistEntriesE2ETests: mehrere (testen Add/Remove mit aktuellem Dropdown + ID-Input)
  - PlaylistReorderE2ETests: mehrere (testen Drag & Drop)

**Lauf 1 – Unit Tests (Category!=E2E):**
- Befehl: `dotnet test VideoWebPlayer.Tests/VideoWebPlayer.Tests.csproj -c Release --filter "Category!=E2E"`
- Zeitpunkt: 2026-09-08, 03:30 UTC
- Branch: task/issue-207-fd729906880143ee8539fa3e046c88f4-playlists-fuer-serien-staffeln-korrektur-auswahl-suche-inhalte-hinzufuegen
- Commit: 30a6176c0f8636f38563eb0cb6eaccfbf9261537
- Gesamttests: 391
- Bestanden: 391
- Fehlgeschlagen: 0
- Übersprungen: 0
- [Nachweis](inventory/test-results/unit-tests.log)

**Lauf 2 – Integration Tests:**
- Keine Tests mit Category=Integration gefunden

**Lauf 3 – E2E Tests (Category=E2E):**
- Befehl: `dotnet test VideoWebPlayer.Tests/VideoWebPlayer.Tests.csproj -c Release --filter "Category=E2E"`
- Zeitpunkt: 2026-09-08, 03:45 UTC
- Gesamttests: 69
- Bestanden: 69
- Fehlgeschlagen: 0
- Übersprungen: 0
- Gesamtdauer: 2 Minuten 41 Sekunden
- [Nachweis](inventory/test-results/e2e-tests.log)

### Relevante bestehende Tests

Die folgenden Test-Dateien sind relevant und könnten beim Ändern der Razor-Komponente und des Controllers betroffen sein:
- `VideoWebPlayer.Tests/Controllers/PlaylistsControllerTests_Entries.cs` – Tests für AddMediaToPlaylist, RemoveMediaFromPlaylist, GetPlaylistEntries
- `VideoWebPlayer.Tests/Services/PlaylistServiceTests_AddMedia.cs` – Tests für AddMediaToPlaylistAsync mit verschiedenen Medientypen
- `VideoWebPlayer.Tests/PlaylistEntriesE2ETests.cs` – E2E-Tests für die Einträge-UI

## Details

### Datenmodelle und DTOs

- [Models und DTOs](inventory/models-dtos.md)

### Controller und Service-Logik

- [Backend-APIs und Logik](inventory/controllers-services.md)

### Razor-Komponenten und UI

- [Razor-Komponenten](inventory/razor-components.md)

### Client-API

- [VideoWebPlayerClient und IPlaylistApiClient](inventory/client-api.md)

### Enums und Konstanten

- [Enums und Media-Typ-Konstanten](inventory/enums-constants.md)

### Tests

- [Test-Details und Baseline](inventory/tests.md)
