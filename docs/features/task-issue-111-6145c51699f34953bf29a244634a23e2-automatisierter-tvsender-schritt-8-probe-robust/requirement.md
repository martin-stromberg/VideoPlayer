# Übersetzte Anforderung – Nachbesserung N4: Robuste Laufzeitermittlung für Remote-Quellen + Retry

## Fachliche Zusammenfassung

Die automatische Laufzeitermittlung (`MediaDurationProbeWorker`/`MediaDurationProbeService` mit `FfprobeMediaDurationProbe`) muss für entfernte, per `pipe:0` gestreamte Quellen robust werden: längeres, separat konfigurierbares Timeout, Wiederholung transienter Fehler statt dauerhafter Fehlermarkierung, und ausreichend Durchsatz, um den bestehenden Backlog (~111.000 ungeprüfte `MediaItem`s) realistisch abzuarbeiten. Bereits als fehlgeschlagen markierte Bestandsitems (`DurationProbedAt` gesetzt, `Duration` NULL) müssen automatisch wieder in die Prüfung aufgenommen werden. Sobald für ein Item eine `Duration` ermittelt wurde, muss es für den `ProgramScheduleService` wieder planbar sein.

## Live-Befund (Ausgangslage)

- Quasi jeder ffprobe-Aufruf über `pipe:0` (SFTP-Stream) läuft ins feste 120-s-Timeout.
- 111.783 MediaItems: 49 ok, 29 failed, 111.705 offen → ~30 Items/Stunde Durchsatz.
- Vermutete Ursache: MP4 ohne vorgelagertes `moov`-Atom zwingt ffprobe, die gesamte Datei zu lesen — über SFTP-Pipe zu langsam für 120 s.
- Verschärfend: Timeout/IO-Fehler setzen `DurationProbedAt` ohne `Duration` → Item permanent „unplanbar", obwohl die Ursache nur Latenz ist.

## Fachliche Muss-Anforderungen

1. **Konfigurierbares Timeout mit Unterscheidung lokaler Pfad vs. `pipe:0`:**
   - `MediaProbe:TimeoutSeconds` bleibt für lokale Dateipfade (Default 120 s).
   - Neuer Schlüssel (z. B. `MediaProbe:StreamTimeoutSeconds`) für Stream-/Pipe-Probes mit sinnvoll größerem Default (600 s).
2. **Retry-Strategie statt permanenter Markierung:**
   - Fehlerklassen unterscheiden: „ffprobe lief sauber durch, konnte aber keine Dauer liefern" = dauerhaft fehlgeschlagen; „Timeout / IO / Prozessstart / abgebrochener Stream" = transient → Wiederholung.
   - Wiederholungen über einen Versuchszähler (`MediaItem.DurationProbeAttempts`) mit konfigurierbarer Wartezeit zwischen Versuchen (`MediaProbe:RetryDelaySeconds`) und konfigurierbarer Obergrenze (`MediaProbe:MaxAttempts`); danach dauerhaft fehlgeschlagen.
   - Bestandsitems mit gesetztem `DurationProbedAt` und `Duration == null` werden automatisch wieder aufgegriffen (neuer Zähler startet bei 0).
3. **Durchsatz:** konfigurierbare Parallelität der Probes innerhalb eines Batches (`MediaProbe:ParallelProbes`, Default > 1), damit lang laufende Remote-Probes den Batch nicht seriell blockieren.
4. **Planbarkeit:** Items, deren `Duration` nachträglich ermittelt wird, sind für die Sendeplanung (`ProgramScheduleService` selektiert `MediaItem.Duration != null`) sofort wieder planbar; die Laufzeitermittlung soll den `IProgramScheduleSignal` auslösen, sobald mindestens eine Laufzeit geschrieben wurde.

## Betroffene Klassen und Komponenten

