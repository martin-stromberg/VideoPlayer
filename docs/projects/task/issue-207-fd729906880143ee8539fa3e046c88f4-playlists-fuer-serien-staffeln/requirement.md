# Technische Anforderungsübersetzung: Playlists für Serien, Staffeln, Episoden, Filme und Filmsammlungen

**Aufgaben-ID:** fd729906-8801-43ee-8539-fa3e046c88f4  
**Branch:** task/issue-207-fd729906880143ee8539fa3e046c88f4-playlists-fuer-serien-staffeln  
**Erstellt:** 2026-09-05

---

## Fachliche Zusammenfassung

Das System wird um ein benutzergesteuertes Playlist-Feature erweitert, das eine flexible Kombination von Serien (mit automatischer Ergänzung aller Staffeln/Episoden), einzelnen Staffeln, einzelnen Episoden, Filmsammlungen (mit automatischer Ergänzung aller Filme) und einzelnen Filmen ermöglicht. Playlists unterstützen zwei Sortiermodi (automatisch nach Erscheinungsdatum oder manuell mit Drag & Drop) und integrieren sich in die Weiterschauen-Logik (`ContinueWatchingEntry`), wobei ein Video mehrfach mit unterschiedlichen Playlists in der Weiterschauen-Liste auftreten kann. Zusätzlich werden automatische Genres-Ableitung, öffentliche Playlists mit reiner Leseberechtigung und benutzerdefinierte oder automatisch generierte Playlist-Abbildungen unterstützt.

---

## Betroffene Klassen und Komponenten

### Neue Datenmodellklassen (Entities)

- **`Playlist`** (neue Entity)
  - `Id` (long, PK)
  - `UserId` (string, RQ, FK auf `ApplicationUser`)
  - `Name` (string, RQ)
  - `Description` (string?, optional)
  - `SortMode` (Enum: `Automatic`, `Manual`)
  - `IsPublic` (bool)
  - `PictureId` (long?, FK auf `Picture`, optional)
  - `GenreNames` (string?, Kommagetrennte Genres)
  - `CreatedAt` (DateTime)
  - `UpdatedAt` (DateTime)
  - Navigation: `User` (ApplicationUser)
  - Navigation: `Picture` (Picture?)
  - Navigation: `PlaylistItems` (ICollection<PlaylistItem>)
  - Navigation: `PlaylistGenres` (ICollection<PlaylistGenre>)

- **`PlaylistItem`** (neue Entity, Zielentität für Playlists)
  - `Id` (long, PK)
  - `PlaylistId` (long, RQ, FK)
  - `MovieId` (long?, FK auf `Movie`, optional)
  - `TVShowId` (long?, FK auf `TVShow`, optional)
  - `TVShowSeasonId` (long?, FK auf `TVShowSeason`, optional)
  - `TVShowEpisodeId` (long?, FK auf `TVShowEpisode`, optional)
  - `MovieCollectionId` (long?, FK auf `MovieCollection`, optional)
  - `Order` (long) - Sortierreihenfolge bei manueller Sortierung
  - `AddedAt` (DateTime)
  - Navigation: `Playlist` (Playlist)
  - Navigation: `Movie` (Movie?)
  - Navigation: `TVShow` (TVShow?)
  - Navigation: `TVShowSeason` (TVShowSeason?)
  - Navigation: `TVShowEpisode` (TVShowEpisode?)
  - Navigation: `MovieCollection` (MovieCollection?)

- **`PlaylistGenre`** (neue Entity)
  - `Id` (long, PK)
  - `PlaylistId` (long, RQ, FK)
  - `GenreId` (long, RQ, FK auf `Genre`)
  - Navigation: `Playlist` (Playlist)
  - Navigation: `Genre` (Genre)

### Erweiterungen bestehender Datenmodelle

- **`ContinueWatchingEntry`** (Erweiterung)
  - Neue Eigenschaft: `PlaylistId` (long?, FK auf `Playlist`, optional)
  - Neue Navigation: `Playlist` (Playlist?)
  - Semantik: Ein `ContinueWatchingEntry` kann nun optional mit einer Playlist verknüpft werden; mehrere Einträge mit unterschiedlicher Playlist-Zuordnung sind für dasselbe Video zulässig

