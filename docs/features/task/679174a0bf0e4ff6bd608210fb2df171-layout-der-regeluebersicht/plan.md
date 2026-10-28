# Umsetzungsplan: Layout der Regelübersicht (TV-Sender)

## Übersicht

Die Programmregeln-Übersicht im Tab „Regeln" von `TVSenderAdmin.razor` (`/admin/tv-sender`, `#tab-regeln`) wird von einer `<table>`-Darstellung in eine vertikale Aneinanderreihung von Informations-Panels umgewandelt — ein Panel je `ProgramRule`. Genres, Quellenfilter und Clip-Gruppen werden als Anzahlen zusammengefasst, die Aktionen werden Symbolbuttons, `IsActive` und `Priority` werden Badges (Priorität entfällt bei 0). Datenmodell, Services und Berechtigungsprüfung bleiben unverändert — reine UI-Änderung an `VideoWebPlayer/Components/Pages/Admin/TVSenderAdmin.razor` und `VideoWebPlayer/wwwroot/app.css`.

## Designentscheidungen

| Komponente / Bereich | Gewählter Ansatz | Begründung |
|----------------------|-----------------|------------|
| „Detailansicht" für Genre-Details | Der vorhandene Regel-Editor-Dialog `#rule-editor` (über `StartEdit` / Bearbeiten-Symbolbutton) gilt als Detailansicht | Er zeigt bereits die vollständige Genre-/Quellen-/Clip-Gruppen-Auswahl als Checkbox-Listen; ein separater schreibgeschützter Dialog wäre redundanter Neubau. Entspricht der Annahme in der Anforderung. |
| Bedienung der Panels | Nur die drei Symbolbuttons sind klickbar; das Panel selbst ist nicht klickbar | Folgt dem vorhandenen Muster `.backup-list-item` (`Backups.razor`), bei dem ebenfalls nur Icon-Buttons Aktionen auslösen; vermeidet Fehlklicks beim Text-Markieren. |
| Panel-CSS | Neue kleine `.rule-panel`-Familie in `app.css` (`.rule-panel`, `.rule-panel-head`, `.rule-panel-badges`, `.rule-panel-name`, `.rule-panel-meta`, `.rule-panel-actions`), gestylt nach `.program-card`/`.backup-list-item`; der vertikale Stack nutzt die vorhandene `.program-grid`-Klasse | `.program-card` ist ein horizontales Poster-Layout und passt nicht ohne Umbau; `.backup-list-item` ist auf ein Leading-Icon ausgelegt. Eine eigene Familie hält die Semantik sauber und kostet nur ~30 Zeilen CSS — alle Tokens (`--admin-*`, `--vp-radius`) sind vorhanden. |
| Markup-Ort | Panel-Markup bleibt inline in `TVSenderAdmin.razor` (kein Auslagern in eine Kind-Komponente) | Der Block ist klein (~50 Zeilen Markup) und braucht sechs private Member (`DiagnosisLabel`, `MaterialSummaryLabel`, `StartEdit`, `ToggleActiveAsync`, `AskDelete`, `ruleDiagnoses`); eine Kind-Komponente erforderte Parameter-/Callback-Plumbing ohne fachlichen Gewinn. `TVSenderAdminClipGroups` ist nur Präzedenz, keine Pflicht. |
| Test-Hooks | Bestehende Hooks `.tvsender-rule-row`, `.tvsender-rule-edit`, `.tvsender-rule-toggle`, `.tvsender-rule-delete`, `.tvsender-rule-diagnosis` sowie `data-rule-id` werden auf den neuen Panel-Elementen beibehalten | Minimiert Test-Anpassungen: Nur Tests, die die Listenform des Material-Texts in der Übersicht prüfen, müssen geändert werden; Selektoren bleiben stabil. |
| Material-Komprimierung | Neues `MaterialSummaryLabel(ProgramRule)`: bei `ProgramRuleMaterialType.Genre` Anzahlen statt Namen (`Genre: {n} Genres`, `— {m} Quelle(n)`, `— {k} Clip-Gruppe(n)`); bei allen anderen Materialarten die vorhandene Kurzform `MaterialLabel(type, id)` plus Clip-Gruppen-Anzahl | Die Anforderung nennt ausdrücklich die Genre-Liste als Problem; die Annahme „Quellenfilter und Clip-Gruppen als Anzahl komprimieren" wird übernommen. Bei Nicht-Genre-Material tritt nur ein Name auf, die Kurzform bleibt. `MaterialLabel` bleibt für den Editor-Chip `MaterialDisplayName` erhalten. |
| Planbar-Diagnose | Volltext von `DiagnosisLabel` bleibt in einer eigenen Meta-Zeile des Panels | Panels haben keine feste Zeilenhöhe — der lange Text stört das Layout nicht, und die Information (warum nicht planbar) bleibt ohne Klick verfügbar. Bestehende Tests auf `.tvsender-rule-diagnosis` („ohne Mediendatei", „altersgerecht") laufen unverändert weiter. |
| Aktivieren/Deaktivieren-Icon | Zwei Zustände: Pause-Symbol bei `IsActive` (`title`/`aria-label` „Deaktivieren"), Play-Symbol bei inaktiv (`title`/`aria-label` „Aktivieren") | Symbol spiegelt die ausgelöste Aktion wider (wie bisher der Button-Text); Inline-SVG im bestehenden Stil (18 px, `stroke="currentColor"`, `aria-hidden="true"`). |
| Lange Material-/Regelnamen | Einzeilige Ellipse auf `.rule-panel-name` und der Material-Meta-Zeile (`text-overflow: ellipsis`) | Sichert die Kompaktheit auch bei langen Serientiteln; entspricht `.program-title` (einzeilig mit Ellipse). |

