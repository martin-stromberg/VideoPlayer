# Anforderung: Nachbesserung Entwicklungsschritt 6 – Korrekturen zum Weiterschauen mit Playlist-Bezug (Runde 1)

## Fachliche Zusammenfassung

Entwicklungsschritt 6 führte die Möglichkeit ein, dass dasselbe Video mehrfach in der Weiterschauen-Liste erscheinen kann (mit unterschiedlichen Playlist-Bezügen oder ganz ohne). Bei der Abnahmeprüfung wurden vier Fehler gefunden, zwei davon schwerwiegend:

1. **Falsche Dictionary-Schlüsselung** – Anzeigedaten für mehrere Varianten desselben Videos überschreiben sich gegenseitig.
2. **Blazor-Key-Kollision** – Der Circuit stürzt bei jedem Re-Render ab, sobald mehrere Varianten eines Videos vorhanden sind.
3. **Datenbankconstraint-Fehler beim Löschen** – Das Löschen einer Playlist schlägt fehl, wenn das Video auch ohne Playlist-Bezug in der Weiterschauen-Liste steht.
4. **Fehlende Wiedergabeposition** – Beim Fortsetzen über den Playlist-Kontext beginnt die Wiedergabe immer bei 0:00 statt an der gespeicherten Position.

Diese Anforderung beschreibt die vollständige Behebung aller vier Probleme einschließlich gezielter Regressionstests.

## Betroffene Klassen und Komponenten

### UI-Komponenten
- **`ContinueWatchingList.razor`** – Zeilen 18–90: Dictionary-Schlüsselung nach Media-Id statt nach Entry-Id; Zeile 147: @key-Attribut mit Duplikaten
- **`PlaylistDetail.razor`** – Zeilen 86–97: VideoPlayer wird ohne `StartPositionSeconds` initialisiert

### Datenmodell und DTOs
- **`ContinueWatchingDto.cs`** – Fehlende Eigenschaft `Id` für die Datenbank-ID der `ContinueWatchingEntry`
- **`DtoPlaylistPlaybackStart.cs`** – Fehlende Eigenschaft für Wiedergabeposition im Video

### Business Logic Services
- **`PlaylistService.DeletePlaylistAsync`** – Zeile 130–136: Fehlerhafte Behandlung von Unique-Index-Konflikten beim Löschen mit `SET NULL`
- **`ContinueWatchingService`** – Erweiterung erforderlich:
  - `EnrichPlaylistInfoAsync` oder vergleichbare Methode: muss `ContinueWatchingDto.Id` setzen (aus `ContinueWatchingEntry.Id`)
  - Möglicherweise: Neue private Methode `ResolvePlaylistDeletionConflictsAsync` für explizite Konfliktauflösung in `PlaylistService.DeletePlaylistAsync`
- **API-Endpunkt** (z. B. `VideoWebPlayerController.RequestContinueWatchingAsync`) – muss `ContinueWatchingDto.Id` mit der Datenbank-ID der `ContinueWatchingEntry` befüllen

### Datenbankmodellkonfiguration
- **`ContinueWatchingEntryConfiguration.cs`** – Zeilen 27–35: Bedingte Unique-Indizes für playlist-lose Einträge; Zeile 54–55: `ON DELETE SET NULL` bei der Playlist-Beziehung (tritt in Konflikt mit Unique-Index)

### Tests
- **`ContinueWatchingServiceTestBase`** – Verwendet `UseInMemoryDatabase`, das Unique-Indizes und FK-Aktionen nicht durchsetzt (strukturelle Testabdeckungslücke)
- **Neue Tests erforderlich:**
  - Rendering-Test für `ContinueWatchingList.razor` mit mehreren Varianten derselben Media
  - SQLite-Integrationtest für `PlaylistService.DeletePlaylistAsync` mit konfliktierendem playlist-losen Eintrag

## Implementierungsansatz

### Problem 1 & 2: Dictionary-Schlüsselung und Key-Duplikate

