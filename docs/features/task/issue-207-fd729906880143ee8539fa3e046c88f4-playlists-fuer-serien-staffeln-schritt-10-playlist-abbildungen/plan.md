# Umsetzungsplan: Playlist-Abbildungen (Schritt 10)

## Übersicht

Das Playlist-Feature wird um ein Cover-Bild-System erweitert. Jede Playlist erhält eine Abbildung, die hochgeladen oder automatisch aus den Medieninhalten der Playlist als Collage erzeugt werden kann. Die Implementierung umfasst Datenmodelländerungen, neue Service-Logik, API-Endpoints, UI-Komponenten, Validierung, Konfiguration sowie umfangreiche Tests inklusive Backup-Kompatibilität.

---

## Designentscheidungen

| Komponente / Bereich | Gewählter Ansatz | Begründung |
|----------------------|-----------------|------------|
| **Picture-Entity Erweiterung** | Neue `PlaylistId` Spalte (nullable) in Picture-Tabelle, analog zum Muster `EpisodeId` | Konsistenz mit bestehendem EF Core Muster; ermöglicht einfache Abfragen und referenzielle Integrität für Cleanup beim Playlist-Löschen |
| **Collage-Komposition** | Wiederverwendung von `HomeBackgroundImageGenerator.Compose()` oder Parametrisierung; falls nicht möglich, Code-Duplikation minimieren | HomeBackgroundImageGenerator liefert bewährte Bildkompositionsmethoden; Parametrisierung für verschiedene Ausgabegrößen sinnvoll |
| **Collage-Generierungs-Trigger** | Nur manuell auslösbar via UI-Button ("Neu erzeugen"), nicht automatisch bei Playlist-Inhaltsänderung | Anforderung: "soll sich anstoßen lassen" = On-demand; verhindert unnötige Regenerierungen bei temporären Änderungen |
| **Prioritätsreihenfolge bei Collage** | Nach Medientyp gruppiert (TVShow → TVShowEpisode → MovieCollection → Movie), nicht nach Eintrags-Sortierreihenfolge | Anforderung: "Reihenfolge Serienbilder, Episodenbilder, Filmsammlungsbilder, Filmbilder" = Priorität pro Typ |
| **Fallback TVShowSeason** | TVShowSeason-Einträge verwenden `TVShow.PosterPictureId` des übergeordneten TVShow | Anforderung: "Fallback auf das übergeordnete TVShow-Bild"; TVShowSeason hat keine eigenen Poster |
| **Picture Cleanup** | Alte Picture löschen, wenn neue Cover hochgeladen oder regeneriert werden | Verhindert verwaiste Bilder; Datenbankaufräumung |
| **Fehlerbehandlung bei Collage-Generierung** | Rückgabewert `null` bei Fehler; UI zeigt Platzhalter statt Fehler | Konsistent mit EpisodeBackgroundImageGenerator; benutzerfreundlich |

---

## Programmabläufe

### Upload Bild-Ablauf

1. Benutzer öffnet PlaylistDetail und klickt "Bild hochladen"
2. PlaylistCoverUploadDialog.razor öffnet Datei-Input
3. Benutzer wählt Datei aus
4. Dialog zeigt Vorschau des Bildes
5. Benutzer klickt "Hochladen"
6. POST `/api/playlists/{id}/cover/upload` wird aufgerufen mit `IFormFile`
7. PlaylistsController prüft Berechtigung: `Playlist.UserId == currentUser.Id`
8. PlaylistService.SetPlaylistCoverAsync() aufgerufen mit Bilddaten
9. PlaylistCoverValidator prüft Format (MIME-Type) gegen konfigurierte AllowedCoverImageFormats
10. PlaylistCoverValidator prüft Dateigröße gegen MaxCoverImageSizeBytes
11. Bei Validierungsfehler: HTTP 400 mit aussagekräftiger Fehlermeldung
12. Bei Erfolg:
    - Alte Picture (falls vorhanden) löschen
    - Neue Picture speichern mit `ContentType`, `Data`, `Width`, `Height`, `IsGeneratedBackground=false`, `PlaylistId={playlistId}`
    - Playlist.CoverPictureId = neue Picture.Id
    - Playlist.CoverPictureIsUserUploaded = true
    - Änderungen speichern
13. Controller antwortet: `{ success: true, message: "Bild erfolgreich hochgeladen.", pictureId: {pictureId} }`
14. UI aktualisiert Cover-Anzeige

Beteiligte Klassen/Komponenten: `PlaylistsController`, `PlaylistService`, `PlaylistCoverValidator`, `Picture`, `Playlist`, `PlaylistCoverUploadDialog.razor`

### Regenerierungs-Ablauf ("Neu erzeugen")

1. Benutzer klickt "Neu erzeugen"-Button in PlaylistDetail
2. POST `/api/playlists/{id}/cover/regenerate` wird aufgerufen
3. PlaylistsController prüft Berechtigung: `Playlist.UserId == currentUser.Id`
4. PlaylistService.GeneratePlaylistCoverAsync() aufgerufen
5. PlaylistCoverImageGenerator.GeneratePlaylistCoverAsync() lädt Playlist-Einträge
6. Einträge werden nach Medientyp gruppiert: TVShow, TVShowEpisode, MovieCollection, Movie
7. Für jede Gruppe werden Poster-Bilder geladen (via PosterPictureId):
   - TVShow → TVShow.PosterPictureId
   - TVShowEpisode → TVShowEpisode.PosterPictureId
   - TVShowSeason → TVShow.PosterPictureId (übergeordnet)
   - MovieCollection → MovieCollection.PosterPictureId
   - Movie → Movie.PosterPictureId
