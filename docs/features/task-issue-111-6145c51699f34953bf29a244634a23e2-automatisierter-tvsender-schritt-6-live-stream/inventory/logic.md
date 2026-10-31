# Bestandsaufnahme: Logik

## `ProgramGuideService`
Datei: `VideoWebPlayer/Services/ProgramGuideService.cs` — scoped (`AddScoped<IProgramGuideService, ProgramGuideService>`)

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `GetNowAsync(utcNow, ct)` | public | Zeitbasis des Streams: laufender `Scheduled`-Slot (`Start <= now < End`) als `DtoProgramGuideEntry` mit `PositionSeconds`/`RemainingSeconds` + nächster `Scheduled`-Slot; `Now == null` bei Sendelücke; rein lesend, zustandslos |
| `GetUpcomingAsync(utcNow, preview, ct)` | public | Begrenzte Vorschau (max. `MaxPreviewHours` = 12 h), nur `Scheduled` |
| `GetChannelIdAsync` | private | Erster `TVChannel` lesend (kein Seiteneffekt-Anlegen) |
| `LoadTitlesAsync` | private | Batch-Titelauflösung Filme/Episoden |

## `ProgramScheduleWorker`
Datei: `VideoWebPlayer/Services/ProgramScheduleWorker.cs` — `BackgroundService`, per `AddHostedService` registriert

| Element | Beschreibung |
|---------|--------------|
| `ExecuteAsync` | Startup-Delay (15 s) → Schleife: Scope öffnen → Intervall aus `ProgramSettingsService` lesen → `IBackgroundProcessingGate.EnterOperationAsync("Sendeplan")` → `ReconcileAsync`+`EnsureCoverageAsync` → schläft `min(Intervall, Signal)` |
| Konstruktor | `IServiceScopeFactory`, `ILogger`, optional `IProgramScheduleSignal`, `TimeSpan? initialDelay`, `TimeProvider` — Muster für Testbarkeit |

## `FfprobeLocator`
Datei: `VideoWebPlayer/Services/FfprobeLocator.cs` — Singleton (`IFfprobeLocator`)

| Methode | Kurzbeschreibung |
|---------|------------------|
| `Resolve()` | `MediaProbeOptions.FfprobePath` (nur wenn Datei existiert) → Konventionskandidaten `{BaseDirectory}/tools/ffmpeg/{name}` + `{BaseDirectory}/{name}` → PATH-Iterieren. `internal` Testkonstruktor mit `baseDirectory`+`pathEnvironment`-Injektion |

## `FfprobeMediaDurationProbe`
Datei: `VideoWebPlayer/Services/FfprobeMediaDurationProbe.cs` — Singleton (`IMediaDurationProbe`)

| Methode | Kurzbeschreibung |
|---------|------------------|
| `ProbeFileAsync` / `ProbeStreamAsync` | ffprobe-Aufruf mit injizierbarem `_executeAsync`-Delegat (Signatur `fileName, args, stdin, timeout, ct → FfprobeInvocationResult`) — Prozessausführung in Tests fälschbar; `stdin`-Pump-Muster vorhanden (`CopyToAsync` in `process.StandardInput.BaseStream`) |
| `ExecuteProcessAsync` | `ProcessStartInfo` mit `RedirectStandard*`, `UseShellExecute=false`, `CreateNoWindow=true`, `WaitForExitAsync` mit Timeout-CTS, `Kill(entireProcessTree: true)` bei Abbruch — wiederverwendbares Prozessmuster |

## `MediaSourceReaderDispatcher` / `LocalMediaSourceReader` / `SftpMediaSourceReader`
Dateien: `VideoWebPlayer/Services/MediaSourceReaderDispatcher.cs`, `LocalMediaSourceReader.cs`, `SftpMediaSourceReader.cs` — scoped (`IMediaSourceReader` → Dispatcher)

