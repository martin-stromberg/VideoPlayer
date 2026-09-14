# Entwicklungsschritt 10: Playlist-Abbildungen – Übersetzte Anforderung

## Fachliche Zusammenfassung

Das Playlist-Feature wird um ein Cover-Bild-System erweitert. Jede Playlist erhält eine Abbildung, die in der Übersicht und Detailansicht angezeigt wird. Der Besitzer kann entweder ein eigenes Bild hochladen oder ein Bild automatisch aus den Medieninhalten der Playlist als Collage (maximal 5 Bilder) erzeugen lassen. Ein hochgeladenes Bild hat immer Vorrang vor einem automatisch erzeugten. Die manuelle Neuerzeugung ist eine auslösbare Aktion in der UI; ein automatischer Trigger bei Inhaltsänderung ist nicht erforderlich. Ist weder ein eigenes noch ein erzeugtes Bild vorhanden, wird eine neutrale Ersatzdarstellung angezeigt.

---

## Betroffene Klassen und Komponenten

### Datenmodellklassen
- **`Playlist`** (VideoWebPlayer/Data/Playlist.cs, Erweiterung):
  - `CoverPictureId` (long?, nullable) – Fremdschlüssel zu Picture
  - `CoverPictureIsUserUploaded` (bool) – Flag, ob das Cover hochgeladen (true) oder automatisch erzeugt (false) ist, analog zum Muster `GenresManuallyOverridden` aus Schritt 9

- **`Picture`** (VideoWebPlayer/Data/Picture.cs, bereits vorhanden, eventuell Erweiterung):
  - Nutzung der bestehenden Spalten: `Data`, `ContentType`, `Width`, `Height`, `IsGeneratedBackground`
  - Neue Spalte zur Unterscheidung (optional): `IsPlaylistCover` (bool) – zur Datenbank-Integrität und späteren Abfragen, oder alternativ Nutzung von `EpisodeId`-Pattern mit neuer `PlaylistId`-Spalte für bessere Entkopplung (wird bei der Implementierung geklärt)

### Services / Logikklassen
- **`PlaylistCoverImageGenerator`** (neu):
  - Methode `GeneratePlaylistCoverAsync()` – Erzeugt Collage aus bis zu 5 Bildern
  - Methodenparameter: `playlistId`, `maxImages=5`, `targetWidth`, `targetHeight`, `quality`, `cancellationToken`
  - Realisiert die Prioritätsreihenfolge nach Medientyp: TVShow → TVShowEpisode → MovieCollection → Movie
  - Kann parametrisierte Wiederverwendung von `HomeBackgroundImageGenerator.Compose()` nutzen oder die Methode duplizieren (wird bei Implementierung entschieden)

- **`PlaylistService`** (VideoWebPlayer/Services/PlaylistService.cs, Erweiterung):
  - Methode `GeneratePlaylistCoverAsync(playlistId, userId, cancellationToken)` – Manuelle Generierung
  - Methode `SetPlaylistCoverAsync(playlistId, userId, pictureData, contentType, cancellationToken)` – Upload-Verarbeitung mit Validierung
  - Methode `GetPlaylistCoverAsync(playlistId, cancellationToken)` – Abruf des Covers (hochgeladen oder erzeugt)
  - Methode `DeletePlaylistCoverAsync(playlistId, userId, cancellationToken)` – Löschen des Covers (optional, für Cleanup)

- **`PlaylistCoverValidator`** (neu):
  - Methode `ValidateUploadAsync(fileContent, contentType, fileSize, allowedFormats, maxSizeBytes)` – Prüfung von Format und Dateigröße
  - Gibt `ValidationResult` mit aussagekräftiger Fehlermeldung zurück

### UI-Komponenten (Blazor)
- **`PlaylistsList.razor`** (VideoWebPlayer/Components/Playlists/PlaylistsList.razor, Erweiterung):
  - Ersetze `PlaylistCoverPlaceholder.razor` durch echtes Bild oder Platzhalter als Fallback
  - Nutze `CoverPictureId` zum Laden des Bildes; wenn nicht vorhanden oder null, zeige Platzhalter

