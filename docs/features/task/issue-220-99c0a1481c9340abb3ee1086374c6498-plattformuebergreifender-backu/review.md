# Plan-Review

## Ergebnis

**Status:** Vollständig umgesetzt

## Umgesetzte Planelemente

### Neue Klassen / Dateien

- [x] `BackupUploadSessionService` (Singleton, `VideoWebPlayer/Services/Backups/BackupUploadSessionService.cs` Z. 9–194) — angelegt; `ConcurrentDictionary<Guid, BackupUploadSession>`-Registry, `BeginSessionAsync` (Validierung `Upload-Name`/`Upload-Length`/`MaxUploadSizeBytes` via `IBackupOptionsProvider` in `IServiceScopeFactory`-Scope, Z. 40–85), `GetSession` (Z. 90–98), `CompleteSessionAsync`/`AbortSessionAsync`, Temp-Datei `vwp-backup-upload-{id:N}.tmp` unter `Path.GetTempPath()`, Lazy-Cleanup inkl. verwaister Temp-Dateien (Z. 121–193), `SessionTimeout` = 24 h, `TimeProvider`-Abhängigkeit
- [x] `BackupUploadSession` (`BackupUploadSessionService.cs` Z. 199–337) — `sealed`, `IAsyncDisposable`/`IDisposable`; `Id`, `FileName`, `TotalLength`, `ReceivedBytes`, `TempPath`, `Stream` (FileStream), `LastAccessUtc`, `IsCompleted`, `SemaphoreSlim` (`WriteLock`); `AppendChunkAsync` mit `FileStream.Seek`, limitiertem Kopieren, `ReceivedBytes`-Fortschaltung, Offset-Mismatch → `ResumeRequired`, Overflow → `Rejected`, Kürzen auf letzten konsistenten Offset bei vorzeitigem Body-Ende
- [x] `BackupUploadStatusResponse` (Record, `BackupUploadSessionService.cs` Z. 426) — `UploadId`, `FileName`, `UploadLength`, `UploadOffset`
- [x] `backupUpload.js` (`VideoWebPlayer/wwwroot/js/backupUpload.js`) — `window.backupUpload.start(inputEl, containerEl, token)`; `fetch()`/`Blob.slice()`-Chunks, `CHUNK_SIZE` 128 MiB überschreibbar über `window.backupUploadChunkSizeBytes` (wird beim Senden gelesen, Z. 7–10/146), Header-Protokoll inkl. `RequestVerificationToken`, 308-/404-/Status-Resume via `queryServerOffset`, `localStorage`-Persistenz `vwp-backup-upload:{name}:{size}:{lastModified}`, Fortschritts-Rendering + Abbrechen via `AbortController`, Abschluss-Navigation mit `backupStatus`/`backupError`
- [x] `BackupUploadSessionServiceTests` (`VideoWebPlayer.Tests/Services/Backups/`) — alle 8 geplanten Tests vorhanden (`BeginSessionAsync_CreatesTempFileAndRejectsInvalidInput`, `BeginSessionAsync_RejectsLengthAboveMaxUploadSize`, `AppendChunk_WritesAtOffsetAndTracksReceivedBytes`, `AppendChunk_RejectsMismatchedOffset`, `AppendChunk_RejectsOverflowBeyondTotalLength`, `GetSession_ReturnsNullForUnknownOrExpired` mit `IncrementingTimeProvider`, `Cleanup_RemovesExpiredSessionsAndOrphanedTempFiles`, `Sessions_SameFileName_DoNotCollide`)
- [x] `VideoWebPlayerBackupFacadeTests` (`VideoWebPlayer.Tests/Services/Backups/`) — 5 Tests: Move+Historie, Überschreiben gleichnamiger Zieldatei, fehlendes `manifest.json`, leere Datei, `.bak`-Erzwingung
- [x] `BackupUploadE2ETests` (`VideoWebPlayer.Tests/`, `[Trait("Category", "E2E")]`, Playwright) — Fixture (`WebApplicationFactory` + `UseKestrel`, `Backups:Path`/`Backups:MaxUploadSizeBytes` per `UseSetting`), `SeedAdminAsync`, `SeedRegularUserAsync` (regulärer Benutzer ohne `IsAdmin`-Claim), `LoginAsync(email, password)`, `CreateValidBackupFileAsync` (echte `.bak` via `IBackupService.StoreAsync`, Kopie unter eigenem Namen außerhalb `StoragePath`); alle 6 geplanten Szenarien vorhanden: `BackupUpload_ValidFile_ShowsProgressAndImportsBackup`, `BackupUpload_LargeFile_UsesMultipleChunks`, `BackupUpload_Interrupted_ResumesFromServerOffset`, `BackupUpload_InvalidFile_ShowsError`, `BackupUpload_ExceedingLimit_ShowsError`, `BackupUpload_NonAdmin_SeesNoUploadControl`

### Geänderte bestehende Klassen/Dateien

