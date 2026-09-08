# Übersetzte Anforderung: Namenssuche für Playlist-Einträge

## Fachliche Zusammenfassung

Die aktuelle Implementierung zum Hinzufügen von Medieninhalten zu einer Playlist erfordert von Anwendern, dass sie die interne Datenbank-ID eines Titels kennen und manuell eingeben (`PlaylistEntriesList.razor`, Zeilen 22–30). Dies ist für Endanwender nicht praktikabel. Die Anforderung verlangt, diese Eingabe durch eine echte Namenssuche/Auswahl zu ersetzen, bei der Anwender einen Titel anhand seines Namens finden und durch Klick auswählen können, ohne die ID zu kennen. Die Such- und Auswahllogik muss alle fünf unterstützten Medientypen abdecken: `Movie`, `TVShow`, `TVShowSeason`, `TVShowEpisode`, `MovieCollection`. Die bestehende Hinzufügungslogik (Duplikat-Handling, Kaskaden-Auflösung, Zugriffsschutz) bleibt unverändert; nur der Eingabe-/Auswahlmechanismus ändert sich.

## Betroffene Klassen und Komponenten

**Bestehend (zu erweitern oder zu ersetzen):**
- `VideoWebPlayer/Components/Playlists/PlaylistEntriesList.razor` — Die Eingabe-UI wird von Dropdown + `<input type="number">` auf eine Such-/Auswahl-Komponente umgestellt
- `VideoWebPlayer/Controllers/ItemsController.cs`, Methode `Get(...)` — Derzeit durchsucht nur `MovieCollections` und `TVShows`; muss um `Movies`, `TVShowSeasons`, `TVShowEpisodes` erweitert werden
- `VideoWebPlayer.Client/IPlaylistApiClient.cs` — Ggf. neue Methode für Media-Suche, falls nicht über vorhandenen `ItemsController.Get` abgewickelt
- `VideoWebPlayer.Client/VideoWebPlayerClient.cs` — Partiale Implementierung von `IPlaylistApiClient`, ggf. neue Suchmethode

**Neu zu erstellen:**
- `VideoWebPlayer/Components/Playlists/MediaSearchSelector.razor` (oder ähnlich) — Die Such-/Auswahl-Komponente mit:
  - Textfeld (Eingabefeld mit `@bind:event="oninput"` für Live-Suche)
  - Serverseitige Suche bei jeder Eingabeänderung
  - Ergebnisliste basierend auf `MediaBox.razor` oder `MediaBaseEntryList.razor`
  - Auswahl-Callback statt Navigation (Vorbild: `OnActionSelected`-Mechanismus in `MediaBox.razor`)
  - Unterstützung aller 5 Medientypen in der Darstellung

**DTOs / Models (möglicherweise):**
- Ggf. neuer DTO für Suchergebnisse, falls nicht `MediaEntryDto` wiederverwendet werden kann. `MediaEntryDto` (bereits vorhanden in `VideoWebPlayer.Controllers.Models`) hat bereits die notwendigen Felder (`Type`, `Id`, `Title`, `PictureId`).

## Implementierungsansatz

1. **Backend-Erweiterung (`ItemsController.Get`)**:
   - Erweitere die Methode `Get(mediaSourceId?, page, size, search?, genreId?)` um `Movies`, `TVShowSeasons`, `TVShowEpisodes` neben den bereits unterstützten `MovieCollections` und `TVShows`
   - Stelle sicher, dass die Namenssuche (`search`-Parameter) auf alle fünf Typen angewendet wird
   - Verknüpfe den existierenden Zugriffskontrollmechanismus (`IUnlockedMediaService`) für alle Typen
   - Alternativ: Neuer, playlist-spezifischer Endpoint (`SearchMediaForPlaylistAsync` o. ä.) unter `PlaylistsController`, falls Risiko bestehen sollte, bestehende `ItemsController.Get`-Aufrufer zu beeinflussen

2. **Client-API-Erweiterung (`IPlaylistApiClient` / `VideoWebPlayerClient`)**:
   - Neue Methode `SearchMediaForPlaylistAsync(string searchTerm, CancellationToken?)` oder Wiederverwendung existierender `ItemsController.Get`-Methode über die `VideoWebPlayerClient` Facade
   - Diese Methode ruft den Backend-Endpoint auf und liefert `List<MediaEntryDto>` oder ähnlich strukturierte Suchergebnisse

3. **Razor-Komponente (`MediaSearchSelector.razor`)**:
   - Inspiriert von `Actors.razor` (Suchmuster) und `MediaBox.razor` (Darstellung)
   - Struktur:
     - Eingabefeld (Textfeld mit `@bind="searchTerm"` + `@bind:event="oninput"`)
     - Lade-Indikator während der Suche
     - Ergebnisliste: Zeigt Treffer als Kacheln oder Listeneinträge mit Bild, Titel und Medientyp
     - Auswahl-Handler: Statt Navigation eine Callback-Methode `OnMediaSelected(EventArgs)`, die `MediaType` + `MediaId` an die Parent-Komponente zurückgibt
   - Callback-Parameter: `EventCallback<(string mediaType, long mediaId)> OnMediaSelected`

4. **Anpassung `PlaylistEntriesList.razor`**:
   - Ersetze Zeilen 22–30 (Dropdown + Number-Input) durch ein `<MediaSearchSelector @ref="..." OnMediaSelected="OnMediaSelectedAsync" />`
   - Die `OnMediaSelectedAsync`-Handler wird `newEntryMediaType` und `newEntryMediaId` aus dem Callback setzen und dann `AddEntryAsync()` aufrufen
   - Lösche die Zustände `newEntryMediaType` und `newEntryMediaId` aus den Komponenten-Properties, da diese jetzt von `MediaSearchSelector` gekapselt sind

5. **Bestehende Logik bleibt unverändert**:
   - `AddEntryAsync()` in `PlaylistEntriesList.razor` bleibt identisch (nutzt noch immer `DtoAddMediaToPlaylistRequest` mit `MediaType` + `MediaId`)
   - `PlaylistService.AddMediaToPlaylistAsync()` (Duplikat-Check, Kaskaden-Logik, Zugriffsschutz) bleibt unverändert
   - `PlaylistsController.AddMediaToPlaylistAsync()` und alle Hilfsmethoden bleiben unverändert

## Konfiguration

Keine zusätzliche Konfiguration erforderlich. Das Such-Limit, Pagierung und Zugriffskontrolle folgen denselben Regeln wie die bestehende `ItemsController.Get`-Methode.

## Offene Fragen

1. **Endpoint-Strategie**: Soll `ItemsController.Get` erweitert werden oder ein neuer, playlist-spezifischer Sucheindpunkt unter `PlaylistsController` entstehen? (Kriterium: Risiko für bestehende Aufrufer von `ItemsController.Get` vs. Durchmischung von Verantwortlichkeiten)

2. **Suche-Performance**: Falls Playlist-Suche häufig genutzt wird, sollte ggf. eine Datenbankindexierung auf `Name`-Feldern von `Movie`, `TVShow`, `TVShowSeason`, `TVShowEpisode`, `MovieCollection` geprüft werden.

3. **UI-Details für `MediaSearchSelector`**:
   - Sollen Treffer in einer Dropdown-Liste angezeigt werden oder als Kachel-Grid wie in `MediaBox`?
   - Sollen Bilder angezeigt werden oder nur Text?
   - Maximale Trefferzahl pro Suche festlegen?

4. **Error-Handling**: Wie mit leeren Suchergebnissen und Netzwerkfehlern umgehen (Meldungen, Retry)?
