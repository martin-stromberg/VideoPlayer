# Business Logic und Services

## `PlaylistService`
Datei: `VideoWebPlayer/Services/PlaylistService.cs`

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| DeletePlaylistAsync(playlistId, userId, cancellationToken) | public async | Löscht eine Playlist aus der Datenbank. **Problem 3 Ort:** Wird blind auf EF-Core-Verhalten (`OnDelete(DeleteBehavior.SetNull)`) verlassen, ohne explizite Behandlung von Unique-Index-Konflikten bei `ContinueWatchingEntry`. |
| GetPlaylistsAsync(userId, cancellationToken) | public async | Abrufen aller Playlists eines Benutzers |
| GetPlaylistAsync(playlistId, userId, cancellationToken) | public async | Abrufen einer einzelnen Playlist mit Zugriffsprüfung |
| CreatePlaylistAsync(userId, name, description, sortMode, cancellationToken) | public async | Erstellt eine neue Playlist |
| UpdatePlaylistAsync(playlistId, userId, name, description, sortMode, cancellationToken) | public async | Aktualisiert Metadaten einer Playlist |
| AddMediaToPlaylistAsync(playlistId, userId, mediaType, mediaId, cancellationToken) | public async | Fügt Media zur Playlist hinzu |
| RemoveMediaFromPlaylistAsync(playlistId, userId, mediaType, mediaId, cancellationToken) | public async | Entfernt Media aus der Playlist |

**Betroffene Methode für Problem 3:**
- `DeletePlaylistAsync` (Zeile 130–136): Führt keine explizite Konfliktauflösung durch. Wenn eine `ContinueWatchingEntry` mit `PlaylistId != null` gelöscht wird und bereits ein playlist-loser Eintrag für dasselbe Video existiert, tritt ein UNIQUE-Constraint-Fehler auf.

---

## `ContinueWatchingService`
Datei: `VideoWebPlayer/Services/ContinueWatchingService.cs`

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| GetListAsync(user, cancellationToken) | public async | Lädt die Weiterschauen-Liste für einen Benutzer. **Problem 1 Ort:** `ContinueWatchingDto`-Instanzen werden ohne `Id`-Eigenschaft befüllt (Zeile 113–142). |
| EnrichPlaylistInfoAsync(list, cancellationToken) | private async | Befüllt `PlaylistName` und `PlaylistEntryId` für Einträge mit `PlaylistId`. (Zeile 167–204) |
| ReportProgressAsync(user, movieId, episodeId, position, duration, playlistId, cancellationToken) | public async | Puffert Wiedergabefortschritt für spätere Verarbeitung |
| ProcessBufferedEntryAsync(userId, movieId, episodeId, position, duration, playlistId, cancellationToken) | public async | Verarbeitet gepufferte Einträge und aktualisiert Speicher |
| Create<T>(ms) | protected | Hilfsmethode: erstellt eine DTO durch Reflection-basierte Eigenschaftszuweisung |

**Betroffene Methode für Problem 1:**
- `GetListAsync` (Zeile 100–152): Zeilen 113–142 erstellen die `ContinueWatchingDto`-Instanzen. Die `Id`-Eigenschaft wird nirgends gesetzt. Zeile 150 ruft `EnrichPlaylistInfoAsync` auf, aber auch diese setzt `Id` nicht.

---

## `IPlaylistApiClient`
Datei: `VideoWebPlayer.Client/IPlaylistApiClient.cs` (Schnittstellendefinition)

Methodensignatur für die Playlist-Playback-Start:
- `StartPlaylistAsync(playlistId, entryId): Task<DtoPlaylistPlaybackStart>` – Startet die Wiedergabe einer Playlist ab einem bestimmten Eintrag.

**Problem 4 Ort:** `DtoPlaylistPlaybackStart` hat keine `StartPositionSeconds`-Eigenschaft, daher kann die Position nicht vom Server an den Client übergeben werden.

---

## UI-Komponenten

### `ContinueWatchingList.razor`
Datei: `VideoWebPlayer/Components/Shared/Home/ContinueWatchingList.razor`

**Methoden:**
- `LoadItemsAsync()` – Lädt die Weiterschauen-Liste und befüllt die Dictionaries
- `HandleContextActionAsync(item, action)` – Behandelt Benutzeraktionen (Hide/Skip)
- `OnInitializedAsync()` – Initialisiert die Komponente beim ersten Laden

**Betroffene Codeabschnitte (Problem 1 & 2):**
- Zeilen 18–21: Dictionaries werden mit `long`-Schlüsseln definiert (`titles`, `images`, `links`, `playlistSubtitles`)
- Zeilen 50–90: Dictionaries werden nach `it.Entry.Id` (Medien-ID) indiziert, nicht nach einer eindeutigen Entry-ID → Kollisionen bei mehreren Varianten desselben Videos
- Zeile 147: `@key="it.Entry.Id"` – Der Blazor-Key ist nach Medien-ID, nicht eindeutig pro Datenbank-Zeile → Blazor-Circuit-Absturz bei mehrfachen Varianten

### `PlaylistDetail.razor`
Datei: `VideoWebPlayer/Components/Playlists/PlaylistDetail.razor`

**Betroffene Codeabschnitte (Problem 4):**
- Zeilen 86–97: `VideoPlayer`-Komponente wird ohne `StartPositionSeconds`-Parameter initialisiert. Im Gegensatz zu `ContinueWatchingList.razor` wird die Wiedergabeposition nicht übergeben.
