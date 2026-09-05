# Code-Review

## Ergebnis

**Status:** Befunde vorhanden

## Befunde

### ApplicationDbContext.cs (ApplicationDbContext)

- **Encoding-Fehler** — Die beiden neu hinzugefügten XML-Doc-Kommentare für `Playlists` und `PlaylistEntries` (`VideoWebPlayer/Data/ApplicationDbContext.cs`, Zeilen 99-104) enthalten kaputte Umlaute in Form von Unicode-Replacement-Zeichen (`Tabelle f�r Playlists.` / `Tabelle f�r Playlist-Eintr�ge.`) statt `für`/`Einträge`. Der direkt darunterstehende, unveränderte Kommentar für `UnlockedMediaEntries` verwendet die korrekten Umlaute (`Tabelle für einzeln freigeschaltete Medieneinträge.`), was zeigt, dass die neuen Kommentare mit einer falschen Zeichenkodierung gespeichert wurden.

  Empfehlung: Die beiden Kommentare durch korrekt kodierten Text ersetzen (`Tabelle für Playlists.` / `Tabelle für Playlist-Einträge.`) und beim Speichern sicherstellen, dass die Datei als UTF-8 (mit BOM, wie der Rest der Datei) geschrieben wird.

### ServiceCollectionExtensions.cs (ServiceCollectionExtensions)

- **Namenskonventionen und Einheitlichkeit** — In `AddVideoWebPlayerServices` wird der Typ für die Options-Registrierung voll qualifiziert referenziert: `services.Configure<VideoWebPlayer.Configuration.PlaylistSettings>(...)` (Zeile 231), obwohl im gesamten übrigen File ausschließlich kurze Typnamen über `using`-Direktiven verwendet werden (z. B. `IPlaylistService`, `PlaylistService` in der Zeile direkt darüber). Es fehlt lediglich ein `using VideoWebPlayer.Configuration;`.

  Empfehlung: `using VideoWebPlayer.Configuration;` zu den bestehenden `using`-Direktiven am Dateianfang hinzufügen und die Zeile zu `services.Configure<PlaylistSettings>(configuration.GetSection("Playlists"));` vereinfachen.

### PlaylistService.cs (PlaylistService)

- **God-Methode** — `AddMediaToPlaylistAsync` (Zeilen 141-242, ca. 100 Zeilen) übernimmt in einer einzigen Methode mehrere klar trennbare Aufgaben hintereinander: Medientyp validieren/parsen, MediaId validieren, Existenz des Medieninhalts prüfen, vorhandene Playlist-Einträge laden, Top-Level-Eintrag bilden, Kaskaden-Einträge (Season/Show/MovieCollection) ermitteln, Höchstanzahl prüfen, persistieren, Titel für alle betroffenen Medientypen nachladen und abschließend die Ergebnis-DTO inkl. Nachrichtentext zusammenbauen.

  Empfehlung: Die Methode in kleinere, benannte Hilfsmethoden aufteilen, z. B. `BuildEntriesToAddAsync(...)` (Duplikatprüfung + Kaskade + Höchstanzahl-Prüfung) und `BuildAddResultAsync(...)` (Titel nachladen, DTOs bauen, Nachricht formulieren), sodass `AddMediaToPlaylistAsync` selbst nur noch die Orchestrierung dieser Schritte zeigt.

- **Doppelter Code** — Die Prüfung auf einen bereits existierenden Playlist-Namen ist in `CreatePlaylistAsync` (Zeilen 73-77) und `UpdatePlaylistAsync` (Zeilen 113-117) nahezu identisch dupliziert; sie unterscheidet sich nur durch den zusätzlichen Ausschluss `p.Id != playlistId` in `UpdatePlaylistAsync`. Zusätzlich ist der manuelle `p.Name.ToLower() == trimmedName.ToLower()`-Vergleich redundant, da die Spalte `Name` in `PlaylistConfiguration` bereits mit `.UseCollation("NOCASE")` case-insensitiv konfiguriert ist.

  Empfehlung: Eine private Hilfsmethode `EnsureNameNotDuplicateAsync(string userId, string name, long? excludePlaylistId, CancellationToken ct)` extrahieren, die von beiden Methoden aufgerufen wird, und dort auf den einfachen Gleichheitsvergleich `p.Name == name` umstellen (ohne `ToLower()`), da die NOCASE-Kollation die Case-Insensitivität bereits sicherstellt.

- **Fehlende Validierung von Eingaben** — `RemoveMediaFromPlaylistAsync` (Zeilen 245-255) übernimmt den `mediaType`-Parameter ungeprüft und unnormalisiert in die Datenbankabfrage (`e.MediaType == mediaType`). Im Gegensatz dazu normalisiert `AddMediaToPlaylistAsync` den Medientyp konsequent über `ParseMediaType(mediaType).ToString()`, bevor er mit gespeicherten Werten verglichen oder gespeichert wird. Da gespeicherte Einträge somit immer in kanonischer Schreibweise vorliegen (z. B. `"Movie"`), führt ein Aufruf von `RemoveMediaFromPlaylistAsync` mit abweichender Schreibweise (z. B. `"movie"`) zu einem fälschlicherweise ausgelösten `KeyNotFoundException`, obwohl der Eintrag existiert.

  Empfehlung: In `RemoveMediaFromPlaylistAsync` denselben Normalisierungsschritt wie in `AddMediaToPlaylistAsync` anwenden, z. B. `var normalizedMediaType = ParseMediaType(mediaType).ToString();` vor der Datenbankabfrage, und diesen normalisierten Wert für den Vergleich verwenden.

## Geprüfte Dateien

- `VideoWebPlayer.Client/Models/DtoAddMediaToPlaylistRequest.cs`
- `VideoWebPlayer.Client/Models/DtoCreatePlaylistRequest.cs`
- `VideoWebPlayer.Client/Models/DtoPlaylist.cs`
- `VideoWebPlayer.Client/Models/DtoPlaylistEntry.cs`
- `VideoWebPlayer.Client/Models/DtoUpdatePlaylistRequest.cs`
- `VideoWebPlayer.Client/Models/MediaTypeValues.cs`
- `VideoWebPlayer.Client/Models/PlaylistSortModeValues.cs`
- `VideoWebPlayer.Client/VideoWebPlayerClient.cs` (neuer Abschnitt `#region Playlists`)
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

Hinweis: `VideoWebPlayer/Migrations/*.Designer.cs` und `ApplicationDbContextModelSnapshot.cs` sind EF-Core-generierte Dateien ohne manuell geschriebene Logik und wurden daher nicht im Detail auf Code-Smells geprüft. Reine Dokumentations-/Markdown-Änderungen (`docs/**`, `README.md`) sind kein Gegenstand eines technischen Code-Reviews und wurden ausgeklammert.