8. Bilder werden gesammelt bis max. 5 Bilder erreicht (Prioritätsreihenfolge beachten)
9. Falls keine Bilder vorhanden: GeneratePlaylistCoverAsync() gibt `null` zurück
10. Falls Bilder vorhanden:
    - PlaylistCoverImageGenerator.Compose() erzeugt Collage (1600×520px, JPEG, Qualität 85)
    - Neue Picture speichern mit `ContentType="image/jpeg"`, `Data={CollageByte}`, `Width=1600`, `Height=520`, `IsGeneratedBackground=true`, `PlaylistId={playlistId}`
    - Alte Picture (falls vorhanden) löschen
    - Playlist.CoverPictureId = neue Picture.Id
    - Playlist.CoverPictureIsUserUploaded = false
    - Änderungen speichern
11. Controller antwortet: `{ success: true, message: "Cover neu erzeugt.", pictureId: {pictureId} }` oder `{ success: false, message: "Keine Bilder verfügbar." }`
12. UI aktualisiert Cover-Anzeige

Beteiligte Klassen/Komponenten: `PlaylistsController`, `PlaylistService`, `PlaylistCoverImageGenerator`, `Picture`, `Playlist`, `ApplicationDbContext`

### Abruf Cover-Bild-Ablauf

1. PlaylistsList oder PlaylistDetail rendert
2. Komponente zeigt `<img src="/api/playlists/{playlistId}/cover" />`
3. GET `/api/playlists/{id}/cover` wird aufgerufen
4. PlaylistsController prüft ob Playlist vorhanden
5. Wenn `Playlist.CoverPictureId` vorhanden:
   - Picture laden und HTTP 200 mit Bilddaten, ContentType, Cache-Headern antworten
6. Wenn `Playlist.CoverPictureId == null`:
   - HTTP 404 zurückgeben
7. UI zeigt Cover oder Platzhalter (PlaylistCoverPlaceholder.razor)

Beteiligte Klassen/Komponenten: `PlaylistsController`, `Picture`, `Playlist`, `PlaylistsList.razor`, `PlaylistDetail.razor`

### Anzeige-Logik in UI-Komponenten

1. PlaylistsList.razor iteriert über Playlists
2. Für jede Playlist: Versuche, Cover-Bild zu laden (`GET /api/playlists/{id}/cover`)
3. Fallback-Priorität:
   - Hochgeladenes Bild (CoverPictureIsUserUploaded=true) wird bevorzugt
   - Automatisch generiertes Bild (CoverPictureIsUserUploaded=false)
   - Neutrale Platzhalter-Komponente (PlaylistCoverPlaceholder.razor) wenn kein Bild vorhanden
4. PlaylistDetail zeigt Cover als Hintergrundbild im Kopfbereich (ähnlich Episode-Detail)

Beteiligte Klassen/Komponenten: `PlaylistsList.razor`, `PlaylistDetail.razor`, `PlaylistCoverPlaceholder.razor`

---

## Neue Klassen

| Klasse | Typ | Zweck |
|--------|-----|-------|
| `PlaylistCoverImageGenerator` | Service | Generiert Collage aus bis zu 5 Bildern der Playlist-Einträge, gruppiert nach Medientyp-Priorität |
| `PlaylistCoverValidator` | Service | Validiert hochgeladene Bilder auf Format (MIME-Type) und Dateigröße |

---

## Änderungen an bestehenden Klassen

### `Playlist` (Datenmodellklasse)

- **Neue Eigenschaften:**
  - `CoverPictureId` (long?, nullable) — Fremdschlüssel zur Picture-Entity, enthält ID des Cover-Bildes (hochgeladen oder generiert)
  - `CoverPictureIsUserUploaded` (bool, Default: false) — Flag, ob das Cover hochgeladen (true) oder automatisch erzeugt (false) ist
  - `CoverPicture` (Picture?, Navigation) — Navigationseigenschaft zur Picture-Entity
- **Änderungen:** Keine vorhandenen Methoden ändern; Eigenschaften sind reine Datenhaltung

### `Picture` (Datenmodellklasse)

- **Neue Eigenschaften:**
  - `PlaylistId` (long?, nullable) — Fremdschlüssel zu Playlist (für Playlist-Cover, analog zu `EpisodeId`)
- **Bemerkung:** Ermöglicht DB-Abfragen und referenzielle Integrität; wird beim Löschen einer Playlist berücksichtigt

### `PlaylistService` (Service)

