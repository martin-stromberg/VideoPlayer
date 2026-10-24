# Umsetzungsplan: Live-Stream ausliefern (Schritt 6)

## Übersicht

Der automatisierte TV-Sender wird als durchgehender HLS-Live-Stream ausgeliefert: Ein Hintergrund-Worker (`LiveStreamWorker`) synchronisiert fortlaufend einen `ffmpeg`-Segmentierungsprozess mit dem laut `IProgramGuideService` laufenden Sendeplatz — rein zeitbasiert, ohne Prozesszustand. Segmente und Wiedergabeliste liegen in einem konfigurierbaren Verzeichnis, abgelaufene Segmente werden automatisiert aufgeräumt, und die Auslieferung läuft über eigene authentifizierte Endpunkte mit dem `access_token`-Query-Muster.

## Designentscheidungen

| Komponente / Bereich | Gewählter Ansatz | Begründung |
|----------------------|-----------------|------------|
| Zeitbasis | `IProgramGuideService.GetNowAsync` je Tick; kein persistenter Stream-Zustand | Anforderung „rein zeitbasiert"; Neustart synchronisiert sich über Uhrzeit allein |
| Prozesssteuerung | Ein `ffmpeg`-Prozess je laufendem Slot; bei Slot-Wechsel/Lücke/Drift stoppen und auf dem Folgeslot neu starten (`-ss`-Offset + `-t`-Restdauer) | Vorgegebene Umschaltstrategie; Neustart = saubere Diskontinuität (neue Wiedergabeliste, eindeutige Segmentnamen) |
| Eingangslokalisierung | `LocalDirectory`-Quellen → physischer Dateipfad an ffmpeg; andere Quellen → `OpenFileStream` an `stdin` (`pipe:0`) | Empirisch verifiziert: `-ss` vor `-i` sucht bei Datei direkt (Keyframe-Seek), bei Pipe verwirft ffmpeg Pakete ohne Dekodierung; lokale Dateien umgehen so die doppelte I/O durch den Server und funktionieren auch für MP4 ohne faststart |
| ffmpeg-Echtzeit | `-re` vor `-i` | Ohne `-re` würde ffmpeg so schnell segmentieren wie die CPU erlaubt — der Stream würde dem Sendeplan davonlaufen |
| Transcodierung | `-c:v libx264 -preset veryfast -c:a aac` mit `-force_key_frames expr:gte(t,n_forced*{seg})` | Beliebige Bibliotheksinhalte (MPEG-2, AVI, VP9 …) brauchen einheitliche HLS-fähige Ausgabe; `-c copy` scheidet an ungeeigneten Codecs und ungenauen Segmentgrenzen aus |
| HLS-Fenster | `-hls_time {Segmentlänge}`, `-hls_list_size 12` (Konstante), `-hls_flags delete_segments+independent_segments`, `-strftime 1` Segmentnamen `seg_%Y%m%d%H%M%S_{slotId}_%05d.ts` | Kollisionsfreie Namen über Prozess-Neustarts hinweg; `delete_segments` hält das Live-Fenster sauber, eigene Retention räumt Reste (Neustart-Reste, verwaiste Läufe) |
| Playlist-Token | `.m3u8` wird beim Ausliefern gelesen und jede Segment-URI mit `?access_token={token}` versehen | Relative URIs übernehmen den Query-String der Playlist-URL nicht — ohne Umschreiben wären Segment-Abrufe unautorisiert |
| Prozess-Testseam | Injizierbare Prozess-Handle-Fabrik (`internal` Ctor, `InternalsVisibleTo` vorhanden) | Muster von `FfprobeMediaDurationProbe._executeAsync`; kein echtes ffmpeg in Unit-Tests nötig |
| Drift-Korrektur | Konstante `MaxDriftSeconds = 30`: weicht die vom Prozess erwartete Position um mehr ab, Neustart mit korrektem Offset | ffmpeg kann ausfallen/hinterherhinken; ohne Korrektur bliebe der Stream dauerhaft hinter dem Programmpunkt |
| Worker-Guard | `AddHostedService<LiveStreamWorker>` nur wenn `!env.IsEnvironment("Testing")` | `MdnsAdvertiserWorker`-Muster; in `WebApplicationFactory`-Tests darf kein ffmpeg starten |
| Aufräumstrategie | Einmalige Leerung des Segment-Verzeichnisses beim Worker-Start + Retention-Sweep je Tick (Segmentdateien älter als Retention löschen) | Stale Dateien aus früheren Läufen sind fachlich nutzlos; beide Mechanismen zusammen decken laufenden Betrieb und Neustart ab |
| Einstellungen | `Setup`-Spalten + `ProgramSettingsService` + `TVSenderAdmin`-Felder | „Etablierte Einstellungsmechanismen" (wie Horizont/Intervall/Bundesland) |

