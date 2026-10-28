# Datenmodell — Bestandsaufnahme

Alle Klassen sind laut Anforderung unverändert; die Übersicht liest nur vorhandene
Eigenschaften und Navigationen. Für die kompakte Genre-Anzahl sind `Genres.Count` und
`MaterialId` bereits vorhanden — `GetRulesAsync` lädt die Navigationen eager mit
(siehe [logic.md](logic.md)).

## `ProgramRule`
Datei: `VideoWebPlayer/Data/ProgramRule.cs`

| Eigenschaft | Typ | Beschreibung / Zweck |
|-------------|-----|----------------------|
| `Id` | `long` | Primärschlüssel; wird als `data-rule-id` auf der Regelzeile ausgegeben (`TVSenderAdmin.razor` Zeile 147) |
| `TVChannelId` / `TVChannel` | `long` / `TVChannel` | Besitzender Sender (genau ein ausgelieferter Kanal) |
| `Name` | `string` (required, `NameMaxLength = 255`) | Regelname; erste Spalte der bisherigen Tabelle |
| `RuleType` | `ProgramRuleType` | Regelart; Anzeige über `RuleTypeLabel` |
| `Recurrence` | `ProgramRuleRecurrence` | Wiederholungsmuster; Anzeige über `RecurrenceLabel` |
| `DayOfWeek` | `DayOfWeek?` | Wochentag, nur bei `Recurrence == Weekly` |
| `SingleDate` | `DateOnly?` | Einmaldatum, nur bei `Recurrence == Once` |
| `TimeOfDay` | `TimeOnly` | Sendezeit; Anzeige `HH:mm` |
| `EpisodeCount` | `int?` | Folgenzahl je Ausstrahlung (Alternative zu `SlotDuration`); fließt in `DurationLabel` ein |
| `SlotDuration` | `TimeSpan?` | Sendeplatz-Länge (Alternative zu `EpisodeCount`); fließt in `DurationLabel` ein |
| `MaterialType` | `ProgramRuleMaterialType` | Materialart; bei `Genre` steuert `MaterialLabel` die Listen-Ausgabe |
| `MaterialId` | `long` | Materialreferenz; bei `Genre` das erste gewählte Genre (Fallback, wenn `Genres` leer) |
| `Genres` | `ICollection<ProgramRuleGenre>` | Alle gewählten Genres bei `MaterialType.Genre`; `Count` liefert die in der Anforderung gewünschte Anzahl |
| `MediaSourceFilters` | `ICollection<ProgramRuleMediaSource>` | Quell-Filter des Genre-Materials (leer = alle Quellen) |
| `ClipGroups` | `ICollection<ProgramRuleClipGroup>` | Zugeordnete Clip-Gruppen (Restzeit-Auffüllung), materialunabhängig |
| `PlaceholderGrace` | `TimeSpan` (Default `DefaultPlaceholderGrace` = 15 Min) | Karenzzeit; wird im Editor über `RuleEditModel.GraceMinutes` gepflegt |
| `Priority` | `int` | Konfliktpriorität; aktuell als nackte Zahl in der Spalte „Priorität" |
| `IsActive` | `bool` (Default `true`) | Aktiv-Status; aktuell als Chip „Aktiv"/„Inaktiv" in eigener Spalte |
| `CreatedAt` / `UpdatedAt` | `DateTime` | Zeitstempel; in der Übersicht nicht angezeigt |

Statische Member: `NameMaxLength` (Zeile 16), `DefaultPlaceholderGrace` (Zeile 22).

## `ProgramRuleGenre`
Datei: `VideoWebPlayer/Data/ProgramRuleGenre.cs`

Join-Entität Regel ↔ Genre (beide Fremdschlüssel kaskadieren).

| Eigenschaft | Typ | Beschreibung / Zweck |
|-------------|-----|----------------------|
| `ProgramRuleId` / `ProgramRule` | `long` / `ProgramRule` | Besitzende Regel |
| `GenreId` / `Genre` | `long` / `Genre` | Verknüpftes Genre; `Genre.Name` wird in `MaterialLabel` aufgelistet |

## `ProgramRuleMediaSource`
Datei: `VideoWebPlayer/Data/ProgramRuleMediaSource.cs`

Join-Entität Regel ↔ Medienquelle (Quell-Filter des Genre-Materials; leer = alle Quellen;
beide Fremdschlüssel kaskadieren).

