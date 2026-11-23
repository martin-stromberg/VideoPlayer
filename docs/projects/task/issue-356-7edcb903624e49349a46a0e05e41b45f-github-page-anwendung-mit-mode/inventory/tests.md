# Tests

## Test-Ausgangszustand vor der Umsetzung

- Zeitpunkt (mit Zeitzone): 2026-11-23; Baseline-Lauf 09:42–10:20 MEZ
  (UTC+01:00), Fokuslauf ~15:0x MEZ.
- Branch und Commit-ID:
  `task/issue-356-7edcb903624e49349a46a0e05e41b45f-github-page-anwendung-mit-mode`,
  Commit `5e4737e541c90b139ee7ecdd35fbbd871c791e8c`
  („docs: Produktions-Installationsanleitung für Linux-Server … (#379)").
- Uncommittete Änderungen im getesteten Stand: nur untracked Dokumentation
  (`docs/features/task/issue-356-…/`, `docs/projects/task/issue-356-…/`,
  `…-tasks.md`). **Keine Code-Änderungen** — Baseline und Fokuslauf testen
  denselben Commit wie der aktuelle Arbeitsstand.
- Testumgebung: Windows (Git Bash), .NET SDK 11.0.100 (Runtime 10.0.15,
  net10.0), Release-Build, Node v24.15.0, Playwright-Chromium installiert.
- Ermittelte Testsuiten: `VideoWebPlayer.Tests` (xUnit v3;
  deterministische Lane `Category!=OsInterface` + OsInterface-Lane mit
  Playwright-E2E, echten Ports/Prozessen/SQLite-Dateien),
  `tools/MarkdownLinkCheck.Tests`, Tool-Läufe `MarkdownLinkCheck`+`SecretScan`
  (pre-commit). Quellen: `AGENTS.md` DoD, `staging-ci.yml`, `release.yml`.

### Wiederverwendete Baseline

Der vollständige Suite-Lauf wurde für die Vor-Bestandsaufnahme desselben Issues
am selben Tag auf **demselben Commit ohne jede Code-Änderung** ausgeführt und
ist hier übernommen statt neu erhoben (Details und Einordnung in der
[Baseline-TRX](test-results/baseline-test-results.trx)).

### Testläufe

| Lauf | Befehl inkl. Filter | Arbeitsverzeichnis | Exit-Code | Erfolgreich | Fehlgeschlagen | Übersprungen | Nachweis |
|------|--------------------|--------------------|-----------|-------------|----------------|--------------|----------|
| 1 Volle Suite (Baseline) | `dotnet test VideoWebPlayer.Tests/VideoWebPlayer.Tests.csproj --no-build -c Release` | Repo-Root | 1 | 2969 | 2 | 2 | [baseline-test-results.trx](test-results/baseline-test-results.trx) |
| 2 Wiederholung der 2 Fehlschläge | `dotnet test … --filter "FullyQualifiedName~FfmpegLiveStreamSegmenterE2ETests.RealFfmpeg…\|…TvSenderOnAirE2ETests.Admin_TogglesOffAir"` | Repo-Root | 0 | 2 | 0 | 0 | [retry-test-results.trx](test-results/retry-test-results.trx) |
| 3 Fokuslauf (heute) | `dotnet test VideoWebPlayer.Tests/VideoWebPlayer.Tests.csproj --no-build -c Release --filter "FullyQualifiedName~LocalMediaSource\|FullyQualifiedName~MediaSource"` | Repo-Root | 0 | 120 | 0 | 0 | [demo-inventory-local-source.trx](test-results/demo-inventory-local-source.trx) |

### Nachgewiesene bestehende Testfehler

| Test-ID inkl. Testfall | Suite / Dateipfad | Fehlerbild / Fehlermeldung | Lauf und Nachweis |
|-----------------------|-------------------|----------------------------|-------------------|
| `…FfmpegLiveStreamSegmenterE2ETests.RealFfmpeg_ProducesPlaylistAndSegments` | E2E (OsInterface, echtes ffmpeg) | `IOException`: `live.m3u8` „is being used by another process" — Dateisperre unter Volllast | Lauf 1, [TRX](test-results/baseline-test-results.trx); Wiederholung 2 bestanden |
| `…TvSenderOnAirE2ETests.Admin_TogglesOffAir_Stream404_LiveTvShowsOffAirImage` | E2E (Playwright) | `ToContainTextAsync`-Timeout auf `#live-tv-now-title` (bekannter Flake, bereits in Issue-#354-Bestandsaufnahme dokumentiert) | Lauf 1, [TRX](test-results/baseline-test-results.trx); Wiederholung 2 bestanden |

### Testlücken und Ausführungsprobleme

- Übersprungen in Lauf 1 (2 begründete Skips): `FfmpegToolDownloadServiceTests_Install.Run_OnUnix_SetsExecutableBit` (plattformbedingt unter Windows) und `…PairedDevice_ReportsProgressForMovieWithoutCollection…` (dokumentierter offener Defekt).
- **Für die Anforderung relevante Lücke: es existiert kein einziger Test für
  `FileSystemDemoDataSetService`, `IDemoDataSetService` oder die
  Demodaten-Auswahl in `Register.razor`** (Suche nach `DemoDataSet` in
  `VideoWebPlayer.Tests` trifft nur Build-Artefakte). `FirstUserRedirectE2ETests`
  und `IdentityRedirectManagerE2ETests` decken Redirect/Login ab, nicht die
  Demo-Auswahl.
- `TempDirectoryCleanup`-Warnungen im Log (gesperrte Temp-DBs) sind bekannte,
  folgenlose Cleanup-Meldungen.

## Testklassen (für diesen Bereich relevant)

### `VideoWebPlayer.Tests/Services/LocalMediaSourceReaderTests.cs` (`[OsInterface]`)

- `ReadRootDirectory_ReturnsLocalRoot` — Root-Collection aus echtem Temp-Verzeichnis
- `ReadRootDirectory_InvalidSourcePath_*` — leere/ungültige Pfade → leer + Warning statt Exception
- Pfad-Confinement-/Case-Insensitive-Auflösung, ReparsePoint-Ablehnung

### `VideoWebPlayer.Tests/MediaSourceAdminDetailsValidationTests.cs` (`[OsInterface]`)

- `TryValidateLocalDirectory_*` — valide, nicht existierende, relative und
  nicht lesbare Pfade (direkt gegen `internal static` Helper)

### `VideoWebPlayer.Tests/Services/MediaSourceReaderDispatcherTests.cs`

- Reader-Auswahl nach `SourceType`; Exception ohne eager-geladene `MediaSource`

### `VideoWebPlayer.Tests/Services/MediaSourceScannerLocalTests.cs` / `MediaSourceScannerTests.cs`

- Scan-Diffing, `ScanDueAt`/`Classifyable`-Semantik mit Reader-Doubles

### `VideoWebPlayer.Tests/LocalMediaSourcePipelineE2ETests.cs` (`E2E`+`OsInterface`, `MediaSourceClassifierCollection`)

- `LocalSource_ScanAndClassify_ExposesTvShowViaApi` — **Beleg für Variante (a):**
  echter Host + echte SQLite-DB, lokale Quelle mit `tvshow.nfo`/`episode01.nfo`
  und Dummy-`.mkv` (Inhalt `„video"`), `RunFullScanAsync` → `TVShow`/`TVShowEpisode`
  per API abrufbar

### `VideoWebPlayer.Tests/LocalMediaSourceStreamingE2ETests.cs` (`E2E`+`OsInterface`)

- Stream-/Download-Endpunkte liefern lokale Datei-Inhalte 1:1 (6-Byte-Dummy,
  kein echtes Video nötig)

### `VideoWebPlayer.Tests/MediaSourceLocalDirectoryE2ETests.cs` (`E2E`+`OsInterface`, Playwright)

- Admin legt lokale Quelle über UI an; Validierungsfehler bei ungültigem Pfad;
  Quelltyp bestehender Quellen nicht änderbar

### Weitere einschlägige Suiten

- `MediaSourceScanCoordinator*` (Scan/Scope/Gate-Lease)
- `MediaSourceClassifier*` (Actors-, AgeRating-, Genre-, Background-Backfill)
- `LocalMediaSourceClassifierTests*` (NFO-Konventionen, Trailer)
- `ItemsControllerLocalSourceTests` (Streaming-Controller)
- `MediaSourceDeleteE2ETests`, `MediaSourceExplorerRescanE2ETests`, `MediaSourceAdminFullScanE2ETests`

## Hilfsmethoden

### `VideoWebPlayer.Tests/Helpers/TestImages.cs`

- `Png(width, height)` — synthetisches, im Screenshot erkennbares PNG (SkiaSharp)
- `SolidPng(w,h,color)`, `Gif` (1×1-Base64)

### `VideoWebPlayer.Tests/Helpers/TempDirectoryCleanup.cs`

- `DeleteBestEffort`/`DeleteFileBestEffort` — Retry-Löschung für Temp-Dateibanken/-Verzeichnisse

### `VideoWebPlayer.Tests/Helpers/SetupSeedHelpers.cs`

- `UpdateSetupAsync`/`SetOnAirAsync` — Setup-Row-Mutationen gegen den Test-Host

### `VideoWebPlayer.Tests/Helpers/E2EBinaryLocator.cs` + `PairingWebApplicationFactory.cs`

- Kestrel-/Factory-Helfer für E2E-Hosts (Muster: `WebApplicationFactory<Program>`
  + `UseKestrel()` + `StartServer()` + Temp-SQLite via
  `ConnectionStrings:DefaultConnection`, siehe `MediaSourceLocalDirectoryE2ETests`)

### `VideoWebPlayer.Tests/Infrastructure/Testing/`

- `[OsInterface]`/`[OsInterfaceFact]`/`[OsInterfaceTheory]` — Lane-Marker
- `[Trait("Category","E2E")]` — Browser-E2E (Konventionstest erzwingt OsInterface-Paar)
- `MediaSourceClassifierCollection` — serialisiert Classifier-Tests wegen des
  prozessweiten `_classificationRunning`-Flags
