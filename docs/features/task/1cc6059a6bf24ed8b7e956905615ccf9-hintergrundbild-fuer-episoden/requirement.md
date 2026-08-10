# Übersetzte Anforderung: Dynamische Hintergrundbild-Generierung für Episoden

## Metadaten

- **Aufgaben-ID:** 1cc6059a-6bf2-4ed8-b7e9-56905615ccf9
- **Branch:** `task/1cc6059a6bf24ed8b7e956905615ccf9-hintergrundbild-fuer-episoden`
- **Titel:** Hintergrundbild für Episoden
- **Status:** Anforderungsanalyse

---

## Fachliche Zusammenfassung

Das Feature ergänzt die Episode-Detailansicht der Webanwendung um dynamisch generierte Hintergrundgrafiken. Bei erstmaligem Aufruf einer Episode wird aus dem Fanart-Bild ein visuell passendes Hintergrundbild erzeugt: Das Fanart wird proportional skaliert, zentriert und die restliche Canvas-Fläche mit der dominanten Farbe des Bildes aufgefüllt. Ein leichter Schleier-Effekt stellt sicher, dass der Episoden-Text lesbar bleibt, während die Grafik erkennbar bleibt. Das generierte Bild wird persistiert, um wiederholte Berechnungen zu vermeiden. Bei Hinzufügen eines neuen Fanarts wird das alte Hintergrundbild als „zu überarbeiten" markiert und bei nächstem Aufruf neu generiert.

---

## Betroffene Klassen und Komponenten

### Datenmodellklassen

1. **`TVShowEpisode`** (bestehend, Erweiterung)
   - Neue Property: `long? GeneratedBackgroundImageId` → Referenz zum generierten Hintergrundbild (Picture)
   - Neue Property: `bool BackgroundImageRequiresUpdate` → Flag, ob Hintergrundbild überarbeitet werden muss
   - Neue Property: `DateTime? BackgroundImageGeneratedAt` → Zeitstempel der letzten Generierung

2. **`Picture`** (bestehend, möglicherweise Erweiterung)
   - Neue Property: `bool IsGeneratedBackground` → Kennzeichnung, dass dieses Bild generiert wurde (nicht vom Scanner importiert)
   - Neue Property: `long? EpisodeIdReference` (optional) → Back-Reference zur Episode für Querys

### Neue Services / Klassen

1. **`EpisodeBackgroundImageGenerator`** (Service)
   - Methode: `Task<Picture> GenerateBackgroundImageAsync(TVShowEpisode episode, CancellationToken ct)`
   - Methode: `byte[] ResizeImage(byte[] imageData, int maxWidth, int maxHeight)`
   - Methode: `System.Drawing.Color GetDominantColor(byte[] imageData)`
   - Methode: `byte[] CreateCanvasWithScaledImage(byte[] sourceImage, int canvasWidth, int canvasHeight, System.Drawing.Color backgroundColor)`
   - Methode: `byte[] ApplyTintOverlay(byte[] imageData, System.Drawing.Color tintColor, float opacity)`
   - Properties: Konfigurierbare Max-Breite und Max-Höhe für Canvas

2. **`EpisodeBackgroundImageService`** (Service, Business Logic)
   - Methode: `Task<Picture?> EnsureBackgroundImageAsync(TVShowEpisode episode, CancellationToken ct)`
   - Methode: `Task MarkBackgroundImageForUpdateAsync(long episodeId, CancellationToken ct)`
   - Caching: In-Memory Cache (`IMemoryCache`) um wiederholte Dateizugriffe zu vermeiden
   - Thread-Safe durch Locks oder AsyncLock für parallele Requests

3. **`EpisodeBackgroundImageMapper`** (optional, für Blazor-Integration)
   - DTO: `EpisodeBackgroundImageDto` mit URL zum generierten Bild

### UI-Komponenten / Controller

1. **`TVShowDetailsPage.razor` oder `TVShowDetailsComponent.razor`** (Webanwendung)
   - Rendering des Background-Images im Kopfbereich mit Opacity
   - Prop-Binding für `Episode.GeneratedBackgroundImageId`

2. **API-Endpoint (falls nötig)**
   - `GET /api/episodes/{episodeId}/background-image` → Rückgabe der generierten Grafik
   - Caching-Header (`Cache-Control: public, max-age=31536000`)

### Test-Klassen

1. **`EpisodeBackgroundImageGeneratorTests`**
   - Test: Image-Skalierung mit Seitenverhältnis-Erhalt
   - Test: Dominante Farbe-Ermittlung
   - Test: Canvas-Erstellung und Platzierung
   - Test: Schleier-Effekt-Anwendung