- **Neue Methoden:**
  - `GeneratePlaylistCoverAsync(playlistId, userId, cancellationToken)` — Erzeugt Cover-Collage aus aktuellen Playlist-Einträgen. Prüft Zugriff, gruppiert Einträge nach Medientyp, lädt Bilder, ruft PlaylistCoverImageGenerator auf, speichert neue Picture, aktualisiert Playlist.
  - `SetPlaylistCoverAsync(playlistId, userId, pictureData, contentType, width, height, cancellationToken)` — Speichert hochgeladenes Bild. Prüft Zugriff, erstellt neue Picture mit PlaylistId, setzt Playlist.CoverPictureId, CoverPictureIsUserUploaded=true, löscht alte Picture.
  - `GetPlaylistCoverAsync(playlistId, cancellationToken)` — Lädt Cover-Picture aus DB. Rückgabe: Picture-Entity oder null.
  - `DeletePlaylistCoverAsync(playlistId, userId, cancellationToken)` — Löscht Cover (optional). Prüft Zugriff, setzt Playlist.CoverPictureId=null, löscht Picture.
- **Betroffene vorhandene Methoden:**
  - `DeletePlaylistAsync()` — Muss erweitert werden: Alte Picture löschen, wenn Cover-ID vorhanden
- **Neue Abhängigkeiten:**
  - `PlaylistCoverImageGenerator`
  - `PlaylistCoverValidator`

### `PlaylistsController` (API Controller)

- **Neue Endpoints:**
  - `[HttpPost("api/playlists/{id}/cover/upload")]` — Upload-Endpoint. Parameter: playlistId (route), file (multipart IFormFile). Validiert Format/Größe, ruft PlaylistService.SetPlaylistCoverAsync() auf. Response: `{ success: bool, message: string, pictureId?: long }`
  - `[HttpPost("api/playlists/{id}/cover/regenerate")]` — Regenerierungs-Endpoint. Parameter: playlistId (route). Ruft PlaylistService.GeneratePlaylistCoverAsync() auf. Response: `{ success: bool, message: string, pictureId?: long }`
  - `[HttpGet("api/playlists/{id}/cover")]` — Abruf-Endpoint. Parameter: playlistId (route). Response: Picture-Bilddaten (200 OK) oder 404 Not Found
  - `[HttpDelete("api/playlists/{id}/cover")]` — Lösch-Endpoint (optional). Parameter: playlistId (route). Response: `{ success: bool }`
- **Betroffene vorhandene Methoden:**
  - Alle Cover-Endpoints müssen Zugriffsprüfung durchführen: `Playlist.UserId == currentUser.Id`, sonst HTTP 403 Forbidden

### `PlaylistSettings` (Konfigurationsklasse)

- **Neue Eigenschaften:**
  - `AllowedCoverImageFormats` (string, Default: "image/jpeg,image/png,image/webp") — Kommagetrennte Liste erlaubter MIME-Types
  - `MaxCoverImageSizeBytes` (long, Default: 5242880 = 5 MB) — Maximale Dateigröße für Uploads
  - `GeneratedCoverWidthPixels` (int, Default: 1600) — Zielbreite für Auto-Collage
  - `GeneratedCoverHeightPixels` (int, Default: 520) — Zielhöhe für Auto-Collage
  - `GeneratedCoverJpegQuality` (int, Default: 85) — JPEG-Qualität (0-100)

### `PlaylistDetail.razor` (Blazor-Komponente)

- **Neue UI-Elemente:**
  - Icon-Button "Bild hochladen" (neben "Neu erzeugen"): Öffnet PlaylistCoverUploadDialog
  - Icon-Button "Neu erzeugen": Triggert POST `/api/playlists/{id}/cover/regenerate`, zeigt Loading-Zustand, Erfolgs-/Fehlermeldung
  - Modal/Dialog für Cover-Upload wird eingebunden
  - Cover-Bild als Hintergrundbild im Kopfbereich (fallback auf PlaylistCoverPlaceholder)
- **Betroffene vorhandene Logik:**
  - Reload-Logik nach erfolgreichem Upload/Regenerierung

### `PlaylistsList.razor` (Blazor-Komponente)

- **Änderung:**
  - Ersetze `<PlaylistCoverPlaceholder />` durch `<img src="/api/playlists/{playlist.Id}/cover" alt="..." onerror="..." />` mit Fallback auf Platzhalter
  - Fallback-CSS oder JavaScript: Wenn Bild 404 ist, zeige PlaylistCoverPlaceholder

### `PlaylistCoverUploadDialog.razor` (neue Blazor-Komponente)

- **Zweck:** Modal-Dialog für Bild-Upload
- **UI-Elemente:**
  - Datei-Input (`<input type="file" accept="image/jpeg,image/png,image/webp" />`)
  - Vorschau des ausgewählten Bildes
  - Dateiname und -größe anzeigen
  - Fehlerausgabe für Format/Größe Validierung (Client-seitig)
  - "Hochladen"-Button (disabled während Upload läuft)
  - "Abbrechen"-Button
  - Erfolgs-/Fehlermeldung nach Upload
- **Logik:**
  - Client-seitige Vorvalidierung (Format, Größe) für bessere UX
  - Ruft POST `/api/playlists/{id}/cover/upload` auf
  - Zeigt Loading-Spinner während Upload

### `PlaylistCoverPlaceholder.razor` (bestehende Komponente)

- **Keine Änderung erforderlich** — bleibt bestehende Fallback-Darstellung, wird nur mehr als true Fallback genutzt

### `ApplicationDbContext` (Datenbank-Kontext)

- **Betroffene Navigations-Properties:**
  - Evtl. neue Fluent-Config für Picture.PlaylistId Fremdschlüssel (je nach EF Core Konvention)