## Programmabläufe

### Segmentierung laufend halten (Worker-Tick)

1. `LiveStreamWorker` wartet nach Start-Verzögerung, leert einmalig das Segment-Verzeichnis (`LiveStreamSegmentStore.ClearAsync`).
2. Je Tick (Scope + `IBackgroundProcessingGate.EnterOperationAsync("Live-Stream")`): `LiveStreamCoordinator.SynchronizeAsync(utcNow)`.
3. Koordinator: `IFfmpegLocator.Resolve()` — `null` → laufenden Prozess stoppen, Ergebnis `FfmpegUnavailable` (Worker loggt gedrosselt).
4. `IProgramGuideService.GetNowAsync(utcNow)`:
   - `Now == null` (Lücke): Prozess stoppen; `WakeAtUtc` = `Next.StartUtc` (oder Poll-Cap).
   - `Now != null`: `LiveStreamMediaResolver.ResolveAsync(MediaItemId)` → `LiveStreamInput` (Pfad oder Stream-Fabrik); `null` (Item/Collection/Datei weg) → wie Lücke behandeln.
   - `CurrentSlotId == SlotId` und `IsRunning` und Drift ≤ 30 s → nichts tun; sonst `StopAsync` + `StartAsync` mit `PositionSeconds`/`RemainingSeconds`.
   - `WakeAtUtc` = `Now.EndUtc`.
5. `LiveStreamSegmentStore.SweepExpiredSegmentsAsync(utcNow, Retention)` je Tick.
6. Worker schläft `min(WakeAtUtc - now, PollInterval 30 s)` bzw. bis `IProgramScheduleSignal` feuert.

### ffmpeg-Aufruf

`-hide_banner -loglevel warning -y -re -ss {PositionSeconds:F3} -i {Eingang|"pipe:0"} -map 0:v:0 -map 0:a:0? -c:v libx264 -preset veryfast -c:a aac -b:a 128k -force_key_frames "expr:gte(t,n_forced*{SegmentSeconds})" -f hls -hls_time {SegmentSeconds} -hls_list_size 12 -hls_flags delete_segments+independent_segments -strftime 1 -hls_segment_filename "{Dir}/seg_%Y%m%d%H%M%S_{SlotId}_%05d.ts" -t {RemainingSeconds} "{Dir}/live.m3u8"`

- Datei-Eingang: Pfad als Argument, `stdin` sofort geschlossen. Stream-Eingang: `pipe:0`, Pump-Task kopiert `OpenFileStream`-Stream in `process.StandardInput`.
- Stop: `Kill(entireProcessTree: true)` nach dem `FfprobeMediaDurationProbe`-Muster.

### HLS-Auslieferung

1. `GET /api/live-stream/{fileName}` — `[BearerTokenCheck]` + `CheckLoggedIn`.
2. Name-Whitelist (`^[A-Za-z0-9][A-Za-z0-9._-]*$`) + Containment im Segment-Verzeichnis → sonst `NotFound`.
3. `.m3u8`: Text lesen, jede Nicht-Kommentar-URI-Zeile um `?access_token={Request.Query["access_token"]}` ergänzen (nur wenn vorhanden), `application/vnd.apple.mpegurl`, `Cache-Control: no-store`.
4. `.ts` (und sonstige Dateien): `PhysicalFile` mit `video/mp2t` bzw. `application/octet-stream`; fehlende Datei → `NotFound`.

