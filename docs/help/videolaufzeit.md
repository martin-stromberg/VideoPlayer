# Videolaufzeit

Die Anwendung ermittelt für jede Videodatei in der Mediathek automatisch die Wiedergabedauer. Sie wird benötigt, um Sendeplätze des automatisierten TV-Senders zeitlich belegen zu können (siehe Projekt „Automatisierter TV-Sender").

## Wie es funktioniert

- Ein Hintergrunddienst (`MediaDurationProbeWorker`) prüft regelmäßig alle Videodateien (`*.mp4`, `*.avi`, `*.mkv`, `*.mpeg`), für die noch keine Laufzeit vorliegt — sowohl neu gescannte Dateien als auch der bereits vorhandene Bestand. Ein manuelles Auslösen ist nicht nötig.
- Die Laufzeit wird mit dem externen Werkzeug **ffprobe** direkt aus der Videodatei gelesen — bei lokalen Quellen über den Dateipfad, bei SFTP-Quellen wird die Datei gestreamt (sie wird nicht auf den Server heruntergeladen und nicht als Ganzes im Speicher gehalten). Gestreamte Probes bekommen ein eigenes, größeres Zeitlimit (`MediaProbe:StreamTimeoutSeconds`), weil ffprobe bei manchen Containern die gesamte Datei lesen muss.
- Schlägt eine Prüfung fehl, wird unterschieden: **Vorübergehende Fehler** (Zeitüberschreitung, unterbrochener Stream, ffprobe nicht startbar, abgebrochener ffprobe-Lauf) werden automatisch wiederholt — pro Datei zählt ein Versuchszähler, zwischen zwei Versuchen liegt eine konfigurierbare Wartezeit (`MediaProbe:RetryDelaySeconds`), und erst nach `MediaProbe:MaxAttempts` erfolglosen Versuchen gilt die Datei dauerhaft als nicht prüfbar. **Dauerhafte Fehler** (ffprobe läuft sauber durch, findet aber keine bestimmbare Laufzeit — z. B. defekte Datei oder kein lesbares Videoformat) werden sofort endgültig markiert und nicht erneut geprüft. Dateien ohne Laufzeit stören weder Scan noch Wiedergabe und sind von der Sendeplanung ausgeschlossen; sobald eine Laufzeit nachträglich ermittelt wird, ist die Datei wieder planbar.
- Wird eine Videodatei ersetzt (der Scan erkennt die geänderte Datei), wird die Laufzeit automatisch neu ermittelt — inklusive Zurücksetzen des Versuchszählers.

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
| `MediaProbe:TimeoutSeconds` | `120` | Zeitlimit je ffprobe-Aufruf über den lokalen Dateipfad |
| `MediaProbe:StreamTimeoutSeconds` | `600` | Zeitlimit je ffprobe-Aufruf über gestreamte Quellen (`pipe:0`, z. B. SFTP) — bewusst großzügiger, weil ffprobe bei manchen Containern die gesamte Datei lesen muss |
| `MediaProbe:RetryDelaySeconds` | `300` | Mindestabstand zwischen zwei Probe-Versuchen derselben Datei nach einem vorübergehenden Fehler |
| `MediaProbe:MaxAttempts` | `10` | Maximale Anzahl Versuche, bevor eine Datei dauerhaft als nicht prüfbar gilt |
| `MediaProbe:ParallelProbes` | `4` | Maximale gleichzeitige ffprobe-Prozesse je Prüfschritt (`1` = seriell); erhöht den Durchsatz bei langsamen Remote-Quellen, erzeugt aber entsprechend viele parallele Verbindungen zur Quelle |

## Lizenz

FFmpeg/ffprobe steht unter LGPL 2.1+ bzw. GPL (je nach Build). Die Anwendung verlinkt keine FFmpeg-Bibliotheken, sondern startet `ffprobe` als eigenständigen Prozess (Kommunikation über Ein-/Ausgabe). Für diese Nutzungsform entstehen keine LGPL-/GPL-Verpflichtungen für das Projekt selbst; beim Selbst-Installieren des Binaries gelten die Lizenzbedingungen der jeweiligen FFmpeg-Distribution.