- **`PlaylistDetail.razor`** (VideoWebPlayer/Components/Playlists/PlaylistDetail.razor, Erweiterung):
  - Kopfbereich: Zeige Cover-Bild oder Platzhalter als Hintergrundbild
  - Neue Icon-Button-Aktion: "Neu erzeugen" (Symbol-Button, passend zu bestehendem Design aus Schritt 7)
  - Neue Modal/Dialog für Upload: "Bild hochladen"

- **`PlaylistCoverUploadDialog.razor`** (neu):
  - Formular mit Datei-Input, Vorschau, Validierungsmeldungen
  - Button: "Hochladen"
  - CSS/Design konsistent mit bestehendem Upload-Pattern in der Anwendung

- **`PlaylistCoverPlaceholder.razor`** (bereits vorhanden, eventuell Erweiterung):
  - Neutrale Fallback-Darstellung, wenn kein Bild vorhanden ist

### Controller
- **`PlaylistsController`** (VideoWebPlayer/Controllers/PlaylistsController.cs, Erweiterung):
  - Endpoint `POST /api/playlists/{id}/cover/upload` – Upload mit Validierung
    - Parameter: `playlistId` (route), `file` (body, IFormFile)
    - Response: `{ success: bool, message: string, pictureId?: long }`
  - Endpoint `POST /api/playlists/{id}/cover/regenerate` – Manuelle Neuerzeugung
    - Parameter: `playlistId` (route)
    - Response: `{ success: bool, message: string, pictureId?: long }`
  - Endpoint `GET /api/playlists/{id}/cover` – Abruf des Covers
    - Parameter: `playlistId` (route)
    - Response: Picture-Daten oder 404, falls kein Cover vorhanden
  - Endpoint `DELETE /api/playlists/{id}/cover` – Löschen des Covers (optional)
    - Parameter: `playlistId` (route)
    - Response: `{ success: bool }`

### Tests
- **Unit-Tests** (`Tests/PlaylistCoverGeneratorTests.cs`, neu):
  - Test: Collage aus TVShow-Bildern + Movie-Bild → Priorität TVShow
  - Test: Playlist ohne Bilder in Einträgen → Generierung liefert null
  - Test: Generierung mit weniger als 5 Bildern → alle verfügbaren nutzen
  
- **Integration-Tests** (`Tests/PlaylistServiceCoverTests.cs`, neu):
  - Test: Upload mit gültigem Format und Größe → erfolgreich gespeichert
  - Test: Upload mit ungültigem Format → aussagekräftige Fehlermeldung
  - Test: Upload mit zu großer Datei → aussagekräftige Fehlermeldung
  - Test: Hochgeladenes Bild hat Vorrang vor generiertem
  - Test: "Neu erzeugen" nach Inhaltsänderung → aktualisiertes Bild
  - Test: Löschen und Neuanlegen einer Playlist → separates Cover

- **Backup-Kompatibilitäts-Tests** (`Tests/BackupCompatibilityTests.cs`, Erweiterung):
  - Test: Neue Spalten `Playlist.CoverPictureId` und `Playlist.CoverPictureIsUserUploaded` werden migriert
  - Test: Alt-Backup ohne diese Spalten wird wiederhergestellt (Defaultwerte: null, false)

- **bUnit-Tests** (`Tests/PlaylistListTests.cs`, Erweiterung; `Tests/PlaylistDetailTests.cs`, Erweiterung):
  - Test: PlaylistsList zeigt Cover-Bild oder Platzhalter
  - Test: PlaylistDetail zeigt Kopfbereich mit Cover oder Platzhalter
  - Test: Upload-Dialog erscheint und akzeptiert Datei
  - Test: "Neu erzeugen"-Button ist sichtbar und auslösbar

### Enums (optional)
- **`PlaylistCoverSourceType`** (neu, optional):
  - `UserUploaded` – vom Benutzer hochgeladenes Bild
  - `Generated` – automatisch erzeugt
  - (Redudant zu `CoverPictureIsUserUploaded` Flag, nur wenn expliziter Typ gewünscht ist)

---

## Implementierungsansatz

### 1. Datenmodell-Migration
- Neue Spalten zu `Playlist`:
  - `CoverPictureId` (long?, nullable, mit FK zu Picture)
  - `CoverPictureIsUserUploaded` (bool, default false)
