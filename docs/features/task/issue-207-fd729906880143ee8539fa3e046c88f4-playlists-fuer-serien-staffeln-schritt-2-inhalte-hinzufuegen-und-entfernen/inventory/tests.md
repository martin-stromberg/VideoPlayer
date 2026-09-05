# Tests

## Testklassen für PlaylistService

### `PlaylistServiceTests_Create`
Datei: `VideoWebPlayer.Tests/Services/PlaylistServiceTests_Create.cs`

| Testmethode | Was wird getestet? |
|-------------|-------------------|
| `CreatePlaylist_ValidInput_ReturnsPlaylistDto` | Erfolgreiche Erstellung mit gültigen Eingaben |
| `CreatePlaylist_EmptyName_ThrowsInvalidOperationException` | Fehlerbehandlung: Leerer Name |
| `CreatePlaylist_NameTooLong_ThrowsInvalidOperationException` | Fehlerbehandlung: Name zu lang (>255 Zeichen) |
| `CreatePlaylist_DescriptionTooLong_ThrowsInvalidOperationException` | Fehlerbehandlung: Beschreibung zu lang (>2000 Zeichen) |
| `CreatePlaylist_DuplicateName_ThrowsInvalidOperationException` | Fehlerbehandlung: Duplikat-Name (case-insensitive) |
| `CreatePlaylist_MaxPlaylistsExceeded_ThrowsInvalidOperationException` | Fehlerbehandlung: Maximale Anzahl Playlists überschritten |
| `CreatePlaylist_PublishesPlaylistCreatedEvent` | Event Publishing: PlaylistCreatedEvent wird publiziert |

---

### `PlaylistServiceTests_Read`
Datei: `VideoWebPlayer.Tests/Services/PlaylistServiceTests_Read.cs`

**Angenommene Testmethoden (Struktur analog zu Create):**
- Erfolgreiche Abfrage aller Playlists
- Erfolgreiche Abfrage einzelner Playlist
- Fehlerbehandlung: Playlist nicht gefunden
- Fehlerbehandlung: Zugriff verweigert (Nicht-Besitzer)

---

### `PlaylistServiceTests_Update`
Datei: `VideoWebPlayer.Tests/Services/PlaylistServiceTests_Update.cs`

**Angenommene Testmethoden:**
- Erfolgreiche Aktualisierung
- Fehlerbehandlung: Playlist nicht gefunden
- Fehlerbehandlung: Zugriff verweigert (Nicht-Besitzer)
- Fehlerbehandlung: Name zu lang
- Fehlerbehandlung: Duplikat-Name
- Event Publishing: PlaylistUpdatedEvent wird publiziert

---

### `PlaylistServiceTests_Delete`
Datei: `VideoWebPlayer.Tests/Services/PlaylistServiceTests_Delete.cs`

**Angenommene Testmethoden:**
- Erfolgreiche Löschung
- Fehlerbehandlung: Playlist nicht gefunden
- Fehlerbehandlung: Zugriff verweigert (Nicht-Besitzer)
- Event Publishing: PlaylistDeletedEvent wird publiziert

---

## Testklassen für PlaylistsController

### `PlaylistsControllerTests_Auth`
Datei: `VideoWebPlayer.Tests/Controllers/PlaylistsControllerTests_Auth.cs`

**Angenommener Inhalt:** Authentifizierungs- und Autorisierungstests für alle Endpoints

---

### `PlaylistsControllerTests_Create`
Datei: `VideoWebPlayer.Tests/Controllers/PlaylistsControllerTests_Create.cs`

**Angenommene Testmethoden:**
- Erfolgreiche Erstellung (POST /api/playlists)
- Fehlerbehandlung: Ungültige Eingaben (400)
- Fehlerbehandlung: Duplikat (409)
- Fehlerbehandlung: Nicht authentifiziert (401)

---

### `PlaylistsControllerTests_Read`
Datei: `VideoWebPlayer.Tests/Controllers/PlaylistsControllerTests_Read.cs`

**Angenommene Testmethoden:**
- GET /api/playlists (alle Playlists)
- GET /api/playlists/{id} (einzelne Playlist)
- Fehlerbehandlung: Nicht gefunden (404)
- Fehlerbehandlung: Zugriff verweigert (403)
- Fehlerbehandlung: Nicht authentifiziert (401)

