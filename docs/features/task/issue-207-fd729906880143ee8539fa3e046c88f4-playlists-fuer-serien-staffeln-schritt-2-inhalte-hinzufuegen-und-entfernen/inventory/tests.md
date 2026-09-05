# Bestandsaufnahme: Tests

## Testklassen für PlaylistService.AddMediaToPlaylistAsync

### `PlaylistServiceTests_AddMedia`
Datei: `VideoWebPlayer.Tests\Services\PlaylistServiceTests_AddMedia.cs`

#### Bestehende Testmethoden

| Testmethode | Zeile | Getestet | Status |
|------------|------|---------|--------|
| `AddMedia_ValidMovie_Success` | 15–29 | Normales Hinzufügen eines Films | ✅ Basic |
| `AddMedia_Duplicate_ThrowsInvalidOperationException` | 32–43 | **Top-Level-Duplikat wirft Exception** | ❌ Wird sich ändern (keine Exception nach Anforderung) |
| `AddMedia_InvalidMediaType_ThrowsInvalidOperationException` | 46–55 | Validierung ungültiger Medientyp | ✅ Bleibt |
| `AddMedia_MediaNotFound_ThrowsKeyNotFoundException` | 58–65 | Medieninhalt nicht in DB | ✅ Bleibt |
| `AddMedia_NotOwner_ThrowsPlaylistAccessDeniedException` | 68–76 | Zugriff-Prüfung | ✅ Bleibt |
| `AddMedia_TVShow_CascadesEpisodes` | 79–99 | Kaskaden funktionieren (Serie → Staffeln → Episoden) | ✅ Bleibt |
| `AddMedia_TVShowSeason_CascadesEpisodes` | 102–116 | Kaskaden für Staffel → Episoden | ✅ Bleibt |
| `AddMedia_MovieCollection_CascadesMovies` | 119–135 | Kaskaden für Filmsammlung → Filme | ✅ Bleibt |
| `AddMedia_CascadeWithDuplicates_SkipsDuplicates` | 138–152 | **Kaskaden-Duplikate werden übersprungen** | ✅ Funktioniert (stille Übersprung), aber Test wird angepasst |
| `AddMedia_MaxItemCountExceeded_ThrowsInvalidOperationException` | 155–169 | Max-Item-Limit wird beachtet | ✅ Bleibt |
| `AddMedia_CascadeExceedsMaxItemCount_ThrowsInvalidOperationExceptionWithoutPartialInsert` | 172–184 | Atomarität: Alles oder nichts bei Limit-Überschreitung | ✅ Bleibt |

#### Kritische Test-Änderung erforderlich

**Testfall: `AddMedia_Duplicate_ThrowsInvalidOperationException` (Zeile 32–43)**

Aktueller Code:
```csharp
[Fact]
public async Task AddMedia_Duplicate_ThrowsInvalidOperationException()
{
    var ct = TestContext.Current.CancellationToken;
    var playlistId = await CreateTestPlaylistWithEntriesAsync(_testUserId);
    var movieId = await CreateTestMediaEntryAsync(MediaTypeValues.Movie);
    await _service.AddMediaToPlaylistAsync(playlistId, _testUserId, MediaTypeValues.Movie, movieId, ct);

    var ex = await Assert.ThrowsAsync<InvalidOperationException>(
        () => _service.AddMediaToPlaylistAsync(playlistId, _testUserId, MediaTypeValues.Movie, movieId, ct));

    Assert.Equal("Medieninhalt bereits in dieser Playlist vorhanden.", ex.Message);
}
```

**Nach Anforderung:** Dieser Test muss umstrukturiert werden:
- ❌ Exception wird nicht mehr geworfen
- ✅ Service gibt `DtoPlaylistAddResult` mit `SkippedDuplicateCount = 1` und `Message` zurück
- Name-Vorschlag: `AddMedia_TopLevelDuplicate_SkipsAndReturnsCount`

#### Fehlende Tests (nach Anforderung erforderlich)

Laut requirement.md Zeile 66–77:

1. **`AddMedia_TopLevelDuplicate_SkipsAndReturnsCount`**
   - Fügt Film zweimal hinzu → zweite sollte als Duplikat übersprungen werden
   - Erwartet: `DtoPlaylistAddResult` mit `SkippedDuplicateCount = 1`, `AddedEntries.Length = 0`
   - Keine Exception

