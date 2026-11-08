# Enums — Bestandsaufnahme „TV-Sender: On-Air" (Issue #348)

## `LiveStreamSyncAction`

Datei: `VideoWebPlayer/Services/LiveStreamCoordinator.cs` (Zeilen 9–24) — Ergebnis-Aktion
eines `LiveStreamCoordinator.SynchronizeAsync`-Takts; wird im `LiveStreamWorker` für
Log-Deduplizierung (`_lastPersistentResult`) und Wake-Timing ausgewertet.

| Wert | Bedeutung |
|------|-----------|
| `Unchanged` | Laufende Segmentierung passt zum Plan — nichts geändert. |
| `Started` | Segmentierungsjob für die aktuelle Sendung (neu) gestartet. |
| `Stopped` | Segmentierung gestoppt (Sendelücke oder nicht auflösbarer Eingang). |
| `FfmpegUnavailable` | ffmpeg nicht auffindbar — keine Segmentierung möglich. |
| `MediaUnavailable` | Medium des laufenden Sendeplatzes nicht auflösbar. |
| `InputUnplayable` | Eingang für ffmpeg nicht lesbar — Sendeplatz wird bis zum Slotende übersprungen. |

Kein On-Air-bezogener Wert vorhanden; ein Off-Air-Stopp würde aktuell unter `Stopped`
einsortiert (die Entscheidung läge aber im `LiveStreamWorker` vor dem Coordinator-Aufruf,
nicht im Coordinator selbst).

## Weitere relevante Enums des Senders *(Kontext, unbetroffen)*

- `VideoWebPlayer/Data/ProgramScheduleContentScope.cs` — `SharedOnly`, `AllContent`
  (Inhaltsspektrum; `Setup.ProgramScheduleContentScope`).
- `VideoWebPlayer/Data/ProgramScheduleUnknownRatingMode.cs` — `RestrictToNightWindow`,
  `Unrestricted` (`Setup.ProgramScheduleUnknownRatingMode`).
- `ProgramSlotStatus`, `ProgramRuleType`, `ProgramRuleRecurrence`,
  `ProgramRuleValidityMode`, `ProgramRuleHolidayAnchor`, `ProgramRuleMaterialType`
  (Sendeplan-Modell — laufen Off-Air unverändert weiter).
