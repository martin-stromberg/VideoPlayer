# Umsetzungsplan: Nachbesserung Entwicklungsschritt 6 – Korrektionen zum Weiterschauen mit Playlist-Bezug (Runde 1)

## Übersicht

Der Plan behebt vier kritische Fehler in Entwicklungsschritt 6, die entstanden sind, weil dasselbe Video mehrfach in der Weiterschauen-Liste mit unterschiedlichen Playlist-Bezügen erscheinen kann. Die Fehler betreffen die Data-Mapping-Schicht (fehlende eindeutige Identifizierung), die Datenbankintegration (Unique-Index-Konflikte) und die UI-Rendering-Logik (Blazor-Key-Duplikate und fehlende Wiedergabeposition). Der Plan kombiniert Datenmodell-Erweiterungen, Geschäftslogik-Anpassungen und Regressionstests.

## Designentscheidungen

| Komponente / Bereich | Gewählter Ansatz | Begründung |
|----------------------|-----------------|------------|
| **Dictionary-Schlüsselung (Problem 1)** | Neue Eigenschaft `ContinueWatchingDto.Id` mit eindeutiger Datenbank-ID der `ContinueWatchingEntry` als Schlüssel | Ermöglicht eindeutige Zuordnung von Display-Daten zu Datenbankzeilen; ist minimal invasiv und folgt Value-Object-Muster (jede DTO-Instanz hat eine eindeutige Identität). Alternative (Entity-ID + Playlist-ID kombiniert) wäre fehleranfälliger. |
| **Unique-Index-Konflikt beim Löschen (Problem 3)** | Explizite Konfliktauflösungslogik in `PlaylistService.DeletePlaylistAsync()` mit vorgezogener Prüfung | Besser als blind auf `SaveChangesAsync()` zu verlassen oder FK-Aktion zu ändern; erhält die Geschäftslogik-Kontrollierbarkeit. Konfliktauflösung: playlist-gebundener Eintrag wird gelöscht, wenn bereits ein playlist-loser Eintrag existiert. |
| **Wiedergabeposition im Playlist-Kontext (Problem 4)** | Neue Eigenschaft `DtoPlaylistPlaybackStart.StartPositionSeconds` mit Befüllung aus `ContinueWatchingEntry.PositionSeconds` | Minimal invasive Erweiterung bestehender DTO; konsistent mit bereits verwendetem Muster in `ContinueWatchingList.razor`. |

## Programmabläufe

### Ablauf 1: Weiterschauen-Liste mit mehreren Varianten desselben Videos laden

1. UI-Komponente `ContinueWatchingList.razor` aufgerufen
2. `LoadItemsAsync()` wird aufgerufen
3. `ContinueWatchingService.GetListAsync(user)` wird aufgerufen → lädt alle `ContinueWatchingEntry`-Zeilen für den Benutzer, inklusive mehrerer Varianten desselben Videos
4. Rückgabe Liste von `ContinueWatchingDto` mit neuer `Id`-Eigenschaft (gesetzt aus `ContinueWatchingEntry.Id`)
5. `EnrichPlaylistInfoAsync()` wird aufgerufen → befüllt `PlaylistName` und `PlaylistEntryId` für Einträge mit `PlaylistId`
6. Dictionaries (`titles`, `images`, `links`, `playlistSubtitles`) werden befüllt, **indiziert nach `it.Id`** statt `it.Entry.Id`
7. Razor-Markup iteriert über Liste, Zugriff auf Dictionary-Werte erfolgt über eindeutige `it.Id`
8. `@key="it.Id"` garantiert Eindeutigkeit pro Datenbank-Zeile → kein Blazor-Key-Duplikat

Beteiligte Klassen/Komponenten: `ContinueWatchingList.razor`, `ContinueWatchingService`, `ContinueWatchingDto`

### Ablauf 2: Kontextaktionen (Hide/Skip) mit mehreren Varianten

