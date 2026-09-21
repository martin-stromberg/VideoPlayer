# Offene Aufgaben

Erstellt am: 2026-09-21
Abbruchgrund: Maximale Iterationsanzahl erreicht (Hauptlauf); Fortsetzungslauf: kein weiterer Fortschritt in der Punktzahl (1 → 1)

Die folgenden Aufgaben konnten im automatisierten Zyklus nicht abgeschlossen werden
und müssen manuell oder in einem erneuten Lauf bearbeitet werden.

## Offene Planelemente

Keine — `review.md` trägt den Status `Vollständig umgesetzt`.

## Code-Review-Befunde

- [x] `BackupUploadSessionServiceTests_*` — Test-Isolation (gelöst: injizierbares `tempDirectory`, `TempDirectory.Create()` pro Test, `CleanupOrphanedTempFiles` scannt nur das Instanz-Verzeichnis)
- [x] `backupUpload.js` `activeRun`-Guard — `showMessage` leerte den Container (gelöst: Hinweis geht in `ui.notice`-Element)
- [x] `FormatBytes`-Duplikation (gelöst: `VideoWebPlayer/Utils/ByteSizeHelper.cs`, `@using static` in `_Imports.razor`, `ByteSizeHelperTests`)

## Usability-Befunde

- [x] Limit-Fehlermeldung in rohen Bytes (gelöst: `FormatBytes` → „5 GB")
- [ ] `Backups.razor` (`/admin/backups`, Einstellungen, Z. 339–343) — Das administrierbare Upload-Limit (`MaxUploadSizeBytes`) muss in rohen Bytes eingegeben werden. Für 6 GB müsste ein nicht-technischer Admin „6442450944" selbst berechnen. Empfehlung: Eingabe in MB/GB bzw. Zahlenfeld mit Einheiten-Auswahl. (Vorbestand der Einstellungs-UI; relevant geworden, da das Limit nun serverseitig durchgesetzt wird. Fachliche Entscheidung nötig, ob im Scope.)

## Fehlgeschlagene Tests

Keine — `test-results.md`: 296/296 bestanden.

## Sonstiges

- [x] Stray-Datei `nul` im Repo-Root (gelöscht und committet)