- Migration: `AddPlaylistCoverFields` (neue Migration im EF Core Migrations-Ordner)
- Optional: Picture um `PlaylistId` oder ähnlich erweitern für bessere Entkopplung, oder `IsGeneratedBackground` + zusätzliches Flag

### 2. Collage-Erzeugung
- **Medientyp-Priorität beim Sammeln:**
  - Iterate über `PlaylistEntry` der Playlist gruppiert nach `MediaType`
  - Reihenfolge: `TVShow`, dann `TVShowEpisode`, dann `MovieCollection`, dann `Movie`
  - Für jeden Medientyp: Lade `Picture` des Medieninhalts (falls vorhanden)
  - Stoppt nach 5 Bildern
  
- **Bild-Abruf für jeden Medientyp:**
  - `TVShow`: Nutze `Movie.PosterPictureId` oder `TVShow.PosterPictureId` (zu klären)
  - `TVShowEpisode`: Nutze `TVShowEpisode.PosterPictureId`
  - `MovieCollection`: Nutze `MovieCollection.PosterPictureId` oder kombiniert aus Filmen (zu klären)
  - `Movie`: Nutze `Movie.PosterPictureId`
  - Für `TVShowSeason`: Nicht separat genannt in Anforderung; Fallback auf übergeordnetes `TVShow.PosterPictureId`

- **Collage-Komposition:**
  - Wiederverwendung der `Compose()`-Methode aus `HomeBackgroundImageGenerator` oder gleichwertiges Verfahren
  - Parameter: bis zu 5 Bilder, Zielauflösung, JPEG-Qualität
  - Ausgabe: Byte-Array (JPEG-kodiert)

- **Speicherung:**
  - Neue `Picture` Entity mit:
    - `Data` = Collage-Bytes
    - `ContentType` = "image/jpeg"
    - `IsGeneratedBackground` = true
    - `Width`/`Height` = Collage-Auflösung (z. B. 1600×520 wie HomeBackgroundImage)
  - `Playlist.CoverPictureId` ← neue Picture.Id
  - `Playlist.CoverPictureIsUserUploaded` = false

### 3. Upload-Validierung
- Ablauf:
  1. Datei-Upload via `IFormFile`
  2. Prüfung: Erlaubte Formate (Konfiguration: z. B. `["image/jpeg", "image/png", "image/webp"]`)
  3. Prüfung: Dateigröße ≤ konfigurierter Maximum (Konfiguration: z. B. 5 MB)
  4. Prüfung: Datei ist echtes Bild (z. B. via ImageSharp-Laden)
  5. Bei Fehler: aussagekräftige Meldung (z. B. "Nur JPEG, PNG und WebP werden unterstützt." / "Datei zu groß, max. 5 MB.")
  6. Bei Erfolg: Picture speichern, `CoverPictureId` + `CoverPictureIsUserUploaded = true` setzen

### 4. Upload-Endpoint
- `[HttpPost("api/playlists/{id}/cover/upload")]`
- `[RequestSizeLimit(5*1024*1024)]` oder via `[RequestFormLimits]`
- Body: `multipart/form-data` mit `file` parameter
- Rückgabe: `{ success: true, message: "Bild erfolgreich hochgeladen.", pictureId: 123 }` oder `{ success: false, message: "Bild zu groß..." }`

### 5. Regenerierungs-Aktion
- `[HttpPost("api/playlists/{id}/cover/regenerate")]`
- Ablauf:
  1. `PlaylistCoverImageGenerator.GeneratePlaylistCoverAsync(playlistId)`
  2. Erhält Liste von `PlaylistEntry` mit priorisierten Medientypen und deren Bildern
  3. Erzeugt Collage
  4. Speichert neue `Picture` oder aktualisiert bestehende
  5. Setzt `Playlist.CoverPictureId`, `Playlist.CoverPictureIsUserUploaded = false`
  6. Rückgabe: `{ success: true, message: "Cover neu erzeugt.", pictureId: 124 }`

### 6. Fallback-Platzhalter
- UI-Komponente `PlaylistCoverPlaceholder.razor`:
  - Zeigt generische, farbliche unterschiedliche Grafik je Playlist (ähnlich wie bisheriger Platzhalter)
  - Wird angezeigt, wenn `CoverPictureId == null` oder Laden fehlgeschlagen

