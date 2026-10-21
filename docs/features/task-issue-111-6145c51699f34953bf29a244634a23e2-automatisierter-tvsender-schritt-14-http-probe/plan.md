# Umsetzungsplan: Remote-Laufzeit-Probes über eigenen HTTP-Stream-Endpunkt (N9)

## Übersicht

Remote-`MediaItem`s werden per ffprobe über einen neuen, server-internen HTTP-Stream-Endpunkt
geprobt (`http://127.0.0.1:{port}/api/internal/media-stream/{token}`), der die Datei über
denselben `IMediaSourceReader`-Pfad wie die Wiedergabe mit Range-Support ausliefert. ffprobe
seekt per HTTP-Range und liest nur Container-Header — kein libssh, kein Komplett-Download.
Die bisherige Kaskade `sftp://` → `pipe:0` bleibt als Fallback bestehen.

## Designentscheidungen

| Komponente / Bereich | Gewählter Ansatz | Begründung |
|----------------------|------------------|------------|
| Endpunkt-Form | **Neuer interner Endpunkt** `api/internal/media-stream/{token}` (token = kurzlebige, pro MediaItem ausgestellte Capability) statt `api/items/{type}/{id}/stream?access_token={jwt}` | Der bestehende Endpunkt verlangt `CheckLogedIn` + `EnsureAccessAsync` → ein interner JWT müsste einen **echten Benutzer** abbilden, der Zugriff auf *alle* Quellen hat — gibt es nicht garantiert; ein Claim-Bypass würde die geteilten Auth-Pfade (`CheckLogedIn`, `EnsureAccessAsync`) schwächen. Der interne Endpunkt bricht den User-Auth-Pfad gar nicht erst an, adressiert **jedes** `MediaItem` (auch nicht klassifizierte ohne `MovieMediaItem`/`TVShowEpisodeMediaItem`-Link — die bestehende Route kann diese nicht bedienen) und scopet den Token auf genau eine Datei. |
| Token-Mechanismus | Neuer Singleton `IInternalMediaStreamTokenService`: kryptografisch zufälliges Token (256 Bit, Base64Url) → `mediaItemId`, In-Memory-`ConcurrentDictionary`, TTL konfigurierbar (Default 600 s), Mehrfachverwendung innerhalb der TTL erlaubt | Kein vorhandener Mechanismus passt (`AuthorizationTokenService` = User-JWT 12 h; `DeviceTokenService` = Geräte-Pairing). Ein in-memory Capability-Token ist minimal: kein DB-Zugriff, kein langlebiges Geheimnis in Prozess-Args, ungültig nach Prozessneustart (harmlos — Probe gilt als transienter Fehler). Mehrfachverwendung nötig, weil ffprobe für Range-Requests mehrere HTTP-Requests auf dieselbe URI ausführen kann. |
| Loopback-Basis-URL | Auto-Erkennung via `IServer.Features.Get<IServerAddressesFeature>()` (erstes `http://`-Address), Host-Normalisierung `*`/`+`/`0.0.0.0`/`[::]`/`localhost` → `127.0.0.1`; Override `MediaProbe:HttpProbeBaseUrl`; kein `http`-Address → HTTP-Probe deaktiviert (Fallback) | `IServerAddressesFeature` ist die kanonische Quelle der gebundenen Adressen (Pattern in `PlaylistsE2ETestBase`) und deckt `ASPNETCORE_URLS`/`UseUrls`/`Kestrel:Endpoints` ab. Nur `http` (kein `https`): selbstsignierte/Entwicklungszertifikate würden ffprobes TLS-Verifikation brechen; `https`-Only-Setups nutzen weiterhin sftp/pipe. `DiscoveryBaseUrlResolver` wird nicht wiederverwendet — er dient der externen Discovery-Werbung und kann externe Hostnamen liefern; der Self-Call braucht zwingend Loopback. |
| Fallback-Reihenfolge | **HTTP-Probe → `sftp://` → `pipe:0`** je Versuch | HTTP ist der einzige Pfad, der zuverlässig seekt (SSH.NET-Read-Pfad wie Wiedergabe). `sftp://` bleibt für kompatible Server sinnvoll (direkter libssh-Seek ohne App-Roundtrip), fällt bei inkompatiblen libssh-Builds in Sekunden auf stderr-Fehler. `pipe:0` bleibt letzte Sicherung (z. B. HTTP-Endpunkt nicht erreichbar). Ein *Ergebnis* des HTTP-Probes (Success/Transient/Permanent) wird **nicht** durch weitere Pfade überschrieben — nur wenn der HTTP-Pfad selbst nicht nutzbar ist (deaktiviert, keine URL baubar, `UriUnsupported`, HTTP-Transportfehler), wird zur nächsten Stufe übergegangen. |
| HTTP-Transportfehler-Klassifikation | In `FfprobeMediaDurationProbe`: bei `http(s)`-URIs stderr-Marker (`Connection refused`, `Connection timed out`, `No route to host`, `HTTP error`, `Server returned`) → `UriUnsupportedFailure` → Service fällt zurück | Trennt „Endpunkt/Token-Infrastruktur kaputt" (→ anderer Pfad versuchen) von „Datei kaputt" (Permanent) und „Lauf zu langsam" (Timeout → Transient, Retry später). So bleibt die App auch bei systemisch unerreichbarem Loopback funktionsfähig — gleiches Verhalten wie heute (sftp→pipe). `_uriProtocolUnsupported` wird bei Transportfehlern **nicht** gesetzt (kein `Protocol not found`). |
| ffprobe-Aufrufparameter | Unverändert (`BuildArguments`); HTTP-URI-Probe läuft mit `TimeoutSeconds` (seekfähig, wie sftp) | ffmpeg seekt über HTTP-Range automatisch (Accept-Ranges/206); keine Zusatzoptionen nötig. |
| Token-Platzierung in der URL | Token als **Pfadsegment** (`…/{token}`) | Der `access_token`-Query-Mechanismus gehört der JWT-Route; ein eigener Pfad macht klar, dass es kein User-Token ist, und verhindert Verwechslung/Missbrauch an anderen Endpunkten. Der Token steht in Prozess-Args und Logs — akzeptabel: nur lokale Benutzer sichtbar, Scope = 1 Datei, TTL ≤ 15 min (analog dokumentierter Passwort-Sichtbarkeit beim sftp-Pfad). |