## Programmabläufe

### Rendern der Regel-Übersicht (geändert)

1. `OnInitializedAsync` → `ReloadRulesAsync` lädt `rules` über `ProgramRuleService.GetRulesAsync` (eager inkl. `Genres`, `MediaSourceFilters`, `ClipGroups`) und `ReloadDiagnosesAsync` füllt `ruleDiagnoses` — **unverändert**.
2. Markup: Bei `rules.Count == 0` bleibt `.admin-empty-state`; sonst rendert ein Container `div.program-grid` (statt `.admin-table-wrap > table`) eine `@foreach`-Schleife über `rules`.
3. Je Regel wird ein `div.rule-panel.tvsender-rule-row` mit `data-rule-id="@rule.Id"` gerendert:
   - `.rule-panel-head`: `.rule-panel-badges` mit Status-Chip (`admin-chip admin-chip-success` „Aktiv" bzw. `admin-chip-warning` „Inaktiv") **vor** dem Namen sowie — nur wenn `rule.Priority != 0` — einem `admin-chip` „Priorität {n}"; `.rule-panel-name` mit `@rule.Name`; `.rule-panel-actions` rechts mit drei Symbolbuttons.
   - `.rule-panel-meta`: Zeile mit `RuleTypeLabel(rule.RuleType)`, `RecurrenceLabel(rule)`, `rule.TimeOfDay.ToString("HH:mm")` und `DurationLabel(rule)` (mit Trennzeichen); Zeile mit `MaterialSummaryLabel(rule)` (einzeilig, Ellipse); Zeile `.tvsender-rule-diagnosis` mit `DiagnosisLabel(rule)`.
4. Die Symbolbuttons rufen die unveränderten Handler auf: `StartEdit(rule)` (`.tvsender-rule-edit`, Stift-Symbol, „Bearbeiten"), `ToggleActiveAsync(rule)` (`.tvsender-rule-toggle`, Pause-/Play-Symbol, „Deaktivieren"/„Aktivieren"), `AskDelete(rule)` (`.tvsender-rule-delete` + `btn-icon-danger`, Papierkorb-Symbol, „Löschen").
5. Statuszeile `#tvsender-rules-status` unterhalb bleibt unverändert.

