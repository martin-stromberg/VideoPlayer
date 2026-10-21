# Datenmodell — Bestandsaufnahme

## `ProgramSlot`
Datei: `VideoWebPlayer/Data/ProgramSlot.cs` · Konfiguration: `VideoWebPlayer/Data/Configurations/ProgramSlotConfiguration.cs`

| Eigenschaft | Typ | Beschreibung / Zweck |
|-------------|-----|----------------------|
| `Id` | `long` | Primärschlüssel |
| `TVChannelId` | `long` | FK auf `TVChannel` mit `DeleteBehavior.Cascade` |
| `TVChannel` | `TVChannel` | Navigation |
| `Start` | `DateTime` | UTC-Start der Ausstrahlung; Index `IX_ProgramSlots_TVChannelId_Start` |
| `End` | `DateTime` | UTC-Ende (`Start` + `MediaItem.Duration`) |
| `MediaItemId` | `long` | **Weiche Referenz ohne FK** — gelöschte MediaItems kaskadieren nicht; verwaiste Ids bleiben abfragbar |
| `MovieId` | `long?` | Weiche Referenz auf `Movie`, sonst `null` |
| `TVShowEpisodeId` | `long?` | Weiche Referenz auf `TVShowEpisode`, sonst `null` |
| `ProgramRuleId` | `long?` | **Weiche Referenz ohne FK** — Regel-Löschung lässt die Zeile stehen |
| `RuleType` | `ProgramRuleType` | Denormalisierter Snapshot der Regelart (Füller erkennbar nach Regel-Löschung) |
| `CreatedAt` | `DateTime` | Erstellungszeitpunkt |

**Wichtig:** Es gibt **keine `Status`-Spalte** — der in der Aufgabenbeschreibung erwähnte Status existiert noch nicht und ist neu anzulegen.

## `ProgramRuleCursor`
Datei: `VideoWebPlayer/Data/ProgramRuleCursor.cs` · Konfiguration: `ProgramRuleCursorConfiguration.cs`

| Eigenschaft | Typ | Beschreibung / Zweck |
|-------------|-----|----------------------|
| `Id` | `long` | Primärschlüssel |
| `ProgramRuleId` | `long` | **Echter FK mit Kaskade** — Regel-Löschung verwirft den Fortsetzungsstand |
| `ProgramRule` | `ProgramRule` | Navigation |
| `LastContentType` | `ProgramCursorContentType` | Art des Elements (`Movie`, `TVShowEpisode`, `PlaylistEntry`) |
| `LastContentId` | `long` | Id des zuletzt eingeplanten Elements in der expandierten Materialliste |
| `UpdatedAt` | `DateTime` | Letzte Aktualisierung |

Cursor-Semantik (`ProgramScheduleService.NextIndexFor`): `Items[IndexOf(Cursor-Element) + 1]`; nicht gefundenes Cursor-Element → Index 0. Über `cursor.LastContentType/LastContentId` auf den Vorgänger zurückzusetzen = „Rückspulen" für Wiedereinplanung.

## `ProgramRule`
Datei: `VideoWebPlayer/Data/ProgramRule.cs`

| Eigenschaft | Typ | Beschreibung / Zweck |
|-------------|-----|----------------------|
| `Id` | `long` | Primärschlüssel |
| `TVChannelId` / `TVChannel` | `long` / `TVChannel` | Sender-Bezug (FK) |
| `Name` | `string` (max. 255) | Anzeigename — **nicht planrelevant** |
| `RuleType` | `ProgramRuleType` | Regelart — planrelevant |
| `Recurrence` | `ProgramRuleRecurrence` | Wiederholungsmuster — planrelevant |
| `DayOfWeek` | `DayOfWeek?` | Wochentag (nur `Weekly`) — planrelevant |
| `SingleDate` | `DateOnly?` | Datum (nur `Once`) — planrelevant |
| `TimeOfDay` | `TimeOnly` | Sendezeit — planrelevant |
| `EpisodeCount` | `int?` | Folgen je Ausstrahlung — planrelevant |
| `SlotDuration` | `TimeSpan?` | Ziellänge — planrelevant |
| `MaterialType` / `MaterialId` | `ProgramRuleMaterialType` / `long` | Programmmaterial (weiche Referenz) — planrelevant |
| `Priority` | `int` | Konfliktpriorität — planrelevant, aber auflösbar über Re-Materialisierung |
| `IsActive` | `bool` | Nur aktive Regeln gehen in die Planung ein |
| `CreatedAt` / `UpdatedAt` | `DateTime` | Zeitstempel |