## Neue Klassen

| Klasse | Typ | Zweck |
|--------|-----|-------|
| `IFfmpegLocator` | Interface | `string? Resolve()` — ffmpeg-Binary lokalisieren |
| `FfmpegLocator` | Klasse (Singleton) | Wie `FfprobeLocator`: `MediaProbeOptions.FfmpegPath` → `tools/ffmpeg/` + App-Verzeichnis → PATH; ausführbarer Name `ffmpeg(.exe)`; interner Testkonstruktor (`baseDirectory`, `pathEnvironment`) |
| `LiveStreamInput` | Record | ffmpeg-Eingangsspezifikation: entweder `FilePath` oder `Func<CancellationToken, Task<Stream?>>` Stream-Fabrik |
| `LiveStreamJob` | Record | Ein Segmentierungsauftrag: `SlotId`, `Input`, `PositionSeconds`, `RemainingSeconds`, `SegmentSeconds`, `SegmentDirectory` |
| `ILiveStreamSegmenter` | Interface | `StartAsync(LiveStreamJob, ct)`, `StopAsync()`, `IsRunning`, `CurrentSlotId`, `ExpectedPositionSeconds` |
| `FfmpegLiveStreamSegmenter` | Klasse (Singleton) | Besitzt den laufenden ffmpeg-Prozess; Argumentbau, stdin-Pump, `Kill(entireProcessTree)`; interne Prozess-Handle-Fabrik `ISegmenterProcess` (HasExited/Kill/WaitForExitAsync) für Tests |
| `LiveStreamMediaResolver` | Klasse (scoped) | `MediaItemId` → `MediaItem` → `MediaCollection`+`MediaSource` → `LiveStreamInput?` (Local → `LocalMediaSourceReader.ResolveLocalFilePath`, sonst `OpenFileStream`-Fabrik) |
| `LiveStreamSegmentStore` | Klasse (scoped) | Effektives Segment-Verzeichnis (Setup-Wert relativ→ContentRoot/absolut, Default `Data/LiveStream`), `TryResolveFileAsync` (Whitelist + Containment), `SweepExpiredSegmentsAsync`, `ClearAsync` |
| `LiveStreamSyncResult` | Record | `WakeAtUtc`, `Action` (Started/Resumed/Stopped/Gap/FfmpegUnavailable/MediaUnavailable), `Detail` |
| `LiveStreamCoordinator` | Klasse (scoped) | `SynchronizeAsync(utcNow, ct)` — Entscheidungslogik gemäß Ablauf; Konstante `MaxDriftSeconds` |
| `LiveStreamWorker` | `BackgroundService` | Initialdelay, einmaliges `ClearAsync`, Tick-Schleife (Gate-Lease, Poll-Cap 30 s, `IProgramScheduleSignal`-Weckung) |
| `LiveStreamController` | Controller | `api/live-stream/{fileName}` — `.m3u8`-Rewrite + Segment-Auslieferung |

## Änderungen an bestehenden Klassen

### `MediaProbeOptions` (Konfiguration)

- **Neue Eigenschaft:** `FfmpegPath` (`string?`) — konfigurierbarer ffmpeg-Pfad.

### `Setup` (Datenmodellklasse)

- **Neue Eigenschaft:** `LiveStreamSegmentLengthSeconds` (`int`, Default 6) — Ziel-Segmentdauer.
- **Neue Eigenschaft:** `LiveStreamSegmentDirectory` (`string?`, Default `null`) — Segment-Verzeichnis; `null`/leer = `Data/LiveStream` unterhalb des ContentRoots.
- **Neue Eigenschaft:** `LiveStreamSegmentRetentionMinutes` (`int`, Default 30) — Aufbewahrung abgelaufener Segmente.

### `ProgramSettingsService` (Service)

- **Neue Methoden:** `GetLiveStreamSegmentLengthAsync` (`<=0` → 6 s), `GetLiveStreamSegmentDirectoryAsync` (`string?`, unverändertes Setup-Feld), `GetLiveStreamSegmentRetentionAsync` (`<=0` → 30 min), `UpdateLiveStreamSettingsAsync(int segmentLengthSeconds, string? segmentDirectory, int retentionMinutes)` mit Validierung (`ArgumentException` bei `segmentLengthSeconds < 1 || > 300`, `retentionMinutes < 1`, ungültigem Pfad — `Path.GetFullPath`-Probe).

