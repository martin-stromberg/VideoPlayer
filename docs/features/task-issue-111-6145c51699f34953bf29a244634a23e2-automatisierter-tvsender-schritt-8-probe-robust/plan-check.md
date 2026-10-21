# Plan-Check – Robustheit der Laufzeitermittlung (N4)

**Status:** Plan vollständig

## Prüfung der Akzeptanzkriterien gegen den Plan

| Kriterium (requirement.md) | Planabdeckung | Bewertung |
|-----------------------------|---------------|-----------|
| 1. Getrenntes Timeout Datei vs. `pipe:0` | Schritt 1 (Option `StreamTimeoutSeconds`) + Schritt 5 (`ProbeStreamAsync` nutzt `EffectiveStreamTimeoutSeconds`); Test über `_executeAsync`-Seam geplant | Abgedeckt |
| 2. Retry statt permanenter Markierung | Schritt 3 (`DurationProbeAttempts`) + Schritt 6 (Auswahl-Query mit `RetryDelay`-Gate und `MaxAttempts`-Obergrenze); Fehlerklassifikation transient/permanent in Schritt 4+5 | Abgedeckt |
| 3. Backlog-Aufgriff alter Fehler | Neue Spalte default 0 → alle Alt-Items mit `Duration == null` fallen automatisch in die Retry-Selektion; Test „Bestandsitem wird wieder geprüft" geplant | Abgedeckt |
| 4. Durchsatz/Parallelität | `ParallelProbes` (Default 4) + `SemaphoreSlim`-Design, DbContext bleibt seriell; Test für seriell/parallel geplant | Abgedeckt |
| 5. Planbarkeit nach nachträglicher `Duration` | `ProgramScheduleService` selektiert bereits `Duration != null` (kein Code nötig); Schritt 6 löst `IProgramScheduleSignal.NotifyScheduleChanged()` aus; Test geplant | Abgedeckt |
| 7. Alt-Backup-Regression | `OptionalRestoreColumns` + `OptionalRestoreIntDefaults` + neuer Restore-Test nach vorhandenem Muster | Abgedeckt |
| 8. Build + volle Testsuite | Schritt 7 + regulärer Ablauf | Abgedeckt |

## Geprüfte Risiken

- **DbContext-Thread-Sicherheit:** Plan sieht vor, dass parallele Tasks nur ffprobe/Stream anfassen und die Entitätsanwendung + `SaveChangesAsync` seriell erfolgt. Korrekt — EF-Core-Kontext wird nie konkurrierend benutzt.
- **`SftpStreamWrapper`/`OpenFileStream`:** Jeder Aufruf baut einen eigenen `SftpClient` (Bestandsaufnahme); parallele Probes erzeugen keine geteilte Verbindung. Hinweis aufgenommen: `Connect()` erfolgt synchron — bei parallelem Batch kurzzeitig N SFTP-Verbindungen; durch `ParallelProbes` begrenzt und konfigurierbar.
- **Semantik-Änderung `DurationProbedAt`:** Wird von „endgültig entschieden" zu „letzter Versuch". Externe Leser geprüft: `ProgramScheduleService` nutzt nur `Duration != null`; Scan-Code setzt `DurationProbedAt` beim Ersetzen von Dateien zurück (siehe Schritt-1-Konvention „ersetzte Datei wird erneut geprüft" — unverändert konsistent, da `DurationProbeAttempts` beim Rescan ebenfalls zurückgesetzt werden muss → **im Implementierungsschritt prüfen, ob der Rescan-Pfad `Duration`/`DurationProbedAt` zurücksetzt und `DurationProbeAttempts` mit zurücksetzen**).
- **Cancellation:** parallele Tasks müssen `OperationCanceledException` bei Abbruch propagieren; Ergebnisanwendung darf nach Abbruch keine halben Batches speichern — `Task.WhenAll` bricht, bereits angewendete Items wurden pro Item gespeichert (konsistent mit Ist-Verhalten).

## Ergänzung bei der Prüfung aufgefallen (in Schritt 6 aufzunehmen)

Beim Rescan einer ersetzten Datei (Scan setzt `DurationProbedAt`/`Duration` zurück) muss auch `DurationProbeAttempts` zurückgesetzt werden, sonst bleibt ein permanent ausgeschöpftes Item trotz neuester Dateiversion ausgeschlossen. Stelle suchen: `MediaSourceScanService`/Scan-Code, der `DurationProbedAt = null` o. ä. setzt.

## Testvollständigkeit

- Unit/Integration: Retry-Gate, MaxAttempts, permanent vs. transient, Stream-Timeout, Signal-Auslösung, Parallelitätsgrad, Backup-Regression — alle als konkrete Tests benannt.
- E2E: Keine UI-/Benutzerfluss-Änderung → keine E2E-Tests erforderlich; Funktionsnachweis über Service-Integrationstests mit echtem SQLite (Repo-Konvention).
