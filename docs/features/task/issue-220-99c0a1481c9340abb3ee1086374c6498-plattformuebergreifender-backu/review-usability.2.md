# Usability-Review

## Ergebnis

**Status:** Befunde vorhanden

## Befunde

### backupUpload.js / Backups.razor (Admin-Seite `/admin/backups`)

- **Erreichbarkeit** — Hinweis auf unterbrochenen Upload ist nicht abstellbar: Nach einem Abbruch oder Verbindungsabbruch zeigt die Seite dauerhaft den Hinweis „Ein früherer Upload von „X" wurde nicht abgeschlossen …" (`Backups.razor` Zeilen 248–253). Der Eintrag liegt im `localStorage` unter dem Schlüssel `vwp-backup-upload:{Name}:{Größe}:{lastModified}` (`backupUpload.js` Zeile 15) und wird nur entfernt, wenn die exakt gleiche Datei erneut ausgewählt und der Upload abgeschlossen bzw. endgültig verworfen wird. Eine Anwenderin, die den Import nicht fortsetzen will oder die Originaldatei nicht mehr besitzt, kann den Hinweis nie loswerden — es gibt kein „Verwerfen"/„Hinweis ausblenden". Der Hinweis erscheint zudem bei jedem Seitenaufruf erneut, auch wenn die Server-Session längst abgelaufen ist.

  Empfehlung: Einen „Verwerfen"-Button (z. B. `backupUpload.discardResumable`) am Hinweis anbieten, der den localStorage-Eintrag löscht und optional serverseitig die Upload-Session abbricht.

- **Erreichbarkeit** — Fortschrittsanzeige aktualisiert sich nur pro ~128-MB-Chunk: `fetch()` liefert keine Upload-Fortschrittsereignisse; `updateProgress` wird erst nach Abschluss jedes Chunks aufgerufen (`backupUpload.js` Zeilen 167–227). Bei einer mehrere GB großen Datei auf langsamen Verbindungen bleiben Prozentzahl und übertragene Bytes minutenlang unverändert (nur die animierten Streifen bewegen sich). Eine nicht-technische Anwenderin kann das als „hängengeblieben" deuten und den Upload unnötig abbrechen — die geforderte Fortschrittsanzeige erfüllt ihren Zweck dann nur eingeschränkt.

  Empfehlung: Entweder kleinere Chunks (z. B. 32 MB) oder XHR mit `upload.onprogress` für Fortschritt innerhalb eines Chunks; alternativ zumindest einen Hinweistext wie „Abschnitt wird übertragen …" während eines laufenden Chunks anzeigen.

- **Erreichbarkeit** — Kryptische Fehlermeldungen bei HTTP-Fehlern ohne JSON-Body: Wenn der Server keinen JSON-Fehler liefert (z. B. IIS-`requestFiltering`-Block 413, abgelaufene Anmeldung 401/403 während eines langen Uploads, 404-Seite eines Proxys), zeigt die Anwenderin nur „Upload fehlgeschlagen (Status 413)." (`backupUpload.js` Zeilen 255–271). Ein bloßer Statuscode ist für Laien nicht interpretierbar und gibt keine Handlungsanweisung.

  Empfehlung: Häufige Statuscodes auf verständliche Texte mappen (401/403 → „Sitzung abgelaufen, bitte neu anmelden", 413 → „Datei zu groß bzw. vom Serverlimit abgelehnt") und generisch einen Hinweis ergänzen, dass der Upload später fortgesetzt werden kann.

## Geprüfte Interaktionen

Liste der aus der Anforderung geprüften Benutzerinteraktionen:
- Backup-Datei über Dateiauswahl auswählen (`accept=".bak"`, klarer Abschnitt „Import / Backup hochladen") → unauffällig
- Upload starten über Button „Backup hochladen" → unauffällig
- Upload ohne ausgewählte Datei starten → Fehlermeldung „Bitte eine Backupdatei auswählen." → unauffällig
- Fortschritt während des Uploads verfolgen → Befund vorhanden (nur chunkweise Aktualisierung)
- Laufenden Upload über „Abbrechen" anhalten → unauffällig (klare Nachricht mit Fortsetzungsanleitung)
- Unterbrochenen Upload fortsetzen (nach Abbruch, Netzwerkfehler oder Seitenreload) → funktioniert über gleiche Datei + Hinweisbanner; Befund vorhanden (Hinweis nicht abstellbar)
- Erfolgs- und Fehlermeldungen nach Upload verstehen → Befund vorhanden (reine HTTP-Statuscodes bei Nicht-JSON-Antworten)
- Interne/technische Kennungen eingeben oder kennen müssen → keine vorhanden → unauffällig
- Auswahl benannter Entitäten mit Such-/Auswahlmöglichkeit → nicht erforderlich (Dateisystem-Dialog) → unauffällig

## Geprüfte Dateien

Liste aller geprüften UI-Dateien:
- `VideoWebPlayer/Components/Pages/Admin/Backups.razor`
- `VideoWebPlayer/Components/App.razor`
- `VideoWebPlayer/wwwroot/js/backupUpload.js`
- `docs/help/backups.md` (Anwenderdokumentation zum Upload-Verfahren)
