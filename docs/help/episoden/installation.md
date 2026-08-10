← [Zurück zur Übersicht](index.md)

# Episoden — Installation & Konfiguration

## Voraussetzungen

- VideoWebPlayer v1.0+ mit Entity Framework Core
- NuGet-Pakete: `SixLabors.ImageSharp` (≥3.0.0), `Nito.AsyncEx` (≥5.1.2)
- Datenbank gemäß Migrationen aktualisiert
- Mindestens 500 MB freier Datenbankplatz für generierte Bilder (bei 10.000 Episoden)

## Konfiguration in appsettings.json

Die Hintergrundbild-Generierung wird über folgende Konfigurationsabschnitt gesteuert:

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

### Konfigurationsparameter

| Parameter | Datentyp | Standardwert | Beschreibung | Auswirkungen |
|-----------|----------|--------------|--------------|--------------|
| `MaxWidth` | int | 1920 | Maximale Breite des generierten Hintergrundbildes (Canvas-Breite) in Pixeln | Größere Werte → höhere Qualität & Dateigröße. 1920 = Full-HD Standard |
| `MaxHeight` | int | 1080 | Maximale Höhe des generierten Hintergrundbildes (Canvas-Höhe) in Pixeln | Größere Werte → höhere Qualität & Dateigröße. 1080 = Full-HD Standard |
| `TintColor` | string (Hex) | `#000000` | Farbe des Schleier-Overlays (Schwarz für bessere Textlesbarkeit) | `#000000` = schwarz, `#FFFFFF` = weiß, `#808080` = grau. Wird mit TintOpacity kombiniert |
| `TintOpacity` | float | 0.4 | Deckkraft des Schleier-Overlays als Dezimalzahl zwischen 0.0 (transparent) und 1.0 (opak) | 0.4 = 40% dunkel. Größere Werte → Text lesbarer, Hintergrundbild weniger sichtbar |
| `CacheDurationMinutes` | int | 60 | Wie lange (in Minuten) generierte Bild-IDs im In-Memory Cache verweilen | Größere Werte → weniger DB-Zugriffe bei wiederholten Episoden-Aufrufen. 60 Min reicht für typische Nutzung |
| `JpegQuality` | int | 85 | JPEG-Kompressions-Qualität (0–100); 85 ist guter Kompromiss | 90+ = beste Qualität, ~300 KB/Bild; 85 = gut, ~200 KB/Bild; 70 = kleiner, ~150 KB/Bild, aber sichtbare Artefakte |
| `EnableLogging` | bool | true | Ob Fehler und Warnungen bei Bildgenerierung geloggt werden | `true` = Fehler in Logs sichtbar (hilft Debugging); `false` = stumm bei Fehlern |

### Beispiel: Entwicklungs-Konfiguration

In `appsettings.Development.json` können Sie eine andere Konfiguration verwenden:

```json
{
  "EpisodeBackgroundImage": {
    "MaxWidth": 960,
    "MaxHeight": 540,
    "JpegQuality": 70,
    "EnableLogging": true
  }
}
```

Dies spart Datenbankplatz und Generierungszeit während der Entwicklung.

### Beispiel: Performance-Optimierung

Für große Installationen mit vielen Benutzern:

```json
{
  "EpisodeBackgroundImage": {
    "MaxWidth": 1920,
    "MaxHeight": 1080,
    "JpegQuality": 80,
    "CacheDurationMinutes": 120
  }
}
```

Dies erhöht Cache-Dauer und reduziert DB-Abfragen.

## Service-Registrierung

Die Services werden in `Program.cs` registriert (bereits automatisch konfiguriert bei Deployment):

```csharp
// In Program.cs, im Services-Aufbau:
services.Configure<EpisodeBackgroundImageOptions>(
    configuration.GetSection("EpisodeBackgroundImage"));
services.AddScoped<EpisodeBackgroundImageGenerator>();
services.AddScoped<EpisodeBackgroundImageService>();
services.AddMemoryCache(); // Fallback, falls nicht bereits vorhanden
```

## Datenbankmigrationen ausführen

