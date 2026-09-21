# Code-Review

## Ergebnis

**Status:** Befunde vorhanden

## Befunde

### BackupsController.cs (BackupsController)

- **God-Methode** — `UploadChunk` (Z. 69–168, ca. 100 Zeilen) erledigt mehrere konzeptuell getrennte Aufgaben hintereinander: Antiforgery-Validierung, Content-Type-/Header-Validierung, Session-Anlage bzw. Session-Auflösung inkl. Konsistenzprüfung von `Upload-Name`/`Upload-Length`, Chunk-Anhang und abschließender Import.

  Empfehlung: Den Block zur Session-Ermittlung (Z. 90–137) in eine eigene private Methode auslagern (z. B. `ResolveOrBeginSessionAsync`), die Erfolg/Fehler als Ergebnisobjekt oder per `IActionResult`-Rückgabe kapselt.

### BackupUploadSessionService.cs (BackupUploadSessionService)

- **Kopplung/Effizienz** — `CleanupExpiredSessions()` wird bei jedem `BeginSessionAsync`- und jedem `GetSession`-Aufruf ausgeführt (Z. 42, 92) und enthält mit `CleanupOrphanedTempFiles` einen vollständigen Scan des Temp-Verzeichnisses (`Directory.GetFiles` + `File.GetLastWriteTimeUtc` + `_sessions.Values.Any(...)` je Datei, Z. 150–180). Da `GetSession` pro Chunk-Request aufgerufen wird, läuft bei einem 5-GiB-Upload mit 128-MiB-Chunks ~40-mal ein Verzeichnisscan — der Reentrancy-Guard (`_cleanupInProgress`) verhindert nur Parallelität, drosselt aber die Häufigkeit nicht.

  Empfehlung: Cleanup zeitlich drosseln (z. B. Zeitstempel des letzten Laufs merken und nur alle X Minuten ausführen) oder den Orphan-Scan in einen periodischen Hintergrundlauf auslagern.

### VideoWebPlayerBackupFacade.cs (VideoWebPlayerBackupFacade)

- **Fehlerbehandlung/Laufzeitverhalten** — `File.Move(tempFilePath, targetPath, overwrite: true)` (Z. 128) ist synchron. Liegt `Path.GetTempPath()` auf einem anderen Volume als der konfigurierte Speicherpfad (z. B. Temp auf `C:\`, Backup-Speicher auf `D:\` oder Netzlaufwerk), kopiert `File.Move` die komplette Datei synchron — bei bis zu 5 GiB blockiert das den Request-Thread und der `cancellationToken` wird nicht beachtet.

  Empfehlung: Temp-Datei im selben Volume wie den Speicherpfad anlegen oder auf asynchrones Kopieren (`CopyToAsync` mit `cancellationToken`) plus `File.Delete` umstellen.

### Backups.razor

- **Namenskonvention** — Das neue Feld `_stateSubscription` (Z. 367) weicht von der Konvention der Datei ab: alle anderen privaten Felder sind camelCase ohne Unterstrich (`backupStatusTimer`, `antiforgeryRequestToken`, `backupFileInput`, `uploadContainer`).

  Empfehlung: In `stateSubscription` umbenennen.

### Program.cs

- **Fehlerbehandlung** — Ein nicht-numerischer Wert für `Kestrel:Limits:MaxRequestBodySize` (Z. 39–42) führt durch den `long.TryParse`-Fehlschlag still zu `MaxRequestBodySize = null` (unbegrenzt). Ein Konfigurationsfehler wie `"5GB"` deaktiviert das Limit unbemerkt, statt als Fehler sichtbar zu werden.

  Empfehlung: Bei vorhandenem, aber nicht parsebarem Wert eine Warnung loggen oder den Start mit einer aussagekräftigen Exception abbrechen.

### BackupUploadSessionServiceTests.cs (BackupUploadSessionServiceTests)

- **Testqualität** — Mehrere Testmethoden prüfen mehrere fachliche Fälle in einem Test: `BeginSessionAsync_CreatesTempFileAndRejectsInvalidInput` (Z. 13–43) deckt Erfolg plus vier unterschiedliche Ablehnungsfälle ab; `GetSession_ReturnsNullForUnknownOrExpired` (Z. 130–151) prüft unbekannte und abgelaufene Sessions; `Cleanup_RemovesExpiredSessionsAndOrphanedTempFiles` (Z. 154–184) prüft Session-Ablauf und Orphan-Löschung gemeinsam.

  Empfehlung: In getrennte Tests pro fachlichem Fall aufteilen (z. B. `BeginSessionAsync_RejectsEmptyFileName`, `BeginSessionAsync_RejectsPathInFileName` usw.).

## Hinweise

- `RaiseUiActionRequested` wird im Branch nicht verwendet (kein Vorkommen im Repository) — der Lifecycle-Prüfpunkt zu UI-Action-Handlern ist damit ohne Befund erfüllt.
- `dotnet build` für `VideoWebPlayer` und `VideoWebPlayer.Tests`: erfolgreich, 0 Warnungen, 0 Fehler.

## Geprüfte Dateien

- `VideoWebPlayer/Controllers/BackupsController.cs`
- `VideoWebPlayer/Services/Backups/BackupUploadSessionService.cs` (neu)
- `VideoWebPlayer/Services/Backups/VideoWebPlayerBackupFacade.cs`
- `VideoWebPlayer/Components/Pages/Admin/Backups.razor`
- `VideoWebPlayer/Components/App.razor`
- `VideoWebPlayer/wwwroot/js/backupUpload.js` (neu)
- `VideoWebPlayer/Program.cs`
- `VideoWebPlayer/Extensions/ServiceCollectionExtensions.cs`
- `VideoWebPlayer/VideoWebPlayer.csproj`
- `VideoWebPlayer/appsettings.Production.json`
- `VideoWebPlayer.Tests/BackupUploadE2ETests.cs` (neu)
- `VideoWebPlayer.Tests/BackupsControllerAuthorizationTests.cs`
- `VideoWebPlayer.Tests/Services/Backups/BackupUploadSessionServiceTests.cs` (neu)
- `VideoWebPlayer.Tests/Services/Backups/VideoWebPlayerBackupFacadeTests.cs` (neu)
- `.gitignore`
- `docs/help/backups.md`
