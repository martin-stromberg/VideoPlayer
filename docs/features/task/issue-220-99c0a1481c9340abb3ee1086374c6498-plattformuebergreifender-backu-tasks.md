# Tasks: Plattformübergreifender Backup-Upload für sehr große Dateien (6 GB+)

| # | Bereich | Aufgabe | Status | Testnachweis |
|---|---------|---------|--------|--------------|
| 1 | Logik | `BackupUploadSession`-Datenmodellklasse anlegen (Id, FileName, TotalLength, ReceivedBytes, TempPath, FileStream, LastAccessUtc, SemaphoreSlim, IsCompleted; `IAsyncDisposable`) | Offen | — |
| 2 | Logik | `BackupUploadSessionService` anlegen: `BeginSessionAsync` mit Validierung (`Upload-Name`, `Upload-Length > 0`, `<= MaxUploadSizeBytes` via `IBackupOptionsProvider`, aufgelöst pro Aufruf über `IServiceScopeFactory`-Scope) und Temp-Datei `vwp-backup-upload-{id:N}.tmp` unter `Path.GetTempPath()` | Offen | — |
| 3 | Logik | `BackupUploadSessionService.GetSession` + `BackupUploadSession.AppendChunkAsync` implementieren (`FileStream.Seek`, limitiertes Kopieren aus `Request.Body`, `ReceivedBytes`-Tracking, Serialisierung über sessioneigenes `SemaphoreSlim`) | Offen | — |
| 4 | Logik | Lazy-Cleanup im `BackupUploadSessionService` implementieren (Sessions/Temp-Dateien älter `SessionTimeout` 24 h sowie verwaiste `vwp-backup-upload-*.tmp` beim Zugriff löschen) | Offen | — |
| 5 | Logik | `BackupUploadStatusResponse`-Record anlegen (`UploadId`, `FileName`, `UploadLength`, `UploadOffset`) | Offen | — |
| 6 | Konfiguration | `services.AddSingleton<BackupUploadSessionService>()` in `ServiceCollectionExtensions.AddVideoWebPlayerServices` registrieren | Offen | — |
| 7 | Logik | `BackupsController`: `Upload`/`RedirectWithUploadError` entfernen; `UploadChunk` (`POST upload/chunk`, `[DisableRequestSizeLimit]`, Header-Auswertung, Antworten 400/404/409/413/415/308/204/200) hinzufügen | Offen | — |
| 8 | Logik | `BackupsController`: `GetUploadStatus` (`GET upload/{uploadId:guid}`, 200 + `BackupUploadStatusResponse` oder 404) hinzufügen | Offen | — |
| 9 | Logik | `VideoWebPlayerBackupFacade.ImportUploadAsync` zu `ImportUploadFileAsync(tempFilePath, …)` ändern (in-place-Validierung + `File.Move(tempPath, targetPath, overwrite: true)` in `StoragePath` — bisheriges Überschreibverhalten von `FileMode.Create` beibehalten) und Abschluss-Aufruf im `BackupsController` mit `session.TempPath` verdrahten | Offen | — |
| 10 | UI | `wwwroot/js/backupUpload.js` anlegen (`window.backupUpload.start(inputEl, containerEl, token)`: `fetch()`/`Blob.slice()`-Chunks 128 MiB, überschreibbar über `window.backupUploadChunkSizeBytes` (wird beim Senden gelesen), Header-Protokoll, 308-/Status-Resume, `localStorage`-Persistenz, Fortschritts-Rendering, `AbortController`, `backupStatus`/`backupError`-Navigation) | Offen | — |
| 11 | UI | `<script src="js/backupUpload.js"></script>` in `Components/App.razor` einbinden | Offen | — |
| 12 | UI | `Backups.razor`: multipart-`<form>` durch File-Input (`@ref`), Upload-Button (`@onclick="StartUploadAsync"`) und Upload-Container (`@ref`) ersetzen; Hinweistext `MaxUploadSizeBytes` beibehalten | Offen | — |
| 13 | UI | `Backups.razor`: `IJSRuntime` + `PersistentComponentState` injizieren, `StartUploadAsync` implementieren, Token-Persistenz nach `Updates.razor`-Muster, `Dispose` um `_stateSubscription.Dispose()` erweitern | Offen | — |
| 14 | Konfiguration | `Kestrel:Limits:MaxRequestBodySize: 0` in `appsettings.Production.json` ergänzen und `builder.WebHost.ConfigureKestrel((context, options) => …)` in `Program.cs` einführen, das den Schlüssel explizit bindet (`<= 0` → `null` = unbegrenzt, `> 0` → Bytes; nicht gesetzt → Kestrel-Default) — Kestrel bindet `Limits` nicht selbst aus der Konfiguration | Offen | — |
| 15 | Tests | `BackupsControllerAuthorizationTests`: `UploadEndpoint_IsExposedAsUnlimitedServerSidePost` an `UploadChunk` anpassen (`upload/chunk`, `DisableRequestSizeLimit`, kein `RequestFormLimits`) | Offen | — |
| 16 | Tests | `BackupsControllerAuthorizationTests`: Test für `GetUploadStatus`-Route (`upload/{uploadId:guid}`) hinzufügen | Offen | — |
| 17 | Tests | `BackupUploadSessionServiceTests` anlegen: `BeginSessionAsync_CreatesTempFileAndRejectsInvalidInput` | Offen | — |
| 18 | Tests | `BackupUploadSessionServiceTests`: `BeginSessionAsync_RejectsLengthAboveMaxUploadSize` (Fake-`IBackupOptionsProvider` über Test-`ServiceCollection`/`IServiceScopeFactory`) | Offen | — |
| 19 | Tests | `BackupUploadSessionServiceTests`: `AppendChunk_WritesAtOffsetAndTracksReceivedBytes` | Offen | — |
| 20 | Tests | `BackupUploadSessionServiceTests`: `AppendChunk_RejectsMismatchedOffset` | Offen | — |
| 21 | Tests | `BackupUploadSessionServiceTests`: `AppendChunk_RejectsOverflowBeyondTotalLength` | Offen | — |
| 22 | Tests | `BackupUploadSessionServiceTests`: `GetSession_ReturnsNullForUnknownOrExpired` (mit `IncrementingTimeProvider`) | Offen | — |
| 23 | Tests | `BackupUploadSessionServiceTests`: `Cleanup_RemovesExpiredSessionsAndOrphanedTempFiles` | Offen | — |
| 24 | Tests | `BackupUploadSessionServiceTests`: `Sessions_SameFileName_DoNotCollide` | Offen | — |
| 25 | Tests | `VideoWebPlayerBackupFacadeTests` anlegen: `ImportUploadFileAsync` Erfolgs-/Fehlerfälle (gültige `.bak`-Temp-Datei mit `manifest.json` → Move in `StoragePath`, vorhandene gleichnamige Zieldatei → Überschreiben, fehlendes Manifest, leere Datei, `.bak`-Erzwingung, Historieneintrag) | Offen | — |
| 26 | E2E-Tests | `BackupUploadE2ETests` anlegen (Playwright, `[Trait("Category","E2E")]`, `WebApplicationFactory`+`UseKestrel`, `SeedAdminAsync` + `SeedRegularUserAsync`, `LoginAsync(email, password)`, `Backups:Path` auf Temp-Verzeichnis, Hilfsmethode `CreateValidBackupFileAsync` via `IBackupService.StoreAsync` + Kopie unter eigenem Upload-Namen außerhalb `StoragePath` für `SetInputFilesAsync` — Kollisionsvermeidung mit der archivierten Datei) | Offen | — |
| 27 | E2E-Tests | `BackupUpload_ValidFile_ShowsProgressAndImportsBackup` (Happy Path: Datei wählen, hochladen, Fortschritt, `backupStatus`, Archiv + Historie) | Offen | — |
| 28 | E2E-Tests | `BackupUpload_LargeFile_UsesMultipleChunks` (`window.backupUploadChunkSizeBytes` per `AddInitScriptAsync` klein gesetzt, mehrere Chunk-Requests, Erfolg) | Offen | — |
| 29 | E2E-Tests | `BackupUpload_Interrupted_ResumesFromServerOffset` (`Page.RouteAsync` bricht Chunk ab, Resume über Status-Endpunkt, Erfolg) | Offen | — |
| 30 | E2E-Tests | `BackupUpload_InvalidFile_ShowsError` (ZIP ohne `manifest.json` → `backupError`, kein Archiv-Eintrag) | Offen | — |
| 31 | E2E-Tests | `BackupUpload_ExceedingLimit_ShowsError` (kleines `Backups:MaxUploadSizeBytes` im Test-Host → 413/Fehlermeldung) | Offen | — |
| 32 | E2E-Tests | `BackupUpload_NonAdmin_SeesNoUploadControl` (eingeloggter regulärer Benutzer via `SeedRegularUserAsync` + `LoginAsync`: „Nicht autorisiert", kein Upload-Control) | Offen | — |
| 33 | Dokumentation | `docs/help/backups.md`: Abschnitt „Upload" auf Chunked-Upload mit Fortschritt/Resume aktualisieren, IIS-OutOfProcess-/`requestFiltering`-Hinweis und systemd-Hinweis ergänzen, veralteten „512 MB"-Hinweis korrigieren | Offen | — |
