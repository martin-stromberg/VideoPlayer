# Bestandsaufnahme: Warm-up-Phase des Live-Streams — Standby-Clip statt Altsegmenten und „nicht aktiv"-Zwischenphase

Nach einem frischen App-Start durchläuft die HLS-Auslieferung des TV-Senders eine Phase, in der `api/live-stream/live.m3u8` mit 404 antwortet bzw. Reste des Vorlaufs auslieferbar sind. Diese Bestandsaufnahme dokumentiert den vorhandenen Code und den Test-Ausgangszustand vor der Umsetzung (Issue #266).

## Zusammenfassung

- **Auslieferungspfad vollständig vorhanden:** `LiveStreamController.GetFile` (`api/live-stream/{fileName}`) prüft Anmeldung, Feature-Flag `Setup.TvSenderEnabled` und Benutzer-Freigabe, löst Dateien über `LiveStreamSegmentStore.TryResolveFileAsync` (Whitelist + Containment) auf und antwortet bei fehlender `live.m3u8` mit 404 — genau die „nicht aktiv"-Phase (clientseitig `LiveTvErrorMessages.StreamUnavailable` nach `NotifyStreamError`, Retry alle 5 s).
- **Warm-up-Sequenz bestätigt:** `LiveStreamWorker` wartet zuerst `_initialDelay` (10 s), erst **danach** läuft `ClearAsync` und der erste `SynchronizeAsync`-Tick — bis zum ersten echten Segment (Segmentlänge Standard 6 s) ist das Verzeichnis leer, davor noch mit Vorlauf-Resten gefüllt.
- **Cache-Verhalten:** `Cache-Control: no-store` steht nur auf der `.m3u8`-Antwort (`LiveStreamController.cs:70`); `.ts`-Segment-Antworten und sonstige Dateien erhalten keinen Cache-Header.
- **Kein Standby-Mechanismus vorhanden:** kein Dienst, kein Asset, kein Signal „erste echte Segmentierung steht". Zustandsauskunft existiert nur auf Prozessebene (`ILiveStreamSegmenter.IsRunning`/`CurrentSlotId`) bzw. Dateiebene (`live.m3u8` im Verzeichnis).
- **Zustandsmodell vorhanden:** `LiveStreamSyncAction` (`Started`, `Stopped`, `FfmpegUnavailable`, `MediaUnavailable`, `Unchanged`) deckt bereits alle „kein Stream"-Zustände ab; `Stopped`/`FfmpegUnavailable`/`MediaUnavailable` führen heute alle zum selben 404-Pfad.
- **Konfiguration vorhanden:** `Setup.LiveStreamSegmentLengthSeconds`/-`Directory`/-`RetentionMinutes` samt `ProgramSettingsService`-Getter/Updater, Admin-UI in `TVSenderAdmin.razor` und `OptionalRestoreColumns`-Registrierung — als Muster für eventuelle Standby-Konfiguration.

Test-Ausgangszustand: volle Suite `dotnet test VideoWebPlayer.Tests/VideoWebPlayer.Tests.csproj` (Debug, ungefiltert) **grün — 2446 von 2448 Tests bestanden, 0 fehlgeschlagen, 2 übersprungen** (ein bewusst übersprungener Bestandefehler außerhalb der Sender-Thematik, ein Unix-only-Test unter Windows). Details und anforderungsbezogene Testlücken: [inventory/tests.md](inventory/tests.md).

## Details

- [Datenmodell](inventory/models.md)
- [Logik](inventory/logic.md)
- [Enums](inventory/enums.md)
- [Interfaces](inventory/interfaces.md)
- [Tests](inventory/tests.md)
