# Code-Review

## Ergebnis

**Status:** Befunde vorhanden

## Hinweis zu vorherigem Durchlauf

Die beiden im vorherigen Durchlauf (`review-code.2.md`) gemeldeten Befunde wurden behoben und im aktuellen Code verifiziert:

- `EnsureNameNotDuplicateAsync` (`VideoWebPlayer/Services/PlaylistService.cs`) vergleicht den Namen jetzt direkt mit `p.Name == name` ohne redundantes `ToLower()` — die Gross-/Kleinschreibungsunabhängigkeit wird ausschliesslich über die `NOCASE`-Kollation der `Name`-Spalte (`PlaylistConfiguration.cs`) sichergestellt. Zusätzlich normalisiert die neue Migration `20260905220309_NormalizePlaylistEntryMediaTypes` bereits gespeicherte `PlaylistEntries.MediaType`-Werte auf die kanonische Schreibweise.
- `ParseMediaType` und `GetMediaTitlesAsync` teilen sich jetzt die gemeinsame private Hilfsmethode `TryParseKnownMediaType`, wodurch die Medientyp-Parsing-Logik nicht mehr dupliziert ist.

## Befunde

### PlaylistForm.razor (PlaylistForm)

- **Duplizierter Code / Kopplung an Fehlertext** — `SaveAsync` erkennt einen Namenskonflikt, indem `ex.Message.Contains("existiert bereits", StringComparison.OrdinalIgnoreCase)` ausgewertet wird (Zeile 148). Der String `"existiert bereits"` ist damit an drei Stellen dupliziert: als Fehlertext in `PlaylistService.EnsureNameNotDuplicateAsync`, als `conflictSubstring`-Vergleich in `PlaylistsController.MapInvalidOperationException` und jetzt erneut in der UI-Komponente. Im selben Branch existiert bereits ein robusterer, bereits eingeführter Mechanismus: `VideoWebPlayerClient` wirft bei einem Fehlschlag jetzt eine `HttpRequestException` mit gesetztem `StatusCode` (siehe die neue `SendAndDeserializeAsync`-Logik), und `PlaylistDetail.razor` nutzt diesen bereits konsequent (`catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.Forbidden)` bzw. `.NotFound`). `PlaylistForm.razor` folgt für den analogen Fall (409 Conflict) diesem Muster nicht, sondern verlässt sich auf brüchiges Parsen des (übersetzten) Fehlertexts.

  Empfehlung: In `SaveAsync` den Conflict-Fall über `catch (HttpRequestException ex) when (ex.StatusCode == System.Net.HttpStatusCode.Conflict)` abfangen (analog zu `PlaylistDetail.razor`) statt über `ex.Message.Contains(...)`, damit die Erkennung nicht mehr vom exakten deutschen Fehlertext abhängt und die Fehlerbehandlung innerhalb des Features einheitlich ist.

### PlaylistsController.cs (PlaylistsController)

- **Speculative Generality** — `MapInvalidOperationException(InvalidOperationException ex, string conflictSubstring = "existiert bereits")` (Zeile 196) hat einen optionalen Parameter `conflictSubstring`, der an allen vier Aufrufstellen (`CreatePlaylist`, `UpdatePlaylist`, `AddMediaToPlaylist`, `RemoveMediaFromPlaylist`) niemals explizit gesetzt wird — überall wird ausschliesslich der Default-Wert verwendet. Der Parameter bildet eine hypothetische, aktuell nicht genutzte Flexibilität ab.

  Empfehlung: Den Parameter entfernen und `"existiert bereits"` direkt im Methodenkörper verwenden, solange kein Aufrufer einen abweichenden Substring benötigt. Falls künftig unterschiedliche Konfliktarten unterschieden werden müssen, kann der Parameter zu diesem Zeitpunkt wieder eingeführt werden.

## Geprüfte Dateien

