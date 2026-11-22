# Tests

## Test-Ausgangszustand vor der Umsetzung

- Zeitpunkt (mit Zeitzone): 2026-11-14, Läufe zwischen 22:56 und 23:05 Uhr MEZ (UTC+01:00).
- Branch und Commit-ID: `task/issue-354-521789ae2c934b89a6767f795b0f5e2a-automatisierung-wiederkehrende`,
  Commit `0b52c8cd037a65e88e1c7a7cee469a9d13ab2d6e` („TV-Sender: Neue Regeln auch in beplanten Tagen materialisieren (#352)").
- Uncommittete Änderungen im getesteten Stand: ausschließlich das untracked
  Feature-Verzeichnis `docs/features/task/issue-354-.../` (requirement.md, todo.md,
  `_anforderung.txt`, diese Bestandsaufnahme). Keine Code-Änderungen.
- Testumgebung und Runtime-/SDK-Versionen: Windows (Git Bash), .NET SDK `10.0.403`,
  Node `v24.15.0`, Playwright-Chromium lokal installiert (`ms-playwright`,
  u. a. chromium-1248); Testziel `net10.0`, Release-Build.
- Ermittelte Testsuiten und Quellen der Testbefehle:
  - `VideoWebPlayer.Tests` (xUnit; Kategorien: Unit/bUnit, `Integration`, `E2E`/Playwright)
    — Befehl aus `AGENTS.md` Definition of Done, `docs/INDEX.md` und
    `.github/workflows/staging-ci.yml` (`build-and-test`-Job).
  - `tools/MarkdownLinkCheck.Tests` — zweite Testsuite laut `docs/INDEX.md`
    „Lokale Qualitätsprüfungen".
  - `tools/MarkdownLinkCheck` und `tools/SecretScan` — Tool-Läufe aus dem
    `pre-commit`-Hook (`.githooks/pre-commit`), die neue Markdown-Dateien
    bzw. Commits absichern.

### Testläufe

| Lauf | Befehl inkl. Filter | Arbeitsverzeichnis | Exit-Code | Erfolgreich | Fehlgeschlagen | Übersprungen | Nachweis |
|------|--------------------|--------------------|-----------|-------------|----------------|--------------|----------|
| 1a Restore+Build | `dotnet restore VideoPlayer.sln` + `dotnet build VideoWebPlayer.Tests/VideoWebPlayer.Tests.csproj --no-restore -c Release -p:NoWarn=NU1903` | Repo-Root | 0 (139 Warnungen, 0 Fehler) | – | – | – | `test-results/baseline-run.log` (nicht versioniert) |
| 1b Volle Suite | `dotnet test VideoWebPlayer.Tests/VideoWebPlayer.Tests.csproj --no-build -c Release --logger "trx;LogFileName=baseline-test-results.trx" --logger "console;verbosity=normal"` | Repo-Root | 1 | 2960 | 2 | 2 | [baseline-test-results.trx](test-results/baseline-test-results.trx), `test-results/baseline-run.log` (nicht versioniert) |
| 2 Wiederholung | `dotnet test VideoWebPlayer.Tests/VideoWebPlayer.Tests.csproj --no-build -c Release --filter "FullyQualifiedName~TvSenderOnAirE2ETests" --logger "trx;LogFileName=retry-test-results.trx"` | Repo-Root | 0 | 2 | 0 | 0 | [retry-test-results.trx](test-results/retry-test-results.trx), `test-results/retry-run.log` (nicht versioniert) |
| 3 Zweite Suite | `dotnet test tools/MarkdownLinkCheck.Tests/MarkdownLinkCheck.Tests.csproj -c Release` | Repo-Root | 0 | 6 | 0 | 0 | `test-results/baseline-tools-run.log` (nicht versioniert) |
| 4 Link-Check | `dotnet run --project tools/MarkdownLinkCheck/MarkdownLinkCheck.csproj -c Release -- --root .` | Repo-Root | 0 (425 Dateien, 277 lokale Links geprüft) | – | – | – | `test-results/baseline-tools-run.log` (nicht versioniert) |
| 5 SecretScan | `dotnet run --project tools/SecretScan/SecretScan.csproj -c Release -- --root . --solution VideoPlayer.sln` | Repo-Root | 0 („Secret scan passed.") | – | – | – | `test-results/baseline-tools-run.log` (nicht versioniert) |

> Die Konsolenausgaben (`baseline-run.log`, `retry-run.log`, `baseline-tools-run.log`) wurden
> damals lokal erzeugt, aber nie versioniert — `*.log` steht in `.gitignore`. Die Angabe
> „(nicht versioniert)" ersetzt die zuvor toten Links; die TRX-Reports sind weiterhin beigefügt.

### Nachgewiesene bestehende Testfehler

| Test-ID inkl. Testfall | Suite / Dateipfad | Fehlerbild / Fehlermeldung | Lauf und Nachweis |
|-----------------------|------------------|---------------------------|-------------------|
| `VideoWebPlayer.Tests.TvSenderOnAirE2ETests.Admin_TogglesBackOnAir_StreamAndPlayerResume` | E2E (Playwright), `VideoWebPlayer.Tests/TvSenderOnAirE2ETests.cs` | `System.TimeoutException` in `CapturePlaylistUrlAsync()` (Zeile 47, aufgerufen aus Zeile 116), nach 51 s | Lauf 1b, [TRX](test-results/baseline-test-results.trx); im Einzel-Wiederholungslauf 2 **bestanden** (7 s) |
| `VideoWebPlayer.Tests.TvSenderOnAirE2ETests.Admin_TogglesOffAir_Stream404_LiveTvShowsOffAirImage` | E2E (Playwright), `VideoWebPlayer.Tests/TvSenderOnAirE2ETests.cs` | `Microsoft.Playwright.PlaywrightException`: `ToContainTextAsync` (Timeout 5 s) auf `#live-tv-now-title` erwartete „Off-Air-Film", war `null`; nach 27 s | Lauf 1b, [TRX](test-results/baseline-test-results.trx); im Einzel-Wiederholungslauf 2 **bestanden** (4 s) |

Einordnung: beide Fehlschläge betreffen dieselbe E2E-Klasse und traten unter
Volllast der parallelen Gesamtsuite auf; isoliert wiederholt liefen beide Tests
durch. Das entspricht dem in `AGENTS.md` beschriebenen gelegentlichen Flackern
einzelner Playwright-Tests unter Last. Als „preexisting" dokumentiert sind beide
Fälle durch den Ausgangslauf 1b nachgewiesen; der Wiederholungserfolg verdeckt
den Ausgangsfehlschlag nicht.

### Testlücken und Ausführungsprobleme

- Übersprungen in Lauf 1b (2 Tests, beide begründet im Code):
  - `VideoWebPlayer.Tests.Services.FfmpegToolDownloadServiceTests_Install.Run_OnUnix_SetsExecutableBit` —
    `Assert.SkipWhen(OperatingSystem.IsWindows())`: plattformbedingt unter Windows
    nicht ausführbar.
  - `VideoWebPlayer.Tests.DevicePlaylistE2ETests_Playback.PairedDevice_ReportsProgressForMovieWithoutCollection_ContinueWatchingListStillLoads` —
    `[Fact(Skip = "...")]` mit dokumentiertem Befund: `api/continue-watching`
    antwortet mit HTTP 500, wenn ein Film zu keiner Filmsammlung gehört
    (bekannter, bewusst offener Defekt — Kommentar in `DevicePlaylistE2ETests_Playback.cs`
    Zeile 74–76).
- Keine Build-, Setup- oder Infrastrukturfehler. Keine nicht ausgeführten Suiten.
- Nachtrag: nach dem Anlegen der Bestandsaufnahme-Dateien wurde der Link-Check
  erneut ausgeführt — bestanden mit 430 Dateien / 293 Links (die neuen
  Markdown-Dateien sind abgedeckt).

## Testklassen

**Für diese Anforderung existieren keine relevanten Testklassen.** Die Anforderung
betrifft ausschließlich GitHub-Actions-YAML unter `.github/workflows/` — es gibt
im Repo weder Workflow-Tests noch eine YAML-Lint-Testsuite. Die CI verifiziert
Workflow-Änderungen ausschließlich dadurch, dass die Workflows selbst laufen
(bei `schedule`-Workflows erst nach Promotion auf `main` bzw. über
`workflow_dispatch`).

## Hilfsmethoden

Keine für diese Anforderung relevanten Hilfsmethoden vorhanden.
