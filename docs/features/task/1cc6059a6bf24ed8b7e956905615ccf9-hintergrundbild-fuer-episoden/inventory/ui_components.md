# Bestandsaufnahme: UI-Komponenten

## `TVShowDetails.razor`
Datei: `VideoWebPlayer/Components/Pages/TV/TVShowDetails.razor`

Haupt-Komponente für die Episode-Detailansicht der Webanwendung.

**Aktuelle Struktur:**

### Parameter
| Parameter | Typ | Beschreibung |
|-----------|-----|-------------|
| `Id` | `long` | TV-Show-ID |
| `SeasonId` | `long?` | (Query) Staffel-ID |
| `EpisodeId` | `long?` | (Query) Episode-ID |
| `Position` | `long?` | (Query) Startposition in Sekunden |

### Komponenten-State
| Variable | Typ | Beschreibung |
|----------|-----|-------------|
| `show` | `DtoTVShow?` | Aktuell angezeigte TV-Show |
| `selectedSeason` | `DtoTVShowSeason?` | Ausgewählte Staffel |
| `selectedEpisode` | `DtoTVShowEpisode?` | Ausgewählte Episode |
| `showSeasonInfo` | `bool` | Flag für Staffel-Info-Anzeige |
| `showPlayer` | `bool` | Flag für Player-Sichtbarkeit |
| `isFavorite` | `bool` | Flag für Favoritenstatus |
| `resumeSeconds` | `long` | Wiederaufnahme-Position |

### Rendering
**Header-Section:**
```html
<div class="tvshow-header" style="background-image: url('@GetBannerUrl(...)'); position: relative;">
```

- Zeigt Banner/Fanart mit CSS `background-image`
- Overlay mit Episoden-/Staffel-/Show-Metadaten
- Play-Button und Back-Button für Navigation

**Besonderheiten:**
- Nutzt `GetBannerUrl()` für Banner, fallback auf Fanart oder Placeholder
- Nutzt `GetPosterUrl()` für Poster-Bilder in Episode-List
- Keine direktive Unterstützung für generierte Hintergrundbilder (noch nicht implementiert)

**Noch nicht vorhanden (für das Feature notwendig):**
- Rendering des `Episode.GeneratedBackgroundImageId` im Header
- Schleier-Effekt (Tint-Overlay) über dem Hintergrundbild
- Asynchrones Laden des generierten Background-Images
- Fallback-Logik bei fehlender Generierung

### Hilfsmethoden
| Methode | Beschreibung |
|---------|-------------|
| `GetBannerUrl(DtoMediaEntry)` | Gibt URL zu Banner-Bild zurück, fallback auf Fanart/Placeholder |
| `GetPosterUrl(DtoMediaEntry)` | Gibt URL zu Poster-Bild zurück, fallback auf Fanart/Placeholder |
| `GetEpisodeStreamUrl(DtoTVShowEpisode)` | Stream-URL für Player |
| `GetEpisodeDownloadUrl(DtoTVShowEpisode)` | Download-URL |
| `ToggleFavorite()` | Wechselt Favoritenstatus |

---

## API-Endpoints

Noch nicht vorhanden (gemäß Anforderung):

### Geplanter Endpoint: `GET /api/episodes/{episodeId}/background-image`
**Beschreibung:** Rückgabe der generierten Hintergrundbild-Grafik

**Response:**
- Content-Type: `image/png` oder `image/jpeg`
- Cache-Control: `public, max-age=31536000` (1 Jahr)

**Implementierungs-Kontext:**
- Sollte optional Authentication unterstützen
- Sollte Caching-Header korrekt setzen
- Fallback auf Placeholder bei fehlendem/fehlgeschlagenem Generate

---

## DTOs/Client-Models

Datei(en): `VideoWebPlayer.Client/Models/Dto*.cs`

**Existierende DTOs:**
- `DtoMovie`, `DtoTVShow`, `DtoTVShowSeason`, `DtoTVShowEpisode`
- `DtoMediaEntry` (Basis)
- `DtoRecentEntry`, `DtoSource`

**Noch nicht vorhanden (für Feature notwendig):**
- `EpisodeBackgroundImageDto` (optional, für Mapper-Integration)
  - Properties: `URL`, `IsGenerated`, `GeneratedAt`
  - Oder direkt als Property in `DtoTVShowEpisode`

---

## Styling/CSS

Keine spezifischen Klassen für generierte Hintergrundbilder gefunden.

**Zu erwartende CSS-Klassen (Anforderung):**
- `.generated-background` oder ähnlich für den Background-Container
- `.background-overlay` oder `.tint-overlay` für Schleier-Effekt
- `opacity: 0.4` oder ähnlich für Lesbarkeit
