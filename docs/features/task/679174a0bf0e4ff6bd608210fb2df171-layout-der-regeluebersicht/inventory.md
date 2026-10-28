# Bestandsaufnahme: Layout der Regelübersicht (TV-Sender)

Analysiert wurde die Programmregeln-Übersicht im Tab „Regeln" der Admin-Seite
`TVSenderAdmin.razor` (`/admin/tv-sender`) sowie alle Artefakte, die für die Umstellung
der `<table>`-Darstellung auf vertikale Informations-Panels je `ProgramRule` relevant
sind (Datenmodell, Services, Label-Methoden, wiederverwendbare CSS-Bausteine, Tests).

## Zusammenfassung

- **Ist-Zustand:** Die Übersicht ist eine echte `<table>` mit 10 Spalten in
  `.admin-table-wrap` (`TVSenderAdmin.razor` Zeilen 128–168). Jede Zeile ist
  `<tr class="tvsender-rule-row" data-rule-id="@rule.Id">`; Aktionen sind drei
  Text-Buttons (`.tvsender-rule-edit`, `.tvsender-rule-toggle`, `.tvsender-rule-delete`).
- **Alle Label-Methoden für ein Panel existieren bereits:** `RuleTypeLabel`,
  `RecurrenceLabel`, `DurationLabel`, `MaterialLabel`, `DiagnosisLabel`, `WeekdayLabel`
  sowie `ClipGroupSuffix` — alle `private`/`private static` im `@code`-Block.
- **Lange Zeilen kommen von `MaterialLabel` (Z. 2157):** Bei `MaterialType.Genre` listet
  es alle Genre-Namen, alle Quell-Filter („— Quellen: …") und alle Clip-Gruppen
  (`ClipGroupSuffix`, „— Clips: …"). Es existiert **keine** kompakte Anzahl-Variante;
  `rule.Genres.Count`, `rule.MediaSourceFilters` und `rule.ClipGroups` stehen aber über
  `GetRulesAsync` eager geladen zur Verfügung. `MaterialLabel` wird außerdem von
  `StartEdit` für den Editor-Chip `MaterialDisplayName` (Z. 1909) wiederverwendet —
  die Listenform muss also bestehen bleiben.
- **Gestaltungsvorlagen sind vorhanden:** Die `.program-card`-Familie des Sendeplan-Tabs
  (Markup Z. 417–512; CSS `app.css` Z. 630–781, Admin-Overrides Z. 784–814), die
  `.admin-chip*`-Badges (Z. 489–524, 2859–2875) und die Symbolbutton-Klassen
  `.btn-icon`/`.btn-icon-danger` (Z. 2552–2580). Musterhafte Symbolbutton-Verwendung:
  `Backups.razor` Z. 181–189 und `tvsender-clip-block-toggle` Z. 458 (Inline-SVG 18 px,
  `aria-hidden`, `title` + `aria-label`).
- **Status-Anzeige heute:** Chip „Aktiv"/„Inaktiv" in eigener Spalte (Z. 156–158);
  Priorität als nackte Zahl, auch „0" (Z. 155). Eine Kachel-Auslagerung hätte einen
  Präzedenzfall: `TVSenderAdminClipGroups.razor` ist bereits Kind-Komponente der Seite
  (Einbindung Z. 905–906).
- **Unverändert nutzbar:** `ProgramRule` samt Join-Entitäten, `IProgramRuleService`,
  `IProgramScheduleService.GetRuleDiagnosesAsync`/`ProgramRuleDiagnosis`, die drei
  Regel-Enums und alle Event-Handler (`StartCreate`, `StartEdit`, `ToggleActiveAsync`,
  `AskDelete`/`DeleteRuleAsync`, `SaveRuleAsync`, `CancelEdit`). Berechtigung wird
  serverseitig in den Services erzwungen — die UI-Änderung berührt das nicht.
- **Test-Hooks werden benutzt:** `.tvsender-rule-row`, `-edit`, `-toggle`, `-delete`,
  `.tvsender-rule-diagnosis` in Playwright-Tests (`TVSenderAdminE2ETests`,
  `TVSenderAdminClipGroupsE2ETests`, `TVSenderAdminE2ETests_AgeWindow`) und bUnit-Tests
  (`TVSenderAdminTests_AgeWindow`, `TVSenderAdminTests_ClipGroups`). Zwei Tests prüfen
  die Listenform des Material-Texts in der Zeile („Genre: E2E Action, E2E Komödie",
  „Clips: Maskottchen"). Das Attribut `data-rule-id` existiert auf der Zeile, wird
  aber an Regelzeilen von keinem Test gelesen (die einzige Assertion gilt dem
  `.tvsender-rule-divider` im Sendeplan). „Inaktiv" wird als Text erwartet.
- **Dokumentation:** `docs/help/tv-sender.md` Z. 33 beschreibt den Tab als
  „Programmregeln-Tabelle".

## Details

- [Datenmodell](inventory/models.md) — `ProgramRule`, Join-Entitäten
  (`ProgramRuleGenre`, `ProgramRuleMediaSource`, `ProgramRuleClipGroup`),
  `ProgramRuleDiagnosis`, `ProgramRuleInput`, `RuleEditModel`, `DtoGenreOption`
- [Logik](inventory/logic.md) — `@code`-Block von `TVSenderAdmin.razor` (Felder,
  Label-Methoden, Event-Handler), `ProgramRuleService`, `ProgramScheduleService`,
  `TVSenderAdminClipGroups` als Auslagerungs-Präzedenzfall
- [Enums](inventory/enums.md) — `ProgramRuleType`, `ProgramRuleRecurrence`,
  `ProgramRuleMaterialType` inkl. Label-Mapping
- [Interfaces](inventory/interfaces.md) — `IProgramRuleService`,
  `IProgramScheduleService`, `IProgramClipGroupService`, `IProgramScheduleSignal`
- [UI-Komponenten](inventory/ui.md) — Regel-Tabelle (Zeilen/Elemente/Hooks),
  Editor- und Lösch-Dialog, Sendeplan-Karten-Vorlage, `Backups.razor`-Symbolbuttons,
  `LiveTv.razor`, vollständige CSS-Bestandsliste mit Zeilenbereichen, Hilfe-Doku
- [Tests](inventory/tests.md) — bUnit- und Playwright-Tests mit ihren Selektoren,
  Test-Hook-Tabelle, Hilfsklassen `ProgramScheduleTestDb`, `CreateContextAsync`,
  `TVSenderAdminE2ETestBase`
