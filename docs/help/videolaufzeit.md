# Videolaufzeit

Die Anwendung ermittelt für jede Videodatei in der Mediathek automatisch die Wiedergabedauer. Sie wird benötigt, um Sendeplätze des automatisierten TV-Senders zeitlich belegen zu können (siehe Projekt „Automatisierter TV-Sender").

## Wie es funktioniert

- Ein Hintergrunddienst (`MediaDurationProbeWorker`) prüft regelmäßig alle Videodateien (`*.mp4`, `*.avi`, `*.mkv`, `*.mpeg`), für die noch keine Laufzeit vorliegt — sowohl neu gescannte Dateien als auch der bereits vorhandene Bestand. Ein manuelles Auslösen ist nicht nötig.
- Die Laufzeit wird mit dem externen Werkzeug **ffprobe** direkt aus der Videodatei gelesen — bei lokalen Quellen über den Dateipfad. Bei entfernten Quellen probiert die Anwendung zuerst ihren **eigenen internen HTTP-Stream-Endpunkt**: Der Server stellt ffprobe eine Loopback-URL der Form `http://127.0.0.1:{port}/api/internal/media-stream/{token}` zur Verfügung, über die ffprobe mit HTTP-Range-Requests **seeken** kann — es liest nur die Metadaten am Dateianfang/-ende (Sekunden statt Minuten) und greift dabei über denselben Quell-Lesepfad zu wie die Wiedergabe (z. B. SSH.NET bei SFTP), also unabhängig vom Quelltyp und ohne das als instabil befundene libssh. Der Zugriff ist dabei an ein kurzlebiges, zufälliges **Stream-Token** gebunden (gültig nur für genau diese eine Datei, nur im Arbeitsspeicher, begrenzte Lebensdauer) — kein Benutzerkonto und kein langlebiges Zugangsgeheimnis steht dafür in der Kommandozeile des ffprobe-Prozesses. Der Endpunkt folgt zudem dem globalen Freischalteschalter des TV-Senders wie alle übrigen Sender-Endpunkte — ohne Freigabe antwortet er 404, auch bei noch gültigem Token (die Laufzeitermittlung selbst läuft ohnehin nur bei freigegebenem Sender, siehe [TV-Sender](tv-sender.md)). Die Loopback-Adresse wird aus den gebundenen Adressen der laufenden Instanz ermittelt (`MediaProbe:HttpProbeBaseUrl` setzt sie bei Bedarf explizit, `MediaProbe:HttpProbeEnabled` schaltet den Pfad ab). Bei SFTP-Quellen mit hinterlegten Zugangsdaten bleibt als zweite Stufe die `sftp://`-URL erhalten, die ffprobe über libssh selbst öffnet (ebenfalls seekfähig — funktioniert auf kompatiblen Servern, erwies sich aber in der Praxis auf manchen Builds als instabil). Kann weder die HTTP-URL gebildet bzw. bedient werden noch eine `sftp://`-URL, fällt die Prüfung auf das bisherige Streaming (`pipe:0`) zurück — dabei bekommen gestreamte Probes weiterhin das größere Zeitlimit `MediaProbe:StreamTimeoutSeconds`, weil ffprobe bei manchen Containern die gesamte Datei lesen muss.
- **Hinweis zur Sichtbarkeit der Zugangsdaten:** Bei der `sftp://`-Probe steht das SFTP-Passwort in der Kommandozeile des ffprobe-Prozesses — es ist damit für Prozesse desselben Benutzers auf dem Server (z. B. Prozessliste) während des wenige Sekunden laufenden Aufrufs sichtbar. libssh bietet keine andere Übergabe der Zugangsdaten; das Passwort liegt ohnehin bereits lesbar in der lokalen Anwendungsdatenbank. In Protokolldateien wird es niemals ausgegeben — Meldungen maskieren die Zugangsdaten in der URL.
- Schlägt eine Prüfung fehl, wird unterschieden: **Vorübergehende Fehler** (Zeitüberschreitung, unterbrochener Stream, ffprobe nicht startbar, abgebrochener ffprobe-Lauf) werden automatisch wiederholt — pro Datei zählt ein Versuchszähler, zwischen zwei Versuchen liegt eine konfigurierbare Wartezeit (`MediaProbe:RetryDelaySeconds`), und erst nach `MediaProbe:MaxAttempts` erfolglosen Versuchen gilt die Datei dauerhaft als nicht prüfbar. **Dauerhafte Fehler** werden sofort endgültig markiert und nicht erneut geprüft — das sind Dateien, die ffprobe sauber durchläuft, ohne eine Laufzeit zu liefern, sowie Dateien, die bei seekfähiger Prüfung (Dateipfad oder `sftp://`-URL) einen endgültigen Strukturfehler melden (z. B. `moov atom not found` oder `Invalid data found` — defekte/unkonventionelle Datei). Bei gestreamten Probes (`pipe:0`) bleiben solche Fehlermeldungen bewusst vorübergehend, weil dieselbe Meldung auch durch einen früh abgebrochenen Stream entstehen kann. Dateien ohne Laufzeit stören weder Scan noch Wiedergabe und sind von der Sendeplanung ausgeschlossen; sobald eine Laufzeit nachträglich ermittelt wird, ist die Datei wieder planbar.
- Wird eine Videodatei ersetzt (der Scan erkennt die geänderte Datei), wird die Laufzeit automatisch neu ermittelt — inklusive Zurücksetzen des Versuchszählers.

## Fortschritt beobachten

Der aktuelle Stand der Ermittlung ist für Administratoren auf der Seite `Einrichtung` → `TV-Sender` in der Karte **„Laufzeitermittlung"** sichtbar: wie viele Videodateien bereits eine Laufzeit haben, wie viele noch ausstehen oder dauerhaft fehlgeschlagen sind, wann zuletzt eine Datei geprüft wurde und welche Dateien zuletzt bearbeitet wurden. Die Anzeige aktualisiert sich selbst — bleiben ausstehende Dateien längere Zeit ohne neue Aktivität, lohnt ein Blick auf die ffprobe-Einrichtung (siehe unten).

In der Liste **„Fehlgeschlagene Dateien"** (hinter „▾ Details" beim fehlgeschlagen-Zähler) kann jede angezeigte Datei einzeln über **„Erneut ermitteln"** wieder in die Ermittlung aufgenommen werden — etwa nachdem eine defekte Datei ersetzt oder repariert wurde, ohne dass der Scan eine Änderung erkannt hat. Die Liste zeigt höchstens 25 Einträge; bei mehr fehlgeschlagenen Dateien weist eine Fußzeile darauf hin, dass diese nur gemeinsam zurückgesetzt werden können. Über **„Alle zurücksetzen"** direkt beim fehlgeschlagen-Zähler (mit Bestätigungsdialog) werden alle dauerhaft fehlgeschlagenen Dateien gemeinsam zurückgesetzt — auch die, die wegen der Listenbegrenzung nicht angezeigt werden. Ein Zurücksetzen löscht Versuchszähler und Zeitstempel; die Datei wird beim nächsten Lauf der Ermittlung erneut geprüft. Bleibt die Datei weiterhin defekt, sammelt sie wieder Versuche und landet nach `MediaProbe:MaxAttempts` erneut in der Fehlerliste.

