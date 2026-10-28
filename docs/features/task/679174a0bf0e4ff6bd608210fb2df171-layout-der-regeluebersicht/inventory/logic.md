# Logik — Bestandsaufnahme

## `TVSenderAdmin` (Razor-Komponente, `@code`-Block)
Datei: `VideoWebPlayer/Components/Pages\Admin\TVSenderAdmin.razor` (`@code` ab Zeile 1025)

### Für die Regel-Übersicht relevante Felder

| Feld | Typ | Zweck |
|------|-----|-------|
| `rules` (Z. 1073) | `List<ProgramRule>` | Datenquelle der `@foreach` über die Regelzeilen |
| `ruleDiagnoses` (Z. 1075) | `IReadOnlyDictionary<long, ProgramRuleDiagnosis>` | Planbar-Diagnose je Regel; Grundlage von `DiagnosisLabel` |
| `editRule` (Z. 1076) | `RuleEditModel?` | Nicht `null` ⇒ Editor-Dialog `#rule-editor` sichtbar |
| `deleteCandidate` (Z. 1077) | `ProgramRule?` | Nicht `null` ⇒ Lösch-Dialog `#rule-delete-confirm` sichtbar |
| `rulesStatus` (Z. 1058) | `string?` | Statuszeile `#tvsender-rules-status` unter der Übersicht |
| `isAdmin` (Z. 1026) | `bool` | Clientseitige Sichtprüfung („Nicht autorisiert"); die eigentliche Durchsetzung liegt in den Services (`RequireAdminAsync`) |
| `activeTab` (Z. 1086) | `string` | Aktiver Tab (`"regeln"` initial); umschaltbar über `SelectTab` (Z. 1192) |

### Methoden, die die Übersicht rendern

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `OnInitializedAsync` (Z. 1132) | `protected override` | Lädt u. a. `channel`, `mediaSources`, `genreOptions`, `playlistOptions` und ruft `ReloadRulesAsync`, `ReloadScheduleAsync`, `ReloadClipGroupsAsync` |
| `ReloadRulesAsync` (Z. 1394) | `private` | `ProgramRuleService.GetRulesAsync(userId, channel.Id)` → `rules`, danach `ReloadDiagnosesAsync` |
| `ReloadDiagnosesAsync` (Z. 1401) | `private` | `ProgramScheduleService.GetRuleDiagnosesAsync(userId, channel.Id)` → `ruleDiagnoses` |
| `MaterialLabel(ProgramRule)` (Z. 2157) | `private` | Listenform: bei `Genre` alle Genre-Namen sortiert + „— Quellen: …" + `ClipGroupSuffix`; sonst `MaterialLabel(type, id)` + `ClipGroupSuffix`. Wird in der Tabelle (Z. 153) und von `StartEdit` für `MaterialDisplayName` (Z. 1909) aufgerufen |
| `MaterialLabel(ProgramRuleMaterialType, long)` (Z. 2178) | `private` | Kurzform „Typ: Name" je Materialart (DB-Lookup über `DbContext`); `Trailer` ohne Lookup |
| `ClipGroupSuffix(ProgramRule)` (Z. 2173) | `private static` | `„ — Clips: a, b"` wenn `rule.ClipGroups.Count > 0`, sonst leer |
| `RuleTypeLabel(ProgramRuleType)` (Z. 2205) | `private static` | Deutsche Regelart-Texte (siehe [enums.md](enums.md)) |
| `RecurrenceLabel(ProgramRule)` (Z. 2216) | `private static` | Wiederholung inkl. Wochentag/Datum (`WeekdayLabel`) |
| `DurationLabel(ProgramRule)` (Z. 2227) | `private static` | `„N Folge(n) / M Min"`, leer ohne Angaben |
| `WeekdayLabel(DayOfWeek)` (Z. 2237) | `private static` | Deutsche Wochentagsnamen |
| `DiagnosisLabel(ProgramRule)` (Z. 1673) | `private` | Setzt `ruleDiagnoses` in die Planbar-Texte um; lange Varianten möglich (z. B. „Nicht planbar: 3 ohne Mediendatei, …") |

### Event-Handler der Übersicht (bleiben fachlich unverändert)

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `StartCreate` (Z. 1883) | `private` | Öffnet Editor mit neuem `RuleEditModel` (Standard: `SeriesFixedTime`, `Daily`, 20:00, `IsActive`, `EpisodeCount = 1`); Aufrufer: `#tvsender-new-rule` (Z. 116) |
| `StartEdit(ProgramRule)` (Z. 1890) | `private` | Befüllt `RuleEditModel` aus der Regel inkl. `SelectedGenreIds`, `SelectedMediaSourceIds`, `SelectedClipGroupIds`, `MaterialDisplayName = MaterialLabel(rule)` |
| `ToggleActiveAsync(ProgramRule)` (Z. 2103) | `private` | `ProgramRuleService.SetRuleActiveAsync(userId, rule.Id, !rule.IsActive)` + Reload; Status „Aktivieren"/„Deaktivieren" hängt nur von `rule.IsActive` ab |
| `AskDelete(ProgramRule)` (Z. 2117) | `private` | Setzt `deleteCandidate` → Bestätigungsdialog |
| `DeleteRuleAsync` (Z. 2138) | `private` | `ProgramRuleService.DeleteRuleAsync` + Reload; `#rule-delete-confirm` |
| `CancelEdit` (Z. 1922) | `private` | Schließt den Editor (`editRule = null`), auch Klick auf Overlay |
| `SaveRuleAsync` (Z. 2052) | `private` | Baut `ProgramRuleInput`, ruft `CreateRuleAsync`/`UpdateRuleAsync`; `#rule-save` |

### Editor-Hilfsmethoden (unverändert, nur Kontext)

`OnRuleTypeChanged` (Z. 1924), `OnRecurrenceChanged` (Z. 1934), `ClearMaterial` (Z. 1940),
`OnMaterialSelected` (Z. 1951), `OnGenreToggled` (Z. 1963), `OnSourceToggledAsync` (Z. 1976),
`UpdateGenreMaterialLabel` (Z. 1988 — baut `MaterialDisplayName` als „Genre: a, b" im Editor),
`OnPlaylistSelected` (Z. 2005), `OnClipSourceSelected` (Z. 2019), `OnTrailerToggled` (Z. 2033),
`OnClipGroupToggled` (Z. 2127), `ReloadClipGroupsAsync` (Z. 2121), `ReloadGenreOptionsAsync` (Z. 1369).

Abonnierte Events: keine — die Komponente abonniert kein Event; `IProgramScheduleSignal` wird
nur publiziert (`NotifyScheduleChanged` in `GenerateNowAsync` Z. 1420, `ResetScheduleAsync`
Z. 1469, `SaveChannelSettingsAsync` Z. 1874).

## `ProgramRuleService`
Datei: `VideoWebPlayer/Services/ProgramRuleService.cs`

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `GetRulesAsync` (Z. 78) | `public` | Admin-gegat; lädt Regeln nach Name sortiert mit `Include(r => r.Genres).ThenInclude(g => g.Genre)`, `Include(r => r.MediaSourceFilters).ThenInclude(s => s.MediaSource)`, `Include(r => r.ClipGroups).ThenInclude(c => c.ClipGroup)` (Z. 84–89) — alle für `MaterialLabel`/Zählungen benötigten Navigationen sind bereits eager geladen |
| `GetAllowedMaterialTypes` | `public` | Materialarten je `ProgramRuleType` (im Editor über `AllowedMaterialTypes`, Z. 1123) |
| `CreateRuleAsync` / `UpdateRuleAsync` / `SetRuleActiveAsync` / `DeleteRuleAsync` / `RenameChannelAsync` / `GetOrCreateChannelAsync` / `GetGenreMaterialOptionsAsync` | `public` | Service-Seite der Handler oben; Berechtigung serverseitig |

## `ProgramScheduleService`
Datei: `VideoWebPlayer/Services/ProgramScheduleService.cs`

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `GetRuleDiagnosesAsync` (Z. 424) | `public` | Admin-gegat; expandiert jede Regel des Senders (auch inaktive) mit der Planungslogik und liefert `IReadOnlyDictionary<long, ProgramRuleDiagnosis>` (Aufbau Z. 452–…) |

## `TVSenderAdminClipGroups` (Kind-Komponente — Präzedenzfall)
Datei: `VideoWebPlayer/Components/Pages/Admin/TVSenderAdminClipGroups.razor`

Eingebunden im Tab „Clip-Gruppen" (`TVSenderAdmin.razor` Z. 905–906) mit Parametern
`UserId`, `Groups`, `Counts`, `MediaSources` und `EventCallback OnGroupsChanged`
(Parameter-Deklaration Z. 195–207). Zeigt, wie ein Teilbereich der Seite in eine eigene
Razor-Komponente ausgelagert ist — eigener `@inject IProgramClipGroupService`, eigener
`IDisposable`-Lebenszyklus.
