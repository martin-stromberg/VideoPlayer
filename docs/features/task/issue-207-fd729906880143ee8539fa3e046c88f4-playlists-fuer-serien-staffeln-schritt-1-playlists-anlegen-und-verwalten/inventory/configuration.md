# Konfiguration und Einstellungen

## `PlaylistSettings`
Datei: `VideoWebPlayer/Configuration/PlaylistSettings.cs`

Stark typisierte Konfigurationsklasse für Playlist-Feature.

| Eigenschaft | Typ | Beschreibung |
|-------------|-----|-------------|
| `MaxPlaylistsPerUser` | `int?` | Maximale Anzahl Playlists pro Benutzer (null = unbegrenzt) |

**Verwendung:**
- Wird in `PlaylistService` über Dependency Injection eingebunden
- `PlaylistService.CreatePlaylistAsync()` prüft Limit vor Erstellung
- Wirft `InvalidOperationException` "Die maximale Anzahl an Playlists wurde erreicht." wenn Limit überschritten

**Konfiguration:**
Die Einstellung wird typischerweise in `appsettings.json` oder Umgebungsvariablen konfiguriert:

```json
{
  "PlaylistSettings": {
    "MaxPlaylistsPerUser": 50
  }
}
```

Oder null/nicht gesetzt für unbegrenzt.