---

## Datenbankmigrationen

| Migrationsname | Betroffene Tabellen/Spalten | Beschreibung der Änderung |
|----------------|----------------------------|---------------------------|
| `AddPlaylistCoverFields` | Playlists, Pictures | Neue Spalten in Playlist: `CoverPictureId` (long?, FK zu Pictures.Id), `CoverPictureIsUserUploaded` (bool, Default: false); neue Spalte in Pictures: `PlaylistId` (long?, nullable FK zu Playlists.Id) |

---

## Validierungsregeln

| Feld / Objekt | Regel | Fehlerfall |
|---------------|-------|------------|
| **Upload: MIME-Type** | Muss in `PlaylistSettings.AllowedCoverImageFormats` enthalten sein | Format nicht unterstützt: "Format-XYZ wird nicht unterstützt. Erlaubte Formate: JPEG, PNG, WebP." |
| **Upload: Dateigröße** | Muss `<= PlaylistSettings.MaxCoverImageSizeBytes` sein | Datei zu groß: "Datei zu groß. Maximal 5 MB erlaubt." |
| **Upload: Datei ist echtes Bild** | Datei muss als gültiges Bild von ImageSharp geladen werden können | Ungültige Bilddatei: "Datei ist kein gültiges Bild." |
| **Upload: Berechtigung** | `Playlist.UserId == currentUser.Id` | HTTP 403 Forbidden |
| **Regenerierung: Berechtigung** | `Playlist.UserId == currentUser.Id` | HTTP 403 Forbidden |

---

## Konfigurationsänderungen

| Eintrag | Typ | Standardwert | Zweck |
|---------|-----|--------------|-------|
| `Playlists:AllowedCoverImageFormats` | string | `"image/jpeg,image/png,image/webp"` | Kommagetrennte Liste erlaubter MIME-Types für Cover-Upload |
| `Playlists:MaxCoverImageSizeBytes` | long | `5242880` | Max. Dateigröße für Cover-Upload in Bytes (5 MB) |
| `Playlists:GeneratedCoverWidthPixels` | int | `1600` | Zielbreite der auto-generierten Cover-Collage in Pixeln |
| `Playlists:GeneratedCoverHeightPixels` | int | `520` | Zielhöhe der auto-generierten Cover-Collage in Pixeln |
| `Playlists:GeneratedCoverJpegQuality` | int | `85` | JPEG-Qualität (0-100) für auto-generierte Collage |

**appsettings.json Eintrag (Sektion "Playlists" erweitern):**
```json
{
  "Playlists": {
    "AllowedCoverImageFormats": "image/jpeg,image/png,image/webp",
    "MaxCoverImageSizeBytes": 5242880,
    "GeneratedCoverWidthPixels": 1600,
    "GeneratedCoverHeightPixels": 520,
    "GeneratedCoverJpegQuality": 85
  }
}
```

---

## Seiteneffekte und Risiken

- **Playlist-Löschung:** `DeletePlaylistAsync()` muss erweitert werden, um Cover-Picture zu löschen (Cleanup). DB-Kaskadenlöschung prüfen.
- **Backup-Kompatibilität:** Alte Backups ohne Spalten `Playlist.CoverPictureId`, `Playlist.CoverPictureIsUserUploaded`, `Picture.PlaylistId` müssen mit Defaultwerten wiederhergestellt werden. `VideoWebPlayerBackupData.OptionalRestoreColumns` muss aktualisiert werden.
- **Picture-Abfragen in anderen Services:** Falls andere Services Pictures abfragen, muss auf neue `PlaylistId` Spalte ggf. gefiltert werden (aber kein bekanntes Risiko bei aktuellen Queries).
- **Datenbankgröße:** Collage-Bilder (1600×520 JPEG) können mehrere KB groß sein; für Benutzern mit vielen Playlists ggf. Speicher-Monitoring nötig.

---

## Umsetzungsreihenfolge

1. **Datenbankmodell und Migration erstellen**
   - Voraussetzungen: Entity Framework Core, bestehende Playlist/Picture Entities
   - Beschreibung: Neue Eigenschaften `Playlist.CoverPictureId`, `Playlist.CoverPictureIsUserUploaded`, `Picture.PlaylistId` anlegen. Migration `AddPlaylistCoverFields` erstellen und anwenden. Konfigurationsdetails (Fremdschlüssel, On Delete Behavior) in EF Fluent API festlegen.

2. **Konfigurationsklasse PlaylistSettings erweitern**
   - Voraussetzungen: PlaylistSettings Klasse vorhanden, IOptions<PlaylistSettings> in DI registriert
   - Beschreibung: Neue Properties `AllowedCoverImageFormats`, `MaxCoverImageSizeBytes`, `GeneratedCoverWidthPixels`, `GeneratedCoverHeightPixels`, `GeneratedCoverJpegQuality` mit Defaults hinzufügen. appsettings.json um entsprechende Einträge erweitern.

3. **PlaylistCoverValidator Service anlegen**
   - Voraussetzungen: PlaylistSettings konfiguriert, SixLabors.ImageSharp NuGet
   - Beschreibung: Neue Klasse PlaylistCoverValidator mit Methode `ValidateUploadAsync(fileContent, contentType, fileSize, cancellationToken)`. Prüft MIME-Type, Dateigröße, ob Datei echtes Bild ist. Rückgabe: ValidationResult mit IsValid und ErrorMessage.

