# Logik — Bestandsaufnahme „TV-Sender: On-Air" (Issue #348)

Klassen im Pfad des Live-Streams und der Schalter-Auswertung. Reihenfolge: Stream-Kette
(Worker → Coordinator → Segmenter), Auslieferung, Settings, dann die unbeteiligten Nachbarn
und die UI.

## `LiveStreamWorker`

Datei: `VideoWebPlayer/Services/LiveStreamWorker.cs` — `BackgroundService`, einziger
Taktgeber der Segmentierung. In Tests direkt instanziiert (DI über `ServiceCollection`).

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `ExecuteAsync` | `protected override` | Start-Sequenz: `LiveStreamSegmentStore.ClearAsync` (Zeile 66) → `LiveStreamStandbyProvider.RefreshAsync` (73) → `_initialDelay` (Default 10 s). Poll-Schleife: `GetTvSenderEnabledAsync` (96) — bei `false` laufenden `ILiveStreamSegmenter` idempotent stoppen (`segmenter.IsRunning` → `StopAsync`, 100–102), sonst hinter `IBackgroundProcessingGate` `LiveStreamCoordinator.SynchronizeAsync` aufrufen und `WakeAtUtc` auf die Verzögerung anrechnen (84–144). Hier liegt das bisher **einzige** Gate — ein On-Air-Gate würde dieselbe Verzweigung betreffen. |
| `LogFfmpegUnavailableThrottled` | `private` | 5-Minuten-Drossel der ffmpeg-fehlt-Warnung. |

Abonnierte „Events": `IProgramScheduleSignal.WaitLiveAsync` — persistente Signal-Task,
weckt die Schleife vorzeitig (Zeilen 87, 159–173). Dedup-Merker `_lastPersistentResult`
für `MediaUnavailable`/`InputUnplayable`-Log-Spam. Konstante `PollCap` = 30 s.

Registrierung: `AddHostedService<LiveStreamWorker>()` in
`ServiceCollectionExtensions.cs:340` — **nur außerhalb der `Testing`-Umgebung** (dort darf
kein ffmpeg-Prozess starten).

## `LiveStreamCoordinator`

Datei: `VideoWebPlayer/Services/LiveStreamCoordinator.cs` — scoped; bringt die laufende
ffmpeg-Segmentierung rein zeitbasiert mit dem Sendeplan in Einklang (kein persistierter
Stream-Zustand — Resynchronisation von selbst).

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `SynchronizeAsync` | `public` | `SynchronizeStreamAsync` + Retention-Sweep (`GetLiveStreamSegmentRetentionAsync`, `SweepExpiredSegmentsAsync`, Zeilen 86–89). |
| `SynchronizeStreamAsync` | `private` | ffmpeg-Auflösung (fehlt → `StopAsync` + `FfmpegUnavailable`), `IProgramGuideService.GetNowAsync` (Sendelücke → `StopAsync` + `Stopped`), `UndecodableInput`/`InputUnplayable`-Skip, Eingangskaskade über `LiveStreamMediaResolver.ResolveAsync` (HTTP → sftp → pipe:0), Drift-Prüfung (`MaxDriftSeconds` = 30), `LiveStreamJob`-Bau, `ILiveStreamSegmenter.StartAsync`. |

Abhängigkeiten: `IProgramGuideService`, `LiveStreamMediaResolver`, `LiveStreamSegmentStore`,
`ILiveStreamSegmenter`, `IFfmpegLocator`, `ProgramSettingsService`.

## `FfmpegLiveStreamSegmenter`

Datei: `VideoWebPlayer/Services/FfmpegLiveStreamSegmenter.cs` — Singleton, implementiert
`ILiveStreamSegmenter` mit genau einem laufenden ffmpeg-Prozess (`-re`, `-ss`, `-t`,
HLS-Muxer `append_list` mit `#EXT-X-DISCONTINUITY`).

