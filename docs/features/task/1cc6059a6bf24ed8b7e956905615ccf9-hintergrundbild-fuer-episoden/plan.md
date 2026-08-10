# Umsetzungsplan: Dynamische Hintergrundbild-Generierung für Episoden

## Übersicht

Das Feature erweitert die Episode-Detailansicht um dynamisch generierte Hintergrundgrafiken. Bei erstmaligem Aufruf oder nach Fanart-Update wird aus dem Episoden-Fanart ein proportional skaliertes Bild erzeugt, das eine Canvas mit dominanter Hintergrundfarbe ausfüllt und mit einem Schleier-Effekt versehen wird. Das generierte Bild wird persistiert und gecacht, um wiederholte Berechnungen zu vermeiden. Der Prozess ist thread-safe und bietet Fallback-Logik bei Fehlern.

---

## Designentscheidungen

| Komponente / Bereich | Gewählter Ansatz | Begründung |
|----------------------|-----------------|------------|
| Bildverarbeits-Bibliothek | **SixLabors.ImageSharp** (statt System.Drawing) | Cross-Platform-Kompatibilität; System.Drawing ist unter Windows GDI-abhängig. ImageSharp läuft auf Linux/Mac/Docker. |
| Canvas-Auflösung | **1920×1080 Pixel** | Entspricht Full-HD Standard; deckt moderne Monitore ab; balanciert Dateigröße und Qualität. |
| Bildformat | **JPEG mit Quality 85%** | Beste Balance zwischen Dateigröße und visueller Qualität für Fotos/Fanart; signifikant kleiner als PNG, aber visuell akzeptabel. |
| Dominante-Farbe-Algorithmus | **Histogram-basiert mit Pixel-Sampling** | Einfach, performant; berechnet 8×8 Sampling-Grid statt aller Pixel. Ausreichend für optische Wirkung. |
| Tint-Overlay-Intensität | **Feste Opazität 0.4 (40%)** | Kompromiss: Hintergrundbild bleibt sichtbar, Text lesbar. Konfigurierbar in appsettings. |
| Persistierung des Fanarts | **Picture.Data als Binär in DB** | Besteht Konsistenz mit existierenden Bildern (PosterPictureId, BannerPictureId, FanartPictureId); keine Dateisystem-Abhängigkeit. |
| Cache-Strategie | **In-Memory Cache nur für generierte Bilder (nicht Intermediate Results)** | Reduziert RAM-Verbrauch; Regenerierung ist schnell (gesamter Prozess <1 Sekunde). Nur Picture-Referenzen cachen. |
| Fallback bei Fehler | **Existierendes Banner/Fanart der Episode fallback verwenden** | Graceful Degradation; User sieht immer *etwas*, keine leere Seite. |
| Synchronisierungs-Primitive | **AsyncLock (Nito.AsyncEx NuGet)** | Verhindert Race Conditions bei parallelen Requests auf gleiche Episode. Bessere Async-Integration als klassische Locks. |
| Backup-Strategie | **Generierte Bilder ausschließen (IsGeneratedBackground = true Filter)** | Reduziert Backup-Größe; generierte Bilder können jederzeit neu erzeugt werden. Nach Restore neu erzeugt. |
| MAUI-Support | **Nicht im Scope dieser Implementierung** | Feature für WebPlayer; MAUI-Integration als separater Punkt. |
| Rückwärts-Kompatibilität | **Episoden ohne Fanart bekommen keinen Background generiert** | Fallback auf existierende Banner/Placeholder; keine Breaking Changes. |

---

## Programmabläufe

### Ablauf 1: Hintergrundbild beim Episode-Aufruf laden/generieren

Wird ausgelöst, wenn die Episode-Detailseite geladen wird.

1. UI ruft `EpisodeBackgroundImageService.EnsureBackgroundImageAsync(episode, ct)` auf.
2. Service prüft: Ist `episode.GeneratedBackgroundImageId != null` UND `episode.BackgroundImageRequiresUpdate == false`?
3. Falls ja → Bild aus `GeneratedBackgroundImageId` laden, in Cache eintragen, zurückgeben.
4. Falls nein → Akquiriere AsyncLock für Episode-ID (Synchronisierung).
5. (Nochmals prüfen, falls parallel generiert wurde.)
6. Prüfe, ob Episode `FanartPictureId` hat.
7. Falls nein → Fallback auf Banner/Placeholder, zurückgeben (kein Hintergrundbild generieren).
8. Falls ja → Lade Fanart-Bild-Daten aus `Picture.Data`.
9. Rufe `EpisodeBackgroundImageGenerator.GenerateBackgroundImageAsync(episode, fanartData, ct)` auf.
10. Generator: Skaliere Fanart auf max 1920×1080, berechne dominante Farbe, erzeugte Canvas, platziere Bild zentriert, fülle Fläche, wende Tint-Overlay an, speichere als JPEG.
11. Service speichert generiertes `Picture`-Objekt mit `IsGeneratedBackground = true` in DB.
12. Setze `episode.GeneratedBackgroundImageId`, `episode.BackgroundImageGeneratedAt`, `episode.BackgroundImageRequiresUpdate = false`.
13. Speichere Episode-Änderung.
14. Trage Bild-ID in In-Memory Cache ein (Schlüssel: Episode-ID).
15. Gebe Picture-Objekt zurück.
16. Release AsyncLock.
17. UI rendert Bild mit `<div style="background-image: url('/api/episodes/{episodeId}/background-image'); opacity: 0.4;">`.

