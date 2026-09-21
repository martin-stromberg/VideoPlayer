# Usability-Review

## Ergebnis

**Status:** Keine Befunde

## Befunde

Keine.

## Geprüfte Interaktionen

Liste der aus der Anforderung geprüften Benutzerinteraktionen:

- Backup-Datei auswählen (`<input type="file" accept=".bak">`, Abschnitt „Backup hochladen") → unauffällig. Standard-Dateiauswahl des Browsers, keine interne Kennung nötig; `accept=".bak"` filtert sinnvoll vor.
- Upload starten über Schaltfläche „Backup hochladen" → unauffällig. Verständlich beschriftet; ohne ausgewählte Datei erscheint „Bitte eine Backupdatei auswählen."; bei fehlendem Token „Sicherheitstoken fehlt. Bitte die Seite neu laden."
- Fortschritt verfolgen → unauffällig. Fortschrittsbalken mit Klartext „Übertragen: X von Y (Z %)", Zwischenstände wie „unterbrochen – wird fortgesetzt" bzw. „wird fortgesetzt".
- Laufenden Upload abbrechen → unauffällig. Sichtbarer „Abbrechen"-Button; nach Abbruch Klartext-Hinweis, wie fortgesetzt werden kann.
- Unterbrochenen Upload fortsetzen → unauffällig. Wiederaufnahme erfolgt transparent (localStorage + Server-Session, Offset-Abfrage); zusätzlich erscheint auf der Seite ein verständlicher Hinweis mit Dateinamen („Ein früherer Upload von „…" wurde nicht abgeschlossen. Wählen Sie dieselbe Datei erneut aus und klicken Sie auf „Backup hochladen"…").
- Nicht abgeschlossenen Upload verwerfen → unauffällig. „Verwerfen"-Schaltfläche im Hinweis; löscht clientseitigen Vermerk und serverseitige Session.
- Upload-Limit verstehen → unauffällig. Hilfetext „Maximal @FormatBytes(MaxUploadSizeBytes)" in menschenlesbarer Einheit (z. B. „5 GB"); Ablehnung wegen Größe wird als verständliche Meldung ausgegeben („Die Datei ist zu groß…"), serverseitige Fehlertexte sind auf Deutsch.
- Fehlerfall einordnen → unauffällig. HTTP-Fehler werden in verständliche Meldungen übersetzt (`describeHttpError`), serverseitige `error`-Texte werden übernommen; Erfolg/Fehler landen wie bisher als Statusmeldung auf der Seite (`backupStatus`/`backupError`-Redirect).
- Zweiten Upload starten, während einer läuft → unauffällig. Hinweis „Es läuft bereits ein Upload. Bitte warten Sie, bis er abgeschlossen ist." erscheint im `notice`-Element des laufenden Uploads, Fortschrittsbalken und Abbrechen-Button bleiben erhalten; Eingabefeld und Button sind während des Laufs deaktiviert.

## Geprüfte Dateien

Liste aller geprüften UI-Dateien:

- `VideoWebPlayer/Components/Pages/Admin/Backups.razor`
- `VideoWebPlayer/wwwroot/js/backupUpload.js`
- `VideoWebPlayer/Components/App.razor` (Einbindung von `js/backupUpload.js`)

Ergänzend zur Bewertung der sichtbaren Meldungen herangezogen (nicht UI-Dateien im engeren Sinn):

- `VideoWebPlayer/Controllers/BackupsController.cs` (serverseitige `error`-Texte, die dem Anwender angezeigt werden)
- `docs/help/backups.md` (aktualisierte Hilfe zum Upload-Verfahren)
