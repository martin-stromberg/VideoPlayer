← [Zurück zur Übersicht](index.md)

# Episoden — Fehlerbehebung

## Hintergrundbild wird nicht angezeigt

**Symptom:** Episode-Detailseite wird geladen, aber das generierte Hintergrundbild fehlt oder es wird das Standard-Banner angezeigt statt des generierten Bildes.

**Mögliche Ursachen:**

1. **Episode hat weder Fanart noch Poster**
   - Das Feature generiert Hintergrundbilder aus dem Fanart; ist keines vorhanden, wird ersatzweise das Poster verwendet
   - Lösung: Fanart oder Poster für die Episode hinzufügen (via Scanner oder manuell)

2. **Generierung war bei erstem Aufruf fehlgeschlagen**
   - Bildverarbeitung konnte Fanart/Poster nicht laden oder verarbeiten
   - Logs überprüfen: `EpisodeBackgroundImageGenerator` mit Error-Level
   - Lösung: `BackgroundImageRequiresUpdate` für die Episode auf `true` setzen (via DB-Query) → Neugenierung beim nächsten Aufruf

3. **API-Endpoint nicht erreichbar**
   - Browser kann `GET /api/episodes/{episodeId}/background-image` nicht aufrufen
   - Überprüfung: Browser F12 → Network → Request nachschauen
   - Lösung: Authentifizierungs-Token überprüfen; API-Endpoint existiert in EpisodesController

4. **Berechtigungen/Authentication fehlt**
   - Endpoint benötigt Authentifizierung
   - Lösung: Sicherstellen, dass Benutzer angemeldet ist; Token wird automatisch an URL angehängt

**Debugging:**
```sql
-- Überprüfen, ob Hintergrundbild generiert wurde:
SELECT e.Id, e.Name, e.GeneratedBackgroundPictureId, e.BackgroundImageRequiresUpdate, e.BackgroundImageGeneratedAt
FROM TVShowEpisodes e
WHERE e.Id = 42; -- Episode ID ersetzen
```

Falls `GeneratedBackgroundPictureId IS NULL`: Picture wurde nicht generiert
Falls `BackgroundImageRequiresUpdate = 1`: Regenerierung erforderlich

---

## Generierung dauert sehr lange

**Symptom:** Beim ersten Aufruf einer Episode braucht die Seite 5–10 Sekunden zum Laden statt der erwarteten 1 Sekunde.

**Mögliche Ursachen:**

1. **Datenbankperformance-Problem**
   - Abfrage zum Laden des Fanarts ist langsam
   - Lösung: Index auf `Pictures` überprüfen; Fanart-Bild-Daten prüfen (sollte <10 MB sein)

2. **Bildverarbeitung ist ineffizient**
   - Sehr großes Fanart-Bild (>10 MB) wird skaliert
   - Lösung: Fanart-Bild von hoher auf normale Auflösung reduzieren (z. B. 2000×3000 statt 4000×6000)

3. **Parallelisierung durch AsyncLock**
   - Mehrere Requests auf gleiche Episode verzögern sich gegenseitig
   - Das ist gewolltes Verhalten (verhindert doppelte Generierung)
   - Lösung: Normalerweise nur beim ersten Request einer Episode ein Problem

4. **Server-Ressourcen begrenzt**
   - CPU- oder RAM-Mangel bei Bildverarbeitung
   - Lösung: Canvas-Größe reduzieren (`MaxWidth`/`MaxHeight` in appsettings.json)

**Optimierungen:**
```json
{
  "EpisodeBackgroundImage": {
    "MaxWidth": 1280,
    "MaxHeight": 720,
    "JpegQuality": 75
  }
}
```

Dies spart Generierungszeit um ~40–50%.

---

## Fehler bei Bildgenerierung (Error-Log)

**Symptom:** Logs enthalten Einträge wie:
```
ERROR: Fehler bei der Generierung des Hintergrundbilds für Episode {EpisodeId}.
Exception: System.ArgumentException: Ungültiges Bildformat
```

**Mögliche Ursachen:**

1. **Fanart-Daten beschädigt**
   - Binärdaten in `Picture.Data` ist kein gültiges Bild-Format
   - Lösung: Fanart neu importieren (Scanner erneut ausführen)

2. **Bildformat nicht unterstützt**
   - ImageSharp unterstützt JPEG, PNG, WebP; TIFF, BMP nur teilweise
   - Lösung: Fanart in JPEG oder PNG konvertieren

3. **Speicher-Mangel**
   - Systemspeicher erschöpft bei Verarbeitung sehr großer Bilder
   - Lösung: Server-RAM erhöhen oder Canvas-Größe reduzieren

4. **Overlay-Farbe ungültig**
   - `TintColor` in appsettings.json hat ungültiges Hex-Format
   - Lösung: Format überprüfen: `#RRGGBB` z. B. `#000000`

**Logging aktivieren:**
```json
{
  "EpisodeBackgroundImage": {
    "EnableLogging": true
  }
}
```

Detaillierte Fehler werden dann in Logs ausgegeben.

---

## Hintergrundbild wird nach Restore nicht angezeigt

**Symptom:** Nach Backup-Restore fehlen Hintergrundbilder und werden nicht neu generiert.

**Ursache:** Generierte Bilder sind nicht im Backup (absichtlich ausgeschlossen zur Platzersparnis), aber Episode-Eigenschaften `GeneratedBackgroundPictureId` verweisen auf nicht-existierende Picture-IDs.

