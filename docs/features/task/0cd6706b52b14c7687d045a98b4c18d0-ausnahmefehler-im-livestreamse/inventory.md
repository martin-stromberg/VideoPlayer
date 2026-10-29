# Bestandsaufnahme: Ausnahmefehler im LiveStreamSegmenter

Bestandsaufnahme des bestehenden Codes zur Anforderung, dass ein erwarteter Pipe-Bruch
(`System.IO.IOException: Broken pipe`) beim geplanten Stop des ffmpeg-Live-Stream-Segmentierers
keinen irreführenden Ausnahme-Stacktrace mehr ins Protokoll schreiben soll.

## Zusammenfassung

- Der beanstandete Codepfad existiert genau wie beschrieben: `SegmenterProcessSupport.PumpInputAsync`
  (`VideoWebPlayer/Services/SegmenterProcessSupport.cs`, Zeilen 123–154) fängt
  `IOException`/`ObjectDisposedException`/`OperationCanceledException` und übergibt das
  Ausnahmeobjekt per `logger.LogDebug(ex, "[{Caller}] Eingabe-Pump beendet.", caller)`
  (Zeile 142) ans Logging — damit landet der volle Stacktrace im Debug-Protokoll.
- Der Pump-`Task` wird in `FfmpegLiveStreamSegmenter.StartAsync` (Zeile 180) als
  Fire-and-forget gestartet (`_ = Task.Run(...)`, `CancellationToken.None`); die Referenz
  wird verworfen. `StopCoreAsync` (Zeilen 202–229) killt den Prozess, ohne die Pumpe
  einzusammeln — die Pumpe kann über den Job-Wechsel hinaus laufen (das im Protokoll
  beobachtete Verhalten).
- `LiveStreamStandbySegmenter` nutzt denselben Helper, holt seinen Pump-Task aber bereits
  zeitgebunden ein (`PumpDrainTimeout` = 10 s, Zeilen 53, 158, 206–207).
- Das Unterscheidungsmerkmal „echter Fehler vs. erwarteter Stop" ist vorhanden:
  `SegmenterPumpOutcome` (`Completed`/`SourceUnavailable`/`Failed`), unerwartete Prozessenden
  laufen über `ReportProcessExit` → Warnung + `LastUnexpectedExit`; echte Pump-Fehler bleiben
  `LogWarning` mit `SegmenterPumpOutcome.Failed`.
- Der `pipe:0`-Pfad ist der einzige mit Pumpe: `LiveStreamInput.IsPipe` ⇒ `OpenStreamAsync`
  gesetzt; erzeugt nur über `LiveStreamInput.ForStream`, im Produktivpfad ausschließlich in
  `LiveStreamMediaResolver.ResolveAsync` (Zeile 132) als Fallback für nicht-lokale Quellen.
- Tests: Für `SegmenterProcessSupport` existiert nur ein `StartRealProcess`-Test —
  `PumpInputAsync` (Log-Verhalten beim Pipe-Bruch) ist **nicht** getestet. Für
  `FfmpegLiveStreamSegmenter` existieren 14 Unit-Tests mit austauschbarem
  `ISegmenterProcess`-Fake (inkl. Pipe-Eingabe), aber kein Test zum Pump-/Log-Verhalten beim
  Stop. Der Helfer `ListLogger<T>` zeichnet Level + Nachricht + Ausnahme-Text auf und eignet
  sich für den geforderten Nachweis.

Test-Ausgangszustand: Hauptsuite (`Category!=E2E`) 2444 von 2445 Tests bestanden, 0 Fehlschläge,
1 Übersprungener (OS-bedingt). Feature-bezogener E2E-Lauf (`FfmpegLiveStreamSegmenterE2ETests`,
echtes ffmpeg): 2/2 bestanden. Details in [Tests](inventory/tests.md).

## Details

- [Logik](inventory/logic.md)
- [Enums](inventory/enums.md)
- [Interfaces](inventory/interfaces.md)
- [Tests](inventory/tests.md)
