# Bestandsaufnahme: Konfiguration, Infrastruktur und externe Abhängigkeit

## `MediaProbeOptions` (Abschnitt `MediaProbe`)
Datei: `VideoWebPlayer/Configuration/MediaProbeOptions.cs`

| Eintrag | Typ | Standardwert | Zweck |
|---------|-----|--------------|-------|
| `FfprobePath` | `string?` | `null` | Konfigurierbarer ffprobe-Pfad (erste Stufe der Suchreihenfolge) |
| `IntervalSeconds` | `int` | 60 | Probe-Worker-Intervall |
| `BatchSize` | `int` | 10 | Items pro Batch |
| `TimeoutSeconds` | `int` | 120 | Timeout je Aufruf |
| `EffectiveIntervalSeconds`/`EffectiveBatchSize`/`EffectiveTimeoutSeconds` | `int` | — | Normalisierte Getter (<=0 → Default) |

`FfmpegPath` fehlt — analoger Eintrag erforderlich.

## Konventionen für Schreibverzeichnisse (appsettings.json / Program.cs)

| Verzeichnis | Basis | Zweck |
|-------------|-------|-------|
| `Data/` | ContentRoot | SQLite-DB (`Data/WebVideoPlayer.db`), `Data/Backups` |
| `Logs/` | ContentRoot (`Program.cs` legt es an) | Serilog-Datei-Logs |
| `Updates/`, `Backups/` | ContentRoot | AutoUpdate-Downloads/-Backups |

Der Standard-Segmentort gehört in dieselbe Familie (z. B. `Data/LiveStream` relativ zum ContentRoot).

## ffmpeg/ffprobe-Bereitstellung

- `ffmpeg 9.0.2-full_build-www.gyan.dev` und `ffprobe 9.0.2` liegen auf **PATH** (verifiziert). Build-Konfiguration enthält `--enable-gpl` (GPL-Build mit libx264/libx265 etc.).
- `tools/` im Repo enthält nur interne Werkzeuge (`MarkdownLinkCheck`, `SecretScan`) — der `{BaseDirectory}/tools/ffmpeg/`-Konventionspfad der Locator bezieht sich auf das **Anwendungsverzeichnis** (`AppContext.BaseDirectory`), nicht das Repo-Root.
- **Kein Bundling:** Lizenz-/Prozessentscheidung — ffmpeg wird wie ffprobe als separat bereitzustellendes Binary behandelt (Prozessaufruf = mere aggregation). Lizenzdokumentation: `docs/` bzw. Hilfe-Eintrag im Schritt 6.

## Empirische ffmpeg-Prüfungen (ffmpeg 9.0.2, lokale Verifikation)

| Szenario | Befehlsform | Ergebnis |
|----------|-------------|----------|
| Datei + Position | `ffmpeg -re -ss {pos} -i {file}` | `-ss` vor `-i` = schneller Keyframe-Seek; `-re` gibt nur die Ausgabe in Echtzeit aus |
| Pipe + Position | `cat f.mkv \| ffmpeg -re -ss {pos} -i pipe:0` | Funktioniert: Pakete vor der Position werden ohne Dekodierung verworfen (I/O-schnell), `-re` bremst die Discard-Phase nicht |
| Pipe + MP4 ohne faststart | `cat f.mp4` (moov am Ende) | **Schlägt fehl** („moov atom not found") — MP4 mit moov am Dateiende ist nicht pipe-tauglich; MKV/faststart-MP4 sind es |
| HLS-Ausgabe | `-f hls -hls_time N -hls_list_size K -hls_flags delete_segments` | Standard-Mechanismus; `delete_segments` räumt aus dem Fenster fallende Segmente selbst ab (Retention als zweite Ebene weiterhin nötig) |

## CI/CD (`.github/workflows/`)

- `staging-ci.yml` / `pr-staging-ci.yml`: ubuntu-latest, .NET 10, `dotnet format --verify-no-changes`, Build mit `TreatWarningsAsErrors`, Tests getrennt nach `Category!=E2E`, `Category=Integration`, `Category=E2E` (Playwright).
- **Kein** ffmpeg/ffprobe-Setup-Schritt in den Workflows; bestehende ffprobe-Tests arbeiten ausschließlich mit Fakes (kein Binary-Aufruf) — neue ffmpeg-Tests folgen demselben Muster, CI-Anpassung nur bei Bedarf.

## Authentifizierung / `access_token`-Muster

- `BearerTokenCheckAttribute` validiert JWT aus `Authorization: Bearer` **oder** `access_token`-Query gegen den `SymmetricSecurityKey`-Singleton + `Jwt:Issuer`.
- Die Blazor-UI hängt `?access_token={Client.AuthorizationToken}` an Medien-URLs an (`/api/items/{type}/{id}/stream`, `/api/pictures/...`) — das Muster, das HLS-Wiedergabeliste und Segmente wiederverwenden.
- **Falle:** In einer `.m3u8` referenzierte Segment-URIs sind relativ — der Query-String der Playlist-URL wandert nicht in die Segment-Requests. Die Playlist muss beim Ausliefern umgeschrieben werden (Token in jede URI einsetzen).

## Migrationen

- EF-Migrationen unter `VideoWebPlayer/Migrations/`, erzeugt per `dotnet ef migrations add`; `app.MigrateDatabase()` in `Program.cs` wendet sie beim Start an. Neueste: `20261021053951_AddProgramSlotStatus`.
- Backup-Registrierung: `OptionalRestoreColumns` (+ `OptionalRestoreIntDefaults` für int-Spalten mit Default) in `VideoWebPlayerBackupData.cs`; Alt-Backup-Regressionstest über `LegacyBackupArchiveBuilder.RemoveColumnsAsync`.

## Umgebungs-Guards

- `env.IsEnvironment("Testing")` steuert die `MdnsAdvertiserWorker`-Registrierung — Muster, um den Stream-Worker in `WebApplicationFactory`-Tests nicht automatisch ffmpeg starten zu lassen.
- `PairingWebApplicationFactory`/`DeviceClientTestBase` setzen `AutoUpdate:HostedServicesEnabled=false` und `Environment=Testing`.