Beteiligte Klassen/Komponenten: `TVSenderAdmin.razor`, `ProgramRule`, `ProgramRuleDiagnosis`, `app.css`

### Material-Kurzlabel für die Übersicht (neu)

1. `MaterialSummaryLabel(ProgramRule rule)` wird beim Rendern der Meta-Zeile aufgerufen.
2. `MaterialType == Genre`: Anzahl `n` = `rule.Genres.Count`; Fallback bei `n == 0`: `DbContext.Genres`-Lookup über `rule.MaterialId` → `n = 1`, wenn gefunden; sonst Rückgabe `„Genre (nicht gefunden)"`. Ergebnis `„Genre: {n} Genres"`, optional `„ — {m} Quelle(n)"` mit `m = rule.MediaSourceFilters.Count` (nur wenn `m > 0`), optional `„ — {k} Clip-Gruppe(n)"` mit `k = rule.ClipGroups.Count` (nur wenn `k > 0`).
3. Andere Materialarten: `MaterialLabel(rule.MaterialType, rule.MaterialId)` (vorhandene Kurzform mit DB-Lookup) plus `„ — {k} Clip-Gruppe(n)"`, wenn `rule.ClipGroups.Count > 0`.
4. Die Listenform `MaterialLabel(ProgramRule)` bleibt bestehen und wird weiterhin nur von `StartEdit` für `RuleEditModel.MaterialDisplayName` verwendet.

Beteiligte Klassen/Komponenten: `TVSenderAdmin.razor` (`@code`), `ProgramRule`, `DbContext`

### Bearbeiten / (De)aktivieren / Löschen über Symbolbuttons (geänderte Aufruf-Stelle)

1. Klick auf `.tvsender-rule-edit` → `StartEdit(rule)` → `#rule-editor`-Dialog (unverändert).
2. Klick auf `.tvsender-rule-toggle` → `ToggleActiveAsync(rule)` → `SetRuleActiveAsync` + Reload (unverändert); danach rendert das Panel mit umgekehrtem Chip/Icon.
3. Klick auf `.tvsender-rule-delete` → `AskDelete(rule)` → `#rule-delete-confirm`-Dialog (unverändert).

Beteiligte Klassen/Komponenten: `TVSenderAdmin.razor`, `IProgramRuleService`

## Neue Klassen

Keine neuen C#-Klassen. Neu sind nur CSS-Klassen und eine private Hilfsmethode:

| Klasse | Typ | Zweck |
|--------|-----|-------|
| `.rule-panel` | CSS-Klasse (`app.css`) | Panel-Container je Regel: Oberfläche `--admin-surface-low`, Rahmen `--admin-border`, Radius, Padding, vertikaler Flex — Vorbild `.program-card`/`.backup-list-item` |
| `.rule-panel-head` | CSS-Klasse | Flex-Kopfzeile: Badges + Name links, Aktionen rechts (`align-items: center`, `gap`) |
| `.rule-panel-badges` | CSS-Klasse | Badge-Zeile im Kopf (flex, wrap) — Status-Chip und Prioritäts-Chip **vor** dem Namen |
| `.rule-panel-name` | CSS-Klasse | Regelname, halbfett, `min-width:0`, einzeilig mit Ellipse — Vorbild `.program-title` |
| `.rule-panel-meta` | CSS-Klasse | Meta-Zeilen unter dem Kopf: gedämpft (`--admin-muted`), kleinere Schrift, Umbruch erlaubt; Material-Zeile einzeilig mit Ellipse |
| `.rule-panel-actions` | CSS-Klasse | Rechtsbündige Symbolbutton-Gruppe (`margin-left:auto`, `gap`) — Vorbild `.backup-list-actions` |
| `MaterialSummaryLabel` | private Methode in `TVSenderAdmin.razor` | Kompaktes Material-Label mit Anzahlen statt Namenslisten (siehe Programmabläufe) |

