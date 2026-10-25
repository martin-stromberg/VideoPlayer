# Datenmodell — Bestandsaufnahme

Alle Aussagen gegen den aktuellen Code verifiziert (Stand Commit `3bbbd49`). **Es existiert noch keine Clip-Gruppen-Persistenz:** keine Entities `ProgramClipGroup`, `ProgramClipGroupItem`, `ProgramRuleClipGroup`, keine Karenzzeit-Spalte an `ProgramRule` (Volltextsuche nach `ClipGroup`, `Placeholder`, `Grace`, `Karenz` über `VideoWebPlayer/`, `VideoWebPlayer.Client/`, `VideoWebPlayer.Tests/` ohne Treffer außer HTML-`placeholder`-Attributen).

## `ProgramRule`
Datei: `VideoWebPlayer/Data/ProgramRule.cs`

| Eigenschaft | Typ | Beschreibung / Zweck |
|-------------|-----|----------------------|
| `Id` | `long` | Primärschlüssel |
| `TVChannelId` / `TVChannel` | `long` / `TVChannel` | Besitzender Sender (FK mit Cascade, s. `ProgramRuleConfiguration`) |
| `Name` | `string` (required, max 255) | Regelname; Unique-Index `(TVChannelId, Name)` mit `NOCASE`-Collation |
| `RuleType` | `ProgramRuleType` | Regelart (u. a. `Filler`) |
| `Recurrence` | `ProgramRuleRecurrence` | Wiederholungsmuster |
| `DayOfWeek` | `DayOfWeek?` | Pflicht bei `Recurrence.Weekly` |
| `SingleDate` | `DateOnly?` | Pflicht bei `Recurrence.Once` |
| `TimeOfDay` | `TimeOnly` | Gewünschte Sendezeit |
| `EpisodeCount` | `int?` | Folgenzahl je Ausstrahlung; mind. `EpisodeCount` oder `SlotDuration` muss gesetzt sein |
| `SlotDuration` | `TimeSpan?` | Ziel-Länge des Sendeplatzes (zeitbasierte Periode) |
| `MaterialType` | `ProgramRuleMaterialType` | Art des Programmmaterials (u. a. `Trailer`, `MediaSourceClips`) |
| `MaterialId` | `long` | Polymorphe Materialreferenz ohne harte FK (Muster `PlaylistEntry.MediaType`/`MediaId`); bei `Genre` die erste Genre-Id |
| `Genres` | `ICollection<ProgramRuleGenre>` | Join-Collection der Genre-Mehrfachauswahl |
| `MediaSourceFilters` | `ICollection<ProgramRuleMediaSource>` | Join-Collection des Quell-Filters (nur Genre-Material) |
| `Priority` | `int` (Default 0) | Konfliktauflösung (höher gewinnt) |
| `IsActive` | `bool` (Default true) | Aktiv-Schalter |
| `CreatedAt` / `UpdatedAt` | `DateTime` | Zeitstempel |

**Keine** Karenzzeit-Eigenschaft und keine Gruppen-Navigation vorhanden.

## `ProgramRuleGenre`
Datei: `VideoWebPlayer/Data/ProgramRuleGenre.cs` — Muster-Join-Entity für die geforderte Gruppe↔Regel-Zuordnung.

| Eigenschaft | Typ | Beschreibung / Zweck |
|-------------|-----|----------------------|
| `ProgramRuleId` / `ProgramRule` | `long` / `ProgramRule` | Teil des Composite-PK `(ProgramRuleId, GenreId)`; FK kaskadiert |
| `GenreId` / `Genre` | `long` / `Genre` | Teil des Composite-PK; FK kaskadiert |

## `ProgramRuleMediaSource`
Datei: `VideoWebPlayer/Data/ProgramRuleMediaSource.cs` — gleiches Join-Muster wie `ProgramRuleGenre` (Composite-PK `(ProgramRuleId, MediaSourceId)`, beide FKs kaskadieren).

## `ProgramRuleCursor`
Datei: `VideoWebPlayer/Data/ProgramRuleCursor.cs` — persistierter Fortsetzungsstand je Regel (und je Strang).

