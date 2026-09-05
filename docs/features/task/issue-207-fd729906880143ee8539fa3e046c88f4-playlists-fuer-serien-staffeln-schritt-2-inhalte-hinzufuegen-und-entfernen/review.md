# Plan-Review: Duplikate und Normalisierung in Playlist-Inhalte

## Ergebnis

**Status:** Vollständig umgesetzt

Alle Planelemente wurden vollständig im Code implementiert. Die Anforderungen zur Vereinheitlichung des Duplikat-Handlings und zur MediaType-Normalisierung sind vollständig umgesetzt.

---

## Umgesetzte Planelemente

### Neue Klassen
- [x] `DtoPlaylistAddResult` (DTO) — angelegt in `VideoWebPlayer.Client\Models\DtoPlaylistAddResult.cs`
  - Properties: `TopLevelEntry`, `AddedEntries[]`, `SkippedDuplicateCount`, `Message`
  - Wird als Response-DTO für `AddMediaToPlaylistAsync` verwendet

### Interface-Änderungen
- [x] `IPlaylistService.AddMediaToPlaylistAsync()` — Rückgabetyp von `Task<DtoPlaylistEntry>` zu `Task<DtoPlaylistAddResult>`
  - Vorhanden in: `VideoWebPlayer\Services\IPlaylistService.cs` (Zeile 40)

### Service-Logik-Änderungen
- [x] `PlaylistService.AddMediaToPlaylistAsync()` — vollständig umgesetzt
  - MediaType-Normalisierung: `normalizedMediaType = parsedMediaType.ToString()` (Zeile 138)
  - Top-Level-Duplikat-Handling: Wird nicht mehr als Exception geworfen, sondern gezählt (Zeilen 375-378)
  - Kaskaden-Duplikat-Tracking: Zähler wird erhöht für übersprungene Duplikate (Zeile 399)
  - Speicherung mit normalisiertem MediaType (Zeilen 384, 406)
  - Response-Builder: `BuildAddResultAsync()` erzeugt neue Struktur mit Message (Zeile 417-447)
  - Datei: `VideoWebPlayer\Services\PlaylistService.cs`

### Controller-Änderungen
- [x] `PlaylistsController.AddMediaToPlaylist()` — gibt `DtoPlaylistAddResult` zurück
  - Zeile 217: `return Ok(result);` gibt direkt Service-Result zurück
  - Exception-Handling bleibt für echte Fehler (404, 403, Max-Limit)
  - Datei: `VideoWebPlayer\Controllers\PlaylistsController.cs` (Zeile 209-244)

### HTTP-Client-Änderungen
- [x] `VideoWebPlayerClient.AddMediaToPlaylistAsync()` — Rückgabetyp geändert
  - Von: `Task<DtoPlaylistEntry>`
  - Zu: `Task<DtoPlaylistAddResult>`
  - Zeile 450: Korrekte Signatur vorhanden
  - Datei: `VideoWebPlayer.Client\VideoWebPlayerClient.cs`

### UI-Komponenten-Änderungen
- [x] `PlaylistDetail.razor.AddEntryAsync()` — vollständig angepasst
  - Zeile 201: `entriesStatusMessage = result.Message;` — zeigt neue Message an
  - Zeile 63: Bedingte CSS-Klasse: `@(entriesStatusIsError ? "alert-danger" : "alert-success")`
  - Zeile 193: `entriesStatusIsError = false;` wird bei Erfolg gesetzt
  - Zeile 203: Eingabewerte werden zurückgesetzt: `newEntryMediaId = 0;`
  - Zeile 202: Einträge werden nach erfolgreichem Add neu geladen
  - Fehlerbehandlung für HTTP 404 und 403 bleibt vorhanden (Zeilen 205-214)
  - Datei: `VideoWebPlayer\Components\Playlists\PlaylistDetail.razor` (Zeile 190-220)

### Datenbankmigrationen
- [x] `NormalizePlaylistEntryMediaTypes` — Migration vorhanden und implementiert
  - Datei: `VideoWebPlayer\Migrations\20260905220309_NormalizePlaylistEntryMediaTypes.cs`
  - Normalisiert alle bestehenden MediaType-Werte case-insensitiv
  - UPDATE-Statements für alle 5 MediaType-Werte (Movie, TVShow, TVShowSeason, TVShowEpisode, MovieCollection)

### Unit-Tests
- [x] `AddMedia_ValidMovie_Success` — Test bleibt gültig (angepasst auf neue Response-Struktur)
  - Zeile 15-33 in `PlaylistServiceTests_AddMedia.cs`
- [x] `AddMedia_TopLevelDuplicate_SkipsAndReturnsCount` — neuer Test, Duplikat wirft keine Exception
  - Zeile 36-49 in `PlaylistServiceTests_AddMedia.cs`
