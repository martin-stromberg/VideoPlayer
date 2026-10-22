# Bestandsaufnahme: Enums

## `ProgramRuleType`
Datei: `VideoWebPlayer/Data/ProgramRuleType.cs`

| Wert | Bedeutung |
|------|-----------|
| `SeriesFixedTime = 0` | Serie zu fester Sendezeit |
| `MoviePrimeTime = 1` | Film zur Prime Time |
| `GenreRotation = 2` | Rotation durch Genre-/Format-Inhalte |
| `Repeat = 3` | Wiederholung bereits gewählten Materials |
| `HolidaySpecial = 4` | Feiertags-Special |
| `Filler = 5` | Füllmaterial zum Auffüllen von Planlücken |

## `ProgramRuleMaterialType`
Datei: `VideoWebPlayer/Data/ProgramRuleMaterialType.cs`

| Wert | Bedeutung |
|------|-----------|
| `TVShow = 0` | ganze Serie (`ProgramRule.MaterialId` → `TVShows.Id`) |
| `Movie = 1` | einzelner Film (`MaterialId` → `Movies.Id`) |
| `MovieCollection = 2` | Filmsammlung (`MaterialId` → `MovieCollections.Id`) |
| `Genre = 3` | Genre (über `ProgramRuleGenre`-Joins; `MaterialId` = Altbestand-Fallback) |
| `Playlist = 4` | Benutzer-Playlist (`MaterialId` → `Playlists.Id`) |
| `Trailer = 5` | Trailer anstehender Filme — globales Material **ohne** `MaterialId`-Referenz |

## `ProgramCursorContentType`
Datei: `VideoWebPlayer/Data/ProgramCursorContentType.cs`

| Wert | Bedeutung |
|------|-----------|
| `Movie = 0` | `Movie` |
| `TVShowEpisode = 1` | `TVShowEpisode` |
| `PlaylistEntry = 2` | `PlaylistEntry` einer Playlist-Materialregel |
| `Trailer = 3` | `MediaItem` eines `MovieMediaItem`-Links mit `IsTrailer` |

## `MediaSourceType`
Datei: `VideoWebPlayer/Data/MediaSourceType.cs`

| Wert | Bedeutung |
|------|-----------|
| `Sftp = 0` | Entfernter SFTP-Server |
| `LocalDirectory = 1` | Lokales Verzeichnis inkl. UNC |

## `PlaylistSortMode` (nur Kontext)
Datei: `VideoWebPlayer/Data/PlaylistSortMode.cs` — `Manual`/`ReleaseDate`, steuert
`ExpandPlaylistAsync`-Reihenfolge.
