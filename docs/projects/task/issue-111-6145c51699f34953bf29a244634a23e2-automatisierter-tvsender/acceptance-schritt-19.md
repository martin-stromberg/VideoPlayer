# Abnahme Schritt 19 — Segmenter stderr-Diagnose + seekfähiger HTTP-Input (N15)

Prüfer: Devin (unabhängige Abnahme, nicht der Implementierer)
Geprüfter Stand: `593fd2a` (`feat: Segmenter stderr-Diagnose + seekfaehiger HTTP-Input
fuer Remote-Medien (N15)`), Arbeitsbaum nach den Gegenproben wieder sauber
(`git status` leer, `git diff HEAD` leer).

## Ergebnis

**Status:** Erfüllt

Alle fünf N15-Anforderungen sind im Code verifiziert umgesetzt: (1) ffmpeg-stderr wird
bei unerwartetem Segmenter-Exit als Warnung mit Exit-Code + 8-KB-Tail ins Log
geschrieben (vorher wurde stderr nur gedrained und verworfen — die eigentliche
Diagnosability-Lücke des Live-Befunds); (2) Remote-Items bekommen über
`TryCreateStreamUri` die range-fähige N9-Endpoint-URL als ffmpeg-`-i`-Argument statt
des pipe:0-Pumps; (3) der Fallback auf Pipe greift ausschließlich bei klassifiziertem
HTTP-Transportfehler desselben Slots; (4) das Restart-Verhalten (+30 s-Sprünge =
positionsbasierter Resync am nächsten Poll-Takt, kein Doppel-Booking) ist geprüft und
im Coordinator-Kommentar + `docs/help/tv-sender.md` dokumentiert; (5) Token wird in
ffmpeg-Aufruf-Log und stderr-Tail maskiert — durch Gegenprobe nachgewiesen, nicht nur
behauptet.

### Verifiziert (Code-Review)

- **stderr-Tail ins Log, auch bei Exit 0** (`FfmpegLiveStreamSegmenter.cs:243-312`):
  `DrainStandardErrorAsync` hält einen rollierenden 8-KB-`StringBuilder`-Tail;
  `ReportProcessExit` klassifiziert nach EOF rein über die Laufzeit (`elapsed <
  RemainingSeconds - max(10 s, 2×SegmentSeconds)` → unerwartet) — der Exit-Code fließt
  nicht in die Einordnung ein, ein Frühexit mit Code 0 wird also ebenfalls gewarnt.
  Warnung enthält SlotId, Elapsed, Exit-Code, Transport-Flag und maskierten Tail.
- **Kein Warnrauschen**: Kill via `StopCoreAsync` löscht `_process` vor dem Kill →
  `ReferenceEquals(_process, process)` scheitert → Debug „wurde angehalten";
  planmäßiges `-t`-Ende → Debug „endete planmaessig". Beide Pfade durch Tests
  (`StopAsync_Kill_IsNotReportedAsUnexpectedExit`, `ExpectedEndOfRun_…`) abgesichert.
- **HTTP-Input** (`BuildArguments:371-376`): URL wird wie der Dateipfad gequotet,
  `-nostdin` + `RedirectStandardInput=false`, kein Pump-Task (`IsPipe` ⇒
  `OpenStreamAsync is not null`). `LiveStreamInput` wird nur über Fabriken erzeugt
  (kein Direkt-`new` im Repo), der inkonsistente Zustand (alles null) ist nicht
  erreichbar.
- **Range-Endpunkt**: `InternalMediaStreamController.Stream` gibt
  `File(stream, …, enableRangeProcessing: true)` — bereits der N9-Pfad, ffmpeg kann
  damit für `-ss` per Range-Request seeken.
- **Token-TTL** (`LiveStreamMediaResolver.cs:120-128`,
  `InternalMediaStreamTokenService.cs:98-101`): `RemainingSeconds + 15 min`, Minimum
  60 s, Default 45 min bei unbekannter Restlaufzeit. Jeder Restart-Versuch prägt ein
  neues Token → Slot-Länge + Reschedule abgedeckt. Stream-Pfad bewusst NICHT an
  `HttpProbeEnabled` gekoppelt (Test `…_ProbeDisabled_StillIssuesUri`).
- **Fallback nur bei HTTP-Transportfehler** (`LiveStreamCoordinator.cs:111-129`):
  `useHttpInput=false` nur wenn `LastUnexpectedExit.HttpTransportFailure` UND
  `SlotId == now.SlotId`; Transport-Flag nur bei `job.Input.HttpUrl != null` UND
  Marker im Tail (`ReportProcessExit:302-303`) — Pipe-/Datei-Läufe mit zufällig
  ähnlichen stderr-Texten triggern keinen Fehl-Fallback (Test
  `UnexpectedExit_PipeInputTransportMarker_NoHttpTransportFlag`). Slot/Item geht beim
  Fallback nicht verloren (gleicher `ResolveAsync`-Pfad, Test
  `…_FallsBackToPipe` prüft `IsPipe` + `StreamUriCalls == 0`). Marker sind mit
  `FfprobeMediaDurationProbe` via `FfmpegErrorMarkers` geteilt — keine doppelte,
  auseinanderlaufende Liste.
- **`LastUnexpectedExit`-Concurrency**: `volatile`-Referenz auf immutablem Record,
  geschrieben vom Drain-Thread des Singletons, gelesen vom Scoped-Coordinator —
  sichere Publikation. `StartAsync` setzt das Feld unter `_gate` zurück; ein
  verspäteter Drain eines Altvorgangs kann es nicht mehr überschreiben
  (`ReferenceEquals`-Wächter).