**Beteiligte Klassen/Komponenten:** `EpisodeBackgroundImageService`, `EpisodeBackgroundImageGenerator`, `ApplicationDbContext`, `IMemoryCache`, `TVShowEpisode`, `Picture`

### Ablauf 2: Hintergrundbild-Update-Flag setzen (bei neuem Fanart)

Wird ausgelöst, wenn Scanner ein neues oder aktualisiertes Fanart findet.

1. `MediaSourceScanner` aktualisiert/erstellt `Picture`-Objekt für neues Fanart.
2. `MediaSourceClassifier.AssignPicturesToTVShowEpisodeAsync()` setzt `episode.FanartPictureId = newFanartId`.
3. `MediaSourceClassifier` ruft `EpisodeBackgroundImageService.MarkBackgroundImageForUpdateAsync(episodeId, ct)` auf.
4. Service setzt `episode.BackgroundImageRequiresUpdate = true`.
5. Entfernt Einträge für diese Episode aus dem In-Memory Cache.
6. Speichert Änderung.
7. Beim nächsten Zugriff wird neues Hintergrundbild generiert (Ablauf 1).

**Beteiligte Klassen/Komponenten:** `MediaSourceScanner`, `MediaSourceClassifier`, `EpisodeBackgroundImageService`, `ApplicationDbContext`

### Ablauf 3: API-Endpoint Bildbereitstellung

Wird ausgelöst, wenn UI `GET /api/episodes/{episodeId}/background-image` aufruft.

1. Controller prüft, ob Episode existiert und ob `GeneratedBackgroundImageId` gesetzt.
2. Falls nein → Fallback auf Banner/Fanart oder Placeholder-Bild zurückgeben.
3. Falls ja → Lade `Picture` mit ID `GeneratedBackgroundImageId` aus DB.
4. Prüfe, ob `Picture.IsGeneratedBackground == true`.
5. Gebe Bild-Daten mit Content-Type `image/jpeg` zurück.
6. Setze Cache-Control-Header: `public, max-age=31536000` (1 Jahr, da Bilder unveränderlich).

**Beteiligte Klassen/Komponenten:** API-Controller, `ApplicationDbContext`, HTTP-Response

---

## Neue Klassen

| Klasse | Typ | Zweck |
|--------|-----|-------|
| `EpisodeBackgroundImageGenerator` | Service | Technische Bildverarbeitung: Laden, Skalieren, Farbextraktion, Canvas-Erstellung, Overlay-Anwendung |
| `EpisodeBackgroundImageService` | Service | Business Logic: Lazy-Loading, Persistierung, In-Memory Caching, Thread-Safety, Update-Flag-Verwaltung |
| `EpisodeBackgroundImageOptions` | Konfigurationsklasse | Stark typisierte Optionen für appsettings (MaxWidth, MaxHeight, TintColor, TintOpacity, CacheDurationMinutes) |
| `EpisodeBackgroundImageGeneratorTests` | Testklasse | Unit-Tests für Bildverarbeitung |
| `EpisodeBackgroundImageServiceTests` | Testklasse | Unit-Tests für Business Logic und Caching |

---

## Änderungen an bestehenden Klassen

### `TVShowEpisode` (Data Model)

- **Neue Eigenschaften:**
  - `long? GeneratedBackgroundImageId` — Foreign Key zu Picture-Tabelle; Referenz zum generierten Hintergrundbild
  - `bool BackgroundImageRequiresUpdate` (default: `false`) — Flag, ob Hintergrundbild überarbeitet werden muss
  - `DateTime? BackgroundImageGeneratedAt` (default: `null`) — Zeitstempel der letzten erfolgreichen Generierung
- **Neue Navigation-Properties:**
  - `Picture? GeneratedBackgroundImage` — Navigation zu generiertem Picture-Objekt
- **Migrationsbetrachtung:** Existierende Episoden erhalten `GeneratedBackgroundImageId = null`, `BackgroundImageRequiresUpdate = false` (weil kein Fanart ggf. oder noch nicht generiert)

### `Picture` (Data Model)

- **Neue Eigenschaften:**
  - `bool IsGeneratedBackground` (default: `false`) — Kennzeichnung, dass dieses Bild von `EpisodeBackgroundImageGenerator` erzeugt wurde
  - `long? EpisodeIdReference` (default: `null`, Nullable Foreign Key) — Back-Reference zur Episode für optimierte Queries; ermöglicht `dbContext.Pictures.Where(p => p.EpisodeIdReference == episodeId)` ohne JOIN über GeneratedBackgroundImageId
