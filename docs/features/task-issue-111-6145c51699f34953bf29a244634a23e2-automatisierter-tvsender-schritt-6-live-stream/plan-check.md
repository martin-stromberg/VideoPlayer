# Plan-Gegenprüfung

## Ergebnis

**Status:** Plan vollständig

## Abgleich Akzeptanzkriterien

| Akzeptanzkriterium | Umsetzung im Plan | Testnachweis im Plan | Status |
|--------------------|-------------------|----------------------|--------|
| 1. Echte Segmentierung (dauerhafter ffmpeg-Prozess → HLS) | `FfmpegLiveStreamSegmenter` + `LiveStreamWorker`/`LiveStreamCoordinator` (echte `-f hls`-Segmentierung, kein virtueller Stream) | `FfmpegLiveStreamSegmenterTests` (Argumente, Prozesslebenszyklus), `LiveStreamCoordinatorTests` | Abgedeckt |
| 2. Zeitbasierte Wiederaufnahme (kein Prozesszustand) | Tick liest `GetNowAsync` + `PositionSeconds` → `-ss`-Offset; kein persistenter Zustand; Startup-Clear des Segment-Dirs | `LiveStreamCoordinatorTests` (Start mit Position, Neustart) | Abgedeckt |
| 3. Sendeplatz-Übergang nahtlos | `CurrentSlotId != SlotId` → Stop+Start auf Nachfolger; eindeutige Segmentnamen pro Lauf; saubere Wiedergabeliste | `LiveStreamCoordinatorTests` (Wechsel, Neustart) | Abgedeckt |
| 4. Sendelücke | `Now == null` → Prozess stoppen, `WakeAtUtc` = `Next.StartUtc`; Auslieferung 404 bei fehlender Datei | `LiveStreamCoordinatorTests` (Lücke), `LiveStreamControllerTests`/`LiveStreamE2ETests` (404) | Abgedeckt |
| 5. Konfigurierbare Segmentlänge | `Setup.LiveStreamSegmentLengthSeconds` + `ProgramSettingsService` + `TVSenderAdmin` | Settings-Tests, `TVSenderAdminE2ETests`-Erweiterung | Abgedeckt |
| 6. Konfigurierbarer Speicherort + Retention | `LiveStreamSegmentDirectory` + `LiveStreamSegmentRetentionMinutes`; `LiveStreamSegmentStore` (Auflösung, `SweepExpiredSegmentsAsync`, Startup-`ClearAsync`) | `LiveStreamSegmentStoreTests`, Settings-Tests, Admin-E2E | Abgedeckt |
| 7. Authentifizierte Auslieferung (`access_token`, serverseitig) | `LiveStreamController` mit `[BearerTokenCheck]` + `CheckLoggedIn`; `.m3u8`-Rewrite injiziert Token | `LiveStreamControllerTests`, `LiveStreamE2ETests` (401/200/Token) | Abgedeckt |
| 8. Pfadsicherheit | Dateinamen-Whitelist + Containment-Check im Store | `LiveStreamControllerTests`, `LiveStreamE2ETests` (Traversal) | Abgedeckt |
| 9. FFmpeg-Bereitstellung (Suchreihenfolge, kein Bundling, Sichtbarkeit) | `FfmpegLocator` analog `FfprobeLocator` + `MediaProbeOptions.FfmpegPath`; fehlendes Binary → `FfmpegUnavailable` (Log, Rest funktionsfähig) | `FfmpegLocatorTests`, `LiveStreamCoordinatorTests` | Abgedeckt |
| 10. Lizenz/Build/CI | Lizenzdoku in `docs/help/tv-sender.md` + Release Notes; CI braucht kein ffmpeg (Fake-Tests); kein Bundling nötig | — (Dokumentationsaufgabe) | Abgedeckt |
| 11. Kein Schema-Zwang für Stream-Zustand; Setup-Felder mit Migration+Backup | `AddSetupLiveStreamSettings` + `OptionalRestore*` | Alt-Backup-Regressionstest | Abgedeckt |
| 12. Namensabgrenzung | `LiveStream*`-Vokabular, kein `Playlist`-Naming (`.m3u8` = Dateiformat) | — | Abgedeckt |

## Fehlende oder unvollständige Testanforderungen

—

## E2E-Abdeckung

| Benutzerfluss / Akzeptanzkriterium | Geplanter E2E-Test | Status |
|------------------------------------|--------------------|--------|
| Authentifizierter Playlist-/Segmentabruf (401 anonym, 200 mit Token, Token-Rewrite) | `LiveStreamE2ETests` (HTTP-E2E, `DeviceClientTestBase`) | Abgedeckt |
| Pfadsicherheit/404-Verhalten | `LiveStreamE2ETests` | Abgedeckt |
| Admin konfiguriert Segmentlänge/-ort/-retention über UI | `TVSenderAdminE2ETests`-Erweiterung (Playwright) | Abgedeckt |

## Fehlende oder unvollständige Planbestandteile

—

## Hinweise

- Die ffmpeg-Argumentform wurde lokal mit ffmpeg 9.0.2 verifiziert (`-re`, `-ss` Datei/Pipe, Pipe-Limitation bei MP4 ohne faststart dokumentiert als Seiteneffekt).
- Der Plan-Commit erfolgt auf Basis der Bestandsaufnahme vom selben Stand (Baseline 1892/1893 grün).
