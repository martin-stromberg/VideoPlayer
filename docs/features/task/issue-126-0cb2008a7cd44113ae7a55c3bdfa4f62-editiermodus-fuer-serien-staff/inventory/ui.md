# UI-Bestandsaufnahme

## Vorhandene Einstiegspunkte

- [`MovieCollectionDetails.razor`](../../../../../VideoWebPlayer/Components/Pages/Movies/MovieCollectionDetails.razor) ist unter `/moviecollection/{Id:long}` geroutet und rendert alternativ die Filmsammlung oder einen ausgewählten Film. Beide Zustände besitzen einen Favoriten-Button, Titel, Jahres-/Genreanzeige, Plot-Platzhalter beziehungsweise Plot sowie Abspiel-/Downloadaktionen.
- [`TVShowDetails.razor`](../../../../../VideoWebPlayer/Components/Pages/TV/TVShowDetails.razor) ist unter `/tvshow/{Id:long}` geroutet. Der Zustand wird über `show`, `selectedSeason`, `selectedEpisode` und `showSeasonInfo` gesteuert. Staffel- und Episodenauswahl sind Methoden `SelectSeason`, `SelectEpisode` und `ShowSeason`.
- Beide Seiten laufen als `InteractiveServer`-Komponenten und laden die Daten über `VideoWebPlayer.Client.VideoWebPlayerClient`.

## Relevante Erweiterungspunkte

- Der Favoritenbereich im Kopf ist der naheliegende Ort für Stift-, Speichern- und Abbrechen-Aktionen.
- Die vorhandenen Statusfelder modellieren Anzeige- und Auswahlkontext, aber keinen Editierkontext und keine Dirty-Prüfung.
- Die Komponenten rendern derzeit keine `EditForm`-/Input-Komponenten für Medienmetadaten und besitzen keine Save-/Cancel-Methoden.
- Kontextwechsel (`SelectMovieAsync`, `ShowCollection`, `SelectSeason`, `SelectEpisode`, `ShowSeason`) müssen künftig vor der Zustandsänderung eine gemeinsame Prüfung auf ungespeicherte Änderungen ausführen.

## Noch nicht vorhandene UI-Flächen

- Es gibt keine Detailansichten für ein einzelnes TV-Staffel-/Episode-Routeziel außerhalb der kombinierten Serienseite.
- Es gibt kein bestehendes Bestätigungsdialog- oder Modal-Abstraktionsmuster in diesen Komponenten, das für das Verwerfen von Änderungen wiederverwendet werden könnte.
- Iconbuttons werden teilweise mit Inline-SVG beziehungsweise Textsymbolen umgesetzt; eine vorhandene Editieraktion ist nicht erkennbar.
