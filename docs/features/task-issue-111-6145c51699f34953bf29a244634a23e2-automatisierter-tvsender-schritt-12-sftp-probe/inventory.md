# Bestandsaufnahme: Seekbare Remote-Probes via `sftp://` statt `pipe:0` (N8)

Analysiert wurde die Laufzeitermittlung (`MediaDurationProbeService`, `FfprobeMediaDurationProbe`, `SftpMediaSourceReader`, Datenmodell `MediaSource`/`MediaCollection`/`MediaItem`) bezogen auf die Anforderung, SFTP-Probes seekbar über `sftp://`-URLs auszuführen und die Fehlerklassifikation zu verschärfen.

## Zusammenfassung

- `ProbeItemAsync` in `MediaDurationProbeService` verzweigt bereits nach `MediaSourceType`: `LocalDirectory` → `ProbeFileAsync(item.Path)`, alles andere → `OpenFileStream` + `ProbeStreamAsync` (`pipe:0`). Hier greift die Anforderung an.
- `FfprobeMediaDurationProbe` hat einen sauberen Testseam (`_executeAsync`-Delegat) und ein zentrales `ProbeAsync`, in dem Timeout, Exit-Code und stdout gemappt werden — geeigneter Punkt für die stderr-basierte Klassifikation und die Maskierung (loggt heute `inputArgument` + gekürztes stderr).
- `MediaSource` trägt `Host`/`Port`/`Username`/`Password`; `MediaItem.Path` enthält bei SFTP bereits den vollen Remote-Pfad, `OpenFileStream` bildet ihn über `CombineSftpPath(collection.Path, item.Name)`.
- ffprobe (Gyan full build, `--enable-libssh`) unterstützt `sftp://` nativ mit Seek; Protokolloptionen sind nur `timeout`/`truncate`/`private_key` — Credentials lassen sich **nur** über URL-Userinfo übergeben (keine `-user`/`-password`-Option, keine Env-Variable).
- `MediaDurationProbeResult` kennt nur `Duration` + `Transient` — für „`sftp` nicht unterstützt → Pipe-Fallback im selben Versuch" wird eine dritte Information nötig.
- Fehlt noch: URI-Probe-Methode am Interface, URL-Aufbau mit Encoding, Credential-Maskierung (Log-Input **und** stderr), permanente Klassifikation für `moov atom not found`/`Invalid data found` bei seekfähigen Inputs, Fallback-Logik.

Test-Ausgangszustand: Baseline 2014 Tests — 2012 bestanden, 1 bekannter E2E-Flake (`PlaylistSkipLockedEntriesE2ETest`, Einzelretry grün), 1 bekannter Alt-Skip. Details und Nachweise: [inventory/tests.md](inventory/tests.md).

## Details

- [Datenmodell](inventory/models.md)
- [Logik](inventory/logic.md)
- [Interfaces](inventory/interfaces.md)
- [Tests](inventory/tests.md)
