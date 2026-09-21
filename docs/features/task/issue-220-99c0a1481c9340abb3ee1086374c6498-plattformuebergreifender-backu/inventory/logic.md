# Logik

## `BackupsController`
Datei: `VideoWebPlayer/Controllers/BackupsController.cs`

`[ApiController]`, `[Authorize(Policy = "AdminOnly")]`, Basisroute `[Route("admin/backups/api")]`. Abhängigkeiten per Konstruktor: `IBackupService`, `ManualBackupJobService`, `VideoWebPlayerBackupFacade`, `IAntiforgery`, `ILogger<BackupsController>`.

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `Create(CancellationToken)` | `public` | `POST create` (Zeilen 45–59): validiert Antiforgery via `_antiforgery.ValidateRequestAsync(HttpContext)`, startet manuelles Backup über `_manualBackupJobs.StartManualBackup(userId)`, Redirect auf `/admin/backups?backupStatus=...` |
| `Upload(CancellationToken)` | `public` | `POST upload` (Zeilen 64–89): `[DisableRequestSizeLimit]` + `[RequestFormLimits(MultipartBodyLengthLimit = long.MaxValue)]`; Antiforgery-Validierung; liest `Request.Form.Files.GetFile("backupFile")` (`IFormFile`), öffnet `file.OpenReadStream()` und ruft `_backupFacade.ImportUploadAsync(stream, file.FileName, userId, cancellationToken)`; Redirect mit `backupStatus`- bzw. `backupError`-Query-Parameter. **multipart/form-data-Pfad — kein Streaming über `Request.Body`, keine Chunk-/Resume-Endpunkte vorhanden** |
| `Download(string fileName, CancellationToken)` | `public` | `GET download/{fileName}` (Zeilen 94–99): `_backupService.OpenBackupReadAsync`, `File(stream, "application/zip", fileName)` |
| `RedirectWithUploadError(string message)` | `private static` | Redirect auf `/admin/backups?backupError=...` (Zeilen 101–102) |

## `VideoWebPlayerBackupFacade`
Datei: `VideoWebPlayer/Services/Backups/VideoWebPlayerBackupFacade.cs`

