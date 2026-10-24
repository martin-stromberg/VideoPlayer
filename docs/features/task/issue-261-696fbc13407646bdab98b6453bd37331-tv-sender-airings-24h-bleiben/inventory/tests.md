# Tests

## Test-Ausgangszustand vor der Umsetzung

- Zeitpunkt (mit Zeitzone): 24.10.2026, ca. 11:05–12:15 Uhr (lokale Systemzeit, Europe/Berlin)
- Branch und Commit-ID: `task/issue-261-696fbc13407646bdab98b6453bd37331-tv-sender-airings-24h-bleiben`, Commit `cbdb0b829b355d4efac96ab152b6b7e13b96cf31`
- Uncommittete Änderungen im getesteten Stand: nur untracked `docs/features/task/issue-261-…/` (Anforderungs- und Inventar-Artefakte); **keine** Änderungen an Produktivcode, Tests oder Konfiguration
- Testumgebung und Runtime-/SDK-Versionen: Windows (Git Bash/MSYS), .NET SDK 10.0.402 (`net10.0`), xunit.v3 3.2.2, Microsoft.Playwright 1.62.0; Playwright-Browser lokal unter `%LOCALAPPDATA%/ms-playwright` installiert (chromium u. a. 1248)
- Ermittelte Testsuiten und Quellen der Testbefehle: `.github/workflows/pr-staging-ci.yml` und `staging-ci.yml` (Job `build-and-test`) verwenden in Release:
  1. `dotnet build VideoWebPlayer.Tests/VideoWebPlayer.Tests.csproj -c Release -p:NoWarn=NU1903`
  2. `pwsh VideoWebPlayer.Tests/bin/Release/net10.0/playwright.ps1 install --with-deps chromium` (nur CI; lokal bereits installiert)
  3. `dotnet test … --filter "Category!=E2E"` (Unit-Tests)
  4. `dotnet test … --filter "Category=Integration"` — im Repo existiert **kein** Test mit `Trait("Category","Integration")`; dieser Schritt würde 0 Tests ausführen und wurde daher lokal nicht gesondert wiederholt
  5. `dotnet test … --filter "Category=E2E"` (Playwright-E2E-Tests, Chromium)
  Die DoD in `AGENTS.md` verlangt `dotnet test VideoWebPlayer.Tests/VideoWebPlayer.Tests.csproj` vollständig — die Filterläufe decken die Suite gemeinsam ab (2012 + 226 = 2238 Tests).

### Testläufe

| Lauf | Befehl inkl. Filter | Arbeitsverzeichnis | Exit-Code | Erfolgreich | Fehlgeschlagen | Übersprungen | Nachweis |
|------|--------------------|--------------------|-----------|-------------|----------------|--------------|----------|
| Build (Release) | `dotnet build VideoWebPlayer.Tests/VideoWebPlayer.Tests.csproj -c Release -p:NoWarn=NU1903` | Repo-Root | 0 | – | – | – | 0 Fehler, 142 Warnungen (BL0008/CS86xx — Bestand, auch in CI toleriert) |
| Unit/ohne E2E | `dotnet test VideoWebPlayer.Tests/VideoWebPlayer.Tests.csproj --no-build -c Release --filter "Category!=E2E" --logger "trx;LogFileName=baseline-non-e2e.trx"` | Repo-Root | 0 | 2012 | 0 | 0 | [baseline-non-e2e.log](test-results/baseline-non-e2e.log), [baseline-non-e2e.trx](test-results/baseline-non-e2e.trx); Dauer 1:21 min |
| E2E | `dotnet test VideoWebPlayer.Tests/VideoWebPlayer.Tests.csproj --no-build -c Release --filter "Category=E2E" --logger "trx;LogFileName=baseline-e2e.trx"` | Repo-Root | 0 | 225 | 0 | 1 | [baseline-e2e.log](test-results/baseline-e2e.log), [baseline-e2e.trx](test-results/baseline-e2e.trx); Dauer 4:13 min |

### Nachgewiesene bestehende Testfehler

Keine — beide Suiten ohne Fehlschlag (Unit 2012/2012, E2E 225/226 mit 1 Übersprungenem).
Keine Wiederholungsläufe nötig; kein Flakern aufgetreten.

### Testlücken und Ausführungsprobleme

- Die `Category=Integration`-Filterstufe der CI besitzt keinen einzigen Test im Repo (kein
  `Trait("Category","Integration")` vorhanden) — kein Ausführungsproblem, die Stufe ist
  inhaltlich leer.
- E2E-Tests benötigen installierte Playwright-Browser (Chromium) und laufen deutlich länger
  (~4,2 min vs. ~1,3 min Unit); einzelne Playwright-Tests flackern unter Last bekanntlich
  (vgl. `AGENTS.md` §7) — im Baseline-Lauf trat kein Flackern auf.
