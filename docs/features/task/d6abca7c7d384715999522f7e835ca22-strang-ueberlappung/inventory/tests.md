# Bestandsaufnahme — Tests

## Test-Ausgangszustand vor der Umsetzung

- Zeitpunkt (mit Zeitzone): 2026-11-09, lokale Zeit (Europe/Berlin)
- Branch und Commit-ID: `task/d6abca7c7d384715999522f7e835ca22-strang-ueberlappung` @ `8e77eb5` („Sendeplan: Faire Strang-Rotation bei interleaveten Ausstrahlungen (Issue #341) (#342)")
- Uncommittete Änderungen im getesteten Stand: keine Quelländerungen — nur neue Planungsartefakte unter `docs/features/task/d6abca7c7d384715999522f7e835ca22-strang-ueberlappung/` (beeinflussen den Build nicht)
- Testumgebung und Runtime-/SDK-Versionen: Windows, .NET SDK 10.0.403, xUnit, SQLite-In-Memory (`ProgramScheduleTestDb`), Playwright-E2E-Tests in der Suite enthalten
- Ermittelte Testsuiten und Quellen der Testbefehle: `VideoWebPlayer.Tests` (einzige Suite) — Befehl gemäß `/run-tests` und AGENTS §7

### Testläufe

| Lauf | Befehl inkl. Filter | Arbeitsverzeichnis | Exit-Code | Erfolgreich | Fehlgeschlagen | Übersprungen | Nachweis |
|------|--------------------|--------------------|-----------|-------------|----------------|--------------|----------|
| Baseline | `dotnet build VideoPlayer.sln` (Debug) + `dotnet test VideoWebPlayer.Tests/VideoWebPlayer.Tests.csproj --no-build --logger "console;verbosity=normal"` | Repo-Root | 0 | 2921 | 0 | 2 | [baseline-dotnet-test.txt](test-results/baseline-dotnet-test.txt) |

### Nachgewiesene bestehende Testfehler

Keine — der Baseline-Lauf ist vollständig grün (0 fehlgeschlagene Tests).

### Testlücken und Ausführungsprobleme

Übersprungen (vom Testframework selbst übersprungen, kein Fehlschlag):

- `DevicePlaylistE2ETests_Playback.PairedDevice_ReportsProgressForMovieWithoutCollection_ContinueWatchingListStillLoads` — übersprungen (Infrastruktur-/Bedingungsskip).
- `FfmpegToolDownloadServiceTests_Install.Run_OnUnix_SetsExecutableBit` — Unix-only, auf Windows übersprungen.

## Testklassen

### `ProgramScheduleServiceTests_StrandRotation` (`VideoWebPlayer.Tests/ProgramScheduleServiceTests_StrandRotation.cs`)

Themenklasse der Strang-Rotation (Vorgänger-Fix Issue #341) — Zielklasse der neuen Tests:

- `EnsureCoverage_ManyCollectionStrands_RotatesLeadStrandAcrossDays` — Lead-Strang rotiert über Tage; alle Stränge kommen dran. **Schwäche (Issue #343):** prüft nur `leads[i] != leads[i-1]`, nicht die Disjunktheit der Bedient-Mengen — der Mechanismus wird verifiziert, nicht das Ergebnis.
- `EnsureCoverage_ManyCollectionStrands_ReleaseOrderAndCursorsPreserved` — Release-Reihenfolge je Sammlung + `ProgramRuleCursors`-Fortschreibung.
- `EnsureCoverage_AllStrandsServed_NoRotation_StableStructure` — Kontrollfall: alle Stränge bedient → stabile Tagesstruktur.
- `Reconcile_RotatedPlan_StaysScheduledNoFalseDisplacement` — pick-agnostische Validierung verwirft rotierten Plan nicht.
- `EnsureCoverage_EpisodeCountBelowStrandCount_StillRotates` — Regressionskontrolle des `EpisodeCount`-Altwegs.

Weitere relevante Klassen (Regressionsschirm): `ProgramScheduleServiceTests_Interleaving`, `_PrimetimeGrace`, `_PrimetimeAgeWindow`, `_PrimetimeRolling`, `_CursorSelection`, `_Reconcile`, `_Stability`, `_ClipGroups`, `_CollectionOrder`, `_RingOrder`.

## Hilfsmethoden

### `ProgramScheduleTestDb` (`VideoWebPlayer.Tests/Helpers/ProgramScheduleTestDb.cs`)

- `CreateAsync(dbName, ct)` — echte SQLite-In-Memory-Datenbank pro Test (erfüllt AGENTS §7: keine EF-InMemory-Tests).
- `CreateRuleAsync(channelId, name, type, recurrence, timeOfDay, materialType, ?, episodeCount, slotDuration, genreIds, mediaSourceIds, clipGroupIds, placeholderGrace, …)` — Regelseed.
- `CreateMovieAsync(name, collectionId:, duration:, releaseDate:)`, `CreateGenreAsync`, `LinkMovieGenreAsync`, `CreateShowAsync`, `LinkShowGenreAsync`, `CreateClipGroupAsync`, `AddClipToGroupAsync`, `CreateMediaItemAsync`, `GetChannelAsync`, `SlotsAsync`, `CreateService`, `SaveDiagAsync`, `AssertNoDisplacedAsync`.

### `PrimetimeSeed` (`VideoWebPlayer.Tests/Helpers/PrimetimeSeed.cs`)

- `Start`/`Period`/`Grace`/`HorizonDays` — Szenariokonstanten (20:15 lokal, 225 min, 15 min, 7 Tage).
- `LocalUtc`, `ToLocal`, `EveningDateOf`, `AiringOf(ruleSlots, evening)` — Titel-Slots eines Ausstrahlungsabends.
- `SetHorizonDaysAsync`, `CreateRuleAsync`, `CreateHolidayRuleAsync`, `AddMovieGenreAsync`, `AddShowGenreAsync`, `AddClipGroupAsync`, `AddTrailerAsync`, `StepAsync` (ExecutePass: Reconcile+Coverage), `AssertPreserved`, `AssertStartsInsidePeriod`, `AssertEveryEveningHasTitles`.

### `SeedCollectionRotationScenarioAsync` (privat, in `ProgramScheduleServiceTests_StrandRotation`)

Seeds `strandCount` `MovieCollection`-Stränge à `moviesPerCollection` Filmen gleicher Laufzeit + Genre + Clip-Gruppe + SlotDuration-Genre-Regel — direkt für die neuen Tests wiederverwendbar (`RotationScenario` trägt `CollectionOfMovie`/`ReleaseOrder`).
