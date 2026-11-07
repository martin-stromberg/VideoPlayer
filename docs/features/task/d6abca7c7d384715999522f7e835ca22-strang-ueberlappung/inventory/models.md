# Bestandsaufnahme — Datenmodell

## `ProgramSlot`

Datei: `VideoWebPlayer/Data/ProgramSlot.cs`

| Eigenschaft | Typ | Beschreibung / Zweck |
|-------------|-----|----------------------|
| `Id` | `long` | Primärschlüssel |
| `TVChannelId` | `long` | Kanal (FK auf `TVChannel`) |
| `Start` / `End` | `DateTime` | Belegungsfenster UTC |
| `MediaItemId` | `long` | Abgespieltes MediaItem (immer gesetzt, weiche Referenz) |
| `MovieId` | `long?` | Film, falls der Slot einen Film ausstrahlt |
| `TVShowEpisodeId` | `long?` | Episode, falls der Slot eine Episode ausstrahlt |
| `ProgramRuleId` | `long?` | Erzeugende Regel (weiche Referenz) |
| `RuleType` | `ProgramRuleType` | Denormalisierter Regeltyp (Füller-Erkennung) |
| `Status` | `ProgramSlotStatus` | `Scheduled` (belegt) / `Displaced` (historisch) |
| `IsClipFill` | `bool` | Clip-Füllung (keine Titel-Pick) |
| `CreatedAt` | `DateTime` | Anlagezeitpunkt |

Relevanz: Der letzte `Scheduled`-, nicht-`IsClipFill`-Slot einer Regel vor dem aktuellen `airingStartUtc` ist der letzte Pick der Vorgänger-Ausstrahlung; `MovieId`/`TVShowEpisodeId`/`MediaItemId` reichen für die Strang-Auflösung per `FindItemIndex`.

## `ProgramRuleCursor`

Datei: `VideoWebPlayer/Data/ProgramRuleCursor.cs`

Cursor je (Regel, Strang): `StrandKey` (`collection:{id}`, `show:{id}`, `other`, `clipgroup:{id}`, `null` für Flat), `LastContentType` (`ProgramCursorContentType`), `LastContentId`, `UpdatedAt`. Fortschreibung unverändert; es gibt **keinen** Rotations-Zeiger-Datensatz — und es wird keiner benötigt, wenn der Zeiger aus Slots abgeleitet wird (keine Migration, keine Backup-Registrierung nötig).

## `ProgramCursorContentType`

Datei: `VideoWebPlayer/Data/ProgramCursorContentType.cs`

| Wert | Bedeutung |
|------|-----------|
| `Movie` | Film (`Movie`) |
| `TVShowEpisode` | Serienepisode |
| `PlaylistEntry` | Playlist-Eintrag |
| `Trailer` | Trailer-MediaItem |
| `MediaItem` | Clip-Quell-MediaItem |

## `SchedulableItem` (interner Record, `ProgramScheduleService.cs:1161`)

`ContentType`, `ContentId`, `MediaItemId`, `MovieId`, `TVShowEpisodeId`, `Duration`, `StrandKey` (`""` → `other`), `AgeRatingMinAge`, `Sequenced`. Genre-Stränge enthalten `Movie`- und `TVShowEpisode`-Items.
