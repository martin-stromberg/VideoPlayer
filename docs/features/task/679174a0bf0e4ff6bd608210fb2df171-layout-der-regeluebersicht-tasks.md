# Tasks: Layout der Regelübersicht (TV-Sender)

| # | Bereich | Aufgabe | Status | Testnachweis |
|---|---------|---------|--------|--------------|
| 1 | UI/CSS | `.rule-panel`-Container-Stil in `app.css` anlegen (Oberfläche, Rahmen, Radius, Padding — Vorbild `.program-card`/`.backup-list-item`) | Offen | — |
| 2 | UI/CSS | `.rule-panel-head` (Flex-Kopfzeile, Aktionen rechts) in `app.css` anlegen | Offen | — |
| 3 | UI/CSS | `.rule-panel-badges` (Badge-Zeile vor dem Namen) in `app.css` anlegen | Offen | — |
| 4 | UI/CSS | `.rule-panel-name` (einzeilig mit Ellipse) in `app.css` anlegen | Offen | — |
| 5 | UI/CSS | `.rule-panel-meta` (gedämpfte Meta-Zeilen, Material-Zeile einzeilig mit Ellipse) in `app.css` anlegen | Offen | — |
| 6 | UI/CSS | `.rule-panel-actions` (rechtsbündige Symbolbutton-Gruppe) in `app.css` anlegen | Offen | — |
| 7 | Logik | `MaterialSummaryLabel(ProgramRule)` als private Methode in `TVSenderAdmin.razor` ergänzen (Genre-Anzahl mit `MaterialId`-Fallback, `— {m} Quelle(n)`, `— {k} Clip-Gruppe(n)`; Nicht-Genre über `MaterialLabel(type, id)`) | Offen | — |
| 8 | UI | Tabellen-Block in `TVSenderAdmin.razor` (Z. ~128–168) durch `.program-grid` mit `@foreach` über `.rule-panel.tvsender-rule-row`-Panels ersetzen; `data-rule-id` und Hooks (`.tvsender-rule-row`, `-edit`, `-toggle`, `-delete`, `-diagnosis`) beibehalten | Offen | — |
| 9 | UI | Panel-Kopf aufbauen: Status-Chip (`admin-chip-success`/`-warning`, „Aktiv"/„Inaktiv") und Prioritäts-Chip („Priorität {n}", nur bei `Priority != 0`) in `.rule-panel-badges` vor `.rule-panel-name` | Offen | — |
| 10 | UI | Drei Symbolbuttons in `.rule-panel-actions`: `btn-icon` mit Inline-SVG (18 px, `aria-hidden`, `stroke="currentColor"`), `title` + `aria-label`; Edit (Stift), Toggle (Pause bei aktiv / Play bei inaktiv), Delete (Papierkorb + `btn-icon-danger`) | Offen | — |
| 11 | UI | Meta-Zeilen im Panel: `RuleTypeLabel`/`RecurrenceLabel`/`TimeOfDay`/`DurationLabel`, `MaterialSummaryLabel` (einzeilig), `.tvsender-rule-diagnosis` mit `DiagnosisLabel` | Offen | — |
| 12 | Tests | bUnit-Test `RulePanel_GenreRule_ShowsGenreCount` in `TVSenderAdminTests` (Genre-Anzahl statt Namen) | Offen | — |
| 13 | Tests | bUnit-Test `RulePanel_GenreRule_ShowsSourceAndClipGroupCounts` in `TVSenderAdminTests` (Quellen-/Clip-Gruppen-Anzahlen) | Offen | — |
| 14 | Tests | bUnit-Test `RulePanel_PriorityBadge_HiddenWhenZero_ShownWhenSet` in `TVSenderAdminTests` | Offen | — |
| 15 | Tests | bUnit-Test `RulePanel_StatusBadge_BeforeName` in `TVSenderAdminTests` (Reihenfolge im Markup) | Offen | — |
| 16 | Tests | bUnit-Test `RulePanel_IconButtons_HaveAriaLabels` in `TVSenderAdminTests` (`aria-label`/`title`, Toggle-Zustände) | Offen | — |
| 17 | Tests | bUnit-Test `RulePanel_RendersDataRuleId` in `TVSenderAdminTests` | Offen | — |
| 18 | Tests | `TVSenderAdminTests_ClipGroups.RuleEditor_ShowsClipGroupSelection_GraceField_AndMaterialLabelSuffix` anpassen (Anzahl-Form in der Übersicht; Listenform ggf. über Editor-Chip prüfen) | Offen | — |
| 19 | Tests | `TVSenderAdminTests_AgeWindow.RuleDiagnosis_ShowsAgeWindowReason` anpassen (`.tvsender-rule-diagnosis` liegt nicht mehr in `<td>`) | Offen | — |
| 20 | E2E-Tests | `TVSenderAdminE2ETests.Admin_CreateEditDeactivateDeleteRule` anpassen (Locators auf Symbolbuttons/`aria-label`, Panel statt Tabellenzeile) | Offen | — |
| 21 | E2E-Tests | `TVSenderAdminE2ETests.Admin_GenreRule_MultiSelectAndSourceFilter` anpassen („Genre: 2 Genres" + Quellen-Anzahl statt Listenform) | Offen | — |
| 22 | E2E-Tests | `TVSenderAdminClipGroupsE2ETests.RuleEditor_ShowsClipGroupSelectionAndGraceField` anpassen („… Clip-Gruppe(n)" statt „Clips: Maskottchen") | Offen | — |
| 23 | E2E-Tests | `TVSenderAdminE2ETests_AgeWindow.Admin_RuleDiagnosis_ShowsAgeWindowReason` prüfen/anpassen (Locator nicht mehr auf `<td>`) | Offen | — |
| 24 | Dokumentation | `docs/help/tv-sender.md` Z. ~33: Formulierung „Programmregeln-Tabelle" an Panel-Layout anpassen | Offen | — |
| 25 | Verifikation | `dotnet build VideoPlayer.sln` in Debug und Release fehlerfrei | Offen | — |
| 26 | Verifikation | Volle Testsuite `dotnet test VideoWebPlayer.Tests/VideoWebPlayer.Tests.csproj` grün (einmalige Playwright-Ausfälle einzeln wiederholen und dokumentieren) | Offen | — |
