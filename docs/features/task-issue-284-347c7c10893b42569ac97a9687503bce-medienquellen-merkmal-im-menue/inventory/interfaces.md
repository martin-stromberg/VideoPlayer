# Interfaces — Bestandsaufnahme

Für das Merkmal ist kein neues Interface erforderlich. Relevant sind die Contracts, die die Quellensichtbarkeit heute steuern.

## `IUnlockedMediaService`
Datei: `VideoWebPlayer/Services/IUnlockedMediaService.cs`

Freischaltungslogik für einzelne Medieneinträge — liefert `SourcesController` die zusätzlichen Quell-IDs. **Darf laut Anforderung nicht geändert werden.**

| Methode | Parameter | Rückgabewert | Zweck |
|---------|-----------|--------------|-------|
| `IsUnlockedAsync` | `DtoMediaEntry entry, CancellationToken` | `Task<bool>` | Einzel-Freischaltung des Eintrags für den aktuellen Benutzer? |
| `GetUnlockedUserIdsAsync` | `DtoMediaEntry entry, CancellationToken` | `Task<string[]>` | Benutzer-IDs mit Freischaltung auf den Eintrag |
| `SetUnlockedUsersAsync` | `DtoMediaEntry entry, string[] userIds, CancellationToken` | `Task` | Ersetzt die Freischaltliste eines Eintrags |
| `GetUnlockedMovieCollectionIdsForUserAsync` | `string userId, CancellationToken` | `Task<long[]>` | Explizit freigeschaltete Filmsammlungen |
| `GetUnlockedTVShowIdsForUserAsync` | `string userId, CancellationToken` | `Task<long[]>` | Explizit freigeschaltete Serien |
| `GetUnlockedSourceIdsForUserAsync` | `string userId, CancellationToken` | `Task<long[]>` | Quell-IDs mit mind. einem freigeschalteten Eintrag — wird in `SourcesController.GetSources`/`GetSource` mit `MediaSourceUsers` vereinigt |
| `GetMediaSourceIdsForUserAsync` | `string userId, CancellationToken` | `Task<long[]>` | Quell-IDs mit regulärer (`MediaSourceUsers`-)Freigabe |

Implementierung: `VideoWebPlayer/Services/UnlockedMediaService.cs` (u. a. `GetUnlockedSourceIdsForUserAsync` Zeile 122).

## `IProgramClipGroupService` (Kontext: Clip-Kandidaten)
Datei: `VideoWebPlayer/Services/IProgramClipGroupService.cs`

| Methode | Parameter | Rückgabewert | Zweck |
|---------|-----------|--------------|-------|
| `GetClipCandidatesAsync` | `string userId, string? search, long? mediaSourceId, int skip, int take, CancellationToken` | `Task<List<ProgramClipGroupCandidate>>` | Kandidatenliste für Clip-Gruppen; `mediaSourceId` filtert auf eine Quelle — versteckte Quellen müssen hier weiterhin auswählbar bleiben |

Implementierung: `VideoWebPlayer/Services/ProgramClipGroupService.cs` (Zeile 116).
