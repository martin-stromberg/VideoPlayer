# Enums und Konstanten

## `PlaylistSortMode`
Datei: `VideoWebPlayer/Data/PlaylistSortMode.cs`

Backend-Enum für die Sortiermodi von Playlists (wird in der Datenbank gespeichert).

| Wert | Bedeutung |
|------|-----------|
| `ByReleaseDate` | Automatische Sortierung nach Erscheinungsdatum der Inhalte (Standard) |
| `Manual` | Manuelle Sortierung durch den Anwender |

## `PlaylistSortModeValues`
Datei: `VideoWebPlayer.Client.Models/PlaylistSortModeValues.cs`

Client-seitige String-Konstanten zur Vermeidung von Duplikaten bei JSON-Serialisierung/Deserialisierung.

| Konstante | Wert | Bedeutung |
|-----------|------|-----------|
| `ByReleaseDate` | `"ByReleaseDate"` | Nach Erscheinungsdatum sortieren |
| `Manual` | `"Manual"` | Manuell sortieren |

**Verwendung:** 
- In Razor-Komponenten (`PlaylistForm.razor`, `PlaylistsList.razor`)
- In DTOs (`DtoPlaylist`, `DtoCreatePlaylistRequest`)
- Vermeidet Hardcoding von String-Literalen und zentrale Wartung
