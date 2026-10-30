# Übersetzte Anforderung: TV-Sender — Live-Stream von Serien/Filmen spielt auf Linux-Produktivserver nicht ab

Quelle: `issue.md` (Issue #311) im Repository-Root.

## Fachliche Zusammenfassung

Die Echtzeit-HLS-Segmentierung des automatisierten TV-Senders (`FfmpegLiveStreamSegmenter`,
gesteuert durch `LiveStreamCoordinator` im Takt des `LiveStreamWorker`) läuft auf dem
produktiven Linux-Server für reguläre Serien-/Film-Sendeplätze fehl, während
Platzhalter-Clip-Sendeplätze fehlerfrei segmentiert werden. Der ffmpeg-Lauf des betroffenen
Sendeplatzes erhält seine Eingabe über `pipe:0` (gestreimter `LiveStreamInput.ForStream`-Pfad)
und bricht nach ~112 s mit Exit-Code 183 ab: Der mov/mp4-Demuxer meldet `partial file` und
`Could not find codec parameters ... unspecified pixel format`, danach
`Cannot determine format of input 0:0 after EOF` — es wird kein einziges HLS-Segment erzeugt.
Zu beheben ist, dass Serien- und Film-Sendeplätze genauso segmentiert und ausgespielt werden
wie Platzhalter-Clips — unabhängig davon, ob das Medium aus einer lokalen
(`MediaSourceType.LocalDirectory`) oder entfernten (`MediaSourceType.Sftp`) Quelle stammt.

## Betroffene Klassen und Komponenten

- `LiveStreamMediaResolver` (`Services/LiveStreamMediaResolver.cs`) — wählt den
  ffmpeg-Eingang je Sendeplatz: `LiveStreamInput.ForFile` für lokale Quellen,
  `LiveStreamInput.ForHttp` (interner Range-Endpunkt `api/internal/media-stream/{token}`)
  für entfernte Quellen bei aktivem `LiveStream:UseHttpInput`, sonst
  `LiveStreamInput.ForStream` (`pipe:0`-Pump über `IMediaSourceReader.OpenFileStream`).
  Zentrale Stelle für Eingangs-Auswahl, Fallback und Diagnose, warum der Pipe-Weg gewählt
  wurde.
- `LiveStreamCoordinator` (`Services/LiveStreamCoordinator.cs`) — steuert über
  `useHttpInput` den HTTP-Input-Fallback bei `LastUnexpectedExit.HttpTransportFailure`
  (`FfmpegErrorMarkers.ContainsHttpTransportError`); ein Pipe-Eingangsfehler wird heute
  nicht anders behandelt als jeder andere unerwartete Exit.
- `FfmpegLiveStreamSegmenter` (`Services/FfmpegLiveStreamSegmenter.cs`) — baut die
  ffmpeg-Argumente (`BuildArguments`: `-re -ss <pos> -i pipe:0 ... -f hls`); relevante
  Ansatzpunkte sind die Input-Optionen (`-analyzeduration`/`-probesize`-Hinweis aus dem
  ffmpeg-Log) sowie die Position von `-ss` relativ zum nicht seekbaren Pipe-Input.
- `SegmenterProcessSupport` (`Services/SegmenterProcessSupport.cs`) — `InputSpec`,
  `NoStdinArg`, `PumpInputAsync`: hier läuft der `CopyToAsync`-Pump der Quelle in die
  ffmpeg-Stdin; mögliche Erweiterung um Vollständigkeitsdiagnose (gepumpte Bytes vs.
  erwartete Dateigröße), um „abgebrochene Quelle" von „moov-am-Ende" zu unterscheiden.
- `LiveStreamInput` (`Services/LiveStreamInput.cs`) — Eingangsarten Datei/HTTP/Pipe;
  falls die Lösung eine neue Eingangsart braucht (z. B. lokal zwischengespeicherte Datei
  entfernter Quellen), ist dies die Erweiterungsstelle.
- `InternalMediaStreamTokenService` (`Services/InternalMediaStreamTokenService.cs`) —
  `TryCreateStreamUri` liefert `null`, wenn keine `http://`-Basis-URL auflösbar ist
  (`MediaProbe:HttpProbeBaseUrl` oder gebundene http-Adresse); ein reiner
  HTTPS-Betrieb/Reverse-Proxy ohne http-Bindung fällt damit still auf `pipe:0` zurück.
- `LiveStreamOptions` (`Configuration/LiveStreamOptions.cs`, Section `LiveStream`) —
  `UseHttpInput` (Default `true`); ggf. neue Optionen je nach Lösungsweg.
- `IMediaSourceReader` / `SftpMediaSourceReader` / `SftpStreamWrapper` — die gepumpte
  Quelle; `OpenFileStream` liefert einen grundsätzlich seekbaren Stream, der über die Pipe
  jedoch nur sequenziell konsumiert wird.
- Tests: `FfmpegLiveStreamSegmenterTests`, `FfmpegLiveStreamSegmenterE2ETests`,
  `LiveStreamMediaResolverTests`, `LiveStreamCoordinatorTests`, `LiveStreamE2ETests`
  (ggf. neue Tests je nach Lösungsweg).

## Implementierungsansatz

Der Fehlerbild-Analyse aus `issue.md` folgend gibt es zwei nicht ausschließende
Ursachenrichtungen, die in der Planung konkretisiert werden:

1. **Eingangsart:** Der Pipe-Pfad (`pipe:0`) ist für MP4-Dateien, deren `moov`-Atom am
   Dateiende liegt, prinzipiell nicht demuxbar — ffmpeg müsste die gesamte `mdat`-Payload
   sequenziell durchlaufen, bevor Codec-Parameter bekannt sind (in `docs/help/tv-sender.md`
   bereits dokumentiert). Platzhalter-Clips laufen vermutlich deshalb, weil sie aus einer
   lokalen Quelle als Dateipfad (`ForFile`, seekbar) an ffmpeg gehen — *Annahme, in der
   Bestandsaufnahme zu verifizieren*. Naheliegende Lösungsrichtung: entfernte Medien dem
   Segmenter wieder seekbar reichen — den bestehenden HTTP-Input (`ForHttp`) auf dem
   Produktivserver zum Greifen bringen oder einen ebenfalls seekbaren Ersatz schaffen
   (z. B. lokale Zwischenablage der entfernten Datei, dann `ForFile`).
2. **Eingangsrobustheit:** Der Pipe-Pfad bleibt als dokumentierter Fallback bestehen und
   muss entweder belastbar funktionieren (Ansatzpunkte aus dem Issue: `probesize`/
   `analyzeduration` erhöhen — hilft nur, wenn das `moov`-Atom rechtzeitig ankommt;
   alternative Container-/Remux-Strategien; Verhalten von `-ss` auf nicht seekbarem
   Input) oder der Fallback darf für betroffene Dateien nicht mehr schweigend zum
   `pipe:0`-Weg führen, der garantiert scheitert.

Diagnose-Aspekt: `partial file`/`after EOF` kann sowohl ein unvollständig gepumpter
Quell-Stream (SFTP-Leseabbruch, Truncation) als auch das `moov`-am-Ende-Problem sein;
der Pump sollte erkennbar machen, ob die Datei vollständig geliefert wurde.

Annahmen (explizit): dass Serien/Filme auf dem Produktivserver aus einer SFTP-Quelle
stammen, dass die Platzhalter-Clips lokal liegen und dass der Pipe-Weg aktiv war, weil
`TryCreateStreamUri` keine http-Basis-URL auflösen konnte (HTTPS-only/Proxy) oder
`LiveStream:UseHttpInput` deaktiviert ist — die tatsächliche Ursache ist produktiv zu
verifizieren, bevor die Lösungsrichtung festgelegt wird.

## Konfiguration

Bestehende, relevante Einstellungen (keine neue Option aus der Anforderung ableitbar):

- `LiveStream:UseHttpInput` (`appsettings.json`, Default `true`) — schaltet den
  HTTP-Range-Eingang entfernter Quellen ab; auf dem Produktivserver prüfen.
- `MediaProbe:HttpProbeBaseUrl` — explizite Basis-URL für den Loopback-Endpunkt, falls
  die automatische Erkennung der gebundenen http-Adresse auf dem Produktivserver scheitert.
- Je nach gewähltem Lösungsweg in der Planung ggf. neue Optionen (z. B. Größe/Ort einer
  lokalen Zwischenablage, ffmpeg-Probe-Optionen) — Ebene: Anwendungseinstellungen.

## Akzeptanzkriterien

1. Serien-/Film-Sendeplätze aus entfernten Quellen werden auf dem Linux-Produktivserver
   segmentiert und im HLS-Stream ausgespielt wie lokale Platzhalter-Clips; ffmpeg endet
   planmäßig (Exit-Code 0 am Slotende) statt mit `partial file`/`Cannot determine format`.
2. Die Ursache, warum auf dem Produktivserver `pipe:0` statt des seekbaren HTTP-Eingangs
   verwendet wurde, ist festgestellt und im Ergebnis dokumentiert (Konfiguration,
   Basis-URL-Auflösung oder Fallback-Kette).
3. Bleibt `pipe:0` als Fallback-Pfad bestehen, ist sein Verhalten für MP4 ohne
   `moov`-Atom am Anfang entweder funktionsfähig gemacht oder der Fehler wird als
   Eingangsproblem erkannt, klar geloggt und verursacht keine wiederholte
   Restart-Schleife ohne Gegenmaßnahme.
4. Ein unvollständig gepumpter Eingabestream ist von einem `moov`-am-Ende-Problem im Log
   unterscheidbar (Diagnose).
5. Bestehendes Verhalten bleibt unverändert: lokale Quellen über Dateipfad, HTTP-Input
   bei verfügbarer Basis-URL, Playlist-Fortführung über `append_list`/`discont_start`,
   Token-Maskierung in Logs.
6. Tests decken den behobenen Pfad ab; Debug- und Release-Build fehlerfrei, volle
   Testsuite grün.

## Offene Fragen

- Warum lief der Sendeplatz 19185 produktiv über `pipe:0` — ist `LiveStream:UseHttpInput`
  gesetzt, scheitert `TryCreateStreamUri` an der http-Basis-URL (keine http-Bindung hinter
  Reverse-Proxy/HTTPS-only) oder griff der HTTP-Transport-Fallback eines früheren Laufs?
- Liegen die betroffenen Serien-/Film-Dateien als MP4 mit `moov`-Atom am Dateiende vor
  (kein `faststart`)? Entscheidet darüber, ob `probesize`/`analyzeduration` allein genügt
  oder ein seekbarer Eingang zwingend ist.
- Wurde die Quelldatei über den Pump vollständig geliefert (Byte-Zählung vs. Dateigröße),
  oder bricht die SFTP-Quelle mittendrin ab (`partial file` als Truncation)?
- Ist auf dem Produktivserver Platzzulage/Strategie für eine lokale Zwischenablage
  entfernter Dateien gegeben, falls der Lösungsweg dahin geht?
- Gilt das erwartete Verhalten auch für den dokumentierten Fallback-Fall „HTTP-Eingang
  bewusst deaktiviert" — muss der Pipe-Weg dann ebenfalls MP4-moov-am-Ende-tauglich sein?
