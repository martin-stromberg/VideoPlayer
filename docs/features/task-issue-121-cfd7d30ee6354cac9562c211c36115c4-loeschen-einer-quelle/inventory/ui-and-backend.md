# UI und Backend

## UI-Dateien

### `VideoWebPlayer/Components/Pages/Admin/MediaSources/MediaSourceAdmin.razor`

- Route: `/admin/mediasources`
- Kicker in der UI: „Einrichtung“
- Listet `MediaSource`-Einträge in einer Tabelle.
- Jede Zeile hat zwei Buttons:
  - `Löschen` ruft `DeleteSource(source)`
  - `Scan zurücksetzen` ruft `ResetScan(source)`
- Aktuell: `DeleteSource` führt `await DbContext.DeleteMediaSourceAsync(source); await LoadSources();` aus, ohne Fehlerbehandlung und ohne Progress.

### `VideoWebPlayer/Components/Pages/Admin/MediaSources/MediaSourceAdminDetails.razor`

- Route: `/admin/mediasources/{Id:long}`
- Detailansicht mit Lösch-Button.
- Ruft ebenfalls `DbContext.DeleteMediaSourceAsync(editSource)` auf.

## Backend-Datei

### `VideoWebPlayer/Data/ApplicationDbContext.cs`

- Enthält `DeleteMediaSourceAsync(MediaSource source)`.
- Lädt alle `MediaCollection`s der Quelle.
- Löscht rekursiv `MediaCollection`s und deren `MediaItem`s.
- Publiziert `MediaSourceDeletedEvent`.
- **Fehlend:** Löschen der `MovieMediaItems`, `TVShowEpisodeMediaItems`, `Movie`, `MovieCollection`, `TVShow`, `TVShowSeason`, `TVShowEpisode`, `MovieGenre` und `TVShowGenre`.
