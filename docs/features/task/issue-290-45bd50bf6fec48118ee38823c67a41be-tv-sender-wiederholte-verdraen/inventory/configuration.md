# Konfiguration — Bestandsaufnahme (Issue #290)

Keine Issue-spezifische Konfiguration existiert; das Verhalten hängt an den folgenden bestehenden Schaltern.

## `ProgramScheduleOptions` (Sektion `ProgramSchedule`)
Datei: `VideoWebPlayer/Configuration/ProgramScheduleOptions.cs` — gebunden in `ProgramScheduleService` (Ctor Z. 60–61, 68–71)

| Eigenschaft | Typ | Default | Zweck |
|-------------|-----|---------|-------|
| `TrailerLeadDays` / `EffectiveTrailerLead` | `int` / `TimeSpan` | 7 Tage | Trailer-Vorlauffenster (`FindLeadMatchOffset`, Clip-Trailer-Gruppe) |
| `MinStartRemainSeconds` / `EffectiveMinStartRemain` | `int` / `TimeSpan` | 30 s | Mindest-Restzeit vor Fenstergrenze, ab der ein Titel noch starten darf (`SelectAiringItems`) |
| `MaxOccurrenceLookbackDays` / `EffectiveMaxOccurrenceLookback` | `int` / `TimeSpan` | 7 Tage, geklemmt auf 30 | Maximale Verbreiterung des `ReconcileAsync`-Lade-Fensters hinter `coverageStart` (Lang-Ausstrahlungen); darüber hinaus wird verdrängt |

Zusätzlich fest verdrahtet in `ProgramScheduleService`: `OccurrenceLookback = 24 h` (Z. 26, Basis-Lade-Fenster und Occurrence-Lookback) und `DiagnosisOccurrenceHorizonDays = 9` (Z. 33, nur Diagnose).

## `MediaProbeOptions` (Sektion `MediaProbe`)
Datei: `VideoWebPlayer/Configuration/MediaProbeOptions.cs`

| Eigenschaft | Default | Zweck |
|-------------|---------|-------|
| `IntervalSeconds` / `EffectiveIntervalSeconds` | 60 s | Takt des `MediaDurationProbeWorker` (Z. 92) → indirekt Frequenz der Probe-Signale |
| `BatchSize` / `EffectiveBatchSize` | 10 | Items je `ProbePendingAsync`-Aufruf → Anzahl Signalfeuer bei Backlog |
| `RetryDelaySeconds` / `EffectiveRetryDelaySeconds` | 300 s | Wartezeit bis zur Wiederholung transienter Fehler |
| `MaxAttempts` / `EffectiveMaxAttempts` | 10 | Versuchsgrenze → `ProbeExhausted`; in `ProgramScheduleService` nur für die Diagnose genutzt |
| `TimeoutSeconds` / `StreamTimeoutSeconds` | 120 / 600 s | ffprobe-Timeouts (lokal/Stream) |
| `ParallelProbes` | 4 | Parallele Proben je Batch |
| `HttpProbeEnabled` / `HttpProbeBaseUrl` / `HttpProbeTokenLifetimeSeconds` | `true` / `null` / 600 s | Loopback-HTTP-Proben |

## `Setup`-Schlüssel (DB-persistiert, via `ProgramSettingsService`)
Datei: `VideoWebPlayer/Data/Setup.cs`

| Schlüssel | Default/Fallback | Bezug |
|-----------|------------------|-------|
| `TvSenderEnabled` | `false` | Feature-Flag, je Tick geprüft |
| `ProgramScheduleHorizonHours` | 48 | Planungshorizont beider Läufe |
| `ProgramScheduleIntervalMinutes` | 15 | Takt des `ProgramScheduleWorker` |
| `ProgramScheduleContentScope` | `SharedOnly` | Zuschauer-Freigabeprüfung der Expansion |
| `ProgramScheduleAgeWindow{12,16,18}Local`, `…NightEndLocal` | 20:00/22:00/23:00/06:00 | Jugendschutzfenster — zeitabhängige Eingabe der Auswahl und der Reproduzierbarkeitsprüfung |
| `ProgramScheduleUnknownRatingMode` | `RestrictToNightWindow` | Freigabeloses Material nur nachts |
| `HolidayFederalState` | `null` | Bundesland der `Holiday`-Regeln (`BuildOccurrences`) |

## Zeitzone
`TimeZoneInfo.Local` wird in `BuildOccurrences` (`ProgramScheduleService.cs:1589`), `ProgramScheduleAgeWindow.IsAllowedAt` (Z. 55) und `GetRuleDiagnosesAsync` (Z. 448–451) verwendet — die Regel-Sendezeiten sind lokal, die Slot-Zeiten UTC.
