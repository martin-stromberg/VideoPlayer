# Umsetzungsplan: N15 — Segmenter-Restart-Schleife (stderr-Logging + seekfähiger HTTP-Input)

## Übersicht

Drei eng zusammenhängende Änderungen am Live-Stream-Pfad: (1) `FfmpegLiveStreamSegmenter`
sammelt einen stderr-Tail und loggt vorzeitige Prozess-Exits mit Exit-Code + maskiertem Tail
(Prio-1-Diagnosability), (2) Remote-Medien werden über den internen Range-Endpunkt
(`api/internal/media-stream/{token}`, N9) als seekfähige HTTP-URL an ffmpeg übergeben statt per
`pipe:0`-Pump — Token-TTL an der Restlaufzeit des Sendeplatzes bemessen, (3) bei nicht baubarem
URI oder HTTP-Transportfehler fällt der Coordinator auf den `pipe:0`-Pfad zurück.
Restart-Analyse: die +30 s-Sprünge sind positionsbasiert korrekt (Re-Sync auf die aktuelle
Programmposition am nächsten Tick), keine Doppelbelegung — dokumentiert im Bericht.

## Designentscheidungen

| Komponente / Bereich | Gewählter Ansatz | Begründung |
|----------------------|-----------------|------------|
| Exit-Diagnose | stderr-Tail (gekappter `StringBuilder`, ~4 KB) im Drain-Task; „vorzeitig" = Prozess noch als aktueller Job registriert (`_process == process`) **und** Laufzeit deutlich unter `RemainingSeconds` (Toleranz `max(10 s, 2×SegmentSeconds)`) | Unterscheidet sauber zwischen Kill-durch-Stop (`_process` bereits `null`), planmäßigem `-t`-Ende (Laufzeit ≈ Restlaufzeit → Debug) und echtem Fehler (vorzeitig → Warning + Exit-Code + Tail + `LastUnexpectedExit`). Ein Exit-Code-0-Frühexit (mögliches Bug-Szenario) wird so ebenfalls erkannt. |
| HTTP-Transportfehler-Klassifikation | `HttpTransportErrorMarkers`/`ContainsHttpTransportErrorMarker` aus `FfprobeMediaDurationProbe` in neue interne Klasse `FfmpegErrorMarkers` extrahiert, von beiden genutzt | N9-Marker wörtlich wiederverwenden (Anforderung), keine Duplikation; Probe-Verhalten bleibt identisch. |
| Neue Eingabeart | `LiveStreamInput` um `HttpUrl` erweitert (`ForHttp`); `IsPipe` neu `=> OpenStreamAsync is not null`; `BuildArguments` quotet URL wie Pfad, `-nostdin`/kein stdin-Redirect | ffmpeg-Seite identisch zu Datei-Input — `-ss` wirkt per HTTP-Range, moov-am-Ende entfällt. |
| Stream-Token | `IInternalMediaStreamTokenService.TryCreateStreamUri(long, TimeSpan lifetime)` neben `TryCreateProbeUri` (gemeinsamer privater Aussteller); **nicht** an `HttpProbeEnabled` gekoppelt | Probe-TTL 600 s deckt keine langen Läufe; die Option `HttpProbeEnabled` steuert fachlich die Laufzeitermittlung, nicht den Stream. |
| Token-TTL | `RemainingSeconds` + 15 min Puffer (Minimum 60 s), vom Resolver an den Token-Dienst übergeben | Token muss den gesamten Segmentierungslauf überdauern; Puffer deckt Tick-Latenz. |
| Stream-seitige Option | Neue `LiveStreamOptions` (Section `LiveStream`), `UseHttpInput` Default `true`; Gate im Resolver | Anforderung nennt explizit eine eigene Option; `MediaProbe`-Section bleibt Probe-Sache. |
| Fallback-Steuerung | `ILiveStreamSegmenter.LastUnexpectedExit` (`LiveStreamExitInfo`: SlotId, ExitCode, HttpTransportFailure) im Singleton-Segmenter; Coordinator liest vor dem Resolve und übergibt `useHttpInput:false` bei Transportfehler desselben Slots | Coordinator ist scoped/zustandslos — nur der Singleton kann Versuchsgrenzen über Ticks halten. Nur Transportfehler kaskadieren (N9-Muster); Inhaltsfehler bleiben auf dem besseren HTTP-Pfad. Wird in `StartAsync` zurückgesetzt. |
| Alternation | Kein Slot-Pinning: schlägt auch der Pipe-Pfad fehl, darf der nächste Tick wieder HTTP probieren | Selbstheilung bei transienten Endpoint-Problemen; beide Pfade sind im Log sichtbar. |
| Token-Maskierung | `MaskStreamTokenInText` + `SftpProbeUriBuilder.MaskCredentialsInText` auf stderr-Tail und neuem Debug-Log der ffmpeg-Args | Token sitzt im URI-Pfad — Credential-Maskierung allein reicht nicht (N9-Erkenntnis). |
| Restart-Semantik | Keine Codeänderung: positionsbasierter Neustart ist korrekt (Single-Process + `_gate`, keine Doppelbelegung); Dokumentation im Bericht + XML-Kommentar | Anforderung verlangt Analyse/Dokumentation, keinen Umbau. |