4. **PlaylistCoverImageGenerator Service anlegen**
   - Voraussetzungen: ApplicationDbContext, PlaylistSettings, SixLabors.ImageSharp NuGet, HomeBackgroundImageGenerator als Referenz
   - Beschreibung: Neue Klasse PlaylistCoverImageGenerator mit Methode `GeneratePlaylistCoverAsync(playlistId, cancellationToken)`. Lädt Playlist-Einträge, gruppiert nach Medientyp, lädt Poster-Bilder (max. 5), ruft Compose() auf (evtl. wiederverwendet von HomeBackgroundImageGenerator), speichert neue Picture, gibt Picture.Id zurück oder null. Enthält Code-Kommentare zur Designentscheidung "Prioritätsreihenfolge nach Medientyp".

5. **PlaylistService erweitern um Cover-Methoden**
   - Voraussetzungen: PlaylistService vorhanden, PlaylistCoverValidator, PlaylistCoverImageGenerator angelegt
   - Beschreibung: Neue Methoden `GeneratePlaylistCoverAsync()`, `SetPlaylistCoverAsync()`, `GetPlaylistCoverAsync()`, `DeletePlaylistCoverAsync()` hinzufügen. Zugriffsprüfung (`Playlist.UserId == userId`) implementieren. `DeletePlaylistAsync()` erweitern um Cover-Picture-Löschen. Enthält Code-Kommentare zu Designentscheidungen.

6. **PlaylistsController erweitern um Cover-Endpoints**
   - Voraussetzungen: PlaylistsController vorhanden, PlaylistService erweitert, Authentifizierung/Autorisierung eingebunden
   - Beschreibung: Neue Endpoints `POST /cover/upload`, `POST /cover/regenerate`, `GET /cover`, `DELETE /cover` (optional). RequestSizeLimit für Upload setzen (5 MB). Validierung, Fehlerbehandlung, aussagekräftige JSON-Responses implementieren.

7. **UI-Komponente PlaylistCoverUploadDialog.razor erstellen**
   - Voraussetzungen: Blazor-Infrastruktur, bestehende Modal-Komponenten als Vorbild
   - Beschreibung: Neue Komponente mit Datei-Input, Vorschau, Client-seitige Validierung (Format/Größe), Upload-Button, Fehlermeldungen. Design konsistent mit bestehendem UI-Pattern (z.B. aus Schritt 7/9).

8. **PlaylistDetail.razor erweitern**
   - Voraussetzungen: PlaylistDetail.razor vorhanden, PlaylistCoverUploadDialog.razor erstellt, API-Endpoints verfügbar
   - Beschreibung: Upload-Button und "Neu erzeugen"-Button hinzufügen. Modal/Dialog einbinden. Cover-Bild im Kopfbereich anzeigen (Hintergrundbild), Fallback auf PlaylistCoverPlaceholder. Buttons triggern API-Calls, UI aktualisiert nach Erfolg/Fehler. Symbol-Button-Stil aus Schritt 7 verwenden.

9. **PlaylistsList.razor anpassen**
   - Voraussetzungen: PlaylistsList.razor vorhanden, API-Endpoint GET /cover verfügbar
   - Beschreibung: `<img src="/api/playlists/{id}/cover" />` einbinden, onerror-Handler für Fallback auf PlaylistCoverPlaceholder. CSS-Styling für konsistente Darstellung.

10. **Backup-Kompatibilität implementieren**
    - Voraussetzungen: VideoWebPlayerBackupData Klasse, OptionalRestoreColumns Pattern bekannt, Migration angelegt
    - Beschreibung: VideoWebPlayerBackupData.OptionalRestoreColumns um neue Spalten erweitern (Playlist.CoverPictureId, Playlist.CoverPictureIsUserUploaded, Picture.PlaylistId). Defaultwerte festlegen (null, false, null). BackupRestorer prüfen und ggf. anpassen.

11. **Dokumentation anlegen/erweitern**
    - Voraussetzungen: docs/help/ und docs/features/ Verzeichnisstruktur
    - Beschreibung:
      - `docs/help/playlists.md` erweitern: Abschnitt "Abbildungen" mit UI-Beschreibung und Auslegungsentscheidungen
      - `docs/help/playlists-business-rules.md` erweitern: Business Rules BR-22 bis BR-25 (Format-Validierung, Prioritätsreihenfolge, Hochgeladenes Bild Vorrang, Manuelle Aktion)
      - `docs/help/playlists-api.md` erweitern: Neue Endpoints dokumentieren mit Request/Response Schemas
      - Evtl. `docs/features/.../design.md` anlegen: Architektur-Überblick

12. **Unit-Tests für PlaylistCoverValidator anlegen**
    - Voraussetzungen: Xunit, Moq, PlaylistCoverValidator implementiert
    - Beschreibung: PlaylistCoverValidatorTests Klasse mit Tests für:
      - Validierung MIME-Type (erlaubt, nicht erlaubt)
      - Validierung Dateigröße (im Limit, zu groß)
      - Validierung Bilddatei (gültig, ungültig/korrupt)
      - Aussagekräftige Fehlermeldungen

