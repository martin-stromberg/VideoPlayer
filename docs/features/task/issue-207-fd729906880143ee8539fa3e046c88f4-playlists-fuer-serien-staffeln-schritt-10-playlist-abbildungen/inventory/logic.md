# Logik-Klassen – Bestandsaufnahme

## `HomeBackgroundImageGenerator`
Datei: `VideoWebPlayer/Services/HomeBackgroundImage/HomeBackgroundImageGenerator.cs`

**Zweck:** Erzeugt Collage-Bilder aus bis zu 5 Bildern (wird als Vorbild für Playlist-Cover-Generierung genutzt)

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `GenerateAsync(user, targetWidth, targetHeight, transition, quality, cancellationToken)` | `public` | Erzeugt asynchron ein JPEG-Composite-Hintergrundbild aus den Continue-Watching-Postern des Nutzers |
| `Compose(imageData, targetWidth, targetHeight, transition, quality)` | `private` | Statische Hilfsmethode: Erzeugt Collage aus Bilddaten mit Überblendungen |
| `GetWeight(index, count, lx, drawW, transition)` | `private` | Statische Hilfsmethode: Berechnet Blendungsgewichtung für ein Pixel |
| `Distribute(total, count)` | `private` | Statische Hilfsmethode: Verteilt Gesamtbreite auf mehrere Streifen |

**Wichtige Details:**
- `DefaultMaxStrips = 5`: Maximal 5 Bilder in der Collage
- Zielauflösung: 1600×520px (via Parameter konfigurierbar)
- Ausgabeformat: JPEG mit einstellbarer Qualität (Standard: 85)
- Fehlerbehandlung: Gibt `null` zurück bei Fehler oder leerer Liste
- Async-Verarbeitung: Nutzt `Task.Run()` für CPU-intensive Collage-Erzeugung

**Abhängigkeiten:**
- `ContinueWatchingService`: Liefert Liste der Medien für Collage
- `ApplicationDbContext`: Lädt Picture-Daten
- `ILogger<HomeBackgroundImageGenerator>`: Logging

**Verwendete Bibliotheken:**
- `SixLabors.ImageSharp` / `SixLabors.ImageSharp.Processing`: Bildverarbeitung
- `SixLabors.ImageSharp.Formats.Jpeg`: JPEG-Kodierung

## `EpisodeBackgroundImageGenerator`
Datei: `VideoWebPlayer/Services/EpisodeBackgroundImage/EpisodeBackgroundImageGenerator.cs`

**Zweck:** Erzeugt automatisch Hintergrundbilder für TV-Episoden (komplexere Bildverarbeitung mit Farb-Dominanz-Extraktion)

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `GenerateBackgroundImageAsync(episode, sourceImageData, cancellationToken)` | `public` | Erzeugt asynchron ein Picture für eine Episode (Resize, Dominantfarbe, Tint-Overlay) |
| `ResizeImage(imageData, maxWidth, maxHeight)` | `public` | Skaliert Bild proportional auf max. Dimensionen (PNG-Ausgabe) |
| `GetDominantColor(imageData)` | `public` | Extrahiert Dominantfarbe mittels 8×8-Sampling-Grid-Histogramm |
| `CreateCanvasWithScaledImage(sourceImage, canvasWidth, canvasHeight, backgroundColor)` | `public` | Erstellt Canvas mit zentriertem Bild und Hintergrundfarbe (PNG-Ausgabe) |
| `ApplyTintOverlay(imageData, tintColor, opacity)` | `public` | Wendet transluzente Tint-Überblendung an (PNG-Ausgabe) |
| `EncodeAsJpeg(imageData, quality)` | `private` | Kodiert PNG zu JPEG |

**Wichtige Details:**
- Erzeugt `Picture`-Entity mit `IsGeneratedBackground=true`, `Type="background"`
- Zielauflösung: Konfigurierbar via `EpisodeBackgroundImageOptions`
- Ausgabeformat: JPEG
- Fehlerbehandlung: Gibt `null` zurück bei Fehler

**Abhängigkeiten:**
- `IOptions<EpisodeBackgroundImageOptions>`: Konfiguration
- `ILogger<EpisodeBackgroundImageGenerator>`: Logging

**Verwendete Bibliotheken:**
- `SixLabors.ImageSharp` / `SixLabors.ImageSharp.Processing`: Bildverarbeitung
- `SixLabors.ImageSharp.Formats.Jpeg`: JPEG-Kodierung