Relevant für On-Air: `IsRunning`, `StopAsync` (`StopTimeout` 10 s, `Kill` auf Prozessbaum),
`StartAsync` (stoppt einen vorherigen Job zuerst), `PlaylistFileName` = `live.m3u8`,
`PlaylistWindowSegmentCount` = 12. Intern `ISegmenterProcess`-Abstraktion
(`SegmenterProcess` über `System.Diagnostics.Process`) für Tests.

## `LiveStreamSegmentStore`

Datei: `VideoWebPlayer/Services/LiveStreamSegmentStore.cs` — scoped; besitzt das
Segment-Verzeichnis (konfiguriert oder `Data/LiveStream`).

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `GetDirectoryAsync` | `public` | Effektives Verzeichnis auflösen + anlegen. |
| `TryResolveFileAsync` | `public` | Dateiname → Pfad; `SafeFileName`-Whitelist + Containment-Check. |
| `SweepExpiredSegmentsAsync` | `public` | Löscht `seg_*.ts` älter als Retention. |
| `ClearAsync` | `public` | Leert das Verzeichnis komplett — bisher nur beim Worker-Start aufgerufen; Kandidat für die offene Frage „Aufräumen beim Off-Air-Schalten". |

## `LiveStreamStandbyProvider`

Datei: `VideoWebPlayer/Services/LiveStreamStandbyProvider.cs` — Singleton; hält die
Warm-up-/Standby-Auslieferung im Speicher (eingebetteter Default-Clip „Gleich geht's los!"
oder konfiguriertes `LiveStreamStandbyMediaItemId`, transkodiert nach `Data/LiveStreamStandby`).

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `BuildPlaylist` | `public` | Rotierende Live-Playlist des aktiven Satzes; triggert ggf. `TriggerPrepare`. |
| `TryGetSegment` | `public` | Segment-Bytes des aktiven Satzes. |
| `NoteStandbyPlaylistServed` / `ConsumePendingStandbyTransition` | `public` | One-shot-Discontinuity-Markierung Standby → echt. |
| `RefreshAsync` | `public` | Einmalige Vorbereitung beim Worker-Start. |
| `PrepareAsync` | `internal` | Vorbereiten/Tausch des Standby-Satzes (Cache → Transcode → Kaskade). |

Relevant: `ServeStandbyAsync` im `LiveStreamController` nutzt diesen Provider — ob er Off-Air
weiter antwortet, ist eine offene Frage der Anforderung.

## `ProgramScheduleSignal`

Datei: `VideoWebPlayer/Services/ProgramScheduleSignal.cs` — Singleton, implementiert
`IProgramScheduleSignal` (Interface siehe [interfaces.md](interfaces.md)). Zwei
Bounded-Channels: `_channel` (Sendeplan-Worker) und `_liveChannel` (LiveStreamWorker);
`WakeWorkers` schreibt in beide. `NotifyScheduleChanged` läuft (außerhalb von Scans) über
das Batching-Fenster `ProgramSchedule:ScheduleChangedDebounceSeconds` — ein sofortiges
Aufwecken beim Umschalten des On-Air-Schalters nach dem Muster der TV-Sender-Freischaltung
nutzt heute denselben Aufruf (`ProgramSettings.razor:234`).

## `ProgramSettingsService`

