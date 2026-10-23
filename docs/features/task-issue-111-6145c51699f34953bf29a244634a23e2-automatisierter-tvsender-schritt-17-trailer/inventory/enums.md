# Bestandsaufnahme: Enums

## `ProgramRuleMaterialType`
Datei: `VideoWebPlayer/Data/ProgramRuleMaterialType.cs`

| Wert | Bedeutung |
|------|-----------|
| `TVShow = 0` | Ganze Serie |
| `Movie = 1` | Einzelner Film |
| `MovieCollection = 2` | Filmsammlung |
| `Genre = 3` | Genre (dedupliziert über Quellen) |
| `Playlist = 4` | Benutzer-Playlist |

Neu: `Trailer = 5` (Material „Trailer anstehender Filme", kein `MaterialId`-Bezug).

## `ProgramCursorContentType`
Datei: `VideoWebPlayer/Data/ProgramCursorContentType.cs`

| Wert | Bedeutung |
|------|-----------|
| `Movie = 0` | `Movie` |
| `TVShowEpisode = 1` | `TVShowEpisode` |
| `PlaylistEntry = 2` | `PlaylistEntry` |

Neu: `Trailer = 3` (`ContentId` = `MediaItemId` der Trailer-Datei).

## `ProgramRuleType` (Kontext)
`Filler = 5` — Trailer-Material ist primär für diese Regelart vorgesehen.