2. **`EpisodeBackgroundImageServiceTests`**
   - Test: Lazily Load & Persist Pattern
   - Test: Cache-Verhalten
   - Test: Thread-Safety bei parallelen Requests
   - Test: Update Flag bei neuem Fanart

---

## Implementierungsansatz

### 1. Datenmodell-Anpassung

- **`TVShowEpisode`** um `GeneratedBackgroundImageId`, `BackgroundImageRequiresUpdate` und `BackgroundImageGeneratedAt` erweitern
- **`Picture`** um `IsGeneratedBackground` und optional `EpisodeIdReference` erweitern
- **Entity Framework Migration** erstellen

### 2. Service-Architektur

#### `EpisodeBackgroundImageGenerator`

- Nutzt **System.Drawing** (oder **ImageSharp** für Cross-Platform) zur Bildverarbeitung:
  - Laden des Fanart-Bildes (aus Picture-Daten)
  - Ermittlung der dominanten Farbe (z.B. über Histogramm oder Pixel-Sampling)
  - Proportionale Skalierung des Fanarts (max-width oder max-height)
  - Canvas-Erstellung mit Zielmaßen
  - Zentrierte Platzierung des Fanarts
  - Auffüllung der restlichen Fläche mit dominanter Farbe
  - Anwendung eines Schleier-Overlays mit Farbtönung
  - Speicherung als PNG oder JPEG
- **Fehlerbehandlung**: Falls Fanart nicht geladen werden kann → null zurück (Fallback auf Placeholder)

#### `EpisodeBackgroundImageService`

- **Öffentliche Schnittstelle**: `EnsureBackgroundImageAsync(TVShowEpisode, ct)`
  - Prüft, ob `GeneratedBackgroundImageId` gesetzt UND `BackgroundImageRequiresUpdate == false`
  - Falls ja → Bild aus `GeneratedBackgroundImageId` laden und zurückgeben (über Repository/DbContext)
  - Falls nein → `EpisodeBackgroundImageGenerator.GenerateBackgroundImageAsync()` aufrufen
  - Das neue `Picture`-Objekt mit `IsGeneratedBackground = true` in der DB speichern
  - `GeneratedBackgroundImageId` und `BackgroundImageGeneratedAt` auf Episode setzen
  - `BackgroundImageRequiresUpdate = false` setzen
  - Resultat in **In-Memory Cache** speichern
  - Async Locking / Semaphore zur Thread-Safety gegen Race Conditions
- **Kontext**: Wird aufgerufen beim Laden der Episode-Detailseite (Blazor Component Lifecycle)

### 3. Integration in Scanner und Fanart-Update

- **In `MediaSourceClassifier`** oder **`MediaSourceScanner`**:
  - Wenn neues Fanart für Episode gefunden wird:
    - `Episode.BackgroundImageRequiresUpdate = true` setzen
    - Existierendes generiertes Hintergrundbild kann lokal verbleiben oder optional gelöscht werden
- **Event / Hook-System**: Könnte über `EventManager` signalisiert werden

### 4. Backup-Integration

- **In `VideoWebPlayerBackupDataProvider`**:
  - Generierte Hintergrundbilder aus Backup ausschließen:
    - Filter: `where Picture.IsGeneratedBackground == false`
    - Oder: Separater Export-Flag für Picture-Tabelle
  - Alternativ: Nach Restore automatisch `BackgroundImageRequiresUpdate = true` setzen (wird beim nächsten Aufruf regeneriert)

### 5. Blazor-Integration (WebPlayer)

- **`TVShowDetailsPage.razor`** oder Subkomponente:
  - Im Kopfbereich ein `<div>` mit `background-image: url(...)`
  - CSS: `opacity: 0.4` oder `background-attachment: fixed` mit Schleier-Effekt
  - Laden des Background-Images asynchron über Service oder API-Endpoint
  - Fallback auf Static Placeholder-Image bei Fehler

### 6. Konfiguration

- **Konfigurierbare Parameter** in `appsettings.json`:
  ```json
  {
    "EpisodeBackgroundImage": {
      "MaxWidth": 1920,
      "MaxHeight": 1080,
      "TintColor": "#000000",
      "TintOpacity": 0.3,
      "CacheDurationMinutes": 60
    }
  }
  ```