1. Benutzer wählt Kontextaktion (z. B. "Ausblenden") für einen `MediaBox`
2. `HandleContextActionAsync(item, action)` wird aufgerufen mit Übergabe des vollständigen `ContinueWatchingDto`-Objekts
3. Methode extrahiert `item.Id` (eindeutige DB-ID) und `item.PlaylistId` zur korrekten Zuordnung
4. Server-seitige API wird aufgerufen mit diesen Identifikatoren
5. Nur der spezifische Eintrag wird bearbeitet; andere Varianten bleiben unverändert

Beteiligte Klassen/Komponenten: `ContinueWatchingList.razor`, `ContinueWatchingService`, API-Endpunkte

### Ablauf 3: Playlist löschen mit konkurrierendem playlist-losem Eintrag

1. Benutzer löscht eine Playlist über die UI
2. `PlaylistService.DeletePlaylistAsync(playlistId, userId)` wird aufgerufen
3. **Neue Logik (Konfliktauflösung):**
   - Finde alle `ContinueWatchingEntry`-Zeilen mit dieser `PlaylistId`
   - Für jede gefundene Zeile: Prüfe, ob bereits ein playlist-loser Eintrag existiert (gleicher `UserId`, gleiches `MovieId` oder `TVShowEpisodeId`, `PlaylistId = NULL`)
   - Falls ja: Lösche den playlist-gebundenen Eintrag
   - Falls nein: Setze `PlaylistId = NULL` (behalte den Eintrag)
4. Nach Konfliktauflösung: Lösche die Playlist selbst
5. `SaveChangesAsync()` wird aufgerufen → keine Unique-Constraint-Verletzung mehr

Beteiligte Klassen/Komponenten: `PlaylistService`, `ContinueWatchingService` (möglicherweise für Hilfsmethode)

### Ablauf 4: Fortsetzen über Playlist mit gespeicherter Position

1. Benutzer navigiert zu `/playlists/{playlistId}?entryId={entryId}`
2. `PlaylistDetail.razor` initialisiert, lädt Playlist-Startdaten
3. `PlaylistApiClient.StartPlaylistAsync(playlistId, entryId)` wird aufgerufen
4. Server liefert `DtoPlaylistPlaybackStart` mit **neuer Eigenschaft `StartPositionSeconds`** (aus `ContinueWatchingEntry.PositionSeconds`)
5. `PlaylistDetail.razor` übergibt diese Position an `VideoPlayer`-Komponente als Parameter
6. Player startet an der gespeicherten Position statt bei 0:00

Beteiligte Klassen/Komponenten: `PlaylistDetail.razor`, `PlaylistApiClient`, `DtoPlaylistPlaybackStart`, `VideoPlayer`-Komponente

## Neue Klassen

Keine neuen Klassen erforderlich. Alle Änderungen sind Erweiterungen bestehender Klassen.

## Änderungen an bestehenden Klassen

### `ContinueWatchingDto` (Datenmodell-Klasse / DTO)

- **Neue Eigenschaften:** 
  - `Id: long` — Datenbank-ID der zugehörigen `ContinueWatchingEntry` (eindeutige Identität pro Datenbankzeile, unabhängig von Media-ID oder Playlist-ID)

### `ContinueWatchingService` (Service-Klasse)

- **Geänderte Methoden:** 
  - `GetListAsync(user, cancellationToken)` — Muss `ContinueWatchingDto.Id` mit der Datenbank-ID der `ContinueWatchingEntry` befüllen (aktuell nicht vorhanden, Zeile 113–142)
  - `GetListAsync` → Zeile 113–142: Für jeden `ContinueWatchingEntry` entsprechende `ContinueWatchingDto.Id` setzen
- **Neue Methoden (privat):** 
  - `ResolvePlaylistDeletionConflictsAsync(playlistId, userId, cancellationToken)` — Prüft und behandelt Unique-Index-Konflikte beim Löschen einer Playlist (findet playlist-gebundene Einträge, prüft auf playlist-lose Duplikate, entfernt Konflikte)

### `PlaylistService` (Service-Klasse)

- **Geänderte Methoden:** 
  - `DeletePlaylistAsync(playlistId, userId, cancellationToken)` — Ruft vor dem Löschen explizite Konfliktauflösungslogik auf; statt blind auf `SaveChangesAsync()` zu verlassen (Zeile 130–136)
  - Neue Ablauf: (1) Konfliktauflösung durchführen, (2) Playlist löschen, (3) `SaveChangesAsync()` aufrufen