### 7. Entscheidungen zu Dokumentation (auslegungshinweise)
Beide Auslegungsentscheidungen müssen explizit dokumentiert werden:

**a) Manuelle "Neu erzeugen" statt automatischer Trigger bei Inhaltsänderung:**
- Code-Kommentar in `PlaylistCoverImageGenerator` / `PlaylistService.GeneratePlaylistCoverAsync()`:
  ```csharp
  /// <summary>
  /// Regenerates the playlist cover image from current playlist entries.
  /// 
  /// DESIGN DECISION (Schritt 10, Anforderung): The generation is triggered manually via UI action.
  /// Unlike genre derivation (Schritt 9), which updates automatically whenever playlist content changes,
  /// cover regeneration is a deliberate, user-initiated action ("Neu erzeugen"). 
  /// This is because:
  /// - Users may temporarily add/remove items without wanting to regenerate the cover every time
  /// - Manual regeneration gives explicit control (optional automatic trigger on first generation, 
  ///   but not on every change)
  /// - Automatic regeneration on every change was intentionally NOT required by customer
  /// </summary>
  ```

**b) Prioritätsreihenfolge der Medientypen (nicht Sortierreihenfolge):**
- Code-Kommentar in `PlaylistCoverImageGenerator`:
  ```csharp
  /// <summary>
  /// Collects and prioritizes images from playlist entries by media type.
  /// 
  /// DESIGN DECISION (Schritt 10, Anforderung): Priority is by media type, not by playlist entry order.
  /// Order: TVShow images → TVShowEpisode images → MovieCollection images → Movie images.
  /// This means: first, we collect images from all TVShow entries, then all TVShowEpisode entries, etc.,
  /// until we have a maximum of 5 images. The physical order of entries in the playlist is not considered
  /// for image selection (only for determining what media types are present).
  /// </summary>
  ```

### 8. Besitzer-Berechtigung
- Alle Cover-Endpoints prüfen `Playlist.UserId == currentUser.Id`, sonst HTTP 403 Forbidden

### 9. Abhängigkeiten
- `SixLabors.ImageSharp` (bereits vorhanden für HomeBackgroundImageGenerator)
- `SixLabors.ImageSharp.Formats.Jpeg` (bereits vorhanden)
- Entity Framework Core (bereits vorhanden)

---

## Konfiguration

### Neue PlaylistSettings-Eigenschaften
```csharp
public class PlaylistSettings
{
    /// <summary>
    /// Comma-separated list of allowed MIME types for playlist cover upload.
    /// Default: "image/jpeg,image/png,image/webp"
    /// </summary>
    public string AllowedCoverImageFormats { get; set; } = "image/jpeg,image/png,image/webp";

    /// <summary>
    /// Maximum file size for playlist cover upload in bytes.
    /// Default: 5 MB (5242880 bytes)
    /// </summary>
    public long MaxCoverImageSizeBytes { get; set; } = 5 * 1024 * 1024;

    /// <summary>
    /// Target width of auto-generated playlist cover collage, in pixels.
    /// Default: 1600
    /// </summary>
    public int GeneratedCoverWidthPixels { get; set; } = 1600;

    /// <summary>
    /// Target height of auto-generated playlist cover collage, in pixels.
    /// Default: 520
    /// </summary>
    public int GeneratedCoverHeightPixels { get; set; } = 520;

    /// <summary>
    /// JPEG quality for auto-generated cover collage (0-100).
    /// Default: 85
    /// </summary>
    public int GeneratedCoverJpegQuality { get; set; } = 85;
}
```

### Eintrag in appsettings.json
```json
{
  "Playlists": {
    "MaxPlaylistsPerUser": null,
    "MaxPlaylistItemCount": null,
    "DefaultPageSize": 20,
    "MaxPageSize": 100,
    "BackfillIntervalMinutes": 15,
    "BackfillBatchSize": 25,
    "AllowedCoverImageFormats": "image/jpeg,image/png,image/webp",
    "MaxCoverImageSizeBytes": 5242880,
    "GeneratedCoverWidthPixels": 1600,
    "GeneratedCoverHeightPixels": 520,
    "GeneratedCoverJpegQuality": 85
  }
}
```