### `LocalMediaSourceReader` (Service)

- **Neue Methode:** `ResolveLocalFilePath(MediaCollection collection, string fileName)` → `string?` — öffentlicher Wrapper um die vorhandene `ResolveFilePath`-Logik (Containment + Case-Auflösung), gibt den physischen Pfad zurück.

### `VideoWebPlayerBackupData` (Backup)

- `OptionalRestoreColumns`: drei `Setups.*`-Einträge für die neuen Spalten.
- `OptionalRestoreIntDefaults`: `(Setups, LiveStreamSegmentLengthSeconds, 6)`, `(Setups, LiveStreamSegmentRetentionMinutes, 30)`.

### `TVSenderAdmin` (Blazor)

- Sender-Karte: drei neue Eingabefelder (`#tvsender-segment-length-seconds`, `#tvsender-segment-directory`, `#tvsender-segment-retention-minutes`); Laden in `OnInitializedAsync`, Persistieren in `SaveChannelSettingsAsync` über `UpdateLiveStreamSettingsAsync`.

### `ServiceCollectionExtensions` (DI)

- `AddSingleton<IFfmpegLocator, FfmpegLocator>()`; `AddSingleton<ILiveStreamSegmenter, FfmpegLiveStreamSegmenter>()`; `AddScoped<LiveStreamMediaResolver>()`; `AddScoped<LiveStreamSegmentStore>()`; `AddScoped<LiveStreamCoordinator>()`; `AddHostedService<LiveStreamWorker>()` nur bei `!env.IsEnvironment("Testing")`.

### `docs/API.md`

- Neue Abschnitte `### GET /api/live-stream/{fileName}` (Wiedergabeliste + Segmente, `access_token`-Muster, `.m3u8`-Umschreibung).

## Datenbankmigrationen

| Migrationsname | Betroffene Tabellen/Spalten | Beschreibung der Änderung |
|----------------|----------------------------|---------------------------|
| `AddSetupLiveStreamSettings` | `Setups`: `LiveStreamSegmentLengthSeconds` (int, Default 6), `LiveStreamSegmentDirectory` (TEXT, nullable), `LiveStreamSegmentRetentionMinutes` (int, Default 30) | Neue Sender-Stream-Einstellungen |

## Validierungsregeln

| Feld / Objekt | Regel | Fehlerfall |
|---------------|-------|------------|
| `segmentLengthSeconds` | ≥ 1 und ≤ 300 | `ArgumentException` → UI-Fehlermeldung |
| `retentionMinutes` | ≥ 1 | `ArgumentException` |
| `segmentDirectory` | `null`/leer oder von `Path.GetFullPath` akzeptierter Pfad | `ArgumentException` |
| `fileName` (Stream-Auslieferung) | Whitelist `^[A-Za-z0-9][A-Za-z0-9._-]*$`, aufgelöster Pfad im Segment-Verzeichnis, Datei existiert | `404 NotFound` |

## Konfigurationsänderungen

| Eintrag | Typ | Standardwert | Zweck |
|---------|-----|--------------|-------|
| `MediaProbe:FfmpegPath` | `string?` | `null` | ffmpeg-Binary (konfigurierbarer Pfad, erste Suchstufe) |
| `Setups.LiveStreamSegmentLengthSeconds` | `int` | 6 | Segmentlänge in Sekunden (Admin-UI) |
| `Setups.LiveStreamSegmentDirectory` | `string?` | `null` → `Data/LiveStream` | Segment-Speicherort (Admin-UI) |
| `Setups.LiveStreamSegmentRetentionMinutes` | `int` | 30 | Segment-Retention (Admin-UI) |

## Seiteneffekte und Risiken

