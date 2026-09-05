# Umsetzungsplan: Playlists anlegen und verwalten

## Übersicht

Das System wird um eine Playlist-Verwaltungsfunktion für authentifizierte Anwender erweitert, die ihnen ermöglicht, private Playlists mit eindeutigem Namen (pro Benutzer), optionaler Beschreibung und konfigurierbarem Sortiermodus (`ByReleaseDate` oder `Manual`) zu erstellen, zu bearbeiten und zu löschen. Die Implementierung umfasst das Datenmodell (`Playlist`, `PlaylistSortMode`), einen Service mit CRUD-Operationen und Validierung (`IPlaylistService`, `PlaylistService`), REST-API-Endpunkte (`PlaylistsController`), Events für Event-Publishing und verbindliche Blazor-UI-Komponenten (`PlaylistsList.razor`, `PlaylistForm.razor`) für die Anwender-Interaktion. Hard-Delete wird verwendet; Playlists eines gelöschten Benutzers werden automatisch mitgelöscht (Cascade-Delete).

## Designentscheidungen

Keine — Die Architektur folgt vollständig bestehenden etablierten Mustern (`FavoritesService` und `FavoritesController` als Referenz). Spezifische Designentscheidungen bezüglich Unique Index, Hard-Delete, Cascade-Delete, Event-Publishing und Validierungsregeln wurden in der Anforderungs-Klärung festgehalten und sind hier implementiert.

## Programmabläufe

### Playlist erstellen

1. Anwender ruft UI auf: `PlaylistsList.razor` mit Button „Neue Playlist"
2. `PlaylistForm.razor` öffnet sich (Modal oder separate View) im Erstellungsmodus
3. Anwender gibt Name (Pflichtfeld), optionale Beschreibung und SortMode ein, klickt „Speichern"
4. Formular validiert lokal (Name erforderlich, nicht nur Leerzeichen, max. 255 Zeichen; Beschreibung max. 2000 Zeichen)
5. Formular sendet `POST /api/playlists` mit `DtoCreatePlaylistRequest` an Controller
6. `PlaylistsController.CreatePlaylist()` extrahiert `CurrentUser.Id`, ruft `IPlaylistService.CreatePlaylistAsync(userId, name, description, sortMode)` auf
7. `PlaylistService.CreatePlaylistAsync()` führt Validierungen durch:
   - Name ist nicht null, nicht leer, nicht nur Leerzeichen → `InvalidOperationException`("Playlist-Name ist erforderlich.")
   - Name max. 255 Zeichen → `InvalidOperationException`
   - Beschreibung max. 2000 Zeichen → `InvalidOperationException`
   - Eindeutigkeit: Case-insensitive Duplikat-Prüfung auf (UserId, Name) → `InvalidOperationException`("Ein Playlist mit diesem Namen existiert bereits.")
   - Max-Playlists-Limit (falls konfiguriert) → `InvalidOperationException`
8. Service erstellt `Playlist` mit `UserId=userId`, `SortMode=(sortMode ?? "ByReleaseDate")`, `CreatedAt=DateTime.UtcNow`, `UpdatedAt=DateTime.UtcNow`
9. Service speichert in DB: `_db.Playlists.AddAsync()`, `_db.SaveChangesAsync()`
10. Service publiziert Event: `_eventManager.Publish(new PlaylistCreatedEvent(playlist))`
11. Service gibt `DtoPlaylist` zurück
12. Controller gibt `200 Ok` mit `DtoPlaylist` zurück
13. UI-Komponente aktualisiert Liste, schliesst Formular
14. Bei Fehler: Controller fängt Exceptions ab und gibt entsprechende HTTP-Status zurück (400 Bad Request für Validierungsfehler, 409 Conflict für Duplikat, 401 Unauthorized, 500 Internal Server Error)

Beteiligte Klassen/Komponenten: `PlaylistsList.razor`, `PlaylistForm.razor`, `PlaylistsController`, `PlaylistService`, `IPlaylistService`, `ApplicationDbContext`, `EventManager`, `Playlist`, `DtoCreatePlaylistRequest`, `DtoPlaylist`, `PlaylistCreatedEvent`

### Playlists auflisten

1. `PlaylistsList.razor` lädt beim `OnInitializedAsync()` alle Playlists
2. Komponente sendet `GET /api/playlists`
3. `PlaylistsController.GetPlaylists()` extrahiert `CurrentUser.Id`, ruft `IPlaylistService.GetPlaylistsAsync(userId)` auf
4. `PlaylistService.GetPlaylistsAsync()` filtert Playlists nach `UserId == userId` (Ownership-Check), konvertiert in `DtoPlaylist[]` via `Create<DtoPlaylist>()`
5. Service gibt `DtoPlaylist[]` zurück
6. Controller gibt `200 Ok` mit Array zurück
7. UI rendert Tabelle/Liste mit Spalten: Name, Beschreibung (gekürzt), SortMode, CreatedAt, UpdatedAt, Aktionen

Beteiligte Klassen/Komponenten: `PlaylistsList.razor`, `PlaylistsController`, `PlaylistService`, `IPlaylistService`, `ApplicationDbContext`, `DtoPlaylist`

### Einzelne Playlist abrufen

1. `PlaylistsController.GetPlaylist(id)` extrahiert `CurrentUser.Id`, ruft `IPlaylistService.GetPlaylistAsync(id, userId)` auf
2. `PlaylistService.GetPlaylistAsync()` lädt Playlist mit Ownership-Check (filtert nach `UserId == userId`)
3. Falls nicht gefunden: Service gibt `null` zurück
4. Falls Ownership verletzt: Service throws `UnauthorizedAccessException`
5. Service konvertiert in `DtoPlaylist` und gibt zurück
6. Controller gibt `200 Ok` zurück, falls gefunden; `404 Not Found`, falls null; `403 Forbidden`, falls Ownership verletzt; `401 Unauthorized`, falls nicht angemeldet

