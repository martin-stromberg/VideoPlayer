# Bestandsaufnahme: Playlist-Verwaltung (Nachbesserung Schritt 1)

Diese Bestandsaufnahme dokumentiert die existierende Infrastruktur für die Playlist-Verwaltung (Anlegen, Umbenennen, Löschen) als Ausgangspunkt für die Planung einer neuen "Öffnen"-Aktion mit Detailseite. Der Fokus liegt auf dem bestehenden Code in Razor-Komponenten, Client-Services, Backend-Controllern, Datenmodellen und Tests.

## Zusammenfassung

### Vorhanden

- **UI-Komponenten:**
  - `PlaylistsList.razor` — Listet alle Playlists auf; enthält "Bearbeiten" und "Löschen"-Buttons
  - `PlaylistForm.razor` — Modal zum Erstellen und Bearbeiten (wird wiederverwendet)
  - Navigation und Authentifizierungsprüfung sind bereits implementiert

- **Backend-Endpoints:**
  - `GET /api/playlists` — Alle Playlists des Benutzers
  - `GET /api/playlists/{id}` — Einzelne Playlist mit Eigentumscheck (403 bei fremder Playlist)
  - `POST /api/playlists` — Neue Playlist erstellen
  - `PUT /api/playlists/{id}` — Playlist aktualisieren
  - `DELETE /api/playlists/{id}` — Playlist löschen

- **Client-Services:**
  - `RequestPlaylistsAsync()` — alle Playlists laden
  - `RequestPlaylistAsync(long id)` — einzelne Playlist laden (gibt null bei 404 zurück)
  - `CreatePlaylistAsync()`, `UpdatePlaylistAsync()`, `DeletePlaylistAsync()`

- **Business-Logik (PlaylistService):**
  - Validierung (Name, Beschreibung, Sortiermode)
  - Duplikat-Prüfung (case-insensitive)
  - Eigentumsschutz mit `PlaylistAccessDeniedException` (wird zu HTTP 403)
  - Max-Limit pro Benutzer (konfigurierbar)
  - Event-Publishing nach CRUD-Operationen

- **Datenmodell:**
  - Entity `Playlist` mit Id, UserId, Name, Description, SortMode, CreatedAt, UpdatedAt
  - DTOs: `DtoPlaylist`, `DtoCreatePlaylistRequest`, `DtoUpdatePlaylistRequest`
  - Enum: `PlaylistSortMode` (ByReleaseDate, Manual)

- **Tests:**
  - E2E-Tests mit Playwright (CRUD, Validierung, Duplikat, Authentifizierung, Isolation)
  - Unit-Tests für Controller und Services
  - Test-Helpers für konsistente Vorbereitung

### Fehlend (für neue Detailseite erforderlich)

- **Neue Razor-Komponente:** `PlaylistDetail.razor` — Route `@page "/playlists/{id:long}"`
- **"Öffnen"-Button in PlaylistsList.razor** — Navigation zur Detailseite
- **Detailseite-Funktionalität:**
  - Anzeige der Stammdaten (Name, Beschreibung, Sortiermode, Zeitstempel)
  - Buttons für Bearbeiten (öffnet PlaylistForm modal) und Löschen
  - Fehlerbehandlung für 403 (fremde Playlist) und 401 (nicht angemeldet)
  - Navigation zurück zur Übersicht

### Hinweise für die Implementierung

- Der Endpoint `GET /api/playlists/{id}` existiert bereits und gibt 403 bei fremder Playlist zurück
- Der Client fängt 404 ab (gibt null), aber **fängt 403 nicht ab** — wird als Exception geworfen
- `PlaylistForm.razor` wird für Bearbeitungsmodal wiederverwendet (bereits implementiert)
- Authentifizierungsmuster: `AuthenticationStateProvider.GetAuthenticationStateAsync()` + `Client.EnsureAuthorizationTokenAsync()`
- Routing nutzt NavigationManager: `NavigationManager.NavigateTo($"/playlists/{playlist.Id}")`

## Details

Die folgenden Dokumente dokumentieren den bestehenden Code in Detail:

- [Datenmodelle und DTOs](inventory/models.md) — Entity `Playlist`, DTOs für Request/Response, String-Konstanten für Sortiermode
- [Logik-Klassen und Services](inventory/logic.md) — `PlaylistService`, `PlaylistsController`, `VideoWebPlayerClient` mit Methoden und Fehlerbehandlung
- [Enums und Konstanten](inventory/enums.md) — `PlaylistSortMode`, `PlaylistSortModeValues`
- [Events und Exceptions](inventory/events-and-exceptions.md) — `PlaylistCreatedEvent`, `PlaylistUpdatedEvent`, `PlaylistDeletedEvent`, `PlaylistAccessDeniedException`
- [UI-Komponenten](inventory/ui-components.md) — `PlaylistsList.razor`, `PlaylistForm.razor` mit Funktionalität und State
- [Konfiguration](inventory/configuration.md) — `PlaylistSettings` mit Max-Limit pro Benutzer
- [Testabdeckung](inventory/tests.md) — E2E-Tests, Unit-Tests, Test-Helpers; abgedeckte und fehlende Szenarien

## Nebenbeobachtungen (aus Requirement)

Zwei korrigierbare Punkte wurden in der Anforderung identifiziert (nicht Implementierung dieser Nachbesserung):

1. **Kommentar-Korrektur in ApplicationDbContext.cs (Zeile 103)**
   - Aktuell: `/// Tabelle fuer einzeln freigeschaltete Medieneintraege.`
   - Korrekt: `/// Tabelle für einzeln freigeschaltete Medieneinträge.`

2. **Dokumentations-Korrektur in docs/help/playlists.md (Abschnitt "Zugriff und Berechtigungen")**
   - Klärung: Bei Zugriff auf fremde Playlist antwortet die API mit HTTP **403 Forbidden** (nicht 404), um die Existenz fremder Playlists nicht preiszugeben.
