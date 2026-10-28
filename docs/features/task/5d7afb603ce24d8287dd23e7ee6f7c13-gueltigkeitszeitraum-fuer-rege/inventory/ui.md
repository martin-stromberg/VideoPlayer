# UI — Bestandsaufnahme

## `TVSenderAdmin.razor`
Datei: `VideoWebPlayer/Components/Pages/Admin/TVSenderAdmin.razor` (2346 Zeilen); Route: `/admin/tv-sender`; Tabs: Regeln / Sendeplan / Einstellungen / Laufzeitermittlung / Clip-Gruppen (Z. 94–98 im E2E-Test dokumentiert).

### Regelliste (Tab „Regeln", Z. 108–175)

- `rules` (`List<ProgramRule>`) kommt aus `ReloadRulesAsync` (Z. 1395) → `ProgramRuleService.GetRulesAsync(userId, channel.Id)` — Rendern in **der vom Service gelieferten Reihenfolge** (aktuell `OrderBy(Name)`); kein UI-seitiges Sortieren. Eine End-Sortierung ungültiger wiederkehrender Regeln müsste daher serverseitig erfolgen oder hier ergänzt werden.
- Pro Regel ein `.rule-panel` (Z. 131–167): Chips `Aktiv`/`Inaktiv` (`admin-chip-success`/`-warning`, Z. 134), `Priorität n` (Z. 135–138), `rule-panel-name`, Aktionsbuttons Bearbeiten (`StartEdit`, Z. 142), Toggle (`ToggleActiveAsync`, Z. 145), Löschen (`AskDelete`, Z. 157).
- Metazeilen: `RuleMetaLabel(rule)` (Z. 163), `MaterialSummaryLabel(rule)` (Z. 164), `DiagnosisLabel(rule)` (Z. 165, `.tvsender-rule-diagnosis`) — keine Gültigkeitsanzeige vorhanden; Muster für eine Status-Kennzeichnung wären die Chips oder `RuleMetaLabel`.

### Regel-Editor-Dialog (`#rule-editor`, Z. 177–389)

Overlay-Dialog; Felder: `rule-name`, `rule-type` (Select über `Enum.GetValues<ProgramRuleType>` + `RuleTypeLabel`, Z. 192–197), `rule-recurrence` (Select mit festen Optionen Täglich/Wöchentlich/Einmalig/An Feiertagen/Werktags/Wochenende, Z. 201–208), konditional `rule-weekday` (Z. 213–224) bzw. `rule-date` (`InputDate`, Z. 225–231), `rule-time`, `rule-episodes`, `rule-duration`, `rule-priority`, `IsActive`-Checkbox (Z. 253–256), Material-Picker (`#rule-material-picker`, Genre-Mehrfachauswahl `#rule-material-genre`, Quellfilter `#rule-material-sources`), Clip-Gruppen `#rule-clip-groups` + `rule-grace-minutes` (nur Nicht-Filler, Z. 340–376), `rule-edit-error`, `rule-save`.

**Kein** Gültigkeitsabschnitt (Art-Auswahl, Von/Bis bzw. Monat-Tag-Eingaben) vorhanden — der `InputDate`-Einsatz bei `rule-date` und die konditionalen Bereiche (`Weekly`/`Once`) sind das vorhandene Muster.

### Code-Behind (relevante Methoden)

| Methode | Zeile | Kurzbeschreibung |
|---------|-------|------------------|
| `ReloadRulesAsync` | 1395 | `rules = await ProgramRuleService.GetRulesAsync(userId, channel.Id)` + `ReloadDiagnosesAsync` |
| `ReloadDiagnosesAsync` | 1402 | `ProgramScheduleService.GetRuleDiagnosesAsync` → `ruleDiagnoses` |
| `GenerateNowAsync` | 1412 | `ScheduleSignal.NotifyScheduleChanged()` + verzögertes Neuladen |
| `DiagnosisLabel` | 1674 | Pro-Regel-Statustext aus `ProgramRuleDiagnosis` (verwaist/planbar/Fehler) |
| `StartCreate` | 1884 | `editRule = new RuleEditModel { RuleType = SeriesFixedTime, Recurrence = Daily, TimeOfDay = 20:00, IsActive = true, EpisodeCount = 1 }` |
| `StartEdit` | 1891 | Befüllt `RuleEditModel` aus der `ProgramRule` (u. a. `DayOfWeek ?? Monday`, `SingleDate?.ToDateTime(MinValue)`, `GraceMinutes`) — neue Gültigkeitsfelder müssten hier übernommen werden |
| `OnRuleTypeChanged` / `OnRecurrenceChanged` | 1925 / 1935 | Select-Handler; `OnRuleTypeChanged` leert unzulässiges Material |
| `SaveRuleAsync` | 2053 | Baut `ProgramRuleInput` positional (Z. 2059–2085) → `CreateRuleAsync`/`UpdateRuleAsync`; Fehler → `editError`; Erfolg → `ReloadRulesAsync` — neue Felder müssten hier ins Input-Record aufgenommen werden |
| `ToggleActiveAsync` | 2104 | `SetRuleActiveAsync` + Reload |
| `AskDelete` / `DeleteRuleAsync` | 2118 / 2139 | Lösch-Dialog `#rule-delete-confirm` (Z. 968) → `DeleteRuleAsync` |
| `MaterialLabel`/`MaterialSummaryLabel`/`ClipGroupSuffix`/`ResolveGenreNames` | 2161–2205 | Materialanzeige |
| `RuleTypeLabel` | 2234 | Labels der `ProgramRuleType`-Werte |
| `RecurrenceLabel` | 2245 | Labels der `ProgramRuleRecurrence` (inkl. Datum bei `Once`) |
| `DurationLabel` / `RuleMetaLabel` | 2256 / 2268 | Meta-Zeile `Regelart · Wiederholung · Zeit · Dauer` |
| `WeekdayLabel` | 2280 | Wochentagsnamen |

### `RuleEditModel` (Z. 2325–2345)

`Id, Name, RuleType, Recurrence, DayOfWeek?, SingleDate? (DateTime?), TimeOfDay, EpisodeCount?, DurationMinutes?, MaterialType, MaterialId, MaterialDisplayName, SelectedGenreIds, SelectedMediaSourceIds, SelectedClipGroupIds, GraceMinutes?, Priority, IsActive` — **keine** Gültigkeitsfelder.

### Sonstige UI-Anknüpfungspunkte

- `GenreCanonicalResolver.BuildOptions(..., includeOutOfSeason: true)` markiert Out-of-Season-Genres im Optionslabel `Name (dd.MM.–dd.MM.)` (`ProgramRuleService.GetGenreMaterialOptionsAsync`) — vorhandenes UI-Muster für „aktuell außerhalb des Fensters".
- `SeasonalGenreList.razor` (`Components/Shared/Home/`): saisonale Genre-Startseitenliste, konsumiert `Client.GetSeasonalGenresAsync()`.
- Hilfe-Doku vorhanden: `docs/help/tv-sender.md` (Admin-Oberfläche), `docs/help/tv-sender-datenmodell.md` (Entitäten), `docs/help/tv-sender-ablauf-technisch.md` (Planungs-/Reconcile-Ablauf) — bei Umsetzung zu ergänzen (DoD).