- **Übersprungener Test:** `DevicePlaylistE2ETests_Playback.PairedDevice_ReportsProgressForMovieWithoutCollection_ContinueWatchingListStillLoads`
  (`DevicePlaylistE2ETests_Playback.cs` Z. 76–77) — absichtlich per `[Fact(Skip = …)]` deaktiviert:
  dokumentierter Befund, dass `api/continue-watching` mit HTTP 500 antwortet, wenn ein Film zu
  keiner Filmsammlung gehört. Unabhängig von dieser Anforderung; nicht Teil der Baseline als
  Fehler, sondern als dokumentierter Skip.

## Testklassen (bereichsbezogen)

### `ProgramScheduleServiceTests_Reconcile` — `VideoWebPlayer.Tests/ProgramScheduleServiceTests_Reconcile.cs`

13 Tests; Aufbau-Muster aller Reconcile-Tests: Fixture anlegen → Kanal holen → Material
(Film/Serie/Füller) mit probierten Dauern erzeugen → Regeln direkt per `CreateRuleAsync` seeden
(Service-Validierung umgangen) → `EnsureCoverageAsync(now)` → Zustand mutieren (Regel
deaktiviert/gelöscht/geändert, MediaItem gelöscht, neue Episode) oder nicht → `ReconcileAsync(now + x)`
→ Assert auf `SlotsDisplaced`, `Status`, Slot-Positionen/-Reihenfolgen, `AssertGapless`.

- `Reconcile_NoChanges_LeavesEverythingScheduled` — Idempotenz ohne Änderungen (`SlotsDisplaced == 0`)
- `Reconcile_DeactivatedRule_DisplacesFutureAndRefills` — inaktive Regel verdrängt alle Zukunfts-Slots, Fenster wird neu befüllt
- `Reconcile_DeletedRule_DisplacesSlotsAndKeepsRows` — gelöschte Regel: Slots bleiben als `Displaced` mit der alten Regel-Id
- `Reconcile_ChangedTimeOfDay_ReplansAtNewTime` — geänderte `TimeOfDay` verdrängt und plant zur neuen Zeit um
- `Reconcile_DisplacedSeries_ReschedulesAtNextOccurrenceWithoutSkip` — Cursor-Rewind: verdrängte Serienfolgen laufen an der nächsten Occurrence in Reihenfolge nach
- `Reconcile_DeletedMediaItem_DisplacesAiringAndContinuesWithoutSkip` — gelöschtes MediaItem verdrängt das Airing; Reihenfolge springt nicht
- `Reconcile_PriorityRaised_EvictsLowerPriorityAiring` — Prioritätsänderung verdrängt das schwächere Airing
- `Reconcile_NewEpisodeInserted_ReplansInAirOrder` — neue Episode mitten im Bestand verändert die Expansion → Verdrängung + Neuplan in Air-Order
- `EnsureCoverage_IgnoresDisplacedSlots` — `Displaced` gilt nicht als Coverage
- `Reconcile_PartiallyAiredOccurrence_KeepsTailScheduled` — **nächster Verwandter des Bugs:** ein zur Coverage-Laufzeit bereits halb gelaufenes Airing (14:00-Occurrence, Lauf ab 15:40 schreibt nur Tail-Slots ab 15:30) darf beim Reconcile nicht verdrängt werden; zweiter Lauf ebenfalls ruhig (keine Oszillation). Nutzt den bestehenden 24-h-Lookback: Occurrence und realer Beginn liegen **innerhalb** des Fensters
- `Reconcile_PartiallyAiredOccurrence_TomorrowAiringsStayStable` — Zukunfts-Airings bleiben positionsstabil
- `Reconcile_WorkerLoop_NoDisplacedAccumulationNoOverlap` — Worker-Kadenz Reconcile+EnsureCoverage 3×; keine Verdrängungs-Duplikate, keine Überlappungen
- `GetSchedule_ReturnsUpcomingSlotsWithStatus` — Admin-Ansicht liefert auch `Displaced`

Lokale Helfer der Datei: `LocalUtc(y,m,d,h,min)` (lokale Zeit → UTC über `TimeZoneInfo.Local`),
`ScheduledAsync`/`DisplacedAsync` (Slot-Filter je Status).

### `ProgramScheduleServiceTests_Interleaving` — `VideoWebPlayer.Tests/ProgramScheduleServiceTests_Interleaving.cs`

6 Tests + Hilfsmethode `SeedGenreWindowAsync` (Genre-Regel `GenreRotation`, täglich 12:00,
`episodeCount: 2`, zwei Serien à 45 min → Interleaved-Pfad mit zwei `"show:"`-Strängen).