13. **Unit-Tests für PlaylistCoverImageGenerator anlegen**
    - Voraussetzungen: Xunit, Moq, PlaylistCoverImageGenerator implementiert, ApplicationDbContext-Mocks
    - Beschreibung: PlaylistCoverGeneratorTests Klasse mit Tests für:
      - Test 1: Prioritätsreihenfolge (TVShow vor Movie)
      - Test 2: Keine Bilder verfügbar → null
      - Test 3: Weniger als 5 Bilder verfügbar → alle nutzen
      - Test 4: Komplette Prioritätsreihenfolge (TVShow, Episode, Collection, Movie) mit max. 5
      - Test 5: TVShowSeason-Fallback auf TVShow

14. **Integration-Tests für PlaylistService Cover-Methoden anlegen**
    - Voraussetzungen: Xunit, SQLite Test-DB, PlaylistService erweitert
    - Beschreibung: PlaylistServiceCoverTests Klasse mit Tests für:
      - Test 6: Upload gültiges Format/Größe → erfolgreich
      - Test 7: Upload ungültiges Format → Fehler
      - Test 8: Upload zu große Datei → Fehler
      - Test 9: Hochgeladenes Bild Vorrang vor generiertem
      - Test 10: Regenerierung nach Inhaltsänderung → neues Bild
      - Test 11: Löschen und Neuanlegen → separates Cover
      - Test 12: Zugriffsprüfung (Fremdbenutzer kann nicht uploaden)

15. **Backup-Kompatibilität-Tests erweitern**
    - Voraussetzungen: Bestehende BackupCompatibilityTests, Migration angelegt
    - Beschreibung: BackupCompatibilityTests um Szenarien erweitern:
      - Test 13: Neue Spalten werden migriert
      - Test 14: Alt-Backup ohne neue Spalten wird mit Defaults wiederhergestellt

16. **bUnit-Tests für PlaylistList und PlaylistDetail erweitern**
    - Voraussetzungen: bUnit, bestehende Tests, UI-Komponenten implementiert
    - Beschreibung:
      - Test 15: PlaylistsList zeigt Cover-Bild oder Platzhalter
      - Test 16: PlaylistDetail zeigt Cover im Kopfbereich
      - Test 17: Upload-Dialog öffnet und akzeptiert Datei
      - Test 18: "Neu erzeugen"-Button sichtbar und auslösbar

17. **Controller-Tests für Playlist-Cover-Endpoints anlegen**
    - Voraussetzungen: Xunit, Moq, PlaylistsController erweitert, AuthenticationTest-Utils
    - Beschreibung: PlaylistCoverControllerTests Klasse mit Tests für:
      - Test 19: POST /upload gültiges Bild → 200 OK
      - Test 20: POST /upload ungültiges Format → 400 Bad Request
      - Test 21: POST /upload Fremdbenutzer → 403 Forbidden
      - Test 22: POST /regenerate → 200 OK
      - Test 23: GET /cover vorhanden → 200 OK mit Bilddaten
      - Test 24: GET /cover nicht vorhanden → 404 Not Found
      - Test 25: DELETE /cover → 200 OK

18. **E2E-Tests für Upload- und Regenerierungs-Abläufe anlegen**
    - Voraussetzungen: E2E-Test-Infrastruktur, UI implementiert, API verfügbar
    - Beschreibung: PlaylistCoverE2ETests Klasse mit Szenarien für:
      - E2E-Test 26 (Pflicht): Happy Path Upload — Benutzer klickt "Bild hochladen", wählt Datei, erfolgreicher Upload, Cover angezeigt
      - E2E-Test 27 (Pflicht): Happy Path Regenerierung — Benutzer klickt "Neu erzeugen", Collage wird erzeugt, Cover angezeigt
      - E2E-Test 28: Upload mit zu großer Datei — Fehlermeldung angezeigt
      - E2E-Test 29: Upload falsches Format — Fehlermeldung angezeigt
      - E2E-Test 30: Prioritätsreihenfolge — Playlist mit Series + Movie → Series-Bild zuerst

19. **Abschließende Integrationstests und Regression-Prüfung**
    - Voraussetzungen: Alle Tests aus Schritten 12-18 vorhanden
    - Beschreibung: Gesamte Test-Suite durchlaufen (`dotnet test`). Regression-Prüfung: Bestehende Playlist-Tests (von Schritt 1-9) müssen weiterhin bestehen. Keine Fehler bei Datenbankmigrationen, Backup-Wiederherstellung, etc.

---

## Tests

### Neue Tests

