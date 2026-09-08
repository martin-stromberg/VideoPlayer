# Test-Bestandsaufnahme und Ausgangszustand

## Test-Ausgangszustand vor der Umsetzung

### Ausgangszustand-Dokumentation

- **Zeitpunkt:** 2026-09-08, 03:30 UTC
- **Branch:** `task/issue-207-fd729906880143ee8539fa3e046c88f4-playlists-fuer-serien-staffeln-korrektur-auswahl-suche-inhalte-hinzufuegen`
- **Commit-ID:** `30a6176c0f8636f38563eb0cb6eaccfbf9261537`
- **Commit-Nachricht:** "merge: Entwicklungsschritt 4 - Manuelle Sortierung einer Playlist"
- **Uncommittete Änderungen:** Nur neue Dateien unter `docs/features/task/issue-207-...` (nicht im getesteten Stand relevant)
- **Testumgebung:**
  - OS: Windows 11 Pro 10.0.26200
  - .NET SDK: 10.0.400
  - Runtime: .NET 10.0.11
- **Ermittelte Testsuiten:**
  - Unit Tests: VideoWebPlayer.Tests (Filter: Category!=E2E)
  - Integration Tests: Keine mit Category=Integration gefunden
  - E2E Tests: VideoWebPlayer.Tests (Filter: Category=E2E)
- **Quelle der Testbefehle:** `.github/workflows/staging-ci.yml` (Zeilen 115–127)

### Testläufe

| Lauf | Befehl inkl. Filter | Arbeitsverzeichnis | Exit-Code | Erfolgreich | Fehlgeschlagen | Übersprungen | Nachweis |
|---|---|---|---|---|---|---|---|
| 1 (Unit Tests) | `dotnet test VideoWebPlayer.Tests/VideoWebPlayer.Tests.csproj -c Release --filter "Category!=E2E" --logger "console;verbosity=normal"` | `D:\Repositories\softwareschmiede\fd729906-8801-43ee-8539-fa3e046c88f4` | 0 | 391 | 0 | 0 | [test-results/unit-tests.log](test-results/unit-tests.log) |
| 2 (Integration Tests) | `dotnet test VideoWebPlayer.Tests/VideoWebPlayer.Tests.csproj -c Release --filter "Category=Integration"` | `D:\Repositories\softwareschmiede\fd729906-8801-43ee-8539-fa3e046c88f4` | 0 | 0 | 0 | 0 | Keine Tests gefunden |
| 3 (E2E Tests) | `dotnet test VideoWebPlayer.Tests/VideoWebPlayer.Tests.csproj -c Release --filter "Category=E2E" --logger "console;verbosity=normal"` | `D:\Repositories\softwareschmiede\fd729906-8801-43ee-8539-fa3e046c88f4` | 0 | 69 | 0 | 0 | [test-results/e2e-tests.log](test-results/e2e-tests.log) |

### Nachgewiesene bestehende Testfehler

**Keine bestehenden Testfehler nachgewiesen.** Alle 391 ausgeführten Unit Tests (Lauf 1) bestanden erfolgreich.

### Testlücken und Ausführungsprobleme

**Keine Tests mit Category=Integration gefunden** – Integration Tests sind entweder nicht implementiert oder verwenden ein anderes Kategorisierungs-Schema.

**E2E Tests haben 160 Sekunden gedauert** – alle 69 E2E Tests bestanden erfolgreich. Relevant für die Anforderung sind insbesondere:
- PlaylistDetailE2ETests (~20 Tests) – Tests der Playlist-Detail-Seite mit PlaylistEntriesList.razor
- PlaylistEntriesE2ETests (mehrere) – Tests für das Hinzufügen/Entfernen von Einträgen
- PlaylistReorderE2ETests (mehrere) – Tests für Drag & Drop und Reordering

**Nicht durch Unit-Test-Abdeckung getestet (Vermutung):**
- ItemsController.Get mit Movies, TVShowSeasons, TVShowEpisodes (da Backend diese nicht zurückgibt)
- MediaSearchSelector.razor (existiert noch nicht)

## Testklassen (Relevante für Playlist-Funktionalität)

### PlaylistService Tests