| Methode | Kurzbeschreibung |
|---------|------------------|
| `GetReader` (Dispatcher) | `SourceType == LocalDirectory` → `LocalMediaSourceReader`, sonst `SftpMediaSourceReader`; wirft `InvalidOperationException`, wenn `MediaSource` nicht eager geladen |
| `OpenFileStream(collection, fileName)` | Liefert Datei-Stream oder `null` — der quell-unabhängige Lesezugriff (ItemsController-Muster) |
| `ResolveFilePath` (LocalReader, privat) | `Path.GetFullPath(Combine(collection.Path, fileName))` mit Containment-Check gegen `MediaSource.Path` + segmentweise case-insensitive Auflösung + ReparsePoint-Abweisung — physischer Pfad nur intern verfügbar |

## `ItemsController` (Referenz: Dateiauslieferung + `access_token`-Nutzung)
Datei: `VideoWebPlayer/Controllers/ItemsController.cs`

| Methode | Kurzbeschreibung |
|---------|------------------|
| `OpenMediaFileAsync(type, id)` | Slot/Item → `MediaItem` → `MediaCollection`+`MediaSource` → `_reader.OpenFileStream(collection, Path.GetFileName(mediaItem.Path))` → `File(stream, contentType, enableRangeProcessing: true)` — die etablierte Auflösungskette MediaItem→Datei |
| `StreamMediaItem` / `Download` | `CheckLogedIn()` + `ExecuteAsync`-Fehlermapping; die Blazor-UI ruft diese URLs mit `?access_token={token}` auf |

## `ProgramSettingsService`
Datei: `VideoWebPlayer/Services/ProgramSettingsService.cs` — scoped

| Methode | Kurzbeschreibung |
|---------|------------------|
| `GetOrCreateSetupAsync` | Singleton-Setupzeile; korrigiert ungültige Werte auf Defaults |
| `GetProgramScheduleHorizonAsync` / `GetProgramScheduleIntervalAsync` | `<= 0` → Default (48 h / 15 min) — Normalisierungsmuster |
| `UpdateProgramScheduleSettingsAsync(h, m)` | Validierung `< 1` → `ArgumentException`, dann persistieren — Muster für Stream-Settings |
| `UpdateHolidayFederalStateAsync` | Wertebereichsvalidierung — Muster für Pfad-Validierung |

## `VideoWebPlayerBackupData`
Datei: `VideoWebPlayer/Services/Backups/VideoWebPlayerBackupData.cs`

| Element | Kurzbeschreibung |
|---------|------------------|
| `OptionalRestoreTables` / `OptionalRestoreColumns` | Neue Tabellen/Spalten hier registrieren, damit Alt-Backups ohne sie wiederherstellbar bleiben (`Setups.*`-Einträge vorhanden) |
| `OptionalRestoreIntDefaults` / `BoolDefaults` / `LongDefaults` | Ersatzwerte beim Restore eines Alt-Backups ohne die Spalte |
| `IgnoredRestoreColumns` | Bekannte Schema-Artefakte |

## `TVSenderAdmin` (UI)
Datei: `VideoWebPlayer/Components/Pages/Admin/TVSenderAdmin.razor`

| Element | Kurzbeschreibung |
|---------|------------------|
| Sender-Karte (Zeilen 35–70) | `admin-settings-grid`-Felder (Name, Bundesland, Horizont, Intervall) + `SaveChannelSettingsAsync` — hier gehören die Stream-Einstellungen hin |
| `OnInitializedAsync` | Admin-Claim-Check (`IsAdmin`), lädt Settings über `ProgramSettings`-Methoden |

## `ServiceCollectionExtensions`
Datei: `VideoWebPlayer/Extensions/ServiceCollectionExtensions.cs`

| Element | Kurzbeschreibung |
|---------|------------------|
| `Configure<MediaProbeOptions>("MediaProbe")` | Options-Binding; `FfprobePath` vorhanden, `FfmpegPath` fehlt |
| `AddSingleton<IFfprobeLocator, FfprobeLocator>` | Locator-Registrierung (Singleton) |
| `AddHostedService<...>` | Worker-Registrierungen; `MdnsAdvertiserWorker` zeigt das `!env.IsEnvironment("Testing")`-Guard-Muster für umgebungsabhängige Worker |
| `SymmetricSecurityKey`-Singleton | JWT-Schlüssel, den `BearerTokenCheckAttribute` zur Validierung nutzt |
