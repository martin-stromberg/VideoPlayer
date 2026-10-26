# Plan-Gegenprüfung

## Ergebnis

**Status:** Plan lückenhaft

## Abgleich Akzeptanzkriterien

| Akzeptanzkriterium | Umsetzung im Plan | Testnachweis im Plan | Status |
|--------------------|-------------------|----------------------|--------|
| Nahtloser Übergang ohne gecachte Altsegmente des Vorlaufs | `ClearAsync` im `LiveStreamWorker` vor `_initialDelay` ziehen (Schritt 5, Designentscheidung „Altsegmente des Vorlaufs"); `no-store` auf allen Antworten | `ExecuteAsync_ClearsSegmentDirectory_BeforeInitialDelay` (LiveStreamWorkerTests), `GetFile_TsSegment_SetsNoStoreCacheHeader`, E2E `PairedDevice_RequestSegment_ReturnsNoStore` | Abgedeckt |
| Keine „Stream ist nicht aktiv"-Zwischenphase in der Warm-up-Phase | Standby-Playlist/-Segmente aus dem Speicher statt 404, solange `live.m3u8` nicht auflösbar (Designentscheidung „Zustandserkennung" + „Geltungsbereich") | `GetFile_MissingPlaylist_ServesStandbyPlaylist`, `GetFile_StandbySegment_ServedFromMemory`, E2E `PairedDevice_RequestPlaylistWithoutProducedStream_ReturnsStandbyPlaylist` + `…RequestStandbySegment_ReturnsSegment`, E2E `LiveTv_Warmup_PlaysStandbyWithoutErrorHint` | Lücke — Erkennungskriterium „Datei `live.m3u8` existiert" garantiert nicht, dass bereits ein echtes Segment referenziert ist; eine existierende, aber noch segmentlose Playlist würde als „echt" ausgeliefert und die Fehlerphase erneut auslösen (siehe Planbestandteile) |
| Standby-Clip („Es geht gleich weiter") serverseitig im Speicher, direkte Auslieferung bis zur ersten echten Segmentierung | `LiveStreamStandbyProvider` (Singleton) mit eingebettetem Default-Clip „Gleich geht's los!" (`standby_XX.ts` als `EmbeddedResource`, `byte[]` im Speicher, rotierende Live-Playlist) | `EmbeddedDefault_LoadsOnConstruction`, `BuildPlaylist_RotatesWindowAndAdvancesMediaSequence`, `TryGetSegment_ActiveSet_ReturnsBytes_UnknownNameReturnsNull`, E2E-Segment-Abruf | Abgedeckt |
| Playlist-/Cache-Header so, dass der Client keine veralteten Segmente ausliefert | `Cache-Control: no-store` auf allen ausgelieferten Antworten (`.m3u8`, `.ts`, Standby) | `GetFile_TsSegment_SetsNoStoreCacheHeader`, Header-Assertions in den neuen Standby-Tests, E2E `PairedDevice_RequestSegment_ReturnsNoStore` | Abgedeckt |
| Übergang Standby → echter Stream (Teil des „nahtlosen Übergangs") | Einmalige `#EXT-X-DISCONTINUITY`-Injektion über Transition-Flag im Provider | `GetFile_RealPlaylistAfterStandby_InjectsDiscontinuity`, `TransitionFlag_NoteAndConsume` | Lücke — nur Unit-/Controller-Ebene; kein E2E-Nachweis des Übergangs über den HTTP-Pfad (siehe E2E-Abdeckung) |
| Neue Admin-Einstellung „Standby-Clip auswählen" in `TVSenderAdmin` | `Setup.LiveStreamStandbyMediaItemId` (weiche Referenz), `GetLiveStreamStandbyMediaItemIdAsync`/`UpdateLiveStreamStandbyClipAsync`, Sub-Card + Auswahl-Maske nach `TVSenderAdminClipGroups`-Muster, Migration + `OptionalRestoreColumns` | `UpdateLiveStreamStandbyClipAsync_Roundtrip`/`_Invalid_Throws` (echtes SQLite), `ReadFromAsync_LegacyBackupWithoutStandbyColumn_RestoresNull`, bUnit `StandbyCard_ShowsDefault_And_PersistsSelection` + `StandbyCard_NonAdmin_Blocked`, E2E Clip-Auswahl + Persistenz | Abgedeckt |
| Zugriffsschutz unverändert (Standby hinter `BearerTokenCheck`, `CheckLoggedIn`, Feature-Flag, `TvSenderUserAccess`) | Standby-Verzweigung erst nach den bestehenden Prüfungen im `path is null`-Pfad (Programmablauf Schritte 2–4) | Bestehende Tests `Anonymous_Request…_Returns401`, `GetFile_UserWithoutTvAccess_Returns403`, `GetFile_NotLoggedIn_Returns401` bleiben als Regression unverändert gelistet; Pfadsicherheit via `TryGetSegment`-Dictionary-Match + `PairedDevice_RequestInvalidOrMissing_Returns404` | Abgedeckt |

## Fehlende oder unvollständige Testanforderungen

- [ ] E2E-Test für den Übergang Standby → echte Playlist fehlt (Akzeptanzkriterium „nahtloser Übergang"): Erste `live.m3u8`-Anfrage ohne Datei → Standby-Playlist; danach `live.m3u8` ins Test-Segmentverzeichnis legen (Muster `SeedSegmentDirectoryAsync` in `LiveStreamE2ETests`) → zweite Anfrage liefert echte Playlist mit `#EXT-X-DISCONTINUITY`, `access_token`-Rewrite und `no-store`. Der Plan deckt das nur per Controller-Unit-Test ab, obwohl der Übergang ein benutzererreichbarer Fluss ist und die HTTP-Strecke (Routing, Token, Header, Tag-Injektion) hier E2E-prüfbar wäre.
- [ ] Für den geplanten E2E `PairedDevice_ConfiguredStandbyClip_PreparesAndServes` ist nicht beschrieben, wie ein tatsächlich transkodierbares MediaItem im E2E-Host bereitgestellt wird (lokale Medienquelle + echte/generierte Videodatei; Muster des Testvideo-Generators aus `FfmpegLiveStreamSegmenterE2ETests` wäre zu benennen).

## E2E-Abdeckung

| Benutzerfluss / Akzeptanzkriterium | Geplanter E2E-Test | Status |
|------------------------------------|--------------------|--------|
| Zuschauer öffnet `/live-tv` in der Warm-up-Phase → Standby läuft, keine Fehleranzeige | `LiveTvE2ETests.LiveTv_Warmup_PlaysStandbyWithoutErrorHint` (Playwright) | Abgedeckt |
| Externer Player / gekoppeltes Gerät ruft Playlist + Standby-Segment ohne produzierten Stream ab | `LiveStreamE2ETests.PairedDevice_RequestPlaylistWithoutProducedStream_ReturnsStandbyPlaylist`, `…RequestStandbySegment_ReturnsSegment` | Abgedeckt |
| Übergang Standby → echter Stream (Playlist wechselt, `DISCONTINUITY`, URIs/Header korrekt) | keiner — nur Unit-Test `GetFile_RealPlaylistAfterStandby_InjectsDiscontinuity` | Lücke |
| Admin wählt Medienquelle → MediaItem → speichert; Auswahl persistiert | `TVSenderAdminE2ETests` (neuer Test auf `TVSenderAdminE2ETestBase`) | Abgedeckt |
| Konfigurierter Clip wird vorbereitet und ausgeliefert | `LiveStreamE2ETests.PairedDevice_ConfiguredStandbyClip_PreparesAndServes` (stiller Rückkehr ohne ffmpeg) | Abgedeckt (Testdaten/Fixture im Plan nicht konkretisiert, siehe oben) |
| `no-store` auf echter `.ts`-Antwort über die HTTP-Pipeline | `LiveStreamE2ETests.PairedDevice_RequestSegment_ReturnsNoStore` | Abgedeckt |
| Auth-/Freigabefälle (401/403/Feature-Flag-404) greifen vor dem Fallback | bestehende E2E-Auth-Tests bleiben unverändert grün (im Plan genannt) | Abgedeckt |

## Fehlende oder unvollständige Planbestandteile

- [ ] Zustandserkennung „erste echte Segmentierung steht": Der Plan legt als Kriterium fest „`live.m3u8` ist über `TryResolveFileAsync` auflösbar → echte Auslieferung" und übernimmt damit nicht die in der Anforderung (`requirement.md`) genannte Variante „Datei mit mindestens einem referenzierten Segment". Nicht belegt bzw. nicht abgesichert ist, dass ffmpeg die Playlist-Datei erst zusammen mit dem ersten Segment schreibt — eine existierende, aber (noch) segmentlose `live.m3u8` würde als „echt" durchgehen und dem Client eine leere Playlist liefern, womit die geforderte Beseitigung der Zwischenphase nicht garantiert ist. Weder ein Verhalten für diesen Fall noch ein Risikoeintrag ist benannt.
- [ ] Liste betroffener bestehender Tests unvollständig: `LiveStreamWorkerTests.ExecuteAsync_FeatureNotReleased_StopsSegmenterAndNeverStarts` erzeugt den Worker über denselben DI-/Fixture-Pfad und braucht ebenfalls den neuen `LiveStreamStandbyProvider`-Parameter, wird aber nicht genannt (nur `ExecuteAsync_Startup_ClearsSegmentDirectoryAndTicks`). Ebenso nicht gelistet: `LiveTvE2ETests.LiveTv_ViaNavMenu_ShowsPlayerAndProgram`, dessen Serververhalten sich ändert (`live.m3u8` liefert im Testing-Host künftig 200 + Standby statt 404) — eine Auswirkungsprüfung fehlt, auch wenn die Assertions vermutlich weiter gültig sind.

## Hinweise

- Das benannte Degradationsverhalten „fehlende eingebettete Default-Assets (Packaging-Fehler) → Fehlerlog + 404" hat keinen Testnachweis; falls prüfbar gewünscht, müsste die Asset-Quelle des Providers abstrahierbar sein.
- Die DoD verlangt `README.md`-Aktualisierung „bei Bedarf" — Schritt 9 nennt nur `docs/help/tv-sender.md` und `RELEASE_NOTES.md`; bei der Umsetzung kurz prüfen.
- Die DoD verlangt fehlerfreien Debug- **und** Release-Build; mit neuem `<EmbeddedResource>`-Eintrag in `VideoWebPlayer.csproj` sollte der Release-Build ausdrücklich eingeplant werden.
- Für den Admin-E2E-Test existieren bereits passende Seed-Hilfen (`SeedClipItemsAsync`, `EnsureMediaSourceIdAsync` in `TVSenderAdminClipGroupsE2ETests`/`TVSenderAdminE2ETestBase`) — bei der Nachplanung referenzieren.
- Randverhalten, das die Nachplanung festlegen sollte: Standby-Segmente bleiben über `TryGetSegment` auch nach Stream-Start auslieferbar (Dictionary-Match unabhängig vom `live.m3u8`-Zustand) — für Clients mit alter Standby-Playlist vermutlich gewollt, aber im Plan nicht explizit begründet.
