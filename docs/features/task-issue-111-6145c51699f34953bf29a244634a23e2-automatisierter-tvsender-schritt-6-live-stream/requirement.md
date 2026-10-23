# Übersetzte Anforderung: Live-Stream ausliefern (HLS-Segmentierung des laufenden Programms)

Quelle: `docs/projects/task/issue-111-6145c51699f34953bf29a244634a23e2-automatisierter-tvsender/project-plan.md`, Schritt 6 + Vorgehensentscheidungen (Streaming-Architektur, externe Abhängigkeit FFmpeg, Stream-Charakter, Konfiguration).

## Fachliche Zusammenfassung

Der automatisierte TV-Sender wird als durchgehender Live-Stream per echter Segmentierung abspielbar. Ein dauerhaft laufender Segmentierungs-/Transcodierungsprozess auf Basis von `ffmpeg` verarbeitet das laut Sendeplan gerade laufende Video fortlaufend zu HLS-Segmenten und wird am Sendeplatz-Übergang auf das nächste Video umgeschaltet, sodass alle Zuschauer zeitgleich denselben Programmpunkt sehen. Die Zeitbasis liefert die bestehende Programmauskunft (`IProgramGuideService.GetNowAsync` mit `PositionSeconds`); nach einem Server-Neustart synchronisiert sich der Prozess allein über Uhrzeit und Sendeplan wieder auf den aktuellen Programmpunkt — es wird kein Prozesszustand fortgesetzt. Segmente und Wiedergabeliste liegen an einem konfigurierbaren Speicherort; abgelaufene Segmente werden automatisiert anhand einer konfigurierbaren Retention aufgeräumt. Die Auslieferung erfolgt über eigene HTTP-Endpunkte, die nur authentifizierte Benutzer bzw. gekoppelte Geräte erreichen — über das etablierte `access_token`-Abfrageparameter-Muster, weil Videoplayer keine Autorisierungs-Header mitsenden. Hinzu kommt die Einführung von `ffmpeg` als externe Abhängigkeit (Bereitstellung, Lizenz, Build/CI).

## Akzeptanzkriterien

1. **Echte Segmentierung:** Ein vom Server gestarteter, dauerhaft laufender `ffmpeg`-Prozess erzeugt aus dem aktuell laufenden Sendeplatz fortlaufend HLS-Segmente (`.ts` bzw. `.m4s` + `.m3u8`) im Segment-Verzeichnis. Kein virtueller Stream aus berechneten Wiedergabelisten über den Originaldateien.
2. **Zeitbasierte Wiederaufnahme:** Beim Start (auch nach Server-Neustart) bestimmt der Prozess über `IProgramGuideService.GetNowAsync` den laufenden `ProgramSlot` samt `PositionSeconds` und startet `ffmpeg` auf der zugehörigen Videodatei mit entsprechendem Offset (`-ss`), ohne Prozesszustand aus der Datenbank oder dem Dateisystem zu rekonstruieren.
3. **Sendeplatz-Übergang:** Erreicht die Sendezeit das Ende des laufenden Slots, wird der `ffmpeg`-Prozess sauber beendet und auf das Nachfolge-Video neu gestartet (erneut mit zeitbasierter Position). Die HLS-Ausgabe muss für den Player konsistent bleiben (saubere Diskontinuität, keine verstümmelte/abgelaufene Wiedergabeliste); ein Player kann durchgängig weitersehen.
4. **Sendelücke:** Deckt kein `Scheduled`-Slot die aktuelle Zeit ab, läuft kein Segmentierungsprozess; der Auslieferungsendpunkt signalisiert den Zustand verständlich (z. B. 404/503), und der Prozess nimmt die Arbeit zeitbasiert wieder auf, sobald der nächste Slot beginnt.
5. **Konfigurierbare Segmentlänge:** Die Ziel-Segmentdauer ist über die etablierten Einstellungsmechanismen (`Setup`/`IProgramSettingsService`, Admin-Oberfläche TV-Sender) konfigurierbar.
6. **Konfigurierbarer Speicherort + Retention:** Das Segment-Verzeichnis ist konfigurierbar (Standard: Unterverzeichnis im App-Datenverzeichnis, kein Quellcode-Verzeichnis). Abgelaufene Segmente und Wiedergabelisten werden automatisiert anhand einer konfigurierbaren Retention entfernt; das Verzeichnis wächst nicht ungebremst.
7. **Authentifizierte Auslieferung:** HLS-Wiedergabeliste und Segmente werden über eigene Endpunkte nur für authentifizierte Benutzer/gekoppelte Geräte ausgeliefert — `BearerTokenCheckAttribute`-Muster mit `access_token`-Abfrageparameter (Player-kompatibel) zusätzlich zum `Authorization`-Header. Anonyme Aufrufe → 401. Zugriffsrechte werden serverseitig erzwungen (angemeldeter Benutzer; die Inhalte sind per Planungsregel bereits vollständig freigegeben).
8. **Pfadsicherheit:** Die Segment-Auslieferung darf keine Dateien außerhalb des Segment-Verzeichnisses ausliefern (kein Path-Traversal über Segment-/Playlist-Namen).
9. **FFmpeg-Bereitstellung:** `ffmpeg` wird analog `ffprobe` lokalisiert (Suchreihenfolge konfigurierbarer Pfad → `tools/ffmpeg/` → PATH), nicht gebundelt; Prozessaufruf = mere aggregation. Fehlt das Binary, ist der Zustand sichtbar und der Rest der Anwendung bleibt funktionsfähig.
10. **Lizenz/Build/CI:** Lizenzlage von FFmpeg ist beurteilt und dokumentiert; Build- und CI-Abläufe sind angepasst, soweit für die neue Abhängigkeit erforderlich.
11. **Kein Schema-Zwang für Stream-Zustand:** Die Wiederaufnahme ist rein zeitbasiert; es wird kein persistenter Prozess-/Stream-Zustand geführt. (Konfigurationsfelder an `Setup` folgen dem üblichen Migrations-/Backup-Registrierungs-Muster, falls sie persistiert werden.)
12. **Namensabgrenzung:** Sender-Vokabular bleibt konsistent (Sender/Live-Stream/Sendeplatz) — kein „Playlist"-Naming für den Stream (die HLS-„Playlist"-Datei selbst ist ein Dateiformat-Begriff und bleibt davon unberührt).

