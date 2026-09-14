# Bestandsaufnahme: Playlist-Abbildungen (Schritt 10)

Dieses Dokument fasst den Projektzustand zusammen vor der Implementierung von Schritt 10 des Playlist-Features: **Playlists sollen eine Abbildung bekommen** (Upload oder automatisch generierte Collage aus Bildern der enthaltenen Titel).

## Zusammenfassung

### Was ist bereits vorhanden

**Infrastruktur für Bildgenerierung:**
- `HomeBackgroundImageGenerator`: Collage-Erzeugung aus bis zu 5 Bildern (Vorbild für Playlist-Cover-Generator)
- `EpisodeBackgroundImageGenerator`: Komplexe Bildverarbeitung mit Farb-Dominanz-Extraktion
- `Picture` Entity: Flexibles Datenmodell für alle Bildtypen mit `Data`, `ContentType`, `Width`, `Height`, `IsGeneratedBackground`, `EpisodeId`

**Datenmodelle:**
- `Playlist` Entity: Vorhanden mit `GenresManuallyOverridden` Flag (Schritt 9, als Vorbild für Cover-Override-Flag)
- `PlaylistEntry` Entity: Vollständig mit `MediaType`, `MediaId` zur Identifikation von Inhalten
- Alle Media-Entitäten (`Movie`, `TVShow`, `TVShowEpisode`, `TVShowSeason`, `MovieCollection`): Erben von `MediaBaseEntry` mit `PosterPictureId`, `BannerPictureId`, `FanartPictureId`

**Konfiguration:**
- `PlaylistSettings` Class: Bereits vorhanden mit Konfigurationen für Playlist-Management (erweiterbar um Cover-Einstellungen)
- appsettings.json: Struktur für Playlist-Konfiguration etabliert

**API-Controller:**
- `PlaylistsController`: Bestehende Endpoints für CRUD-Operationen (erweiterbar um Cover-Endpoints)
- `EpisodesController`: Vorbild für Bildauslieferungs-Endpoints (GetBackgroundImage)
- `PicturesController`: Vorbild für generische Bild-Auslieferung (GetPicture, GetHeroBackground)

**UI-Komponenten:**
- `PlaylistCoverPlaceholder.razor`: Bereits vorhanden, zeigt generisches Playlist-Symbol auf farbigem Hintergrund
- `PlaylistsList.razor`: Nutzt aktuell den Platzhalter (Zeile 66)
- `PlaylistDetail.razor`: Zeigt Platzhalter im Header (Zeile 32)

**Backup-Mechanismus:**
- `VideoWebPlayerBackupData`: Vollständig etabliert mit `OptionalRestoreTables` und `OptionalRestoreColumns`
- Pattern für neue DB-Spalten vorhanden: Spalten können als "optional" markiert werden

**Bestehende Tests:**
- Umfangreiche Tests für Playlists (E2E, bUnit, Service-Tests)
- Tests für `EpisodeBackgroundImageGenerator` und `EpisodeBackgroundImageService` als Vorbild
- Tests für Bildauslieferungs-Endpoints vorhanden

### Was fehlt für Schritt 10

**Datenmodell-Erweiterung:**
- `Playlist.CoverPictureId` (long?, Fremdschlüssel zu Picture) – NICHT VORHANDEN
- `Playlist.CoverPictureIsUserUploaded` (bool) – NICHT VORHANDEN
- Picture-Erweiterung für Playlist-Kontext: Entweder `PlaylistId` oder `IsPlaylistCover` Flag – NICHT VORHANDEN
- EF Core Migration für diese neuen Spalten – NICHT VORHANDEN

**Logik-Klassen:**
- `PlaylistCoverImageGenerator` Service – NICHT VORHANDEN
- `PlaylistCoverValidator` Service – NICHT VORHANDEN
- Cover-Verwaltungs-Methoden in `PlaylistService` – NICHT VORHANDEN
  - `GeneratePlaylistCoverAsync()`
  - `SetPlaylistCoverAsync()`
  - `GetPlaylistCoverAsync()`
  - `DeletePlaylistCoverAsync()`

**API-Endpoints:**
- `POST /api/playlists/{id}/cover/upload` – NICHT VORHANDEN
- `POST /api/playlists/{id}/cover/regenerate` – NICHT VORHANDEN
- `GET /api/playlists/{id}/cover` – NICHT VORHANDEN
- `DELETE /api/playlists/{id}/cover` – NICHT VORHANDEN

