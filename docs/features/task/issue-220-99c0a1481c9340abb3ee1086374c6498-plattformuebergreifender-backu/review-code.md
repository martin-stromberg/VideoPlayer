# Code-Review

## Ergebnis

**Status:** Keine Befunde

## Hinweise

- **Befund aus Runde 4 (`FormatBytes`-Duplikat) korrekt behoben:** Die drei wortidentischen C#-Kopien wurden durch die neue statische Klasse `VideoWebPlayer/Utils/ByteSizeHelper.cs` ersetzt; `BackupUploadSessionService.cs` (Z. 84), `Backups.razor` und `Updates.razor` nutzen sie. Platzierung und Stil (`public static class …Helper`, XML-Docs) folgen exakt der bestehenden Konvention von `LocalNetworkHelper` im selben Ordner.
- **`@using static VideoWebPlayer.Utils.ByteSizeHelper` in `_Imports.razor` (Z. 14):** `FormatBytes` steht damit allen Komponenten unter `Components/` unqualifiziert zur Verfügung. Repo-weite Suche bestätigt: Keine andere Komponente deklariert ein Mitglied `FormatBytes` (kein Shadowing/Ambiguität), kein zweiter `using static` exportiert denselben Namen, kein Typ namens `ByteSizeHelper` existiert sonstwo. `dotnet build VideoWebPlayer.Tests.csproj`: 0 Fehler (140 Warnungen sind sämtlich vorhandene CS86xx-/BL0008-Hinweise, keine neuen).
- **Kultur-/Rundungsverhalten:** `FormatBytes` verwendet weiterhin `$"{value:0.##} …"` mit `CurrentCulture` — identisch zum bisherigen Verhalten der entfernten Kopien, kein Verhaltenswechsel. `ByteSizeHelperTests.FormatBytes_UsesLargestFittingUnit` kompensiert den kulturabhängigen Dezimalseparator explizit (`NumberDecimalSeparator`-Ersetzung) — korrekt; die Service-Tests prüfen ganzzahlige Werte („1 KB", „5 GB") und sind damit kulturunabhängig.
- **Testqualität:** `ByteSizeHelperTests` deckt Grenzfälle (0, 1023/1024-Übergang, 1.5 KB mit Dezimalstelle, TB-Überlauf → Kappen bei GB) sauber als Theory/Fact ab. `BeginSessionAsync_OrphanedTempFileOutsideOwnDirectory_IsNotScanned` prüft die Temp-Verzeichnis-Kapselung deterministisch; `…_FormatsGigabyteLimit` inkl. `DoesNotContain("5368709120")` sichert den Usability-Befund ab.
- **`ui.notice` in `backupUpload.js`:** Das neue `div.backup-upload-notice` (Z. 61–62, 71) trägt die `ALREADY_RUNNING_MESSAGE`, ohne Fortschrittsbalken/Abbrechen-Button des laufenden Runs zu zerstören; `run.ui` (Z. 265) wird im `finally` über `activeRun` korrekt freigegeben. Bei abweichendem `containerEl` wird zusätzlich `showMessage` auf dem fremden Container ausgeführt — sicher.
- **Injizierbares `tempDirectory`:** Optionaler Ctor-Parameter (`string? = null` → `Path.GetTempPath()`); DI-Auflösung über `AddSingleton<BackupUploadSessionService>()` funktioniert per Default-Parameter-Unterstützung (kein `string`-Dienst registriert). `Directory.CreateDirectory`, Dateianlage und Orphan-Scan nutzen konsistent `_tempDirectory`; `TempDirectory`-Testhelfer isoliert parallele Tests.
- Kleinigkeiten, bewusst nicht als Befund gewertet:
  - Die JS-Variante `formatBytes` (`backupUpload.js` Z. 20–29) bleibt eine sprachbedingte, nicht deduplizierbare Parallelimplementierung mit abweichender Präzision (`toFixed` 0/1 Stellen vs. `0.##` bis zu 2 Stellen) — Fortschrittsanzeige vs. Limits, kosmetisch konsistent genug.
  - `FormatBytes` endet bei `GB` (z. B. „2048 GB" für 2 TiB) — gleiche Beschränkung wie zuvor; unrealistische Größen.
  - `catch (JSException) { }` in `Backups.razor` (`OnAfterRenderAsync`, `DiscardResumableUploadAsync`) ist bewusstes Best-Effort-Verhalten für eine nicht-kritische UI-Ergänzung (Resume-Hinweis); der Code fährt anschließend sinnvoll fort.
  - `TempDirectory.Dispose()` (Test-Support) schluckt Fehler kommentiert als best-effort Cleanup — in Test-Teardown akzeptabel.
- `RaiseUiActionRequested` kommt im Repository nicht vor — der Lifecycle-Prüfpunkt ist ohne Befund erfüllt.
- Testlauf (`--filter ByteSizeHelper|BackupUploadSessionService|VideoWebPlayerBackupFacade|KestrelLimits`): 39/39 bestanden.

## Geprüfte Dateien

- `VideoWebPlayer/Utils/ByteSizeHelper.cs` (neu)
- `VideoWebPlayer/Components/_Imports.razor` (`@using static`)
- `VideoWebPlayer/Components/Pages/Admin/Backups.razor` (Aufrufe, entfernte Kopie)
- `VideoWebPlayer/Components/Pages/Admin/Updates.razor` (Aufruf, entfernte Kopie)
- `VideoWebPlayer/Services/Backups/BackupUploadSessionService.cs` (`tempDirectory`, `FormatBytes`-Aufruf)
- `VideoWebPlayer/Services/Backups/VideoWebPlayerBackupFacade.cs`
- `VideoWebPlayer/Controllers/BackupsController.cs`
- `VideoWebPlayer/Extensions/KestrelLimits.cs`
- `VideoWebPlayer/Extensions/ServiceCollectionExtensions.cs` (DI-Auflösung des optionalen Parameters)
- `VideoWebPlayer/Program.cs` (`ConfigureKestrel`-Bindung)
- `VideoWebPlayer/Components/App.razor` (Script-Einbindung)
- `VideoWebPlayer/wwwroot/js/backupUpload.js` (`ui.notice`, `activeRun`-Guard)
- `VideoWebPlayer/VideoWebPlayer.csproj`, `VideoWebPlayer/appsettings.Production.json`, `.gitignore`, `README.md`, `docs/help/backups.md`
- `VideoWebPlayer.Tests/Utils/ByteSizeHelperTests.cs` (neu)
- `VideoWebPlayer.Tests/Helpers/BackupUploadSessionServiceTestSupport.cs` (`TempDirectory`)
- `VideoWebPlayer.Tests/Services/Backups/BackupUploadSessionServiceTests_{BeginSession,AppendChunk,Cleanup,Concurrency,GetSession}.cs`
- `VideoWebPlayer.Tests/Services/Backups/VideoWebPlayerBackupFacadeTests.cs`
- `VideoWebPlayer.Tests/BackupUploadE2ETests.cs`
- `VideoWebPlayer.Tests/BackupsControllerAuthorizationTests.cs`
- `VideoWebPlayer.Tests/KestrelLimitsTests.cs`
- `VideoWebPlayer.Tests/MediaSourceSwitchE2ETests.cs` (Klick-Retry)