---

## Offene Fragen / Zu klärende Punkte

1. **Picture-Entity Erweiterung:** Soll die `Picture`-Entity um eine `PlaylistId`-Spalte erweitert werden (wie `EpisodeId`), oder soll die Entkopplung über ein Associations-Pattern (z. B. separate Tabelle `PlaylistCover`) erfolgen? (Empfehlung: `PlaylistId`, analog zu `EpisodeId`, für Konsistenz)

2. **Bildquellen für einzelne Medientypen:** Wie werden `PosterPictureId` auf den Entitäten `TVShow`, `TVShowEpisode`, `Movie`, `MovieCollection` abgefragt? Sind diese vorhanden oder müssen sie über Navigations-Properties ermittelt werden?

3. **Automatischer Trigger bei Playlist-Erstellung:** Soll beim Anlegen einer neuen Playlist sofort ein Cover generiert werden (Schnellstart), oder erst, wenn der Benutzer es explizit triggert? (Empfehlung: optional, aber nicht erforderlich)

4. **Upload-Bildformat-Umwandlung:** Sollen hochgeladene Bilder in ein einheitliches Format konvertiert werden (z. B. alle zu JPEG), oder als Upload-Format beibehalten?

5. **Maximale Bildauflösung für Uploads:** Sollen hochgeladene Bilder auf ein Maximum (z. B. 4000×3000) beschnitten oder skaliert werden, oder als-is akzeptiert?

6. **Fehlerbehandlung bei Generierung:** Wenn bei der Collage-Erzeugung ein Fehler auftritt (z. B. beschädigtes Bild), soll fallback auf Platzhalter erfolgen, oder Fehlermeldung anzeigen?

7. **Async-Generierung:** Soll die Collage-Erzeugung im Hintergrund erfolgen (langläufige Operation), oder synchron erwartet werden? (Empfehlung: async, via Task.Run wie in HomeBackgroundImageGenerator)

8. **Cleanup beim Löschen:** Wenn `CoverPictureId` auf null gesetzt wird (z. B. beim Upload eines neuen Bildes), soll die alte `Picture` gelöscht werden, oder orphaned bleiben? (Empfehlung: löschen für Cleanup, aber optional)

---

## Tests – Detaillierte Szenarien

### Unit-Tests (PlaylistCoverGeneratorTests)

**Test 1: Playlist mit TVShow- + Movie-Eintrag → TVShow-Bild bevorzugt**
```
Given: Playlist mit Einträgen:
  - TVShow (ID=100, hat PosterPictureId=1001, Show-Bild)
  - Movie (ID=500, hat PosterPictureId=2001, Film-Bild)
When: GeneratePlaylistCoverAsync() aufgerufen
Then: Collage enthält Show-Bild an erster Stelle, Film-Bild an zweiter
  (TVShow in Prioritätsreihenfolge vor Movie)
```

**Test 2: Playlist mit Einträgen ohne Bilder → Generierung ergibt null**
```
Given: Playlist mit Einträgen, aber alle haben PosterPictureId = null
When: GeneratePlaylistCoverAsync() aufgerufen
Then: Rückgabewert = null
```

**Test 3: Collage mit weniger als 5 Bildern**
```
Given: Playlist mit 3 verfügbaren Bildern (weniger als Max 5)
When: GeneratePlaylistCoverAsync() aufgerufen
Then: Collage nutzt alle 3 verfügbaren Bilder
```

**Test 4: Prioritätsreihenfolge vollständig (TVShow → TVShowEpisode → MovieCollection → Movie)**
```
Given: Playlist mit Einträgen:
  - Movie (Bild 1)
  - TVShow (Bild 2)
  - TVShowEpisode (Bild 3)
  - MovieCollection (Bild 4)
  - Movie (Bild 5)
  - Movie (Bild 6, sollte nicht berücksichtigt werden)
When: GeneratePlaylistCoverAsync() aufgerufen
Then: Collage enthält genau 5 Bilder in Reihenfolge:
  1. TVShow-Bild (Bild 2)
  2. TVShowEpisode-Bild (Bild 3)
  3. MovieCollection-Bild (Bild 4)
  4. Movie-Bild (Bild 1)
  5. Movie-Bild (Bild 5)
```

