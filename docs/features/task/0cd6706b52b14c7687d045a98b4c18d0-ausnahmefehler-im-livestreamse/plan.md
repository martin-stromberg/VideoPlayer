# Umsetzungsplan: Ausnahmefehler im LiveStreamSegmenter

## Übersicht

Beim geplanten Stop (Sendeplatz-Wechsel) des ffmpeg-Live-Stream-Segmentierers bricht die
stdin-Pumpe erwartungsgemäß mit `IOException` (Broken pipe) ab — `SegmenterProcessSupport.PumpInputAsync`
behandelt das bereits als `SegmenterPumpOutcome.Completed`, protokolliert die Ausnahme aber per
`logger.LogDebug(ex, ...)` mit vollem Stacktrace, was wie ein Ausnahmefehler wirkt. Der Plan sieht
zwei Änderungen vor: (1) Der erwartete-Stop-Catch-Pfad loggt nur noch Ausnahmetyp und Kurzmeldung
ohne Ausnahmeobjekt — kein Stacktrace mehr. (2) `FfmpegLiveStreamSegmenter` hält den Pump-`Task`
in einem Feld fest und holt ihn in `StopCoreAsync` zeitgebunden ein (Muster `PumpDrainTimeout` aus
`LiveStreamStandbySegmenter`), damit die „Eingabe-Pump beendet"-Meldung geordnet vor dem nächsten
Job-Start erscheint statt als verwaister Task danach. Echte Fehler bleiben unverändert
unterscheidbar (`Failed`-Warnung der Pumpe, `ReportProcessExit`/`LastUnexpectedExit`).

## Designentscheidungen

