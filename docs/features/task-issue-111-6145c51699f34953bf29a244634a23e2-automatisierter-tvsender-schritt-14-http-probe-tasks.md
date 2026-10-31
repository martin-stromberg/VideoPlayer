# Tasks: Remote-Laufzeit-Probes über eigenen HTTP-Stream-Endpunkt (N9)

| # | Bereich | Aufgabe | Status | Testnachweis |
|---|---------|---------|--------|--------------|
| 1 | Konfiguration | `MediaProbeOptions` um `HttpProbeEnabled`, `HttpProbeBaseUrl`, `HttpProbeTokenLifetimeSeconds`, `EffectiveHttpProbeTokenLifetimeSeconds` erweitern | Fertig | `InternalMediaStreamTokenServiceTests` (Disabled/Override/TTL) |
| 2 | Konfiguration | `appsettings.json` `MediaProbe`-Abschnitt um die drei neuen Schlüssel ergänzen | Fertig | Options-Bindung über Suite |
| 3 | Logik | `MediaFileContentTypes`-Helper anlegen und `ItemsController.OpenMediaFileAsync` darauf umstellen | Fertig | `InternalMediaStreamControllerTests` (video/mp4), `LocalMediaSourceStreamingE2ETests` |
| 4 | Logik | `IInternalMediaStreamTokenService` + `InternalMediaStreamTokenService` anlegen (Token-Store, TTL, Loopback-URL-Aufbau via `IServerAddressesFeature`) | Fertig | `InternalMediaStreamTokenServiceTests` |
| 5 | Controller | `InternalMediaStreamController` anlegen (`GET api/internal/media-stream/{token}`, 404 bei ungültigem Token, `enableRangeProcessing`) | Fertig | `InternalMediaStreamControllerTests`, `MediaProbeHttpIntegrationTests` |
| 6 | Logik | `FfprobeMediaDurationProbe`: `HttpTransportErrorMarkers` → `UriUnsupportedFailure` für http(s)-URIs | Fertig | `FfprobeMediaDurationProbeTests` (Theorie-Fälle + Nicht-http-Gegenprobe) |
| 7 | Logik | `MediaDurationProbeService`: optionale Token-Service-Abhängigkeit, HTTP-first-Fallback in `ProbeRemoteItemAsync` | Fertig | `MediaDurationProbeServiceTests` (5 neue Fälle) |
| 8 | DI | `InternalMediaStreamTokenService` als Singleton in `ServiceCollectionExtensions` registrieren | Fertig | Auflösung via `MediaProbeHttpIntegrationTests`/`InternalMediaStreamControllerTests` aus echtem DI |
| 9 | Tests | `InternalMediaStreamTokenServiceTests` anlegen (URI-Aufbau, Normalisierung, Disabled/Override, TTL, unbekanntes Token) | Fertig | 12 Fälle grün |
| 10 | Tests | `MediaDurationProbeServiceTests` um HTTP-Pfad-Fälle erweitern | Fertig | 5 neue Fälle grün |
| 11 | Tests | `FfprobeMediaDurationProbeTests` um HTTP-Transportfehler-Fälle erweitern | Fertig | 8 neue Fälle grün |
| 12 | Tests | `InternalMediaStreamControllerTests` anlegen (200/206 mit Token, 404 ohne) | Fertig | 4 Fälle grün |
| 13 | E2E-Tests | `MediaProbeHttpIntegrationTests` anlegen (echter Kestrel + echtes ffprobe, Skip bei fehlendem Binary) | Fertig | 2 Fälle grün (echtes ffprobe 9.0.2) |
| 14 | Dokumentation | `docs/help/videolaufzeit.md` aktualisieren (HTTP-Probe-Pfad, Optionen, Fallback-Kette) | Fertig | — |
| 15 | Dokumentation | `docs/RELEASE_NOTES.md` aktualisieren | Fertig | EN- und DE-Abschnitt |
| 16 | Dokumentation | `README.md` prüfen/aktualisieren | Fertig | Kein Update nötig (beschreibt nur ffprobe-Installation) |
