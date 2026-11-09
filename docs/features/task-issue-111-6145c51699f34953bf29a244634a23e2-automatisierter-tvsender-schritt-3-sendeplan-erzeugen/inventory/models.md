# Datenmodell – Bestandsaufnahme

## `MediaItem`
Datei: `VideoWebPlayer/Data/MediaItem.cs`

| Eigenschaft | Typ | Beschreibung / Zweck |
|-------------|-----|----------------------|
| `Id` | `long` | PK (von `MediaEntry` geerbt) |
| `MediaCollectionId` | `long` | FK auf `MediaCollection` |
| `Duration` | `TimeSpan?` | Per ffprobe ermittelte Laufzeit; `null` = nicht ermittelbar/nie probiert — solche Inhalte sind von der Planung auszuschließen |
| `DurationProbedAt` | `DateTime?` | Zeitpunkt des letzten Probes; `DurationProbedAt != null && Duration == null` markiert einen fehlgeschlagenen Probe (nicht erneut versuchen) |

Über `MediaItem.MediaCollection.MediaSourceId` ist die Quelle ermittelbar. `MediaCollection` hat keinen direkten `MediaSourceId`-Pfad am Item herumgereicht — Zugriff über `MediaItem.MediaCollection.MediaSourceId`.

## `TVChannel`
Datei: `VideoWebPlayer/Data/TVChannel.cs`

| Eigenschaft | Typ | Beschreibung / Zweck |
|-------------|-----|----------------------|
| `Id` | `long` | PK |
| `Name` | `string` | Sendername (`NameMaxLength = 255`, NOCASE) |
| `CreatedAt`, `UpdatedAt` | `DateTime` | Zeitstempel |
| `ProgramRules` | `ICollection<ProgramRule>` | Regeln des Senders (FK-Kaskade) |

Konfiguration `TVChannelConfiguration` (`VideoWebPlayer/Data/Configurations/`). Genau ein Sender wird von `ProgramRuleService.GetOrCreateChannelAsync` angelegt („TV-Sender").

## `ProgramRule`
Datei: `VideoWebPlayer/Data/ProgramRule.cs`

| Eigenschaft | Typ | Beschreibung / Zweck |
|-------------|-----|----------------------|
| `Id` | `long` | PK |
| `TVChannelId` | `long` | FK auf `TVChannel` (Kaskade) |
| `Name` | `string` | Regelname, Unique pro Sender (`(TVChannelId, Name)`) |
| `RuleType` | `ProgramRuleType` | Regelart |
| `Recurrence` | `ProgramRuleRecurrence` | Wiederholungsmuster |
| `DayOfWeek` | `DayOfWeek?` | Pflicht bei `Weekly` |
| `SingleDate` | `DateOnly?` | Pflicht bei `Once` |
| `TimeOfDay` | `TimeOnly` | Gewünschte Sendezeit |
| `EpisodeCount` | `int?` | Folgen je Ausstrahlung (Pflicht bei `SeriesFixedTime`); mind. eins von `EpisodeCount`/`SlotDuration` |
| `SlotDuration` | `TimeSpan?` | Ziel-Länge des Sendefensters |
| `MaterialType` | `ProgramRuleMaterialType` | Art des Programmmaterials (Serie/Film/Filmsammlung/Genre/Playlist) |
| `MaterialId` | `long` | Id im Materialtyp; **bewusst kein DB-FK** — verwaiste Materialien sind in Schritt 3 beim Expandieren fachlich zu behandeln (prüfen/überspringen) |
| `Priority` | `int` | Konfliktlösung: höher gewinnt |
| `IsActive` | `bool` | Nur aktive Regeln gehen in die Planung ein |
| `CreatedAt`, `UpdatedAt` | `DateTime` | Zeitstempel |

Konfiguration `ProgramRuleConfiguration`.

## `Setup`
Datei: `VideoWebPlayer/Data/Setup.cs`

Persistierte Anwendungseinstellungen (Einzel-Zeile). Relevante vorhandene Felder:

| Eigenschaft | Typ | Beschreibung / Zweck |
|-------------|-----|----------------------|
| `PlaylistBackfillLastSweepAt` | `DateTime?` | Muster für persistierte Lauf-Zeitstempel eines Workers |
| `HolidayFederalState` | `string?` | Bundesland (ISO-3166-2 `DE-*`) für den Feiertagskalender; `null` = nur bundesweit |

## `Movie` / `TVShow` / `TVShowSeason` / `TVShowEpisode` / `MovieCollection`
Dateien: `VideoWebPlayer/Data/{Movie,TVShow,TVShowSeason,TVShowEpisode,MovieCollection}.cs` (Basis `MediaBaseEntry`)

| Eigenschaft | Typ | Beschreibung / Zweck |
|-------------|-----|----------------------|
| `MediaSourceId` | `long` | Quelle des Inhalts (Freigabe-Ebene) |
| `Movie.MovieCollectionId` | `long?` | Optionaler Sammlungsbezug |
| `Movie.MovieMediaItems` / `TVShowEpisode.TVShowEpisodeMediaItems` | `ICollection<…>` | Verknüpfung zu `MediaItem`s (die eigentliche abspielbare Datei) |
| `TVShowSeason.TVShowId`, `TVShowSeason.Episodes` | – | Staffelgehörigkeit; Staffeln haben **keine Nummer** — Reihenfolge per `Id` (Konvention aus `MediaHierarchyRegistry.GetHierarchySequenceAsync`) |
| `TVShowEpisode.TVShowSeasonId`, `TVShowEpisode.Number` | `long`, `int` | Episoden-Sendedreihenfolge: Staffel (Id-Reihenfolge), dann `Number` |
| `MovieGenre`/`TVShowGenre` | Verknüpfungen | Genre-Material expandiert über diese Tabellen |

## `Playlist` / `PlaylistEntry`
Dateien: `VideoWebPlayer/Data/{Playlist,PlaylistEntry}.cs`

| Eigenschaft | Typ | Beschreibung / Zweck |
|-------------|-----|----------------------|
| `Playlist.SortMode` | `PlaylistSortMode` | `ByReleaseDate` oder `Manual` (dann `SortOrder`) |
| `PlaylistEntry.MediaType` | `string` | Einer der `MediaTypeValues`-Werte; abspielbar sind `Movie`/`TVShowEpisode` (`PlaylistEntryMediaTypeResolver.IsPlayable`) |
| `PlaylistEntry.MediaId` | `long` | Id im jeweiligen Typ (kein DB-FK) |
| `PlaylistEntry.SortOrder` | `long?` | Manuelle Reihenfolge |

Playlist-Material expandiert zu den abspielbaren Einträgen in Playlist-Reihenfolge (`Manual`: `SortOrder`, `AddedAt`; sonst Release-Datum — vgl. `PlaylistService.SortPlaylistEntriesForModeAsync`).

## `MediaSourceUser` / `UnlockedMediaEntry` / `ApplicationUser`
Dateien: `VideoWebPlayer/Data/{MediaSourceUser,UnlockedMediaEntry,ApplicationUser}.cs`

| Eigenschaft | Typ | Beschreibung / Zweck |
|-------------|-----|----------------------|
| `MediaSourceUser` | (SourceId, UserId) | Regulärer Quellenzugriff je Benutzer |
| `UnlockedMediaEntry.MovieCollectionId`/`TVShowId` | `long?` | Einzelfreischaltung je Benutzer, nur auf Sammlungs-/Serien-Ebene |
| `ApplicationUser.IsAdmin` | `bool` | Admins umgehen die Quellenfilterung (Controller prüfen Claim) |
