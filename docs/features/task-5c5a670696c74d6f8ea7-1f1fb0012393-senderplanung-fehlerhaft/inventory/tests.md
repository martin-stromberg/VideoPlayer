# Tests — Bestandsaufnahme Senderplanung

## Test-Ausgangszustand vor der Umsetzung

- **Zeitpunkt (mit Zeitzone):** Läufe am 2026-11-01, 17:56–18:05 Uhr +01:00
  (Europe/Berlin; Datum der Bestandsaufnahme 2026-11-01T17:55+01:00 /
  16:55 UTC).
- **Branch und Commit-ID:** `task/5c5a670696c74d6f8ea71f1fb0012393-senderplanung-fehlerhaft`,
  Commit `58a63dfcffc035068a91dd39dd726469d90390ea`
  („chore: Dummy-Commit für Update-Test-Prerelease (#327)").
- **Uncommittete Änderungen im getesteten Stand:** ausschließlich neue, untracked
  Dateien — keine Änderungen an getracktem Code:
  - `VideoWebPlayer.Tests/Helpers/PrimetimeSeed.cs`
  - `VideoWebPlayer.Tests/ProgramScheduleServiceTests_PrimetimeAgeWindow.cs`
  - `VideoWebPlayer.Tests/ProgramScheduleServiceTests_PrimetimeChanges.cs`
  - `VideoWebPlayer.Tests/ProgramScheduleServiceTests_PrimetimeGrace.cs`
  - `VideoWebPlayer.Tests/ProgramScheduleServiceTests_PrimetimeRolling.cs`
  - `VideoWebPlayer.Tests/ProgramScheduleServiceTests_PrimetimeTrailer.cs`
  - `docs/features/task-5c5a670696c74d6f8ea7-1f1fb0012393-senderplanung-fehlerhaft/`
    (Arbeitsdokumente inkl. dieser Bestandsaufnahme und der Testnachweise)
- **Testumgebung und Runtime-/SDK-Versionen:** Windows (MINGW64_NT-10.0-26300),
  .NET SDK 10.0.403, Test-Runtime .NET 10.0.14, xUnit.net v3 (Test Sdk 18.10.0,
  Adapter 3.1.5), Ziel-Framework `net10.0`; Playwright 1.62.0 mit lokal
  installiertem Chromium (`%USERPROFILE%\AppData\Local\ms-playwright`,
  Builds bis 1248). Tests laufen gegen echtes SQLite
  (`Microsoft.Data.Sqlite`, In-Memory shared cache, `EnsureCreated`).
- **Ermittelte Testsuiten und Quellen der Testbefehle:** CI-Workflow
  `.github/workflows/pr-staging-ci.yml` (und identisch `staging-ci.yml`) definiert
  drei Läufe auf dem Release-Build des Testprojekts:
  `Category!=E2E` (Unit-Tests, blocking), `Category=Integration`,
  `Category=E2E` (Playwright). Traits: 75 Vorkommen von
  `[Trait("Category", "E2E")]`; es existiert **kein** Test mit
  `Category=Integration` im Codebestand. AGENTS.md §7 nennt zusätzlich den
  DoD-Gesamtlauf `dotnet test VideoWebPlayer.Tests/VideoWebPlayer.Tests.csproj`
  sowie `dotnet build VideoPlayer.sln` in Debug UND Release.

### Builds

| Build | Befehl | Ergebnis |
|-------|--------|----------|
| Release-Build des Testprojekts | `dotnet build VideoWebPlayer.Tests/VideoWebPlayer.Tests.csproj -c Release -p:NoWarn=NU1903` | Erfolgreich: 0 Fehler, 140 Warnungen (u. a. BL0008 Razor-Hinweise) — Voraussetzung aller drei Läufe |

### Testläufe

| Lauf | Befehl inkl. Filter | Arbeitsverzeichnis | Exit-Code | Erfolgreich | Fehlgeschlagen | Übersprungen | Nachweis |
|------|--------------------|--------------------|-----------|-------------|----------------|--------------|----------|
| Unit (Category!=E2E), 17:56:46–17:59:34 +01:00 | `dotnet test VideoWebPlayer.Tests/VideoWebPlayer.Tests.csproj --no-build -c Release --filter "Category!=E2E" --logger "trx;LogFileName=unit-test-results.trx"` | Repo-Root | 1 | 2603 von 2607 (2606 ausgeführt) | 3 | 1 (NotExecuted) | [TRX](test-results/unit-test-results.trx), [Konsole](test-results/unit-test-console.log) |
| Integration (Category=Integration), ~17:59 +01:00 | `dotnet test VideoWebPlayer.Tests/VideoWebPlayer.Tests.csproj --no-build -c Release --filter "Category=Integration"` | Repo-Root | 0 | 0 | 0 | 0 | [Konsole](test-results/integration-test-console.log) — „Kein Test entspricht dem angegebenen Testfallfilter" |
| E2E (Category=E2E), 18:00:44–18:04:58 +01:00 | `dotnet test VideoWebPlayer.Tests/VideoWebPlayer.Tests.csproj --no-build -c Release --filter "Category=E2E" --logger "trx;LogFileName=e2e-test-results.trx"` | Repo-Root | 0 | 247 von 248 (247 ausgeführt) | 0 | 1 (NotExecuted) | [TRX](test-results/e2e-test-results.trx), [Konsole](test-results/e2e-test-console.log) |

### Nachgewiesene bestehende Testfehler

Exakt die drei in `issue.md` belegten Soll-Abweichungen — gleiche Tests, gleiches
Fehlerbild:

| Test-ID inkl. Testfall | Suite / Dateipfad | Fehlerbild / Fehlermeldung | Lauf und Nachweis |
|------------------------|-------------------|----------------------------|-------------------|
| `VideoWebPlayer.Tests.ProgramScheduleServiceTests_PrimetimeRolling.EnsureCoverage_FirstRunAfterEndedOccurrence_RingStartsAtBeginning` | `VideoWebPlayer.Tests/ProgramScheduleServiceTests_PrimetimeRolling.cs` (Z. 99) | `Assert.Equal() Failure: Collections differ` — Expected `[1, 2, 3, 4, 5]`, Actual `[6, 7, 8, 1, 2]` (Episode-Ids): die „gestrige" 20:15-Occurrence, die zum Planungszeitpunkt 12:00 bereits beendet war und nie geplant wurde, verbraucht die ersten Ringpositionen (Lookback-Konsum) | Unit-Lauf, [TRX](test-results/unit-test-results.trx) |
| `VideoWebPlayer.Tests.ProgramScheduleServiceTests_PrimetimeChanges.Simulation_GenreAddedToRule_NoDisplacement` | `VideoWebPlayer.Tests/ProgramScheduleServiceTests_PrimetimeChanges.cs` (Z. 31) | `AssertNoDisplacedAsync` schlägt fehl: **18 Slots verdrängt** ab dem Änderungstag (03.19:15–09.00:05, Regel 1) — Genre-Zuwachs während der Wochensimulation | Unit-Lauf, [TRX](test-results/unit-test-results.trx) |
| `VideoWebPlayer.Tests.ProgramScheduleServiceTests_PrimetimeChanges.Simulation_MovieAddedToGenre_NoDisplacement` | `VideoWebPlayer.Tests/ProgramScheduleServiceTests_PrimetimeChanges.cs` (Z. 57) | `AssertNoDisplacedAsync` schlägt fehl: **18 Slots verdrängt** ab dem Änderungstag (04.19:15–09.00:05, Regel 1) — ein einzelner neuer Film in der Materialmenge | Unit-Lauf, [TRX](test-results/unit-test-results.trx) |

Alle drei Fehlschläge sind die dokumentierten Soll-Abweichungen der Anforderung
(Befund 1: Mengenwachstum verdrängt Bestand; Befund 2: Lookback verbraucht
Ringpositionen), keine Testfehler.

### Übersprungene / nicht ausgeführte Tests

| Test | Lauf | Grund |
|------|------|-------|
| `VideoWebPlayer.Tests.Services.FfmpegToolDownloadServiceTests_Install.Run_OnUnix_SetsExecutableBit` | Unit | `Assert.SkipWhen(OperatingSystem.IsWindows(), "Unix-Dateirechte lassen sich nur unter Unix prüfen.")` — Laufzeitbedingter Skip auf Windows |
| `VideoWebPlayer.Tests.DevicePlaylistE2ETests_Playback.PairedDevice_ReportsProgressForMovieWithoutCollection_ContinueWatchingListStillLoads` | E2E | `[Fact(Skip = …)]` — bekannter, bewusst dokumentierter Befund außerhalb dieses Auftrags (HTTP 500 in `api/continue-watching` bei Film ohne Filmsammlung, `ContinueWatchingService.Create` ohne Null-Prüfung) |

### Testlücken und Ausführungsprobleme

- Die Suite `Category=Integration` enthält im gesamten Projekt **keinen einzigen
  Test** — der CI-Schritt ist de facto leer (Nachweis:
  [integration-test-console.log](test-results/integration-test-console.log)).
- Namensgebung vs. Trait: `TVSenderAdminE2ETests` und `TVSenderAdminE2ETests_AgeWindow`
  tragen **kein** `[Trait("Category", "E2E")]` und laufen daher im Unit-Filter
  `Category!=E2E` mit (dort als Playwright-Tests erfolgreich gelaufen, u. a.
  `TVSenderAdmin_RepeatGenerate_ProducesNoDisplacedSeries` 36 s). Kennzeichnung:
  `Category=E2E`-Lauf = 248 Tests, `Category!=E2E`-Lauf = 2607 Tests.
- Wiederholungsläufe wurden nicht benötigt: kein flackernder Ausfall aufgetreten.

## Neue Testklassen (Soll-Spezifikation, uncommittet)

Alle fünf Dateien gehören zur Klasse der neuen Szenario-Tests aus `issue.md` und
verwenden `ProgramScheduleTestDb` (echtes SQLite) plus `PrimetimeSeed`.
Getestet wird der produktive `ProgramScheduleService` ohne Testdoublen; die Zeit
wird ausschließlich über den `utcNow`-Parameter gesteuert.

### `ProgramScheduleServiceTests_PrimetimeRolling`

Datei: `VideoWebPlayer.Tests/ProgramScheduleServiceTests_PrimetimeRolling.cs`
(Szenarien 1.a/1.f/1.k/1.l + Soll-Entscheidung Lookback). Hilfsmethode
`SeedScenarioAsync` (Z. 20): zwei Film-Genres (6×100 min, 6×95 min), ein
Serien-Genre (12×45 min), eine Clip-Gruppe (5×2 min) + Trailer-Gruppe.

- `Simulation_WeekOfDailyRuns_PlanOnlyExtendsNoDisplacement` (Z. 39) — 1.a: 7
  Tage `StepAsync` (Reconcile + EnsureCoverage), `AssertNoDisplacedAsync` +
  `AssertPreserved` je Tag, Negativraum `AssertStartsInsidePeriod`,
  `AssertEveryEveningHasTitles`. **Grün.**
- `EnsureCoverage_RuleCreatedMidPrimetime_SchedulesBackToPeriodStart` (Z. 66) —
  1.f: Regel „erstellt" um 20:45 → Plan rückwirkend ab 20:15. **Grün.**
- `EnsureCoverage_FirstRunAfterEndedOccurrence_RingStartsAtBeginning` (Z. 99) —
  Soll-Entscheidung: beendete Occurrence verbraucht keine Ringposition.
  Erwartet `[E1..E5]`, erhält `[E6,E7,E8,E1,E2]`. **Rot (belegte Soll-Abweichung).**
- `EnsureCoverage_WeekOfAirings_StrandStructureStableAndOrderContinues` (Z. 122) —
  1.k: Episodenreihenfolge +1/Wrap ohne Sprung; identische Titel-Typ-Struktur je
  Abend. **Grün.**
- `EnsureCoverage_AcrossDstChange_EveryAiringStartsAt2015Local` (Z. 171) —
  1.l: zwei Läufe um die Zeitumstellung 25./26.10.2026; jeder Abend startet
  20:15 lokal. **Grün.**

### `ProgramScheduleServiceTests_PrimetimeChanges`

Datei: `VideoWebPlayer.Tests/ProgramScheduleServiceTests_PrimetimeChanges.cs`
(Szenarien 1.d/1.e). `SeedBaseAsync` (Z. 16): 1 Film-Genre (4×100 min),
1 Serien-Genre (8×45 min), 1 Clip-Gruppe (4×2 min) + Trailer-Gruppe.

- `Simulation_GenreAddedToRule_NoDisplacement` (Z. 31) — 1.d: am Tag 2 kommt ein
  Genre mit eigener Serie (neuer Interleaved-Strang) hinzu; `AssertNoDisplacedAsync`
  + `AssertPreserved`. **Rot** — 18 verdrängte Slots ab Änderungstag.
- `Simulation_MovieAddedToGenre_NoDisplacement` (Z. 57) — 1.e: ein neuer Film im
  bestehenden Genre; gleiche Assertions. **Rot** — 18 verdrängte Slots ab
  Änderungstag.

### `ProgramScheduleServiceTests_PrimetimeGrace`

Datei: `VideoWebPlayer.Tests/ProgramScheduleServiceTests_PrimetimeGrace.cs`
(Szenarien 1.b/1.c/1.j).

- `EnsureCoverage_RemainderBelowGrace_ClipsFillNoThirdMovie` (Z. 23) — 1.b:
  110+105 min → 10 min Rest < Karenzzeit; genau 2 Titel, Rest mit Clips, kein
  Wrap-around-Film, kein Titel nach dem letzten Titel-Ende. **Grün.**
- `EnsureCoverage_RemainderAboveGrace_ThirdMovieOvershootsMidnight` (Z. 64) — 1.c:
  100+95 min → 30 min Rest; dritter Film startet 23:30, endet 01:30; keine
  Clips der abgelaufenen Periode danach; Folgetag sauber ab 20:15. **Grün.**
- `EnsureCoverage_ClipRest_DistributedAcrossBothGroupsNoTitleAfter` (Z. 109) —
  1.j: Rest aus beiden Nutzer-Clip-Gruppen im Wechsel (Round-Robin); kein Titel
  nach Füllbeginn. **Grün.**
- Hilfsmethode `FixtureMovieAsync` (Z. 149).

### `ProgramScheduleServiceTests_PrimetimeAgeWindow`

Datei: `VideoWebPlayer.Tests/ProgramScheduleServiceTests_PrimetimeAgeWindow.cs`
(Szenarien 1.g/1.h; Default-Fenster FSK 18 ab 23:00, Nachtende 06:00;
Hilfsfunktion `InsideNightWindow` Z. 21).

- `EnsureCoverage_Fsk18Movie_NeverStartsOutsideNightWindow` (Z. 30) — 1.g
  ungeordneter Ring: FSK-18-Film startet nie außerhalb des Nachtfensters; jeder
  Abend behält Titel. **Grün.**
- `EnsureCoverage_Fsk18Show_BlockedHeadKeepsAirOrder` (Z. 59) — 1.g geordneter
  Strang: blockierte Kopf-Episode deferriert, Sendedreihenfolge bleibt
  lückenlos (+1/Wrap). **Grün.**
- `EnsureCoverage_UnratedMovie_Unrestricted_StartsInPrimetime` (Z. 92) — 1.h
  `Unrestricted`: Titel ohne Freigabe darf 20:15 starten. **Grün.**
- `EnsureCoverage_UnratedMovie_Restricted_NeverStartsBeforeNightWindow` (Z. 117) —
  1.h `RestrictToNightWindow`: derselbe Titel nie vor 23:00. **Grün.**

### `ProgramScheduleServiceTests_PrimetimeTrailer`

Datei: `VideoWebPlayer.Tests/ProgramScheduleServiceTests_PrimetimeTrailer.cs`
(Szenario 1.i).

- `EnsureCoverage_TrailerClipGroup_AirsTrailerBeforeItsMovie` (Z. 19) — Trailer
  des geplanten Films läuft vor dessen Ausstrahlung; Trailer eines nie
  geplanten Films erscheint nirgends. **Grün.**

## Hilfsmethoden

### `PrimetimeSeed` (`internal static`)

Datei: `VideoWebPlayer.Tests/Helpers/PrimetimeSeed.cs` — Shared-Seed- und
Assertion-Hilfen der Primetime-Szenarien. Konstanten: `Start` = 20:15,
`Period` = 225 min, `Grace` = 15 min, `HorizonDays` = 7;
Zeitbasis `TimeZoneInfo.Local` (wie `BuildOccurrences`).

- `LocalUtc(y, m, d, h, min)` / `ToLocal(utc)` / `LocalMinutesOfDay(utc)` —
  lokale Zeitkonversion wie im Produktivcode.
- `EveningDateOf(utc)` — ordnet einen Slot seinem Sende-Abend zu (Starts ab
  20:15 bis Folgetag 20:15; Overshoot nach Mitternacht bleibt am Startabend).
- `SetHorizonDaysAsync(fixture, days, ct)` — setzt
  `Setup.ProgramScheduleHorizonHours` (Horizont aus
  `ProgramSettingsService.GetProgramScheduleHorizonAsync`).
- `CreateRuleAsync(fixture, channelId, genreIds, clipGroupIds)` — baut die
  Szenario-Regel: `GenreRotation`, `Daily`, 20:15, `Genre`-Material (Id 0),
  Quell-Filter `[1]`, 225 min, Karenzzeit 15 min.
- `AddMovieGenreAsync` / `AddShowGenreAsync` — Genre + Filme bzw. Serie auf
  Quelle 1, jeweils verlinkt, optional `minAge`.
- `AddClipGroupAsync` — Nutzer-Clip-Gruppe mit unklassifizierten MediaItems.
- `AddTrailerAsync` — `MovieMediaItem`-Link mit `IsTrailer` (MediaItem bleibt
  unklassifiziert für den Clip-Kandidatenfilter).
- `StepAsync(fixture, now, ct)` — eine Worker-Iteration: `ReconcileAsync` dann
  `EnsureCoverageAsync` mit demselben `utcNow`.
- `AssertPreserved(before, after, context)` — Vollbestand: jeder
  Scheduled-Vorgänger existiert unverändert (Start, Ende, Inhalt, Regel,
  Clip-Flag).
- `AssertStartsInsidePeriod(slots)` — Negativraum: kein Regel-Start vor 20:15
  lokal.
- `AiringOf(ruleSlots, evening)` / `AssertEveryEveningHasTitles(ruleSlots,
  firstEvening, evenings)` — Abend-Auswahl und Positiv-Check.

### `ProgramScheduleTestDb` (`internal sealed`, Fixture)

Datei: `VideoWebPlayer.Tests/Helpers/ProgramScheduleTestDb.cs` — echtes SQLite
(In-Memory `mode=memory&cache=shared`) mit FKs; `Setup`-Zeile mit
`TvSenderEnabled = true` und `ProgramScheduleUnknownRatingMode = Unrestricted`
(Tests, die den restriktiven Modus prüfen, setzen ihn explizit); seedet die
Trailer-System-Clip-Gruppe (`EnsureCreated` führt keine `InsertData`-Migration
aus). Erzeugt den echten `ProgramScheduleService` (`CreateService`, Z. 79) und
`ProgramGuideService`; Seed-Hilfen für Benutzer, Quellen, MediaItems mit
probierten Laufzeiten, Filme, Serien, Genres, Regeln (`CreateRuleAsync` nutzt
`ProgramRuleService.ApplyValidity`), Slots; Assertions
`AssertNoDisplacedAsync`, `AssertReconcileNoOpAsync`,
`AssertNoSelfDisplacedAsync`.

## Bestehende Testklassen des Bereichs (Regressionsschutz)

28 `ProgramScheduleServiceTests_*`-Dateien (~212 Facts/Theories) decken den
Service bereits ab; für diese Anforderung besonders relevant:

| Klasse | Fokus |
|--------|-------|
| `ProgramScheduleServiceTests_RingOrder` | `ItemOrderKey`-Determinismus, `MixUnorderedRing`-Mischung, `Reconcile_MixedRing_StaysReproducible` |
| `ProgramScheduleServiceTests_Interleaving` | Strang-Interleaving, `StrandOrderKey`, Cursor-Fortsetzung je Serie |
| `ProgramScheduleServiceTests_Reconcile` | Verdrängungs-/Beibehalt-Verhalten, Long-Airing-Lookback, Rewind (`Reconcile_DisplacedSeries_ReschedulesAtNextOccurrenceWithoutSkip`, `Reconcile_PartiallyAiredOccurrence_*`) |
| `ProgramScheduleServiceTests_Generation` | Recurrence/Validity-Occurrences, `EnsureCoverage_SpanningOccurrence_KeepsTrueStart`, `EnsureCoverage_SecondRun_ExtendsNotDuplicates` |
| `ProgramScheduleServiceTests_Boundary` | Overshoot/Shift, `MinStartRemain`, `MaxOccurrenceLookback`-Normalisierung |
| `ProgramScheduleServiceTests_ClipFillPeriodBounds` | Clip-Fill innerhalb des nominalen Periodenendes |
| `ProgramScheduleServiceTests_Stability` | Reconcile-Loops ohne falsche Positive |
| `ProgramScheduleServiceTests_AgeWindow` | Fenster-Semantik geordnet/ungeordnet, Displaced-Ring |
| `ProgramScheduleServiceTests_Issue315`/`_Issue320` | Laufende Slots, Deferral-Rekonstruktion |
| `ProgramScheduleServiceTests_Trailer`, `_ClipGroups`, `_ClipSource` | Trailer-Vorlauf und Clip-Füllung |
| `ProgramScheduleServiceTests_CursorSelection`, `_PriorityTakeover`, `_Conflicts`, `_Genres`, `_Filler`, `_Access`, `_ContentScope`, `_Diagnosis`, `_Cleanup`, `_InvalidReason` | Einzelaspekte des Services |

Alle 16 neuen Primetime-Tests sind im Unit-Lauf (`Category!=E2E`) enthalten —
Status: 13 grün, 3 rot (s. o.).
