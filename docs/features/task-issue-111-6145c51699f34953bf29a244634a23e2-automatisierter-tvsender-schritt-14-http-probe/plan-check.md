# Plan-Gegenprüfung

## Ergebnis

**Status:** Plan vollständig

## Abgleich Akzeptanzkriterien

| Akzeptanzkriterium | Umsetzung im Plan | Testnachweis im Plan | Status |
|--------------------|-------------------|----------------------|--------|
| Remote-Items per HTTP-Loopback-URL proben (seekfähig, nur Header) | `ProbeRemoteItemAsync` baut `http://127.0.0.1:{port}/api/internal/media-stream/{token}` via `IInternalMediaStreamTokenService`, `ProbeUriAsync` | `ProbePendingAsync_RemoteItem_ProbesViaHttpUri`, `MediaProbeHttpIntegrationTests` | Abgedeckt |
| Loopback-Port aus laufender Konfiguration (`IServerAddressesFeature`/`ASPNETCORE_URLS`), Option in `MediaProbeOptions` | Auto-Erkennung `IServerAddressesFeature` + `HttpProbeBaseUrl`-Override + `HttpProbeEnabled` | `TryCreateProbeUri_*`-Tests inkl. Normalisierung, Disabled, Override | Abgedeckt |
| Interner kurzlebiger Token, kein langlebiger User-Token in Prozess-Args | `InternalMediaStreamTokenService`: 256-Bit-Token → MediaItemId, TTL 600 s, In-Memory; kein JWT/User | `ResolveMediaItemId_UnknownOrExpired_ReturnsNull`, Controller-404-Test | Abgedeckt |
| Mapping MediaItem → Adressierung | **Abweichung begründet:** interner Endpunkt adressiert `MediaItemId` direkt im Token statt `{type}/{id}` — deckt auch nicht klassifizierte Items ab, keine User-Auth nötig | Controller-/Integrationstests | Abgedeckt |
| Fallback-Reihenfolge begründet | HTTP → sftp:// → pipe:0; Ergebnis-Weiche dokumentiert | `…_HttpUriUnsupported_FallsBackToSftpOrPipe`, `…_HttpProbeSuccess_NoFurtherFallback`, `…_TokenServiceUnavailable_FallsBackToSftp` | Abgedeckt |
| `pipe:0` bleibt letzter Fallback, `sftp://` bleibt bestehen | Kaskade unverändert hinter dem HTTP-Pfad | vorhandene `…_UriUnsupported_FallsBackToStream`, `…_StreamsViaOpenFileStream` bleiben grün | Abgedeckt |
| Echter Integrationstest wo möglich | `MediaProbeHttpIntegrationTests` (Kestrel echt + ffprobe echt, Skip ohne Binary) + Controller-Range-Test | beide im Plan | Abgedeckt |
| Kein Abbruch bei systemisch unerreichbarem Loopback | HTTP-Transportfehler-Marker → `UriUnsupported` → Fallback | `ProbeUriAsync_HttpTransportError_ReturnsUriUnsupported` | Abgedeckt |

## Fehlende oder unvollständige Testanforderungen

Keine — Status ist `Plan vollständig`.

## E2E-Abdeckung

| Benutzerfluss / Akzeptanzkriterium | Geplanter E2E-Test | Status |
|------------------------------------|--------------------|--------|
| Ende-zu-Ende-Probe über HTTP-Range | `MediaProbeHttpIntegrationTests.HttpProbe_RangeEndpoint_ReturnsDuration` | Abgedeckt (System-E2E statt Browser — kein UI-Flow, Begründung im Plan) |
| Token-Absicherung interner Endpunkt | `InternalMediaStreamControllerTests` | Abgedeckt |
| Browser-UI-Fluss | — | Nicht erforderlich: reine Hintergrund-Änderung ohne Benutzerinteraktion; Fortschrittsanzeige unverändert |

## Fehlende oder unvollständige Planbestandteile

Keine.

## Hinweise

- Der Plan weicht bewusst vom in N9 skizzierten `api/items/{type}/{id}/stream?access_token=`-Pfad
  ab: Der bestehende Endpunkt erfordert einen echten Benutzer mit `MediaSourceUsers`-/
  `UnlockedMedia`-Zeile (kein garantierter „Super-Benutzer" vorhanden) und kann nicht
  klassifizierte `MediaItem`s ohne Join-Zeile nicht adressieren. Der interne Token-Endpunkt
  ist minimaler und leistungsfähiger — dokumentierte Designentscheidung im Plan.
- Zu beobachten bei Implementierung: optionaler Ctor-Parameter `IInternalMediaStreamTokenService?`
  in `MediaDurationProbeService` — DI fällt auf Default `null` zurück, wenn der Dienst nicht
  registriert ist (Bestandstests registrieren ihn nicht); früh per Build+Test verifizieren.
- Token steht unmaskiert im ffprobe-Prozess-Arg und in Log-Zeilen — akzeptiert und im Plan
  dokumentiert (Scope = eine Datei, TTL ≤ 15 min, nur lokale Sichtbarkeit).