### Integration-Tests (PlaylistServiceCoverTests)

**Test 5: Upload mit gültigem Format und Größe → erfolgreich**
```
Given: PlaylistService, Datei JPEG, 2 MB, erlaubte Formate konfiguriert
When: SetPlaylistCoverAsync(playlistId, userId, fileBytes, "image/jpeg") aufgerufen
Then: 
  - Neue Picture in DB gespeichert
  - Playlist.CoverPictureId ← neue Picture.Id
  - Playlist.CoverPictureIsUserUploaded = true
  - ValidationResult.IsValid = true
```

**Test 6: Upload mit ungültigem Format (z. B. BMP) → aussagekräftige Fehlermeldung**
```
Given: PlaylistService, Datei BMP, nicht in erlaubten Formaten
When: SetPlaylistCoverAsync(…, "image/bmp") aufgerufen
Then: ValidationResult.IsValid = false, Message enthält "BMP wird nicht unterstützt"
```

**Test 7: Upload mit zu großer Datei (z. B. 10 MB, Max 5 MB) → Fehler**
```
Given: PlaylistService, Datei 10 MB, MaxCoverImageSizeBytes = 5 MB
When: SetPlaylistCoverAsync(…, fileBytes.Length=10MB) aufgerufen
Then: ValidationResult.IsValid = false, Message enthält "max. 5 MB"
```

**Test 8: Hochgeladenes Bild hat Vorrang vor generiertem**
```
Given: Playlist mit:
  - Automatisch generiertem Cover (CoverPictureId=10, IsUserUploaded=false)
  - Benutzer lädt neues Bild hoch (wird Picture ID=11)
When: SetPlaylistCoverAsync(…, newFile) aufgerufen
Then: Playlist.CoverPictureId = 11, Playlist.CoverPictureIsUserUploaded = true
  (alte Picture ID=10 wird überschrieben oder gelöscht)
```

**Test 9: "Neu erzeugen" nach Inhaltsänderung liefert aktualisiertes Bild**
```
Given: Playlist mit Cover (ID=15, 2 verfügbare Bilder)
  Anwender fügt Film mit Bild hinzu (jetzt 3 verfügbare Bilder)
When: GeneratePlaylistCoverAsync() explizit aufgerufen
Then: Neue Picture mit 3 Bildern erzeugt (ID=16)
  Playlist.CoverPictureId = 16
  (alte Picture ID=15 wird überschrieben, alte Bilder verworfen)
```

### Backup-Kompatibilitäts-Tests

**Test 10: Neue Spalten werden migriert**
```
Given: Datenbankversion vor Schritt 10 (keine Spalten Playlist.CoverPictureId, etc.)
When: Migration ausgeführt
Then: 
  - Spalten vorhanden
  - Bestehende Playlists haben Defaultwerte: CoverPictureId=null, CoverPictureIsUserUploaded=false
```

**Test 11: Alt-Backup ohne neue Spalten wird wiederhergestellt**
```
Given: Backup aus Schritt 9 (vor Cover-Feature) wird restauriert
When: Wiederherstellung durchgeführt
Then: 
  - Playlists in neuer Struktur mit Defaultwerten
  - Keine Fehler, Abwärtskompatibilität gewährleistet
  - OptionalRestoreColumns berücksichtigt fehlende Spalten
```

### bUnit-Tests (UI)

**Test 12: PlaylistsList zeigt Cover-Bild oder Platzhalter**
```
Given: PlaylistsList Komponente mit Playlists:
  - Playlist 1 mit CoverPictureId (Bild vorhanden)
  - Playlist 2 mit CoverPictureId=null (kein Bild)
When: Komponente gerendert
Then: 
  - Playlist 1: Bild angezeigt
  - Playlist 2: Platzhalter angezeigt
```

**Test 13: PlaylistDetail zeigt Kopfbereich mit Cover oder Platzhalter**
```
Given: PlaylistDetail Komponente, Playlist mit/ohne Cover
When: Komponente gerendert
Then: Hintergrundbild im Kopfbereich ist Cover oder Platzhalter
```

