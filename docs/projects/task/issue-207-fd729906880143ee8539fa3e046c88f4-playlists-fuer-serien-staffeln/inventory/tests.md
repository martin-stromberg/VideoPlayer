# Bestehende Tests und Test-Hilfsmethoden

## Testklassen mit relevanten Patterns

### `ContinueWatchingE2ETests`
**Datei:** `VideoWebPlayer.Tests/ContinueWatchingE2ETests.cs`

End-to-End Tests für Weiterschauen-Funktionalität.

**Muster:**
- Verwendet `WebApplicationFactory<global::Program>` für In-Process-Server
- Temporary SQLite-Datenbank für Tests
- JWT-Token-Setup für Authentication

**Status:** Vorhanden, ähnliches Pattern für Playlist E2E-Tests anwendbar.

---

### `ContinueWatchingServiceGetNextEpisodeTests`
**Datei:** `VideoWebPlayer.Tests/Services/ContinueWatchingServiceGetNextEpisodeTests.cs`

Unit-Tests für Episode-Navigation und Sortierlogik.

**Methoden:**
- `HappyPath_SimpleEpisodeSequence_ReturnsNextEpisode()` — Basis-Szenario
- `AllEpisodesWithoutReleaseDate_SortsByNumber()` — Sortierung ohne Datumsangabe
- `AllEpisodesWithReleaseDate_SortsCorrectly()` — Sortierung mit Datumsangaben
- Weitere Tests für Edge-Cases

**Basis-Klasse:** `ContinueWatchingServiceTestBase`

**Status:** Vorhanden, ähnliches Pattern für Playlist-Sortierungs-Tests anwendbar.

---

### `ContinueWatchingServiceTestBase`
**Datei:** `VideoWebPlayer.Tests/Helpers/ContinueWatchingServiceTestBase.cs`

Basis-Klasse mit gemeinsamen Setup-Methoden.

**Muster:**
- Stellt `ApplicationDbContext` zur Verfügung
- Stellt `ContinueWatchingService` zur Verfügung
- Definiert Test-Konstanten wie `Duration`, `CompletedPosition`

**Status:** Vorhanden, ähnliches Pattern für Playlist-Test-Basis anwendbar.

---

### `FavoritesServiceContextMenuActionTests`
**Datei:** `VideoWebPlayer.Tests/Services/FavoritesServiceContextMenuActionTests.cs`

Tests für Favorites-Service-Logik.

**Pattern:**
- Tests für Hinzufügen/Entfernen
- Tests für Duplikat-Behandlung
- Zugriffsprüfung und Ownership-Checks

**Status:** Vorhanden, ähnliches Pattern für Playlist-CRUD-Tests anwendbar.

---

## Test-Hilfsmethoden

### `TestHelpers`
**Datei:** `VideoWebPlayer.Tests/Helpers/TestHelpers.cs`

Statische Hilfsmethoden für Test-Setups.

**Methoden:**
- `WaitForMessageAsync()` — Wartet auf Log-Nachrichten mit Timeout
- `DumpDatabaseStateAsync()` — Dumpt Datenbank-Status für Debugging
- `WaitForMediaCollectionAsync()` — Wartet auf Medien-Collection-Erstellung
- `CreateTvShowWithSeasonsAsync()` — Erstellt Test-Serien mit Staffeln und Episoden
- Weitere Hilfsmethoden für verschiedene Medientypen

**Status:** Vorhanden, sollte um `CreatePlaylistWithItemsAsync()` und verwandte Methoden erweitert werden.

---

### `ContinueWatchingServiceTestBase`
**Datei:** `VideoWebPlayer.Tests/Helpers/ContinueWatchingServiceTestBase.cs`

Base-Klasse für ContinueWatching-Tests.

**Muster:**
- In-Memory SQLite-Datenbank Setup
- Service-Initialisierung
- Test-Benutzer-Setup
- Cleanup-Logik

**Status:** Vorhanden, ähnliches Pattern für Playlist-Tests anwendbar.

---

## Test-Framework

**Framework:** XUnit
**Basis-Verzeichnis:** `VideoWebPlayer.Tests/`

**Test-Kategorien:**
- **E2E-Tests:** In `VideoWebPlayer.Tests/` (E2E Trait)
- **Service-Tests:** In `VideoWebPlayer.Tests/Services/`
- **Controller-Tests:** In `VideoWebPlayer.Tests/Controllers/`
- **Component-Tests:** In `VideoWebPlayer.Tests/Components/`

**Patterns:**
- Fluent Assertions
- Trait-basierte Kategorisierung (`[Trait("Category", "E2E")]`)
- WebApplicationFactory für In-Process-Server-Tests
- In-Memory-Datenbank für Isolation

---

## Zu erstellende Test-Klassen

### `PlaylistServiceTests`
**Empfohlener Standort:** `VideoWebPlayer.Tests/Services/PlaylistServiceTests.cs`

Unit-Tests für Playlist CRUD-Operationen.

**Test-Szenarien:**
- `CreatePlaylist_ValidInput_ReturnsPlaylist()`
- `CreatePlaylist_DuplicateName_Throws()`
- `UpdatePlaylist_OnlyOwnerCanUpdate()`
- `DeletePlaylist_OnlyOwnerCanDelete()`
- `GetPlaylist_PublicPlaylist_AnyoneCanAccess()`
- `GetPlaylist_PrivatePlaylist_OnlyOwnerCanAccess()`

