# Umsetzungsplan: Duplikate und Normalisierung in Playlist-Inhalte

## Übersicht

Die Nachbesserung behebt zwei kritische Konsistenz- und Sicherheitsmängel im Playlist-Feature Schritt 2:

1. **Inkonsistentes Duplikat-Handling:** Top-Level-Duplikate führen zu HTTP 409 und Fehlerabbruch, während Kaskaden-Duplikate stumm übersprungen werden. Die Anforderung vereinheitlicht dies: Alle Duplikate werden übersprungen mit benutzerfreundlicher Rückmeldung.

2. **Fehlende MediaType-Normalisierung:** Rohe Request-Strings (z. B. `"movie"` vs. `"Movie"`) werden unverarbeitet in die DB geschrieben, was die Duplikat-Prüfung case-sensitiv macht. Die API akzeptiert verschiedene Schreibweisen für denselben Typ, was zu doppelten DB-Einträgen führt. Die Anforderung normalisiert alle MediaType-Werte auf den kanonischen Enum-Wert.

Betroffene Bereiche: Service-Logik, Datenmodell, Response-DTO, API-Controller, UI-Komponente, Tests, Dokumentation.

---

## Designentscheidungen

| Komponente / Bereich | Gewählter Ansatz | Begründung |
|----------------------|-----------------|------------|
| Response-DTO für ADD-Operation | Neue DTO `DtoPlaylistAddResult` statt Erweiterung von `DtoPlaylistEntry` | Ermöglicht klare Trennung zwischen einzelnen Eintrag-DTO und Mehrergebnis-Response; Zukunftssicherer für API-Versionierung; `DtoPlaylistEntry` bleibt unverändert für Get-Operationen |
| MediaType-Normalisierung | Kanonischer Enum-Wert via `parsedMediaType.ToString()` speichern, nicht rohen Request-String | Garantiert case-insensitive Duplikat-Erkennung; Konsistent mit `MediaTypeValues` Konstanten und MediaTypeHandler Dictionary |
| Duplikat-Rückmeldung | Benutzerfreundliche Nachricht in `DtoPlaylistAddResult.Message` | Ermöglicht präzise Fehlerbehandlung in UI; Im Gegensatz zu HTTP-Status-Code: Status ist immer 200 OK, Nachricht beschreibt Details |
| Daten-Migration | Raw SQL via `migrationBuilder.Sql()` zur Normalisierung bestehender Werte | EF Core Bulk-Update ist nicht möglich in Migrations; Raw SQL ist zuverlässig und performant für alle MediaType-Varianten |
| Breaking Change | Rückgabetyp `AddMediaToPlaylistAsync` von `DtoPlaylistEntry` zu `DtoPlaylistAddResult` | Akzeptable Breaking Change, da Methode neu dokumentiert wird; Consumer sind intern (Controller, Client, Razor) — keine Public API für externe Clients |

---

## Programmabläufe

### AddMediaToPlaylist mit einheitlichem Duplikat-Handling und Normalisierung

Schritte, die in `PlaylistService.AddMediaToPlaylistAsync` implementiert werden:

1. Validiere Ownership: `await GetOwnedPlaylistAsync(playlistId, userId)` wirft Exception bei Zugriffsverletzung
2. Validiere MediaType: `ParseMediaType(mediaType)` gibt `MediaType` Enum zurück oder wirft `InvalidOperationException`
3. **Normalisiere MediaType:** `var normalizedMediaType = parsedMediaType.ToString()` (kanonischer Wert, z. B. `"Movie"`)
4. Lade alle bestehenden Einträge als HashSet: `existingKeys = { (MediaType, MediaId), ... }`
5. **Top-Level-Duplikat-Check (neu: keine Exception):**
   - Falls `(normalizedMediaType, mediaId)` in `existingKeys`: Zähler `skippedCount++`, nicht werfen
   - Sonst: Neue Entry zur Liste `entriesToAdd` hinzufügen (mit normalisiertem `normalizedMediaType`)
6. Hole Cascade-Kinder: `cascadeEntries = await GetCascadeMediaIdsAsync(parsedMediaType, mediaId, cancellationToken)`
7. **Filtere Cascade-Duplikate (neu: mit Zähler):**
   - Für jeden Cascade-Eintrag `(cascadeMediaType, cascadeMediaId)`:
     - Normalisiere: `var normalizedCascadeType = cascadeMediaType.ToString()`
     - Falls `(normalizedCascadeType, cascadeMediaId)` in `existingKeys`: `skippedCount++`
     - Sonst: Neue Entry zur Liste `entriesToAdd` hinzufügen (mit normalisiertem Type)