- **Pipe-Pfad unverändert**: `PumpInputAsync`, `ForStream` und die
  `OpenFileStream`-Fabrik unverändert; alle Alt-Tests laufen.
- **Option**: `LiveStreamOptions.UseHttpInput` Default `true`, gebunden via
  `services.Configure<LiveStreamOptions>(…"LiveStream")`, in `appsettings.json` +
  `docs/help/tv-sender.md` dokumentiert; Resolver respektiert `false` (Test
  `…_OptionDisabled_FallsBackToPipe`).
- **DI**: `ILiveStreamSegmenter` und `IInternalMediaStreamTokenService` Singletons,
  `LiveStreamMediaResolver`/`LiveStreamCoordinator` Scoped — Lifetime-korrekt.
- **Kein Token/Passwort in Logs**: `Job`/`Input`-Records werden nirgends ganz
  geloggt; die einzigen Stellen mit URL (Start-`Arguments`, stderr-Tail) sind
  doppelt maskiert (`MaskStreamTokenInText` + `MaskCredentialsInText`); Regex
  `…/media-stream/[0-9a-fA-F]{16,}` trifft das 64-Hex-Token.

### Gegenproben (selbst kaputtgemacht, Test rot, revertiert)

- Token-Maskierung aus `ReportProcessExit` entfernt →
  `UnexpectedExit_MasksStreamTokenInStderr` schlägt fehl (Token steht unmaskiert in
  der Warnung). → `git checkout --` revertiert.
- `HttpUrl`-Zweig aus `BuildArguments` entfernt (HTTP-Input fiele still auf pipe:0) →
  `StartAsync_HttpInput_UsesUrlArgumentAndNoStdin` schlägt fehl. → revertiert.

### Build & Tests (selbst ausgeführt, synchron)

- `dotnet build VideoPlayer.sln` Debug: **0 Fehler**, 144 Warnungen — identisch zum
  Release-Lauf, alle Altlasten (BL0008/CS9113/CS8602), keine neue Warnung in den
  geänderten Dateien.
- `dotnet build VideoPlayer.sln -c Release`: **0 Fehler**.
- `dotnet test VideoWebPlayer.Tests/VideoWebPlayer.Tests.csproj` vollständig:
  **2145 bestanden / 0 Fehler / 1 übersprungen** (`…ReportsProgressForMovieWithoutCollection…`,
  Alt-Skip), 2146 gesamt — deckt sich exakt mit dem Implementierer-Bericht
  (+21 neue Tests verifiziert im Diff).

## Abweichungen

- [ ] Keine fachlichen Abweichungen festgestellt.

## Hinweise

- **Kleines Wettlauffenster beim Exit-Reporting**: Beendet sich der Prozess selbst
  und der Coordinator-Tick ruft `StopAsync`/`StartAsync`, BEVOR der Drain-Task
  `ReportProcessExit` ausführt (Millisekunden-Fenster; `ReadExitCode` wartet sogar
  bis zu 5 s), schlägt `ReferenceEquals` fehl → Warnung und `LastUnexpectedExit`
  gehen für diesen einen Exit verloren. Folge: ein zusätzlicher erfolgloser
  HTTP-Versuch, danach greift der Fallback regulär. Selbstkorrigierend, sehr
  unwahrscheinlich, nicht sicherheitsrelevant — als bekannt hinnehmbar.
- **`ResolveAsync` prägt je Tick ein Token**, auch wenn der Lauf „Unchanged" bleibt
  (Coordinator ruft den Resolver vor der Drift-Prüfung). Durch `SweepExpiredTokens`
  gebunden; vom Implementierer im Handover transparent gemacht. Vertretbar.
- **Drain-Exception ohne Report**: Wirft der stderr-Stream `IOException`/
  `ObjectDisposedException` statt sauberem EOF, endet `DrainStandardErrorAsync` ohne
  `ReportProcessExit` → kein Exit-Report. Betrifft faktisch nur Kill/Dispose-Pfade
  (natürliches Prozessende schließt die Pipe sauber per EOF); vertretbar.
- **Handover-Ungenauigkeit (kosmetisch)**: Der Handover behauptet, die
  Lifecycle-Artefakte unter `docs/features/…-schritt-19-stream-input/` seien „im
  Abschlusscommit entfernt" — tatsächlich sind alle 13 Dateien (inventory/, plan.md,
  review*.md, …) in `593fd2a` committed. Präzedenzfall existiert (Schritte 10 und 17
  haben ihre Artefakt-Ordner ebenfalls eingecheckt); nur der Selbstbericht ist
  ungenau. Nicht funktionsrelevant.
- **Oszillation bei Dauer-Transportfehler**: Scheitert HTTP am Transport, läuft der
  Pipe-Fallback; stirbt auch dieser (aus anderem Grund), versucht der nächste Tick
  wieder HTTP — bei dauerhaft tote Endpunkt + defekter Datei alterniert das
  Verfahren. Da jeder HTTP-Fehlversuch sauber gewarnt wird, bleibt das im Log
  nachvollziehbar; kein Kaskadieren, vertretbar.
- Frühexit bei Medium kürzer als Slot-Restlaufzeit wird als „unerwartet" gemeldet —
  fachlich korrekt (Sendelücke entsteht), kein Fehlalarm.
