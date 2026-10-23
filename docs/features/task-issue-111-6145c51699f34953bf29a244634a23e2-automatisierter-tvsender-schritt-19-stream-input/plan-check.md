# Plan-Gegenprüfung

## Ergebnis

**Status:** Plan vollständig

## Abgleich Akzeptanzkriterien

| Akzeptanzkriterium | Umsetzung im Plan | Testnachweis im Plan | Status |
|--------------------|-------------------|----------------------|--------|
| 1. stderr bei unerwartetem Exit loggen (Exit-Code + Tail, zentral Warning/Fehler) | stderr-Tail in `DrainStandardErrorAsync`, vorzeitige-Exit-Klassifikation, `LogWarning` mit maskiertem Tail | `UnexpectedExit_LogsWarningWithExitCodeAndStderrTail`, `UnexpectedExit_MasksStreamTokenInStderr` | Abgedeckt |
| 2. Seekfähige HTTP-Eingabe über internen Range-Endpunkt statt `pipe:0`; Token-TTL für lange Läufe | `LiveStreamInput.ForHttp`, `TryCreateStreamUri` mit anforderer-TTL (`RemainingSeconds` + Puffer) | `ResolveAsync_RemoteSource_ReturnsHttpUrl`, `ResolveAsync_PassesRunDurationAsTokenLifetime`, `TryCreateStreamUri_CustomLifetime_Honored` | Abgedeckt |
| 3. Fallback: URI nicht baubar oder Transportfehler → `pipe:0`; nicht jeder Fehler kaskadiert | Resolver-Fallback bei `null`-URI/deaktivierter Option; Coordinator-Fallback nur bei `HttpTransportFailure` (N9-Marker) | `ResolveAsync_UseHttpInputFalse/TokenServiceNull/OptionDisabled_FallsBackToPipe`, `SynchronizeAsync_HttpTransportFailure_...FallsBackToPipe`, `SynchronizeAsync_NonTransportExit_KeepsHttp` | Abgedeckt |
| 4. Restart-Verhalten (+30 s) analysiert/dokumentiert | Designentscheidung „Restart-Semantik": positionsbasiert korrekt, keine Doppelbelegung; Kommentar + Bericht | `LiveStreamCoordinatorTests`-Bestand (Restart-Pfade bleiben grün) | Abgedeckt |
| 5. Tests: stderr-Pfad, HTTP-URL + Maskierung, Fallback, Coordinator-Restart | 18 neue Tests über 4 Testklassen | siehe Plan §Tests | Abgedeckt |
| 6. Kein Token in Logs; Debug+Release fehlerfrei; Suite grün | Maskierung auf stderr-Tail + Args-Debug-Log; Verify-Task | `UnexpectedExit_MasksStreamTokenInStderr`, volle Suite | Abgedeckt |

## E2E-Abdeckung

| Benutzerfluss / Akzeptanzkriterium | Geplanter E2E-Test | Status |
|------------------------------------|--------------------|--------|
| Kein Benutzerfluss betroffen (reines Backend: Prozess-Input, Logging, Fallback) | — | Nicht erforderlich — begründet: keine UI-/Nutzeraktion erreichbar; bestehender `LiveStreamE2ETests`/`LiveTvE2ETests` bleibt als Regression. |

## Hinweise

- Geprüfte Risiken: TTL-Falle (Probe-TTL 600 s < Slot-Länge) — explizit gelöst über
  `TryCreateStreamUri(lifetime)`; scoped Coordinator kann keinen Fehlerzustand halten →
  `LastUnexpectedExit` im Singleton-Segmenter; Exit-Code-0-Frühexit wird durch die
  Laufzeit-Toleranz (statt nur Exit-Code ≠ 0) als vorzeitig erkannt; planmäßiges `-t`-Ende
  erzeugt kein Warnrauschen.
- `FakeSegmenterProcess.StandardError` ist ein `MemoryStream` — EOF-Semantik passt zum
  Drain-Reader ohne weitere Synchronisation; Exit-Simulation über `HasExited`/`ExitCode`-Setter.
- `ListLogger<T>`-Testhelper existiert (`VideoWebPlayer.Tests/Helpers`) für Log-Level-Assertions.
