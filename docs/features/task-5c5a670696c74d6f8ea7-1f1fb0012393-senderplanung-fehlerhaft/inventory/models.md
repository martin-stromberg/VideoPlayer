# Datenmodell — Bestandsaufnahme Senderplanung

Betroffene Entitäten der Sendeplanung. Alle Dateien liegen unter `VideoWebPlayer/Data/`.
Keine Schemaänderung ist aus der Anforderung ableitbar — die Anforderung selbst stellt das
fest („Keine Schemaänderung aus der Anforderung ableitbar", `requirement.md` Z. 113).

## `ProgramRule`

Datei: `VideoWebPlayer/Data/ProgramRule.cs`

Die Programmregel („Programmregel") eines `TVChannel`. Das Szenario „Primetime" nutzt:
`RuleType = GenreRotation`, `Recurrence = Daily`, `TimeOfDay = 20:15`,
`SlotDuration = 225 min`, `MaterialType = Genre`, `PlaceholderGrace = 15 min`,
`ValidityMode = Unrestricted`, `Priority = 0`, `IsActive = true`.

| Eigenschaft | Typ | Beschreibung / Zweck |
|-------------|-----|----------------------|
| `Id` | `long` | Primärschlüssel |
| `TVChannelId` | `long` | FK auf `TVChannel` |
| `Name` | `string` (required, max. 255) | Regelname |
| `RuleType` | `ProgramRuleType` | Regeltyp (u. a. `GenreRotation`, `Filler`) |
| `Recurrence` | `ProgramRuleRecurrence` | Wiederholungsmuster |
| `DayOfWeek` | `DayOfWeek?` | Wochentag bei `Weekly` |
| `SingleDate` | `DateOnly?` | Einzeldatum bei `Once` |
| `TimeOfDay` | `TimeOnly` | Sendezeit (lokale Zeit, `TimeZoneInfo.Local` in `BuildOccurrences`) |
| `EpisodeCount` | `int?` | Episoden je Ausstrahlung (Alternative zu `SlotDuration`) |
| `SlotDuration` | `TimeSpan?` | Nominale Periodenlänge (Szenario: 225 min → 20:15–00:00) |
| `MaterialType` | `ProgramRuleMaterialType` | Art der Materialreferenz |
| `MaterialId` | `long` | Weiche Materialreferenz; bei `Genre` erstes Genre bzw. 0 = „alle Genres" |
| `Genres` | `ICollection<ProgramRuleGenre>` | Genre-Auswahl (Join-Rows) |
| `MediaSourceFilters` | `ICollection<ProgramRuleMediaSource>` | Quell-Filter; leer = alle Quellen |
| `ClipGroups` | `ICollection<ProgramRuleClipGroup>` | Zugeordnete Clip-Gruppen |
| `PlaceholderGrace` | `TimeSpan` | Karenzzeit; Default `DefaultPlaceholderGrace` = 15 min (Z. 22). Wirkt als `RuleState.ClipGrace` nur bei `SlotDuration`-Regeln mit Clip-Gruppen |
| `Priority` | `int` | Konfliktpriorität (höher gewinnt) |
| `IsActive` | `bool` | Aktiv-Flag (Default `true`) |
| `ValidityMode` | `ProgramRuleValidityMode` | Gültigkeitsmodus (Default `Unrestricted`) |
| `ValidFrom` / `ValidTo` | `DateOnly?` | Fenstergrenzen (`OneTime`/`AnnualWindow`) |
| `ValidFromDay` / `ValidToDay` | `int?` | Tag-im-Monat-Grenzen (`MonthlyWindow`) |
| `ValidWeekdays` | `int?` | Wochentag-Bitmask (`WeeklyWindow`) |
| `ValidHolidayAnchor` | `ProgramRuleHolidayAnchor?` | Feiertagsanker (`HolidayWindow`) |
| `ValidHolidayBeforeDays` / `ValidHolidayAfterDays` | `int?` | Fenster um den Anker |
| `CreatedAt` / `UpdatedAt` | `DateTime` | Zeitstempel |

## `ProgramSlot`

Datei: `VideoWebPlayer/Data/ProgramSlot.cs`

Ein geplanter Sendeplatz. Alle Inhalts- und Regelreferenzen sind bewusst weich
(keine FKs), damit Planhistorie das Löschen von Medien/Regeln überlebt.
Datumskonverter `UtcOnRead` in `ProgramSlotConfiguration`; Index auf
`(TVChannelId, Start)`.

| Eigenschaft | Typ | Beschreibung / Zweck |
|-------------|-----|----------------------|
| `Id` | `long` | Primärschlüssel |
| `TVChannelId` | `long` | FK auf `TVChannel` (Kaskade) |
| `Start` / `End` | `DateTime` | UTC-Start/-Ende; `End = Start + MediaItem.Duration` |
| `MediaItemId` | `long` | Weiche Referenz auf `MediaItem` |
| `MovieId` | `long?` | Weiche Film-Referenz (auch Trailer-Clip-Slots tragen die MovieId des beworbenen Films) |
| `TVShowEpisodeId` | `long?` | Weiche Episoden-Referenz |
| `ProgramRuleId` | `long?` | Weiche Referenz auf die erzeugende Regel |
| `RuleType` | `ProgramRuleType` | Denormalisierter Snapshot des Regeltyps (Filler-Erkennung ohne Regel) |
| `Status` | `ProgramSlotStatus` | `Scheduled` (Default) oder `Displaced` — Displaced bleibt als Historie, belegt keine Sendezeit |
| `IsClipFill` | `bool` | Clip-Füll-Slot (Snapshot, auch nach Löschen von Regel/Gruppe erkennbar) |
| `CreatedAt` | `DateTime` | Zeitstempel |

## `ProgramRuleCursor`

Datei: `VideoWebPlayer/Data/ProgramRuleCursor.cs`

Persistierter Fortsetzungsstand („Fortsetzungsstand") je Strang einer Regel.
`ProgramRuleCursorConfiguration`: FK-Kaskade; Unique-Index
`(ProgramRuleId, StrandKey)` plus gefilterter Unique-Index
`IX_ProgramRuleCursors_ProgramRuleId_NullStrandKey` für `StrandKey IS NULL`
(SQLite-NULL-Semantik).

| Eigenschaft | Typ | Beschreibung / Zweck |
|-------------|-----|----------------------|
| `Id` | `long` | Primärschlüssel |
| `ProgramRuleId` | `long` | FK auf `ProgramRule` (Kaskade) |
| `StrandKey` | `string?` | Strang-Schlüssel: `show:{TVShowId}`, `other`, `clipgroup:{id}` oder `null` (einzelner Ring nicht-interleavter Regeln) |
| `LastContentType` | `ProgramCursorContentType` | Art der `LastContentId` |
| `LastContentId` | `long` | Zuletzt geplantes Inhaltselement des Strangs |
| `UpdatedAt` | `DateTime` | Zeitstempel |

## `ProgramClipGroup`

Datei: `VideoWebPlayer/Data/ProgramClipGroup.cs`

Benannte Platzhalter-Clip-Gruppe. Systemgruppe „Trailer": `TrailerGroupId = 1`,
`TrailerGroupName = "Trailer"` (per Migration geseedet; Test-Fixture seedet die Zeile
selbst, weil `EnsureCreated` keine `InsertData`-Migration ausführt).

| Eigenschaft | Typ | Beschreibung / Zweck |
|-------------|-----|----------------------|
| `Id` | `long` | Primärschlüssel |
| `Name` | `string` (required, max. 255) | Gruppenname (unique, case-insensitive) |
| `IsSystem` | `bool` | Reservierte Systemgruppe — nicht änder-/löschbar |
| `CreatedAt` / `UpdatedAt` | `DateTime` | Zeitstempel |

## Join-Entitäten

| Klasse | Datei | Felder | Zweck |
|--------|-------|--------|-------|
| `ProgramRuleClipGroup` | `Data/ProgramRuleClipGroup.cs` | `ProgramRuleId`, `ProgramClipGroupId` (+ Navigationen) | Clip-Gruppe ↔ Regel; beide FKs kaskadieren |
| `ProgramRuleGenre` | `Data/ProgramRuleGenre.cs` | `ProgramRuleId`, `GenreId` (+ Navigationen) | Genre-Auswahl einer `Genre`-Regel; beide FKs kaskadieren |
| `ProgramRuleMediaSource` | `Data/ProgramRuleMediaSource.cs` | `ProgramRuleId`, `MediaSourceId` (+ Navigationen) | Quell-Filter; leer = alle Quellen; beide FKs kaskadieren |
| `ProgramClipGroupItem` | `Data/ProgramClipGroupItem.cs` | `ProgramClipGroupId`, `MediaItemId` (+ Navigationen) | Clip-Mitgliedschaft; beide FKs kaskadieren |

## Backup-Registrierung (Kontext für DoD)

`VideoWebPlayer/Services/Backups/VideoWebPlayerBackupData.cs`: alle Programm-Tabellen
stehen bereits in `OptionalRestoreTables` (Z. 40–48), die nachgerüsteten Spalten
(`ProgramSlots.Status`, `IsClipFill`, `ProgramRuleCursors.StrandKey`,
`ProgramRules.PlaceholderGrace` und die Validity-Felder) in `OptionalRestoreColumns`
(Z. 93–125) samt Standardwerten (Z. 152–179). Die Anforderung erfordert keine neuen
Tabellen oder Spalten.
