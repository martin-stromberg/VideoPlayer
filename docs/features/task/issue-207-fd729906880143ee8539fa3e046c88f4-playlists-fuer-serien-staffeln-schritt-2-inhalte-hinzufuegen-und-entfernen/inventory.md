# Bestandsaufnahme: Playlists — Inhalte hinzufügen und entfernen (Duplikate und Normalisierung)

Analyse der bestehenden Implementierung der Playlist-Funktionalität, insbesondere der Methode `PlaylistService.AddMediaToPlaylistAsync` und ihrer Verbraucher, bezogen auf die Anforderung „Duplikate und Normalisierung" (Nachbesserung Schritt 2.1).

## Zusammenfassung

- **PlaylistService.AddMediaToPlaylistAsync** existiert bereits und funktioniert grundlegend, wirft aber bei Top-Level-Duplikaten `InvalidOperationException` (HTTP 409)
- **MediaType Normalisierung** ist teilweise vorhanden (Validierung case-insensitiv über `ParseMediaType`), aber **Speicherung erfolgt nicht normalisiert** — der rohe Request-String wird gespeichert
- **Duplikat-Prüfung** erfolgt case-sensitiv auf dem gespeicherten String, was zu Inkonsistenz führt
- **Kaskaden-Duplikate** werden still übersprungen, ohne dass der Benutzer informiert wird
- **Response-DTO** (`DtoPlaylistEntry`) enthält nur einzelnen Eintrag, keine Informationen über übersprungene Titel
- **PlaylistDetail.razor** behandelt HTTP 409 als Fehler und zeigt Fehlermeldung an
- **Tests vorhanden** für grundlegende Duplikat-Szenarien, aber nicht für Normalisierung oder einheitliches Handling

## Detailanalyse nach Komponenten

### Datenmodell
siehe [Datenmodell-Dokumentation](inventory/models.md)

**Kritische Befunde:**
- `PlaylistEntry.MediaType` wird als roher String gespeichert, nicht normalisiert
- Keine Datenbank-Migration für Normalisierung bestehender Einträge vorhanden

### Logik und Service
siehe [Logik-Dokumentation](inventory/logic.md)

**Kritische Befunde in `PlaylistService.AddMediaToPlaylistAsync`:**

1. **Zeile 162–163 (Top-Level-Duplikat-Check):**
   ```csharp
   if (existingKeys.Contains((mediaType, mediaId)))
       throw new InvalidOperationException("Medieninhalt bereits in dieser Playlist vorhanden.");
   ```
   - ❌ Wirft Exception → HTTP 409
   - ❌ Nutzt rohen Request-`mediaType`, nicht normalisierten Enum-Wert
   - Sollte: Zähler erhöhen, nicht werfen

2. **Zeile 165–169 (Kaskaden-Duplikat-Filter):**
   ```csharp
   var newCascadeEntries = cascadeEntries
       .Select(e => (MediaType: e.MediaType.ToString(), e.MediaId))
       .Where(e => !existingKeys.Contains(e))
       .ToList();
   ```
   - ❌ Kaskaden-Duplikate werden still übersprungen ohne Zähler
   - ❌ Filter nutzt `ToString()` (normalisiert), aber `existingKeys` hat rohe Strings

3. **Zeile 182 (MediaType-Speicherung):**
   ```csharp
   MediaType = mediaType,  // ROHER STRING!
   ```
   - ❌ Speichert nicht-normalisierten Request-String
   - Beispiel: "Movie" und "movie" erzeugen zwei DB-Einträge

4. **Zeile 205 (Rückgabe):**
   ```csharp
   return ToDto(topLevelEntry, mediaTitle, null);
   ```
   - ❌ Nur einzelner Eintrag, keine Info über übersprungene Titel

**Positiv:**
- `MediaTypeHandler` Dictionary funktioniert korrekt mit kanonischen Enum-Werten
- Cascade-Logik selbst ist korrekt implementiert

### Enums und Konstanten
siehe [Enums-Dokumentation](inventory/enums.md)

- `MediaType` Enum hat fünf Werte mit korrekten `ToString()` Ausgaben
- `MediaTypeValues` Konstanten spiegeln kanonische Enum-Werte korrekt wider
- UI sendet immer normalisierte Werte (über Konstanten-Dropdown)
- Problem: REST-API akzeptiert jeden String (case-insensitiv parsing, aber unsachgemäße Speicherung)