### `DtoPlaylistPlaybackStart` (Datenmodell-Klasse / DTO)

- **Neue Eigenschaften:** 
  - `StartPositionSeconds: long` — Wiedergabeposition in Sekunden (aus `ContinueWatchingEntry.PositionSeconds` befüllt)

### `ContinueWatchingList.razor` (Razor-Komponente / UI)

- **Geänderte Codeabschnitte:** 
  - Zeilen 18–21: Dictionary-Indizierung bleibt (Syntax ändert sich nicht)
  - Zeilen 50–90: Dictionary-Befüllung ändert sich: Statt `titles[it.Entry.Id]` → `titles[it.Id]`, analog für `images`, `links`, `playlistSubtitles`
  - Zeilen 145–148: Zugriff auf Dictionaries ändert sich: `if (titles.ContainsKey(it.Id))` statt `it.Entry.Id`, ebenso bei allen Wert-Zugriffen
  - Zeile 147: `@key="it.Id"` statt `@key="it.Entry.Id"` — garantiert Eindeutigkeit pro Datenbank-Zeile

### `PlaylistDetail.razor` (Razor-Komponente / UI)

- **Geänderte Codeabschnitte:** 
  - Zeilen 86–97: `VideoPlayer`-Komponente wird nun mit `StartPositionSeconds`-Parameter initialisiert (Übergabe von `playbackStart.StartPositionSeconds`)

### `ContinueWatchingEntryConfiguration` (EF-Core-Konfigurationsklasse)

- **Betroffene Konfiguration:** 
  - FK-Aktion auf Playlist bleibt `OnDelete(DeleteBehavior.SetNull)` (keine Änderung der Konfiguration selbst; die Konfliktauflösung erfolgt auf Anwendungslogik-Ebene)

## Datenbankmigrationen

Keine. 

Begründung:
- `ContinueWatchingDto.Id` ist nicht persistent in der Datenbank — es ist nur eine Projektion des bestehenden `ContinueWatchingEntry.Id`
- Die Unique-Indizes sind bereits vorhanden (Anforderung, Zeile 27–35 in `ContinueWatchingEntryConfiguration`)
- Die FK-Aktion bleibt `OnDelete(DeleteBehavior.SetNull)` (kein Datenbankschema-Change)

## Validierungsregeln

Keine neuen Validierungsregeln erforderlich.

Begründung: Die Validierungen für `ContinueWatchingEntry` (Eindeutigkeit auf `(UserId, MovieId/TVShowEpisodeId, PlaylistId)`, FK-Constraints) sind bereits in der Datenbankmodellkonfiguration definiert. Die neuen Eigenschaften `ContinueWatchingDto.Id` und `DtoPlaylistPlaybackStart.StartPositionSeconds` sind reine Datenleseoperationen ohne Nutzer-Input.

## Konfigurationsänderungen

Keine.

## Seiteneffekte und Risiken