| Eigenschaft | Typ | Beschreibung / Zweck |
|-------------|-----|----------------------|
| `Id` | `long` | Primärschlüssel |
| `ProgramRuleId` / `ProgramRule` | `long` / `ProgramRule` | FK mit Cascade |
| `StrandKey` | `string?` | Strang-Schlüssel (`show:{id}` / `other`); `null` = Gesamt-Cursor |
| `LastContentType` | `ProgramCursorContentType` | Art der referenzierten Inhalte (`LastContentId`) |
| `LastContentId` | `long` | Zuletzt eingeplantes Element |
| `UpdatedAt` | `DateTime` | Zeitstempel |

Indizes (`ProgramRuleCursorConfiguration`): Unique `(ProgramRuleId, StrandKey)` plus gefilterter Unique-Index `IX_ProgramRuleCursors_ProgramRuleId_NullStrandKey` (`[StrandKey] IS NULL`).

## `ProgramSlot`
Datei: `VideoWebPlayer/Data/ProgramSlot.cs` — ein geplanter Sendeplatz.

| Eigenschaft | Typ | Beschreibung / Zweck |
|-------------|-----|----------------------|
| `Id` | `long` | Primärschlüssel |
| `TVChannelId` / `TVChannel` | `long` / `TVChannel` | FK mit Cascade |
| `Start` / `End` | `DateTime` (UTC-Konverter in `ProgramSlotConfiguration`) | Sendefenster |
| `MediaItemId` | `long` | **Weiche** Referenz auf `MediaItem` (kein DB-FK) |
| `MovieId` / `TVShowEpisodeId` | `long?` | **Weiche** Inhaltsreferenzen (kein DB-FK); beide `null` = „bare media item"-Slot (Clip/Trailer) |
| `ProgramRuleId` | `long?` | **Weiche** Referenz auf die erzeugende Regel (kein DB-FK) |
| `RuleType` | `ProgramRuleType` | Denormalisierter Snapshot der Regelart (identifiziert Füller-Slots auch nach Regel-Löschung) |
| `Status` | `ProgramSlotStatus` (Default `Scheduled`) | `Scheduled` belegt Sendezeit; `Displaced` nur noch Nachvollziehbarkeit |
| `CreatedAt` | `DateTime` | Zeitstempel |

Index `(TVChannelId, Start)`. Clip-Slots einer neuen Gruppen-Füllung wären über `MovieId == null && TVShowEpisodeId == null` + `ProgramRuleId`/`RuleType` adressierbar — eine eigene Kennzeichnung existiert nicht.

## `MediaItem`
Datei: `VideoWebPlayer/Data/MediaItem.cs` (erbt `Id`, `Name`, `Path`, `CreatedAt`, `ClassifiedAt`, `Changed` von `MediaEntry`)

| Eigenschaft | Typ | Beschreibung / Zweck |
|-------------|-----|----------------------|
| `MediaCollectionId` / `MediaCollection` | `long` / `MediaCollection` | Zugehörigkeit; darüber `MediaSourceId` erreichbar |
| `Duration` | `TimeSpan?` | Gemessene Abspiellänge; `null` = unbekannt/nie gemessen — die Anforderung „nur Clips mit bekannter Länge" bildet darauf ab |
| `DurationProbedAt` | `DateTime?` | Zeitpunkt des letzten Messversuchs |
| `DurationProbeAttempts` | `int` | Anzahl fehlgeschlagener Messversuche (Max. `MediaProbeOptions.MaxAttempts` = 10) |

„Unklassifizierter Clip" ist kein Feld, sondern wird abgeleitet: `MediaItem` ohne `MovieMediaItem`-/`TVShowEpisodeMediaItem`-Verknüpfung und `VideoFileExtensions.IsVideoFileName(Path)` — so in `ProgramScheduleService.ExpandMediaSourceClipsAsync` (`ProgramScheduleService.cs:2024`).

## `MovieMediaItem`
Datei: `VideoWebPlayer/Data/MovieMediaItem.cs`

| Eigenschaft | Typ | Beschreibung / Zweck |
|-------------|-----|----------------------|
| `MovieId` / `Movie` | `long` / `Movie` | Verknüpfung Film ↔ Datei |
| `MediaItemId` / `MediaItem` | `long` / `MediaItem` | Verknüpfte Datei |
| `IsTrailer` | `bool` | Markiert Trailer-Links — Kandidatenmenge der reservierten „Trailer"-Gruppe und der `Trailer`-Materialart |

## `TVChannel`
Datei: `VideoWebPlayer/Data/TVChannel.cs`

