# Code-Review

## Ergebnis

**Status:** Befunde vorhanden

## Befunde

### MediaSourceScannerLocalTests.cs (MediaSourceScannerLocalTests)

- **Ressourcen-Handling** — `CreateServiceProviderAsync` (Zeilen 147–173) gibt einen `ServiceProvider` zurück, den keiner der drei Aufrufer disposed (Zeilen 33, 65, 108: `var serviceProvider = await CreateServiceProviderAsync(...)` ohne `using`/`Dispose`/`finally`). Zusätzlich wird die Keeper-`SqliteConnection` (Zeilen 150–151) geöffnet, aber weder im `ServiceCollection` registriert noch disposed — anders als in `LocalMediaSourceClassifierTests`, wo sie per `services.AddSingleton(keeperConnection)` dem Provider übergeben wurde und dadurch mit diesem geschlossen wird. Die Shared-In-Memory-Datenbank bleibt damit bis zum GC offen.

  Empfehlung: `keeperConnection` per `services.AddSingleton(keeperConnection)` im Provider registrieren und den `ServiceProvider` in den Tests per `await using`/`using` bzw. in einem `finally` disposen (Muster aus `LocalMediaSourceClassifierTests` übernehmen).

### ItemsControllerLocalSourceTests.cs (ItemsControllerLocalSourceTests)

- **Ressourcen-Handling** — In `CreateControllerWithLocalMovieAsync` werden die Keeper-`SqliteConnection` (Zeilen 87–88) und der `ServiceProvider` (Zeile 100) angelegt, aber nie disposed; das Rückgabetupel enthält nur `(Controller, Movie)`, sodass der Aufrufer die Ressourcen gar nicht freigeben kann. Pro Testlauf bleiben Provider, DbContext und die offene Shared-In-Memory-Connection liegen.

  Empfehlung: Die Connection per `services.AddSingleton(keeperConnection)` registrieren und den `ServiceProvider` entweder als drittes Tuple-Element zurückgeben (Aufrufer disposed in `finally`) oder in einem Feld der Testklasse halten und in `Dispose()` freigeben.

### MediaSourceReaderDispatcher.cs (MediaSourceReaderDispatcher)

- **Fehlerbehandlung** — `GetReader(collection?.MediaSource)` (Zeile 51–52) bildet ein nicht geladenes `MediaSource`-Navigation-Property still auf den SFTP-Reader ab. Wird eine `MediaCollection` einer `LocalDirectory`-Quelle ohne `Include(mc => mc.MediaSource)` übergeben, wird der falsche Reader gewählt; der Fehler manifestiert sich erst als `NullReferenceException` beim Zugriff auf `collection.MediaSource.Host` im `SftpMediaSourceReader` statt als klare, diagnostizierbare Fehlermeldung.

  Empfehlung: In den collection-basierten Methoden die Vorbedingung explizit prüfen und bei `collection?.MediaSource is null` eine aussagekräftige Exception werfen (z. B. `InvalidOperationException` mit Hinweis, dass `MediaSource` eager geladen werden muss).

### LocalMediaSourceReaderTests.cs (LocalMediaSourceReaderTests)

- **Testqualität / Ressourcen-Handling** — Der Konstruktor legt das Verzeichnis `vwp-localreader-outside` inklusive Datei direkt unter `Path.GetTempPath()` an (Zeilen 26–27), `Dispose()` (Zeilen 30–33) löscht jedoch nur `_baseDir`. Das Verzeichnis bleibt nach jedem Testlauf als Artefakt im Temp-Ordner zurück.

  Empfehlung: Das Outside-Verzeichnis in `Dispose()` ebenfalls löschen (ggf. mit demselben `try/catch`-Muster wie `_baseDir`).

## Geprüfte Dateien

Liste aller geprüften Dateien:
- `VideoWebPlayer/Data/MediaSource.cs`
- `VideoWebPlayer/Data/MediaSourceExtensions.cs`
- `VideoWebPlayer/Data/MediaSourceType.cs`
- `VideoWebPlayer/Services/IMediaSourceReader.cs`
- `VideoWebPlayer/Services/LocalMediaSourceReader.cs`
- `VideoWebPlayer/Services/MediaEntryFilter.cs`
- `VideoWebPlayer/Services/MediaSourceReaderDispatcher.cs`
- `VideoWebPlayer/Services/SftpMediaSourceReader.cs`
- `VideoWebPlayer/Services/MediaSourceScanner.cs`
- `VideoWebPlayer/Services/MediaSourceClassifier.cs`
- `VideoWebPlayer/Services/Backups/VideoWebPlayerBackupData.cs`
- `VideoWebPlayer/Controllers/ItemsController.cs`
- `VideoWebPlayer/Extensions/ServiceCollectionExtensions.cs`
- `VideoWebPlayer/Components/Layout/NavMenu.razor`
- `VideoWebPlayer/Components/Pages/Admin/MediaSources/MediaSourceAdmin.razor`
- `VideoWebPlayer/Components/Pages/Admin/MediaSources/MediaSourceAdminDetails.razor`
- `VideoWebPlayer/Migrations/20260922180232_AddMediaSourceSourceType.cs`
- `VideoWebPlayer/Migrations/20260922180232_AddMediaSourceSourceType.Designer.cs`
- `VideoWebPlayer/Migrations/ApplicationDbContextModelSnapshot.cs`
- `VideoWebPlayer.Tests/ApplicationDbContextTests.cs`
- `VideoWebPlayer.Tests/Helpers/TestHelpers.cs`
- `VideoWebPlayer.Tests/MediaSourceClassifierBackgroundImageTests.cs`
- `VideoWebPlayer.Tests/MediaSourceScanServiceTests.cs`
- `VideoWebPlayer.Tests/Services/Backups/VideoWebPlayerBackupDataTests.cs`
- `VideoWebPlayer.Tests/Services/MediaSourceScannerTests.cs`
- `VideoWebPlayer.Tests/Controllers/ItemsControllerLocalSourceTests.cs`
- `VideoWebPlayer.Tests/LocalMediaSourceClassifierTests.cs`
- `VideoWebPlayer.Tests/LocalMediaSourcePipelineE2ETests.cs`
- `VideoWebPlayer.Tests/LocalMediaSourceStreamingE2ETests.cs`
- `VideoWebPlayer.Tests/MediaSourceAdminDetailsValidationTests.cs`
- `VideoWebPlayer.Tests/MediaSourceLocalDirectoryE2ETests.cs`
- `VideoWebPlayer.Tests/Services/LocalMediaSourceReaderTests.cs`
- `VideoWebPlayer.Tests/Services/MediaEntryFilterTests.cs`
- `VideoWebPlayer.Tests/Services/MediaSourceReaderDispatcherTests.cs`
- `VideoWebPlayer.Tests/Services/MediaSourceScannerLocalTests.cs`
