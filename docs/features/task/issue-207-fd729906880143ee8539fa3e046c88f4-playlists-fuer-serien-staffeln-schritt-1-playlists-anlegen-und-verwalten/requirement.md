# Anforderung: Playlists anlegen und verwalten

## Fachliche Zusammenfassung

Das System wird um ein Playlist-Feature erweitert, das authentifizierten Anwendern die Verwaltung eigener Playlists mit konfigurable Sortierung ermöglicht. Eine Playlist wird durch einen Pflichtname, optionale Beschreibung und einen Sortiermodus definiert (automatisch nach Erscheinungsdatum oder manuell). Anwender können Playlists erstellen, umbenennen, löschen und eine zentrale Übersicht aller ihrer Playlists einsehen. Die Verwaltung ist benutzer-bezogen und privat: Nur der Besitzer einer Playlist hat Zugriff. Das Feature unterstützt optional eine konfigurierbare maximale Anzahl von Playlists pro Anwender, wobei diese Einschränkung standardmäßig deaktiviert ist.

## Betroffene Klassen und Komponenten

### Datenmodell
- **`Playlist`** (neu): Entität für Playlist-Verwaltung mit den Eigenschaften:
  - `Id` (long, Primärschlüssel)
  - `UserId` (string, Fremdschlüssel zu `ApplicationUser`)
  - `Name` (string, Pflichtfeld, nicht leer)
  - `Description` (string?, optional)
  - `SortMode` (Enum: `ByReleaseDate` oder `Manual`, Standard: `ByReleaseDate`)
  - `CreatedAt` (DateTime)
  - `UpdatedAt` (DateTime)

- **`PlaylistSortMode`** (neu): Enum mit Werten:
  - `ByReleaseDate` (automatische Sortierung nach Erscheinungsdatum)
  - `Manual` (manuelle Sortierung)

### Services und Interfaces
- **`IPlaylistService`** (neu): Service-Interface für Playlist-Verwaltung
  - `GetPlaylistsAsync(userId, cancellationToken)`: Ruft alle Playlists eines Anwenders ab
  - `GetPlaylistAsync(playlistId, userId, cancellationToken)`: Ruft eine einzelne Playlist ab (mit Ownership-Prüfung)
  - `CreatePlaylistAsync(userId, name, description, sortMode, cancellationToken)`: Erstellt eine neue Playlist
  - `UpdatePlaylistAsync(playlistId, userId, name, description, sortMode, cancellationToken)`: Aktualisiert eine Playlist (mit Ownership-Prüfung)
  - `DeletePlaylistAsync(playlistId, userId, cancellationToken)`: Löscht eine Playlist (mit Ownership-Prüfung)

- **`PlaylistService`** (neu): Implementierung von `IPlaylistService`
  - Überprüfung der Ownership (Benutzer darf nur seine eigenen Playlists verwalten)
  - Validierung: Name ist Pflichtfeld, leere Namen führen zu aussagekräftiger Fehlermeldung
  - Optionale Prüfung der maximalen Playlist-Anzahl (falls konfiguriert)
  - CRUD-Operationen gegen `ApplicationDbContext`

### Controller
- **`PlaylistsController`** (neu): REST-API-Endpunkte unter `/api/playlists`
  - `GET /api/playlists`: Liefert alle Playlists des aktuellen Anwenders (`DtoPlaylist[]`)
  - `GET /api/playlists/{id}`: Liefert eine einzelne Playlist (mit 404/403-Behandlung)
  - `POST /api/playlists`: Erstellt eine neue Playlist
  - `PUT /api/playlists/{id}`: Aktualisiert eine Playlist (Name, Beschreibung, Sortiermodus)
  - `DELETE /api/playlists/{id}`: Löscht eine Playlist
  - Alle Endpunkte erfordern Bearer-Token-Authentifizierung (`[BearerTokenCheck]`)
  - 404 bei nicht existierenden Ressourcen
  - 403 bei fehlender Berechtigung (Ownership-Verletzung)

### DTOs (Data Transfer Objects)
- **`DtoPlaylist`** (neu): Transfer-Objekt für Playlists
  - `id` (long)
  - `name` (string)
  - `description` (string?)
  - `sortMode` (string: `"ByReleaseDate"` oder `"Manual"`)
  - `createdAt` (DateTime)
  - `updatedAt` (DateTime)

- **`DtoCreatePlaylistRequest`** (neu): Request-Objekt für Playlist-Erstellung
  - `name` (string, erforderlich)
  - `description` (string?, optional)
  - `sortMode` (string?, optional, Default: `"ByReleaseDate"`)

- **`DtoUpdatePlaylistRequest`** (neu): Request-Objekt für Playlist-Updates
  - `name` (string, erforderlich)
  - `description` (string?, optional)
  - `sortMode` (string?, optional)

### Datenbank-Kontext
- **`ApplicationDbContext`** (Erweiterung):
  - Neue `DbSet<Playlist>` Property: `Playlists`
  - Neue Entity-Konfiguration für `Playlist` in `OnModelCreating()` mit Indices/Constraints

