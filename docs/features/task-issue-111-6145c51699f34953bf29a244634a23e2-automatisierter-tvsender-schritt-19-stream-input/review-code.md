# Code-Review

## Ergebnis

**Status:** Keine Befunde

## Befunde

Keine.

## Geprüfte Dateien

- `VideoWebPlayer/Services/FfmpegErrorMarkers.cs` (neu)
- `VideoWebPlayer/Services/FfprobeMediaDurationProbe.cs` (Marker-Extraktion)
- `VideoWebPlayer/Services/LiveStreamInput.cs` (`HttpUrl`, `IsPipe`)
- `VideoWebPlayer/Services/LiveStreamExitInfo.cs` (neu)
- `VideoWebPlayer/Services/ILiveStreamSegmenter.cs` (`LastUnexpectedExit`)
- `VideoWebPlayer/Services/FfmpegLiveStreamSegmenter.cs` (stderr-Tail, Exit-Klassifikation,
  maskiertes Args-Debug-Log)
- `VideoWebPlayer/Services/IInternalMediaStreamTokenService.cs` +
  `InternalMediaStreamTokenService.cs` (`TryCreateStreamUri`, gemeinsamer Aussteller)
- `VideoWebPlayer/Services/LiveStreamMediaResolver.cs` (HTTP-Primärpfad, TTL)
- `VideoWebPlayer/Services/LiveStreamCoordinator.cs` (Fallback-Steuerung)
- `VideoWebPlayer/Configuration/LiveStreamOptions.cs`, `ServiceCollectionExtensions.cs`,
  `appsettings.json`
- Tests: `FfmpegLiveStreamSegmenterTests`, `LiveStreamMediaResolverTests`,
  `LiveStreamCoordinatorTests`, `InternalMediaStreamTokenServiceTests`,
  `MediaDurationProbeServiceTests` (Stub), `LiveStreamWorkerTests` (Fakes/DI)

## Selbstkritisch geprüfte Punkte

- **Maskierung:** stderr-Tail und ffmpeg-Argumente laufen durch
  `MaskStreamTokenInText` + `MaskCredentialsInText`; Test
  `UnexpectedExit_MasksStreamTokenInStderr` prüft Token-Freiheit aller Logeinträge.
- **Vorzeitigkeits-Klassifikation:** Kill via `StopCoreAsync` löscht `_process` vor dem Kill
  → absichtliche Stops werden nicht als unerwartet gemeldet; `-t`-Laufende innerhalb der
  Toleranz erzeugt kein Warnrauschen; ein Frühexit mit Exit-Code 0 wird erkannt
  (Kriterium ist die Laufzeit, nicht nur der Code).
- **TTL:** Stream-Token = `RemainingSeconds` + 15 min (Min. 60 s) — deckt lange Sendeplätze;
  abgelaufene Token werden beim Sweep entfernt, Endpoint antwortet dann 404 →
  Transportfehler-Klassifikation → Pipe-Fallback (selbstheilend).
- **Race-Betrachtung:** `_process`-Referenzvergleich im Drain-Task ist ohne Lock — ein
  veralteter gelesener Wert kann höchstens einen unerwarteten Exit als erwartet einordnen
  (verlorener Log), nie umgekehrt; für Diagnosezwecke vertretbar.
- **Token-Churn:** `ResolveAsync` läuft je Tick auch im „Unchanged"-Fall und prägt dann ein
  Token (~1/30 s, TTL ≥ 60 s); Bestand ist durch `SweepExpiredTokens` auf wenige Einträge
  begrenzt — bewusst als vernachlässigbar akzeptiert, keine Codeänderung.
- **Interface-Brüche:** `ILiveStreamSegmenter`/`IInternalMediaStreamTokenService`/
  `LiveStreamMediaResolver`-Konstruktor/`ResolveAsync`-Signatur — alle Implementierer,
  Fakes und Aufrufstellen angepasst; Build + volle Suite grün.
- **Kein Verhaltensbruch beim Probe-Pfad:** `TryCreateProbeUri` unverändert (gleiche TTL,
  gleiches Gate); `FfprobeMediaDurationProbe` ruft identische Markertexte über
  `FfmpegErrorMarkers` auf.
- **Blocking:** `ReadExitCode` wartet max. 5 s synchron auf einem Task.Run-Thread —
  begrenzt und nur im Exit-Pfad.

## Hinweise

- `dotnet format --verify-no-changes --severity error` sauber; Debug- und Release-Build
  0 Fehler, Warnungsstand unverändert (keine neuen Warnungen).