Beteiligte Klassen/Komponenten: `PlaylistsController`, `PlaylistService`, `IPlaylistService`, `ApplicationDbContext`

### Playlist aktualisieren

1. Anwender klickt auf „Bearbeiten" in der Liste
2. `PlaylistForm.razor` lädt Playlist-Daten via `GET /api/playlists/{id}`
3. Formular pre-füllt Felder (Name, Beschreibung, SortMode)
4. Anwender ändert Daten, klickt „Speichern"
5. Formular validiert lokal (wie bei Erstellung)
6. Formular sendet `PUT /api/playlists/{id}` mit `DtoUpdatePlaylistRequest`
7. `PlaylistsController.UpdatePlaylist(id)` extrahiert `CurrentUser.Id`, ruft `IPlaylistService.UpdatePlaylistAsync(id, userId, name, description, sortMode)` auf
8. `PlaylistService.UpdatePlaylistAsync()` führt Ownership-Check durch (filtert nach `UserId == userId`)
9. Falls nicht gefunden oder Ownership verletzt: throws `UnauthorizedAccessException`
10. Service validiert Eingaben (wie bei Erstellung, aber Duplikat-Prüfung muss aktuelle Playlist ausschließen)
11. Service aktualisiert Entity-Eigenschaften: `Name`, `Description`, `SortMode`, `UpdatedAt=DateTime.UtcNow`
12. Service speichert in DB
13. Service publiziert Event: `_eventManager.Publish(new PlaylistUpdatedEvent(playlist))`
14. Service gibt `DtoPlaylist` zurück
15. Controller gibt `200 Ok` zurück
16. UI-Komponente aktualisiert Liste

Beteiligte Klassen/Komponenten: `PlaylistForm.razor`, `PlaylistsController`, `PlaylistService`, `IPlaylistService`, `ApplicationDbContext`, `EventManager`, `DtoUpdatePlaylistRequest`, `PlaylistUpdatedEvent`

### Playlist löschen

1. Anwender klickt auf „Löschen" in der Liste
2. Bestätigungsdialog erscheint: „Möchten Sie diese Playlist wirklich löschen?"
3. Anwender bestätigt
4. UI sendet `DELETE /api/playlists/{id}`
5. `PlaylistsController.DeletePlaylist(id)` extrahiert `CurrentUser.Id`, ruft `IPlaylistService.DeletePlaylistAsync(id, userId)` auf
6. `PlaylistService.DeletePlaylistAsync()` führt Ownership-Check durch
7. Falls nicht gefunden oder Ownership verletzt: throws `UnauthorizedAccessException`
8. Service entfernt Playlist aus DB (Hard-Delete via `_db.Playlists.Remove()`)
9. Service speichert in DB
10. Service publiziert Event: `_eventManager.Publish(new PlaylistDeletedEvent(id, userId))`
11. Controller gibt `204 No Content` zurück
12. UI-Komponente entfernt Eintrag aus Liste, zeigt ggf. Bestätigungs-Toast

Beteiligte Klassen/Komponenten: `PlaylistsList.razor`, `PlaylistsController`, `PlaylistService`, `IPlaylistService`, `ApplicationDbContext`, `EventManager`, `PlaylistDeletedEvent`

### Anwender-Löschung und Cascade-Delete

1. Administratives Löschen eines Benutzers erfolgt via Identity-API
2. Datenbank-Trigger oder EF Core Cascade-Delete führt automatisch durch:
   - Alle Playlists mit `UserId == deletedUserId` werden gelöscht (Hard-Delete)
3. Wird in Datenbank-Konfiguration definiert: Foreign Key `FK_Playlists_AspNetUsers` mit `OnDelete(DeleteBehavior.Cascade)`

Beteiligte Klassen/Komponenten: `ApplicationDbContext` (OnModelCreating), `ApplicationUser`, `Playlist`

## Neue Klassen

| Klasse | Typ | Zweck |
|--------|-----|-------|
| `Playlist` | Datenmodellklasse | Entität für Playlist-Verwaltung mit Id, UserId, Name, Description, SortMode, CreatedAt, UpdatedAt |
| `PlaylistSortMode` | Enum | Sortiermodi: `ByReleaseDate` (automatisch nach Erscheinungsdatum), `Manual` (manuelle Sortierung) |
| `IPlaylistService` | Interface | Service-Schnittstelle für Playlist-CRUD und Validierung |
| `PlaylistService` | Service-Implementierung | Implementierung von `IPlaylistService` mit CRUD-Logik, Ownership-Checks, Validierung, Event-Publishing |
| `PlaylistsController` | API-Controller | REST-API-Endpunkte unter `/api/playlists` für CRUD-Operationen |
| `DtoPlaylist` | Response-DTO | Transfer-Objekt für Playlist-Responses (Id, Name, Description, SortMode, CreatedAt, UpdatedAt) |
| `DtoCreatePlaylistRequest` | Request-DTO | Transfer-Objekt für Playlist-Erstellung (Name, Description?, SortMode?) |
| `DtoUpdatePlaylistRequest` | Request-DTO | Transfer-Objekt für Playlist-Updates (Name, Description?, SortMode?) |
| `PlaylistCreatedEvent` | Event | Event beim Erstellen einer Playlist |
| `PlaylistUpdatedEvent` | Event | Event beim Aktualisieren einer Playlist |
| `PlaylistDeletedEvent` | Event | Event beim Löschen einer Playlist |
| `PlaylistsList.razor` | Blazor-Komponente | UI-Übersicht aller Playlists des Anwenders mit Tabelle, Aktionen (Bearbeiten, Löschen, Öffnen), Bestätigungsdialog, „Neue Playlist"-Button |
| `PlaylistForm.razor` | Blazor-Komponente | UI-Formular zum Erstellen und Bearbeiten von Playlists mit Validierungsfeedback |

## Änderungen an bestehenden Klassen

### `ApplicationDbContext` (Erweiterung)