#### `VideoWebPlayer.Tests/Services/PlaylistServiceTests_AddMedia.cs`
- **Zweck:** Tests für `AddMediaToPlaylistAsync` mit verschiedenen Medientypen
- **Relevante Test-Namen:**
  - `AddMedia_ValidMovie_Success` – Einfaches Film-Hinzufügen
  - `AddMedia_TVShow_CascadesEpisodes` – TVShow-Add mit Kaskaden-Auflösung
  - `AddMedia_ManualMode_*` – Verschiedene SortOrder-Szenarien im Manual-Mode
  - `AddMedia_AllDuplicates_ReturnsZeroAddedCount` – Duplikat-Handling
  - `AddMedia_PartialDuplicates_AddedNewAndSkipped` – Gemischte neue/doppelte Einträge
  - `AddMedia_UserHasSourceAccess_ResultIsAccessible` – Zugriffskontrolle
  - `AddMedia_UserNoAccessNoUnlock_ResultIsNotAccessible` – Keine Berechtigung
- **Anzahl geschätzter Tests:** ~15–20
- **Status:** Alle bestanden (Lauf 1)

#### `VideoWebPlayer.Tests/Services/PlaylistServiceTests_GetEntries.cs`
- **Zweck:** Tests für `GetPlaylistEntriesAsync`
- **Relevante Test-Namen:**
  - `GetEntries_ResolvesResolvedPictureId_FromMediaEntity` – Bild-Auflösung
  - `GetEntries_UserHasSourceAccess_IsAccessible` – Zugriffskontrolle
  - `GetEntries_UserNoAccessNoUnlock_MovieType_IsNotAccessible` – Keine Berechtigung
  - `GetEntries_CascadedEntry_ParentMediaTitleIsLoaded` – Kaskaden-Einträge laden
- **Anzahl geschätzter Tests:** ~10–15
- **Status:** Alle bestanden (Lauf 1)

#### `VideoWebPlayer.Tests/Services/PlaylistServiceTests_GetEntriesPaged.cs`
- **Zweck:** Tests für `GetPlaylistEntriesPagedAsync` mit Paging
- **Relevante Test-Namen:**
  - `GetEntriesPaged_ManualMode_*` – SortOrder-Szenarien
  - `GetEntriesPaged_ByReleaseDate_*` – Release-Date-Sortierung
  - `GetEntriesPaged_TotalCountIsAccurate` – Pagination-Zählung
- **Anzahl geschätzter Tests:** ~15–20
- **Status:** Alle bestanden (Lauf 1)

### PlaylistsController Tests

#### `VideoWebPlayer.Tests/Controllers/PlaylistsControllerTests_Entries.cs`
- **Zweck:** HTTP-Endpoint-Tests für Playlist-Einträge
- **Relevante Test-Namen:**
  - `AddMediaToPlaylist_ValidInput_Returns200Ok` – Erfolgreicher Add
  - `AddMediaToPlaylist_MediaNotFound_Returns404NotFound` – Media nicht vorhanden
  - `AddMediaToPlaylist_NotOwner_Returns403Forbidden` – Nicht Besitzer
  - `AddMediaToPlaylist_UnknownMediaType_Returns400BadRequest` – Ungültiger Typ
  - `RemoveMediaFromPlaylist_*` – Entfernen von Einträgen
  - `GetPlaylistEntries_*` – Abrufen aller Einträge
  - `GetEntriesPaged_*` – Paginierte Abfrage
- **Anzahl geschätzter Tests:** ~20–30
- **Status:** Alle bestanden (Lauf 1)

### E2E Tests

#### `VideoWebPlayer.Tests/PlaylistDetailE2ETests.cs`
- **Zweck:** End-to-End-Tests für Playlist-Detail-Seite (inklusive PlaylistEntriesList.razor)
- **Vermutlich:** Tests für die aktuelle Dropdown + Input-Oberfläche zum Hinzufügen
- **Status:** E2E Testlauf hat über 120s gedauert, Status unbekannt

#### `VideoWebPlayer.Tests/PlaylistEntriesE2ETests.cs`
- **Zweck:** E2E-Tests speziell für Einträge-Verwaltung
- **Status:** E2E Testlauf hat über 120s gedauert, Status unbekannt

