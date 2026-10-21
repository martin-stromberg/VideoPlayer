# Externe Abhängigkeit: ffprobe/ffmpeg

## Befund

- Kein ffmpeg/ffprobe im Repo, keine NuGet-Referenz auf ffmpeg-Bindings, kein Build-/CI-Schritt für externe Binaries. `build_script.bat` ist ein toter Maui-Stub.
- `ffprobe` ist Teil der FFmpeg-Suite, liefert `format=duration` als Sekundenzahl (`-v error -show_entries format=duration -of default=noprint_wrappers=1:nokey=1 <input>`); lesbares Format auch aus `pipe:0` (stdin) — nötig für SFTP-Quellen, die keinen lokalen Dateipfad bieten.

## Lizenzprüfung (FFmpeg)

- FFmpeg/ffprobe steht unter **LGPL 2.1+** (Standard-Build) bzw. **GPL 2/3** bei Builds mit GPL-Komponenten (`--enable-gpl`, z. B. libx264). Offizielle Builds auf ffmpeg.org sind i. d. R. GPL-Builds; Distributionen wie `ffmpeg-release-essentials` (gyan.dev) sind GPL-3-Builds.
- Die Anwendung **linkt nicht** gegen FFmpeg-Bibliotheken — sie startet `ffprobe` als separaten Prozess und liest dessen Textausgabe. Das ist die von der LGPL ausdrücklich unkritische Nutzungsform („mere aggregation"); auch bei einem GPL-Build entsteht dadurch kein abgeleitetes Werk — das ffprobe-Binary bleibt ein eigenständiges Programm, das Daten per Standardein-/ausgabe austauscht.
- Konsequenz: **Kein Binary-Bundling im Repo/Installer.** Dadurch entfällt jede GPL-/LGPL-Mitlieferungspflicht (Quellcode-Angebot, Lizenztext-Beilage) für das Projekt selbst. Der Anwender installiert ffmpeg selbst (Paketmanager/Download); die Anwendung nutzt einen konfigurierbaren Pfad oder `PATH`. Wer ein Binary neben der App ablegt (`tools/ffmpeg/ffprobe.exe`), bekommt es automatisch gefunden — Beilagenpflichten für diesen Fall verbleiben beim Verteiler des Bundles, nicht beim Projekt.
- CI benötigt kein ffprobe: alle Tests arbeiten gegen eine austauschbare Probe-Abstraktion; ein echter ffprobe-Integrationstest ist nicht geplant (kein Binary in der CI-Umgebung garantiert).

## Bereitstellungsentscheidung (Festlegung für den Plan)

Reihenfolge der Pfadauflösung: 1) `MediaProbe:FfprobePath` (konfigurierbar), 2) `<AppContext.BaseDirectory>/tools/ffmpeg/ffprobe(.exe)` und `…/ffprobe(.exe)` (Konventionsverzeichnis für manuelles Ablegen), 3) `ffprobe`/`ffprobe.exe` auf `PATH`. Nicht auffindbar → Items bleiben ungeprüft, Worker loggt einmalig eine Warnung und ruht (kein Scan-/Betriebs-Fehler).
