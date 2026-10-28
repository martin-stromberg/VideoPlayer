# Übersetzte Anforderung: Layout der Regelübersicht (TV-Sender)

## Fachliche Zusammenfassung

Die Programmregeln-Übersicht im Tab „Regeln" der Admin-Seite `TVSenderAdmin.razor` (`/admin/tv-sender`, `#tab-regeln`) wird von einer klassischen `<table>`-Darstellung in eine vertikale Aneinanderreihung von Informations-Panels (Karten) umgewandelt — ein Panel je `ProgramRule`. Motivation: Bei Regeln der Materialart `ProgramRuleMaterialType.Genre` listet `MaterialLabel(rule)` alle zugeordneten Genres (plus Quellenfilter und Clip-Gruppen-Suffix) vollständig auf; bei vielen Genres wird die Tabellenzeile unverhältnismäßig hoch. In der Übersicht sollen nur oberflächliche Informationen stehen: Genres werden als Anzahl zusammengefasst (Details über die Detailansicht), Aktionen werden Symbolbuttons, `IsActive` wird ein Badge vor dem Namen, `Priority` ein Badge, der bei Wert 0 entfällt. Datenmodell, Services und Berechtigungsprüfung bleiben unverändert — reine UI-Änderung.

## Betroffene Klassen und Komponenten

### Zu ändernde Artefakte

- `VideoWebPlayer/Components/Pages/Admin/TVSenderAdmin.razor` — Markup des Regel-Blocks (aktuell Zeilen ~128–168: `.admin-table-wrap` > `<table class="table table-dark table-striped">` mit Zeilen `.tvsender-rule-row` und den Spalten Name, Regelart, Wiederholung, Sendezeit, Umfang, Material, Planbar, Priorität, Status, Aktionen). Der Regel-Editor-Dialog (`#rule-editor`, `editRule`, `RuleEditModel`) und der Lösch-Dialog (`#rule-delete-confirm`, `deleteCandidate`) bleiben unverändert.
- `VideoWebPlayer/wwwroot/app.css` — neue Panel-Stile (bzw. Wiederverwendung/Erweiterung der vorhandenen `.program-card`-/`.program-badges`-Familie aus dem Sendeplan-Tab, definiert ca. Zeilen 627–790) sowie Verwendung der bereits vorhandenen Badge-/Chip-Klassen `.admin-chip`, `.admin-chip-success`, `.admin-chip-warning` (Zeilen ~489–520) und der Symbolbutton-Klassen `.btn-icon`, `.btn-icon-danger` (Zeilen ~2552–2580).

### Neu zu erstellende Artefakte (Annahme / optional)

- Ggf. eine neue private Hilfsmethode in `TVSenderAdmin.razor`, z. B. `MaterialSummaryLabel(ProgramRule)`, die für `ProgramRuleMaterialType.Genre` nur die Anzahl ausgibt (`rule.Genres.Count` bzw. Fallback über `MaterialId`), statt die Namen zu listen. Das vorhandene `MaterialLabel(ProgramRule)` (Zeilen ~2157–2171) bleibt für den Editor-Chip `MaterialDisplayName` (Zeile ~1909) erhalten.
- Optional: Auslagerung des Panels in eine eigene Razor-Komponente (z. B. `TVSenderAdminRulePanel.razor`) — Präzedenzfall `TVSenderAdminClipGroups.razor`, das bereits als Kind-Komponente der Seite eingebunden ist.

### Nicht betroffen (keine Änderung)

- Datenmodell: `ProgramRule` (`VideoWebPlayer/Data/ProgramRule.cs`) inkl. `Genres`, `MediaSourceFilters`, `ClipGroups`, `Priority`, `IsActive` — keine neue Spalte, keine EF-Migration.
- Services/Interfaces: `IProgramRuleService`, `ProgramRuleService`, `IProgramScheduleService`, `ProgramRuleDiagnosis` (`GetRuleDiagnosesAsync`, befüllt `ruleDiagnoses`) — unverändert.
- Enums: `ProgramRuleType`, `ProgramRuleRecurrence`, `ProgramRuleMaterialType` — unverändert.
- Event-Handler `StartEdit`, `ToggleActiveAsync`, `AskDelete`, `StartCreate` — Logik unverändert, nur andere Aufruf-Stellen im Markup.

### Betroffene Tests

- `VideoWebPlayer.Tests/TVSenderAdminE2ETests.cs` (Playwright) — verwendet die Selektoren `.tvsender-rule-row`, `.tvsender-rule-edit`, `.tvsender-rule-toggle`, `.tvsender-rule-delete`, `.tvsender-rule-diagnosis` und prüft u. a. den Text „Inaktiv" und `data-rule-id`; muss an das Panel-Markup angepasst werden (oder die Hooks werden bewusst beibehalten).
- `VideoWebPlayer.Tests/Components/TVSenderAdminTests_AgeWindow.cs` (bUnit) — prüft `.tvsender-rule-diagnosis`.
- `VideoWebPlayer.Tests/Components/TVSenderAdminTests_ClipGroups.cs` (bUnit) — dokumentiert das `MaterialLabel`-Suffix-Verhalten in der Regel-Tabelle.
- Neue Tests (bUnit, ggf. E2E): Genre-Anzahl statt Namensliste, Prioritäts-Badge ausgeblendet bei `Priority == 0`, Status-Badge vor dem Namen, Symbolbuttons mit `aria-label`/`title`.