Datei: `VideoWebPlayer/Services/ProgramSettingsService.cs` — scoped; einziger
Lese-/Schreibzugang zur `Setups`-Zeile.

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `GetOrCreateSetupAsync` | `public` | Single-Row lesen oder anlegen (Unique-Violation-Race abgefangen, `CreateSetupAsync`); normalisiert ungültige Werte. |
| `GetTvSenderEnabledAsync` | `public` | Liest `Setup.TvSenderEnabled` — das Vorbild-Muster für `GetTvSenderOnAirAsync`. Wird aufgerufen von: `LiveStreamWorker`, `LiveStreamController`, `ProgramGuideController`, `InternalMediaStreamController`, `ProgramScheduleWorker`, `MediaDurationProbeWorker`, `LiveTv.razor`, `TVSenderAdmin.razor`, `IsTvSenderAccessGrantedAsync`. |
| `UpdateTvSenderEnabledAsync` | `public` | Persistiert `Setup.TvSenderEnabled`. Wird aktuell **nicht** aufgerufen (die Freischaltung läuft über `UpdateGeneralSettingsAsync`) — dokumentierter Befund aus Schritt 26. |
| `IsTvSenderAccessGrantedAsync` | `public` | `TvSenderEnabled` UND (Admin ODER `TvSenderUserAccess`-Zeile) — kombiniertes Gate für Navigation, `/live-tv`, `/api/program-guide`, `/api/live-stream`. |
| `UpdateGeneralSettingsAsync` | `public` | Atomares Schreiben der Allgemein-Einstellungen inkl. `TvSenderEnabled` (Zeile 185). |
| `GetLiveStreamSegmentLengthAsync` / `GetLiveStreamSegmentDirectoryAsync` / `GetLiveStreamSegmentRetentionAsync` | `public` | Segmentierungseinstellungen mit Defaults (6 s, `Data/LiveStream`, 30 min). |
| `UpdateLiveStreamSettingsAsync` | `public` | Persistiert Segmentlänge/-verzeichnis/-retention mit Validierung. |
| `GetLiveStreamStandbyMediaItemIdAsync` / `UpdateLiveStreamStandbyClipAsync` | `public` | Standby-Clip lesen/setzen (Existenz- und Klassifikationsprüfung). |
| übrige | `public` | Scan-Intervalle, Titel, Weiterschauen, mDNS, Discovery-URL, Feiertags-Bundesland, Planungshorizont/-intervall, Inhaltsspektrum, Fensterzeiten, Unbekannt-Modus, `GetTvSenderAllowedUserIdsAsync`/`UpdateTvSenderAllowedUserIdsAsync`. |

Publizierte Events: keine — Aufrufer feuern `IProgramScheduleSignal.NotifyScheduleChanged()`
selbst (z. B. `ProgramSettings.razor:234`, `TVSenderAdmin.razor:2028`).

## `LiveStreamController`

Datei: `VideoWebPlayer/Controllers/LiveStreamController.cs` — `[ApiController]`,
`[Route("api/live-stream")]`, `[BearerTokenCheck]`; liefert HLS-Playlist und -Segmente.

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `GetFile` | `public` (`[HttpGet("{fileName}")]`) | Gate-Kette (Zeilen 64–83): `CheckLoggedIn` → **404 bei `!TvSenderEnabled`** (77) → **403 bei `!IsTvSenderAccessGrantedAsync`** (81). Danach: `TryResolveFileAsync` → echte `live.m3u8` mit Segment-Referenz (Token-Rewrite + evtl. Discontinuity) → `.ts`-Datei → `ServeStandbyAsync`. Genau hier (zwischen Freigabe und Auslieferung) sieht die Anforderung das On-Air-Gate vor. |
| `ServeStandbyAsync` | `private` | `live.m3u8`-Anfrage → `BuildPlaylist` des Standby-Providers; `standby_*`-Segmente aus dem Speicher; sonst 404 (Zeilen 136–157). |
| `ReadPlaylistAsync` / `PlaylistReferencesSegment` / `InjectDiscontinuity` / `RewritePlaylistContent` | `private static` | Playlist-Lesen (ReadWrite\|Delete-Share), Segment-Referenz-Erkennung, `#EXT-X-DISCONTINUITY`, `access_token`-Rewrite. |

## `ProgramGuideController` *(nicht Teil der Verarbeitungseinheit)*

Datei: `VideoWebPlayer/Controllers/ProgramGuideController.cs` — `/api/program-guide/now`
und `/upcoming`; Gates `FeatureReleasedAsync` (`GetTvSenderEnabledAsync` → 404) und
`AccessGrantedAsync` (`IsTvSenderAccessGrantedAsync` → 403). Bleibt Off-Air laut
Anforderung unverändert abfragbar.

## `InternalMediaStreamController` *(nicht Teil der Verarbeitungseinheit)*

