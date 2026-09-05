# Bestandsaufnahme: Playlists anlegen und verwalten

Diese Bestandsaufnahme analysiert die bestehende Projektstruktur des VideoWebPlayer-Systems bezüglich der neuen Playlist-Verwaltungsfunktionalität. Das Playlist-Feature ist noch nicht implementiert, aber das System bietet bewährte Patterns und eine etablierte Architektur, auf die die Implementierung aufbauen kann.

## Zusammenfassung

**Neuer Code erforderlich:**
- `Playlist`-Entität und `PlaylistSortMode`-Enum
- `IPlaylistService` und `PlaylistService`
- `PlaylistsController` mit REST-API-Endpunkten
- `DtoPlaylist`, `DtoCreatePlaylistRequest`, `DtoUpdatePlaylistRequest` DTOs
- Events: `PlaylistCreatedEvent`, `PlaylistUpdatedEvent`, `PlaylistDeletedEvent`
- `PlaylistsList.razor` und optional `PlaylistForm.razor` UI-Komponenten

**Bestehende Patterns und Infrastruktur (vollständig dokumentiert):**
- [Datenmodell und DbContext-Integration](inventory/dbcontext-and-entities.md)
- [Service-Architektur und Dependency Injection](inventory/service-architecture.md)
- [Controller und API-Patterns](inventory/controller-patterns.md)
- [DTO-Struktur und Mapping](inventory/dto-structure.md)
- [Event-Management und EventManager](inventory/event-management.md)
- [Authentifizierung und Authorization](inventory/authentication-authorization.md)

## Details

Das System folgt einer etablierten Architektur mit folgenden Komponenten:

### Datenmodell und DbContext Integration
Siehe [dbcontext-and-entities.md](inventory/dbcontext-and-entities.md) für Details zu:
- `ApplicationDbContext` und DbSet-Registrierung
- Entitäts-Patterns (z.B. `FavoriteEntry`)
- Entity Framework Core Konfiguration

### Service-Architektur
Siehe [service-architecture.md](inventory/service-architecture.md) für Details zu:
- `IFavoritesService` und `FavoritesService` als Referenz-Pattern
- Dependency Injection in `ServiceCollectionExtensions.cs`
- Scoped vs. Singleton-Registrierung

### Controller und API-Patterns
Siehe [controller-patterns.md](inventory/controller-patterns.md) für Details zu:
- `ApiBaseController` Basis-Klasse
- `FavoritesController` als Referenz-Implementation
- `BearerTokenCheck` Authentifizierungs-Attribut
- Error Handling und Status Codes

### DTO-Struktur
Siehe [dto-structure.md](inventory/dto-structure.md) für Details zu:
- `DtoFavoriteEntry` und `DtoMediaEntry` als Referenz-DTOs
- DTO-Speicherort im Client-Projekt
- Reflection-basiertes Mapping mit `Create<T>()`

### Event-Management
Siehe [event-management.md](inventory/event-management.md) für Details zu:
- `EventManager` Klasse und Publish-Pattern
- Bestehende Events (`MediaSourceCreatedEvent`, etc.)
- Event-Publishing im `ApplicationDbContext`

### Authentifizierung und Authorization
Siehe [authentication-authorization.md](inventory/authentication-authorization.md) für Details zu:
- `CurrentUser`-Eigenschaft in `ApiBaseController`
- Ownership-Checks und Authorization
- `IAuthService` und Authentication-Pattern

## Abhängigkeiten und Strukturelle Informationen

**Wichtige Pfade:**
- Backend-Code: `VideoWebPlayer/`
- Services: `VideoWebPlayer/Services/`
- Controller: `VideoWebPlayer/Controllers/`
- DTOs und Models: `VideoWebPlayer.Client/Models/`
- Datenbank-Kontext: `VideoWebPlayer/Data/ApplicationDbContext.cs`
- Dependency Injection: `VideoWebPlayer/Extensions/ServiceCollectionExtensions.cs`

**Konfiguration:**
- JWT und Authentifizierung: `appsettings.json`
- Datenbank-Verbindungsstring: `"DefaultConnection"` in Configuration