8. Validiere Max-Item-Limit: Falls `currentCount + entriesToAdd.Count > MaxPlaylistItemCount`: Werfe `InvalidOperationException` (HTTP 400/409)
9. Speichere alle neuen Einträge **mit normalisiertem MediaType:** `await _db.PlaylistEntries.AddRangeAsync(entriesToAdd)` → `await _db.SaveChangesAsync()`
10. **Baue Benutzer-Nachricht:**
    - Falls `entriesToAdd.Count > 0` und `skippedCount > 0`: `"{entriesToAdd.Count} Titel hinzugefügt, {skippedCount} bereits vorhanden und übersprungen."`
    - Falls `entriesToAdd.Count > 0` und `skippedCount == 0`: `"{entriesToAdd.Count} Titel hinzugefügt."`
    - Falls `entriesToAdd.Count == 0` und `skippedCount > 0`: `"Alle {skippedCount} Titel waren bereits vorhanden."`
11. **Baue Response:**
    ```csharp
    return new DtoPlaylistAddResult
    {
        TopLevelEntry = entriesToAdd.FirstOrDefault(e => e.ParentMediaType == null) |> ToDto(...),
        AddedEntries = entriesToAdd.Select(e => ToDto(e, ...)).ToArray(),
        SkippedDuplicateCount = skippedCount,
        Message = message
    };
    ```

Beteiligte Klassen/Komponenten: `PlaylistService`, `PlaylistEntry`, `MediaType` (Enum), `DtoPlaylistAddResult`

### PlaylistDetail.razor AddEntryAsync mit neuer Response-Verarbeitung

Schritte, die in `PlaylistDetail.razor` implementiert werden:

1. Benutzer füllt MediaType-Dropdown und MediaId aus, klickt "Hinzufügen"
2. `AddEntryAsync()` wird aufgerufen
3. Konstruiere Request: `new DtoAddMediaToPlaylistRequest { MediaType = newEntryMediaType, MediaId = newEntryMediaId }`
4. Rufe Service auf: `result = await Client.AddMediaToPlaylistAsync(Id, request)` (gibt jetzt `DtoPlaylistAddResult` zurück)
5. **Verarbeite Response (neu):**
   - Falls erfolgreich (HTTP 200): 
     - Setze `entriesStatusMessage = result.Message` (z. B. "3 Titel hinzugefügt, 2 bereits vorhanden")
     - CSS-Klasse: `alert-success` oder `alert-info` (nicht `alert-warning`)
   - Falls HTTP 404: `entriesStatusMessage = "Medieninhalt wurde nicht gefunden"`
   - Falls HTTP 403: `entriesStatusMessage = "Sie sind nicht Besitzer dieser Playlist"`
   - Falls andere Exception: `entriesStatusMessage = "Fehler beim Hinzufügen: ..."`
6. Lade Einträge neu: `await LoadEntriesAsync()` (zeigt aktualisierte Liste)
7. Lösche Eingabewerte: `newEntryMediaId = 0` (Formular zurücksetzen)

Beteiligte Klassen/Komponenten: `PlaylistDetail.razor`, `VideoWebPlayerClient`, `DtoPlaylistAddResult`

---

## Neue Klassen

| Klasse | Typ | Zweck |
|--------|-----|-------|
| `DtoPlaylistAddResult` | DTO (Datenklasse) | Response-Struktur für `AddMediaToPlaylistAsync`: enthält Array der neu hinzugefügten Einträge, Anzahl übersprungener Duplikate und Benutzer-Nachricht |

---

## Änderungen an bestehenden Klassen

### `PlaylistService` (Service-Klasse)

- **Geänderte Methode:** `AddMediaToPlaylistAsync(playlistId, userId, mediaType, mediaId, cancellationToken)`
  - Rückgabetyp ändert sich von `Task<DtoPlaylistEntry>` zu `Task<DtoPlaylistAddResult>`
  - Logik-Änderung: Top-Level-Duplikate werfen keine Exception mehr, sondern werden gezählt
  - Kaskaden-Duplikate werden gezählt und nicht mehr stumm übersprungen
  - MediaType-Speicherung: Verwendet `parsedMediaType.ToString()` statt rohen Request-String
  - Neue Rückgabe-Struktur mit Message, AddedEntries[], SkippedCount

