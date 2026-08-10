# Bestandsaufnahme: Datenmodellklassen

## `TVShowEpisode`
Datei: `VideoWebPlayer/Data/TVShowEpisode.cs`

Aktuell vorhandene Eigenschaften:

| Eigenschaft | Typ | Beschreibung |
|-------------|-----|-------------|
| `Number` | `int` | Episodennummer |
| `TVShowSeasonId` | `long` | Referenz zur übergeordneten Staffel |
| `TVShowSeason` | `TVShowSeason` | Navigation zur Staffel |
| `Plot` | `string?` | Handlungszusammenfassung |
| `TVShowEpisodeMediaItems` | `ICollection<TVShowEpisodeMediaItem>` | Zuordnung zu MediaItems |
| `MediaItems` (Komfort-Property) | `IEnumerable<MediaItem>` | Direkte Medien-Zugriff |

**Geerbt von `MediaBaseEntry`:**
- `Id`, `Name`, `ReleaseDate`, `PremieredAt`, `EndedAt`
- `MediaSourceId`, `CollectionId`, `CreatedAt`, `ClassifiedAt`, `Changed`
- `PosterPictureId`, `BannerPictureId`, `FanartPictureId` und zugehörige Navigation-Properties

**Noch nicht vorhanden (gemäß Anforderung):**
- `GeneratedBackgroundImageId` → Referenz zum generierten Hintergrundbild (Picture)
- `BackgroundImageRequiresUpdate` → Flag für Neugenerierung
- `BackgroundImageGeneratedAt` → Zeitstempel der letzten Generierung

---

## `Picture`
Datei: `VideoWebPlayer/Data/Picture.cs`

Aktuell vorhandene Eigenschaften:

| Eigenschaft | Typ | Beschreibung |
|-------------|-----|-------------|
| `Id` | `long` | Eindeutige Bild-ID |
| `MediaItemId` | `long` | Referenz zur MediaItem (Bilddatei) |
| `Type` | `string` | Bildtyp (z.B. "poster", "banner", "fanart", "thumb") |
| `Width` | `int?` | Bildbreite in Pixeln |
| `Height` | `int?` | Bildhöhe in Pixeln |
| `Description` | `string?` | Optionale Bildbeschreibung |
| `MediaItem` | `MediaItem` | Navigation zur MediaItem |
| `Data` | `byte[]` | Bilddaten (Binär) |
| `ContentType` | `string` | MIME-Type (z.B. "image/jpeg") |

**Noch nicht vorhanden (gemäß Anforderung):**
- `IsGeneratedBackground` → Kennzeichnung, dass dieses Bild generiert wurde (nicht importiert)
- `EpisodeIdReference` (optional) → Back-Reference zur Episode für optimierte Queries

---

## `MediaBaseEntry`
Datei: `VideoWebPlayer/Data/MediaBaseEntry.cs`

Basis-Klasse für alle Medieneinträge. Bereits vorhanden:

| Eigenschaft | Typ | Beschreibung |
|-------------|-----|-------------|
| `Id` | `long` | Eindeutige Eintrags-ID |
| `Name` | `string` | Anzeigename |
| `ReleaseDate` | `DateTime?` | Veröffentlichungsdatum |
| `PremieredAt` | `DateTime?` | Premiere-Datum |
| `EndedAt` | `DateTime?` | Enddatum |
| `MediaSourceId` | `long` | Referenz zur Medienquelle |
| `CollectionId` | `long` | Referenz zur Sammlung |
| `CreatedAt` | `DateTime` | Erstellungs-Zeitstempel |
| `ClassifiedAt` | `DateTime?` | Klassifizierungs-Zeitstempel |
| `Changed` | `bool` | Flag für Änderungen |
| `PosterPictureId`, `BannerPictureId`, `FanartPictureId` | `long?` | Bild-Referenzen |
| `PosterPicture`, `BannerPicture`, `FanartPicture` | `Picture?` | Bild-Navigation-Properties |

---

## `TVShowSeason`
Datei: `VideoWebPlayer/Data/TVShowSeason.cs`

Erbt von `MediaBaseEntry`.

| Eigenschaft | Typ | Beschreibung |
|-------------|-----|-------------|
| `TVShowId` | `long` | Referenz zur übergeordneten TV-Show |
| `TVShow` | `TVShow` | Navigation zur Show |
| `Episodes` | `ICollection<TVShowEpisode>` | Episoden dieser Staffel |
