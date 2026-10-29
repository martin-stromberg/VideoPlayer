# Übersetzte Anforderung — Ausnahmefehler im LiveStreamSegmenter

Quelle: Kundenanforderung (Protokollauszug mit `System.IO.IOException: Broken pipe` aus
`SegmenterProcessSupport.PumpInputAsync`).

## Fachliche Zusammenfassung

Beim Sendeplatz-Wechsel des TV-Sender-Live-Streams erscheint im Protokoll eine
`System.IO.IOException: Broken pipe` mit vollständigem Stacktrace, ausgelöst in
`SegmenterProcessSupport.PumpInputAsync` (`VideoWebPlayer/Services/SegmenterProcessSupport.cs`,
`source.CopyToAsync(process.StandardInput, ...)`). Die aus dem Code ableitbare Ursache: Die
Eingabe-Pumpe des beendeten Sendeplatzes läuft als Fire-and-forget-`Task` weiter
(`FfmpegLiveStreamSegmenter.StartAsync`, `_ = Task.Run(() => PumpInputAsync(process, job))`),
während `StopCoreAsync` den ffmpeg-Prozess killt; der nächste Schreibzugriff auf die stdin-Pipe
des toten Prozesses schlägt fehl. Im Protokollauszug endet die Pumpe von Sendeplatz 18866
erst nach dem Start von Sendeplatz 18867. Der Fehler ist erwartetes Verhalten und wird bereits
als `SegmenterPumpOutcome.Completed` behandelt — beanstandet wird der per `LogDebug(ex, ...)`
mitprotokollierte Ausnahme-Stacktrace, der wie ein Ausnahmefehler wirkt. Ziel: Erwartete
Pipe-Brüche beim geplanten Stop erzeugen keinen irreführenden Ausnahmeeintrag mehr; die
Diagnosefähigkeit für echte Fehler (unerwarteter Prozess-Exit über `ReportProcessExit`/
`LastUnexpectedExit`, `Failed`-Warnung der Pumpe) bleibt erhalten. Es wird ausschließlich der
`pipe:0`-Eingabepfad betroffen sein (im Protokoll: `-i pipe:0`); bei Datei-/HTTP-Eingaben
(`LiveStreamInput.ForFile`/`ForHttp`) läuft keine Pumpe.

## Betroffene Klassen und Komponenten

- `VideoWebPlayer/Services/SegmenterProcessSupport.cs`
  - `PumpInputAsync` (Zeilen 123–154): Catch-Pfad für erwartete Stopps
    (`IOException`/`ObjectDisposedException`/`OperationCanceledException`, Zeilen 139–144) —
    dort wird die Ausnahme aktuell als `Debug`-Logeintrag inkl. Stacktrace ausgegeben.
- `VideoWebPlayer/Services/FfmpegLiveStreamSegmenter.cs`
  - `StartAsync` (Zeile 180): Start des Pump-Tasks ohne Referenz/Einholen.
  - `StopCoreAsync` (Zeilen 202–229): killt den Prozess, ohne die noch laufende Pumpe
    einzusammeln — der zeitliche Grund für die über den Job-Wechsel hinaus laufende Pumpe.
  - Private `PumpInputAsync`-Hülle (Zeilen 311–317): ruft den Helper mit dem
    `FfmpegLiveStreamSegmenter (Slot {SlotId})`-Caller-Tag aus dem Protokoll.
- `VideoWebPlayer/Services/LiveStreamStandbySegmenter.cs`
  - Nutzt denselben Helper (`PumpInputAsync`, Zeilen 268–278); holt den Pump-Task bereits
    zeitgebunden ein (`PumpDrainTimeout`, Zeile 206). Änderungen am Helper-Verhalten sind
    hier mitzudenken.
- `VideoWebPlayer/Services/LiveStreamWorker.cs` / `LiveStreamCoordinator.cs`
  - Auslösekontext des Sendeplatz-Wechsels; voraussichtlich unverändert.
- `VideoWebPlayer/Services/LiveStreamInput.cs` / `LiveStreamMediaResolver.cs`
  - `LiveStreamInput.ForStream` (Pipe-Fallback für nicht-lokale Quellen,
    `LiveStreamMediaResolver`, Zeile 132) als einziger Pfad mit `IsPipe == true`.
- Tests
  - `VideoWebPlayer.Tests/Services/SegmenterProcessSupportTests.cs` (bestehend, derzeit nur
    `StartRealProcess`-Abdeckung): Nachweis, dass ein Pipe-Bruch beim geplanten Stop keinen
    Ausnahme-Stacktrace mehr ins Log schreibt.
  - `VideoWebPlayer.Tests/Services/FfmpegLiveStreamSegmenterTests.cs`: Verhalten beim
    Slot-Wechsel/Stop mit Pipe-Eingabe (Fake-`ISegmenterProcess` ist vorhanden).

## Implementierungsansatz

- In `SegmenterProcessSupport.PumpInputAsync` den Catch-Pfad für erwartete Stopps so
  anpassen, dass kein Ausnahmeobjekt ans Logging übergeben wird — nur Ausnahmetyp/Kurzmeldung
  oder Nachricht ohne Stacktrace (z. B. `ex.GetType().Name`/`ex.Message` in der
  Log-Vorlage statt `ex` als erstem Argument).
- Ergänzend in der Planung abzuwägen (Annahme, nicht aus der Anforderung ableitbar): Den
  Pump-`Task` in `FfmpegLiveStreamSegmenter` in einem Feld festhalten und in `StopCoreAsync`
  nach dem Prozess-Kill zeitgebunden einholen — Muster ist `PumpDrainTimeout` aus
  `LiveStreamStandbySegmenter`. Damit endet die „Eingabe-Pump beendet"-Meldung geordnet vor
  dem nächsten Job-Start statt als verwaister Task danach. Ein unbegrenztes Warten ist
  ausgeschlossen, da `CopyToAsync` ohne `CancellationToken` läuft und ein in
  `source.ReadAsync` blockierender Pump durch den Pipe-Bruch nicht zwingend aufwacht.
- Echte Fehler bleiben unterscheidbar: Unerwartete Prozessenden meldet `ReportProcessExit`
  als Warnung samt `LastUnexpectedExit` für den Input-Fallback des Koordinators;
  unerwartete Pump-Fehler bleiben `LogWarning` mit `SegmenterPumpOutcome.Failed`.

## Konfiguration

Keine neue Konfiguration erforderlich. Der betroffene Eintrag steht bereits auf
`Debug`-Ebene; die Protokolltiefe steuert weiterhin die bestehende Logging-Konfiguration.

## Offene Fragen

- Soll der Eintrag bei erwartetem Stop nur ohne Stacktrace als kompakte `Debug`-Meldung
  erhalten bleiben (weiterhin diagnostisch sichtbar) oder ganz entfallen?
- Genügt die Log-Anpassung, oder soll der Pump-Task strukturell beim Stoppen eingeholt
  werden (größerer Eingriff in `FfmpegLiveStreamSegmenter`, analog dem
  `LiveStreamStandbySegmenter`-Muster)?
