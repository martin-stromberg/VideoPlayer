# Code-Review

## Ergebnis

**Status:** Befunde vorhanden

## Befunde

### PlaylistService.cs (PlaylistService)

- **Redundante Logik trotz DB-Kollation** — `EnsureNameNotDuplicateAsync` (Zeile 264-271) vergleicht weiterhin mit `p.Name.ToLower() == name.ToLower()`, obwohl die Spalte `Name` in `PlaylistConfiguration` bereits mit `.UseCollation("NOCASE")` case-insensitiv konfiguriert ist. Der vorherige Review-Durchlauf hatte genau diesen Punkt bereits adressiert; die Duplicate-Check-Logik wurde zwar wie empfohlen in eine gemeinsame Methode extrahiert, der eigentliche Kern der Empfehlung (einfacher Gleichheitsvergleich ohne `ToLower()`) wurde dabei aber nicht übernommen. Der zusätzliche `ToLower()`-Aufruf auf beiden Seiten ist unnötig, verschleiert die eigentliche Absicht (Vergleich verlässt sich nicht auf die Kollation) und verhindert ggf., dass der eindeutige Index `IX_Playlists_UserId_Name` effizient genutzt wird, da SQLite für den Vergleich zunächst `lower()` auf die Spaltenwerte anwenden muss.

  Empfehlung: Den Vergleich zu `p.Name == name` vereinfachen (ohne `ToLower()`), da die NOCASE-Kollation die Case-Insensitivität bereits auf Datenbankebene sicherstellt.

- **Doppelter Code** — Die Logik zum Parsen eines rohen `mediaType`-Strings in den internen `MediaType`-Enum-Wert samt Prüfung, ob dafür ein Handler existiert, ist zweimal fast identisch vorhanden: einmal in `ParseMediaType` (Zeile 351-357, wirft bei Fehler eine `InvalidOperationException`) und erneut inline in `GetMediaTitlesAsync` (Zeile 466, `Enum.TryParse<MediaType>(mediaType, ignoreCase: true, out var parsedType) || !MediaTypeHandlers.TryGetValue(parsedType, out var handler)`, gibt bei Fehler statt eines Wurfs ein leeres Dictionary zurück).

  Empfehlung: Eine private Hilfsmethode `TryParseKnownMediaType(string mediaType, out MediaType parsed)` extrahieren, die den `Enum.TryParse` + `MediaTypeHandlers`-Lookup kapselt, und sowohl in `ParseMediaType` (wirft bei `false`) als auch in `GetMediaTitlesAsync` (gibt bei `false` ein leeres Dictionary zurück) darauf aufbauen.

## Hinweis zu bereits behobenen Befunden aus dem vorherigen Durchlauf

Alle übrigen vier Befunde aus `review-code.1.md` wurden zwischenzeitlich vollständig behoben und im Rahmen dieses Durchlaufs verifiziert:

- Die Encoding-Fehler bei den Umlauten in den XML-Doc-Kommentaren für `Playlists`/`PlaylistEntries` in `VideoWebPlayer/Data/ApplicationDbContext.cs` (Zeilen 99-105) sind korrigiert (`für`/`Einträge` statt Replacement-Zeichen).
- `VideoWebPlayer/Extensions/ServiceCollectionExtensions.cs` enthält jetzt `using VideoWebPlayer.Configuration;` und referenziert `PlaylistSettings` über den kurzen Typnamen.
- Die vormalige God-Methode `AddMediaToPlaylistAsync` in `VideoWebPlayer/Services/PlaylistService.cs` wurde in `BuildEntriesToAddAsync` und `BuildAddResultAsync` aufgeteilt; `AddMediaToPlaylistAsync` selbst umfasst nun nur noch ca. 25 Zeilen Orchestrierung.
- `RemoveMediaFromPlaylistAsync` normalisiert den `mediaType`-Parameter jetzt konsistent über `ParseMediaType(mediaType).ToString()` vor dem Datenbankvergleich; zusätzlich wurde eine Datenmigration (`20260905220309_NormalizePlaylistEntryMediaTypes`) ergänzt, die bereits gespeicherte `PlaylistEntries`-Datensätze mit abweichender Schreibweise auf die kanonische Form vereinheitlicht, sowie Regressionstests (`AddMedia_NormalizeMediaType_CaseInsensitiveDuplicateDetection`, `RemoveMedia_DifferentCasingMediaType_RemovesEntry`, `RemoveMediaFromPlaylist_DifferentCasingMediaType_Returns204NoContent`, `AddMediaToPlaylist_DifferentCasingSameMedia_SecondCallDetectsDuplicate`).