| Eigenschaft | Typ | Beschreibung / Zweck |
|-------------|-----|----------------------|
| `Id` | `long` | Primärschlüssel |
| `Name` | `string` (required, max 255) | Sendername |
| `CreatedAt` / `UpdatedAt` | `DateTime` | Zeitstempel |
| `ProgramRules` | `ICollection<ProgramRule>` | Navigations-Collection (Cascade-Löschung) |

## `TvSenderUserAccess`
Datei: `VideoWebPlayer/Data/TvSenderUserAccess.cs` — Allowlist für Nicht-Admin-Zugriff (`UserId` als PK, FK auf `ApplicationUser`).

## `Setup` (Auszug der Sender-Felder)
Datei: `VideoWebPlayer/Data/Setup.cs`

| Eigenschaft | Typ | Beschreibung / Zweck |
|-------------|-----|----------------------|
| `TvSenderEnabled` | `bool` | Feature-Flag |
| `HolidayFederalState` | `string?` | Bundesland für `Holiday`-Wiederholung |
| `ProgramScheduleHorizonHours` | `int` (Default 48) | Planungshorizont |
| `ProgramScheduleIntervalMinutes` | `int` (Default 15) | Worker-Intervall |

## `ApplicationDbContext`-Registrierungen
Datei: `VideoWebPlayer/Data/ApplicationDbContext.cs` — Sender-DbSets (Z. 200–229): `TVChannels`, `ProgramRules`, `ProgramSlots`, `ProgramRuleCursors`, `ProgramRuleGenres`, `ProgramRuleMediaSources`, `TvSenderUserAccess`. EF-Konfigurationen liegen unter `Data/Configurations/` (`ProgramRuleConfiguration`, `ProgramSlotConfiguration`, `ProgramRuleCursorConfiguration`, `ProgramRuleGenreConfiguration`, `ProgramRuleMediaSourceConfiguration`, `TvSenderUserAccessConfiguration`).

## Backup-Registrierung (`VideoWebPlayerBackupData`)
Datei: `VideoWebPlayer/Services/Backups/VideoWebPlayerBackupData.cs`

- `OptionalRestoreTables` (Z. 23–46): enthält bereits `TVChannels`, `ProgramRules`, `ProgramSlots`, `ProgramRuleCursors`, `ProgramRuleGenres`, `ProgramRuleMediaSources`, `TvSenderUserAccess`.
- `OptionalRestoreColumns` (Z. 48–96): enthält u. a. `MediaItems.Duration`, `MediaItems.DurationProbedAt`, `MediaItems.DurationProbeAttempts`, `MovieMediaItems.IsTrailer`, `ProgramSlots.Status`, `ProgramRuleCursors.StrandKey`, `Setups.TvSenderEnabled`, `Setups.ProgramScheduleHorizonHours`, `Setups.ProgramScheduleIntervalMinutes`, `Setups.HolidayFederalState`.
- Default-Mechanismus (`AddOptionalRestoreDefaults`, Z. 951–1005): drei getrennte Listen `OptionalRestoreBoolDefaults` (Z. 106), `OptionalRestoreLongDefaults` (Z. 123), `OptionalRestoreIntDefaults` (Z. 128). **Kein `TimeSpan`-Default-Typ vorhanden** — eine `TimeSpan`-Karenzspalte mit Nicht-Null-Default müsste den Mechanismus erweitern (oder als Sekunden/Ticks-`long` abgebildet werden). `ToDbValue` (Z. 1014) behandelt `TimeSpan` nur über den `JsonSerializer.Deserialize`-Fallback (Z. 1057).
- Sonderbehandlung Restore `ProgramRuleCursors` (Z. ~461–499): Deduplikation mehrerer `NULL`-`StrandKey`-Zeilen je Regel (jüngste `UpdatedAt`/`Id` gewinnt).

## EF-Migrationen im Sender-Bereich
`VideoWebPlayer/Migrations/`: `20261021040456_AddTVChannelsAndProgramRules`, `20261021045013_AddProgramSchedule`, `20261021053951_AddProgramSlotStatus`, `20261021131012_AddProgramRuleGenreAndSourceLinks`, `20261022190000_AddProgramRuleCursorStrandKey`, `20261023050115_AddSetupTvSenderEnabled`, `20261023055517_AddTvSenderUserAccess`, `20261024072426_EnforceProgramRuleCursorNullStrandKeyUnique` — danach weitere Migrationen außerhalb des Sender-Bereichs.