| Test / Hilfsmethode | Testklasse | Was wird geprüft / bereitgestellt? |
|--------------------|------------|-------------------------------------|
| `ValidateUpload_ValidJpeg_Success` | PlaylistCoverValidatorTests | MIME-Type image/jpeg erlaubt |
| `ValidateUpload_InvalidBmp_Fails` | PlaylistCoverValidatorTests | MIME-Type image/bmp nicht erlaubt, Fehlermeldung aussagekräftig |
| `ValidateUpload_FileTooLarge_Fails` | PlaylistCoverValidatorTests | Datei > MaxCoverImageSizeBytes, Fehlermeldung enthält "max. 5 MB" |
| `ValidateUpload_CorruptImage_Fails` | PlaylistCoverValidatorTests | Datei ist kein gültiges Bild, Fehler erkannt |
| `GeneratePlaylistCover_TVShowAndMovie_TVShowFirst` | PlaylistCoverGeneratorTests | Playlist mit TVShow + Movie → Collage in Prioritätsreihenfolge |
| `GeneratePlaylistCover_NoImages_ReturnsNull` | PlaylistCoverGeneratorTests | Playlist ohne Bilder → null |
| `GeneratePlaylistCover_LessThanFiveImages_UsesAll` | PlaylistCoverGeneratorTests | Playlist mit 3 Bildern → alle 3 in Collage |
| `GeneratePlaylistCover_CompletePriority_MaxFive` | PlaylistCoverGeneratorTests | Playlist mit TVShow, Episode, Collection, Movie, Movie (6 total) → 5 Bilder, Reihenfolge korrekt |
| `GeneratePlaylistCover_TVShowSeasonFallback_UsesTVShow` | PlaylistCoverGeneratorTests | Playlist mit TVShowSeason → nutzt übergeordnetes TVShow-Bild |
| `SetPlaylistCover_ValidUpload_Saved` | PlaylistServiceCoverTests | Upload gültiges Format, 2 MB → erfolgreich gespeichert, CoverPictureIsUserUploaded=true |
| `SetPlaylistCover_InvalidFormat_Fails` | PlaylistServiceCoverTests | Upload BMP → Validierungsfehler |
| `SetPlaylistCover_FileTooLarge_Fails` | PlaylistServiceCoverTests | Upload 10 MB (Max 5 MB) → Fehler |
| `SetPlaylistCover_UploadedPrecedesGenerated` | PlaylistServiceCoverTests | Playlist mit generiertem Cover, Upload neues Bild → altes gelöscht, neues aktiv |
| `GeneratePlaylistCover_AfterContentChange_Updated` | PlaylistServiceCoverTests | Playlist mit Cover + 2 Bildern, Eintrag hinzugefügt (3 Bilder), Regenerierung → neue Picture |
| `SetPlaylistCover_Unauthorized_Throws` | PlaylistServiceCoverTests | Fremdbnutzer versucht, Picture hochzuladen → Exception/Fehler |
| `DeletePlaylistAsync_CoverPictureDeleted` | PlaylistServiceCoverTests | Playlist mit Cover gelöscht → Picture gelöscht (Cleanup) |
| `MigrationApplied_NewColumns_Exist` | BackupCompatibilityTests | Migration `AddPlaylistCoverFields` angewendet → Spalten vorhanden |
| `BackupRestore_OldBackupWithoutCover_DefaultValues` | BackupCompatibilityTests | Alt-Backup ohne Cover-Spalten wiederhergestellt → CoverPictureId=null, CoverPictureIsUserUploaded=false |
| `PlaylistsList_WithCover_ShowsImage` | PlaylistListTests (bUnit) | Playlist mit CoverPictureId → Bild angezeigt |
| `PlaylistsList_NoCover_ShowsPlaceholder` | PlaylistListTests (bUnit) | Playlist ohne Cover → Platzhalter angezeigt |
| `PlaylistDetail_UploadDialog_Opens` | PlaylistDetailTests (bUnit) | "Bild hochladen" Button klicken → Dialog öffnet |
| `PlaylistDetail_UploadDialog_AcceptsFile` | PlaylistDetailTests (bUnit) | Dialog öffnet, Datei auswählen, "Hochladen" → API wird aufgerufen |
| `PlaylistDetail_RegenerateButton_Visible` | PlaylistDetailTests (bUnit) | PlaylistDetail zeigt "Neu erzeugen"-Button |
| `PlaylistDetail_RegenerateButton_Triggers` | PlaylistDetailTests (bUnit) | "Neu erzeugen" Button klicken → POST /regenerate aufgerufen |
| `PlaylistCoverController_Upload_ValidFile_200` | PlaylistCoverControllerTests | POST /upload gültiges Bild → 200 OK, JSON Response |
| `PlaylistCoverController_Upload_InvalidFormat_400` | PlaylistCoverControllerTests | POST /upload BMP → 400 Bad Request, Fehlermeldung |
| `PlaylistCoverController_Upload_Unauthorized_403` | PlaylistCoverControllerTests | POST /upload Fremdbenutzer → 403 Forbidden |
| `PlaylistCoverController_Regenerate_200` | PlaylistCoverControllerTests | POST /regenerate → 200 OK |
| `PlaylistCoverController_GetCover_Exists_200` | PlaylistCoverControllerTests | GET /cover vorhanden → 200 OK mit Bilddaten |
| `PlaylistCoverController_GetCover_NotExists_404` | PlaylistCoverControllerTests | GET /cover nicht vorhanden → 404 Not Found |
| `PlaylistCoverController_Delete_200` | PlaylistCoverControllerTests | DELETE /cover → 200 OK |
| `E2E_Upload_HappyPath` | PlaylistCoverE2ETests | Benutzer: Bild hochladen via UI → erfolgreich, Cover sichtbar |
| `E2E_Regenerate_HappyPath` | PlaylistCoverE2ETests | Benutzer: "Neu erzeugen" klicken → erfolgreich, Collage angezeigt |
| `E2E_Upload_FileTooBig_Error` | PlaylistCoverE2ETests | Benutzer: Datei > 5 MB → Fehlermeldung angezeigt |
| `E2E_Upload_InvalidFormat_Error` | PlaylistCoverE2ETests | Benutzer: Datei BMP → Fehlermeldung angezeigt |
| `E2E_Regenerate_PriorityOrder` | PlaylistCoverE2ETests | Playlist: TVShow + Movie → Collage-Reihenfolge korrekt (TVShow zuerst) |

