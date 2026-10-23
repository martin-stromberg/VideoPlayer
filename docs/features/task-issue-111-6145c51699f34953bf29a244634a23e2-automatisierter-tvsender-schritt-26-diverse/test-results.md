# Testergebnisse: Schritt 26 — N23–N25

## Befehle

- `dotnet build VideoPlayer.sln -c Debug/-c Release` — jeweils **0 Fehler** (Debug+Release).
  Hinweis: `VideoWebPlayer\bin\Debug` war durch die laufende Benutzer-App (PID 50736) gesperrt
  (MSB3027, wie projektbekannt). Builds/Tests liefen daher über `-p:OutDir=D:/Repos_scratch/…`
  (Debug) bzw. `-c Release` in den echten `bin\Release`-Pfaden (unlocked) — dokumentierte Ausweichung.
- `dotnet test VideoWebPlayer.Tests/VideoWebPlayer.Tests.csproj -c Release` — synchron:
  **2189 erfolgreich, 1 Fehler, 1 übersprungen (2191 gesamt)**.
  Der 1 Fehler (`NavMenuPlaylistsTests…PlaylistEntry`) war eine fehlende `ProgramSettingsService`-
  Registrierung in der Test-DI — gefixt, danach gezielt 165/165 grün über die betroffenen Suiten
  (NavMenu*, LiveTv*, Interleaving, TVSenderAdmin, ProgramGuide, LiveStream, Worker, ProgramSettings).
  Erster Zwischenlauf im Scratch-OutDir zeigte 47 Fehler — sämtlich OutDir-Artefakte
  (repo-relative Pfadauflösung) plus drei echte Flag-Seeds (Client-ProgramGuide ×2, LiveTv-Gap-E2E),
  die anschließend behoben wurden. 1 Skip = bekannter Alt-Skip (Playwright-Umgebung).

## Neue/geänderte Tests je Aufgabe

- **N23:** `StrandOrderKey_IsDeterministic_AndNotAlphabetic`,
  `EnsureCoverage_GenreRule_LeadStrand_VariesAcrossRules_NotAlphabetical`,
  `StableSlotsAcrossDays` (gehärtet: Positionskonsistenz statt fixierter A/B-Identität).
- **N24:** `PollTick_RefreshesUpcomingList`, `PollTick_NowFails_UpcomingStillRefreshes`
  (bUnit + `ControllableTimeProvider`-Testhelfer).
- **N25:** `GetNow/GetUpcoming_FeatureNotReleased_Returns404`,
  `NavMenu_HidesLiveTvEntry_WhenFeatureNotReleased`,
  `Page_FeatureNotReleased_ShowsHint_WithoutGuideCallOrPoll`;
  Fixture-/E2E-Seeds aktivieren `TvSenderEnabled` (ProgramScheduleTestDb, Worker-Tests,
  ProgramGuideE2E, LiveStreamE2E, LiveTvE2E, TVSenderAdminE2E, Client-Guide-Tests).

## Nachbesserungen Runde 2 (acceptance-schritt-26.md)

- Alt-Backup-Regressionstest `ReadFromAsync_LegacyBackupWithoutTvSenderEnabledColumn_
  RestoresDefaultFalse` (Seed true -> Restore false).
- Worker-Idle-Tests: `Worker_IdlesWithoutSchedule_WhenFeatureNotReleased`,
  `ExecuteAsync_FeatureNotReleased_StopsSegmenterAndNeverStarts`,
  `ExecuteAsync_FeatureNotReleased_LeavesItemsUnprobed`.
- N23-Test auf Hash-Pin umgestellt (`LeadStrand_FollowsHashOrder_NotAlphabetical`).
- Voller Lauf Release: 2193/2195 gruen, 1 Alt-Skip, 1 Playwright-Flake
  (`PlaylistSkipLockedEntriesE2ETest`, beim Einzelretry gruen).

## Reviews

- Plan-Check: `plan-check.md` (Plan vollständig).
- Code-Review (selbstkritisch): Strang-Mix cursor-kompatibel; Poll startet erst interaktiv;
  `upcoming`-Abruf entkoppelt; Flag serverseitig an allen Eintrittspunkten; Worker idle.
  Befund im Lauf: doppelter `result`-Block im LiveStreamWorker entfernt; leere Migration
  durch stale-Assembly-`ef` (handgepflegt, Snapshot verifiziert: `PendingModelChanges`
  danach weg).