## Änderungen an bestehenden Klassen

### `TVSenderAdmin` (Razor-Komponente, `VideoWebPlayer/Components/Pages/Admin/TVSenderAdmin.razor`)

- **Neue Methoden:** `MaterialSummaryLabel(ProgramRule rule)` — `private string`; kompakte Material-Darstellung mit Anzahlen für Genre/Quellen/Clip-Gruppen (Details siehe Programmabläufe).
- **Geänderte Methoden:** keine — alle Event-Handler (`StartCreate`, `StartEdit`, `ToggleActiveAsync`, `AskDelete`, `DeleteRuleAsync`, `SaveRuleAsync`, `CancelEdit`) und Label-Methoden (`MaterialLabel`, `ClipGroupSuffix`, `RuleTypeLabel`, `RecurrenceLabel`, `DurationLabel`, `DiagnosisLabel`, `WeekdayLabel`) bleiben unverändert.
- **Geändertes Markup:** Zeilen ~128–168 — `.admin-table-wrap` > `<table>` wird durch `.program-grid` mit `@foreach` über `.rule-panel.tvsender-rule-row`-Panels ersetzt. Status-Chip wandert vor den Namen in den Panel-Kopf; Priorität wird ein `admin-chip` „Priorität {n}", der nur bei `rule.Priority != 0` gerendert wird; die drei Text-Buttons werden Symbolbuttons (`btn btn-ghost btn-icon`, Löschen zusätzlich `btn-icon-danger`) mit Inline-SVG (`aria-hidden="true"`, 18×18, `stroke="currentColor"`), `title`- und `aria-label`-Attribut. Die Hooks `.tvsender-rule-row`, `data-rule-id`, `.tvsender-rule-edit`, `.tvsender-rule-toggle`, `.tvsender-rule-delete`, `.tvsender-rule-diagnosis` sowie die Texte „Aktiv"/„Inaktiv" bleiben erhalten.
- **Unverändertes Markup:** Header `.admin-card-header` mit Zähler-Chip und `#tvsender-new-rule`, Leerzustand `.admin-empty-state`, `#tvsender-rules-status`, Editor-Dialog `#rule-editor`, Lösch-Dialog `#rule-delete-confirm`.
- **Neue Events / Event-Handler:** keine.

### `app.css` (`VideoWebPlayer/wwwroot/app.css`)