- **Neue Properties:** `DbSet<Playlist> Playlists` — DbSet für Playlist-Entitäten
- **Änderung in `OnModelCreating()`:**
  - Entity-Konfiguration für `Playlist`:
    - Primary Key: `Id` (long, auto-increment)
    - Foreign Key: `UserId` (string) → `ApplicationUser.Id` mit `OnDelete(DeleteBehavior.Cascade)`
    - Unique Index auf `(UserId, Name)` mit case-insensitiver Sortierung (SQL Server: `COLLATE SQL_Latin1_General_CP1_CI_AS` oder äquivalent)
    - Constraints: `Name` NotNull, `UserId` NotNull
    - Column Types: `Name` varchar(255), `Description` varchar(2000) nullable

### `ServiceCollectionExtensions.cs` (Erweiterung)

- **Neue Registrierung (Zeile ~229, neben `IFavoritesService`):**
  ```csharp
  services.AddScoped<IPlaylistService, PlaylistService>();
  ```
- **Optional: Konfigurationsklasse registrieren:**
  ```csharp
  services.Configure<PlaylistSettings>(configuration.GetSection("Playlists"));
  ```

## Datenbankmigrationen

| Migrationsname | Betroffene Tabellen/Spalten | Beschreibung der Änderung |
|---|---|---|
| `AddPlaylistsTable` | Neue Tabelle `Playlists` | Erstellt Tabelle mit Spalten: `Id` (long PK), `UserId` (string FK), `Name` (varchar 255 NOT NULL), `Description` (varchar 2000 nullable), `SortMode` (varchar 50, Default: 'ByReleaseDate'), `CreatedAt` (datetime NOT NULL), `UpdatedAt` (datetime NOT NULL). Unique Index auf (UserId, Name). Foreign Key zu `AspNetUsers` mit Cascade-Delete. |

## Validierungsregeln

| Feld / Objekt | Regel | Fehlerfall |
|---|---|---|
| `Playlist.Name` (Create/Update) | Nicht null, nicht leer, nicht nur Leerzeichen | `InvalidOperationException`: "Playlist-Name ist erforderlich." |
| `Playlist.Name` (Create/Update) | Max. 255 Zeichen | `InvalidOperationException`: "Name darf maximal 255 Zeichen lang sein." |
| `Playlist.Description` (Create/Update) | Max. 2000 Zeichen (optional) | `InvalidOperationException`: "Beschreibung darf maximal 2000 Zeichen lang sein." |
| `Playlist.Name` (Create) | Eindeutigkeit pro UserId (case-insensitiv) | `InvalidOperationException`: "Ein Playlist mit diesem Namen existiert bereits." |
| `Playlist.Name` (Update) | Eindeutigkeit pro UserId (case-insensitiv), aktuelle Playlist ausschließen | `InvalidOperationException`: "Ein Playlist mit diesem Namen existiert bereits." |
| `Playlist.SortMode` (Create/Update) | Muss "ByReleaseDate" oder "Manual" sein (optional, Default: "ByReleaseDate") | Bei ungültigen Werten: Service setzt Default oder throws Exception (TBD, empfohlen: Default setzen) |
| Ownership (alle Operationen außer Create) | `UserId` der Playlist muss `CurrentUser.Id` entsprechen | `UnauthorizedAccessException` → HTTP 403 Forbidden |

## Konfigurationsänderungen

| Eintrag | Typ | Standardwert | Zweck |
|---|---|---|---|
| `Playlists:MaxPlaylistsPerUser` | `int?` | `null` (unbegrenzt) | Maximale Anzahl von Playlists pro Anwender. `null` = keine Limit. |

**Eintrag in `appsettings.json`:**
```json
{
  "Playlists": {
    "MaxPlaylistsPerUser": null
  }
}
```

**Optional: Konfigurationsklasse (falls Max-Limit implementiert wird):**
```csharp
public class PlaylistSettings
{
    public int? MaxPlaylistsPerUser { get; set; }
}
```

Registrierung in `ServiceCollectionExtensions.cs`:
```csharp
services.Configure<PlaylistSettings>(configuration.GetSection("Playlists"));
```

## Seiteneffekte und Risiken

- **Benutzerlöschung:** Bei Löschung eines Anwenders werden alle seine Playlists automatisch gelöscht (Cascade-Delete). Dies ist beabsichtigt und verhindert verwaiste Datensätze, kann aber zu Datenverlust führen, wenn Playlists später relevant werden (z.B. für Exports). Die Anforderung bestätigt diesen Ansatz.

- **Unique Index Performance:** Der Unique Index auf `(UserId, Name)` mit case-insensitiver Sortierung kann unter hoher Last (Millionen Benutzer, Millionen Playlists) zu Contention führen. Bei normalem Betrieb sollte dies kein Problem darstellen; bei Bedarf kann eine Sharding-Strategie später umgesetzt werden.

- **Event-Publishing:** Wenn `EventManager` nicht ordnungsgemäß konfiguriert ist, werden Events nicht publiziert. Dies beeinträchtigt SignalR-Updates oder Audit-Logging, bricht aber nicht die funktionalen CRUD-Operationen.

- **DTOs und Sicherheit:** `DtoPlaylist` exponiert `UserId` nicht (via `[IgnoreAssignProperty]`), was die Sicherheit schützt. Dies ist konsistent mit bestehenden Patterns.

Keine anderen bekannten Seiteneffekte auf bestehende Features (`FavoritesService`, `FavoritesController`, andere Services).

## Umsetzungsreihenfolge

1. **Enum und Konfigurationsklasse erstellen**
   - Voraussetzungen: Keine
   - Beschreibung: Erstelle `PlaylistSortMode`-Enum in `VideoWebPlayer/Data/` mit Werten `ByReleaseDate` und `Manual`. Erstelle optional `PlaylistSettings`-Konfigurationsklasse in `VideoWebPlayer/Configuration/` mit Property `MaxPlaylistsPerUser: int?`.

