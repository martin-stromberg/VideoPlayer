# Umsetzungsplan: TV-Sender — Warm-up-Phase mit konfigurierbarem Standby-Clip statt Altsegmenten und „nicht aktiv"-Zwischenphase

## Übersicht

Nach einem App-/Server-Start ist `api/live-stream/live.m3u8` bis zum ersten echten Segment (≈ `_initialDelay` 10 s + Segmentlänge 6 s) nicht verfügbar: Der Client sieht Altsegmente des Vorlaufs oder die „nicht verfügbar"-Zwischenphase (`LiveTvErrorMessages.StreamUnavailable`). Der `LiveStreamController` soll statt 404 eine im Speicher gehaltene Standby-HLS-Playlist ausliefern, solange keine echte `live.m3u8` mit mindestens einem referenzierten Segment existiert — bewusst sichtbar als Hinweis-Clip. Der Clip ist in den Server-Einstellungen konfigurierbar (Medienquelle → MediaItem); ohne Auswahl gilt ein neu erzeugter, fest eingebetteter Funki-Clip „Gleich geht's los!". Zusätzlich wird `ClearAsync` vor `_initialDelay` gezogen und `Cache-Control: no-store` auf alle Stream-Antworten gesetzt.

## Designentscheidungen

| Komponente / Bereich | Gewählter Ansatz | Begründung |
|---|---|---|
| Standby-Standard-Clip | Neuer Funki-Clip „Gleich geht's los!" über `docs/projects/task/issue-111-…/maskottchen/generator/generate_clips.py` erzeugen (neue Szene `gleich-gehts-los` + Jingle-Eintrag in `CLIPS`), als `funki-gleich-gehts-los.mp4` in `maskottchen/clips/` committen, einmalig in `.ts`-Segmente transkodieren und unter `VideoWebPlayer/Assets/LiveStreamStandby/` als `EmbeddedResource` einbetten | Neuer Clip ist ausdrücklich gewollt (Anwender-Entscheidung). Embedded Resource = kein Dateipfad-/Locking-/Deploy-Problem und **kein ffmpeg im Auslieferungspfad** — der Default greift damit auch im Zustand `FfmpegUnavailable`. Rolle: fester Default/Fallback, aktiv wenn kein MediaItem konfiguriert ist oder dessen Vorbereitung (noch) nicht steht. Er ist bewusst **kein** automatisches MediaItem — wer ihn als auswählbares MediaItem will, legt die Datei selbst in eine Clip-Quelle. |
| Konfiguration des Standby-Clips | `Setup.LiveStreamStandbyMediaItemId` (`long?`, **weiche Referenz ohne FK** — Konvention `ProgramSlot.MediaItemId`/`ProgramRule.MaterialId`), Getter `GetLiveStreamStandbyMediaItemIdAsync` + Updater `UpdateLiveStreamStandbyClipAsync(long?)` im `ProgramSettingsService`; `null` = eingebetteter Default | Sender-Konfiguration muss das Löschen eines MediaItems überleben; ein FK mit `SetNull` würde die Einstellung beim Löschen still zurücksetzen statt tolerant auf den Default zu fallen. Die Auflösung behandelt eine unbekannte ID wie „nicht konfiguriert". EF-Migration + `OptionalRestoreColumns`-Eintrag (`Setups.LiveStreamStandbyMediaItemId`, nullable → kein Restore-Default nötig) + Regressionstest mit Alt-Backup. |
| Zulässige Auswahlmenge | Nur **unklassifizierte Video-MediaItems** (keine Film-/Episoden-Verknüpfung) — dieselbe Zulässigkeitsregel wie `IProgramClipGroupService.GetClipCandidatesAsync`/`AddClipsToGroupAsync` | Ein Standby-Slate ist fachlich ein Clip, kein Spielfilm; die Regel ist im Repo bereits etabliert und serverseitig erzwungen, kein neuer Abfrage-/Validierungspfad nötig. |
| Auswahl-UI | `TVSenderAdmin.razor`, Tab „Einstellungen", neue Sub-Card „Standby-Clip (Warm-up-Phase)": aktueller Clip als Chip + Auswahl-Maske **nach dem Muster der Clip-Kandidaten in `TVSenderAdminClipGroups.razor`** (`#clip-candidate-source`-Quellen-`<select>`, 400-ms-Debounce-Suche, Kandidatentabelle Name/Quelle/Laufzeit mit „Weitere laden") — Datenquelle `IProgramClipGroupService.GetClipCandidatesAsync(userId, search, sourceId, skip, take)`; persistiert über den bestehenden `#tvsender-save-settings`-Button | Projekt-Vorgabe (Suche/Browsing/Klartext statt ID-Eingabe) wird exakt durch das vorhandene, bereits E2E-getestete Auswahlmuster erfüllt; `GetClipCandidatesAsync` erzwingt bereits serverseitig Admin (`RequireAdminAsync`). |
| Vorbereitung des konfigurierten Clips | **Voraus-Segmentierung + Cache:** `LiveStreamStandbySegmenter` (Singleton) transkodiert das MediaItem einmalig per ffmpeg (`-f segment`) in das Cache-Verzeichnis `Data/LiveStreamStandby/` (Content-Root, **nicht** im Segment-Verzeichnis → von `ClearAsync`/Retention-Sweep unberührt); `LiveStreamStandbyProvider` lädt die Segmente danach in den Speicher. Transkodier-Kappe `MaxStandbyDuration` = 60 s | „Server hält ihn immer im Speicher" + ffmpeg steht zur Laufzeit zur Verfügung — aber nicht garantiert (`FfmpegUnavailable`), daher Voraus-Segmentierung statt Live-Segmentierung im Auslieferungspfad. Cache auf Platte vermeidet Neu-Transkodierung bei jedem Start; Kappe verhindert, dass ein versehentlich gewählter Langfilm den Speicher bläht. |
| Auslöser der Vorbereitung | Zwei Trigger: (a) `LiveStreamWorker`-Start-Sequenz (`RefreshAsync`), (b) lazy bei jeder Standby-Playlist-Anfrage, wenn die übergebene konfigurierte ID vom vorbereiteten Satz abweicht — dedupliziert über `SemaphoreSlim`, fehlgeschlagene ID wird bis zur nächsten Konfigurationsänderung gemerkt (`_failedMediaItemId`), weiter ausgeliefert wird der bisher aktive Satz | Kein zusätzlicher Hosted Service, keine Blockade im Save-Pfad der Admin-UI. Der erste Request nach Konfigurationsänderung sieht wenige Sekunden noch den Default — fachlich korrekt (Default ist ja genau dafür da). Cache-Hit (Marker `standby-source.json` mit MediaItemId + Quellpfad/Änderungsstempel) lädt von Platte ohne ffmpeg. |
| Auflösung MediaItem → ffmpeg-Eingang | `LiveStreamMediaResolver.ResolveAsync(mediaItemId, runDuration: null, useHttpInput: true)` wiederverwenden (lokale Quelle → Dateipfad, Remote-Quelle → HTTP-/Pipe-Eingang) | Deckt SFTP-Quellen ab, inkl. Pipe-Fallback; nichts Neues erfinden. |
| Form des Standby-Streams | Vorsegmentierte `.ts`-Dateien als `byte[]` im Speicher + dynamisch gebaute, zeitbasiert rotierende Live-Playlist (`#EXT-X-MEDIA-SEQUENCE` aus `utcNow`/Gesamtdauer des aktiven Satzes, Fenster 3 Einträge, `#EXT-X-PROGRAM-DATE-TIME`, kein `#EXT-X-ENDLIST`). Segmentdauern per ffprobe (`IFfprobeLocator`) ermittelt, Fallback nominale 4 s | Rotierende Playlist = übliches Standby-Slate-Muster; statisch würde einfrieren, `ENDLIST` würde die Wiedergabe hart beenden. `.ts` passt zum bestehenden `video/mp2t`-Pfad. |
| Segmentnamen | Generationspräfix: Default-Satz `standby_0_XX.ts`, jeder vorbereitete Custom-Satz `standby_{n}_XX.ts` (n = fortlaufende Generation) | Der Satzwechsel Default→Custom ändert die Segmentanzahl; mit Generationspräfix greifen nur aktuell beworbene URIs — ein Client mit alter Playlist kann kein inzwischen nicht existierendes Segment anfordern (404→fataler Playerfehler vermieden). Lookup bleibt ein strenges Dictionary-Match. |
| Zustandserkennung „erste echte Segmentierung steht" | **Verschärftes Dateikriterium:** `live.m3u8` ist über `LiveStreamSegmentStore.TryResolveFileAsync` auflösbar **und** der gelesene Inhalt referenziert mindestens ein Segment (mindestens eine Nicht-`#`-, Nicht-Leer-Zeile = Media-URI). Nur dann echte Auslieferung, sonst Standby. **Verifikation:** Empirisch belegt (Probe 2026-10-26, ffmpeg 9.0.2, exakt die Flags aus `FfmpegLiveStreamSegmenter.BuildArguments` — `-f hls -hls_time N -hls_list_size 12 -hls_flags delete_segments+independent_segments+append_list+discont_start+program_date_time`): Der HLS-Muxer legt `live.m3u8` erst beim ersten fertig geschriebenen Segment an — keine Header-only-Datei. Das verschärfte Kriterium ist dennoch geplant, weil es zusätzlich gegen Torn Reads beim In-Place-Rewrite der Playlist und gegen künftige ffmpeg-Verhaltensänderungen absichert — die Playlist wird ohnehin zum Rewrite gelesen, also kein zusätzlicher I/O. Ein explizites Flag in `ILiveStreamSegmenter`/`LiveStreamCoordinator` wird nicht eingeführt: Die Datei ist die einzige Wahrheit der Auslieferung; ein Prozess-Flag könnte vom Dateizustand abweichen und wäre ein zweiter konsistent zu haltender Zustand. | Die Anforderung verlangt „Playlist mit mindestens einem referenzierten Segment"; die Probe bestätigt zwar das günstige ffmpeg-Verhalten, aber der Content-Check kostet nichts und deckt die Restfälle ab (siehe Seiteneffekte). |
| Geltungsbereich des Fallbacks | **Nur bei fehlender oder segmentloser `live.m3u8`** — Warm-up nach App-Start sowie alle Zustände ohne verwertbare Playlist (`Stopped` ohne Datei, `FfmpegUnavailable`, `MediaUnavailable`, segmentlose Playlist). Eine **veraltete, liegengebliebene `live.m3u8` mit Segmentreferenzen** (z. B. kurz nach `Stopped`/Sendelücke) wird weiter ausgeliefert wie bisher | Anwender-Entscheidung: Playlist-Altersprüfung ist außerhalb dieses Issues. |
| Übergang Standby → echter Stream | Einmalige Injektion von `#EXT-X-DISCONTINUITY` vor das erste Segment der ersten echten Playlist, die nach einer Standby-Auslieferung ausgeliefert wird (Transition-Flag im Provider, global) | Die echte `live.m3u8` startet ihre Media-Sequence bei 0, die Standby-Playlist zählt zeitbasiert hoch; `DISCONTINUITY` signalisiert den Track-Wechsel standardkonform, Restrisiko federt der 5-s-Re-Attach in `liveTv.js`. |
| Standby-Segmente nach Stream-Start | `TryGetSegment` liefert den aktiven Satz **unabhängig** vom `live.m3u8`-Zustand aus — auch wenn bereits die echte Playlist greift | Gewollt: Ein Client, der die letzte Standby-Playlist noch lädt (Reload-Intervall ~Segmentdauer), darf deren beworbene Segmente nicht mit 404 verlieren — ein Segment-404 gilt in hls.js als fataler Netzwerkfehler und würde genau die Fehlerphase auslösen, die das Feature beseitigt. Segment-Requests steuern den Übergang nicht, nur die Playlist. |
| Altsegmente des Vorlaufs | `LiveStreamSegmentStore.ClearAsync` im `LiveStreamWorker` **vor** `Task.Delay(_initialDelay)` ziehen; `Cache-Control: no-store` auf **allen** ausgelieferten Antworten (`.m3u8`, `.ts`, Standby) | Vorlauf-Dateien sind heute bis 10 s nach Start auslieferbar und `.ts`-Antworten cache-bar. |
| `_initialDelay` (10 s) | Beibehalten | Der Clip überbrückt die Wartezeit fachlich vollständig; der Zweck der Startverzögerung ist nicht dokumentiert, ein Eingriff wäre ungerechtfertigtes Risiko. |
| Sichtbarkeit / UI-Hinweis | Keine UI-Änderung in `LiveTv.razor`: Der Clip spielt normal im Player (`NotifyStreamPlaying`), `#live-tv-stream-error` entsteht gar nicht erst | Die Warm-up-Phase ist bewusst sichtbar (Anwender-Entscheidung) — der Hinweis steckt im Clip selbst („Gleich geht's los!"). Ein Overlay wäre redundant und zustandsblind. |
| Serverseitige Durchsetzung | Admin-Gate `isAdmin` der Blazor-Server-Seite (serverseitig) + `RequireAdminAsync` in `GetClipCandidatesAsync` + Validierung in `UpdateLiveStreamStandbyClipAsync` | Entspricht dem Muster der übrigen `ProgramSettingsService`-Updater; die Kandidaten-Abfrage erzwingt Admin bereits eigenständig. |
| Degradation „fehlende Embedded-Assets" | Default-Asset-Quelle des Providers über internen Konstruktor-Overload abstrahierbar (`internal LiveStreamStandbyProvider(..., Func<StandbySegmentSet?>? loadDefaultSet)` — `null` = Laden aus Embedded Resources). Ergibt das Laden keinen Satz (Packaging-Fehler) → einmaliges `LogError`, aktiver Satz `null` → `BuildPlaylist`/`TryGetSegment` liefern `null` → Controller bleibt beim bisherigen 404 | Der Pfad ist damit direkt testbar (Test injiziert `() => null`) statt nur als Kommentar zu existieren; der Endpunkt degradiert kontrolliert auf den Status quo statt zu crashen. |
| Test-Seam für `PrepareAsync` | Der `internal`-Konstruktor-Overload des `LiveStreamStandbyProvider` erhält neben `loadDefaultSet` zwei weitere optionale Delegates: `Func<long, CancellationToken, Task<LiveStreamInput?>>? resolveMediaInputAsync` (`null` = Produktivpfad: `IServiceScopeFactory`-Scope → `LiveStreamMediaResolver.ResolveAsync(id, runDuration: null, useHttpInput: true)`) und `Func<LiveStreamInput, string, CancellationToken, Task<IReadOnlyList<StandbyTranscodedSegment>?>>? transcodeAsync` (`null` = injizierter `LiveStreamStandbySegmenter`). `PrepareAsync` ruft ausschließlich die Feld-Delegates; der `public`-Konstruktor verdrahtet sie mit den echten Pfaden. `InternalsVisibleTo("VideoWebPlayer.Tests")` ist bereits gesetzt (`VideoWebPlayer.csproj:15`) | Gleiches Seam-Muster wie die `Func<ProcessStartInfo, ISegmenterProcess?>`-Fabrik des `FfmpegLiveStreamSegmenter` (`FfmpegLiveStreamSegmenter.cs:119–127`) und die injizierbare Asset-Quelle: Die `PrepareAsync_*`-Tests injizieren Resolver-/Segmenter-Fakes (z. B. `transcodeAsync`-Fake, der `.ts`-Dateien ins Temp-Cache-Verzeichnis legt und die Segmentliste zurückgibt; `resolveMediaInputAsync`-Fake → `LiveStreamInput.ForFile(...)` bzw. `null`), ohne `LiveStreamMediaResolver` (`public sealed`, scoped) und `LiveStreamStandbySegmenter` (`sealed`) anfassen oder fälschen zu müssen. Interfaces (`ILiveStreamMediaResolver`/`ILiveStreamStandbySegmenter`) wurden geprüft und verworfen — sie würden öffentliche API und DI-Registrierungen nur für Tests erweitern. |

## Programmabläufe

### Standby-Auslieferung der Playlist (Warm-up) — Schritt für Schritt

1. Client (Browser/hls.js in `LiveTv.razor` oder externer Player) ruft `GET api/live-stream/live.m3u8` auf — autorisiert über `Authorization`-Header oder `access_token`-Query.
2. `BearerTokenCheckAttribute` validiert das JWT; `CheckLoggedIn()` → 401 bei fehlender Anmeldung.
3. `ProgramSettingsService.GetTvSenderEnabledAsync` → `false` → 404 (Standby bleibt hinter dem Feature-Flag).
4. `ProgramSettingsService.IsTvSenderAccessGrantedAsync` → verweigert → 403 (unverändert vor jeglicher Auslieferung).
5. `LiveStreamSegmentStore.TryResolveFileAsync("live.m3u8")`:
   - **Pfad gefunden** → Playlist lesen (`FileShare.ReadWrite | FileShare.Delete`) und prüfen:
     - **Mindestens eine Media-URI-Zeile** (Nicht-`#`, Nicht-Leer) → echte Playlist: `Response.Headers.CacheControl = "no-store"`, falls das Transition-Flag gesetzt ist `#EXT-X-DISCONTINUITY` vor die erste Segmentzeile einfügen und Flag verbrauchen, Playlist-Rewrite (`access_token` an Segment-URIs), `ContentResult` mit `application/vnd.apple.mpegurl` → 200.
     - **Keine Media-URI-Zeile** (z. B. Torn Read während des ffmpeg-Rewrites) → wie „`null`" behandeln (Standby), siehe nächster Punkt.
   - **`null` (oder segmentlos)** → Standby: `ProgramSettingsService.GetLiveStreamStandbyMediaItemIdAsync` liefert die konfigurierte ID; `LiveStreamStandbyProvider.BuildPlaylist(configuredId)` — weicht die ID vom aktiven Satz ab → Hintergrund-`PrepareAsync` anstoßen (dedupliziert); Playlist wird über den **aktuell aktiven** Satz gebaut (Default oder zuletzt vorbereiteter Custom-Satz), Transition-Flag wird gesetzt (`NoteStandbyPlaylistServed`), `access_token`-Rewrite wie bei echten Playlists, `no-store` → 200 `application/vnd.apple.mpegurl`. Ist auch kein Standby-Satz aktiv (fehlende Embedded-Assets, `BuildPlaylist` → `null`), bleibt es beim bisherigen 404.

Beteiligte Klassen/Komponenten: `LiveStreamController`, `LiveStreamStandbyProvider` (neu), `LiveStreamSegmentStore`, `ProgramSettingsService`, `BearerTokenCheckAttribute`, `ApiBaseController`.

### Standby-Segment-Auslieferung — Schritt für Schritt

1. Client fordert ein Segment aus der zuletzt erhaltenen Standby-Playlist: `GET api/live-stream/standby_{n}_XX.ts` (mit `access_token`).
2. Authentifizierung + Freigabeprüfung wie oben (Schritte 2–4).
3. `TryResolveFileAsync` → `null` (die Datei existiert nicht im Segment-Verzeichnis).
4. `LiveStreamStandbyProvider.TryGetSegment(fileName)` → Dictionary-Treffer im aktiven Satz → `byte[]` als `FileContentResult` mit `video/mp2t` + `no-store` → 200. Kein Treffer (fremde/veraltete Generation, unbekannter Name) → 404 wie bisher.

Beteiligte Klassen/Komponenten: `LiveStreamController`, `LiveStreamStandbyProvider`, `LiveStreamSegmentStore`, `ProgramSettingsService`.

### Vorbereitung eines konfigurierten Standby-Clips — Schritt für Schritt

1. `LiveStreamStandbyProvider.PrepareAsync(targetMediaItemId)` läuft unter einem `SemaphoreSlim` (Singleton, prozessweit dedupliziert).
2. `targetMediaItemId == null` → aktiver Satz wird der eingebettete Default (keine externen Abhängigkeiten).
3. Cache-Prüfung: `Data/LiveStreamStandby/standby-source.json` enthält dieselbe MediaItemId (plus Quell-Deskriptor) und alle `.ts` vorhanden → Segmente von Platte in den Speicher laden, Satz wechseln, fertig.
4. Sonst: `_resolveMediaInputAsync(id)` — Produktivpfad: Scope über `IServiceScopeFactory` → `LiveStreamMediaResolver.ResolveAsync(id)` — liefert `LiveStreamInput` (`null` → Warnlog, `_failedMediaItemId` merken, aktiver Satz bleibt).
5. `_transcodeAsync(input, cacheDir)` — Produktivpfad: `LiveStreamStandbySegmenter.TranscodeAsync` — startet ffmpeg einmalig (`-t` Kappe 60 s, `-f segment`, `standby_%03d.ts`), drainiert stderr analog `DrainStandardErrorAsync`; Rückgabe `null` (ffmpeg fehlt, Exit ≠ 0 oder keine Segmente) → Warnlog, `_failedMediaItemId`, aktiver Satz bleibt.
6. Anhand der gelieferten `StandbyTranscodedSegment`-Liste (Dateiname + Dauer, ffprobe-Fallback nominal 4 s) den Marker `standby-source.json` schreiben, die `.ts`-Dateien aus dem Cache-Verzeichnis als `byte[]` laden, neuen `StandbySegmentSet` (Generation hochzählen) atomar als aktiv setzen.
7. Ab dem nächsten `BuildPlaylist` wird der Custom-Satz beworben.

Beteiligte Klassen/Komponenten: `LiveStreamStandbyProvider`, `LiveStreamStandbySegmenter` (neu), `LiveStreamMediaResolver`, `ProgramSettingsService`, `IFfmpegLocator`/`IFfprobeLocator`, `IServiceScopeFactory`, `ApplicationDbContext`.

### Start-Sequenz des Workers (geändert) — Schritt für Schritt

1. `LiveStreamWorker.ExecuteAsync` startet.
2. **Neu:** sofort `LiveStreamSegmentStore.ClearAsync` in einem Scope — Vorlauf-Reste sind ab jetzt nicht mehr auslieferbar.
3. **Neu:** `LiveStreamStandbyProvider.RefreshAsync` — konfigurierte ID lesen und Cache-Vorbereitung anstoßen (nicht blockierend kritisch; Fehler → Warnlog, Default bleibt).
4. Danach `Task.Delay(_initialDelay)` (10 s, unverändert).
5. Poll-Schleife wie bisher; ab `live.m3u8` mit erstem referenzierten Segment greift automatisch der echte Auslieferungspfad.

Beteiligte Klassen/Komponenten: `LiveStreamWorker`, `LiveStreamSegmentStore`, `LiveStreamStandbyProvider`, `LiveStreamCoordinator`, `ILiveStreamSegmenter`, `ProgramSettingsService`, `IBackgroundProcessingGate`, `IProgramScheduleSignal`.

### Admin-Einstellung „Standby-Clip" — Schritt für Schritt

1. Admin öffnet `/admin/tv-sender`, Tab „Einstellungen" (Gate: `isAdmin`, `tvSenderEnabled` wie bisher).
2. Sub-Card „Standby-Clip (Warm-up-Phase)" zeigt den aktuellen Stand: Chip „Standard-Clip (Gleich geht's los)" oder „{Name} ({Quelle})" mit „Ändern".
3. In der Auswahl-Maske wählt der Admin zuerst eine **Medienquelle** im `<select>` (Liste `mediaSources`, bereits geladen), danach ein **MediaItem** aus der Kandidatentabelle (Suche + „Weitere laden" über `IProgramClipGroupService.GetClipCandidatesAsync` — erzwingt Admin serverseitig).
4. „Speichern" ruft `SaveChannelSettingsAsync` → zusätzlich `ProgramSettings.UpdateLiveStreamStandbyClipAsync(standbyMediaItemId)`; Validierungsfehler erscheinen in `#tvsender-channel-status`.
5. Die neue Auswahl wirkt ab der nächsten Standby-Anfrage (lazy prepare) bzw. spätestens nach dem nächsten Worker-Start.

Beteiligte Klassen/Komponenten: `TVSenderAdmin.razor`, `IProgramClipGroupService`/`ProgramClipGroupService`, `ProgramSettingsService`, `LiveStreamStandbyProvider`.

## Neue Klassen

| Klasse | Typ | Zweck |
|---|---|---|
| `LiveStreamStandbyProvider` | `public sealed class`, Singleton (`VideoWebPlayer/Services/LiveStreamStandbyProvider.cs`) | Hält den aktiven `StandbySegmentSet` atomar austauschbar im Speicher (`null` = kein Standby verfügbar → 404-Degradation); lädt die eingebetteten Default-Segmente beim Start; `BuildPlaylist(long? configuredMediaItemId)` erzeugt die rotierende Playlist über `TimeProvider` und stößt bei Konfigurationsabweichung `PrepareAsync` an; `PrepareAsync`/`RefreshAsync` orchestrieren Cache-Prüfung, MediaItem-Auflösung (`IServiceScopeFactory` → `LiveStreamMediaResolver`), Transkodierung und Satzwechsel; `TryGetSegment(string)` liefert Segment-Bytes nach Dictionary-Match; Transition-Flag (`NoteStandbyPlaylistServed`/`ConsumePendingStandbyTransition`); `_failedMediaItemId` unterdrückt Retry-Stürme. Interner Konstruktor-Overload mit injizierbarer Default-Asset-Quelle (`Func<StandbySegmentSet?>?`) für den Degradationstest **sowie injizierbaren `resolveMediaInputAsync`-/`transcodeAsync`-Delegates für die `PrepareAsync`-Tests** (Signatur und Begründung: Designentscheidung „Test-Seam"). |
| `LiveStreamStandbySegmenter` | `public sealed class`, Singleton (`VideoWebPlayer/Services/LiveStreamStandbySegmenter.cs`) | Führt die einmalige ffmpeg-Transkodierung eines `LiveStreamInput` in das Cache-Verzeichnis aus (Segment-Muxer, `-t`-Kappe 60 s, Keyframe-Grenzen, stderr-Drain mit Token-Maskierung analog `FfmpegLiveStreamSegmenter`). Signatur: `TranscodeAsync(LiveStreamInput input, string cacheDirectory, CancellationToken)` → `Task<IReadOnlyList<StandbyTranscodedSegment>?>` — `null` bei fehlendem ffmpeg, Exit ≠ 0 oder keinen produzierten Segmenten, sonst die erzeugten `.ts`-Dateinamen samt per ffprobe ermittelter Dauer (Fallback nominal 4 s). Prozessstart über internen Konstruktor-Overload mit `Func<ProcessStartInfo, ISegmenterProcess?>` (gleiches Muster wie `FfmpegLiveStreamSegmenter` Z. 119–127) für `LiveStreamStandbySegmenterTests`; `IFfmpegLocator`/`IFfprobeLocator` für Pfadauflösung und Segmentdauern. |
| `StandbySegmentSet` | `internal sealed record` (in `LiveStreamStandbyProvider.cs`) | Immutabler Satz: `Generation`, `MediaItemId` (`null` = Default), `TotalDuration`, Liste der Segmente (Name, `Duration`, `byte[]`) — atomarer Referenzwechsel statt Locking im Anfragpfad. |
| `StandbyTranscodedSegment` | `internal sealed record` (in `LiveStreamStandbySegmenter.cs`) | Ergebnis eines produzierten Standby-Segments: `FileName`, `Duration` — Elementtyp der `TranscodeAsync`-Rückgabe, Baustein zum Befüllen des `StandbySegmentSet` (Bytes lädt der Provider aus dem Cache-Verzeichnis). |

Neue Dateien (keine Klassen): `VideoWebPlayer/Assets/LiveStreamStandby/standby_XX.ts` — einmalig aus dem neuen `funki-gleich-gehts-los.mp4` generiert (Transkodierung mit Keyframe-Grenzen; exakter Befehl + Segmentdauern als Kommentar/Konstanten im Provider dokumentiert).

## Änderungen an bestehenden Klassen

### `Setup` (`VideoWebPlayer/Data/Setup.cs`)

- Neue Eigenschaft `long? LiveStreamStandbyMediaItemId` — Admin-konfiguriertes Standby-MediaItem, `null` = eingebetteter Default-Clip. Weiche Referenz ohne FK (Konvention `ProgramSlot.MediaItemId`).

### `ProgramSettingsService` (`VideoWebPlayer/Services/ProgramSettingsService.cs`)

- Neu: `GetLiveStreamStandbyMediaItemIdAsync` → `long?`.
- Neu: `UpdateLiveStreamStandbyClipAsync(long? mediaItemId, CancellationToken)` — `null` löscht die Auswahl; sonst Validierung (MediaItem existiert, `VideoFileExtensions.IsVideoFileName`, nicht klassifiziert → `ArgumentException`/`KeyNotFoundException`), dann speichern.

### `LiveStreamController` (`VideoWebPlayer/Controllers/LiveStreamController.cs`)

- Neuer Konstruktor-Parameter `LiveStreamStandbyProvider`.
- `GetFile` — geändert:
  - `.m3u8`-Pfad: Datei lesen; **nur bei mindestens einer Media-URI-Zeile** echte Auslieferung (DISCONTINUITY-Injektion bei gesetztem Transition-Flag, Rewrite, `no-store`); segmentlose Playlist fällt in den Standby-Pfad.
  - Standby-Verzweigung (bei `path is null` **oder** segmentloser `live.m3u8`): (a) `fileName == FfmpegLiveStreamSegmenter.PlaylistFileName` → `GetLiveStreamStandbyMediaItemIdAsync` + `BuildPlaylist(id)` (`null` → 404) + Transition-Flag + Rewrite + `no-store`; (b) `TryGetSegment(fileName)` trifft → Speicher-Bytes `video/mp2t` + `no-store`; (c) sonst 404 wie bisher.
  - `Cache-Control: no-store` auf **alle** erfolgreichen Antworten (`.m3u8`, `.ts`, Standby).
- `RewritePlaylistAsync` — Datei-Lesen und Rewrite trennen; `RewritePlaylistContent(string content, string accessToken)` für echte und Standby-Playlist gemeinsam; Hilfsfunktion `PlaylistReferencesSegment(string content)` (≥ 1 Nicht-`#`-, Nicht-Leer-Zeile).
- Neue interne Hilfslogik: bei echter `.m3u8` `ConsumePendingStandbyTransition()` → ggf. `#EXT-X-DISCONTINUITY` vor erster Segmentzeile.

### `LiveStreamWorker` (`VideoWebPlayer/Services/LiveStreamWorker.cs`)

- Neuer Konstruktor-Parameter `LiveStreamStandbyProvider` (Singleton — direkte Injektion, kein Scope nötig).
- `ExecuteAsync` — geändert: `ClearAsync`-Scope vor `Task.Delay(_initialDelay)`; danach `LiveStreamStandbyProvider.RefreshAsync` anstoßen (Fehler toleriert → Warnlog).

### `TVSenderAdmin.razor` (`VideoWebPlayer/Components/Pages/Admin/`)

- Einstellungen-Tab: neue Sub-Card „Standby-Clip (Warm-up-Phase)" — aktueller Clip (`standbyMediaItemId` + Anzeigename aus `MediaItem`/`MediaCollection`/`MediaSource` beim Init, `#tvsender-standby-current`), Auswahl-Maske `#tvsender-standby-picker` mit Quellen-`<select>` `#tvsender-standby-source`, Suche `#tvsender-standby-search` (400-ms-Debounce-Muster aus `TVSenderAdminClipGroups`), Kandidatentabelle `.standby-candidate-row`/`.standby-candidate-pick` + „Weitere laden" + „Standard-Clip verwenden" (`#tvsender-standby-clear`).
- `OnInitializedAsync`/`SaveChannelSettingsAsync` — Standby-Wert laden bzw. über `UpdateLiveStreamStandbyClipAsync` speichern.

### `ServiceCollectionExtensions` (`VideoWebPlayer/Extensions/ServiceCollectionExtensions.cs`)

- `services.AddSingleton<LiveStreamStandbyProvider>()` und `services.AddSingleton<LiveStreamStandbySegmenter>()` im Live-Stream-Block (~Z. 249–252). Singleton nötig — Speicherbestand, Generation und Transition-Flag gelten prozessweit.

### `VideoWebPlayer.csproj`

- `<EmbeddedResource Include="Assets\LiveStreamStandby\*.ts" />` für die Default-Segmente.

### `VideoWebPlayerBackupData` (`VideoWebPlayer/Services/Backups/`)

- `OptionalRestoreColumns` += `$"{nameof(ApplicationDbContext.Setups)}.{nameof(Setup.LiveStreamStandbyMediaItemId)}"` (nullable → kein Eintrag in den Defaults-Tabellen nötig).

### `generate_clips.py` (`docs/projects/task/issue-111-…/maskottchen/generator/`)

- Neue Szene `scene_gleich_gehts_los` (Funki aktiv, Text „Gleich geht's los!") + kleines Jingle + Eintrag `"gleich-gehts-los"` in `CLIPS`; passendes Drehbuch unter `maskottchen/drehbuecher/` ergänzen.

### `TVSenderAdminClipGroupsE2ETests` / `TVSenderAdminE2ETestBase` (Test-Hilfen)

- `SeedClipItemsAsync` (bislang `private` in `TVSenderAdminClipGroupsE2ETests`, Z. ~71) als `protected`-Hilfsmethode in `TVSenderAdminE2ETestBase` hochziehen, damit der neue Admin-E2E-Test der Standby-Auswahl dieselbe Seed-Logik nutzt; `EnsureMediaSourceIdAsync` ist bereits `protected static` in `PlaylistsE2ETestBase` (Z. ~953).

## Datenbankmigrationen

| Migration | Inhalt |
|---|---|
| `{timestamp}_AddSetupLiveStreamStandbyMediaItem` | `AddColumn<long>("LiveStreamStandbyMediaItemId", "Setups", nullable: true)` — weiche Referenz, kein Index, kein FK. |

## Validierungsregeln

| Regel | Ort | Durchsetzung |
|---|---|---|
| Standby-MediaItem: `null` oder existierendes, unklassifiziertes Video (`VideoFileExtensions.IsVideoFileName`, kein `MovieMediaItems`-/`TVShowEpisodeMediaItems`-Link) | `ProgramSettingsService.UpdateLiveStreamStandbyClipAsync` | `ArgumentException`/`KeyNotFoundException` → Meldung in `#tvsender-channel-status` |
| Admin-Zwangsprüfung der Kandidatenliste | `ProgramClipGroupService.GetClipCandidatesAsync` (`RequireAdminAsync`, bestehend) | `ForbiddenAccessException`/`UnauthorizedAccessException` |
| Standby-Segmentnamen: nur aktive Generation | `LiveStreamStandbyProvider.TryGetSegment` | Strenges Dictionary-Match, kein Pfadzugriff; `SafeFileName`-Whitelist bleibt für Dateipfade wirksam |
| Echte Playlist nur bei ≥ 1 referenziertem Segment | `LiveStreamController.GetFile` (`PlaylistReferencesSegment`) | Content-Check über dem ohnehin gelesenen Playlist-Text; segmentlos → Standby-Pfad |

## Konfigurationsänderungen

| Einstellung | Speicher | Standard |
|---|---|---|
| `Setups.LiveStreamStandbyMediaItemId` | `Setup`-Spalte (`long?`), Admin-UI `TVSenderAdmin` Tab „Einstellungen" | `null` = eingebetteter Clip „Gleich geht's los!" |

## Seiteneffekte und Risiken

- **`GetFile_InvalidOrMissing_ReturnsNotFound`** (`LiveStreamControllerTests`, `[InlineData("live.m3u8")]`): fehlende `live.m3u8` liefert künftig 200 + Standby-Playlist → Testfall anzupassen.
- **Testing-/E2E-Hosts:** `LiveStreamWorker` ist in Umgebung `Testing` nicht registriert → keine echte `live.m3u8`; `live.m3u8`-Anfragen liefern dort grundsätzlich Standby (gewollt, deterministisch). Die Vorbereitung eines konfigurierten Clips läuft dort über den lazy Trigger — E2E mit echtem ffmpeg nur wo ffmpeg vorhanden (stiller Rückkehr wie `FfmpegLiveStreamSegmenterE2ETests`).
- **Segmentlose `live.m3u8` (Torn Read / geändertes ffmpeg-Verhalten):** wird wie fehlende Datei behandelt → Standby statt leerer Playlist; die empirische Probe (ffmpeg 9.0.2, Build-Flags aus `BuildArguments`, 2026-10-26) zeigt, dass ffmpeg die Datei erst mit dem ersten Segment anlegt — der Fall sollte daher selten bleiben. Nebenwirkung: ein Torn Read setzt das Transition-Flag, sodass die nächste echte Auslieferung ein technisch überflüssiges, aber harmloses `DISCONTINUITY` trägt.
- **Veraltete `live.m3u8` mit Segmentreferenzen** (z. B. liegengebliebene Playlist, deren `seg_*.ts` vom `delete_segments`-Flag/Retention-Sweep schon gelöscht sind): bleibt „echt" und wird wie bisher ausgeliefert — Playlist-Altersprüfung ist bewusst außerhalb des Issues (Anwender-Entscheidung).
- **Lazy-Wechsel:** nach Konfigurationsänderung liefert der erste Standby-Request noch den bisherigen Satz bis `PrepareAsync` fertig ist (Sekunden) — bewusstes Degradationsverhalten, kein Fehlerbild.
- **Fehlgeschlagene Vorbereitung** (ffmpeg fehlt, Medium unauflösbar, Transcode-Exit ≠ 0): ID wird bis zur Konfigurationsänderung gemerkt, Warnlog, aktiver Satz bleibt — kein Request-Loop gegen ffmpeg.
- **Fehlende Embedded-Assets** (Packaging-Fehler): einmaliges `LogError`, aktiver Satz `null`, Endpunkt degradiert auf 404 — abgesichert über injizierbare Asset-Quelle + Tests.
- **Cache-Verzeichnis `Data/LiveStreamStandby/`:** neues Content-Root-Unterverzeichnis; Backup-/Update-Routinen, die `Data/` behandeln, prüfen (untraced Cache, nicht backup-relevant — kein `FilePayload`-Eintrag).
- **hls.js-Übergang:** Media-Sequence-Rücksprung beim Wechsel Standby→echt; DISCONTINUITY + 5-s-Re-Attach decken das ab.
- **Transition-Flag global** (nur ein Stream): theoretisch einmal `DISCONTINUITY` zu viel bei zwei gleichzeitig wechselnden Clients — harmlos.
- **Speicherbedarf:** Default ~400 KB + Custom ≤ ~2 MB (60-s-Kappe) prozessweit — vernachlässigbar.
- **Soft-Referenz ohne FK:** wird das konfigurierte MediaItem gelöscht, bleibt die ID stehen; `PrepareAsync` behandelt sie als Fehlschlag (`_failedMediaItemId`) → Default greift weiter. Bewusst so (Setup überlebt Media-Löschung), in der Hilfe dokumentieren.
- **Entfernte Quelle als Standby-Clip:** `ResolveAsync` liefert HTTP-/Pipe-Eingang; Transkodierung braucht Netzwerkzugriff auf die Quelle — fällt sie aus → Default.
- **Deploy:** Embedded Resources landen in der Assembly → kein IIS-Sonderfall; der neue `<EmbeddedResource>`-Eintrag ist im Release-Build zu verifizieren (CI baut Release).

## Umsetzungsreihenfolge

1. **„Gleich geht's los"-Clip produzieren und einbetten**
   - Voraussetzungen: `generate_clips.py` vorhanden; Python + Pillow + ffmpeg/ffprobe im PATH (nur einmalig).
   - Beschreibung: Szene `gleich-gehts-los` + Jingle in `generate_clips.py`, Drehbuch-Datei, `funki-gleich-gehts-los.mp4` unter `maskottchen/clips/` rendern und committen; MP4 einmalig in `.ts`-Segmente transkodieren → `VideoWebPlayer/Assets/LiveStreamStandby/standby_XX.ts` committen; `EmbeddedResource` in `VideoWebPlayer.csproj`; Segmentdauern per ffprobe als Konstanten-Grundlage sichern.

2. **`Setup`-Spalte, Migration, Backup-Registrierung, Settings-Methoden**
   - Voraussetzungen: keine.
   - Beschreibung: `Setup.LiveStreamStandbyMediaItemId`; `dotnet ef migrations add AddSetupLiveStreamStandbyMediaItem`; `OptionalRestoreColumns`-Eintrag; `GetLiveStreamStandbyMediaItemIdAsync` + `UpdateLiveStreamStandbyClipAsync` (mit Validierung) im `ProgramSettingsService`.

3. **`LiveStreamStandbyProvider` + `LiveStreamStandbySegmenter` anlegen und registrieren**
   - Voraussetzungen: Schritte 1–2.
   - Beschreibung: `StandbySegmentSet` + `StandbyTranscodedSegment`, Default-Ladevorgang aus Embedded Resources (über injizierbare Asset-Quelle für den Degradationstest), `BuildPlaylist(configuredId)` über `TimeProvider` (rotierendes Fenster, Generationsnamen, PROGRAM-DATE-TIME, kein ENDLIST), `TryGetSegment`, Transition-Flag, `PrepareAsync` (Cache-Marker, `LiveStreamMediaResolver` via `IServiceScopeFactory`, `_failedMediaItemId`) über die Feld-Delegates `resolveMediaInputAsync`/`transcodeAsync` (interner Ctor-Overload, Test-Seam lt. Designentscheidung), `TranscodeAsync` mit injizierbarer Prozess-Factory (`Func<ProcessStartInfo, ISegmenterProcess?>`); Singleton-Registrierungen.

4. **`LiveStreamController` umbauen**
   - Voraussetzungen: Schritt 3.
   - Beschreibung: Provider injizieren; `no-store` auf allen Antworten; verschärftes Playlist-Kriterium (`PlaylistReferencesSegment` — echte Auslieferung nur bei ≥ 1 Media-URI-Zeile); Standby-Verzweigungen im `path is null`- bzw. segmentlos-Pfad; `RewritePlaylistContent` extrahieren; DISCONTINUITY-Injektion.

5. **`LiveStreamWorker`: `ClearAsync` vor `_initialDelay` + `RefreshAsync`-Trigger**
   - Voraussetzungen: Schritt 3 (für Refresh; die Clear-Umordnung selbst ist unabhängig).
   - Beschreibung: `LiveStreamStandbyProvider` als Konstruktor-Parameter; Reihenfolge in `ExecuteAsync` umdrehen, Standby-Refresh anstoßen.

6. **Admin-UI „Standby-Clip"**
   - Voraussetzungen: Schritt 2 (Settings-Methoden), `mediaSources`-Laden und Clip-Kandidaten-Muster vorhanden.
   - Beschreibung: Sub-Card im Einstellungen-Tab, Auswahl-Maske nach `TVSenderAdminClipGroups`-Muster, Save-Wiring in `SaveChannelSettingsAsync`; `SeedClipItemsAsync` nach `TVSenderAdminE2ETestBase` heben (für den E2E in Schritt 8).

7. **Unit-/Integrationstests**
   - Voraussetzungen: Schritte 2–6.
   - Beschreibung: `LiveStreamStandbyProviderTests` + `LiveStreamStandbySegmenterTests` neu; `LiveStreamControllerTests`, `LiveStreamWorkerTests` (beide Konstruktor-Aufrufe), `ProgramSettingsServiceLiveStreamTests`, `TVSenderAdminTests` (bUnit) anpassen/erweitern; Backup-Regressionstest `VideoWebPlayerBackupDataTests_LiveStreamStandbyMediaItem`.

8. **E2E-Tests**
   - Voraussetzungen: Schritte 4–6.
   - Beschreibung: `LiveStreamE2ETests` (Standby-Playlist/-Segment/`no-store`, **Übergang Standby → echte Playlist**, Custom-Clip-Vorbereitung mit echtem ffmpeg); `LiveTvE2ETests` (Warm-up-Fluss + Auswirkungsprüfung `LiveTv_ViaNavMenu_ShowsPlayerAndProgram`); `TVSenderAdminE2ETests` (Clip-Auswahl + Persistenz).

9. **Build-Verifikation Debug + Release**
   - Voraussetzungen: Schritte 1–8.
   - Beschreibung: `dotnet build VideoPlayer.sln -c Debug` **und** `-c Release` fehlerfrei — der neue `<EmbeddedResource>`-Eintrag muss im Release-Build ausdrücklich geprüft werden (CI baut Release; vgl. DoD). Volle Testsuite `dotnet test VideoWebPlayer.Tests/VideoWebPlayer.Tests.csproj` grün.

10. **Dokumentation**
    - Voraussetzungen: Schritte 4–6.
    - Beschreibung: `docs/help/tv-sender.md` (Standby-Verhalten inkl. konfigurierbarem Clip, Cache-Verzeichnis, `no-store` auf Segmenten, Clear-Reihenfolge, geänderte 404-/Standby-Semantik — Z. ~420–447), `docs/RELEASE_NOTES.md`; **`docs/API.md`** aktualisieren — Abschnitt „GET /api/live-stream/{fileName}" (Z. ~565–578) dokumentiert den externen Vertrag und enthält künftig falsche Aussagen: die Behauptung „Ungültige oder fehlende Dateinamen — einschließlich des Zustands ‚es wird gerade kein Stream erzeugt' … — werden mit `404` beantwortet" gilt nicht mehr. Anzupassen auf: fehlende oder segmentlose `live.m3u8` liefert künftig `200` mit der Standby-Playlist (`application/vnd.apple.mpegurl`, `standby_*`-Segment-URIs mit `access_token`-Rewrite, `no-store`; `404` nur, wenn kein Standby-Satz verfügbar ist); `standby_*`-Segmente werden aus dem Speicher mit `200` `video/mp2t` ausgeliefert (keine Datei im Segment-Verzeichnis nötig); `.ts`-Antworten tragen künftig ebenfalls `Cache-Control: no-store`. Hinweis: `docs/API.md` wird nicht durch `ApiDocumentationContractTests` abgesichert — eine veraltete Doku fiele nicht im Testlauf auf. **`README.md` auf Änderungsbedarf prüfen** (DoD „bei Bedarf" — Abschnitt über TV-Sender/Live-Stream).

## Tests

### Neue Tests

| Test/Hilfsmethode | Testklasse | Was wird geprüft? |
|---|---|---|
| `BuildPlaylist_RotatesWindowAndAdvancesMediaSequence` | `LiveStreamStandbyProviderTests` (neu) | Zwei Aufrufe zu unterschiedlichen `utcNow`: MEDIA-SEQUENCE wächst, Fenster rotiert über `standby_0_XX.ts`, `TARGETDURATION` korrekt, kein `ENDLIST`, `PROGRAM-DATE-TIME` vorhanden. |
| `TryGetSegment_ActiveSet_ReturnsBytes_UnknownNameReturnsNull` | `LiveStreamStandbyProviderTests` | Aktueller Generationsname → Bytes (TS-Sync-Byte `0x47`); fremde Generation, `seg_x.ts`, Traversal → `null`. |
| `EmbeddedDefault_LoadsOnConstruction` | `LiveStreamStandbyProviderTests` | Default-Konstruktor lädt alle committeten Default-Ressourcen, Default-Satz aktiv — sichert den csproj-Eintrag ab. |
| `MissingDefaultAssets_BuildPlaylistReturnsNull_LogsError` | `LiveStreamStandbyProviderTests` | Injizierte Asset-Quelle `() => null` → `BuildPlaylist`/`TryGetSegment` liefern `null`, `LogError` einmalig — Degradationspfad „fehlende Embedded-Assets". |
| `BuildPlaylist_ConfiguredIdDiffers_ServesActiveSet_AndTriggersPrepare` | `LiveStreamStandbyProviderTests` | Bei konfigurierter ID ≠ aktiver Satz wird weiter der aktive Satz ausgeliefert und `PrepareAsync` genau einmal angestoßen (Semaphore-Dedup). |
| `PrepareAsync_Success_SwitchesToCustomSet` | `LiveStreamStandbyProviderTests` | Über den internen Ctor-Overload injizierte `resolveMediaInputAsync`-/`transcodeAsync`-Delegates (Test-Seam lt. Designentscheidung): Resolve-Fake → `LiveStreamInput.ForFile(...)`, Transcode-Fake legt `.ts`-Dateien ins Temp-Cache-Verzeichnis und liefert die Segmentliste → aktiver Satz trägt `MediaItemId` + neue Generationsnamen; Playlist bewirbt Custom-Namen. |
| `PrepareAsync_Failure_KeepsPreviousSet` | `LiveStreamStandbyProviderTests` | Resolve-Delegate liefert `null` bzw. Transcode-Delegate `null` → aktiver Satz bleibt, `_failedMediaItemId` verhindert Sofort-Retry, Warnlog. |
| `PrepareAsync_NullTarget_SwitchesBackToDefault` | `LiveStreamStandbyProviderTests` | Abgewählte Konfiguration → Default-Satz wieder aktiv; weder Resolve- noch Transcode-Delegate werden aufgerufen. |
| `PrepareAsync_CacheHit_LoadsFromDisk_WithoutFfmpeg` | `LiveStreamStandbyProviderTests` | Marker + `.ts` im Cache-Verzeichnis → weder Resolve- noch Transcode-Delegate werden aufgerufen (Aufrufzähler-Assertion), Satzwechsel aus Datei. |
| `TranscodeAsync_BuildsSegmentArgs_AndAppliesDurationCap` | `LiveStreamStandbySegmenterTests` (neu) | Fake-Prozess: `-f segment`, `-t 60`-Kappe, Keyframe-Ausdruck, Zielmuster `standby_%03d.ts`; stderr wird gedraint; Exit ≠ 0 → Fehler ohne Datei-Übernahme. |
| `TransitionFlag_NoteAndConsume` | `LiveStreamStandbyProviderTests` | `NoteStandbyPlaylistServed` → `ConsumePendingStandbyTransition()` einmal `true`, danach `false`. |
| `GetFile_MissingPlaylist_ServesStandbyPlaylist` | `LiveStreamControllerTests` | Ohne `live.m3u8`: `ContentResult` `application/vnd.apple.mpegurl`, `standby_`-URIs mit `access_token`-Rewrite, `no-store`, kein 404. |
| `GetFile_SegmentlessPlaylist_ServesStandbyPlaylist` | `LiveStreamControllerTests` | `live.m3u8` existiert, referenziert aber kein Segment (nur Header/Torn-Read-Simulation) → Standby-Playlist statt leerer echter Playlist — sichert das verschärfte Kriterium ab. |
| `GetFile_StandbySegment_ServedFromMemory` | `LiveStreamControllerTests` | Aktiver `standby_*`-Name ohne Datei → 200 `video/mp2t` + `no-store` + Bytes; auch **nach** vorhandener echter `live.m3u8` weiter auslieferbar (bewusstes Verhalten). |
| `GetFile_TsSegment_SetsNoStoreCacheHeader` | `LiveStreamControllerTests` | Reale `.ts`-Auslieferung trägt `Cache-Control: no-store`. |
| `GetFile_RealPlaylistAfterStandby_InjectsDiscontinuity` | `LiveStreamControllerTests` | Standby-Aufruf, dann echte `live.m3u8` mit Segment → `#EXT-X-DISCONTINUITY` vor erstem Segment; zweiter Abruf ohne Standby → kein weiteres Tag. |
| `GetFile_FeatureDisabled_Returns404` | `LiveStreamControllerTests` | `[Theory]` mit `[InlineData("live.m3u8")]` + `[InlineData("standby_0_00.ts")]`: **vor** dem Aufruf `TvSenderEnabled` deaktivieren — `ProgramScheduleTestDb` seedet das Flag immer `true`, daher im Testkörper das Flag auf der geseedeten Zeile zurücksetzen (vorhandenes Muster `ProgramGuideControllerTests.GetNow_FeatureNotReleased_Returns404`, Z. ~57: `(await _fixture.Db.Setups.FirstAsync(ct)).TvSenderEnabled = false; await _fixture.Db.SaveChangesAsync(ct)` — alternativ `ProgramSettingsService.UpdateTvSenderEnabledAsync(false, ct)` wie in `TvSenderUserAccessTests`/`InternalMediaStreamControllerTests`). Trotz aktivem Standby-Satz (echter Provider aus der Assembly) muss `GetFile` weiterhin `NotFoundResult` liefern — Standby bleibt hinter dem Feature-Flag; eine Verdrahtung des Fallbacks vor der Flag-Prüfung schlägt hier fehl. |
| `GetFile_MissingPlaylist_WithoutStandbyAssets_Returns404` | `LiveStreamControllerTests` | Controller mit Provider ohne aktiven Satz (injiziertes Asset-Double `null`) → 404 wie bisher — Degradationspfad. |
| `ExecuteAsync_ClearsSegmentDirectory_BeforeInitialDelay` | `LiveStreamWorkerTests` | `initialDelay > 0` mit `ControllableTimeProvider`: Altsegment gelöscht, bevor der Delay-Timer feuert; Standby-Refresh aufgerufen. |
| `GetLiveStreamStandbyMediaItemId_Default_IsNull` + `UpdateLiveStreamStandbyClipAsync_Roundtrip` | `ProgramSettingsServiceLiveStreamTests` | Default `null`; Setzen/Lesen/Leeren rundtrippelt. |
| `UpdateLiveStreamStandbyClipAsync_Invalid_Throws` | `ProgramSettingsServiceLiveStreamTests` | Unbekannte ID, klassifiziertes Item (Movie-/Episode-Link), Nicht-Video-Datei → Fehler, Setup unverändert (echtes SQLite). |
| `ReadFromAsync_LegacyBackupWithoutStandbyColumn_RestoresNull` | `VideoWebPlayerBackupDataTests_LiveStreamStandbyMediaItem` (neu) | `LegacyBackupArchiveBuilder.RemoveColumnsAsync` ohne die Spalte → Restore ohne Fehler, Wert `null` (Muster `…_TvSenderEnabled`). |
| `StandbyCard_ShowsDefault_And_PersistsSelection` | `TVSenderAdminTests` (bUnit) | Sub-Card zeigt „Standard-Clip"; Quellenwahl füllt Kandidaten; Auswahl + Speichern → `Setup.LiveStreamStandbyMediaItemId` gesetzt; „Standard-Clip verwenden" → `null`. |
| `StandbyCard_NonAdmin_Blocked` | `TVSenderAdminTests` | `GetClipCandidatesAsync` wirft für Nicht-Admin (`RequireAdminAsync` — serverseitige Durchsetzung). |

### Betroffene bestehende Tests

| Test | Anpassung |
|---|---|
| `LiveStreamControllerTests.GetFile_InvalidOrMissing_ReturnsNotFound` | `[InlineData("live.m3u8")]` entfernen (neu: 200 Standby); übrige Fälle bleiben 404. |
| `LiveStreamControllerTests` (alle Konstruktor-Aufrufe) | `LiveStreamStandbyProvider`-Parameter ergänzen (echte Instanz — Assets liegen in der Assembly). |
| `LiveStreamControllerTests.GetFile_AdminWithoutTvAccessRow_ReturnsFileOr404Not403` | Assertion bleibt `IsNotType<ForbidResult>`; Kommentar anpassen (fehlende Datei → jetzt 200 Standby statt 404 möglich). |
| `LiveStreamWorkerTests.ExecuteAsync_Startup_ClearsSegmentDirectoryAndTicks` | `LiveStreamStandbyProvider`-Konstruktor-Parameter ergänzen (echte Instanz mit Test-Assets bzw. Provider-Double über internen Overload); ggf. Refresh-Aufruf verifizieren. |
| `LiveStreamWorkerTests.ExecuteAsync_FeatureNotReleased_StopsSegmenterAndNeverStarts` | Derselbe DI-/Konstruktor-Pfad — ebenfalls `LiveStreamStandbyProvider`-Parameter ergänzen; Testlogik unverändert. |
| `LiveStreamE2ETests.PairedDevice_RequestInvalidOrMissing_Returns404` | Unverändert grün — keine der Inline-Dateien ist `live.m3u8`/`standby_*`. |
| `LiveStreamE2ETests` Auth-Pfade (401/403) | Unverändert — Auth/Freigabe greifen vor dem Fallback. |
| `LiveTvE2ETests.LiveTv_ViaNavMenu_ShowsPlayerAndProgram` | Auswirkungsprüfung: Serververhalten ändert sich (fehlende `live.m3u8` → 200 + Standby statt 404). Der Test seedet eine Playlist **mit** Segmentreferenz → bleibt auf dem echten Pfad, Assertions (`access_token` im m3u8-Request, `#live-tv-video`) sollten unverändert halten — im Lauf verifizieren; bei Abweichung Test anpassen, nicht abschwächen. |
| `LiveStreamE2ETests.PairedDevice_RequestPlaylistWithAccessToken_ReturnsRewrittenPlaylist` | Verifizieren (fachlich betroffen): der `.m3u8`-Pfad ändert sich (verschärfter Content-Check `PlaylistReferencesSegment` + mögliche `DISCONTINUITY`-Injektion). Der Test seedet eine Playlist **mit** Segmentreferenz und der Provider-Singleton ist je Test-Host frisch → kein gesetztes Transition-Flag, echte Auslieferung wie bisher — voraussichtlich unverändert grün; im Lauf bestätigen, bei Abweichung anpassen, nicht abschwächen. |
| `LiveStreamE2ETests.PairedDevice_RequestSegmentWithAccessToken_ReturnsSegment` | Verifizieren (fachlich betroffen): die `.ts`-Antwort trägt künftig `Cache-Control: no-store`; die vorhandenen Assertions (Status 200, `video/mp2t`, Body) prüfen den Header nicht und bleiben gültig — voraussichtlich unverändert grün; im Lauf bestätigen, bei Abweichung anpassen, nicht abschwächen. |
| `LiveTvE2ETests.LiveTv_ProgramGap_ShowsFriendlyHintAndUpcoming` | Verifizieren: Sendelücke → `guide?.Now is null` → kein `#live-tv-video`, kein Stream-Request → vom Standby-Fallback unberührt; voraussichtlich unverändert grün — im Lauf bestätigen. |
| `TVSenderAdminTests` (bUnit, Bestandstests) | Verifizieren: echter `ProgramSettingsService` liefert `LiveStreamStandbyMediaItemId = null` → neue Sub-Card zeigt „Standard-Clip"-Chip; vorhandene Assertions unberührt — im Lauf bestätigen. |
| `LiveStreamControllerTests.GetFile_M3u8_InjectsAccessTokenIntoSegmentUris` + `GetFile_M3u8WithoutToken_LeavesUrisUntouched` | Verifizieren (fachlich betroffen): beide Tests üben den umgebauten `.m3u8`-Pfad aus (neuer Content-Check `PlaylistReferencesSegment`, Extraktion von `RewritePlaylistContent`, mögliche `DISCONTINUITY`-Injektion über das Transition-Flag). Beide seeden Playlists **mit** Segmentreferenzen (`seg_001.ts`/`seg_002.ts`) und bleiben daher auf dem echten Auslieferungspfad — voraussichtlich unverändert grün; im Lauf bestätigen, bei Abweichung anpassen, nicht abschwächen. |
| `LiveStreamControllerTests.GetFile_TsSegment_ReturnsVideoMp2t` + `GetFile_TsSegment_FileCanBeDeletedWhileDeliveryStreamIsOpen` | Verifizieren (fachlich betroffen): die reale `.ts`-Auslieferung trägt künftig `Cache-Control: no-store` mit aus; die vorhandenen Assertions (Status, `FileStreamResult`, `video/mp2t`, Share-Flags) prüfen den Header nicht und bleiben gültig — voraussichtlich unverändert grün; im Lauf bestätigen, bei Abweichung anpassen, nicht abschwächen. |
| `TVSenderAdminClipGroupsE2ETests` (beide `SeedClipItemsAsync`-Aufrufe, Z. 202 + Z. 346) | Verifizieren: `SeedClipItemsAsync` wird von `private` nach `TVSenderAdminE2ETestBase` (`protected`) hochgezogen — die bestehenden Aufrufe lösen sich danach über die vererbte Methode auf; kompilierbar und voraussichtlich unverändert grün — im Lauf bestätigen. |
| `TVSenderAdminE2ETests.Admin_CreateEditDeactivateDeleteRule` (`TVSenderAdminE2ETests.cs:82`, Einstellungen-Tab Z. ~137–156) | Verifizieren (fachlich betroffen): der Test klickt `#tvsender-save-settings` (Z. ~145) und prüft den persistierten Stand in der DB — er übt exakt den geänderten `SaveChannelSettingsAsync`-Pfad über die echte HTTP-/DB-Strecke aus, der künftig zusätzlich `UpdateLiveStreamStandbyClipAsync(standbyMediaItemId)` aufruft. `standbyMediaItemId` bleibt beim Seed `null` → `UpdateLiveStreamStandbyClipAsync(null)` ist ein No-op (nur Auswahl löschen) — voraussichtlich unverändert grün; im Lauf bestätigen, bei Abweichung anpassen, nicht abschwächen. |
| `TVSenderAdminE2ETests` (`Admin_ProbeReset_SingleItemAndAllFailed`, `Admin_GenreRule_MultiSelectAndSourceFilter`, `NonAdmin_SeesNotAuthorized`) + `TVSenderAdminE2ETests_FfmpegDownload` (beide Tests) | Verifizieren (fachlich betroffen): alle rendern die Seite über den geänderten `OnInitializedAsync`-Init-Pfad, der künftig den Standby-Wert samt Anzeigename aus `MediaItem`/`MediaCollection`/`MediaSource` lädt. `standbyMediaItemId = null` → neue Sub-Card zeigt „Standard-Clip"-Chip, sonst keine Verhaltensänderung — voraussichtlich unverändert grün; im Lauf bestätigen, bei Abweichung anpassen, nicht abschwächen. |

### E2E-Tests (primärer Funktionsnachweis)

| Priorität | Szenario | Testdatei/Testklasse | Abgedecktes Akzeptanzkriterium | Warum E2E nötig ist |
|---|---|---|---|---|
| P1 | Freigegebenes Gerät ruft `live.m3u8` ohne produzierten Stream auf → Standby-Playlist statt 404; danach Standby-Segment aus dem Speicher | `LiveStreamE2ETests` (`PairedDevice_RequestPlaylistWithoutProducedStream_ReturnsStandbyPlaylist`, `PairedDevice_RequestStandbySegment_ReturnsSegment`) | „Nahtloser Übergang ohne ‚nicht aktiv'-Zwischenphase" über den kompletten HTTP-Pfad (JWT, `access_token`-Rewrite, `no-store`) | Nur Ende-zu-Ende prüfbar: Routing, Token-Validierung, Header-Serialisierung — genau die Schnittstelle externer Player. |
| P1 | Übergang Standby → echte Playlist: erste `live.m3u8`-Anfrage ohne Datei → Standby; danach `live.m3u8` + `seg_001.ts` ins Test-Segmentverzeichnis legen → zweite Anfrage liefert echte Playlist mit `#EXT-X-DISCONTINUITY` vor der ersten Segmentzeile, `access_token`-Rewrite und `no-store` | `LiveStreamE2ETests` (`PairedDevice_RequestPlaylist_TransitionsStandbyToReal_Discontinuity`) — Segmentverzeichnis-Seed nach dem `SeedSegmentDirectoryAsync`-Muster (Variante ohne initiale Playlist; das nachträgliche Schreiben der Playlist nutzt denselben Schreibweg) | „Nahtloser Übergang" inkl. korrektem Wechsel der Playlist-Generation über HTTP | Der Übergang ist ein benutzererreichbarer Fluss; nur E2E prüft die komplette Strecke Routing → Zustandserkennung → Tag-Injektion → Header. |
| P1 | `/live-tv` in der Warm-up-Phase: Player attach, Standby geladen, keine Fehleranzeige | `LiveTvE2ETests` (`LiveTv_Warmup_PlaysStandbyWithoutErrorHint`, Playwright). **Fixture/Seed:** `LiveTv.razor` rendert `#live-tv-video` und ruft `liveTv.attach` nur bei laufendem Slot (`guide?.Now is not null`, `LiveTv.razor` ~Z. 65–67) → der Test braucht einen laufenden Programm-Slot nach dem Muster `SeedChannelWithRunningProgramAsync` (Channel + `MediaSource`/`MediaCollection`/`MediaItem` + `Movie`/`MovieMediaItem` + ein `ProgramSlot` mit `Start ≤ now < End` + `TvSenderUserAccess` für `UserA`), darf aber **keine** `live.m3u8` ins Segmentverzeichnis legen (sonst greift der echte Auslieferungspfad statt Warm-up). Konkret: `SeedSegmentDirectoryAsync` so aufteilen, dass das Schreiben von `live.m3u8`/`seg_001.ts` getrennt steuerbar ist — z. B. Parameter `writePlaylist = true` oder eine herausgelöste `SeedRunningProgramAsync`, die nur Channel/Slot/Freigabe seedet; `setup.LiveStreamSegmentDirectory` wird auf ein **leeres** Temp-Verzeichnis gesetzt, `TvSenderEnabled = true`. Im Testing-Host ist `LiveStreamWorker` nicht registriert → es entsteht deterministisch keine echte Playlist. | „Kein ‚Stream ist nicht aktiv' beim Zuschauer" + bewusst sichtbarer Hinweis-Clip im realen Benutzerfluss | Nur im Browser entscheidet sich, ob hls.js die Standby-Playlist abspielt und `NotifyStreamError` ausbleibt. |
| P1 | Admin wählt Medienquelle → MediaItem → speichert; `Setup.LiveStreamStandbyMediaItemId` persistiert | `TVSenderAdminE2ETests` (neuer Test, Playwright; `TVSenderAdminE2ETestBase`; Kandidaten-Seed über die nach `TVSenderAdminE2ETestBase` gehobene `SeedClipItemsAsync` + `EnsureMediaSourceIdAsync`) | Konfigurierbarkeit der Einstellung im echten Admin-Fluss | Nur E2E deckt die komplette Strecke UI → Service → DB inkl. serverseitiger Admin-Prüfung der Kandidatenliste ab. |
| P2 | Mit echtem ffmpeg: konfigurierter Clip wird vorbereitet und die Standby-Playlist bewirbt Custom-Segmente | `LiveStreamE2ETests` (`PairedDevice_ConfiguredStandbyClip_PreparesAndServes` — stiller Rückkehr ohne ffmpeg, Muster `FfmpegLiveStreamSegmenterE2ETests`). **Testdaten-Bereitstellung:** `MediaSource { SourceType = MediaSourceType.LocalDirectory, Path = tempDir }` + `MediaCollection { MediaSourceId, Path = tempDir }` + `MediaItem { Path = "{tempDir}/standby-input.mp4", Duration, DurationProbedAt }` in der E2E-Host-DB (Scope über `CreateScope()`/`ApplicationDbContext`, analog `SeedSegmentDirectoryAsync`); die Videodatei wird physikalisch per ffmpeg-lavfi erzeugt (Hilfsmuster `GenerateVideoAsync` aus `FfmpegLiveStreamSegmenterE2ETests` — `testsrc`+`sine`, ~6 s h264/aac); `Setup.LiveStreamStandbyMediaItemId` direkt per `ProgramSettingsService`/`UpdateLiveStreamStandbyClipAsync` setzen; dann Playlist anfordern, bis der Custom-Satz beworben wird (lazy `PrepareAsync` → Poll auf `standby_{n>0}_`-URIs). `ResolveLocalFilePath` verlangt eine real existierende Datei unterhalb des Quell-Roots — daher lokale Quelle + echte Datei, kein Stub. | Ende-zu-Ende-Nachweis Auflösung → Transkodierung → Cache → Auslieferung | Transcode-/Cache-Verhalten ist nur mit echtem ffmpeg-Prozess aussagekräftig; lokale Quelle hält den Test ohne Netzwerk deterministisch. |
| P2 | `Cache-Control: no-store` auf echter `.ts`-Antwort | `LiveStreamE2ETests` (`PairedDevice_RequestSegment_ReturnsNoStore`) | „Client liefert keine veralteten Segmente aus" | Header-Verhalten über die echte HTTP-Pipeline. |

## Offene Punkte

Keine.
