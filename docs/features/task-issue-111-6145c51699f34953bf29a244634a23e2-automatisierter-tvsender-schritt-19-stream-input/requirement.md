# Übersetzte Anforderung: N15 — Segmenter-Restart-Schleife: stderr-Logging + seekfähiger HTTP-Input

Quelle: `docs/projects/task/issue-111-6145c51699f34953bf29a244634a23e2-automatisierter-tvsender/nachbesserungen.md` §N15 (Live-Befund 2026-10-22).

## Fachliche Zusammenfassung

Der Live-Stream des automatisierten TV-Senders restartet `FfmpegLiveStreamSegmenter` für entfernte
Medienquellen alle ~14–17 s: ffmpeg verlässt `pipe:0` (`PumpInputAsync` meldet „Die Pipe wurde
beendet"), der Coordinator startet beim nächsten Tick an der dann aktuellen Programmposition neu —
die Position springt je Versuch ~30 s vor, der Stream bleibt degradiert. Drei Maßnahmen:
(1) ffmpeg-stderr beim unerwarteten Segmenter-Exit mit Exit-Code und stderr-Tail loggen
(Diagnosability, Prio 1 — heute wird stderr gedrained, aber verworfen), (2) für Remote-Medien
denselben internen Range-Endpunkt `GET /api/internal/media-stream/{token}` wie bei N9 als
ffmpeg-Input-URL nutzen, damit ffmpeg seeken kann (`-ss` wirkt, moov-am-Ende entfällt,
kein Volldurchlauf-Pump), (3) bei nicht baubarem HTTP-Input oder HTTP-Transportfehlern auf den
bisherigen `pipe:0`-Pump zurückfallen. Zusätzlich ist das Restart-Verhalten (+30 s-Sprünge)
zu analysieren und als korrekt oder fehlerhaft zu dokumentieren.

## Betroffene Klassen und Komponenten

- `FfmpegLiveStreamSegmenter` / `ISegmenterProcess` / `SegmenterProcess` — stderr-Erfassung
  (Tail-Puffer), Klassifikation „unerwarteter Exit", Logging mit Token-/Credential-Maskierung.
- `ILiveStreamSegmenter` — ggf. neuer Member, der dem Coordinator den letzten unerwarteten Exit
  (Slot, Fehlerklassifikation) mitteilt.
- `LiveStreamInput` — neue Eingabeart „HTTP-URL" neben `FilePath` und Stream-Pump.
- `LiveStreamMediaResolver` — Remote-Items zuerst als Token-URI auflösen, Fallback Stream.
- `IInternalMediaStreamTokenService` / `InternalMediaStreamTokenService` — neue Methode für
  Stream-Tokens mit laufzeitdeckender TTL (`TryCreateProbeUri` bleibt Probe-TTL); Maskierung
  `MaskStreamTokenInText` wiederverwenden.
- `LiveStreamCoordinator` — HTTP-Fallback-Steuerung auf Basis der Exit-Klassifikation;
  Restart-Semantik prüfen/dokumentieren.
- `MediaProbeOptions` / `ProgramScheduleOptions` — Option für den HTTP-Input auf Stream-Seite
  (z. B. `LiveStream:UseHttpInput`) oder bewusste Wiederverwendung von `MediaProbe:HttpProbeEnabled`.
- Tests: `FfmpegLiveStreamSegmenterTests`, `LiveStreamMediaResolverTests`,
  `LiveStreamCoordinatorTests`, `InternalMediaStreamTokenServiceTests`.

## Implementierungsansatz

- stderr-Drain sammelt einen gekappten Tail-Puffer; endet der stderr-Stream, während der Prozess
  noch als aktueller Job registriert ist (kein `StopAsync`), gilt der Exit als unerwartet →
  `LogWarning` mit Exit-Code und maskiertem Tail.
- `LiveStreamInput` wird um `ForHttp(string url)` erweitert; `BuildArguments` behandelt URL-Input
  wie Datei-Input (`-nostdin`, `-i "url"`, `RedirectStandardInput = false`, `-ss` bleibt wirksam).
- `IInternalMediaStreamTokenService.TryCreateStreamUri(mediaItemId, lifetime)` stellt ein Token
  aus, das die Restlaufzeit des Sendeplatzes abdeckt (Probe-TTL 600 s reicht für lange Läufe nicht).
- Fallback: Resolver erhält `useHttpInput`-Parameter; der Coordinator deaktiviert HTTP für einen
  Sendeplatz, wenn der letzte unerwartete Exit ein HTTP-Transportfehler war (Marker analog
  `FfprobeMediaDurationProbe.HttpTransportErrorMarkers`); ist die URI nicht baubar, fällt der
  Resolver direkt auf den Stream-Pump zurück.
- Token-Sicherheit: Tokens stehen im URI-Pfad — sämtliche Logausgaben (ffmpeg-Args, stderr)
  über `MaskStreamTokenInText` maskieren.

## Konfiguration

Anwendungseinstellung (`appsettings.json`): Option auf Stream-Seite, Vorschlag
`LiveStream:UseHttpInput` (Default `true`) — getrennt von `MediaProbe:HttpProbeEnabled`, da
Laufzeitermittlung und Live-Stream unabhängig abschaltbar sein sollen. Token-TTL für
Stream-Tokens leitet sich aus der Restlaufzeit des Sendeplatzes ab (plus Puffer), keine
eigene Option.

## Akzeptanzkriterien

1. Unerwartetes ffmpeg-Exit erzeugt einen Warn-Logeintrag mit Exit-Code und stderr-Tail;
   Tokens/Credentials sind maskiert.
2. Remote-Medien werden dem Segmenter als `http://`-URL des internen Range-Endpunkts übergeben,
   sofern baubar; Token-TTL deckt die Restlaufzeit des Sendeplatzes.
3. Nicht baubarer URI (Feature aus, keine Adresse) und HTTP-Transportfehler führen zum
   bisherigen `pipe:0`-Pfad; andere Fehler kaskadieren nicht in den Fallback.
4. Das +30 s-Restart-Verhalten ist analysiert und dokumentiert (positions-/slot-basiert korrekt
   oder Doppelbelegung).
5. Neue/angepasste Tests: stderr-Logging-Pfad (Fake-Prozess mit stderr-Inhalt), HTTP-URL-Übergabe
   und Maskierung, Fallback-Pfade, Coordinator-Restart-Semantik.
6. Keine Token/Credentials in Logs; Debug- und Release-Build fehlerfrei; volle Suite grün.

## Offene Fragen

- Keine — Vorgehen ist in §N15 vorgegeben; Detailentscheidungen (Option-Name, TTL-Herleitung)
  werden in der Planung getroffen und begründet.