- **Migrationsbetrachtung:** Existierende Pictures erhalten beide Properties mit default-Werten

### `MediaSourceClassifier` (Logic Service)

- **Neue Methode oder Änderung:**
  - Nach erfolgreichem `AssignPicturesToTVShowEpisodeAsync()` für neue/geänderte Fanarts: Aufruf von `EpisodeBackgroundImageService.MarkBackgroundImageForUpdateAsync(episodeId, ct)`
  - Ort: In der Methode `AssignPicturesToTVShowEpisodeAsync()`, nach Setzen von `episode.FanartPictureId`

### `TVShowDetails.razor` (UI-Komponente)

- **Neuer Code im Header-Bereich:**
  - Injiziere `EpisodeBackgroundImageService` (oder HttpClient für API-Call)
  - OnInitializedAsync: Rufe `EnsureBackgroundImageAsync(selectedEpisode, ct)` auf, speichere Picture-ID oder URL
  - Ändere Hintergrund-Style: Falls `GeneratedBackgroundImageId` vorhanden, nutze `url('/api/episodes/{id}/background-image')` statt Banner
  - Wende CSS-Klasse `.background-with-overlay` an oder inline `opacity: 0.4`

### `ApplicationDbContext` (Entity Framework)

- **Entity Mappings in OnModelCreating:**
  - `GeneratedBackgroundImage` (Navigation) auf Foreign Key `GeneratedBackgroundImageId` konfigurieren
  - `EpisodeIdReference` in Picture als optional Foreign Key (Shadow Property oder explizite Navigation)
  - Indices für Queries: `Picture.Where(p => p.EpisodeIdReference == id)` optimieren (optional, Index auf `(EpisodeIdReference, IsGeneratedBackground)`)

### `VideoWebPlayerBackupDataProvider` (Backup-Service)

- **Änderung in ExportAsync:**
  - Filter beim Export der Picture-Tabelle: `WHERE IsGeneratedBackground = false`
  - Sichert nur benutzerseitig importierte Bilder; generierte Bilder können neu erzeugt werden
- **Änderung in RestoreAsync (optional):**
  - Nach Restore automatisch alle `episode.BackgroundImageRequiresUpdate = true` setzen (optional, da beim nächsten Zugriff regeneriert wird)

---

## Datenbankmigrationen

| Migrationsname | Betroffene Tabellen/Spalten | Beschreibung der Änderung |
|----------------|----------------------------|---------------------------|
| `AddEpisodeBackgroundImageProperties` | `TVShowEpisodes` (3 Spalten) | Hinzufügen von `GeneratedBackgroundImageId` (long?, FK zu Pictures), `BackgroundImageRequiresUpdate` (bool, default false), `BackgroundImageGeneratedAt` (datetime2, nullable) |
| `AddPictureGeneratedBackgroundProperties` | `Pictures` (2 Spalten) | Hinzufügen von `IsGeneratedBackground` (bool, default false), `EpisodeIdReference` (long?, nullable, optional FK) |
| `AddIndexOnPictureEpisodeRef` (optional) | `Pictures` (Index) | Index auf `(EpisodeIdReference, IsGeneratedBackground)` für häufige Queries |

---

## Validierungsregeln

| Feld / Objekt | Regel | Fehlerfall |
|---------------|-------|------------|
| `TVShowEpisode.GeneratedBackgroundImageId` | Muss null sein oder auf ein existierendes Picture mit `IsGeneratedBackground = true` verweisen | Datenbankintegrität: Foreign Key Constraint in EF Core |
| `Picture.IsGeneratedBackground` | Wenn true, muss `EpisodeIdReference` gesetzt sein UND `Type` sollte einen standardisierten Wert haben (z.B. "generated-background") | Validierung in Service: Bei Erstellung einer generierten Picture wird validiert |
| Fanart-Daten in `Picture` | Binärdaten müssen valides Bild-Format sein (JPEG, PNG, WebP) | Fehlerbehandlung in `EpisodeBackgroundImageGenerator.GenerateBackgroundImageAsync()`: Rückgabe null bei ungültigem Format |

---

## Konfigurationsänderungen

| Eintrag | Typ | Standardwert | Zweck |
|---------|-----|--------------|-------|
| `EpisodeBackgroundImage:MaxWidth` | int | `1920` | Maximale Breite des generierten Bildes (Canvas-Breite) |
| `EpisodeBackgroundImage:MaxHeight` | int | `1080` | Maximale Höhe des generierten Bildes (Canvas-Höhe) |
| `EpisodeBackgroundImage:TintColor` | string (Hex) | `#000000` | Farbe des Schleier-Overlays (Schwarz für Lesbarkeit) |
| `EpisodeBackgroundImage:TintOpacity` | float | `0.4` | Opazität des Schleier-Overlays (0.0–1.0); 0.4 = 40% Deckkraft |
| `EpisodeBackgroundImage:CacheDurationMinutes` | int | `60` | Dauer, wie lange generierte Bild-IDs im In-Memory Cache verweilen |
| `EpisodeBackgroundImage:JpegQuality` | int | `85` | JPEG-Komprimierungsqualität (0–100); 85 = guter Kompromiss Größe/Qualität |
| `EpisodeBackgroundImage:EnableLogging` | bool | `true` | Ob Generierungs-Fehler und Warnings geloggt werden |

