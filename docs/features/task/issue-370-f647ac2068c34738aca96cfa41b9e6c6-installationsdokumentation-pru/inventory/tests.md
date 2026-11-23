# Tests — Ausgangszustand

## Test-Ausgangszustand vor der Umsetzung

- Zeitpunkt (mit Zeitzone): 2026-11-23, 02:24–03:35 UTC (03:24–04:35 +01:00, lokale Zeit)
- Branch und Commit-ID: `task/issue-370-f647ac2068c34738aca96cfa41b9e6c6-installationsdokumentation-pru`,
  HEAD `5fc611eea7b27a38eb74fb5633e58163033e5360` („Flaky-Test-Härtung: OsInterface-Lane, Deadlines/Cleanup und Retry-/Quarantäne-Konvention (#377)")
- Uncommittete Änderungen im getesteten Stand: nur das untracked Feature-Verzeichnis
  `docs/features/task/issue-370-…-installationsdokumentation-pru/` (requirement.md, todo.md,
  diese Inventardateien und die Testlogs unter `inventory/test-results/`). Keine Änderungen
  an getrackten Dateien; die neuen Markdown-Dateien erhöhen lediglich die Zähler des
  MarkdownLinkCheck (siehe Lauf 3).
- Testumgebung und Runtime-/SDK-Versionen: Microsoft Windows 11 Pro (x64);
  `dotnet --version` = 11.0.100 (installierte SDKs: 10.0.404, 11.0.100 — Ziel-TPM der
  Projekte ist `net10.0`); Playwright-Chromium unter `%LOCALAPPDATA%\ms-playwright`
  vorhanden (mehrere Builds, u. a. chromium-1169…1255).
- Ermittelte Testsuiten und Quellen der Testbefehle: `AGENTS.md` §7
  (`dotnet build VideoPlayer.sln` Debug+Release, volle Suite
  `dotnet test VideoWebPlayer.Tests/VideoWebPlayer.Tests.csproj`),
  `docs/INDEX.md`/`README.md`/`GUIDE_Installation.md` (beide `dotnet test`-Befehle +
  `dotnet run … MarkdownLinkCheck … --root .`), Lane-Aufteilung aus
  `docs/help/stabilitaet/os-interface-tests.md` und `.github/workflows/pr-staging-ci.yml`
  (Filter `Category!=E2E&Category!=OsInterface` = blockierende Lane,
  `Category=OsInterface` = best-effort-Lane). Da die Anforderung eine reine
  Dokumentationsaufgabe ist, wurde die volle Suite als zwei CI-konforme Läufe
  (reguläre + OsInterface-Lane, zusammen = voller Testumfang) ausgeführt.

### Testläufe

| Lauf | Befehl inkl. Filter | Arbeitsverzeichnis | Exit-Code | Erfolgreich | Fehlgeschlagen | Übersprungen | Nachweis |
|------|--------------------|--------------------|-----------|-------------|----------------|--------------|----------|
| 1 | `dotnet test VideoWebPlayer.Tests/VideoWebPlayer.Tests.csproj -c Release --filter "Category!=E2E&Category!=OsInterface" --logger "console;verbosity=normal"` | Repo-Root | 0 | 2549 | 0 | 0 | [Log](test-results/dotnet-test-regular-lane.log) |
| 2 | `dotnet test VideoWebPlayer.Tests/VideoWebPlayer.Tests.csproj -c Release --no-build --filter "Category=OsInterface" --logger "console;verbosity=normal"` | Repo-Root | 0 | 422 | 0 | 2 | [Log](test-results/dotnet-test-osinterface-lane.log) |
| 3 | `dotnet test tools/MarkdownLinkCheck.Tests/MarkdownLinkCheck.Tests.csproj --logger "console;verbosity=normal"` | Repo-Root | 0 | 6 | 0 | 0 | [Log](test-results/dotnet-test-markdownlinkcheck-tests.log) |
| 4 | `dotnet run --project tools/MarkdownLinkCheck/MarkdownLinkCheck.csproj -- --root .` | Repo-Root | 0 | – (Tool, kein Test) | 0 Fehler — „passed (445 files, 292 local links)" | – | [Log](test-results/markdownlinkcheck.log) |

Summe `VideoWebPlayer.Tests` über beide Lanes: 2971 ausgeführte Tests (2971 bestanden,
0 fehlgeschlagen, 2 übersprungen) — deckt den vollen Testumfang der Suite ab.

### Nachgewiesene bestehende Testfehler

Keine. In beiden Lanes schlug kein Test fehl (0 Fehlschläge); es gibt damit keine
nachgewiesenen preexisting Testfehler.

### Testlücken und Ausführungsprobleme

- **Übersprungene Tests (2, OsInterface-Lane):**
  - `DevicePlaylistE2ETests_Playback.PairedDevice_ReportsProgressForMovieWithoutCollection_ContinueWatchingListStillLoads`
    — bewusst übersprungen (`Skip`-Begründung im Test: bekannter Befund
    „api/continue-watching antwortet mit 500 …", wird laut Skip-Kommentar nicht in
    diesem Kontext behoben). Kein Fehlschlag, kein Nachweisbedarf.
  - `FfmpegToolDownloadServiceTests_Install.Run_OnUnix_SetsExecutableBit` —
    Unix-only-Test, unter Windows plattformbedingt übersprungen.
- **Nicht-fatale Infrastruktur-Meldungen in Lauf 2:** `TempDirectoryCleanup`-Warnungen
  („… konnte nach 5 Versuchen nicht geloescht werden: … being used by another process.")
  — dokumentiertes, bekanntes Windows-Locking-Verhalten der Temp-DB-Bereinigung
  (siehe `docs/help/stabilitaet/os-interface-tests.md`); kein Test schlug deshalb fehl.
- **Testbezogene Lücke der Aufgabe selbst:** Die Anforderung ist eine reine
  Dokumentationsänderung ohne Code-Tests; einzige berührte Qualitätssicherung ist der
  Markdown-Link-Check (Lauf 4, grün) sowie indirekt der SecretScan im Pre-Commit-Hook
  (in dieser Bestandsaufnahme nicht separat ausgeführt; laut `pr-staging-ci.yml` läuft
  der CI-Security-Scan nur gegen NuGet-Pakete, nicht gegen Markdown).

## Testklassen (für den Bereich relevante Bestandstests)

Keine Code-Änderung geplant; folgende Bestandstests in `VideoWebPlayer.Tests/`
sichern Verhalten ab, das die Dokumentation beschreibt (Auswahl, keine Änderung nötig):

### `MdnsServiceProfileBuilderTests` (`MdnsServiceProfileBuilderTests.cs`)
- Port-Auflösung in der dokumentierten Vorrangreihenfolge (`Mdns:Port` → gebundene
  Adresse → `Kestrel` → `Host:Port` → 5000) und feste TXT-Records `path=/`, `app=VideoWebPlayer`.

### `MdnsAdvertiserWorkerTests` (`MdnsAdvertiserWorkerTests.cs`)
- Konjunktion aus `Mdns:Enabled` und Admin-Schalter `Setup.MdnsAdvertisementEnabled`.

### `MdnsConfigurationTests`, `MdnsOptionsValidatorTests`, `MdnsRegistrationTests`
- Options-Binding, Validierung (`ServiceType`-Muster, `Port`-Bereich) und Registrierung.

### `DiscoveryResponseBuilderTests`, `DiscoveryBaseUrlResolverTests`
- Ableitungsregeln (Loopback-/Wildcard-Adressen werden nie gemeldet) und die
  Vorrangkette Admin-Wert → `Discovery:PublicBaseUrl` → Ableitung inkl. fail-open.

### `DiscoveryOptionsValidatorTests`, `DiscoveryUrlRulesTests`, `DiscoveryConfigurationTests`
- Validierung von `Discovery:PublicBaseUrl` (absolute `http`/`https`-URL, fail-fast).

### `MarkdownLinkCheckerTests` (`tools/MarkdownLinkCheck.Tests`, 6 Tests)
- Link-Auflösung relativ zum Repo-Root, Root-Verlassen = Fehler, Casing-Prüfung auch
  unter Windows, Fragment-/Anker-Prüfung, Code-Fence-Auslassung.

## Hilfsmethoden

Keine aufgabenrelevanten Test-Hilfsmethoden — keine Code-Tests im Scope.
