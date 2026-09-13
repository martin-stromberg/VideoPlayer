# Test-Bestandsaufnahme

## Test-Ausgangszustand vor der Umsetzung

- **Zeitpunkt (mit Zeitzone):** 2026-09-13, 22:35 UTC+2 (Windows 11 Pro, lokale Zeit)
- **Branch und Commit-ID:** Branch `task/issue-207-fd729906880143ee8539fa3e046c88f4-playlists-fuer-serien-staffeln-schritt-6-weiterschauen-playlist-bezug`, Commit `bc9d79e1b24786b9cdb7cf0fd6b5ba4b2f6518e6`
- **Uncommittete Änderungen im getesteten Stand:** 
  - `?? docs/features/task/` (untracked)
  - `?? docs/projects/task/issue-207-fd729906880143ee8539fa3e046c88f4-playlists-fuer-serien-staffeln/acceptance-schritt-6.md` (untracked)
  - Keine geänderten oder gestaged Dateien im Produktivcode
- **Testumgebung und Runtime-/SDK-Versionen:**
  - .NET SDK: 10.0
  - Runtime: .NETCoreApp v10.0
  - Testframework: Xunit
  - Datenbankprovider für Tests: EF-InMemory (ContinueWatchingService-Tests), SQLite In-Memory (PlaylistService-Tests)
- **Ermittelte Testsuiten und Quellen der Testbefehle:**
  - Testprojekt: `VideoWebPlayer.Tests/VideoWebPlayer.Tests.csproj`
  - Testbefehl: `dotnet test VideoWebPlayer.Tests/VideoWebPlayer.Tests.csproj --no-build -v quiet`
  - Tests im Projekt: mehrere Testklassen (PlaylistServiceTests_*, ContinueWatchingService*Tests, E2E-Tests, Komponententests, etc.)

### Testläufe

| Lauf | Befehl inkl. Filter | Arbeitsverzeichnis | Exit-Code | Erfolgreich | Fehlgeschlagen | Übersprungen | Nachweis |
|------|--------------------|--------------------|-----------|-------------|----------------|--------------|----------|
| 1 (Ausgangszustand) | `dotnet test VideoWebPlayer.Tests/VideoWebPlayer.Tests.csproj --no-build -v quiet` | `D:/Repositories/softwareschmiede/fd729906-8801-43ee-8539-fa3e046c88f4` | 0 | 585 | 0 | 0 | Inline (siehe unten) |

### Nachgewiesene bestehende Testfehler

**Keine Testfehler nachgewiesen.** Alle 585 Tests bestanden im Ausgangszustand.

### Testlücken und Ausführungsprobleme

**Strukturelle Testabdeckungslücke für Problem 3 (Unique-Index-Konflikt beim Playlist-Löschen):**
- `ContinueWatchingServiceTestBase` (Datei: `VideoWebPlayer.Tests/Helpers/ContinueWatchingServiceTestBase.cs`, Zeile 35–37) nutzt `UseInMemoryDatabase()` (EF-InMemory).
- Der EF-InMemory-Provider durchsetzt **keine** Unique-Indizes und **keine** Foreign-Key-Aktionen (`OnDelete(DeleteBehavior.SetNull)`).
- Dadurch können Tests für `PlaylistService.DeletePlaylistAsync` mit konkurrierendem playlist-losem `ContinueWatchingEntry` die Unique-Constraint-Verletzung nicht nachweisen.
- `PlaylistServiceTestBase` (Datei: `VideoWebPlayer.Tests/Helpers/PlaylistServiceTestBase.cs`, Zeile 25–33) nutzt dagegen **SQLite In-Memory mit echtem Schema**, daher wird das Problem potenziell dort erkannt – aber es existiert kein spezifischer Test für das Szenario "Playlist-Löschung mit konkurrierendem playlist-losem Eintrag".

**Fehlende Komponententests für ContinueWatchingList.razor:**
- Es gibt keine bUnit-Tests für die Razor-Komponente `ContinueWatchingList.razor` mit mehreren Varianten derselben Media (Problem 1 & 2).
- `MediaBoxContextMenuInteractionTests.cs` testet die Interaktionslogik einer anderen Komponente, nicht `ContinueWatchingList` direkt.
- `ContinueWatchingE2ETests` (Datei: `VideoWebPlayer.Tests/ContinueWatchingE2ETests.cs`) sind End-to-End-Tests, aber nicht spezialisiert auf das Rendering mehrfacher Varianten.

