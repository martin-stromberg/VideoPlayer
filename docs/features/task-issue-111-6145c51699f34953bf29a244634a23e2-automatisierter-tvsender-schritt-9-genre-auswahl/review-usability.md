# Usability-Review — Schritt 9

*Selbstreview des Implementierers (kein separates Review-Tool verfügbar); die fachliche Abnahme
bleibt dem Prüfer gemäß AGENTS.md §4 vorbehalten.*

## Änderungen an der Oberfläche

- `rule-material-genre` ist jetzt eine **Checkbox-Liste** (statt Single-Select) mit stabiler
  Checkbox-Id `rule-genre-{id}` — Mehrfachauswahl direkt, kein „Ändern"-Umweg nötig.
- Der Genre-Editor bleibt sichtbar, solange Genre-Material gewählt ist — die Material-Chip-Anzeige
  versteckt ihn nicht mehr (sonst wäre eine Zweitauswahl unmöglich).
- Neue **Quellen-Checkbox-Liste** `rule-material-sources` (`rule-source-{id}`), nur bei
  Genre-Material sichtbar; Beschriftung „keine Auswahl = alle Quellen" beschreibt die Semantik.
- Änderung der Quell-Auswahl lädt die Genre-Optionen neu; **bereits gewählte Genres, die
  außerhalb der neuen Quell-Auswahl liegen, bleiben als angehakte Optionen sichtbar** (kein
  stilles Wegfallen von gespeicherter Auswahl).
- Regel-Tabelle zeigt `Genre: A, B` und bei Quell-Filter zusätzlich `— Quellen: …` an.
- Versteckte bzw. nicht kanonisch sichtbare Genres erscheinen nicht als Checkbox (getestet).
- CSS `.rule-multi-select`: scrollbare Liste (max-height 200px), Admin-Theme-konforme Border.

## Festgestellte Punkte

- Wird ein Genre gewählt, erscheint die Material-Chip `Genre: A, B` — konsistent mit bisherigem
  Verhalten; über „Ändern" wird die Auswahl komplett geleert (inkl. Quell-Filter). Beabsichtigt.
- Keine Fokusfallen; Checkboxen sind nativ und tastaturbedienbar. Stabile Ids für E2E vorhanden.
