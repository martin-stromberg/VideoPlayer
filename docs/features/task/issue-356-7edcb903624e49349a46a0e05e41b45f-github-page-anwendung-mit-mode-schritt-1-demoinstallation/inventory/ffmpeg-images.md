# FFmpeg-Verfügbarkeit und Bilderzeugung

## ffmpeg / ffprobe

**Nicht im Repository gebundelt** — Auflösung zur Laufzeit über drei Stufen
(`FfmpegLocator.cs:40-53`, `FfprobeLocator.cs:40-53`, identische Konvention):

1. Konfigurierter Pfad: `MediaProbe:FfmpegPath` bzw. `MediaProbe:FfprobePath`
   (`appsettings.json:51-62`; Default `null`)
2. Konventionsverzeichnisse: `{BaseDirectory}/tools/ffmpeg/{ffmpeg,ffprobe}` und
   `{BaseDirectory}/{ffmpeg,ffprobe}` (Locator Zeilen 55–62)
3. `PATH`-Durchsuchung (Zeilen 64–90)

`tools/ffmpeg/` wird über `FfmpegToolDownloadService`
(`Services/FfmpegToolDownloadService.cs`) befüllt: Admin-ausgelöster Download
des plattformpassenden statischen Pakets (~145 MB, enthält ffmpeg **und**
ffprobe) mit Zip-Bomb-Guard und Fortschritts-Snapshot; im TV-Sender-Admin
verdrahtet (`TVSenderAdminTests_FfmpegDownload`). `tools/` ist nicht
git-ignored-spezifisch; Download schreibt nur nach `{baseDirectory}/tools/ffmpeg/`.

Nutzung im Bestand:

| Verbraucher | Zweck |
|-------------|-------|
| `FfprobeMediaDurationProbe` | `ffprobe -show_entries format=duration` auf Datei/URI/Stream (`MediaDurationProbeService`) |
| `FfmpegLiveStreamSegmenter`/`LiveStream*` | HLS-Segmentierung für den TV-Sender |
| `InternalMediaStreamController` | Token-gesicherte Loopback-URL als ffprobe-/ffmpeg-Input |

**Eignung für dynamische Dummy-Video-Erzeugung:** technisch machbar
(`ffmpeg -f lavfi -i testsrc/…`), und im Dev-Clone meist vorhanden, wenn der
TV-Sender schon eingerichtet wurde — aber **nicht garantiert**: ohne Download
oder PATH-Installation liefert `Resolve()` `null`. Für B15 („dynamisch falls
möglich, sonst beigelegt") ist ein Fallback nötig. Alternativ: eine minimale
mp4-Datei statisch im Repo beilegen (Kosten: Repo-Größe, keine Laufzeitabhängigkeit).

## Bilderzeugung im Code

**SkiaSharp** (`SkiaSharp` + `SkiaSharp.NativeAssets.Linux.NoDependencies`,
`VideoWebPlayer.csproj:34-35`) ist die Bildbibliothek — **kein ImageSharp**:

| Stelle | Was sie tut |
|--------|-------------|
| `Services/EpisodeBackgroundImage/EpisodeBackgroundImageGenerator.cs` | Resize, dominant color, Canvas + Tint-Overlay, JPEG-Encode → `Picture` mit `Type="background"`, `IsGeneratedBackground=true` |
| `Services/EpisodeBackgroundImage/EpisodeBackgroundImageService.cs` | Lazy, pro-Episode gelockte Generierung+Persistenz (von Episodes-API/Worker benutzt) |
| `Services/HomeBackgroundImage/HomeBackgroundImageGenerator.cs` | Collage aus Continue-Watching-Postern (JPEG) |
| `Services/PlaylistCover/{Jpeg,Png}IntegrityChecker.cs` | Format-Validierung von Covern |
| `VideoWebPlayer.Tests/Helpers/TestImages.cs` | `Png(w,h)`/`SolidPng`/`Gif` — synthetische, wiedererkennbare Testbilder per SkiaSharp |

Damit existiert produktiver Code, der Anzeigebilder **zur Laufzeit aus
Nichts erzeugt** — für Demo-Poster/Banner/Fanart direkt wiederverwendbar
(B16 „möglichst klein, gut genug für Screenshots").

## Klassifizierungs-Nebeneffekte

- `MediaDurationProbeService`: probedurationen werden lazy nachgeholt; bei
  ungültiger Dummy-Datei → `PermanentFailure` (seekbar + Undecodable-Marker)
  oder `TransientFailure` bei fehlendem ffprobe — kein Absturz, die Kachel
  fehlt lediglich die Laufzeit.
- `SetPictureAndMarkBackgroundForUpdateAsync` (Classifier) markiert Episoden
  für Hintergrundbilder — setzt vorhandene Fanart/Poster voraus.
