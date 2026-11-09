# Tasks: Seekbare Remote-Probes via `sftp://` statt `pipe:0` (N8)

| # | Bereich | Aufgabe | Status | Testnachweis |
|---|---------|---------|--------|--------------|
| 1 | Logik | `SftpProbeUriBuilder` anlegen (`TryBuildUri`, `MaskCredentials`, `MaskCredentialsInText`) | Offen | — |
| 2 | Logik | `MediaDurationProbeResult` um `UriUnsupported` + `UriUnsupportedFailure` erweitern | Offen | — |
| 3 | Interface | `IMediaDurationProbe.ProbeUriAsync` deklarieren | Offen | — |
| 4 | Logik | `FfprobeMediaDurationProbe`: `ProbeUriAsync` + `ProbeAsync` um `displayInput`/`seekable`/stderr-Maskierung/permanente Marker/`Protocol not found`-Erkennung erweitern | Offen | — |
| 5 | Logik | `MediaDurationProbeService.ProbeItemAsync`: SFTP-URI-Probe mit Pipe-Fallback + `ResolveRemotePath` | Offen | — |
| 6 | Tests | `SftpProbeUriBuilderTests` (Aufbau, Encoding, IPv6, Port, Maskierung, `null`-Fälle) | Offen | — |
| 7 | Tests | `FfprobeMediaDurationProbeTests`: URI-Probe, Timeout, Klassifikation (permanent seekbar / transient pipe), `UriUnsupported`-Cache, Passwort-freie Logs | Offen | — |
| 8 | Tests | `MediaDurationProbeServiceTests`: `FakeDurationProbe.ProbeUriAsync`, Credentials-URI-Pfad, `UriUnsupported`-Fallback, relativer-Pfad-Fallback | Offen | — |
| 9 | Doku | `docs/help/videolaufzeit.md`: sftp://-Probe, Credential-Sichtbarkeit, Fallback, Klassifikation | Offen | — |
| 10 | Doku | `docs/RELEASE_NOTES.md` (EN+DE) + ggf. `README.md` | Offen | — |
