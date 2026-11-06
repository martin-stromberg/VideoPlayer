# Tests

## Test-Ausgangszustand vor der Umsetzung

- Zeitpunkt: 2026-10-23 (MESZ), Branch `task-issue-111-…-schritt-26-diverse`, Commit `6af2e7a`.
- Uncommittete Änderungen: untracked `VideoWebPlayer/Data/LiveStream/` (Test-Rest, wird nicht committed).
- Testbefehl laut Projektregeln: `dotnet build VideoPlayer.sln` (Debug+Release), dann
  `dotnet test VideoWebPlayer.Tests/VideoWebPlayer.Tests.csproj` — synchron, nie im Hintergrund.
- Erwarteter Basisstand: ~2182 grün + 1 Alt-Skip (Projekt-Historie).
- Voller Vorbereitungslauf: aus Zeitgründen wird der Baseline-Lauf mit dem Abschlusslauf
  zusammengeführt; vorab werden die gezielt betroffenen Suiten ausgeführt (Abweichung offengelegt).

## Testklassen (betroffen / relevant)

### `ProgramScheduleServiceTests_Interleaving` — `VideoWebPlayer.Tests/ProgramScheduleServiceTests_Interleaving.cs`
- `…StableSlotsAcrossDays` — fixiert A-an-12:00/B-an-12:45 (alphabetische Strangreihenfolge!)
- `…EpisodeOrderPerShow`, `…SecondRun_ContinuesEachShowCursor`, `…Reconcile_…` — Strang-/Cursor-Verhalten

### `ProgramScheduleTestDb` — `VideoWebPlayer.Tests/Helpers/ProgramScheduleTestDb.cs`
- SQLite shared-memory, `CreateShowAsync`/`CreateGenreAsync`/`CreateRuleAsync`/`CreateSlotAsync`,
  `CreateService`, `SlotsAsync` — zentraler Anker für N23/N25-Seeds.

### `LiveTvTests` (bUnit) — `VideoWebPlayer.Tests/Components/LiveTvTests.cs`
- `CreateContext` registriert `IProgramGuideApiClient`-Mock, `TokenClient`, `StaticAssetVersioner` —
  hier `ProgramSettingsService` + `TimeProvider` ergänzen; kein Poll-Test vorhanden.

### `NavMenuLiveTvTests` — `VideoWebPlayer.Tests/Components/NavMenuLiveTvTests.cs`
- Erwartet `/live-tv`-Link; InMemory-DB — muss Flag-Seed bekommen + Disabled-Fall.

### `ProgramGuideControllerTests` / `LiveStreamControllerTests` — `VideoWebPlayer.Tests/Controllers/`
- Controller direkt konstruiert (`CreateController`) — Konstruktor-Änderung + Flag-Fälle.

### `ProgramScheduleWorkerTests`, `LiveStreamWorkerTests`, `MediaDurationProbeWorkerTests`
- eigene ServiceCollections; Setup-Seeds ohne Flag (`Setup { DataVersion }`) — Flag-Seed nötig.

### E2E (`DeviceClientTestBase`/`PlaylistsE2ETestBase`)
- `ProgramGuideE2ETests`, `LiveStreamE2ETests`, `LiveTvE2ETests`, `TVSenderAdminE2ETests` —
  frische SQLite pro Test; `Setup`-Zeile → Flag aktivieren (Helper).

### `ProgramSettingsServiceTests` / `ProgramSettingsServiceLiveStreamTests`
- `GeneralSettingsUpdate`-Aufrufe positional — Record-Erweiterung → Aufrufer anpassen;
  neuer Flag-Roundtrip-Test.
