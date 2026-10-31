# Bestandsaufnahme: Schritt 26 — N23–N25

Analyse des TV-Sender-Bereichs bezogen auf die drei Nachbesserungen N23 (Serienmischung),
N24 (Upcoming-Refresh), N25 (Sender-Freischaltung).

## Zusammenfassung

- **N23 — Ursache gefunden:** `RuleState.SetItems` (`ProgramScheduleService.cs` ~Z. 883-912)
  bildet Stränge in **Reihenfolge des ersten Auftretens** im Item-Ring — `ExpandGenreAsync`
  liefert Episoden `ORDER BY Show.Name`, also strand 0 = „other" (Filme zuerst), dann Serien
  alphabetisch. `StrandStartOffset` liefert bei `wanted >= m` (EpisodeCount ≥ Strangzahl und
  alle SlotDuration-Regeln) immer 0 → dieselbe Strangreihenfolge an jeder Position jedes Tags.
  Cursors sind pro `StrandKey` persistiert — eine Permutation der Strangreihenfolge ist
  cursor-kompatibel. `string.GetHashCode` ist prozess-zufällig → eigener stabiler Hash nötig.
- **N24 — zwei reale Defekte:**
  1. `_pollTask = PollLoopAsync()` startet schon in `OnInitializedAsync` → die beim
     Prerender erzeugte Wegwerf-Instanz startet ebenfalls eine Schleife, die nie disposed
     wird (Timer-Leak, HTTP-Fehlversuche alle 15 s).
  2. `RefreshGuideAsync` koppelt `now`- und `upcoming`-Abruf in einem try — schlägt der
     `now`-Request dauerhaft fehl, friert `upcoming` trotz laufendem Poll komplett ein
     (genau das gemeldete Symptom). Beide Abrufe müssen unabhängig aktualisieren.
- **N25 — alle Bausteine vorhanden:** Persistente Settings laufen über `Setup`-Entität +
  `ProgramSettingsService` (Muster `MdnsAdvertisementEnabled`); Admin-Formular
  `ProgramSettings.razor` (`UpdateGeneralSettingsAsync`/`GeneralSettingsUpdate`); NavMenu
  zeigt `/live-tv` für alle Angemeldeten; `/admin/tv-sender` + `LiveTv.razor` prüfen nur
  `isAdmin`/Auth, nicht ein Feature-Flag; Controller `ProgramGuideController` +
  `LiveStreamController` prüfen nur `CheckLoggedIn()`; Worker `ProgramScheduleWorker`,
  `LiveStreamWorker`, `MediaDurationProbeWorker` laufen ungeprüft. `MediaItem.Duration`
  wird nur vom Sender konsumiert → Probe-Worker darf pausieren.
- **Test-Infrastruktur:** `ProgramScheduleTestDb` (echtes SQLite, `EnsureCreated`),
  bUnit für Komponenten, `FakeAuthService`, WebApplicationFactory-E2E
  (`DeviceClientTestBase`/`PlaylistsE2ETestBase`), `TimeProvider.System` ist DI-registriert
  (`ServiceCollectionExtensions.cs:271`) → `PeriodicTimer(period, timeProvider)` testbar.
- **Betroffene Alttests:** `NavMenuLiveTvTests` (Link muss bei Flag aus verschwinden),
  `LiveTvTests`/`TVSenderAdminTests` (DI-Service `ProgramSettingsService` nötig),
  `ProgramScheduleWorkerTests`/`MediaDurationProbeWorkerTests` (seeds `Setup` ohne Flag),
  `ProgramGuideControllerTests`/`LiveStreamControllerTests` (Controller-Konstruktor ändert
  sich), E2E `ProgramGuideE2ETests`/`LiveStreamE2ETests`/`LiveTvE2ETests`/
  `TVSenderAdminE2ETests` (Flag in Test-DB setzen),
  `ProgramScheduleServiceTests_Interleaving.StableSlotsAcrossDays` (fixierte A/B-Position).

Test-Ausgangszustand: siehe [tests.md](inventory/tests.md) — voller Lauf steht noch aus
(Build vorab wegen möglichem App-Prozess-Lock auf `bin/Debug` mit Scratch-OutDir).

## Details

- [Logik / Stränge & Expansion](inventory/logic.md)
- [Settings & Worker](inventory/settings-workers.md)
- [UI & Endpunkte](inventory/ui-endpoints.md)
- [Tests](inventory/tests.md)
