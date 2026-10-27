# Enum-Bestandsaufnahme

## `ProgramRuleMaterialType`

Datei: `VideoWebPlayer/Data/ProgramRuleMaterialType.cs`

| Wert | Bedeutung |
|------|-----------|
| `TVShow = 0` | Ganze Serie → `ExpandShowAsync`, geordneter Strang (`TVShowEpisode`). |
| `Movie = 1` | Einzelfilm → `ExpandMovieAsync` (ein Element). |
| `MovieCollection = 2` | Filmsammlung → `ExpandMovieCollectionAsync`, **ungeordneter Ring** (`Movie`). |
| `Genre = 3` | Genre-Auswahl → `ExpandGenreAsync`: Filme im `other`-Strang (**ungeordnet**), je Serie ein `show:{id}`-Strang (**geordnet**). |
| `Playlist = 4` | Benutzer-Playlist → `ExpandPlaylistAsync`, `PlaylistEntry` → `PreservesOrder = true`. |
| `Trailer = 5` | Trailer kommender Filme → `ExpandTrailersAsync`, **ungeordneter Ring** (`Trailer`). |
| `MediaSourceClips = 6` | Unklassifizierte Clips einer Quelle → `ExpandMediaSourceClipsAsync`/`FilterClipCandidatesAsync`, **ungeordneter Ring** (`MediaItem`). |

## `ProgramRuleType`

Datei: `VideoWebPlayer/Data/ProgramRuleType.cs`

| Wert | Bedeutung |
|------|-----------|
| `SeriesFixedTime = 0` | Serienfenster zur festen Zeit. |
| `MoviePrimeTime = 1` | Spielfilm zur Primetime. |
| `GenreRotation = 2` | Rotation durch Genre-/Format-Inhalte. |
| `Repeat = 3` | Wiederholung. |
| `HolidaySpecial = 4` | Feiertags-Sondersendung. |
| `Filler = 5` | Füllmaterial für Lücken; Filler-Regeln bekommen kein Interleaving (`RuleState.SetItems`) und werden über `FillGaps` am Ringkopf gezogen. |

## `ProgramRuleRecurrence`

Datei: `VideoWebPlayer/Data/ProgramRuleRecurrence.cs`

| Wert | Bedeutung |
|------|-----------|
| `Daily = 0` | Täglich. |
| `Weekly = 1` | Wöchentlich am `DayOfWeek`. |
| `Once = 2` | Einmalig am `SingleDate`. |
| `Holiday = 3` | An Feiertagen (deutscher Kalender). |
| `Weekday = 4` | Montag–Freitag. |
| `Weekend = 5` | Samstag/Sonntag. |

## `ProgramSlotStatus`

Datei: `VideoWebPlayer/Data/ProgramSlotStatus.cs`

| Wert | Bedeutung |
|------|-----------|
| `Scheduled = 0` | Teil des wirksamen Plans, belegt Sendezeit. |
| `Displaced = 1` | Durch Re-Bewertung verdrängt (Historie, blockiert nichts). |

## `ProgramCursorContentType`

Datei: `VideoWebPlayer/Data/ProgramCursorContentType.cs`

| Wert | Bedeutung |
|------|-----------|
| `Movie = 0` | Film (`ContentId` = `Movie.Id`). Ring ungeordnet. |
| `TVShowEpisode = 1` | Serienepisode. Strang geordnet (`PreservesOrder`). |
| `PlaylistEntry = 2` | Playlist-Eintrag (`ContentId` = `PlaylistEntry.Id`). Strang geordnet (`PreservesOrder`). |
| `Trailer = 3` | Trailer-MediaItem. Ring ungeordnet. |
| `MediaItem = 4` | Rohes MediaItem (Clip-Quelle/Clip-Gruppe). Ring ungeordnet. |

## `PlaylistSortMode`

Datei: `VideoWebPlayer/Data/PlaylistSortMode.cs`

| Wert | Bedeutung |
|------|-----------|
| `ByReleaseDate` | Automatische Sortierung nach Erscheinungsdatum (Reihenfolge der Playlist-Expansion). |
| `Manual` | Manuelle Sortierung (`SortOrder`, `AddedAt`). |

## `ProgramScheduleContentScope`

Datei: `VideoWebPlayer/Data/ProgramScheduleContentScope.cs`

| Wert | Bedeutung |
|------|-----------|
| `SharedOnly = 0` | Nur für das gesamte Publikum freigegebene Inhalte werden eingeplant (Default). |
| `AllContent = 1` | Freigabeprüfung entfällt; alle expandierten Inhalte werden eingeplant. |
