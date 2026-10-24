# Bestandsaufnahme: Enums

## `LiveStreamSyncAction`

**Datei:** `VideoWebPlayer/Services/LiveStreamCoordinator.cs:9-21` (gleiche Datei wie `LiveStreamCoordinator`)

| Wert | Bedeutung |
|---|---|
| `Unchanged` | Laufende Segmentierung passt zum Sendeplan |
| `Started` | Segmentierungs-Job wurde (neu) gestartet |
| `Stopped` | Segmentierung gestoppt (Sendelücke oder Medium nicht auflösbar) |
| `FfmpegUnavailable` | **ffmpeg nicht auflösbar — keine Segmentierung möglich** (zentrale „fehlt"-Meldung; erreicht heute nur das Log via `LiveStreamWorker.LogFfmpegUnavailableThrottled`) |
| `MediaUnavailable` | Medium des laufenden Sendeplatzes nicht auflösbar |

## `ManualBackupJobStatus` (Muster für einen Download-Job-Status)

**Datei:** `VideoWebPlayer/Services/Backups/ManualBackupJobService.cs:112-138`

| Wert | Bedeutung |
|---|---|
| `Idle` | Kein Job bekannt |
| `Queued` | Angenommen, wartet auf Ausführung |
| `Running` | Läuft |
| `Succeeded` | Erfolgreich abgeschlossen |
| `Failed` | Fehlgeschlagen |

Wertemenge und Reihenfolge sind per `EnumContractTests.ManualBackupJobStatus_HasTheDocumentedStatesInOrder` (`VideoWebPlayer.Tests/EnumContractTests.cs:38-46`) als Vertrag gepinnt; `RestoreBackupJobStatus` spiegelt sie.

## `ProbeOutcomeKind` (intern)

**Datei:** `VideoWebPlayer/Services/MediaDurationProbeService.cs:263-268` — `Success`, `TransientFailure`, `PermanentFailure` (private, nur interne Ergebnisklassifikation).

## `ProgramRuleType` / `ProgramRuleRecurrence` / `ProgramRuleMaterialType` / `ProgramSlotStatus` (Kontext der TVSenderAdmin-Seite)

**Dateien:** `VideoWebPlayer/Data/ProgramRuleType.cs`, `ProgramRuleRecurrence.cs`, `ProgramRuleMaterialType.cs`, `ProgramSlotStatus.cs` — nicht direkt betroffen; relevant ist nur `ProgramSlotStatus.Scheduled`/`Displaced` für die Sendeplan-Anzeige.

## `AutoUpdateState` (extern, `msTools.Updater`)

Nicht im Repo definiert; verwendet in `UpdateAdminService.cs` und `Updates.razor`: u. a. `Checking`, `Downloading`, `Installing`, `UpdateAvailable`, `ReadyToInstall`, `InstallationFailed`. Für den eigenen Download-Job existiert kein entsprechendes Enum.
