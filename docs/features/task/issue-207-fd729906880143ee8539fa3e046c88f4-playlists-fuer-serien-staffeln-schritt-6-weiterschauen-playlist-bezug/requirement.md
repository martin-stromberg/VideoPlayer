# Technische Anforderungsübersetzung: Weiterschauen mit Playlist-Bezug (Schritt 6)

**Aufgaben-ID:** fd729906-8801-43ee-8539-fa3e046c88f4  
**Branch:** task/issue-207-fd729906880143ee8539-fa3e046c88f4-playlists-fuer-serien-staffeln-schritt-6-weiterschauen-playlist-bezug  
**Erstellt:** 2026-09-13

---

## Fachliche Zusammenfassung

Das bestehende Weiterschauen-System wird um optionale Playlist-Bindung erweitert. `ContinueWatchingEntry` erhält eine neue Eigenschaft `PlaylistId` (nullable Foreign Key), wodurch ein Video mehrfach in der Weiterschauen-Liste erscheinen kann — einmal für jede unterschiedliche Playlist sowie einmal für Wiedergaben ohne Playlist-Kontext. Diese Einträge werden separat verwaltet (Fortschrittsposition, Ausblendung, Löschung). Die globale Gesehen-Markierung (`WatchedEntry`) bleibt davon unberührt und gilt weiterhin playlist-übergreifend. Für Non-Playlist-Wiedergaben bleibt das bisherige Verhalten erhalten.

---

## Betroffene Klassen und Komponenten

### Neue / Erweiterte Datenmodellklassen (Entities)

- **`ContinueWatchingEntry`** (Erweiterung)
  - Neue Eigenschaft: `PlaylistId` (long?, nullable Foreign Key auf `Playlist`)
  - Neue Navigation: `Playlist` (Playlist?)
  - Neue Composite Unique Constraint: `(UserId, MovieId OR TVShowEpisodeId, PlaylistId)` — um sicherzustellen, dass ein Video für einen Benutzer pro Playlist nur einmal in der Weiterschauen-Liste auftritt
  - Semantik: Ein `ContinueWatchingEntry` ist nun eindeutig identifiziert durch die Kombination aus:
    - Benutzer (`UserId`)
    - Video (entweder `MovieId` oder `TVShowEpisodeId`)
    - Playlist (`PlaylistId`, kann NULL sein)

### Transfer-Objekte (DTOs)

- **`ContinueWatchingDto`** (Erweiterung)
  - Neue Eigenschaft: `PlaylistId` (long?, optional)
  - Neue Eigenschaft: `PlaylistName` (string?, optional, zur Anzeige in der UI)
  - Diese Eigenschaften werden befüllt, wenn der Eintrag mit einer Playlist verknüpft ist

### UI-Komponenten / Controller (Blazor Components und API Endpoints)