Nach dem Deployment müssen die Datenbank-Migrationen ausgeführt werden:

```bash
dotnet ef database update
```

Dies führt folgende Änderungen durch:
- Fügt Spalten zu `TVShowEpisodes` hinzu
- Fügt Spalten zu `Pictures` hinzu
- Erstellt Indizes für optimierte Queries

**Sicherheitshinweis:** Migrationen sind additive (fügen nur hinzu). Bestehende Daten werden nicht verändert oder gelöscht.

## Überprüfung der Installation

### 1. Datenbank-Spalten prüfen

Stellen Sie sicher, dass neue Spalten existieren:

```sql
-- Für SQL Server:
SELECT COLUMN_NAME FROM INFORMATION_SCHEMA.COLUMNS 
WHERE TABLE_NAME = 'TVShowEpisodes' 
AND COLUMN_NAME IN ('GeneratedBackgroundPictureId', 'BackgroundImageRequiresUpdate', 'BackgroundImageGeneratedAt');

SELECT COLUMN_NAME FROM INFORMATION_SCHEMA.COLUMNS 
WHERE TABLE_NAME = 'Pictures' 
AND COLUMN_NAME IN ('IsGeneratedBackground', 'EpisodeId');
```

Erwartet: 3 neue Spalten in `TVShowEpisodes`, 2 neue in `Pictures`.

### 2. Service-Registrierung prüfen

Die Services sollten registriert und injizierbar sein. Dies wird automatisch beim Starten der Anwendung überprüft.

### 3. Episode-Seite aufrufen

1. Melden Sie sich an
2. Navigieren Sie zu einer TV-Show mit Episoden
3. Wählen Sie eine Episode mit Fanart
4. Kontrollieren Sie die Entwickler-Konsole des Browsers (F12)
5. Der API-Call `GET /api/episodes/{episodeId}/background-image` sollte erfolgen
6. Status 200 mit Bild-Daten oder Status 404/500 (falls Fehler)

### 4. Logs prüfen

Bei aktiviertem `EnableLogging`:
- Logs sollten `EpisodeBackgroundImageService` oder `EpisodeBackgroundImageGenerator` enthalten
- Fehler-Logs zeigen Details bei Problemen
- Info-Logs zeigen Generierungs-Start

## Häufige Konfigurationsfälle

### Großes Datenvolumen (10.000+ Episoden)

```json
{
  "EpisodeBackgroundImage": {
    "MaxWidth": 1600,
    "MaxHeight": 900,
    "JpegQuality": 80,
    "CacheDurationMinutes": 240
  }
}
```

### Speicherplatz-Beschränkung

```json
{
  "EpisodeBackgroundImage": {
    "MaxWidth": 1280,
    "MaxHeight": 720,
    "JpegQuality": 75
  }
}
```

Pro Bild: ~100 KB statt 200 KB → bei 10.000 Episoden: 1 GB statt 2 GB Einsparung.

### Qualität vor Größe

```json
{
  "EpisodeBackgroundImage": {
    "MaxWidth": 1920,
    "MaxHeight": 1080,
    "JpegQuality": 95,
    "CacheDurationMinutes": 120
  }
}
```

Pro Bild: ~300–400 KB, aber visuelle Qualität maximal.

## Fehlerbehebung bei Installation

**Problem:** Migrationen lassen sich nicht ausführen
- **Lösung:** Datenbank-Verbindungsstring in `appsettings.json` überprüfen; Berechtigungen der Datenbankverbindung prüfen

**Problem:** Service nicht registriert
- **Lösung:** `Program.cs` überprüfen auf Service-Registrierungen; Anwendung neu starten

**Problem:** Generierte Bilder erscheinen nicht
- **Lösung:** Logs überprüfen; Episode ein Fanart haben; `EpisodeBackgroundImageGenerator` lädt korrekt

**Problem:** Zu hohe Datenbankgröße
- **Lösung:** `MaxWidth`/`MaxHeight` oder `JpegQuality` reduzieren; Alte generierte Bilder manuell löschen (Query mit `IsGeneratedBackground = true`)
