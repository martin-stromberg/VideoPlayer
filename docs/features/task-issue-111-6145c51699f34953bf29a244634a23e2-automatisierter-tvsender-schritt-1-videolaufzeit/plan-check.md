# Plan-Gegenprüfung

## Ergebnis

**Status:** Plan vollständig

## Abgleich Akzeptanzkriterien

| Akzeptanzkriterium | Umsetzung im Plan | Testnachweis im Plan | Status |
|--------------------|-------------------|----------------------|--------|
| AC1: Laufzeit-Feld je abspielbarem Video | `MediaItem.Duration` (`TimeSpan?`) + `MediaItem.DurationProbedAt` + EF-Migration `AddMediaItemDuration`; `MediaItem` ist die Datei-Ebene, `Movie`/`TVShowEpisode` erreichen sie über `MovieMediaItem`/`TVShowEpisodeMediaItem` | `ProbePendingAsync_ProbesPendingVideoItems_WritesDuration`, Migration im Build | Abgedeckt |
| AC2: Probe beim regulären Scan für neue Inhalte | Neue `MediaItem`s entstehen mit `DurationProbedAt == null` und werden vom `MediaDurationProbeWorker` im Intervall aufgegriffen | `ExecuteAsync_ProcessesPendingItems`, `ProbePendingAsync_ProbesPendingVideoItems_WritesDuration` | Abgedeckt |
| AC3: Automatisierte Nachtragsbefüllung des Bestands | Dieselbe Pending-Abfrage (`DurationProbedAt == null`) erfasst Altbestand; Worker entleert den Rückstand schrittweise (Startup-Catch-up + Batch-Loop) | `ProbePendingAsync_RespectsBatchSize`, `ExecuteAsync_ProcessesPendingItems` | Abgedeckt |
| AC4: Fehlerkennzeichnung („als solche erkennbar", nicht endlos wiederholt) | `DurationProbedAt` gesetzt + `Duration == null` = fehlgeschlagen; Pending-Abfrage greift nur `DurationProbedAt == null` → kein Retry | `ProbePendingAsync_FailedProbe_MarksProbedWithoutDuration`, `ProbePendingAsync_SkipsAlreadyProbedItems` | Abgedeckt |
| AC5: Keine Störung von Scan/Betrieb | Probe entkoppelt in eigenem Worker; Exception je Item wird gefangen und als Fehlprobe markiert; fehlendes Binary → Worker wartet, Items bleiben ungeprüft | `ProbePendingAsync_ProbeThrows_MarksFailedAndContinues`, `ProbePendingAsync_NoFfprobe_LeavesItemsUnprobed` | Abgedeckt |
| AC6: Dateiwechsel → Neuermittlung | `MediaSourceScanner` setzt bei geändertem `CreatedAt` `Duration`/`DurationProbedAt` zurück | `ScanNextMediaCollection_ChangedFile_ResetsDuration` | Abgedeckt |
| AC7: Migration + Backup-Registrierung + Alt-Backup-Regression | Migration `AddMediaItemDuration`; `OptionalRestoreColumns` um `MediaItems.Duration`/`DurationProbedAt` (kein Default → `NULL` = ungeprüft) | `ReadFromAsync_LegacyBackupWithoutDurationColumns_RestoresSuccessfully` | Abgedeckt |
| AC8: Binary-Bereitstellung + Lizenz + Build/CI | `FfprobeLocator` (Config → `tools/ffmpeg/` → PATH); Lizenz-/Bereitstellungsfestlegung in `inventory/ffmpeg.md` dokumentiert; CI braucht kein Binary | `FfprobeLocatorTests` (Auflösungsreihenfolge, NotFound) | Abgedeckt |
| Nicht-Anforderung: NFO als Quelle | NFO wird nicht gelesen — Quelle ist ausschließlich ffprobe auf der Videodatei | implizit durch alle Probe-Tests (kein NFO-Pfad im Design) | Abgedeckt |

## Fehlende oder unvollständige Testanforderungen

– (Status `Plan vollständig`)

## E2E-Abdeckung

| Benutzerfluss / Akzeptanzkriterium | Geplanter E2E-Test | Status |
|------------------------------------|--------------------|--------|
| — (kein Benutzerfluss: rein automatische Hintergrundfunktion, keine UI, keine Endpunkte, keine anwendersichtbare Interaktion; Erkennbarkeit ist ein DB-Zustand für Schritt 3) | keiner | Nicht erforderlich mit Begründung |

## Fehlende oder unvollständige Planbestandteile

– (Status `Plan vollständig`)

## Hinweise

- Der Plan trifft die fachlich relevante Modellentscheidung „Laufzeit auf `MediaItem` statt `Movie`/`TVShowEpisode`" begründet (1:n-Verknüpfung, Sendeplatz = konkrete Datei) und dokumentiert sie in den Designentscheidungen.
- `ReadFileStreamAsync` vs. `OpenFileStream` für SFTP ist korrekt unterschieden (Speicherlast vs. Streaming).
- ffprobe 9.0.2 liegt in der lokalen Entwicklungsumgebung auf `PATH` vor — ein manueller Smoke-Nachweis ist damit möglich, Tests bleiben binary-unabhängig.