## Geprüfte Dateien

- `VideoWebPlayer.Client/Models/DtoAddMediaToPlaylistRequest.cs`
- `VideoWebPlayer.Client/Models/DtoCreatePlaylistRequest.cs`
- `VideoWebPlayer.Client/Models/DtoPlaylist.cs`
- `VideoWebPlayer.Client/Models/DtoPlaylistAddResult.cs`
- `VideoWebPlayer.Client/Models/DtoPlaylistEntry.cs`
- `VideoWebPlayer.Client/Models/DtoUpdatePlaylistRequest.cs`
- `VideoWebPlayer.Client/Models/MediaTypeValues.cs`
- `VideoWebPlayer.Client/Models/PlaylistSortModeValues.cs`
- `VideoWebPlayer.Client/VideoWebPlayerClient.cs` (Abschnitt `#region Playlists`)
- `VideoWebPlayer/Components/Layout/NavMenu.razor` (Diff)
- `VideoWebPlayer/Components/Playlists/PlaylistDeleteConfirmationDialog.razor`
- `VideoWebPlayer/Components/Playlists/PlaylistDetail.razor`
- `VideoWebPlayer/Components/Playlists/PlaylistForm.razor`
- `VideoWebPlayer/Components/Playlists/PlaylistsList.razor`
- `VideoWebPlayer/Configuration/PlaylistSettings.cs`
- `VideoWebPlayer/Controllers/PlaylistsController.cs`
- `VideoWebPlayer/Data/ApplicationDbContext.cs` (Diff)
- `VideoWebPlayer/Data/Configurations/PlaylistConfiguration.cs`
- `VideoWebPlayer/Data/Configurations/PlaylistEntryConfiguration.cs`
- `VideoWebPlayer/Data/MediaType.cs`
- `VideoWebPlayer/Data/Playlist.cs`
- `VideoWebPlayer/Data/PlaylistEntry.cs`
- `VideoWebPlayer/Data/PlaylistSortMode.cs`
- `VideoWebPlayer/Events/PlaylistCreatedEvent.cs`
- `VideoWebPlayer/Events/PlaylistDeletedEvent.cs`
- `VideoWebPlayer/Events/PlaylistUpdatedEvent.cs`
- `VideoWebPlayer/Extensions/ServiceCollectionExtensions.cs` (Diff)
- `VideoWebPlayer/Migrations/20260905081404_AddPlaylistsTable.cs`
- `VideoWebPlayer/Migrations/20260905165237_AddPlaylistEntriesTable.cs`
- `VideoWebPlayer/Migrations/20260905220309_NormalizePlaylistEntryMediaTypes.cs`
- `VideoWebPlayer/Services/Exceptions/PlaylistAccessDeniedException.cs`
- `VideoWebPlayer/Services/IPlaylistService.cs`
- `VideoWebPlayer/Services/PlaylistService.cs`
- `VideoWebPlayer/appsettings.json` (Diff)
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

Hinweis: `VideoWebPlayer/Migrations/*.Designer.cs` und `ApplicationDbContextModelSnapshot.cs` sind EF-Core-generierte Dateien ohne manuell geschriebene Logik und wurden daher nicht im Detail auf Code-Smells geprüft. Reine Dokumentations-/Markdown-Änderungen (`docs/**`, `README.md`) sind kein Gegenstand eines technischen Code-Reviews und wurden ausgeklammert. Die Datei `VideoWebPlayer/Controllers/Exceptions/RecordNotFoundException.cs` wurde als Referenz zum Stilvergleich für `PlaylistAccessDeniedException.cs` herangezogen, ist selbst aber nicht Teil dieses Branches.