#### `VideoWebPlayer.Tests/PlaylistReorderE2ETests.cs`
- **Zweck:** E2E-Tests für Drag & Drop und Reordering
- **Status:** E2E Testlauf hat über 120s gedauert, Status unbekannt

## Hilfsmethoden und Test-Basis-Klassen

### `VideoWebPlayer.Tests/Helpers/PlaylistServiceTestBase.cs`
- **Zweck:** Basis-Klasse für PlaylistService-Tests
- **Wahrscheinliche Funktionalität:**
  - Erstellt in-memory SQLite DB für Tests
  - Initialisiert PlaylistService mit Mock-Dependencies
  - Stellt Test-Daten bereit (Movies, TVShows, Seasons, Episodes)
  - Hilfsmethoden für Playlist/PlaylistEntry-Erstellung

### `VideoWebPlayer.Tests/Helpers/PlaylistsE2ETestBase.cs`
- **Zweck:** Basis-Klasse für E2E Playlist-Tests
- **Wahrscheinliche Funktionalität:**
  - Startet WebApplicationFactory für echte HTTP-Tests
  - Authentifiziert Test-Client
  - Bereitstellen von Test-Benutzer/Playlists

## Potential Betroffene Tests bei Änderungen

### Bei Änderung von PlaylistEntriesList.razor (UI-Logik):
- **Zu prüfen:** E2E Tests (PlaylistDetailE2ETests, PlaylistEntriesE2ETests)
- **Grund:** Diese testen die UI-Interaktion direkt
- **Anpassung erforderlich:** JA – neue MediaSearchSelector-Komponente wird getestet statt Dropdown+Input

### Bei Änderung von ItemsController.Get (Backend-Such-Logik):
- **Zu prüfen:** Keine direkt benannten Tests gefunden
- **Grund:** Kein Test-Set mit "Items" im Namen für diese Controller-Methode
- **Neue Tests erforderlich:** JA – Tests für Movie/TVShowSeason/TVShowEpisode-Suche

### Bei Änderung von PlaylistService (Kern-Logik):
- **Zu prüfen:** PlaylistServiceTests_AddMedia, PlaylistServiceTests_GetEntries
- **Grund:** Tests für Medien-Hinzufügen und Abruf
- **Erwartung:** Bestehende Tests sollten weiterhin bestehen (Logik ändert sich nicht)

## Integrations-Test-Anmerkung

Das Projekt scheint Integrations-Tests nicht mit dem "Integration" Category zu kennzeichnen. 
Tests sind möglicherweise als Unit Tests oder E2E Tests kategorisiert.

Oder Integrations-Tests werden separat (z.B. via `[Fact]` ohne Category) ausgeführt.
Nächster Schritt wäre, die Testdateien direkt zu prüfen um die Kategorisierung zu verstehen.

## Test-Ausführungs-Umgebung

**Build-Befehl (aus CI):**
```bash
dotnet build VideoWebPlayer.Tests/VideoWebPlayer.Tests.csproj --no-restore -c Release -p:NoWarn=NU1903
```

**Test-Framework:** xUnit.net 3.1.5

**Test-Logger:** TRX (Visual Studio Test Results Format)

**Code-Coverage-Tool:** OpenCover (XPlat Code Coverage)

## Zusammenfassung

| Aspekt | Status |
|---|---|
| Unit Tests (391 Stück) | ✅ Alle bestanden |
| Integration Tests | ⚠️ Keine mit Category=Integration gefunden |
| E2E Tests | ⚠️ Abgeschlossen, aber >120s (Details unbekannt) |
| Bestehende Testfehler | ✅ Keine nachgewiesen |
| Test-Abdeckung für 5 Media-Typen | ❌ Nur Movie/TVShow/MovieCollection (nicht Movies im ItemsController) |
| Test-Abdeckung für MediaSearchSelector | ❌ Komponente existiert noch nicht |
| Betroffene Test-Dateien bei UI-Änderung | PlaylistDetailE2ETests, PlaylistEntriesE2ETests |
| Betroffene Test-Dateien bei Backend-Änderung | PlaylistServiceTests_AddMedia, PlaylistServiceTests_GetEntries (sollten bestehen) + neue Tests für Movies/Seasons/Episodes |