## Betroffene Klassen und Komponenten

- **Neu:** `FfmpegLocator` (analog `FfprobeLocator`) — Lokalisierung des `ffmpeg`-Binaries; `MediaProbe`-Optionen ggf. erweitert (analoger `ExecutablePath`/`ProbePath`-Mechanismus).
- **Neu:** Segmentierungs-/Stream-Hintergrunddienst (Worker-Muster `ProgramScheduleWorker` + `IBackgroundProcessingGate`), der den ffmpeg-Prozess zeitbasiert startet/umstellt/beendet; Prozessverwaltung als eigene, testbare Komponente.
- **Neu:** Retention-Aufräumlogik für das Segment-Verzeichnis (im Worker oder als eigener Dienst).
- **Neu:** Controller für HLS-Auslieferung (Wiedergabeliste + Segmente) mit `BearerTokenCheck` + `CheckLoggedIn`-Muster; Path-Traversal-sichere Dateiauslieferung aus dem Segment-Verzeichnis.
- **Bestehend (genutzt):** `IProgramGuideService` (`GetNowAsync`, `PositionSeconds`, `Next`), `ProgramSlot`, `MediaItem`/`Movie`/`TVShowEpisode` (Dateipfade), `IProgramSettingsService`/`Setup` (neue Einstellungen: Segmentlänge, Speicherort, Retention), `FfprobeLocator`-Suchreihenfolge, `IBackgroundProcessingGate`, `ProgramScheduleSignal` (ggf. als Wecksignal bei Plan-Nachführung).
- **Admin-UI:** Erweiterung `TVSenderAdmin`/Setup-Bereich um die neuen Stream-Einstellungen (Segmentlänge, Speicherort, Retention) — wie bestehende Sender-Einstellungen.
- **Tests:** Service-/Prozesslogik-Tests (zeitbasierte Übergänge, Retention, Pfadauflösung mit Fake-Locator/Fake-Prozess), Controller-Tests (401 anonym, Pfadsicherheit, Content-Type), HTTP-E2E (`DeviceClientTestBase`-Muster) für authentifizierten Abruf inkl. `access_token`-Query; E2E der Admin-Einstellungen, falls UI-Felder neu sind.

## Implementierungsansatz

- Worker analog `ProgramScheduleWorker`: periodischer Tick (und/oder präziser Timer bis Slot-Ende), liest `GetNowAsync`, startet `ffmpeg -ss {PositionSeconds} -i {Videodatei} ... -f hls -hls_time {Segmentlänge} {Ausgabeverzeichnis}`, beendet bei Slot-Wechsel und startet auf dem Nachfolger neu.
- ffmpeg-Argumentation: `-ss` vor `-i` (Input-Seeking, schnell) oder danach (Output-Seeking, exakt) — Entscheidung im Plan unter Präzisions-/Performance-Gesichtspunkten; HLS-Optionen (`-hls_time`, `-hls_list_size`, `-hls_flags`, ggf. `-strftime`/löschende Optionen vs. eigene Retention) im Plan festlegen.
- Auslieferung: eigener Controller unter eigenem Routen-Präfix (z. B. `api/...`), der `.m3u8` und `.ts` aus dem Segment-Verzeichnis ausliefert; `BearerTokenCheckAttribute` akzeptiert bereits `access_token`-Query (aus Schritt 5 bekannt).
- `FfprobeLocator` als Vorbild für `FfmpegLocator`; `MediaProbe`-Optionsmuster (`ExecutablePath` etc.) prüfen und analog wiederverwenden.
- Einstellungen an `Setup` (Persistenz + Migration + `OptionalRestoreColumns`-Registrierung + Alt-Backup-Regressionstest), Editierbarkeit über `ProgramSettingsService` und Admin-UI — Muster der bestehenden Sender-Einstellungen (Horizont, Intervall, Bundesland).

## Konfiguration

- `Setup`-Felder (über `IProgramSettingsService`, Admin-UI): Segmentlänge (Sekunden), Segment-Speicherort (Pfad, leer = Standard unter App-Daten), Segment-Retention (Minuten/Stunden).
- `ffmpeg`-Pfad: analog `ffprobe` (konfigurierbarer Pfad in den MediaProbe-Optionen → `tools/ffmpeg/` → PATH).

## Offene Fragen

Keine — Architektur (echte Segmentierung, kein virtueller Stream), Umschaltstrategie (Prozess-Neustart pro Slot mit `-ss`-Offset), Authentifizierung (`access_token`-Muster), Konfigurierbarkeit und der Umfang der FFmpeg-Bereitstellung sind durch Vorgehensentscheidungen und Stakeholder-Vorgaben festgelegt.
