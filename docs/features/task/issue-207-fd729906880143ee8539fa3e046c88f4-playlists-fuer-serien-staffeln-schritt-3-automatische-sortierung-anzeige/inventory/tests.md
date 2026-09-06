# Tests und Test-Coverage

## Testklassen und -Methoden

### PlaylistServiceTestBase

Datei: `VideoWebPlayer.Tests/Helpers/PlaylistServiceTestBase.cs`

Basis-Klasse für alle PlaylistService-Tests. Stellt eine SQLite-In-Memory-Datenbank und einen vorkonfigurierten `PlaylistService` bereit.

**Wichtige Hilfsmethoden:**

| Methode | Kurzbeschreibung | Parameter |
|---------|------------------|-----------|
| `UnlockMediaForUserAsync` | Entsperrt ein Medium direkt für einen Benutzer durch Einfügen eines `UnlockedMediaEntry`. | `userId`, `mediaType`, `mediaId` |
| `CreateTestMediaEntryAsync` | Erstellt ein echtes Medien-Entity (Movie, TVShow, etc.) in der DB. Alle werden mit `MediaSourceId = 1` erstellt. | `mediaType`, `name` |
| `CreateTestPlaylistWithEntriesAsync` | Erstellt eine Playlist mit direktem Seeding von Einträgen. | `userId`, Tupel von `(mediaType, mediaId)` |
| `CreateTestPlaylistWithReleaseDatesAsync` | Erstellt eine Playlist mit Medien, die Erscheinungsdaten haben. | `userId`, Tupel von `(mediaType, name, releaseDate)` |

**Wichtige Test-Benutzer:**
- `_testUserId = "test-user-123"` — Der primäre Testbenutzer
- `_otherUserId = "other-user-456"` — Ein zweiter Benutzer für Zugriffsschutz-Tests

---

### PlaylistServiceTests_GetEntries

Datei: `VideoWebPlayer.Tests/Services/PlaylistServiceTests_GetEntries.cs`

Tests für `GetPlaylistEntriesAsync`.

| Test | Zeilen | Status | Beschreibung | Issue |
|------|--------|--------|--------------|-------|
| `GetEntries_ReturnsAllEntries` | 16–30 | ✓ | Ruft mehrere Einträge ab und prüft Titel. | - |
| `GetEntries_EmptyPlaylist_ReturnsEmpty` | 33–41 | ✓ | Leere Playlist liefert leeres Array. | - |
| `GetEntries_NotOwner_ThrowsPlaylistAccessDeniedException` | 44–51 | ✓ | Zugriff verweigert für Nicht-Besitzer. | - |
| `GetEntries_PlaylistNotFound_ThrowsKeyNotFoundException` | 54–60 | ✓ | Fehler, wenn Playlist nicht existiert. | - |
| `GetEntries_RemovesOrphanedEntries` | 63–75 | ✓ | Verwaiste Einträge werden entfernt. | - |
| `GetEntries_OrphanedEntry_DeletedFromDatabase` | 78–87 | ✓ | Verwaiste Einträge werden aus der DB gelöscht. | - |
| `GetEntries_CascadedEntry_ParentMediaTitleIsLoaded` | 90–109 | ✓ | Parent-Titel werden für kaskadierten Einträge geladen. | - |
| `GetEntries_EntryNotUnlocked_IsNotAccessible` | 112–122 | ✓ | TVShow ohne Freischaltung → `IsAccessible = false` | **NUR TVShow getestet** |
| `GetEntries_EntryUnlockedForCurrentUser_IsAccessible` | 125–136 | ✓ | TVShow mit Freischaltung → `IsAccessible = true` | **NUR TVShow getestet** |
| `GetEntries_ResolvesResolvedPictureId_FromMediaEntity` | 139–156 | ✓ | Bild-ID wird korrekt aufgelöst. | - |
| `GetEntries_NoPictureSet_ResolvedPictureIdIsNull` | 159–169 | ✓ | Wenn kein Bild gesetzt, ist PictureId null. | - |