- `EnsureCoverage_GenreRule_InterleavesShows_StableSlotsAcrossDays` — stabile Slot-Positionen je Serie über Tage
- `EnsureCoverage_GenreRule_Interleaving_EpisodeOrderPerShow` — Episodenreihenfolge je Strang + eine Cursor-Zeile pro Strang
- `EnsureCoverage_GenreRule_SecondRun_ContinuesEachShowCursor` — zweiter Lauf setzt Strang-Cursor fort (dokumentiert die „Lookback-Consume"-Semantik: der Dienst verbraucht innerhalb des 24-h-Fensters eine bereits gelaufene Occurrence erneut)
- `StrandOrderKey_IsDeterministic_AndNotAlphabetic` — Hash-Mix reproduzierbar, nicht alphabetisch
- `EnsureCoverage_GenreRule_LeadStrand_FollowsHashOrder_NotAlphabetical` — Führungsstrang folgt `StrandOrderKey`
- `Reconcile_GenreRule_Interleaved_NoChanges_LeavesEverythingScheduled` — **einziger Reconcile-Test des Interleaved-Pfads:** `SlotsDisplaced == 0` ohne Änderungen (deckt `AiringReproducibleInterleaved` im Regelfall ab, aber nur innerhalb des 24-h-Fensters)

### `ProgramScheduleServiceTests_Boundary` — `VideoWebPlayer.Tests/ProgramScheduleServiceTests_Boundary.cs`

11 Tests; hier stecken die Invader-/Verschiebe-Szenarien, die `invaderStarts` absichern:

- `Reconcile_PartialOccurrence_OvershootingTailStaysScheduled_SuccessorShifts` — „Katastrophenzustand": Slots einer Regel werden per `ExecuteUpdate` auf `Displaced` gesetzt; Reconcile materialisiert den Tail hinter dem weiterlaufenden Vorgänger-Film neu (begonnenes Video wird nie geschnitten)
- `Reconcile_ShiftedAndOvershootingAirings_StayStableAcrossRuns` — 6-h-`SlotDuration`-Airing (10:00) + 4-h-Airing (16:00): überschießende/verschobene Airings bleiben über 3 Worker-Kadenzen stabil (`SlotsDisplaced == 0`) — längste real erzeugte Airings im Bestand, aber weit unter 24 h
- `EnsureCoverage_OversizedHeadItem_AirsAndOvershootsSuccessor` — 300-min-Film überschießt die Nachbar-Occurrence; Nachfolger startet hinter seinem Ende (Präzedenzfall für sehr lange Items per `CreateMovieAsync(duration: …)`)
- `EnsureCoverage_ShortRemainder_OvershootingItem_Completes` / `EnsureCoverage_RemainderBelowMinRemain_PlansFiller` / `EnsureCoverage_HardBound_ItemCannotComplete_DefersToNextOccurrence` — Bound-Semantik (weich/hart, `MinStartRemain`)
- `ProgramScheduleOptions_MinStartRemain_Normalizes` — Options-Normalisierung (Muster für eine evtl. neue Option)
- `DeleteDisplacedSlots_RemovesOnlyDisplaced` / `_NonAdmin_Throws`, `ResetSchedule_RemovesAllSlotsAndCursors` — Housekeeping/Auth
- `EnsureCoverage_AdjacentWindows_OvershootCompletesAndShiftsSuccessor`

### Übrige `ProgramScheduleServiceTests_*`-Suiten (Kurzüberblick)

- `_Generation` (13): Recurrence-Arten, Air-Order, Wrap-Around, `SlotDuration`, Lückenlosigkeit, verwaistes Material, `EnsureCoverage_SpanningOccurrence_KeepsTrueStart` (Occurrence über coverageStart behält wahren Beginn), `EnsureCoverage_SecondRun_ExtendsNotDuplicates` (Lookback-Konsumation)
- `_Conflicts` (2): Priorität und Regel-Id-Tiebreak
- `_Access` (4): Publikumsfreigabe der Expansion
- `_ClipSource` (8), `_CursorSelection` (3), `_Diagnosis` (10), `_Filler` (2), `_Genres` (10), `_Trailer` (5)
- `ProgramScheduleWorkerTests` (2): Worker ruft `EnsureCoverage` beim Start / idlet ohne Feature-Flag
- `ProgramGuideServiceTests` / `ProgramGuideControllerTests` / `Components/TVSenderAdminTests` / `ProgramSlotTests_DateTimeKind`: nutzen `ProgramScheduleTestDb.CreateSlotAsync` zum direkten Seeden von Slots (Guide-/UI-Sicht, nicht die Reconcile-Logik)

## Hilfsmethoden

### `ProgramScheduleTestDb` — `VideoWebPlayer.Tests/Helpers/ProgramScheduleTestDb.cs`

Gemeinsame Fixture: **echtes In-Memory-SQLite** (`mode=memory&cache=shared`) mit EF-`EnsureCreated`
und Fremdschlüsseln — kein InMemory-Provider. Seedet `Setup { TvSenderEnabled = true }`.

- `CreateAsync(dbName, ct)` / `Dispose()` — Verbindung + `ApplicationDbContext` (mit `EventManager`)
- `CreateService(scheduleOptions = null)` — `ProgramScheduleService` mit `ProgramRuleService`, `ProgramSettingsService`, `NullLogger`; optionales `IOptions<ProgramScheduleOptions>` (einzige Stelle, über die Tests Optionen steuern)
- `CreateGuideService()` — `ProgramGuideService`
- `CreateSlotAsync(channelId, startUtc, endUtc, mediaItemId, movieId?, episodeId?, ruleId?, ruleType, status)` — **direktes Seeden eines `ProgramSlot`** (inkl. Status `Displaced`); wichtig für einen Reproduktionstest, der ein >24-h-Airing ohne Umweg über `EnsureCoverage` anlegen will — Achtung: `ProgramRuleId`/`RuleType` und MediaItem-/Inhaltsbezüge müssen zur Expansion der Regel passen, sonst ist das Airing aus anderen Gründen invalid
- `CreateMovieAsync(name, sourceId, duration, collectionId, releaseDate)` — Film + MediaItem mit probierter Dauer; `duration` akzeptiert beliebige `TimeSpan` (Präzedenz: 300 min im Boundary-Test — ein >24-h-Item ist damit direkt abbildbar)
- `CreateShowAsync(name, episodeCount, sourceId, duration)` — Serie + Staffel + n nummerierte Episoden à 45 min (Default) mit eigenem MediaItem
- `CreateMediaItemAsync(sourceId, duration)` / `SeedProbeItemAsync(name, duration, probedAt, attempts, ct)` — MediaItems mit Probe-Status
- `CreateGenreAsync(name, sourceId, isHidden, alternateNames, startDate, endDate)` / `LinkMovieGenreAsync` / `LinkShowGenreAsync` — Genre-Material (Interleaved-Stränge)
- `CreateRuleAsync(channelId, name, type, recurrence, timeOfDay, materialType, materialId, priority, episodeCount, slotDuration, dayOfWeek, singleDate, isActive, genreIds, mediaSourceIds)` — Regel direkt in der DB (Validierung umgangen); für >24-h-Szenarien relevant: `slotDuration` erlaubt Fenster ≥ 24 h, `episodeCount` lange Item-Ketten
- `AddUserAsync(email, isAdmin)` / `EnsureSourceAsync(id)` / `GrantSourceAccessAsync` — Benutzer-/Freigabe-Setup
- `GetChannelAsync(ct)` — `GetOrCreateChannelAsync` über `ProgramRuleService`
- `SlotsAsync(ct)` — alle Slots (AsNoTracking, nach Start sortiert); `SaveDiagAsync()` — SaveChanges mit FK-Diagnose
- `AssertGapless(slots, from, to)` — lückenlose, überlappungsfreie Abdeckung `[from, to]`

### Muster für einen Reproduktionstest (Fakten aus dem Bestand)

- Zeiten werden **lokal** konstruiert (`LocalUtc(2026,10,24,h,m)`) und über `TimeZoneInfo.Local`
  konvertiert — dieselbe TZ nutzt der Service intern (`BuildOccurrences`, Z. 1095).
- Der Bug lässt sich end-to-end erzeugen ohne Slots von Hand zu schreiben:
  Regel `Daily` + `TimeOfDay` + ein Item mit Dauer > 24 h (z. B.
  `CreateMovieAsync(duration: TimeSpan.FromHours(30))`, `episodeCount: 1` oder `slotDuration`) →
  `EnsureCoverageAsync` vor der Occurrence schreibt den Slot `[occurrence, +30 h]` →
  `ReconcileAsync` zu einem Zeitpunkt > 24 h nach dem nominalen Occurrence-Start: der Slot wird
  geladen (`End > loadFrom`), aber `earliestStart = occurrenceStarts.Min()` liegt schon auf der
  *nächsten* Occurrence → `airingStart < earliestStart` → `AiringReproducible` bricht bei `k = 0`
  ab → `Displaced`. Aktuell würde `Assert.Equal(0, summary.SlotsDisplaced)` fehlschlagen.
- Alternative: `CreateSlotAsync` seeded den >24-h-Slot direkt (Vorlage: Guide-Tests); kürzer,
  aber weniger end-to-end — die Slot-Felder müssen exakt zur Expansion passen.
- Für den Interleaved-Pfad: `SeedGenreWindowAsync`-Muster (Genre mit ≥ 2 Serien) plus ein Item
  bzw. eine Pick-Kette > 24 h; `AiringReproducibleInterleaved` findet dann keinen
  `occurrenceStart <= airing[0].Start` mehr.