- [x] `BackupsController` (`VideoWebPlayer/Controllers/BackupsController.cs`) — `BackupUploadSessionService` per Konstruktor injiziert (Z. 33); `UploadChunk` mit `[HttpPost("upload/chunk")]` + `[DisableRequestSizeLimit]` (Z. 67–168): Antiforgery-Prüfung, `application/octet-stream`-Pflicht (415), Header-Auswertung, 400/404/409/413/308/204/200, Abschluss-Import; `GetUploadStatus` mit `[HttpGet("upload/{uploadId:guid}")]` (Z. 173–182) → 200 + `BackupUploadStatusResponse` + `Upload-Offset`-Header bzw. 404; `Upload`/`RedirectWithUploadError`/`Request.Form`/`[RequestFormLimits]` vollständig entfernt; `[ApiController]`, `[Authorize(Policy = "AdminOnly")]`, `Create`, `Download` unverändert
- [x] `VideoWebPlayerBackupFacade` (`VideoWebPlayer/Services/Backups/VideoWebPlayerBackupFacade.cs` Z. 86–144) — `ImportUploadFileAsync(string tempFilePath, string fileName, string? userId, CancellationToken)` ersetzt `ImportUploadAsync`: in-place-Validierung (`.bak`-Erzwingung, `manifest.json` via `ZipArchive`, nicht leer), `File.Move(tempPath, targetPath, overwrite: true)` in `StoragePath`, Historieneintrag `"Upload"`, Temp-Datei bleibt bei Fehlschlag erhalten
- [x] `Backups.razor` (`VideoWebPlayer/Components/Pages/Admin/Backups.razor`) — multipart-`<form>` ersetzt durch `<input type="file" @ref="backupFileInput" accept=".bak">` + Button `@onclick="StartUploadAsync"` + Upload-Container `@ref="uploadContainer"` (Z. 243–249), Hinweistext `MaxUploadSizeBytes` beibehalten; `IJSRuntime` + `PersistentComponentState` injiziert (Z. 13–14); `StartUploadAsync` ruft `backupUpload.start` (Z. 410–428); `CreateAntiforgeryRequestToken` mit `TryTakeFromJson`/`PersistAsJson("antiforgeryToken")` + `PersistingComponentStateSubscription` (Z. 367, 392–408); `Dispose` ruft `_stateSubscription.Dispose()` (Z. 725)
- [x] `App.razor` — `<script src="js/backupUpload.js"></script>` im `<head>` (Z. 17)
- [x] `ServiceCollectionExtensions` — `services.AddSingleton<BackupUploadSessionService>()` im Backup-Block (Z. 251)
- [x] `VideoWebPlayer.csproj` — `<AspNetCoreHostingModel>OutOfProcess</AspNetCoreHostingModel>` gesetzt
- [x] `Program.cs` — `builder.WebHost.ConfigureKestrel((context, options) => …)` (Z. 34–43): liest `Kestrel:Limits:MaxRequestBodySize`, `<= 0`/nicht parsebar → `null` (unbegrenzt), `> 0` → Bytes, Schlüssel nicht gesetzt → Default unverändert
- [x] `appsettings.Production.json` — `"Kestrel": { "Limits": { "MaxRequestBodySize": 0 } }` ergänzt, `Endpoints` unverändert
- [x] `BackupsControllerAuthorizationTests` — `UploadEndpoint_IsExposedAsUnlimitedServerSidePost` ersetzt durch `UploadChunkEndpoint_IsExposedAsUnlimitedStreamPost` (Template `upload/chunk`, `DisableRequestSizeLimitAttribute` vorhanden, `RequestFormLimitsAttribute` abwesend); `GetUploadStatusEndpoint_IsExposedAsGet` neu
- [x] `docs/help/backups.md` — Abschnitt „Upload" auf Chunked-Upload/Fortschritt/Wiederaufnahme umgestellt, serverseitige Durchsetzung von `MaxUploadSizeBytes` dokumentiert, „512 MB"-Hinweis auf 5 GiB korrigiert, IIS-OutOfProcess/`requestFiltering`/`maxAllowedContentLength`- sowie systemd-Hinweis ergänzt

### Build-Status

`dotnet build VideoWebPlayer.Tests/VideoWebPlayer.Tests.csproj`: 0 Fehler (140 Warnungen, sämtlich vorhandene BL0008-Hinweise in Account-Pages, nicht durch diese Änderung verursacht).

## Offene Aufgaben

Keine.

## Hinweise

- Die Offset-Prüfung (`Upload-Offset != ReceivedBytes` → 308) liegt — abweichend von der wörtlichen Plan-Reihenfolge — innerhalb von `BackupUploadSession.AppendChunkAsync` (Ergebnis `ResumeRequired`), der Controller bildet sie korrekt auf `308` + `Upload-Offset`-Header ab; funktional deckungsgleich mit dem Plan.
- `AppendChunkAsync` hat gegenüber dem Plan einen zusätzlichen `offset`-Parameter (Signatur `AppendChunkAsync(long offset, Stream source, long contentLength, CancellationToken)`); die Offset-Validierung ist damit in der Session gekapselt.
- `.gitignore` wurde ergänzt (`!VideoWebPlayer.Tests/Services/Backups/`), damit die neue Testdatei nicht vom bestehenden `Backup*`-Muster verschluckt wird — unterstützende, nicht geplante Änderung.
- Die Tasks-Datei (`…-tasks.md`) wurde aktualisiert: alle 33 Aufgaben auf `Erledigt` mit Testnachweis.
