# Plan-Gegenprüfung

## Ergebnis

**Status:** Plan lückenhaft

## Abgleich Akzeptanzkriterien

| Akzeptanzkriterium | Umsetzung im Plan | Testnachweis im Plan | Status |
|--------------------|-------------------|----------------------|--------|
| Nahtloser Übergang ohne gecachte Altsegmente des Vorlaufs | `ClearAsync` im `LiveStreamWorker` vor `Task.Delay(_initialDelay)` (Designentscheidung „Altsegmente des Vorlaufs", Start-Sequenz Schritt 2); `Cache-Control: no-store` auf allen Antworten | `ExecuteAsync_ClearsSegmentDirectory_BeforeInitialDelay` (mit `ControllableTimeProvider`, `initialDelay > 0` — belegt die Reihenfolge), `GetFile_TsSegment_SetsNoStoreCacheHeader`, E2E `PairedDevice_RequestSegment_ReturnsNoStore` | Abgedeckt |
| Keine „nicht aktiv"-Zwischenphase in der Warm-up-Phase | Standby-Playlist/-Segmente aus dem Speicher statt 404; verschärftes Erkennungskriterium „`live.m3u8` auflösbar **und** ≥ 1 Media-URI-Zeile" (empirisch verifiziert: ffmpeg legt die Datei erst mit dem ersten Segment an; Content-Check deckt Torn Reads ab) | `GetFile_MissingPlaylist_ServesStandbyPlaylist`, `GetFile_SegmentlessPlaylist_ServesStandbyPlaylist`, `GetFile_StandbySegment_ServedFromMemory`, E2E `PairedDevice_RequestPlaylistWithoutProducedStream_ReturnsStandbyPlaylist` + `…RequestStandbySegment_ReturnsSegment`, E2E `LiveTv_Warmup_PlaysStandbyWithoutErrorHint` | Abgedeckt |
| Standby-Clip („Es geht gleich weiter") serverseitig im Speicher, direkte Auslieferung bis zur ersten echten Segmentierung | `LiveStreamStandbyProvider` (Singleton): eingebetteter Default „Gleich geht's los!" (`EmbeddedResource`, `byte[]` im Speicher, zeitbasiert rotierende Live-Playlist, kein `ENDLIST`); konfigurierbarer Clip über Voraus-Segmentierung + Platten-Cache | `EmbeddedDefault_LoadsOnConstruction`, `BuildPlaylist_RotatesWindowAndAdvancesMediaSequence`, `TryGetSegment_ActiveSet_ReturnsBytes_UnknownNameReturnsNull`, `PrepareAsync_*`-Reihe, E2E `PairedDevice_ConfiguredStandbyClip_PreparesAndServes` | Abgedeckt |
| Playlist-/Cache-Header so, dass der Client keine veralteten Segmente ausliefert | `no-store` auf `.m3u8`, `.ts` und Standby-Antworten | `GetFile_TsSegment_SetsNoStoreCacheHeader`, Header-Assertions in den Standby-Tests, E2E `PairedDevice_RequestSegment_ReturnsNoStore` | Abgedeckt |
| Übergang Standby → echter Stream (Teil des „nahtlosen Übergangs") | Einmalige `#EXT-X-DISCONTINUITY`-Injektion über Transition-Flag im Provider; Standby-Segmente bleiben nach Stream-Start auslieferbar (begründet) | `TransitionFlag_NoteAndConsume`, `GetFile_RealPlaylistAfterStandby_InjectsDiscontinuity`, **E2E** `PairedDevice_RequestPlaylist_TransitionsStandbyToReal_Discontinuity` (Runde-1-Lücke geschlossen) | Abgedeckt |
| Admin-Einstellung „Standby-Clip" in `TVSenderAdmin` (Medienquelle → MediaItem, Suche/Browsing, persistiert) | `Setup.LiveStreamStandbyMediaItemId` (weiche Referenz), Getter/Updater mit Validierung, Sub-Card + Auswahl-Maske nach `TVSenderAdminClipGroups`-Muster, EF-Migration + `OptionalRestoreColumns` | `UpdateLiveStreamStandbyClipAsync_Roundtrip`/`_Invalid_Throws` (echtes SQLite), `ReadFromAsync_LegacyBackupWithoutStandbyColumn_RestoresNull`, bUnit `StandbyCard_ShowsDefault_And_PersistsSelection` + `StandbyCard_NonAdmin_Blocked`, E2E Clip-Auswahl + Persistenz | Abgedeckt |
| Zugriffsschutz unverändert: Anmeldung (`BearerTokenCheck`/`CheckLoggedIn`) und Freigabe (`IsTvSenderAccessGrantedAsync`) vor jeder Standby-Auslieferung | Standby-Verzweigung erst nach den bestehenden Prüfungen (Programmablauf Schritte 2–4); Pfadsicherheit via `SafeFileName`-Whitelist + Dictionary-Match | Bestehende `Anonymous_Request…_Returns401`, `GetFile_NotLoggedIn_Returns401`, `GetFile_UserWithoutTvAccess_Returns403`, `PairedDevice_RequestInvalidOrMissing_Returns404` bleiben als Regression gelistet | Abgedeckt |
| Zugriffsschutz unverändert: Standby bleibt hinter dem Feature-Flag (`TvSenderEnabled=false` → 404) | Im Programmablauf (Schritt 3) behauptet: Feature-Flag-Prüfung vor jeglicher Auslieferung | **kein Test** — weder neu noch im Bestand (kein `GetFile`-Test mit `TvSenderEnabled=false`; `ProgramScheduleTestDb` seedet das Flag immer `true`) | **Lücke** (siehe unten) |

## Fehlende oder unvollständige Testanforderungen

- [ ] Kein Test für die Feature-Flag-Wechselwirkung (Akzeptanzkriterium „Zugriffsschutz unverändert"): Der Plan behauptet „Standby bleibt hinter dem Feature-Flag" (Programmablauf Schritt 3), enthält aber keinen Test, der `GET api/live-stream/live.m3u8` (und ggf. `standby_*.ts`) bei `TvSenderEnabled=false` auf 404 statt Standby prüft. Im Bestand existiert ein solcher Test ebenfalls nicht — eine versehentliche Verdrahtung des Fallbacks vor der Flag-Prüfung bliebe unbemerkt. Konkret fehlt z. B. `GetFile_FeatureDisabled_Returns404` in `LiveStreamControllerTests` bei aktivem Standby-Satz (`Setup.TvSenderEnabled=false` vor dem Aufruf setzen).
- [ ] Fixture/Seed für `LiveTvE2ETests.LiveTv_Warmup_PlaysStandbyWithoutErrorHint` nicht spezifiziert: `LiveTv.razor` rendert `#live-tv-video`/führt `liveTv.attach` nur bei laufendem Slot aus (`guide?.Now is not null`, `LiveTv.razor:65-67`). Der Test braucht daher einen laufenden Programm-Slot (Muster `SeedChannelWithRunningProgramAsync` aus `LiveTvE2ETests`), darf aber **keine** `live.m3u8` ins Segmentverzeichnis legen (sonst echter Auslieferungspfad statt Warm-up). Der Plan nennt — anders als bei den übrigen neuen E2E-Tests, deren Fixtures detailliert beschrieben sind — für diesen Test keinen Seed; ohne laufenden Slot wäre er wirkungslos (kein Attach, kein Playlist-Request, die Fehleranzeige kann gar nicht entstehen).

## E2E-Abdeckung

| Benutzerfluss / Akzeptanzkriterium | Geplanter E2E-Test | Status |
|------------------------------------|--------------------|--------|
| Zuschauer öffnet `/live-tv` in der Warm-up-Phase → Standby läuft, keine Fehleranzeige | `LiveTvE2ETests.LiveTv_Warmup_PlaysStandbyWithoutErrorHint` (Playwright) | Abgedeckt (Fixture unvollständig beschrieben, siehe oben) |
| Externer Player / gekoppeltes Gerät ruft Playlist + Standby-Segment ohne produzierten Stream ab | `LiveStreamE2ETests.PairedDevice_RequestPlaylistWithoutProducedStream_ReturnsStandbyPlaylist`, `…RequestStandbySegment_ReturnsSegment` | Abgedeckt |
| Übergang Standby → echter Stream über die HTTP-Strecke (`DISCONTINUITY`, `access_token`-Rewrite, `no-store`) | `LiveStreamE2ETests.PairedDevice_RequestPlaylist_TransitionsStandbyToReal_Discontinuity` (Seed-Variante von `SeedSegmentDirectoryAsync` benannt) | Abgedeckt (Runde-1-Lücke geschlossen) |
| Admin wählt Medienquelle → MediaItem → speichert; Auswahl persistiert | `TVSenderAdminE2ETests` (neuer Playwright-Test; `SeedClipItemsAsync` + `EnsureMediaSourceIdAsync` als Fixtures benannt) | Abgedeckt |
| Konfigurierter Clip wird vorbereitet (Auflösung → Transcode → Cache → Auslieferung) | `LiveStreamE2ETests.PairedDevice_ConfiguredStandbyClip_PreparesAndServes` (Testdaten jetzt konkret: lokale Medienquelle + lavfi-generiertes Video nach `GenerateVideoAsync`-Muster) | Abgedeckt (Runde-1-Lücke geschlossen) |
| `no-store` auf echter `.ts`-Antwort über die HTTP-Pipeline | `LiveStreamE2ETests.PairedDevice_RequestSegment_ReturnsNoStore` | Abgedeckt |
| Auth-/Freigabefälle (401/403/404 auf fremde Dateien) greifen vor dem Fallback | bestehende E2E-/Controller-Tests bleiben unverändert gelistet | Abgedeckt |

## Fehlende oder unvollständige Planbestandteile

- [ ] Siehe die beiden Punkte unter „Fehlende oder unvollständige Testanforderungen" (Feature-Flag-Test; Fixture-Beschreibung des Warm-up-E2E). Darüber hinaus keine fehlenden Umsetzungs-, Entscheidungs-, Voraussetzungs- oder Risikobestandteile festgestellt.

## Hinweise

- `LiveStreamE2ETests.PairedDevice_RequestPlaylistWithAccessToken_ReturnsRewrittenPlaylist` steht nicht in der Liste betroffener Tests, ist aber fachlich betroffen (`.m3u8`-Pfad ändert sich: Content-Check + mögliche `DISCONTINUITY`-Injektion). Da der ge seeded-Playlist ein Segment referenziert und der Provider-Singleton je Test-Host frisch ist, bleibt der Test voraussichtlich unverändert grün — zur Nachplanung trotzdem in die Verifikationsliste aufnehmen.
- Ebenso nicht einzeln gelistet: `LiveTvE2ETests.LiveTv_ProgramGap_ShowsFriendlyHintAndUpcoming` (Sendelücke → kein Video-Element → kein Stream-Request; voraussichtlich unberührt) und die bUnit-`TVSenderAdminTests` (echter `ProgramSettingsService` liefert `LiveStreamStandbyMediaItemId = null` → „Standard-Clip"-Chip; voraussichtlich unberührt).
- Der Warm-up-E2E kann im Testing-Host deterministisch laufen (`LiveStreamWorker` ist dort nicht registriert → keine echte `live.m3u8`) — der Plan nutzt das bereits, sollte aber sicherstellen, dass der Seed kein Segmentverzeichnis mit Playlist anlegt.
- `VideoWebPlayerClient`/`TokenClient`-bUnit-Strecke (`LiveTvTests`) bleibt unberührt — keine UI-Änderung an `LiveTv.razor` geplant; korrekt so.