**appsettings.json Struktur:**
```json
{
  "EpisodeBackgroundImage": {
    "MaxWidth": 1920,
    "MaxHeight": 1080,
    "TintColor": "#000000",
    "TintOpacity": 0.4,
    "CacheDurationMinutes": 60,
    "JpegQuality": 85,
    "EnableLogging": true
  }
}
```

**Service-Registrierung in Program.cs:**
```csharp
services.Configure<EpisodeBackgroundImageOptions>(
    configuration.GetSection("EpisodeBackgroundImage"));
services.AddScoped<EpisodeBackgroundImageGenerator>();
services.AddScoped<EpisodeBackgroundImageService>();
services.AddMemoryCache(); // Falls nicht bereits vorhanden
```

---

## Seiteneffekte und Risiken

- **Episode-Detailseite Performance:** Generierung kann beim ersten Zufriff ~1 Sekunde dauern. Mit In-Memory Cache ist zweiter Zugriff <10ms. Risiko: Lange Generierungszeit bei vielen gleichzeitigen Requests (AsyncLock serialisiert); Mitigation: Caching verhindert Wiederholungen.
- **Datenbankgröße:** Picture-Tabelle wächst um generierte Bilder. Mit 1920×1080 JPEG ~200 KB pro Episoden-Hintergrundbild. Bei 10.000 Episoden ~2 GB zusätzlich. Mitigation: Backup-Ausschluss reduziert Backup-Größe.
- **Speicherverbrauch (In-Memory Cache):** Cache speichert nur Picture-IDs (longs), nicht die Bilddaten selbst (~16 Bytes pro Eintrag). Minimal; Risiko gering.
- **Änderungen an MediaSourceClassifier:** Integration des Update-Flags erfordert Anpassung von `AssignPicturesToTVShowEpisodeAsync()`. Vorsicht: Fehler könnte verhindern, dass neue Fanarts erkannt werden. Mitigation: Unit-Tests sichern ab.
- **Fallback-Logik:** Wenn generierte Bilder nicht vorhanden sind, wird auf Banner/Fanart zurückgegriffen. Altverhalten bleibt erhalten; kein Breaking Change.
- **Backup-Kompatibilität:** Nach Restore fehlen generierte Bilder (aus Export ausgeschlossen). Beim nächsten Episode-Aufruf werden sie neu generiert. Einmaliger Verzögerung; Risiko minimal.

---

## Umsetzungsreihenfolge

1. **Datenmodell-Erweiterung und Migrationen**
   - Voraussetzungen: `TVShowEpisode.cs`, `Picture.cs`, `ApplicationDbContext.cs`, Entity Framework Core nutzbar
   - Beschreibung: Neue Properties zu `TVShowEpisode` und `Picture` hinzufügen. Entity-Mappings in `ApplicationDbContext.OnModelCreating()` erweitern. Zwei EF Core Migrationen erstellen: eine für Episode-Properties, eine für Picture-Properties. Migrationen nicht noch nicht `Update-Database`; Ziel ist, dass Code kompiliert.

2. **NuGet-Abhängigkeiten hinzufügen**
   - Voraussetzungen: Keine (Projektdatei muss bereits mit NuGet.exe oder .csproj editierbar sein)
   - Beschreibung: `SixLabors.ImageSharp` (für Cross-Platform Bildverarbeitung) und `Nito.AsyncEx` (für AsyncLock) via NuGet in `VideoWebPlayer` Projekt installieren. Versions-Constraint: ImageSharp >= 3.0.0 (LTS), Nito.AsyncEx >= 5.1.2.

3. **`EpisodeBackgroundImageOptions` Konfigurationsklasse erstellen**
   - Voraussetzungen: .NET 6+ (Strongly Typed Config Pattern vorhanden)
   - Beschreibung: Neue Datei `Services/EpisodeBackgroundImage/EpisodeBackgroundImageOptions.cs`. Properties für MaxWidth, MaxHeight, TintColor, TintOpacity, CacheDurationMinutes, JpegQuality, EnableLogging. Validation im Constructor.