**Test 14: Upload-Dialog erscheint und akzeptiert Datei**
```
Given: PlaylistDetail mit "Bild hochladen"-Button
When: Button geklickt
Then: 
  - Dialog erscheint
  - Datei-Input funktioniert
  - Upload-Button triggert API-Call
```

**Test 15: "Neu erzeugen"-Button auslösbar**
```
Given: PlaylistDetail mit "Neu erzeugen"-Button
When: Button geklickt
Then: 
  - POST /api/playlists/{id}/cover/regenerate aufgerufen
  - Cover wird aktualisiert
  - Erfolgs- oder Fehlermeldung angezeigt
```

---

## Dokumentation

### Zu aktualisieren/neu anlegen:

1. **docs/help/playlists.md** (Erweiterung):
   - Neuer Abschnitt "Abbildungen" ("Covers")
   - Erklärung: Jede Playlist hat ein Cover (hochgeladen oder automatisch generiert)
   - UI-Beschreibung: Upload-Aktion, "Neu erzeugen"-Button, Platzhalter
   - Auslegungsentscheidung: "Neu erzeugen ist manuelle Aktion, nicht automatisch bei Inhaltsänderung"

2. **docs/help/playlists-business-rules.md** (Erweiterung):
   - Neue Rules:
     - BR-22: Upload-Validierung (Format, Dateigröße)
     - BR-23: Prioritätsreihenfolge der Medientypen bei automatischer Collage-Erzeugung
     - BR-24: Hochgeladenes Bild hat Vorrang vor automatisch erzeugtem
     - BR-25: "Neu erzeugen" ist manuelle Aktion (keine Automatisierung bei Inhaltsänderung erforderlich)

3. **docs/help/playlists-api.md** (Erweiterung):
   - Neue Endpoints:
     - `POST /api/playlists/{id}/cover/upload`
     - `POST /api/playlists/{id}/cover/regenerate`
     - `GET /api/playlists/{id}/cover`
     - `DELETE /api/playlists/{id}/cover` (optional)
   - Request/Response Schemas für jeden Endpoint
   - Fehlerbehandlung (400 Bad Request bei Invalid Format/Size, 403 Forbidden bei fehlender Berechtigung, 404 Not Found)

4. **docs/features/task/issue-207-…/design.md** (neu oder Erweiterung):
   - Architektur der Cover-Verwaltung
   - Entkopplungsmuster (Picture-Referenzen)
   - Collage-Algorithmus (ähnlich HomeBackgroundImage)

---

## Zusammenfassung der Entscheidungen

| Entscheidung | Wert | Begründung |
|--------------|------|------------|
| **Regenerierungs-Trigger** | Manuell (UI-Button), nicht automatisch bei Änderung | Anforderung wörtlich: "soll sich anstoßen lassen" = on-demand, kein zwingend automatischer Trigger |
| **Prioritätsreihenfolge** | Nach Medientyp (TVShow → TVShowEpisode → MovieCollection → Movie), nicht Sortierreihenfolge | Anforderung: "Reihenfolge … Serienbilder, Episodenbilder, …" wird als Priorität pro Typ, nicht Eintrags-Reihenfolge interpretiert |
| **Collage-Maximal** | 5 Bilder | Anforderung: "höchstens fünf Bilder" |
| **Bildformat Upload** | JPEG, PNG, WebP (konfigurierbar) | Gängige, webkompatible Formate |
| **Max Dateigröße** | 5 MB (konfigurierbar) | Praxiswert, nicht in Anforderung spezifiziert, daher Projektvorgabe |
| **Collage-Auflösung** | 1600×520px (konfigurierbar) | Analog HomeBackgroundImageGenerator, weit verbreitet |
| **Codec Ausgabe** | JPEG, Qualität 85 (konfigurierbar) | Analog HomeBackgroundImageGenerator |
| **Hochgeladenes Bild Vorrang** | Ja, eindeutig | Anforderung: "Ein hochgeladenes Bild hat immer Vorrang vor einem automatisch erzeugten." |
| **Fallback bei fehlendem Bild** | Neutrale Platzhalter-Darstellung | Anforderung: "wird eine neutrale Ersatzdarstellung angezeigt" |