Datei: `VideoWebPlayer/Controllers/InternalMediaStreamController.cs` —
`/api/internal/media-stream/{token}`; tokenbasiert (kein Benutzer-Login), einziges Gate
`GetTvSenderEnabledAsync` → 404 (Zeile 69). Dient Laufzeitermittlung **und**
Live-Stream-HTTP-Eingang; folgt laut Anforderung (Annahme) weiter allein `TvSenderEnabled`.

## `ProgramScheduleWorker` *(nicht Teil der Verarbeitungseinheit)*

Datei: `VideoWebPlayer/Services/ProgramScheduleWorker.cs` — `BackgroundService`; je Takt
`GetTvSenderEnabledAsync` → `IProgramScheduleService.ExecutePassAsync`; materialisierte
Änderungen wecken den Live-Stream über `NotifyLiveScheduleChanged` (Zeilen 77–79). Läuft
Off-Air unverändert weiter.

## `MediaDurationProbeWorker` *(nicht Teil der Verarbeitungseinheit)*

Datei: `VideoWebPlayer/Services/MediaDurationProbeWorker.cs` — `BackgroundService`; je Takt
`GetTvSenderEnabledAsync` → `MediaDurationProbeService.ProbePendingAsync` im Batch-Loop
unter `BeginScan`-Unterdrückung. Läuft Off-Air unverändert weiter.

## `LiveStreamMediaResolver`

Datei: `VideoWebPlayer/Services/LiveStreamMediaResolver.cs` — scoped;
`ResolveAsync(mediaItemId, runDuration, useHttpInput, useSftpInput, cancellationToken)`
(Zeile 83) liefert den passenden `LiveStreamInput` (lokal/HTTP/sftp/pipe) — vom On-Air-Schalter
unbetroffen.

## `LiveStreamStandbySegmenter`

Datei: `VideoWebPlayer/Services/LiveStreamStandbySegmenter.cs` — Singleton;
`TranscodeAsync(input, dir, ct)` transkodiert den Standby-Clip einmalig —
unabhängig vom On-Air-Schalter.

## UI-Komponenten

### `LiveTv.razor`

Datei: `VideoWebPlayer/Components/Pages/LiveTv.razor` — Seite `/live-tv`,
`@rendermode InteractiveServer`.

- Zustand: `isAuthenticated`, `tvSenderEnabled`, `tvSenderAccessGranted`, `isLoading`,
  `loadError`, `streamError`(+`streamErrorKind`), `unsupportedBrowser`, `guide`, `upcoming`.
- `OnInitializedAsync` (236–259): Login → `GetTvSenderEnabledAsync` →
  `IsTvSenderAccessGrantedAsync` → `RefreshGuideAsync(true)`. Anzeige-Kaskade (33–51):
  nicht freigeschaltet → `#live-tv-disabled`; kein Zugriff → `#live-tv-no-access`;
  Ladefehler → `#live-tv-error`. Der Off-Air-Zustand käme als eigener Zweig derselben
  Kaskade hinzu (Lesestelle `ProgramSettings` bereits injiziert).
- Player-Bereich (64–87): `guide.Now != null` → `<video>` + `liveTv.attach` (via
  `OnAfterRenderAsync`, SlotId-gesteuertes Attach/Detach, Zeilen 261–292); sonst
  `#live-tv-no-program`-Hinweis. `StreamUrl` = `/api/live-stream/live.m3u8?access_token=…`.
- `PollLoopAsync` (294–320): 15-s-`PeriodicTimer` — `RefreshGuideAsync(false)` holt
  `now`- und `upcoming`-Daten **unabhängig** voneinander (ein Ausfall friert den anderen
  Bereich nicht ein); JS-Callbacks `NotifyStreamPlaying/Error/Unsupported`.
- Programmauskunft-Karten (`livetv-side`): laufen über `/api/program-guide/*` — unbetroffen.

### `LiveTvErrorMessages`

Datei: `VideoWebPlayer/ViewModels/LiveTvErrorMessages.cs` — interne Konstanten
`NoProgramRunning`, `StreamUnavailable`, `UnsupportedBrowser`, `LoadFailed`, `Forbidden`
+ `Describe(Exception)` (403 → `Forbidden`). Kein Off-Air-Text vorhanden.

