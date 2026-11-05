# Tasks: Sendeplan — Bündelung scope-freier Commit-Weckrufe (Issue #337)

| # | Bereich | Aufgabe | Status | Testnachweis |
|---|---------|---------|--------|--------------|
| 1 | Konfiguration | `ProgramScheduleOptions` um `ScheduleChangedDebounceSeconds` (Standard 5) und `EffectiveScheduleChangedDebounce` (Normalisierung: <0 → 5 s, 0 → sofort, >0 → Wert) erweitern | Offen | — |
| 2 | Konfiguration | `appsettings.json` Sektion `ProgramSchedule` um `ScheduleChangedDebounceSeconds` ergänzen | Offen | — |
| 3 | Logik | `IProgramScheduleSignal` um `NotifyScheduleChangedNow()` erweitern (Sofort-Pfad, Scope-Suppression respektierend) | Offen | — |
| 4 | Logik | `ProgramScheduleSignal`: optionalen Konstruktor (`IOptions<ProgramScheduleOptions>?`, `TimeProvider?`) und Bündelzustand hinzufügen | Offen | — |
| 5 | Logik | `ProgramScheduleSignal.NotifyScheduleChanged()` auf festes Bündelfenster umstellen (Scope-Pfad und Sofort-Modus bei 0 unverändert) | Offen | — |
| 6 | Logik | `ProgramScheduleSignal.NotifyScheduleChangedNow()` implementieren (sofortige Zustellung beider Kanäle, Verbrauch ausstehender Bündelung, Scan-Suppression) | Offen | — |
| 7 | Logik | `ProgramScheduleSignal`: Flush-Logik implementieren (Fensterablauf über `TimeProvider`, Überführung in `_suppressedNotifications` bei offenem Scan, `EndScan` verbraucht ausstehende Bündelung) | Offen | — |
| 8 | UI | `TVSenderAdmin.razor` `GenerateNowAsync` auf `ScheduleSignal.NotifyScheduleChangedNow()` umstellen | Offen | — |
| 9 | Tests | `GatedScheduleSignal` (Test-Double in `MediaDurationProbeWorkerTests`) um `NotifyScheduleChangedNow()` erweitern und `_inner` mit `ProgramScheduleOptions { ScheduleChangedDebounceSeconds = 0 }` konstruieren (Sofort-Modus) | Offen | — |
| 10 | Tests | `FakeScheduleSignal` (Test-Double in `MediaDurationProbeServiceTests`) um `NotifyScheduleChangedNow()` erweitern | Offen | — |
| 11 | Tests | `ProgramScheduleSignalTests.Notify_WakesWaiter` und `Notify_IsCoalesced` auf Sofort-Modus (`ScheduleChangedDebounceSeconds = 0`) umstellen | Offen | — |
| 12 | Tests | Neue Tests: `Notify_OutsideScan_WakesAfterDebounceWindow`, `Notify_BurstWithinWindow_WakesOnce`, `Notify_AfterWindowDelivery_StartsNewWindow` (mit `ControllableTimeProvider`) | Offen | — |
| 13 | Tests | Neue Tests: `Notify_BatchedWake_AlsoFeedsLiveChannel`, `DebounceDisabled_DeliversImmediately` | Offen | — |
| 14 | Tests | Neue Tests: `NotifyScheduleChangedNow_WakesImmediately`, `NotifyScheduleChangedNow_DuringScan_IsSuppressed`, `NotifyScheduleChangedNow_ConsumesPendingBatch` | Offen | — |
| 15 | Tests | Neuer Test: `PendingBatch_FlushedDuringScan_IsSuppressedUntilScanEnds` | Offen | — |
| 16 | Tests | Neuer Test: `EndScan_Wake_ConsumesPendingBatch_LateFlushDoesNotWake` (Interleaving: Meldung vor `BeginScan` + unterdrückte Meldung im Scan + `EndScan`-Wecken + späterer Flush ohne doppeltes Wecken) | Offen | — |
| 17 | Dokumentation | `docs/help/tv-sender-ablauf-technisch.md` Abschnitt „Gebündelte Auslöse-Signale" (Z. 937–960) anpassen: Bündelfenster, `NotifyScheduleChangedNow`-Sofort-Pfad, Mitbündelung der Live-Leitung, Option `ProgramSchedule:ScheduleChangedDebounceSeconds` | Offen | — |
| 18 | Dokumentation | `docs/RELEASE_NOTES.md`-Eintrag zur Bündelung scope-freier Sendeplan-Weckrufe ergänzen (Issue #337, Muster der Einträge zu #290/#315) | Offen | — |
| 19 | Tests | Betroffene Bestandstests verifizieren (`TVSenderAdminTests`, `MediaSourceScanServiceTests`, `MediaSourceScanCoordinatorScopeTests`, `MediaDurationProbeWorkerTests`, E2E-Klassen `TVSenderAdminE2ETests` u. a.) sowie volle Suite und Debug-/Release-Build | Offen | — |
