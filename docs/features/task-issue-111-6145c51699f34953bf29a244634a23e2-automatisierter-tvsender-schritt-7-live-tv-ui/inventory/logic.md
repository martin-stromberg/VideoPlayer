# Logik / Services / Controller / Client

## `LiveStreamController`
Datei: `VideoWebPlayer/Controllers/LiveStreamController.cs`

| Element | Beschreibung |
|---------|--------------|
| Route | `api/live-stream`, `[ApiController]`, `[BearerTokenCheck]` (Header oder `access_token`-Query) |
| `GetFile(string fileName)` | `GET {fileName}` — liefert `.m3u8` (per `RewritePlaylistAsync` mit `access_token` auf jede Segment-URI ergänzt, `Cache-Control: no-store`, Content-Type `application/vnd.apple.mpegurl`) und `seg_*.ts` (`video/mp2t`); `CheckLoggedIn`; 404 bei ungültigem/fehlendem Namen inkl. Sendelücke (Whitelist `seg_*`/`live.m3u8` + Containment im `LiveStreamSegmentStore`) |

Konsum: `<video src>`/hls.js kann keine Header senden → `?access_token={token}` ist vorgesehener Aufruf.

## `ProgramGuideController`
Datei: `VideoWebPlayer/Controllers/ProgramGuideController.cs`

| Methode | Route | Rückgabe |
|---------|-------|----------|
| `GetNow` | `GET api/program-guide/now` | `DtoProgramGuideNow` (Now/Next, `ServerTimeUtc`) |
| `GetUpcoming(int hours = 6)` | `GET api/program-guide/upcoming?hours=` | `DtoProgramGuideEntry[]`; 400 außerhalb 1..`MaxPreviewHours` |

Beide `ExecuteAsync`-Fehlermapping (401/400/500) + `CheckLoggedIn`.

## `IProgramGuideService` / `ProgramGuideService`
Datei: `VideoWebPlayer/Services/IProgramGuideService.cs`, `ProgramGuideService.cs`

| Methode | Rückgabewert | Zweck |
|---------|--------------|-------|
| `GetNowAsync(DateTimeOffset utcNow, CancellationToken)` | `Task<DtoProgramGuideNow>` | Laufender Slot + Position/Remaining + nächster Slot; `null` in Sendelücke |
| `GetUpcomingAsync(DateTimeOffset utcNow, TimeSpan preview, CancellationToken)` | `Task<DtoProgramGuideEntry[]>` | Laufender + folgende `Scheduled`-Slots im halboffenen Fenster `[now, now+preview)` |
| `MaxPreviewHours` | `const int = 12` | Obergrenze der Vorschau (API-Default 6 h) |

DI: `services.AddScoped<IProgramGuideService, ProgramGuideService>()` (`ServiceCollectionExtensions.cs`).

## `VideoWebPlayerClient` (+ `InternalVideoWebPlayerClient`)
Datei: `VideoWebPlayer.Client/VideoWebPlayerClient.cs`, `VideoWebPlayer.Client/VideoWebPlayerClient.Playlists.cs`, `VideoWebPlayer/Services/Authentication/InternalVideoWebPlayerClient.cs`

| Methode/Element | Zweck |
|-----------------|-------|
| `HttpGetAsync<T>(endPoint[, ct])` etc. | protected-virtuelle HTTP-Helfer mit `SendWithReauthorizationAsync` (einmaliger 401-Retry über `HandleUnauthorized`) und `SendAndDeserializeAsync` (`HttpRequestException` mit `StatusCode` bei Fehler) |
| `CreateJsonContent(request)` | JSON-Body-Helfer (in Playlists-Partial genutzt) |
| `AuthorizationToken` (Property) | aktueller Bearer-Token-String (für `access_token`-Query) |
| `EnsureAuthorizationTokenAsync(user[, ct])` / `EnsureAuthorizationTokenSilentlyAsync(user[, ct])` | Token sicherstellen; `InternalVideoWebPlayerClient` impersoniert den angemeldeten Benutzer (`AuthorizationTokenService`) |
| `VideoWebPlayerClient.Playlists.cs` | Partial-Muster: implementiert `IPlaylistApiClient`, physisch getrennt, teilt HTTP-Infrastruktur |

DI: `VideoWebPlayerClient` scoped = `InternalVideoWebPlayerClient` (HttpClient mit BaseAddress aus HttpContext); `IPlaylistApiClient` scoped auf dieselbe Instanz (`ServiceCollectionExtensions.cs`, Z. 216–227).

**Hinweis:** `VideoWebPlayerClient` implementiert `IPlaylistApiClient` — kein `program-guide`-Zugriff bisher.

## `IPlaylistApiClient`
Datei: `VideoWebPlayer.Client/IPlaylistApiClient.cs`

Schmales Interface der Playlist-API-Operationen; Implementierung durch `VideoWebPlayerClient` selbst (Partial). Vorbild für einen `IProgramGuideApiClient`.

## `MediaAccessErrorMessages`
Datei: `VideoWebPlayer/ViewModels/MediaAccessErrorMessages.cs`

| Member | Inhalt |
|--------|--------|
| `Forbidden` | „Sie haben keinen Zugriff auf diesen Titel." |
| `NotFound` | „Dieser Titel existiert nicht oder hat keine Videodatei." |
| `PlaybackFailed` | Generische Wiedergabe-Fehlermeldung (Browser-Error-Event ohne Statuscode) |
| `Describe(Exception)` | `HttpRequestException.StatusCode` 403→Forbidden / 404→NotFound / sonst `null` |

`internal static` — für titelbezogene Fehler; Live-TV braucht senderbezogene eigene Texte (gleiches Muster).

## Hintergrund-Streaming (nur Kontext, nicht UI-relevant)

`LiveStreamCoordinator`/`LiveStreamWorker`/`FfmpegLiveStreamSegmenter` erzeugen die HLS-Segmente zeitbasiert; `LiveStreamMediaResolver` löst Dateipfade/Streams auf; `LiveStreamSegmentStore` kapselt Verzeichnis + Retention. Für die UI relevant nur: m3u8 kann 404 liefern (Sendelücke/kein ffmpeg/kein Sender) → Fehlerzustand.

## JS-Interop-Module (Muster)

`window.<modul>`-Objekte in eigenen Dateien unter `VideoWebPlayer/wwwroot/js/` (`continueWatching.js` attach/detach auf `<video>`; `playlistDragDrop.js`; `mediaContextMenu.js` …), eingebunden in `Components/App.razor` via `@AssetVersions.Url("js/<datei>")` (`StaticAssetVersioner` hängt `?v=<fingerprint>` an). DotNet-Rückrufe per `DotNetObjectReference` (Muster: `videoPlayer.registerTimeUpdate`-Aufruf in `VideoPlayer.razor`).

**Feststellung (Bestand, nicht Teil dieses Schritts):** `VideoPlayer.razor` ruft `videoPlayer.reload/registerTimeUpdate/setStartPosition/getCurrentPosition/unregisterTimeUpdate` auf; `window.videoPlayer` ist in keiner Repo-Datei definiert (getrackte `.js`-Dateien: backupUpload, continueWatching, downloads(leer), layoutNavigation, mediaContextMenu, playlistDragDrop, scroll, statusTicker, scroll-horizontal). Für die Live-Ansicht wird ein eigenes, tatsächlich vorhandenes JS-Modul angelegt.