### `IPlaylistService` (Interface)

- **Geänderte Methode-Signatur:** `AddMediaToPlaylistAsync(...) : Task<DtoPlaylistAddResult>` 
  - Rückgabetyp von `Task<DtoPlaylistEntry>` zu `Task<DtoPlaylistAddResult>`
  - Alle Implementierungen müssen angepasst werden

### `PlaylistsController` (API-Controller)

- **Geänderte Methode:** `AddMediaToPlaylist(id, request)`
  - Response-Mapping: Gibt `DtoPlaylistAddResult` zurück (statt `DtoPlaylistEntry`)
  - Exception-Handling: `InvalidOperationException` für Top-Level-Duplikate wird nicht mehr geworfen; nur noch für Max-Item-Limit
  - Exception-Handling kann vereinfacht werden: `MapInvalidOperationException` wird nur noch für echte Fehler benötigt (Max-Limit)

### `VideoWebPlayerClient` (HTTP-Client)

- **Geänderte Methode:** `AddMediaToPlaylistAsync(playlistId, request)`
  - Rückgabetyp ändert sich von `Task<DtoPlaylistEntry>` zu `Task<DtoPlaylistAddResult>`
  - Generischer Typ in `HttpPostAsync<T>` wird angepasst

### `PlaylistDetail.razor` (UI-Komponente)

- **Geänderte Methode:** `AddEntryAsync()`
  - Verarbeitet neue `DtoPlaylistAddResult` Response-Struktur
  - Zeigt `result.Message` statt "bereits vorhanden" Fehlermeldung
  - HTTP 409 Catch-Block wird entfernt (wirft nicht mehr)
  - CSS-Klasse für Meldung: `alert-success` oder `alert-info` (nicht `alert-warning`)

### `PlaylistEntry` (Entity/Datenmodell)

- **Keine Code-Änderungen erforderlich**
- MediaType-Feld bleibt `string (Required)`
- **Daten-Migration erforderlich:** Normalisierung bestehender Werte in der DB

---

## Datenbankmigrationen

| Migrationsname | Betroffene Tabellen/Spalten | Beschreibung der Änderung |
|----------------|----------------------------|---------------------------|
| `NormalizePlaylistEntryMediaTypes` | `PlaylistEntries.MediaType` | Normalisierung aller bestehenden MediaType-Werte in der PlaylistEntries-Tabelle: `"movie"` → `"Movie"`, `"tvshow"` → `"TVShow"`, `"tvshowseason"` → `"TVShowSeason"`, `"tvshowEpisode"` → `"TVShowEpisode"`, `"moviecollection"` → `"MovieCollection"` (case-insensitive Ersetzung). SQL wird case-insensitiv geschrieben, um alle Varianten zu erfassen. |

**Implementierung (pseudocode):**
```
UP:
  UPDATE PlaylistEntries SET MediaType = 'Movie' WHERE LOWER(MediaType) = 'movie'
  UPDATE PlaylistEntries SET MediaType = 'TVShow' WHERE LOWER(MediaType) = 'tvshow'
  UPDATE PlaylistEntries SET MediaType = 'TVShowSeason' WHERE LOWER(MediaType) = 'tvshowseason'
  UPDATE PlaylistEntries SET MediaType = 'TVShowEpisode' WHERE LOWER(MediaType) = 'tvshowEpisode'
  UPDATE PlaylistEntries SET MediaType = 'MovieCollection' WHERE LOWER(MediaType) = 'moviecollection'

DOWN:
  -- Keine Rückwärts-Rekonstruktion möglich (Original-Casing verloren)
  -- Oder: Alle Werte zurücksetzen auf kanonische Werte (keine Änderung)
```

---

## Validierungsregeln

