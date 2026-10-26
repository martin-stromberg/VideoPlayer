# Plan-Review

## Ergebnis

**Status:** Offene Aufgaben vorhanden

## Umgesetzte Planelemente

- [x] `scene_gleich_gehts_los` + `jingle_go` + `CLIPS`-Eintrag `gleich-gehts-los` (generate_clips.py:250/414/439) — erweitert; Drehbuch `drehbuecher/07-gleich-gehts-los.md` und `clips/funki-gleich-gehts-los.mp4` angelegt
- [x] `VideoWebPlayer/Assets/LiveStreamStandby/standby_00..02.ts` + `<EmbeddedResource>` (VideoWebPlayer.csproj:50) — angelegt; Dauern (4,023 s/4,076 s/2,059 s) und Transcode-Befehl als Kommentar im Provider dokumentiert
- [x] `Setup.LiveStreamStandbyMediaItemId` (`long?`, Setup.cs:128) — angelegt, weiche Referenz ohne FK
- [x] Migration `20261026154346_AddSetupLiveStreamStandbyMediaItem` — angelegt (AddColumn nullable long, kein Index/FK)
- [x] `OptionalRestoreColumns`-Eintrag `Setups.LiveStreamStandbyMediaItemId` (VideoWebPlayerBackupData.cs:109) — erweitert
- [x] `GetLiveStreamStandbyMediaItemIdAsync` + `UpdateLiveStreamStandbyClipAsync` (ProgramSettingsService.cs:467-504) — angelegt inkl. Validierung (existiert/Video/unklassifiziert → `KeyNotFoundException`/`ArgumentException`)
- [x] `LiveStreamStandbyProvider` (public sealed, Singleton) — angelegt: `StandbySegment`/`StandbySegmentSet` (internal records), interner Ctor-Overload mit `loadDefaultSet`/`resolveMediaInputAsync`/`transcodeAsync`-Delegates, `BuildPlaylist(long?)` (rotierendes Fenster 3, MEDIA-SEQUENCE aus `utcNow`, `standby_{n}_XX.ts`, PROGRAM-DATE-TIME, kein ENDLIST, `TimeProvider`), `TryGetSegment`, `NoteStandbyPlaylistServed`/`ConsumePendingStandbyTransition`, `PrepareAsync`/`RefreshAsync` (SemaphoreSlim-Dedup, `_failedMediaItemId`, Cache-Marker `standby-source.json`, `Data/LiveStreamStandby/`), `LogError`-Degradation ohne Default-Satz
- [x] `LiveStreamStandbySegmenter` (public sealed, Singleton) — angelegt: `StandbyTranscodedSegment` (internal record), `TranscodeAsync` (`-f segment`, `-t 60`-Kappe, Keyframe-Ausdruck, `standby_%03d.ts`, stderr-Drain mit Token-/Credential-Maskierung, `null` bei Fehlschlag), interner Ctor-Overload mit `Func<ProcessStartInfo, ISegmenterProcess?>`, `IFfmpegLocator`/`IFfprobeLocator` mit nominalem 4-s-Fallback
- [x] `LiveStreamController` — erweitert: Provider-Ctor-Param, `PlaylistReferencesSegment` (segmentlose Playlist → Standby-Pfad), Standby-Verzweigung (`GetLiveStreamStandbyMediaItemIdAsync` + `BuildPlaylist` → 200 m3u8 / `null` → 404; `standby_*` → Speicher-Bytes `video/mp2t`), `RewritePlaylistContent` extrahiert und für beide Pfade genutzt, `InjectDiscontinuity` + `ConsumePendingStandbyTransition`, `Cache-Control: no-store` auf allen erfolgreichen Antworten
- [x] `LiveStreamWorker` — erweitert: Provider-Ctor-Param, `ClearAsync` vor `Task.Delay(_initialDelay)`, danach `RefreshAsync` (Fehler → Warnlog)
- [x] `ServiceCollectionExtensions` — zwei Singleton-Registrierungen (Z. 253-254)
- [x] `TVSenderAdmin.razor` — Sub-Card „Standby-Clip (Warm-up-Phase)" mit `#tvsender-standby-current`/`-picker`/`-source`/`-search`/`-clear`, `.standby-candidate-row`/`.standby-candidate-pick`, „Weitere laden", 400-ms-Debounce, `GetClipCandidatesAsync`-Datenquelle, Label-Laden in `OnInitializedAsync`, `UpdateLiveStreamStandbyClipAsync` in `SaveChannelSettingsAsync`
- [x] `SeedClipItemsAsync` von `TVSenderAdminClipGroupsE2ETests` (private) nach `TVSenderAdminE2ETestBase` (protected, Z. 54) gehoben
- [x] `LiveStreamStandbyProviderTests` — alle 10 geplanten Tests vorhanden (Rotation, TryGetSegment, EmbeddedDefault, MissingDefaultAssets, ConfiguredIdDiffers/TriggerPrepare, Prepare Success/Failure/NullTarget/CacheHit, TransitionFlag)
- [x] `LiveStreamStandbySegmenterTests` — 5 Tests (Args+60-s-Kappe, Pipe, Exit ≠ 0, keine Segmente, ffmpeg fehlt)
- [x] `LiveStreamControllerTests` — alle geplanten Tests vorhanden (`MissingPlaylist`/`Segmentless`/`StandbySegment`/`WithoutStandbyAssets`/`NoStore`/`Discontinuity`/`FeatureDisabled`-Theory); `InlineData("live.m3u8")` aus `GetFile_InvalidOrMissing_ReturnsNotFound` entfernt; Ctor-Aufrufe mit Provider-Param; Kommentar in `GetFile_AdminWithoutTvAccessRow…` angepasst
- [x] `LiveStreamWorkerTests` — `ExecuteAsync_ClearsSegmentDirectory_BeforeInitialDelay` neu (steuerbarer `TimeProvider`, Clear+Refresh vor Delay); beide Bestandstests mit Provider-Param
- [x] `ProgramSettingsServiceLiveStreamTests` — 3 Standby-Tests (Default `null`, Roundtrip, Invalid_Throws) gegen echtes SQLite
- [x] `VideoWebPlayerBackupDataTests_LiveStreamStandbyMediaItem` — `ReadFromAsync_LegacyBackupWithoutStandbyColumn_RestoresNull`
- [x] `TVSenderAdminTests_Standby` (bUnit) — `StandbyCard_ShowsDefault_And_PersistsSelection` (inkl. Clear → `null`), `StandbyCard_NonAdmin_Blocked`
- [x] `LiveStreamE2ETests` — alle 5 Erweiterungen (Standby-Playlist mit Token-Rewrite + no-store, Standby-Segment mit 0x47, DISCONTINUITY-Übergang, `no-store` auf echtem Segment, Custom-Clip mit echtem ffmpeg + `standby_1_`-Poll, stiller Rückkehr ohne ffmpeg)
- [x] `LiveTvE2ETests`-Fixture — `SeedSegmentDirectoryAsync(writePlaylist)` + `SeedChannelWithRunningProgram_NoPlaylistAsync` (leeres Segment-Verzeichnis, `TvSenderEnabled`, `TvSenderUserAccess`)
- [x] `TVSenderAdminE2ETests.Admin_StandbyClip_PickAndPersist` — Quellen-/Kandidaten-Auswahl → Speichern → DB-Persistenz
- [x] `docs/help/tv-sender.md` (Z. 428-458), `docs/API.md` (Z. 565-589), `docs/RELEASE_NOTES.md` (Z. 487-490) — aktualisiert; `README.md` geprüft (kein 404-Verhalten dokumentiert → kein Änderungsbedarf)

