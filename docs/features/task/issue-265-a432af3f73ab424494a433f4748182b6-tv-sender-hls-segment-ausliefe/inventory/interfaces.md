# Interfaces — Bestandsaufnahme

Keine Interface-Änderung ist Teil der Anforderung. Folgende Contracts liegen im betroffenen Bereich.

## `ILiveStreamSegmenter`

Datei: `VideoWebPlayer/Services/ILiveStreamSegmenter.cs`

| Methode / Eigenschaft | Parameter | Rückgabewert | Zweck |
|-----------------------|-----------|--------------|-------|
| `IsRunning` | – | `bool` | Läuft ein Segmentierungsprozess? |
| `CurrentSlotId` | – | `long?` | Sendeplatz-Id des laufenden Jobs |
| `ExpectedPositionSeconds` | – | `double?` | Erwartete Wiedergabeposition des laufenden Prozesses |
| `LastUnexpectedExit` | – | `LiveStreamExitInfo?` | Diagnose des letzten vorzeitigen Prozessendes (HTTP-Transport-Fallback) |
| `StartAsync` | `LiveStreamJob job, CancellationToken` | `Task` | Job starten; ein evtl. laufender Job wird vorher gestoppt |
| `StopAsync` | `CancellationToken` | `Task` | Laufenden Prozess stoppen |

Implementiert von `FfmpegLiveStreamSegmenter`; aufgerufen von `LiveStreamCoordinator` und `LiveStreamWorker`.

## `IAuthService`

Datei: `VideoWebPlayer/Services/Authentication/` (Vertragsdatei; Implementierung für Tests: `VideoWebPlayer.Tests/Helpers/FakeAuthService.cs`)

| Methode / Eigenschaft | Parameter | Rückgabewert | Zweck |
|-----------------------|-----------|--------------|-------|
| `CurrentUser` | – | `ApplicationUser?` | Aktuell angemeldeter Benutzer; Basis von `ApiBaseController.CurrentUser`/`CheckLoggedIn` |
| `LoginAsync` | `AuthenticationRequest` | `Task<AuthorizationToken>` | Anmeldung |
| `ImpersonateAsync` | `ImpersonateRequest` | `Task<AuthorizationToken>` | Identitätswechsel |

## `ISegmenterProcess` (internal)

Datei: `VideoWebPlayer/Services/FfmpegLiveStreamSegmenter.cs` (Z. 16–35)

| Methode / Eigenschaft | Parameter | Rückgabewert | Zweck |
|-----------------------|-----------|--------------|-------|
| `HasExited`, `ExitCode` | – | `bool` / `int` | Prozesszustand |
| `StandardInput`, `StandardError` | – | `Stream` | Pipe-Eingang bzw. zu drainender stderr |
| `WaitForExitAsync` | `CancellationToken` | `Task` | Auf Prozessende warten |
| `Kill` | – | `void` | Prozessbaum beenden |

Implementiert von `SegmenterProcess`; injizierbar für Tests des Segmenters.