2. **Playlist-Entity erstellen**
   - Voraussetzungen: Keine (abhängig von Schritt 1: `PlaylistSortMode` sollte existieren)
   - Beschreibung: Erstelle `Playlist`-Klasse in `VideoWebPlayer/Data/Entities/` mit Eigenschaften: `Id` (long), `UserId` (string), `Name` (string), `Description` (string?), `SortMode` (PlaylistSortMode, Default: ByReleaseDate), `CreatedAt` (DateTime), `UpdatedAt` (DateTime). Nutze Data Annotations (`[Key]`, `[ForeignKey]`, etc.) zur Konfiguration.

3. **Events erstellen**
   - Voraussetzungen: Keine (abhängig von Schritt 2: `Playlist`-Entity sollte existieren)
   - Beschreibung: Erstelle drei Event-Klassen in `VideoWebPlayer/Events/`:
     - `PlaylistCreatedEvent` mit Constructor `PlaylistCreatedEvent(Playlist playlist)` und Property `Playlist`
     - `PlaylistUpdatedEvent` mit Constructor `PlaylistUpdatedEvent(Playlist playlist)` und Property `Playlist`
     - `PlaylistDeletedEvent` mit Constructor `PlaylistDeletedEvent(long playlistId, string userId)` und Properties `PlaylistId`, `UserId`

4. **DTOs erstellen**
   - Voraussetzungen: Keine
   - Beschreibung: Erstelle drei DTO-Klassen in `VideoWebPlayer.Client/Models/`:
     - `DtoPlaylist`: Properties `Id`, `Name`, `Description`, `SortMode` (string), `CreatedAt`, `UpdatedAt`. Property `UserId` mit `[IgnoreAssignProperty]` (nicht exponiert).
     - `DtoCreatePlaylistRequest`: Properties `Name`, `Description`, `SortMode` (optional).
     - `DtoUpdatePlaylistRequest`: Properties `Name`, `Description`, `SortMode` (optional).

5. **ApplicationDbContext konfigurieren**
   - Voraussetzungen: Schritt 2 (`Playlist`-Entity existiert), Schritt 3 (Events existieren), `EventManager` bereits registriert (vorhanden)
   - Beschreibung: 
     - Füge Property `public DbSet<Playlist> Playlists { get; set; }` zu `ApplicationDbContext` hinzu
     - Erweitere `OnModelCreating(ModelBuilder modelBuilder)` mit Konfiguration für `Playlist`:
       ```
       modelBuilder.Entity<Playlist>(entity =>
       {
           entity.HasKey(p => p.Id);
           entity.Property(p => p.Name).IsRequired().HasMaxLength(255);
           entity.Property(p => p.Description).HasMaxLength(2000);
           entity.Property(p => p.SortMode).HasDefaultValue(PlaylistSortMode.ByReleaseDate);
           entity.Property(p => p.CreatedAt).IsRequired();
           entity.Property(p => p.UpdatedAt).IsRequired();
           entity.HasOne<ApplicationUser>()
               .WithMany()
               .HasForeignKey(p => p.UserId)
               .OnDelete(DeleteBehavior.Cascade);
           entity.HasIndex(p => new { p.UserId, p.Name }).IsUnique();
       });
       ```

6. **IPlaylistService-Interface erstellen**
   - Voraussetzungen: Keine (Schritt 2: `Playlist`-Entity sollte existieren; Schritt 4: DTOs sollten existieren)
   - Beschreibung: Erstelle `IPlaylistService`-Interface in `VideoWebPlayer/Services/` mit Methoden:
     - `Task<DtoPlaylist[]> GetPlaylistsAsync(string userId, CancellationToken cancellationToken = default)`
     - `Task<DtoPlaylist?> GetPlaylistAsync(long playlistId, string userId, CancellationToken cancellationToken = default)`
     - `Task<DtoPlaylist> CreatePlaylistAsync(string userId, string name, string? description, string? sortMode, CancellationToken cancellationToken = default)`
     - `Task<DtoPlaylist> UpdatePlaylistAsync(long playlistId, string userId, string name, string? description, string? sortMode, CancellationToken cancellationToken = default)`
     - `Task DeletePlaylistAsync(long playlistId, string userId, CancellationToken cancellationToken = default)`

7. **PlaylistService implementieren**
   - Voraussetzungen: Schritt 6 (`IPlaylistService`-Interface), Schritt 5 (ApplicationDbContext konfiguriert), Schritt 3 (Events existieren), `EventManager` (vorhanden)
   - Beschreibung: Erstelle `PlaylistService`-Klasse in `VideoWebPlayer/Services/` mit:
     - Constructor: `PlaylistService(ApplicationDbContext db, EventManager eventManager, IOptions<PlaylistSettings>? playlistSettings = null)`
     - Implementierung aller `IPlaylistService`-Methoden
     - In jedem Create/Update/Delete: Validierung, Ownership-Check (falls nicht Create), Event-Publishing
     - Duplikat-Prüfung (case-insensitiv) via LINQ: `.Where(p => p.UserId == userId && p.Name.ToLower() == name.ToLower())`
     - Max-Playlists-Limit-Prüfung (falls konfiguriert) in `CreatePlaylistAsync`
     - Exception-Handling: `InvalidOperationException` für Validierungsfehler, `UnauthorizedAccessException` für Ownership-Verletzungen

8. **Dependency Injection registrieren**
   - Voraussetzungen: Schritt 7 (`PlaylistService`), Schritt 1 (optional `PlaylistSettings`)
   - Beschreibung: 
     - Öffne `VideoWebPlayer/Extensions/ServiceCollectionExtensions.cs`
     - Füge Zeile (neben `IFavoritesService`) hinzu: `services.AddScoped<IPlaylistService, PlaylistService>();`
     - Falls `PlaylistSettings` implementiert: `services.Configure<PlaylistSettings>(configuration.GetSection("Playlists"));`