**Root Cause:** 
- `ContinueWatchingDto` hat **keine eigene `Id`-Eigenschaft** – sie enthält nur `Entry` (mit `Entry.Id` = Medien-ID), `PlaylistId`, `PlaylistEntryId`, etc.
- Die Dictionaries (`titles`, `images`, `links`, `playlistSubtitles`) in `ContinueWatchingList.razor` (Zeilen 18–21, 50–90) werden nach `it.Entry.Id` (Medien-ID) indiziert.
- Bei mehreren `ContinueWatchingDto`-Einträgen für dasselbe Video (z. B. Film 42 mit Playlist A, B und ohne Playlist) kollidieren alle drei Einträge im Dictionary bei Schlüssel `42`, und nur der letzte Wert bleibt.
- Folge: Alle drei Karten erhalten denselben Link, denselben Image-URL und dasselbe Badge.
- Der `@key`-Wert (Zeile 147) ist ebenfalls `it.Entry.Id`, also doppelt (Zeile 147 sagt `@key="it.Entry.Id"`, was `42, 42, 42` produziert).

**Lösung:**
1. **Ergänze `ContinueWatchingDto` um eine Eigenschaft `long Id`**, die die Datenbank-ID der zugehörigen `ContinueWatchingEntry` repräsentiert.
2. **Aktualisiere die API-Methode** (z. B. `VideoWebPlayerClient.RequestContinueWatchingAsync`), die `ContinueWatchingDto`-Instanzen befüllt, um diese `Id` aus der Datenbank zu setzen.
3. **Umindiziere alle Dictionaries** in `ContinueWatchingList.razor` Zeile 50–90:
   - `titles[it.Id] = ...` statt `titles[it.Entry.Id] = ...`
   - `images[it.Id] = ...` statt `images[it.Entry.Id] = ...`
   - `links[it.Id] = ...` statt `links[it.Entry.Id] = ...`
   - `playlistSubtitles[it.Id] = ...` statt `playlistSubtitles[it.Entry.Id] = ...`
4. **Aktualisiere den Zugriff** in der Razor-Markup (Zeile 145–148):
   - `if (titles.ContainsKey(it.Id))` statt `if (titles.ContainsKey(it.Entry.Id))`
   - `Title="@titles[it.Id]"` statt `titles[it.Entry.Id]`
   - Analog für `images`, `links`, `playlistSubtitles`
5. **Setze den @key korrekt** (Zeile 147): `@key="it.Id"` statt `@key="it.Entry.Id"` – jetzt wirklich eindeutig pro Datenbankzeile.

Nach diesen Änderungen sind beide Probleme behoben: die Dictionaries überschreiben sich nicht mehr, und die @key-Werte sind tatsächlich eindeutig.

### Problem 3: Unique-Index-Konflikt beim Playlist-Löschen

**Root Cause:** `PlaylistService.DeletePlaylistAsync` vertraut blind auf das EF-Core-Verhalten (`OnDelete(DeleteBehavior.SetNull)`). Wenn eine Playlist gelöscht wird, setzt die Datenbank `PlaylistId = NULL` für alle zugehörigen `ContinueWatchingEntry`-Zeilen. Gleichzeitig bestehen bedingte Unique-Indizes auf `(UserId, MovieId, NULL)` und `(UserId, TVShowEpisodeId, NULL)` (Filter: `[PlaylistId] IS NULL`). Kollidieren zwei Zeilen (eine ursprünglich playlist-los, eine gerade auf NULL gesetzt), schlägt `SaveChangesAsync` fehl.

**Lösung:**
- Ersetze die simple `SaveChangesAsync` in `PlaylistService.DeletePlaylistAsync` durch explizite Anwendungslogik (in `PlaylistService` oder `ContinueWatchingService`):
  1. **Vor** dem Löschen der Playlist: Finde alle `ContinueWatchingEntry`-Zeilen mit dieser `PlaylistId`.
  2. Für jede solche Zeile: Prüfe, ob bereits ein playlist-loser Eintrag für dasselbe Video existiert (same `UserId`, same `MovieId` oder `TVShowEpisodeId`, `PlaylistId = NULL`).
  3. Falls ja, **entferne** den playlist-gebundenen Eintrag (statt ihn auf NULL zu setzen).
  4. Falls nein, lasse den Eintrag wie geplant auf NULL setzen.
  5. **Dann** erst die Playlist löschen.