**UI-Komponenten:**
- `PlaylistCoverUploadDialog.razor` – NICHT VORHANDEN
- Erweiterungen in `PlaylistDetail.razor`:
  - "Neu erzeugen" Button für Cover-Regenerierung – NICHT VORHANDEN
  - "Bild hochladen" Dialog – NICHT VORHANDEN
- Erweiterungen in `PlaylistsList.razor` und `PlaylistDetail.razor` zur Anzeige echter Bilder – NICHT VORHANDEN

**Konfiguration:**
- Cover-Einstellungen in `PlaylistSettings`:
  - `AllowedCoverImageFormats` (kommagetrennte MIME-Typen) – NICHT VORHANDEN
  - `MaxCoverImageSizeBytes` – NICHT VORHANDEN
  - `GeneratedCoverWidthPixels` – NICHT VORHANDEN
  - `GeneratedCoverHeightPixels` – NICHT VORHANDEN
  - `GeneratedCoverJpegQuality` – NICHT VORHANDEN
- appsettings.json Einträge für Cover-Konfiguration – NICHT VORHANDEN

**Tests:**
- `PlaylistCoverGeneratorTests` (Unit-Tests) – NICHT VORHANDEN
- `PlaylistServiceCoverTests` (Integration-Tests) – NICHT VORHANDEN
- `PlaylistCoverValidatorTests` – NICHT VORHANDEN
- Erweiterungen in `BackupCompatibilityTests` für neue Spalten – ERFORDERLICH
- bUnit-Tests für Cover-Upload-Dialog und -Anzeige – ERFORDERLICH
- Controller-Tests für neue Endpoints – ERFORDERLICH

### Test-Ausgangszustand

**Status:** Tests erfolgreich abgeschlossen (2026-09-14, ~14:45 UTC)

**Testergebnisse Ausgangslauf:**
- **VideoWebPlayer.Tests:** 652 erfolgreich, 0 Fehler, 0 übersprungen (Dauer: 2 m 42 s)
- **MarkdownLinkCheck.Tests:** 6 erfolgreich, 0 Fehler, 0 übersprungen (Dauer: 181 ms)
- **Gesamttests:** 658 erfolgreich, 0 Fehler, 0 übersprungen

Alle bestehenden Playlist-Tests funktionieren ohne Fehler:
- Bestehende Playlist-Tests vorhanden und voll funktionsfähig
- Keine Tests für Cover-Funktionalität (da nicht implementiert) – wird in Schritt 10 hinzugefügt
- Backup-Kompatibilität-Tests müssen um neue Spalten erweitert werden

**Nachweis:** Siehe [inventory/tests.md](inventory/tests.md) und [test-results/](inventory/test-results/)

## Details

### [Datenmodelle](inventory/models.md)
Detaillierte Übersicht aller relevanten Entities:
- `Picture` – Generisches Bild-Entity (aktuell: Struktur ohne Playlist-Kontext)
- `MediaBaseEntry` (Basisklasse) mit `PosterPictureId`, `BannerPictureId`, `FanartPictureId`
- `Movie`, `TVShow`, `TVShowEpisode`, `TVShowSeason`, `MovieCollection` (erben von MediaBaseEntry)
- `Playlist` – Aktuell OHNE CoverPictureId und CoverPictureIsUserUploaded
- `PlaylistEntry` – Abbildung von Einträgen mit MediaType/MediaId
- `ApplicationDbContext` – Datenbankkontext mit allen DbSets

### [Logik-Klassen](inventory/logic.md)
Bestandsaufnahme der vorhandenen Services und Generatoren:
- `HomeBackgroundImageGenerator` – Collage-Erzeugung (Vorbild für Playlist-Cover)
- `EpisodeBackgroundImageGenerator` – Komplexe Bildverarbeitung
- `PlaylistService` – Zentrale Playlist-Logik (erweiterbar um Cover-Operationen)
- `EpisodeBackgroundImageService` – Orchestrierung von Hintergrundbild-Generierung

### [Interfaces](inventory/interfaces.md)
Service-Interfaces:
- `IPlaylistService` – Keine Cover-Methoden vorhanden
- `IUnlockedMediaService` – Zugriffsprüfung

### [Tests](inventory/tests.md)
Übersicht bestehender Tests und Testlücken:
- Umfangreiche Playlist-Tests (E2E, bUnit, Service-Tests)
- Tests für Bildgenerierung (Episode-Hintergrund) als Vorbild
- Tests für Image-Delivery-Endpoints als Vorbild
- **Testlücken:** Keine Tests für Playlist-Cover-Generierung, -Upload, -Validierung