Technisch stellt die Aktion denselben Zustand her, den der Medien-Scan bei einer geänderten Datei selbst anwendet (`DurationProbedAt = null`, `DurationProbeAttempts = 0`), sodass der Probe-Lauf die Datei wieder als ausstehend auswählt. Beide Zurücksetzen-Aktionen sind Administratoren vorbehalten; die Berechtigung wird serverseitig erzwungen (`MediaDurationProbeService.ResetFailedProbeAsync` für den Einzel-Reset, `ResetFailedProbesAsync` für den Sammel-Reset — Auswahl über die `MediaItem.Id`, die der Listeneintrag als `MediaDurationProbeDetailItem.MediaItemId` mitführt). Der Einzel-Reset greift nur bei Dateien, die aktuell zur Fehlermenge gehören — ein zwischenzeitlich erfolgreich ermittelter Wert kann dadurch nicht verloren gehen; der Sammel-Reset wirkt auf die gesamte Fehlermenge, nicht nur auf die gekappte Anzeige.

## ffprobe einrichten

ffprobe ist Teil von [FFmpeg](https://ffmpeg.org/) und wird **nicht** mit der Anwendung ausgeliefert. Es muss auf dem Server installiert werden.

**Am einfachsten per Download-Schaltfläche** (nur Administratoren): Fehlt ffprobe, zeigt der Tab **„Laufzeitermittlung"** unter `Einrichtung` → `TV-Sender` die Warnung „ffprobe wurde nicht gefunden — die Laufzeitermittlung pausiert." mit der Schaltfläche **„ffmpeg/ffprobe herunterladen"** direkt daneben. Nach einem Bestätigungsdialog mit Lizenzhinweis lädt die Anwendung das zum Server passende statische ffmpeg-Paket herunter und legt `ffmpeg` und `ffprobe` gemeinsam im Ordner `tools/ffmpeg/` neben der Anwendung ab — unter Linux inklusive Ausführungsbit, eine dort vorhandene Version wird ersetzt. Fortschritt, Ergebnis und Fehlerbilder (kein Internet, Zielordner nicht beschreibbar, Plattform nicht unterstützt, Paket unverwendbar) sowie die konfigurierbaren Download-Quellen (`MediaProbe:DownloadSources`) beschreibt [TV-Sender → ffmpeg-Bereitstellung und Lizenz](tv-sender.md).

Alternativ bleibt die manuelle Installation möglich, z. B.:

- Windows: `winget install Gyan.FFmpeg` oder das Build von https://www.gyan.dev/ffmpeg/builds/ bzw. https://ffmpeg.org/download.html
- Linux: `apt install ffmpeg` (Debian/Ubuntu) bzw. Paket der Distribution

Die Anwendung sucht das `ffprobe`-Binary in dieser Reihenfolge:

1. Konfiguration `MediaProbe:FfprobePath` in `appsettings.json` (absoluter Pfad, optional),
2. `tools/ffmpeg/ffprobe(.exe)` und `ffprobe(.exe)` im Anwendungsverzeichnis,
3. `PATH` des Betriebssystems.

Ist kein Binary auffindbar, wartet die Laufzeitermittlung und protokolliert einmalig eine Warnung; im Tab „Laufzeitermittlung" erscheint zusätzlich der beschriebene Hinweis mit Download-Schaltfläche. Der restliche Betrieb läuft unverändert weiter. Nach einer Installation laufen ausstehende Dateien automatisch nach.

## Konfiguration (`appsettings.json`)

| Schlüssel | Standard | Bedeutung |
|-----------|----------|-----------|
| `MediaProbe:FfprobePath` | `null` | Optionaler absoluter Pfad zum ffprobe-Binary |
| `MediaProbe:FfmpegPath` | `null` | Optionaler absoluter Pfad zum ffmpeg-Binary (Live-Stream-Segmentierung) |
| `MediaProbe:DownloadSources:win-x64` / `MediaProbe:DownloadSources:linux-x64` | statische Builds von gyan.dev bzw. johnvansickle.com | Paket-URLs für die Download-Schaltfläche „ffmpeg/ffprobe herunterladen" (Details: [TV-Sender → ffmpeg-Bereitstellung und Lizenz](tv-sender.md)) |
| `MediaProbe:IntervalSeconds` | `60` | Intervall, in dem nach neuen, ungeprüften Dateien gesucht wird |
| `MediaProbe:BatchSize` | `10` | Maximale Anzahl Dateien je Prüfschritt |
| `MediaProbe:TimeoutSeconds` | `120` | Zeitlimit je ffprobe-Aufruf über den lokalen Dateipfad oder eine seekfähige URL (`http://`-Loopback des internen Stream-Endpunkts, `sftp://`) |
| `MediaProbe:StreamTimeoutSeconds` | `600` | Zeitlimit je ffprobe-Aufruf über gestreamte Quellen (`pipe:0`-Fallback, z. B. SFTP ohne Zugangsdaten oder ohne libssh-ffprobe) — bewusst großzügiger, weil ffprobe bei manchen Containern die gesamte Datei lesen muss |
| `MediaProbe:RetryDelaySeconds` | `300` | Mindestabstand zwischen zwei Probe-Versuchen derselben Datei nach einem vorübergehenden Fehler |
| `MediaProbe:MaxAttempts` | `10` | Maximale Anzahl Versuche, bevor eine Datei dauerhaft als nicht prüfbar gilt |
| `MediaProbe:ParallelProbes` | `4` | Maximale gleichzeitige ffprobe-Prozesse je Prüfschritt (`1` = seriell); erhöht den Durchsatz bei langsamen Remote-Quellen, erzeugt aber entsprechend viele parallele Verbindungen zur Quelle |
| `MediaProbe:HttpProbeEnabled` | `true` | Schaltet die HTTP-Probe über den internen Stream-Endpunkt an/aus; bei `false` bleibt nur die `sftp://`-/`pipe:0`-Kaskade |
| `MediaProbe:HttpProbeBaseUrl` | `null` | Optionale explizite Loopback-Basis-URL (z. B. `http://127.0.0.1:5039`) für den HTTP-Probe, wenn die automatische Erkennung aus den gebundenen Server-Adressen keine erreichbare Adresse liefert (z. B. spezielle Bindings); ohne gültige `http://`-Adresse der Instanz bleibt der HTTP-Probe automatisch deaktiviert (HTTPS-only-Installationen nutzen die Fallbacks — selbstsignierte Zertifikate würden die TLS-Prüfung von ffprobe brechen) |
| `MediaProbe:HttpProbeTokenLifetimeSeconds` | `600` | Gültigkeitsdauer eines internen Stream-Tokens für den HTTP-Probe; muss nur einen einzelnen ffprobe-Aufruf überdauern |

## Lizenz

FFmpeg/ffprobe steht unter LGPL 2.1+ bzw. GPL (je nach Build). Die Anwendung verlinkt keine FFmpeg-Bibliotheken, sondern startet `ffprobe` als eigenständigen Prozess (Kommunikation über Ein-/Ausgabe). Für diese Nutzungsform entstehen keine LGPL-/GPL-Verpflichtungen für das Projekt selbst; beim Selbst-Installieren des Binaries gelten die Lizenzbedingungen der jeweiligen FFmpeg-Distribution.
