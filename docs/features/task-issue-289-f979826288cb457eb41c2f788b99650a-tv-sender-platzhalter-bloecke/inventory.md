# Bestandsaufnahme: Platzhalter-Blöcke im Sendeplan kompakt darstellen

Analysiert wurden das `ProgramSlot`-Datenmodell, die Clip-Füll-Erzeugung in
`ProgramScheduleService`, die Admin-Sendeplan-Anzeige `TVSenderAdmin.razor`, die öffentliche
Programmauskunft (`ProgramGuideService`/`LiveTv.razor`) sowie die zugehörige
Testinfrastruktur — bezogen auf die rein darstellerische Zusammenfassung zusammenhängender
Clip-Füll-Läufe zu einem Block-Eintrag mit Detailansicht (Issue #289,
`requirement.md`).

## Zusammenfassung

- **Clip-Füll-Slots sind persistiert nicht markiert.** `FillAiringSegments`
  (`ProgramScheduleService`, Z. 2603) erzeugt Clip-`ProgramSlot`s mit `RuleType =
  rule.RuleType` der erzeugenden Regel (nicht `Filler`) und `ProgramRuleId = rule.Id`;
  Trailer-Clips tragen zusätzlich `MovieId`. Die einzige heutige Erkennung ist die
  `clipIds`-Laufzeitklassifikation (`ClipMediaItemIds`, Z. 523): `MediaItemId` ∈
  Clip-Gruppen-Ringe der Regel. In der Anzeige (Razor und DTO) existiert **keinerlei**
  Clip-Erkennung — eine Blockbildung braucht entweder ein neues denormalisiertes Kennzeichen
  (Variante A: Migration + `OptionalRestoreColumns` + Alt-Backup-Test) oder Laufzeit-Lookup
  über `ProgramClipGroupItems` ⨝ `ProgramRuleClipGroups` (Variante B).
- **Der Admin-Sendeplan rendert eine Karte je `ProgramSlot`** (`#tvsender-schedule-cards`,
  Z. 420–475): Tages-Header je lokalem Kalendertag, `tvsender-rule-divider` bei
  `ProgramRuleId`-Wechsel, Karte mit LIVE-Badge/Fortschritt (`IsRunning`/`ProgressPercent`),
  `ScheduleCardContent`-View-Model je `slot.Id` (Titelauflösung per EF, Bare-Item-Slots →
  `MediaItem.Name` = Clip-Dateiname). Datenquelle `GetScheduleAsync(channelId, UtcNow-2h,
  200)` liefert flache Liste inkl. `Displaced` — die Gruppierung muss beim Anzeigeaufbau
  entstehen; `maxCount` 200 begrenzt bei vielen Kurzclips den sichtbaren Horizont.
- **Overlay-Detailansicht: Muster vorhanden.** `admin-dialog-overlay`/`admin-dialog` wird
  auf derselben Seite sechsmal verwendet (Regel-Editor + 5 Bestätigungsdialoge, Z. 176–219,
  843–923); `aria-label`-Buttons sind projektüblich.
- **Öffentliche Ansicht (scope-offen):** `LiveTv.razor` (`#live-tv-upcoming`) zeigt ebenfalls
  einen Eintrag je Slot über `IProgramGuideApiClient` → `ProgramGuideController` →
  `ProgramGuideService.GetUpcomingAsync` (nur `Scheduled`). `DtoProgramGuideEntry.IsFiller`
  trifft Clip-Füll-Slots **nicht** — Client-seitige Erkennung wäre unzuverlässig.
  **Randbedingung bestätigt:** `LiveStreamCoordinator` (Z. 99) nutzt
  `GetNowAsync` für die Auslieferung — dort darf nicht gruppiert werden.
- **Plan-Logik bleibt unangetastet:** `EnsureCoverageAsync`/`ReconcileAsync`,
  `GroupAirings`/`AiringReproducible`/`RewindCursor` und `ProgramRuleCursor` sind bereits
  clip-bewusst (`clipIds`-Split) — die Anforderung verlangt ausdrücklich keine Änderung daran.
- **Backup-Relevanz:** `ProgramSlots` steht bereits in `OptionalRestoreTables`,
  `ProgramSlots.Status` in `OptionalRestoreColumns` mit Int-Default — eine neue Spalte muss
  dort ergänzt werden (Muster-Regressionstests vorhanden:
  `VideoWebPlayerBackupDataTests_ProgramRuleLinks`, `…_ProgramScheduleContentScope`,
  `LegacyBackupArchiveBuilder`).
- **Testinfrastruktur vollständig:** `ProgramScheduleTestDb` (echtes SQLite-InMemory) mit
  `CreateClipGroupAsync`/`AddClipToGroupAsync`/`SeedTrailerClipGroupAsync`/`CreateSlotAsync`/
  `CreateRuleAsync(clipGroupIds, placeholderGrace, slotDuration)`; bUnit-Kontextfabrik
  `TVSenderAdminTests.CreateContextAsync`; Playwright-Basis `TVSenderAdminE2ETestBase`.
  **Lücke:** kein Seed-Helfer/kein Kennzeichen, das einen Slot als Clip-Füllung ausweist —
  Tests erzeugen heute entweder echte Clip-Füll-Airings über `EnsureCoverageAsync`
  (`SeedClipAiringAsync`-Muster) oder klassifizieren per `clipIds` der geseedeten Items.

**Test-Ausgangszustand:** Voller Baseline-Lauf `dotnet test` (Debug, 5,25 min): **2513 Tests —
2510 bestanden, 1 fehlgeschlagen, 2 übersprungen.** Der eine Fehlschlag
(`MediaBoxContextMenuInteractionE2ETests.PointerReleaseBeforeDelay_DoesNotOpenMenu_AndNavigatesNormally`,
Playwright-Timing) ist ein dokumentierter Flaky-Ausfall — zweimal isoliert wiederholt, beide
grün; fachlich außerhalb dieser Anforderung. Beide Skips sind beabsichtigt (harter `Skip` bei
bekanntem Alt-Befund; Unix-only-Test unter Windows). Nachweis:
[inventory/tests.md](inventory/tests.md).

## Details

- [Datenmodell](inventory/models.md) — `ProgramSlot`, `ProgramClipGroup(Item)`,
  `ProgramRuleClipGroup`, `ProgramRule`, EF-Konfiguration, `VideoWebPlayerBackupData`
- [Logik](inventory/logic.md) — `ProgramScheduleService` (Clip-Füll-Machinerie,
  `clipIds`-Klassifikation), `ProgramGuideService`, `ProgramGuideController`,
  `LiveStreamCoordinator`-Randbedingung, `TVSenderAdmin`/`LiveTv` Code-Behind
- [Enums](inventory/enums.md) — `ProgramRuleType`, `ProgramSlotStatus`,
  `ProgramRuleMaterialType`
- [Interfaces](inventory/interfaces.md) — `IProgramScheduleService`, `IProgramGuideService`,
  `IProgramGuideApiClient`, `DtoProgramGuideEntry`, `IProgramScheduleSignal`
- [UI](inventory/ui.md) — Sendeplan-Render-Loop, Karten-/Badge-/Dialog-Muster,
  `LiveTv`-Upcoming-Liste, CSS-Fundorte
- [Tests](inventory/tests.md) — Test-Ausgangszustand, relevante Testklassen und
  Hilfsmethoden
