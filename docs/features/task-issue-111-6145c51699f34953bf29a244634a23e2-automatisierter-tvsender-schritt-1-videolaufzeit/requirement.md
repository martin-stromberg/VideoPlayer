# Technische Anforderungsübersetzung: Videolaufzeit erfassen (Schritt 1 – Automatisierter TV-Sender)

**Projekt:** Automatisierter TV-Sender (GitHub Issue #111, Aufgaben-ID 6145c516-99f3-4953-bf29-a244634a23e2)
**Schritt:** 1 von 7 — Videolaufzeit erfassen
**Branch:** `task-issue-111-6145c51699f34953bf29a244634a23e2-automatisierter-tvsender-schritt-1-videolaufzeit`
**Basisbranch:** `task/issue-111-6145c51699f34953bf29a244634a23e2-automatisierter-tvsender`
**Erstellt:** 2026-10-22

---

## Fachliche Zusammenfassung

Das Datenmodell wird um die Wiedergabedauer von Videodateien erweitert: Für jedes abspielbare `MediaItem` (Videodatei) wird eine Laufzeit geführt, die später über `Movie`/`TVShowEpisode` den Sendeplätzen des automatisierten TV-Senders zugeordnet werden kann. Die Laufzeit wird per externem `ffprobe`-Prozess aus der Videodatei selbst ermittelt — automatisiert beim regulären Medien-Scan für neue Dateien sowie als Hintergrund-Nachtragsbefüllung für den bestehenden Bestand, ohne Anwenderinteraktion. Dateien, deren Laufzeit nicht bestimmbar ist, werden als „fehlgeschlagen" markiert und bleiben damit für die spätere Sendeplanung erkennbar ausgeschlossen; sie stören weder Scan noch Betrieb. Die Bereitstellung des ffprobe-Binaries (konfigurierbarer Pfad bzw. Konvention) samt Lizenz- und Build-/CI-Betrachtung ist Teil des Schritts.

## Akzeptanzkriterien

1. **Laufzeit-Feld:** Jedes `MediaItem` kann eine Laufzeit speichern (TimeSpan), die aus der Videodatei ermittelt wird.
2. **Probe beim Scan:** Neu gescannte Videodateien erhalten automatisiert eine Laufzeitmessung per `ffprobe`, ohne manuelles Eingreifen.
3. **Backfill für Bestand:** Bereits vorhandene `MediaItem`s ohne Laufzeit werden automatisiert nachgefüllt.
4. **Fehlerkennzeichnung:** Videos, deren Laufzeit per `ffprobe` nicht bestimmt werden kann, sind als „Probe fehlgeschlagen" erkennbar (unterscheidbar von „noch nicht geprüft") und werden nicht endlos erneut geprüft.
5. **Keine Störung:** Weder fehlgeschlagene Probes noch ein fehlendes ffprobe-Binary stören Scan, Klassifizierung oder sonstigen Betrieb.
6. **Dateiwechsel:** Wird eine Videodatei ersetzt (Scan erkennt geändertes `CreatedAt`), wird die Laufzeit neu ermittelt.
7. **Migration + Backup:** Neue Spalten sind durch eine EF-Migration abgesichert und in `VideoWebPlayerBackupData.OptionalRestoreColumns` registriert; ein Alt-Backup ohne die Spalten ist weiterhin wiederherstellbar (Regressionstest).
8. **Binary-Bereitstellung:** Es ist definiert und dokumentiert, wie `ffprobe` bereitgestellt wird; die Lizenzprüfung (LGPL/GPL) ist dokumentiert; Build und CI benötigen kein eingechecktes Binary.

## Betroffene Klassen und Komponenten

### Datenmodell (zu ändern)