## Programmabläufe

### Unerwarteter Segmenter-Exit (Diagnosability)

1. `StartAsync` startet `DrainStandardErrorAsync(process, job)` (Job mitgeben für Toleranz/Input-Art).
2. Der Drain liest stderr per `StreamReader` bis EOF und hält die letzten ~4 KB als Tail.
3. Bei EOF: `WaitForExitAsync` mit kleinem Grace-Timeout → Exit-Code lesen.
4. `_process == process`? Nein → absichtlicher Stop → `LogDebug`, Ende.
5. Ja → Laufzeit (`_timeProvider.GetUtcNow() - _startedAt`) vs. `job.RemainingSeconds - Toleranz`:
   nicht vorzeitig → `LogDebug`. Vorzeitig → Tail maskieren (`MaskStreamTokenInText` +
   `MaskCredentialsInText`), `LogWarning` mit Exit-Code + Tail, `LastUnexpectedExit` setzen
   (`HttpTransportFailure` = `job.Input.HttpUrl != null && tail enthält HTTP-Transport-Marker`).

### Remote-Input-Auflösung mit HTTP-Primärpfad

1. Coordinator: `var useHttpInput = !(_segmenter.LastUnexpectedExit is { SlotId: var s, HttpTransportFailure: true } && s == now.SlotId)`; bei `false` `LogInformation` (Fallback aktiv).
2. `LiveStreamMediaResolver.ResolveAsync(mediaItemId, runDuration, useHttpInput, ct)`:
   `LocalDirectory` → weiterhin `ForFile`. Remote: `useHttpInput && _liveStreamOptions.UseHttpInput`
   → `_streamTokens.TryCreateStreamUri(item.Id, ttl)` (`ttl = (runDuration ?? 30 min) + 15 min`).
   URI != null → `ForHttp(uri)`; sonst (deaktiviert/nicht baubar) → bisheriger `ForStream`.
3. `BuildArguments`: `HttpUrl` → `-i "{url}"`, `-nostdin`, kein `RedirectStandardInput`, `-ss` wirkt
   via HTTP-Range.

### ffmpeg-Argumente (geändert)

`-i` wählt Pfad → `HttpUrl` → `pipe:0` in dieser Reihenfolge; `noStdin`/Stdin-Redirect allein an
`IsPipe`. Neuer `LogDebug` der maskierten Argumente in `StartAsync`.

## Neue Klassen

| Klasse | Typ | Zweck |
|--------|-----|-------|
| `FfmpegErrorMarkers` | internal static | `HttpTransportErrorMarkers` + `ContainsHttpTransportError` (aus `FfprobeMediaDurationProbe` extrahiert). |
| `LiveStreamExitInfo` | public record | `SlotId`, `ExitCode`, `HttpTransportFailure`, `OccurredAtUtc` — Rückmeldung des Segmenters an den Coordinator. |
| `LiveStreamOptions` | Optionsklasse | Section `LiveStream`; `UseHttpInput` (Default `true`), `EffectiveUseHttpInput` nicht nötig (bool). |

## Änderungen an bestehenden Klassen

### `FfmpegLiveStreamSegmenter` / `ILiveStreamSegmenter`

- **Neue Eigenschaft:** `LastUnexpectedExit` (`LiveStreamExitInfo?`) — gesetzt bei vorzeitigem
  Exit, in `StartAsync` zurückgesetzt.
- **`DrainStandardErrorAsync(process, job)`:** Tail-Puffer, EOF-Klassifikation, Warning-Log.
- **`StartAsync`:** `LogDebug` der maskierten Argumente; `LastUnexpectedExit = null`.
- **`BuildArguments`:** `HttpUrl`-Zweig.

### `LiveStreamInput` (record)