---

### `PlaylistItemServiceTests`
**Empfohlener Standort:** `VideoWebPlayer.Tests/Services/PlaylistItemServiceTests.cs`

Unit-Tests für PlaylistItem-Verwaltung.

**Test-Szenarien:**
- `AddItem_ValidItem_ReturnsPlaylistItem()`
- `AddItem_DuplicateItem_Throws()`
- `RemoveItem_ValidItem_ReturnsTrue()`
- `RemoveItem_ItemNotFound_ReturnsFalse()`
- `GetPlaylistItems_WithPagination_ReturnsPaginatedList()`

---

### `PlaylistSortingServiceTests`
**Empforlener Standort:** `VideoWebPlayer.Tests/Services/PlaylistSortingServiceTests.cs`

Unit-Tests für Sortierlogik.

**Test-Szenarien:**
- `GetSortedItems_AutomaticMode_SortsByReleaseDate()`
- `GetSortedItems_ManualMode_SortsByOrder()`
- `CalculateAutomaticOrder_NewEpisode_PositionedCorrectly()`

---

### `PlaylistGenreServiceTests`
**Empfohlener Standort:** `VideoWebPlayer.Tests/Services/PlaylistGenreServiceTests.cs`

Unit-Tests für Genre-Verwaltung.

**Test-Szenarien:**
- `DeriveGenres_MixedItems_AggregatesCorrectly()`
- `UpdatePlaylistGenres_ManualOverride_ReplacesAutomatic()`

---

### `PlaylistImageServiceTests`
**Empfohlener Standort:** `VideoWebPlayer.Tests/Services/PlaylistImageServiceTests.cs`

Unit-Tests für Bildverwaltung.

**Test-Szenarien:**
- `GeneratePlaylistImage_MultipleItems_ReturnsComposite()`
- `UploadPlaylistImage_ValidImage_Stored()`
- `GetPlaylistImage_Found_ReturnsImage()`

---

### `ContinueWatchingPlaylistServiceTests`
**Empfohlener Standort:** `VideoWebPlayer.Tests/Services/ContinueWatchingPlaylistServiceTests.cs`

Unit-Tests für Weiterschauen-Playlist-Integration.

**Test-Szenarien:**
- `CreateOrUpdateContinueWatchingWithPlaylist_ValidInput_Stored()`
- `GetContinueWatchingEntries_WithPlaylistInfo_ReturnsIncludingPlaylistData()`
- `ReplaceOrDeleteContinueWatchingOnRemoval_HasNext_Replaces()`
- `ReplaceOrDeleteContinueWatchingOnRemoval_NoNext_Deletes()`

---

### `PlaylistE2ETests`
**Empfohlener Standort:** `VideoWebPlayer.Tests/PlaylistE2ETests.cs`

End-to-End Tests für Playlist-Workflows.

**Test-Szenarien:**
- `CreatePlaylist_AddItems_UpdatePlayback_RetrieveContinueWatching()` — Kompletter Workflow
- `PublicPlaylist_AnyoneCanAccess()` — Öffentliche Playlists
- `AutomaticMode_NewContent_AddedCorrectly()` — Automatische Ergänzung
- `ManualMode_DragAndDrop_ReordersCorrectly()` — Manuelle Sortierung

---

### `PlaylistE2EControllerTests`
**Empfohlener Standort:** `VideoWebPlayer.Tests/PlaylistE2EControllerTests.cs` oder `VideoWebPlayer.Tests/Controllers/PlaylistsControllerE2ETests.cs`

API-Controller-Tests via HTTP.

**Test-Szenarien:**
- `GET /api/playlists` — Authentifizierung, Auslisting
- `POST /api/playlists` — Erstellung mit Validierung
- `PUT /api/playlists/{id}` — Update mit Ownership-Check
- `DELETE /api/playlists/{id}` — Löschung mit Ownership-Check
- `POST /api/playlists/{id}/items` — Item-Hinzufügung
- `DELETE /api/playlists/{id}/items/{itemId}` — Item-Entfernung

---

## Helper-Methoden (zu erweitern)

### In `TestHelpers`

```csharp
// Neue Methoden für Playlist-Tests
public static async Task<Playlist> CreatePlaylistAsync(
    ApplicationDbContext db,
    string userId,
    string name,
    PlaylistSortMode sortMode = PlaylistSortMode.Automatic,
    string? description = null);

public static async Task<PlaylistItem> AddPlaylistItemAsync(
    ApplicationDbContext db,
    long playlistId,
    PlaylistItemType itemType,
    long itemId);

public static async Task<Playlist> CreatePlaylistWithItemsAsync(
    ApplicationDbContext db,
    string userId,
    PlaylistItemType itemType,
    params long[] itemIds);
```

### In `ContinueWatchingServiceTestBase` (oder neue Basis-Klasse)

```csharp
// Test-Basis für Playlist-Tests
protected async Task<Playlist> CreateTestPlaylistAsync(
    PlaylistSortMode sortMode = PlaylistSortMode.Automatic);

protected async Task<PlaylistItem> AddTestPlaylistItemAsync(
    long playlistId,
    PlaylistItemType itemType,
    long itemId);
```
