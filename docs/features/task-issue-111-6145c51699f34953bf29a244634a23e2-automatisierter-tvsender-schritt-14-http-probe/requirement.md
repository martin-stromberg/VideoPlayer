# Übersetzte Anforderung: Remote-Laufzeit-Probes über eigenen HTTP-Stream-Endpunkt (N9)

## Fachliche Zusammenfassung

Der `MediaDurationProbeWorker` soll Remote-MediaItems (SFTP- und andere Nicht-Local-Quellen)
nicht mehr primär über `pipe:0` (Komplett-Streaming) oder `sftp://` (libssh, live als
inkompatibel verifiziert) proben, sondern über den eigenen Range-fähigen
HTTP-Stream-Endpunkt `GET /api/items/{type}/{id}/stream` (`ItemsController`,
`enableRangeProcessing: true`). ffprobe kann über HTTP mit Range-Requests seeken und liest
dadurch nur Header/`moov`-Atom statt der kompletten Datei — derselbe SSH.NET-Lesepfad wie
die Wiedergabe, quelltyp-unabhängig, ohne libssh. `sftp://` bleibt als Pfad für kompatible
Server erhalten, `pipe:0` bleibt letzter Fallback.

## Betroffene Klassen und Komponenten

- `FfprobeMediaDurationProbe` (`ProbeFileAsync`/`ProbeStreamAsync`/`ProbeUriAsync`,
  Seekable-Flag, TimeoutSplit) — neuer HTTP-Probe-Pfad
- `MediaDurationProbeService` — Auswahl der Probe-Strategie je MediaItem/Quelltyp,
  URL-Aufbau, Fallback-Reihenfolge (HTTP → sftp:// → pipe)
- `MediaProbeOptions` — ggf. neue Optionen (Loopback-URL/-Port, HTTP-Probe an/aus, Timeouts)
- `ItemsController` (`StreamMediaItem`/`OpenMediaFileAsync`, `CheckLogedIn`,
  `access_token`-Query-Handling, `BearerTokenCheckAttribute`) — Auth-Verständnis,
  `{type}/{id}`-Auflösung (movie vs. episode)
- `ApiBaseController`
- Auth-Infrastruktur: `DeviceTokenService`, `RefreshTokenService`,
  `BearerTokenCheckAttribute` — Wiederverwendung oder minimaler interner
  Kurzzeit-Token-Dienst für den Server-Self-Call
- Mapping `MediaItem` → `{type}/{id}`: `MovieMediaItems` (Movie ↔ MediaItem),
  `TVShowEpisodes.MediaItemId` (Episode ↔ MediaItem)
- Eigenadresse der laufenden Instanz: `IServerAddressesFeature` / `ASPNETCORE_URLS` /
  Kestrel-Konfiguration — Ermittlung der Loopback-Basis-URL
- Tests: Unit-Tests URL-Aufbau/Fallback/Token, Integrationstest mit echtem ffprobe gegen
  den Range-Endpunkt (falls Testserver-Infrastruktur vorhanden)

## Implementierungsansatz

- Im `MediaDurationProbeService` für Nicht-Local-Items zuerst eine Loopback-HTTP-URL der
  Form `http://127.0.0.1:{port}/api/items/{type}/{id}/stream?access_token={token}` an
  `FfprobeMediaDurationProbe.ProbeUriAsync` übergeben.
- Interner, kurzlebiger Token für den Self-Call — vorhandene Mechanismen prüfen
  (`access_token`-Query-Handler, `DeviceTokenService`); kein langlebiger User-Token in
  Prozess-Argumenten. Falls nichts passt: minimaler interner Token-Dienst (In-Memory,
  einmalig/kurzlebig, nur für den Stream-Endpunkt).
- Loopback-Basis-URL aus der laufenden Server-Konfiguration ermitteln
  (`IServerAddressesFeature` via `IServer`, Fallback konfigurierbar über
  `MediaProbeOptions`, sinnvoller Default `http://127.0.0.1:5039` nur als letzter
  Rückfall oder gar kein Default — Entscheidung im Plan).
- Mapping MediaItem → Route: `movie` über `MovieMediaItems`, `episode` über
  `TVShowEpisodes.MediaItemId` — exakt wie `ItemsController` die Route auflöst.
- Fallback-Reihenfolge: HTTP-Probe → `sftp://` (nur Sftp-Quellen, bestehender Pfad) →
  `pipe:0`; Reihenfolge im Plan begründen.
- ffprobe-HTTP-Optionen: `timeout`/`rw_timeout` sinnvoll setzen; Range-Support des
  Endpunkts per Integrationstest verifizieren.

## Konfiguration

- `MediaProbeOptions`: Schalter für HTTP-Probe (Default: an, mit Fallback wenn Self-Call
  nicht erreichbar), optional explizite Loopback-Basis-URL-Override,
  Timeout-Werte für den HTTP-Pfad.

## Offene Fragen

- Keine fachlich offenen Fragen — die technischen Detailentscheidungen (Token-Mechanismus,
  URL-Ermittlung, exakte Fallback-Reihenfolge) werden im Plan anhand der Bestandsaufnahme
  getroffen und begründet.