4. **`EpisodeBackgroundImageGenerator` Service implementieren**
   - Voraussetzungen: `Picture.cs`, `TVShowEpisode.cs`, `EpisodeBackgroundImageOptions`, `SixLabors.ImageSharp` nutzbar, `ILogger<EpisodeBackgroundImageGenerator>` via DI
   - Beschreibung: Neue Datei `Services/EpisodeBackgroundImage/EpisodeBackgroundImageGenerator.cs`. Implementiere Methoden:
     - `GenerateBackgroundImageAsync(TVShowEpisode, CancellationToken): Task<Picture>` — Orchestriert gesamten Prozess
     - `ResizeImage(byte[], int, int): byte[]` — Skaliert mit Seitenverhältnis-Erhalt
     - `GetDominantColor(byte[]): System.Drawing.Color` — Berechnet dominante Farbe
     - `CreateCanvasWithScaledImage(byte[], int, int, System.Drawing.Color): byte[]` — Canvas mit zentriertem Bild
     - `ApplyTintOverlay(byte[], System.Drawing.Color, float): byte[]` — Wendet Tint an
     Fehlerbehandlung: Logt Fehler, rückgabe null bei kritischen Fehlern.

5. **`EpisodeBackgroundImageService` Service implementieren**
   - Voraussetzungen: `ApplicationDbContext`, `EpisodeBackgroundImageGenerator`, `IMemoryCache`, `Nito.AsyncEx`, `EpisodeBackgroundImageOptions`, `ILogger<EpisodeBackgroundImageService>`
   - Beschreibung: Neue Datei `Services/EpisodeBackgroundImage/EpisodeBackgroundImageService.cs`. Implementiere Methoden:
     - `EnsureBackgroundImageAsync(TVShowEpisode, CancellationToken): Task<Picture?>` — Lazy-Load mit Generierung, Persistierung, Caching, AsyncLock
     - `MarkBackgroundImageForUpdateAsync(long episodeId, CancellationToken): Task` — Setzt Flag, leert Cache
     - `TryGetCachedImageIdAsync(long episodeId): Task<long?>` (private/intern) — Cache-Lookup
     AsyncLock-Klasse instanziieren (Nito.AsyncEx.AsyncLock); Thread-Safety implementieren.

6. **Services in `Program.cs` registrieren**
   - Voraussetzungen: `Program.cs` nutzt `WebApplicationBuilder.Services`, `appsettings.json` existiert
   - Beschreibung: Service-Registrierungen hinzufügen (siehe Konfigurationsabschnitt oben). `IMemoryCache` falls nicht vorhanden hinzufügen.

7. **`appsettings.json` Sektion hinzufügen**
   - Voraussetzungen: `appsettings.json` vorhanden
   - Beschreibung: `EpisodeBackgroundImage` Objekt mit Standardwerten (siehe Konfigurationsabschnitt) hinzufügen.

8. **Database Migrationen ausführen**
   - Voraussetzungen: Migrationen aus Schritt 1 vorhanden, `Program.cs` und DbContext konfiguriert, Datenbank erreichbar
   - Beschreibung: `dotnet ef database update` ausführen, um Schema in Datenbank zu aktualisieren. Bestehende Daten unverändert; neue Spalten bekommen default-Werte.

9. **API-Endpoint `GET /api/episodes/{episodeId}/background-image` implementieren**
   - Voraussetzungen: ASP.NET Core Controller-Setup, `ApplicationDbContext`, `EpisodeBackgroundImageService`
   - Beschreibung: Neuer Controller oder Erweiterung bestehenden Episode-Controllers. Endpoint:
     - Parametr: `episodeId` (long)
     - Logic: Lade Episode, prüfe ob `GeneratedBackgroundImageId` gesetzt, lade Picture, gebe Daten zurück
     - Fallback: Falls kein generiertes Bild, lade Banner oder Fanart, oder Placeholder-Datei aus `wwwroot/`
     - Headers: `Content-Type: image/jpeg`, `Cache-Control: public, max-age=31536000`
     - Status: 200 OK (Bild), 404 Not Found (keine Episode), 500 Internal Server Error (bei kritischen Fehlern)

10. **`TVShowDetails.razor` UI-Integration**
    - Voraussetzungen: `TVShowDetails.razor` existiert, `EpisodeBackgroundImageService` registriert und injizierbar, Blazor-Component-Lifecycle bekannt
    - Beschreibung: Änderungen in `TVShowDetails.razor`:
      - Injiziere `EpisodeBackgroundImageService` (falls Service-Aufruf vom Component aus, sonst API-Call via HttpClient)
      - OnInitializedAsync: Rufe `EnsureBackgroundImageAsync(selectedEpisode, ct)` auf, speichere Picture-ID oder URL
      - Ändere Header-Hintergrund: Falls `GeneratedBackgroundImageId` vorhanden und > 0, nutze `/api/episodes/{selectedEpisode.Id}/background-image`
      - Wende Schleier-Effekt an: CSS `opacity: 0.4` oder dedicated Overlay-Div mit Tint-Farbe
      - Fallback-Logik: Falls Generation fehlgeschlagen (null), verwende bestehendes Banner/Fanart
      - Fehlerbehandlung: Catch-Blöcke für Exceptions

