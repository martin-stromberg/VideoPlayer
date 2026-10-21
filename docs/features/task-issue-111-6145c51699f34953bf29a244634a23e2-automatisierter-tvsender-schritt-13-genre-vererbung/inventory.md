# Bestandsaufnahme: Genre-Konfiguration vererben (N7)

Analyse des Genre-Lebenszyklus (Erzeugung beim Scan und per Metadaten-Edit, globale
Admin-Pflege, kanonische Auflösung) bezogen auf die Anforderung N7.

## Zusammenfassung

- `Genre`-Zeilen sind quellbezogen (`Genre.MediaSourceId`); Konfiguration (`IsHidden`,
  `StartDate`/`EndDate`, `GenreName`-Alternativnamen) wird in `GenreAdmin.razor`
  **global pro Name** gepflegt (`GroupBy(g.Name.Trim().ToLower())` in `ReloadGenres`,
  `SaveGenre` schreibt auf alle Zeilen mit `g.Name == editGenre.Name` — **exakter
  Vergleich, Case-Mismatch vorhanden**).
- Zwei Erzeugungspfade legen neue `Genre`-Zeilen ohne Konfigurationserbe an:
  `MediaSourceClassifier.GetOrCreateGenresAsync` (Zeile 1405: `new Genre { Name, MediaSourceId }`,
  `IsHidden=false`, keine `AlternateNames`) und
  `MediaMetadataEditorService.NormalizeGenresAsync` (Zeile 214, identisches Muster).
- Die Alternativnamen-Umlenkung existiert bereits pro Quelle: findet `GetOrCreateGenresAsync`
  eine Zeile mit `GenreName`-Einträgen (≠ Eigenname), wird rekursiv auf die Alternativnamen
  derselben Quelle aufgelöst (Zeilen 1412–1419). **Kein Zyklenschutz** — A↔B-Alternativnamen
  oder Self-Referenz per Case-Variante würden endlos rekursieren.
- `GenreCanonicalResolver` (Schritt 9) spiegelt dieselbe Semantik für TV-Sender-Auswahl und
  Sendeplan-Expansion — quellbezogen, rekursiv, zyklensicher; `IsVisible` (IsHidden +
  saisonales Fenster) liegt dort zentral vor.
- Reparaturpfad vorhanden: `GenreService.MarkGenresAsChangedAsync` setzt `Setup.GenresChanged`;
  `MediaSourceScanService` ruft `classifier.CheckReloadGenres` → `ReloadGenres` iteriert alle
  nicht-manuellen Movies/TVShows und baut die Genre-Zuordnung über `GetOrCreateGenresAsync`
  neu auf. `DataUpgradeManager` (`CurrentVersion = 8`, Upgrades 5+6 = `ReloadGenres`) ist der
  etablierte Mechanismus für einmalige Daten-Upgrades.
- Test-Ausgangszustand: dokumentierte Basis aus `acceptance-schritt-12.md` (Commit `c386318`,
  Arbeitsbaum sauber, HEAD `a1572c3` unterscheidet sich nur durch Schritt-Marker-Commit):
  **2045 grün + 1 Skip + 1 Last-Flake (`PlaylistSkipLockedEntriesE2ETest`, Einzelwiederholung
  grün) — effektiv 2046 grün + 1 Alt-Skip**. Details: [Tests](inventory/tests.md).

## Details

- [Datenmodell](inventory/models.md)
- [Logik](inventory/logic.md)
- [Tests](inventory/tests.md)