## Programmabläufe

### Remote-Probe über internen HTTP-Endpunkt

1. `MediaDurationProbeService.ProbeItemAsync` → `ProbeRemoteItemAsync` (Nicht-Local-Item).
2. Ist `IInternalMediaStreamTokenService` verfügbar, baut
   `TryCreateProbeUri(item.Id)` die URI: Basis-URL auflösen (Option → `IServerAddressesFeature`,
   Normalisierung auf `127.0.0.1`), Token ausstellen (TTL), Pfad
   `/api/internal/media-stream/{token}` anhängen → `http://127.0.0.1:{port}/api/internal/media-stream/{token}`.
3. `FfprobeMediaDurationProbe.ProbeUriAsync(httpUri)` → ffprobe macht Range-Requests gegen
   den laufenden Server.
4. `InternalMediaStreamController.Stream` löst Token → `mediaItemId` auf (404 bei
   unbekannt/abgelaufen), lädt `MediaItem`+`MediaCollection`+`MediaSource`, öffnet
   `_reader.OpenFileStream(collection, Path.GetFileName(item.Path))`, liefert
   `File(stream, contentType, enableRangeProcessing: true)` → ASP.NET bedient ffprobes
   `Range`-Header seekfähig (SFTP: `SftpFileStream.Seek` via SSH.NET).
5. Ergebnis-Weiche im Service: `UriUnsupported` (inkl. HTTP-Transportfehler) → weiter mit
   `sftp://`/`pipe:0`; sonst Ergebnis direkt zurück (Success/Transient/Permanent).

Beteiligte Klassen: `MediaDurationProbeService`, `IInternalMediaStreamTokenService`,
`InternalMediaStreamTokenService`, `InternalMediaStreamController`, `IMediaSourceReader`,
`FfprobeMediaDurationProbe`, `MediaProbeOptions`.

### Bestehende Kaskade (unverändert als Fallback)

`sftp://` (`SftpProbeUriBuilder`, nur SFTP+Credentials+absoluter Pfad) → `pipe:0`
(`_reader.OpenFileStream` + `ProbeStreamAsync`) — wie heute.

## Neue Klassen