## `PlaylistService`
Datei: `VideoWebPlayer/Services/PlaylistService.cs`

**Zweck:** Zentrale Service-Klasse für alle Playlist-Operationen (CRUD, Sorting, Genres, Playback)

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `GetPlaylistsAsync(userId, genreId?, cancellationToken)` | `public` | Lädt alle Playlists eines Nutzers (optional nach Genre gefiltert) |
| `GetPlaylistAsync(playlistId, userId, cancellationToken)` | `public` | Lädt eine einzelne Playlist mit Zugriffsprüfung |
| `CreatePlaylistAsync(userId, name, description, sortMode, cancellationToken)` | `public` | Erstellt neue Playlist mit Validierung |
| `UpdatePlaylistAsync(playlistId, userId, name, description, sortMode, cancellationToken)` | `public` | Aktualisiert Name/Beschreibung einer Playlist |
| `DeletePlaylistAsync(playlistId, userId, cancellationToken)` | `public` | Löscht eine Playlist |
| `AddMediaToPlaylistAsync(playlistId, userId, mediaType, mediaId, cancellationToken)` | `public` | Fügt Media-Eintrag zur Playlist hinzu |
| `RemoveMediaFromPlaylistAsync(playlistId, userId, mediaType, mediaId, ..., cancellationToken)` | `public` | Entfernt Media-Eintrag aus Playlist |
| `GetPlaylistEntriesAsync(playlistId, userId, cancellationToken)` | `public` | Lädt alle Einträge einer Playlist |
| `GetPlaylistEntriesPagedAsync(playlistId, userId, pageNumber, pageSize, cancellationToken)` | `public` | Lädt paginierte Playlist-Einträge |
| `ChangeSortModeAsync(playlistId, userId, newSortMode, ..., cancellationToken)` | `public` | Ändert Sortiermodus der Playlist |
| `ReorderPlaylistEntryAsync(id, userId, entryId, newSortOrder, cancellationToken)` | `public` | Ändert manuelle Sortierreihenfolge eines Eintrags |
| `BatchReorderPlaylistEntriesAsync(id, userId, operations, cancellationToken)` | `public` | Führt mehrere Umordnungen atomar durch |
| `SetPlaylistGenresAsync(playlistId, userId, genreIds, cancellationToken)` | `public` | Setzt Genres manuell (setzt `GenresManuallyOverridden=true`) |
| `ResetPlaylistGenresAsync(playlistId, userId, cancellationToken)` | `public` | Setzt Genres zurück auf automatische Berechnung |
| `StartPlaylistAsync(playlistId, userId, entryId?, cancellationToken)` | `public` | Startet Wiedergabe einer Playlist |

**Abhängigkeiten:**
- `ApplicationDbContext`: Datenbankzugriff
- `PlaylistEntryAccessResolver`: Validierung von Media-Zugriff
- `PlaylistEntryReorderService`: Umordnungs-Logik
- `PlaylistGenreService`: Genre-Verwaltung
- `IOptions<PlaylistSettings>`: Konfiguration

**Bemerkungen zu Schritt 10:**
- PlaylistService hat noch KEINE Methoden für Cover-Generierung oder -Upload
- Diese müssen hinzugefügt werden: `GeneratePlaylistCoverAsync()`, `SetPlaylistCoverAsync()`, `GetPlaylistCoverAsync()`, `DeletePlaylistCoverAsync()`

## `EpisodeBackgroundImageService`
Datei: `VideoWebPlayer/Services/EpisodeBackgroundImage/EpisodeBackgroundImageService.cs`

**Zweck:** Orchestriert die Verwaltung von Episode-Hintergrundbildern (Lazy Generation, DB-Speicherung)

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `EnsureBackgroundImageAsync(episode, cancellationToken)` | `public` | Stellt sicher, dass Episode ein Hintergrundbild hat (generiert lazy falls nötig) |

**Abhängigkeiten:**
- `ApplicationDbContext`: Speichert generierte Pictures
- `EpisodeBackgroundImageGenerator`: Erzeugt die Bilder
- `ILogger<EpisodeBackgroundImageService>`: Logging

**Bemerkungen:**
- Pattern für Lazy Generation von Bildern könnte Vorbild für Playlist-Cover-Generierung sein

