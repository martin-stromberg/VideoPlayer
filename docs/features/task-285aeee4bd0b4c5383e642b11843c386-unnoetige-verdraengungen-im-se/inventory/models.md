# Datenmodell – Bestandsaufnahme

Betroffene Datenmodellklassen der Anforderung „Unnötige Verdrängungen im Sendeplan vermeiden".
Kein neues Datenmodell und keine Migration absehbar (laut `requirement.md`).

## `ProgramSlot`

Datei: `VideoWebPlayer/Data/ProgramSlot.cs`

Zentraler Sendeplatz. Alle Referenzen (`MediaItemId`, `MovieId`, `TVShowEpisodeId`,
`ProgramRuleId`) sind bewusst weiche Referenzen ohne FK, damit die Planhistorie Löschungen
überlebt. Verdrängte Zeilen bleiben als Historie erhalten und werden nie gelöscht (außer über
`DeleteDisplacedSlotsAsync` / `ResetScheduleAsync`).

| Eigenschaft | Typ | Beschreibung / Zweck |
|-------------|-----|----------------------|
| `Id` | `long` | Primärschlüssel |
| `TVChannelId` | `long` | FK zum Kanal |
| `TVChannel` | `TVChannel` | Navigation |
| `Start` | `DateTime` | UTC-Start des Sendeplatzes |
| `End` | `DateTime` | UTC-Ende (`Start` + Mediendauer) |
| `MediaItemId` | `long` | Abgespielte Mediendatei (weiche Referenz) |
| `MovieId` | `long?` | Film-Referenz bei Filminhalten (weich) |
| `TVShowEpisodeId` | `long?` | Episoden-Referenz bei Serieninhalten (weich) |
| `ProgramRuleId` | `long?` | Erzeugende Regel, `null` nach Regel-Löschung (weich) |
| `RuleType` | `ProgramRuleType` | Denormalisierter Snapshot des Regeltyps — identifiziert Filler-Slots auch nach Regel-Löschung |
| `Status` | `ProgramSlotStatus` | `Scheduled` (belegt Sendezeit) / `Displaced` (verdrängt, nur Historie); Default `Scheduled` |
| `IsClipFill` | `bool` | Denormalisierter Snapshot „als Clip-Füllung erzeugt" (einzige Erzeugungsstelle: `FillAiringSegments`) |
| `CreatedAt` | `DateTime` | Erzeugungszeitpunkt |

Backup-Registrierung: `ProgramSlots` steht in `VideoWebPlayerBackupData.OptionalRestoreTables`
(`VideoWebPlayer/Services/Backups/VideoWebPlayerBackupData.cs:41`); `Status` und `IsClipFill`
in `OptionalRestoreColumns` (Zeilen 93–94) mit Defaults `Scheduled`/`false` (Zeilen 152, 168).

## `ProgramRuleCursor`

Datei: `VideoWebPlayer/Data/ProgramRuleCursor.cs`

Persistierter Fortsetzungsstand („Cursor") je Regel und Strang — Grundlage der
Reproduzierbarkeits-Prüfung (Cursor-/Ringposition) und des `RewindCursor`-Mechanismus.

| Eigenschaft | Typ | Beschreibung / Zweck |
|-------------|-----|----------------------|
| `Id` | `long` | Primärschlüssel |
| `ProgramRuleId` | `long` | FK zur Regel (echte Kaskade — Regel-Löschung verwirft Fortsetzungsstand) |
| `ProgramRule` | `ProgramRule` | Navigation |
| `StrandKey` | `string?` | Strang-Schlüssel (`show:{id}`, `other`, `clipgroup:{id}`); `null` = einzelner Gesamt-Cursor; gefilterter Unique-Index |
| `LastContentType` | `ProgramCursorContentType` | Art des Inhalts, auf den `LastContentId` zeigt |
| `LastContentId` | `long` | Zuletzt eingeplantes Element der expandierten Materialliste |
| `UpdatedAt` | `DateTime` | Letzter Schreibzeitpunkt (Siegerkriterium bei Duplikaten: höchstes `UpdatedAt`, dann höchste `Id` — siehe `SelectStrandCursor`) |

Backup-Registrierung: `ProgramRuleCursors` in `OptionalRestoreTables` (Zeile 42);
`StrandKey` in `OptionalRestoreColumns` (Zeile 99); Restore-Deduplikation von
NULL-StrandKey-Duplikaten (Zeile 518 ff.).

## `ProgramRule`

Datei: `VideoWebPlayer/Data/ProgramRule.cs`

Programmregel — Träger der Eingabeparameter, gegen die `ReconcileAsync` die Bestandsplätze
prüft. Nur die für die Gültigkeitsprüfung relevanten Eigenschaften:

| Eigenschaft | Typ | Beschreibung / Zweck |
|-------------|-----|----------------------|
| `Id` | `long` | Primärschlüssel; weiche Referenz der Slots über `ProgramSlot.ProgramRuleId` |
| `RuleType` | `ProgramRuleType` | `Filler`-Slots werden beim Reconcile einzeln geprüft, alle anderen zu Ausstrahlungen gruppiert |
| `Recurrence` | `ProgramRuleRecurrence` | Wiederholungsmuster → Occurrence-Anker (`BuildOccurrences`) |
| `DayOfWeek` | `DayOfWeek?` | Wochentag bei `Weekly` |
| `SingleDate` | `DateOnly?` | Datum bei `Once` |
| `TimeOfDay` | `TimeOnly` | Sendezeit (lokal) → Occurrence-Start |
| `EpisodeCount` | `int?` | Titel-Anzahl je Ausstrahlung (`wanted` der Auswahl-/Reproduktionslogik) |
| `SlotDuration` | `TimeSpan?` | Alternativ: Ziellänge der Ausstrahlung; Voraussetzung für Clip-Füllung |
| `MaterialType` | `ProgramRuleMaterialType` | Materialart → Expansionspfad (`ExpandMaterialAsync`) |
| `MaterialId` | `long` | Materialreferenz (weich, ohne FK) |
| `Genres` | `ICollection<ProgramRuleGenre>` | Genre-Auswahl bei `Genre`-Material |
| `MediaSourceFilters` | `ICollection<ProgramRuleMediaSource>` | Quellfilter (leer = alle Quellen) |
| `ClipGroups` | `ICollection<ProgramRuleClipGroup>` | Zugeordnete Clip-Gruppen |
| `PlaceholderGrace` | `TimeSpan` | Karenzzeit (`clipGrace` der Auswahl; Default 15 min) |
| `Priority` | `int` | Konflikt-/Verdrängungsreihenfolge (`Evicts`, `BoundaryAfter`) |
| `IsActive` | `bool` | Inaktiv → Ausstrahlungen invalid (`Validate`) |
| `ValidityMode` + `ValidFrom`/`ValidTo`/`ValidFromDay`/`ValidToDay`/`ValidWeekdays`/`ValidHolidayAnchor`/`ValidHolidayBeforeDays`/`ValidHolidayAfterDays` | div. | Gültigkeitsfenster — Tage ohne Gültigkeit erzeugen keine Occurrence; Ausstrahlungen verlieren ihren Anker |
| `CreatedAt`/`UpdatedAt` | `DateTime` | Zeitstempel |