- **MP4 ohne faststart über Pipe (Nicht-Local-Quellen):** moov am Dateiende → ffmpeg kann stdin nicht demuxen; Prozess endet mit Fehler, Slot gilt als nicht streambar (Log, Lücke). Dokumentierte Einschränkung — `LocalDirectory` ist der Referenzpfad.
- **`LocalMediaSourceReader`**: neue öffentliche Methode delegiert auf bestehende sichere Auflösung — kein neuer Pfad-Code.
- **CPU-Last:** Dauerhafte Transcodierung — bewusste Entscheidung zugunsten universeller Abspielbarkeit.
- **Playlist-Token:** Umschreiben der `.m3u8` pro Request — kleine Datei, kein nennenswerter Aufwand.
- **Restore-Betrieb:** Gate-Lease je Tick; ffmpeg schreibt nur Dateien (kein DB-Zugriff) und läuft während eines Restarts nicht gegen die Datenbank.
- **`ApiDocumentationContractTests`:** berührt nur `PlaylistsController`-Routen + Pflichtliste; neue Route wird dokumentiert (Konvention), kein Testbruch erwartet.

## Umsetzungsreihenfolge

1. **`Setup`-Spalten + Migration `AddSetupLiveStreamSettings` + Backup-Registrierung**
   - Voraussetzungen: keine
   - `Setup.cs` drei Felder; `dotnet ef migrations add AddSetupLiveStreamSettings --project VideoWebPlayer`; `VideoWebPlayerBackupData` Optional-Einträge.
2. **`MediaProbeOptions.FfmpegPath` + `IFfmpegLocator`/`FfmpegLocator`**
   - Voraussetzungen: keine
   - `FfprobeLocator`-Muster kopieren, Executable-Name `ffmpeg`.
3. **`ILiveStreamSegmenter`/`FfmpegLiveStreamSegmenter` + Prozess-Testseam**
   - Voraussetzungen: Schritt 2
   - Argumentbau, Start/Stop/IsRunning, stdin-Pump, Kill-Baum, interne `ISegmenterProcess`-Fabrik.
4. **`LocalMediaSourceReader.ResolveLocalFilePath` + `LiveStreamMediaResolver`**
   - Voraussetzungen: keine
   - Public-Wrapper + Resolver-Service.
5. **`LiveStreamSegmentStore`**
   - Voraussetzungen: Schritt 1
   - Verzeichnisauflösung (Default `Data/LiveStream`), Datei-Whitelist, Sweep, Clear.
6. **`LiveStreamCoordinator`**
   - Voraussetzungen: 2–5
   - `SynchronizeAsync`-Entscheidungslogik + `LiveStreamSyncResult`.
7. **`LiveStreamWorker` + DI-Registrierungen**
   - Voraussetzungen: 2–6
   - BackgroundService, Gate-Lease, Signal-Weckung, Testing-Guard.
8. **`LiveStreamController`**
   - Voraussetzungen: 5
   - Auslieferung + `.m3u8`-Rewrite.
9. **`ProgramSettingsService`-Methoden**
   - Voraussetzungen: 1
   - Getter/Update mit Validierung.
10. **`TVSenderAdmin`-Felder**
    - Voraussetzungen: 9
    - Drei Eingabefelder + Load/Save.
11. **Tests** (parallel/anschließend)
    - Voraussetzungen: je Komponente
12. **Dokumentation** (`docs/API.md`, `docs/help/tv-sender.md`, `docs/RELEASE_NOTES.md`, `changes.log`, ffmpeg-Lizenz/-Bereitstellung)
    - Voraussetzungen: alle

## Tests

### Neue Tests

