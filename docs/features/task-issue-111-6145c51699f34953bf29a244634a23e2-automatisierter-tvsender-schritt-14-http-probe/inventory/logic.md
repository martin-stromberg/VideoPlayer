# Logik — relevante Klassen und Abläufe

## `MediaDurationProbeService`
Datei: `VideoWebPlayer/Services/MediaDurationProbeService.cs`

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `ProbePendingAsync(int, CancellationToken)` | public | Batch-Auswahl (`Duration == null`, Retry-Regeln), ruft `RunProbesAsync`, wendet Ergebnisse via `ApplyOutcome` an, feuert `IProgramScheduleSignal` bei geschriebenen Laufzeiten |
| `GetProgressAsync(string, int, CancellationToken)` | public | Admin-Aggregat für Fortschrittsanzeige |
| `RunProbesAsync` | private | Parallelismus via `SemaphoreSlim(EffectiveParallelProbes)` |
| `ProbeItemAsync` | private | LocalDirectory → `ProbeFileAsync`; sonst `ProbeRemoteItemAsync`; Exceptions → `TransientFailure` |
| `ProbeRemoteItemAsync` | private | SFTP + Credentials + absoluter Pfad → `sftp://`-URI via `SftpProbeUriBuilder.TryBuildUri` → `ProbeUriAsync`; `UriUnsupported` oder nicht baubar → `_reader.OpenFileStream` + `ProbeStreamAsync` |
| `ResolveRemotePath` | private static | Absoluten Remote-Pfad aus `item.Path` oder Collection+Name bilden |
| `ApplyOutcome` | private | Success → Duration setzen, Attempts=0; Transient → Attempts++; Permanent → Attempts=max |
| `VideoExtensionPredicate` | private static | `Name.EndsWith` über `VideoFileExtensions.All` |

Konstruktor-Abhängigkeiten: `ApplicationDbContext`, `IMediaSourceReader`, `IMediaDurationProbe`,
`IFfprobeLocator`, `IOptions<MediaProbeOptions>`, `ILogger`, optional `TimeProvider`,
optional `IProgramScheduleSignal`. Registriert als **Scoped**
(`ServiceCollectionExtensions:242`).

## `FfprobeMediaDurationProbe` (`IMediaDurationProbe`)
Datei: `VideoWebPlayer/Services/FfprobeMediaDurationProbe.cs`

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `ProbeFileAsync` | public | Dateipfad, seekable=true, `EffectiveTimeoutSeconds` |
| `ProbeUriAsync` | public | Beliebige URI, seekable=true, `EffectiveTimeoutSeconds`; `_uriProtocolUnsupported` merkt sich abgelehnte Schemata prozessweit; Display-Input via `SftpProbeUriBuilder.MaskCredentials` (maskiert nur `user:pass@`-Userinfo, **keine Query-Parameter**) |
| `ProbeStreamAsync` | public | `pipe:0`, seekable=false, `EffectiveStreamTimeoutSeconds` |
| `ProbeAsync` | private | Prozesslauf; Timeout → Transient; Exit≠0: `Protocol not found` → UriUnsupported; seekable + `moov atom not found`/`Invalid data found` → Permanent; sonst Transient; Exit=0 + parsebare Duration → Success, sonst Permanent |
| `BuildArguments` | internal static | `-v error -show_entries format=duration -of default=noprint_wrappers=1:nokey=1 {input}` |
| `TryParseDurationOutput` | internal static | Dezimalsekunden → `TimeSpan` |
| `ExecuteProcessAsync` | private static | Prozessstart, Timeout via `CancelAfter`, `Kill(entireProcessTree)` |

Registriert als **Singleton** (`ServiceCollectionExtensions:241`). Interner Test-Ctor injiziert
`executeAsync`-Delegate (stdout/stderr/timeout simulierbar, siehe
`FfprobeMediaDurationProbeTests`).

## `SftpProbeUriBuilder` (internal static)
Datei: `VideoWebPlayer/Services/SftpProbeUriBuilder.cs`

| Methode | Kurzbeschreibung |
|---------|------------------|
| `TryBuildUri` | `sftp://user:pass@host:port/path`, null bei fehlenden Credentials/nicht-absolute m Pfad |
| `CombinePath` | Verzeichnis+Dateiname zusammensetzen |
| `MaskCredentials`/`MaskCredentialsInText` | `scheme://***@host` — Regex `([a-zA-Z][a-zA-Z0-9+.\-]*://)[^/\s@]*@` maskiert nur Userinfo |