## Offene Aufgaben

- [ ] `LiveTvE2ETests.LiveTv_Warmup_PlaysStandbyWithoutErrorHint` (LiveTvE2ETests.cs:187-217) — teilweise umgesetzt: Playlist-Request 200 + `access_token`-Rewrite werden assertiert; die zwei übrigen geplanten Prüfpunkte fehlen: (a) „`standby_`-Segment-Request erfolgt" — `standbySegmentStatuses` (Z. 191/200) wird nur gesammelt, nie assertiert; (b) „`#live-tv-stream-error` erscheint nicht" — keine Prüfung vorhanden. Ein Code-Kommentar (Z. 208-212) begründet das mit fehlenden H.264/AAC-Codecs im Headless-Chromium; der Plan verlangt die Prüfungen jedoch ausdrücklich — entweder nachrüsten (soweit technisch sinnvoll, z. B. Segment-Request-Eintreffen ≥ 1 mit 200 assertieren) oder die Planabweichung explizit als Entscheidung dokumentieren.

## Hinweise

- Die Tasks-Datei wurde vollständig überschrieben; Aufgabe 19 steht auf `Offen`, alle übrigen auf `Erledigt` mit konkretem Testnachweis (Datei:Zeile).
- Build-/Testlauf-Nachweise (Task 21, 25) wurden in dieser Review nicht erneut ausgeführt — geprüft wurde das Vorhandensein der Implementierung und der Tests im Code; die Aussage „grün" stammt aus dem Implementierungslauf.
- `PrepareAsync` ist `internal` (statt `public`) — konsistent mit der Test-Seam-Designentscheidung (`InternalsVisibleTo` gesetzt, csproj:15).
- Kleinigkeit außerhalb des Plans: `SetupsSingleRowMigrationTests.cs:40` enthält einen Kommentar-Verweis auf die neue Spalte — plausibel, kein Handlungsbedarf.
