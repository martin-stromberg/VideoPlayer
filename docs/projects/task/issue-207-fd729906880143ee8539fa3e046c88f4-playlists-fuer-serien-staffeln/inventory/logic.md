# Verwandte Services und Logik

## `ContinueWatchingService`
**Datei:** `VideoWebPlayer/Services/ContinueWatchingService.cs`

Zentrale Service-Klasse zur Verwaltung von Weiterschauen-Einträgen.

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `GetListAsync(ClaimsPrincipal user, CancellationToken ct)` | public async | Ruft die Weiterschauen-Liste des aktuellen Benutzers ab |
| `ReportProgressAsync(ApplicationUser user, long? movieId, long? episodeId, TimeSpan position, TimeSpan duration, CancellationToken ct)` | public async | Meldet Wiedergabefortschritt eines Videos |
| `HideAsync(string userId, long? movieId, long? episodeId, CancellationToken ct)` | public async | Blendet einen Weiterschauen-Eintrag aus |
| `SkipAsync(string userId, long? movieId, long? episodeId, CancellationToken ct)` | public async | Überspringt einen Eintrag und navigiert zum nächsten |
| `GetUserIdAsync(ClaimsPrincipal user, CancellationToken ct)` | protected async | Ruft die Benutzer-ID aus Claims ab |
| `Create<T>(object ms)` | protected | Erstellt ein DTO durch Kopieren von Eigenschaften |

**Abhängigkeiten:**
- `ApplicationDbContext` — Datenbankzugriff
- `UserManager<ApplicationUser>` — Benutzerverwaltung
- `ContinueWatchingBuffer` — In-Memory-Puffer für Progress-Einträge
- `MediaUpdateNotificationService` — SignalR-Benachrichtigungen
- `ProgramSettingsService` — Programmeinstellungen
- `WatchedStatusService` — Gekauft-Status-Logik
- `TimeProvider` — Zeitanbieter (testbar)

**Enums:**
- `SkipResult` — Ergebnis von Skip-Operationen (NotFound, Replaced, RemovedWithoutNext)

**Status:** Vorhanden und funktionsfähig, muss um Playlist-Bezug erweitert werden.

---

## `FavoritesService`
**Datei:** `VideoWebPlayer/Services/FavoritesService.cs`

Service zur Verwaltung von Favoriten-Einträgen.

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `GetUserFavoritesAsync(ApplicationUser user, CancellationToken ct)` | public async | Ruft Favoriten des Benutzers ab |
| `AddFavoriteAsync(ApplicationUser user, long? movieId, long? episodeId, CancellationToken ct)` | public async | Fügt ein Favorite hinzu |
| `RemoveFavoriteAsync(ApplicationUser user, long? movieId, long? episodeId, CancellationToken ct)` | public async | Entfernt ein Favorite |

**Status:** Vorhanden, ähnliches Pattern für Playlist-Integration anwendbar.

---

## `GenreService`
**Datei:** `VideoWebPlayer/Services/GenreService.cs`

Service zur Verwaltung von Genres.

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `GetGenresAsync(long mediaSourceId, CancellationToken ct)` | public async | Ruft alle Genres einer MediaSource ab |
| `GetGenresByNamesAsync(long mediaSourceId, IEnumerable<string> names, CancellationToken ct)` | public async | Ruft Genres nach Namen ab |

**Status:** Vorhanden, relevant für Playlist-Genre-Ableitung.

---

## `UnlockedMediaService`
**Datei:** `VideoWebPlayer/Services/UnlockedMediaService.cs`

Service für Freischaltungs-Logik von Medien.

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `GetUnlockedItemsAsync(ApplicationUser user, long mediaSourceId, CancellationToken ct)` | public async | Ruft freigeschaltete Items ab |
| `IsItemUnlockedAsync(ApplicationUser user, long? movieId, long? episodeId, CancellationToken ct)` | public async | Prüft, ob Item freigeschalten ist |
| `GetUnlockedMoviesAsync(ApplicationUser user, CancellationToken ct)` | public async | Ruft freigeschaltete Filme ab |
| `GetUnlockedEpisodesAsync(ApplicationUser user, CancellationToken ct)` | public async | Ruft freigeschaltete Episoden ab |

**Status:** Vorhanden, relevant für Zugriffsprüfung in Playlists.

---

## `WatchedStatusService`
**Datei:** `VideoWebPlayer/Services/WatchedStatusService.cs`

Service zur Verwaltung von Gekauft-Markierungen.

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `GetWatchedItemsAsync(string userId, CancellationToken ct)` | public async | Ruft gekaufte Items eines Benutzers ab |
| `MarkAsWatchedAsync(string userId, long? movieId, long? episodeId, CancellationToken ct)` | public async | Markiert Item als gekauft |
| `MarkAsUnwatchedAsync(string userId, long? movieId, long? episodeId, CancellationToken ct)` | public async | Entfernt Gekauft-Markierung |

**Status:** Vorhanden, unabhängig von Playlists nutzbar (wie in Anforderung beschrieben).

---

## `ContinueWatchingBuffer`
**Datei:** `VideoWebPlayer/Services/ContinueWatchingBuffer.cs`

In-Memory-Puffersystem für Progress-Einträge zur Performanceoptimierung.

**Status:** Vorhanden, könnte für Playlist-Zugriffs-Pufferung genutzt werden.

---

## Datenbankzugriff
**Datei:** `VideoWebPlayer/Data/ApplicationDbContext.cs`

Entity Framework Core `DbContext` mit allen Entities und Konfigurationen.

**Bestehende DbSets:**
- `Users` — ApplicationUser
- `ContinueWatchingEntries` — ContinueWatchingEntry
- `WatchedEntries` — WatchedEntry
- `Movies` — Movie
- `TVShows` — TVShow
- `TVShowSeasons` — TVShowSeason
- `TVShowEpisodes` — TVShowEpisode
- `MovieCollections` — MovieCollection
- `Genres` — Genre
- `Pictures` — Picture
- Weitere (Actors, MediaSources, etc.)

**Status:** Muss um `DbSet<Playlist>`, `DbSet<PlaylistItem>`, `DbSet<PlaylistGenre>` erweitert werden. `ContinueWatchingEntry` muss um `PlaylistId` erweitert werden.

---

## Konfigurationen (EF Core Fluent API)
**Verzeichnis:** `VideoWebPlayer/Data/Configurations/`

Bestehende Konfigurationen für Entity-Mappings:
- `ContinueWatchingEntryConfiguration`
- `MovieConfiguration`
- `TVShowEpisodeConfiguration`
- `GenreConfiguration`
- Weitere

**Pattern:** Implementieren von `IEntityTypeConfiguration<T>` mit Indexes, Foreign Keys und Cascade-Delete-Logik.

**Status:** Muster vorhanden, muss für Playlist-Entities implementiert werden.
