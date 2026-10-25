# Usability-Review

## Ergebnis

**Status:** Befunde vorhanden

## Befunde

### TVSenderAdmin.razor (Tab „Clip-Gruppen" — Clip-Zuordnung)

- **Fehlende Suche/Auswahl** — Die Kandidatenliste unter „Clips hinzufügen" ist hart auf die ersten 100 Treffer begrenzt (`ReloadCandidatesAsync` ruft `GetClipCandidatesAsync(..., 0, 100)`), zeigt aber weder eine Trefferzahl noch einen Hinweis „… und N weitere" noch eine Blättern-Schaltfläche. Enthält die Quelle mehr als 100 unklassifizierte Videodateien, sind Clips jenseits der ersten 100 alphabetisch unsichtbar — eine Anwenderin, die die Kandidatenmenge durchstöbert (statt gezielt nach einem bekannten Namen zu suchen), kann diese Clips der Gruppe nie zuordnen, ohne zu wissen, dass sie existieren.

  Empfehlung: „Weitere laden"-Schaltfläche bzw. Seitensteuerung nachrüsten oder zumindest einen Hinweis „Es gibt weitere Treffer — bitte Suche oder Quelle eingrenzen" anzeigen.

- **Erreichbarkeit** — Bereits der Gruppe zugeordnete Clips erscheinen weiterhin unverändert in der Kandidatenliste mit aktivem „Hinzufügen"-Button. `GetClipCandidatesAsync` blendet vorhandene Mitglieder nicht aus, und auch direkt nach dem Klick auf „Hinzufügen" bleibt die Zeile mit demselben Button stehen (die Kandidatenliste wird nicht neu geladen). Ein erneutes Hinzufügen läuft serverseitig still ins Leere („items already in the group are skipped"). Eine Anwenderin kann an der Liste nicht erkennen, welche Clips schon in der Gruppe sind, und erhält nach „Hinzufügen" keine Rückmeldung an der betroffenen Zeile — sie muss selbst in der darüberliegenden Mitgliedertabelle nachsehen, ob es geklappt hat.

  Empfehlung: Bereits zugeordnete Clips in der Kandidatenliste ausblenden oder die Zeile als „bereits in dieser Gruppe" kennzeichnen und den Button deaktivieren; nach dem Hinzufügen die Kandidatenliste aktualisieren.

### TVSenderAdmin.razor (Tab „Clip-Gruppen" — reservierte Gruppe „Trailer")

- **Erreichbarkeit** — Für die System-Gruppe „Trailer" bietet die Liste die Schaltfläche „Ansehen" an. Sie öffnet dieselbe Mitgliedertabelle wie bei normalen Gruppen, zeigt aber zwangsläufig „0 Einträge" mit leerer Tabelle, weil die Trailer-Gruppe keine gespeicherten Mitglieder hat (Befüllung erfolgt dynamisch zur Planungszeit). Trotz des Hinweistextes darüber kann eine nicht-technische Anwenderin das als „die Gruppe ist leer / funktioniert nicht" lesen — gerade weil die Anforderung sie „wie eine normale Gruppe" auswählbar macht.

  Empfehlung: Für die Trailer-Gruppe statt der leeren Mitgliedertabelle entweder die tatsächlich anstehenden Trailer (Filme im Vorlauffenster) auflisten oder die Tabelle durch einen erläuternden Text ersetzen („wird zur Planungszeit automatisch gefüllt — keine gespeicherte Mitgliederliste").

## Geprüfte Interaktionen

Liste der aus der Anforderung geprüften Benutzerinteraktionen:
- Benannte Clip-Gruppe anlegen („Neue Gruppe" → Namensdialog) → unauffällig
- Gruppe umbenennen („Umbenennen" → Namensdialog) → unauffällig
- Gruppe löschen (Bestätigungsdialog mit klarer Warnung) → unauffällig
- Reservierter Name „Trailer" bei Anlage/Umbenennung → unauffällig (serverseitige Prüfung, verständliche Fehlermeldung im Dialog)
- Clips einer Gruppe zuordnen (Suche nach Name, Filter nach Quelle, Klartext-Namen — keine IDs) → Befund vorhanden (100er-Kappe ohne Hinweis; bereits zugeordnete Clips bleiben als „Hinzufügen" sichtbar)
- Clip aus Gruppe entfernen („Entfernen" je Zeile) → unauffällig
- Abspiellänge eines Clips erkennen → unauffällig (Spalte „Laufzeit", „keine Laufzeit" markiert ungeeignete Clips)
- Gruppen an einer Sendeplatz-Regel wählen, inkl. reservierter „Trailer" (Mehrfachauswahl) → unauffällig (Checkbox-Liste mit Klartextnamen und „(automatisch befüllt)"-Kennzeichnung; Leer-Hinweis verweist auf den Clip-Gruppen-Tab; Hinweis bei fehlender Sendeplatz-Länge)
- Inhalt der Trailer-Gruppe einsehen („Ansehen") → Befund vorhanden (leere Tabelle wirkt wie leere Gruppe)
- Karenzzeit an der Regel einstellen → unauffällig (Zahlenfeld „Karenzzeit (Minuten)", Vorauswahl 15)

## Geprüfte Dateien

Liste aller geprüften UI-Dateien:
- `VideoWebPlayer/Components/Pages/Admin/TVSenderAdmin.razor`