- **`ContinueWatchingList.razor`** (Erweiterung)
  - Anzeige von Playlist-Zuordnung pro Eintrag (z. B. Badge oder Zusatzinfo „In Playlist: [Name]")
  - Separate Behandlung von Einträgen mit derselben Video-ID aber unterschiedlichen Playlists
  - Optional: Filtermöglichkeit nach Playlist-Zugehörigkeit

- **API-Endpoints (ASP.NET Core)** (Erweiterung)
  - `POST /api/continue-watching` — Erweitert: Optional `playlistId` Parameter übernehmen
  - `GET /api/continue-watching` — Weiterschauen-Einträge mit Playlist-Informationen zurückgeben
  - `DELETE /api/continue-watching/{id}` — Löschen basierend auf eindeutigem Schlüssel (UserId + VideoId + PlaylistId)

### Logikklassen / Services

- **`ContinueWatchingService`** (Erweiterung)
  - Neue Methode: `CreateOrUpdateContinueWatchingWithPlaylistAsync(userId, videoType, videoId, playlistId?, position, duration, cancellationToken)` — Erstellt oder aktualisiert einen Eintrag unter Berücksichtigung des optionalen Playlist-Bezugs
  - Erweiterung: `GetListAsync(user, cancellationToken)` — Gibt Einträge mit Playlist-Informationen zurück (ggf. mit Playlist-Namen)
  - Erweiterung: `ProcessBufferedEntryAsync(...)` — Berücksichtigt `PlaylistId` beim Speichern und Abrufen von Einträgen
  - Erweiterung: `RemoveExistingTVShowEntry(userId, nextEpisodeId, playlistId?, cancellationToken)` — Entfernt nur Einträge der gleichen Serie *und* gleichen Playlist (beachtet PlaylistId)
  - Erweiterung: `RemoveExistingMovieCollectionEntry(userId, nextMovieId, playlistId?, cancellationToken)` — Analog zu TV-Show-Logik
  - Neue Methode: `DeleteContinueWatchingEntryAsync(userId, videoType, videoId, playlistId?, cancellationToken)` — Löscht einen Eintrag unter Berücksichtigung der optionalen Playlist-ID

- **`VideoPlayer.razor`** (Erweiterung)
  - Beim Aufruf von `ContinueWatchingService.ReportProgressAsync()` wird zusätzlich die aktuelle `PlaylistPlaybackContext` (sofern vorhanden) extrahiert und die `PlaylistId` übergeben
  - Beim Starten der Wiedergabe aus einer Weiterschauen-Position muss der `PlaylistPlaybackContext` (sofern Playlist-ID existiert) rekonstruiert und in den Player übergeben werden

### Tests

- **Unit-Tests:**
  - `ContinueWatchingServicePlaylistTests` — Tests für Erstellen/Aktualisieren mit Playlist-Bezug
  - `ContinueWatchingServiceMultipleEntriesTests` — Tests für mehrfaches Vorkommen desselben Videos (mit/ohne Playlist)
  - `ContinueWatchingServiceRemovalTests` — Tests für Entfernen von Einträgen mit Playlist-Filter
  - `ContinueWatchingDtoTests` — Tests für DTO mit Playlist-Properties

- **Integrationstests:**
  - Playlist-Wiedergabe: Video wird gestartet, Eintrag mit Playlist-Bezug wird erstellt
  - Mehrfach-Vorkommen: Dasselbe Video wird mit und ohne Playlist, sowie mit unterschiedlichen Playlists in der Weiterschauen-Liste gespeichert
  - Separate Verwaltung: Fortschritt, Ausblendung und Löschung werden pro Playlist-Kombination verwaltet
  - Non-Playlist-Wiedergabe: Existierendes Verhalten bleibt erhalten
  - Gesehen-Markierung: Global und playlist-unabhängig

---

## Implementierungsansatz

### 1. Datenmodell und Migrationen

- **EF Core Migration:**
  - Spalte `PlaylistId` (long?, nullable) zu `ContinueWatchingEntry` hinzufügen
  - Foreign Key Constraint zu `Playlist` erstellen (Cascade-Delete optional oder SetNull)
  - Unique Index / Constraint auf `(UserId, (MovieId OR TVShowEpisodeId), PlaylistId)` erstellen, um Duplikate zu vermeiden
  - Hinweis: Index-Definition muss mit berechneter Spalte erfolgen, falls EF Core bedingte Indizes nicht ausreichend unterstützt (conditional index: nur aktive Einträge, oder zweiter Index pro Video-Typ)

### 2. Service-Layer

- **Aufrufen aus VideoPlayer.razor:**
  - VideoPlayer.razor hat während der Wiedergabe Zugriff auf `PlaylistPlaybackContext` (sofern vorhanden)
  - Beim Aufruf von `ReportProgressAsync()` wird die `PlaylistId` aus diesem Context extrahiert und übergeben
  - Falls kein Context vorhanden ist (Non-Playlist-Wiedergabe), wird `playlistId = null` übergeben

- **Duplikat-Vermeidung:**
  - Beim Erstellen/Aktualisieren wird geprüft, ob bereits ein Eintrag mit derselben Kombination `(UserId, VideoId, PlaylistId)` existiert
  - Falls ja: Position aktualisieren (upsert-Verhalten)
  - Falls nein: Neuer Eintrag wird eingefügt

- **Entfernen bestehender Einträge:**
  - `RemoveExistingTVShowEntry()` und `RemoveExistingMovieCollectionEntry()` werden erweitert, um nur Einträge der gleichen Playlist zu entfernen
  - Falls `playlistId` vorhanden ist: Nur Einträge mit derselben `PlaylistId` werden entfernt
  - Falls `playlistId` = null: Nur Einträge ohne Playlist (PlaylistId IS NULL) werden entfernt

- **Weiterschauen-Liste abrufen:**
  - `GetListAsync()` gibt alle Einträge des Benutzers mit ihren Playlist-Informationen zurück
  - Die Playlist-Informationen werden durch einen Join mit der `Playlist`-Tabelle ermittelt (um den Namen für die UI zu bekommen)
  - Die Reihenfolge in der Liste bleibt unverändert (`ListOrder`)

### 3. API und Authorization

- **Ownership-Check:** Bleibt unverändert (User-Zugriff auf die eigene Weiterschauen-Liste)
- **Playlist-Validierung:** Wenn eine `PlaylistId` übermittelt wird, muss validiert werden:
  - Playlist existiert
  - Aktueller Benutzer besitzt die Playlist (oder sie ist öffentlich, falls relevant)
  - Das zu speichernde Video gehört zur Playlist (oder wird hinzugefügt)

### 4. UI-Layer (Blazor Components)

- **ContinueWatchingList.razor:**
  - Für jeden Eintrag wird zusätzlich die Playlist-Information angezeigt (falls `PlaylistId` nicht null)
  - Format: z. B. „Weiterschauen in Playlist [PlaylistName]" oder Badge-Stil „[PlaylistName]"
  - Visuelle Trennung von Einträgen mit und ohne Playlist-Bezug (optional, z. B. Gruppierung)

- **Starten der Wiedergabe:**
  - Beim Klick auf einen Eintrag wird überprüft, ob `PlaylistId` vorhanden ist
  - Falls ja: `PlaylistPlaybackContext` wird mit den Playlist-Informationen rekonstruiert und an VideoPlayer übergeben
  - Falls nein: VideoPlayer wird ohne Playlist-Kontext gestartet (bisheriges Verhalten)

### 5. Abhängigkeiten und Hooks

- **Bestehende Mechaniken renutzen:**
  - `WatchedEntry` bleibt unabhängig und playlist-übergreifend (keine Änderung erforderlich)
  - Bestehende `ContinueWatchingEntry` ohne `PlaylistId` bleiben funktional unverändert
  - `MediaUpdateNotificationService` signalisiert Änderungen der Weiterschauen-Liste (wie bisher)

- **Neue Abhängigkeiten:**
  - `IPlaylistService` — zum Validieren und Laden von Playlist-Informationen
  - `PlaylistPlaybackContext` — wird aus `VideoPlayer.razor` extrahiert und übergeben

---

## Konfiguration

1. **Anwendungsebene:**
   - Keine neue Konfiguration erforderlich; das Feature nutzt bestehende Einstellungen

2. **Benutzer-spezifisch:**
   - Keine neue Konfiguration erforderlich

3. **Pro Eintrag:**
   - `PlaylistId` wird automatisch gesetzt/geleert je nach Wiedergabe-Kontext

---

## Offene Fragen und Annahmen

### Annahmen (zur Klärung kennzeichnet):

1. **Unique Constraint auf (UserId, VideoId, PlaylistId):** Annahme: Das System erlaubt nicht, dass dasselbe Video zweimal für einen Benutzer in der Weiterschauen-Liste mit der *gleichen* Playlist-ID auftritt. Mehrfach-Vorkommen mit *unterschiedlichen* Playlists oder ohne Playlist sind hingegen zulässig. → Bestätigung erforderlich.

2. **Cascade-Delete-Verhalten:** Annahme: Wenn eine Playlist gelöscht wird, werden auch die zugehörigen Weiterschauen-Einträge (`ContinueWatchingEntry` mit dieser `PlaylistId`) gelöscht. Alternative: SetNull (Eintrag bleibt, Playlist-Bezug wird gelöscht). → Bestätigung erforderlich.

3. **Playlist-Rekonstruktion beim Fortsetzen:** Annahme: Wenn ein Benutzer einen Eintrag mit Playlist-Bezug aus der Weiterschauen-Liste wählt, werden die vollständigen Playlist-Informationen (Reihenfolge, aktuelle Position) neu geladen, um den `PlaylistPlaybackContext` zu rekonstruieren. → Bestätigung erforderlich.

4. **UI-Display bei gelöschter Playlist:** Annahme: Falls eine Playlist gelöscht wird (oder Cascade-Delete nicht aktiv), wird der Eintrag entweder gelöscht oder der Playlist-Bezug wird gelöst (zu Annahme 2). In der UI wird der Eintrag dann als „ohne Playlist" angezeigt. → Bestätigung erforderlich.

5. **Sortierung in der Weiterschauen-Liste:** Annahme: Die Reihenfolge der Einträge wird weiterhin durch `ListOrder` bestimmt, unabhängig von Playlist-Zugehörigkeit. Mehrere Einträge desselben Videos (mit/ohne Playlist) können somit durcheinander angezeigt werden. Alternative: Gruppierung nach Playlist. → Bestätigung erforderlich.

### Zu klärende Punkte:

1. **Explizite Text-Wording für UI:** Wie soll die Playlist-Zuordnung in der Weiterschauen-Liste angezeigt werden? (z. B. „In Playlist: [Name]", Badge, Gruppierung?)

2. **Handling fehlender Playlist-Rekonstruktion:** Falls beim Fortsetzen die Playlist-Informationen nicht korrekt rekonstruiert werden können (z. B. Playlist gelöscht, Video aus Playlist entfernt), soll die Wiedergabe trotzdem starten (ohne Playlist-Kontext) oder sollte eine Fehlermeldung angezeigt werden?

3. **Performance bei vielen Einträgen:** Wenn ein Benutzer dasselbe Video 10+ Male mit unterschiedlichen Playlists in der Weiterschauen-Liste hat, gibt es Performance-Überlegungen für das Laden und Rendering?

4. **Alte Daten nach Migration:** Wie werden bestehende `ContinueWatchingEntry`-Einträge (ohne `PlaylistId`) behandelt? Sie sollten null/unverändert bleiben, damit Non-Playlist-Wiedergaben weiterhin funktionieren. → Bestätigung erforderlich.

5. **Gesehen-Markierung und Playlist-Kontext:** Die globale Gesehen-Markierung soll weiterhin unabhängig sein. Beim Markieren eines Videos als „gesehen" sollen aber ALL seine Einträge in der Weiterschauen-Liste entfernt werden (mit allen Playlist-Kombinationen), da die globale Markierung playlist-übergreifend wirkt. → Bestätigung erforderlich.

