# Bestandsaufnahme: Enums — Live-Stream-Eingangskette

## `MediaSourceType`
Datei: `VideoWebPlayer/Data/MediaSourceType.cs`

| Wert | Bedeutung |
|------|-----------|
| `Sftp = 0` | Entfernter SFTP-Server (Default für Bestandsdaten) — Resolver-Pfad: HTTP oder `pipe:0`. |
| `LocalDirectory = 1` | Lokales Verzeichnis auf dem Server (inkl. UNC-Pfade) — Resolver-Pfad: `ForFile`. |

**Der einzige Enum-Wert, der die ffmpeg-Eingangsart fachlich bestimmt** (`LiveStreamMediaResolver.cs:110`).

## `LiveStreamSyncAction`
Datei: `VideoWebPlayer/Services/LiveStreamCoordinator.cs:9-21`

| Wert | Bedeutung |
|------|-----------|
| `Unchanged` | Laufende Segmentierung passt zum Sendeplan — nichts geändert. |
| `Started` | Segmentierungsjob für die aktuelle Sendung (neu) gestartet. |
| `Stopped` | Segmentierung gestoppt (Sendelücke oder nicht auflösbarer Eingang). |
| `FfmpegUnavailable` | ffmpeg nicht auffindbar — keine Segmentierung möglich. |
| `MediaUnavailable` | Medium des laufenden Sendeplatzes nicht zu einem Eingang auflösbar. |

## `SegmenterPumpOutcome`
Datei: `VideoWebPlayer/Services/SegmenterProcessSupport.cs:14-25` — `internal`

| Wert | Bedeutung |
|------|-----------|
| `Completed` | Quell-Stream bis zum Ende gepumpt (oder Pipe brach beim regulären Prozessende). **Zählt nicht die gelieferte Datenmenge — „vollständig" vs. „Quelle brach mittendrin ab" wird nicht unterschieden.** |
| `SourceUnavailable` | `OpenStreamAsync` lieferte `null`. |
| `Failed` | Unerwarteter Pump-Fehler (bereits geloggt). |

## `ProgramRuleType`
Datei: `VideoWebPlayer/Data/ProgramRuleType.cs`

| Wert | Bedeutung |
|------|-----------|
| `SeriesFixedTime = 0` | Serie zur festen Zeit. |
| `MoviePrimeTime = 1` | Film zur Primetime. |
| `GenreRotation = 2` | Rotation durch Genre-/Format-Inhalte. |
| `Repeat = 3` | Wiederholung. |
| `HolidaySpecial = 4` | Feiertags-Sondersendung. |
| `Filler = 5` | Füllmaterial für Planlücken — die fachliche „Platzhalter"-Kategorie auf Regel-Ebene. |

## `ProgramSlotStatus`
Datei: `VideoWebPlayer/Data/ProgramSlotStatus.cs`

| Wert | Bedeutung |
|------|-----------|
| `Scheduled` | Belegt Sendezeit. |
| `Displaced` | Durch Plan-Neubewertung verdrängt; nur Historie. |

Hinweis: `ProgramSlot.IsClipFill` (bool, `ProgramSlot.cs:88`) markiert Clip-Füll-Slots; **kein Enum und ohne Einfluss auf die Eingangs-Auswahl** — siehe `logic.md`/`models.md`.