| Klasse | Typ | Zweck |
|--------|-----|-------|
| `IInternalMediaStreamTokenService` | Interface | `TryCreateProbeUri(long mediaItemId)` → `string?` (null wenn deaktiviert/keine http-Basis-URL); `long? ResolveMediaItemId(string token)` |
| `InternalMediaStreamTokenService` | Singleton (Service) | Token-Ausstellung/-Auflösung (In-Memory, TTL via `TimeProvider`, Lazy-Cleanup) + Loopback-URL-Aufbau (`IServer`, `IOptions<MediaProbeOptions>`, `ILogger`) |
| `InternalMediaStreamController` | API-Controller | `GET api/internal/media-stream/{token}` — kein `BearerTokenCheck`; Token-Validierung → Dateistream mit `enableRangeProcessing` |
| `MediaFileContentTypes` | internal static Helper | Dateiendung → Content-Type (aus `ItemsController.OpenMediaFileAsync` extrahiert, dort weiterverwendet) |

## Änderungen an bestehenden Klassen

### `MediaDurationProbeService` (Service)

- **Neue Abhängigkeit:** optionaler Ctor-Parameter `IInternalMediaStreamTokenService?` —
  `null` (Tests/ohne Registrierung) → HTTP-Pfad übersprungen, Verhalten wie bisher.
- **Geänderte Methoden:** `ProbeRemoteItemAsync` — vor dem sftp-Block zuerst HTTP-URI
  bilden und proben; `UriUnsupported` → Weiterfall auf bestehende Kaskade.

### `FfprobeMediaDurationProbe` (`IMediaDurationProbe`)

- **Geänderte Methoden:** `ProbeAsync` — zusätzliche stderr-Marker-Liste
  `HttpTransportErrorMarkers`; bei `http(s)`-Eingabe-URI und Treffer →
  `UriUnsupportedFailure` (ohne `_uriProtocolUnsupported` zu setzen).
- XML-Doku von `ProbeUriAsync` (Interface + Impl) um HTTP-Hinweis ergänzen.

### `ItemsController`

- **Geänderte Methoden:** `OpenMediaFileAsync` — Content-Type-Switch durch
  `MediaFileContentTypes.ForFileName(fileName)` ersetzt (Verhalten identisch).

### `MediaProbeOptions` (Konfiguration)

- **Neue Eigenschaften:**
  - `HttpProbeEnabled` (`bool`, Default `true`) — HTTP-Self-Probe an/aus
  - `HttpProbeBaseUrl` (`string?`, Default `null`) — Override der Loopback-Basis-URL
  - `HttpProbeTokenLifetimeSeconds` (`int`, Default `600`) — Token-TTL
  - `EffectiveHttpProbeTokenLifetimeSeconds` (`int`) — normalisiert (>0 sonst 600)

### `ServiceCollectionExtensions`

- `services.AddSingleton<IInternalMediaStreamTokenService, InternalMediaStreamTokenService>()`

## Datenbankmigrationen

Keine.

## Validierungsregeln

| Feld / Objekt | Regel | Fehlerfall |
|---------------|-------|------------|
| Token (Route) | muss im Store vorhanden und nicht abgelaufen sein | 404 (kein Detail-Leak) |
| `mediaItemId` aus Token | MediaItem + Collection + lesbarer Stream muss existieren | 404 wie bei ungültigem Token |
| `HttpProbeBaseUrl` | falls gesetzt: muss `http(s)`-Absolute-URI sein, sonst ignorieren + Warnung | Option wird ignoriert, Auto-Erkennung greift |

## Konfigurationsänderungen

| Eintrag | Typ | Standardwert | Zweck |
|---------|-----|--------------|-------|
| `MediaProbe:HttpProbeEnabled` | bool | `true` | Schaltet den HTTP-Self-Probe-Pfad |
| `MediaProbe:HttpProbeBaseUrl` | string? | `null` | Explizite Loopback-Basis-URL (z. B. `http://127.0.0.1:5039`) für Fälle, in denen die Auto-Erkennung nicht greift |
| `MediaProbe:HttpProbeTokenLifetimeSeconds` | int | `600` | Gültigkeitsdauer eines internen Stream-Tokens |
| `appsettings.json` | — | — | die drei Schlüssel im `MediaProbe`-Abschnitt ergänzen |

## Seiteneffekte und Risiken

- **Neuer anonymer Endpunkt:** `api/internal/media-stream/{token}` ist ohne `BearerTokenCheck`
  erreichbar — Absicherung ausschließlich über den 256-Bit-Zufallstoken (pro MediaItem,
  kurzlebig, In-Memory). Risiko: Wer den Token mitliest (Prozessliste/Log desselben Hosts),
  kann ≤ TTL die eine Datei streamen — dokumentiert, analog zur Passwort-Sichtbarkeit beim
  sftp-Pfad. Kein `MediaSourceUsers`-/`UnlockedMedia`-Check nötig: das ist der Sinn des
  internen Pfades (darf nur der Server selbst auslösen).
