# Bestandsaufnahme: Logik

Analysiert bezogen auf die Anforderung, `GET /api/internal/media-stream/{token}` an das
Feature-Flag `Setup.TvSenderEnabled` zu binden (Variante A) oder die Ausnahme begründet zu
dokumentieren (Variante B). Zusätzlich zu den betroffenen Klassen sind die bestehenden
Flag-Gates als Referenzmuster aufgeführt.

## `InternalMediaStreamController`

Datei: `VideoWebPlayer/Controllers/InternalMediaStreamController.cs`

Route: `api/internal/media-stream`, `[ApiController]`, **kein** `[BearerTokenCheck]`, erbt
direkt von `ControllerBase` (nicht von `ApiBaseController` — kein `CheckLoggedIn`, keine
Benutzer-Prüfung). Einzige Berechtigung ist das In-Memory-Token des
`IInternalMediaStreamTokenService`.

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `InternalMediaStreamController(ApplicationDbContext, IMediaSourceReader, IInternalMediaStreamTokenService, ILogger<InternalMediaStreamController>)` | public | Konstruktor, Z. 34–44. **`ProgramSettingsService` wird aktuell nicht injiziert.** |
| `Stream(string token, CancellationToken)` | public | `[HttpGet("{token}")]`, Z. 54–85. Ablauf: `_tokens.ResolveMediaItemId(token)` → `null` ⇒ 404 (Z. 57–59); `MediaItems` inkl. `MediaCollection.MediaSource` laden → `null` ⇒ 404 (Z. 61–66); `_reader.OpenFileStream(...)` → Ausnahme ⇒ 500 mit Warn-Log, `null` ⇒ 404 (Z. 70–82); `File(stream, ..., enableRangeProcessing: true)` (Z. 84). |

**Keine Flag-Prüfung:** Der Methodenkörper enthält keinen Zugriff auf
`Setup.TvSenderEnabled`/`ProgramSettingsService`. Ein Einschub von
`GetTvSenderEnabledAsync` wäre fachlich möglich vor Z. 57 (vor dem Token-Resolve — dann
antwortet auch ein ungültiges Token mit 404 wie bei den anderen Gates) oder zwischen
Z. 59 und Z. 61 (nach dem Token-Resolve — Verhalten für ungültige Tokens bleibt
unverändert). `HttpContext.RequestAborted` steht als CancellationToken zur Verfügung
(Vorbild: `LiveStreamController` Z. 56).

