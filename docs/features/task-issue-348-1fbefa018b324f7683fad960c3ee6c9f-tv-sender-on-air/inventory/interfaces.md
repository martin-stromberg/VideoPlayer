# Interfaces — Bestandsaufnahme „TV-Sender: On-Air" (Issue #348)

## `ILiveStreamSegmenter`

Datei: `VideoWebPlayer/Services/ILiveStreamSegmenter.cs` — Singleton-Contract des einen
kontinuierlich laufenden HLS-Segmentierungsprozesses (Implementierung
`FfmpegLiveStreamSegmenter`; Test-Fake `FakeSegmenter` in `LiveStreamWorkerTests`).

| Member | Parameter | Rückgabewert | Zweck |
|--------|-----------|--------------|-------|
| `IsRunning` | — | `bool` | Läuft gerade ein Segmentierungsprozess? — vom `LiveStreamWorker` vor `StopAsync` geprüft (idempotenter Stopp). |
| `CurrentSlotId` | — | `long?` | Slot-Id des laufenden Jobs, `null` bei Leerlauf. |
| `ExpectedPositionSeconds` | — | `double?` | Erwartete Wiedergabeposition des laufenden Prozesses (Drift-Prüfung). |
| `LastUnexpectedExit` | — | `LiveStreamExitInfo?` | Diagnose des letzten frühzeitigen Selbstendes (Eingangskaskade). |
| `SftpInputSupported` | — | `bool` | Latch: `sftp://`-Fähigkeit des ffmpeg-Builds. |
| `StartAsync` | `LiveStreamJob`, `CancellationToken` | `Task` | Job starten; ein laufender wird zuerst gestoppt. |
| `StopAsync` | `CancellationToken` | `Task` | Laufenden Prozess stoppen (10-s-Timeout, danach Kill). |

## `IProgramScheduleSignal`

Datei: `VideoWebPlayer/Services/ProgramScheduleSignal.cs` — Wake-up-Leitung zwischen
planrelevanten Änderungen und den Workern (Implementierung `ProgramScheduleSignal`, Singleton).

| Methode | Parameter | Rückgabewert | Zweck |
|---------|-----------|--------------|-------|
| `NotifyScheduleChanged` | — | `void` | Planeingriffe geändert; gebatcht über `ScheduleChangedDebounceSeconds`, unterdrückt während `BeginScan`. Weckt beide Worker (`WakeWorkers`). Wird u. a. von `ProgramSettings.razor` nach dem Speichern der TV-Sender-Freischaltung aufgerufen — das Muster für das sofortige Wirken des On-Air-Schalters. |
| `NotifyScheduleChangedNow` | — | `void` | Ungebatchtes Aufwecken (manuell ausgelöst, z. B. „Jetzt generieren"). |
| `BeginScan` | — | `IDisposable` | Scan-Scope: unterdrückt Benachrichtigungen, weckt beim Dispose einmal (nur bei unterdrückten Meldungen). |
| `NotifyLiveScheduleChanged` | — | `void` | Materialisierte Planänderung → schreibt nur den Live-Channel. |
| `WaitAsync` | `CancellationToken` | `ValueTask` | Warten auf Sendeplan-Signal (`ProgramScheduleWorker`). |
| `WaitLiveAsync` | `CancellationToken` | `ValueTask` | Warten auf Live-Signal (`LiveStreamWorker`). |

## `IProgramGuideService`

Datei: `VideoWebPlayer/Services/IProgramGuideService.cs` — zeitbasierte Programmauskunft
(Implementierung `ProgramGuideService`, scoped).

| Methode | Parameter | Rückgabewert | Zweck |
|---------|-----------|--------------|-------|
| `GetNowAsync` | `DateTimeOffset utcNow`, `CancellationToken` | `Task<DtoProgramGuideNow>` | Jetzt/Gleich-Antwort (`Now` = laufender Slot inkl. Position/Restlaufzeit oder `null` in der Sendelücke, `Next`). Vom `LiveStreamCoordinator` je Takt aufgerufen. |
| `GetUpcomingAsync` | `DateTimeOffset utcNow`, `TimeSpan preview`, `CancellationToken` | `Task<DtoProgramGuideEntry[]>` | Begrenzte Vorschau (max. `MaxPreviewHours`). |

## `IProgramGuideApiClient`

Datei: `VideoWebPlayer.Client/IProgramGuideApiClient.cs` — Blazor-seitige Client-Schnittstelle
(Implementierung `VideoWebPlayerClient`, scoped via `ServiceCollectionExtensions.cs:234`).

| Methode | Parameter | Rückgabewert | Zweck |
|---------|-----------|--------------|-------|
| `RequestProgramGuideNowAsync` | `CancellationToken` | `Task<DtoProgramGuideNow>` | Jetzt/Gleich für `/live-tv` (unbetroffen vom On-Air-Schalter). |
| `RequestProgramGuideUpcomingAsync` | `int hours`, `CancellationToken` | `Task<DtoProgramGuideEntry[]>` | Vorschau für `/live-tv`. |

## `IInternalMediaStreamTokenService` *(Kontext)*

Datei: `VideoWebPlayer/Services/` (Interface in
`InternalMediaStreamTokenService.cs`) — erzeugt/resolved kurzlebige Tokens für
`/api/internal/media-stream/*` (`TryCreateProbeUri`, `TryCreateStreamUri`,
`ResolveMediaItemId`). Bleibt laut Anforderung unberührt.

## `IBackgroundProcessingGate` *(Kontext)*

Datei: `VideoWebPlayer/Services/Backups/IBackgroundProcessingGate.cs` —
`EnterOperationAsync(name, ct)` liefert ein Lease, das Hintergrundoperationen während einer
Backup-Wiederherstellung anhält. `LiveStreamWorker`, `ProgramScheduleWorker` und
`MediaDurationProbeWorker` betreten es je Takt — ein Off-Air-Stopp läge wie der
Feature-Flag-Stopp **außerhalb** des Leases (das Gate hält nur den Arbeitstakt, nicht den
Schalter).
