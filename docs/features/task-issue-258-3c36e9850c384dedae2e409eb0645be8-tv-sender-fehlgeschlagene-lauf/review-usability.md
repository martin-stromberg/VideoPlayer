# Usability-Review

## Ergebnis

**Status:** Keine Befunde

## Geprüfte Interaktionen

- Fehlgeschlagene Dateien einsehen (Tab „Laufzeitermittlung", Chip „N fehlgeschlagen" → „▾ Details") → unauffällig. Bereits etabliertes Muster, unverändert; die Detailtabelle zeigt Name, Quelle, Versuche und letzten Versuch — für einen nicht-technischen Administrator ausreichend, um die Datei zu identifizieren, ohne interne Kennung.
- Einzelnen Eintrag zurücksetzen (Schaltfläche „Erneut ermitteln" je Zeile, `TVSenderAdmin.razor:596`) → unauffällig. Auswahl erfolgt per Klick auf die Tabellenzeile, keine ID-Eingabe nötig. Die Beschriftung „Erneut ermitteln" ist laienverständlich; die Statusmeldung „… wurde zurückgesetzt und wird beim nächsten Ermittlungslauf erneut geprüft" (`TVSenderAdmin.razor:1481`) erklärt Ergebnis und Zeitpunkt.
- Massen-Reset aller fehlgeschlagenen Dateien („Alle zurücksetzen", `TVSenderAdmin.razor:519-521`) → unauffällig. Die Schaltfläche steht direkt im Zähler-Bereich der Karte und ist sichtbar, sobald Fehler vorliegen — ohne dass der Details-Bereich aufgeklappt sein muss. Tooltip erklärt die Wirkung. Bestätigungsdialog (`TVSenderAdmin.razor:681-695`) folgt dem etablierten `admin-dialog`-Muster, nennt die konkrete Anzahl und beschreibt in einem Satz, was passiert; Abbruch ist möglich.
- Kappung der Fehlerliste (>25 Einträge) verstehen → unauffällig. Die Fußzeile der Tabelle (`TVSenderAdmin.razor:602`) erklärt ausdrücklich, dass nur die ersten Einträge angezeigt werden und „Alle zurücksetzen" auch die nicht angezeigten Dateien erfasst. Ein gezielter Einzel-Reset nicht angezeigter Items ist nicht möglich, wurde aber von der Anforderung nicht gefordert — der Massen-Reset deckt genau diesen Fall ab.
- Feedback und Fortschritt nach dem Reset → unauffällig. Statusmeldung über `probeStatus` im selben Karten-Bereich (`TVSenderAdmin.razor:641-644`, Muster wie `scheduleStatus`/`rulesStatus`), Liste wird sofort neu geladen und läuft über die bestehende 15-s-Aktualisierung weiter.

## Geprüfte Dateien

- `VideoWebPlayer/Components/Pages/Admin/TVSenderAdmin.razor` (einzige geänderte UI-Datei lt. `git diff`/`git status` gegen `staging`)