### Validierung und Fehlerbehandlung
- **Validierungen**:
  - Name ist nicht null und nicht leer → Fehlermeldung: `"Playlist-Name ist erforderlich."`
  - Whitespace-only Namen werden ebenfalls abgelehnt
  - Ownership-Validierung: Benutzer kann nur auf eigene Playlists zugreifen
  - Optionale Playlist-Limit-Validierung (wenn konfiguriert): Fehlermeldung bei Überschreitung

### Tests
- **Unit Tests**:
  - `PlaylistServiceTests`: CRUD-Operationen, Ownership-Prüfung, Validierung
  - `PlaylistsControllerTests`: Endpunkt-Tests mit Mock-Service
  
- **Integration Tests** (optional):
  - Datenbank-Tests mit echtem `ApplicationDbContext`

### UI-Komponenten (Blazor/Razor)
- **`PlaylistsList.razor`** (neu): Übersicht aller Playlists des aktuellen Anwenders
  - Listet alle Playlists auf
  - Aktionen: Öffnen, Bearbeiten (In-Place oder Modal), Löschen
  - Löschen mit Bestätigungsdialog

- **`PlaylistForm.razor`** (neu, optional): Formular für Erstellung und Bearbeitung
  - Eingabefelder: Name (Pflichtfeld), Beschreibung (optional), Sortiermodus (Dropdown)
  - Validierungsfeedback für Benutzereingaben

## Implementierungsansatz

### Event-basierte Architektur
Das Projekt folgt einer Event-basierten Architektur (siehe `EventManager` in `ApplicationDbContext`):
- Beim Erstellen einer Playlist: `PlaylistCreatedEvent` publizieren
- Beim Aktualisieren: `PlaylistUpdatedEvent` publizieren
- Beim Löschen: `PlaylistDeletedEvent` publizieren

Falls zutreffend, können Events für Signal R-Updates oder Audit-Logging genutzt werden.

### Dependency Injection
- `IPlaylistService` wird in `Program.cs` registriert (scoped oder transient, analog `IFavoritesService`)
- Service wird in `PlaylistsController` über Constructor-Injection bereitgestellt

### Authentifizierung und Authorization
- Alle Endpunkte nutzen das bestehende `[BearerTokenCheck]`-Attribut
- `CurrentUser`-Eigenschaft des `ApiBaseController` wird für Ownership-Checks herangezogen
- 403 Forbidden bei unberechtightem Zugriff (analog `FavoritesController`)

### Entity Framework Core
- `Playlist`-Entität wird mit Fluent API in `OnModelCreating()` konfiguriert
- Indices für `(UserId, Name)` zur Performance-Optimierung (Unique Index, da Namen pro User eindeutig sein sollen — *Annahme*)
- Cascade-Delete-Verhalten für Playlists bei Benutzer-Löschung (falls relevant)

### Fehlerbehandlung
- `ArgumentNullException` für Null-Checks
- `InvalidOperationException` für Validierungsfehler (z.B. leerer Name)
- `UnauthorizedAccessException` für Ownership-Verletzungen
- Controller fängt Exceptions ab und gibt passende HTTP-Statuscodes zurück

## Konfiguration

### Optional: Playlist-Limit konfigurierbar
- Konfigurationseintrag (z.B. in `appsettings.json`):
  ```json
  {
    "Playlists": {
      "MaxPlaylistsPerUser": null
    }
  }
  ```
- Wert `null` = unbegrenzt (Standard)
- Numerischer Wert = Limit pro Anwender
- `IPlaylistService` liest diese Konfiguration (über `IConfiguration` oder `IOptions<PlaylistSettings>`)

### Keine Migration für bestehende Installationen
- Neue Datenbank-Migration wird erstellt
- Bestehende Installationen führen die Migration aus; es werden keine Pre-Seed-Daten eingefügt

## Offene Fragen

1. **Unique Index auf (UserId, Name)**:  
   Sollen Playlist-Namen pro Benutzer eindeutig sein, oder sind doppelte Namen erlaubt?

2. **Sortiermodus und Playlist-Inhalt**:  
   Dieses Anforderungs-Dokument beschreibt die Playlist-Verwaltung (CRUD). Die spätere Integration von Medieneinträgen in Playlists (und deren Sortierung) ist nicht in diesem Schritt enthalten. Ist die Annahme korrekt?

3. **Soft-Delete vs. Hard-Delete**:  
   Sollen gelöschte Playlists im Datensatz bleiben (`IsDeleted`-Flag) oder vollständig entfernt werden?

4. **Audit-Logging**:  
   Sollen Änderungen an Playlists (Erstellung, Aktualisierung, Löschung) in einem Audit-Log erfasst werden?

5. **Datei- oder Konfigurationsimporte**:  
   Gibt es einen Bedarf für Playlist-Export oder -Import (z.B. JSON)?

6. **Namenslänge und -zeichen**:  
   Gibt es Einschränkungen für die maximal zulässige Namenslänge oder zulässige Zeichen?

7. **Beschreibungs-Länge**:  
   Gibt es eine maximale Länge für die optionale Beschreibung?

8. **Anwender-Löschung und Kaskadeneffekte**:  
   Sollen Playlists eines gelöschten Anwenders automatisch gelöscht werden (Cascade), oder sollen sie orphan bleiben?

9. **Client-seitige Razor-Komponenten**:  
   Sollen die Playlist-Verwaltungs-UI-Komponenten in diesem Schritt implementiert werden, oder erst in einem nachfolgenden?
