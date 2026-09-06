# Tests

## Testklassen

### Service-Tests

#### `PlaylistServiceTests_GetEntries`
Datei: `VideoWebPlayer.Tests/Services/PlaylistServiceTests_GetEntries.cs`

Testet die Methode `GetPlaylistEntriesAsync()`:

- `GetEntries_ReturnsAllEntries` – Prüft, dass alle Einträge einer Playlist zurückgeliefert werden
- `GetEntries_EmptyPlaylist_ReturnsEmpty` – Prüft, dass eine leere Playlist korrekt behandelt wird
- `GetEntries_NotOwner_ThrowsPlaylistAccessDeniedException` – Prüft Berechtigungslogik
- `GetEntries_PlaylistNotFound_ThrowsKeyNotFoundException` – Prüft Fehlerbehandlung
- `GetEntries_RemovesOrphanedEntries` – Prüft, dass verwaiste Einträge gefiltert werden
- `GetEntries_OrphanedEntry_DeletedFromDatabase` – Prüft, dass verwaiste Einträge aus DB gelöscht werden
- `GetEntries_CascadedEntry_ParentMediaTitleIsLoaded` – Prüft, dass Kaskaden-Titel geladen werden

**Basis-Klasse:** `PlaylistServiceTestBase`

---

#### `PlaylistServiceTests_AddMedia`
Datei: `VideoWebPlayer.Tests/Services/PlaylistServiceTests_AddMedia.cs`

Testet die Methode `AddMediaToPlaylistAsync()`:

- Tests für das Hinzufügen verschiedener Medientypen
- Tests für Kaskaden-Logik
- Duplikat-Handling
- Berechtigungsprüfungen

**Basis-Klasse:** `PlaylistServiceTestBase`

---

#### `PlaylistServiceTests_RemoveMedia`
Datei: `VideoWebPlayer.Tests/Services/PlaylistServiceTests_RemoveMedia.cs`

Testet die Methode `RemoveMediaFromPlaylistAsync()`:

- Tests für das Entfernen von Einträgen
- Fehlerbehandlung für nicht existierende Einträge
- Berechtigungsprüfungen

**Basis-Klasse:** `PlaylistServiceTestBase`

---

#### `PlaylistServiceTests_Create`
Datei: `VideoWebPlayer.Tests/Services/PlaylistServiceTests_Create.cs`

Testet die Methode `CreatePlaylistAsync()`:

- Tests für Playlist-Erstellung
- Validierung von Namen und Beschreibungen
- Sortiermodus-Handling
- Duplikat-Namen-Prüfung

---

#### `PlaylistServiceTests_Update`
Datei: `VideoWebPlayer.Tests/Services/PlaylistServiceTests_Update.cs`

Testet die Methode `UpdatePlaylistAsync()`:

- Tests für Playlist-Aktualisierung
- Validierung von Namen und Beschreibungen
- Sortiermodus-Änderung

---

#### `PlaylistServiceTests_Delete`
Datei: `VideoWebPlayer.Tests/Services/PlaylistServiceTests_Delete.cs`

Testet die Methode `DeletePlaylistAsync()`:

- Tests für Playlist-Löschung
- Cascade-Delete Handling

---

#### `PlaylistServiceTests_Read`
Datei: `VideoWebPlayer.Tests/Services/PlaylistServiceTests_Read.cs`

Testet die Lese-Methoden:

- `GetPlaylistsAsync()`
- `GetPlaylistAsync()`

---

### Controller-Tests

#### `PlaylistsControllerTests_Entries`
Datei: `VideoWebPlayer.Tests/Controllers/PlaylistsControllerTests_Entries.cs`

Testet die API-Endpoints für Playlist-Einträge:

- `GET /api/playlists/{id}/entries` – Get entries endpoint
- `POST /api/playlists/{id}/entries` – Add media endpoint
- `DELETE /api/playlists/{id}/entries/{mediaType}/{mediaId}` – Remove media endpoint

---

#### Weitere Controller-Tests
- `PlaylistsControllerTests_Auth` – Authentifizierungs- und Autorisierungstests
- `PlaylistsControllerTests_Create` – Tests für Playlist-Erstellung
- `PlaylistsControllerTests_Delete` – Tests für Playlist-Löschung
- `PlaylistsControllerTests_Read` – Tests für Playlist-Abfragen
- `PlaylistsControllerTests_Update` – Tests für Playlist-Updates