### Betroffene bestehende Tests

| Test / Testklasse | Grund der Anpassung |
|-------------------|---------------------|
| `PlaylistServiceTests` (alle Playlist-Tests) | Evtl. Anpassung bei `DeletePlaylistAsync()` Mocking — Mock muss jetzt auch Picture-Löschen erwarten |
| `PlaylistDetailTests` (bUnit) | Evtl. Anpassung bei Rendering-Tests — neue Cover-Elemente in DOM können Selektoren beeinflussen |
| `PlaylistsE2ETests` | Evtl. Anpassung bei Playlist-Detailansicht — neue Buttons könnten Seitenstruktur verändern |
| `BackupCompatibilityTests` | Erweiterung erforderlich: Tests für neue Spalten + Alt-Backup-Wiederherstellung |

### E2E-Tests (primärer Funktionsnachweis)

| Priorität | Szenario | Testdatei / Testklasse | Abgedecktes Akzeptanzkriterium | Warum E2E nötig ist |
|-----------|----------|------------------------|-------------------------------|-------------------|
| **Pflicht** | Happy Path Upload: Benutzer wählt Datei aus, klickt "Hochladen", Datei wird gespeichert, Cover angezeigt | PlaylistCoverE2ETests.cs / `E2E_Upload_HappyPath` | "Benutzer kann Bild hochladen und sieht das Cover in der Übersicht und Detailansicht" | End-to-End-Validierung des kompletten Upload-Ablaufs inkl. UI-Interaktion, API-Call, DB-Speicherung, Bild-Auslieferung |
| **Pflicht** | Happy Path Regenerierung: Benutzer klickt "Neu erzeugen", Collage wird aus Playlist-Inhalten erzeugt, Cover angezeigt | PlaylistCoverE2ETests.cs / `E2E_Regenerate_HappyPath` | "Benutzer kann Cover-Bild aus Playlist-Inhalten automatisch erzeugen" | End-to-End-Validierung von Collage-Generierung, Medientyp-Priorität, Speicherung, UI-Update |
| Wichtig | Upload-Fehlbedienung: Datei zu groß (> 5 MB) → Fehlermeldung | PlaylistCoverE2ETests.cs / `E2E_Upload_FileTooBig_Error` | "Validierungsfehler bei zu großer Datei werden benutzerfreundlich angezeigt" | Benutzer muss Fehler in UI sehen; Unit/Integration-Tests prüfen nur Backend-Logik |
| Wichtig | Upload-Fehlbedienung: Falsches Format (z.B. BMP) → Fehlermeldung | PlaylistCoverE2ETests.cs / `E2E_Upload_InvalidFormat_Error` | "Validierungsfehler bei ungültigem Format werden angezeigt" | Benutzer muss Fehler in UI sehen und verstehen, welche Formate erlaubt sind |
| Wichtig | Prioritätsreihenfolge Validierung: Playlist mit TVShow + Movie, Regenerierung → Collage zeigt TVShow-Bild zuerst | PlaylistCoverE2ETests.cs / `E2E_Regenerate_PriorityOrder` | "Medientyp-Prioritätsreihenfolge wird bei Collage-Generierung eingehalten" | Aktuelles Testverhalten (Unit-Tests) kann implementierungsbasiert sein; E2E validiert echte Bildkomposition + UI-Anzeige |

### Bestehende E2E-Tests mit Auswirkungen

| Test / Testklasse | Grund der Anpassung |
|-------------------|---------------------|
| PlaylistDetailE2ETests | Anpassung möglich: PlaylistDetail-Seite hat neue Buttons/Elemente ("Bild hochladen", "Neu erzeugen"), evtl. Element-Selektoren ändern |
| PlaylistsE2ETests | Anpassung möglich: PlaylistsList-Seite zeigt jetzt echte Cover-Bilder statt reiner Platzhalter, Rendering kann anders sein |

---

## Offene Punkte

Keine. Alle Punkte aus requirement.md wurden geklärt oder sind durch Designentscheidungen oben dokumentiert:

- **Bildquellen-Abfrage:** Geklärt — alle Media-Entitäten erben PosterPictureId von MediaBaseEntry
- **Picture-Entity PlaylistId vs. IsPlaylistCover:** Entschieden — PlaylistId nutzen (konsistent mit EpisodeId-Muster)
- **Automatischer Trigger bei Playlist-Erstellung:** Entschieden — nicht erforderlich, nur manuell
- **Upload-Bildformat-Umwandlung:** Entschieden — nicht erforderlich, Upload-Format beibehalten
- **Maximale Bildauflösung für Uploads:** Entschieden — nicht erforderlich, keine Skalierung
- **Fehlerbehandlung bei Generierung:** Entschieden — null zurückgeben, UI zeigt Platzhalter
- **Async-Generierung:** Entschieden — Task.Run wie HomeBackgroundImageGenerator
- **Cleanup beim Löschen:** Entschieden — alte Picture löschen
