# Datenmodell — Bestandsaufnahme

## `ProgramSlot`
Datei: `VideoWebPlayer/Data/ProgramSlot.cs`

| Eigenschaft | Typ | Beschreibung / Zweck |
|-------------|-----|----------------------|
| `Id` | `long` | Slot-ID |
| `TVChannelId` / `TVChannel` | `long` / `TVChannel` | Besitzender Sender (einzige echte FK) |
| `Start` / `End` | `DateTime` | UTC-Sendezeitfenster |
| `MediaItemId` | `long` | Weiche Referenz auf die Videodatei (kein FK) |
| `MovieId` | `long?` | Weiche Referenz auf Film (kein FK) |
| `TVShowEpisodeId` | `long?` | Weiche Referenz auf Episode (kein FK) |
| `ProgramRuleId` | `long?` | Weiche Referenz auf erzeugende Regel |
| `RuleType` | `ProgramRuleType` | Denormalisierte Regelart (Filler-Erkennung) |
| `Status` | `ProgramSlotStatus` | `Scheduled` = Sendezeit belegt; `Displaced` = verdrängt, nur Historie |
| `CreatedAt` | `DateTime` | Anlagezeitpunkt |

## `Movie` (erbt `MediaBaseEntry`)
Datei: `VideoWebPlayer/Data/Movie.cs`

| Eigenschaft | Typ | Beschreibung / Zweck |
|-------------|-----|----------------------|
| `Name` | `string` | Anzeigetitel (aus `MediaBaseEntry`) |
| `Plot` | `string?` | Handlungsbeschreibung aus NFO-`plot` — Beschreibungsquelle der Karten |
| `GenreNames` | `string?` | Kommagetrennte Genre-Namen aus NFO — Quelle des Genre-Badges |
| `PosterPictureId` | `long?` | Poster-Bild-Id (aus `MediaBaseEntry`) — Quelle des Kartenbilds |
| `MovieGenres` | `ICollection<MovieGenre>` | Genre-Verknüpfungen (Alternative zu `GenreNames`) |
| `MovieMediaItems` | `ICollection<MovieMediaItem>` | Datei-Verknüpfungen |

## `TVShowEpisode` (erbt `MediaBaseEntry`)
Datei: `VideoWebPlayer/Data/TVShowEpisode.cs`

| Eigenschaft | Typ | Beschreibung / Zweck |
|-------------|-----|----------------------|
| `Name` | `string` | Episodentitel |
| `Number` | `int` | Episodennummer |
| `TVShowSeasonId` / `TVShowSeason` | `long` / `TVShowSeason` | Staffel-Navigation (über sie: `TVShowSeason.TVShow`) |
| `Plot` | `string?` | Episodenbeschreibung aus NFO-`plot` |
| `PosterPictureId` | `long?` | Episoden-Poster (selten befüllt) |
| `GeneratedBackgroundPictureId` | `long?` | Generiertes Episoden-Standbild (Querformat, lazy via `/api/episodes/{id}/background-image`) |

## `TVShowSeason` (erbt `MediaBaseEntry`)
Datei: `VideoWebPlayer/Data/TVShowSeason.cs`

| Eigenschaft | Typ | Beschreibung / Zweck |
|-------------|-----|----------------------|
| `Name` | `string` | Staffelname, vom Klassifizierer als `Staffel {nn}` oder `Specials` erzeugt |
| `TVShowId` / `TVShow` | `long` / `TVShow` | Serien-Navigation |
| `PosterPictureId` | `long?` | Staffel-Poster (Fallback-Ebene) |

Kein numerisches `Number`-Feld — die Staffelnummer steckt nur im Namen (`Staffel 06`).

## `TVShow` (erbt `MediaBaseEntry`)
Datei: `VideoWebPlayer/Data/TVShow.cs`

| Eigenschaft | Typ | Beschreibung / Zweck |
|-------------|-----|----------------------|
| `Name` | `string` | Serientitel |
| `Plot` | `string?` | Serienbeschreibung |
| `GenreNames` | `string?` | Kommagetrennte Genre-Namen — Genre-Badge für Episoden-Slots |
| `PosterPictureId` | `long?` | Serien-Poster (letzte Fallback-Ebene) |

## `MediaItem` (erbt `MediaEntry`)
Datei: `VideoWebPlayer/Data/MediaItem.cs`

| Eigenschaft | Typ | Beschreibung / Zweck |
|-------------|-----|----------------------|
| `Name` | `string` | Dateiname — Titel-Fallback für Clip-Slots ohne Inhaltsbezug |
| `Duration` | `TimeSpan?` | Ermittelte Laufzeit |

Kein `Plot`/`PosterPictureId` — Bare-MediaItem-Slots (Clip-Füller) haben kein Bild/Beschreibung.

## `MediaBaseEntry`
Datei: `VideoWebPlayer/Data/MediaBaseEntry.cs`

Liefert `Id`, `Name`, `PosterPictureId`, `BannerPictureId`, `FanartPictureId` für alle
Inhaltstypen (Movie, TVShow, TVShowSeason, TVShowEpisode, MovieCollection).

## `Picture`
Datei: `VideoWebPlayer/Data/Picture.cs`

Bilddaten (`Data`, `ContentType`), ausgeliefert über `GET /api/pictures/{id}`
(`PicturesController`, `[BearerTokenCheck]`, liefert Platzhalter-PNG bei unbekannter Id).

## `DtoProgramGuideEntry`
Datei: `VideoWebPlayer.Client/Models/DtoProgramGuideEntry.cs`

| Eigenschaft | Typ | Beschreibung / Zweck |
|-------------|-----|----------------------|
| `SlotId` | `long` | Slot-ID |
| `Title` | `string` | Anzeigetitel (Filmname oder „Show — Staffel, E{n}: Episodentitel", „Unbekannt"-Fallback) |
| `MediaType` | `string?` | `MediaTypeValues.Movie` / `TVShowEpisode` |
| `MediaId` | `long?` | Film- bzw. Episoden-Id |
| `MediaItemId` | `long` | Videodatei-Id (Live-Stream) |
| `IsFiller` | `bool` | Füller-Slot |
| `StartUtc` / `EndUtc` | `DateTimeOffset` | Sendefenster |
| `DurationSeconds` | `double` | Slotdauer |
| `PositionSeconds` / `RemainingSeconds` | `double?` | Nur beim laufenden Slot gesetzt |

**Fehlt für die Anforderung:** Poster-Verweis, Beschreibung, Genre, strukturierte Episoden-Info.