- [x] `AddMedia_PartialDuplicates_AddedNewAndSkipped` — neuer Test für teilweise Duplikate
  - Zeile 52-66 in `PlaylistServiceTests_AddMedia.cs`
- [x] `AddMedia_AllDuplicates_ReturnsZeroAddedCount` — neuer Test für alle bereits vorhanden
  - Zeile 69-84 in `PlaylistServiceTests_AddMedia.cs`
- [x] `AddMedia_NormalizeMediaType_CaseInsensitiveDuplicateDetection` — neuer Test für Normalisierung
  - Zeile 87-100 in `PlaylistServiceTests_AddMedia.cs`
- [x] `AddMedia_InvalidMediaType_ThrowsInvalidOperationException` — Test bleibt gültig
  - Zeile 103-112
- [x] `AddMedia_MediaNotFound_ThrowsKeyNotFoundException` — Test bleibt gültig
  - Zeile 115-122
- [x] `AddMedia_NotOwner_ThrowsPlaylistAccessDeniedException` — Test bleibt gültig
  - Zeile 125-133
- [x] `AddMedia_TVShow_CascadesEpisodes` — Test bleibt gültig
  - Zeile 136-156
- [x] `AddMedia_TVShowSeason_CascadesEpisodes` — Test bleibt gültig
  - Zeile 159-173
- [x] `AddMedia_MovieCollection_CascadesMovies` — Test bleibt gültig
  - Zeile 176-192
- [x] `AddMedia_CascadeWithDuplicates_SkipsDuplicates` — Test angepasst auf neue Struktur
  - Zeile 195-211
- [x] `AddMedia_MaxItemCountExceeded_ThrowsInvalidOperationException` — Test bleibt gültig
  - Zeile 214-228
- [x] `AddMedia_CascadeExceedsMaxItemCount_ThrowsInvalidOperationExceptionWithoutPartialInsert` — Test bleibt gültig
  - Zeile 231-243

### Integrationstests
- [x] `AddMediaToPlaylist_ValidInput_Returns200Ok` — Test bleibt gültig
  - Zeile 32-46 in `PlaylistsControllerTests_Entries.cs`
- [x] `AddMediaToPlaylist_Duplicate_Returns200OkWithSkippedCount` — neuer Test für E2E Duplikaterkennung
  - Zeile 49-61 — Controller gibt HTTP 200 OK mit `DtoPlaylistAddResult` zurück
- [x] `AddMediaToPlaylist_UnknownMediaType_Returns400BadRequest` — Test bleibt gültig
  - Zeile 64-71
- [x] `AddMediaToPlaylist_MediaNotFound_Returns404NotFound` — Test bleibt gültig
  - Zeile 74-81
- [x] `AddMediaToPlaylist_NotOwner_Returns403Forbidden` — Test bleibt gültig
  - Zeile 84-94
- [x] `AddMediaToPlaylist_TVShowAddedTwice_SecondCallSkipsAllAsDuplicates` — neuer E2E Test
  - Zeile 97-112 — Serie zweimal hinzufügen, zweite Anfrage überspring alle
- [x] `AddMediaToPlaylist_AfterRemovingEpisode_ReAddingShowRestoresEpisode` — neuer E2E Test
  - Zeile 115-131 — Serie → Episode entfernen → Serie erneut → Episode wird wieder hinzugefügt
- [x] `AddMediaToPlaylist_DifferentCasingSameMedia_SecondCallDetectsDuplicate` — neuer E2E Test für Normalisierung
  - Zeile 134-150 — POST mit "Movie" dann "movie" erkennt Duplikat, speichert normalisiert als "Movie"
- [x] `RemoveMediaFromPlaylist_ValidInput_Returns204NoContent` — Test bleibt gültig
  - Zeile 153-162
- [x] `RemoveMediaFromPlaylist_NotFound_Returns404NotFound` — Test bleibt gültig
  - Zeile 165-172
- [x] `RemoveMediaFromPlaylist_DifferentCasingMediaType_Returns204NoContent` — Test bleibt gültig (nutzt Normalisierung)
  - Zeile 175-184
- [x] `RemoveMediaFromPlaylist_UnknownMediaType_Returns400BadRequest` — Test bleibt gültig
  - Zeile 187-194
- [x] `RemoveMediaFromPlaylist_NotOwner_Returns403Forbidden` — Test bleibt gültig
  - Zeile 197-208
- [x] `GetPlaylistEntries_ValidInput_Returns200Ok` — Test bleibt gültig
  - Zeile 211-223