- Drittes Feld `HttpUrl` (`string?`, Default `null`), `ForHttp(string)`, `IsPipe` →
  `OpenStreamAsync is not null`.

### `IInternalMediaStreamTokenService` / `InternalMediaStreamTokenService`

- **Neue Methode:** `TryCreateStreamUri(long mediaItemId, TimeSpan lifetime)` — gemeinsamer
  privater Aussteller `TryCreateUri(mediaItemId, ttl)`; `TryCreateProbeUri` delegiert mit
  Probe-TTL und bleibt an `HttpProbeEnabled` gekoppelt; Stream-Variante prüft nur `ResolveBaseUrl`.
  TTL-Untergrenze 60 s.

### `LiveStreamMediaResolver`

- Neue Dependencies: `IInternalMediaStreamTokenService`, `IOptions<LiveStreamOptions>`.
- **`ResolveAsync`** neue Signatur: `(long mediaItemId, TimeSpan? runDuration = null,
  bool useHttpInput = true, CancellationToken ct = default)`; HTTP-Primärpfad für Remote-Quellen.

### `LiveStreamCoordinator`

- `useHttpInput` aus `LastUnexpectedExit` ableiten, Fallback-Log, `ResolveAsync`-Aufruf mit
  `runDuration`/`useHttpInput`; kurzer XML-/Code-Kommentar zur positionsbasierten Restart-Semantik.

### `FfprobeMediaDurationProbe`

- `HttpTransportErrorMarkers`/`ContainsHttpTransportErrorMarker` → `FfmpegErrorMarkers` delegieren
  (kein Verhaltenswechsel).

### `ServiceCollectionExtensions` / `appsettings.json`

- `services.Configure<LiveStreamOptions>(configuration.GetSection("LiveStream"))`;
  `"LiveStream": { "UseHttpInput": true }` dokumentiert.

## Datenbankmigrationen

Keine.

## Validierungsregeln

Keine neuen Eingaben — `UseHttpInput` ist boolsche Option; `TryCreateStreamUri` clamped TTL ≥ 60 s.

## Konfigurationsänderungen

| Eintrag | Typ | Standardwert | Zweck |
|---------|-----|--------------|-------|
| `LiveStream:UseHttpInput` | bool | `true` | Remote-Medien für den Live-Stream über den internen Range-Endpunkt an ffmpeg reichen; `false` = alter `pipe:0`-Pump. |

## Seiteneffekte und Risiken

- **Interface-Bruch `ILiveStreamSegmenter`/`IInternalMediaStreamTokenService`:** Test-Fakes
  (`FakeSegmenter`, `StubStreamTokenService`) müssen die neuen Member implementieren.
- **`LiveStreamMediaResolver.ResolveAsync`-Signatur:** bestehende Aufrufer (Coordinator +
  Resolver-Tests) werden angepasst.
- **Loopback-Abhängigkeit des Live-Streams:** der Stream hängt neu am eigenen HTTP-Endpoint —
  nicht erreichbar (z. B. HTTPS-only) liefert `TryCreateStreamUri` `null` → automatischer
  Pipe-Fallback, kein Funktionsverlust.
- **Token in Prozess-Args:** die ffmpeg-Kommandozeile trägt das Token (sichtbar in
  Prozesslisten — wie bei ffprobe seit N9 akzeptiert; Token ist kurzlebig und loopback-intern).
  Logs werden maskiert.
- **stdout bleibt unbeachtet:** ffmpeg schreibt Diagnosen auf stderr (umgeleitet); kein
  weiterer Puffer nötig.

## Umsetzungsreihenfolge

1. **`FfmpegErrorMarkers` extrahieren** — Voraussetzungen: keine; `FfprobeMediaDurationProbe` umstellen.
2. **`LiveStreamInput.HttpUrl` + `BuildArguments`** — Voraussetzungen: keine.
3. **`TryCreateStreamUri` im Token-Dienst** — Voraussetzungen: keine.
4. **stderr-Tail + `LastUnexpectedExit` im Segmenter** — Voraussetzungen: 1.
5. **`LiveStreamOptions` + DI + appsettings** — Voraussetzungen: keine.
6. **Resolver-HTTP-Pfad** — Voraussetzungen: 2, 3, 5.
7. **Coordinator-Fallback + Restart-Kommentar** — Voraussetzungen: 4, 6.
8. **Tests** — Voraussetzungen: 1–7.
9. **Doku** (`docs/help/tv-sender.md`, `docs/RELEASE_NOTES.md`) — Voraussetzungen: 1–7.

