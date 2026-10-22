# UI

## `TVSenderAdmin.razor` — aktuelle Struktur (zeilenweise)

| Zeilen | Inhalt | Stabile Selektoren |
|--------|--------|---------------------|
| 22–25 | `!isAdmin` → „Nicht autorisiert." | `.alert-danger` |
| 28–36 | `admin-console admin-tvsender-page` + `admin-page-header` (Kicker „Einrichtung", h1, Zurück-Link) | `admin-tvsender-page` hat keine CSS-Regel |
| 38–85 | **Sender-Karte**: Name, Bundesland, Horizont, Intervall, Segmentlänge, Segment-Verzeichnis, Aufbewahrung + Speichern | `#tvsender-name`, `#tvsender-federal-state`, `#tvsender-horizon-hours`, `#tvsender-interval-minutes`, `#tvsender-segment-length-seconds`, `#tvsender-segment-directory`, `#tvsender-segment-retention-minutes`, `#tvsender-save-settings`, `#tvsender-channel-status` |
| 87–149 | **Programmregeln-Karte**: Tabelle (Name/Regelart/Wiederholung/Sendezeit/Umfang/Material/Planbar/Priorität/Status/Aktionen) | `#tvsender-new-rule`, `.tvsender-rule-row`, `.tvsender-rule-diagnosis`, `.tvsender-rule-edit`, `.tvsender-rule-toggle`, `.tvsender-rule-delete`, `#tvsender-rules-status` |
| 151–199 | **Sendeplan-Karte**: Tabelle (Start/Ende/Inhalt/Regel/Status) | `#tvsender-generate-now`, `#tvsender-schedule-table`, `.tvsender-slot-row`, `.tvsender-slot-displaced` (CSS fehlt!), `#tvsender-schedule-status` |
| 201–270 | **Fortschrittskarte**: Chips ermittelt/ausstehend/fehlgeschlagen, Progress-Bar, letzte Aktivität/Rate, `RecentItems`-Tabelle | `#tvsender-probe-card`, `#tvsender-probe-empty`, `#tvsender-probe-status`, `#tvsender-probe-counts`, `#tvsender-probe-probed`, `#tvsender-probe-pending`, `#tvsender-probe-failed`, `#tvsender-probe-last-activity`, `#tvsender-probe-rate`, `#tvsender-probe-recent`, `.tvsender-probe-row` |
| 272–425 | **Regel-Editor** als `admin-dialog-overlay`/`admin-dialog` bei `editRule != null` | `#rule-name`, `#rule-type`, `#rule-recurrence`, `#rule-weekday`, `#rule-date`, `#rule-time`, `#rule-episodes`, `#rule-duration`, `#rule-priority`, `#rule-material-selected`, `#rule-material-picker`, `#rule-material-playlist`, `#rule-genre-editor`, `#rule-material-genre`, `#rule-genre-{id}`, `#rule-material-sources`, `#rule-source-{id}`, `#rule-edit-error`, `#rule-save` |
| 427–441 | **Lösch-Bestätigung** als Overlay | `#rule-delete-confirm` |

## `app.css` — relevante vorhandene Klassen

`.admin-console` (definiert `--admin-*`-Palette, Z. 319–333), `.admin-page-header`,
`.admin-kicker`, `.admin-settings-grid`, `.admin-card` + `.admin-card-wide`,
`.admin-card-header`, `.admin-actions`, `.admin-chip` (+`-success`/`-warning`/
`-danger`/`-info`/`-muted`), `.admin-chip-list`, `.rule-multi-select`,
`.admin-table-wrap`, `.admin-console .table(-dark)`, `.admin-empty-state`,
`.admin-section`, `.admin-dialog-overlay`, `.admin-dialog`, `.admin-toggle-row`,
`.alert-info`, `.admin-console .form-control/.form-select/.btn-*`.

## Mockup-Klassen ohne Gegenstück in `app.css`

`.admin-tabs`, `.admin-tab` (+`.active`), `.tab-pane` (+`.active`),
`.rule-editor-card`, `.probe-counts`, `.chip-detail-btn`, `.probe-detail`,
`.table-wrap-inner`, `.admin-header-side`, `.admin-sub-card`,
`.admin-card-header-left`, `.actions-cell`, `.row-displaced`, `.mt-2`/`.mb-2`
(kommt aus Bootstrap), `.progress`/`.progress-bar` (Bootstrap), `.form-check`
(Bootstrap), `.alert-info` (existiert), `.admin-toggle-row` (existiert).

## Vergleichbare Tab-Muster im Projekt

Keine bestehende Tab-Navigation in den Admin-Seiten gefunden — `GenreAdmin`,
`Backups`, `Updates` sind ebenfalls lineare Karten-Seiten. Das Mockup führt das
Muster neu ein (`<nav role="tablist">` + Buttons mit `role="tab"`,
`aria-selected`; Panes mit `.tab-pane`/`.active`).

## Abgenommenes Mockup

`docs/projects/task/issue-111-6145c51699f34953bf29a244634a23e2-automatisierter-tvsender/preview/admin-ux-vorschau.html`:
vier Tabs (Regeln aktiv), Regel-Editor als Card mit „Schließen"-Button,
Kopfzeile mit Status-Chips (`Sender: …`, `Plan alle 15 Min aktiv`), Sendeplan mit
„Jetzt generieren" im Kartenkopf, Einstellungen in drei `admin-sub-card`-Gruppen
(Sender / Planung / Segmentierung), Laufzeitermittlung mit
`.probe-counts`-Chips inkl. `▾ Details`-Buttons und zwei `<details class="probe-detail">`-Tabellen
(Spalten: Name, Quelle, Versuche, Letzter Versuch) plus „Letzte Ermittlungen".
