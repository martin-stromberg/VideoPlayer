# Umsetzungsplan: Videolaufzeit erfassen (Schritt 1 – Automatisierter TV-Sender)

## Übersicht

`MediaItem` erhält `Duration` (`TimeSpan?`) und `DurationProbedAt` (`DateTime?`). Ein neuer Hintergrund-Worker (`MediaDurationProbeWorker` + `MediaDurationProbeService`) ermittelt fehlende Laufzeiten per externem `ffprobe`-Prozess — für lokale Quellen direkt über den Dateipfad, für SFTP-Quellen über `OpenFileStream` in die `stdin` von ffprobe (`pipe:0`). Fehlgeschlagene Probes werden per gesetztem `DurationProbedAt` bei `Duration == null` erkennbar und nicht wiederholt; ein fehlendes Binary führt zu wartendem Worker, nicht zu Fehlern. Migration, `OptionalRestoreColumns`-Registrierung mit Alt-Backup-Regressionstest und Konfiguration/Doku runden den Schritt ab.

## Designentscheidungen

| Komponente / Bereich | Gewählter Ansatz | Begründung |
|----------------------|-----------------|------------|
| Speicherort der Laufzeit | `MediaItem.Duration` + `MediaItem.DurationProbedAt` (Datei-Ebene), nicht `Movie`/`TVShowEpisode` | Die Laufzeit ist eine physische Eigenschaft der Datei; `Movie`/`TVShowEpisode` sind 1:n über `MovieMediaItem`/`TVShowEpisodeMediaItem` verknüpft — ein Sendeplatz spielt später konkret eine Datei ab. Vermeidet Mehrdeutigkeit bei mehreren Dateien je Film/Episode. |
| Unterscheidung „ungeprüft" vs. „fehlgeschlagen" | Zeitstempel `DurationProbedAt` statt Status-Enum | Folgt der bestehenden Marker-Konvention (`ClassifiedAt`, `ActorsClassifiedAt`, `LastScannedAt`); `null` = ungeprüft, gesetzt + `Duration == null` = fehlgeschlagen. |
| Ermittlungsart | Eigener `BackgroundService` (`MediaDurationProbeWorker`) mit periodischem Intervall + Startup-Catch-up, statt Probe im `MediaSourceScanner`/`MediaSourceClassifier` | ffprobe-Aufrufe sind langsam (Prozessstart + Datei lesen; bei SFTP komplette Datei streamen). Eine Probe im Scanner würde Scan und Klassifizierung ausbremsen; der Worker deckt neue Items (ungeprüfte entstehen beim Scan automatisch) und den Altbestand mit derselben Abfrage ab. |
| Remote-Probe (SFTP) | `IMediaSourceReader.OpenFileStream` → ffprobe `pipe:0` | Einziger streamender Zugriff ohne die ganze Datei in den Speicher zu laden (`ReadFileStreamAsync` lädt komplett in `MemoryStream` — für Videos ungeeignet). |
| ffprobe-Bereitstellung | Kein Bundling; konfigurierbarer Pfad `MediaProbe:FfprobePath` → Konventionsverzeichnis `tools/ffmpeg/` bzw. App-Verzeichnis → `PATH` | Kein Binary im Repo/Installer ⇒ keine GPL-/LGPL-Mitlieferungspflichten (Prozessaufruf = mere aggregation, keine Verlinkung); siehe `inventory/ffmpeg.md`. |
| Prozesskapselung | `IMediaDurationProbe` (fachlich) über `FfprobeMediaDurationProbe` mit internem Ausführungs-Delegate für Tests | Prozess-Start bleibt dünn und reviewbar; Argumente, Parsing, Timeout- und stdin-Handling sind ohne echtes ffprobe testbar. |
| Video-Selektion | Geteilte Konstante `VideoFileExtensions` (`.mp4`, `.avi`, `.mkv`, `.mpeg`), `MediaSourceClassifier` wird auf sie umgestellt | Heute private Liste `fileExtensions_Video` im Classifier; die Probe braucht exakt dieselbe Abgrenzung „abspielbare Videodatei" — eine Quelle vermeidet Drift. |
| Fehlerbehandlung je Datei | Jede Exception je Item wird gefangen, geloggt und als fehlgeschlagene Probe markiert | Anforderung: Fehler dürfen Scan/Betrieb nicht stören und nicht endlos wiederholt werden. |
| Fehlendes Binary | `IFfprobeLocator.Resolve()` liefert `null` → Batch wird nicht angetastet, Warnung einmalig | Ungeprüfte Items bleiben ungeprüft (nicht fälschlich „fehlgeschlagen"); nach Installation von ffprobe laufen sie automatisch nach. |

## Programmabläufe

### Laufzeit-Probe (Hintergrund)

1. `MediaDurationProbeWorker.ExecuteAsync` wartet `InitialDelay`, dann endlos: `IBackgroundProcessingGate.EnterOperationAsync("Laufzeitermittlung")` → `MediaDurationProbeService.ProbePendingAsync(batchSize, ct)` → wiederholt, bis Rückgabe 0 → `Task.Delay(IntervalSeconds, TimeProvider, ct)`.
2. `MediaDurationProbeService.ProbePendingAsync`:
   a. `IFfprobeLocator.Resolve()` → `null`: einmalige Warnung, Rückgabe 0.
   b. Lädt bis `batchSize` `MediaItem`s mit `DurationProbedAt == null` und Video-Endung (`Name.EndsWith` je Endung), inkl. `MediaCollection.MediaSource`, geordnet nach `Id`.
   c. Je Item: `LocalDirectory` → `probe.ProbeFileAsync(item.Path)`; sonst → `reader.OpenFileStream(collection, item.Name)` (`null` → Fehler-Probe) → `probe.ProbeStreamAsync(stream)`.
   d. `item.Duration = result` (ggf. `null`), `item.DurationProbedAt = now`; `SaveChangesAsync` je Item; Exceptions je Item fangen → ebenfalls als geprüft-fehlgeschlagen markieren.
   e. Rückgabe: Anzahl verarbeiteter Items.
3. Beim Scan: `MediaSourceScanner.ScanMediaCollectionInternalAsync` setzt bei geändertem `CreatedAt` zusätzlich `Duration = null`, `DurationProbedAt = null` → der Worker prüft die Datei neu.

### Alt-Backup-Wiederherstellung

1. `ReadFromAsync` findet im Alt-Payload `MediaItems` ohne `Duration`/`DurationProbedAt` → `OptionalRestoreColumns` lässt die Lücke zu.
2. `InsertRowAsync` fügt die fehlenden Spalten nicht ins INSERT ein → `NULL` ⇒ Items gelten als ungeprüft und werden vom Worker nachgezogen.

## Neue Klassen

| Klasse | Typ | Zweck |
|--------|-----|-------|
| `VideoFileExtensions` | `public static class` (`VideoWebPlayer/Utils/`) | Geteilte Video-Endungsliste + `IsVideoFileName(string)` |
| `MediaProbeOptions` | `public sealed class` (`VideoWebPlayer/Configuration/`) | Options: `FfprobePath`, `IntervalSeconds`, `BatchSize`, `TimeoutSeconds` |
| `IFfprobeLocator` | `public interface` | Löst den ffprobe-Pfad auf (`string?`) |
| `FfprobeLocator` | `public sealed class` | Auflösung: Options-Pfad → `tools/ffmpeg/` + App-Verzeichnis → `PATH` |
| `IMediaDurationProbe` | `public interface` | `ProbeFileAsync(path, ct)` / `ProbeStreamAsync(stream, ct)` → `Task<TimeSpan?>` |
| `FfprobeMediaDurationProbe` | `public class` | Startet `ffprobe` (Prozess), parst die Dauer-Ausgabe; interner Test-Seam über Prozess-Delegate |
| `FfprobeInvocationResult` | `internal sealed record` | `ExitCode`, `StandardOutput`, `TimedOut` — Ergebnis des Prozesslaufs |
| `MediaDurationProbeService` | `public sealed class` | Batch-Verarbeitung ungeprobter Video-`MediaItem`s |
| `MediaDurationProbeWorker` | `public sealed class` (`BackgroundService`) | Startup-Catch-up + Intervall-Schleife |

## Änderungen an bestehenden Klassen

### `MediaItem` (`VideoWebPlayer/Data/MediaItem.cs`)

- **Neue Eigenschaften:** `Duration` (`TimeSpan?`) — Wiedergabedauer; `DurationProbedAt` (`DateTime?`) — Zeitpunkt der letzten Probe.

### `MediaItemConfiguration` (`VideoWebPlayer/Data/Configurations/MediaItemConfiguration.cs`)

- Index auf `DurationProbedAt` für die Pending-Abfrage.

### `MediaSourceScanner` (`VideoWebPlayer/Services/MediaSourceScanner.cs`)

- **Geänderte Stelle:** Dateiwechsel-Zweig in `ScanMediaCollectionInternalAsync` → `Duration`/`DurationProbedAt` zurücksetzen.

### `MediaSourceClassifier` (`VideoWebPlayer/Services/MediaSourceClassifier.cs`)

- Private `fileExtensions_Video` durch geteilte `VideoFileExtensions` ersetzt (reine Umstellung, kein Verhaltensunterschied).

### `VideoWebPlayerBackupData` (`VideoWebPlayer/Services/Backups/VideoWebPlayerBackupData.cs`)

- `OptionalRestoreColumns` += `MediaItems.Duration`, `MediaItems.DurationProbedAt`. Keine Default-Registrierung nötig (`NULL` = ungeprüft ist der korrekte Alt-Wert).

### `ServiceCollectionExtensions` (`VideoWebPlayer/Extensions/ServiceCollectionExtensions.cs`)

- `services.Configure<MediaProbeOptions>(configuration.GetSection("MediaProbe"))`
- `services.AddSingleton<IFfprobeLocator, FfprobeLocator>()`, `services.AddSingleton<IMediaDurationProbe, FfprobeMediaDurationProbe>()`
- `services.AddScoped<MediaDurationProbeService>()`, `services.AddHostedService<MediaDurationProbeWorker>()`

### `appsettings.json`

- Neuer Abschnitt `MediaProbe`: `FfprobePath: null`, `IntervalSeconds: 60`, `BatchSize: 10`, `TimeoutSeconds: 120`.

## Datenbankmigrationen

| Migrationsname | Betroffene Tabellen/Spalten | Beschreibung der Änderung |
|----------------|----------------------------|---------------------------|
| `AddMediaItemDuration` | `MediaItems.Duration` (`TimeSpan?`, TEXT), `MediaItems.DurationProbedAt` (`DateTime?`, TEXT), Index `IX_MediaItems_DurationProbedAt` | Neue Spalten für Laufzeit + Probe-Zeitpunkt; Alt-Daten behalten `NULL` (= ungeprüft) → automatische Nachtragsbefüllung |

## Validierungsregeln

| Feld / Objekt | Regel | Fehlerfall |
|---------------|-------|------------|
| `MediaProbeOptions.IntervalSeconds` / `BatchSize` / `TimeoutSeconds` | `<= 0` → Default (60 / 10 / 120) | Ungültige Konfiguration wird nicht als Fehler weitergereicht, sondern auf Default geklemmt (Konvention wie `ProgramSettingsService`) |
| `MediaProbeOptions.FfprobePath` | gesetzt → muss als Datei existieren, sonst gilt „nicht gefunden" | Wie fehlendes Binary behandelt (Warnung, Items bleiben ungeprüft) |
| ffprobe-Ausgabe | `format=duration` muss positive Zahl sein | `N/A`, leer, nicht parsebar, Exit ≠ 0, Timeout → Probe fehlgeschlagen (`Duration = null`) |

## Konfigurationsänderungen

| Eintrag | Typ | Standardwert | Zweck |
|---------|-----|--------------|-------|
| `MediaProbe:FfprobePath` | `string?` | `null` | Absoluter Pfad zum ffprobe-Binary (Override der Auto-Lokalisierung) |
| `MediaProbe:IntervalSeconds` | `int` | `60` | Polling-Intervall des `MediaDurationProbeWorker` |
| `MediaProbe:BatchSize` | `int` | `10` | Max. Items je Probeschritt |
| `MediaProbe:TimeoutSeconds` | `int` | `120` | Timeout je ffprobe-Aufruf (deckt SFTP-Streaming ab) |

## Seiteneffekte und Risiken

- **Scan/Classification:** `MediaSourceScanner` berührt zwei zusätzliche Spalten beim Dateiwechsel; `MediaSourceClassifier` nutzt geteilte Endungsliste — kein Verhaltensunterschied.
- **SFTP-Probe:** Das Streaming großer Dateien durch `OpenFileStream` → `pipe:0` kann die komplette Datei übertragen; `TimeoutSeconds` begrenzt den Vorgang pro Datei. Der Worker läuft sequenziell (max. 1 ffprobe-Prozess gleichzeitig) — keine Lastspitze.
- **Restore:** Neue `OptionalRestoreColumns` ohne Default → `NULL`; verifikationsseitig ist das der gewünschte Zustand (ungeprüft).
- **Worker-Zusammenhang:** Probe-Worker hält `IBackgroundProcessingGate`-Lease wie andere Hintergrundarbeiten — Restore blockiert ihn korrekt.
- **Tests:** Neue Tabellen-Spalten verändern Backup-Payloads (`index.json` enthält die neuen Spalten) — bestehende Backup-Tests, die Spaltenlisten fest verdrahtet prüfen, müssen ggf. erweitert werden (siehe „Betroffene bestehende Tests").

## Umsetzungsreihenfolge

1. **`VideoFileExtensions` anlegen und Classifier umstellen**
   - Voraussetzungen: Keine.
   - Beschreibung: Statische Klasse mit Endungsliste; `MediaSourceClassifier.fileExtensions_Video` darauf umstellen.

2. **Datenmodell + Migration**
   - Voraussetzungen: Keine.
   - Beschreibung: `MediaItem.Duration`/`DurationProbedAt`; Index in `MediaItemConfiguration`; `dotnet ef migrations add AddMediaItemDuration`.

3. **`MediaProbeOptions` + `IFfprobeLocator`/`FfprobeLocator`**
   - Voraussetzungen: Keine.
   - Beschreibung: Options-Klasse, Locator mit Auflösungsreihenfolge, `appsettings.json`-Abschnitt.

4. **`IMediaDurationProbe`/`FfprobeMediaDurationProbe`**
   - Voraussetzungen: `MediaProbeOptions`, `IFfprobeLocator`.
   - Beschreibung: Prozessaufruf (Datei- und Stream-Modus), `TryParseDurationOutput`, Timeout-Handling; interner Delegate-Seam für Tests.

5. **`MediaDurationProbeService` + `MediaDurationProbeWorker`**
   - Voraussetzungen: Schritte 1–4, `IBackgroundProcessingGate` (vorhanden).
   - Beschreibung: Batch-Logik, Quelltyp-Routing, Fehlermarkierung; Worker-Loop; DI-Registrierung.

6. **Scanner-Rücksetzung bei Dateiwechsel**
   - Voraussetzungen: Schritt 2.
   - Beschreibung: `MediaSourceScanner.ScanMediaCollectionInternalAsync` erweitern.

7. **Backup-Registrierung**
   - Voraussetzungen: Schritt 2.
   - Beschreibung: `OptionalRestoreColumns` erweitern.

8. **Tests**
   - Voraussetzungen: Schritte 1–7.
   - Beschreibung: Testklassen gemäß Abschnitt „Tests".

9. **Dokumentation**
   - Voraussetzungen: alle fachlichen Schritte.
   - Beschreibung: `docs/help/` (ffprobe-Einrichtung + Laufzeit-Verhalten), `README.md`, `docs/RELEASE_NOTES.md`.

## Tests

### Neue Tests

| Test / Hilfsmethode | Testklasse | Was wird geprüft / bereitgestellt? |
|--------------------|------------|-------------------------------------|
| `ProbePendingAsync_ProbesPendingVideoItems_WritesDuration` | `MediaDurationProbeServiceTests` | Erfolg: `Duration` + `DurationProbedAt` gesetzt (echtes SQLite) |
| `ProbePendingAsync_FailedProbe_MarksProbedWithoutDuration` | `MediaDurationProbeServiceTests` | Fehlschlag: `DurationProbedAt` gesetzt, `Duration == null` |
| `ProbePendingAsync_SkipsAlreadyProbedItems` | `MediaDurationProbeServiceTests` | Items mit `DurationProbedAt` werden nicht erneut beprobt |
| `ProbePendingAsync_SkipsNonVideoItems` | `MediaDurationProbeServiceTests` | `.nfo`/`.jpg`-Items werden nie ausgewählt |
| `ProbePendingAsync_LocalSource_UsesFileProbe` | `MediaDurationProbeServiceTests` | `LocalDirectory` → `ProbeFileAsync(item.Path)` |
| `ProbePendingAsync_SftpSource_StreamsViaOpenFileStream` | `MediaDurationProbeServiceTests` | SFTP → `OpenFileStream` → `ProbeStreamAsync`; Stream == null → fehlgeschlagene Probe |
| `ProbePendingAsync_ProbeThrows_MarksFailedAndContinues` | `MediaDurationProbeServiceTests` | Exception je Item → Fehlprobe, nächstes Item läuft |
| `ProbePendingAsync_NoFfprobe_LeavesItemsUnprobed` | `MediaDurationProbeServiceTests` | Locator liefert `null` → keine Markierung, Rückgabe 0 |
| `ProbePendingAsync_RespectsBatchSize` | `MediaDurationProbeServiceTests` | Max. `BatchSize` Items je Aufruf |
| `TryParseDurationOutput_*` | `FfprobeMediaDurationProbeTests` | `"123.456"`→`TimeSpan`; `"N/A"`/`""`/Müll → `null` |
| `ProbeFileAsync_NonZeroExit_ReturnsNull` / `TimedOut_ReturnsNull` / `ParsesStdout` | `FfprobeMediaDurationProbeTests` | Exit-Code/Timeout/Parsing über injiziertes Prozess-Delegate |
| `ProbeStreamAsync_PipesStdin` | `FfprobeMediaDurationProbeTests` | Stream wird an `pipe:0` übergeben (Delegate erhält Stream) |
| `Resolve_ConfiguredPath_Wins` / `Resolve_ConventionDir` / `Resolve_PathFallback` / `Resolve_NotFound_ReturnsNull` | `FfprobeLocatorTests` | Auflösungsreihenfolge mit Temp-Verzeichnissen |
| `ScanNextMediaCollection_ChangedFile_ResetsDuration` | `MediaSourceScannerLocalTests` (erweitern) oder `MediaDurationProbeScannerTests` | `CreatedAt`-Wechsel setzt `Duration`/`DurationProbedAt` zurück |
| `ReadFromAsync_LegacyBackupWithoutDurationColumns_RestoresSuccessfully` | `VideoWebPlayerBackupDataTests` | Alt-Backup ohne `MediaItems.Duration`/`DurationProbedAt` wird wiederhergestellt (Vorlage: `...WithoutPlaylistIdColumnInContinueWatchingEntries...` + `LegacyBackupArchiveBuilder.RemoveColumnsAsync`) |
| `ExecuteAsync_ProcessesPendingItems` | `MediaDurationProbeWorkerTests` | Worker ruft `ProbePendingAsync` nach `InitialDelay`/Intervall auf (Muster `PlaylistBackfillWorkerTests`) |
| `FakeMediaDurationProbe` / `StubFfprobeRunner` | Test-Helfer | `IMediaDurationProbe`-Fake mit einstellbaren Ergebnissen |

### Betroffene bestehende Tests

| Test / Testklasse | Grund der Anpassung |
|-------------------|---------------------|
| `VideoWebPlayerBackupDataTests`-Hilfen (`LegacyBackupArchiveBuilder`) | keine Anpassung nötig — `RemoveColumnsAsync` kann beliebige Spalten entfernen; nur neuer Testfall nötig |
| `MediaSourceScannerLocalTests` | neue Assertions bzw. neuer Test für `Duration`-Reset (kein Umbau vorhandener Tests) |

### E2E-Tests (primärer Funktionsnachweis)

Die Anforderung enthält **keinen Benutzerfluss**: Weder legt der Anwender etwas an noch interagiert er mit einer Oberfläche — die Laufzeitermittlung läuft vollautomatisch im Hintergrund (Scan-Integration + Backfill-Worker), und die Erkennbarkeit fehlgeschlagener Probes ist ein Datenbankzustand, der erst in Schritt 3 (Sendeplanung) fachlich konsumiert wird. Es gibt keine neue Seite, kein Bedienelement, keine Navigation und keine anwendersichtbare Rückmeldung — dementsprechend ist kein E2E-Szenario als primärer Funktionsnachweis erforderlich; Unit-/Integrationstests gegen echtes SQLite decken das Verhalten vollständig ab.

Bestehende E2E-Tests sind nicht betroffen (kein geändertes UI, keine geänderten Endpunkte).

## Offene Punkte

Keine.