9. **Datenbank-Migration erstellen und anwenden**
   - Voraussetzungen: Schritt 5 (ApplicationDbContext konfiguriert)
   - Beschreibung:
     - Öffne Package Manager Console oder Terminal
     - Führe aus: `Add-Migration AddPlaylistsTable` (oder `dotnet ef migrations add AddPlaylistsTable`)
     - Prüfe generierte Migration in `VideoWebPlayer/Data/Migrations/`
     - Führe aus: `Update-Database` (oder `dotnet ef database update`)
     - Verifiziere, dass Tabelle `Playlists` in DB existiert mit korrektem Schema

10. **PlaylistsController erstellen**
    - Voraussetzungen: Schritt 8 (`IPlaylistService` registriert), Schritt 4 (DTOs), `ApiBaseController` (vorhanden)
    - Beschreibung: Erstelle `PlaylistsController`-Klasse in `VideoWebPlayer/Controllers/` mit:
      - Attribute: `[ApiController]`, `[Route("api/playlists")]`, `[BearerTokenCheck]`
      - Base: `ApiBaseController`
      - Constructor: `PlaylistsController(IPlaylistService playlistService, IAuthService authService, ILogger<PlaylistsController> logger)`
      - Methoden (mit Error-Handling via Try-Catch):
        - `[HttpGet] GetPlaylists()` → `IActionResult`
        - `[HttpGet("{id}")] GetPlaylist(long id)` → `IActionResult`
        - `[HttpPost] CreatePlaylist([FromBody] DtoCreatePlaylistRequest request)` → `IActionResult`
        - `[HttpPut("{id}")] UpdatePlaylist(long id, [FromBody] DtoUpdatePlaylistRequest request)` → `IActionResult`
        - `[HttpDelete("{id}")] DeletePlaylist(long id)` → `IActionResult`
      - Alle Methoden: `CheckLogedIn()` aufrufen, `CurrentUser!.Id` übergeben
      - Error-Mapping:
        - `InvalidOperationException` → `BadRequest(ex.Message)` oder `Conflict()` (für Duplikate)
        - `UnauthorizedAccessException` → `Forbidden()` oder `NotFound()` (Ownership-Verletzung)
        - Allgemein `Exception` → `StatusCode(500, "Internal server error")`

11. **PlaylistsList.razor erstellen**
    - Voraussetzungen: Schritt 10 (PlaylistsController), Schritt 4 (DTOs)
    - Beschreibung: Erstelle Blazor-Komponente in `VideoWebPlayer/Components/Playlists/`:
      - `@page "/playlists"` (oder ähnliche Route)
      - `OnInitializedAsync()`: Lade Playlists via `GET /api/playlists`
      - UI-Elemente:
        - Button „Neue Playlist erstellen" → öffne `PlaylistForm.razor` im Erstellungsmodus
        - Tabelle mit Spalten: Name, Beschreibung (gekürzt, z.B. erste 100 Zeichen), SortMode, CreatedAt, UpdatedAt, Aktionen
        - Aktionen-Spalte: Buttons „Bearbeiten", „Löschen", ggf. „Öffnen"
        - Bei „Bearbeiten": Modal/Drawer öffnet sich mit `PlaylistForm.razor` im Edit-Modus
        - Bei „Löschen": `<ConfirmDialog>` oder ähnlich anzeigen mit Bestätigung
        - Nach erfolgreichem Delete: Liste neu laden, Toast-Benachrichtigung anzeigen
      - Error-Handling: Bei 401/403/404/500 Fehlermeldung anzeigen
      - Loading-State: Spinner während Daten geladen werden

12. **PlaylistForm.razor erstellen**
    - Voraussetzungen: Schritt 11 (PlaylistsList.razor), Schritt 4 (DTOs)
    - Beschreibung: Erstelle Blazor-Komponente als Modal oder Drawer:
      - Parameter: `long? PlaylistId` (null = Create-Modus, long = Edit-Modus), `EventCallback OnSave`, `EventCallback OnCancel`
      - `OnInitializedAsync()`: Falls Edit-Modus, lade Playlist-Details via `GET /api/playlists/{id}`
      - Eingabefelder:
        - `<InputText @bind-Value="playlist.Name">` mit Label „Playlist-Name" (Pflichtfeld)
        - `<InputTextArea @bind-Value="playlist.Description">` mit Label „Beschreibung" (optional)
        - `<InputSelect @bind-Value="playlist.SortMode">` mit Optionen: "ByReleaseDate" (Selected), "Manual"
      - Validierungsfeedback:
        - `@if (string.IsNullOrWhiteSpace(playlist.Name))` → zeige Error "Playlist-Name ist erforderlich."
        - `@if (playlist.Name?.Length > 255)` → zeige Error "Name darf maximal 255 Zeichen lang sein."
        - `@if (playlist.Description?.Length > 2000)` → zeige Error "Beschreibung darf maximal 2000 Zeichen lang sein."
      - Buttons: „Speichern", „Abbrechen"
      - Bei „Speichern":
        - Lokale Validierung
        - Sende `POST /api/playlists` (Create) oder `PUT /api/playlists/{id}` (Update)
        - Bei Erfolg: `OnSave` callback auslösen, Modal schliessen, Liste aktualisieren
        - Bei Fehler (409 Conflict): Zeige "Ein Playlist mit diesem Namen existiert bereits."
        - Bei anderen Fehlern: Generische Fehlermeldung anzeigen