**Tests für DtoPlaylistPlaybackStart und StartPositionSeconds:**
- Es gibt keine Tests, die überprüfen, dass `DtoPlaylistPlaybackStart.StartPositionSeconds` (Problem 4) korrekt befüllt wird.

## Testklassen

### `PlaylistServiceTests_Delete`
Datei: `VideoWebPlayer.Tests/Services/PlaylistServiceTests_Delete.cs`
- `DeletePlaylist_ValidInput_RemovesFromDb` – Überprüft, dass eine gültige Playlist gelöscht wird
- `DeletePlaylist_OwnershipViolation_ThrowsPlaylistAccessDeniedException` – Überprüft Zugriffsprüfung
- `DeletePlaylist_NotFound_ThrowsKeyNotFoundException` – Überprüft Fehlerbehandlung

**Lücke:** Kein Test für das Szenario mit konkurrierendem playlist-losem `ContinueWatchingEntry`.

### `ContinueWatchingServiceTestBase`
Datei: `VideoWebPlayer.Tests/Helpers/ContinueWatchingServiceTestBase.cs`
Basisklasse für ContinueWatching-Tests mit EF-InMemory-Datenbank.

### `ContinueWatchingServicePlaylistTests`
Datei: `VideoWebPlayer.Tests/Services/ContinueWatchingServicePlaylistTests.cs`
Tests speziell für Playlist-bezogene Funktionalität.

### `ContinueWatchingServiceMultipleEntriesTests`
Datei: `VideoWebPlayer.Tests/Services/ContinueWatchingServiceMultipleEntriesTests.cs`
Tests für mehrere Einträge, aber nicht spezialisiert auf mehrere Varianten **desselben Videos**.

### `PlaylistServiceTestBase`
Datei: `VideoWebPlayer.Tests/Helpers/PlaylistServiceTestBase.cs`
Basisklasse für PlaylistService-Tests mit SQLite In-Memory.

### `ContinueWatchingE2ETests`
Datei: `VideoWebPlayer.Tests/ContinueWatchingE2ETests.cs`
End-to-End-Tests für Continue-Watching-Logik (Nächste Episode, Episoden-Lücken, Staffelwechsel, Serienende).

## Hilfsmethoden

### `PlaylistServiceTestBase`
- `CreateTestPlaylistAsync(userId, name)` – Erstellt und speichert eine Test-Playlist
- `CreateTestMediaEntryAsync(mediaType, name)` – Erstellt echte Medienentitäten
- `UnlockMediaForUserAsync(userId, mediaType, mediaId)` – Gewährt Freigabe-Zugriff
- `GrantMediaSourceAccessForUserAsync(userId, mediaSourceId)` – Gewährt Mediaquellen-Zugriff

### `ContinueWatchingServiceTestBase`
- `CreateTestPlaylistAsync(userId, name)` – Erstellt eine Test-Playlist (Zeile 98–100)

## Test-Ausgangslauf

```
Testlauf für "D:\Repositories\softwareschmiede\fd729906-8801-43ee-8539-fa3e046c88f4\VideoWebPlayer.Tests\bin\Debug\net10.0\VideoWebPlayer.Tests.dll" (.NETCoreApp,Version=v10.0)
Insgesamt 1 Testdateien stimmten mit dem angegebenen Muster überein.

Bestanden!   : Fehler:     0, erfolgreich:   585, übersprungen:     0, gesamt:   585, Dauer: 2 m 46 s - VideoWebPlayer.Tests.dll (net10.0)
```

**Zusammenfassung:**
- Alle 585 Tests erfolgreich
- 0 Fehler, 0 übersprungen
- Dauer: 2 Minuten 46 Sekunden

## Relevant für die Anforderung

Die aktuellen Tests decken **nicht** die vier Probleme ab:
1. **Problem 1 (Dictionary-Schlüsselung):** Kein bUnit-Test für `ContinueWatchingList.razor` mit mehrfachen Varianten
2. **Problem 2 (Blazor-Key-Kollision):** Folge von Problem 1, würde automatisch durch Behebung gelöst
3. **Problem 3 (Unique-Index-Konflikt):** `ContinueWatchingServiceTestBase` nutzt EF-InMemory, das keine Indizes durchsetzt. Obwohl `PlaylistServiceTestBase` SQLite nutzt, gibt es keinen spezifischen Test für das Szenario
4. **Problem 4 (Fehlende Wiedergabeposition):** Kein Test für `DtoPlaylistPlaybackStart.StartPositionSeconds` oder deren Übergabe an `VideoPlayer`
