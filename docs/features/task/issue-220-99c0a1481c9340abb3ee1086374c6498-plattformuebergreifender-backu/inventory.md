# Bestandsaufnahme: Plattformübergreifender Backup-Upload für sehr große Dateien (6 GB+)

Analyse des bestehenden Backup-Upload-/Import-Pfads (Admin-Seite `/admin/backups`, `BackupsController`, `VideoWebPlayerBackupFacade`, Backup-Einstellungen) bezogen auf die Anforderung aus `requirement.md` (Issue #220): Umstellung von `multipart/form-data` auf einen stream-/chunkbasierten `application/octet-stream`-Upload mit Resume-Fähigkeit.

## Zusammenfassung

- Der Upload läuft aktuell vollständig über einen klassischen `multipart/form-data`-Formularpost: `BackupsController.Upload` (`POST admin/backups/api/upload`, `VideoWebPlayer/Controllers/BackupsController.cs` Zeilen 64–89) liest `Request.Form.Files.GetFile("backupFile")` und ist mit `[DisableRequestSizeLimit]` und `[RequestFormLimits(MultipartBodyLengthLimit = long.MaxValue)]` annotiert. Es gibt **keine** Stream-Verarbeitung über `Request.Body`/`BodyReader`, keine Chunk-Endpunkte und kein Resume-Protokoll.
- Die fachliche Import-Logik existiert bereits vollständig in `VideoWebPlayerBackupFacade.ImportUploadAsync` (`VideoWebPlayer/Services/Backups/VideoWebPlayerBackupFacade.cs` Zeilen 86–150): Dateinamen-Validierung, Temp-Datei via `Path.GetTempPath()` + `FileOptions.DeleteOnClose`, `manifest.json`-Prüfung per `ZipArchive`, Kopiervorgang in den `BackupOptions.StoragePath` und Historieneintrag „Upload" über `BackupOperationHistoryService`. Eine Session-/Chunk-Verwaltung (z. B. `BackupUploadSessionService`) existiert **nicht**.
- Die Admin-Seite `VideoWebPlayer/Components/Pages/Admin/Backups.razor` rendert den Upload als multipart-`<form>` (Zeilen 240–250); das Antiforgery-Token (`antiforgeryRequestToken`, Zeilen 384/391–397) steht bereits als Request-Token zur Verfügung. Es gibt keine JS-Uploaddatei; unter `wwwroot/js/` existiert lediglich das `window.<name>`-Muster (z. B. `continueWatching.js`), `downloads.js` ist leer. `Components/App.razor` bindet die JS-Dateien global ein.
- `MaxUploadSizeBytes` ist in `BackupSettings` (Standard 5 GiB) persistiert und über `BackupSettingsService`/`IBackupOptionsProvider` administrierbar; es wird im Upload-Pfad nur in der UI angezeigt (`Backups.razor` Zeile 249) und serverseitig **nicht durchgesetzt**.
- `appsettings.Production.json` enthält eine `Kestrel:Endpoints`-Konfiguration, aber **kein** `Kestrel:Limits:MaxRequestBodySize`. Eine `web.config` oder IIS-/systemd-Deployment-Datei liegt nicht im Repo.
- Pipeline (`WebApplicationExtensions.UseVideoWebPlayer`): `UseBackups()` (msTools.Backup-Middleware), `RestoreInProgressMiddleware`, `UseAntiforgery` und `MapControllers` sind vorhanden; `AddAntiforgery` ist ohne eigenen Header-Namen registriert (Default `RequestVerificationToken`).

**Test-Ausgangszustand:** Alle ausgeführten Suiten sind fehlerfrei — `Category!=E2E`: 205/205 bestanden, `Category=E2E` (Playwright): 34/34 bestanden, `Category=Integration`: kein Test vorhanden, `MarkdownLinkCheck.Tests`: 6/6 bestanden. Keine nachgewiesenen Testfehler. Details und Nachweise in [Tests](inventory/tests.md).

## Details

- [Datenmodell](inventory/models.md) — `BackupSettings`, `BackupOperationHistory`, `BackupOptions`/`BackupDescriptor`/`BackupOperationResult` (msTools.Backup), `SettingsModel`
- [Logik](inventory/logic.md) — `BackupsController`, `VideoWebPlayerBackupFacade`, `BackupSettingsService`, Job-Services, Middleware, `Backups.razor`, DI-/Pipeline-Registrierung
- [Enums](inventory/enums.md) — `ManualBackupJobStatus`, `RestoreBackupJobStatus`, `BackupGeneration`, `BackupOperationType`
- [Interfaces](inventory/interfaces.md) — msTools.Backup-Contracts (`IBackupService`, `IBackupOptionsProvider` u. a.), `IBackgroundProcessingGate`
- [Tests](inventory/tests.md) — Test-Ausgangszustand, bestehende Testklassen und Hilfsmethoden
