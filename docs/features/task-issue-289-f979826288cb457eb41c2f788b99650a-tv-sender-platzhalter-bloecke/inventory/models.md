# Bestandsaufnahme: Datenmodell

## `ProgramSlot`

Datei: `VideoWebPlayer/Data/ProgramSlot.cs`

Ein Sendeplatz („broadcast slot") des `TVChannel`: ein konkreter Film, eine Episode oder ein
Clip zwischen `Start` und `End` (UTC) im rollierenden Sendeplan. Alle Referenzen außer
`TVChannelId` sind absichtlich **weiche Referenzen ohne Datenbank-Fremdschlüssel** — der Plan
überlebt das Löschen von Medien und Regeln (Historie bleibt nachvollziehbar).

| Eigenschaft | Typ | Beschreibung / Zweck |
|-------------|-----|----------------------|
| `Id` | `long` | Primärschlüssel (`[Key]`) |
| `TVChannelId` | `long` | FK auf `TVChannel` (`[ForeignKey]`, Cascade) |
| `TVChannel` | `TVChannel` | Navigation zum Sender |
| `Start` | `DateTime` | UTC-Start der Ausstrahlung |
| `End` | `DateTime` | UTC-Ende (`Start` + Medienlaufzeit) |
| `MediaItemId` | `long` | Referenziertes Medien-Item (weiche Referenz, kein FK) |
| `MovieId` | `long?` | Film-Referenz bei Film-Slots, sonst `null` (weiche Referenz; Trailer-Clips tragen sie ebenfalls) |
| `TVShowEpisodeId` | `long?` | Episoden-Referenz bei Episoden-Slots, sonst `null` (weiche Referenz) |
| `ProgramRuleId` | `long?` | Erzeugende `ProgramRule`, `null` nach Regel-Löschung (weiche Referenz) |
| `RuleType` | `ProgramRuleType` | Denormalisierter Snapshot des `ProgramRule.RuleType` — identifiziert Füller-Slots auch nach Regel-Löschung |
| `Status` | `ProgramSlotStatus` | `Scheduled` (belegt Sendezeit) oder `Displaced` (verdrängt, nur Traceability); Default `Scheduled` |
| `CreatedAt` | `DateTime` | Anlagezeitpunkt, Default `DateTime.UtcNow` |

**Wichtig für die Anforderung:** Clip-Füll-Slots sind **nicht** explizit markiert. Es gibt
kein `IsClipFill`/`ProgramSlotKind`-Feld. `FillAiringSegments` erzeugt Clip-Slots mit
`RuleType = rule.RuleType` der erzeugenden Regel (also z. B. `MoviePrimeTime`, **nicht**
`ProgramRuleType.Filler`) und `ProgramRuleId = rule.Id`. Unterscheidungsmerkmale heute:

- `MediaItemId` ∈ Clip-Gruppen-Expansion der Regel (die `clipIds`-Klassifikation in
  `ProgramScheduleService`, s. logic.md) — nur zur Laufzeit rekonstruierbar.
- Bare-Item-Slots ohne `MovieId`/`TVShowEpisodeId` fallen in `LoadDetailsAsync`
  (ProgramGuideService) bzw. `ReloadScheduleAsync` (TVSenderAdmin) auf den `MediaItem.Name`-
  Fallback — das trifft aber auch „Clip-Quelle"-Füller anderer Machart.
- Trailer-Clips (Systemgruppe `TrailerGroupId = 1`) tragen zusätzlich `MovieId`, zeigen also
  in der Programmauskunft die Filmdaten.

EF-Konfiguration: `VideoWebPlayer/Data/Configurations/ProgramSlotConfiguration.cs` —
`Start`/`End`/`CreatedAt` mit `UtcOnRead`-Konverter (SQLite speichert DateTime als TEXT ohne
Kind), `RuleType`/`Status` required, Index `{TVChannelId, Start}`, `TVChannel` cascade.
Modell-Snapshot: `VideoWebPlayer/Migrations/ApplicationDbContextModelSnapshot.cs` (~Z. 1531),
Tabellenname `ProgramSlots`, Spalten: `Id, CreatedAt, End, MediaItemId, MovieId,
ProgramRuleId, RuleType, Start, Status, TVChannelId, TVShowEpisodeId` — keine Clip-Markierung.

## `ProgramClipGroup`

Datei: `VideoWebPlayer/Data/ProgramClipGroup.cs`

Benannte Platzhalter-Clip-Gruppe; über `ProgramRuleClipGroup` an Regeln hängbar. Clips füllen
die Restzeit eines Sendeplatzes unterhalb von `ProgramRule.PlaceholderGrace`.

| Eigenschaft | Typ | Beschreibung / Zweck |
|-------------|-----|----------------------|
| `TrailerGroupId` | `const long = 1` | Id der reservierten Systemgruppe „Trailer" (per Migration geseedet) |
| `TrailerGroupName` | `const string = "Trailer"` | Name der Systemgruppe; auch für Benutzergruppen reserviert |
| `NameMaxLength` | `const int = 255` | Maximale Namenslänge |
| `Id` | `long` | Primärschlüssel |
| `Name` | `string` (required) | Gruppenname, eindeutig case-insensitiv (`NOCASE`-Kollation + Unique-Index in `ProgramClipGroupConfiguration`) |
| `IsSystem` | `bool` | Systemgruppe (automatisch befüllt, nicht umbenenn-/lösch-/befüllbar), Default `false` |
| `CreatedAt`/`UpdatedAt` | `DateTime` | Zeitstempel |

## `ProgramClipGroupItem`

Datei: `VideoWebPlayer/Data/ProgramClipGroupItem.cs`

Join-Entity Clip↔Gruppe; ein Clip darf mehreren Gruppen angehören. Konfiguration in
`ProgramClipGroupItemConfiguration`: zusammengesetzter Schlüssel `{ProgramClipGroupId,
MediaItemId}`, beide FKs kaskadieren.

| Eigenschaft | Typ | Beschreibung / Zweck |
|-------------|-----|----------------------|
| `ProgramClipGroupId` | `long` | FK auf `ProgramClipGroup` (Cascade) |
| `ProgramClipGroup` | `ProgramClipGroup` | Navigation |
| `MediaItemId` | `long` | FK auf `MediaItem` (Cascade) |
| `MediaItem` | `MediaItem` | Navigation |

## `ProgramRuleClipGroup`

Datei: `VideoWebPlayer/Data/ProgramRuleClipGroup.cs`

Join-Entity Gruppe↔Regel (zusätzliche Zuordnung unabhängig vom Materialtyp); beide FKs
kaskadieren, zusammengesetzter Schlüssel `{ProgramRuleId, ProgramClipGroupId}` (Konfiguration
`ProgramRuleClipGroupConfiguration`).

## `ProgramRule` (Auszug — nur für die Anforderung relevante Eigenschaften)

Datei: `VideoWebPlayer/Data/ProgramRule.cs`

| Eigenschaft | Typ | Beschreibung / Zweck |
|-------------|-----|----------------------|
| `DefaultPlaceholderGrace` | `static readonly TimeSpan` | 15 Minuten — Standard-Karenzzeit |
| `Id`, `TVChannelId`, `TVChannel`, `Name`, `RuleType`, `Recurrence`, `DayOfWeek`, `SingleDate`, `TimeOfDay`, `Priority`, `IsActive`, `CreatedAt`, `UpdatedAt` | div. | Regel-Stammdaten |
| `EpisodeCount` | `int?` | Episoden je Ausstrahlung (Alternative zu `SlotDuration`) |
| `SlotDuration` | `TimeSpan?` | Gewünschte Sendeplatz-Länge — Clip-Füllung wirkt nur bei gesetzter Länge |
| `MaterialType` | `ProgramRuleMaterialType` | Materialart (Serie/Film/Sammlung/Genre/Playlist/Trailer/Clip-Quelle) |
| `MaterialId` | `long` | Polymorphe Materialreferenz (weich, kein FK) |
| `Genres` | `ICollection<ProgramRuleGenre>` | Genre-Auswahl bei Genre-Material |
| `MediaSourceFilters` | `ICollection<ProgramRuleMediaSource>` | Quellenfilter bei Genre-Material |
| `ClipGroups` | `ICollection<ProgramRuleClipGroup>` | Zugeordnete Clip-Gruppen (nur wirksam bei Nicht-Füller-Regeln mit `SlotDuration`) |
| `PlaceholderGrace` | `TimeSpan` | Karenzzeit: Rest < Grace → kein Titel mehr, Clips füllen; Default `DefaultPlaceholderGrace` |

## `TVChannel`, `ProgramRuleCursor`

- `VideoWebPlayer/Data/TVChannel.cs` — der eine ausgelieferte Sender (FK-Ziel der Slots).
- `VideoWebPlayer/Data/ProgramRuleCursor.cs` — Fortsetzungsstände der Regeln/Stränge
  (`StrandKey`; Clip-Gruppen nutzen den Strandschlüssel `clipgroup:{GroupId}`). Bleibt von der
  Anforderung unberührt (nur Kontext).

## `ApplicationDbContext` (relevante DbSets)

Datei: `VideoWebPlayer/Data/ApplicationDbContext.cs` (Z. 204–244):
`ProgramRules`, `ProgramSlots`, `ProgramRuleCursors`, `ProgramRuleGenres`,
`ProgramRuleMediaSources`, `ProgramClipGroups`, `ProgramClipGroupItems`,
`ProgramRuleClipGroups`.

## `VideoWebPlayerBackupData` — Restore-Whitelist

Datei: `VideoWebPlayer/Services/Backups/VideoWebPlayerBackupData.cs`

- `OptionalRestoreTables` (Z. 23–49): enthält bereits `TVChannels`, `ProgramRules`,
  `ProgramSlots`, `ProgramRuleCursors`, `ProgramRuleGenres`, `ProgramRuleMediaSources`,
  `TvSenderUserAccess`, `ProgramClipGroups`, `ProgramClipGroupItems`, `ProgramRuleClipGroups`.
- `OptionalRestoreColumns` (Z. 51–111): u. a. `ProgramSlots.Status` und
  `ProgramRules.PlaceholderGrace` — ältere Backups ohne diese Spalten werden toleriert.
- `OptionalRestoreIntDefaults` (Z. 144–157): `ProgramSlots.Status` → `Scheduled`;
  `OptionalRestoreBoolDefaults`, `OptionalRestoreLongDefaults`,
  `OptionalRestoreTimeSpanDefaults` analog (u. a. `ProgramRules.PlaceholderGrace`).
- `IsOptionalColumn` (Z. ~1121) prüft `"{table}.{column}"` gegen `OptionalRestoreColumns`.

**Konsequenz:** Eine neue `ProgramSlot`-Spalte (Variante A der Anforderung, z. B.
`IsClipFill`) muss in `OptionalRestoreColumns` plus passender Default-Tabelle eingetragen
werden, samt Alt-Backup-Regressionstest (Muster:
`VideoWebPlayerBackupDataTests_ProgramRuleLinks` /
`VideoWebPlayerBackupDataTests_ProgramScheduleContentScope`).
