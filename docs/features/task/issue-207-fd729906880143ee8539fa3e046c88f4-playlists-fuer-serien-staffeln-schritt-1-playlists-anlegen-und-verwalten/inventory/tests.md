# Testabdeckung

## End-to-End Tests

### `PlaylistsE2ETests`
Datei: `VideoWebPlayer.Tests/PlaylistsE2ETests.cs`

Automatkierte Browser-Tests mit Playwright. Testet die gesamte User-Journey für Playlist-Verwaltung.

**Testmethoden:**

| Testmethode | Getestet | Beschreibung |
|-------------|----------|-------------|
| `Create_List_Edit_And_Delete_Playlist_HappyPath` | CRUD-Workflow | Erstellt Playlist → sieht sie in der Liste → bearbeitet sie → löscht sie mit Bestätigung |
| `EmptyName_ShowsValidationError` | Validierung | Name-Feld darf nicht leer sein; Speichern ist blockiert |
| `DuplicateName_ShowsConflictError` | Duplikat-Prüfung | Zweite Playlist mit gleichem Namen (unterschiedliche Groß/Kleinschreibung) zeigt Fehler |
| `Unauthenticated_Access_Shows_Error` | Authentifizierung | Unangemeldeter Benutzer sieht Fehlermeldung beim Zugriff auf `/playlists` |
| `UserB_Does_Not_See_UserA_Playlists` | Isolation | Benutzer B kann Playlists von Benutzer A nicht sehen (Eigentumsschutz) |

**Test-Setup:**
- Nutzt `WebApplicationFactory` mit Test-Datenbank
- Erstellt zwei Test-Benutzer (UserA, UserB)
- Browser-Automatisierung mit Chromium (Playwright)

## Unit Tests

### Controller Tests

**Datei:** `VideoWebPlayer.Tests/Controllers/PlaylistsControllerTests_*.cs`

Mehrere Test-Klassen für verschiedene CRUD-Operationen:

#### `PlaylistsControllerTests_Read`
Datei: `VideoWebPlayer.Tests/Controllers/PlaylistsControllerTests_Read.cs`

| Test | Getestet |
|------|----------|
| `GetPlaylists_Returns200Ok` | GET `/api/playlists` gibt 200 OK mit Playlists zurück |
| `GetPlaylist_ValidId_Returns200Ok` | GET `/api/playlists/{id}` gibt 200 OK mit einzelner Playlist zurück |
| `GetPlaylist_NotFound_Returns404NotFound` | GET mit ungültiger ID gibt 404 Not Found |
| `GetPlaylist_OwnershipViolation_Returns403Forbidden` | GET mit fremder Playlist gibt 403 Forbidden (Eigentumscheck) |

#### `PlaylistsControllerTests_Create`
Datei: `VideoWebPlayer.Tests/Controllers/PlaylistsControllerTests_Create.cs`

Testet POST `/api/playlists` für Erstellung mit verschiedenen Szenarien (gültig, Duplikat, Limit überschritten, Validierungsfehler).

#### `PlaylistsControllerTests_Update`
Datei: `VideoWebPlayer.Tests/Controllers/PlaylistsControllerTests_Update.cs`

Testet PUT `/api/playlists/{id}` für Bearbeitung mit Eigentumscheck, Duplikat-Prüfung, Validierung.

#### `PlaylistsControllerTests_Delete`
Datei: `VideoWebPlayer.Tests/Controllers/PlaylistsControllerTests_Delete.cs`

Testet DELETE `/api/playlists/{id}` für Löschung mit Eigentumscheck.

#### `PlaylistsControllerTests_Auth`
Datei: `VideoWebPlayer.Tests/Controllers/PlaylistsControllerTests_Auth.cs`

Testet Authentifizierung und Autorisierung (401 Unauthorized, 403 Forbidden).

### Service Tests

Datei: `VideoWebPlayer.Tests/Services/PlaylistServiceTests_*.cs`

Unit-Tests für `PlaylistService` mit verschiedenen Szenarien:

| Test-Klasse | Getestet |
|-------------|----------|
| `PlaylistServiceTests_Create` | Erstellung mit Validierung, Duplikat-Check, Limit-Enforcement |
| `PlaylistServiceTests_Read` | Abruf mit Eigentumscheck (403 bei fremder Playlist) |
| `PlaylistServiceTests_Update` | Bearbeitung mit Validierung und Eigentumscheck |
| `PlaylistServiceTests_Delete` | Löschung mit Eigentumscheck |

### Test Helpers

#### `PlaylistsControllerTestBase`
Datei: `VideoWebPlayer.Tests/Helpers/PlaylistsControllerTestBase.cs`

Basis-Klasse für Controller-Tests. Stellt bereit:
- Fake-Authentifizierung (`FakeAuthService`)
- In-Memory-Datenbank
- Vorbereitete Controller-Instanz
- Test-Benutzer (Haupt- und Sekundär-Benutzer für Eigentumscheck)

#### `PlaylistServiceTestBase`
Datei: `VideoWebPlayer.Tests/Helpers/PlaylistServiceTestBase.cs`

Basis-Klasse für Service-Tests. Stellt bereit:
- In-Memory-Datenbank
- EventManager-Mock
- PlaylistSettings (mit/ohne Max-Limit)
- Service-Instanz
- Test-Benutzer

## Test-Abdeckung

**Abgedeckte Szenarien:**
- ✅ Happy Path: Erstelle → Lese → Aktualisiere → Lösche
- ✅ Validierung: Name erforderlich, Längenbeschränkungen
- ✅ Duplikat-Prüfung: Case-insensitive
- ✅ Eigentumsschutz: 403 bei fremder Playlist
- ✅ Authentifizierung: 401 ohne Token
- ✅ Autorisierung: 403 bei Zugriff auf fremde Daten
- ✅ Limit-Enforcement: Max. Playlists pro Benutzer
- ✅ Event-Publishing: Events nach CRUD-Operationen
- ✅ E2E-Workflow: Browser-Tests durch gesamte UI

**Nicht abgedeckt (wäre Aufgabe neuer Entwicklungsschritte):**
- Playlist-Inhalte (Titel-Liste)
- Infinity-List und automatische Sortierung
- Detailseite für Playlists
- Sharing/Zugriff-Verwaltung