- **`ApplicationUser`** (Erweiterung)
  - Neue Navigation: `Playlists` (ICollection<Playlist>)

### Enums

- **`PlaylistSortMode`** oder `SortMode` (innerhalb Playlist-Namespace)
  - `Automatic` - Sortierung nach Erscheinungsdatum, Reihenfolge nicht manuell änderbar
  - `Manual` - Manuelle Sortierung mit Drag & Drop

### UI-Komponenten / Controller (Blazor Components und API Endpoints)

- **Playlist-Verwaltungs-Komponenten:**
  - `PlaylistListComponent` - Anzeige aller Playlists des Benutzers
  - `PlaylistDetailComponent` - Detail- und Bearbeitungsansicht einer Playlist
  - `PlaylistItemManagementComponent` - Infinity-List für manuelle Sortierung (nur bei `Manual` SortMode)
  - `PlaylistImageComponent` - Upload oder Generierung von Playlist-Abbildern

- **Playlist-Integration in bestehende Komponenten:**
  - Erweiterung der Videowiedergabe-Komponenten um Playlist-Bezug
  - Erweiterung der Weiterschauen-Komponente, um Playlist-Kontext anzuzeigen

- **API-Endpoints (ASP.NET Core):**
  - `GET /api/playlists` - Alle Playlists des aktuellen Benutzers
  - `POST /api/playlists` - Neue Playlist erstellen
  - `GET /api/playlists/{id}` - Einzelne Playlist abrufen (Public-Check beachten)
  - `PUT /api/playlists/{id}` - Playlist aktualisieren (nur für Besitzer)
  - `DELETE /api/playlists/{id}` - Playlist löschen (nur für Besitzer)
  - `POST /api/playlists/{id}/items` - Item zur Playlist hinzufügen
  - `DELETE /api/playlists/{id}/items/{itemId}` - Item aus Playlist entfernen (mit Sicherheitsabfrage-Logik)
  - `PUT /api/playlists/{id}/items/{itemId}/order` - Reihenfolge bei manueller Sortierung ändern
  - `POST /api/playlists/{id}/items/reorder` - Batch-Reordering bei Manual-Mode
  - `GET /api/playlists/{id}/items` - Alle Items einer Playlist mit Paginierung/Infinity-List
  - `POST /api/playlists/{id}/image/upload` - Benutzerdefiniertes Abbildgenerator-Bild hochladen
  - `POST /api/playlists/{id}/image/generate` - Automatisches Abbilder generieren
  - `PUT /api/playlists/{id}/genres` - Genres manuell setzen/überschreiben

### Logikklassen / Services

- **`IPlaylistService`** (Interface)
  - `CreatePlaylistAsync(userId, name, sortMode, ...)`
  - `GetPlaylistAsync(playlistId, userId?)`
  - `UpdatePlaylistAsync(playlistId, userId, ...)`
  - `DeletePlaylistAsync(playlistId, userId)`
  - `GetUserPlaylistsAsync(userId)`
  - `GetPublicPlaylistsAsync(userId, accessibleToCurrentUserId?)`
  - `AddItemToPlaylistAsync(playlistId, userId, itemType, itemId)`
  - `RemoveItemFromPlaylistAsync(playlistId, userId, itemId, enforceWatchedEntryCheck)`
  - `GetPlaylistItemsAsync(playlistId, sortMode, pageSize, page)`
  - `ReorderPlaylistItemAsync(playlistId, userId, itemId, newOrder)`
  - `GetNextVideoInPlaylistAsync(playlistId, currentVideoType, currentVideoId)`
  - `AutomaticallyAddNewContentAsync(playlistId)` - Batch-Job für automatische Ergänzung neuer Inhalte
  - `VerifyDuplicatesAsync(playlistId, itemType, itemId)` - Prüfung auf Duplikate

