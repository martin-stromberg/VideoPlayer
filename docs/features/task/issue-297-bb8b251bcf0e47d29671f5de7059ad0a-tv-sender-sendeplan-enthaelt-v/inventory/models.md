# Datenmodell-Bestandsaufnahme

Das Datenmodell wird von der Anforderung voraussichtlich nicht verändert (Cursor referenzieren
Inhalte über `LastContentType`/`LastContentId`, nicht über Positionen). Relevant sind die
Tabellen, deren Zeilen die `SchedulableItem`-Ringe und deren Fortsetzungszustand bilden.

## `ProgramRule`

Datei: `VideoWebPlayer/Data/ProgramRule.cs`

| Eigenschaft | Typ | Beschreibung / Zweck |
|-------------|-----|----------------------|
| `Id` | `long` | Primärschlüssel; geht u. a. in `StrandOrderKey(ruleId, strandKey)` ein. |
| `TVChannelId` / `TVChannel` | `long` / `TVChannel` | Besitzender Kanal. |
| `Name` | `string` (required, max 255) | Regelname. |
| `RuleType` | `ProgramRuleType` | Serienfenster, Spielfilm, Genre-Rotation, Wiederholung, Feiertag, Füller. `Filler`-Regeln durchlaufen kein Interleaving (s. `RuleState.SetItems`). |
| `Recurrence` | `ProgramRuleRecurrence` | Wiederholungsmuster (täglich, wöchentlich, einmalig, Feiertag, Werktag, Wochenende). |
| `DayOfWeek` | `DayOfWeek?` | Wochentag bei `Weekly`. |
| `SingleDate` | `DateOnly?` | Datum bei `Once`. |
| `TimeOfDay` | `TimeOnly` | Lokale Sendezeit. |
| `EpisodeCount` | `int?` | Titel je Ausstrahlung; alternativ `SlotDuration` (mindestens eines nötig). |
| `SlotDuration` | `TimeSpan?` | Periodenlänge der Ausstrahlung; Voraussetzung für Clip-Gruppen/`ClipGrace`. |
| `MaterialType` | `ProgramRuleMaterialType` | Materialart, wählt die Expand-Methode. |
| `MaterialId` | `long` | Material-Referenz (polymorph, kein harter FK); bei `Genre` das erste Genre, Rest in `Genres`. |
| `Genres` | `ICollection<ProgramRuleGenre>` | Genre-Auswahl bei `MaterialType.Genre`. |
| `MediaSourceFilters` | `ICollection<ProgramRuleMediaSource>` | Quellen-Filter der Genre-Expansion (leer = alle). |
| `ClipGroups` | `ICollection<ProgramRuleClipGroup>` | Zugeordnete Clip-Gruppen (Clip-Füllung). |
| `PlaceholderGrace` | `TimeSpan` (Default 15 min) | Karenzzeit: darunter wird kein Titel mehr gestartet, Rest per Clip-Füllung. |
| `Priority` | `int` | Konfliktlösung zwischen Regeln (höher gewinnt). |
| `IsActive` | `bool` | Nimmt an der Planung teil. |
| `CreatedAt` / `UpdatedAt` | `DateTime` | Zeitstempel. |

## `ProgramRuleCursor`

Datei: `VideoWebPlayer/Data/ProgramRuleCursor.cs`

| Eigenschaft | Typ | Beschreibung / Zweck |
|-------------|-----|----------------------|
| `Id` | `long` | Primärschlüssel. |
| `ProgramRuleId` / `ProgramRule` | `long` / `ProgramRule` | Besitzende Regel (echter FK, Cascade). |
| `StrandKey` | `string?` | Strang des Cursors: `show:{TVShowId}`, `other`, `clipgroup:{id}` oder `null` (flacher Ring). Gefilterter Unique-Index: max. eine `null`-Zeile je Regel. |
| `LastContentType` | `ProgramCursorContentType` | Art des referenzierten Inhalts. |
| `LastContentId` | `long` | Zuletzt eingeplantes Element (Movie-/Episode-/PlaylistEntry-/MediaItem-Id) — **Inhalts-, nicht Positionsanker**; `NextIndexFor` sucht per `FindIndex` im aktuellen Ring. |
| `UpdatedAt` | `DateTime` | Zeitstempel; Tiebreak-Kriterium von `SelectStrandCursor`. |

## `ProgramSlot`

Datei: `VideoWebPlayer/Data/ProgramSlot.cs`