- **Neue Stile:** `.rule-panel`-Familie (siehe „Neue Klassen"), im TV-Sender-Abschnitt bei den `.tvsender-*`-Regeln (~Zeilen 818–916) oder bei den Admin-Listen (~Zeilen 2462–2550) ergänzen. Bestehende Klassen werden nicht geändert; `.admin-table-wrap`/`.table` bleiben für andere Tabs erhalten.

## Datenbankmigrationen

Keine. Es gibt keine Datenmodell-Änderung; alle benötigten Navigationen (`Genres`, `MediaSourceFilters`, `ClipGroups`) werden bereits von `GetRulesAsync` eager geladen.

## Validierungsregeln

Keine. Es werden keine neuen oder geänderten Eingaben verarbeitet — reine Darstellungsänderung.

## Konfigurationsänderungen

Keine. Keine persistierte Einstellung, kein Feature-Flag.

## Seiteneffekte und Risiken

- **E2E-Selektor-Stabilität:** Da die Hooks bewusst beibehalten werden, laufen die meisten E2E-Klicks unverändert; Risiko besteht bei Playwright-Locators, die auf `<td>`/Tabellenstruktur oder sichtbarem Button-Text („Bearbeiten") filtern — diese müssen auf die neuen Elemente bzw. `aria-label`/CSS-Klassen umgestellt werden (siehe „Betroffene bestehende Tests").
- **Material-Text-Assertions:** Zwei Tests prüfen die Listenform (`„Genre: E2E Action, E2E Komödie"`, `„Clips: Maskottchen"`) im Übersichts-Markup — sie brechen bewusst und werden auf die Anzahl-Form umgestellt; die Listenform bleibt über den Editor-Chip `#rule-material-selected` prüfbar.
- **`MaterialLabel`-Doppelnutzung:** `MaterialLabel(rule)` wird weiterhin von `StartEdit` für `MaterialDisplayName` aufgerufen — es darf nicht komprimiert oder entfernt werden; die Trennung Übersicht (`MaterialSummaryLabel`) vs. Editor (`MaterialLabel`) ist einzuhalten.
- **`data-rule-id` an `.tvsender-rule-divider`:** Der Sendeplan-Tab nutzt dasselbe Attribut an `.tvsender-rule-divider` und bleibt unberührt; Panel-`data-rule-id` darf nicht mit dem Divider verwechselt werden (Scopes in Tests beachten).
- **Layout-Regressionen anderer Tabs:** `.program-grid` wird im Sendeplan wiederverwendet; die neuen `.rule-panel-*`-Klassen dürfen keine globalen `.program-*`- oder `.admin-*`-Stile überschreiben (eigene Selektoren, keine Änderung an bestehenden Regeln).

## Umsetzungsreihenfolge

1. **CSS: `.rule-panel`-Familie in `app.css` anlegen**
   - Voraussetzungen: Keine — alle CSS-Variablen (`--admin-surface-low`, `--admin-border`, `--admin-muted`, `--vp-radius`), `.program-grid`, `.admin-chip*`, `.btn-icon`/`.btn-icon-danger` existieren bereits.
   - Beschreibung: `.rule-panel`, `.rule-panel-head`, `.rule-panel-badges`, `.rule-panel-name`, `.rule-panel-meta`, `.rule-panel-actions` nach Vorbild `.program-card`/`.backup-list-item` definieren; Name und Material-Zeile einzeilig mit Ellipse; Meta-Zeilen gedämpft.

2. **Hilfsmethode `MaterialSummaryLabel` in `TVSenderAdmin.razor` ergänzen**
   - Voraussetzungen: Keine — `rule.Genres.Count`, `rule.MediaSourceFilters.Count`, `rule.ClipGroups.Count`, `MaterialLabel(type, id)` und `DbContext` sind vorhanden.
   - Beschreibung: `private string MaterialSummaryLabel(ProgramRule rule)` neben `MaterialLabel` (Z. ~2157) einfügen; Genre-Anzahl mit `MaterialId`-Fallback, Quellen- und Clip-Gruppen-Anzahlen als Suffix, Nicht-Genre-Arten über `MaterialLabel(type, id)`.

3. **Tabellen-Markup durch Panel-Markup ersetzen**
   - Voraussetzungen: Schritte 1 und 2 (`.rule-panel`-CSS und `MaterialSummaryLabel` müssen existieren).
   - Beschreibung: Zeilen ~128–168 ersetzen: `.program-grid`-Container mit `@foreach` über `rules`; Panel-Aufbau (Badges vor Name, Prioritäts-Chip nur bei `Priority != 0`, drei Symbolbuttons mit SVG/`title`/`aria-label`, Meta-Zeilen inkl. `.tvsender-rule-diagnosis`). Alle bisherigen Hooks und `data-rule-id` auf die neuen Elemente übertragen. Kein Code.

4. **bUnit-Tests anpassen und neue Rendering-Tests schreiben**
   - Voraussetzungen: Schritt 3; vorhandene Infrastruktur `TVSenderAdminTests.CreateContextAsync` und `ProgramScheduleTestDb` (beide bereits im Repo).
   - Beschreibung: `TVSenderAdminTests_ClipGroups` und `TVSenderAdminTests_AgeWindow` an das Panel-Markup anpassen; neue Tests für Genre-Anzahl, Prioritäts-Badge (entfällt bei 0), Badge-vor-Name-Reihenfolge, `aria-label`/`title` der Symbolbuttons (siehe Tests).

5. **Playwright-E2E-Tests anpassen**
   - Voraussetzungen: Schritt 3; `TVSenderAdminE2ETestBase` bereits vorhanden.
   - Beschreibung: `TVSenderAdminE2ETests` (Lebenszyklus + Genre-Regel), `TVSenderAdminClipGroupsE2ETests` (Clip-Gruppen-Assertion) und `TVSenderAdminE2ETests_AgeWindow` auf Panel-Markup bzw. Anzahl-Form umstellen.

6. **Hilfe-Dokumentation aktualisieren**
   - Voraussetzungen: Schritt 3.
   - Beschreibung: `docs/help/tv-sender.md` Z. ~33 — Formulierung „Programmregeln-Tabelle" an das Panel-Layout anpassen.

7. **Build und Testsuite verifizieren**
   - Voraussetzungen: Schritte 1–6.
   - Beschreibung: `dotnet build VideoPlayer.sln` in Debug **und** Release; volle `dotnet test VideoWebPlayer.Tests/VideoWebPlayer.Tests.csproj`; einmalige Playwright-Ausfälle einzeln wiederholen und im Bericht nennen (Definition of Done laut AGENTS.md).

## Tests

### Neue Tests

| Test / Hilfsmethode | Testklasse | Was wird geprüft / bereitgestellt? |
|--------------------|------------|-------------------------------------|
| `RulePanel_GenreRule_ShowsGenreCount` | `VideoWebPlayer.Tests/Components/TVSenderAdminTests.cs` | Genre-Regel mit ≥2 Genres über `ProgramScheduleTestDb.CreateRuleAsync(genreIds: …)` seeden; `.tvsender-rule-row` enthält „Genre: 2 Genres" und **nicht** die Genre-Namen |
| `RulePanel_GenreRule_ShowsSourceAndClipGroupCounts` | `TVSenderAdminTests` | Genre-Regel mit Quellfilter + Clip-Gruppe: Meta-Zeile enthält „1 Quelle(n)"/„1 Clip-Gruppe(n)" (Anzahl-Form) statt Namen |
| `RulePanel_PriorityBadge_HiddenWhenZero_ShownWhenSet` | `TVSenderAdminTests` | Regel mit `priority: 0` → kein Prioritäts-Chip; Regel mit `priority > 0` → `admin-chip` mit „Priorität {n}" |
| `RulePanel_StatusBadge_BeforeName` | `TVSenderAdminTests` | Im gerenderten Markup steht der Status-Chip („Aktiv"/„Inaktiv") innerhalb von `.rule-panel-badges` vor `.rule-panel-name` (Reihenfolge im Markup prüfen) |
| `RulePanel_IconButtons_HaveAriaLabels` | `TVSenderAdminTests` | `.tvsender-rule-edit`/`-toggle`/`-delete` sind `btn-icon`-Buttons mit nicht-leerem `aria-label` und `title`; Toggle-Label wechselt je `IsActive` („Deaktivieren"/„Aktivieren") |
| `RulePanel_RendersDataRuleId` | `TVSenderAdminTests` | `.tvsender-rule-row` trägt `data-rule-id` mit der Regel-`Id` |

### Betroffene bestehende Tests

| Test / Testklasse | Grund der Anpassung |
|-------------------|---------------------|
| `TVSenderAdminTests_ClipGroups.RuleEditor_ShowsClipGroupSelection_GraceField_AndMaterialLabelSuffix` (`VideoWebPlayer.Tests/Components/TVSenderAdminTests_ClipGroups.cs` Z. ~167–181) | Prüft `„Clips: Maskottchen"` im Seiten-Markup — in der Übersicht erscheint nur noch die Anzahl („… Clip-Gruppe(n)"); Assertion auf Anzahl-Form umstellen und/oder die Listenform über den geöffneten Editor (`#rule-material-selected` / `MaterialDisplayName`) prüfen |
| `TVSenderAdminTests_AgeWindow.RuleDiagnosis_ShowsAgeWindowReason` (`VideoWebPlayer.Tests/Components/TVSenderAdminTests_AgeWindow.cs` Z. ~99–120) | Selektor `.tvsender-rule-diagnosis` bleibt, liegt aber nicht mehr in einer `<td>` — ggf. `FindAll`/Scope anpassen; Text-Assertion unverändert |

### E2E-Tests (Pflicht)

| Szenario | Testdatei / Testklasse | Abgedecktes Akzeptanzkriterium |
|----------|------------------------|-------------------------------|
| Regel-Lebenszyklus über Panels: anlegen → Panel mit Name/Regelart/kompaktem Material → Bearbeiten-Symbolbutton → Toggle-Symbolbutton → „Inaktiv"-Chip → Löschen-Symbolbutton → Panel weg + Statuszeile | `VideoWebPlayer.Tests/TVSenderAdminE2ETests.cs` — `Admin_CreateEditDeactivateDeleteRule` (angepasst) | Übersicht als Panels, Symbolbuttons bedienbar, Status-Badge, Löschfluss |
| Genre-Regel zeigt Anzahl statt Namensliste | `TVSenderAdminE2ETests.cs` — `Admin_GenreRule_MultiSelectAndSourceFilter` (angepasst) | „Genre: 2 Genres" statt „Genre: E2E Action, E2E Komödie"; Quellen-Anzahl statt Quellname(n); Details weiterhin im Editor sichtbar |
| Clip-Gruppen-Zuordnung erscheint als Anzahl im Panel | `VideoWebPlayer.Tests/TVSenderAdminClipGroupsE2ETests.cs` — `RuleEditor_ShowsClipGroupSelectionAndGraceField` (angepasst) | „… Clip-Gruppe(n)" statt „Clips: Maskottchen" in `.tvsender-rule-row` |
| Planbar-Diagnose im Panel sichtbar | `VideoWebPlayer.Tests/TVSenderAdminE2ETests_AgeWindow.cs` — `Admin_RuleDiagnosis_ShowsAgeWindowReason` (angepasst) | `.tvsender-rule-diagnosis` im Panel enthält „altersgerecht"-Text |

Bestehende E2E-Tests, die angepasst werden müssen:

| Test / Testklasse | Grund der Anpassung |
|-------------------|---------------------|
| `Admin_CreateEditDeactivateDeleteRule` (`TVSenderAdminE2ETests` Z. ~84–280) | Nutzt `.tvsender-rule-row`, `-edit`, `-toggle`, `-delete` — Hooks bleiben, aber Locators, die auf Text-Buttons/Tabellenzellen filtern (z. B. `GetByRole`/Text „Bearbeiten"), müssen auf Symbolbuttons (`aria-label`/Klasse) umgestellt werden |
| `Admin_GenreRule_MultiSelectAndSourceFilter` (`TVSenderAdminE2ETests` Z. ~355–427) | Erwartet Listenform „Genre: E2E Action, E2E Komödie" + Quellname in der Zeile — auf Anzahl-Form umstellen |
| `RuleEditor_ShowsClipGroupSelectionAndGraceField` (`TVSenderAdminClipGroupsE2ETests` Z. ~162–220) | Erwartet „Clips: Maskottchen" in `.tvsender-rule-row` — auf Anzahl-Form umstellen |
| `Admin_RuleDiagnosis_ShowsAgeWindowReason` (`TVSenderAdminE2ETests_AgeWindow` Z. ~83–94) | `.tvsender-rule-diagnosis` bleibt als Hook erhalten — voraussichtlich minimal oder keine Anpassung nötig; prüfen, ob Locator auf `<td>` filtert |

## Offene Punkte

Keine. Alle sechs Rückfragen aus der Anforderung wurden anhand der Bestandsaufnahme und der genannten Annahmen als Designentscheidungen getroffen (Detailansicht = Editor-Dialog, nur Symbolbuttons klickbar, Material-Komprimierung als Anzahlen, Diagnose-Volltext, Pause-/Play-Icon, Ellipse für lange Namen).
