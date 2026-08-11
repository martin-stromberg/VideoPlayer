← [Zurück zur Übersicht](index.md)

# Episoden — Datenmodell

## Entitäten

### TVShowEpisode — Neue Eigenschaften

| Eigenschaft | Datentyp | Beschreibung |
|-------------|----------|--------------|
| `GeneratedBackgroundPictureId` | `long?` | Foreign Key zur `Picture` Tabelle. Referenz zum generierten Hintergrundbild. `null`, wenn noch kein Hintergrundbild generiert wurde. |
| `BackgroundImageRequiresUpdate` | `bool` | Flag, das angibt, ob das Hintergrundbild neu generiert werden muss. Wird auf `true` gesetzt, wenn der Scanner ein neues Fanart oder Poster findet. Standard: `false`. |
| `BackgroundImageGeneratedAt` | `DateTime?` | Zeitstempel der letzten erfolgreichen Hintergrundbild-Generierung. `null`, wenn noch nicht generiert. |
| `GeneratedBackgroundPicture` | `Picture?` | Navigation Property zum generierten Picture-Objekt. Ermöglicht Zugriff auf das Bild über EF Core ohne explizites Laden. |

**Beispiel Episoden-Datensatz:**
```
Id: 42
Name: "Pilot"
Number: 1
TVShowSeasonId: 5
Plot: "Ein brillanter Chemiker-Lehrer..."
FanartPictureId: 123 ← Originales Fanart
GeneratedBackgroundPictureId: 456 ← Generiertes Hintergrundbild
BackgroundImageRequiresUpdate: false ← Aktuell, keine Regenerierung nötig
BackgroundImageGeneratedAt: 2026-08-10T14:32:45Z ← Zuletzt generiert
```

### Picture — Neue Eigenschaften

| Eigenschaft | Datentyp | Beschreibung |
|-------------|----------|--------------|
| `IsGeneratedBackground` | `bool` | Kennzeichnung, ob dieses Picture vom `EpisodeBackgroundImageGenerator` automatisch erzeugt wurde (nicht vom Benutzer oder Scanner importiert). Standard: `false`. |
| `EpisodeId` | `long?` | Foreign Key zur Episode. Back-Reference für optimierte Queries beim Löschen alter generierter Bilder. Nur gesetzt, wenn `IsGeneratedBackground == true`. |

**Beispiel Generiertes Picture:**
```
Id: 456
Data: [JPEG-Binärdaten, ~200 KB]
MediaItemId: null ← Kein physisches Medium
Type: "generated-background"
IsGeneratedBackground: true ← Kennzeichnung
EpisodeId: 42 ← Gehört zu Episode 42
CreatedAt: 2026-08-10T14:32:45Z
```

## Beziehungen

```
TVShowEpisode (1) ──────────────── (0..1) Picture
              ↑                          ↑
              └─ GeneratedBackgroundPictureId
                 BackgroundImageRequiresUpdate
                 BackgroundImageGeneratedAt
                 GeneratedBackgroundPicture (Nav)
                 
Picture
  ├─ IsGeneratedBackground = true (nur für generierte)
  └─ EpisodeId (Back-Reference)
```

**Beziehungsbeschreibung:**
- Eine Episode kann höchstens ein generiertes Hintergrundbild haben (0 oder 1)
- Ein generiertes Picture gehört zu genau einer Episode
- Die Beziehung ist von Episode's Seite optional (Episode ohne Fanart und ohne Poster hat kein Hintergrundbild)
- Von Picture's Seite ist `EpisodeId` optional nullable für Backcompat

## Indizes

Zur Optimierung von Queries wurden folgende Indizes erstellt:

| Tabelle | Spalten | Zweck |
|---------|---------|-------|
| `Pictures` | `(EpisodeId, IsGeneratedBackground)` | Schnelles Löschen von Bildern beim Neugenieren |
| `Pictures` | `IsGeneratedBackground` | Schnelles Filtern bei Backup-Export (nur nicht-generierte Bilder) |