| Eigenschaft | Typ | Beschreibung / Zweck |
|-------------|-----|----------------------|
| `Id` | `long` | Primärschlüssel. |
| `TVChannelId` / `TVChannel` | `long` / `TVChannel` | Besitzender Kanal. |
| `Start` / `End` | `DateTime` | UTC-Sendezeit (End = Start + MediaItem-Dauer). |
| `MediaItemId` | `long` | Referenziertes MediaItem — bewusst kein FK (Planhistorie überlebt Löschungen). |
| `MovieId` | `long?` | Film des Slots (Soft-Referenz). |
| `TVShowEpisodeId` | `long?` | Episode des Slots (Soft-Referenz). |
| `ProgramRuleId` | `long?` | Erzeugende Regel (Soft-Referenz). |
| `RuleType` | `ProgramRuleType` | Denormalisierte Kopie des Regeltyps. |
| `Status` | `ProgramSlotStatus` | `Scheduled` (belegt Sendezeit) / `Displaced` (Historie). |
| `IsClipFill` | `bool` | Kennzeichnet Clip-Füll-Slots (denormalisiert). |
| `CreatedAt` | `DateTime` | Zeitstempel. |

## `ProgramClipGroup` / `ProgramClipGroupItem` / `ProgramRuleClipGroup`

Dateien: `VideoWebPlayer/Data/ProgramClipGroup.cs`, `ProgramClipGroupItem.cs`, `ProgramRuleClipGroup.cs`

| Eigenschaft | Typ | Beschreibung / Zweck |
|-------------|-----|----------------------|
| `ProgramClipGroup.Id` | `long` | Primärschlüssel; `TrailerGroupId = 1` ist die reservierte Systemgruppe. |
| `ProgramClipGroup.Name` | `string` | Gruppenname (unique). |
| `ProgramClipGroup.IsSystem` | `bool` | Systemgruppe (automatisch befüllt, nicht änderbar). |
| `ProgramClipGroupItem.ProgramClipGroupId` / `.MediaItemId` | `long` | Join: Clip-Mitgliedschaft (beide FKs kaskadieren). |
| `ProgramRuleClipGroup.ProgramRuleId` / `.ProgramClipGroupId` | `long` | Join: Gruppen-Zuordnung zur Regel; `OrderBy(ProgramClipGroupId)` ist die Round-robin-Reihenfolge des Clip-Fills. |

## `ProgramRuleGenre` / `ProgramRuleMediaSource`

Dateien: `VideoWebPlayer/Data/ProgramRuleGenre.cs`, `ProgramRuleMediaSource.cs`

| Eigenschaft | Typ | Beschreibung / Zweck |
|-------------|-----|----------------------|
| `ProgramRuleId` + `GenreId` | `long` | Join: Genre-Auswahl einer Genre-Regel. |
| `ProgramRuleId` + `MediaSourceId` | `long` | Join: Quellen-Filter einer Genre-Regel. |

## `Playlist` / `PlaylistEntry` (relevante Felder)

Dateien: `VideoWebPlayer/Data/Playlist.cs`, `PlaylistEntry.cs`

| Eigenschaft | Typ | Beschreibung / Zweck |
|-------------|-----|----------------------|
| `Playlist.SortMode` | `PlaylistSortMode` | `Manual` → `SortOrder`/`AddedAt`; `ByReleaseDate` → Releasedate/`AddedAt`/`Id` (Expansion in `ExpandPlaylistAsync`). |
| `PlaylistEntry.SortOrder` | `long?` | Manuelle Position (nur bei `Manual` maßgeblich). |
| `PlaylistEntry.AddedAt` | `DateTime` | Tiebreak beider Sortiermodi. |
| `PlaylistEntry.MediaType` / `.MediaId` | `string` / `long` | Polymorphe Inhaltsreferenz; wird zu `ProgramCursorContentType.PlaylistEntry` + `ContentId = entry.Id`. |

## `Movie` / `TVShowEpisode` / `MediaItem` (relevante Felder)

Dateien: `VideoWebPlayer/Data/Movie.cs`, `MediaBaseEntry.cs`, `TVShowEpisode.cs`, `MediaItem.cs`

| Eigenschaft | Typ | Beschreibung / Zweck |
|-------------|-----|----------------------|
| `MediaBaseEntry.Name` | `string` | Titel — Tiebreak der Genre-Film-Sortierung; Ursache des beobachteten „A"-Bias. |
| `MediaBaseEntry.ReleaseDate` / `.PremieredAt` | `DateTime?` | Primärschlüssel der Sortierung in `ExpandMovieCollectionAsync`/`ExpandGenreAsync` (`ReleaseDate ?? PremieredAt`). |
| `MediaBaseEntry.MediaSourceId` | `long` | Quelle für Freigabe- und Filterprüfung. |
| `Movie.MovieCollectionId` | `long?` | Collection-Zugehörigkeit (`ExpandMovieCollectionAsync`, Unlock-Prüfung). |
| `Movie.AgeRatingMinAge` | `int?` | Jugendschutz-Mindestalter (Skip-Logik ungeordneter Ringe). |
| `TVShowEpisode.Number` / `.TVShowSeasonId` | `int` / `long` | Sendedreihenfolge innerhalb einer Serie. |
| `MediaItem.Duration` / `.DurationProbedAt` / `.DurationProbeAttempts` | `TimeSpan?` / `DateTime?` / `int` | Planbarkeit (nur geprobte Dauer ist einplanbar). |
| `MovieMediaItem.IsTrailer` | `bool` | Trailer-Link (Trailer-Expansion). |
