# Datenmodell- und API-Bestandsaufnahme

## Domänenmodell

- [`MediaBaseEntry.cs`](../../../../../VideoWebPlayer/Data/MediaBaseEntry.cs) enthält die gemeinsamen Identität-, Titel-, Datums-, Bild- und Klassifizierungsfelder. `Name`, `ReleaseDate`, `PremieredAt`, `EndedAt`, `ClassifiedAt` und `Changed` sind bereits vorhanden.
- [`TVShow.cs`](../../../../../../VideoWebPlayer/Data/TVShow.cs) enthält `Plot`, `GenreNames`, `PremieredAt` und die Genre-Verknüpfungen.
- [`TVShowSeason.cs`](../../../../../../VideoWebPlayer/Data/TVShowSeason.cs) verwendet die gemeinsamen Felder und die Beziehung zu Serie/Episoden, besitzt aber keine eigenen Plot-/Genre-Felder.
- [`TVShowEpisode.cs`](../../../../../../VideoWebPlayer/Data/TVShowEpisode.cs) enthält `Plot`, `ReleaseDate`, `PremieredAt` und Episodennummer.
- [`Movie.cs`](../../../../../../VideoWebPlayer/Data/Movie.cs) enthält `Plot`, `GenreNames`, `ReleaseDate`, `PremieredAt` sowie die Film-/Sammlungsbeziehung.
- [`MovieCollection.cs`](../../../../../../VideoWebPlayer/Data/MovieCollection.cs) leitet sich von `MediaEntry` ab und besitzt damit derzeit nicht dieselben Metadatenfelder wie `MediaBaseEntry`; Titel und abgeleitete Datumswerte werden separat behandelt.
- Genrebeziehungen werden über `MovieGenre` und `TVShowGenre` sowie die kommagetrennten Felder `GenreNames` gespeichert. Eine standardisierte Auswahl-/Update-API für Genres existiert nicht.

## DTOs und Client

- [`DtoMovie.cs`](../../../../../../VideoWebPlayer.Client/Models/DtoMovie.cs) definiert `DtoMediaEntry`, `DtoMovie`, `DtoMovieCollection`, `DtoTVShow`, `DtoTVShowSeason` und `DtoTVShowEpisode` für die Detailseiten.
- [`ItemsController.cs`](../../../../../../VideoWebPlayer/Controllers/ItemsController.cs) liest die fünf Kontexte über `FindEntry` und liefert verschachtelte DTOs inklusive Favoritenstatus und Eltern-/Kindbeziehungen.
- [`VideoWebPlayerClient`](../../../../../../VideoWebPlayer.Client/VideoWebPlayerClient.cs) wird von den Razor-Seiten für Lese- und Favoritenoperationen genutzt. Ein generischer Metadaten-Speicherendpunkt ist im Bestand nicht vorhanden.

## Konsequenzen für die Umsetzung

- Für Speichern werden neue autorisierte Update-Verträge benötigt, die den konkreten Objekttyp und die editierbaren Felder eindeutig zuordnen.
- Das Modell benötigt ein persistentes Kennzeichen je editierbarem Objekt oder eine gleichwertige Override-Struktur. `Changed`/`ClassifiedAt` allein unterscheiden keine manuelle Änderung von Scanstatus.
- Saison-Daten haben im aktuellen Modell keine eigenen Plot-/Genre-Felder. Die Anforderung muss daher für Staffeln entweder auf die vorhandenen gemeinsamen Felder begrenzt oder durch eine Modell-Erweiterung ergänzt werden.
