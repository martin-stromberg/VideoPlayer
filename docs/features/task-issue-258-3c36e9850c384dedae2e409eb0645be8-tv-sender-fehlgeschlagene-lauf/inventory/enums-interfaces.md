# Inventar: Enums und Interfaces

## Records / DTOs

### `MediaDurationProbeProgress` (`VideoWebPlayer/Services/MediaDurationProbeProgress.cs`)

`public sealed record` — Aggregat für die Admin-Ansicht:

| Member | Typ |
|---|---|
| `TotalCount` | `int` |
| `ProbedCount` | `int` |
| `PendingCount` | `int` |
| `FailedCount` | `int` |
| `LastActivityAt` | `DateTime?` |
| `ProbedLastHour` | `int` |
| `RecentItems` | `IReadOnlyList<MediaDurationProbeRecentItem>` |
| `PendingItems` | `IReadOnlyList<MediaDurationProbeDetailItem>` |
| `FailedItems` | `IReadOnlyList<MediaDurationProbeDetailItem>` |

### `MediaDurationProbeRecentItem` (selbe Datei)

`public sealed record(string Name, string SourceName, DateTime ProbedAt, TimeSpan? Duration, int Attempts)` —
keine Item-ID.

### `MediaDurationProbeDetailItem` (selbe Datei)

`public sealed record(string Name, string SourceName, DateTime? LastAttemptAt, int Attempts)` —
**trägt heute keine `MediaItem.Id`**; `Name` ist nicht eindeutig (relevant für den
geplanten Einzel-Reset).

### `MediaDurationProbeResult` (`VideoWebPlayer/Services/MediaDurationProbeResult.cs`)

`public sealed record(TimeSpan? Duration, bool Transient)` mit `UriUnsupported { get; init; }`
und den statischen Werten `TransientFailure`, `PermanentFailure`, `UriUnsupportedFailure`
sowie `Success(TimeSpan)`.

### `ProgramScheduleResetResult` (`VideoWebPlayer/Services/IProgramScheduleService.cs:79`)

`public readonly record struct(int SlotsDeleted, int CursorsDeleted)` — Vorbild für
Rückgabewerte von Admin-Aktionen.

## Enums

| Enum | Ort | Werte |
|---|---|---|
| `MediaSourceType` | `VideoWebPlayer/Data/MediaSourceType.cs` | `Sftp = 0`, `LocalDirectory = 1` |
| `ProbeOutcomeKind` | `private` in `MediaDurationProbeService.cs:263` | `Success`, `TransientFailure`, `PermanentFailure` |
| `ProgramSlotStatus` | `VideoWebPlayer/Data/` | `Scheduled`, `Displaced` (in `TVSenderAdmin` für Slot-Darstellung genutzt) |

## Interfaces

| Interface | Ort | Contract |
|---|---|---|
| `IMediaDurationProbe` | `VideoWebPlayer/Services/IMediaDurationProbe.cs` | `ProbeFileAsync(string, CancellationToken)`, `ProbeUriAsync(string, CancellationToken)`, `ProbeStreamAsync(Stream, CancellationToken)` — alle `Task<MediaDurationProbeResult>` |
| `IFfprobeLocator` | `VideoWebPlayer/Services/IFfprobeLocator.cs` | `string? Resolve()` |
| `IInternalMediaStreamTokenService` | `VideoWebPlayer/Services/IInternalMediaStreamTokenService.cs` | `TryCreateProbeUri(long mediaItemId)`, `TryCreateStreamUri(long, TimeSpan)`, `ResolveMediaItemId(string)` |
| `IProgramScheduleSignal` | `VideoWebPlayer/Services/ProgramScheduleSignal.cs` | `NotifyScheduleChanged()`, `BeginScan()`, `WaitAsync(CancellationToken)`; Singleton `ProgramScheduleSignal` |
| `IProgramScheduleService` | `VideoWebPlayer/Services/IProgramScheduleService.cs` | u. a. `DeleteDisplacedSlotsAsync`, `ResetScheduleAsync`, `GetRuleDiagnosesAsync` (Admin-Muster-Vorbild) |
| `IMediaSourceReader` | `VideoWebPlayer/Services/` | `ReadRootDirectory`, `ReadDirectoryEntries`, `FileExistsAsync`, `ReadFileAsync`, `ReadFileStreamAsync`, `OpenFileStream` (nur indirekt relevant über `ProbeRemoteItemAsync`) |

**Hinweis:** `MediaDurationProbeService` besitzt **kein Interface** und wird direkt als
konkrete Klasse registriert/injiziert.

## Exceptions

- `ForbiddenAccessException` (`VideoWebPlayer/Controllers/Exceptions/ForbiddenAccessException.cs`):
  `internal class` im **globalen Namespace** (keine Namespace-Deklaration); Meldungstexte
  deutsch; in `RequireAdminAsync` für Nicht-Admins.
- `UnauthorizedAccessException` (BCL): für unbekannte/ fehlende Benutzer.

## Hilfstypen

- `VideoFileExtensions` (`VideoWebPlayer/Utils/VideoFileExtensions.cs`):
  `All` = `{ ".mp4", ".avi", ".mkv", ".mpeg" }`; `IsVideoFileName(string?)`.