| Feld / Objekt | Regel | Fehlerfall |
|---------------|-------|------------|
| `AddMediaToPlaylistAsync(mediaType)` | `ParseMediaType(mediaType)` muss erfolgreich sein (case-insensitive Matching) | HTTP 400 `InvalidOperationException`: "Ungültiger Medientyp" |
| `AddMediaToPlaylistAsync(mediaId)` | `mediaId > 0` | HTTP 400 Bad Request (implizit via API-Validierung) |
| `AddMediaToPlaylistAsync()` gesamt | Ownership-Check: Benutzer muss Besitzer der Playlist sein | HTTP 403 `PlaylistAccessDeniedException` |
| `AddMediaToPlaylistAsync()` gesamt | Max-Item-Limit: `currentCount + entriesToAdd.Count <= MaxPlaylistItemCount` | HTTP 400 `InvalidOperationException`: "Playlist-Limit überschritten" |

Keine neuen Validierungen erforderlich — bestehende Validierungen bleiben gültig.

---

## Konfigurationsänderungen

Keine.

---

## Seiteneffekte und Risiken

- **Breaking Change - Rückgabetyp:** `VideoWebPlayerClient.AddMediaToPlaylistAsync()` und `IPlaylistService.AddMediaToPlaylistAsync()` ändern Rückgabetyp. Alle Verbraucher müssen angepasst werden: `PlaylistsController.AddMediaToPlaylist()`, `PlaylistDetail.razor.AddEntryAsync()`. **Risiko:** Compiler-Fehler werden schnell aufgedeckt, daher niedrig.

- **MediaType-Normalisierung in Datenbank:** Bestandsdaten werden migriert. Falls alte Clients mit nicht-normalisierten Werten in einer anderen Datenbank arbeiten, könnten Inkonsistenzen entstehen. **Mitigation:** Migration ist Single-Direction, kein Rollback möglich — sollte vor Deployment gründlich getestet werden.

- **HTTP 409 wird nicht mehr geworfen:** Externe API-Clients, die auf HTTP 409 für Duplikate prüfen, müssen angepasst werden. **Mitigation:** Dies ist eine Breaking Change für die Public API — sollte dokumentiert und ggf. mit Major-Version kommuniziert werden.

- **Exception-Handling im Controller:** `MapInvalidOperationException` wird für Duplikate nicht mehr verwendet. Der Code ist für Max-Item-Limit weiterhin notwendig. **Risiko:** niedrig — Exception wird weiterhin für andere Fehler geworfen.

---

## Umsetzungsreihenfolge

