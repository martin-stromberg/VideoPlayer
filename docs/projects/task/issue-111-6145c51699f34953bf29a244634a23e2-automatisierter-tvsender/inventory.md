# Bestandsaufnahme: Automatisierter TV-Sender (Live-Stream aus der Mediathek)

Analyse des bestehenden Projektstandes (ASP.NET Core + Blazor, Projekte `VideoWebPlayer/`, `VideoWebPlayer.Client/`, `VideoWebPlayer.Tests/`) bezogen auf die übersetzte Anforderung in [requirement.md](requirement.md) (GitHub Issue #111).

## Zusammenfassung

- **Die Kernkomponenten des TV-Senders existieren nicht.** Es gibt keine Entities `TvChannel`, `ScheduleRule`, `ScheduleSlot` oder `ScheduleProgress`, keine Services `IScheduleService`/`IPlaylistEngine`/`ILiveSegmenter`/`ILiveStatusService`, keinen `LiveTvController`/`ScheduleRulesController` und keine der Enums `ScheduleRuleType`, `ScheduleRecurrence`, `ScheduleSlotStatus`, `LiveStreamFormat` (verifiziert per Volltextsuche über `VideoWebPlayer/`, `VideoWebPlayer.Client/`).
- **Der Befund „kein Dauer-Feld" ist bestätigt.** Weder `Movie`, `TVShowEpisode`, `MediaItem`, `MediaBaseEntry` noch `MediaEntry` besitzen eine Laufzeit-/Dauer-Eigenschaft. Einzige `Duration` im Datenmodell: `ContinueWatchingEntry.Duration` (vom Client beim Abspielen gemeldete Gesamtlaufzeit, `TimeSpan?`) — kein Scan-Metadatum. NFO-Parsing (`Movie.LoadFromXml`, `TVShowEpisode.LoadFromXml`) liest kein `<runtime>`-Element.
- **Der Befund „keine HLS/FFmpeg-Infrastruktur" ist bestätigt.** Kein Treffer für `ffmpeg`, `ffprobe`, `.m3u8`, `EXT-X`, `dash`, `hls.js` in `*.cs`, `*.csproj`, `*.json`, `*.yml`. Streaming besteht ausschließlich aus HTTP-Range-Requests auf die Originaldateien über `GET /api/items/{type}/{id}/stream` (`ItemsController.StreamMediaItem`, `enableRangeProcessing: true`).
- **Der Namenskonflikt ist bestätigt.** `IPlaylistService`/`PlaylistService`, `Playlist`, `PlaylistEntry`, `PlaylistsController` (`api/playlists`) bilden eine umfangreiche, fertige Benutzer-Playlist-Funktionalität (Issue #207). Eine „Playlist-Engine" für den Live-Betrieb braucht einen anderen Namen.
- **Wiederverwendbare Infrastruktur vorhanden:** `access_token`-Query-Authentifizierung (`BearerTokenCheckAttribute`) für `<video>`-/`<img>`-Abrufe ohne Header; serverseitige Zugriffsprüfung über `MediaSourceUser`/`UnlockedMediaEntry`/`IUnlockedMediaService`; Worker-Muster mit Signal-Aufweckung (`PlaylistBackfillWorker` + `IPlaylistBackfillSignal` + `PlaylistBackfillMarker`-SaveChanges-Hook); Admin-Settings über `Setup`-Tabelle + `ProgramSettingsService`; EF-Konfigurationen unter `Data/Configurations/`; Backup-Registrierung über `OptionalRestoreTables`/`OptionalRestoreColumns` in `VideoWebPlayerBackupData`; Such-/Auswahldialog `MediaSearchSelector.razor`; `<video>`-Player `VideoPlayer.razor`; Geräte-Auth (`DeviceTokenService`, `PairedDevice`, `RefreshTokenService`, `PairingCode`).

**Test-Ausgangszustand:** 1.760 Tests bestanden, 0 fehlgeschlagen, 1 übersprungen (bereits per `[Fact(Skip)]` deaktivierter Test mit dokumentiertem Befund). Drei Läufe in Release (`Category!=E2E`: 1.552/1.552; `Category=Integration`: 0 Tests — die Kategorie existiert im Projekt nicht; `Category=E2E`: 208 bestanden, 1 übersprungen). Nachweis: [inventory/tests.md](inventory/tests.md), Reports unter [inventory/test-results/](inventory/test-results/).

## Details

- [Datenmodell](inventory/models.md) — Media-/Playlist-/Auth-Entities, Backup-Registrierung, Nachweis fehlender Dauer-Felder und Sender-Entities
- [Logik](inventory/logic.md) — Scan-/Klassifikations-Pipeline, Playlist-Services, Worker/Signal-Muster, Streaming- und Auth-Endpoints, Konfigurationsdienste, Blazor-Komponenten
- [Enums](inventory/enums.md) — vorhandene Enums inkl. vertraglich gepinnter, fehlende Sender-Enums
- [Interfaces](inventory/interfaces.md) — `IPlaylistService` (Namenskonflikt), `IMediaSourceReader`, `IAuthService`, `IUnlockedMediaService`, `IPlaylistBackfillSignal`, Geräte-/Token-Interfaces, `IPlaylistApiClient`
- [Tests](inventory/tests.md) — Test-Ausgangszustand mit Lauf-Nachweisen, relevante Testklassen und Hilfsmethoden