## `TVChannel`
Datei: `VideoWebPlayer/Data/TVChannel.cs` — `Id`, `Name` (max. 255), `CreatedAt`, `UpdatedAt`. Genau ein Datensatz (`ProgramRuleService.GetOrCreateChannelAsync` legt „TV-Sender" an).

## `MediaItem` (relevante Ausschnitte)
Datei: `VideoWebPlayer/Data/MediaItem.cs`

| Eigenschaft | Typ | Beschreibung |
|-------------|-----|--------------|
| `Duration` | `TimeSpan?` | Per ffprobe ermittelte Laufzeit; `null` = nicht planbar |
| `DurationProbedAt` | `DateTime?` | `null` = nie probiert; gesetzt + `Duration==null` = Probe fehlgeschlagen |

## `Setup` (relevante Ausschnitte)
`ProgramScheduleHorizonHours` (int, ≤0 → 48 h), `ProgramScheduleIntervalMinutes` (int, ≤0 → 15 min), `HolidayFederalState` (string?) — bereits über `ProgramSettingsService` les-/schreibbar.

## `ApplicationDbContext`
Datei: `VideoWebPlayer/Data/ApplicationDbContext.cs` (+ `ApplicationDbContext.PlaylistBackfillMarking.cs`)

- `DbSet`s: `TVChannels`, `ProgramRules`, `ProgramSlots`, `ProgramRuleCursors`, `PlaylistBackfillMarkers`, `MediaItems` u. a.
- Ctor: `ApplicationDbContext(DbContextOptions, EventManager, IPlaylistBackfillSignal? backfillSignal = null)` — optionales Signal wird per DI injiziert; Tests übergeben ihn nicht.
- **Medien-Löschung:** `DeleteMediaSourceAsync` und `DeleteMediaCollectionsForSourceAsync` löschen `MovieMediaItems`, `TVShowEpisodeMediaItems`, `TVShowEpisodes`, `Movies`, `MediaItems` u. a. per **`ExecuteDeleteAsync` (Bulk)** — umgeht `SaveChanges`-Change-Tracker-Hooks vollständig. Nach Commit wird `MediaSourceDeletedEvent` publiziert (`_eventManager.Publish`, bisher ohne Subscriber).
- Der `PlaylistBackfillMarker`-`SaveChanges`-Hook (`CollectMarkCandidates`) sieht nur Added/Modified von `Movie`, `TVShowSeason`, `TVShowEpisode` — Deleted nicht.

## `PlaylistBackfillMarker`
Datei: `VideoWebPlayer/Data/PlaylistBackfillMarker.cs` — `Id`, `MediaType` (string), `MediaId`, `MarkedAt`, `Version` (Upsert mit `ON CONFLICT DO UPDATE`, Unique-Index auf `MediaType`+`MediaId`). Referenzmuster für persistente Markierungen.

## `VideoWebPlayerBackupData`
Datei: `VideoWebPlayer/Services/Backups/VideoWebPlayerBackupData.cs`

- `OptionalRestoreTables` (Zeilen 23–43) enthält bereits `ProgramRules`, `ProgramSlots`, `ProgramRuleCursors`.
- `OptionalRestoreColumns` (ab Zeile 45) enthält bisher **keinen** `ProgramSlots`-Eintrag — neue Spalten (`Status`) sind dort zu registrieren, sonst schlägt die Alt-Backup-Wiederherstellung fehl.