| Eigenschaft | Typ | Beschreibung / Zweck |
|-------------|-----|----------------------|
| `ProgramRuleId` / `ProgramRule` | `long` / `ProgramRule` | Besitzende Regel |
| `MediaSourceId` / `MediaSource` | `long` / `MediaSource` | Eingeschränkte Quelle; `MediaSource.Name` wird in `MaterialLabel` als „Quellen: …" aufgelistet |

## `ProgramRuleClipGroup`
Datei: `VideoWebPlayer/Data/ProgramRuleClipGroup.cs`

Join-Entität Regel ↔ `ProgramClipGroup` (beide Fremdschlüssel kaskadieren).

| Eigenschaft | Typ | Beschreibung / Zweck |
|-------------|-----|----------------------|
| `ProgramRuleId` / `ProgramRule` | `long` / `ProgramRule` | Besitzende Regel |
| `ProgramClipGroupId` / `ClipGroup` | `long` / `ProgramClipGroup` | Zugeordnete Clip-Gruppe; `ClipGroup.Name` wird über `ClipGroupSuffix` als „Clips: …" angehängt |

## `ProgramRuleDiagnosis` (readonly record struct)
Datei: `VideoWebPlayer/Services/IProgramScheduleService.cs` (Zeilen 71–80)

Pro-Regel-Planungsdiagnose für die Admin-Ansicht; befüllt `ruleDiagnoses` und wird von
`DiagnosisLabel` zu Text aufbereitet (Spalte „Planbar").

| Positionsparameter | Typ | Beschreibung / Zweck |
|--------------------|-----|----------------------|
| `RuleId` | `long` | Zugehörige Regel |
| `MaterialMissing` | `bool` | Referenziertes Material nicht mehr auflösbar → „Material nicht gefunden (verwaist)" |
| `CandidateCount` | `int` | Anzahl expandierter Kandidaten (von `DiagnosisLabel` nicht direkt verwendet) |
| `SchedulableCount` | `int` | Planbare Inhalte → „N planbar" |
| `NoMediaItemCount` | `int` | Ohne Mediendatei → „N ohne Mediendatei" |
| `ProbePendingCount` | `int` | Laufzeitermittlung läuft → „N ohne Laufzeit (Ermittlung läuft)" |
| `ProbeExhaustedCount` | `int` | Laufzeitermittlung gescheitert → „N ohne Laufzeit (gescheitert)" |
| `NoAccessCount` | `int` | Nicht für alle freigegeben → „N nicht für alle freigegeben" |
| `AgeWindowBlockedCount` | `int` | Nicht altersgerecht im Sendefenster → „N nicht altersgerecht im Sendefenster" bzw. „kein altersgerechtes Material im Fenster" |

## `ProgramRuleInput` (sealed record)
Datei: `VideoWebPlayer/Services/ProgramRuleInput.cs` (Zeilen 13–29)

Eingabe-Record für `CreateRuleAsync`/`UpdateRuleAsync`; wird in `SaveRuleAsync` aus dem
`RuleEditModel` zusammengesetzt. Für die Übersicht nicht direkt relevant, aber der einzige
Datenvertrag zwischen Editor und Service.

Parameter: `Name`, `RuleType`, `Recurrence`, `DayOfWeek`, `SingleDate`, `TimeOfDay`,
`EpisodeCount`, `SlotDuration`, `MaterialType`, `MaterialId`, `Priority`, `IsActive`,
`GenreIds?`, `MediaSourceIds?`, `ClipGroupIds?`, `PlaceholderGrace?`.

## `RuleEditModel` (private sealed class)
Datei: `VideoWebPlayer/Components/Pages/Admin/TVSenderAdmin.razor` (Zeilen 2282–2302)

Dialog-Viewmodel des Regel-Editors (`editRule`-Feld). Für die Übersicht relevant:
`MaterialDisplayName` wird beim Öffnen des Editors mit `MaterialLabel(rule)` befüllt
(Zeile 1909) — die Listenform des Labels bleibt damit auch nach einer kompakten
Übersichts-Darstellung im Editor-Chip `#rule-material-selected` erhalten.

## `DtoGenreOption` (sealed class)
Datei: `VideoWebPlayer.Client/Models/DtoMovie.cs` (Zeilen 100–111)

Nur `Id` + `Name`; Optionen der Genre-Mehrfachauswahl (`genreOptions`), befüllt von
`ReloadGenreOptionsAsync` über `IProgramRuleService.GetGenreMaterialOptionsAsync`. Wird
von `UpdateGenreMaterialLabel` zum Editor-Label zusammengesetzt.