- **Service-Registrierung** in `Program.cs`:
  ```csharp
  services.Configure<EpisodeBackgroundImageOptions>(configuration.GetSection("EpisodeBackgroundImage"));
  services.AddScoped<EpisodeBackgroundImageGenerator>();
  services.AddScoped<EpisodeBackgroundImageService>();
  ```

---

## Konfiguration

### Anwendungsebene

- **`appsettings.json`**: Canvas-Maßstäbe, Farbtönung, Cache-Dauer
- **`appsettings.Development.json`**: Optional deaktivierbar zum schnelleren Testing

### Fehlerbehandlung

- Logstufe: `ILogger<EpisodeBackgroundImageService>` für Fehler bei Generierung
- Fallback: Verwendung existierendes Placeholder-Hintergrundbild der Episode
- User-Facing: Keine Fehlerbehandlung notwendig (visuelles Degradation ist akzeptabel)

---

## Offene Fragen

1. **Canvas-Zielmaßstäbe**: Welche Auflösung soll das generierte Bild haben? (z.B. 1920×1080 wie aktuelles Placeholder?)

2. **Bildformat und Komprimierung**: Sollen generierte Bilder als PNG (Qualität) oder JPEG (Größe) gespeichert werden? Welche Kompressionsstufe?

3. **Dominante Farbe-Berechnung**: Soll die Farbe aus der zentralen Region oder dem gesamten Bild ermittelt werden? Welcher Algorithmus (einfaches Histogramm, k-means, etc.)?

4. **Tint-Overlay-Intensität**: Wie stark soll der Schleier sein? Feste Opazität oder basierend auf Bildkontrast?

5. **Persistierung des Fanarts**: Wird das Fanart-Bild als Binary in der `Picture.Data` Spalte oder als Dateisystem-Referenz gespeichert? (Bestehende Praxis prüfen)

6. **Cache-Strategie**: Soll nur das generierte Bild gecacht werden oder auch intermediate Ergebnisse (dominante Farbe, skaliertes Fanart)?

7. **Bestehende Placeholder-Logik**: Wo ist das aktuell verwendete Placeholder-Hintergrundbild definiert (statische Datei, CSS-Background, Blazor-Asset)? Soll dies die Fallback bleiben?

8. **MAUI-Frontend**: Gilt das Feature auch für die Mobile-App (VideoWebPlayer.Maui)? Falls ja, braucht es eine separate Implementierung oder REST-API Integration?

9. **Performance unter Last**: Wie viele Episoden-Zugriffe pro Minute sind zu erwarten? Reicht In-Memory Cache aus oder ist Distributed Cache (Redis) notwendig?

10. **Rückwärtskompatibilität**: Sollen Episoden ohne Fanart auch ein generiertes Bild bekommen (z.B. einfarbiger Fallback basierend auf Serie-Farbe)?

---

## Referenzen & Abhängigkeiten

- **Bestehende Klassen**: `TVShowEpisode`, `Picture`, `MediaBaseEntry`, `ApplicationDbContext`, `EventManager`
- **Services**: `MediaSourceClassifier`, `MediaSourceScanner`, `VideoWebPlayerBackupDataProvider`
- **UI-Komponenten**: `TVShowDetailsPage.razor` (WebPlayer / Blazor)
- **Frameworks**: Entity Framework Core, System.Drawing / SixLabors.ImageSharp (für Bildverarbeitung)

---

## Akzeptanzkriterien

- [ ] `TVShowEpisode` hat Properties für generiertes Hintergrundbild und Update-Flag
- [ ] `EpisodeBackgroundImageGenerator` erzeugt Hintergrundbilder mit allen geforderten Verarbeitungsschritten
- [ ] `EpisodeBackgroundImageService` implementiert Lazy-Load, Persistierung und Caching
- [ ] Beim Scannen eines neuen Fanarts wird `BackgroundImageRequiresUpdate` korrekt gesetzt
- [ ] Episode-Detailseite zeigt generiertes Hintergrundbild im Kopfbereich mit Schleier-Effekt
- [ ] Generierte Bilder sind aus dem Backup ausgeschlossen oder werden nach Restore ignoriert
- [ ] Mehrere parallele Zugriffe auf die gleiche Episode führen nicht zu Race Conditions (Thread-Safe)
- [ ] Bei fehlerndem Fanart wird Fallback auf Placeholder-Bild angewendet
- [ ] Fehler bei Bildgenerierung werden geloggt, beeinträchtigen aber nicht die Episode-Anzeige
- [ ] In-Memory Cache verhindert redundante Dateizugriffe und Berechnungen
- [ ] Unit Tests für Generator, Service und Integration-Szenarien vorhanden