- `VideoWebPlayer/Configuration/MediaProbeOptions.cs` — neue Optionen.
- `VideoWebPlayer/appsettings.json` — neue Schlüssel im Abschnitt `MediaProbe`.
- `VideoWebPlayer/Data/MediaItem.cs` — neue Spalte `DurationProbeAttempts` (int, Default 0) + aktualisierte XML-Doku.
- `VideoWebPlayer/Migrations/` — neue EF-Migration.
- `VideoWebPlayer/Services/IMediaDurationProbe.cs` — Rückgabetyp mit Fehlerklassifikation (`MediaDurationProbeResult`).
- `VideoWebPlayer/Services/FfprobeMediaDurationProbe.cs` — Stream-Timeout, Fehlerklassifikation, ggf. stderr für Diagnose.
- `VideoWebPlayer/Services/MediaDurationProbeService.cs` — Options injizieren, Auswahl-Query (Retry-Gate), Versuchszähler, Parallelität, `IProgramScheduleSignal`.
- `VideoWebPlayer/Services/Backups/VideoWebPlayerBackupData.cs` — `OptionalRestoreColumns` + `OptionalRestoreIntDefaults` für `MediaItems.DurationProbeAttempts`.
- Tests: `MediaDurationProbeServiceTests`, `FfprobeMediaDurationProbeTests`, `MediaDurationProbeWorkerTests`, `VideoWebPlayerBackupDataTests` (Alt-Backup-Regression).
- Doku: `docs/help/videolaufzeit.md`, ggf. `docs/help/tv-sender.md`, `README.md`, `docs/RELEASE_NOTES.md`.

## Implementierungsansatz

- `IMediaDurationProbe` liefert statt `TimeSpan?` einen Ergebnistyp `MediaDurationProbeResult(TimeSpan? Duration, bool Transient)` mit Fabrikmethoden `Success/TransientFailure/PermanentFailure`; einziges Implementierungsziel ist `FfprobeMediaDurationProbe`, Test-Fakes werden angepasst.
- Klassifikation in `FfprobeMediaDurationProbe`: Timeout, Prozessstart-Fehler (`ExitCode == -1`), unerwartete Ausnahme und Exit-Code != 0 (z. B. abgebrochener Pipe-Stream) → transient; Exit-Code 0 mit nicht interpretierbarer/`N/A`-Ausgabe → permanent.
- `MediaDurationProbeService`: selektiert `Duration == null && (DurationProbedAt == null || (DurationProbeAttempts < MaxAttempts && DurationProbedAt <= jetzt - RetryDelay))`; Probes laufen parallel (`SemaphoreSlim`, Grad aus `ParallelProbes`) ohne DbContext-Zugriff; Ergebnisse werden seriell auf die Entitäten angewendet und pro Item gespeichert.
- `IProgramScheduleSignal` wird optional injiziert und nach einem Batch mit mindestens einer neu geschriebenen `Duration` einmal ausgelöst.

## Konfiguration

Anwendungseinstellungen im Abschnitt `MediaProbe` der `appsettings.json` (bisheriges Muster):

| Schlüssel | Default | Bedeutung |
|-----------|---------|-----------|
| `StreamTimeoutSeconds` | 600 | Zeitlimit für Probes über `pipe:0` (gestreamte Quellen) |
| `RetryDelaySeconds` | 300 | Mindestabstand zwischen zwei Probe-Versuchen eines Items |
| `MaxAttempts` | 10 | Maximale Versuche, bevor ein Item dauerhaft als fehlgeschlagen gilt |
| `ParallelProbes` | 4 | Maximale gleichzeitige ffprobe-Prozesse je Batch |

## Akzeptanzkriterien

1. Ein `pipe:0`-Probe verwendet `StreamTimeoutSeconds`, ein Dateipfad-Probe `TimeoutSeconds` (nachweisbar über die injizierte Invoke-Seam).
2. Timeout/IO-Fehler erhöhen `DurationProbeAttempts` und lassen das Item nach `RetryDelaySeconds` erneut zu; Erfolg setzt `Duration`.
3. Ein permanent als fehlgeschlagen markiertes Bestandsitem (`DurationProbedAt` gesetzt, `Duration` NULL, `DurationProbeAttempts` < `MaxAttempts`) wird wieder geprüft.
4. Items mit `DurationProbeAttempts >= MaxAttempts` bzw. permanentem Ergebnis (Exit 0 ohne parsebare Dauer) werden nicht erneut geprüft.
5. `ParallelProbes` > 1 führt mehrere Probes gleichzeitig aus; `ParallelProbes` == 1 bleibt seriell.
6. Nach dem Schreiben mindestens einer `Duration` wird `IProgramScheduleSignal.NotifyScheduleChanged()` ausgelöst.
7. Alt-Backup ohne `DurationProbeAttempts` stellt wieder her (Default 0 → Item wird erneut geprüft).
8. `dotnet build` Debug + Release fehlerfrei; volle Testsuite grün.

## Offene Fragen

Keine — die Anforderung lässt das Design im Ermessen, die oben dokumentierten Entscheidungen sind getroffen.