### Betroffene Dokumentation

- `docs/help/tv-sender.md` — beschreibt den Tab „Regeln" aktuell als „Programmregeln-Tabelle" (Zeile ~33); Formulierung an das Panel-Layout anpassen.
- Hinweis: Die im Workflow genannte `docs/features.md` existiert nicht; Feature-Doku liegt unter `docs/features/task/`.

## Implementierungsansatz

- Ersetzen des `<table>`-Blocks in `TVSenderAdmin.razor` durch eine `@foreach`-Schleife über `rules`, die je Regel ein vertikal gestapeltes Panel rendert. `data-rule-id="@rule.Id"` und die Status-Texte („Aktiv"/„Inaktiv") beibehalten; Test-Hooks (`.tvsender-rule-row` o. ä.) entweder auf den Panels weiterführen oder Tests gezielt auf neue Klassen (z. B. `.tvsender-rule-panel`) umstellen.
- Panel-Aufbau (Annahme, aus der Anforderung ableitbar): Kopfzeile mit Status-Badge (`admin-chip admin-chip-success`/`-warning`) **vor** dem Regelnamen und den Symbolbuttons rechts; darunter Meta-Zeile(n) mit `RuleTypeLabel`, `RecurrenceLabel`, `TimeOfDay`, `DurationLabel`, kompaktem Material-Label und der Planbar-Diagnose (`DiagnosisLabel` aus `ruleDiagnoses`).
- Priorität: `admin-chip` (z. B. „Priorität N" bzw. „Prio N") nur rendern, wenn `rule.Priority != 0`.
- Symbolbuttons nach bestehendem Muster: `class="btn btn-ghost btn-icon …"` mit Inline-SVG (`aria-hidden="true"`, ~18 px, `stroke="currentColor"`), `title`- und `aria-label`-Attribut; Löschen zusätzlich `.btn-icon-danger`. Vorbilder: `Backups.razor` (Zeilen ~181–187), `tvsender-clip-block-toggle` in `TVSenderAdmin.razor` (Zeile ~458). Für Aktivieren/Deaktivieren zwei Zustände (Icon und `title`/`aria-label` abhängig von `rule.IsActive`).
- Kompakte Material-Darstellung: neues Label, das bei `ProgramRuleMaterialType.Genre` die Anzahl der Genres zeigt (z. B. „Genre: 5 Genres"); alle anderen Materialarten können die bisherige Kurzform behalten, da dort nur ein Name auftritt. Annahme: Quellenfilter (`MediaSourceFilters`) und Clip-Gruppen-Suffix (`ClipGroupSuffix`) werden analog als Anzahl komprimiert — zu klären.
- Klassennamen und Layout der Sendeplan-Karten (`.program-card`, `.program-body`, `.program-badges`) können als Gestaltungsvorlage dienen; ob sie direkt wiederverwendet oder eine eigene `.rule-panel`-Familie angelegt wird, ist eine Design-Entscheidung der Umsetzung.
- Optionaler Prozessschritt mit Präzedenz: Für die Sendeplan-Umstellung wurde zuvor ein freigegebener HTML-Prototyp erstellt (`docs/projects/task/issue-111-…-automatisierter-tvsender/preview/sendeplan-vorschau.html`); ein solcher Vorschau-Prototyp wäre auch hier möglich.

## Konfiguration

Keine Konfiguration erforderlich. Die Umstellung ist eine reine Darstellungsänderung ohne persistierte Einstellung, ohne Feature-Flag und ohne Datenmodell-Änderung.

## Offene Fragen

1. **„Detailansicht":** Ist mit „Detailansicht aufrufen" der vorhandene Regel-Editor-Dialog (`#rule-editor`) gemeint, der die volle Genre-/Quellen-Auswahl als Checkbox-Liste zeigt (Annahme: ja), oder soll ein separates, schreibgeschütztes Detail-Panel/Dialog entstehen?
2. **Bedienung des Panels:** Soll das ganze Panel klickbar sein (öffnet Detail/Bearbeiten) oder nur die Symbolbuttons?
3. **Umfang der Material-Komprimierung:** Sollen neben der Genre-Liste auch die Quellenfilter („Quellen: …") und das Clip-Gruppen-Suffix („Clips: …") in der Übersicht auf Anzahlen reduziert oder ganz weggelassen werden (Annahme: als Anzahl komprimieren)?
4. **Planbar-Diagnose:** Die Spalte „Planbar" (`DiagnosisLabel`, `.tvsender-rule-diagnosis`) kann ebenfalls langen Text enthalten (z. B. „Nicht planbar: 3 ohne Mediendatei, 2 ohne Laufzeit …"). Soll sie im Panel vollständig, gekürzt oder nur als kompakter Hinweis (z. B. „N planbar" + Warnung) erscheinen?
5. **Symbol für Aktivieren/Deaktivieren:** Gewünschte Ikonografie nicht spezifiziert — Vorschlag: Pause-/Play- oder Power-Symbol im bestehenden SVG-Stil; Rückfrage, falls ein anderes Symbol gewünscht ist.
6. **Nicht-Genre-Material:** Bei Regeln mit sehr langem Material-Namen (z. B. langer Serientitel) — soll im Panel eine einzeilige Ellipse gelten, um die Kompaktheit zu sichern?
