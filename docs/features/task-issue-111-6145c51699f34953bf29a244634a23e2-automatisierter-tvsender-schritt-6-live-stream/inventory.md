# Bestandsaufnahme: Live-Stream ausliefern (Schritt 6)

Analyse des Bestands für die HLS-Live-Stream-Auslieferung des automatisierten TV-Senders: externe Prozesssteuerung (ffprobe/ffmpeg), zeitbasierte Programmauskunft, Worker-Muster, Einstellungen, Authentifizierung und Medienzugriff.

## Zusammenfassung

- **Vorhanden:** `FfprobeLocator` mit exakt der geforderten Suchreihenfolge (konfigurierbarer Pfad `MediaProbeOptions.FfprobePath` → `{BaseDirectory}/tools/ffmpeg/` + App-Verzeichnis → PATH) — direkt als Vorbild für einen `FfmpegLocator` kopierbar. `FfprobeMediaDurationProbe` zeigt das Muster injizierbarer Prozessausführung (`_executeAsync`-Delegat) für testbaren Prozesscode.
- **Vorhanden:** `IProgramGuideService.GetNowAsync` liefert den laufenden `ProgramSlot` samt `PositionSeconds`, `RemainingSeconds` und `Next` — die komplette Zeitbasis des Streams, zustandslos und neustartfest.
- **Vorhanden:** Worker-Muster `ProgramScheduleWorker` (`BackgroundService` + Scope je Lauf + `IBackgroundProcessingGate`-Lease + `IProgramScheduleSignal`-Weckleitung + `TimeProvider`-Injektion).
- **Vorhanden:** `BearerTokenCheckAttribute` akzeptiert bereits `Authorization`-Header **und** `access_token`-Query — exakt das geforderte Player-kompatible Muster; `ApiBaseController.CheckLoggedIn` erzwingt die Anmeldung serverseitig.
- **Vorhanden:** Medienzugriff abstrahiert über `IMediaSourceReader`/`MediaSourceReaderDispatcher` (SFTP vs. `LocalDirectory`); `LocalMediaSourceReader` löst Dateipfade traversal-sicher auf (`ResolveFilePath`, privat), `OpenFileStream` liefert Streams für beide Quelltypen.
- **Vorhanden:** `Setup`-Einstellungsmuster (Spalte + Migration + `OptionalRestoreColumns`/Defaults in `VideoWebPlayerBackupData` + `ProgramSettingsService`-Getter/Update + `TVSenderAdmin`-UI + Alt-Backup-Regressionstest via `LegacyBackupArchiveBuilder`).
- **Fehlend:** ffmpeg-Lokalisierung (`FfmpegPath` in `MediaProbeOptions` existiert nicht), Segmentierungs-/Prozessverwaltung, Retention, HLS-Auslieferungsendpunkte, Stream-Einstellungen (`Setup`), Admin-UI-Felder.
- **ffmpeg-Verfügbarkeit:** `ffmpeg 9.0.2-full_build-www.gyan.dev` liegt auf PATH vor (gleiche Distribution wie ffprobe 9.0.2). Empirisch verifiziert: `-re -ss {pos} -i pipe:0` funktioniert — die Discard-Phase bis zur Position läuft ohne Echtzeit-Bremse und ohne Dekodierung (Paket-Discard); bei Dateieingabe sucht `-ss` vor `-i` direkt (Keyframe-Seek). Pipe-Eingabe scheitert an MP4 mit moov-Atom am Ende (kein faststart); MKV/faststart-MP4 funktionieren.
- **Hinweis Auslieferung:** HLS-Wiedergabelisten referenzieren Segmente relativ — der `access_token`-Query-Parameter geht bei der Segment-URI-Auflösung verloren; die `.m3u8` muss daher beim Ausliefern mit dem Token in den Segment-URIs umgeschrieben werden.
- **Test-Ausgangszustand:** siehe [Tests](inventory/tests.md) — Baseline-Lauf vollständig grün.

## Details

- [Datenmodell](inventory/models.md)
- [Logik](inventory/logic.md)
- [Interfaces](inventory/interfaces.md)
- [Konfiguration, Infrastruktur und externe Abhängigkeit](inventory/config.md)
- [Tests](inventory/tests.md)
