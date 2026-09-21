# Usability-Review

## Ergebnis

**Status:** Befunde vorhanden

## Befunde

### Backups.razor / BackupUploadSessionService (Seite `/admin/backups`, Upload-Fehlermeldung)

- **Erreichbarkeit** — Lehnt der Server eine zu große Datei ab, wird dem Anwender der Fehlertext `Die Datei überschreitet das Upload-Limit von {maxUploadSizeBytes} Bytes.` (`BackupUploadSessionService.cs`, `BeginSessionResult.TooLarge`, Zeile 73) unverändert im roten Fehlerhinweis angezeigt (über `readErrorMessage` in `backupUpload.js` → Redirect `backupError` → Alert in `Backups.razor`). Ein Laie sieht dort z. B. „5368709120 Bytes" und kann weder erkennen, wie groß das Limit in vertrauten Einheiten ist, noch wie weit seine Datei darüber liegt. Gerade weil das Feature für Dateien ab 6 GB gedacht ist, das konfigurierbare Limit aber standardmäßig bei 5 GiB liegt, ist genau dieser Fall ein erwartbarer Fehlerpfad.

  Empfehlung: Das Limit in der Meldung serverseitig in menschenlesbarer Form ausgeben (z. B. „5 GB"), analog zur vorhandenen `FormatBytes`-Darstellung, die auf derselben Seite bereits „Maximal X" als Klartext zeigt.

## Geprüfte Interaktionen

Liste der aus der Anforderung geprüften Benutzerinteraktionen:
- Backup-Datei (.bak) über Dateiauswahl auswählen → unauffällig (Standard-Dateidialog mit `accept=".bak"`, kein technischer Wert erforderlich)
- Upload über beschriftete Schaltfläche „Backup hochladen" starten → unauffällig
- Fortschritt während des Uploads verfolgen → unauffällig (Fortschrittsbalken mit Klartext „Übertragen: X von Y (Z %)")
- Upload abbrechen → unauffällig (Schaltfläche „Abbrechen" mit verständlichem Hinweistext zum Fortsetzen)
- Unterbrochenen Upload fortsetzen → unauffällig (Info-Hinweis nennt den Dateinamen und beschreibt in Klartext: dieselbe Datei erneut auswählen und „Backup hochladen" klicken)
- Unterbrochenen Upload verwerfen → unauffällig (beschriftete Schaltfläche „Verwerfen")
- Erfolgs- und Fehlermeldungen nach Abschluss/Abbruch wahrnehmen → unauffällig (deutsche Klartext-Meldungen über das bestehende `backupStatus`/`backupError`-Query-Parametermuster; `describeHttpError` mappt Statuscodes auf verständliche Texte)
- Fehlermeldung bei Überschreiten des Upload-Limits verstehen → Befund vorhanden (Limit wird in rohen Bytes statt in GB ausgegeben)
- Interne/technische Kennungen eingeben → unauffällig (keine; Upload-Id/Offsets werden unsichtbar via Header/localStorage verwaltet)

## Geprüfte Dateien

Liste aller geprüften UI-Dateien:
- `VideoWebPlayer/Components/Pages/Admin/Backups.razor`
- `VideoWebPlayer/Components/App.razor`
- `VideoWebPlayer/wwwroot/js/backupUpload.js`
- `VideoWebPlayer/Controllers/BackupsController.cs` (nur anwendersichtbare Fehlertexte)
- `VideoWebPlayer/Services/Backups/BackupUploadSessionService.cs` (nur anwendersichtbare Fehlertexte)
