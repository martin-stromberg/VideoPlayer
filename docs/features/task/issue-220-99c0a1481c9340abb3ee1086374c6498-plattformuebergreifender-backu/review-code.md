# Code-Review

## Ergebnis

**Status:** Befunde vorhanden

## Befunde

### BackupUploadSessionServiceTests_*.cs (Test-Isolation)

- **Testqualität** — Tests mit gefakter Zeit (`IncrementingTimeProvider` + `Advance(TimeSpan.FromHours(25))`) schieben den Cleanup-Cutoff in die reale Zukunft: `cutoff = fakeNow - 24h ≈ realNow + 1h`. `CleanupOrphanedTempFiles` (`BackupUploadSessionService.cs`, Z. 209–239) behandelt damit **alle** `vwp-backup-upload-*.tmp` im gemeinsamen `Path.GetTempPath()` als verwaist und löscht sie, sofern sie nicht in `_sessions` der eigenen Service-Instanz registriert sind. Da xUnit Testklassen ohne `CollectionBehavior`-Überschreibung parallel ausführt, können die betroffenen Tests (`GetSession_WhenSessionExpired_RemovesSessionAndTempFile`, `GetSession_WhenSessionExpired_ReturnsNull`, `GetSession_ExpiredSessionWhoseWriteLockIsHeld_IsNotDisposed`) Temp-Dateien parallel laufender Tests anderer Klassen löschen — unter Windows bei offenem `FileShare.None`-Stream zwar durch Sharing-Violation abgefedert, unter Linux jedoch erfolgreich (Unlink) → Schreibzugriffe gehen in einen gelöschten Inode → flaky fehlschlagende Tests. Auch die von `BeginSessionAsync_WhenOrphanedTempFileIsOld_DeletesOrphan` angelegte, bereits geschlossene Orphan-Datei kann von einem parallelen Fake-Time-Cleanup gelöscht werden.

  Empfehlung: Temp-Verzeichnis der Session-Service-Instanz isolierbar machen — z. B. interner Konstruktorparameter/`internal`-Property mit Default `Path.GetTempPath()`, den die Tests auf ein eindeutiges Verzeichnis pro Instanz setzen (bessere Lösung als reine Serienausführung, da es auch die Prod-Hygiene verbessern würde, ein Unterverzeichnis wie `vwp-backup-uploads/` allein reicht dagegen nicht — es bleibt instanzübergreifend geteilt). Alternativ: alle `BackupUploadSessionServiceTests_*`-Klassen in eine gemeinsame xUnit-`[Collection]` packen.

### backupUpload.js (window.backupUpload)

- **Laufzeitverhalten** — Im `activeRun`-Guard (Z. 229–233) ruft `start` bei laufendem Upload `showMessage(containerEl, 'Es läuft bereits ein Upload. …')` auf. `showMessage` leert den kompletten Container (`innerHTML = ''`, Z. 33). Ist `containerEl` dasselbe Element wie `activeRun.containerEl`, werden dabei Fortschrittsbalken und Abbrechen-Button des laufenden Uploads zerstört — der Upload läuft weiter, ist aber unsichtbar und nicht mehr abbrechbar. Erreichbar, sobald Blazor den per `setControlsDisabled` deaktivierten Button nach einem Re-Render wieder aktiviert (z. B. wenn `IsRestoreActive` während des Uploads auf `true` und zurück auf `false` wechselt — `disabled` ist an `isBusy || IsRestoreActive` gebunden, `Backups.razor` Z. 245).

  Empfehlung: Den Hinweis nicht über `showMessage` in den Container schreiben, sondern in die UI des laufenden Runs (z. B. `ui` auf dem `run`-Objekt ablegen und `ui.text` aktualisieren) oder bei `activeRun.containerEl === containerEl` den vorhandenen Status-Text ergänzen, ohne den Container zu leeren.

## Hinweise

- Alle 3 Befunde aus Runde 2 wurden bearbeitet und sind korrekt umgesetzt:
  - **Dispose-Race:** `WriteLock` wird bewusst nicht disposed (kommentiert, Z. 403–405), `_isDisposed`/`_isCompleted` sind `volatile`, `GetSession` prüft `IsDisposed` nach `Touch()`, `CleanupExpiredSessions` re-checkt `LastAccessUtc` unter dem gehaltenen `WriteLock` und trägt die Session per `TryAdd` wieder ein. Wartende Requests landen sauber im `Rejected`-Pfad statt in `ObjectDisposedException`. `CleanupLockTimeout` (250 ms) ist ein begrenzter synchroner `SemaphoreSlim.Wait` ohne weitere gehaltene Locks — keine Deadlock-Gefahr; im worst case blockiert der Request-Thread N×250 ms pro abgelaufener Session (Cleanup ist über `CleanupInterval` gedrosselt).
  - **`MoveTempFileAsync`:** `.part`-Staging + garantiertes Rename, `FileShare.None`, Catch mit `_logger.LogWarning` statt leerem Block. `IsSameFileSystem`/`GetMountPoint` nutzen `DriveInfo.GetDrives()` mit Longest-Prefix-Match inkl. Boundary-Prüfung — unter Linux korrekt (z. B. `/tmp`-tmpfs vs. `/` werden unterschieden); bei nicht auflösbarem Mount oder IOException fällt es auf den sicheren Copy-Pfad zurück.
  - **`backupUpload.js`:** `activeRun`-Objekt mit `done`-Promise für die Übernahme verwaister Runs, MutationObserver wird im `finally` disconnected (kein Leak), Redirect nur bei `containerEl.isConnected`.