- **`IPlaylistGenreService`** (Interface)
  - `DeriveGenresFromPlaylistItemsAsync(playlistId)`
  - `UpdatePlaylistGenresAsync(playlistId, userId, genreIds)`
  - `GetPlaylistGenresAsync(playlistId)`

- **`IPlaylistImageService`** (Interface)
  - `GeneratePlaylistImageAsync(playlistId)` - Erstellt automatisch ein Composite-Bild
  - `UploadPlaylistImageAsync(playlistId, userId, imageData)`
  - `GetPlaylistImageAsync(playlistId)`

- **`IContinueWatchingPlaylistService`** (Interface oder Erweiterung)
  - `CreateOrUpdateContinueWatchingWithPlaylistAsync(userId, videoType, videoId, playlistId?, position, duration)`
  - `GetContinueWatchingEntriesAsync(userId, includePlaylistInfo)`
  - `ReplaceOrDeleteContinueWatchingOnPlaylistItemRemovalAsync(playlistId, itemId)` - Sicherheitslogik beim Entfernen

- **`PlaylistValidationService`** (Interface)
  - `ValidatePlaylistItemAsync(playlistId, itemType, itemId)` - Zugriffsprüfung
  - `ValidateDuplicateAsync(playlistId, itemType, itemId)` - Duplikat-Check
  - `ValidateAccessAsync(playlistId, userId, requireEdit?)` - Zugriffskontrolle (Public vs. Private)

- **`IPlaylistSortingService`** (Interface)
  - `GetSortedItemsAsync(playlistId, sortMode)` - Liefert sortierte Items (automatisch oder manuell)
  - `CalculateAutomaticOrderAsync(playlistId, newItemType, newItemId)` - Bestimmt Position bei Automatic-Mode

### Tests

- **Unit-Tests:**
  - `PlaylistServiceTests` - CRUD-Operationen, Duplikat-Vermeidung, Zugriffskontrolle
  - `PlaylistItemServiceTests` - Hinzufügen/Entfernen von Items, Duplikat-Checks
  - `PlaylistSortingServiceTests` - Automatische und manuelle Sortierlogik
  - `PlaylistGenreServiceTests` - Genre-Ableitung und manuelle Überschreibung
  - `ContinueWatchingPlaylistServiceTests` - Weiterschauen-Verknüpfungen, mehrfach vorkommende Videos
  - `PlaylistImageServiceTests` - Bild-Generierung und Upload

- **Integrationstests:**
  - Komplette Playlist-Workflows (Erstellen, Hinzufügen von Items, Abspielen, Weiterschauen aktualisieren)
  - Sicherheitsabfrage beim Entfernen eines Items aus Weiterschauen-Liste
  - Öffentliche Playlists (Read-Only-Zugriff)
  - Automatische Ergänzung neuer Inhalte
  - Genre-Derivation bei Änderungen

- **UI/Component-Tests:**
  - Drag & Drop bei manueller Sortierung
  - Infinity-List-Rendering
  - Button-Aktionen (An Anfang / An Ende)
  - Ausgrauen nicht-freigeschalteter Inhalte
  - Sicherheitsabfrage beim Entfernen

---

## Implementierungsansatz

### 1. Datenmodell und Migrations

- Neue EF Core Migrations für die neuen Entities `Playlist`, `PlaylistItem`, `PlaylistGenre`
- Erweiterung von `ContinueWatchingEntry` um `PlaylistId`-Eigenschaft und Foreign Key zu `Playlist`
- Fluent API Konfigurationen für:
  - Cascade-Delete: Beim Löschen einer Playlist werden alle `PlaylistItem` gelöscht
  - Cascade-Update: Beim Löschen eines Videos prüfen, ob es in `PlaylistItem` referenziert wird (evtl. Cascade oder Constraint)
  - Unique-Constraints: Für jede Playlist ein eindeutiger Video-Eintrag (per Kombinationsinduktionsschlüssel)

### 2. Service-Layer

