# Videolaufzeit

Die Anwendung ermittelt für jede Videodatei in der Mediathek automatisch die Wiedergabedauer. Sie wird benötigt, um Sendeplätze des automatisierten TV-Senders zeitlich belegen zu können (siehe Projekt „Automatisierter TV-Sender").

## Wie es funktioniert

- Ein Hintergrunddienst (`MediaDurationProbeWorker`) prüft regelmäßig alle Videodateien (`*.mp4`, `*.avi`, `*.mkv`, `*.mpeg`), für die noch keine Laufzeit vorliegt — sowohl neu gescannte Dateien als auch der bereits vorhandene Bestand. Ein manuelles Auslösen ist nicht nötig.
- Die Laufzeit wird mit dem externen Werkzeug **ffprobe** direkt aus der Videodatei gelesen — bei lokalen Quellen über den Dateipfad, bei SFTP-Quellen wird die Datei gestreamt (sie wird nicht auf den Server heruntergeladen und nicht als Ganzes im Speicher gehalten).
- Kann die Laufzeit einer Datei nicht bestimmt werden (defekte Datei, kein lesbares Videoformat, Zeitüberschreitung), wird die Datei intern als „Probe fehlgeschlagen" markiert (`DurationProbedAt` gesetzt, `Duration` leer). Solche Dateien stören weder Scan noch Wiedergabe und werden nicht erneut geprüft; sie sind von der Sendeplanung ausgeschlossen.
- Wird eine Videodatei ersetzt (der Scan erkennt die geänderte Datei), wird die Laufzeit automatisch neu ermittelt.

## ffprobe einrichten

ffprobe ist Teil von [FFmpeg](https://ffmpeg.org/) und wird **nicht** mit der Anwendung ausgeliefert. Es muss auf dem Server installiert werden, z. B.:

- Windows: `winget install Gyan.FFmpeg` oder das Build von https://www.gyan.dev/ffmpeg/builds/ bzw. https://ffmpeg.org/download.html
- Linux: `apt install ffmpeg` (Debian/Ubuntu) bzw. Paket der Distribution

Die Anwendung sucht das `ffprobe`-Binary in dieser Reihenfolge:

1. Konfiguration `MediaProbe:FfprobePath` in `appsettings.json` (absoluter Pfad, optional),
2. `tools/ffmpeg/ffprobe(.exe)` und `ffprobe(.exe)` im Anwendungsverzeichnis,
3. `PATH` des Betriebssystems.

Ist kein Binary auffindbar, wartet die Laufzeitermittlung und protokolliert einmalig eine Warnung; der restliche Betrieb läuft unverändert weiter. Nach einer Installation laufen ausstehende Dateien automatisch nach.

## Konfiguration (`appsettings.json`)

| Schlüssel | Standard | Bedeutung |
|-----------|----------|-----------|
| `MediaProbe:FfprobePath` | `null` | Optionaler absoluter Pfad zum ffprobe-Binary |
| `MediaProbe:IntervalSeconds` | `60` | Intervall, in dem nach neuen, ungeprüften Dateien gesucht wird |
| `MediaProbe:BatchSize` | `10` | Maximale Anzahl Dateien je Prüfschritt |
| `MediaProbe:TimeoutSeconds` | `120` | Zeitlimit je ffprobe-Aufruf (relevant bei SFTP-Quellen) |

## Lizenz

FFmpeg/ffprobe steht unter LGPL 2.1+ bzw. GPL (je nach Build). Die Anwendung verlinkt keine FFmpeg-Bibliotheken, sondern startet `ffprobe` als eigenständigen Prozess (Kommunikation über Ein-/Ausgabe). Für diese Nutzungsform entstehen keine LGPL-/GPL-Verpflichtungen für das Projekt selbst; beim Selbst-Installieren des Binaries gelten die Lizenzbedingungen der jeweiligen FFmpeg-Distribution.