### Interfaces
siehe [Interfaces-Dokumentation](inventory/interfaces.md)

**Breaking Change erforderlich:**
- `IPlaylistService.AddMediaToPlaylistAsync` Rückgabetyp ändert sich
- Von: `Task<DtoPlaylistEntry>`
- Zu: `Task<DtoPlaylistAddResult>` (neue DTO)

**Verbraucher müssen angepasst werden:**
1. PlaylistsController (Zeile 215–217)
2. VideoWebPlayerClient (Zeile 450–455)
3. PlaylistDetail.razor (Zeile 193–198)

### Data Transfer Objects (DTOs)
siehe [DTOs-Dokumentation](inventory/dtos.md)

**Bestehend:**
- `DtoPlaylistEntry` — einzelner Eintrag (Response aktuell)
- `DtoAddMediaToPlaylistRequest` — Request-Struktur (unverändert)
- `DtoPlaylist` — Playlist-Metadaten (unverändert)

**Neu erforderlich:**
- `DtoPlaylistAddResult` mit:
  - `TopLevelEntry?: DtoPlaylistEntry` — der neu hinzugefügte Top-Level-Eintrag
  - `AddedEntries: DtoPlaylistEntry[]` — alle neu hinzugefügten (Top-Level + Cascade)
  - `SkippedDuplicateCount: int` — Anzahl übersprungener Duplikate
  - `Message: string` — Benutzer-freundliche Meldung

### UI-Komponenten
siehe [UI-Dokumentation](inventory/ui.md)

**PlaylistDetail.razor — `AddEntryAsync` Methode (Zeile 188–212):**
- ❌ Behandelt HTTP 409 als Fehler
- ❌ Keine Verarbeitung der neuen Response-Struktur
- ❌ Keine Rückmeldung über übersprungene Titel

**Nach Anforderung sollte:**
- HTTP 200 OK für alle Fälle (auch Duplikate)
- Neue DTO-Struktur verarbeiten
- `Message` aus Response anzeigen
- Unterschied Fehler vs. Info in UI (Farbe/Icon)

### API-Controller
siehe [Controller-Dokumentation](inventory/controller.md)

**PlaylistsController.AddMediaToPlaylist (Zeile 209–244):**
- Aktuell: Mappt InvalidOperationException zu HTTP 409 (via `MapInvalidOperationException`)
- Nach Anforderung: Exception wird nicht mehr geworfen
- Response-Mapping: `DtoPlaylistEntry` zu `DtoPlaylistAddResult`

**HTTP-Status-Code Änderungen:**
| Szenario | Aktuell | Nach Anforderung |
|----------|---------|------------------|
| Top-Level-Duplikat | 409 Conflict | 200 OK |
| Cascade-Duplikat | 200 OK (stumm) | 200 OK + Message |
| Medieninhalt nicht gefunden | 404 Not Found | 404 Not Found |
| Max-Items überschritten | 400/409 | 400 Bad Request |

### Tests
siehe [Tests-Dokumentation](inventory/tests.md)

**Bestehende Tests (müssen angepasst werden):**
- `AddMedia_Duplicate_ThrowsInvalidOperationException` (Zeile 32–43)
  - ❌ Exception wird nicht mehr geworfen
  - Umzustrukturieren zu: `AddMedia_TopLevelDuplicate_SkipsAndReturnsCount`

**Bestehende Tests (bleiben gültig):**
- `AddMedia_ValidMovie_Success`
- `AddMedia_InvalidMediaType_ThrowsInvalidOperationException`
- `AddMedia_MediaNotFound_ThrowsKeyNotFoundException`
- `AddMedia_NotOwner_ThrowsPlaylistAccessDeniedException`
- `AddMedia_TVShow_CascadesEpisodes`
- `AddMedia_TVShowSeason_CascadesEpisodes`
- `AddMedia_MovieCollection_CascadesMovies`
- `AddMedia_CascadeWithDuplicates_SkipsDuplicates` (angepasst)
- `AddMedia_MaxItemCountExceeded_ThrowsInvalidOperationException`
- `AddMedia_CascadeExceedsMaxItemCount_ThrowsInvalidOperationExceptionWithoutPartialInsert`