2. **`AddMedia_PartialDuplicates_AddedNewAndSkipped`**
   - Adds Serie mit 3 Episoden
   - Eine Episode existiert bereits
   - Erwartet: 2 neue Episoden hinzugefügt, 1 übersprungen
   - `DtoPlaylistAddResult` mit `SkippedDuplicateCount = 1`, `AddedEntries.Length = 2`

3. **`AddMedia_AllDuplicates_ReturnsZeroAddedCount`**
   - Fügt Serie hinzu, dann versucht Serie erneut hinzuzufügen (alle Episoden bereits vorhanden)
   - Erwartet: `DtoPlaylistAddResult` mit `AddedEntries.Length = 0`, `SkippedDuplicateCount = X`
   - `Message` erklärt, dass alles bereits vorhanden war
   - HTTP 200, keine Exception

4. **`AddMedia_NormalizeMediaType_CaseInsensitiveDuplicateDetection`**
   - Fügt Film mit `MediaType = "Movie"` hinzu
   - Versucht Film mit `MediaType = "movie"` (lowercase) hinzuzufügen
   - Erwartet: Duplikat erkannt, `SkippedDuplicateCount = 1`
   - Ohne diese Test: API-Bug bleibt möglich (über REST-API: zwei Einträge für gleiche Film-ID mit unterschiedlichem Casing)

### Test-Basis-Klasse
Datei: `VideoWebPlayer.Tests\Helpers\PlaylistServiceTestBase.cs`

#### Bestehende Hilfsmethoden

| Methode | Kurzbeschreibung |
|---------|------------------|
| `PlaylistServiceTestBase()` (Ctor) | Initialisiert In-Memory-DB und PlaylistService mit Test-UserIds |
| `CreateService(...)` | Factory zur Erstellung von Service mit benutzerdefinierten PlaylistSettings |
| `CreateTestMediaEntryAsync` | Erstellt echte Media-Entitäten (Movie, TVShow, TVShowSeason, TVShowEpisode, MovieCollection) in DB |
| `CreateTestPlaylistWithEntriesAsync` | Erstellt Playlist und seed sie mit optionalen (mediaType, mediaId) Einträgen |

#### Nach Anforderung erforderliche Erweiterungen?

- ❌ Keine neuen Hilfsmethoden nötig (bestehende sind ausreichend)
- ✅ Aber Assertions könnten erweitert werden, um `DtoPlaylistAddResult` zu überprüfen (Message-Content, AddedEntries-Array, etc.)

### Weitere Test-Dateien (andere Aspekte, nicht direkt änderung-betroffen)

- `PlaylistServiceTests_Create.cs` — Tests für CreatePlaylistAsync
- `PlaylistServiceTests_Delete.cs` — Tests für DeletePlaylistAsync
- `PlaylistServiceTests_Update.cs` — Tests für UpdatePlaylistAsync
- `PlaylistServiceTests_Read.cs` — Tests für GetPlaylistAsync, GetPlaylistsAsync
- `PlaylistServiceTests_GetEntries.cs` — Tests für GetPlaylistEntriesAsync
- `PlaylistServiceTests_RemoveMedia.cs` — Tests für RemoveMediaFromPlaylistAsync
- `PlaylistsControllerTests_Entries.cs` — Integration-Tests für Entries-Endpoints
- Andere Controller-Tests — Nicht direkt betroffen

### Integrationstests (nach Anforderung erforderlich)

Laut requirement.md Zeile 73–77:

1. **E2E: Dieselbe Serie zweimal hinzufügen**
   - Prüfe dass nur neue Episoden hinzugefügt werden

2. **E2E: Serie hinzufügen, Episode entfernen, Serie erneut hinzufügen**
   - Erwartung: Episode wird wieder hinzugefügt

3. **E2E: MediaType-Normalisierung über API**
   - POST mit `MediaType="Movie"` dann mit `"movie"` → eindeutige Duplikaterkennung

Diese könnten in `PlaylistsControllerTests_Entries.cs` oder einer neuen Datei `PlaylistServiceTests_AddMedia_Integration.cs` hinzugefügt werden.

### UI/Component-Tests (nach Anforderung erforderlich)

Laut requirement.md Zeile 79–81:

1. **Erfolgs-Meldung bei Duplikat-Addition korrekt dargestellt**
   - PlaylistDetail.razor zeigt die Message aus `DtoPlaylistAddResult` an

2. **Keine Fehlermeldung (rot), sondern Info-Meldung (grün/gelb) bei Duplikaten**
   - CSS-Klasse `alert-warning` oder `alert-success` statt `alert-danger`

Diese würden mit Blazor-Component-Testing (z.B. bUnit) implementiert.