Fachliche Fassade; Abhängigkeiten: `IBackupService`, `IBackupDataSource`, `VideoWebPlayerBackupDataFactory`, `IBackupOptionsProvider`, `IHostEnvironment`, `BackupSettingsService`, `BackupOperationHistoryService`, `ILogger`.

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `ListBackupsAsync` | `public` | Delegiert an `_backupService.ListBackupsAsync` |
| `CreateManualBackupAsync` | `public` | Zeilen 54–81: `_dataSource.GetBackupDataAsync` → `_backupService.StoreAsync` (`manual-{yyyyMMdd-HHmmss}`, `BackupGeneration.Manual`) → `ApplyRetentionAsync` → Historieneintrag `"Backup"` |
| `ImportUploadAsync(Stream, string fileName, string? userId, CancellationToken)` | `public` | Zeilen 86–150 — **zentraler Import-Einstieg**: `Path.GetFileName`-Validierung, `.bak`-Endung erzwingen; `StoragePath` aus `_optionsProvider.GetOptionsAsync` (Fallback `Data/Backups`, relativ zum `ContentRootPath`); Temp-Datei `vwp-backup-import-{Guid:N}.tmp` in `Path.GetTempPath()` mit `FileMode.CreateNew`/`FileOptions.DeleteOnClose`; `stream.CopyToAsync`; leere Datei → Fehler; `ZipArchive`-Prüfung auf `manifest.json` (`InvalidDataException` → „kein gültiges Backup"); Kopie in `Path.Combine(fullStoragePath, safeFileName)` via `FileMode.Create`; Descriptor-Ermittlung via `ListBackupsAsync`; Historieneintrag `"Upload"` bei Erfolg; `catch` → `LogError` + `Failure`. `MaxUploadSizeBytes` wird **nicht** geprüft |
| `RestoreAsync` | `public` | Zeilen 155–183: `confirmRestore`-Pflicht, `_backupService.RestoreAsync` mit `VideoWebPlayerBackupDataFactory` (`UserId`/`Progress`), Historieneintrag `"Restore"` (auch bei Fehler) |
| `DeleteAsync` | `public` | Zeilen 188–194: `_backupService.DeleteBackupAsync`, Historieneintrag `"Löschen"` |
| `GetSettingsAsync` / `UpdateSettingsAsync` | `public` | Delegiert an `BackupSettingsService` |
| `GetHistoryAsync` | `public` | `_historyService.GetLatestAsync(25, ...)` |

## `BackupSettingsService`
Datei: `VideoWebPlayer/Services/Backups/BackupSettingsService.cs`

Implementiert `IBackupOptionsProvider`. Konstanten: `UpdateSettingsRowId = 1`, `PreviousDefaultMaxUploadSizeBytes = 512 MiB`, `DefaultMaxUploadSizeBytes = 5 GiB`, `DefaultProgramUpdateRetentionCount = 5`.

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `GetOrCreateAsync` | `public` | Zeilen 32–63: lädt/erstellt `BackupSettings`-Zeile aus `IConfiguration`-Sektion `Backups:*`; hebt persistiertes Legacy-Limit (512 MiB) auf konfigurierten Default an |
| `UpdateAsync` | `public` | Zeilen 68–82: persistiert Einstellungen; `MaxUploadSizeBytes` mindestens 1 MiB |
| `GetOptionsAsync` | `public` | Zeilen 85–110: mappt `BackupSettings` auf `BackupOptions` inkl. `Schedule`/`Retention` und `ProgramUpdateCount` aus `UpdateSettings` |
| `GetProgramUpdateRetentionCountAsync` | `private` | `AutoUpdate:Backup:RetainedBackupCount` bzw. `UpdateSettings.RetainedUpdateBackupCount` |
| `GetConfiguredMaxUploadSizeBytes` | `private` | `Backups:MaxUploadSizeBytes` aus `IConfiguration`, Default 5 GiB |

## `BackupOperationHistoryService`
Datei: `VideoWebPlayer/Services/Backups/BackupOperationHistoryService.cs`

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `AddAsync(string operation, BackupOperationResult result, string? userId, DateTime startedAtUtc, CancellationToken)` | `public` | Persistiert `BackupOperationHistory`-Zeile |
| `GetLatestAsync(int count = 25, CancellationToken)` | `public` | Neueste Historieneinträge (`AsNoTracking`, absteigend nach `StartedAtUtc`) |

## `ManualBackupJobService`
Datei: `VideoWebPlayer/Services/Backups/ManualBackupJobService.cs`

Singleton; führt manuelle Backups außerhalb der Request-Lebensdauer aus (`Task.Run` + eigener DI-Scope).

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `GetSnapshot` | `public` | Aktueller `ManualBackupJobSnapshot` (thread-sicher via `lock`) |
| `StartManualBackup(string? userId)` | `public` | Verweigert parallele Jobs (`_current.IsActive`), queued `RunBackupAsync` |
| `RunBackupAsync` / `SetSnapshot` | `private` | Ruft `VideoWebPlayerBackupFacade.CreateManualBackupAsync` im neuen Scope; Statusübergänge Queued→Running→Succeeded/Failed |

## `RestoreBackupJobService`
Datei: `VideoWebPlayer/Services/Backups/RestoreBackupJobService.cs`

Singleton; analoges Job-Muster für Restore inkl. zweistufigem Fortschritt.

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `GetSnapshot` | `public` | Aktueller `RestoreBackupJobSnapshot` |
| `StartRestore(string fileName, string? userId, bool confirmRestore)` | `public` | `ArgumentException` bei leerem Namen; verweigert parallele Restores; queued `RunRestoreAsync` |
| `RunRestoreAsync` / `SetSnapshot` / `SetProgress` | `private` | Ruft `VideoWebPlayerBackupFacade.RestoreAsync` mit `RestoreJobProgress` (`IProgress<BackupRestoreProgress>`) |
| `RestoreJobProgress` (nested) | `private` | `IProgress<BackupRestoreProgress>`-Adapter → `SetProgress` |

## `RestoreInProgressMiddleware`
Datei: `VideoWebPlayer/Services/Backups/RestoreInProgressMiddleware.cs`

Middleware (`app.UseMiddleware<RestoreInProgressMiddleware>()`); blockiert bei aktivem Restore alle Routen außer `/admin/backups`, `/_blazor`, `/_framework`, `/css`, `/js`, `/bootstrap`, `/images`, `/favicon.png` — Upload-Endpunkte unter `/admin/backups/api/*` sind während eines Restores also erreichbar. Antwortet mit 503 + `RetryAfter: 5` (JSON für API-Requests, HTML-Seite sonst). `RestoreInProgressResponse`-Record am Dateiende.

## `Backups.razor` (Komponenten-Logik)
Datei: `VideoWebPlayer/Components/Pages/Admin/Backups.razor` (`@page "/admin/backups"`, `@rendermode InteractiveServer`)

Relevante Mechanismen:

- **Upload-Formular** (Zeilen 240–250): `<form method="post" action="/admin/backups/api/upload" enctype="multipart/form-data">` mit hidden `__RequestVerificationToken`, `<input type="file" name="backupFile" accept=".bak">`, Hinweistext „Maximal @FormatBytes(settingsModel.MaxUploadSizeBytes)". **Kein JS-Upload, kein `IJSRuntime`-Inject in dieser Komponente.**
- **Antiforgery-Token** (Zeilen 384, 391–397): `CreateAntiforgeryRequestToken()` ruft `Antiforgery.GetAndStoreTokens(httpContext).RequestToken` über `IHttpContextAccessor` ab — Token steht als `antiforgeryRequestToken` für Header-Übergabe an JS bereit.
- **Statusmeldungen** (Zeilen 399–406): `ApplyQueryMessages` liest `backupStatus`/`backupError` aus der URL (`QueryHelpers`) — bisheriges Redirect-Muster des Controllers.
- **Polling** (Zeilen 433–472): `PeriodicTimer` (3 s) aktualisiert `manualBackupJob`/`restoreJob` über `ManualBackupJobs.GetSnapshot()`/`RestoreJobs.GetSnapshot()` und lädt bei Abschluss Liste + Historie neu.
- **Operationen**: `SelectRestore`/`RestoreAsync` (über `RestoreJobs.StartRestore`), `SelectDelete`/`DeleteAsync` (über `BackupFacade.DeleteAsync`), `SaveSettingsAsync` (über `BackupFacade.UpdateSettingsAsync`), `ReloadAsync`, `RunOperationAsync` (Busy-/Fehlerbehandlung), `ApplyResult`, `FormatBytes` (B/KB/MB/GB).
- `isAdmin`-Prüfung via `IsAdmin`-Claim in `OnInitializedAsync`; `Dispose` stoppt Timer/CTS.

## Pipeline und DI-Registrierung

### `WebApplicationExtensions.UseVideoWebPlayer`
Datei: `VideoWebPlayer/Extensions/WebApplicationExtensions.cs` (Zeilen 38–93)

Reihenfolge: Host-Header-Check → `UseMigrationsEndPoint`/`UseExceptionHandler`+`UseHsts` → `UseHttpsRedirection` → `UseWhitelistIp` → `UseStaticFiles` → `UseAuthentication`/`UseAuthorization` → `UseBackups()` (msTools.Backup-Middleware) → `RestoreInProgressMiddleware` → `FirstUserRedirectMiddleware` → `UseAntiforgery` → `MapRazorComponents<App>().AddInteractiveServerRenderMode()` → `MapControllers()` → `MapHub<MediaUpdateHub>` → `MapAdditionalIdentityEndpoints`. Keine uploadbezogenen Limits in der Pipeline.

### `ServiceCollectionExtensions.AddVideoWebPlayerServices`
Datei: `VideoWebPlayer/Extensions/ServiceCollectionExtensions.cs`

- `AddAntiforgery` (Zeilen 142–146): nur Cookie-Optionen (`SameSite=Lax`, `SecurePolicy=SameAsRequest`); **kein `HeaderName` gesetzt → Default `RequestVerificationToken`**.
- Backup-DI (Zeilen 237–250): `services.AddBackups(configuration.GetSection("Backups"))` (msTools.Backup), `IBackupDataSource` → `VideoWebPlayerBackupDataSource`, `VideoWebPlayerBackupDataFactory` + `IBackupDataFactory`, `IBackgroundProcessingGate` → `BackgroundProcessingGate` (Singleton), `IBackupRestoreGuard` → `VideoWebPlayerBackupRestoreGuard`, `BackupSettingsService` + `IBackupOptionsProvider`, `BackupOperationHistoryService`, `IBackupOperationHistory` → `InMemoryBackupOperationHistory`, `IAutomaticBackupRunner` → `VideoWebPlayerAutomaticBackupRunner`, `VideoWebPlayerBackupFacade` (Scoped), `ManualBackupJobService`/`RestoreBackupJobService` (Singletons).
- Keine `FormOptions`-Konfiguration im gesamten Projekt (Suche nach `FormOptions`/`RequestFormLimits`/`MaxRequestBodySize`: nur `BackupsController` + Test + Anforderungsdokumente).

## Konfigurationsdateien

- `VideoWebPlayer/appsettings.json` (Zeilen 5–14): `Backups:Path`, `AutomaticBackupsEnabled`, `MaxUploadSizeBytes: 5368709120` (5 GiB), `Retention:{Son,Father,Grandfather}Count`.
- `VideoWebPlayer/appsettings.Production.json` (Zeilen 18–24): `Kestrel:Endpoints:Http:Url = http://*:5002`; **kein `Kestrel:Limits`-Block vorhanden**.
- `VideoWebPlayer/appsettings.Development.json`: keine backup-/kestrelbezogenen Einstellungen.
- Keine `web.config`, keine systemd-Unit-Datei und keine IIS-Deployment-Dokumentation im Repo.

## Nicht vorhanden (für die Anforderung relevant)

- Keine Upload-Session-/Chunk-Verwaltung (kein `BackupUploadSessionService` o. ä.), keine Offset-/Resume-Logik, keine 308-Antworten.
- Keine JS-Datei für den Upload unter `VideoWebPlayer/wwwroot/js/` (vorhanden: `continueWatching.js`, `downloads.js` — leer, `layoutNavigation.js`, `mediaContextMenu.js`, `scroll.js`, `statusTicker.js`; Muster: `window.<name>`-Objekte, eingebunden in `Components/App.razor` Zeilen 12–16).
- `IJSRuntime` wird in `Backups.razor` nicht injiziert (Muster vorhanden in `VideoPlayer.razor`, `NavMenu.razor`, `Security.razor` u. a.).
- Keine Hash-Validierung beim Upload.