| Komponente / Bereich | Gewählter Ansatz | Begründung |
| --- | --- | --- |
| Log im erwarteten-Stop-Pfad von `SegmenterProcessSupport.PumpInputAsync` | Kompakter `Debug`-Eintrag **ohne** Ausnahmeobjekt: `ex.GetType().Name` und `ex.Message` als Parameter der Log-Vorlage (z. B. `"[{Caller}] Eingabe-Pump beendet ({ExceptionType}: {Reason})."`); Level `Debug` bleibt | Erfüllt die Kernforderung (kein irreführender Ausnahme-Stacktrace) und erhält die Diagnosefähigkeit: Aus der Meldung bleibt erkennbar, welcher der drei erwarteten Ausnahmetypen (`IOException`/`ObjectDisposedException`/`OperationCanceledException`) und welche Ursache („Broken pipe") auftrat — besser als ganzes Entfernen des Eintrags. |
| Pump-`Task` in `FfmpegLiveStreamSegmenter` | **Einholen:** Task-Referenz in Feld `_pumpTask` festhalten und in `StopCoreAsync` nach `Kill()`/`WaitForExitAsync` zeitgebunden per `WaitAsync(PumpDrainTimeout)` einholen | Der im Protokoll beanstandete Nebeneffekt (Pumpe des alten Sendeplatzes endet erst nach dem Start des neuen) entsteht erst dadurch, dass der Task verwaischt. Einholen macht die Log-Reihenfolge geordnet („Eingabe-Pump beendet" vor „Segmentierung ... beendet") und den Unit-Test deterministisch (`await StopAsync` garantiert Pump-Ende, kein Polling). Alternativ nur die Log-Anpassung — die wäre minimaler, lässt aber das im Protokoll sichtbare Ordnungsproblem und den verwaisten Task bestehen. |
| Begrenzung des Pump-Einholens | Konstante `PumpDrainTimeout` (10 s, wie `LiveStreamStandbySegmenter`), über internen Ctor als `TimeSpan?`-Parameter injizierbar | `CopyToAsync` läuft ohne `CancellationToken` — ein in `source.ReadAsync` blockierender Pump wacht durch den Pipe-Bruch nicht zwingend auf; unbegrenztes Warten würde `StopAsync` hängen. Timeout-Injektion folgt dem bestehenden Muster (`transcodeTimeout`/`stderrDrainTimeout` im internen Ctor von `LiveStreamStandbySegmenter`) und hält den Stall-Test schnell. |
| Verwaister Pump nach Ablauf des Drain-Timeouts | Nach `TimeoutException` spätere Faultierung per `ContinueWith(..., OnlyOnFaulted)` beobachten, kein `await` mehr | Gleiche Situation wie beim stderr-Drain in `LiveStreamStandbySegmenter.TranscodeAsync` (Z. 199–201) — dortiges Muster wird übernommen, damit keine unbeobachtete Task-Ausnahme liegen bleibt. |
| `LiveStreamStandbySegmenter` | Keine Änderung | Holt den Pump-Task bereits zeitgebunden ein (`PumpDrainTimeout`, Z. 206); die Helper-Log-Änderung gilt dort automatisch — gewollte Konsistenz des Log-Formats über beide Segmentierer. |

## Programmabläufe

### Geplanter Stop mit Pipe-Eingabe (Normalfall, beanstandeter Protokollpfad)

1. `LiveStreamCoordinator.SynchronizeStreamAsync` (Sendeplatz-Wechsel) ruft
   `ILiveStreamSegmenter.StartAsync` bzw. `StopAsync`; `LiveStreamWorker` nur Auslösekontext.
2. `FfmpegLiveStreamSegmenter.StopCoreAsync` (unter `_gate`) liest `_process`, `_job` **und neu
   `_pumpTask`**, setzt alle drei auf `null` (Markierung „geplanter Stop").
3. `process.Kill()` + `process.WaitForExitAsync` mit `StopTimeout` (10 s); `process.Dispose()`.
4. Der Kill bricht die stdin-Pipe; `SegmenterProcessSupport.PumpInputAsync` landet im
   erwarteten-Stop-Catch (`IOException`/`ObjectDisposedException`/`OperationCanceledException`)
   und schreibt **neu** einen kompakten `Debug`-Eintrag mit Ausnahmetyp + Kurzmeldung **ohne**
   Ausnahmeobjekt — kein Stacktrace. `finally` schließt `process.StandardInput`; Rückgabe
   `SegmenterPumpOutcome.Completed`.
5. `StopCoreAsync` wartet auf `_pumpTask.WaitAsync(PumpDrainTimeout)` — kehrt im Normalfall in
   Millisekunden zurück; erst danach folgt die `Information`-Meldung „Segmentierung für
   Sendeplatz {SlotId} beendet". Log-Reihenfolge dadurch: Pump-Ende vor Segmentierungs-Ende.
6. Nächster Job-Start (`StartAsync`) wiederholt den Ablauf mit frischem Prozess; `_pumpTask`
   wird dort bei `job.Input.IsPipe` neu gesetzt (sonst `Task.CompletedTask`).

Beteiligte Klassen/Komponenten: `FfmpegLiveStreamSegmenter` (`StartAsync`, `StopAsync`,
`StopCoreAsync`, `PumpInputAsync`-Hülle), `SegmenterProcessSupport` (`PumpInputAsync`),
`ISegmenterProcess`, `LiveStreamInput` (`IsPipe`).

### Geplanter Stop mit stallender Quelle (Pump wacht nicht auf)

1. Wie Normalfall bis Schritt 5: `source.ReadAsync` blockiert trotz Pipe-Bruch.
2. `_pumpTask.WaitAsync(PumpDrainTimeout)` läuft in `TimeoutException` → wird geschluckt;
   die noch laufende Pumpe bekommt eine `OnlyOnFaulted`-Fortsetzung, damit eine spätere
   Faultierung beobachtet ist.
3. `StopCoreAsync` kehrt innerhalb der Schranke zurück; der verwaiste Pump-Task bleibt
   fachlich harmlos (schreibt nur noch in den geschlossenen stdin-Handle des toten Prozesses).

Beteiligte Klassen/Komponenten: `FfmpegLiveStreamSegmenter.StopCoreAsync`,
`SegmenterProcessSupport.PumpInputAsync`.

### Unerwarteter Pump-Fehler und unerwartetes Prozessende (unverändert)

- Nicht-erwartete Ausnahme im Pump: `LogWarning(ex, ...)` mit Ausnahme →
  `SegmenterPumpOutcome.Failed`; `FfmpegLiveStreamSegmenter`-Hülle loggt bei
  `SourceUnavailable` weiterhin eine eigene Warnung.
- Unerwartetes Prozessende: unverändert über `DrainStandardErrorAsync` → `ReportProcessExit`
  (Warnung + `LastUnexpectedExit` für den HTTP→Pipe-Fallback des Koordinators). Der
  stderr-Drain-Task bleibt bewusst Fire-and-forget — nicht Gegenstand dieser Anforderung.

Beteiligte Klassen/Komponenten: `SegmenterProcessSupport`, `FfmpegLiveStreamSegmenter`
(`DrainStandardErrorAsync`, `ReportProcessExit`), `LiveStreamCoordinator`.

## Neue Klassen

Keine Produktivklassen — nur Test-Attrappen (privat, klassenintern in den Testdateien):

| Klasse | Typ | Zweck |
| --- | --- | --- |
| `ThrowingWriteStream` | `Stream`-Attrappe (Tests) | `StandardInput`-Ersatz, dessen `WriteAsync` eine konfigurierbare Ausnahme wirft (`IOException`/`ObjectDisposedException`/`OperationCanceledException`/`InvalidOperationException`) — erzeugt den Pipe-Bruch deterministisch; `Close()`-Aufruf für den `finally`-Nachweis zählbar. |
| `FakePumpProcess` | `ISegmenterProcess`-Fake (Tests) | Minimale Prozess-Attrappe für `SegmenterProcessSupportTests`: konfigurierbares `StandardInput`, stderr-Pipe, `Killed`-Flag. |
| `NeverEndingReadStream` | `Stream`-Attrappe (Tests) | Quell-Stream, dessen `ReadAsync` nie abschließt — modelliert die stallende Quelle für den `PumpDrainTimeout`-Pfad (Muster: `NeverEndingStream` in `LiveStreamStandbySegmenterTests`). |
| `BlockingSource` / Erweiterung `FakeSegmenterProcess` | Test-Attrappe (Tests) | Für `FfmpegLiveStreamSegmenterTests`: Pipe-Eingabe, deren `OpenStreamAsync` einen kontrolliert endenden bzw. nie endenden Stream liefert, und `StandardInput`, das nach `Kill()` beim Schreiben `IOException` wirft. |

## Änderungen an bestehenden Klassen

### `SegmenterProcessSupport` (`internal static`)

- **Geänderte Methoden:** `PumpInputAsync` — im Catch-Pfad für erwartete Stopps
  (`IOException`/`ObjectDisposedException`/`OperationCanceledException`) wird das
  Ausnahmeobjekt nicht mehr ans Logging übergeben. Statt `logger.LogDebug(ex, "...")` wird eine
  strukturierte `Debug`-Meldung mit `ex.GetType().Name` und `ex.Message` als Parametern
  ausgegeben (z. B. `"[{Caller}] Eingabe-Pump beendet ({ExceptionType}: {Reason})."`).
  Rückgabe `SegmenterPumpOutcome.Completed` bleibt; der `Failed`-/`SourceUnavailable`-Pfad
  und das `finally`-`Close()` bleiben unverändert.

### `FfmpegLiveStreamSegmenter` (`public sealed`)

- **Neue Felder:** `_pumpTask` (`Task`, Referenz des laufenden Eingabe-Pumps; `Task.CompletedTask`
  bei Nicht-Pipe-Eingaben), `_pumpDrainTimeout` (`TimeSpan`, aus Ctor-Parameter oder Default).
- **Neue Konstanten:** `PumpDrainTimeout` (`private static readonly TimeSpan`, 10 s — wie
  `LiveStreamStandbySegmenter`).
- **Geänderte Methoden:**
  - Interner Ctor — neuer optionaler Parameter `pumpDrainTimeout` (`TimeSpan?`, Default
    `PumpDrainTimeout`) für schnelle Stall-Tests; Produktiv-Ctor unverändert.
  - `StartAsync` — der Pump-`Task.Run` bei `job.Input.IsPipe` wird in `_pumpTask` festgehalten
    statt verworfen (`_ =`); ohne Pipe-Eingabe `_pumpTask = Task.CompletedTask`.
  - `StopCoreAsync` — liest und leert zusätzlich `_pumpTask`; holt den Pump nach dem
    Prozessende-`try/finally` zeitgebunden ein (`WaitAsync(_pumpDrainTimeout)`,
    `TimeoutException` geschluckt + `OnlyOnFaulted`-Beobachtung), vor der
    `Information`-Meldung „Segmentierung ... beendet".
- **Neue Eigenschaften:** Keine. **Neue Events:** Keine. **Neue Event-Handler:** Keine.

### Weitere Klassen

- `LiveStreamStandbySegmenter`: **keine Änderung** (Pump-Einholen vorhanden; Log-Änderung
  greift automatisch).
- `LiveStreamWorker`, `LiveStreamCoordinator`, `LiveStreamInput`, `LiveStreamMediaResolver`,
  `ILiveStreamSegmenter`, `ISegmenterProcess`: **keine Änderung**.

## Datenbankmigrationen

Keine.

## Validierungsregeln

Keine.

## Konfigurationsänderungen

Keine. Der betroffene Eintrag bleibt auf `Debug`-Ebene; die Protokolltiefe steuert weiterhin
die bestehende Logging-Konfiguration (aus der Anforderung: keine neue Konfiguration nötig).

## Seiteneffekte und Risiken

- **`LiveStreamStandbySegmenter`:** Nutzt denselben Helper — sein erwarteter-Stop-Eintrag
  verliert ebenfalls den Stacktrace (gewollte Konsistenz, gleiche Semantik). Kein
  Codeeingriff nötig.
- **Stop-/Wechsel-Laufzeit:** `StopCoreAsync` wartet zusätzlich auf den Pump — im Normalfall
  < 1 s (Pipe bricht beim Kill), im Worst Case bis `PumpDrainTimeout` (10 s) bei stallender
  Quelle. Der Sendeplatz-Wechsel (`StartAsync` → `StopCoreAsync`) verlängert sich entsprechend
  höchstens um diese Schranke.
- **Verwaiste Pumpe nach Drain-Timeout:** Bei blockiertem `source.ReadAsync` läuft der
  Pump-Task weiterhin verwaist (ohne `CancellationToken` in `CopyToAsync` nicht abbrechbar) —
  Faultierung wird per Fortsetzung beobachtet; fachlich harmlos, da nur noch der
  stdin-Handle des toten Prozesses berührt wird.
- **Log-Textänderung:** „Eingabe-Pump beendet." wird zu „Eingabe-Pump beendet ({Typ}: {Meldung})."
  ohne Stacktrace — automatisierte Auswertungen auf den exakten alten Text oder den
  angehängten Stacktrace wären betroffen (kein solcher Verbraucher im Repo bekannt).
- **stderr-Drain bleibt Fire-and-forget:** „wurde angehalten" aus `ReportProcessExit` kann
  weiterhin asynchron nach der „beendet"-Meldung erscheinen — bewusst unverändert, nicht
  Teil der Anforderung.
- **Thread-Sicherheit:** `_pumpTask` wird nur unter `_gate` gelesen/geschrieben — kein
  Race mit dem Pump-Task selbst (dieser schreibt das Feld nicht).

## Umsetzungsreihenfolge

1. **Log-Anpassung in `SegmenterProcessSupport.PumpInputAsync`**
   - Voraussetzungen: keine.
   - Beschreibung: Erwarteter-Stop-Catch auf Ausnahme-freie `Debug`-Meldung mit
     `ex.GetType().Name`/`ex.Message` umstellen; Kommentar „expected during stops" anpassen.
2. **Pump-Einholen in `FfmpegLiveStreamSegmenter`**
   - Voraussetzungen: Schritt 1 (deterministischer Log-Nachweis setzt die geänderte
     Meldung voraus; code-technisch unabhängig, aber gleicher Schritt-Branch).
   - Beschreibung: `PumpDrainTimeout`-Konstante, `_pumpTask`-/`_pumpDrainTimeout`-Felder,
     Ctor-Parameter, `_pumpTask` in `StartAsync` setzen, Einholen in `StopCoreAsync` inkl.
     `TimeoutException`-Behandlung und `OnlyOnFaulted`-Beobachtung.
3. **Unit-Tests `SegmenterProcessSupportTests`**
   - Voraussetzungen: Schritt 1; neue Attrappen (`ThrowingWriteStream`, `FakePumpProcess`).
   - Beschreibung: Direkter Test von `PumpInputAsync` — Pipe-Bruch → `Completed` + kompakter
     `Debug`-Eintrag ohne Stacktrace; unerwarteter Fehler → `Failed` + `Warning` **mit**
     Ausnahme; `SourceUnavailable`; erfolgreiches Pumpen inkl. stdin-`Close()`.
4. **Unit-Tests `FfmpegLiveStreamSegmenterTests`**
   - Voraussetzungen: Schritt 2; Erweiterung der Fakes (werfendes `StandardInput`,
     nie endende Quelle).
   - Beschreibung: Stop mit Pipe-Eingabe → Pump-Debug-Eintrag ohne Stacktrace ist nach
     `await StopAsync` vorhanden und vor der „beendet"-Meldung; stallende Quelle →
     `StopAsync` kehrt innerhalb der injizierten Schranke zurück; Slot-Wechsel via
     zweitem `StartAsync` sammelt die alte Pumpe ein.
5. **E2E-Test mit echtem ffmpeg**
   - Voraussetzungen: Schritte 1–2; ffmpeg im `PATH` (Test beendet sich sonst still,
     `E2EBinaryLocator`-Muster).
   - Beschreibung: `FfmpegLiveStreamSegmenterE2ETests` — echter Prozess, Pipe-Eingabe aus
     generiertem Video, geplanter Stop → kein Logeintrag mit Ausnahme-Stacktrace.
6. **Build und volle Testsuite**
   - Voraussetzungen: Schritte 1–5.
   - Beschreibung: `dotnet build VideoPlayer.sln` Debug + Release fehlerfrei;
     `dotnet test VideoWebPlayer.Tests/VideoWebPlayer.Tests.csproj` grün (DoD, AGENTS.md).

## Tests

### Neue Tests

| Test / Hilfsmethode | Testklasse | Was wird geprüft / bereitgestellt? |
| --- | --- | --- |
| `ThrowingWriteStream` (Attrappe) | `SegmenterProcessSupportTests` | `WriteAsync` wirft konfigurierbare Ausnahme; `Close()`-Aufruf zählbar. |
| `FakePumpProcess` (Attrappe) | `SegmenterProcessSupportTests` | `ISegmenterProcess` mit konfigurierbarem `StandardInput`. |
| `PumpInputAsync_BrokenPipe_LogsCompactDebugWithoutStacktrace` | `SegmenterProcessSupportTests` | `IOException` beim Schreiben → `SegmenterPumpOutcome.Completed`; genau ein `Debug`-Eintrag mit Caller-Tag, der weder das Ausnahme-`ToString()` (`"System.IO.IOException:"`) noch Stacktrace-Zeilen enthält. |
| `PumpInputAsync_ExpectedStopExceptions_LogWithoutStacktrace` (Theory: `ObjectDisposedException`, `OperationCanceledException`) | `SegmenterProcessSupportTests` | Gleicher Catch-Pfad der anderen erwarteten Ausnahmen: `Completed`, `Debug`-Eintrag ohne Ausnahme-Text/Stacktrace. |
| `PumpInputAsync_UnexpectedError_LogsWarningWithException` | `SegmenterProcessSupportTests` | `InvalidOperationException` beim Schreiben → `Failed`, `Warning`-Eintrag **mit** Ausnahme-Text — Diagnosefähigkeit für echte Fehler bleibt. |
| `PumpInputAsync_SourceUnavailable_ReturnsOutcomeWithoutPump` | `SegmenterProcessSupportTests` | `OpenStreamAsync` → `null` ⇒ `SourceUnavailable`, kein Logeintrag; stdin wird trotzdem geschlossen. |
| `PumpInputAsync_Success_PumpsAndClosesStdin` | `SegmenterProcessSupportTests` | Endliche Quelle → `Completed`, Bytes landen im `StandardInput`, `Close()` im `finally` läuft. |
| Erweiterung `FakeSegmenterProcess` + `NeverEndingReadStream` (Attrappen) | `FfmpegLiveStreamSegmenterTests` | `StandardInput`, das nach `Kill()` beim Schreiben `IOException` wirft; Quell-Stream, dessen `ReadAsync` nie abschließt. |
| `StopAsync_PipeInputPumpBreak_LogsCompactDebugBeforeStopReturns` | `FfmpegLiveStreamSegmenterTests` | Geplanter Stop mit Pipe-Eingabe: nach `await StopAsync` existiert der `Debug`-Eintrag „Eingabe-Pump beendet" (deterministisch, kein Polling) ohne Ausnahme-Text/Stacktrace und **vor** der `Information` „Segmentierung ... beendet". |
| `StopAsync_StalledPump_ReturnsWithinPumpDrainTimeout` | `FfmpegLiveStreamSegmenterTests` | Quelle blockiert in `ReadAsync`; mit injiziertem kleinem `pumpDrainTimeout` kehrt `StopAsync` innerhalb einer oberen Schranke zurück (kein Hängen). |
| `StartAsync_SlotSwitch_CollectsPreviousPump` | `FfmpegLiveStreamSegmenterTests` | Zweiter `StartAsync` (Sendeplatz-Wechsel) mit Pipe-Eingabe: die Pumpe des ersten Jobs ist beim Rückkehr des zweiten `StartAsync` bereits beendet — Debug-Eintrag des alten Slots liegt vor der Start-Meldung des neuen Slots. |
| `RealFfmpeg_PipeInputStop_LogsNoExceptionStacktrace` | `FfmpegLiveStreamSegmenterE2ETests` | Siehe E2E-Abschnitt. |

### Betroffene bestehende Tests

Keine — kein bestehender Test prüft die Pump-Logmeldung oder hängt am Fire-and-forget-Verhalten;
`StopAsync`-Aufrufe in vorhandenen Tests haben endliche Pumpen (leere `MemoryStream`-Quellen),
die das neue Einholen sofort abschließt.

### E2E-Tests (primärer Funktionsnachweis)

Kein Benutzerfluss betroffen (reine Server-interne Prozess-/Log-Logik) — der primäre Nachweis
erfolgt auf Unit-Ebene, weil dort der Pipe-Bruch deterministisch auslösbar und die Log-Aussage
direkt prüfbar ist. Ergänzend ein echter-ffmpeg-E2E, der das im Produktiv-Protokoll beobachtete
Szenario (echter Prozess-Kill unter laufender Pipe-Pumpe) einmal real durchspielt — der
Test ist nicht racy, weil das neue Einholen den Pump-Eintrag vor Rückkehr von `StopAsync`
garantiert (bei `-re` und ~6 s Material ist die Pumpe beim Stop noch aktiv; der Kill bricht die
Pipe zwingend):

| Szenario | Testklasse | Erwartetes Ergebnis |
| --- | --- | --- |
| Geplanter Stop mit Pipe-Eingabe und echtem ffmpeg | `FfmpegLiveStreamSegmenterE2ETests` (`RealFfmpeg_PipeInputStop_LogsNoExceptionStacktrace`) | `ListLogger` am Produktiv-Ctor: Pipe-Eingabe (`LiveStreamInput.ForStream` über `FileStream` des generierten Videos), ~1 s laufen lassen, `StopAsync` → in `Entries` kein Eintrag mit Ausnahme-`ToString()`/Stacktrace-Zeilen; kein `Warning` der Pumpe; der kompakte `Debug`-Eintrag „Eingabe-Pump beendet" ist (wenn vorhanden) stacktrace-frei. |

## Offene Punkte

Keine. (Die beiden ursprünglichen Punkte — Log-Meldung behalten als kompakte `Debug`-Meldung, Pump-`Task` zeitgebunden einholen — wurden vom Anwender am 2026-10-29 jeweils mit dem empfohlenen Vorschlag bestätigt und sind in den Designentscheidungen eingearbeitet.)