**Klassen-Kommentar (Z. 8–17):** Begründet bereits die fehlende Benutzer-Authentifizierung
(„the unguessable, expiring token … is the only credential"), nennt aber nur den
ffprobe-Probe-Zweck — der Segmentierer-Pfad (`TryCreateStreamUri`, lange Token-Lebensdauer)
und das Verhältnis zum Feature-Flag sind nicht erwähnt. Für Variante B wäre dies der
natürliche Ort der dokumentierten Ausnahme.

## `InternalMediaStreamTokenService`

Datei: `VideoWebPlayer/Services/InternalMediaStreamTokenService.cs` (Interface: `IInternalMediaStreamTokenService.cs`, siehe [interfaces.md](interfaces.md))

`public sealed partial class`, DI: **Singleton**
(`ServiceCollectionExtensions.cs` Z. 244). Token liegen in einer `ConcurrentDictionary<string, TokenEntry>` im Speicher (Z. 51) — nie persistiert; ein Prozess-Neustart macht
ausstehende Tokens ungültig. `TokenEntry` = `record(long MediaItemId, DateTimeOffset ExpiresAt)` (Z. 64).

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `TryCreateProbeUri(long mediaItemId)` | public | Z. 89–95. Liefert `null` bei `MediaProbeOptions.HttpProbeEnabled == false`; sonst `TryCreateUri` mit TTL `EffectiveHttpProbeTokenLifetimeSeconds` (Default 600 s). Aufgerufen von `MediaDurationProbeService.ProbeRemoteItemAsync`. |
| `TryCreateStreamUri(long mediaItemId, TimeSpan lifetime)` | public | Z. 98–101. TTL = `max(lifetime, MinimumStreamTokenLifetime = 60 s)` (Z. 86). **Nicht** an `HttpProbeEnabled` gekoppelt. Aufgerufen von `LiveStreamMediaResolver.ResolveAsync`. |
| `ResolveMediaItemId(string token)` | public | Z. 126–136. `null`/unbekannt/abgelaufen ⇒ `null` (abgelaufene Einträge werden entfernt). |
| `TryCreateUri(long, TimeSpan)` | private | Z. 109–123. Gemeinsamer Aussteller: Loopback-Basis-URL + 256-Bit-Zufallstoken (hex 64 Zeichen) + `SweepExpiredTokens`. |
| `ResolveBaseUrl()` | private | Z. 148–195. `MediaProbe:HttpProbeBaseUrl` vor `IServerAddressesFeature`; Wildcard/Loopback ⇒ `127.0.0.1`; nur `http` (kein https für ffprobe). Negativ-Ergebnis wird nicht gecacht (Start-Race). |
| `MaskStreamTokenInText(string?)` | internal static | Z. 42–45. Maskiert `/api/internal/media-stream/{hex}` in Log-Texten; genutzt von `FfmpegLiveStreamSegmenter` (Z. 177, 281) und `FfprobeMediaDurationProbe` (Z. 113, 180). |
| `TryNormalizeServerAddress(string)` | internal static | Z. 214–255. Adressnormalisierung für Loopback-URIs. |
| `SweepExpiredTokens()` | private | Z. 285–293. Entfernt abgelaufene Einträge bei jeder Ausstellung. |

**Token-Arten über demselben Endpunkt (für die Varianten-Entscheidung relevant):**

| Token-Art | Aussteller-Aufruf | TTL | Flag-Bezug heute |
|-----------|-------------------|-----|------------------|
| Probe-Token | `TryCreateProbeUri` via `MediaDurationProbeService.ProbeRemoteItemAsync` (Z. 319) | `MediaProbe:HttpProbeTokenLifetimeSeconds`, Default 600 s | Ausstellung nur solange `MediaDurationProbeWorker` läuft — der prüft das Flag pro Zyklus |
| Stream-Token | `TryCreateStreamUri` via `LiveStreamMediaResolver.ResolveAsync` (Z. 127) | `RemainingSeconds` des Sendeplatzes + 15 min Marge, Fallback 45 min, Minimum 60 s | Ausstellung nur solange `LiveStreamWorker` synchronisiert — der prüft das Flag pro Tick |

Beide Token-Arten werden von `InternalMediaStreamController.Stream` identisch über
`ResolveMediaItemId` aufgelöst; der Endpunkt unterscheidet sie nicht. **Ein bereits
ausgestelltes, noch gültiges Token bleibt nutzbar, solange es nicht abläuft — auch wenn
das Flag zwischenzeitlich ausgeschaltet wird.** Bei Stream-Tokens kann das die restliche
Sendeplatz-Laufzeit plus 15 Minuten bedeuten.

## `ProgramSettingsService`

Datei: `VideoWebPlayer/Services/ProgramSettingsService.cs`

`public sealed class`, DI: **scoped** (`ServiceCollectionExtensions.cs` Z. 267) —
kann in den scoped `InternalMediaStreamController` injiziert werden (Vorbild:
`LiveStreamController` Z. 33, `ProgramGuideController` Z. 30).

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `GetOrCreateSetupAsync(CancellationToken)` | public | Z. 31–73. Lädt die einzige `Setup`-Zeile, legt sie bei Bedarf an (schreibend!) und korrigiert ungültige Default-Felder. |
| `GetTvSenderEnabledAsync(CancellationToken)` | public | Z. 167–171. `GetOrCreateSetupAsync` → `setup.TvSenderEnabled`. Eine DB-Abfrage pro Aufruf (kein Cache) — gleiches Muster wie an allen anderen Flag-Gates. |
| `UpdateTvSenderEnabledAsync(bool, CancellationToken)` | public | Z. 178–183. Persistiert den Schalter (Admin-Oberfläche `ProgramSettings.razor`). |
| `UpdateGeneralSettingsAsync(GeneralSettingsUpdate, CancellationToken)` | public | Z. 139–160. Setzt u. a. `setup.TvSenderEnabled` aus dem Admin-Formular (Z. 157). |
| `IsTvSenderAccessGrantedAsync(string?, bool, CancellationToken)` | public | Z. 195–203. Flag UND (Admin ODER `TvSenderUserAccess`-Zeile) — für den internen Endpunkt ohne Benutzerkontext nicht anwendbar. |

## `LiveStreamController` (Referenzmuster Variante A)

Datei: `VideoWebPlayer/Controllers/LiveStreamController.cs`

Route `api/live-stream`, `[BearerTokenCheck]`, erbt `ApiBaseController`. In `GetFile`
(Z. 49–95): `CheckLoggedIn()` → **Flag-Gate Z. 55–57** (`GetTvSenderEnabledAsync` → 404,
Kommentar „ohne Admin-Freigabe ist der Stream-Endpunkt unsichtbar (404)") →
Zugriffs-Gate Z. 59–62 (`IsTvSenderAccessGrantedAsync` → 403) → Dateiauflösung.
Der Flag-Check steht also **vor** jeder fachlichen Auswertung.

## `ProgramGuideController` (Referenzmuster Variante A, inkl. Begründung)

Datei: `VideoWebPlayer/Controllers/ProgramGuideController.cs`

Route `api/program-guide`, `[BearerTokenCheck]`, `ApiBaseController`.
`FeatureReleasedAsync` (Z. 39–40) kapselt `GetTvSenderEnabledAsync`; der Kommentar
Z. 37–38 begründet das 404-Verhalten: „while unreleased the endpoints answer 404 — the
feature is indistinguishable from 'does not exist'". Reihenfolge in beiden Endpunkten:
`CheckLoggedIn` → Flag → Benutzerfreigabe → Logik.

## `MediaDurationProbeService` (Probe-Token-Verbraucher)

Datei: `VideoWebPlayer/Services/MediaDurationProbeService.cs`

DI: scoped (Z. 245). `IInternalMediaStreamTokenService` ist **optional** injiziert
(`streamTokens = null`, Z. 57) — ohne den Service fallen Remote-Probes direkt auf
`sftp://`/`pipe:0` zurück.

- `ProbeRemoteItemAsync(MediaItem, CancellationToken)` (Z. 314–349): Z. 319
  `_streamTokens?.TryCreateProbeUri(item.Id)` → `IMediaDurationProbe.ProbeUriAsync(httpUri)`;
  nur bei `UriUnsupported` Fallback auf `sftp://` (nur `MediaSourceType.Sftp` mit
  Credentials) bzw. `pipe:0`-Stream **innerhalb desselben Versuchs**.
  Fehlerklassifikation in `FfprobeMediaDurationProbe.ProbeAsync` (Z. 192–198): für
  `http(s)`-Eingänge wird ein HTTP-Transportfehler in stderr
  (`FfmpegErrorMarkers` — u. a. „Server returned", „HTTP error") zu
  `MediaDurationProbeResult.UriUnsupported` — ein flag-bedingtes 404 des Endpunkts würde
  demnach **nicht** den Versuch verwerfen, sondern unmittelbar auf `sftp://`/`pipe:0`
  zurückfallen lassen. Ein bereits laufender Seek würde trotzdem hart abgebrochen und neu
  begonnen.
- Wird ausschließlich vom `MediaDurationProbeWorker` getrieben (s. u.) — bei Flag aus
  entstehen keine neuen Probes.

## `LiveStreamMediaResolver` (Stream-Token-Verbraucher)

Datei: `VideoWebPlayer/Services/LiveStreamMediaResolver.cs`

`public sealed class`, DI: scoped (Z. 240). Konstanten:
`StreamTokenLifetimeMargin = 15 min` (Z. 24), `DefaultStreamTokenLifetime = 45 min` (Z. 27).

- `ResolveAsync(long mediaItemId, TimeSpan? runDuration, bool useHttpInput, CancellationToken)`
  (Z. 78–133): `LocalDirectory`-Quelle → `LiveStreamInput.ForFile` (physischer Pfad);
  entfernte Quelle + `useHttpInput` + `LiveStreamOptions.UseHttpInput` →
  `_streamTokens.TryCreateStreamUri(item.Id, lifetime)` mit `lifetime = runDuration + 15 min`
  bzw. 45 min Fallback (Z. 124–127) → `LiveStreamInput.ForHttp(uri)`; sonst
  `LiveStreamInput.ForStream` (Pipe-Fabrik über `_reader.OpenFileStream`, Z. 132).
- Aufgerufen von `LiveStreamCoordinator.SynchronizeStreamAsync` (Z. 116) mit
  `runDuration = now.RemainingSeconds`.

## `LiveStreamCoordinator` + `FfmpegLiveStreamSegmenter` (Konsumenten der HTTP-URL)

Dateien: `VideoWebPlayer/Services/LiveStreamCoordinator.cs`,
`VideoWebPlayer/Services/FfmpegLiveStreamSegmenter.cs`

- `LiveStreamCoordinator` (scoped, Z. 242): `SynchronizeStreamAsync` (Z. 89–159) entscheidet
  `useHttpInput` anhand von `_segmenter.LastUnexpectedExit` — war der letzte
  unerwartete Exit desselben Slots ein `HttpTransportFailure`, wird der nächste Versuch auf
  `pipe:0` umgestellt (Z. 111–129). Relevant für Variante A: **ein durch das Flag-Gate
  verursachtes 404 des Endpunkts erscheint in ffmpeg-stderr als „Server returned 404…" und
  wird über `FfmpegErrorMarkers.ContainsHttpTransportError` als HTTP-Transportfehler
  klassifiziert** → `LiveStreamExitInfo.HttpTransportFailure = true`
  (`FfmpegLiveStreamSegmenter` Z. 305–309) → der selbe Slot würde beim nächsten Tick
  (falls das Flag wieder an ist) mit Pipe-Eingang neu gestartet.
- `FfmpegLiveStreamSegmenter` (Singleton, `ILiveStreamSegmenter`, Z. 239):
  `BuildArguments` (Z. 372–397) setzt `job.Input.HttpUrl` als `-i "{url}"` — ffmpeg liest
  den Endpunkt über die gesamte Slot-Restlaufzeit (`-t {remaining}`). Ein Flag-Gate würde
  einen laufenden Read sofort abbrechen; der reguläre Stopp-Pfad ist dagegen
  `LiveStreamWorker` → `ILiveStreamSegmenter.StopAsync` → `process.Kill()`.

## Flag-Gates der Worker (heutiger Zustand)

| Worker | Datei | Flag-Auswertung |
|--------|-------|-----------------|
| `MediaDurationProbeWorker` | `VideoWebPlayer/Services/MediaDurationProbeWorker.cs` | Z. 65: Probes nur `if (GetTvSenderEnabledAsync)` — Flag wird **pro Zyklus** gelesen; Kommentar: die Laufzeitermittlung „dient nur dem Sender" |
| `LiveStreamWorker` | `VideoWebPlayer/Services/LiveStreamWorker.cs` | Z. 73–79: Flag aus ⇒ laufenden Segmenter via `ILiveStreamSegmenter.StopAsync` stoppen, keine `SynchronizeAsync`-Ticks |
| `ProgramScheduleWorker` | `VideoWebPlayer/Services/ProgramScheduleWorker.cs` | Z. 63: `ReconcileAsync`/`EnsureCoverageAsync` nur bei Flag an; Intervall-Sleep bleibt |

Alle drei lesen das Flag frisch aus der DB (über `ProgramSettingsService` im jeweiligen
Scope) — eine Flag-Änderung greift ohne Neustart, aber erst beim nächsten Takt/Zyklus.

## Weitere Flag-Verbraucher (UI-Kontext, für die Entscheidung nicht direkt relevant)

- `Components/Layout/NavMenu.razor` Z. 136: `IsTvSenderAccessGrantedAsync` → Nav-Eintrag „Live-TV".
- `Components/Pages/LiveTv.razor` Z. 205–209: `GetTvSenderEnabledAsync` + `IsTvSenderAccessGrantedAsync`.
- `Components/Pages/Admin/TVSenderAdmin.razor` Z. 771 und
  `Components/Pages/Admin/ProgramSettings.razor` (Schalter „Automatisierten TV-Sender freischalten", `UpdateGeneralSettingsAsync`).

## Querverweise

- `InternalMediaStreamController.Stream` ← konsumiert Tokens von `InternalMediaStreamTokenService`
- `InternalMediaStreamTokenService.TryCreateProbeUri` ← aufgerufen von `MediaDurationProbeService.ProbeRemoteItemAsync` (ffprobe-Eingang)
- `InternalMediaStreamTokenService.TryCreateStreamUri` ← aufgerufen von `LiveStreamMediaResolver.ResolveAsync` → `LiveStreamInput.ForHttp` → `FfmpegLiveStreamSegmenter.BuildArguments` (`-i` ffmpeg)
- `MaskStreamTokenInText` ← aufgerufen von `FfmpegLiveStreamSegmenter` (Z. 177, 281), `FfprobeMediaDurationProbe` (Z. 113, 180)
- `ProgramSettingsService` ← injiziert in `LiveStreamController`, `ProgramGuideController`, `LiveStreamCoordinator`, `LiveStreamSegmentStore`, `ProgramScheduleService`, `MediaSourceScanner`, `ContinueWatchingService`; per `GetRequiredService` in den drei Workern sowie `MdnsAdvertiserWorker`, `MediaSourceScanService`, `PlaylistBackfillCoordinator`
