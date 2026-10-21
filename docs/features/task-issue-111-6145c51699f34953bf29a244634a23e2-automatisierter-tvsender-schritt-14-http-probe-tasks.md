# Tasks: Remote-Laufzeit-Probes über eigenen HTTP-Stream-Endpunkt (N9)

| # | Bereich | Aufgabe | Status | Testnachweis |
|---|---------|---------|--------|--------------|
| 1 | Konfiguration | `MediaProbeOptions` um `HttpProbeEnabled`, `HttpProbeBaseUrl`, `HttpProbeTokenLifetimeSeconds`, `EffectiveHttpProbeTokenLifetimeSeconds` erweitern | Offen | — |
| 2 | Konfiguration | `appsettings.json` `MediaProbe`-Abschnitt um die drei neuen Schlüssel ergänzen | Offen | — |
| 3 | Logik | `MediaFileContentTypes`-Helper anlegen und `ItemsController.OpenMediaFileAsync` darauf umstellen | Offen | — |
| 4 | Logik | `IInternalMediaStreamTokenService` + `InternalMediaStreamTokenService` anlegen (Token-Store, TTL, Loopback-URL-Aufbau via `IServerAddressesFeature`) | Offen | — |
| 5 | Controller | `InternalMediaStreamController` anlegen (`GET api/internal/media-stream/{token}`, 404 bei ungültigem Token, `enableRangeProcessing`) | Offen | — |
| 6 | Logik | `FfprobeMediaDurationProbe`: `HttpTransportErrorMarkers` → `UriUnsupportedFailure` für http(s)-URIs | Offen | — |
| 7 | Logik | `MediaDurationProbeService`: optionale Token-Service-Abhängigkeit, HTTP-first-Fallback in `ProbeRemoteItemAsync` | Offen | — |
| 8 | DI | `InternalMediaStreamTokenService` als Singleton in `ServiceCollectionExtensions` registrieren | Offen | — |
| 9 | Tests | `InternalMediaStreamTokenServiceTests` anlegen (URI-Aufbau, Normalisierung, Disabled/Override, TTL, unbekanntes Token) | Offen | — |
| 10 | Tests | `MediaDurationProbeServiceTests` um HTTP-Pfad-Fälle erweitern (HTTP-first, Fallback, Success, Local unverändert) | Offen | — |
| 11 | Tests | `FfprobeMediaDurationProbeTests` um HTTP-Transportfehler-Fälle erweitern | Offen | — |
| 12 | Tests | `InternalMediaStreamControllerTests` anlegen (200/206 mit Token, 404 ohne) | Offen | — |
| 13 | E2E-Tests | `MediaProbeHttpIntegrationTests` anlegen (echter Kestrel + echtes ffprobe, Skip bei fehlendem Binary) | Offen | — |
| 14 | Dokumentation | `docs/help/videolaufzeit.md` aktualisieren (HTTP-Probe-Pfad, Optionen, Fallback-Kette) | Offen | — |
| 15 | Dokumentation | `docs/RELEASE_NOTES.md` aktualisieren | Offen | — |
| 16 | Dokumentation | `README.md` prüfen/aktualisieren | Offen | — |
