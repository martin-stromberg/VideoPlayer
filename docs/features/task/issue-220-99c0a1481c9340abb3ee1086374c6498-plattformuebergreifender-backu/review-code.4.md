# Code-Review

## Ergebnis

**Status:** Befunde vorhanden

## Befunde

### BackupUploadSessionService.cs (BackupUploadSessionService)

- **Doppelter Code** — Die neue private Methode `FormatBytes` (Z. 265–277) ist eine wortidentische Kopie der bereits vorhandenen privaten `FormatBytes`-Implementierungen in `Backups.razor` (Z. 666–678) und `Updates.razor` (Z. 508–520). Damit existieren jetzt drei identische C#-Implementierungen desselben Formatierungsalgorithmus (plus die abweichende JS-Variante `formatBytes` in `backupUpload.js`).

  Empfehlung: Den Algorithmus in eine gemeinsame statische Hilfsklasse auslagern (z. B. `internal static class Formatting` / `ByteSizeFormatter` in einem geteilten Ordner wie `VideoWebPlayer/Common` oder `VideoWebPlayer/Extensions`) und aus `BackupUploadSessionService`, `Backups.razor` und `Updates.razor` aufrufen.

## Hinweise

- **Alle 3 Fortsetzungs-Befunde sind korrekt umgesetzt:**
  - **Test-Isolation:** Der optionale Ctor-Parameter `string? tempDirectory` (Z. 50) wird vom integrierten DI-Container korrekt über seinen Default (`null` → `Path.GetTempPath()`, Z. 55) aufgelöst — `AddSingleton<BackupUploadSessionService>()` (`ServiceCollectionExtensions.cs` Z. 251) nutzt die Default-Parameter-Unterstützung von `CallSiteFactory`; es ist kein `string`-Dienst registriert, der versehentlich injiziert würde. Datei-Anlage (Z. 86, 90) und Orphan-Scan (`CleanupOrphanedTempFiles`, Z. 225) verwenden konsistent `_tempDirectory`; `Directory.CreateDirectory` liegt im try-Block. `TempDirectory.Create()` erzeugt Unterverzeichnisse von `%TEMP%`, die vom nicht-rekursiven `Directory.GetFiles`-Scan parallel laufender (E2E-)Instanzen nicht erfasst werden — Isolation vollständig. Neuer Test `BeginSessionAsync_OrphanedTempFileOutsideOwnDirectory_IsNotScanned` prüft die Kapselung deterministisch (Cleanup läuft beim ersten `BeginSessionAsync` garantiert, da `_lastCleanupTicks = 0`).
  - **`activeRun`-Guard (`backupUpload.js` Z. 234–244):** `ui` wird am `run`-Objekt abgelegt (Z. 265), `ALREADY_RUNNING_MESSAGE` geht in `ui.notice` (neues `div.backup-upload-notice`, Z. 61–62, 71) — Fortschrittsbalken und Abbrechen-Button bleiben erhalten. Bei abweichendem `containerEl` wird zusätzlich `showMessage` auf dem fremden Container ausgeführt (sicher, da der aktive Run diesen nicht nutzt). `activeRun` wird im `finally` zurückgesetzt (Z. 415–416) — kein Zugriff auf veraltete UI. Der `if (activeRun.ui)`-Guard ist defensiv, aber harmlos.
  - **Limit-Fehlermeldung:** `FormatBytes(maxUploadSizeBytes)` (Z. 83) liefert „5 GB"; Tests prüfen exakt „1 KB" und „5 GB" inkl. `DoesNotContain("5368709120")`. `0.##` nutzt `CurrentCulture` — identisch zum Verhalten der bestehenden Kopien; die Assertions sind ganzzahlig und damit kulturunabhängig.
- Kleinigkeiten, bewusst nicht als Befund gewertet:
  - `FormatBytes` endet bei `GB` — ein Limit ≥ 1 TiB würde z. B. „2048 GB" zeigen; gleiche Beschränkung haben die vorhandenen Kopien, und solche Limits sind unrealistisch.
  - `Directory.CreateDirectory(_tempDirectory)` läuft bei jedem `BeginSessionAsync` — vernachlässigbarer Overhead, deckt aber gelöschte injizierte Verzeichnisse ab.
  - Die gesetzte Notice bleibt bis zum Run-Ende stehen (Element wird nur bei `createUi`/Navigation zurückgesetzt) — vertretbar.
- Stray-Datei `nul` aus `continue.md` ist gelöscht (nicht mehr im Repo-Root).
- `RaiseUiActionRequested` kommt im Repository nicht vor — der Lifecycle-Prüfpunkt ist ohne Befund erfüllt.
- `dotnet build VideoWebPlayer.Tests.csproj`: erfolgreich, 0 Fehler (140 Warnungen sind sämtlich vorhandene BL0008-/Razor-Hinweise, keine neuen).
- Untracked `review-code.3.md`/`review-usability.3.md` sind die archivierten Vor-Runden — diese Datei ersetzt `review-code.md` für die aktuelle Runde.

## Geprüfte Dateien

Fokus dieser Runde: die uncommitteten Nachbesserungen (`git diff HEAD`) im Kontext der DI-Registrierung und der Razor-Aufrufer:

- `VideoWebPlayer/Services/Backups/BackupUploadSessionService.cs`
- `VideoWebPlayer/wwwroot/js/backupUpload.js`
- `VideoWebPlayer/Extensions/ServiceCollectionExtensions.cs` (DI-Auflösung des optionalen Parameters)
- `VideoWebPlayer/Components/Pages/Admin/Backups.razor` (`uploadContainer`, `StartUploadAsync`, `FormatBytes`)
- `VideoWebPlayer/Components/Pages/Admin/Updates.razor` (`FormatBytes`-Duplikat)
- `VideoWebPlayer.Tests/Helpers/BackupUploadSessionServiceTestSupport.cs`
- `VideoWebPlayer.Tests/Helpers/IncrementingTimeProvider.cs`
- `VideoWebPlayer.Tests/Services/Backups/BackupUploadSessionServiceTests_BeginSession.cs`
- `VideoWebPlayer.Tests/Services/Backups/BackupUploadSessionServiceTests_AppendChunk.cs`
- `VideoWebPlayer.Tests/Services/Backups/BackupUploadSessionServiceTests_GetSession.cs`
- `VideoWebPlayer.Tests/Services/Backups/BackupUploadSessionServiceTests_Cleanup.cs`
- `VideoWebPlayer.Tests/Services/Backups/BackupUploadSessionServiceTests_Concurrency.cs`
- `VideoWebPlayer.Tests/BackupUploadE2ETests.cs` (DI-Auflösung via `WebApplicationFactory`)