13. **Unit Tests: PlaylistService**
    - Voraussetzungen: Schritt 7 (PlaylistService)
    - Beschreibung: Erstelle `PlaylistServiceTests`-Testklasse in `VideoWebPlayer.Tests/Services/` mit Tests für:
      - `CreatePlaylist_ValidInput_ReturnsPlaylistDto` — Validinput erfolgreich erstellt
      - `CreatePlaylist_EmptyName_ThrowsInvalidOperationException` — Leerer Name wird abgelehnt
      - `CreatePlaylist_NameTooLong_ThrowsInvalidOperationException` — Name > 255 Zeichen wird abgelehnt
      - `CreatePlaylist_DescriptionTooLong_ThrowsInvalidOperationException` — Beschreibung > 2000 Zeichen wird abgelehnt
      - `CreatePlaylist_DuplicateName_ThrowsInvalidOperationException` — Duplikat wird erkannt (case-insensitiv)
      - `CreatePlaylist_MaxPlaylistsExceeded_ThrowsInvalidOperationException` — Max-Limit-Prüfung (falls konfiguriert)
      - `CreatePlaylist_PublishesPlaylistCreatedEvent` — Event wird nach Save publiziert
      - `GetPlaylists_ReturnsUserPlaylists` — Abfrage filtert nach UserId
      - `GetPlaylists_EmptyList_ReturnsEmptyArray` — Leere Liste wird korrekt zurückgegeben
      - `GetPlaylist_ValidId_ReturnsPlaylist` — Einzelne Playlist wird geladen
      - `GetPlaylist_InvalidId_ReturnsNull` — Nicht existierende ID gibt null zurück
      - `GetPlaylist_OwnershipViolation_ThrowsUnauthorizedAccessException` — Ownership-Check wirkt
      - `UpdatePlaylist_ValidInput_UpdatesEntity` — Update funktioniert
      - `UpdatePlaylist_DuplicateName_ThrowsInvalidOperationException` — Duplikat-Prüfung beim Update (aktuelle Playlist ausgeschlossen)
      - `UpdatePlaylist_OwnershipViolation_ThrowsUnauthorizedAccessException` — Ownership-Check beim Update
      - `UpdatePlaylist_PublishesPlaylistUpdatedEvent` — Event wird nach Save publiziert
      - `DeletePlaylist_ValidInput_RemovesFromDb` — Hard-Delete entfernt komplett
      - `DeletePlaylist_OwnershipViolation_ThrowsUnauthorizedAccessException` — Ownership-Check beim Delete
      - `DeletePlaylist_PublishesPlaylistDeletedEvent` — Event wird nach Delete publiziert

14. **Unit Tests: PlaylistsController**
    - Voraussetzungen: Schritt 10 (PlaylistsController)
    - Beschreibung: Erstelle `PlaylistsControllerTests`-Testklasse mit Mock-Service und Tests für:
      - `GetPlaylists_Returns200Ok` — Erfolgreicher GET
      - `GetPlaylist_ValidId_Returns200Ok` — Erfolgreicher GET einzelner Playlist
      - `GetPlaylist_NotFound_Returns404NotFound` — 404 bei nicht existierender ID
      - `GetPlaylist_OwnershipViolation_Returns403Forbidden` — 403 bei Ownership-Verletzung
      - `CreatePlaylist_ValidInput_Returns200Ok` — Erfolgreicher POST
      - `CreatePlaylist_InvalidName_Returns400BadRequest` — 400 bei Validierungsfehler
      - `CreatePlaylist_DuplicateName_Returns409Conflict` — 409 bei Duplikat
      - `UpdatePlaylist_ValidInput_Returns200Ok` — Erfolgreicher PUT
      - `UpdatePlaylist_NotFound_Returns404NotFound` — 404 bei nicht existierender ID
      - `UpdatePlaylist_OwnershipViolation_Returns403Forbidden` — 403 bei Ownership-Verletzung
      - `DeletePlaylist_ValidInput_Returns204NoContent` — Erfolgreicher DELETE mit 204
      - `DeletePlaylist_NotFound_Returns404NotFound` — 404 bei nicht existierender ID
      - `DeletePlaylist_OwnershipViolation_Returns403Forbidden` — 403 bei Ownership-Verletzung
      - `Unauthorized_Returns401Unauthorized` — 401 wenn nicht angemeldet

15. **E2E-Tests: Playlist-Szenarien**
    - Voraussetzungen: Schritte 11, 12 (UI-Komponenten), Schritt 10 (API)
    - Beschreibung: Erstelle E2E-Testklasse (z.B. Playwright, Selenium) mit Tests für:
      - Happy Path: Playlist erstellen, auflisten, bearbeiten, löschen mit Bestätigungsdialog
      - Fehler: Leerer Name, Duplikat-Name, Zu lange Eingaben
      - Authentifizierung: Unauthentifizierter Zugriff wird blockiert
      - Ownership: Benutzer A sieht nicht die Playlists von Benutzer B
      - Validierungsfeedback: Fehlermeldungen werden angezeigt
      - SortMode-Default: Neue Playlists haben Default "ByReleaseDate"

## Tests

### Neue Tests

