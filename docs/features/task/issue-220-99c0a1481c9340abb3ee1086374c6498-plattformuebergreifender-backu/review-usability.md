# Usability-Review

## Ergebnis

**Status:** Befunde vorhanden

## Befunde

### Backups.razor (Admin-Seite `/admin/backups`, Bereich „Einstellungen")

- **Interne Kennung / technischer Wert** — Die Anforderung sieht `MaxUploadSizeBytes` als „administrierbares Limit" vor. Die Pflege erfolgt über ein Zahlenfeld „Upload-Limit in Bytes" (Zeilen 339–343): Wer das Limit z. B. auf 6 GB anheben will, muss den Wert in Bytes (6 442 450 944) selbst berechnen und fehlerfrei eintippen. Ein nicht-technischer Admin kann diese Umrechnung nicht leisten; eine Vertipper-Einordnung (KB vs. GB) ist praktisch nicht erkennbar. Der Hinweis „Aktuell: …" zeigt den bestehenden Wert zwar formatiert, hilft aber bei der Neueingabe nicht.

  Empfehlung: Eingabe in einer verständlichen Einheit ermöglichen — z. B. Zahlenfeld „Upload-Limit in MB/GB" mit serverseitiger Umrechnung oder Einheiten-Auswahl (Dropdown MB/GB) neben dem Zahlenfeld.

## Geprüfte Interaktionen

Liste der aus der Anforderung geprüften Benutzerinteraktionen:
- Backupdatei auswählen und hochladen (Datei-Dialog, `accept=".bak"`, Button „Backup hochladen") → unauffällig
- Fortschritt des Uploads verfolgen (Fortschrittsbalken mit Bytes/Prozent, Klartext) → unauffällig
- Upload abbrechen (Button „Abbrechen" mit anschließendem Klartext-Hinweis zur Fortsetzung) → unauffällig
- Unterbrochenen Upload fortsetzen (Info-Banner nennt Dateinamen und beschreibt die Schritte in Klartext) → unauffällig
- Unterbrochenen Upload verwerfen (Button „Verwerfen") → unauffällig
- Fehlerfälle verstehen (deutsche Klartext-Meldungen für 401/403, 404, 413, 5xx, Netzwerkfehler; Erfolgs-/Fehlermeldung via `backupStatus`/`backupError`) → unauffällig
- Upload-Limit (`MaxUploadSizeBytes`) administrieren → Befund vorhanden (Eingabe in Bytes)

## Geprüfte Dateien

Liste aller geprüften UI-Dateien:
- `VideoWebPlayer/Components/Pages/Admin/Backups.razor`
- `VideoWebPlayer/Components/Pages/Admin/Updates.razor` (nur Refactoring `FormatBytes` → `ByteSizeHelper`, keine Bedienänderung)
- `VideoWebPlayer/Components/App.razor` (nur Script-Einbindung `backupUpload.js`)
- `VideoWebPlayer/Components/_Imports.razor` (nur `@using static`)
- `VideoWebPlayer/wwwroot/js/backupUpload.js`
