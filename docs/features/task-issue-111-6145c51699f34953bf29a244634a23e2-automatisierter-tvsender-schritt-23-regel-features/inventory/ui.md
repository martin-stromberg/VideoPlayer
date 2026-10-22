# UI

## `TVSenderAdmin.razor`
Datei: `VideoWebPlayer/Components/Pages/Admin/TVSenderAdmin.razor` (1350 Zeilen)

- Vier Tabs (`admin-tabs`): `regeln`, `sendeplan`, `einstellungen`, `laufzeit`. Panes bleiben
  gerendert (CSS-hidden) — Zustand überlebt Tabwechsel.
- Regel-Editor: `@if (editRule != null)`-Block, `div.admin-card.admin-card-wide.rule-editor-card`
  mit `id="rule-editor"`, inline im Regeln-Pane. Enthält: Name, Regelart-Select
  (`OnRuleTypeChanged`), Wiederholungs-Select (`OnRecurrenceChanged`, hartcodierte Optionen
  Täglich/Wöchentlich/Einmalig/An Feiertagen — **hier fehlen Werktag/Wochenende**),
  konditional Wochentag/Datum, Sendezeit, Umfang, Aktiv-Checkbox, Material-Picker
  (`MediaSearchSelector`, Playlist-Select, Trailer-Checkbox, Clip-Quellen-Select,
  Genre-Mehrfachauswahl + Quellenfilter), Fehleranzeige `#rule-edit-error`,
  Speichern/Abbrechen.
- `RecurrenceLabel(ProgramRule)` (Z. ~1300): Label-Mapping für die Regel-Tabelle — **hier
  fehlen ebenfalls Werktag/Wochenende**.
- Overlay-Konvention der App: `div.admin-dialog-overlay` (fixed, z-index 1600,
  `@onclick` = schließen) > `div.admin-dialog` (`@onclick:stopPropagation`). Beispiele auf
  derselben Seite: `confirmCleanup` (Verdrängte bereinigen), `confirmReset` (Plan
  zurücksetzen), `deleteCandidate` (Regel löschen). Generisch: `Components/Shared/
  ConfirmationDialog.razor`, `Components/Playlists/PlaylistCoverPanel.razor` (großes
  Overlay-Panel, `.admin-dialog` + eigene Breite).
- CSS: `.admin-dialog-overlay`/`.admin-dialog` in `wwwroot/app.css` (~Z. 889–931,
  `width: min(720px,100%)`, max-height 90vh, overflow-y auto); `.rule-editor-card` ~Z. 589.

## `GenreAdmin.razor`
Nutzt dieselbe `admin-dialog-overlay`-Konvention für Bestätigungsdialoge.
