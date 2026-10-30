# Code-Review

## Ergebnis

**Status:** Befunde vorhanden

Geprüft wurde `git diff 8a6b914..HEAD` (Feature-Commits inkl. der vier
Nachbesserungs-Commits `bd76cc2`, `c5472d5`, `0d4cfef`, `2ddf2ea` der
Iteration 3).

**Verifikation der Iteration-2-Befunde:**

- **Befund 1 (Eskalationsgedächtnis, `bd76cc2`) — NICHT wirksam behoben.**
  Die Logik in `SynchronizeStreamAsync` ist an sich korrekt, aber das
  Gedächtnis (`_inputFailureSlotId`/`_slotHttpInputFailed`/`_slotSftpInputFailed`)
  liegt in Instanzfeldern des **scoped** registrierten Koordinators, den der
  Worker pro Tick in einem neuen Scope auflöst — die Flags überleben keinen
  Tick, die Kaskade verhält sich produktiv exakt wie vor dem Fix
  (siehe Befund 1 unten; per Mutationsprobe nachgewiesen).
- **Befund 2 (sftp-Transport-Vorrang im ffprobe-Probe, `c5472d5`) — korrekt
  behoben:** `FfprobeMediaDurationProbe.cs:191`–198 wertet für `scheme ==
  "sftp"` `ContainsSftpInputError` vor der Demux-Prüfung und liefert
  `TransientFailure`; Reihenfolge Unsupported-Scheme → HTTP → sftp-Transport →
  Demux ist konsistent zum Segmenter; `scheme` wird für Datei-/Pipe-Probes mit
  `null` übergeben (Check greift dort nicht).
- **Befund 3 (Pump-Ergebnis vor Klassifikation, `0d4cfef`) — korrekt
  behoben:** `ReportProcessExit` ist jetzt `async` und wird im Drain-Task
  awaited (`FfmpegLiveStreamSegmenter.cs:274`); das Pump-Ergebnis wird
  gebunden eingeholt (`_pumpDrainTimeout` = `PumpDrainTimeout` = 10 s —
  sinnvoll: ein Pump des beendeten Prozesses kann nur noch auf den Pipe-Bruch
  oder eine stallende `source.ReadAsync` warten; die Schranke verzögert die
  Diagnose um höchstens 10 s, weit unter dem 30-s-Poll-Takt, und blockiert
  keinen Aufrufer). Ein unbekannter/fehlgeschlagener Pump-Ausgang wertet
  Demux-Marker bei Pipe-Eingang nicht mehr als Eingangsfehler
  (`pumpResult is { Outcome: Completed }`, Z. 342–345). Kein Deadlock-Risiko:
  das Warten läuft im Fire-and-forget-Drain-Task, und `StopCoreAsync`
  verlässt den Pfad über den `ReferenceEquals`-Check bereits vor dem Await.
- **Befund 4 (Standby-Kaskade bis pipe:0, `2ddf2ea`) — korrekt behoben:**
  `PrepareAsync` (`LiveStreamStandbyProvider.cs:242`–312) läuft die Kaskade
  HTTP → sftp → pipe:0 in einer Schleife; jede gescheiterte Stufe wird über
  die lokalen Flags `useHttpInput`/`useSftpInput` für die Neuauflösung
  deaktiviert (Flags werden je `PrepareAsync`-Aufruf frisch auf `true`
  initialisiert), die Schleife terminiert garantiert (monoton fallende Flags;
  Datei-/Pipe-Eingang = Endstufe), die Fallback-Info nennt beide
  `InputKindLabel`s.

**Tests:** 218 betroffene Tests grün (Debug-Build fehlerfrei).
**Mutationsprobe:** Ein Test, der — wie die Produktion — je Tick einen
frischen `LiveStreamCoordinator` bei geteiltem Segmenter aufsetzt, schlägt
fehl: nach HTTP-Transportfehler (Tick 1 → sftp) und sftp-Transportfehler
(Tick 2 → HTTP) wählt Tick 3 nach erneutem HTTP-Fehlschlag wieder **sftp**
statt `pipe:0` — die Alternation aus Runde-2-Befund-1 besteht produktiv fort.
(Probe anschließend sauber revertiert.)

## Befunde