**Neue Tests erforderlich (laut requirement.md):**
1. `AddMedia_TopLevelDuplicate_SkipsAndReturnsCount` — Top-Level-Duplikat
2. `AddMedia_PartialDuplicates_AddedNewAndSkipped` — Mix aus neuen und übersprungenen
3. `AddMedia_AllDuplicates_ReturnsZeroAddedCount` — Alle bereits vorhanden
4. `AddMedia_NormalizeMediaType_CaseInsensitiveDuplicateDetection` — "Movie" vs "movie"

**Integrations-Tests erforderlich:**
- E2E: Serie zweimal hinzufügen
- E2E: Serie entfernen, Serie erneut hinzufügen
- E2E: MediaType-Normalisierung über API

## Kritische Fehler im aktuellen Code

### 1. MediaType-Normalisierung fehlt (Sicherheit/Zuverlässigkeit)

**Ort:** PlaylistService.cs Zeile 182

**Problem:**
```csharp
var topLevelEntry = new PlaylistEntry
{
    MediaType = mediaType,  // roher String aus Request!
    ...
};
```

**Folge:**
- API-Clients können `"Movie"` und `"movie"` mit gleicher ID senden
- DB hat zwei separate Einträge für das gleiche Medium
- Duplikat-Prüfung wird unreliabel

**Lösung:** `MediaType = parsedMediaType.ToString()`

### 2. Top-Level-Duplikate werfen Exception (Benutzer-Feedback)

**Ort:** PlaylistService.cs Zeile 162–163

**Problem:**
```csharp
if (existingKeys.Contains((mediaType, mediaId)))
    throw new InvalidOperationException(...);
```

**Folge:**
- HTTP 409 statt 200 OK
- Ganze Operation wird abgebrochen
- Kaskaden-Einträge werden nicht hinzugefügt (wenn teilweise neu)

**Lösung:** Zähler erhöhen, weiterverarbeiten

### 3. Keine Rückmeldung über Duplikate (Benutzer-Erlebnis)

**Ort:** PlaylistService.cs Zeile 205

**Problem:** Nur einzelnes DTO wird zurückgegeben, keine Statistik

**Folge:**
- Benutzer weiß nicht, ob 10 oder 100 Titel hinzugefügt wurden
- Stumme Übersprung von Duplikaten ist verwirrend

**Lösung:** Neue Response-DTO mit AddedEntries[], SkippedCount, Message

## Abhängigkeiten und Verbraucher

```
PlaylistService.AddMediaToPlaylistAsync
├─ IPlaylistService Interface (wird in Signaturen erweitert)
├─ PlaylistsController
│  └─ PlaylistsController.AddMediaToPlaylist
│     └─ MapInvalidOperationException (wird vereinfacht/angepasst)
├─ VideoWebPlayerClient
│  └─ VideoWebPlayerClient.AddMediaToPlaylistAsync
│     └─ HttpPostAsync<DtoPlaylistEntry> (wird zu <DtoPlaylistAddResult>)
└─ PlaylistDetail.razor
   └─ AddEntryAsync (wird erheblich angepasst)
      └─ Fehlerbehandlung und Meldungsanzeige
```

## Zusammenfassung der Befunde

| Bereich | Status | Notizen |
|---------|--------|---------|
| MediaType-Normalisierung | ❌ Nicht implementiert | Zeile 182: roher String wird gespeichert |
| Top-Level-Duplikat-Handling | ❌ Werft Exception | Sollte Info-Meldung sein (HTTP 200) |
| Cascade-Duplikat-Handling | ✅ Funktioniert (stumm) | Muss mit Zähler versehen werden |
| Response-DTO Struktur | ❌ Zu simpel | Nur einzelner Eintrag, keine Statistik |
| Controller Exception-Mapping | ⚠️ Wird angepasst | HTTP 409 wird nicht mehr nötig |
| UI-Fehlerbehandlung | ⚠️ Wird angepasst | HTTP 409-Check wird obsolet |
| Tests für Normalisierung | ❌ Nicht vorhanden | Sicherheitslücke nicht getestet |
| Tests für vereinheitlichtes Handling | ⚠️ Teilweise | Test `AddMedia_Duplicate_*` muss umstrukturiert werden |