| Test / Hilfsmethode | Testklasse | Was wird geprüft / bereitgestellt? |
|---|---|---|
| `CreatePlaylist_ValidInput_ReturnsPlaylistDto` | `PlaylistServiceTests` | Erfolgreiches Erstellen einer Playlist mit gültigen Daten (Name, optionale Beschreibung, SortMode) |
| `CreatePlaylist_EmptyName_ThrowsInvalidOperationException` | `PlaylistServiceTests` | Validierung: Name erforderlich (nicht leer, nicht nur Leerzeichen) |
| `CreatePlaylist_NameTooLong_ThrowsInvalidOperationException` | `PlaylistServiceTests` | Validierung: Name max. 255 Zeichen |
| `CreatePlaylist_DescriptionTooLong_ThrowsInvalidOperationException` | `PlaylistServiceTests` | Validierung: Description max. 2000 Zeichen |
| `CreatePlaylist_DuplicateName_ThrowsInvalidOperationException` | `PlaylistServiceTests` | Unique-Constraint pro UserId (case-insensitiv) beim Create |
| `CreatePlaylist_MaxPlaylistsExceeded_ThrowsInvalidOperationException` | `PlaylistServiceTests` | Max-Playlist-Limit wird beachtet (falls konfiguriert) |
| `CreatePlaylist_PublishesPlaylistCreatedEvent` | `PlaylistServiceTests` | Event wird nach erfolgreichem Save publiziert |
| `GetPlaylists_ReturnsUserPlaylists` | `PlaylistServiceTests` | Abfrage filtert nach UserId und gibt `DtoPlaylist[]` zurück |
| `GetPlaylists_EmptyList_ReturnsEmptyArray` | `PlaylistServiceTests` | Leere Liste wird korrekt als `[]` zurückgegeben |
| `GetPlaylist_ValidId_ReturnsPlaylist` | `PlaylistServiceTests` | Einzelne Playlist mit gültiger ID und UserId wird geladen und als `DtoPlaylist` zurückgegeben |
| `GetPlaylist_InvalidId_ReturnsNull` | `PlaylistServiceTests` | Nicht existierende ID gibt null zurück |
| `GetPlaylist_OwnershipViolation_ThrowsUnauthorizedAccessException` | `PlaylistServiceTests` | Ownership-Check wirft Exception wenn falscher UserId |
| `UpdatePlaylist_ValidInput_UpdatesEntity` | `PlaylistServiceTests` | Update mit gültigen Daten aktualisiert alle Felder und `UpdatedAt` |
| `UpdatePlaylist_DuplicateName_ThrowsInvalidOperationException` | `PlaylistServiceTests` | Duplikat-Prüfung beim Update schließt aktuelle Playlist aus |
| `UpdatePlaylist_OwnershipViolation_ThrowsUnauthorizedAccessException` | `PlaylistServiceTests` | Ownership-Check beim Update |
| `UpdatePlaylist_PublishesPlaylistUpdatedEvent` | `PlaylistServiceTests` | Event wird nach erfolgreichem Update publiziert |
| `DeletePlaylist_ValidInput_RemovesFromDb` | `PlaylistServiceTests` | Hard-Delete entfernt Playlist komplett aus DB |
| `DeletePlaylist_OwnershipViolation_ThrowsUnauthorizedAccessException` | `PlaylistServiceTests` | Ownership-Check beim Delete |
| `DeletePlaylist_PublishesPlaylistDeletedEvent` | `PlaylistServiceTests` | Event wird nach erfolgreichem Delete publiziert |
| `GetPlaylists_Returns200Ok` | `PlaylistsControllerTests` | Erfolgreicher GET `/api/playlists` mit `200 Ok` |
| `GetPlaylist_ValidId_Returns200Ok` | `PlaylistsControllerTests` | Erfolgreicher GET `/api/playlists/{id}` mit `200 Ok` und `DtoPlaylist` |
| `GetPlaylist_NotFound_Returns404NotFound` | `PlaylistsControllerTests` | Nicht existierende ID gibt `404 Not Found` |
| `GetPlaylist_OwnershipViolation_Returns403Forbidden` | `PlaylistsControllerTests` | Ownership-Verletzung gibt `403 Forbidden` |
| `CreatePlaylist_ValidInput_Returns200Ok` | `PlaylistsControllerTests` | Erfolgreicher POST `/api/playlists` mit `200 Ok` |
| `CreatePlaylist_InvalidName_Returns400BadRequest` | `PlaylistsControllerTests` | Leerer Name gibt `400 Bad Request` mit Fehlermeldung |
| `CreatePlaylist_DuplicateName_Returns409Conflict` | `PlaylistsControllerTests` | Duplikat-Name gibt `409 Conflict` |
| `UpdatePlaylist_ValidInput_Returns200Ok` | `PlaylistsControllerTests` | Erfolgreicher PUT `/api/playlists/{id}` mit `200 Ok` |
| `UpdatePlaylist_NotFound_Returns404NotFound` | `PlaylistsControllerTests` | Nicht existierende ID gibt `404 Not Found` |
| `UpdatePlaylist_OwnershipViolation_Returns403Forbidden` | `PlaylistsControllerTests` | Ownership-Verletzung gibt `403 Forbidden` |
| `DeletePlaylist_ValidInput_Returns204NoContent` | `PlaylistsControllerTests` | Erfolgreicher DELETE `/api/playlists/{id}` mit `204 No Content` |
| `DeletePlaylist_NotFound_Returns404NotFound` | `PlaylistsControllerTests` | Nicht existierende ID gibt `404 Not Found` |
| `DeletePlaylist_OwnershipViolation_Returns403Forbidden` | `PlaylistsControllerTests` | Ownership-Verletzung gibt `403 Forbidden` |
| `Unauthorized_Returns401Unauthorized` | `PlaylistsControllerTests` | Fehlender Bearer-Token gibt `401 Unauthorized` |
| `PlaylistsList_OnInitialized_LoadsAllPlaylists` | `PlaylistsListTests` (E2E) | Komponente lädt alle Playlists beim Initialisieren via API |
| `PlaylistsList_DeleteWithConfirmation_DeletesPlaylist` | `PlaylistsListTests` (E2E) | Delete-Button zeigt Dialog, nach Bestätigung wird Playlist gelöscht und aus UI entfernt |
| `PlaylistForm_Create_ValidInput_SavesNewPlaylist` | `PlaylistFormTests` (E2E) | Formular speichert neue Playlist mit gültigen Daten |
| `PlaylistForm_Edit_ValidInput_UpdatesPlaylist` | `PlaylistFormTests` (E2E) | Formular lädt und aktualisiert existierende Playlist |
| `PlaylistForm_EmptyName_ShowsValidationError` | `PlaylistFormTests` (E2E) | Formular zeigt Validierungsfehler bei leerem Namen und speichert nicht |
| `PlaylistForm_DuplicateName_ShowsConflictError` | `PlaylistFormTests` (E2E) | Formular zeigt Fehler bei Duplikat-Name nach API-Aufruf |

### Betroffene bestehende Tests

Keine — `Playlist` ist eine neue Entität und beeinträchtigt keine existierenden Services, Controller oder Tests.

### E2E-Tests (primärer Funktionsnachweis)