### `liveTv.js`

Datei: `VideoWebPlayer/wwwroot/js/liveTv.js` — `window.liveTv.attach/detach`; natives HLS
oder hls.js (Lazy-Load), fataler Fehler → `NotifyStreamError` + 5-s-Re-Attach-Timer, bis
`detach` gerufen wird. Ein Off-Air-Hinweis könnte serverseitig ohne Player-Attach
entschieden werden (kein js-Eingriff nötig) oder den Re-Attach weiterlaufen lassen.

### `TVSenderAdmin.razor`

Datei: `VideoWebPlayer/Components/Pages/Admin/TVSenderAdmin.razor` — Seite `/admin/tv-sender`,
admin-only (`isAdmin`-Claim), Top-Gate `tvSenderEnabled` (Zeilen 31–39, gelesen in
`OnInitializedAsync` Zeile 1260 — nicht freigeschaltet → Hinweis mit Link auf
Einrichtung → Allgemein).

- Tabs: `regeln`, `sendeplan`, `einstellungen`, `laufzeit`, `clipgruppen`.
- Tab „Einstellungen" (644–834): Karte „Sender-Einstellungen" mit Sub-Karten
  „Sender" (Name, Bundesland), „Planung" (Horizont, Intervall, Inhaltsspektrum,
  Jugendschutz-Fenster, Unbekannt-Modus), **„Segmentierung"** (ffmpeg-Hinweis +
  Segmentlänge/-verzeichnis/-retention, 717–740) und „Standby-Clip (Überbrückung)"
  (741–823). Speichern: `SaveChannelSettingsAsync` (~1980–2035) →
  `UpdateLiveStreamSettingsAsync` + `UpdateLiveStreamStandbyClipAsync` u. a., danach
  `ScheduleSignal.NotifyScheduleChanged()` (2028) und Status-Text.
- `TVSenderAdminClipGroups.razor` ist eine ausgelagerte Partial-Komponente des
  Clip-Gruppen-Tabs — unbetroffen.

### `ProgramSettings.razor`

Datei: `VideoWebPlayer/Components/Pages/Admin/ProgramSettings.razor` — Seite
`/admin/program-settings` (Einrichtung → Allgemein), admin-only. Karte „TV-Sender"
(94–130): `InputCheckbox` `model.TvSenderEnabled` (Freischaltung) +
Benutzer-Freigabeliste. `SaveAsync` (210–245): `UpdateGeneralSettingsAsync` →
`UpdateTvSenderAllowedUserIdsAsync` → `ScheduleSignal.NotifyScheduleChanged()` —
das etablierte Muster für „sofort wirksam ohne Neustart".

### `NavMenu.razor`

Datei: `VideoWebPlayer/Components/Layout/NavMenu.razor` — Live-TV-Eintrag nur bei
`IsTvSenderAccessGrantedAsync` (Zeilen 41–45, 136) — reine Sichtbarkeit, kein
On-Air-Bezug (Eintrag bleibt Off-Air voraussichtlich sichtbar; die Seite selbst zeigt den
Zustand).

## DI-Registrierungen (relevante)

Datei: `VideoWebPlayer/Extensions/ServiceCollectionExtensions.cs`:

- `ILiveStreamSegmenter` → `FfmpegLiveStreamSegmenter` (Singleton, Zeile 250)
- `LiveStreamMediaResolver`, `LiveStreamSegmentStore`, `LiveStreamCoordinator` (scoped, 251–253)
- `LiveStreamStandbySegmenter`, `LiveStreamStandbyProvider` (Singleton, 254–255)
- `ProgramScheduleSignal` + `IProgramScheduleSignal` (Singleton, 269–270)
- `ProgramSettingsService` (scoped, 281)
- `MediaDurationProbeWorker`, `ProgramScheduleWorker` (Hosted, 259 + 336)
- `LiveStreamWorker` (Hosted, 340 — nicht in `Testing`-Umgebung)
