# Usability-Review

## Ergebnis

**Status:** Befunde vorhanden

## Befunde

### TVSenderAdmin.razor (TV-Sender-Administration, Tab „Einstellungen")

- **Interne Kennung** — Ist das zuletzt gewählte Standby-Medium inzwischen gelöscht, zeigt die Statuszeile oberhalb des „Ändern"-Buttons `#{Id} (Medium nicht mehr vorhanden)` (Methode `LoadStandbyLabelAsync`). Die nackte Datenbank-Id (`#1234`) ist für eine nicht-technische Anwenderin bedeutungslos und lässt sich auch nicht zum Wiederfinden des früheren Clips nutzen, weil kein Eingabefeld diese Id akzeptiert.

  Empfehlung: Statt der nackten Id einen selbsterklärenden Text anzeigen, z. B. „Zuvor gewählter Clip ist nicht mehr vorhanden — Standard-Clip wird verwendet". Falls die Id für Support-Zwecke erhalten bleiben soll, sie in Klammern hinter den Klartext setzen oder gar nicht anzeigen.

## Geprüfte Interaktionen

- Standby-Clip in den Sender-Einstellungen auswählen (Tab „Einstellungen" → Karte „Standby-Clip (Warm-up-Phase)" → „Ändern" → Quelle filtern bzw. suchen → „Auswählen" → „Speichern") → unauffällig: erreichbar über eine sichtbar beschriftete Schaltfläche, erklärender Text über Zweck und Einschränkung („nur nicht klassifizierte Videodateien"), Kandidaten mit Klartext-Name, Quelle und Laufzeit in einer Tabelle.
- Gewünschten Clip in großen Beständen finden → unauffällig: Live-Suche mit Eingabeverzögerung, Quellen-Dropdown mit Klartext-Namen („Alle Quellen" als Standard), „Weitere laden" für lange Trefferlisten samt Hinweistext. Gleiches Auswahlmuster wie die bewährte Kandidaten-Auswahl in `TVSenderAdminClipGroups.razor` — inklusive identischem Leerzustand-Text.
- Aktuell gewählten Clip erkennen und zurücksetzen → unauffällig: Chip mit „Name (Quelle)" bzw. „Standard-Clip (Gleich geht's los)", markierte Zeile „Ausgewählt", Schaltfläche „Standard-Clip verwenden" zum Zurücksetzen.
- Zuschauende in der Warm-up-Phase → unauffällig: keine UI-Änderung nötig bzw. vorhanden (`LiveTv.razor`/`liveTv.js` unverändert); die Standby-Auslieferung erfolgt automatisch, ohne dass Anwender etwas einstellen oder kennen müssen.

## Geprüfte Dateien

- `VideoWebPlayer/Components/Pages/Admin/TVSenderAdmin.razor` (einzige geänderte UI-Datei; neue Karte „Standby-Clip (Warm-up-Phase)" mit Auswahlliste, Suche, Quellenfilter und Zurücksetzen)