---

### `PlaylistsControllerTests_Update`
Datei: `VideoWebPlayer.Tests/Controllers/PlaylistsControllerTests_Update.cs`

**Angenommene Testmethoden:**
- Erfolgreiche Aktualisierung (PUT /api/playlists/{id})
- Fehlerbehandlung: Nicht gefunden (404)
- Fehlerbehandlung: Zugriff verweigert (403)
- Fehlerbehandlung: Duplikat (409)
- Fehlerbehandlung: Ungültige Eingaben (400)

---

### `PlaylistsControllerTests_Delete`
Datei: `VideoWebPlayer.Tests/Controllers/PlaylistsControllerTests_Delete.cs`

**Angenommene Testmethoden:**
- Erfolgreiche Löschung (DELETE /api/playlists/{id})
- Fehlerbehandlung: Nicht gefunden (404)
- Fehlerbehandlung: Zugriff verweigert (403)

---

## Hilfsmethoden und Test-Basen

### `PlaylistServiceTestBase`
Datei: `VideoWebPlayer.Tests/Helpers/PlaylistServiceTestBase.cs`

| Methode / Eigenschaft | Beschreibung |
|----------------------|-------------|
| `_db` | In-Memory-Datenbank für Tests (EF Core) |
| `_eventManager` | EventManager-Instanz für Event-Abonnements in Tests |
| `_service` | PlaylistService-Instanz für Tests |
| `_testUserId` | Test-Benutzer-ID: `"test-user-123"` |
| `_otherUserId` | Zweiter Test-Benutzer-ID: `"other-user-456"` |
| `CreateService(maxPlaylistsPerUser)` | Factory-Methode zum Erstellen eines PlaylistService mit optionalen Einstellungen |

**Zweck:** Basis-Klasse für alle PlaylistService-Tests mit vorkonfigurierter In-Memory-Datenbank

---

### `PlaylistsControllerTestBase`
Datei: `VideoWebPlayer.Tests/Helpers/PlaylistsControllerTestBase.cs`

**Zweck:** Basis-Klasse für PlaylistsController-Tests (analog zu PlaylistServiceTestBase)

---

### `PlaylistsE2ETestBase`
Datei: `VideoWebPlayer.Tests/Helpers/PlaylistsE2ETestBase.cs`

**Zweck:** Basis-Klasse für End-to-End-Tests (E2E) mit echter Datenbank und HTTP-Aufrufen

---

## E2E-Tests

### `PlaylistsE2ETests`
Datei: `VideoWebPlayer.Tests/PlaylistsE2ETests.cs`

**Zweck:** End-to-End-Tests für Playlist-Verwaltung (alle CRUD-Operationen)

---

### `PlaylistDetailE2ETests`
Datei: `VideoWebPlayer.Tests/PlaylistDetailE2ETests.cs`

**Zweck:** End-to-End-Tests für PlaylistDetail.razor-Komponente

---

## Nicht vorhanden (zu erstellen für Schritt 2):

### Tests für PlaylistService
- Unit-Tests für `AddMediaToPlaylistAsync`:
  - Erfolgreicher Hinzufügen
  - Duplikatsprüfung (Konflikt-Handling)
  - Cascade-Hinzufügen (Serie → Staffeln/Episoden, Staffel → Episoden, Sammlung → Filme)
  - Berechtigungsprüfung (Nicht-Besitzer)
  - Medieninhalt nicht vorhanden (404)

- Unit-Tests für `RemoveMediaFromPlaylistAsync`:
  - Erfolgreiche Entfernung
  - Berechtigungsprüfung
  - Eintrag nicht vorhanden (Fehlerbehandlung)

- Unit-Tests für `GetPlaylistEntriesAsync`:
  - Erfolgreiche Listierung
  - Berechtigungsprüfung
  - Leere Playlist

### Tests für PlaylistsController
- Integration-Tests für POST `/api/playlists/{id}/entries`
- Integration-Tests für DELETE `/api/playlists/{id}/entries/{mediaType}/{mediaId}`
- Integration-Tests für GET `/api/playlists/{id}/entries`
- Fehlerbehandlung (400, 404, 409, 403)