## Abhängigkeiten und externe Bibliotheken

**Bereits vorhanden und getestet:**
- `SixLabors.ImageSharp` (mit JPEG-Unterstützung): Bildverarbeitung
- `Microsoft.EntityFrameworkCore`: ORM und Migrations
- `Microsoft.AspNetCore.Mvc`: Controller-Framework
- `System.ComponentModel.DataAnnotations`: Validierung

**Für Schritt 10 erforderlich (bereits vorhanden):**
- Alle obigen Bibliotheken sind bereits in Verwendung

## Designentscheidungen und Auslegungshilfen

Zwei wichtige Auslegungsentscheidungen aus der Anforderung, die bei der Implementierung dokumentiert werden müssen:

### 1. Manuelle "Neu erzeugen" statt automatischer Trigger
**Anforderung:** "soll sich anstoßen lassen" = On-demand statt automatisch bei Inhaltsänderung
**Unterschied zu Schritt 9:** Genre-Ableitung aktualisiert sich automatisch, Cover-Regenerierung ist explizite Benutzeraktion
**Zu dokumentieren in:** Code-Kommentare in `PlaylistCoverImageGenerator.GeneratePlaylistCoverAsync()`

### 2. Prioritätsreihenfolge der Medientypen (nicht Sortierreihenfolge)
**Anforderung:** "Reihenfolge: TVShow → TVShowEpisode → MovieCollection → Movie" ist Priorität pro Typ, nicht Eintrags-Reihenfolge
**Zu dokumentieren in:** Code-Kommentare in `PlaylistCoverImageGenerator`

## Architektur-Übersicht

### Bildverarbeitungs-Pipeline (Vorbild für Schritt 10)

```
HomeBackgroundImageGenerator.GenerateAsync()
  ├─ ContinueWatchingService.GetListAsync() – Quellmedien laden
  ├─ ApplicationDbContext.Pictures – Bilddaten abrufen
  └─ Compose() (statisch) – Collage erzeugen
      ├─ ImageSharp.Image.Load() – Bilder laden
      ├─ Bildtransformationen (Resize, Crop)
      └─ MemoryStream – JPEG-Kodierung
```

**Für Playlist-Cover analog zu erwarten:**
```
PlaylistCoverImageGenerator.GeneratePlaylistCoverAsync()
  ├─ PlaylistEntry-Abfrage nach MediaType/Priority
  ├─ Picture-Daten für referenzierte Media laden
  └─ Compose() – Collage (ggf. wiederverwendbar)
```

## Besonderheiten bei der Implementierung

### Backup-Kompatibilität
- Neue Spalten `Playlist.CoverPictureId`, `Playlist.CoverPictureIsUserUploaded` müssen in `VideoWebPlayerBackupData.OptionalRestoreColumns` registriert werden
- Alte Backups ohne diese Spalten müssen mit Defaultwerten wiederhergestellt werden (`CoverPictureId=null`, `CoverPictureIsUserUploaded=false`)

### Bildquellen-Abfrage
Es müssen Abfragen etabliert werden, um Bilder nach Medientyp zu sammeln:
- `TVShow` → `PosterPictureId` (aus `MediaBaseEntry`)
- `TVShowEpisode` → `PosterPictureId` (aus `MediaBaseEntry`)
- `MovieCollection` → `PosterPictureId` (aus `MediaBaseEntry`)
- `Movie` → `PosterPictureId` (aus `MediaBaseEntry`)

### Zugriffsprüfung
Alle Cover-Endpoints müssen prüfen: `Playlist.UserId == CurrentUser.Id`, sonst HTTP 403 Forbidden

## Hinweise für die Implementierung

1. **HomeBackgroundImageGenerator.Compose()** ist statisch und könnte wiederverwendet werden, muss aber ggf. für verschiedene Ausgabeparameter parametrisiert werden
2. **Picture-Entity** muss um Kontext erweitert werden (PlaylistId oder IsPlaylistCover), um Cleanup beim Löschen von Playlists zu ermöglichen
3. **Fehlerbehandlung:** Like `EpisodeBackgroundImageGenerator`, sollte `PlaylistCoverImageGenerator` `null` zurückgeben bei Fehlern, nicht Exceptions werfen
4. **Async-Verarbeitung:** `Task.Run()` wird für CPU-intensive Collage-Erzeugung empfohlen (wie in HomeBackgroundImageGenerator)
5. **Cache-Strategie:** ETag-basiertes Caching (wie in EpisodesController) für Cover-Bilder erwägen

