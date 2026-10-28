# Plan-Review

## Ergebnis

**Status:** Vollständig umgesetzt

## Umgesetzte Planelemente

- [x] `.rule-panel` (CSS-Klasse, `app.css` Z. 921) — angelegt
- [x] `.rule-panel-head` (CSS-Klasse, `app.css` Z. 931) — angelegt
- [x] `.rule-panel-badges` (CSS-Klasse, `app.css` Z. 937) — angelegt
- [x] `.rule-panel-name` (CSS-Klasse, `app.css` Z. 945) — angelegt
- [x] `.rule-panel-meta` (CSS-Klasse, `app.css` Z. 953) — angelegt
- [x] `.rule-panel-actions` (CSS-Klasse, `app.css` Z. 965) — angelegt
- [x] `.rule-panel-material` (CSS-Klasse, `app.css` Z. 959) — angelegt (zusätzlich zur Plan-Liste; setzt die geforderte einzeilige Material-Zeile mit Ellipse um)
- [x] Methode `MaterialSummaryLabel(ProgramRule)` in `TVSenderAdmin.razor` (Z. 2174) — vorhanden, `private string`; Genre-Anzahl mit `MaterialId`-Fallback (`n = 1` bei DB-Treffer, sonst „Genre (nicht gefunden)"), Suffixe `— {m} Quelle(n)` / `— {k} Clip-Gruppe(n)` nur bei `> 0`, Nicht-Genre-Arten über `MaterialLabel(type, id)` + Clip-Gruppen-Suffix
- [x] `MaterialLabel(ProgramRule)` Listenform in `TVSenderAdmin.razor` (Z. 2158) — unverändert erhalten, weiterhin von `StartEdit` für `MaterialDisplayName` genutzt (Z. 1910)
- [x] Markup `.program-grid` + `@foreach` über `rules` statt `.admin-table-wrap > table` (`TVSenderAdmin.razor` Z. 128–169) — ersetzt
- [x] Panel `div.rule-panel.tvsender-rule-row` mit `data-rule-id="@rule.Id"` (Z. 131) — vorhanden
- [x] Status-Chip `admin-chip-success`/`-warning` („Aktiv"/„Inaktiv") in `.rule-panel-badges` vor `.rule-panel-name` (Z. 133–140) — vorhanden
- [x] Prioritäts-Chip „Priorität {n}" nur bei `rule.Priority != 0` (Z. 135–138) — vorhanden
- [x] Symbolbuttons `.rule-panel-actions`: Edit (Stift), Toggle (Pause bei aktiv / Play bei inaktiv), Delete (Papierkorb + `btn-icon-danger`); alle `btn btn-ghost btn-icon` mit Inline-SVG 18×18, `aria-hidden="true"`, `stroke="currentColor"`, `title` + `aria-label` (Z. 141–160) — vorhanden
- [x] Meta-Zeilen: `RuleTypeLabel`/`RecurrenceLabel`/`TimeOfDay`/`DurationLabel` mit Trennzeichen, `MaterialSummaryLabel` einzeilig, `.tvsender-rule-diagnosis` mit `DiagnosisLabel` (Z. 162–166) — vorhanden
- [x] Hooks `.tvsender-rule-row`, `data-rule-id`, `.tvsender-rule-edit`, `.tvsender-rule-toggle`, `.tvsender-rule-delete`, `.tvsender-rule-diagnosis` — alle auf den Panel-Elementen erhalten
- [x] Unverändertes Markup: `.admin-card-header` mit Zähler-Chip + `#tvsender-new-rule`, `.admin-empty-state`, `#tvsender-rules-status`, `#rule-editor`, `#rule-delete-confirm` — erhalten
- [x] Eager-Loading in `ProgramRuleService.GetRulesAsync` (`ProgramRuleService.cs` Z. 78–89: `Genres`, `MediaSourceFilters`, `ClipGroups`) — unverändert vorhanden
- [x] Test `RulePanel_GenreRule_ShowsGenreCount` in `TVSenderAdminTests.cs` (Z. 253) — vorhanden, grün
- [x] Test `RulePanel_GenreRule_ShowsSourceAndClipGroupCounts` in `TVSenderAdminTests.cs` (Z. 277) — vorhanden, grün
- [x] Test `RulePanel_PriorityBadge_HiddenWhenZero_ShownWhenSet` in `TVSenderAdminTests.cs` (Z. 304) — vorhanden, grün
- [x] Test `RulePanel_StatusBadge_BeforeName` in `TVSenderAdminTests.cs` (Z. 331) — vorhanden, grün
- [x] Test `RulePanel_IconButtons_HaveAriaLabels` in `TVSenderAdminTests.cs` (Z. 354) — vorhanden, grün
- [x] Test `RulePanel_RendersDataRuleId` in `TVSenderAdminTests.cs` (Z. 388) — vorhanden, grün
- [x] Angepasster Test `RuleEditor_ShowsClipGroupSelection_GraceField_AndMaterialLabelSuffix` in `TVSenderAdminTests_ClipGroups.cs` (Z. 167–212) — prüft „1 Clip-Gruppe(n)" in der Übersicht und „Clips: Maskottchen" im Editor-Chip `#rule-material-selected`, grün
- [x] Test `RuleDiagnosis_ShowsAgeWindowReason` in `TVSenderAdminTests_AgeWindow.cs` (Z. 99–121) — benötigte keine Code-Änderung (`FindAll` auf Klasse ist elementunabhängig), grün
- [x] E2E `Admin_CreateEditDeactivateDeleteRule` in `TVSenderAdminE2ETests.cs` — auf Symbolbuttons/`aria-label` umgestellt (Z. 196–233), grün
- [x] E2E `Admin_GenreRule_MultiSelectAndSourceFilter` in `TVSenderAdminE2ETests.cs` — Anzahl-Form „Genre: 2 Genres" / „1 Quelle(n)", Namen werden negativ geprüft (Z. 407–414), grün
- [x] E2E `RuleEditor_ShowsClipGroupSelectionAndGraceField` in `TVSenderAdminClipGroupsE2ETests.cs` — „2 Clip-Gruppe(n)" statt „Clips: Maskottchen" (Z. 217–220), grün
- [x] E2E `Admin_RuleDiagnosis_ShowsAgeWindowReason` in `TVSenderAdminE2ETests_AgeWindow.cs` — Locator elementunabhängig, unverändert lauffähig, grün
- [x] `docs/help/tv-sender.md` — Z. 33 „Programmregeln als Panels" sowie weitere Stellen (Z. 313, 412) auf Panel-Begriff umgestellt
- [x] Build `dotnet build VideoPlayer.sln` Debug und Release — je 0 Fehler (im Review ausgeführt)
- [x] Volle Testsuite `dotnet test VideoWebPlayer.Tests/VideoWebPlayer.Tests.csproj` — 2617 erfolgreich, 2 übersprungen, 1 einmaliger Ausfall (siehe Hinweise; bei Einzelwiederholung bestanden)

## Hinweise

- **Zusätzliche CSS-Klasse `.rule-panel-material`:** Nicht in der Plan-Tabelle aufgeführt, aber fachlich durch die Anforderung „Material-Zeile einzeilig mit Ellipse" gedeckt (im Plan bei `.rule-panel-meta` beschrieben). Kein Planverstoß.
- **AgeWindow-Tests ohne Diff:** Sowohl der bUnit- als auch der E2E-AgeWindow-Test nutzen Selektoren auf `.tvsender-rule-diagnosis`, die nicht an `<td>` gebunden sind — die geplante Anpassung entfiel, weil keine nötig war. Beide Tests sind gegen das neue Markup gelaufen und grün.
- **Einmaliger Playwright-Ausfall:** `PlaylistPlaybackE2ETests.PlaylistSkipLockedEntriesE2ETest` schlug im Gesamtlauf unter Last einmal fehl und bestand bei isolierter Wiederholung (11 s). Fachfremd (Playlist-Wiedergabe, kein Bezug zur Regelübersicht) — dokumentiert gemäß AGENTS.md.
- **Arbeitsstand uncommitted:** Alle Implementierungs- und Teständerungen liegen als uncommittete Modifikationen im Arbeitsverzeichnis (`git status`: `TVSenderAdmin.razor`, `app.css`, 4 Testdateien, `tv-sender.md`, `todo.md`). Es gibt noch keinen Feature-Commit.