1. **Neue DTO `DtoPlaylistAddResult` anlegen**
   - Voraussetzungen: Keine
   - Beschreibung: Erstelle Klasse `DtoPlaylistAddResult.cs` in `VideoWebPlayer.Client\Models\` mit Properties: `TopLevelEntry`, `AddedEntries[]`, `SkippedDuplicateCount`, `Message`. Kein Code-Generieren nötig, nur Klassen-Datei.

2. **Datenbankmigrationen erstellen**
   - Voraussetzungen: EF Core CLI/Tooling installiert, Datenbank erreichbar
   - Beschreibung: Erstelle neue Migration `NormalizePlaylistEntryMediaTypes` mit SQL-Statements zur Normalisierung aller bestehenden `PlaylistEntry.MediaType` Werte (siehe Migrations-Tabelle oben).

3. **`PlaylistService.AddMediaToPlaylistAsync` überarbeiten**
   - Voraussetzungen: `DtoPlaylistAddResult` Klasse vorhanden (Schritt 1)
   - Beschreibung: 
     - Ändere Rückgabetyp von `Task<DtoPlaylistEntry>` zu `Task<DtoPlaylistAddResult>`
     - Implementiere MediaType-Normalisierung: `normalizedMediaType = parsedMediaType.ToString()` nach `ParseMediaType`
     - Entferne Exception für Top-Level-Duplikate; erhöhe stattdessen Zähler `skippedCount`
     - Implementiere Zähler für Cascade-Duplikate
     - Baue neue Response-Struktur mit Message basierend auf `skippedCount` und `entriesToAdd.Count`
     - Speichere alle neuen Einträge mit normalisiertem MediaType

4. **Interface `IPlaylistService.AddMediaToPlaylistAsync` aktualisieren**
   - Voraussetzungen: `DtoPlaylistAddResult` Klasse vorhanden (Schritt 1)
   - Beschreibung: Ändere Rückgabetyp-Signatur von `Task<DtoPlaylistEntry>` zu `Task<DtoPlaylistAddResult>`

5. **`PlaylistsController.AddMediaToPlaylist` anpassen**
   - Voraussetzungen: Service-Methode überarbeitet (Schritt 3), Interface aktualisiert (Schritt 4)
   - Beschreibung:
     - Response-Mapping: `DtoPlaylistAddResult` wird direkt vom Service zurückgegeben (keine zusätzliche Umwandlung nötig)
     - Exception-Handling: Entferne Catch-Block für "bereits in dieser Playlist vorhanden" (wird nicht mehr geworfen)
     - Behalte Exception-Handling für echte Fehler (Max-Limit, Ownership, Media nicht gefunden)

6. **`VideoWebPlayerClient.AddMediaToPlaylistAsync` aktualisieren**
   - Voraussetzungen: `DtoPlaylistAddResult` Klasse vorhanden (Schritt 1), Service-Signatur geändert (Schritt 3)
   - Beschreibung: Ändere Rückgabetyp von `Task<DtoPlaylistEntry>` zu `Task<DtoPlaylistAddResult>` in der Methoden-Signatur und im generischen Typ `HttpPostAsync<DtoPlaylistAddResult>`

7. **`PlaylistDetail.razor.AddEntryAsync` überarbeiten**
   - Voraussetzungen: Service-Methode überarbeitet (Schritt 3), Client aktualisiert (Schritt 6)
   - Beschreibung:
     - Verarbeite neue `DtoPlaylistAddResult` Response-Struktur
     - Setze `entriesStatusMessage = result.Message` (statt Fehlermeldung)
     - Entferne HTTP 409 Catch-Block
     - Ändere CSS-Klasse für Meldungsanzeige: Verwende `alert-success` oder `alert-info` statt `alert-warning`
     - Behalte Fehlerbehandlung für HTTP 404, 403 und andere Exceptions

8. **Migrationsdatei anwenden**
   - Voraussetzungen: Migration erstellt (Schritt 2), Service-Code aktualisiert (Schritt 3)
   - Beschreibung: Führe Datenbankmigrationen durch: `dotnet ef database update` oder via Azure Data Studio. Prüfe, dass bestehende MediaType-Werte normalisiert wurden.

9. **Unit-Tests anpassen und erweitern**
   - Voraussetzungen: Service-Methode überarbeitet (Schritt 3)
   - Beschreibung:
     - Umstrukturiere Test `AddMedia_Duplicate_ThrowsInvalidOperationException` → `AddMedia_TopLevelDuplicate_SkipsAndReturnsCount`: Verifies, dass Duplikat nicht wirft, sondern `DtoPlaylistAddResult` mit `SkippedCount = 1` zurückgibt
     - Passe Test `AddMedia_CascadeWithDuplicates_SkipsDuplicates` an: Verifies, dass neue Response-Struktur mit SkippedCount korrekt ist
     - Füge neue Tests hinzu: `AddMedia_PartialDuplicates_AddedNewAndSkipped`, `AddMedia_AllDuplicates_ReturnsZeroAddedCount`, `AddMedia_NormalizeMediaType_CaseInsensitiveDuplicateDetection`

10. **Integrationstests schreiben**
    - Voraussetzungen: Unit-Tests angepasst (Schritt 9), Service-Methode funktioniert (Schritt 3)
    - Beschreibung:
      - E2E-Test: Serie zweimal hinzufügen → prüfe neue Episoden werden hinzugefügt, alte übersprungen
      - E2E-Test: Serie hinzufügen, Episode entfernen, Serie erneut hinzufügen → Episode wird wieder hinzugefügt
      - E2E-Test: Normalisierung über API → `MediaType="Movie"` und `"movie"` mit gleicher ID → Duplikat erkannt

11. **UI/Component-Tests schreiben** (falls bUnit o. ä. vorhanden)
    - Voraussetzungen: `PlaylistDetail.razor` angepasst (Schritt 7), Integrationstests bestehen (Schritt 10)
    - Beschreibung:
      - Test: Duplikat-Addition zeigt korrekte Message (z. B. "3 Titel hinzugefügt, 2 bereits vorhanden")
      - Test: Meldung nutzt `alert-success`/`alert-info` CSS-Klasse (nicht `alert-danger`)
      - Test: Eingabewerte werden nach erfolgreichem Add zurückgesetzt

12. **Dokumentation aktualisieren**
    - Voraussetzungen: Alle Implementierungsschritte abgeschlossen (Schritte 1–11)
    - Beschreibung:
      - `docs/help/playlists-api.md`: Dokumentiere neue Response-Struktur (`DtoPlaylistAddResult`), zeige Beispiele für HTTP 200 OK bei Duplikaten (statt 409)
      - `docs/help/playlists-business-rules.md`: Dokumentiere vereinheitlichte Duplikat-Regel und MediaType-Normalisierung
      - `docs/API.md` (falls vorhanden): Update HTTP-Status-Codes (409 nicht mehr möglich für Duplikate)

---

## Tests

### Neue Tests

| Test / Hilfsmethode | Testklasse | Was wird geprüft / bereitgestellt? |
|--------------------|------------|-------------------------------------|
| `AddMedia_TopLevelDuplicate_SkipsAndReturnsCount` | `PlaylistServiceTests_AddMedia` | Top-Level-Duplikat wirft keine Exception, sondern gibt `DtoPlaylistAddResult` mit `SkippedCount=1`, `AddedEntries.Length=0` zurück |
| `AddMedia_PartialDuplicates_AddedNewAndSkipped` | `PlaylistServiceTests_AddMedia` | Serie mit 3 Episoden, 1 existiert bereits: `AddedEntries.Length=2`, `SkippedCount=1`, Message zeigt Statistik |
| `AddMedia_AllDuplicates_ReturnsZeroAddedCount` | `PlaylistServiceTests_AddMedia` | Serie erneut hinzufügen (alle Episoden vorhanden): `AddedEntries.Length=0`, `SkippedCount=X`, Message: "Alle ... waren bereits vorhanden" |
| `AddMedia_NormalizeMediaType_CaseInsensitiveDuplicateDetection` | `PlaylistServiceTests_AddMedia` | Film mit `"Movie"` hinzufügen, dann mit `"movie"` (lowercase): Zweiter Aufruf erkauft Duplikat, `SkippedCount=1` |
| E2E: Serie zweimal hinzufügen | `PlaylistsControllerTests_Entries` oder neue Integration-Test-Klasse | Erste Serie: 1 neue Serie + N Episoden hinzugefügt; Zweite Serie: 0 neue, N übersprungen, Message entsprechend |
| E2E: Serie entfernen und erneut hinzufügen | Integration-Test-Klasse | Serie → Episode entfernen → Serie erneut → Episode wird wieder hinzugefügt, `SkippedCount=N-1` |
| E2E: MediaType-Normalisierung über API | Integration-Test-Klasse | POST mit `"Movie"`, dann POST mit `"movie"`: Zweiter Request sieht Duplikat, HTTP 200 mit SkippedCount=1 |
| PlaylistDetail AddEntryAsync mit Duplikat | bUnit Component-Test (falls vorhanden) | UI zeigt Message aus Response, nutzt `alert-success` Klasse, lädt Einträge neu |
| PlaylistDetail AddEntryAsync mit Error | bUnit Component-Test (falls vorhanden) | UI zeigt Fehler-Message für HTTP 404/403, nutzt `alert-danger` Klasse |

### Betroffene bestehende Tests

| Test / Testklasse | Grund der Anpassung |
|-------------------|---------------------|
| `AddMedia_Duplicate_ThrowsInvalidOperationException` | ❌ Exception wird nicht mehr geworfen → Test muss zu `AddMedia_TopLevelDuplicate_SkipsAndReturnsCount` umstrukturiert werden: Verifies `DtoPlaylistAddResult` mit `SkippedCount > 0` statt Exception |
| `AddMedia_CascadeWithDuplicates_SkipsDuplicates` | ⚠️ Test funktioniert noch, aber Assertions müssen angepasst werden: Verifies neuer Response-Struktur `DtoPlaylistAddResult` mit Zähler-Tracking von Duplikaten |
| `AddMedia_MaxItemCountExceeded_ThrowsInvalidOperationException` | ⚠️ Exception wird weiterhin geworfen (für Max-Limit), Test bleibt gültig, aber neuer Return-Type ist irrelevant (Exception wirft) |
| `AddMedia_CascadeExceedsMaxItemCount_ThrowsInvalidOperationExceptionWithoutPartialInsert` | ⚠️ Wie oben: Exception bleibt, Return-Type ist irrelevant |

### E2E-Tests (primärer Funktionsnachweis)

| Priorität | Szenario | Testdatei / Testklasse | Abgedecktes Akzeptanzkriterium | Warum E2E nötig ist |
|-----------|----------|------------------------|-------------------------------|-------------------|
| Pflicht | Serie zweimal hinzufügen | `PlaylistsControllerTests_Entries` oder `PlaylistIntegrationTests` | HTTP 200 OK, Message zeigt "X neue Titel hinzugefügt, Y übersprungen" | Der tatsächliche Benutzerfluss umfasst HTTP-Request → Service → Datenbank-Abfrage → Cascade-Abfrage → Response-Verarbeitung; nur E2E-Test verifiziert alle Schichten zusammen |
| Pflicht | MediaType-Normalisierung über API | `PlaylistIntegrationTests` | `POST "Movie"` dann `POST "movie"` → beide referenzieren gleichen DB-Eintrag, zweiter HTTP 200 mit SkippedCount=1 | E2E testet die tatsächliche HTTP-Serialisierung, Datenbankpersistenz und Duplikat-Prüfung über die vollständige Schicht |
| Pflicht | UI: Duplikat-Message angezeigt | Component-Test für `PlaylistDetail.razor` oder E2E-Browser-Test | `AddEntryAsync` zeigt Message aus Response in `alert-success` Klasse, nicht als Fehler | Nur ein Component- oder Browser-Test verifiziert, dass die UI die neue Response-Struktur korrekt interpretiert und anzeigt |
| Stark empfohlen | Serie → Episode entfernen → Serie erneut | `PlaylistIntegrationTests` | Episode wird nach Entfernung wiedera hinzugefügt bei erneuter Serie-Addition | Validiert korrektes Cascade-Tracking und Duplikat-Reset nach Löschung |
| Stark empfohlen | Alle Titel bereits vorhanden | `PlaylistIntegrationTests` | HTTP 200 mit Message "Alle X Titel waren bereits vorhanden", `AddedEntries.Length=0` | Edge-Case: Benutzer versucht identische Serie erneut → UI sollte Info zeigen, nicht Fehler |

Bestehende E2E-Tests, die angepasst werden müssen:

| Test / Testklasse | Grund der Anpassung |
|-------------------|---------------------|
| Falls vorhanden: "Duplikat hinzufügen führt zu Fehler" E2E-Test | ❌ HTTP 409 wird nicht mehr geworfen → Test muss angepasst oder gelöscht werden (Anforderung hat sich geändert) |

Falls noch keine Component-Tests für `PlaylistDetail.razor` vorhanden sind: **E2E-Browser-Test (Selenium o. ä.) wird benötigt**, um zu verifizieren, dass die UI die neue Message korrekt anzeigt. Unit-Tests für Service-Logik allein reichen nicht aus, da UI-Verarbeitung und Message-Anzeige nicht abgedeckt wären.

---

## Offene Punkte

Keine — die Anforderung ist vollständig spezifiziert, und alle technischen Details sind durch die Bestandsaufnahme geklärt.

---

## Anhang: Kritische Code-Stellen (Referenz für Implementierung)

### PlaylistService.cs — Zeilen 141–206 (AddMediaToPlaylistAsync)

**Zu ändernde Zeilen:**

- Zeile 145: `var parsedMediaType = ParseMediaType(mediaType);` → **danach:** `var normalizedMediaType = parsedMediaType.ToString();`
- Zeile 162–163: **Ersetzen** Exception durch Zähler-Erhöhung
- Zeile 182: `MediaType = mediaType,` → `MediaType = normalizedMediaType,`
- Zeile 169: Cascade-Filter nutzt `ToString()`, aber muss auf Zähler erweitert werden
- Zeile 205: **Rückgabe ersetzen** durch neue Response-DTO-Konstruktion

### PlaylistsController.cs — Zeilen 209–244 (AddMediaToPlaylist)

**Zu ändernde Zeilen:**

- Zeile 234–238: Catch-Block für Duplikat kann vereinfacht oder angepasst werden

### PlaylistDetail.razor — Zeilen 188–212 (AddEntryAsync)

**Zu ändernde Zeilen:**

- Zeile 57–60: HTTP 409 Catch-Block kann entfernt oder angepasst werden
- Zeile 55–59: Response-Verarbeitung erweitern für neue `DtoPlaylistAddResult` Struktur
- Zeile 34–35: CSS-Klasse für Meldungsanzeige anpassen: `alert-success` statt `alert-warning`
