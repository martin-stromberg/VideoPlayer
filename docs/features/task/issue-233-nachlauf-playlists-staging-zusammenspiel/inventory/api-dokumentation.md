# API-Dokumentation und Fehlerbehandlung

## docs/API.md: Fehlende Playlist-Endpunkte (A2)

**Datei:** `docs/API.md`

**Stand:** Letzte Aktualisierung 2026-08-25 (veraltet, neuere Features aus September sind nicht dokumentiert)

### Dokumentierte Playlist-Endpunkte
- `POST /api/playlists` (Create)
- `GET /api/playlists` (List)
- `GET /api/playlists/{id}` (Get)
- `PATCH /api/playlists/{id}` (Update name/description)
- `DELETE /api/playlists/{id}` (Delete)
- `POST /api/playlists/{id}/entries` (Add entry)
- `GET /api/playlists/{id}/entries/paged` (List entries paged)
- `DELETE /api/playlists/{id}/entries/{entryId}` (Remove entry)
- `GET /api/playlists/public` (List public playlists)
- `PUT /api/playlists/{id}/public` (Toggle public flag)

### Fehlende Endpunkte (17 total, F2)

**Wiedergabe (4):**
- `POST /api/playlists/{id}/play`
- `POST /api/playlists/{id}/play/next`
- `POST /api/playlists/{id}/play/previous`
- `POST /api/playlists/{id}/play/advance`

**Cover-Management (5):**
- `GET /api/playlists/{id}/cover`
- `POST /api/playlists/{id}/cover/upload`
- `POST /api/playlists/{id}/cover/regenerate`
- `POST /api/playlists/{id}/cover/preview`
- `DELETE /api/playlists/{id}/cover`

**Sortierung und Genres (5):**
- `PATCH /api/playlists/{id}/sort-mode`
- `PUT /api/playlists/{id}/genres`
- `POST /api/playlists/{id}/genres/reset`

**Manual-Reordering (3):**
- `PUT /api/playlists/{id}/entries/{entryId}/order`
- `POST /api/playlists/{id}/entries/batch-reorder`
- `GET /api/playlists/{id}/entries/max-sort-order`
- `POST /api/playlists/{id}/entries/{entryId}/move-to-beginning`
- `POST /api/playlists/{id}/entries/{entryId}/move-between`

### Session-Endpunkte in Pairing-Bereich (nicht dokumentiert)
- `POST /api/pairing/bootstrap` (QR-Bootstrap, Zeile 14 erwähnt, aber nicht dokumentiert)
- `POST /api/auth/refresh` (Session-Renewal, dokumentiert Zeile 75–101 aber unvollständig)
- `POST /api/auth/logout` (Logout, dokumentiert Zeile 103–110 aber unvollständig)

### Query-Parameter: access_token

**Dokumentiert:** Zeile 14–15 erwähnt, dass `access_token` als Query-Parameter alternativ zum Bearer-Token möglich ist, aber nicht systematisch dokumentiert für jeden Endpunkt.

**Praktische Relevanz:** Erforderlich für `<img>`-Tags und Videostreams (CORS/Cookies), funktioniert aber bei **allen** Endpunkten (bestätigt im Code: `BearerTokenCheckAttribute.cs:31–34`).

## docs/help/playlists-api.md

**Datei:** `docs/help/playlists-api.md`

**Befund:** Beschreibt alle Playlist-Endpunkte vollständig; Widerspruch zu `docs/API.md` besteht.

## ApiDocumentationContractTests (F2)

**Datei:** `VideoWebPlayer.Tests/ApiDocumentationContractTests.cs` Zeile 50–71

**Prüft (19 Pflichtrouten):**
- `GET /api/health`
- `POST /api/auth/login`
- Weitere Standard-Endpunkte

**Fehlen:**
- Keine einzige Playlist-Route
- Neue Session-Endpunkte `POST /api/pairing/bootstrap`, `POST /api/auth/refresh`, `POST /api/auth/logout` fehlen, obwohl sie in `docs/API.md` erwähnt sind

**Laufzeitprobe:** `MauiContract_RuntimeLoginHealthAndAuthenticatedRead_Succeeds` endet bei `GET /api/items`.

**Befund:** Der Vertragstest ist selbst lückenhaft.

## Fehlerbehandlung: 401 vs. 403, 500 vs. 404 (A4)

### Aktuelle Mappings

**PlaylistsController (korrekt):**
- `PlaylistAccessDeniedException` → `Forbid()` (HTTP 403)
- `KeyNotFoundException` → `NotFound()` (HTTP 404)

**ItemsController (falsch):**
- `UnauthorizedAccessException` bei fehlender Berechtigung → `Unauthorized()` (HTTP 401) [sollte 403 sein]
- `RecordNotFoundException` nicht abgebildet → generischer `catch (Exception)` → `StatusCode(500)` [sollte 404 sein]

### Dokumentation vs. Realität

| Fall | Doku (API.md:29–30) | PlaylistsController | ItemsController |
|------|---------------------|---------------------|-----------------|
| Benutzer angemeldet, keine Berechtigung | 403 | ✓ 403 | ✗ 401 |
| Ressource nicht vorhanden | 404 | ✓ 404 | ✗ 500 |
| Kein/ungültiger Token | 401 | ✓ 401 | ✓ 401 |

### Wirkung durch Playlists (A4-Kontext)

Mit öffentlichen Playlists ist das kein Randfall mehr: fremde Anwender sehen gesperrte Titel (gedimmt) und klicken sie an → `401` statt `403`.

**Client-Fehler:** `VideoWebPlayerClient.SendWithReauthorizationAsync` (Zeile 66–94) interpretiert jedes `401` als abgelaufenes Token und versucht Refresh → unnötige Token-Rotation, dann erneut `401` → Fehlermeldung falsch (sollte "Für diesen Titel fehlt Ihnen die Freischaltung" sein).

## Release Notes (A3)

**Datei:** `docs/RELEASE_NOTES.md`

### Englischer Teil (What's New, Zeile 15–65)

Enthält QR-Bootstrap (Zeile 38):
- "QR bootstrap from the profile page"
- "New public endpoint `POST /api/pairing/bootstrap`"
- "New session endpoints `POST /api/auth/refresh` ... and `POST /api/auth/logout`"
- "Revoking a paired device now also revokes all its refresh tokens immediately"
- "Bootstrap tickets"
- "New NuGet dependency `QRCoder`"
- "Device pairing for client apps"
- "New admin page `Devices`"
- "New public endpoint `POST /api/pairing/exchange`"

Unter "Important Notes Before Update" (Zeile 12–13):
- Schema-Änderung erwähnt
- Vier neue Config-Keys aufgelistet

### Deutscher Teil (Neuerungen, Zeile 76 ff.)

Fehlt komplett: Alle QR-Bootstrap-Einträge (entsprechend Englisch Zeile 38–50)

Unter "Wichtige Hinweise vor dem Update" (Zeile 68–74):

Fehlen:
- Hinweis auf `dotnet ef database update` (nur Englisch Zeile 12)
- Vier neue Konfigurationsschlüssel (nur Englisch Zeile 13)

### Zusätzliches Problem (A1)

Zeile 5 (Englisch) und Zeile 68 (Deutsch) behaupten:
> "Backups from older versions remain restorable because the new tables and columns are optional during restore."

**Befund F1:** Das ist derzeit falsch (PairedDevices, PairingCodes, RefreshTokens fehlen in OptionalRestoreTables).