**Lösung:**

Option 1: Manuell zurücksetzen (einfach)
```sql
UPDATE TVShowEpisodes
SET GeneratedBackgroundPictureId = NULL, BackgroundImageRequiresUpdate = 0;
```
Beim nächsten Episode-Aufruf werden Bilder neu generiert.

Option 2: Automatisches Script nach Restore (optional)
```csharp
// In Restore-Prozess hinzufügen:
var episodes = dbContext.TVShowEpisodes.ToList();
foreach (var ep in episodes)
{
    ep.BackgroundImageRequiresUpdate = true;
}
dbContext.SaveChanges();
```

---

## Zu viel Datenbankplatz verbraucht

**Symptom:** Datenbank wächst sehr schnell; generierte Bilder belegen Gigabytes Speicher.

**Ursache:** Große Canvas-Größe oder hohe JPEG-Qualität; viele Episoden mit Fanarts.

**Lösungen:**

Option 1: Konfiguration reduzieren
```json
{
  "EpisodeBackgroundImage": {
    "MaxWidth": 1280,
    "MaxHeight": 720,
    "JpegQuality": 70
  }
}
```

Einsparung: ~100 KB pro Bild statt 200 KB.

Option 2: Generierte Bilder löschen
```sql
-- Nur generierte Bilder löschen (nicht Album-Covers, etc.)
DELETE FROM Pictures
WHERE IsGeneratedBackground = 1;

-- Episoden zurücksetzen
UPDATE TVShowEpisodes
SET GeneratedBackgroundPictureId = NULL, BackgroundImageRequiresUpdate = 1;
```

Neugenierung mit neuer Konfiguration beim nächsten Episode-Aufruf.

Option 3: Regelmäßige Bereinigung
```sql
-- Alternativer Ansatz: alte Generierungen löschen (älter als 90 Tage)
DELETE FROM Pictures
WHERE IsGeneratedBackground = 1 
AND CreatedAt < DATEADD(day, -90, GETUTCDATE());
```

---

## Cache-Konsistenz-Problem

**Symptom:** Nach manueller Änderung in DB wird Hintergrundbild nicht aktualisiert; Cache zeigt altes Bild.

**Ursache:** In-Memory Cache hält Bild-IDs für 60 Minuten (Standard `CacheDurationMinutes`).

**Lösung:**

Option 1: Cache-Dauer reduzieren
```json
{
  "EpisodeBackgroundImage": {
    "CacheDurationMinutes": 5
  }
}
```

Option 2: Cache löschen (Anwendung neu starten)
In-Memory Cache wird beim Starten geleert.

Option 3: Episode manuell invalidieren
```sql
-- Markiert Episode für Regenerierung beim nächsten Aufruf:
UPDATE TVShowEpisodes
SET BackgroundImageRequiresUpdate = 1
WHERE Id = 42; -- Episode ID
```

---

## Parallele Generierung verursacht Deadlock

**Symptom:** Bei sehr vielen parallelen Episode-Aufrufen (>100 gleichzeitig) friert Anwendung ein.

**Ursache:** AsyncLock-Congestion oder Datenbankverbindungs-Pool erschöpft.

**Lösungen:**

Option 1: Connection Pool erhöhen
```json
{
  "ConnectionStrings": {
    "DefaultConnection": "... Max Pool Size=200;"
  }
}
```

Option 2: Cache-Dauer erhöhen
```json
{
  "EpisodeBackgroundImage": {
    "CacheDurationMinutes": 240
  }
}
```

Reduziert redundante Generierungen bei wiederholten Aufrufen.

Option 3: Vorwärmen des Cache
Nach Deployment alle Episoden einmal aufrufen (z. B. via Batch-Script) um Generierungen anzuwärmen.

---

## TokenValidation Fehler beim API-Call

**Symptom:** Browser zeigt 401 Unauthorized bei `GET /api/episodes/{episodeId}/background-image`.

**Ursache:** Authentication-Token abgelaufen oder nicht korrekt übergeben.

**Lösung:**
1. Browser F12 öffnen → Network
2. Background-Image-Request überprüfen
3. `Authorization` Header sollte vorhanden sein
4. Token in URL-Query (`access_token=...`) überprüfen
5. Benutzer erneut anmelden

Falls Problem persistiert: AuthenticationStateProvider in `TVShowDetails.razor` überprüfen.

---

## Performance-Tipps

| Problem | Tipp |
|---------|------|
| Generierung zu langsam | Canvas-Größe reduzieren (Max Width/Height) |
| Hohe Datenbankgröße | JPEG-Qualität reduzieren (80 statt 85) |
| Zu viele Cache-Misses | Cache-Dauer erhöhen (120 statt 60 Minuten) |
| Zu viele DB-Zugriffe | Indizes überprüfen; AsNoTracking() Queries nutzen |
| Speicher-Überlauf | Komponenten-Cache limitieren; größere Instanzen |

---

## Support-Kontakt

Falls ein Problem nach dieser Dokumentation nicht gelöst ist:

1. Logs sammeln: `appsettings.json` auf `"Loglevel": "Debug"` setzen
2. SQL-Queries ausführen: `TVShowEpisodes` und `Pictures` Tabellen inspizieren
3. Configuration überprüfen: `appsettings.json` auf korrekte Werte kontrollieren
4. Datenbank-Integrität: Migrationen vollständig ausgeführt? Foreign Keys OK?
