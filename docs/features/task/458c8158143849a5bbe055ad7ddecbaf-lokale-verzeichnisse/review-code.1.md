# Code-Review

## Ergebnis

**Status:** Befunde vorhanden

## Befunde

### LocalMediaSourceReader.cs (LocalMediaSourceReader)

- **Fehlerbehandlung** — Vier `catch (Exception)`-Blöcke schlucken Exceptions still, ohne jegliche Behandlung oder Logging: `ReadDirectoryEntries` (Zeile 50–53, Einträge verschwinden lautlos aus dem Scan) sowie `ResolveFilePath` (Zeilen 154–157, 190–193 und 203–206). Die Klasse besitzt keinen `ILogger`; ein Zugriffsfehler oder ein ungültiger Pfad ist damit im Betrieb nicht nachvollziehbar. Zudem fängt `catch (Exception)` mehr als die erwarteten Fehler (z. B. `IOException`, `UnauthorizedAccessException`, `ArgumentException`, `NotSupportedException`).

  Empfehlung: Nur die konkret erwarteten Exception-Typen fangen und einen `ILogger<LocalMediaSourceReader>` per Konstruktor injizieren, um übersprungene Einträge bzw. nicht auflösbare Pfade zumindest per `LogDebug`/`LogWarning` zu protokollieren.

- **Doppelter Code** — Die Methode `IsIgnoredEntry` (Zeilen 230–233) ist identisch zu `SftpMediaSourceReader.IsIgnoredEntry` (`VideoWebPlayer/Services/SftpMediaSourceReader.cs`, Zeilen 293–296) dupliziert. Bei einer Änderung der Ignorier-Regeln (z. B. weitere Präfixe) müssten beide Stellen synchron angepasst werden.

  Empfehlung: Die Prüfung in eine gemeinsame interne Hilfsklasse auslagern (z. B. `internal static class MediaEntryFilter`) oder eine gemeinsame Basisklasse für beide Reader einführen.

### MediaSourceAdminDetails.razor

- **Fehlerbehandlung** — In `Save` (Zeilen 289–296) fängt `catch (Exception)` alle Fehler von `Path.GetFullPath`/`Directory.Exists` still ab und bildet sie auf `directoryExists = false` ab. Dadurch wird auch bei ungültigen Pfadzeichen, zu langen Pfaden oder fehlenden Zugriffsrechten die irreführende Meldung „Verzeichnis existiert nicht." ausgegeben.

  Empfehlung: Die Auswertung differenzieren — z. B. `ArgumentException`/`NotSupportedException`/`PathTooLongException` von `GetFullPath` separat fangen und als „Ungültiger Pfad" melden, und nur ein echtes `Directory.Exists == false` als „Verzeichnis existiert nicht." ausgeben.

### LocalMediaSourceClassifierTests.cs

- **Toter Code / Ressourcen-Handling** — `CreateContextAndServiceAsync` (Zeile 89) gibt den `ServiceProvider` im Rückgabetupel zurück, der einzige Aufrufer verwirft ihn jedoch mit `_ = serviceProvider;` (Zeile 36). Der Provider (inkl. DbContext und offener Keeper-Connection) wird dadurch nie disposed und das Tuple-Element ist ungenutzter Ballast.

  Empfehlung: Entweder den `ServiceProvider` aus dem Rückgabetupel entfernen oder im Test per `using`/in einem `finally` disposen (analog zum vorhandenen Cleanup des Temp-Verzeichnisses).

## Geprüfte Dateien

Liste aller geprüften Dateien:
- `VideoWebPlayer/Data/MediaSource.cs`
- `VideoWebPlayer/Data/MediaSourceExtensions.cs`
- `VideoWebPlayer/Data/MediaSourceType.cs`
- `VideoWebPlayer/Services/IMediaSourceReader.cs`
- `VideoWebPlayer/Services/LocalMediaSourceReader.cs`
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
- `VideoWebPlayer.Tests/MediaSourceClassifierBackgroundImageTests.cs`
- `VideoWebPlayer.Tests/MediaSourceScanServiceTests.cs`
- `VideoWebPlayer.Tests/Services/Backups/VideoWebPlayerBackupDataTests.cs`
- `VideoWebPlayer.Tests/Services/MediaSourceScannerTests.cs`
- `VideoWebPlayer.Tests/Controllers/ItemsControllerLocalSourceTests.cs`
- `VideoWebPlayer.Tests/LocalMediaSourceClassifierTests.cs`
- `VideoWebPlayer.Tests/LocalMediaSourcePipelineE2ETests.cs`
- `VideoWebPlayer.Tests/LocalMediaSourceStreamingE2ETests.cs`
- `VideoWebPlayer.Tests/MediaSourceLocalDirectoryE2ETests.cs`
- `VideoWebPlayer.Tests/Services/LocalMediaSourceReaderTests.cs`
- `VideoWebPlayer.Tests/Services/MediaSourceReaderDispatcherTests.cs`
- `VideoWebPlayer.Tests/Services/MediaSourceScannerLocalTests.cs`
