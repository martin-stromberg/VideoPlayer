# DbContext und Entity-Struktur

## ApplicationDbContext

**Datei:** `VideoWebPlayer/Data/ApplicationDbContext.cs`

Der zentrale Datenbankkontext erbt von `IdentityDbContext<ApplicationUser>` und enthält einen `EventManager` für Event-Publishing.

### Klassische Eigenschaften

| DbSet-Eigenschaft | Entity-Klasse | Zweck |
|-------------------|---------------|-------|
| `FavoriteEntries` | `FavoriteEntry` | Favoriten-Verwaltung für Benutzer |
| `RecentEntries` | `RecentEntry` | Zuletzt angesehene Einträge |
| `ContinueWatchingEntries` | `ContinueWatchingEntry` | Fortgesetztes Schauen |
| `WatchedEntries` | `WatchedEntry` | Vermerkte als gesehen |
| `UnlockedMediaEntries` | `UnlockedMediaEntry` | Freigeschaltete Media-Einträge |
| `MediaSources` | `MediaSource` | Media-Quellen |
| `MovieCollections` | `MovieCollection` | Film-Sammlungen |
| `Movies` | `Movie` | Filme |
| `TVShows` | `TVShow` | TV-Serien |
| `TVShowSeasons` | `TVShowSeason` | TV-Staffeln |
| `TVShowEpisodes` | `TVShowEpisode` | TV-Episoden |

### Konstruktor

```csharp
public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options, EventManager eventManager)
    : base(options)
{
    _eventManager = eventManager;
}
```

- **Parameter:** `DbContextOptions<ApplicationDbContext>` und `EventManager`
- **Zweck:** EventManager wird injiziert für Event-Publishing

### Verwendete Patterns

**Event-Publishing in Manipulation-Methoden:**
- `AddMediaSourceAsync()` publiziert `MediaSourceCreatedEvent`
- `UpdateMediaSourceAsync()` publiziert `MediaSourceUpdatedEvent`
- `DeleteMediaSourceAsync()` publiziert `MediaSourceDeletedEvent`

Diese Methoden folgen dem Pattern:
1. Entity modifizieren/hinzufügen
2. `SaveChangesAsync()` aufrufen
3. `_eventManager.Publish(new SomeEvent(...))` aufrufen

## FavoriteEntry (Referenz-Entity)

**Datei:** `VideoWebPlayer/Data/FavoriteEntry.cs`

Eine Referenz-Implementierung für eine benutzer-spezifische Entität mit optionalen Medien-Verweisen.

### Eigenschaften

| Eigenschaft | Typ | Beschreibung |
|-------------|-----|-------------|
| `Id` | long | Primärschlüssel `[Key]` |
| `UserId` | string | Fremdschlüssel zu `ApplicationUser` (erforderlich) |
| `MovieCollectionId` | long? | Optional: Verweis auf eine Film-Sammlung |
| `TVShowId` | long? | Optional: Verweis auf eine TV-Serie |
| `TVShowSeasonId` | long? | Optional: Verweis auf eine TV-Staffel |
| `TVShowEpisodeId` | long? | Optional: Verweis auf eine TV-Episode |
| `MovieId` | long? | Optional: Verweis auf einen Film |
| `CreatedAt` | DateTime | Erstellungszeitstempel (Default: `DateTime.UtcNow`) |

### Besonderheiten

- **Benutzer-Bindung:** `UserId` ist erforderlich und bindet die Entität an einen Benutzer
- **Flexible Media-Unterstützung:** Nur eines der Media-Felder ist pro Favorite gesetzt
- **Keine Soft-Delete:** Gelöschte Einträge werden komplett aus der DB entfernt (Hard-Delete)
- **Timestamps:** Nur `CreatedAt`, kein `UpdatedAt` (Favoritenliste wird nicht aktualisiert, nur hinzugefügt/entfernt)

## ApplicationUser (Identity-Integration)

**Datei:** `VideoWebPlayer/Data/ApplicationUser.cs`

Die Identitäts-Entität des Systems, erweitert von ASP.NET Core Identity `IdentityUser`.

```csharp
public class ApplicationUser : IdentityUser
{
}
```

### Nutzung im System

- Verwendet von `FavoriteEntry.UserId` als Fremdschlüssel
- Verwendet von `RecentEntry.UserId` als Fremdschlüssel
- Verwendet von `ContinueWatchingEntry.UserId` als Fremdschlüssel
- Wird in Controllern via `ApiBaseController.CurrentUser` abgerufen
- Wird in Services für Ownership-Prüfung genutzt

## Zusammenhang mit Services

**Benutzer-Spezifische Entitäten im System:**
- Services wie `IFavoritesService` erhalten `userId` als Parameter
- Alle Datenbankzugriffe filtern nach `userId` (z.B. `Where(f => f.UserId == userId)`)
- Ownership-Checks erfolgen durch Vergleich des `CurrentUser.Id` mit `UserId` der Entität