- **XHR-vs-fetch-Parität geprüft:** `postChunk` setzt alle Header identisch (`RequestVerificationToken` inklusive), same-origin Cookies werden bei XHR wie bei fetch (`credentials: 'same-origin'`-Default) gesendet, kein Timeout bei beiden, Abort über `signal`-Listener + `xhr.abort()`. Der 308-Resume-Pfad funktioniert, weil der Server kein `Location`-Header sendet — sowohl fetch als auch XHR liefern Redirect-Antworten ohne `Location` unverändert aus.
- **`AbortUpload` (DELETE):** Klassenweites `[Authorize(Policy = "AdminOnly")]` + explizite `ValidateRequestAsync` → Authorization/Antiforgery korrekt.
- **Kleinigkeiten, bewusst nicht als Befund gewertet:**
  - `await delay(500 * attempts)` ist nicht abort-sensitiv — die Übernahme eines verwaisten Runs (`await activeRun.done`) kann im Backoff wenige Sekunden blockieren.
  - Abort-vs.-Finish-Race: Ein `DELETE` kann die Temp-Datei löschen, während der finale Chunk importiert wird — Ergebnis ist entweder sauberer Fehler („Datei ist leer") oder der Import gewinnt; beides vertretbar.
  - Schlägt `File.Delete(sourcePath)` nach erfolgreichem `File.Move` fehl, wird der Import als fehlgeschlagen gemeldet, obwohl die `.bak` bereits im Speicherpfad liegt.
  - Stray-Datei `nul` (77 Bytes) im Repo-Root — Artefakt einer `> nul`-Umleitung unter Git Bash, sollte gelöscht werden.
- `RaiseUiActionRequested` kommt im Repository nicht vor — der Lifecycle-Prüfpunkt ist ohne Befund erfüllt.
- `dotnet build VideoWebPlayer.csproj`: erfolgreich, 0 Fehler, 0 Warnungen.

## Geprüfte Dateien

- `VideoWebPlayer/Controllers/BackupsController.cs`
- `VideoWebPlayer/Services/Backups/BackupUploadSessionService.cs` (neu)
- `VideoWebPlayer/Services/Backups/VideoWebPlayerBackupFacade.cs`
- `VideoWebPlayer/Components/Pages/Admin/Backups.razor`
- `VideoWebPlayer/Components/App.razor`
- `VideoWebPlayer/wwwroot/js/backupUpload.js` (neu)
- `VideoWebPlayer/Extensions/KestrelLimits.cs` (neu)
- `VideoWebPlayer/Extensions/ServiceCollectionExtensions.cs`
- `VideoWebPlayer/Program.cs`
- `VideoWebPlayer/VideoWebPlayer.csproj`
- `VideoWebPlayer/appsettings.Production.json`
- `VideoWebPlayer.Tests/BackupUploadE2ETests.cs` (neu)
- `VideoWebPlayer.Tests/BackupsControllerAuthorizationTests.cs`
- `VideoWebPlayer.Tests/MediaSourceSwitchE2ETests.cs`
- `VideoWebPlayer.Tests/KestrelLimitsTests.cs` (neu)
- `VideoWebPlayer.Tests/Helpers/BackupUploadSessionServiceTestSupport.cs` (neu)
- `VideoWebPlayer.Tests/Services/Backups/BackupUploadSessionServiceTests_BeginSession.cs` (neu)
- `VideoWebPlayer.Tests/Services/Backups/BackupUploadSessionServiceTests_AppendChunk.cs` (neu)
- `VideoWebPlayer.Tests/Services/Backups/BackupUploadSessionServiceTests_GetSession.cs` (neu)
- `VideoWebPlayer.Tests/Services/Backups/BackupUploadSessionServiceTests_Cleanup.cs` (neu)
- `VideoWebPlayer.Tests/Services/Backups/BackupUploadSessionServiceTests_Concurrency.cs` (neu)
- `VideoWebPlayer.Tests/Services/Backups/VideoWebPlayerBackupFacadeTests.cs` (neu)
- `.gitignore`
- `docs/help/backups.md`
