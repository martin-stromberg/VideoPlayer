# Bestandsaufnahme: Flaky-Test-Härtung — OsInterface-Lane, Deadlines/Cleanup und Retry-/Quarantäne-Konvention (Issue #368)

Bestandsaufnahme des Ist-Zustands von `VideoWebPlayer.Tests/`, der CI-Workflows und der Referenzimplementierung (martin-stromberg/Softwareschmiede PR #432) vor der Umsetzung der Anforderung in [`requirement.md`](requirement.md). Keine Planung, keine Implementierungsvorschläge — nur Befunde.

## Zusammenfassung

**Vorhanden:**

- Einziges Trait-System: `[Trait("Category", "E2E")]` — 79 Vorkommen in 53 Dateien; `xunit.v3` 3.2.2 ist referenziert (unterstützt `ITraitAttribute` als Basis für die Blaupausen-Attribute).
- Drei Filter-Lanes in beiden Staging-Workflows (`Category!=E2E`, `Category=Integration`, `Category=E2E`), alle im selben **blockierenden** `build-and-test`-Job (30 min Timeout); das 70-%-Coverage-Gate speist sich aus dem `Category!=E2E`-Lauf.
- Ausgereifte Fixture-Schicht (`Helpers/`): Kestrel-Loopback + Playwright (`PlaylistsE2ETestBase` u. a.), `WebApplicationFactory`-TestServer mit Temp-SQLite-Dateibank (`DeviceClientTestBase`, `PairingWebApplicationFactory`), UDP-, ffmpeg- und Junction/Symlink-Szenarien.
- `pwsh` 7.6.10 lokal verfügbar; CI nutzt bereits `pwsh`-Schritte (`playwright.ps1 install --with-deps chromium`) — Voraussetzung für die Blaupausen-Skripte erfüllt.
- `scripts/` existiert (nur `resolve-release-version.mjs`).

**Fehlt / Befunde:**

- **Kein `OsInterface`-/`Flaky`-/`Integration`-Trait, keine `ITraitAttribute`-Implementierung, keine `TestCategories`-Konstanten, kein `TempDirectoryCleanup`, kein Meta-Test** — die gesamte Trait-/Retry-/Analyse-Infrastruktur ist neu zu erzeugen (→ `inventory/test-infrastructure.md`).
- **8 Playwright-Klassen tragen keinen `Category`-Trait** und laufen heute mit echtem Chromium + Kestrel-Port in der `Category!=E2E`-Lane — live im Baseline-TRX verifiziert (34 Testergebnisse). Die Requirement-Angabe „79 `Category=E2E`-Klassen" bezieht sich auf Trait-Vorkommen, nicht Klassenanzahl (→ `inventory/os-interface-audit.md`, Kategorie A).
- **`Category=Integration`-Schritt matcht 0 Tests** — die behauptete Doppelausführung findet nicht statt; sie droht erst, sobald `Integration`-Traits vergeben werden.
- **16 ungeschützte `Directory.Delete`- + 1 ungeschützte `File.Delete`-Stelle** in Dispose-Pfaden; kein `Kill(entireProcessTree)`-Muster für langlebige Kindprozesse; ~50 geschützte Deletes in drei Varianten (→ `inventory/test-infrastructure.md`).
- **Deadline-Mismatch punktuell:** `LiveStreamE2ETests` Host-Poll 60 s < `TranscodeTimeout` 120 s; `FirstOutput`-Warte 2 s und `WaitFor*` 2–10 s sind die knappsten Budgets; zwei unbegrenzte `WaitForExitAsync` auf echten Prozessen (→ `inventory/time-budgets.md`).
- **`release.yml` läuft die volle Suite ungefiltert und blockierend** als Release-Gate.
- **Stille Skip-Pfade:** `SkipBrowser`-/`E2EBinaryLocator`-`return`-Muster melden nicht ausgeführte Tests als `Passed`.
- **Blaupause PR #432 übertragbar mit Anpassungen:** 1 statt 2 Testprojekte, `ubuntu-latest` statt `windows-latest` (Heuristiken Playwright/ffmpeg/Kestrel/SQLite statt ConPTY/FlaUI/Clipboard), Release-Builds, kein FluentAssertions, eigener Playwright-Install-Schritt im OS-Job nötig (→ `inventory/blueprint-comparison.md`).

**Test-Ausgangszustand** (Details → [`inventory/tests.md`](inventory/tests.md), Nachweise unter `inventory/test-results/`):

| Lauf | Gesamt | Bestanden | Fehlgeschlagen | Übersprungen |
|------|--------|-----------|----------------|--------------|
| `Category!=E2E` (Release, lokal Windows) | 2708 | 2707 | 0 | 1 (`SkipWhen` Unix-Gate) |
| `Category=E2E` (Release, lokal Windows) | 256 | 254 | **1** | 1 (dauerhaftes `Fact(Skip)`) |

Der eine Fehlschlag — `TvSenderOnAirE2ETests.Admin_TogglesOffAir_Stream404_LiveTvShowsOffAirImage` (`TvSenderOnAirE2ETests.cs:92`, `ToContainTextAsync` 5-s-Timeout auf `#live-tv-now-title`) — ist ein dokumentierter **Timing-Flake** erster Kajüte: Off-Air-Bild gerendert, Programm-„Jetzt"-Karte hinkt hinterher. Er ist zugleich der erste `Flaky`-Kandidat und der Beleg, dass die Lane-Trennung echte Fehlschläge adressiert.

## Details

- [Audit: Umgebungssensitivität der Tests](inventory/os-interface-audit.md) — kategorisierte Datei-/Klassenliste (Playwright, Prozesse, Ports, Dateibanken, Temp-Cleanup, Zeitbudgets, OS-Gates, Fehlpositive) inkl. der 8 ungetaggten Playwright-Klassen und offener Klassifizierungsfragen
- [Trait-/Kategorien- und Test-Infrastruktur](inventory/test-infrastructure.md) — Trait-Bestand, Basisklassen/Fixtures, Dispose-/Cleanup-Muster mit Zeilenreferenzen, fehlende Artefakte
- [CI-Workflows](inventory/ci-workflows.md) — `staging-ci.yml`, `pr-staging-ci.yml`, `release.yml`: Jobstruktur, `needs:`-Ketten, Filter, Timeouts, Coverage-Gate, TRX-Namenskonvention
- [Zeitbudgets und Deadlines](inventory/time-budgets.md) — Host- vs. Kind-Budgets in Tests und Produktivcode, identifizierte Mismatches
- [Vergleichsblaupeise PR #432](inventory/blueprint-comparison.md) — übertragbare Artefakte, nicht zutreffende Annahmen, Anpassungsbedarf
- [Tests und Test-Ausgangszustand](inventory/tests.md) — Läufe, Fehlschlagdetails, Skips, stille Skip-Pfade, Test-/Hilfsklassen

## Verzeichnis der Nachweise

- `inventory/test-results/test-results-inventory-regular.trx` — TRX des `Category!=E2E`-Laufs (2.708 Tests)
- `inventory/test-results/run-regular-console.txt` — Konsole dto.
- `inventory/test-results/test-results-inventory-e2e.trx` — TRX des `Category=E2E`-Laufs (256 Tests, inkl. Stacktrace des Flakes)
- `inventory/test-results/run-e2e-console.txt` — Konsole dto.
