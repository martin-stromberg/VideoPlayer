# UI-Komponenten — Bestandsaufnahme

## `TVSenderAdmin` — Tab „Regeln"
Datei: `VideoWebPlayer/Components/Pages/Admin/TVSenderAdmin.razor` (Route `@page "/admin/tv-sender"`, `@rendermode InteractiveServer`)

### Regel-Übersicht (die zu ändernde Stelle)

Relevanter Markup-Bereich: **Zeilen 110–174** — `.admin-card.admin-card-wide` „Programmregeln"
mit Header (`.admin-card-header`, Zähler-Chip `@rules.Count Einträge`, Button
`#tvsender-new-rule`), Leerzustand `.admin-empty-state` (Z. 121–124) und darunter der
Tabellen-Block **Zeilen 128–168**:

```
.admin-table-wrap (Z. 128)
└─ <table class="table table-dark table-striped align-middle"> (Z. 129)
   ├─ <thead>: Name · Regelart · Wiederholung · Sendezeit · Umfang · Material · Planbar · Priorität · Status · Aktionen (Z. 130–143)
   └─ <tbody> @foreach (var rule in rules) (Z. 144–166)
      └─ <tr class="tvsender-rule-row" data-rule-id="@rule.Id"> (Z. 147)
```

| Element | CSS-Klasse(n) / Hook | Inhalt | Zeile |
|---------|----------------------|--------|-------|
| `<td>` Name | — | `@rule.Name` | 148 |
| `<td>` Regelart | — | `@RuleTypeLabel(rule.RuleType)` | 149 |
| `<td>` Wiederholung | — | `@RecurrenceLabel(rule)` | 150 |
| `<td>` Sendezeit | — | `@rule.TimeOfDay.ToString("HH:mm")` | 151 |
| `<td>` Umfang | — | `@DurationLabel(rule)` | 152 |
| `<td>` Material | — | `@MaterialLabel(rule)` — volle Listenform inkl. Genres, „— Quellen: …", „— Clips: …" | 153 |
| `<td>` Planbar | `.tvsender-rule-diagnosis` | `@DiagnosisLabel(rule)` | 154 |
| `<td>` Priorität | — | `@rule.Priority` (nackte Zahl, auch „0") | 155 |
| `<td>` Status | `.admin-chip` + `admin-chip-success`/`admin-chip-warning` | „Aktiv" / „Inaktiv" | 156–158 |
| `<td>` Aktionen | `.actions-cell` | drei Text-Buttons | 159–163 |
| Button Bearbeiten | `.btn.btn-sm.btn-primary.tvsender-rule-edit` | `StartEdit(rule)` | 160 |
| Button (De)aktivieren | `.btn.btn-sm.btn-outline-light.tvsender-rule-toggle` | `ToggleActiveAsync(rule)`; Text „Deaktivieren"/„Aktivieren" je `rule.IsActive` | 161 |
| Button Löschen | `.btn.btn-sm.btn-danger.tvsender-rule-delete` | `AskDelete(rule)` | 162 |

Statuszeile unterhalb: `#tvsender-rules-status` mit `@rulesStatus` (Z. 170–173).

**Bisher keine Symbolbuttons im Regeln-Tab** — alle Aktionen sind Text-Buttons.

### Regel-Editor-Dialog (unverändert)

Zeilen 176–388: `.admin-dialog-overlay` > `.admin-dialog.rule-editor-dialog` `#rule-editor`
(Formular: Name `#rule-name`, Regelart `#rule-type`, Wiederholung `#rule-recurrence`,
Wochentag `#rule-weekday`, Datum `#rule-date`, Sendezeit `#rule-time`, Folgen `#rule-episodes`,
Länge `#rule-duration`, Priorität `#rule-priority`, `IsActive`-Checkbox `.admin-toggle-row`,
Material-Chip `#rule-material-selected` mit `MaterialDisplayName`, Picker
`#rule-material-picker` mit `MediaSearchSelector` / `#rule-material-playlist` /
`#rule-material-trailer` / `#rule-material-clipsource`, Genre- + Quellen-Mehrfachauswahl
`.rule-multi-select` `#rule-material-genre`/`#rule-material-sources`, Clip-Gruppen
`#rule-clip-groups` + Karenzzeit `#rule-grace-minutes`, Fehler `#rule-edit-error`,
Buttons `#rule-save` + Abbrechen). Handler siehe [logic.md](logic.md).

### Lösch-Bestätigungsdialog (unverändert)

Zeilen 958–972: `.admin-dialog-overlay` > `.admin-dialog` — bestätigt über `#rule-delete-confirm`
(`DeleteRuleAsync`), Abbruch setzt `deleteCandidate = null`.

### Gestaltungsvorlage in derselben Datei: Sendeplan-Karten

Zeilen 417–512 (`#tvsender-schedule-cards` `.program-grid`): vertikal gestapelte
`.program-card`-Panels je Sendeplatz mit `.program-poster`, `.program-body`
(`.program-badges` mit `.program-badge.badge-live`/`.badge-genre` und
`.admin-chip`-Status, `.program-time`, `.program-title`, `.program-sub`,
`.program-desc`, `.progress-track`/`.progress-fill`). Tages-Header
`.tvsender-day-header` (Z. 426) und Regel-Trennlinien `.tvsender-rule-divider` (Z. 436,
ebenfalls mit `data-rule-id`).

Symbolbutton-Vorbild in derselben Datei — Zeile 458: der Clip-Block-Details-Button

```
<button class="btn btn-ghost btn-icon tvsender-clip-block-toggle"
        title="Platzhalter-Clips anzeigen" aria-label="Platzhalter-Clips anzeigen" …>
    <svg aria-hidden="true" width="18" height="18" viewBox="0 0 24 24" fill="none"
         stroke="currentColor" stroke-width="2" stroke-linecap="round"
         stroke-linejoin="round">…</svg>
</button>
```

## `TVSenderAdminClipGroups` (Kind-Komponente)
Datei: `VideoWebPlayer/Components/Pages/Admin/TVSenderAdminClipGroups.razor`

Präzedenzfall für Auslagerung eines Seiten-Teils: eingebunden unter `#tab-clipgruppen`
(`TVSenderAdmin.razor` Z. 905–906), Parameter `UserId`, `Groups`, `Counts`, `MediaSources`,
`OnGroupsChanged` (`EventCallback`). Rendert selbst noch eine Tabelle
(`.admin-table-wrap` > `.table`, Zeilen `.clipgroup-row` mit `data-group-id`,
`.actions-cell`, Z. 23–47).

## `Backups` — Symbolbutton- und Listen-Vorbild
Datei: `VideoWebPlayer/Components/Pages/Admin/Backups.razor`

Zeilen 153–200: `.backup-list-card` > `<ul class="backup-list">` mit
`.backup-list-item`-Zeilen (`.backup-list-item-main`, `.backup-list-icon`,
`.backup-list-info`, `.backup-list-name`, `.backup-list-meta`, `.backup-list-status(-success|-error)`)
und rechts `.backup-list-actions` mit Symbolbuttons nach festem Muster (Z. 181–189):
`class="btn btn-ghost btn-icon"` bzw. zusätzlich `btn-icon-danger` beim Löschen,
jeweils `title`- + `aria-label`-Attribut und Inline-SVG (`aria-hidden="true"`,
18×18, `stroke="currentColor"`, `stroke-width="2"`, runde Caps/Joins).

## `LiveTv` — zweite Verwendung der `.program-card`-Familie
Datei: `VideoWebPlayer/Components/Pages/LiveTv.razor`

Nutzt `program-card compact` für „Jetzt"/„Gleich" (`#live-tv-now` Z. 94, `#live-tv-next`
Z. 124) und `livetv-upcoming-row program-card compact` für kommende Sendeplätze (Z. 143) —
die Karten-Familie ist damit bereits an zwei Stellen im Einsatz.

## Relevante CSS-Klassen in `VideoWebPlayer/wwwroot/app.css`

### Badges / Chips

| Klasse | Zeilen | Aussehen/Zweck |
|--------|--------|----------------|
| `.admin-chip` (zusammen mit `.badge`) | 489–501 | Pill-Badge: 999px-Radius, `inline-flex`, bläuliche Farbe (`--admin-primary`) |
| `.admin-chip-success` | 503–507 | Grün (`--admin-success`) — „Aktiv"/„Geplant"/„ermittelt" |
| `.admin-chip-warning` | 509–513 | Gelb (`--admin-warning`) — „Inaktiv"/„Verdrängt"/„ausstehend" |
| `.admin-chip-danger` | 2859–2863 | Rot (`--admin-danger`) — „fehlgeschlagen" |
| `.admin-chip-info` | 2865–2869 | Blau — weitere Chip-Variante |
| `.admin-chip-muted` | 2871–2875 | Gedämpft — weitere Chip-Variante |
| `.admin-chip-list` | 520–524 | Flex-Zeile für mehrere Chips |
| `.program-badge` | 722–730 | Kleineres Uppercase-Badge der Karten |
| `.badge-live` / `.badge-genre` | 732–740 | LIVE- bzw. Genre-Badge |
| `.chip-detail-btn` | 927–940 | Mini-Button „▾ Details" innerhalb eines Chips |

### Karten / Panels (Sendeplan-Vorlage)

| Klasse | Zeilen | Aussehen/Zweck |
|--------|--------|----------------|
| `.program-grid` | 630–634 | Vertikaler Flex-Stack, `gap: 10px` |
| `.program-card` (+ `:hover`) | 636–650 | Horizontale Karte: `display:flex`, `gap:14px`, `padding:12px`, `--vp-surface-low`, 12px-Radius, Hover-Hebung |
| `.program-poster` (+ `img`) | 652–673 | 92px breites 2:3-Poster links |
| `.program-body` | 675–681 | Vertikaler Inhaltsbereich, `min-width:0` + `flex:1` |
| `.program-badges` | 683–688 | Badge-Zeile (flex, wrap) |
| `.program-time` | 690–695 | Kleine halbfette Zeit, `tabular-nums` |
| `.program-title` | 697–703 | Titel einzeilig mit Ellipse |
| `.program-sub` | 705–708 | Gedämpfte Unterzeile |
| `.program-desc` | 710–720 | Beschreibung, 3-zeiliger Clamp |
| `.slot-now`, `.now-card` | 743–746 | Primärfarben-Rahmen (läuft) |
| `.progress-track` / `.progress-fill` | 748–760 | 3px-Fortschrittsbalken |
| `.program-card.compact` (+ Poster/Titel/Desc) | 763–781 | Kompaktvariante (Live-TV) |
| Admin-Overrides `.admin-console .program-*` | 784–814 | `--admin-*`-Oberflächen für Karten im Admin-Bereich |

### Symbolbuttons

| Klasse | Zeilen | Aussehen/Zweck |
|--------|--------|----------------|
| `.admin-console .btn-icon` | 2552–2563 | 2.25rem-Quadrat, zentriert, transparent, `--admin-muted` |
| `.admin-console .btn-icon:hover` | 2565–2569 | Hover: Textfarbe, `--admin-surface-high`, Rahmen |
| `.admin-console .btn-icon-danger:hover` | 2571–2575 | Hover in Danger-Farbe |
| `.admin-console .btn-icon:disabled` | 2577–2580 | 50 % Deckkraft |
| `.admin-console .btn-ghost`, `.admin-console .btn-outline-light` | 1250–1255 | Admin-Variante der Ghost-Buttons (`.btn-ghost` Basis Z. 119–126) |

### Listen / Struktur (allgemein wiederverwendbar)

| Klasse | Zeilen | Aussehen/Zweck |
|--------|--------|----------------|
| `.backup-list-card` / `.backup-list` / `.backup-list-item` (+ `-main`, `-icon`, `-info`, `-name`, `-meta`, `-status(-success|-error)`, `-actions`) | 2462–2550 | Vertikale Listen-Panels mit Icon links und Symbolbuttons rechts — alternativer Gestaltungs-Vorläufer neben `.program-card` |
| `.admin-card` / `.admin-card-wide` | 455–461 | Kartenrahmen der Admin-Bereiche |
| `.admin-card-header`, `.admin-actions`, `.admin-header-actions` | 463–475 | Flex-Kopfzeile (Titel links, Aktionen rechts) |
| `.admin-card-header-left` | 602–607 | Titel + Chip nebeneinander |
| `.admin-sub-card` | 610–615 | Unterkarte in Einstellungen |
| `.admin-empty-state` | 997–1012 | Gestrichelter Leerzustand |
| `.admin-table-wrap` + `.admin-console .table(-dark)` | 965–995 | Bisheriger Tabellen-Rahmen + Admin-Tabellenthema |
| `.actions-cell` | 617–623 | `white-space: nowrap` + Button-Abstand der bisherigen Aktionszelle |
| `.admin-tabs` / `.admin-tab(.active)` / `.tab-pane(.active)` | 544–586 | Tab-Leiste; Panes bleiben gerendert, werden per CSS ein-/ausgeblendet |
| `.admin-toggle-row` | 1223 | Checkbox-zeile („Regel ist aktiv") |
| `.rule-multi-select` | 527–541 | Scrollbare Checkbox-Listen des Editors (Genre/Quellen/Clip-Gruppen) |
| `.rule-editor-dialog` | 590–592 | Breitere Editor-Dialog-Variante (`min(960px,100%)`) |
| `.admin-dialog-overlay` / `.admin-dialog` | 1173–1221 | Overlay-Dialoge |
| `.tvsender-day-header` / `.tvsender-rule-divider` | 818–844 | Tages-Gliederung und Regel-Trennlinie im Sendeplan |
| `.program-card.tvsender-slot-displaced` | 847–855 | Durchgestrichene verdrängte Karte |
| `.tvsender-clip-block-toggle` | 858–860 | `margin-left:auto` — rechtsbündiger Icon-Button in `.program-badges` |
| `.tvsender-clip-block-list/-row/-head/-time/-duration/-title` | 863–916 | Detail-Liste des Clip-Block-Dialogs |

### CSS-Variablen (für Panels relevant)

Die Admin-Oberfläche nutzt die Tokens `--admin-surface`, `--admin-surface-low`,
`--admin-surface-high`, `--admin-border`, `--admin-text`, `--admin-muted`,
`--admin-primary`, `--admin-primary-strong`, `--admin-success`, `--admin-warning`,
`--admin-danger`, `--vp-radius`, `--vp-radius-sm` (im ganzen Stylesheet verwendet).

## Betroffene Hilfe-Dokumentation

- `docs/help/tv-sender.md` Zeile 33 — beschreibt den Tab „Regeln" aktuell als
  „**Programmregeln-Tabelle**; der Regel-Editor öffnet sich als Overlay-Panel über der
  Liste" (Formulierung betroffen).
- Präzedenzfall Prototyp: `docs/projects/task/issue-111-6145c51699f34953bf29a244634a23e2-automatisierter-tvsender/preview/sendeplan-vorschau.html`
  (freigegebener HTML-Entwurf der Sendeplan-Karten, auf den ein Kommentar in
  `TVSenderAdmin.razor` Z. 417–419 verweist).
