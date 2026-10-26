# Plan-Gegenprüfung

## Ergebnis

**Status:** Plan lückenhaft

## Abgleich Akzeptanzkriterien

| Akzeptanzkriterium | Umsetzung im Plan | Testnachweis im Plan | Status |
|--------------------|-------------------|----------------------|--------|
| Nahtloser Übergang ohne gecachte Altsegmente des Vorlaufs | `ClearAsync` im `LiveStreamWorker` vor `Task.Delay(_initialDelay)` (Designentscheidung „Altsegmente des Vorlaufs", Start-Sequenz Schritt 2); `Cache-Control: no-store` auf allen Antworten | `ExecuteAsync_ClearsSegmentDirectory_BeforeInitialDelay` (mit `ControllableTimeProvider`), `GetFile_TsSegment_SetsNoStoreCacheHeader`, E2E `PairedDevice_RequestSegment_ReturnsNoStore` | Abgedeckt |
| Keine „nicht aktiv"-Zwischenphase in der Warm-up-Phase | Standby-Playlist/-Segmente aus dem Speicher statt 404; verschärftes Erkennungskriterium „`live.m3u8` auflösbar **und** ≥ 1 Media-URI-Zeile" (empirisch belegt: ffmpeg legt die Datei erst mit dem ersten Segment an) | `GetFile_MissingPlaylist_ServesStandbyPlaylist`, `GetFile_SegmentlessPlaylist_ServesStandbyPlaylist`, `GetFile_StandbySegment_ServedFromMemory`, E2E `PairedDevice_RequestPlaylistWithoutProducedStream_ReturnsStandbyPlaylist` + `…RequestStandbySegment_ReturnsSegment`, E2E `LiveTv_Warmup_PlaysStandbyWithoutErrorHint` | Abgedeckt |
| Standby-Clip („Es geht gleich weiter") serverseitig im Speicher, direkte Auslieferung bis zur ersten echten Segmentierung | `LiveStreamStandbyProvider` (Singleton): eingebetteter Default „Gleich geht's los!" (`EmbeddedResource`, `byte[]` im Speicher, rotierende Live-Playlist, kein `ENDLIST`); konfigurierbarer Clip über Voraus-Segmentierung + Platten-Cache mit Generationspräfix | `EmbeddedDefault_LoadsOnConstruction`, `BuildPlaylist_RotatesWindowAndAdvancesMediaSequence`, `TryGetSegment_ActiveSet_ReturnsBytes_UnknownNameReturnsNull`, `PrepareAsync_*`-Reihe, E2E `PairedDevice_ConfiguredStandbyClip_PreparesAndServes` | Abgedeckt |
| Playlist-/Cache-Header so, dass der Client keine veralteten Segmente ausliefert | `no-store` auf `.m3u8`, `.ts` und Standby-Antworten | `GetFile_TsSegment_SetsNoStoreCacheHeader`, Header-Assertions in den Standby-Tests, E2E `PairedDevice_RequestSegment_ReturnsNoStore` | Abgedeckt |
| Übergang Standby → echter Stream (Teil des „nahtlosen Übergangs") | Einmalige `#EXT-X-DISCONTINUITY`-Injektion über Transition-Flag; Standby-Segmente bleiben nach Stream-Start auslieferbar (begründet) | `TransitionFlag_NoteAndConsume`, `GetFile_RealPlaylistAfterStandby_InjectsDiscontinuity`, E2E `PairedDevice_RequestPlaylist_TransitionsStandbyToReal_Discontinuity` | Abgedeckt |
| Admin-Einstellung „Standby-Clip" in `TVSenderAdmin` (Medienquelle → MediaItem, Suche/Browsing, persistiert) | `Setup.LiveStreamStandbyMediaItemId` (weiche Referenz ohne FK), Getter/Updater mit Validierung, Sub-Card + Auswahl-Maske nach `TVSenderAdminClipGroups`-Muster, EF-Migration + `OptionalRestoreColumns` | `UpdateLiveStreamStandbyClipAsync_Roundtrip`/`_Invalid_Throws` (echtes SQLite), `ReadFromAsync_LegacyBackupWithoutStandbyColumn_RestoresNull`, bUnit `StandbyCard_ShowsDefault_And_PersistsSelection` + `StandbyCard_NonAdmin_Blocked`, E2E Clip-Auswahl + Persistenz | Abgedeckt |
| Zugriffsschutz unverändert: Anmeldung (`BearerTokenCheck`/`CheckLoggedIn`) und Freigabe (`IsTvSenderAccessGrantedAsync`) vor jeder Standby-Auslieferung | Standby-Verzweigung erst nach den bestehenden Prüfungen (Programmablauf Schritte 2–4); Pfadsicherheit via `SafeFileName`-Whitelist + Dictionary-Match | Bestehende 401/403/404-Tests als Regression gelistet; `TryGetSegment_ActiveSet_ReturnsBytes_UnknownNameReturnsNull` (fremde Generation/Traversal → `null` → 404) | Abgedeckt |
| Zugriffsschutz unverändert: Standby bleibt hinter dem Feature-Flag (`TvSenderEnabled=false` → 404) | Feature-Flag-Prüfung vor jeglicher Auslieferung (Programmablauf Schritt 3) | `GetFile_FeatureDisabled_Returns404` (Theory mit `live.m3u8` + `standby_0_00.ts`, konkretes Seed-Muster benannt) | Abgedeckt |

## Fehlende oder unvollständige Testanforderungen

- [ ] Liste betroffener bestehender Tests unvollständig — `.m3u8`-Rewrite-Tests fehlen: `LiveStreamControllerTests.GetFile_M3u8_InjectsAccessTokenIntoSegmentUris` (Z. 61) und `GetFile_M3u8WithoutToken_LeavesUrisUntouched` (Z. 80) üben genau den umgebauten Pfad aus (neuer Content-Check `PlaylistReferencesSegment`, Extraktion von `RewritePlaylistContent`, mögliche `DISCONTINUITY`-Injektion über das Transition-Flag). Beide seeden Playlists **mit** Segmentreferenzen (`seg_001.ts`/`seg_002.ts`) und bleiben daher voraussichtlich unverändert grün — gehören aber der Vollständigkeit halber als „verifizieren"-Eintrag in die Tabelle, exakt analog zum in Runde 3 nachgetragenen `LiveStreamE2ETests.PairedDevice_RequestPlaylistWithAccessToken_ReturnsRewrittenPlaylist`. Die Blanket-Zeile „alle Konstruktor-Aufrufe" deckt nur die mechanische Signaturanpassung ab, nicht die Verhaltensverifikation des refaktorierten Pfads.
- [ ] Liste betroffener bestehender Tests unvollständig — reale `.ts`-Auslieferung fehlt: `LiveStreamControllerTests.GetFile_TsSegment_ReturnsVideoMp2t` (Z. 94) und `GetFile_TsSegment_FileCanBeDeletedWhileDeliveryStreamIsOpen` (Z. 108) liefern künftig `Cache-Control: no-store` mit aus. Die Assertions können nicht brechen (Status/Stream/Content-Type bleiben), aber der E2E-Pendant-Fall `PairedDevice_RequestSegmentWithAccessToken_ReturnsSegment` wurde in Runde 3 für dieselbe Header-Änderung als fehlender „verifizieren"-Eintrag bemängelt — die Unit-Seite fehlt jetzt entsprechend.
- [ ] Liste betroffener bestehender Tests unvollständig — `SeedClipItemsAsync`-Aufrufer fehlen: Der Plan zieht `SeedClipItemsAsync` von `TVSenderAdminClipGroupsE2ETests` (Z. 71, `private`) nach `TVSenderAdminE2ETestBase` (`protected`) hoch. Die zwei bestehenden Aufrufe in `TVSenderAdminClipGroupsE2ETests` (Z. 202, Z. 346) lösen sich danach über die vererbte Methode auf — kompilierbar und vermutlich unverändert grün, die Klasse wird aber angefasst; die betroffenen Tests gehören als „verifizieren"-Eintrag in die Tabelle.

## E2E-Abdeckung

| Benutzerfluss / Akzeptanzkriterium | Geplanter E2E-Test | Status |
|------------------------------------|--------------------|--------|
| Zuschauer öffnet `/live-tv` in der Warm-up-Phase → Standby läuft, keine Fehleranzeige | `LiveTvE2ETests.LiveTv_Warmup_PlaysStandbyWithoutErrorHint` (Playwright; Fixture vollständig: laufender Slot nach `SeedChannelWithRunningProgramAsync`-Muster, leeres Temp-Segmentverzeichnis ohne `live.m3u8`, `SeedSegmentDirectoryAsync`-Aufteilung benannt) | Abgedeckt |
| Externer Player / gekoppeltes Gerät ruft Playlist + Standby-Segment ohne produzierten Stream ab | `LiveStreamE2ETests.PairedDevice_RequestPlaylistWithoutProducedStream_ReturnsStandbyPlaylist`, `…RequestStandbySegment_ReturnsSegment` | Abgedeckt |
| Übergang Standby → echter Stream über die HTTP-Strecke (`DISCONTINUITY`, `access_token`-Rewrite, `no-store`) | `LiveStreamE2ETests.PairedDevice_RequestPlaylist_TransitionsStandbyToReal_Discontinuity` | Abgedeckt |
| Admin wählt Medienquelle → MediaItem → speichert; Auswahl persistiert | `TVSenderAdminE2ETests` (neuer Playwright-Test; `SeedClipItemsAsync` nach `TVSenderAdminE2ETestBase` gehoben + `EnsureMediaSourceIdAsync`) | Abgedeckt |
| Konfigurierter Clip wird vorbereitet (Auflösung → Transcode → Cache → Auslieferung) | `LiveStreamE2ETests.PairedDevice_ConfiguredStandbyClip_PreparesAndServes` (lokale Medienquelle + lavfi-generiertes Video nach `GenerateVideoAsync`-Muster, stiller Rückkehr ohne ffmpeg) | Abgedeckt |
| `no-store` auf echter `.ts`-Antwort über die HTTP-Pipeline | `LiveStreamE2ETests.PairedDevice_RequestSegment_ReturnsNoStore` | Abgedeckt |
| Auth-/Freigabefälle (401/403/404 auf fremde Dateien) greifen vor dem Fallback | bestehende E2E-/Controller-Tests bleiben unverändert gelistet | Abgedeckt |

## Fehlende oder unvollständige Planbestandteile

- [ ] Siehe die drei Punkte unter „Fehlende oder unvollständige Testanforderungen" (fehlende „verifizieren"-Einträge in der Tabelle betroffener bestehender Tests). Darüber hinaus keine fehlenden Umsetzungs-, Entscheidungs-, Voraussetzungs- oder Risikobestandteile festgestellt.

## Hinweise

- Die Runde-3-Lücken sind geschlossen: Der Test-Seam für `PrepareAsync` ist jetzt als eigene Designentscheidung spezifiziert (interner Konstruktor-Overload mit `resolveMediaInputAsync`-/`transcodeAsync`-Delegates, `Func<ProcessStartInfo, ISegmenterProcess?>`-Fabrik für den Segmenter; `InternalsVisibleTo` bereits gesetzt) und `PairedDevice_RequestSegmentWithAccessToken_ReturnsSegment` steht als „verifizieren"-Eintrag in der Tabelle.
- `LiveStreamWorker` braucht kein neues `TimeProvider`-Mitglied (Parameter existiert bereits); `ControllableTimeProvider` liegt vor — der geplante Reihenfolge-Test ist ohne weitere Codeänderung umsetzbar.
- `TVSenderAdminE2ETests` und `TVSenderAdminE2ETestBase` existieren; `EnsureMediaSourceIdAsync` ist `protected static` in `PlaylistsE2ETestBase` — der neue Admin-E2E-Test kann die geplanten Hilfen nutzen.
- Kleinteilige, nicht lückenrelevante Beobachtung: `RefreshAsync` des Providers muss die konfigurierte ID lesen — dafür braucht der Singleton einen Scope (`IServiceScopeFactory` ist für den Resolver ohnehin eingeplant); im Plan nur implizit, bei der Umsetzung darauf achten, dass `ProgramSettingsService` nicht als Singleton-Abhängigkeit injiziert wird.
