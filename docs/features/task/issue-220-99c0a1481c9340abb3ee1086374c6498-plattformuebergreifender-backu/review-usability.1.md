# Usability-Review

## Ergebnis

**Status:** Befunde vorhanden

## Befunde

### Backups.razor / backupUpload.js (Admin-Seite `/admin/backups`, Upload-Bereich)

- **Erreichbarkeit** — Die in der Anforderung geforderte Wiederaufnahme unterbrochener Uploads ist für eine Endanwenderin nicht erkennbar. Nach Klick auf „Abbrechen" erscheint nur der Text „Upload abgebrochen. Er kann später fortgesetzt werden." (`backupUpload.js`, `finishAborted`, Zeile 259) — ohne Angabe, *wie*. Nach einem Browser-Neustart, Seitenwechsel oder einem Netzwerkfehler (`finishWithError` mit `keepEntry`, Zeile 170) existiert zwar ein fortsetzbarer localStorage-Eintrag, die Seite zeigt jedoch keinerlei Hinweis, dass ein unvollständiger Upload vorliegt oder dass er fortgesetzt werden kann. Ein Laie müsste wissen, dass er exakt dieselbe Datei erneut auswählen und erneut „Backup hochladen" klicken muss — dieses Wissen kann er nicht haben.

  Empfehlung: Hinweistext konkretisieren („…wählen Sie dieselbe Datei erneut aus und klicken Sie auf ‚Backup hochladen'") und/oder beim Rendern der Seite vorhandene `vwp-backup-upload:*`-Einträge in localStorage erkennen und einen sichtbaren Hinweis bzw. „Fortsetzen"-Affordance mit Dateiname anzeigen.

### Backups.razor / backupUpload.js (Admin-Seite `/admin/backups`, Upload-Button)

- **Erreichbarkeit** — Der Button „Backup hochladen" (Backups.razor, Zeile 245) bleibt während eines laufenden Uploads klickbar: `isBusy` wird für den JS-Upload nicht gesetzt, und das Skript deaktiviert nur das Dateifeld (`inputEl.disabled = true`, `backupUpload.js` Zeile 116), nicht den Button. Ein erneuter Klick löscht über `createUi` die sichtbare Fortschrittsanzeige und startet einen parallelen Upload-Loop auf dieselbe Session; der Server lehnt dann den Offset der Nebenläufigkeit mit 400/409 ab, worauf der Upload mit einer technischen Fehlermeldung (z. B. „Upload-Offset fehlt oder ist ungültig.") abbricht. Ein Doppelklick oder ungeduldiges erneutes Klicken zerstört so den laufenden Upload ohne erkennbaren Grund.

  Empfehlung: Während eines aktiven Uploads den Upload-Button deaktivieren (z. B. Button-Referenz ans Skript übergeben und `disabled` setzen wie beim Dateifeld) oder weitere `start`-Aufrufe ignorieren, solange ein Upload läuft.

## Geprüfte Interaktionen

Liste der aus der Anforderung geprüften Benutzerinteraktionen:
- Backup-Datei über Dateiauswahl-Dialog auswählen (`input type="file" accept=".bak"` im beschrifteten Bereich „Import / Backup hochladen") → unauffällig
- Upload starten (Button „Backup hochladen") → Befund vorhanden (Button während Upload weiterhin aktiv)
- Upload-Fortschritt verfolgen (Fortschrittsbalken mit „Übertragen: X von Y (Z %)") → unauffällig
- Upload abbrechen („Abbrechen"-Button während des Uploads) → unauffällig
- Unterbrochenen Upload fortsetzen (Wiederaufnahme) → Befund vorhanden (keine erkennbare Fortsetzen-Möglichkeit / kein Hinweis auf vorhandene Session)
- Erfolgs- und Fehlermeldungen nach Abschluss erhalten (Redirect mit `backupStatus`/`backupError` → Alerts auf der Seite) → unauffällig
- Upload-Limit und zulässiges Dateiformat erkennen (Hinweistext „Maximal … Nur gültige Backup-Dateien (.bak)"; Limit wird serverseitig via 413 mit deutscher Fehlermeldung durchgesetzt) → unauffällig

## Geprüfte Dateien

Liste aller geprüften UI-Dateien:
- `VideoWebPlayer/Components/Pages/Admin/Backups.razor`
- `VideoWebPlayer/Components/App.razor`
- `VideoWebPlayer/wwwroot/js/backupUpload.js`