- `VideoWebPlayer.Client/Models/DtoAddMediaToPlaylistRequest.cs`
- `VideoWebPlayer.Client/Models/DtoCreatePlaylistRequest.cs`
- `VideoWebPlayer.Client/Models/DtoPlaylist.cs`
- `VideoWebPlayer.Client/Models/DtoPlaylistAddResult.cs`
- `VideoWebPlayer.Client/Models/DtoPlaylistEntry.cs`
- `VideoWebPlayer.Client/Models/DtoUpdatePlaylistRequest.cs`
- `VideoWebPlayer.Client/Models/MediaTypeValues.cs`
- `VideoWebPlayer.Client/Models/PlaylistSortModeValues.cs`
- `VideoWebPlayer.Client/VideoWebPlayerClient.cs`
- `VideoWebPlayer.Tests/Controllers/PlaylistsControllerTests_Auth.cs`
- `VideoWebPlayer.Tests/Controllers/PlaylistsControllerTests_Create.cs`
- `VideoWebPlayer.Tests/Controllers/PlaylistsControllerTests_Delete.cs`
- `VideoWebPlayer.Tests/Controllers/PlaylistsControllerTests_Entries.cs`
- `VideoWebPlayer.Tests/Controllers/PlaylistsControllerTests_Read.cs`
- `VideoWebPlayer.Tests/Controllers/PlaylistsControllerTests_Update.cs`
- `VideoWebPlayer.Tests/Helpers/PlaylistServiceTestBase.cs`
- `VideoWebPlayer.Tests/Helpers/PlaylistsControllerTestBase.cs`
- `VideoWebPlayer.Tests/Helpers/PlaylistsE2ETestBase.cs`
- `VideoWebPlayer.Tests/PlaylistDetailE2ETests.cs`
- `VideoWebPlayer.Tests/PlaylistEntriesE2ETests.cs`
- `VideoWebPlayer.Tests/PlaylistsE2ETests.cs`
- `VideoWebPlayer.Tests/Services/PlaylistServiceTests_AddMedia.cs`
- `VideoWebPlayer.Tests/Services/PlaylistServiceTests_Create.cs`
- `VideoWebPlayer.Tests/Services/PlaylistServiceTests_Delete.cs`
- `VideoWebPlayer.Tests/Services/PlaylistServiceTests_GetEntries.cs`
- `VideoWebPlayer.Tests/Services/PlaylistServiceTests_Read.cs`
- `VideoWebPlayer.Tests/Services/PlaylistServiceTests_RemoveMedia.cs`
- `VideoWebPlayer.Tests/Services/PlaylistServiceTests_Update.cs`
- `VideoWebPlayer/Components/Layout/NavMenu.razor`
- `VideoWebPlayer/Components/Playlists/PlaylistDeleteConfirmationDialog.razor`
- `VideoWebPlayer/Components/Playlists/PlaylistDetail.razor`
- `VideoWebPlayer/Components/Playlists/PlaylistForm.razor`
- `VideoWebPlayer/Components/Playlists/PlaylistsList.razor`
- `VideoWebPlayer/Configuration/PlaylistSettings.cs`
- `VideoWebPlayer/Controllers/PlaylistsController.cs`
- `VideoWebPlayer/Data/ApplicationDbContext.cs`
- `VideoWebPlayer/Data/Configurations/PlaylistConfiguration.cs`
- `VideoWebPlayer/Data/Configurations/PlaylistEntryConfiguration.cs`
- `VideoWebPlayer/Data/MediaType.cs`
- `VideoWebPlayer/Data/Playlist.cs`
- `VideoWebPlayer/Data/PlaylistEntry.cs`
- `VideoWebPlayer/Data/PlaylistSortMode.cs`
- `VideoWebPlayer/Events/PlaylistCreatedEvent.cs`
- `VideoWebPlayer/Events/PlaylistDeletedEvent.cs`
- `VideoWebPlayer/Events/PlaylistUpdatedEvent.cs`
- `VideoWebPlayer/Extensions/ServiceCollectionExtensions.cs`
- `VideoWebPlayer/Migrations/20260905081404_AddPlaylistsTable.cs`
- `VideoWebPlayer/Migrations/20260905081404_AddPlaylistsTable.Designer.cs`
- `VideoWebPlayer/Migrations/20260905165237_AddPlaylistEntriesTable.cs`
- `VideoWebPlayer/Migrations/20260905165237_AddPlaylistEntriesTable.Designer.cs`
- `VideoWebPlayer/Migrations/20260905220309_NormalizePlaylistEntryMediaTypes.cs`
- `VideoWebPlayer/Migrations/20260905220309_NormalizePlaylistEntryMediaTypes.Designer.cs`
- `VideoWebPlayer/Migrations/ApplicationDbContextModelSnapshot.cs`
- `VideoWebPlayer/Services/Exceptions/PlaylistAccessDeniedException.cs`
- `VideoWebPlayer/Services/IPlaylistService.cs`
- `VideoWebPlayer/Services/PlaylistService.cs`
- `VideoWebPlayer/appsettings.json`