---

### E2E-Tests

#### `PlaylistsE2ETests`
Datei: `VideoWebPlayer.Tests/PlaylistsE2ETests.cs`

End-to-End Tests für Playlist-Features über den gesamten Stack.

---

#### `PlaylistDetailE2ETests`
Datei: `VideoWebPlayer.Tests/PlaylistDetailE2ETests.cs`

End-to-End Tests für die PlaylistDetail Razor-Komponente.

---

#### `PlaylistEntriesE2ETests`
Datei: `VideoWebPlayer.Tests/PlaylistEntriesE2ETests.cs`

End-to-End Tests für Playlist-Einträge-Verwaltung.

---

## Hilfsmethoden und Test-Utilities

### `PlaylistServiceTestBase`
Datei: `VideoWebPlayer.Tests/Helpers/PlaylistServiceTestBase.cs`

Basis-Klasse für Service-Tests mit Hilfsmethoden:

- `CreateTestPlaylistWithEntriesAsync()` – Erstellt Playlists mit vordefinierten Einträgen
- `CreateTestMediaEntryAsync()` – Erstellt Test-Medieneinträge verschiedener Typen
- `_db` – Zugriff auf Test-Datenbankkontext
- `_service` – Zugriff auf Test-PlaylistService Instanz
- `_testUserId` – Standard-Test-Benutzer-ID
- `_otherUserId` – Alternative Test-Benutzer-ID für Autorisierungstests

---

### `PlaylistsControllerTestBase`
Datei: `VideoWebPlayer.Tests/Helpers/PlaylistsControllerTestBase.cs`

Basis-Klasse für Controller-Tests mit Hilfsmethoden für:

- Authentifizierungs-Setup
- Mock-Service-Konfiguration
- HTTP-Anfragen-Ausführung

---

### `PlaylistsE2ETestBase`
Datei: `VideoWebPlayer.Tests/Helpers/PlaylistsE2ETestBase.cs`

Basis-Klasse für E2E-Tests mit:

- WebApplication/WebApplicationFactory-Setup
- Browser-Automation oder HTTP-Client-Setup
- Test-Daten-Initialization

---

### `TestHelpers.CreateTvShowWithSeasonsAsync()`
Datei: `VideoWebPlayer.Tests/Helpers/TestHelpers.cs`

Hilfsmethode zum Erstellen von Test-TV-Shows mit Staffeln und Episoden:

```csharp
public static async Task<TVShow> CreateTvShowWithSeasonsAsync(
    ApplicationDbContext db,
    params (string SeasonName, (int EpisodeNumber, DateTime? AirDate)[] Episodes)[] seasons)
```

Ermöglicht komfortable Erstellung von Test-Hierarchien mit Datum-Informationen für Sortierungs-Tests.

---

## Lücken für Schritt 3

**Tests, die NICHT vorhanden sind, aber benötigt werden:**

1. **Unit-Tests für paginierte Einträge:**
   - Test für `GetPlaylistEntriesPagedAsync()` mit verschiedenen `pageNumber` und `pageSize`
   - Test für `hasNextPage` Flag
   - Test für `totalCount` Genauigkeit

2. **Unit-Tests für Sortierung nach Erscheinungsdatum:**
   - Test: Sortierung nach `ReleaseDate` (alle Einträge haben Datum)
   - Test: Sortierung mit fehlenden Erscheinungsdaten (Fallback auf Hierarchie)
   - Test: Sortierung mit fehlenden Hierarchie-Daten (Fallback auf `AddedAt`)
   - Test: Gemischte Szenarien (einige mit Datum, einige ohne)
   - Test: Mehrere Medientypen in einer Playlist korrekt sortiert

3. **Unit-Tests für Lizenz-Status / Zugriffsrechte:**
   - Test: Ausgegrenzte Inhalte sind in der sortierten Liste sichtbar
   - Test: `IsAccessible` Flag wird korrekt gesetzt

4. **E2E-Tests für Virtual-Scrolling:**
   - Tests für Lazy Loading beim Scrollen
   - Tests für korrekte Anzeige von Seite 1, dann nächste Seiten

---

## Beachte

- Alle existierenden Tests verwenden Xunit als Test-Framework
- Tests nutzen `EF Core InMemory` für Datenbank-Tests
- Test-Hilfsmethoden verwenden `async/await` durchgehend
- Cancellation Tokens werden durchgehend unterstützt