- **Duplikat-Vermeidung:**
  - `IPlaylistService.AddItemToPlaylistAsync()` prüft zuerst, ob ein identisches Item bereits in der Playlist existiert
  - Gibt Fehler oder Warnung zurück, wenn Duplikat erkannt wird
  
- **Sortierlogik:**
  - `PlaylistSortingService`: Für `Automatic`-Mode werden alle Videos chronologisch nach `ReleaseDate` / `PremieredAt` / `Year` sortiert (je nach Inhaltstyp)
  - Bei manueller Sortierung wird die `Order`-Spalte in `PlaylistItem` verwendet
  - Neuer Inhalt wird bei `Manual`-Mode am Ende eingefügt (höchste Order-Nummer)

- **Automatische Inhalts-Ergänzung:**
  - Hintergrund-Job (z. B. `PlaylistBackfillWorker`) prüft regelmäßig, ob neue Staffeln zu einer Serie hinzugefügt wurden oder neue Episoden zu einer Staffel
  - Neue Videos werden entsprechend der Sortierung eingefügt
  
- **Genre-Ableitung:**
  - `PlaylistGenreService.DeriveGenresFromPlaylistItemsAsync()` liest alle Genres aus zugeordneten Videos und aggregiert sie
  - Benutzer kann Genres manuell überschreiben via `UpdatePlaylistGenresAsync()`

- **Weiterschauen-Integration:**
  - Beim Starten eines Videos aus einer Playlist wird die `PlaylistId` in `ContinueWatchingEntry.PlaylistId` gespeichert
  - Ein Video kann mehrfach in `ContinueWatchingEntry` vorkommen (einmal ohne Playlist, einmal mit Playlist A, einmal mit Playlist B)
  - Die globale Gesehen-Markierung (WatchedEntry) ist unabhängig von Playlists

- **Sicherheitsabfrage beim Entfernen:**
  - `ContinueWatchingPlaylistService.ReplaceOrDeleteContinueWatchingOnPlaylistItemRemovalAsync()` prüft, ob das zu löschende Item in `ContinueWatchingEntry` mit dieser Playlist verknüpft ist
  - Falls ja: Suche nächsten verfügbaren Titel in der Playlist und ersetze den `ContinueWatchingEntry`
  - Falls kein weiteres Video: Lösche den Eintrag

### 3. API und Authorization

- **Ownership-Check:** Alle Schreibzugriffe prüfen `Playlist.UserId == currentUserId`
- **Public-Check:** Bei Lesezugriffen auf `IsPublic == true` prüfen (kein Ownership erforderlich)
- **Zugriff auf Videos:** Reutilisiere bestehende Zugriffskontrolle für Videos (MediaSource-Zugriff, Freischaltung)

### 4. UI-Layer (Blazor Components)

- **Automatische Sortierung:**
  - Anzeige als statische Liste (nicht bearbeitbar)
  - Neue Videos werden automatisch eingefügt
  
- **Manuelle Sortierung:**
  - Infinity-List mit Virtuelisierung für Performance
  - Drag & Drop via interop (z. B. HTMLElement.dragstart/dragend)
  - Buttons: „An Anfang" (Order = 0, bestehende Indices shiften), „An Ende" (Order = Max+1)
  
- **Ausgrauen nicht-freigeschalteter Inhalte:**
  - Check `UnlockedMediaEntry` für jedes Video
  - CSS-Klasse `locked` oder `disabled` anwenden

### 5. Abhängigkeiten und Hooks

- **Bestehende Mechaniken renutzen:**
  - `UnlockedMediaEntry` für Freischaltungs-Check
  - `WatchedEntry` für globale Gesehen-Markierung
  - `MediaSource` für Zugriffskontrolle
  
- **Neue Events/Hooks:**
  - `PlaylistItemAdded` - Bei Hinzufügen eines Items (Hintergrund-Jobs können reagieren)
  - `PlaylistItemRemoved` - Trigger für Sicherheitsabfrage-Logik
  - `VideoReleasedOrAdded` - Trigger für automatische Ergänzung von Playlist-Items