## Datenbankmigrationen

Das Datenmodell wurde durch folgende Migrationen erweitert:

### Migration: AddEpisodeBackgroundImageProperties
- Fügt `GeneratedBackgroundPictureId` (long?, FK) zu `TVShowEpisodes` hinzu
- Fügt `BackgroundImageRequiresUpdate` (bool, default false) zu `TVShowEpisodes` hinzu
- Fügt `BackgroundImageGeneratedAt` (datetime2, nullable) zu `TVShowEpisodes` hinzu
- Erstellt Foreign Key Constraint: `TVShowEpisodes.GeneratedBackgroundPictureId` → `Pictures.Id`

### Migration: AddPictureGeneratedBackgroundProperties
- Fügt `IsGeneratedBackground` (bool, default false) zu `Pictures` hinzu
- Fügt `EpisodeId` (long?, nullable, optional FK) zu `Pictures` hinzu
- Erstellt optionalen Foreign Key: `Pictures.EpisodeId` → `TVShowEpisodes.Id`

**Betroffene Tabellen:**
- `TVShowEpisodes`: +3 Spalten
- `Pictures`: +2 Spalten

**Betroffene Datensätze:**
- Existierende Episoden erhalten: `GeneratedBackgroundPictureId = null`, `BackgroundImageRequiresUpdate = false`
- Existierende Pictures erhalten: `IsGeneratedBackground = false`
- Keine Datenlöschung oder Datenbeschädigung

## Backup und Restore

### Backup
Beim Export (Backup erstellen) werden generierte Bilder **ausgeschlossen**:

```sql
-- Backup-Export filtert:
SELECT * FROM Pictures
WHERE IsGeneratedBackground = false
```

**Grund:** Generierte Bilder belegen Speicherplatz und können jederzeit neu erzeugt werden. Das reduziert Backup-Größe um bis zu 20%.

### Restore
Nach dem Restore fehlen generierte Bilder (da nicht im Backup). Beim nächsten Aufruf einer Episode:
- `BackgroundImageRequiresUpdate` wird geprüft
- Falls alte `GeneratedBackgroundPictureId` auf nicht-existierende Picture verweist: Neugenierung
- Bilder werden on-demand neu erzeugt (Lazy-Loading)

**Benutzer-Erlebnis:** Nach Restore: erste Episode-Ladevorgänge dauern ~1 Sekunde länger (Generierung), danach normal.

## Konsistenz und Validierung

| Validierungsregel | Durchsetzung |
|------------------|--------------|
| `GeneratedBackgroundPictureId` muss null sein oder auf existierendes Picture mit `IsGeneratedBackground = true` verweisen | Foreign Key Constraint (EF Core) |
| Wenn `IsGeneratedBackground = true`, sollte `EpisodeId` gesetzt sein | Service-Validierung bei Erstellung |
| Generierung erfolgt nur, wenn `FanartPictureId` oder `PosterPictureId` der Episode gesetzt ist und nutzbare Bilddaten liefert | Implizit: Service generiert nur bei Fanart oder Poster vorhanden (Fanart hat Vorrang) |
| Generierte Pictures sollten nicht gelöscht werden (nur beim Neugenieren) | Service handhabt Löschung in `RemoveObsoleteGeneratedPictureAsync()` |

## Schema-Diagramm

```mermaid
erDiagram
    TVSHOWEPISODE ||--o| PICTURE : "GeneratedBackgroundPicture"
    TVSHOWEPISODE {
        long Id
        int Number
        long TVShowSeasonId
        string Plot
        long "FanartPictureId?"
        long "GeneratedBackgroundPictureId?" FK
        bool BackgroundImageRequiresUpdate
        datetime2 "BackgroundImageGeneratedAt?"
    }
    PICTURE {
        long Id
        long "MediaItemId?"
        string Type
        binary "Data?"
        bool IsGeneratedBackground
        long "EpisodeId?" FK
        datetime2 CreatedAt
    }
```
