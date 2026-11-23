# Tests

## Test-Ausgangszustand vor der Umsetzung

- Zeitpunkt (mit Zeitzone): 2026-11-23, ~15:3x MEZ (UTC+01:00)
- Branch und Commit-ID:
  `task/issue-356-…-schritt-1-demoinstallation`,
  Commit `f0392874adbcb454dc19fe72e15e6d5f198bfa29`
  („chore: Schritt 1 (Demoinstallation) in Arbeit genommen (Issue #356)").
- Uncommittete Änderungen im getesteten Stand: nur untracked
  Lifecycle-Artefakte unter `docs/features/task/issue-356-…-schritt-1-demoinstallation/`.
  **Keine Code-Änderungen** — `git diff` gegen den Basisbranch ist leer.
- Testumgebung: Windows (Git Bash), .NET SDK 11.0.100, net10.0, Release-Build,
  Playwright-Chromium installiert.
- Ermittelte Testsuiten und Quellen: `VideoWebPlayer.Tests` (xUnit v3;
  deterministische Lane + `OsInterface`-Lane mit echten Ports/Prozessen/
  SQLite-Dateien/Playwright), `tools/MarkdownLinkCheck.Tests`, Tool-Läufe
  `MarkdownLinkCheck`+`SecretScan` (pre-commit). Quellen: `AGENTS.md` DoD,
  `staging-ci.yml`, `release.yml`.

### Wiederverwendete Voll-Suite-Baseline

Der vollständige Suite-Lauf des Projekt-Branches wurde am selben Tag auf dem
Code-Stand ausgeführt, von dem dieser Schritt-Branch abzweigt (Basis-Commit
`5e4737e`, danach nur Dokumentations-Commits — keine Code-Änderung). Er gilt
daher als Baseline: **2969 bestanden, 2 nachgewiesene Flakes (Retry grün),
2 begründete Übersprünge.** Nachweis:
`docs/projects/task/issue-356-…/inventory/test-results/baseline-test-results.trx`
sowie `retry-test-results.trx`. Die volle Suite wird nach der Umsetzung in
Schritt 10 erneut komplett ausgeführt.

### Testläufe (dieser Schritt-Branch)

| Lauf | Befehl inkl. Filter | Arbeitsverzeichnis | Exit-Code | Erfolgreich | Fehlgeschlagen | Übersprungen | Nachweis |
|------|--------------------|--------------------|-----------|-------------|----------------|--------------|----------|
| 1 Fokuslauf (Baseline) | `dotnet test VideoWebPlayer.Tests/VideoWebPlayer.Tests.csproj --no-build -c Release --filter "FullyQualifiedName~LocalMediaSource\|FullyQualifiedName~MediaSource\|FullyQualifiedName~FirstUser"` | Repo-Root | 0 | 123 | 0 | 0 | [inventory-baseline.trx](test-results/inventory-baseline.trx) |

### Nachgewiesene bestehende Testfehler

Keine im Fokuslauf. In der wiederverwendeten Voll-Suite-Baseline sind zwei
OsInterface-E2E-Flakes dokumentiert
(`FfmpegLiveStreamSegmenterE2ETests.RealFfmpeg_ProducesPlaylistAndSegments`,
`TvSenderOnAirE2ETests.Admin_TogglesOffAir_Stream404_LiveTvShowsOffAirImage`),
beide im Wiederholungslauf grün — siehe Projekt-inventory `tests.md`.

### Testlücken und Ausführungsprobleme

- **Kein einziger Test für `FileSystemDemoDataSetService`,
  `IDemoDataSetService` oder die Demodaten-Auswahl in `Register.razor`** —
  zentrale Lücke für diese Anforderung.
- `TempDirectoryCleanup`-Warnungen (gesperrte Temp-DBs) sind bekannte,
  folgenlose Cleanup-Meldungen.

## Testklassen (für diesen Bereich relevant)

### `VideoWebPlayer.Tests/LocalMediaSourcePipelineE2ETests.cs` (`E2E`+`OsInterface`, `MediaSourceClassifierCollection`)

- `LocalSource_ScanAndClassify_ExposesTvShowViaApi` — Beleg für den geplanten
  Weg: echter Host + echte SQLite-DB, lokale Quelle mit `tvshow.nfo`/
  `episode01.nfo` + Dummy-`.mkv`, `RunFullScanAsync` → `TVShow`/`TVShowEpisode`
  per API abrufbar.

### `VideoWebPlayer.Tests/LocalMediaSourceStreamingE2ETests.cs` (`E2E`+`OsInterface`)

- Stream-/Download-Endpunkte liefern lokale Datei-Inhalte 1:1.

### `VideoWebPlayer.Tests/FirstUserRedirectE2ETests.cs` (`E2E`+`OsInterface`)

- `RegisterFirstUser_SubmitsAndSetsAuthCookie` — HTTP-E2E-Muster für den
  Registrierungs-POST (Antiforgery-Token extrahieren, Form-Felder, Cookie).

### `VideoWebPlayer.Tests/MediaSourceLocalDirectoryE2ETests.cs` (`E2E`+`OsInterface`, Playwright)

- Admin legt lokale Quelle über UI an; Pfad-Validierung; Quelltyp-Sperre.

### `VideoWebPlayer.Tests/Services/LocalMediaSourceReaderTests.cs`, `MediaSourceScannerLocalTests.cs`, `MediaSourceReaderDispatcherTests.cs`

- Root-Collection, Confinement, Scan-Diffing, Reader-Auswahl.

## Hilfsmethoden

### `VideoWebPlayer.Tests/Helpers/TestImages.cs`

- `Png(width, height)` / `SolidPng` / `Gif` — synthetische Testbilder per
  SkiaSharp (Referenz für Bildgenerierung).

### `VideoWebPlayer.Tests/Helpers/TempDirectoryCleanup.cs`

- `DeleteBestEffort` / `DeleteFileBestEffort` — Retry-Löschung für
  Temp-Dateien/-Verzeichnisse.

### `VideoWebPlayer.Tests/Infrastructure/Testing/`

- `[OsInterface]`/`[Trait("Category","E2E")]` — Lane-Marker;
  `MediaSourceClassifierCollection` serialisiert Classifier-Tests
  (prozessweites `_classificationRunning`-Flag).