## Tests

### Neue Tests

| Test | Testklasse | Was wird geprüft? |
|------|------------|--------------------|
| `StartAsync_HttpInput_UsesUrlArgumentAndNoStdin` | `FfmpegLiveStreamSegmenterTests` | `-i "http://…"`, `-nostdin`, `RedirectStandardInput=false`. |
| `UnexpectedExit_LogsWarningWithExitCodeAndStderrTail` | dto. | Fake: stderr mit Fehlertext, `HasExited`, ExitCode ≠ 0 → Warning enthält Exit-Code + Tail. |
| `UnexpectedExit_MasksStreamTokenInStderr` | dto. | stderr mit `…/api/internal/media-stream/{64hex}` → Log zeigt `***`, kein Token. |
| `UnexpectedExit_SetsLastUnexpectedExit_HttpTransportMarker` | dto. | HTTP-Job + stderr „Server returned 404" → `LastUnexpectedExit.HttpTransportFailure == true`, SlotId stimmt. |
| `UnexpectedExit_PipeInput_NoHttpTransportFlag` | dto. | Pipe-Job, Transport-Marker → `HttpTransportFailure == false`. |
| `ExpectedEndOfRun_NoWarning` | dto. | Exit nahe `RemainingSeconds` → kein Warning, `LastUnexpectedExit == null`. |
| `StopAsync_Kill_NoUnexpectedExitLog` | dto. | Kill → kein Warning/`LastUnexpectedExit`. |
| `StartAsync_ClearsLastUnexpectedExit` | dto. | Neuer Job setzt `LastUnexpectedExit` zurück. |
| `ResolveAsync_RemoteSource_ReturnsHttpUrl` | `LiveStreamMediaResolverTests` | Stub-Token-Service liefert URI → `HttpUrl` gesetzt, nicht `IsPipe`. |
| `ResolveAsync_UseHttpInputFalse_ReturnsPipe` | dto. | `useHttpInput:false` → `IsPipe`. |
| `ResolveAsync_TokenServiceNull_FallsBackToPipe` | dto. | `TryCreateStreamUri` → `null` → `IsPipe`. |
| `ResolveAsync_OptionDisabled_FallsBackToPipe` | dto. | `LiveStream:UseHttpInput=false` → `IsPipe`. |
| `ResolveAsync_PassesRunDurationAsTokenLifetime` | dto. | Stub protokolliert Lifetime = Restlaufzeit + Puffer. |
| `SynchronizeAsync_HttpTransportFailure_PreviousSlot_FallsBackToPipe` | `LiveStreamCoordinatorTests` | `LastUnexpectedExit`(Slot, transport) → Job-Input `IsPipe`. |
| `SynchronizeAsync_NonTransportExit_KeepsHttp` | dto. | Exit ohne Marker → Job-Input `HttpUrl`. |
| `TryCreateStreamUri_CustomLifetime_Honored` | `InternalMediaStreamTokenServiceTests` | Token löst vor/nach TTL korrekt auf (Fake `TimeProvider`). |
| `TryCreateStreamUri_ProbeDisabled_StillWorks` | dto. | `HttpProbeEnabled=false` → URI wird trotzdem ausgestellt. |
| `TryCreateStreamUri_NoAddress_ReturnsNull` | dto. | Keine http-Adresse → `null`. |

### Betroffene bestehende Tests

| Test / Testklasse | Grund der Anpassung |
|-------------------|---------------------|
| `FakeSegmenter` in `LiveStreamCoordinatorTests` | neuer Interface-Member `LastUnexpectedExit`. |
| `StubStreamTokenService` in `MediaDurationProbeServiceTests` | neuer Interface-Member `TryCreateStreamUri`. |
| `LiveStreamMediaResolverTests`-Ctor-Aufrufe | neue Dependencies (Token-Stub, `LiveStreamOptions`). |
| `LiveStreamCoordinatorTests`-Ctor-Aufruf | Resolver-Dependencies. |
| `InternalMediaStreamTokenServiceTests` ggf. | gemeinsame Aussteller-Refaktorisierung (nur bei Bruch). |

### E2E-Tests

Keine neuen E2E-Tests — die Anforderung berührt keinen Benutzerfluss (reines
Backend-/Infrastrukturverhalten: Prozess-Input und Logging). Bestehender
`LiveStreamE2ETests`/`LiveTvE2ETests`-Pfad bleibt unverändert und dient als Regression.

## Offene Punkte

Keine.
