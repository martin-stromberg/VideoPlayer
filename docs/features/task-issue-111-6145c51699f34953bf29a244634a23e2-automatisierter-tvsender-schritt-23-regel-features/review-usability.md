# Review: Benutzerfreundlichkeit (Schritt 23 — N21 Overlay-Editor)

## Ergebnis

**Status:** Erfüllt

## Geprüfte Punkte

| Aspekt | Befund |
|--------|--------|
| Overlay-Konvention | Editor nutzt `.admin-dialog-overlay` + `.admin-dialog` — identisch zu den Bestätigungsdialogen der Seite und `PlaylistCoverPanel`/`ConfirmationDialog`. |
| Breite | `.rule-editor-dialog { width: min(960px, 100%) }` — das Formular (viele Felder: Material-Picker, Genre-Mehrfachauswahl, Quellenfilter) bekommt mehr Platz als der 720-px-Standarddialog; Scrollen via `max-height:90vh`/`overflow-y:auto` von `.admin-dialog` geerbt. |
| Schließen | Drei Wege: „Schließen" (Kopfzeile), „Abbrechen" (Fuß), Klick auf den abgedunkelten Hintergrund (`@onclick="CancelEdit"` mit `@onclick:stopPropagation` im Dialog) — exakt die Dialog-Konvention der App. |
| Fehleranzeige | `#rule-edit-error` unverändert im Dialog sichtbar. |
| Zustand | `editRule`/`editError`-Zustand unverändert; Editor öffnet über „Neue Programmregel" und die Bearbeiten-Schaltflächen der Tabelle. |
| IDs/Labels | Alle Feld-IDs (`rule-name`, `rule-recurrence`, `rule-save`, …) unverändert — bestehende E2E-Abläufe greifen weiter; neue Wiederholungsoptionen sind deutsche, konsistente Labels. |
| Rechte | Admin-Zwang unverändert serverseitig (`ProgramRuleService`); UI blendet nur aus. |

## Verifikation

- bUnit: `RuleEditor_RendersAsOverlayDialog` — Overlay-Struktur, Overlay-Klick schließt,
  Schließen-Button schließt, Felder vorhanden.
- E2E: `TVSenderAdminE2ETests.Admin_CreateEditDeactivateDeleteRule` — kompletter Fluss
  (Anlegen → Bearbeiten → Deaktivieren → Löschen) durch das Overlay, grün.

## Hinweise

- Kein dediziertes Fokus-Trapping/Escape-Handling — entspricht den übrigen App-Dialogen
  (kein Dialog der App implementiert das); Fokus landet beim Öffnen auf dem ersten Feld
  des Dialogs (DOM-Reihenfolge).