**Kritische Lücken in Tests:**
1. **Keine Tests für Medientyp-Vielfalt:** Nur TVShow wird bei den Accessibility-Tests getestet. Movie, TVShowEpisode, TVShowSeason sind nicht abgedeckt.
2. **Kein Test für regulären Quellenzugriff (MediaSourceUsers):** Es wird nie überprüft, dass ein Benutzer mit Quellenzugriff auf ein Medium zugreifen darf, auch wenn es nicht freigeschaltet ist.
3. **Kein Test für Hierarchie-Auflösung:** Nicht getestet, dass Movie → MovieCollection und TVShowEpisode → TVShow aufgelöst wird.
4. **Fehlende Szenarien:**
   - Benutzer hat Quellenzugriff, kein Unlock → sollte `true` sein
   - Benutzer hat kein Quellenzugriff, aber ist Unlock → sollte `true` sein
   - Benutzer hat weder Quelle noch Unlock → sollte `false` sein

---

### PlaylistServiceTests_GetEntriesPaged

Datei: `VideoWebPlayer.Tests/Services/PlaylistServiceTests_GetEntriesPaged.cs`

Tests für `GetPlaylistEntriesPagedAsync`. Nutzt die gleichen Hilfsmethoden wie `PlaylistServiceTests_GetEntries`.

**Erwartung:** Sollte ähnliche Zugriffslogik haben wie `GetPlaylistEntriesAsync`, da beide über `BuildEntryDtosAsync` laufen.

---

### PlaylistServiceTests_AddMedia

Datei: `VideoWebPlayer.Tests/Services/PlaylistServiceTests_AddMedia.cs`

Tests für `AddMediaToPlaylistAsync`. Nutzt auch `IsEntryAccessible` über `BuildAddResultAsync`.

---

## Fehlende Test-Szenarien

### Für IsEntryAccessible / BuildEntryDtosAsync

**Szenario 1: Regulärer Quellenzugriff (MediaSourceUsers)**

```csharp
[Fact]
public async Task GetEntries_UserHasSourceAccess_IsAccessible()
{
    // Arrange
    var showId = await CreateTestMediaEntryAsync(MediaTypeValues.TVShow, "Serie mit Quellenzugriff");
    var playlistId = await CreateTestPlaylistWithEntriesAsync(_testUserId, (MediaTypeValues.TVShow, showId));
    
    // Benutzer hat Quellenzugriff für MediaSourceId=1, aber kein Unlock
    _db.MediaSourceUsers.Add(new MediaSourceUser 
    { 
        UserId = _testUserId, 
        MediaSourceId = 1 
    });
    await _db.SaveChangesAsync();

    // Act
    var result = await _service.GetPlaylistEntriesAsync(playlistId, _testUserId, CancellationToken.None);

    // Assert
    var entry = Assert.Single(result);
    Assert.True(entry.IsAccessible); // ← DERZEIT FALSCH: würde false geben!
}
```

**Szenario 2: Nur Freischaltung (kein Quellenzugriff)**

```csharp
[Fact]
public async Task GetEntries_UserUnlockedNoSourceAccess_IsAccessible()
{
    // Arrange
    var showId = await CreateTestMediaEntryAsync(MediaTypeValues.TVShow, "Nur freigeschalten");
    var playlistId = await CreateTestPlaylistWithEntriesAsync(_testUserId, (MediaTypeValues.TVShow, showId));
    
    // Benutzer hat Unlock, aber KEINEN Quellenzugriff
    await UnlockMediaForUserAsync(_testUserId, MediaTypeValues.TVShow, showId);

    // Act
    var result = await _service.GetPlaylistEntriesAsync(playlistId, _testUserId, CancellationToken.None);

    // Assert
    var entry = Assert.Single(result);
    Assert.True(entry.IsAccessible); // ← Existierender Test prüft das
}
```

**Szenario 3: Benutzer hat weder Quelle noch Unlock**

```csharp
[Fact]
public async Task GetEntries_UserNoAccessNoUnlock_IsNotAccessible()
{
    // Arrange
    var showId = await CreateTestMediaEntryAsync(MediaTypeValues.TVShow, "Keine Berechtigung");
    var playlistId = await CreateTestPlaylistWithEntriesAsync(_testUserId, (MediaTypeValues.TVShow, showId));

    // Act
    var result = await _service.GetPlaylistEntriesAsync(playlistId, _testUserId, CancellationToken.None);

    // Assert
    var entry = Assert.Single(result);
    Assert.False(entry.IsAccessible); // ← Existierender Test prüft das
}
```

