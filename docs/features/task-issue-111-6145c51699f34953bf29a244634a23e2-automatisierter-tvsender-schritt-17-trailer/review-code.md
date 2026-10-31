# Code-Review (selbstkritisch)

## Ergebnis

**Status:** Bestanden mit Hinweisen

## Befunde

- **Korrektheit Trailer-Erkennung:** Suffix-Match endet auf `-trailer` (OrdinalIgnoreCase) am
  Dateinamen-Stamm; `trailers/`-Ordner via `MediaCollection.Name` — beides deckt die
  Plex/Jellyfin-Konvention ab. Items in `trailers/`-Collections werden nie Movie-Kandidaten
  (vor dem NFO-Zweig gesammelt) und nie Episoden.
- **Hauptdatei-Ausschluss:** `MovieMediaItemsAsync`, `ItemsController.FindMediaItemAsync`,
  `BackfillMissingActorsAsync`, `AssignPicturesToMovieAsync` filtern `!IsTrailer` — ein Trailer
  wird nie Hauptdatei, Genre-/Playlist-Expansion geht über `Movies`/`TVShowEpisode` +
  `MovieMediaItemsAsync` und ist damit ebenfalls sauber.
- **Vorlauf-Logik:** `FindLeadMatchOffset` prüft `(fillAt, fillAt+Lead]` gegen die
  Ausstrahlungskarte; Trailer-Slots tragen `MovieId`, sind aber via `trailerItemIds` aus der
  Karte ausgeschlossen → Trailer „holt" sich nicht selbst als Ausstrahlung. Cursor `Trailer=3`
  additiv.
- **Verzögerte Expansion:** `ExpandTrailerStatesAsync` setzt `Items` + `RewindTo(NextIndexFor)`
  nach Materialisierung — `RuleState.Items` wurde dafür setzbar gemacht (einzige
  Mutabilitätsänderung).
- **Backup:** `MovieMediaItems.IsTrailer` in `OptionalRestoreColumns` + Bool-Default false;
  Legacy-Test belegt Restore ohne Spalte.
- **Hinweis (kein Befund):** Trailer ohne NFO-Fluss erzeugen kein `Movie` — beabsichtigt;
  `trailer/` Unbekannte (kein Film im Elternordner) bleiben schlicht unverknüpft.