### LiveStreamCoordinator.cs (LiveStreamCoordinator)

- **Logik / Fix wirkungslos durch Objektlebensdauer** — `_inputFailureSlotId`,
  `_slotHttpInputFailed`, `_slotSftpInputFailed` (Z. 59–61) sind
  Instanzfelder, aber `LiveStreamCoordinator` ist per
  `AddScoped<LiveStreamCoordinator>()` registriert
  (`Extensions/ServiceCollectionExtensions.cs:252`) und `LiveStreamWorker`
  löst ihn je Tick in einem frischen Scope auf (`LiveStreamWorker.cs:92`,
  `using var scope = _scopeFactory.CreateScope();` in der Poll-Schleife).
  Jede Tick-Instanz startet mit `null`/`false` — die Kumulierung über
  aufeinanderfolgende Ticks, die der Commit `bd76cc2` einführen wollte, findet
  produktiv nie statt; `useHttpInput`/`useSftpInput` werden faktisch weiter
  nur aus dem letzten `LastUnexpectedExit` abgeleitet (das überlebt als
  Zustand des Singleton-Segmenters). HTTP- und sftp-Fehlschlag desselben
  Sendeplatzes alternieren daher weiter endlos; `pipe:0` wird nur über den
  `SftpInputSupported`-Latch erreicht — exakt der Runde-2-Befund.
  Mutationsprobe bestätigt: drei Ticks mit frischem Koordinator (HTTP-Fail →
  sftp-Fail → HTTP-Fail) ergeben erneut einen sftp-Job statt `pipe:0`.

  Empfehlung: Das Eskalationsgedächtnis in einen Singleton verlagern —
  naheliegend in `FfmpegLiveStreamSegmenter` neben `SftpInputSupported`/
  `LastUnexpectedExit` (z. B. eine per Slot geführte Fehlstufen-Maske, die
  `ReportProcessExit` mitpflegt und beim Slotwechsel/Jobstart verwirft), als
  eigenes kleines Singleton-State-Objekt, oder in `LiveStreamExitInfo`
  mitgeführt. **Nicht** geeignet: den Koordinator zum Singleton machen (er
  hängt an scoped `LiveStreamMediaResolver`/`LiveStreamSegmentStore`/
  `ProgramSettingsService`/DbContext) oder den Worker-Scope über Ticks
  halten (hielte den DbContext dauerhaft). Der bestehende
  Slot-Wechsel-Reset (Z. 139–144) ist ansonsten korrekt modelliert.

### LiveStreamCoordinatorTests.cs (LiveStreamCoordinatorTests)

- **Testqualität / Produktionslebensdauer nicht abgebildet** —
  `SynchronizeAsync_AlternatingHttpAndSftpFailures_EscalatesToPipe` (Z. 456),
  `SynchronizeAsync_PipeExitAfterHttpFailure_StaysOnPipeInput` (Z. 486) und
  `SynchronizeAsync_InputFailureMemory_ResetsOnSlotChange` (Z. 509) rufen
  `SynchronizeAsync` mehrfach auf **derselben** Koordinator-Instanz auf
  (`_coordinator` aus `InitializeAsync`). Damit prüfen sie Instanzzustand,
  den die Produktion nicht hat (je Tick neue Instanz), und decken genau das
  Wirksamkeitsproblem des Fixes nicht auf — die Tests sind „grün über einer
  Lebensdauer, die es produktiv nicht gibt".

  Empfehlung: Nach Verlagerung des Gedächtnisses in einen Singleton einen
  produktionsnahen Test ergänzen, der pro „Tick" eine neue
  Koordinator-Instanz bei geteiltem Segmenter-State verwendet (oder einen
  Worker-Level-Test über den echten `IServiceScopeFactory`-Pfad, der mehrere
  Ticks fährt — die Infrastruktur dafür existiert bereits in
  `LiveStreamWorkerTests`).

### FfmpegLiveStreamSegmenter.cs (FfmpegLiveStreamSegmenter)