- Alternative: Setze die FK-Aktion auf `OnDelete(DeleteBehavior.Cascade)` statt `SetNull` – aber das würde den playlist-gebundenen Weiterschauen-Eintrag vollständig entfernen, auch wenn kein playlist-loser Eintrag existiert. Prüfe, ob das erwünscht ist. Wahrscheinlicher ist, dass die explizite Logik gewünscht ist (see first option).
- **Wichtig:** Der EF-InMemory-Provider erzwingt weder Unique-Indizes noch FK-Aktionen. Alle Änderungen müssen gegen eine echte SQLite-Datenbank getestet werden.

### Problem 4: Fehlende Wiedergabeposition im Playlist-Kontext

**Root Cause:** 
- `DtoPlaylistPlaybackStart` hat keine Eigenschaft für die Wiedergabeposition im Video (`StartPositionSeconds` oder ähnlich).
- `PlaylistDetail.razor` übergibt diese Information nicht an den `VideoPlayer` (vgl. Zeilen 86–97 vs. das playlist-lose Verhalten in `ContinueWatchingList.razor` Zeilen 83, 88).

**Lösung:**
- Ergänze `DtoPlaylistPlaybackStart` um eine Eigenschaft `long StartPositionSeconds` (oder `int`, je nach Konvention).
- Befülle diese Eigenschaft im `PlaylistApiClient.StartPlaylistAsync`-Endpunkt mit der Position aus dem `ContinueWatchingEntry.PositionSeconds`.
- Übergebe diese Position in `PlaylistDetail.razor` an den `VideoPlayer` als `StartPositionSeconds`-Parameter.

## Dokumentation und Konfiguration

### Datenbankmigrationen
- Falls erforderlich: Eine neue EF-Core-Migration, um die FK-Aktion zu ändern (falls nicht über die Logik gelöst wird; die Indizes selbst sind bereits vorhanden).
- Keine Migration notwendig für `ContinueWatchingDto.Id`, da dieses Feld nicht in der Datenbank gespeichert ist (es ist nur eine Projektion des `ContinueWatchingEntry.Id`).

### Dokumentation
- Anforderungsdokumentation aktualisieren (Schritt 6), falls vorhanden.
- Technische Dokumentation für Entwickler: ggf. Hinweis zur Verwendung von `ContinueWatchingDto.Id` statt `Entry.Id` für Eindeutigkeit.

### Tests

**Wichtig:** Die bestehenden Unit-Tests nutzen EF-InMemory, das weder Unique-Indizes noch FK-Aktionen durchsetzt. Problem 3 wurde deshalb nicht erkannt. Alle neuen Tests für Problem 3 müssen gegen **echte SQLite-Datenbank** (z. B. SQLite In-Memory mit echtem Schema) laufen.

Geplante Regressionstests:

1. **Unit-Test: ContinueWatchingList.razor mit mehreren Varianten derselben Media**
   - Szenario: Drei `ContinueWatchingDto`-Einträge mit `Entry.Id = 42` (Film) und unterschiedlichen `PlaylistId`-Werten (Playlist 1, Playlist 2, NULL).
   - Verifikation: Komponente rendert mit bUnit, jeder `MediaBox` erhält einen eindeutigen `@key`-Wert (basierend auf `it.Id`).
   - Verifikation: Jeder `MediaBox` erhält den korrekten `Title`, `LinkUrl`, `Subtitle` (Playlist-Badge nur bei nicht-NULL PlaylistId).
   - Verifikation: Keine `InvalidOperationException` beim Rendern oder Re-Rendern.

2. **Unit-Test: ContinueWatchingList.razor – Context-Aktionen (Hide/Skip) mit mehreren Varianten**
   - Szenario: Drei Einträge wie oben; Benutzer wählt "Ausblenden" für den mittleren Eintrag.
   - Verifikation: `HandleContextActionAsync` wird mit dem korrekten `PlaylistId` aufgerufen (nicht mit den anderen Playlist-IDs).
   - Verifikation: Nach dem Ausblenden bleibt die Liste konsistent (die anderen beiden Einträge sind noch vorhanden und haben weiterhin korrekte Display-Daten).