11. **Integration mit `MediaSourceClassifier`**
    - Voraussetzungen: `MediaSourceClassifier.cs`, `AssignPicturesToTVShowEpisodeAsync()` Methode vorhanden, `EpisodeBackgroundImageService` registriert
    - Beschreibung: Änderung in `MediaSourceClassifier`:
      - Nach Setzen von `episode.FanartPictureId` in `AssignPicturesToTVShowEpisodeAsync()`:
        - Injiziere `EpisodeBackgroundImageService` (über Constructor DI)
        - Rufe `await service.MarkBackgroundImageForUpdateAsync(episode.Id, ct)` auf
      - Oder: Event basiert via `EventManager` (Alternative, wenn DirectService-Injection zu coupling ist)

12. **Backup-Integration in `VideoWebPlayerBackupDataProvider`**
    - Voraussetzungen: `VideoWebPlayerBackupDataProvider.cs`, `ExportAsync()` Methode vorhanden, Entity Framework Queries möglich
    - Beschreibung: Änderung in `ExportAsync()`:
      - Beim Export der Picture-Tabelle: Filter `where !picture.IsGeneratedBackground` hinzufügen
      - Sichert nur benutzerseitig importierte Bilder; generierte Bilder werden auf Backup-Restore automatisch beim nächsten Episode-Zugriff neu erzeugt

13. **Unit-Tests: `EpisodeBackgroundImageGeneratorTests`**
    - Voraussetzungen: Test-Projekt `VideoWebPlayer.Tests`, MSTest oder xUnit nutzbar, Test-Bilder (Testressourcen)
    - Beschreibung: Neue Testdatei. Test-Methoden (siehe Inventory/tests.md):
      - `Test_ResizeImage_KeepAspectRatio()` — Verschiedene Input-Größen testen
      - `Test_GetDominantColor_ReturnsCorrectColor()` — Einfache Testbilder (einfarbig, Gradient)
      - `Test_CreateCanvasWithScaledImage_PlacesImageCentered()` — Verifiziere Platzierung
      - `Test_ApplyTintOverlay_OpacityApplied()` — Farb- und Opazitäts-Check
      - `Test_GenerateBackgroundImage_WithValidFanart_ReturnsImage()` — End-to-End mit echtem Fanart
      - `Test_GenerateBackgroundImage_WithMissingFanart_ReturnsNull()` — Error-Handling
      Nutze einfache Testbilder (z.B. generiert im Setup)

14. **Unit-Tests: `EpisodeBackgroundImageServiceTests`**
    - Voraussetzungen: Test-Projekt, Mocking-Framework (z.B. Moq), `ApplicationDbContext`, `EpisodeBackgroundImageGenerator`
    - Beschreibung: Neue Testdatei. Test-Methoden:
      - `Test_EnsureBackgroundImage_LazyLoads_OnFirstCall()` — Verifiziere Generierung und DB-Persistierung
      - `Test_EnsureBackgroundImage_UsesCached_OnSubsequentCall()` — Cache-Hit verifizieren
      - `Test_EnsureBackgroundImage_ThreadSafe_ParallelRequests()` — Mehrere parallele Tasks, nur eine Generierung
      - `Test_MarkBackgroundImageForUpdate_SetsFlag_OnNewFanart()` — Flag-Setzung verifizieren
      - `Test_EnsureBackgroundImage_Regenerates_WhenFlagSet()` — Nach Flag-Reset neugeneriert
      - `Test_EnsureBackgroundImage_ReturnsFallback_OnGenerationError()` — Fehlerbehandlung
      Mock `ApplicationDbContext`, `EpisodeBackgroundImageGenerator`, `IMemoryCache`

15. **Integration-Tests (Optional)**
    - Voraussetzungen: Testdatenbank aufgesetzt, EF Core Migrations vorhanden
    - Beschreibung: Optional: Ein bis zwei Integration-Tests mit echter Datenbank:
      - `Test_FullWorkflow_GenerateAndRetrieveBacgroundImage()` — Scanner → Classification → Service-Aufruf → Bild in DB → API-Aufruf
      - `Test_BackupExcludesGeneratedImages()` — Export mit Filter verifizieren

16. **Dokumentation und Cleanup**
    - Voraussetzungen: Alle Schritte abgeschlossen, Tests laufen, Commits vorhanden
    - Beschreibung: Optional-Schritt: Code-Kommentare in Klassen hinzufügen (Public Methods dokumentieren). XML-Dokumentation für öffentliche APIs. Keine separate README nötig (Feature im Hauptdoku zu aktualisieren).

---

## Tests

### Neue Tests