| Priorität | Szenario | Testdatei / Testklasse | Abgedecktes Akzeptanzkriterium | Warum E2E nötig |
|---|---|---|---|---|
| Pflicht | Happy Path: Playlist erstellen | `E2E.PlaylistsTests.cs` | "Authentifizierter Anwender kann eine Playlist mit Name und optionaler Beschreibung erstellen; Playlist erscheint in der Übersicht" | UI-Formular-Interaktion, API-Integration, Datenbankpersistierung, Datenbindung in der Liste; Unit-/Integration-Tests können nicht verifizieren, dass der Anwender die neu erstellte Playlist im UI sieht |
| Pflicht | Happy Path: Playlists auflisten | `E2E.PlaylistsTests.cs` | "Anwender sieht eine Übersicht aller seiner Playlists mit Name, Beschreibung, SortMode, CreatedAt, UpdatedAt" | UI-Rendering der Tabelle/Liste, Korrektheit der Datenbindung, Anzeige aller Spalten; Unit-Tests des Services können nicht verifizieren, dass die UI die Daten korrekt rendert |
| Pflicht | Happy Path: Playlist bearbeiten | `E2E.PlaylistsTests.cs` | "Anwender kann eine Playlist bearbeiten (Name, Beschreibung, SortMode ändern); Änderungen sind sofort in der Übersicht sichtbar" | Modal-Öffnung, Formular-Befüllung mit bestehenden Daten, Update-API-Aufruf, Datenspeicherung, UI-Refresh; E2E ist nötig, um die gesamte UI-Interaktion zu verifizieren |
| Pflicht | Happy Path: Playlist mit Bestätigungsdialog löschen | `E2E.PlaylistsTests.cs` | "Anwender klickt Löschen, sieht Bestätigungsdialog, bestätigt, Playlist wird gelöscht und nicht mehr in der Übersicht angezeigt" | Dialog-Interaktion (Benutzer klickt "Ja" auf Bestätigung), API-Aufruf, Datenbankentfernung, Echtzeit-UI-Update; E2E ist der einzige Weg, um die Dialog-Interaktion vollständig zu testen |
| Pflicht | Fehler: Leerer Name wird in Formular abgelehnt | `E2E.PlaylistsTests.cs` | "Formular zeigt Validierungsfehler 'Playlist-Name ist erforderlich.' für leeren Namen; Speichern-Button ist deaktiviert oder API-Aufruf wird verhindert" | Formular-Validierung (ClientSide und/oder ServerSide), Error-Message-Rendering; Unit-Tests können lokale Validierung testen, aber E2E verifiziert das Zusammenspiel mit der UI |
| Pflicht | Fehler: Duplikat-Name wird abgelehnt | `E2E.PlaylistsTests.cs` | "Anwender versucht, eine Playlist mit einem bereits existierenden Namen zu erstellen (für seinen Account); API gibt 409 Conflict; Formular zeigt Fehlermeldung 'Ein Playlist mit diesem Namen existiert bereits.'" | API-Error-Handling (409), UI-Error-Message-Rendering; E2E testet die gesamte API-Antwort-Verarbeitung durch die UI |
| Pflicht | Authentifizierung: Unauthentifizierter Zugriff wird blockiert | `E2E.PlaylistsTests.cs` | "Benutzer ohne gültigen Bearer-Token kann die Playlists-Seite nicht aufrufen; Redirect auf Login-Seite oder 401-Fehler wird angezeigt" | Auth-Guard im Frontend, API-Response (401), Navigation; E2E ist nötig, um den kompletten Auth-Flow zu verifizieren |
| Pflicht | Ownership: Benutzer A sieht nicht die Playlists von Benutzer B | `E2E.PlaylistsTests.cs` (mit 2 Benutzern) | "Benutzer A erstellt Playlist X; Benutzer B sieht Playlist X nicht in seiner Übersicht (API filtert nach UserId)" | Multi-User-Szenario, API-Ownership-Check, Datenisolation; E2E mit zwei Browsern/Sessions ist notwendig, um die Datenisolation zu verifizieren |
| Optional | Validierung: Zu lange Eingaben | `E2E.PlaylistsTests.cs` | "Name > 255 Zeichen wird abgelehnt; Beschreibung > 2000 Zeichen wird abgelehnt" | Input-Length-Validierung im Formular und auf Server-Seite; Unit-Tests testen Logik, E2E verifiziert UI-Integration |
| Optional | SortMode-Standard | `E2E.PlaylistsTests.cs` | "Neu erstellte Playlists ohne expliziter SortMode-Angabe haben den Default 'ByReleaseDate'" | Default-Value-Handling; Unit-Tests testen Service-Logik, E2E verifiziert, dass der Default in der UI-Anzeige korrekt ist |

**Welche bestehenden E2E-Tests müssen angepasst werden:**

Keine — Playlist ist eine neue Funktionalität mit eigenem UI-Flow.

## Offene Punkte

Keine. Alle technischen und fachlichen Punkte wurden in der Anforderungs-Klärung beantwortet:

1. **Unique Index auf (UserId, Name):** Ja, implementieren — case-insensitive Vergleich via SQL Server Collation
2. **Soft-Delete vs. Hard-Delete:** Hard-Delete — konsistent mit `FavoriteEntry`-Pattern
3. **Audit-Logging:** Nein — Event-Publish-Infrastruktur genügt
4. **Datei-/Konfigurationsimporte:** Nein — nicht erforderlich
5. **Namenslänge und -zeichen:** Max. 255 Zeichen, Unicode erlaubt, keine Sonderzeichen-Restriktionen
6. **Beschreibungslänge:** Max. 2000 Zeichen, optional
7. **Anwender-Löschung und Kaskadeneffekte:** Ja, Cascade-Delete
8. **Client-seitige Razor-Komponenten:** Ja, verbindlich (PlaylistsList.razor, PlaylistForm.razor)

Implementierung kann unmittelbar beginnen.
