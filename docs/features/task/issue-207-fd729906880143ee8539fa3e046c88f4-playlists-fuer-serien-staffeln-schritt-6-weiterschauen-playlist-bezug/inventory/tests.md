# Test-Ausgangszustand vor der Umsetzung

## Test-Ausgangszustand

- **Zeitpunkt (mit Zeitzone):** 2026-09-14 02:53 UTC (während der Agent-Ausführung)
- **Branch und Commit-ID:** `task/issue-207-fd729906880143ee8539fa3e046c88f4-playlists-fuer-serien-staffeln-schritt-6-weiterschauen-playlist-bezug` (HEAD: `a86fff3 fix: Nachbesserung Weiterschauen mit Playlist-Bezug (Schritt 6, Runde 1)`)
- **Uncommittete Änderungen im getesteten Stand:**
  - Modified: `docs/projects/task/issue-207-fd729906880143ee8539fa3e046c88f4-playlists-fuer-serien-staffeln/acceptance-schritt-6.md`
  - Untracked: `docs/features/task/` (neu)
  - Untracked: `docs/projects/task/issue-207-fd729906880143ee8539fa3e046c88f4-playlists-fuer-serien-staffeln/acceptance-schritt-6.1.md`
- **Testumgebung und Runtime-/SDK-Versionen:** 
  - .NET 10.0.12
  - xUnit.net VSTest Adapter v3.1.5
  - Betriebssystem: Windows 11 Pro
  - Test-Framework: bUnit (für Blazor-Component-Tests), Xunit
- **Ermittelte Testsuiten und Quellen der Testbefehle:**
  - Befehl: `dotnet test --logger "console;verbosity=quiet" --no-build --no-restore`
  - Arbeitsverzeichnis: `D:\Repositories\softwareschmiede\fd729906-8801-43ee-8539-fa3e046c88f4`
  - Suites: `MarkdownLinkCheck.Tests` (6 Tests), `VideoWebPlayer.Tests` (592 Tests)

### Testläufe

| Lauf | Befehl inkl. Filter | Arbeitsverzeichnis | Exit-Code | Erfolgreich | Fehlgeschlagen | Übersprungen | Nachweis |
|------|--------------------|--------------------|-----------|-------------|----------------|--------------|----------|
| Runde 1 (Baseline vom 13.09.23:07) | `dotnet test` (VSTest) | `D:\Repositories\...` | 0 | 592 | 0 | 0 | Nachweis-Datei aus vorherigem (bereits abgeschlossenem und bereinigtem) Lifecycle-Lauf nicht mehr vorhanden; siehe Runde 2 für aktuellen Nachweis. |
| Runde 2 (Aktuelle Bestandsaufnahme) | `dotnet test --logger "console;verbosity=quiet" --no-build --no-restore` | `D:\Repositories\...` | 0 | 598 (6 MarkdownLinkCheck + 592 VideoWebPlayer) | 0 | 0 | [initial-test-run.log](test-results/initial-test-run.log) |

**Zusammenfassung:** Alle Tests bestanden erfolgreich. Es gibt keine vorangekündigten Testfehler im Ausgangszustand.

## Nachgewiesene bestehende Testfehler

Keine bestehenden Testfehler nachgewiesen. Alle 592 Tests der VideoWebPlayer.Tests und alle 6 Tests der MarkdownLinkCheck.Tests bestanden.

## Testlücken und Ausführungsprobleme

Keine bekannten Build-, Setup- oder Infrastrukturfehler.

**Wichtiger Befund für Schritt 6, Runde 2:** 
Es gibt einen Regressions-Test in `PlaylistDetailTests`, der spezifisch auf die aktuelle Anforderung zielt:

- **Test:** `PlaylistDetailTests.StartPlaybackAsync_OnSuccess_PassesStartPositionSecondsToVideoPlayer()` (Zeilen 93-122)
- **Status:** Dieser Test **bestanden** derzeit, weil er nur den Initial-Start prüft
- **Limitierung:** Dieser Test prüft NICHT die kritische Regression: dass `StartPositionSeconds` nach einem Entry-Wechsel (Next/Previous/Advance/Restart) im Parent nicht aktualisiert wird

Es gibt E2E-Tests in `PlaylistDetailE2ETests.cs`, aber diese testen hauptsächlich UI-Navigation und Zugriffskontrolle, nicht die `StartPositionSeconds`-Logik.

## Testklassen

### `PlaylistDetailTests`
Datei: `VideoWebPlayer.Tests/Components/PlaylistDetailTests.cs`

Regressions-Tests für PlaylistDetail-Komponente (bUnit).

| Test | Beschreibung |
|------|-------------|
| `StartPlaybackAsync_OnFailure_SetsPlaybackErrorNotLoadError()` | Prüft, dass Fehler bei `StartPlaylistAsync` in `playbackError` statt `loadError` landen |
| `StartPlaybackAsync_OnSuccess_ClearsPreviousPlaybackError()` | Prüft, dass erfolgreicher Start vorherige Fehler löscht |
| `StartPlaybackAsync_OnSuccess_PassesStartPositionSecondsToVideoPlayer()` | **RELEVANT FÜR SCHRITT 6:** Prüft, dass `StartPositionSeconds` vom DTO an VideoPlayer übergeben wird. Dies ist ein Test für Initial-Start, nicht für Entry-Wechsel! |