### Für andere Medientypen

**Szenario 4: Movie mit MovieCollection-Unlock**

```csharp
[Fact]
public async Task GetEntries_MovieWithCollectionUnlock_IsAccessible()
{
    // Arrange
    var movie = new Movie { Name = "Film", MediaSourceId = 1, CreatedAt = DateTime.UtcNow };
    _db.Movies.Add(movie);
    await _db.SaveChangesAsync();
    
    var collection = new MovieCollection { Name = "Sammlung", MediaSourceId = 1, CreatedAt = DateTime.UtcNow };
    _db.MovieCollections.Add(collection);
    await _db.SaveChangesAsync();
    
    movie.MovieCollectionId = collection.Id;
    await _db.SaveChangesAsync();
    
    var playlistId = await CreateTestPlaylistWithEntriesAsync(_testUserId, (MediaTypeValues.Movie, movie.Id));
    await UnlockMediaForUserAsync(_testUserId, MediaTypeValues.MovieCollection, collection.Id);

    // Act
    var result = await _service.GetPlaylistEntriesAsync(playlistId, _testUserId, CancellationToken.None);

    // Assert
    var entry = Assert.Single(result);
    Assert.True(entry.IsAccessible); // ← DERZEIT FALSCH: würde false geben!
}
```

**Szenario 5: TVShowEpisode mit TVShow-Unlock**

```csharp
[Fact]
public async Task GetEntries_EpisodeWithShowUnlock_IsAccessible()
{
    // Arrange
    var show = new TVShow { Name = "Serie", MediaSourceId = 1, CreatedAt = DateTime.UtcNow };
    _db.TVShows.Add(show);
    await _db.SaveChangesAsync();
    
    var season = new TVShowSeason { Name = "Staffel", TVShowId = show.Id, MediaSourceId = 1, CreatedAt = DateTime.UtcNow };
    _db.TVShowSeasons.Add(season);
    await _db.SaveChangesAsync();
    
    var episode = new TVShowEpisode { Name = "Episode", TVShowSeasonId = season.Id, MediaSourceId = 1, CreatedAt = DateTime.UtcNow };
    _db.TVShowEpisodes.Add(episode);
    await _db.SaveChangesAsync();
    
    var playlistId = await CreateTestPlaylistWithEntriesAsync(_testUserId, (MediaTypeValues.TVShowEpisode, episode.Id));
    await UnlockMediaForUserAsync(_testUserId, MediaTypeValues.TVShow, show.Id);

    // Act
    var result = await _service.GetPlaylistEntriesAsync(playlistId, _testUserId, CancellationToken.None);

    // Assert
    var entry = Assert.Single(result);
    Assert.True(entry.IsAccessible); // ← DERZEIT FALSCH: würde false geben!
}
```

**Szenario 6: TVShowSeason mit TVShow-Unlock**

```csharp
[Fact]
public async Task GetEntries_SeasonWithShowUnlock_IsAccessible()
{
    // Arrange
    var show = new TVShow { Name = "Serie", MediaSourceId = 1, CreatedAt = DateTime.UtcNow };
    _db.TVShows.Add(show);
    await _db.SaveChangesAsync();
    
    var season = new TVShowSeason { Name = "Staffel", TVShowId = show.Id, MediaSourceId = 1, CreatedAt = DateTime.UtcNow };
    _db.TVShowSeasons.Add(season);
    await _db.SaveChangesAsync();
    
    var playlistId = await CreateTestPlaylistWithEntriesAsync(_testUserId, (MediaTypeValues.TVShowSeason, season.Id));
    await UnlockMediaForUserAsync(_testUserId, MediaTypeValues.TVShow, show.Id);

    // Act
    var result = await _service.GetPlaylistEntriesAsync(playlistId, _testUserId, CancellationToken.None);

    // Assert
    var entry = Assert.Single(result);
    Assert.True(entry.IsAccessible); // ← DERZEIT FALSCH: würde false geben!
}
```
