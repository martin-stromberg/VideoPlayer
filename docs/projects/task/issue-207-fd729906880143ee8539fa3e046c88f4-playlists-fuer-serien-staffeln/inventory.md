# Bestandsaufnahme: Playlists für Serien, Staffeln, Episoden, Filme und Filmsammlungen

Diese Bestandsaufnahme analysiert den bestehenden Projektcode bezogen auf die Anforderung von benutzergesteuerten Playlists mit automatischer Ergänzung, Sortierlogik, Weiterschauen-Integration und öffentliche Playlists.

## Zusammenfassung

- **Neue Entities nicht vorhanden:** `Playlist`, `PlaylistItem`, `PlaylistGenre` existieren noch nicht
- **Service-Interfaces nicht vorhanden:** Keine `IPlaylistService`, `IPlaylistGenreService`, `IPlaylistImageService`, etc.
- **Controller nicht vorhanden:** Kein `PlaylistController` oder API-Endpoints für Playlists
- **UI-Komponenten nicht vorhanden:** Keine Blazor-Komponenten für Playlist-Verwaltung
- **Erweiterungen bestehender Modelle nicht vorhanden:** `ContinueWatchingEntry.PlaylistId` und `ApplicationUser.Playlists` Navigation nicht vorhanden
- **Verwandte Entities vorhanden:** `ApplicationUser`, `ContinueWatchingEntry`, `Movie`, `TVShow`, `TVShowSeason`, `TVShowEpisode`, `MovieCollection`, `Genre`, `Picture` existieren bereits
- **Service- und Controller-Patterns vorhanden:** Projekt nutzt etablierte Muster für Services, Controller, Konfigurationen und Tests

## Details

- [Verwandte Datenmodelle](inventory/models.md)
- [Verwandte Services und Logik](inventory/logic.md)
- [Enums](inventory/enums.md)
- [Interfaces](inventory/interfaces.md)
- [Bestehende Tests](inventory/tests.md)