## `MediaDurationProbeWorker` (BackgroundService)
Datei: `VideoWebPlayer/Services/MediaDurationProbeWorker.cs` — Scope je Intervall,
`IBackgroundProcessingGate`, ruft `ProbePendingAsync` bis Batch leer.

## `ItemsController` — Stream-Pfad
Datei: `VideoWebPlayer/Controllers/ItemsController.cs`, `[Route("api/[controller]")]`,
`[BearerTokenCheck]` auf Klassenebene.

| Methode | Kurzbeschreibung |
|---------|------------------|
| `StreamMediaItem` (`GET {type}/{id}/stream`, :663) | `CheckLogedIn` → `OpenMediaFileAsync` → `File(stream, contentType, enableRangeProcessing: true)` |
| `OpenMediaFileAsync` (:610) | `NormalizeMediaFileType` (`tvshow`→`tvshowepisode`) → `FindMediaItemAsync` (enthält `EnsureAccessAsync`!) → `_reader.OpenFileStream(collection, Path.GetFileName(item.Path))` → ContentType-Switch (mp4/mkv/avi/mpeg/octet-stream) |
| `FindEntry` (:505) | `movie`, `moviecollection`, `tvshow`, `tvshowseason`, `tvshowepisode` → Entity per Id |
| `EnsureAccessAsync` (:523) | `MediaSourceUsers`-Zeile ODER `UnlockedMediaEntries`-Zeile für `CurrentUser` nötig (`IsAccessible = hasSourceAccess \|\| isUnlocked`) |
| `FindMediaItemAsync` (:556) | `movie` → `MovieMediaItems[MovieId].MediaItem`; `tvshowepisode` → `TVShowEpisodeMediaItems[TVShowEpisodeId].MediaItem` |

## Auth-Infrastruktur

| Artefakt | Datei | Relevanz |
|----------|-------|----------|
| `BearerTokenCheckAttribute` | `Controllers/Attributes/` | `Authorization`-Header oder `?access_token=` → `JwtSecurityTokenHandler.ValidateToken` (Issuer=Audience=`Jwt:Issuer`, `SymmetricSecurityKey` aus DI, Lifetime geprüft) → `HttpContext.User` |
| `AuthorizationTokenService.CreateToken` | `Services/Authentication/IAuthService.cs:59` | JWT mit `Sub`/`NameIdentifier`=user.Id, `Email`, `Name`, `IsAdmin`, 12 h Gültigkeit — kein kurzlebiger/interner Mechanismus |
| `AuthService.CurrentUser` | ebenda :130 | `UserManager.GetUserAsync(HttpContext.User)` — verlangt echten `ApplicationUser` mit `NameIdentifier`-Claim |
| `DeviceTokenService`/`RefreshTokenService` | — | nicht für server-interne Self-Calls konzipiert (geprüft: nichts Passendes vorhanden) |

## Eigenadresse der laufenden Instanz

- `IServer` (DI, `Microsoft.AspNetCore.Hosting.Server`) → `Features.Get<IServerAddressesFeature>().Addresses`.
- Adressen können `http://*:5002`, `http://0.0.0.0:…`, `http://[::]:…`, `http://localhost:…`
  oder `https://…` sein → Normalisierung auf `127.0.0.1` nötig; Port unverändert.
- Konfiguration: `ASPNETCORE_URLS`, `UseUrls`, `Kestrel:Endpoints:Http:Url`
  (`appsettings.Production.json` → `http://*:5002`; Entwicklung `:5039`).
- Verwendetes Pattern in Tests: `PlaylistsE2ETestBase.InitializeAsync` (:73-75).
- Es existiert außerdem `DiscoveryBaseUrlResolver` (mDNS-Discovery) — löst Basis-URL der
  Instanz für Discovery-Werbung auf; prüfen, ob wiederverwendbar.

## Weitere relevante Stellen

- `ApplicationDbContext.GetRelationsForMediaItemAsync` (:699) → `MediaItemRelationResult`
  (Movie/MovieCollection oder TVShowEpisode/Season/Show) — fertiges Mapping für
  `MediaItem` → `{type}/{id}`, falls der bestehende Endpunkt genutzt wird.
- `ServiceCollectionExtensions` (:234-243): `MediaProbeOptions`-Bindung, `IFfprobeLocator`,
  `IMediaDurationProbe` Singleton, `MediaDurationProbeService` Scoped, Worker HostedService.
