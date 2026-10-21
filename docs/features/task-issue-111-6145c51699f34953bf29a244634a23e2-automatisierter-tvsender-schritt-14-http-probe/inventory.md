# Bestandsaufnahme: Remote-Laufzeit-Probes über eigenen HTTP-Stream-Endpunkt (N9)

Analyse der ffprobe-Laufzeitermittlung, des Stream-Endpunkts, der Token-/Auth-Infrastruktur
und der Server-Adress-Ermittlung, bezogen auf `requirement.md`.

## Zusammenfassung

- `MediaDurationProbeService.ProbeRemoteItemAsync` probiert für SFTP-Quellen zuerst eine
  `sftp://`-URI via `ProbeUriAsync` und fällt bei `UriUnsupported`/nicht baubarer URI auf
  `pipe:0` (`ProbeStreamAsync`) zurück. Ein HTTP-Pfad existiert noch nicht.
- `FfprobeMediaDurationProbe.ProbeUriAsync` ist generisch (beliebige URI), maskiert nur
  Userinfo (`user:pass@`) in Logs — Query-Parameter wie `access_token` würden **nicht**
  maskiert (wichtig für das Design: Token liegt im URI-Pfad/Query).
- Der Stream-Endpunkt `GET api/items/{type}/{id}/stream` (`ItemsController:663`) nutzt
  `File(stream, contentType, enableRangeProcessing: true)` über
  `IMediaSourceReader.OpenFileStream`. `SftpMediaSourceReader.OpenFileStream` liefert
  `SftpStreamWrapper` (seekfähig, delegiert an `SftpFileStream`) → Range-Requests seeken
  über denselben SSH.NET-Pfad wie die Wiedergabe.
- Auth: `BearerTokenCheckAttribute` akzeptiert `Authorization`-Header **oder**
  `?access_token=` (JWT, `SymmetricSecurityKey` + `Jwt:Issuer` aus DI/Konfiguration).
  `CheckLogedIn` benötigt `IAuthService.CurrentUser` (UserManager-Lookup per
  `NameIdentifier`-Claim) — ein interner Token müsste also einen **echten Benutzer**
  abbilden, und `EnsureAccessAsync` verlangt `MediaSourceUsers`- oder
  `UnlockedMediaEntries`-Zeile für diesen Benutzer. Es gibt keinen dedizierten
  internen/maschinellen Token-Mechanismus.
- Mapping `MediaItem` → `{type}/{id}`: `MovieMediaItems.MediaItemId → movie/{MovieId}`,
  `TVShowEpisodeMediaItems.MediaItemId → tvshowepisode/{TVShowEpisodeId}`.
  `ApplicationDbContext.GetRelationsForMediaItemAsync` löst genau das bereits auf.
  Achtung: nicht klassifizierte `MediaItem`s (ohne Movie-/Episode-Link) sind über den
  bestehenden Endpunkt **nicht** adressierbar.
- Eigenadresse: `IServer.Features.Get<IServerAddressesFeature>().Addresses` (Pattern in
  `PlaylistsE2ETestBase`), konfiguriert über `ASPNETCORE_URLS`/`UseUrls`/
  `Kestrel:Endpoints:*:Url` (`appsettings.Production.json`: `http://*:5002`).
- Testinfrastruktur: `WebApplicationFactory<Program>` mit `UseKestrel()` +
  `UseUrls("http://127.0.0.1:0")` existiert (u. a. `PlaylistsE2ETestBase`,
  `LocalMediaSourceStreamingE2ETests` für den Stream-Endpunkt über TestServer).
  `SeedMovieAsync`-Pattern (User + `MediaSourceUser` + Movie + `MediaItem` +
  `MovieMediaItem`) ist vorhanden. ffprobe 9.0.2 liegt auf PATH (kein Binary im Repo).
- Test-Ausgangszustand: **2055 bestanden, 0 Fehler, 1 übersprungen** (nach TEMP-Umleitung
  auf D: — C: war zu 100 % voll, erster Lauf mit 42 `SQLite Error 13`-Fehlschlägen war
  rein umgebungsbedingt). Nachweis: `inventory/tests.md`, Logs unter
  `inventory/test-results/`.

## Details

- [Datenmodell](inventory/models.md)
- [Logik](inventory/logic.md)
- [Interfaces](inventory/interfaces.md)
- [Tests](inventory/tests.md)