- **Zusatzlast auf dem eigenen Server:** ffprobe-Range-Requests gehen durch die normale
  Request-Pipeline; bei `ParallelProbes` entsprechend viele parallele Self-Calls + offene
  `OpenFileStream`-Streams (SFTP-Verbindungen) — wie bei echten Wiedergaben, begrenzt durch
  `ParallelProbes`.
- **Token-Store-Wachstum:** bounded durch TTL+Lazy-Cleanup (Batch-Größen × TTL ≈ Tausende
  Einträge max — unkritisch).
- **`pipe:0`-Semantik unverändert:** keine Änderung an Timeout/Fehlerklassifikation dort.
- **HTTPS-only-Installationen:** HTTP-Probe nicht nutzbar → automatischer Fallback auf
  bisheriges Verhalten (dokumentiert).
- **Reverse-Proxy/gebundene Sonderfälle:** Auto-Erkennung kann fehlschlagen →
  `HttpProbeBaseUrl`-Override als Ausweg.
- **ItemsController-Refactoring:** Content-Type-Switch wird ausgelagert — identische
  Zuordnung, bestehende Tests (`LocalMediaSourceStreamingE2ETests` u. a.) decken das ab.

## Umsetzungsreihenfolge

1. **`MediaProbeOptions` erweitern** (`HttpProbeEnabled`, `HttpProbeBaseUrl`,
   `HttpProbeTokenLifetimeSeconds`, `EffectiveHttpProbeTokenLifetimeSeconds`) +
   `appsettings.json`-Schlüssel
   - Voraussetzungen: keine
2. **`MediaFileContentTypes`-Helper** anlegen, `ItemsController.OpenMediaFileAsync` umstellen
   - Voraussetzungen: keine
3. **`IInternalMediaStreamTokenService` + `InternalMediaStreamTokenService`**
   (Token-Store mit `TimeProvider`, Basis-URL-Auflösung mit `IServer`)
   - Voraussetzungen: Schritt 1
4. **`InternalMediaStreamController`** anlegen
   - Voraussetzungen: Schritte 2, 3
5. **`FfprobeMediaDurationProbe`**: `HttpTransportErrorMarkers` → `UriUnsupportedFailure`
   - Voraussetzungen: keine
6. **`MediaDurationProbeService`**: optionale Abhängigkeit + HTTP-first in
   `ProbeRemoteItemAsync`; `ServiceCollectionExtensions`-Registrierung
   - Voraussetzungen: Schritte 3, 5
7. **Unit-Tests** (Token-Service, URL-Aufbau, Service-Fallback-Logik, Probe-Marker,
   Controller)
   - Voraussetzungen: Schritte 1–6
8. **Integrationstest** mit echtem Kestrel + echtem ffprobe (skip wenn Binary fehlt)
   - Voraussetzungen: Schritte 1–6
9. **Dokumentation** (`docs/help/videolaufzeit.md`, `RELEASE_NOTES.md`, ggf. README)
   - Voraussetzungen: Schritte 1–8

## Tests

### Neue Tests

