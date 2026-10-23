# Usability-Review

## Ergebnis

**Status:** Befunde vorhanden

## Befunde

### TVSenderAdmin.razor (Seite „TV-Sender", Tab „Laufzeitermittlung")

- **Fehlende Suche/Auswahl** — Die Fehlerliste „Fehlgeschlagene Dateien" zeigt maximal `detailItemCount` (Default 25) Einträge; der Rest erscheint nur als Zeile „… und N weitere" (Zeilen 594–597). Ein Administrator, der gezielt **eine bestimmte** Datei zurücksetzen will, die nicht unter den ersten 25 sichtbaren Einträgen ist, kann diese weder sehen noch einzeln adressieren — es gibt weder Paging noch eine Suche. Die einzige Option ist „Alle zurücksetzen", was alle fehlgeschlagenen Dateien betrifft. Empfehlung: Paging bzw. „Alle anzeigen" für die Fehlerliste oder ein Suchfeld, damit einzelne Einträge jenseits der Kappung sichtbar und einzeln zurücksetzbar werden; mindestens ein Hinweis in der „… weitere"-Zeile, dass nicht angezeigte Dateien nur über „Alle zurücksetzen" erfasst werden.

- **Erreichbarkeit** — Die Massen-Reset-Schaltfläche „Alle zurücksetzen" (Zeile 601) liegt innerhalb des zugeklappten `<details>`-Blocks `tvsender-probe-failed-detail`. Solange der Administrator „▾ Details" am fehlgeschlagen-Chip nicht geöffnet hat, existiert auf der Karte keinerlei sichtbarer Hinweis, dass ein Zurücksetzen überhaupt möglich ist. Für die Einzelaktion je Zeile ist das unvermeidlich und im Muster etabliert; die Sammelaktion wäre jedoch auch ohne Aufklappen sinnvoll erreichbar. Empfehlung: „Alle zurücksetzen" zusätzlich direkt beim fehlgeschlagen-Chip bzw. im Kartenkopf der Fehlerliste anbieten (Bestätigungsdialog bleibt unverändert).

## Geprüfte Interaktionen

- Tab „Laufzeitermittlung" auf `/admin/tv-sender` öffnen und Status der Ermittlung ablesen → unauffällig
- Fehlerliste über „▾ Details" am Zähler „N fehlgeschlagen" aufklappen → unauffällig (Toggle ist sichtbar und mit „Betroffene Dateien anzeigen" beschriftet)
- Einzelnen fehlgeschlagenen Eintrag anhand von Name und Quelle identifizieren und „Probe zurücksetzen" klicken → Befund vorhanden (nur die ersten 25 Einträge sind einzeln adressierbar, siehe Befund 1; für sichtbare Einträge keine interne Kennung nötig)
- Massen-Reset „Alle zurücksetzen" mit Bestätigungsdialog ausführen → Befund vorhanden (Schaltfläche nur im aufgeklappten Details-Bereich erreichbar, siehe Befund 2; Dialog selbst folgt dem etablierten `admin-dialog`-Muster und nennt die volle Anzahl)
- Rückmeldung nach dem Zurücksetzen lesen (Statusmeldung + aktualisierte Zähler) → unauffällig (verständliche Erfolgs-/Fehltexte, Fortschrittsanzeige wird neu geladen)

## Geprüfte Dateien

- `VideoWebPlayer/Components/Pages/Admin/TVSenderAdmin.razor`
