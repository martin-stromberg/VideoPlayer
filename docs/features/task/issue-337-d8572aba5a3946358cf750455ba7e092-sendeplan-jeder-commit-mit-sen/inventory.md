# Bestandsaufnahme: Sendeplan — jeder Commit mit sendeplan-relevanten Entitäten weckt sofort (Issue #337)

Bestandsaufnahme des Weckpfads `NotifyScheduleChanged()` → `ProgramScheduleSignal` → `ProgramScheduleWorker`/`LiveStreamWorker` bezogen auf die Anforderung, schnell aufeinanderfolgende Einzel-Commits außerhalb von `BeginScan`-Scopes zu einem Plan-Lauf zu bündeln (oder das Ist-Verhalten bewusst zu dokumentieren).

## Zusammenfassung

- **Das Signal existiert und ist der zentrale Punkt:** `ProgramScheduleSignal` (`VideoWebPlayer/Services/ProgramScheduleSignal.cs`) ist ein Singleton mit zwei koaleszierenden 1-Slot-Channels (`_channel` für `WaitAsync`/`ProgramScheduleWorker`, `_liveChannel` für `WaitLiveAsync`/`LiveStreamWorker`) plus Scan-Zählern `_activeScans`/`_suppressedNotifications`. `NotifyScheduleChanged()` (Z. 83–101) hat außerhalb eines Scopes **keinerlei Zeitkomponente**: jeder Aufruf schreibt sofort in beide Kanäle — ein Debounce-/Batch-Mechanismus existiert nicht und wäre neu.
- **Kanal-Koaleszierung bündelt bereits — aber nur solange niemand liest:** Durch `Bounded(1)`/`DropWrite` fallen schnelle Signalfolgen zu einem Kanaleintrag zusammen. Sobald der Worker aber nach dem ersten Weckruf einen `ExecutePassAsync`-Lauf abgearbeitet und wieder in `WaitAsync` liegt, löst jeder weitere Commit einen **eigenen** Lauf aus — genau das beschriebene Ist-Verhalten.
- **Aufrufer-Landschaft ist vollständig sichtbar:** Der Commit-Hook `ApplicationDbContext.PlaylistBackfillMarking.cs` feuert in `SaveChanges(bool)` (Z. 85, 94) und `SaveChangesAsync` (Z. 133, 142) nur bei geschriebenen Backfill-Markern; `DeleteMediaSourceAsync` feuert nach dem Commit (`ApplicationDbContext.cs` Z. 448); `ProgramRuleService` (4 Stellen), `ProgramClipGroupService` (5 Stellen), `MediaDurationProbeService.ProbePendingAsync` (1× je Aufruf, Z. 128), `TVSenderAdmin.razor` (3 Stellen: „Jetzt generieren" Z. 1541, Reset Z. 1590, `SaveChannelSettingsAsync` Z. 2028), `ProgramSettings.razor` (Z. 234). Eine zentrale Bündelung im Singleton würde alle Aufrufer ohne eigene Änderung erfassen.
- **Live-Leitung wird mitgespeist:** `NotifyScheduleChanged` schreibt immer auch `_liveChannel` — eine Bündelung müsste entscheiden, ob der `LiveStreamWorker` bei Eingabeänderungen ebenfalls verzögert wird (offene Frage 2 der Anforderung). `NotifyLiveScheduleChanged` (materialisierte Änderungen, Issue #315) ist ein getrennter, unmittelbarer Pfad und bleibt außen vor.
- **Referenzmuster vorhanden:** Das Scope-Bündeln via `BeginScan`/`EndScan` läuft bereits in `MediaSourceScanCoordinator` (Issue #333) und `MediaDurationProbeWorker` (Issue #290); `PlaylistBackfillSignal` zeigt dasselbe Grundmuster mit abweichender Weckbedingung. „Jetzt generieren" in `TVSenderAdmin.razor` wartet aktuell fest 4 s und lädt dann neu — eine verzögerte Zustellung würde diese Warte ggf. mit dem Bündelfenster überlagern (offene Frage 3).
- **Konfigurationsvorlagen vorhanden:** `ProgramSettingsService` hält Sender-Laufzeitparameter datenbankbasiert (`ProgramScheduleIntervalMinutes`, Default 15 min); `MediaProbeOptions`/`ProgramScheduleOptions` zeigen das Options-Muster mit `Effective*`-Normalisierung. Eine Debounce-Dauer hat derzeit keinen Platz in der Konfiguration.

Test-Ausgangszustand: Volle Suite `dotnet test VideoWebPlayer.Tests/VideoWebPlayer.Tests.csproj` — **2906 Tests: 2903 bestanden, 1 fehlgeschlagen, 2 übersprungen** (Exit-Code 1). Der eine Fehlschlag (`PlaylistPlaybackE2ETests.PlaylistSkipLockedEntriesE2ETest`, Playwright-Timeout) ist ein nicht anforderungsbezogener, im Einzel-Nachlauf bestandener Flaky-E2E; die zwei Skips sind ein dokumentierter bewusster Skip und ein Unix-only-Test. Details und Nachweise: [Tests](inventory/tests.md).

## Details

- [Datenmodell](inventory/models.md)
- [Logik](inventory/logic.md)
- [Enums](inventory/enums.md)
- [Interfaces](inventory/interfaces.md)
- [Tests](inventory/tests.md)
