# Code-Review

## Ergebnis

**Status:** Befunde vorhanden

## Befunde

### BackupUploadSessionService.cs (BackupUploadSessionService / BackupUploadSession)

- **Concurrency** — Eine Session kann disposed werden, während ein Request sie noch verwendet. Zwei Pfade:
  1. `CleanupExpiredSessions` (Z. 127–162) entscheidet anhand des gelesenen `LastAccessUtc` über die Entfernung und ruft anschließend `session.Dispose()` auf. Ein paralleler Request kann dieselbe Session zwischen der Expired-Prüfung und `TryRemove` noch über `GetSession` → `TryGetValue` erhalten (`Touch()` in Z. 102 kommt dann zu spät) und anschließend in `AppendChunkAsync` auf den bereits freigegebenen `Stream`/`WriteLock` zugreifen → `ObjectDisposedException` → HTTP 500. Szenario: Session ≥ 24 h unangetastet und ein Resume-/Status-Request trifft genau während des Cleanup-Laufs ein.
  2. `CompleteSessionAsync`/`AbortSessionAsync` (Z. 110–125) und `Dispose`/`DisposeAsync` (Z. 339–350) geben `WriteLock` frei, obwohl ein zweiter Request für dieselbe Session (möglich z. B. durch zwei Browser-Tabs mit gemeinsamem localStorage-Eintrag) in `WriteLock.WaitAsync` (Z. 283) blockiert sein kann → `ObjectDisposedException` statt sauberem `Rejected`/`ResumeRequired`.

  Empfehlung: `WriteLock` nicht in `Dispose`/`DisposeAsync` freigeben — `SemaphoreSlim` benötigt kein Dispose, solange `AvailableWaitHandle` nie abgefragt wird; wartende Requests laufen dann sauber in den `IsCompleted`-Pfad. Zusätzlich in `CleanupExpiredSessions` nach `TryRemove` den `WriteLock` der Session kurz akquirieren und `LastAccessUtc` unter dem Lock erneut prüfen, bevor disposed wird (Session ggf. wieder eintragen).

### VideoWebPlayerBackupFacade.cs (VideoWebPlayerBackupFacade)

- **Fehlerbehandlung** — `MoveTempFileAsync` enthält einen leeren inneren `catch`-Block (Z. 168–175): Schlägt das Löschen eines teilkopierten Ziels fehl, bleibt eine abgeschnittene `.bak` im Speicherpfad zurück, die in `ListBackupsAsync` als (ungültiges) Backup auftaucht — ohne jeden Hinweis im Log.

  Empfehlung: Methode nicht `static` machen und `_logger` für eine Warnung nutzen (oder Logger als Parameter übergeben); mindestens ein Kommentar, der die bewusste Entscheidung dokumentiert.

- **Cancellation/Robustheit** — Die Same-Volume-Erkennung in `MoveTempFileAsync` (Z. 150–156) vergleicht nur `Path.GetPathRoot`. Auf Linux können `/tmp` (tmpfs) und der Speicherpfad unter demselben Root `/` auf unterschiedlichen Dateisystemen liegen; `File.Move` führt dann intern eine vollständige synchrone Kopie aus und ignoriert den `cancellationToken` — der ursprüngliche Befund aus Runde 1 besteht für diesen Fall fort.

  Empfehlung: Entweder die Einschränkung dokumentieren oder die Temp-Datei direkt im Speicherverzeichnis (z. B. `targetPath + ".part"`) anlegen, sodass `File.Move` garantiert ein Rename bleibt.

### backupUpload.js (window.backupUpload)

- **Laufzeitverhalten** — `running` und der `AbortController` leben im Module-Scope (Z. 8, 138). Blazor-Enhanced-Navigation lädt die Seite nicht neu: Verlässt der Nutzer die Seite während eines laufenden Uploads, läuft der Upload im Hintergrund weiter und `finishWithStatus`/`finishWithError` (Z. 77–86) erzwingen per `window.location.assign` einen Redirect zurück nach `/admin/backups`, obwohl der Nutzer inzwischen auf einer anderen Seite ist. Umgekehrt ist `start` bei einem erneuten Besuch während des laufenden Uploads durch den `running`-Guard (Z. 122) stillschweigend wirkungslos.

  Empfehlung: Redirect nur ausführen, wenn der Upload-Container noch im DOM existiert (z. B. `containerEl.isConnected` prüfen), bzw. den Upload bei Verlassen der Seite aborten.

## Hinweise

- Alle 6 Befunde aus Runde 1 wurden bearbeitet und sind korrekt umgesetzt: `UploadChunk` extrahiert (`ResolveOrBeginSessionAsync`/`BeginNewSessionAsync`), Cleanup-Drosselung über `CleanupInterval` + `Interlocked`-Guard, `MoveTempFileAsync` mit async Cross-Volume-Kopie + Cancellation, `stateSubscription` camelCase, `KestrelLimits.ParseMaxRequestBodySize` mit `InvalidOperationException`, Testklasse sauber in Einzelfall-Tests aufgeteilt (`_BeginSession`, `_AppendChunk`, `_GetSession`, `_Cleanup` + `BackupUploadSessionServiceTestSupport`).
- `Uri.UnescapeDataString` für den `Upload-Name`-Header (BackupsController.cs Z. 164, 185) wurde gegen `UriFormatException` geprüft: unter .NET 10 werden ungültige Escape-Sequenzen unverändert durchgereicht — kein Befund.
- `RaiseUiActionRequested` kommt im Repository nicht vor — der Lifecycle-Prüfpunkt ist ohne Befund erfüllt.
- `dotnet build VideoWebPlayer.csproj`: erfolgreich, 0 Fehler (Warnungen sind pre-existing Analyzer-Hinweise in Account-Pages).

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
- `VideoWebPlayer.Tests/Services/Backups/VideoWebPlayerBackupFacadeTests.cs` (neu)
- `.gitignore`
- `docs/help/backups.md`
