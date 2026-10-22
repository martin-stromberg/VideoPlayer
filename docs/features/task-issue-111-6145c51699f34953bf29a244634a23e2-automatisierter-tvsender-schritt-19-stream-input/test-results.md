# Test-Ergebnisse

## Ergebnis

**Status:** Keine Fehler

## Fehlgeschlagene Tests

Keine.

## E2E-Abdeckung

Keine neuen E2E-Szenarien erforderlich — die Anforderung berührt keinen Benutzerfluss
(reines Backend: Prozess-Input, Logging, Fallback). Bestehende Live-Stream-/Live-TV-E2E-Tests
liefen im Volllauf als Regression mit.

## Zusammenfassung

| Lauf | Befehl | Ergebnis |
|------|--------|----------|
| Zieltests | `dotnet test VideoWebPlayer.Tests/VideoWebPlayer.Tests.csproj --no-build --filter "…Segmenter|…MediaResolver|…Coordinator|…TokenService|…Worker"` | 58 grün / 0 Fehler |
| Bereichstests | `--filter "…MediaDurationProbe|…Ffprobe|…MediaProbe|…InternalMediaStreamController|…LiveStream"` | 152 grün / 0 Fehler |
| Volle Suite | `dotnet test VideoWebPlayer.Tests/VideoWebPlayer.Tests.csproj` | **2145 grün / 0 Fehler / 1 übersprungen** (Dauer 3 m 53 s) |
| Build Debug | `dotnet build VideoPlayer.sln` | 0 Fehler, 144 Warnungen (Bestand) |
| Build Release | `dotnet build VideoPlayer.sln -c Release` | 0 Fehler, 144 Warnungen (Bestand) |
| Format | `dotnet format VideoPlayer.sln --verify-no-changes --severity error` | sauber |

Übersprungen: `PairedDevice_ReportsProgressForMovieWithoutCollection_ContinueWatchingListStillLoads`
— bekannter Alt-Skip der Baseline.

## Testabdeckung

21 neue/angepasste Tests:

- `FfmpegLiveStreamSegmenterTests` (8): HTTP-URL-Argumente/-nostdin/kein Stdin-Redirect;
  Warning mit Exit-Code + stderr-Tail bei unerwartetem Exit; Token-Maskierung in stderr-Log
  und Args-Debug-Log; `LastUnexpectedExit` mit/ohne `HttpTransportFailure` (HTTP- vs.
  Pipe-Job); planmäßiges Laufende ohne Warning; Kill ≠ unerwartet; Reset bei `StartAsync`.
  Neues `ControlledPipeStream`-Fake blockiert stderr-Leser bis Prozessende (realistisches
  EOF-Verhalten).
- `LiveStreamMediaResolverTests` (6): Remote → `HttpUrl` (+Token-Aufruf);
  `useHttpInput:false`, `TryCreateStreamUri → null`, `UseHttpInput=false` → Pipe;
  `runDuration` → Token-Lifetime (90 min → 105 min); LocalSource ruft Token-Dienst nie.
- `LiveStreamCoordinatorTests` (4): Remote-Medium nutzt HTTP-Input; Transportfehler desselben
  Slots → Pipe (kein Token-Aufruf); Nicht-Transport-Exit → bleibt HTTP; Transportfehler eines
  anderen Slots → bleibt HTTP.
- `InternalMediaStreamTokenServiceTests` (4): freie TTL eingehalten (2 h);
  `HttpProbeEnabled=false` sperrt `TryCreateStreamUri` nicht; keine http-Adresse → `null`;
  Mini-TTL wird auf 60 s-Minimum geclamped.

## Fehlende Tests

Keine bekannt.

## Hinweise

- Baseline (identischer Code-Stand, Schritt 18): 2124 grün + 1 Alt-Skip → jetzt
  2145 grün + 1 Alt-Skip (+21 Tests, davon 1 Ersetzung).
