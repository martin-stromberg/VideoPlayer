# Datenmodelle

## `TVShowEpisode`
Datei: `VideoWebPlayer/Data/TVShowEpisode.cs`

Erbt von `MediaBaseEntry`.

| Eigenschaft | Typ | Beschreibung |
|-------------|-----|-------------|
| `Number` | `int` | Episodennummer innerhalb der Staffel |
| `TVShowSeasonId` | `long` | Fremdschlüssel zur zugehörigen Staffel |
| `TVShowSeason` | `TVShowSeason` | Navigation zur Staffel |
| `Plot` | `string?` | Zusammenfassung der Episode |
| `GeneratedBackgroundPictureId` | `long?` | Fremdschlüssel zum generierten Hintergrund-Bild |
| `BackgroundImageRequiresUpdate` | `bool` | Markiert, ob das Hintergrund-Bild erneuert werden soll |
| `BackgroundImageGeneratedAt` | `DateTime?` | Zeitstempel der letzten erfolgreichen Generierung |
| `GeneratedBackgroundPicture` | `Picture?` | Navigation zum generierten Hintergrund-Bild |
| `TVShowEpisodeMediaItems` | `ICollection<TVShowEpisodeMediaItem>` | Zugehörige Media-Items |
| `MediaItems` | `IEnumerable<MediaItem>` | Komfort-Property für direkten Zugriff auf Media-Items |
| `ReleaseDate` | `DateTime?` | **Von `MediaBaseEntry` geerbt** – Veröffentlichungsdatum (kann NULL sein) |
| `PremieredAt` | `DateTime?` | **Von `MediaBaseEntry` geerbt** – Premiere-Datum (kann NULL sein) |
| `Id` | `long` | **Von `MediaBaseEntry` geerbt** – Eindeutige Kennung |
| `Name` | `string` | **Von `MediaBaseEntry` geerbt** – Anzeigename |
| `MediaSourceId` | `long` | **Von `MediaBaseEntry` geerbt** – Mediendaten-Quelle |

## `TVShowSeason`
Datei: `VideoWebPlayer/Data/TVShowSeason.cs`

Erbt von `MediaBaseEntry`.

| Eigenschaft | Typ | Beschreibung |
|-------------|-----|-------------|
| `TVShowId` | `long` | Fremdschlüssel zur zugehörigen Serie |
| `TVShow` | `TVShow` | Navigation zur Serie |
| `Episodes` | `ICollection<TVShowEpisode>` | Episoden dieser Staffel |
| `Name` | `string` | **Von `MediaBaseEntry` geerbt** – Staffel-Name (z.B. "Staffel 01") |
| `ReleaseDate` | `DateTime?` | **Von `MediaBaseEntry` geerbt** – Veröffentlichungsdatum |
| `PremieredAt` | `DateTime?` | **Von `MediaBaseEntry` geerbt** – Premiere-Datum |

## `MediaBaseEntry`
Datei: `VideoWebPlayer/Data/MediaBaseEntry.cs`

Abstraktbasisklasse für alle Medieneinträge.

| Eigenschaft | Typ | Beschreibung |
|-------------|-----|-------------|
| `Id` | `long` | Eindeutige Kennung |
| `Name` | `string` | Anzeigename |
| `ReleaseDate` | `DateTime?` | Veröffentlichungsdatum (kann NULL sein) |
| `PremieredAt` | `DateTime?` | Premiere-Datum (kann NULL sein) |
| `EndedAt` | `DateTime?` | Ende-Datum |
| `MediaSourceId` | `long` | Mediendaten-Quelle |
| `CollectionId` | `long` | Sammlungskennung |
| `CreatedAt` | `DateTime` | Erstellungszeitstempel |
| `ClassifiedAt` | `DateTime?` | Letzter Klassifizierungszeitstempel |
| `Changed` | `bool` | Markiert, ob der Eintrag geändert wurde |
| `PosterPictureId` | `long?` | Fremdschlüssel zum Poster-Bild |
| `BannerPictureId` | `long?` | Fremdschlüssel zum Banner-Bild |
| `FanartPictureId` | `long?` | Fremdschlüssel zum Fanart-Bild |
| `PosterPicture` | `Picture?` | Navigation zum Poster-Bild |
| `BannerPicture` | `Picture?` | Navigation zum Banner-Bild |
| `FanartPicture` | `Picture?` | Navigation zum Fanart-Bild |