3. **Integration-Test (SQLite): PlaylistService.DeletePlaylistAsync mit konkurrierendem playlist-losen Eintrag**
   - Szenario: Datenbank mit zwei `ContinueWatchingEntry`-Zeilen für Film 42:
     - Zeile 1: `UserId=u1, MovieId=42, PlaylistId=1, PositionSeconds=300`
     - Zeile 2: `UserId=u1, MovieId=42, PlaylistId=NULL, PositionSeconds=100` (playlist-los)
   - Aktion: `PlaylistService.DeletePlaylistAsync(playlistId=1, userId=u1)` ausführen.
   - Verifikation: Keine `DbUpdateException` mit "UNIQUE constraint failed".
   - Verifikation: Playlist wird gelöscht; Zeile 1 wird ebenfalls gelöscht oder auf NULL gesetzt (je nach Geschäftslogik).
   - Verifikation: Zeile 2 bleibt unverändert (das ist das ursprüngliche playlist-lose Backup).
   - Datenbank-Constraint: Kein Duplicate auf `(UserId=u1, MovieId=42, PlaylistId=NULL)`.

4. **Unit-Test: DtoPlaylistPlaybackStart mit StartPositionSeconds**
   - Szenario: Eines `ContinueWatchingEntry` mit `PositionSeconds=1200` wird über `PlaylistClient.StartPlaylistAsync` aufgerufen.
   - Verifikation: Rückgabe `DtoPlaylistPlaybackStart.StartPositionSeconds = 1200`.

5. **Integration-Test (Blazor/E2E, optional): Fortsetzen über Playlist mit Position**
   - Szenario: Benutzer navigiert zu `/playlists/1?entryId=22` (ein Eintrag mit gespeicherter Position 600 Sekunden).
   - Verifikation: `PlaylistDetail.razor` übergibt `StartPositionSeconds=600` an den `VideoPlayer`.
   - Verifikation: Der Player startet bei 0:10 (600 Sekunden) statt 0:00.

## Offene Fragen

1. **Dictionary-Schlüsselung (Problem 1):** Bestätigung erforderlich: `ContinueWatchingDto` benötigt eine neue Eigenschaft `long Id`. Diese soll die Datenbank-ID der zugehörigen `ContinueWatchingEntry` repräsentieren (ähnlich wie `Entry.Id` die Medien-ID repräsentiert). Ist dies die erwartete Lösung?

2. **FK-Aktion (Problem 3):** Ist das gewünschte Verhalten beim Playlist-Löschen:
   - (A) Den playlist-gebundenen Eintrag entfernen, wenn bereits ein playlist-loser existiert?
   - (B) Den Eintrag auf NULL setzen und den bestehenden playlist-losen überschreiben/zusammenführen?
   - (C) Den Eintrag komplett löschen (Cascade), auch wenn kein playlist-loser Eintrag existiert?
   
   Die Anforderung deutet auf (A) hin.

3. **Key-Fehler ist direkte Folge von Problem 1:** Der Fehler "More than one sibling of component 'MediaBox' has the same key value" tritt auf, weil mehrere @key-Werte identisch sind (beide `42`). Nach der Umindizierung auf `it.Id` sollte dieser Fehler verschwinden – keine separate Untersuchung erforderlich, sondern automatische Auflösung durch die Behebung von Problem 1.

4. **Test-Infrastruktur:** Wo soll der SQLite-Integrationtest untergebracht werden? In einer neuen Test-Klasse, in einer bestehenden mit SQLite-Fixture, oder in `ContinueWatchingServiceTestBase` mit einem Test-Schalter?

5. **API-Kompatibilität:** Ist die Ergänzung von `StartPositionSeconds` in `DtoPlaylistPlaybackStart` eine inkompatible Änderung für bestehende Clients? Falls ja, sollte eine Versionierung oder ein Fallback in Betracht gezogen werden (z. B. mit Standardwert 0 bei Abwesenheit).