### `PlaylistDetailE2ETests`
Datei: `VideoWebPlayer.Tests/PlaylistDetailE2ETests.cs`

End-to-End-Tests mit Playwright.

| Test | Beschreibung |
|------|-------------|
| `Load_Detail_Page_Unauthenticated_Shows_Error()` | Authentifizierungs-Fehlerbehandlung |
| `Load_Detail_Page_ValidPlaylist_ShowsMetadata()` | Anzeige von Playlist-Metadaten |
| `Open_Button_In_List_Navigates_To_Detail()` | Navigation |
| `Detail_Page_Edit_Opens_Form_And_Saves()` | Bearbeitung |
| `Detail_Page_Delete_Shows_Confirmation_And_Deletes()` | Löschung |
| `Detail_Page_Back_Button_Navigates_To_List()` | Rück-Navigation |
| `Detail_Page_Foreign_Playlist_Shows_403_Error()` | Zugriffskontrolle (fremde Playlist) |
| `Detail_Page_Nonexistent_Playlist_Shows_404_Error()` | Nicht existierende Playlist |
| `PlaylistDetail_LoadsFirstPage_OnInitialize()` | Virtualisierung, erste Seite |
| `PlaylistDetail_LoadsNextPage_OnScrollNearEnd()` | Virtualisierung, nächste Seite |
| `PlaylistDetail_StopsLoading_WhenHasNextPageFalse()` | Virtualisierung, keine weiteren Seiten |
| `PlaylistDetail_DoesNotShowReducedOpacity_WhenEntryIsUnlocked()` | Zugriffsstatus (freigeschaltet) |
| `PlaylistDetail_ShowsReducedOpacity_WhenEntryNotAccessible()` | Zugriffsstatus (gesperrt) |
| `PlaylistDetail_RemoveButton_EnabledForAllEntries()` | Entfernen-Button (auch für gesperrte) |
| `PlaylistDetail_DoesNotShowReducedOpacity_WhenUserHasSourceAccess()` | Quellenzugriff |
| `PlaylistDetail_DoesNotShowReducedOpacity_WhenMovieCollectionIsUnlocked()` | Sammlung-Zugriff |
| `PlaylistDetail_DoesNotShowReducedOpacity_WhenEpisodeShowIsUnlocked()` | Episode-Show-Zugriff |
| `PlaylistDetail_DisplaysImageForEachEntry()` | Bildanzeige |

**Befund:** Keine E2E-Tests für die kritische Regression (StartPositionSeconds bei Entry-Wechsel).

## Hilfsmethoden und Test-Fixtures

### `PlaylistDetailTests`

| Methode | Beschreibung |
|---------|-------------|
| `CreatePlaylistClientMock()` | Erstellt ein vorkonfiguriertes `IPlaylistApiClient`-Mock mit Standard-Playlist-Responses. Hinterlässt nur `StartPlaylistAsync` zum Konfigurieren. |
| `CreateTestContext(Mock<IPlaylistApiClient> playlistClientMock)` | Erstellt einen bUnit `TestContext` mit allen Dependencies für `PlaylistDetail`. Setzt Autorisierung, registriert Mocks. |

### `PlaylistDetailE2ETests`

Erbt von `PlaylistsE2ETestBase`, das wahrscheinlich Playwright-Setup und Hilfsmethoden bereitstellt:
- `LoginAsync(string email)` — Authentifizierung
- `CreatePlaylistViaUiAsync(...)` — Playlist erstellen
- `SeedMoviesIntoPlaylistAsync(...)` — Test-Daten
- `UnlockMediaForUserAsync(...)` — Zugriffsstatus setzen
- Etc.

## Weitere Test-relevante Dateien

### `ContinueWatchingServicePlaylistTests` (Playlist-spezifische Tests für Continue Watching)
Datei: `VideoWebPlayer.Tests/Services/ContinueWatchingServicePlaylistTests.cs`

| Test | Beschreibung |
|------|-------------|
| `Playlist_CreateEntry_WithSamePlaylistId_UpdatesExisting()` | Prüft, dass `CreateEntry` mit gleicher `PlaylistId` Updates statt Neuanlage macht |
| `Playlist_CreateEntry_WithDifferentPlaylistIds_AllowsBoth()` | Prüft Trennung nach `PlaylistId` |
| `Playlist_CreateEntry_WithAndWithoutPlaylistId_Independent()` | Prüft Unabhängigkeit von Einträgen mit/ohne `PlaylistId` |
| `Playlist_ValidateOwnership_*` | Autorisierungsprüfungen |

**Relevanz:** Diese Tests validieren, dass die in Schritt 6 korrekt implementierte Trennung nach `PlaylistId` funktioniert. Sie sind ein guter Indikator, dass das Backend die Playlist-Spezifität bereits unterstützt.

### `PlaylistsControllerTests_*` (API-Tests)
Verschiedene Suites prüfen PlaylistsController-Endpoints:
- `PlaylistsControllerTests_Auth.cs` — Authentifizierung und Autorisierung
- `PlaylistsControllerTests_Create.cs` — Erstellen
- `PlaylistsControllerTests_Delete.cs` — Löschen
- `PlaylistsControllerTests_Entries.cs` — Einträge verwalten

Diese Tests sind für die Backend-Logik relevant, nicht direkt für die Front-End-Regression, aber sie dokumentieren den aktuellen API-Zustand.