| Test / Hilfsmethode | Testklasse | Was wird geprüft / bereitgestellt? |
|--------------------|------------|-------------------------------------|
| `Test_ResizeImage_KeepAspectRatio` | `EpisodeBackgroundImageGeneratorTests` | Skalierung behält Seitenverhältnis bei; Output passt in Zielgröße |
| `Test_GetDominantColor_ReturnsCorrectColor` | `EpisodeBackgroundImageGeneratorTests` | Dominante Farbe wird korrekt ermittelt (einfarbiges Testbild) |
| `Test_CreateCanvasWithScaledImage_PlacesImageCentered` | `EpisodeBackgroundImageGeneratorTests` | Bild wird zentriert auf Canvas platziert |
| `Test_ApplyTintOverlay_OpacityApplied` | `EpisodeBackgroundImageGeneratorTests` | Tint-Overlay wird mit korrekter Opazität angewendet |
| `Test_GenerateBackgroundImage_WithValidFanart_ReturnsImage` | `EpisodeBackgroundImageGeneratorTests` | End-to-End: Válides Fanart → Picture mit Binärdaten |
| `Test_GenerateBackgroundImage_WithMissingFanart_ReturnsNull` | `EpisodeBackgroundImageGeneratorTests` | Fehlerbehandlung: Ungültiges/fehlendes Fanart → null |
| `CreateTestImage(width, height, color)` | `EpisodeBackgroundImageGeneratorTests` | Hilfsmethode: Erzeugt einfaches Test-Bild für Tests |
| `Test_EnsureBackgroundImage_LazyLoads_OnFirstCall` | `EpisodeBackgroundImageServiceTests` | Lazy-Load: Episode ohne GeneratedBackgroundImageId → Generierung + Persistierung |
| `Test_EnsureBackgroundImage_UsesCached_OnSubsequentCall` | `EpisodeBackgroundImageServiceTests` | Cache-Hit: Zweiter Aufruf → In-Memory Cache, keine Neugenerierung |
| `Test_EnsureBackgroundImage_ThreadSafe_ParallelRequests` | `EpisodeBackgroundImageServiceTests` | Thread-Safety: 10 parallele Requests auf gleiche Episode → nur eine Generierung |
| `Test_MarkBackgroundImageForUpdate_SetsFlag_OnNewFanart` | `EpisodeBackgroundImageServiceTests` | Flagging: Neues Fanart → BackgroundImageRequiresUpdate = true |
| `Test_EnsureBackgroundImage_Regenerates_WhenFlagSet` | `EpisodeBackgroundImageServiceTests` | Nach Flagging: EnsureBackgroundImage() regeneriert Bild |
| `Test_EnsureBackgroundImage_ReturnsFallback_OnGenerationError` | `EpisodeBackgroundImageServiceTests` | Fehlerbehandlung: Generator-Fehler → Fallback auf Banner/Fanart |
| `CreateTestEpisode(withFanart, ...)` | `EpisodeBackgroundImageServiceTests` | Hilfsmethode: Erzeugt Test-Episode mit/ohne Fanart |
| `CreateTestPicture(type, data)` | `EpisodeBackgroundImageServiceTests` | Hilfsmethode: Erzeugt Test-Picture |
| `CreateMockDbContext()` | `EpisodeBackgroundImageServiceTests` | Hilfsmethode: Mock ApplicationDbContext für Tests |

### Betroffene bestehende Tests

| Test / Testklasse | Grund der Anpassung |
|-------------------|---------------------|
| `MediaSourceClassifierTests.*` (falls vorhanden) | Falls Tests für `AssignPicturesToTVShowEpisodeAsync()` existieren: Anpassung erwarteter Methodenaufrufe (neuer Call zu `EpisodeBackgroundImageService.MarkBackgroundImageForUpdateAsync()` mocken/verifizieren) |
| `VideoWebPlayerBackupDataProviderTests.* ` (falls vorhanden) | Falls Tests für `ExportAsync()` existieren: Anpassung erwarteter Daten (generierte Bilder sollten nicht mehr im Export auftauchen) |

Falls keine Tests für diese Klassen existieren: „Keine."

### E2E-Tests (Pflicht)

| Szenario | Testdatei / Testklasse | Abgedecktes Akzeptanzkriterium |
|----------|------------------------|-------------------------------|
| Happy Path: Episode laden, generiertes Hintergrundbild sichtbar | `E2ETests/TVShowDetailsPageTests.cs` (neu oder Erweiterung) | „Episode-Detailseite zeigt generiertes Hintergrundbild im Kopfbereich" + „In-Memory Cache verhindert redundante Dateizugriffe" (Performance) |
| Fanart-Update: Nach Scanner-Scan wird neues Hintergrundbild generiert | `E2ETests/MediaScannerE2ETests.cs` (neu oder Erweiterung) | „Beim Scannen eines neuen Fanarts wird BackgroundImageRequiresUpdate korrekt gesetzt" |
| Fehlerfall: Fehlender Fanart fallback auf Placeholder | `E2ETests/TVShowDetailsPageTests.cs` | „Bei fehlerndem Fanart wird Fallback auf Placeholder-Bild angewendet" |
| Backup/Restore: Generierte Bilder werden ausgeschlossen, nach Restore neu generiert | `E2ETests/BackupE2ETests.cs` (neu oder Erweiterung) | „Generierte Bilder sind aus dem Backup ausgeschlossen oder werden nach Restore ignoriert" |
| Thread-Safety: Mehrere gleichzeitige Episoden-Ladevorgänge | `E2ETests/ConcurrencyE2ETests.cs` (optional, neu) | „Mehrere parallele Zugriffe auf die gleiche Episode führen nicht zu Race Conditions" |