| Test / Hilfsmethode | Testklasse | Was wird geprüft / bereitgestellt? |
|--------------------|------------|-------------------------------------|
| `TryCreateProbeUri_ReturnsLoopbackUriWithToken` | `InternalMediaStreamTokenServiceTests` | Adresse `http://*:5002` → `http://127.0.0.1:5002/api/internal/media-stream/{token}`; `ResolveMediaItemId` liefert Item-Id |
| `TryCreateProbeUri_NormalizesWildcardAndLocalhost` | dto. | `*`, `+`, `0.0.0.0`, `[::]`, `localhost` → `127.0.0.1`; Port bleibt |
| `TryCreateProbeUri_NoHttpAddress_ReturnsNull` | dto. | nur `https://` → null |
| `TryCreateProbeUri_Disabled_ReturnsNull` | dto. | `HttpProbeEnabled=false` → null |
| `TryCreateProbeUri_Override_UsesConfiguredBaseUrl` | dto. | `HttpProbeBaseUrl` schlägt Auto-Erkennung; ungültiger Wert → Warnung + Auto |
| `ResolveMediaItemId_UnknownOrExpired_ReturnsNull` | dto. | unbekanntes Token → null; TTL abgelaufen (TimeProvider-Fake) → null |
| `ProbePendingAsync_RemoteItem_ProbesViaHttpUri` | `MediaDurationProbeServiceTests` | Stub-Token-Service → `ProbeUriAsync` mit `http://127.0.0.1`-URI; kein Stream/Reader-Call |
| `ProbePendingAsync_HttpUriUnsupported_FallsBackToSftpOrPipe` | dto. | `UriResult=UriUnsupported` → sftp-URI bzw. Stream-Pfad wie bisher |
| `ProbePendingAsync_HttpProbeSuccess_NoFurtherFallback` | dto. | HTTP-Success → kein sftp-URI-Call, kein `OpenFileStream` |
| `ProbePendingAsync_TokenServiceUnavailable_FallsBackToSftp` | dto. | Token-Service null/`TryCreateProbeUri`→null → Verhalten wie Baseline |
| `ProbePendingAsync_LocalItem_NoHttpProbe` | dto. | LocalDirectory → weiter `ProbeFileAsync`, kein Token-Service-Call |
| `ProbeUriAsync_HttpTransportError_ReturnsUriUnsupported` | `FfprobeMediaDurationProbeTests` | stderr `HTTP error 404`/`Connection refused` → `UriUnsupported`, Flag nicht gesetzt (nächster Probe läuft) |
| `Stream_ValidToken_ReturnsFileWithRangeSupport` | `InternalMediaStreamControllerTests` | TestServer/WebApplicationFactory: gültiges Token → 200, `Accept-Ranges`, Range-Request → 206 |
| `Stream_InvalidToken_Returns404` | dto. | Unbekanntes/abgelaufenes Token → 404 |
| `HttpProbe_RangeEndpoint_ReturnsDuration` | `MediaProbeHttpIntegrationTests` (Category `E2E`) | echter Kestrel auf `127.0.0.1:0`, Local-Quelle mit echter Videodatei (per ffmpeg erzeugt, sonst Skip), `TryCreateProbeUri` + realer `FfprobeMediaDurationProbe` → `Duration` ≈ erzeugte Dauer — beweist Ende-zu-Ende: Range + SSH-loser Read-Pfad + Token |

Hilfsmethoden: Fake-`IServer`/`IServerAddressesFeature` (konfigurierbare `Addresses`),
Stub-`IInternalMediaStreamTokenService` (rückgabesteuerbare URI, Aufrufzähler),
Video-Erzeugungs-Helper (ffmpeg `-f lavfi`, Skip bei fehlendem Binary).

### Betroffene bestehende Tests

| Test / Testklasse | Grund der Anpassung |
|-------------------|---------------------|
| `MediaDurationProbeServiceTests` (Fixture) | optionaler neuer Ctor-Parameter — Fake-Token-Service in DI ergänzen (Default null → vorhandene Fälle unverändert, Verhalten bleibt Baseline); `SeedSftpItemAsync`-Pattern wiederverwenden |
| `FfprobeMediaDurationProbeTests` | neue Marker-Fälle ergänzen; `Protocol not found`-Flag-Verhalten unverändert |

### E2E-Tests (primärer Funktionsnachweis)

Die Anforderung betrifft einen reinen **Hintergrund-Workflow** (kein Benutzerfluss, keine
UI). Der Benutzer-sichtbare Teil (Fortschrittsanzeige) bleibt unverändert. E2E-Nachweis
daher auf Systemebene statt Browser: der `MediaProbeHttpIntegrationTests`-Fall
(Kestrel+ffprobe echt) ist der primäre Funktionsnachweis des HTTP-Probe-Pfads; der
Controller-Range-Test deckt die Range-Fähigkeit des neuen Endpunkts über echten HTTP.
Browser-E2E ist nicht erforderlich — keine neue oder geänderte Benutzerinteraktion.

| Priorität | Szenario | Testdatei / Testklasse | Abgedecktes Akzeptanzkriterium | Warum E2E nötig ist |
|-----------|----------|------------------------|-------------------------------|-------------------|
| Pflicht | ffprobe probed per HTTP-Range über internen Endpunkt | `MediaProbeHttpIntegrationTests` | ffprobe liest Laufzeit einer echten Datei über `http://127.0.0.1`-Loopback mit Token-URL | Nachweis, dass Range-Support + Token + Self-Call real funktionieren — Unit-Tests können ffmpeg-Range-Verhalten nicht belegen |
| Pflicht | Token-Gate des internen Endpunkts | `InternalMediaStreamControllerTests` | ungültiges/abgelaufenes Token → 404, gültiges → 200/206 | Absicherung des einzigen Schutzes des anonymen Endpunkts |

## Offene Punkte

Keine.
