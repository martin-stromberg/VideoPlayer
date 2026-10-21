# Code-Review

## Ergebnis

**Status:** Keine Befunde

## Befunde

Keine.

## Geprüfte Dateien

- `VideoWebPlayer/Data/MediaItem.cs` — zwei neue Eigenschaften, XML-Docs vorhanden
- `VideoWebPlayer/Data/Configurations/MediaItemConfiguration.cs` — Index-Erweiterung, minimal
- `VideoWebPlayer/Migrations/20261021033436_AddMediaItemDuration*.cs` + `ApplicationDbContextModelSnapshot.cs` — generiert, korrekt (nullable TEXT-Spalten + Index)
- `VideoWebPlayer/Utils/VideoFileExtensions.cs` — statische geteilte Liste, single source für Classifier und Probe
- `VideoWebPlayer/Configuration/MediaProbeOptions.cs` — Options mit `Effective*`-Defaults (Konvention wie `ProgramSettingsService`-Defaults)
- `VideoWebPlayer/Services/IFfprobeLocator.cs`, `FfprobeLocator.cs` — klare Auflösungsreihenfolge, testbarer Seam (baseDirectory/PATH-Func), OS-abhängige Dateinamen
- `VideoWebPlayer/Services/IMediaDurationProbe.cs`, `FfprobeMediaDurationProbe.cs` — Prozesskapselung: Redirect stdin/stdout/stderr, Timeout mit `entireProcessTree`-Kill, echte Cancellation propagieren; `catch (Exception)`-Blöcke sind begründet eng um Prozess-IO (Stream-Pump, Kill, SafeResult) bzw. loggen mit Kontext — kein stilles Schlucken; `TryParseDurationOutput` lehnt `N/A`/leer/≤0 ab
- `VideoWebPlayer/Services/MediaDurationProbeService.cs` — Batch-Logik, OR-Expression für Video-Endungen (SQL-übersetzbar), Quelltyp-Routing, Fehlprobe-Markierung je Item mit Log, statisches `_missingBinaryLogged` (einmalige Warnung über scoped Instanzen hinweg), kein Retry fehlgeschlagener Probes
- `VideoWebPlayer/Services/MediaDurationProbeWorker.cs` — BackgroundService-Muster wie `PlaylistBackfillWorker`: Startup-Catch-up, Drain-Loop bis `processed == 0`, Gate-Lease, Cancellation sauber
- `VideoWebPlayer/Services/MediaSourceScanner.cs` — drei Zeilen Reset im Dateiwechsel-Zweig
- `VideoWebPlayer/Services/MediaSourceClassifier.cs` — ein Feld auf geteilte Liste umgestellt, Signaturen unverändert
- `VideoWebPlayer/Services/Backups/VideoWebPlayerBackupData.cs` — zwei `OptionalRestoreColumns`-Einträge, kein Default nötig (`NULL` korrekt)
- `VideoWebPlayer/Extensions/ServiceCollectionExtensions.cs` — Registrierungen konsistent zu den vorhandenen (scoped Service, Singletons, HostedService)
- `VideoWebPlayer/appsettings.json` — `MediaProbe`-Abschnitt mit dokumentierten Defaults
- `VideoWebPlayer.Tests/Services/MediaDurationProbeServiceTests.cs` — 10 fachlich getrennte Tests, echtes SQLite, Arrange-Act-Assert, Fake statt Moq für Probe/Locator/Reader
- `VideoWebPlayer.Tests/Services/FfprobeMediaDurationProbeTests.cs` — Delegate-Seam-Tests inkl. Cancellation
- `VideoWebPlayer.Tests/Services/FfprobeLocatorTests.cs` — Temp-Verzeichnisse, Dispose-Aufräumen
- `VideoWebPlayer.Tests/Services/MediaDurationProbeWorkerTests.cs` — reeller Worker gegen Fake-Probe, Deadline-Polling
- `VideoWebPlayer.Tests/Services/MediaSourceScannerLocalTests.cs` — Reset-Test ergänzt
- `VideoWebPlayer.Tests/Services/Backups/VideoWebPlayerBackupDataTests.cs` — Alt-Backup-Regression mit `ChangeTracker.Clear()` vor Assert

Anmerkung (kein Befund, bewusste Entscheidung): `FfprobeMediaDurationProbe` ist nicht `sealed` und besitzt einen `internal` Ctor-Overload als Test-Seam — minimal invasiv, kein überflüssiges Interface für den Prozess-Executor.