- **Dictionaries in `ContinueWatchingList.razor`:** Die Umindizierung von `it.Entry.Id` auf `it.Id` ist eine lokalräumliche Änderung innerhalb der Razor-Komponente. Andere Zugriffe auf die Dictionaries müssen überprüft werden (siehe Plan Schritt „Code-Review der Dictionaries in `ContinueWatchingList.razor`").
  
- **API-Kompatibilität – `ContinueWatchingDto.Id`:** Neue Eigenschaft in existierendem DTO. Client-seitig: Die Eigenschaft wird durch die neue Befüllung im Service verfügbar; bestehende Clients, die die Eigenschaft nicht verwenden, sind nicht betroffen (additive Änderung). Server-seitig: Keine Breaking Change.

- **API-Kompatibilität – `DtoPlaylistPlaybackStart.StartPositionSeconds`:** Neue Eigenschaft in existierendem DTO. Client-seitig: Bestehende Clients ignorieren neue Eigenschaften. Server-seitig: Keine Breaking Change. (**Hinweis:** Falls ein externer Client diese Eigenschaft zwingend erwartet und bei Abwesenheit nicht korrekt fallback wird, könnte dies zu Problemen führen — aber die Anforderung deutet darauf hin, dass `StartPositionSeconds` immer befüllt wird.)

- **FK-Aktion und Constraints:** Wenn die Konfliktauflösungslogik fehlerhaft ist oder nicht aufgerufen wird, können Unique-Constraint-Verletzungen weiterhin auftreten. Testabdeckung ist kritisch (siehe Tests).

- **EF-InMemory-Testlücke:** `ContinueWatchingServiceTestBase` nutzt EF-InMemory, das keine Unique-Indizes oder FK-Aktionen durchsetzt. Problem 3 kann mit EF-InMemory nicht getestet werden. Abhilfe: Neue SQLite-basierte Integrationstests (siehe Tests-Abschnitt).

## Umsetzungsreihenfolge

1. **`ContinueWatchingDto.Id`-Eigenschaft hinzufügen**
   - Voraussetzungen: Keine
   - Beschreibung: Füge in `VideoWebPlayer.Client/Models/ContinueWatchingDto.cs` eine neue Eigenschaft `public long Id { get; set; }` hinzu

2. **`ContinueWatchingService.GetListAsync()` anpassen – `Id` befüllen**
   - Voraussetzungen: Schritt 1 abgeschlossen
   - Beschreibung: Änderung in `VideoWebPlayer/Services/ContinueWatchingService.cs` Zeile 113–142. Für jeden erzeugten `ContinueWatchingDto` die `Id`-Eigenschaft mit `entry.Id` (Datenbank-ID der `ContinueWatchingEntry`) setzen

3. **`ContinueWatchingList.razor` Dictionaries umindizieren**
   - Voraussetzungen: Schritt 1 und 2 abgeschlossen
   - Beschreibung: Änderung in `VideoWebPlayer/Components/Shared/Home/ContinueWatchingList.razor`:
     - Zeilen 50–90: Alle Dictionary-Befüllungen von `it.Entry.Id` zu `it.Id` ändern
     - Zeilen 145–148: Alle Dictionary-Zugriffe von `it.Entry.Id` zu `it.Id` ändern
     - Zeile 147: `@key`-Attribut von `@key="it.Entry.Id"` zu `@key="it.Id"` ändern

4. **`DtoPlaylistPlaybackStart.StartPositionSeconds`-Eigenschaft hinzufügen**
   - Voraussetzungen: Keine
   - Beschreibung: Füge in `VideoWebPlayer.Client/Models/DtoPlaylistPlaybackStart.cs` eine neue Eigenschaft `public long StartPositionSeconds { get; set; }` hinzu

5. **API-Endpunkt `PlaylistApiClient.StartPlaylistAsync()` anpassen – `StartPositionSeconds` befüllen**
   - Voraussetzungen: Schritt 4 abgeschlossen
   - Beschreibung: Änderung im Server-seitigen Endpunkt (z. B. in `VideoWebPlayerController` oder zugehörigem Service), der `DtoPlaylistPlaybackStart` erzeugt. `StartPositionSeconds` mit `ContinueWatchingEntry.PositionSeconds` befüllen (eventuell in Sekunden konvertieren, falls intern Millisekunden oder `TimeSpan` verwendet)

6. **`PlaylistDetail.razor` anpassen – `StartPositionSeconds` an VideoPlayer übergeben**
   - Voraussetzungen: Schritt 4 und 5 abgeschlossen
   - Beschreibung: Änderung in `VideoWebPlayer/Components/Playlists/PlaylistDetail.razor` Zeilen 86–97. `VideoPlayer`-Komponente wird mit neuem Parameter aufgerufen: `StartPositionSeconds="@playbackStart.StartPositionSeconds"`

7. **`ContinueWatchingService.ResolvePlaylistDeletionConflictsAsync()` implementieren**
   - Voraussetzungen: Keine (aber sollte vor Schritt 8 abgeschlossen sein)
   - Beschreibung: Neue private Methode in `VideoWebPlayer/Services/ContinueWatchingService.cs`:
     - Parameter: `playlistId: long, userId: string, cancellationToken: CancellationToken`
     - Logik:
       1. Laden aller `ContinueWatchingEntry`-Zeilen mit dieser `PlaylistId` für den Benutzer
       2. Für jede Zeile: Prüfen, ob ein playlist-loser Eintrag existiert (same `UserId`, same `MovieId` oder `TVShowEpisodeId`, `PlaylistId = NULL`)
       3. Falls ja: Lösche die playlist-gebundene Zeile
       4. Falls nein: Behalte die Zeile (wird später auf NULL gesetzt)
     - Rückgabe: Void (die Änderungen werden durch die DbContext-Tracking verwaltet, aber noch nicht mit `SaveChangesAsync()` committed)

8. **`PlaylistService.DeletePlaylistAsync()` anpassen – Konfliktauflösung aufrufen**
   - Voraussetzungen: Schritt 7 abgeschlossen
   - Beschreibung: Änderung in `VideoWebPlayer/Services/PlaylistService.cs` Zeile 130–136:
     - Ersetze die bloße `SaveChangesAsync()`-Aufrufe durch:
       1. Aufruf von `ContinueWatchingService.ResolvePlaylistDeletionConflictsAsync(playlistId, userId, cancellationToken)` (z. B. über Dependency Injection)
       2. Dann: Lade die Playlist und lösche sie
       3. Dann: `SaveChangesAsync()` aufrufen

9. **Regressionstests schreiben – Problem 1 & 2 (Dictionary-Schlüsselung und Blazor-Keys)**
   - Voraussetzungen: Schritte 1–3 abgeschlossen
   - Beschreibung: Neue bUnit-Testmethode in `VideoWebPlayer.Tests/Components/ContinueWatchingListTests.cs` (oder neue Testklasse):
     - Testname: `ContinueWatchingList_MultipleVariantsOfSameMedia_RendersWithUniqueKeys`
     - Szenario: Drei `ContinueWatchingDto`-Einträge mit `Entry.Id = 42` (Film), unterschiedliche `PlaylistId` (1, 2, NULL)
     - Verifikation: Komponente rendert, jeder `MediaBox` hat eindeutigen `@key`, keine `InvalidOperationException` beim Rendern
     - Verifikation: Jeder `MediaBox` zeigt korrekte `Title`, `LinkUrl`, `Subtitle` (Playlist-Badge nur bei nicht-NULL PlaylistId)

10. **Regressionstests schreiben – Problem 3 (Unique-Index-Konflikt beim Löschen)**
    - Voraussetzungen: Schritte 7–8 abgeschlossen
    - Beschreibung: Neue SQLite-Integrationstestmethode (in neuer Testklasse `PlaylistServiceTests_DeleteWithConflictResolution.cs` oder in `PlaylistServiceTests_Delete`):
      - Testname: `DeletePlaylist_WithCompetingNullPlaylistEntry_ResolvesConflictAndDeletesPlaylist`
      - Szenario: SQLite-Datenbank mit zwei `ContinueWatchingEntry`-Zeilen für Film 42:
        - Zeile 1: `UserId=u1, MovieId=42, PlaylistId=1, PositionSeconds=300`
        - Zeile 2: `UserId=u1, MovieId=42, PlaylistId=NULL, PositionSeconds=100` (playlist-los)
      - Aktion: `PlaylistService.DeletePlaylistAsync(playlistId=1, userId=u1)`
      - Verifikation: Keine `DbUpdateException` mit "UNIQUE constraint failed"
      - Verifikation: Zeile 1 wird gelöscht (oder auf NULL gesetzt, je nach Geschäftslogik — Anforderung Zeile 75 deutet auf Deletion hin)
      - Verifikation: Zeile 2 bleibt unverändert
      - Verifikation: Playlist wird gelöscht

11. **Regressionstests schreiben – Problem 4 (Fehlende Wiedergabeposition)**
    - Voraussetzungen: Schritte 4–6 abgeschlossen
    - Beschreibung: Unit-Test + optional E2E-Test:
      - Unit-Test: `DtoPlaylistPlaybackStart_WithStartPositionSeconds_IsCorrectlyPopulated`
        - Prüft, dass der API-Endpunkt `StartPositionSeconds` korrekt mit `ContinueWatchingEntry.PositionSeconds` befüllt
      - E2E-Test (optional): Benutzer navigiert zu Playlist-Detail-Page für ein Video mit gespeicherter Position (z. B. 600 Sekunden), verifiziert, dass Player bei dieser Position startet (oder über TimeSpan-Vergleich verifiziert wird)

12. **Unit-Test: ContinueWatchingList mit Kontextaktionen und mehrfachen Varianten**
    - Voraussetzungen: Schritte 1–3 abgeschlossen
    - Beschreibung: Zusätzliche bUnit-Testmethode in `ContinueWatchingListTests`:
      - Testname: `ContinueWatchingList_ContextActionOnMultipleVariants_TargetsCorrectEntryById`
      - Szenario: Drei Einträge wie oben; Benutzer wählt "Ausblenden" für einen bestimmten Eintrag
      - Verifikation: `HandleContextActionAsync` wird mit korrektem `item.Id` und `item.PlaylistId` aufgerufen (nicht mit den anderen Einträgen verwechselt)
      - Verifikation: Nach Ausblenden bleibt die Liste konsistent, andere Einträge sind noch vorhanden

## Tests

### Neue Tests

| Test / Hilfsmethode | Testklasse | Was wird geprüft / bereitgestellt? |
|--------------------|------------|-------------------------------------|
| `ContinueWatchingList_MultipleVariantsOfSameMedia_RendersWithUniqueKeys` | `ContinueWatchingListTests` | Problem 1 & 2: bUnit-Rendering mit mehrfachen Varianten desselben Videos; verifiziert eindeutige @key-Werte und korrekte Display-Daten pro Eintrag |
| `ContinueWatchingList_ContextActionOnMultipleVariants_TargetsCorrectEntryById` | `ContinueWatchingListTests` | Problem 1 & 2: Kontextaktion auf einem von mehreren Einträgen desselben Videos; verifiziert Routing zur korrekten Datenbank-ID |
| `DeletePlaylist_WithCompetingNullPlaylistEntry_ResolvesConflictAndDeletesPlaylist` | `PlaylistServiceTests_DeleteWithConflictResolution` (neue Klasse oder in `PlaylistServiceTests_Delete`) | Problem 3: SQLite-Integrationtest mit konkurrierendem playlist-losem Eintrag; verifiziert Konfliktauflösung ohne `DbUpdateException` |
| `DtoPlaylistPlaybackStart_WithStartPositionSeconds_IsCorrectlyPopulated` | `PlaylistServiceTests` oder neue Testklasse | Problem 4: Unit-Test prüft, dass `StartPositionSeconds` vom API-Endpunkt korrekt befüllt wird |
| `PlaylistDetail_WithStartPositionSeconds_PassesToVideoPlayer` | `PlaylistDetailTests` (neue Klasse, bUnit) oder E2E | Problem 4: Verifiziert, dass `PlaylistDetail.razor` den Parameter an `VideoPlayer` übergibt |

### Betroffene bestehende Tests

Keine Tests sollten brechen, solange die Änderungen korrekt durchgeführt werden:

- **`ContinueWatchingServiceTestBase` (EF-InMemory):** Bleibt unverändert. Kann Problem 3 nicht testen (FK-Aktionen nicht durchgesetzt), ist aber für andere Tests ausreichend.
- **Existing `ContinueWatchingList` Unit-Tests:** Falls vorhanden, müssen sie ggf. angepasst werden, wenn sie auf `ContinueWatchingDto.Id` zugreifen oder Dictionary-Zugriffe mocken (gering wahrscheinlich).
- **Existing `PlaylistService.DeletePlaylistAsync` Tests:** Müssen mit der neuen Konfliktauflösungslogik umgehen. Falls die Tests direkt `SaveChangesAsync()` mocken, könnten sie angepasst werden müssen — aber bei Integration gegen SQLite sollten die Tests weiterhin grün sein.

### E2E-Tests (primärer Funktionsnachweis)

| Priorität | Szenario | Testdatei / Testklasse | Abgedecktes Akzeptanzkriterium | Warum E2E nötig ist |
|-----------|----------|------------------------|-------------------------------|-------------------|
| Pflicht | Weiterschauen-Liste mit mehrfachen Varianten desselben Videos (Rendering) | `ContinueWatchingE2ETests` (neue Methode) | Problem 1 & 2: Alle Varianten werden mit unterschiedlichen Zusatzinfos (Playlist-Badge, Link, Image) korrekt angezeigt; kein Crash bei Re-Render | UI-Rendering und Benutzer-Sichtbarkeit können nur im echten Browser (Selenium, Playwright, etc.) mit Blazor-Interop getestet werden; Unit-Tests mit bUnit sind hilfreicher für Details, E2E aber nötig für Integration mit Ladefluss |
| Pflicht | Kontextaktionen auf mehrfachen Varianten (Hide/Skip) | `ContinueWatchingE2ETests` (neue Methode) | Problem 1 & 2: Benutzer führt Aktion auf einer Variante aus; richtige Variante wird bearbeitet, andere bleiben unverändert | Benutzerfluss: UI-Klick → Server-Verarbeitung → Datenbank-Update → UI-Refresh. Kann nur E2E validiert werden. |
| Pflicht | Playlist löschen mit konkurrierendem playlist-losem Eintrag | `ContinueWatchingE2ETests` oder `PlaylistServiceE2ETests` (neue Methode) | Problem 3: Benutzer löscht Playlist; Operation schlägt nicht mit DB-Fehler fehl; Weiterschauen-Liste wird korrekt aktualisiert | Komplette Geschäftslogik: Playlist-Löschung → Konfliktauflösung → Weiterschauen-Liste aktualisiert. Kann nur E2E (mit echtem Browser und DB) validiert werden. |
| Pflicht | Fortsetzen einer Episode über Playlist mit gespeicherter Position | `ContinueWatchingE2ETests` oder neuer E2E-Test | Problem 4: Benutzer navigiert zu einer Playlist und startet die Wiedergabe eines Videos mit gespeicherter Position; Player startet bei dieser Position statt bei 0:00 | Benutzerfluss: Navigation → VideoPlayer-Rendering → Playback-Position. Timing und Player-Initialisierung können nur E2E validiert werden. |

**Bestehende E2E-Tests betroffen:** 

Keine bekannten bestehenden E2E-Tests sollten brechen. Falls `ContinueWatchingE2ETests` existiert:
- Tests, die mehrfache Varianten nicht explizit vorbereiten, könnten betroffen sein, wenn die Test-Daten versehentlich mehrfache Varianten enthalten. Diese müssen überprüft und ggf. angepasst werden (Test-Datenvorbereitung-Logik isolieren).

**Begründung für E2E-Priorität:** Alle vier Probleme sind benutzersichtbar und beeinflussen direkt den Ablauf der Anwendung (Rendering, Interaktion, Playback). Unit-Tests können Details validieren, aber nicht das Zusammenspiel zwischen UI, Service und Datenbank. E2E-Tests sind hier das primäre Nachweismittel.

## Offene Punkte

Keine.

**Begründung:** 

Die vier Probleme sind in der Anforderung vollständig beschrieben, inklusive Root Causes und Lösungsansätze. Die offenen Fragen der Anforderung (Punkte 1–5, Anforderung Zeilen 138–154) können basierend auf gängiger Praxis und Anforderungskontext beantwortet werden:

1. **Problem 1 – Dictionary-Schlüsselung:** `ContinueWatchingDto.Id` ist die Standard-Lösung (gelöst im Plan).
2. **Problem 3 – FK-Aktion:** Anwendungslogik-Lösung (Konfliktauflösung in `PlaylistService`) gewählt (gelöst im Plan).
3. **Problem 3 – Key-Fehler:** Automatisch gelöst durch Problem 1 (kein separater Punkt nötig).
4. **Problem 4 – Test-Infrastruktur:** Neue Tests gegen SQLite-Basis geplant (gelöst im Plan).
5. **Problem 4 – API-Kompatibilität:** Additive Änderung (neue Eigenschaft), kein Fallback nötig (gelöst im Plan).

Alle technischen und fachlichen Entscheidungen basieren auf der Anforderung und sind im Plan dokumentiert.
