# Usability-Review

## Ergebnis

**Status:** Befunde vorhanden

## Befunde

### TVSenderAdminClipGroups.razor (Tab „Clip-Gruppen", Bereich „Clips hinzufügen")

- **Abweichendes Muster** — Die Kandidaten-Suche für die Clip→Gruppe-Zuordnung bindet das Suchfeld mit `@bind:event="oninput"` (Zeile 88), löst aber kein Neuladen aus: Erst der Klick auf „Suchen" (Zeile 101) wendet Suchtext und Quellenfilter an. Das Quellen-Dropdown (Zeile 92–98) reagiert ebenfalls nicht bei Auswahl. Eine Anwenderin, die einen Clip-Namen eintippt oder eine Quelle wählt, sieht keine Reaktion der Tabelle — im selben Admin-Dialog sucht die etablierte `MediaSearchSelector`-Komponente live mit 400-ms-Debounce (`@bind:after="OnSearchTermChangedAsync"`). Wer das Live-Suchverhalten aus dem Regel-Dialog kennt, hält die Eingabe hier für defekt, bis der Button entdeckt wird.

  Empfehlung: `ReloadCandidatesAsync` gedebounced per `@bind:after` am Suchfeld auslösen und das Quellen-`<select>` bei Änderung sofort neu laden (`@bind:after` oder `@onchange`); der „Suchen"-Button kann dann entfallen oder bleibt als explizite Alternative.

### TVSenderAdmin.razor (Regel-Dialog, Hinweistext unter der Gruppen-Mehrfachauswahl)

- **Erreichbarkeit** — Der Hinweis „Clip-Gruppen und Karenzzeit wirken nur bei gesetzter Sendeplatz-Länge" (Zeile 359) verwendet den Begriff „Sendeplatz-Länge", das zugehörige Eingabefeld im selben Dialog heißt aber „Länge (Minuten)" (`rule-duration`, Zeile 243). Eine Laiin kann „Sendeplatz-Länge" nicht sicher diesem Feld zuordnen; der Begriff existiert sonst nirgends als Feldbeschriftung.

  Empfehlung: Hinweiswortlaut an die tatsächliche Feldbeschriftung angleichen, z. B. „… wirken nur bei gesetzter Länge (Minuten)", oder das Feld in „Sendeplatz-Länge (Minuten)" umbenennen.

## Geprüfte Interaktionen

Liste der aus der Anforderung geprüften Benutzerinteraktionen:

- Clip-Gruppe anlegen (Tab „Clip-Gruppen" → „Neue Gruppe") → unauffällig
- Clip-Gruppe umbenennen („Umbenennen" je Gruppe, Dialog) → unauffällig
- Clip-Gruppe löschen (Bestätigungsdialog mit Konsequenz-Hinweis „Zuordnungen zu Regeln werden ebenfalls entfernt") → unauffällig
- Reservierter Name „Trailer" bei Gruppenanlage (kein Vorab-Hinweis im Dialog, aber klare Inline-Fehlermeldung „… ist für die reservierte System-Gruppe vorbehalten" beim Speichern) → unauffällig
- Clips einer Gruppe zuordnen (Kandidatentabelle mit Name/Quelle/Laufzeit, Suchfeld + Quellenfilter + „Weitere laden"-Paging, Status „Bereits zugeordnet") → Befund vorhanden (Such-/Filteranwendung nur per Button, abweichend vom Live-Suchmuster)
- Clips aus einer Gruppe entfernen („Entfernen" je Zeile in der Mitgliederliste) → unauffällig
- Kennzeichnung „keine Laufzeit" und Verständlichkeit der Konsequenz (Markierung in Mitglieder- und Kandidatenliste plus erklärender Hinweis „werden bei der Restzeit-Auffüllung aber nicht berücksichtigt" mit Verweis auf Tab „Laufzeitermittlung") → unauffällig
- System-Gruppe „Trailer" (in Gruppenliste als „Trailer (System — automatisch befüllt)", Button „Ansehen" mit Erklärtext, nicht umbenenn-/lösch-/befüllbar; am Regel-Dialog als „Trailer (automatisch befüllt)" auswählbar) → unauffällig
- Gruppen-Mehrfachauswahl am Regel-Dialog (Checkbox-Liste mit Klartext-Namen, Muster der Genre-/Quellen-Mehrfachauswahl wiederverwendet, Darstellung der Zuordnung in der Regel-Liste als „— Clips: …") → unauffällig
- Karenzzeit am Regel-Dialog (Zahlenfeld „Karenzzeit (Minuten)", Default 15; erscheint nur bei gesetzter Dauer, sonst Hinweistext) → Befund vorhanden (Hinweisbegriff „Sendeplatz-Länge" weicht von Feldbeschriftung „Länge (Minuten)" ab)
- Interne Kennungen in Eingaben (keine Stelle verlangt Ids/GUIDs/Dateinamen; alle Zuordnungen über Namen und Auswahllisten) → unauffällig
- Erreichbarkeit der Verwaltung (eigener Tab „Clip-Gruppen" in der Tab-Leiste, aria-Wiring wie die übrigen Tabs) → unauffällig

## Geprüfte Dateien

Liste aller geprüften UI-Dateien:

- `VideoWebPlayer/Components/Pages/Admin/TVSenderAdmin.razor`
- `VideoWebPlayer/Components/Pages/Admin/TVSenderAdminClipGroups.razor`