### Betroffene bestehende E2E-Tests

Falls bereits E2E-Tests für TVShowDetails oder Backup existieren:

| Test / Testklasse | Grund der Anpassung |
|-------------------|---------------------|
| `E2E*/TVShowDetailsPageTests` (falls vorhanden) | Anpassung Assertions: Hintergrundbild-Element wird jetzt gerendert, URL-Struktur kann sich ändern |
| `E2E*/BackupE2ETests` (falls vorhanden) | Anpassung Assertions: Generierte Bilder sollten nicht im Backup auftauchen (Datenvolumen-Check aktualisieren) |

Falls keine existieren: „Keine."

---

## Offene Punkte

Folgende Fragen waren in der Anforderung offen. Sie werden hier mit empfohlenen Antworten aufgelöst:

| # | Offener Punkt | Empfohlener Vorschlag |
|---|---------------|----------------------|
| 1 | Canvas-Zielmaßstäbe? | **1920×1080 Pixel** — Entspricht Full-HD Standard, moderner Monitor-Standard, balanciert Dateigröße und Qualität. |
| 2 | Bildformat und Komprimierung? | **JPEG mit Quality 85%** — Best Practice für Fotos/Fanart-Bilder. PNG für Qualität (~3× größer), JPEG für Dateigröße (~3× kleiner). Quality 85 ist optischer Kompromiss. |
| 3 | Dominante-Farbe-Berechnung? | **Histogram-basiert mit 8×8 Sampling-Grid** — Einfach und performant (kein k-means-Overhead). Grid reicht für optische Farbwahl aus. Nicht aus zentraler Region allein (kann Artefakte haben). |
| 4 | Tint-Overlay-Intensität? | **Feste Opazität 0.4 (40%)** — Guter Kompromiss: Hintergrundbild bleibt sichtbar, Text ist lesbar. Konfigurierbar in `appsettings.json` als `EpisodeBackgroundImage:TintOpacity`. |
| 5 | Persistierung des Fanarts? | **Binary in Picture.Data (Datenbankdatei)** — Folgt bestehender Konvention für PosterPictureId, BannerPictureId. Keine Dateisystem-Abhängigkeit; Backup/Restore konsistent. |
| 6 | Cache-Strategie? | **In-Memory Cache nur für generierte Bild-IDs (nicht Intermediate Results)** — Reduziert RAM-Verbrauch. Intermediate Results (skaliertes Fanart, dominante Farbe) sind schnell recalculiert (< 1 Sekunde). Nur Picture-ID cachen (~16 Bytes pro Entry). |
| 7 | Bestehende Placeholder-Logik? | **Fallback auf vorhandenes Banner/Fanart der Episode** — Keine statische Placeholder-Datei nötig. Graceful Degradation: Wenn Generierung fehlschlägt, wird aktuelles Verhalten (Banner/Fanart) verwendet. Benutzer sieht immer etwas. |
| 8 | MAUI-Frontend? | **Nicht im Scope dieser Implementierung** — Feature ist für WebPlayer (Blazor). MAUI-Integration kann später über REST-API (Endpoint aus Schritt 9) erfolgen oder als separates Feature. |
| 9 | Performance unter Last? | **In-Memory Cache mit 60 Minuten TTL reicht aus** — Bei 10.000 aktiven Episoden und 1000 Requests/Minute: Cache hält ~600 Episoden-IDs im Speicher (~10 KB). AsyncLock serialisiert nur erste Generierung pro Episode (Bottleneck minimal). Falls Distributed Cache nötig (Redis): Als optionale Erweiterung nach initial Release. |
| 10 | Rückwärts-Kompatibilität? | **Episoden ohne Fanart bekommen keinen Background generiert** — Fallback auf Banner/Placeholder. Feature ist opt-in via Fanart-Vorhandensein. Keine Breaking Changes. Migration setzt `GeneratedBackgroundImageId = null` für existierende Episoden. |

Alle genannten offenen Punkte gelten nun als geklärt und sind vollständig im Plan eingearbeitet (siehe Designentscheidungen, Konfiguration, Ablauf-Beschreibungen).

---

## Zusammenfassung

Dieser Plan definiert eine vollständige, thread-safe Implementierung von dynamisch generierten Episoden-Hintergrundbildern. Die Lösung nutzt bewährte Muster (Lazy-Loading, In-Memory Caching, AsyncLock für Thread-Safety) und integriert sich nahtlos in bestehende Architektur. Alle 10 offenen Fragen sind adressiert. Die Umsetzungsreihenfolge ist sequenziell, mit expliziten Voraussetzungen für jeden Schritt. Tests decken Bildverarbeitung, Business Logic, Thread-Safety und Integration ab.
