# Technische Anforderungsübersetzung: Nachbesserung Entwicklungsschritt 2, Runde 1
Inhalte hinzufügen und entfernen — Duplikate und Normalisierung

**Aufgaben-ID:** fd729906-8801-43ee-8539-fa3e046c88f4 (Nachbesserung Schritt 2.1)  
**Branch:** task/issue-207-fd729906880143ee8539fa3e046c88f4-playlists-fuer-serien-staffeln-schritt-2-inhalte-hinzufuegen-und-entfernen  
**Erstellungsdatum:** 2026-09-05  
**Nachbesserung:** Runde 1

---

## Fachliche Zusammenfassung

Die Anforderung behebt zwei kritische Konsistenz- und Sicherheitsmängel im Playlist-Feature (Entwicklungsschritt 2):

1. **Inkonsistentes Duplikat-Handling mit fehlender Benutzer-Rückmeldung:**  
   Das Hinzufügen bereits existierender Inhalte zur Playlist erzeugt heute unterschiedliche Verhaltensweisen je nachdem, ob es sich um einen direkt angeforderten Top-Level-Titel (z. B. Serie) oder um einen Kaskaden-Titel (z. B. Episode, die durch das Hinzufügen der Serie automatisch miteinbezogen würde) handelt. Kaskaden-Duplikate werden stumm übersprungen, Top-Level-Duplikate führen zu HTTP 409 und Abbruch der gesamten Operation. Benutzer erhalten in beiden Fällen keine Rückmeldung über übersprungene oder hinzugefügte Titel.  
   **Korrektur:** Einheitliches, nicht-destruktives Handling: Alle bereits existierenden Titel (Top-Level und Kaskaden) sollen übersprungen werden, neue Titel werden hinzugefügt. Der Benutzer erhält eine klare, in der UI sichtbare Rückmeldung (z. B. „3 Titel hinzugefügt, 2 bereits vorhanden und übersprungen").

2. **Eindeutigkeit über die API umgehbar durch fehlende Normalisierung des `MediaType`-Wertes:**  
   Der `PlaylistService.AddMediaToPlaylistAsync`-Methode validiert den `mediaType`-Eingabeparameter case-insensitiv, speichert aber den rohen, unveränderten Request-String in die Datenbank. Duplikatsprüfung und Unique-Index vergleichen den gespeicherten Wert hingegen ordinal (case-sensitiv). Dadurch können Aufrufe mit `MediaType="Movie"` und `MediaType="movie"` mit derselben `mediaId` zwei separate Datenbankeinträge für denselben Film erzeugen — über die reguläre UI nicht erreichbar (dort werden nur Konstanten gesendet), aber über die dokumentierte REST-API möglich.  
   **Korrektur:** Nach Validierung muss der kanonische Enum-Wert (via `ToString()`) verwendet werden, nicht der rohe Request-String. Dies gewährleistet, dass unterschiedliche Groß-/Kleinschreibung desselben Medientyps garantiert als Duplikat erkannt wird.

---

## Betroffene Klassen und Komponenten

### Logikklassen / Services

- **`PlaylistService`** (Erweiterung/Modifikation)
  - `AddMediaToPlaylistAsync(playlistId, userId, mediaType, mediaId)` — Erweiterte Duplikat-Behandlung, normalisierte MediaType-Speicherung, erweiterte Rückgabe
  - Neue interne Methode: `NormalizeMediaTypeAsync()` (oder Verwendung von `ParseMediaType().ToString()`)

### Datenmodellklassen (Entities) — Änderungen in der Persistierungsebene

- **`PlaylistEntry`** (bestehende Entity, Daten-Migration erforderlich)
  - `MediaType` (string, Required) — Datenbank-Migration: Normalisierung bestehender Werte

### Response-DTOs

- **`DtoPlaylistEntry`** (bestehende DTO, Erweiterung) oder neue DTO **`DtoPlaylistAddResult`**
  - Neue Felder (eine der beiden Optionen):
    - Option A: `DtoPlaylistAddResult` (neue DTO) mit:
      - `AddedEntries: DtoPlaylistEntry[]` — Array der neu hinzugefügten Einträge
      - `SkippedCount: int` — Anzahl übersprungener Duplikate
      - `SkippedMediaTypeIds?: (string MediaType, long MediaId)[]` — Optional: Detailliste übersprungener Items
    - Option B: Erweiterung von `DtoPlaylistEntry` um `SkippedDuplicates: int?`, `Message?: string`
  - Empfohlen: **Option A** (neue DTO `DtoPlaylistAddResult`) für klare Separation von Erfolgs- und Fehlerfall

### API-Endpoints

- **`POST /api/playlists/{id}/items`** — Rückgabetyp-Erweiterung:
  - HTTP 200 OK (statt 409) für den Fall, dass Top-Level-Titel bereits existiert, aber neue Kaskaden-Titel hinzugefügt werden
  - HTTP 200 OK (statt 409) auch wenn ausschließlich alle angeforderten Titel bereits existieren (SkippedCount = Gesamtanzahl, AddedCount = 0)
  - HTTP 200 OK mit neu strukturiertem Response, der Anzahl der neu hinzugefügten und übersprungenen Titel enthält

### UI-Komponenten / Controller

- **`PlaylistDetail.razor`** (Erweiterung)
  - Anpassung des `AddEntryAsync()`-Aufrufs, um neuen Response zu verarbeiten
  - Anzeige von Rückmeldungen wie „3 Titel hinzugefügt, 2 bereits vorhanden und übersprungen" (Erfolgs- oder Info-Meldung, nicht Fehler)
  - Aktualisierung der Fehlerbehandlung (nicht mehr auf HTTP 409 warten, sondern auf Info-Meldung prüfen)

### Tests

- **Unit-Tests `PlaylistServiceTests_AddMedia`** (Erweiterung)
  - Neue Testfälle:
    - `AddMedia_TopLevelDuplicate_SkipsAndReturnsCount()` — Top-Level-Duplikat führt nicht zu Fehler
    - `AddMedia_PartialDuplicates_AddedNewAndSkipped()` — Kaskade mit teilweisen Duplikaten
    - `AddMedia_AllDuplicates_ReturnsZeroAddedCount()` — Alle Titel bereits vorhanden
    - `AddMedia_NormalizeMediaType_CaseInsensitiveDuplicateDetection()` — Case-Normalisierung
  - Bestehende Testfälle anpassen (z. B. `AddMedia_Duplicate_ThrowsInvalidOperationException` in `AddMedia_Duplicate_SkipsAndReturnsCount()` umstrukturieren)

- **Integrationstests** (Erweiterung)
  - E2E-Test: Dieselbe Serie zweimal hinzufügen, prüfe dass nur neue Episoden hinzugefügt werden
  - E2E-Test: Serie hinzufügen, Episode entfernen, Serie erneut hinzufügen → Episode wird wieder hinzugefügt
  - E2E-Test: `"Movie"` und `"movie"` mit derselben `mediaId` → eindeutige Duplikaterkennung

- **UI/Component-Tests** (Erweiterung)
  - Erfolgs-Meldung bei Duplikat-Addition korrekt dargestellt
  - Keine Fehlermeldung (rot), sondern Info-Meldung (grün/gelb) bei Duplikaten

### Dokumentation

- **`docs/help/playlists-api.md`** (Überarbeitung)
  - Dokumentation der neuen Response-Struktur (`DtoPlaylistAddResult`)
  - Erklärung: Top-Level-Duplikate führen nicht mehr zu HTTP 409
  - Erklärung: MediaType-Normalisierung (case-insensitive)

- **`docs/help/playlists-business-rules.md`** (Überarbeitung)
  - Vereinheitlichte Duplikat-Regel: „Beim Hinzufügen bereits enthaltener Titel werden diese übersprungen und der Anwender erhält einen entsprechenden Hinweis."
  - Entfernung der früheren Aussage „Cascade-Duplikate werden still übersprungen"
  - Klarstellung: MediaType wird normalisiert gespeichert

---

## Implementierungsansatz

### 1. MediaType-Normalisierung

**Problem im aktuellen Code (Zeilen 141–206 in `PlaylistService.cs`):**
- Zeile 145: `var parsedMediaType = ParseMediaType(mediaType);` validiert case-insensitiv
- Zeile 182: `MediaType = mediaType,` speichert den **rohen Request-String**, nicht `parsedMediaType.ToString()`
- Zeile 162: `if (existingKeys.Contains((mediaType, mediaId)))` vergleicht den rohen String case-sensitiv

**Lösung:**
1. Nach Validierung (Zeile 145) muss der kanonische Enum-Wert verwendet werden:
   ```csharp
   var normalizedMediaType = parsedMediaType.ToString(); // → "Movie" (kanonisch)
   ```
2. Alle Speicherungen verwenden `normalizedMediaType`, nicht `mediaType`
3. Duplikat-Checks verwenden ebenfalls den normalisierten Wert
4. **Daten-Migration erforderlich:** Bestehende `PlaylistEntry.MediaType`-Werte in Datenbank müssen normalisiert werden (z. B. `"movie"` → `"Movie"`)

### 2. Einheitliches Duplikat-Handling mit Rückmeldung

**Problem im aktuellen Code:**
- Zeile 162–163: Top-Level-Duplikat wirft Fehler (HTTP 409)
- Zeile 168–169: Kaskaden-Duplikate werden still übersprungen
- Zeile 205: Rückgabe enthält keine Informationen über übersprungene Titel

**Lösung — Option A (empfohlen): Neue Response-DTO**

Neue Klasse `DtoPlaylistAddResult` in `VideoWebPlayer.Client.Models/`:
```csharp
public class DtoPlaylistAddResult
{
    /// <summary>
    /// Neu hinzugefügte Top-Level-Eintrag (oder null, wenn bereits existierte).
    /// </summary>
    public DtoPlaylistEntry? TopLevelEntry { get; set; }

    /// <summary>
    /// Alle neu hinzugefügten Einträge (Top-Level + Kaskaden).
    /// </summary>
    public DtoPlaylistEntry[] AddedEntries { get; set; } = Array.Empty<DtoPlaylistEntry>();

    /// <summary>
    /// Anzahl der übersprungenen Duplikate.
    /// </summary>
    public int SkippedDuplicateCount { get; set; }

    /// <summary>
    /// Benutzer-freundliche Nachricht.
    /// Beispiele:
    /// - "3 Titel hinzugefügt, 2 bereits vorhanden und übersprungen."
    /// - "2 Titel hinzugefügt."
    /// - "Alle 5 Titel waren bereits vorhanden."
    /// </summary>
    public string Message { get; set; } = string.Empty;
}
```

Überarbeitete Signatur in `PlaylistService.AddMediaToPlaylistAsync()`:
```csharp
public async Task<DtoPlaylistAddResult> AddMediaToPlaylistAsync(
    long playlistId, 
    string userId, 
    string mediaType, 
    long mediaId, 
    CancellationToken cancellationToken = default)
```

**Logik-Änderung:**
1. Duplikat-Check für Top-Level-Titel nicht werfen, sondern Zähler erhöhen
2. Cascade-Duplikate wie bisher filtern, Zähler erhöhen
3. Alle neuen Einträge hinzufügen (Liste erweitern)
4. Falls alle Titel bereits existieren (AddedCount == 0, SkippedCount > 0): HTTP 200 OK mit Message
5. Falls teilweise neue Titel: HTTP 200 OK mit Message und AddedEntries
6. Fehler nur für echte Fehler (z. B. MediaId nicht gefunden, Benutzer nicht Besitzer, MaxItems überschritten)

**Pseudocode:**
```csharp
// Duplikat-Checks
int skippedDuplicateCount = 0;
var entriesToAdd = new List<PlaylistEntry>();

// Top-Level-Eintrag prüfen
if (existingKeys.Contains((normalizedMediaType, mediaId)))
{
    skippedDuplicateCount++;
}
else
{
    entriesToAdd.Add(topLevelEntry);
}

// Kaskaden-Einträge prüfen
var cascadeEntries = await GetCascadeMediaIdsAsync(parsedMediaType, mediaId, cancellationToken);
foreach (var (childMediaType, childMediaId) in cascadeEntries)
{
    if (existingKeys.Contains((childMediaType, childMediaId)))
    {
        skippedDuplicateCount++;
    }
    else
    {
        entriesToAdd.Add(new PlaylistEntry { ... });
    }
}

// Alle hinzufügen (falls count + entriesToAdd.Count <= MaxItemCount)
await _db.PlaylistEntries.AddRangeAsync(entriesToAdd, cancellationToken);
await _db.SaveChangesAsync(cancellationToken);

// Response bauen
var message = entriesToAdd.Count switch
{
    > 0 when skippedDuplicateCount > 0 => 
        $"{entriesToAdd.Count} Titel hinzugefügt, {skippedDuplicateCount} bereits vorhanden und übersprungen.",
    > 0 => 
        $"{entriesToAdd.Count} Titel hinzugefügt.",
    _ => 
        $"Alle {skippedDuplicateCount} Titel waren bereits vorhanden."
};

return new DtoPlaylistAddResult
{
    TopLevelEntry = topLevelEntry, // null wenn Duplikat
    AddedEntries = entriesToAdd.Select((e, i) => ToDto(e, titles[e.MediaType][e.MediaId], ...)).ToArray(),
    SkippedDuplicateCount = skippedDuplicateCount,
    Message = message
};
```

### 3. Daten-Migration

**EF Core Migration erforderlich:**
- Neue Migration: `NormalizePlaylistEntryMediaTypes`
- SQL-Operation: Normalisierung aller `MediaType`-Werte in der `PlaylistEntries`-Tabelle
  ```sql
  UPDATE PlaylistEntries SET MediaType = 'Movie' WHERE MediaType LIKE 'movie' OR MediaType LIKE 'MOVIE' OR ...;
  -- etc. für jeden Typ
  ```
  Oder in C# mit EF Core:
  ```csharp
  protected override void Up(MigrationBuilder migrationBuilder)
  {
      migrationBuilder.Sql(
          @"UPDATE PlaylistEntries SET MediaType = 'Movie' WHERE LOWER(MediaType) = 'movie';
            UPDATE PlaylistEntries SET MediaType = 'TVShow' WHERE LOWER(MediaType) = 'tvshow';
            -- etc."
      );
  }
  ```

### 4. Fehlerbehandlung

**Welche Fälle bleiben Fehler (HTTP 4xx/5xx):**
- `mediaId <= 0` — HTTP 400 (Bad Request)
- Medieninhalt nicht gefunden — HTTP 404 (Not Found)
- Benutzer ist nicht Besitzer der Playlist — HTTP 403 (Forbidden)
- Playlist nicht gefunden — HTTP 404 (Not Found)
- MaxPlaylistItemCount überschritten — HTTP 400 (Bad Request) oder 409 (Conflict)

**Welche Fälle sind jetzt HTTP 200 OK (statt Fehler):**
- Top-Level-Duplikat (übersprungen, SkippedCount erhöht)
- Alle Titel bereits vorhanden (SkippedCount = Gesamtzahl, Message zeigt dies)

### 5. API-Endpoint Signatur-Änderung

**POST /api/playlists/{playlistId}/items**

**Vorher (aktuell):**
```
Request:  POST /api/playlists/1/items
          { mediaType: "Movie", mediaId: 5 }
Response: HTTP 200 OK
          { 
            id: 10,
            playlistId: 1,
            mediaType: "Movie",
            mediaId: 5,
            mediaTitle: "The Matrix",
            ...
          }
          
          oder HTTP 409 Conflict (bei Duplikat)
```

**Nachher (neu):**
```
Request:  POST /api/playlists/1/items
          { mediaType: "Movie", mediaId: 5 }
Response: HTTP 200 OK
          {
            topLevelEntry: { 
              id: 10,
              playlistId: 1,
              mediaType: "Movie",
              mediaId: 5,
              mediaTitle: "The Matrix",
              ...
            },
            addedEntries: [ ... ],
            skippedDuplicateCount: 2,
            message: "1 Titel hinzugefügt, 2 bereits vorhanden und übersprungen."
          }
          
          oder HTTP 200 OK mit addedEntries: [] und message: "Alle 3 Titel waren bereits vorhanden."
```

---

## Konfiguration

Keine neue Konfiguration erforderlich. Bestehende Einstellung `PlaylistSettings.MaxPlaylistItemCount` bleibt bestehen und wird weiterhin beachtet (Fehler, falls die Summe der neuen Einträge Limit überschreitet).

---

## Abhängigkeiten und kritische Pfade

### Abhängigkeiten zu bestehenden Komponenten

- **`MediaType` Enum** (bestehende Enum in `VideoWebPlayer.Services.Models` oder ähnlich)
  - Verwendung für Normalisierung via `ToString()`

- **`PlaylistEntry` Entity** (bestehende Entity)
  - Daten-Migration notwendig (Normalisierung von `MediaType`-Werten)

- **`IPlaylistService` Interface** (bestehende Schnittstelle)
  - Signaturen-Änderung erforderlich (Rückgabetyp `DtoPlaylistAddResult` statt `DtoPlaylistEntry`)
  - Alle Consumer des Interface müssen angepasst werden

### Verbraucher (Brechen möglicherweise bei nicht-Anpassung)

- **`PlaylistDetail.razor`** — Direkter Aufrufer von `AddEntryAsync()`
- **`PlaylistController` oder ähnliche API-Handler** — Geben Response zurück
- **Tests** — Verwenden direkt oder indirekt `AddMediaToPlaylistAsync()`
- **Frontend-Code** (JavaScript/TypeScript), falls vorhanden — Wartet auf alte Response-Struktur

### Rückwärtskompatibilität

**Breaking Change:** Rückgabetyp von `DtoPlaylistEntry` auf `DtoPlaylistAddResult` ändert sich. Dies ist eine Breaking Change für:
- API-Clients (externen REST-Clients)
- Interne Blazor-Komponenten, die auf alte DTO-Felder zugreifen

**Mitigation:** Versionierung beachten (z. B. `/api/v2/playlists/{id}/items` einführen oder nur in Major-Release ändern).

---

## Aktualisierungen der Dokumentation und README

### Zu aktualisierende Dateien

1. **`docs/help/playlists-api.md`**
   - **Abschnitt „Add Media to Playlist (POST)":**
     - Response-Schema: Alt (Eintrag) → Neu (DtoPlaylistAddResult mit Message)
     - Beispiel: Duplikat-Response zeigen (HTTP 200, nicht 409)
     - Klarstellung: MediaType ist case-insensitive in Input, aber normalisiert in Speichering und Duplikat-Prüfung

2. **`docs/help/playlists-business-rules.md`**
   - **Duplikat-Regel überarbeiten:**
     - **Alt:** „Cascade-Duplikate werden still übersprungen; Top-Level-Duplikate führen zu Fehler 409."
     - **Neu:** „Alle Duplikate (Top-Level und Cascade) werden übersprungen. Der Anwender erhält eine Rückmeldung über die Anzahl neu hinzugefügter und übersprungener Titel."
   - **Medientyp-Normalisierung hinzufügen:**
     - „MediaType-Eingaben werden case-insensitiv verarbeitet und in kanonischer Form (z. B. ‚Movie' statt ‚movie') gespeichert. Unterschiedliche Casing desselben Typs führt zu korrekter Duplikaterkennung."

3. **`docs/API.md`** (falls vorhanden)
   - Response-Schema aktualisieren
   - HTTP-Status-Code-Erklärung: 409 ist nicht mehr möglich

4. **README oder Changelog**
   - Eintrag: „Duplikat-Handling für Playlists überarbeitet: Top-Level- und Cascade-Duplikate werden einheitlich übersprungen mit Rückmeldung an Benutzer"
   - Eintrag: „MediaType-Normalisierung: API akzeptiert jetzt case-insensitive MediaType-Eingaben und speichert kanonisch normalisiert"

### Benutzer-sichtbare Verhaltensänderungen

| Szenario | Aktuelles Verhalten | Neues Verhalten | Benutzer-sichtbar? |
|----------|---------------------|-----------------|-------------------|
| Serie A zweimal hinzufügen (Top-Level Duplikat) | HTTP 409, Fehler, nichts hinzugefügt | HTTP 200, Info-Meldung „0 Titel hinzugefügt, alle bereits vorhanden" | Ja — Erfolgreiche Operation statt Fehler |
| Serie hinzufügen, dann Staffel aus dieser Serie hinzufügen (Cascade Duplikat) | HTTP 200, Stille Übersprung, keine Rückmeldung | HTTP 200, Info-Meldung „0 Titel hinzugefügt, alle bereits vorhanden" | Ja — Jetzt mit Rückmeldung |
| Serie A mit 3 neuen Episoden hinzufügen (teilweise Cascade Duplikate) | HTTP 200, nur neue hinzugefügt, stille Übersprung | HTTP 200, Info-Meldung „3 neue Titel, 2 bereits vorhanden" | Ja — Detaillierte Rückmeldung statt Stille |
| `"Movie"` und `"movie"` mit gleicher ID hinzufügen (Normalisierung) | Zwei separate Einträge in DB | Ein Eintrag, zweite Hinzufügung als Duplikat erkannt | Nein (über UI nicht erreichbar, über API jetzt korrekt) |

---

## Offene Fragen und Annahmen

### Annahmen (Implementierungs-Details)

1. **Rückgabe-DTO Struktur:**  
   Annahme: Ein neues DTO `DtoPlaylistAddResult` wird eingeführt (Option A), nicht eine Erweiterung des bestehenden `DtoPlaylistEntry`. Dies ermöglicht klarere API-Kontrakte und bessere Separation von Concerns.

2. **Message-Formatierung:**  
   Annahme: Die benutzer-freundliche Nachricht wird auf Englisch UND Deutsch zur Verfügung gestellt (z. B. via Ressourcen-Dateien oder `ILocalizer`-Schnittstelle). Detailliertes Wording wird mit Stakeholder abgestimmt.

3. **Verwaiste Parent-Referenzen:**  
   Annahme: Wenn ein Top-Level-Sammelwerk (z. B. eine Serie) aus einer Playlist entfernt wird, aber dessen Cascade-Einträge (Episoden) bleiben, werden die `ParentMediaType/ParentMediaId`-Felder dieser verwaisten Einträge nicht angepasst. Sie werden weiterhin mit ihrer ursprünglichen Parent-ID gespeichert. Dies ist unkritisch für Anzeige (Parent-Titel wird separat aufgelöst), sollte aber bei zukünftigen Nachzugs-Logiken berücksichtigt werden.

4. **Maximale Item-Zahl:**  
   Annahme: `MaxPlaylistItemCount` begrenzt die Gesamtanzahl (Top-Level + Cascade), nicht einzelne Items. Wenn die Grenze überschritten würde, wird HTTP 400/409 geworfen, BEVOR irgendetwas hinzugefügt wird (Atomarität).

5. **MediaType Enum-Werte:**  
   Annahme: Die fünf gültigen MediaType-Werte sind: `Movie`, `TVShow`, `TVShowSeason`, `TVShowEpisode`, `MovieCollection` (Großbuchstaben am Start, rest lowercase). Diese entsprechen den Konstanten in `MediaTypeValues`.

### Zu klärende Punkte

1. **Message-Sprache:**  
   Sollen die Nachrichten in Deutsch oder Englisch sein? Falls mehrsprachig, wie wird die Lokalisierung gehandhabt?

2. **Granularität der Duplicate-Liste:**  
   Soll `DtoPlaylistAddResult.SkippedDuplicateCount` nur eine Zahl sein, oder wird eine detaillierte Liste übersprungener Items gewünscht (z. B. Array von `(MediaType, MediaId)`)?

3. **HTTP Status für vollständige Duplikate:**  
   Wenn **alle** angeforderten Titel bereits existieren, ist HTTP 200 OK korrekt, oder soll es HTTP 204 No Content sein (da nichts neu hinzugefügt wurde)?

4. **UI-Feedback Dauer:**  
   Wie lange soll die Erfolgs-Meldung in der UI angezeigt werden? Automatisch ausblenden nach X Sekunden, oder vom Benutzer schließbar?

5. **Daten-Migration Rollback:**  
   Wie wird ein Rollback der Normalisierungs-Migration gehandhabt? (Forward-Migration einfach, aber Rückwärts-Rekonstruktion der Original-Casing ist unmöglich.)

---

## Implementierungs-Reihenfolge (Empfohlen)

1. **Datenmodell & Migration:**
   - Normalisierungs-Migration erstellen und testen
   - Bestehende DB-Einträge normalisieren

2. **Service-Logik:**
   - `PlaylistService.AddMediaToPlaylistAsync()` umstrukturieren
   - MediaType-Normalisierung implementieren
   - Duplikat-Handling überarbeiten
   - Neue Response-DTO `DtoPlaylistAddResult` erstellen

3. **Tests:**
   - Unit-Tests anpassen/erweitern
   - Integrationstests hinzufügen

4. **API & UI:**
   - Controller aktualisieren (Response-Mapping)
   - `PlaylistDetail.razor` anpassen
   - Fehlerbehandlung & Message-Anzeige

5. **Dokumentation:**
   - API-Docs aktualisieren
   - Business-Rules-Docs überarbeiten
   - README/Changelog ergänzen

6. **Akzeptanztests & QA:**
   - E2E-Szenarien durchspielen
   - Benutzer-Akzeptanz-Kriterien validieren
