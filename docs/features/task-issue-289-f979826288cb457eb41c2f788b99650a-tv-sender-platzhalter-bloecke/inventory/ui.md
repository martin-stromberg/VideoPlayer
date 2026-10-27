# Bestandsaufnahme: UI

## `TVSenderAdmin.razor` — Tab „Sendeplan"

Datei: `VideoWebPlayer/Components/Pages/Admin/TVSenderAdmin.razor` (2064 Zeilen),
Route `/admin/tv-sender`, `@rendermode InteractiveServer`, nur für `IsAdmin`.

### Tab-Gerüst

Fünf Tabs (Z. 59–75): `Regeln`, `Sendeplan`, `Einstellungen`, `Laufzeitermittlung`,
`Clip-Gruppen` (`#tvsender-tab-{regeln,sendeplan,einstellungen,laufzeit,clipgruppen}`,
`role="tablist"`, `aria-selected`, `aria-controls`). `SelectTab` schaltet nur die
`active`-Klasse — **alle Panes bleiben im DOM** (CSS-hidden), kein Nachladen beim Wechsel.
Der Clip-Gruppen-Tab rendert die Kind-Komponente `TVSenderAdminClipGroups.razor`.

### Sendeplan-Render-Loop (Z. 392–483)

- Kartenkopf: `@schedule.Count Einträge`-Chip, Buttons `#tvsender-generate-now`,
  `#tvsender-cleanup-displaced`, `#tvsender-reset-schedule`.
- Leerstand: `admin-empty-state`; Statuszeile `#tvsender-schedule-status`.
- Grid: `<div class="program-grid" id="tvsender-schedule-cards">`.
- **Tagesgruppierung:** `@foreach (var day in schedule.GroupBy(s => s.Start.ToLocalTime().Date))`
  → `<div class="tvsender-day-header" data-day="yyyy-MM-dd">dddd, dd.MM.yyyy</div>` (lokale
  Serverzeit; Kommentar N27 verweist auf den freigegebenen Prototyp `sendeplan-vorschau.html`).
- **Regel-Trennlinie:** innerhalb eines Tages bei `slot.ProgramRuleId != previousRuleId`
  (nicht beim ersten Slot des Tages): `<div class="tvsender-rule-divider"
  data-rule-id="…"><span class="tvsender-rule-divider-label">@RuleName(slot)</span></div>`.
- **Slot-Karte (Z. 443–472):**
  `<div class="program-card tvsender-slot-row [tvsender-slot-displaced] [slot-now]"
  data-slot-id="…">` mit
  - `.program-poster` + `<img src="@PosterUrl(card)">`,
  - `.program-badges`: `badge-live` („Live") bei `IsRunning`, `badge-genre`,
    Status-Chip „Geplant"/„Verdrängt" (`admin-chip-success`/`-warning`),
  - `.program-time`: `dd.MM. HH:mm – HH:mm` (lokal),
  - `.program-title`: `card.ShowName ?? card.Title`,
  - `.program-sub` (Episode) + `.program-sub` „Regel: @RuleName(slot)",
  - `.program-desc`: `card.Description ?? ProgramGuideEntryText.NoDescriptionText`,
  - `.progress-track > .progress-fill` (Breite `ProgressPercent`) nur bei `IsRunning`.

**Jeder `ProgramSlot` = genau eine Karte.** Es gibt weder eine Gruppierung noch eine
Detail-/Expand-Mechanik an Karten — Clip-Füll-Läufe erscheinen heute als viele einzelne
Kurz-Karten mit dem Clip-Dateinamen als Titel (Bare-Item-Fallback).

### Overlay-Muster `admin-dialog-overlay`

Vorhandene Dialoge auf derselben Seite (Z. 176–219 Regel-Editor `#rule-editor`,
Z. 843–923 fünf Bestätigungsdialoge): einheitliches Muster —
`@if (flag)` → `<div class="admin-dialog-overlay" @onclick="Schließen">` →
`<div class="admin-dialog" @onclick:stopPropagation>` mit `admin-card-header` (Titel +
Schließen-Button) und `admin-actions` (Confirm + `btn-ghost` Abbrechen).
Referenz für die geforderte Clip-Detailansicht.

### Zugehöriges Markup/Muster außerhalb

- `ConfirmationDialog.razor` (`VideoWebPlayer/Components/Shared/`) — generischer
  Bestätigungsdialog mit demselben Overlay.
- Styles: `VideoWebPlayer/wwwroot/app.css` — `.program-grid` (~Z. 630),
  `.program-card` (~Z. 636/784), `.slot-now` (~Z. 743), `.program-card.compact` (~Z. 763),
  `.admin-console .tvsender-day-header` (~Z. 818),
  `.admin-console .tvsender-rule-divider` (~Z. 831, gestrichelte Linie mit Label),
  `.program-card.tvsender-slot-displaced` (~Z. 847, durchgestrichen),
  `.admin-dialog-overlay`/`.admin-dialog` (~Z. 1112/1128).
- `TVSenderAdminClipGroups.razor` — eigenes Markup für den Clip-Gruppen-Tab (Gruppenliste
  `.clipgroup-row`, Mitgliederverwaltung, Lösch-Overlay `#clipgroup-delete-confirm`);
  zeigt das Dialog-Muster in einer Kind-Komponente.

## `LiveTv.razor` — öffentliche Ansicht (scope-abhängig)

Datei: `VideoWebPlayer/Components/Pages/LiveTv.razor` (396 Zeilen), Route `/live-tv`,
`@rendermode InteractiveServer`, Login- + Feature- + Zuschauer-Freigabe-Gate.

- `#live-tv-now` / `#live-tv-next`: kompakte `program-card` mit Live-Badge/Fortschritt.
- `#live-tv-upcoming`: `<ul class="livetv-upcoming-list">` — **ein
  `<li class="livetv-upcoming-row program-card compact" data-slot-id="…">` je
  `DtoProgramGuideEntry`** (Zeit, Titel, Sub, geklemmte Beschreibung) — hier zeigen
  Clip-Füll-Läufe ebenfalls viele Einzeleinträge.
- Daten kommen über `IProgramGuideApiClient` (HTTP gegen `ProgramGuideController`), nicht
  per EF — eine serverseitige Gruppierung müsste DTO/Controller erweitern oder clientseitig
  erkannt werden (`IsFiller` trifft Clip-Füll nicht!).
- Styles: `LiveTv.razor.css` + `app.css`.