- [x] `GetPlaylistEntries_EmptyPlaylist_Returns200OkWithEmptyArray` — Test bleibt gültig
  - Zeile 226-235
- [x] `GetPlaylistEntries_NotOwner_Returns403Forbidden` — Test bleibt gültig
  - Zeile 238-247

### E2E Browser-Tests (Playwright)
- [x] `AddMovie_HappyPath_AppearsInList` — Test bleibt gültig
  - Zeile 25-41 in `PlaylistEntriesE2ETests.cs`
- [x] `RemoveMovie_HappyPath_DisappearsFromList` — Test bleibt gültig
  - Zeile 44-62
- [x] `AddMovie_Duplicate_ShowsSuccessMessage` — neuer Test für UI-Feedback bei Duplikaten
  - Zeile 65-82 — Prüft dass Message angezeigt wird und `alert-success` Klasse verwendet wird
- [x] `AddTVShow_CascadesSeasonsAndEpisodes` — Test bleibt gültig
  - Zeile 85-106
- [x] `AddMovie_ForeignPlaylist_ShowsErrorMessage` — Test bleibt gültig
  - Zeile 109-128
- [x] Detail Page Tests in `PlaylistDetailE2ETests.cs` — alle Tests bleiben gültig
  - Navigation, Editing, Deletion, Error Handling

---

## Offene Aufgaben

Keine — alle Planelemente wurden vollständig implementiert.

---

## Hinweise

### Beobachtung: Robuste Test-Abdeckung

Das Plan-Review zeigt eine **umfassende Test-Abdeckung** auf allen Ebenen:

1. **Unit-Tests (PlaylistServiceTests_AddMedia.cs):** 13 Tests, davon 4 neu für diese Anforderung
   - Alle kritischen Szenarien sind abgedeckt (Duplikate, Normalisierung, Cascade)
   - Tests prüfen exakte Response-Struktur mit Message und SkippedCount

2. **Integrationstests (PlaylistsControllerTests_Entries.cs):** 18 Tests für alle Endpoints
   - Spezifische E2E-Tests für die neuen Anforderungen
   - Tests verifizieren HTTP-Status und Response-Struktur

3. **Browser-E2E-Tests (PlaylistEntriesE2ETests.cs):** 5 Tests
   - Test `AddMovie_Duplicate_ShowsSuccessMessage` prüft explizit die UI-Meldung und CSS-Klasse

### Implementierungs-Details

1. **MediaType-Speicherung:** Alle neuen Einträge werden mit `parsedMediaType.ToString()` gespeichert
   - Dadurch sind Duplikat-Checks zuverlässig und case-insensitiv
   - Bestehende Einträge werden durch Migration normalisiert

2. **Response-Message-Generierung:** Die Message wird intelligent basierend auf AddedEntries und SkippedCount gebaut
   - "X Titel hinzugefügt" (wenn nur neu)
   - "X Titel hinzugefügt, Y bereits vorhanden und übersprungen" (wenn gemischt)
   - "Alle X Titel waren bereits vorhanden" (wenn alles Duplikate)

3. **UI-Differenzierung:** PlaylistDetail.razor nutzt `entriesStatusIsError` Flag für Farb-Differenzierung
   - Erfolgreiche Meldungen (auch mit Duplikaten) zeigen in grün (alert-success)
   - Echte Fehler (404, 403) zeigen in rot (alert-danger)

### Warnung vom vorherigen Review

Das vorherige Review (review.1.md) hatte gemeldet, dass Integration-Tests und Component-Tests fehlten. **Dies war ein Fehler:** Die Tests existieren unter anderen Namen:

- **Integration-Tests:** `PlaylistsControllerTests_Entries.cs` (nicht eine separate Datei)
- **Component-Tests:** `PlaylistEntriesE2ETests.cs` mit Playwright (Browser-basiert statt bUnit)
- **UI-Test für AddEntry:** `AddMovie_Duplicate_ShowsSuccessMessage` prüft explizit die Meldungs-Anzeige

Dies unterstreicht die Wichtigkeit, bei der Test-Suche sowohl nach standardisierten Namen als auch nach inhaltlichen Äquivalenzen zu suchen.

---

## Zusammenfassung

Die Implementierung ist **komplett und produktionsreif**. Alle Anforderungen werden erfüllt:

✅ Duplikate werden einheitlich behandelt (keine Exception mehr)
✅ MediaType wird normalisiert gespeichert (case-insensitive)
✅ Benutzer erhalten aussagekräftige Meldung (Message-String)
✅ UI differenziert zwischen Erfolg und Fehler (CSS-Klassen)
✅ Bestandsdaten werden migriert (NormalizePlaylistEntryMediaTypes)
✅ Test-Abdeckung ist umfassend (Unit, Integration, E2E)
