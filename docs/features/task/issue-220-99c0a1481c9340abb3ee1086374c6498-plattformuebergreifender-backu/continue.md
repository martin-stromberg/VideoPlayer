# Offene Aufgaben

Erstellt am: 2026-09-21
Abbruchgrund: Maximale Iterationsanzahl erreicht

Die folgenden Aufgaben konnten im automatisierten Zyklus nicht abgeschlossen werden
und müssen manuell oder in einem erneuten Lauf bearbeitet werden.

## Offene Planelemente

Keine — `review.md` trägt den Status `Vollständig umgesetzt`.

## Code-Review-Befunde

- [ ] `BackupUploadSessionServiceTests_*` — Test-Isolation: Tests mit `IncrementingTimeProvider` + `Advance(25h)` verschieben den Cleanup-Cutoff ~1h in die reale Zukunft. `CleanupOrphanedTempFiles` (`BackupUploadSessionService.cs` Z. 209–239) löscht dann alle `vwp-backup-upload-*.tmp` im gemeinsamen `%TEMP%`, die nicht in `_sessions` der eigenen Instanz stehen — inkl. Temp-Dateien parallel laufender xUnit-Testklassen (kein `DisableTestParallelization`). Unter Linux wird sogar der offene `FileShare.None`-Stream per Unlink entfernt → flaky Tests. Empfehlung: Temp-Verzeichnis pro Service-Instanz injizierbar machen oder betroffene Klassen in eine `[Collection]`.
- [ ] `backupUpload.js` Z. 229–233 — `showMessage(containerEl, ...)` im `activeRun`-Guard leert den kompletten Container und zerstört Fortschrittsbalken + Abbrechen-Button des laufenden Uploads, wenn `containerEl` dasselbe Element ist (erreichbar, sobald Blazor den `disabled`-Button nach Re-Render reaktiviert, z. B. bei `IsRestoreActive`-Wechsel). Empfehlung: Hinweis in `ui.text` des laufenden Runs schreiben statt Container zu leeren.

## Usability-Befunde

- [ ] `BackupUploadSessionService.cs` (Z. 73) / Upload-Fehlermeldung auf `/admin/backups` — Bei Überschreiten des Upload-Limits wird „Die Datei überschreitet das Upload-Limit von 5368709120 Bytes." angezeigt — rohe Bytes sind für nicht-technische Anwender unverständlich. Empfehlung: Limit menschenlesbar formatieren („5 GB"), analog zum vorhandenen `FormatBytes`.

## Fehlgeschlagene Tests

Keine — `test-results.md` (Runde 3): 286/286 bestanden.

## Sonstiges

- [ ] Stray-Datei `nul` (77 Bytes) im Repo-Root löschen (aus Code-Review Runde 3).
