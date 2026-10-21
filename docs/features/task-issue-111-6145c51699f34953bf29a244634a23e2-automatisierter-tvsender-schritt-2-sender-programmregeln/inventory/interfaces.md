# Interfaces — Bestandsaufnahme

## `IUnlockedMediaService`
Datei: `VideoWebPlayer/Services/IUnlockedMediaService.cs` (Impl.: `UnlockedMediaService`, scoped)

| Methode | Parameter | Rückgabewert | Zweck |
|---------|-----------|--------------|-------|
| `IsUnlockedAsync` | `DtoMediaEntry`, ct | `Task<bool>` | Einzelfreigabe-Status für aktuellen Benutzer |
| `GetUnlockedUserIdsAsync` | `DtoMediaEntry`, ct | `Task<string[]>` | Freigeschaltete Benutzer |
| `SetUnlockedUsersAsync` | `DtoMediaEntry`, `string[]`, ct | `Task` | Freigabeliste ersetzen |
| `GetUnlockedMovieCollectionIdsForUserAsync` | `userId`, ct | `Task<long[]>` | freigeschaltete Filmsammlungen des Benutzers |
| `GetUnlockedTVShowIdsForUserAsync` | `userId`, ct | `Task<long[]>` | freigeschaltete Serien des Benutzers |
| `GetUnlockedSourceIdsForUserAsync` | `userId`, ct | `Task<long[]>` | Quellen mit Einzelfreigaben |
| `GetMediaSourceIdsForUserAsync` | `userId`, ct | `Task<long[]>` | regulärer Quellenzugriff (`MediaSourceUser`) |
| `IsAccessible` | `hasSourceAccess`, `isUnlocked` | `bool` | kanonische Zugänglichkeitsregel (Quellenzugriff ODER Einzelfreigabe) |

Relevanz: In Schritt 3 Grundlage für „nur vollständig freigegebene Inhalte"; in Schritt 2 indirekt über den `/api/items`-Endpunkt wirksam (sichtbarer Medienbestand).

## `IPlaylistService`
Datei: `VideoWebPlayer/Services/IPlaylistService.cs` (Impl.: `PlaylistService`, scoped) — **Namenskonflikt beachten**: bezeichnet die Benutzer-Playlists, nicht den Sender.

| Methode | Zweck |
|---------|-------|
| `GetPlaylistsAsync(userId, genreId?, ct)` | eigene Playlists des Benutzers |
| `GetPublicPlaylistsAsync(userId, genreId?, ct)` | öffentliche Playlists |
| `GetPlaylistAsync(id, userId, ct)` | einzelne Playlist (Eigentümer oder öffentlich) |
| `SetPlaylistPublicAsync(playlistId, userId, requesterIsAdmin, isPublic, ct)` | Muster: Admin-Flag wird als Parameter aus dem DB-Benutzerdatensatz übergeben |

Relevanz: Quelle der wählbaren „Benutzer-Playlists" als Programmmaterial sowie Muster der serverseitigen Admin-Erzwingung.

## `IAuthService`
Datei: `VideoWebPlayer/Services/Authentication/IAuthService.cs` (Impl.: `AuthService`, transient)

| Methode | Zweck |
|---------|-------|
| `CurrentUser` | `ApplicationUser?` des angemeldeten Benutzers (inkl. `IsAdmin`-Flag aus der Datenbank) |
| `LoginAsync` / `ImpersonateAsync` | Token-Erzeugung |

## `IPlaylistBackfillSignal`
Datei: `VideoWebPlayer/Services/PlaylistBackfillSignal.cs` — Signal-Mechanismus zum Aufwecken von Hintergrund-Workern; erst in Schritt 3/4 relevant (Regeländerung → Plan-Neubewertung), in Schritt 2 nicht benötigt.