- **Race Condition (gering)** — `ReportProcessExit` (Z. 297–349): zwischen
  dem `ReferenceEquals(_process, process)`-Check (Z. 297) und dem Schreiben
  von `_lastUnexpectedExit` (Z. 347) liegt jetzt das gebundene Pump-Warten
  (bis `_pumpDrainTimeout` = 10 s). Läuft in diesem Fenster
  `StopCoreAsync`/`StartAsync` (Slotwechsel, Drift-Neustart, manueller Stop),
  wird `_lastUnexpectedExit` dort auf `null` zurückgesetzt und ein neuer Job
  gestartet — die verspätete Klassifikation des alten Prozesses überschreibt
  den geleerten Zustand danach wieder mit einer Diagnose, die nicht mehr zum
  laufenden Prozess gehört. Der Schaden ist durch den `SlotId`-Filter des
  Koordinators begrenzt (fremde Slots werden ignoriert; für denselben Slot
  ist die gemeldete Fehlstufe meist ohnehin zutreffend), aber unnötig; die
  Race-Klasse existierte vorher schon, das Await hat das Fenster vergrößert.

  Empfehlung: Nach dem gebundenen Await erneut
  `ReferenceEquals(_process, process)` prüfen und `_lastUnexpectedExit` nur
  schreiben, wenn der Prozess noch der aktuelle ist (alternativ den Write
  gegen denselben `_gate`-Semaphore wie `StopCoreAsync` absichern — der
  einfache Re-Check reicht fachlich aus).

## Geprüfte Dateien

- `VideoWebPlayer/Configuration/LiveStreamOptions.cs`
- `VideoWebPlayer/Extensions/ServiceCollectionExtensions.cs` (referenziert,
  DI-Lebensdauern)
- `VideoWebPlayer/Services/FfmpegErrorMarkers.cs`
- `VideoWebPlayer/Services/FfmpegLiveStreamSegmenter.cs`
- `VideoWebPlayer/Services/FfprobeMediaDurationProbe.cs`
- `VideoWebPlayer/Services/ILiveStreamSegmenter.cs`
- `VideoWebPlayer/Services/InternalMediaStreamTokenService.cs`
- `VideoWebPlayer/Services/LiveStreamCoordinator.cs`
- `VideoWebPlayer/Services/LiveStreamExitInfo.cs`
- `VideoWebPlayer/Services/LiveStreamInput.cs`
- `VideoWebPlayer/Services/LiveStreamMediaResolver.cs`
- `VideoWebPlayer/Services/LiveStreamStandbyProvider.cs`
- `VideoWebPlayer/Services/LiveStreamStandbySegmenter.cs`
- `VideoWebPlayer/Services/LiveStreamWorker.cs`
- `VideoWebPlayer/Services/MediaDurationProbeService.cs`
- `VideoWebPlayer/Services/SegmenterProcessSupport.cs`
- `VideoWebPlayer/Services/SftpProbeUriBuilder.cs`
- `VideoWebPlayer/appsettings.json`
- `VideoWebPlayer.Tests/Helpers/FailingReadStream.cs`
- `VideoWebPlayer.Tests/Helpers/TruncatingReadStream.cs`
- `VideoWebPlayer.Tests/LiveStreamCoordinatorTests.cs`
- `VideoWebPlayer.Tests/LiveStreamWorkerTests.cs`
- `VideoWebPlayer.Tests/Services/FfmpegErrorMarkersTests.cs`
- `VideoWebPlayer.Tests/Services/FfmpegLiveStreamSegmenterE2ETests.cs`
- `VideoWebPlayer.Tests/Services/FfmpegLiveStreamSegmenterTests.cs`
- `VideoWebPlayer.Tests/Services/FfprobeMediaDurationProbeTests.cs`
- `VideoWebPlayer.Tests/Services/InternalMediaStreamTokenServiceTests.cs`
- `VideoWebPlayer.Tests/Services/LiveStreamMediaResolverTests.cs`
- `VideoWebPlayer.Tests/Services/LiveStreamStandbyProviderTests.cs`
- `VideoWebPlayer.Tests/Services/LiveStreamStandbySegmenterTests.cs`
- `VideoWebPlayer.Tests/Services/SegmenterProcessSupportTests.cs`
- `docs/help/tv-sender.md`, `docs/help/tv-sender-ablauf-technisch.md`,
  `docs/help/videolaufzeit.md`, `docs/RELEASE_NOTES.md`, Tasks-Liste
