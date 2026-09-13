# Datenmodelle

## `DtoPlaylistNavigationResult`
Datei: `VideoWebPlayer.Client/Models/DtoPlaylistNavigationResult.cs`

| Eigenschaft | Typ | Beschreibung |
|-------------|-----|-------------|
| Entry | DtoPlaylistEntry | Der durch Navigation aufgelöste Eintrag |
| Position | int | 1-basierte Position des Eintrags in der aktuellen Sortierreihenfolge der Playlist |

**Status:**
- Feld `Direction` (optional): **NICHT VORHANDEN** – wird für die Implementierung benötigt, um zwischen Forward/Backward-Navigation zu unterscheiden
- Beide bestehenden Felder sind vorhanden und korrekt dokumentiert

## `DtoPlaylistEntry`
Datei: `VideoWebPlayer.Client/Models/` (wird von PlaylistService verwendet)

**Relevant für diese Anforderung:**
- `IsAccessible` – Eigenschaft zur Prüfung der Benutzerzugriffsberechtigung
- `MediaType` – Enum-Wert (Movie, TVShowEpisode, TVShow, TVShowSeason, MovieCollection)

**Status:**
- `IsAccessible` ist vorhanden und wird in der UI-Rendering verwendet (PlaylistEntriesList.razor Zeile 53)
- Wird in `PlaylistService.BuildEntryDtosAsync` korrekt befüllt (Zeile 526)