- **`MediaItem`** (`VideoWebPlayer/Data/MediaItem.cs`) — neue Eigenschaften:
  - `Duration` (`TimeSpan?`) — ermittelte Wiedergabedauer, `null` wenn keine ermittelt werden konnte/noch nicht geprüft.
  - `DurationProbedAt` (`DateTime?`) — Zeitpunkt der letzten Probe; `null` = noch nie geprüft (zusammen mit `Duration == null` + `DurationProbedAt != null` = „Probe fehlgeschlagen").
- **`MediaItems`-Tabelle:** EF-Migration für beide Spalten; `MediaItemConfiguration` ggf. Index auf `DurationProbedAt` für die Backfill-Abfrage.

### Neue Logikklassen / Services

- **`IMediaDurationProbe` / `FfprobeMediaDurationProbe`** (neu) — kapselt den `ffprobe`-Prozessaufruf:
  - `ProbeFileAsync(path)` für lokale Dateien (Dateipfad direkt an ffprobe).
  - `ProbeStreamAsync(stream)` für entfernte Dateien (SFTP) — ffprobe liest von `stdin` (`pipe:0`).
  - Ergebnis `TimeSpan?`; `null` bei nicht bestimmbarer Dauer, Fehlerausgabe oder Timeout.
- **`IFfprobeLocator` / `FfprobeLocator`** (neu) — Auflösung des ffprobe-Pfads: konfigurierter Pfad (`MediaProbe:FfprobePath`) → Konventionsverzeichnis neben der Anwendung → `PATH`-Suche.
- **`MediaDurationProbeService`** (neu, scoped) — fachliche Verarbeitung: lädt eine Charge ungeprobter Video-`MediaItem`s (mit `MediaCollection.MediaSource`), ruft den Probe auf und persistiert `Duration`/`DurationProbedAt`.
- **`MediaDurationProbeWorker`** (neu, `BackgroundService`) — periodischer Worker (Muster `MediaSourceScanService`/`ActorBackfillWorker`): Startup-Catch-up + Intervall-Schleife über ungeprobte Items, gebremst über `IBackgroundProcessingGate`.

### Zu ändernde Klassen

- **`MediaSourceScanner.ScanMediaCollectionInternalAsync`** — bei erkanntem Dateiwechsel (`existingItem.CreatedAt != file.CreatedAt`) `Duration`/`DurationProbedAt` zurücksetzen, damit neu geprüft wird.
- **`ApplicationDbContext`** — keine neuen DbSets, nur Spalten auf `MediaItems`.
- **`VideoWebPlayerBackupData`** — `OptionalRestoreColumns` um `MediaItems.Duration` und `MediaItems.DurationProbedAt` erweitern.
- **`ServiceCollectionExtensions.AddVideoWebPlayerServices`** — Registrierung von Options, Probe, Service und `AddHostedService<MediaDurationProbeWorker>()`.
- **`appsettings.json`** — neuer Abschnitt `MediaProbe` (Pfad-Override, Intervall, Batchgröße, Timeout).

### Konfiguration

- `MediaProbe:FfprobePath` (`string?`, Standard `null` = Auto-Locate) — optionaler absoluter Pfad zum ffprobe-Binary.
- `MediaProbe:IntervalSeconds` (`int`, Standard ca. 60) — Polling-Intervall des Workers.
- `MediaProbe:BatchSize` (`int`, Standard ca. 10–25) — maximale Items pro Durchlauf.
- `MediaProbe:TimeoutSeconds` (`int`, Standard ca. 120) — Timeout je ffprobe-Aufruf (wichtig für gestreamte SFTP-Dateien).

### Tests

- **Unit-Tests** `MediaDurationProbeServiceTests` (echtes SQLite, shared in-memory): Items ohne Probe werden verarbeitet; erfolgreiche Probe schreibt `Duration`+`DurationProbedAt`; fehlgeschlagene Probe schreibt nur `DurationProbedAt` und `Duration == null`; bereits geprüfte Items werden nicht erneut verarbeitet; Nicht-Video-Dateien werden übersprungen.
- **Unit-Tests** `FfprobeMediaDurationProbeTests`: Ausgabeparsing (gültige Sekundenangabe, „N/A", Fehlerausgabe, Exit-Code ≠ 0), Argumente/Pfad-Handling — über austauschbare Prozess-Abstraktion ohne echtes ffprobe.
- **Unit-Test** `FfprobeLocatorTests`: Auflösungsreihenfolge (Konfiguration → Konvention → PATH).
- **Unit-Test** `MediaSourceScanner`: geändertes `CreatedAt` setzt `Duration`/`DurationProbedAt` zurück.
- **Backup-Regressionstest** `VideoWebPlayerBackupDataTests`: Alt-Backup ohne `MediaItems.Duration`/`DurationProbedAt` wird wiederhergestellt; Spalten fehlen im Payload → `null`-Standard.

## Implementierungsansatz

1. Datenmodell erweitern (`MediaItem.Duration`, `MediaItem.DurationProbedAt`) + EF-Migration.
2. ffprobe-Kapselung (`IMediaDurationProbe`) und Pfadauflösung (`FfprobeLocator`) implementieren.
3. `MediaDurationProbeService` + `MediaDurationProbeWorker` implementieren und in DI registrieren; Worker läuft mit `IBackgroundProcessingGate`-Lease.
4. Scan-Rücksetzung bei Dateiwechsel in `MediaSourceScanner` ergänzen.
5. `OptionalRestoreColumns` registrieren + Regressionstest.
6. Konfiguration (`MediaProbe`-Abschnitt) und Dokumentation (ffprobe-Bereitstellung, Lizenz) erstellen.

### ffprobe-Aufrufkonvention

- Lokal: `ffprobe -v error -show_entries format=duration -of default=noprint_wrappers=1:nokey=1 "<pfad>"`
- Entfernt (SFTP): gleiche Argumente mit Eingabe `pipe:0`; Dateiinhalt wird über `IMediaSourceReader.OpenFileStream` in `stdin` des Prozesses gestreamt.
- Parse: `double`-Sekunden → `TimeSpan`; leere/`N/A`-Ausgabe oder Exit ≠ 0 oder Timeout → Probe fehlgeschlagen.

### Erkennbarkeit / Ausschluss

- `DurationProbedAt == null` → „noch nicht geprüft" (wird vom Worker aufgegriffen).
- `DurationProbedAt != null && Duration != null` → Laufzeit bekannt.
- `DurationProbedAt != null && Duration == null` → „Probe fehlgeschlagen" — erkennbar, wird nicht erneut versucht, in Schritt 3 (Sendeplanung) ausgeschlossen.

## Betroffene Video-Dateiendungen

Videodateien werden über die bestehende Endungsliste des `MediaSourceClassifier` identifiziert (`.mp4`, `.avi`, `.mkv`, `.mpeg`) — dieselbe Liste wird für die Probe-Selektion zentralisiert bzw. wiederverwendet.

## Offene Fragen

Keine — Designentscheidungen (Speicherort `MediaItem` statt `Movie`/`TVShowEpisode`; Streaming-Probe für SFTP; konfigurierbarer ffprobe-Pfad statt Bundling) sind aus Anforderung und Code-Konventionen ableitbar und werden im Plan festgehalten.