| Test / Hilfsmethode | Testklasse | Was wird geprüft / bereitgestellt? |
|--------------------|------------|-------------------------------------|
| `FfmpegLocatorTests` | `Services/FfmpegLocatorTests.cs` | Suchreihenfolge konfigurierbarer Pfad → Konventionsverzeichnisse → PATH; `null` bei Nichtfund; spiegelt `FfprobeLocatorTests` |
| `FfmpegLiveStreamSegmenterTests` | `Services/` | Fake-`ISegmenterProcess`: Argumente korrekt (`-re`, `-ss`, `-t`, `-f hls`, Segmentnamen, Playlist-Pfad), Datei- vs. Pipe-Eingang (stdin-Pump), Start/Stop/IsRunning, Doppelstart-Abwehr, Kill-Baum, erwartete Position |
| `LiveStreamCoordinatorTests` | `Services/` (SQLite `ProgramScheduleTestDb`) | Laufender Slot → Start mit Pfad/Position; Slot-Wechsel → Neustart; Lücke → Stop; ffmpeg fehlt → kein Start + `FfmpegUnavailable`; MediaItem fehlt → `MediaUnavailable`; SFTP → Stream-Fabrik; Drift > 30 s → Neustart; WakeAtUtc-Werte |
| `LiveStreamMediaResolverTests` | `Services/` | Local → Dateipfad (`ResolveLocalFilePath`), SFTP → `OpenFileStream`-Fabrik, fehlendes Item/Collection → `null` |
| `LiveStreamSegmentStoreTests` | `Services/` | Verzeichnis-Auflösung (Default/konfiguriert relativ/absolut), `TryResolveFileAsync` (Whitelist, Containment, Existenz), Sweep löscht alte `seg_*.ts` und behält `live.m3u8` + frische Dateien, `ClearAsync` |
| `LiveStreamControllerTests` | `Controllers/` | `.m3u8`-Rewrite injiziert `access_token` in URI-Zeilen; `.ts` Content-Type `video/mp2t`; ungültiger Name → `NotFound`; fehlende Datei → `NotFound` |
| `ProgramSettingsServiceLiveStreamTests` | `ProgramSettingsServiceTests.cs` (Erweiterung oder neue Klasse) | Defaults, Update+Get-Roundtrip, Validierungsfehler |
| `LiveStreamWorkerTests` | `VideoWebPlayer.Tests/` | Startup-Clear + Tick-Aufruf, Poll-Cap, Signal-Weckung (leichtgewichtig, Fakes) |
| `ReadFromAsync_LegacyBackupWithoutLiveStreamSettings_RestoresSuccessfully` | `Services/Backups/VideoWebPlayerBackupDataTests.cs` | `RemoveColumnsAsync` der drei neuen `Setups`-Spalten → Restore ohne Fehler, Defaults greifen |

### Betroffene bestehende Tests

| Test / Testklasse | Grund der Anpassung |
|-------------------|---------------------|
| `TVSenderAdminE2ETests.Admin_CreateEditDeactivateDeleteRule` | Erweiterung um die drei Stream-Einstellungsfelder (füllen → speichern → `Setups`-Werte prüfen) — konsolidiert im selben Browserlauf |

### E2E-Tests (primärer Funktionsnachweis)

Der Benutzerfluss dieses Schritts ist der authentifizierte Abruf von Wiedergabeliste und Segmenten (die eigentliche Player-Oberfläche ist Schritt 7) sowie die Admin-Einstellung der Stream-Parameter.

| Priorität | Szenario | Testdatei / Testklasse | Abgedecktes Akzeptanzkriterium | Warum E2E nötig ist |
|-----------|----------|------------------------|-------------------------------|-------------------|
| Pflicht | Anonymer Abruf von Playlist + Segment → 401 | `LiveStreamE2ETests` (`DeviceClientTestBase`) | Authentifizierte Auslieferung | Serverseitige Durchsetzung nur über HTTP nachweisbar |
| Pflicht | Gepaartes Gerät ruft Playlist via `access_token` ab; URIs enthalten Token; Segment 200 | `LiveStreamE2ETests` | Token-Muster + Umschreibung | Ende-zu-Ende-Nachweis des Auslieferungspfads |
| Pflicht | Fehlende/ungültige Datei → 404; Traversal-Sicherheit | `LiveStreamE2ETests` | Pfadsicherheit, verständlicher Zustand | Endpunktverhalten über HTTP |
| Pflicht | Admin setzt Segmentlänge/-verzeichnis/Retention über TV-Sender-Seite | `TVSenderAdminE2ETests` (erweitert) | Konfigurierbarkeit über Admin-UI | Echter UI-Speicherfluss |

## Offene Punkte

Keine.