---

## Konfiguration

1. **Anwendungsebene:**
   - Batch-Größe für `PlaylistBackfillWorker` (z. B. 100 Videos pro Run)
   - Scan-Intervall für neue Inhalte (z. B. alle 12 Stunden)
   - Maximale Anzahl von Playlists pro Benutzer (optional)
   - Maximale Anzahl von Items pro Playlist (optional)

2. **Benutzer-spezifisch:**
   - `Playlist.IsPublic` - Admin kann als öffentlich markieren
   - `Playlist.SortMode` - Benutzer wählt bei Playlist-Erstellung
   - Manuell überschriebene Genres

3. **Pro Playlist:**
   - `Playlist.Name`, `Playlist.Description`
   - `Playlist.PictureId` (benutzerdefiniert oder auto-generiert)

---

## Offene Fragen und Annahmen

### Annahmen (zu Klärung kennzeichnet):

1. **Duplikat-Definition:** Annahme: Ein Duplikat liegt vor, wenn dasselbe Video (z. B. Episode S01E01) bereits in der Playlist ist. Mehrfaches Hinzufügen untersagt.

2. **Zeitstempel für Sortierung:** Annahme: Bei Automatic-Mode wird `TVShowEpisode.ReleaseDate` oder `PremieredAt` verwendet. Bei fehlendem Datum wird `AddedAt` als Fallback genutzt.

3. **MovieCollection vs. Filmsammlung:** Annahme: `MovieCollection` entspricht einer Sammlung von Filmen (z. B. MCU, Looney Tunes). Die Anforderung nennt diesen Inhaltstyp „Filmsammlung".

4. **Bildgenerierung:** Annahme: Automatisch generiertes Bild ist ein Collage der ersten 5 Bilder aus den zugeordneten Titeln. Priorität: SeriesBild > EpisodeBild > FilmsammlungsBild > FilmBild.

5. **Nächstes Video:** Annahme: Die Methode `GetNextVideoInPlaylistAsync()` liefert das nächste Video in der Playlist basierend auf der aktuellen Position im aktuellen Video und der Sortierreihenfolge.

6. **Öffentliche Playlists:** Annahme: Nur Administratoren können Playlists als öffentlich markieren. Anwender mit Zugriff können diese abspielen und ansehen, aber nicht bearbeiten.

### Zu klärende Punkte:

1. **Versionierung und Kopieren:** Die Anforderung sagt „keine Versionierung und kein Kopieren". Ist damit gemeint, dass Playlists nicht geklont werden können und keine Änderungsverlauf erfasst wird? Bestätigung erforderlich.

2. **Cascade-Delete bei Video-Löschung:** Falls ein Video aus dem System gelöscht wird (z. B. Datei gelöscht), soll das zugehörige `PlaylistItem` auch gelöscht werden oder nur ein Fehler angezeigt werden?

3. **Musik-Videos oder andere Inhaltstypen:** Die Anforderung nennt nur Serien, Staffeln, Episoden, Filme und Filmsammlungen. Sind andere Inhaltstypen ausgeschlossen?

4. **Benutzerberechtigungen für öffentliche Playlists:** Wenn ein Benutzer auf eine öffentliche Playlist keinen Zugriff auf ein Video hat (z. B. wegen Freischaltung), wird dieses Video ausgegraut angezeigt? Bestätigung erforderlich.

5. **Playlist-Beschreibung:** Ist eine optionale Beschreibung für Playlists gewünscht?

6. **Automatische Bildgenerierung:** Technische Details zu Bild-Auflösung, Format (PNG/JPG), Größe und Cache-Dauer?

7. **Infinity-List Performance:** Soll die Infinity-List mit beliebig vielen Items umgehen können, oder gibt es eine praktische Obergrenze (z. B. 5000)?

8. **Sicherheitsabfrage Wording:** Exakte Textformulierung für die Sicherheitsabfrage beim Entfernen eines Items aus der Weiterschauen-Liste erforderlich (oder ist die in der Anforderung genannte Wording ausreichend)?
