# Übersetzte Anforderung: Seekbare Remote-Probes via `sftp://` statt `pipe:0` (Nachbesserung N8, Schritt 12)

Quelle: `docs/projects/task/issue-111-6145c51699f34953bf29a244634a23e2-automatisierter-tvsender/nachbesserungen.md` §N8 (Priorität: kritisch, BUG).

## Fachliche Zusammenfassung

Die automatische Laufzeitermittlung (`MediaDurationProbeService` + `FfprobeMediaDurationProbe`) probt Mediendateien von SFTP-Quellen derzeit, indem der Dateiinhalt über `SftpMediaSourceReader.OpenFileStream` gestreamt und ffprobe per `pipe:0` auf stdin zugeführt wird. Da ffprobe bei MP4-Containern mit `moov`-Atom am Dateiende die **gesamte** Datei lesen muss, läuft fast jeder Remote-Probe in das 600-s-`StreamTimeoutSeconds` — der Fortschritt stagniert sichtbar ("54 von 15386") und jeder Retry verbrennt weitere 600 s.

ffprobe/ffmpeg kann `sftp://`-URLs nativ über libssh — dieser Zugriff ist **seekfähig**: ffprobe liest nur Header und moov-Atom, der Probe dauert Sekunden statt Minuten. Für `MediaSourceType.Sftp`-Items mit hinterlegten Credentials soll die Probe deshalb direkt über eine `sftp://user:pass@host:port/path`-URL laufen. Zusätzlich soll die Fehlerklassifikation stderr-basierte, endgültige Fehler (`moov atom not found`, `Invalid data found`) bei seekfähiger Eingabe als **permanent** einordnen, damit kaputte Dateien nicht 10× die Wiederholung durchlaufen.

## Betroffene Klassen und Komponenten

- `VideoWebPlayer/Services/FfprobeMediaDurationProbe.cs` — neue Probe-Art für Remote-URI (seekbar), Credential-Maskierung in Logs, stderr-Klassifikation.
- `VideoWebPlayer/Services/IMediaDurationProbe.cs` — Interface-Erweiterung um URI-Probe.
- `VideoWebPlayer/Services/MediaDurationProbeService.cs` — `ProbeItemAsync`: SFTP-Items mit Credentials über `sftp://`-URL proben; Fallback auf `pipe:0`, wenn keine Credentials oder `sftp://` nicht unterstützt.
- `VideoWebPlayer/Services/MediaDurationProbeResult.cs` — ggf. Erweiterung um eine Fehlerkategorie "Eingabe-Protokoll nicht unterstützt" (für den Pipe-Fallback).
- `VideoWebPlayer/Services/SftpMediaSourceReader.cs` — Referenz für die Pfadbildung (`CombineSftpPath(collection.Path, fileName)`); ggf. wiederverwendbarer Pfadbaustein.
- `VideoWebPlayer/Data/MediaSource.cs` — liefert `Host`, `Port`, `Username`, `Password` (keine Änderung).
- Tests: `VideoWebPlayer.Tests/Services/FfprobeMediaDurationProbeTests.cs`, `MediaDurationProbeServiceTests.cs` — neue Fälle für URI-Aufbau, URL-Encoding, Maskierung, Klassifikation, Fallback.

Nicht betroffen (bewusst): `LiveStreamInput`/`LiveStreamMediaResolver`/`FfmpegLiveStreamSegmenter` — die gleiche Pipe-Einschränkung besteht dort beim Streaming weiterhin; dieser Schritt ändert nur die Probe-Seite.

## Implementierungsansatz

- Neuer Interface-Vertrag `IMediaDurationProbe.ProbeUriAsync(string uri, CancellationToken)` (oder gleichwertig): übergibt die URL als ffprobe-Input statt Datei oder `pipe:0`.
- URL-Aufbau aus `MediaSource` (`Host`, `Port`, `Username`, `Password`) + Remote-Pfad des Items. Pfadbildung konsistent zu `SftpMediaSourceReader.OpenFileStream` (`collection.Path` + `item.Name`); `MediaItem.Path` enthält bereits den vollen SFTP-Pfad (`entry.FullName`). Sonderzeichen in Credentials und Pfad werden perzentual kodiert (`Uri.EscapeDataString` je Segment).
- **Credential-Schutz:** Das Passwort darf nicht in Logs landen — `ProbeAsync` loggt das Input-Argument und gekürztes stderr (ffprobe echot die URL in Fehlermeldungen). Sowohl Log-Anzeigename als auch stderr müssen maskiert werden (Userinfo `user:pass@` → `user:***@` bzw. `***@`).
- Das Passwort ist in der Prozess-Aufrufzeile für denselben Benutzer auf demselben System sichtbar — libssh-Protokolloptionen (`ffprobe -h protocol=sftp`: nur `timeout`, `truncate`, `private_key`) bieten keine Credential-Übergabe außerhalb der URL. Entscheidung ist zu dokumentieren (lokaler Server-/Desktop-Kontext → akzeptabel).
- **Klassifikation:** Bei Exit ≠ 0 und seekfähiger Eingabe (Datei oder URI, nicht `pipe:0`) stderr auf permanente Marker prüfen (`moov atom not found`, `Invalid data found when processing input`) → `PermanentFailure` statt `TransientFailure`. Für `pipe:0` bleibt Exit ≠ 0 transient (Stream-Abriss erzeugt dieselbe stderr-Signatur).
- **Fallback:** Kein `Host`/`Username`/`Password`, kein absoluter Remote-Pfad oder ffprobe-Build ohne libssh (`Protocol not found` in stderr) → bisheriger `pipe:0`-Pfad, idealerweise ohne einen Versuch zu verbrennen.

## Konfiguration

Keine neuen Konfigurationsschlüssel zwingend erforderlich. Der URI-Probe nutzt das bestehende `MediaProbe:TimeoutSeconds` (seekbarer Zugriff ≈ lokaler Pfad, nicht der 600-s-Stream-Timeout).

## Offene Fragen

Keine — die Designfreiheitsgrade (URL-Format, Maskierungsansatz, Fallback-Trigger) sind im Auftrag explizit delegiert („Design im Rahmen wählen, begründen").
