# Datenmodell

Für die Anforderung relevante persistierte Klassen. Keine Schema-Änderung nötig — die Strang-Cursor existieren bereits in `ProgramRuleCursors` (Restore-Behandlung in `VideoWebPlayer/Services/Backups/VideoWebPlayerBackupData.cs:~505–535`, NULL-`StrandKey`-Deduplikation).

## `ProgramRule`

Datei: `VideoWebPlayer/Data/ProgramRule.cs`

| Eigenschaft | Typ | Beschreibung / Zweck |
|-------------|-----|----------------------|
| `Id` | `long` | Primärschlüssel; geht in `StrandOrderKey`/`ItemOrderKey` ein. |
| `TVChannelId` | `long` | FK auf `TVChannel`. |
| `Name` | `string` | Regelname (`NameMaxLength` = 255). |
| `RuleType` | `ProgramRuleType` | Slot-Typ (u. a. `GenreRotation`, `MoviePrimeTime`, `Filler`). |
| `Recurrence` | `ProgramRuleRecurrence` | Wiederholungsmuster der Occurrences. |
| `DayOfWeek`/`SingleDate` | `DayOfWeek?`/`DateOnly?` | Wochentag (Weekly) bzw. Einmaldatum (Once). |
| `TimeOfDay` | `TimeOnly` | Lokale Sendezeit der Occurrence. |
| `EpisodeCount` | `int?` | Titel je Ausstrahlung; steuert `wanted` in der Auswahl (und damit die heutige Rotationsbedingung `wanted < strandCount`). |
| `SlotDuration` | `TimeSpan?` | Nominale Periodenlänge; bei Fehlen von `EpisodeCount` `wanted = int.MaxValue` → heutige Rotation nie aktiv. Mindestens eines von beiden muss gesetzt sein. |
| `MaterialType` | `ProgramRuleMaterialType` | Materialart; nur `Genre` erzeugt mehrere Stränge. |
| `MaterialId` | `long` | Weiche Materialreferenz; bei `Genre` Fallback/Legacy-Marker (`0` = alle Genres). |
| `Genres` | `ICollection<ProgramRuleGenre>` | Genre-Auswahl der Genre-Regel. |
| `MediaSourceFilters` | `ICollection<ProgramRuleMediaSource>` | Quellenfilter der Genre-Regel (leer = alle). |
| `ClipGroups` | `ICollection<ProgramRuleClipGroup>` | Zugeordnete Clip-Gruppen; aktiviert `ClipGrace` nur bei `SlotDuration`. |
| `PlaceholderGrace` | `TimeSpan` | Restzeit-Schwelle der Titel-Auswahl (Default `DefaultPlaceholderGrace` = 15 min); begrenzt die faktisch platzierende Titelzahl. |
| `Priority` | `int` | Konfliktauflösung (höher gewinnt; Bound `Hard`). |
| `IsActive` | `bool` | Regel aktiv. |
| `ValidityMode`/`ValidFrom`/`ValidTo`/`ValidFromDay`/`ValidToDay`/`ValidWeekdays`/`ValidHolidayAnchor`/`ValidHolidayBeforeDays`/`ValidHolidayAfterDays` | div. | Gültigkeitsfenster (`ProgramRuleValidity.IsValidOn` je Tag). |
| `CreatedAt`/`UpdatedAt` | `DateTime` | Zeitstempel. |

## `ProgramRuleCursor`

Datei: `VideoWebPlayer/Data/ProgramRuleCursor.cs`

| Eigenschaft | Typ | Beschreibung / Zweck |
|-------------|-----|----------------------|
| `Id` | `long` | Primärschlüssel; Tie-Break bei `SelectStrandCursor`. |
| `ProgramRuleId` | `long` | FK auf `ProgramRule` (echte Kaskade). |
| `StrandKey` | `string?` | Strang-Schlüssel (`show:{id}`, `collection:{id}`, `other`, `clipgroup:{id}`); `null` = Gesamtcursor flacher Regeln. Gefilterter Unique-Index: max. eine NULL-Zeile je Regel; Composite-Unique je (Regel, StrangKey). |
| `LastContentType` | `ProgramCursorContentType` | Inhaltstyp des zuletzt platzierten Elements. |
| `LastContentId` | `long` | Id des zuletzt platzierten Inhalts (`NextIndexFor` = Position+1). |
| `UpdatedAt` | `DateTime` | Gewinnerkriterium bei Dubletten (höchster Wert). |

## `ProgramSlot`

Datei: `VideoWebPlayer/Data/ProgramSlot.cs`

| Eigenschaft | Typ | Beschreibung / Zweck |
|-------------|-----|----------------------|
| `Id` | `long` | Primärschlüssel. |
| `TVChannelId` | `long` | FK auf `TVChannel`. |
| `Start`/`End` | `DateTime` | UTC-Intervall (`End` = `Start` + Laufzeit der Datei). |
| `MediaItemId` | `long` | Weiche Referenz auf die Datei (kein FK — Historie überlebt Löschung). |
| `MovieId`/`TVShowEpisodeId` | `long?` | Weiche Inhaltsreferenz. |
| `ProgramRuleId` | `long?` | Erzeugende Regel (weich). |
| `RuleType` | `ProgramRuleType` | Denormalisierter Typ-Snapshot (Filler-Erkennung ohne Regel). |
| `Status` | `ProgramSlotStatus` | `Scheduled` (belegt Sendezeit) / `Displaced` (nur Evidenz). |
| `IsClipFill` | `bool` | Clip-Füll-Slot aus `FillAiringSegments` (Clip-Vorklassifikation). |
| `CreatedAt` | `DateTime` | Zeitstempel. |
