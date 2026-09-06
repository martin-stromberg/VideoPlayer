# Tasks: Nachbesserung Entwicklungsschritt 3 – Playlist-Inhalte mit Titelbild, Freischaltung und korrekter Button-Logik

| # | Bereich | Aufgabe | Status | Testnachweis |
|---|---------|---------|--------|--------------|
| 1 | Datenmodell | `DtoPlaylistEntry.cs`: Feld `PosterPictureId` (`long?`) hinzufügen | Offen | — |
| 2 | Datenmodell | `DtoPlaylistEntry.cs`: Kommentar bei `IsAccessible` aktualisieren oder entfernen | Offen | — |
| 3 | Service | `PlaylistService.ctor`: `IUnlockedMediaService _unlockedMediaService` als Parameter hinzufügen | Offen | — |
| 4 | Service | `PlaylistService.ctor`: `IAuthService _authService` als Parameter hinzufügen | Offen | — |
| 5 | Service | Hilfsmethode `PlaylistService.LoadPosterPictureIdAsync()` erstellen zum Laden von Bild-IDs mit Fallback (Poster→Banner→Fanart) | Offen | — |
| 6 | Service | Private Methode `PlaylistService.ToDtoAsync()` erstellen (async Variante von `ToDto()`) mit Freischaltungsprüfung via `IUnlockedMediaService.IsUnlockedAsync()` | Offen | — |
| 7 | Service | `PlaylistService.GetPlaylistEntriesPagedAsync()`: Anpassung um `ToDtoAsync()` statt `ToDto()` zu nutzen | Offen | — |
| 8 | Service | `PlaylistService.GetPlaylistEntriesAsync()`: TODO-Kommentar hinzufügen für zukünftige Async-Refaktorierung | Offen | — |
| 9 | UI | `PlaylistDetail.razor`: Neue Spalte für Titelbild in Thead hinzufügen | Offen | — |
| 10 | UI | `PlaylistDetail.razor`: Bildladungs-Logik in Tbody implementieren (Fallback: PosterPictureId→BannerPictureId→FanartPictureId→Placeholder) | Offen | — |
| 11 | UI | `PlaylistDetail.razor`: `onerror`-Fallback auf Placeholder-Bild hinzufügen | Offen | — |
| 12 | UI | `PlaylistDetail.razor` Zeile 109: `disabled="@(!entry.IsAccessible)"` Binding vom "Entfernen"-Button entfernen | Offen | — |
| 13 | UI | `PlaylistDetail.razor`: Verifizierung, dass `.opacity-50` CSS-Klasse auf `<tr>` für nicht freigeschaltete Einträge erhalten bleibt | Offen | — |
| 14 | Tests | E2E-Test `PlaylistDetail_DoesNotShowReducedOpacity_WhenAllEntriesAccessible()`: Überprüfung und ggf. Anpassung | Offen | — |
| 15 | Tests | E2E-Test `PlaylistDetail_ShowsReducedOpacity_WhenEntryNotAccessible()` neu erstellen | Offen | — |
| 16 | Tests | E2E-Test `PlaylistDetail_RemoveButton_EnabledForAllEntries()` neu erstellen | Offen | — |
| 17 | Tests | E2E-Test `PlaylistDetail_DisplaysImageForEachEntry()` neu erstellen | Offen | — |
| 18 | Tests | Test-Hilfsmethode `PlaylistsE2ETestBase.RemoveUnlockedMediaEntryAsync()` erstellen | Offen | — |
| 19 | Dokumentation | Code-Kommentare für komplexe Logik hinzufügen (async-Refaktorierung, Fallback-Bildladung) | Offen | — |
