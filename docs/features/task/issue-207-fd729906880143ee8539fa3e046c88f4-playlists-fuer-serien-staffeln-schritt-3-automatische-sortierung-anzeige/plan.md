# Umsetzungsplan: Korrektur der Freischaltungsprüfung in Playlist-Einträgen

## Übersicht

Die fehlerhafte Methode `IsEntryAccessible` in `PlaylistService` wird korrigiert, um die kanonische Zugriffsregel der Anwendung `hasSourceAccess OR isUnlocked` für alle fünf unterstützten Medientypen (Movie, MovieCollection, TVShowEpisode, TVShowSeason, TVShow) korrekt zu implementieren. Dies umfasst die Integration von regulärem Quellenzugriff (`MediaSourceUsers`) über hierarchische Auflösung (Movie→MovieCollection, TVShowEpisode→TVShowSeason→TVShow, TVShowSeason→TVShow) sowie die Erweiterung der Test-Coverage und Dokumentation.

---

## Designentscheidungen

| Komponente / Bereich | Gewählter Ansatz | Begründung |
|----------------------|-----------------|------------|
| **Quellenzugriff-Bulk-Load** | Neue Methode `GetMediaSourceIdsForUserAsync` in `IUnlockedMediaService` / `UnlockedMediaService` | Zentralisiert die Zugriffslogik im Service Layer (PoEAA Service Layer Pattern). Stärkt die Separationof Concerns: `IUnlockedMediaService` wird die zentrale Anlaufstelle für alle Zugriffsprüfungen. Verhindert N+1-Queries und ist konsistent mit dem bestehenden Design (`GetUnlockedMovieCollectionIdsForUserAsync`, `GetUnlockedTVShowIdsForUserAsync`). |
| **Hierarchie-Auflösung für Unlock** | Refaktorierung von bereits bestehender Logik in `PlaylistService` (`GetParentMediaRefs`, Bulk-Laden für Titel/Bilder) in wiederverwendbare Hilfsmethoden | Die Auflösungslogik existiert bereits teilweise; eine Refaktorierung zu klaren, testbaren Hilfsmethoden (`ResolveUnlockMediaIdAsync`, `ResolveMediaSourceIdAsync`) verbessert Wartbarkeit und Testbarkeit. |
| **Struktur der Zugriffsprüfung** | Neue Methode `CheckAccessibleEntriesAsync` in `PlaylistService`, die `BuildEntryDtosAsync` nutzt und alle Zugriffsinformationen (Unlock-IDs, Quellzugriffs-IDs, Hierarchie-Mappings) auf einmal lädt | Bulk-Loading reduziert Datenbankzugriffe von O(n) auf O(1) pro Eintrag-Typ-Gruppe. Die zentrale Methode wird von allen drei Verwendungsstellen (`GetPlaylistEntriesAsync`, `GetPlaylistEntriesPagedAsync`, `AddMediaToPlaylistAsync`) genutzt und ist dort bereits über `BuildEntryDtosAsync` integriert. |
| **Mutation von `IsEntryAccessible`** | Wird von `private static` zu `private async` geändert (oder wird als neue Hilfsmethode `CheckEntryAccessibleAsync` angelegt) und nutzt vorgeladene Zugriffsdaten | Asynchrone Operationen werden vermieden (alle Daten sind vorgeladen), aber für Konsistenz mit dem bestehenden async-Stil wird die Signatur angepasst. |

---

## Programmabläufe

### Ablauf: Playlist-Einträge mit Zugriffsprüfung laden (GetPlaylistEntriesAsync, GetPlaylistEntriesPagedAsync)

1. Benutzer ruft `GetPlaylistEntriesAsync` oder `GetPlaylistEntriesPagedAsync` auf.
2. Methode validiert Playlist-Besitz über `CheckPlaylistOwnershipAsync`.
3. Playlist-Einträge werden aus der DB geladen (mit Paginierung falls erforderlich).
4. `BuildEntryDtosAsync` wird aufgerufen mit der Eintragsliste und der BenutzerID.
5. **Innerhalb `BuildEntryDtosAsync`:**
   - Alle Unlock-IDs werden über `LoadUnlockedMediaIdsAsync` (bestehend) geladen.
   - **NEU:** Alle MediaSource-IDs, auf die der Benutzer Zugriff hat, werden über `LoadMediaSourceIdsForUserAsync` (neu) geladen.
   - Für jeden Eintrag:
     - Hierarchie wird aufgelöst: Movie → MovieCollection.Id, TVShowEpisode → TVShowSeason.TVShowId, TVShowSeason → TVShowId, TVShow/MovieCollection → direkter Id.
     - MediaSourceId wird aufgelöst über Bulk-geladene Daten (bestehend für Titel/Bilder, wird erweitert).
     - Zugriff wird geprüft: `hasSourceAccess = mediaSourceIdsForUser.Contains(resolvedMediaSourceId)` OR `isUnlocked = unlockedIds.Contains(resolvedUnlockMediaId)`.
     - DTO wird mit `IsAccessible = hasSourceAccess || isUnlocked` erstellt.
6. DTOs werden an Aufrufer zurückgegeben.

Beteiligte Klassen/Komponenten: `PlaylistService`, `IUnlockedMediaService`, `UnlockedMediaService`, `ApplicationDbContext`, `PlaylistEntry`, `DtoPlaylistEntry`.

### Ablauf: Medium zu Playlist hinzufügen (AddMediaToPlaylistAsync)

1. Benutzer ruft `AddMediaToPlaylistAsync` auf (mit mediaType, mediaId).
2. Playlist-Besitz wird validiert.
3. Medium (und ggf. Cascade-Kinder wie Episoden) werden hinzugefügt — **unverändert**: `AddMediaToPlaylistAsync` prüft aktuell keinen Medienzugriff vor dem Hinzufügen (nur Playlist-Besitz und Existenz des Mediums) und das bleibt so; dies ist **nicht** Gegenstand dieser Korrektur, die sich ausschließlich auf das korrekt berechnete `IsAccessible`-Feld der zurückgegebenen DTOs bezieht.
4. `BuildAddResultAsync` wird aufgerufen mit den neu hinzugefügten Einträgen.
5. `BuildEntryDtosAsync` wird aufgerufen (gleicher Ablauf wie oben) und berechnet für jeden neu hinzugefügten Eintrag korrekt `IsAccessible = hasSourceAccess || isUnlocked`.
6. Ergebnis-DTO wird mit aktualisierten Einträgen (inkl. korrektem `IsAccessible`) zurückgegeben.

Beteiligte Klassen/Komponenten: `PlaylistService`, `IUnlockedMediaService`, `ApplicationDbContext`.

**Wichtige Abgrenzung:** Diese Korrektur ändert ausschließlich die Berechnung von `IsAccessible` in `BuildEntryDtosAsync` (verwendet von allen drei Aufrufstellen). Sie führt **keine neue Zugriffs-Durchsetzung** (Blockieren von `AddMediaToPlaylistAsync` bei fehlendem Zugriff) ein, da dies nicht Teil der gemeldeten Anforderung ist und eine Verhaltensänderung außerhalb des Bugfix-Scopes wäre.

---

## Neue Klassen

Keine.

---

## Änderungen an bestehenden Klassen

### `IUnlockedMediaService` (Interface)

- **Neue Methode:** `GetMediaSourceIdsForUserAsync(string userId, CancellationToken cancellationToken = default)` — Lädt alle `MediaSourceId`-Werte, auf die der Benutzer Zugriff hat (über `MediaSourceUsers`-Tabelle). Rückgabewert: `Task<long[]>`. Diese Methode ist die Ergänzung zu den bestehenden `GetUnlockedMovieCollectionIdsForUserAsync` und `GetUnlockedTVShowIdsForUserAsync`.

### `UnlockedMediaService` (Implementierung)

- **Neue Methode:** `GetMediaSourceIdsForUserAsync` — Implementierung der neuen Interface-Methode. Query-Struktur: `SELECT DISTINCT ms.Id FROM MediaSourceUsers msu JOIN MediaSources ms ON msu.MediaSourceId = ms.Id WHERE msu.UserId = @userId`. Dies ist eine einzelne Bulk-Query, die alle Quellzugriffe des Benutzers in einem Durchlauf lädt.

### `PlaylistService` (Service-Klasse)

- **Geänderte Methode:** `LoadUnlockedMediaIdsAsync` — wird umbenannt oder erweitert zu `LoadAccessCheckDataAsync`. Neue Signatur: Lädt zusätzlich die MediaSource-IDs des Benutzers. Rückgabewert: `(unlockedMovieCollectionIds: HashSet<long>, unlockedTVShowIds: HashSet<long>, mediaSourceIds: HashSet<long>)`. Dies reduziert die Anzahl der Service-Aufrufe und zentralisiert die Datenladung.

- **Neue private Hilfsmethode:** `ResolveUnlockMediaIdAsync(PlaylistEntry entry, Dictionary<MediaType, Dictionary<long, long>> hierarchyMappings)` — Gibt die ID zurück, die für die Unlock-Prüfung verwendet werden soll:
  - Für Movie: die MovieCollectionId (aufgelöst via Hierarchie-Mapping).
  - Für TVShowEpisode: die TVShowId (aufgelöst via Episode → Season → Show).
  - Für TVShowSeason: die TVShowId.
  - Für TVShow, MovieCollection: die direkte Id.

- **Neue private Hilfsmethode:** `ResolveMediaSourceIdAsync(PlaylistEntry entry, Dictionary<MediaType, Dictionary<long, long>> mediaSourceMappings)` — Gibt die MediaSourceId des Mediums zurück:
  - Für Movie, TVShowEpisode, TVShowSeason: wird die MediaSourceId der übergeordneten Collection/Show aufgelöst (via Hierarchie-Mapping).
  - Für TVShow, MovieCollection: wird die direkte MediaSourceId verwendet.
  - Diese Methode nutzt bestehende Bulk-Loading-Muster (`LoadTitlesForMediaRefsAsync`-ähnlich für Media-Source-Zuordnungen).

- **Geänderte Methode:** `IsEntryAccessible` (oder neue Methode `CheckEntryAccessible` als nicht-statische Methode mit Zugriff auf vorgeladene Daten) — Neue Signatur: `private bool CheckEntryAccessible(PlaylistEntry entry, long? unlockedUnlockId, long? resolvedMediaSourceId, HashSet<long> unlockedMovieCollectionIds, HashSet<long> unlockedTVShowIds, HashSet<long> mediaSourceIds)`. Logik:
  ```
  hasSourceAccess = mediaSourceIds.Contains(resolvedMediaSourceId)
  isUnlocked = (unlockedUnlockId.HasValue && 
                (unlockedMovieCollectionIds.Contains(unlockedUnlockId.Value) ||
                 unlockedTVShowIds.Contains(unlockedUnlockId.Value)))
  return hasSourceAccess || isUnlocked;
  ```
  Diese Methode wird testbar und verständlich, da alle Daten vorgeladen sind.

- **Geänderte Methode:** `BuildEntryDtosAsync` — Integration der neuen Zugriffsprüfung:
  1. Ruft `LoadAccessCheckDataAsync` auf (statt `LoadUnlockedMediaIdsAsync`).
  2. Baut Hierarchie-Mappings auf (Movie→MovieCollectionId, Episode→ShowId via Season, Season→ShowId).
  3. Baut MediaSource-Mappings auf (welches Movie/Episode/Season gehört zu welcher Collection/Show mit welcher MediaSourceId).
  4. Für jeden Eintrag: ruft `ResolveUnlockMediaIdAsync` und `ResolveMediaSourceIdAsync` auf.
  5. Ruft `CheckEntryAccessible` auf mit allen aufgelösten Daten.
  6. Setzt `entry.IsAccessible` im DTO basierend auf dem Ergebnis.

- **Änderung der Verwendungsstellen:** `GetPlaylistEntriesAsync`, `GetPlaylistEntriesPagedAsync`, `BuildAddResultAsync` benötigen keine Änderungen — die Logik ist zentral in `BuildEntryDtosAsync` implementiert und wird automatisch angewendet.

---

## Datenbankmigrationen

Keine.

---

## Validierungsregeln

Keine neuen Validierungen erforderlich.

---

## Konfigurationsänderungen

Keine.

---

## Seiteneffekte und Risiken

- **Bereich: Bestehende Playlist-Abfragen:** Durch die erweiterte Zugriffsprüfung können Einträge, die zuvor fälschlicherweise als nicht zugänglich markiert waren, jetzt als zugänglich erscheinen. Dies ist die beabsichtigte Korrektur, könnte aber für Anwender unerwartet sein (z. B. wenn sie auf ausgeblendete Einträge verlassen haben).
- **Bereich: Performance:** Drei zusätzliche Datenbank-Queries (MediaSource-IDs, hierarchische Auflösungen für Movie/Episode/Season) werden pro Playlist-Abruf durchgeführt. Mit Bulk-Loading (bestehend) ist dies O(1) und sollte nicht zu Performance-Problemen führen. Überwachung empfohlen.
- **Bereich: Tests:** Bestehende Tests, die auf das fehlerhafte Verhalten angewiesen sind (z. B. `GetEntries_EntryNotUnlocked_IsNotAccessible` für Movie/TVShowEpisode/TVShowSeason), müssen angepasst oder neu geschrieben werden.
- **Bereich: Dokumentation:** Die fehlerhafte Dokumentation in `docs/help/playlists-api.md` muss korrigiert werden, sonst entsteht Verwirrung.

---

## Umsetzungsreihenfolge

1. **Neue Methode `GetMediaSourceIdsForUserAsync` in `IUnlockedMediaService` und `UnlockedMediaService` anlegen**
   - Voraussetzungen: Keine (Interfaces und Service existieren).
   - Beschreibung: Implementiere die Methode in beiden Klassen. Query lädt alle MediaSourceIds für einen Benutzer aus der `MediaSourceUsers`-Tabelle.

2. **Refaktorierung von `LoadUnlockedMediaIdsAsync` zu `LoadAccessCheckDataAsync` in `PlaylistService`**
   - Voraussetzungen: Schritt 1 muss abgeschlossen sein.
   - Beschreibung: Ändere die Methode, um zusätzlich `GetMediaSourceIdsForUserAsync` aufzurufen und alle drei Datensätze (unlockedMovieCollectionIds, unlockedTVShowIds, mediaSourceIds) als Tuple zurückzugeben.

3. **Hilfsmethoden für Hierarchie- und MediaSource-Auflösung in `PlaylistService` implementieren**
   - Voraussetzungen: Schritt 2 muss abgeschlossen sein.
   - Beschreibung: Implementiere `ResolveUnlockMediaIdAsync` und `ResolveMediaSourceIdAsync`. Diese nutzen Bulk-geladene Mappings (für Hierarchie: verwende bestehende Datenstrukturen wie `GetParentMediaRefs` als Vorlage; für MediaSourceId: lade diese zusammen mit Titeln/Bildern via bestehende DB-Abfragen).

4. **Neue oder geänderte Methode `CheckEntryAccessible` (oder Mutation von `IsEntryAccessible`) implementieren**
   - Voraussetzungen: Schritt 3 muss abgeschlossen sein.
   - Beschreibung: Schreibe die Zugriffsprüfungs-Logik als testbare Methode mit allen Parametern vorgeladen (keine async-Operationen). Die Logik ist: `hasSourceAccess OR isUnlocked` wie in der Referenzimplementierung `EnsureAccessAsync`.

5. **`BuildEntryDtosAsync` anpassen, um neue Zugriffsprüfung zu integrieren**
   - Voraussetzungen: Schritte 2–4 müssen abgeschlossen sein.
   - Beschreibung: Rufe `LoadAccessCheckDataAsync` auf, baue Hierarchie- und MediaSource-Mappings auf, rufe `ResolveUnlockMediaIdAsync` und `ResolveMediaSourceIdAsync` für jeden Eintrag auf, und setze `entry.IsAccessible` basierend auf `CheckEntryAccessible`.

6. **Betroffene Unit-Tests korrigieren / neue Unit-Tests schreiben**
   - Voraussetzungen: Schritte 1–5 müssen abgeschlossen sein.
   - Beschreibung: 
     - Bestehende Tests, die das fehlerhafte Verhalten testet (z. B. `GetEntries_EntryNotUnlocked_IsNotAccessible` für Movie), müssen angepasst werden.
     - Schreibe neue Tests für alle Szenarien (siehe Tests-Sektion unten): Quellenzugriff ohne Unlock, Unlock ohne Quellenzugriff, weder noch, für alle 5 Medientypen.
     - Hilfsmethoden `ResolveUnlockMediaIdAsync` und `ResolveMediaSourceIdAsync` werden durch Integration in `BuildEntryDtosAsync` getestet.

7. **Dokumentation in `docs/help/playlists-api.md` korrigieren**
   - Voraussetzungen: Schritte 1–5 müssen abgeschlossen sein (damit die korrekte Funktionsweise getestet ist).
   - Beschreibung: Ändere die Aussagen zum Feld `IsAccessible` in der `DtoPlaylistEntry`-Dokumentation. Schreibe:
     - "Gibt an, ob der aktuell angemeldete Benutzer Zugriff auf den referenzierten Medieninhalt hat."
     - "Zugriff wird gewährt, wenn der Benutzer regulären Zugriff auf die Mediaquelle hat ODER wenn der Eintrag für ihn individuell freigeschaltet wurde."
     - "Für Filme wird die Freischaltung über die übergeordnete Filmsammlung geprüft; für Episoden und Staffeln über die übergeordnete Serie."
     - Referenziere die korrekte Hierarchie-Auflösungslogik.

8. **E2E-Tests für veränderte Benutzerfunktionalität schreiben (falls erforderlich)**
   - Voraussetzungen: Schritte 1–7 müssen abgeschlossen sein.
   - Beschreibung: Falls die Playlist-API über HTTP-Endpunkte verfügbar ist und User-sichtbar ist, schreibe E2E-Tests für:
     - Playlist-Eintrag ist zugänglich via Quellenzugriff (ohne Unlock).
     - Playlist-Eintrag ist zugänglich via Unlock (ohne Quellenzugriff).
     - Playlist-Eintrag ist nicht zugänglich (weder Quelle noch Unlock).
     - Dies sollte über echte HTTP-Requests getestet werden, um sicherzustellen, dass die Änderungen end-to-end korrekt funktionieren.

9. **Finaler Commit und Code-Review**
   - Voraussetzungen: Schritte 1–8 müssen abgeschlossen sein.
   - Beschreibung: Commit mit aussagekräftiger Commit-Message (z. B. "fix: korrekte Freischaltungsprüfung in Playlist-Einträgen für alle Medientypen"). Code-Review durchführen.

---

## Tests

### Neue Tests

| Test / Hilfsmethode | Testklasse | Was wird geprüft / bereitgestellt? |
|--------------------|------------|-------------------------------------|
| `GetEntries_UserHasSourceAccess_IsAccessible` | `PlaylistServiceTests_GetEntries` | Benutzer mit MediaSourceUser-Zugriff auf ein TVShow (ohne Unlock) → `IsAccessible = true` |
| `GetEntries_UserHasSourceAccess_MovieType_IsAccessible` | `PlaylistServiceTests_GetEntries` | Benutzer mit Quellenzugriff auf Movie (über MovieCollection) → `IsAccessible = true` |
| `GetEntries_UserHasSourceAccess_EpisodeType_IsAccessible` | `PlaylistServiceTests_GetEntries` | Benutzer mit Quellenzugriff auf TVShowEpisode (über TVShow) → `IsAccessible = true` |
| `GetEntries_UserHasSourceAccess_SeasonType_IsAccessible` | `PlaylistServiceTests_GetEntries` | Benutzer mit Quellenzugriff auf TVShowSeason (über TVShow) → `IsAccessible = true` |
| `GetEntries_UserUnlockedNoSourceAccess_IsAccessible` | `PlaylistServiceTests_GetEntries` | Benutzer ohne Quellenzugriff, aber mit Unlock auf TVShow → `IsAccessible = true` |
| `GetEntries_UserUnlockedNoSourceAccess_MovieType_IsAccessible` | `PlaylistServiceTests_GetEntries` | Benutzer mit Unlock auf MovieCollection (Movie → Collection) → `IsAccessible = true` |
| `GetEntries_UserUnlockedNoSourceAccess_EpisodeType_IsAccessible` | `PlaylistServiceTests_GetEntries` | Benutzer mit Unlock auf TVShow (TVShowEpisode → Season → Show) → `IsAccessible = true` |
| `GetEntries_UserUnlockedNoSourceAccess_SeasonType_IsAccessible` | `PlaylistServiceTests_GetEntries` | Benutzer mit Unlock auf TVShow (TVShowSeason → Show) → `IsAccessible = true` |
| `GetEntries_UserNoAccessNoUnlock_IsNotAccessible` | `PlaylistServiceTests_GetEntries` | Benutzer ohne Quelle und ohne Unlock auf TVShow → `IsAccessible = false` |
| `GetEntries_UserNoAccessNoUnlock_MovieType_IsNotAccessible` | `PlaylistServiceTests_GetEntries` | Benutzer ohne Zugriff auf Movie → `IsAccessible = false` |
| `GetEntries_UserNoAccessNoUnlock_EpisodeType_IsNotAccessible` | `PlaylistServiceTests_GetEntries` | Benutzer ohne Zugriff auf TVShowEpisode → `IsAccessible = false` |
| `GetEntries_UserNoAccessNoUnlock_SeasonType_IsNotAccessible` | `PlaylistServiceTests_GetEntries` | Benutzer ohne Zugriff auf TVShowSeason → `IsAccessible = false` |
| `GetEntries_UserNoAccessNoUnlock_CollectionType_IsNotAccessible` | `PlaylistServiceTests_GetEntries` | Benutzer ohne Zugriff auf MovieCollection → `IsAccessible = false` |
| `GetEntriesPaged_UserHasSourceAccess_IsAccessible` | `PlaylistServiceTests_GetEntriesPaged` | Pagination mit Quellenzugriff-Prüfung → `IsAccessible = true` |
| `GetEntriesPaged_UserUnlockedNoSourceAccess_IsAccessible` | `PlaylistServiceTests_GetEntriesPaged` | Pagination mit Unlock-Prüfung → `IsAccessible = true` |
| `GetEntriesPaged_UserNoAccessNoUnlock_IsNotAccessible` | `PlaylistServiceTests_GetEntriesPaged` | Pagination ohne Zugriff → `IsAccessible = false` |
| `AddMedia_UserHasSourceAccess_ResultIsAccessible` | `PlaylistServiceTests_AddMedia` | Benutzer mit Quellenzugriff (ohne Unlock) fügt Medium hinzu → zurückgegebener Eintrag hat `IsAccessible = true` |
| `AddMedia_UserUnlockedNoSourceAccess_ResultIsAccessible` | `PlaylistServiceTests_AddMedia` | Benutzer ohne Quellenzugriff, aber mit Unlock, fügt Medium hinzu → zurückgegebener Eintrag hat `IsAccessible = true` |
| `AddMedia_UserNoAccessNoUnlock_ResultIsNotAccessible` | `PlaylistServiceTests_AddMedia` | Benutzer ohne Quellenzugriff und ohne Unlock fügt Medium hinzu (weiterhin erlaubt, wie bisher) → zurückgegebener Eintrag hat `IsAccessible = false` |

### Betroffene bestehende Tests

| Test / Testklasse | Grund der Anpassung |
|-------------------|---------------------|
| `GetEntries_EntryNotUnlocked_IsNotAccessible` | Dieser Test testet derzeit nur TVShow. Er muss überprüft werden: falls er ein `IsAccessible = false` erwartet für ein Medium OHNE Unlock UND OHNE Quellenzugriff, bleibt das korrekt. Falls der Test ein Movie/Episode/Season ohne Unlock testet und `IsAccessible = false` erwartet, muss überprüft werden, ob der Test auch den Quellenzugriff berücksichtigt (wahrscheinlich nicht → Test muss angepasst werden oder erweitert). |
| `GetEntries_EntryUnlockedForCurrentUser_IsAccessible` | Wie oben: überprüfen, ob der Test alle betroffenen Medientypen abdeckt oder nur TVShow. Die Logik bleibt korrekt (Unlock → true), aber die Test-Coverage muss erweitert werden. |
| `PlaylistServiceTests_AddMedia` (allgemein) | Falls Tests für `AddMediaToPlaylistAsync` derzeit davon ausgehen, dass ein Benutzer ohne Unlock-Eintrag das Medium nicht hinzufügen darf, müssen diese Tests angepasst werden: Benutzer MIT Quellenzugriff sollten das Medium hinzufügen dürfen, unabhängig vom Unlock-Status. |

Falls bestehende Tests nicht an die neue Logik angepasst werden, werden sie fehlschlagen oder zu falschen Ergebnissen führen.

### E2E-Tests (primärer Funktionsnachweis)

| Priorität | Szenario | Testdatei / Testklasse | Abgedecktes Akzeptanzkriterium | Warum E2E nötig ist |
|-----------|----------|------------------------|-------------------------------|-------------------|
| Pflicht | **Szenario 1:** Benutzer mit Quellenzugriff, ohne Unlock — Medium ist in Playlist zugänglich | `PlaylistApiTests` oder äquivalent | `IsAccessible = true` via HTTP GET `/api/playlists/{id}/entries` | Der HTTP-Endpunkt muss das korrekte DTO mit `IsAccessible = true` liefern. Unit-Tests prüfen nur die Geschäftslogik; E2E prüft die komplette Integration (HTTP → Service → DTO-Serialisierung). |
| Pflicht | **Szenario 2:** Benutzer ohne Quellenzugriff, mit Unlock — Medium ist in Playlist zugänglich | `PlaylistApiTests` oder äquivalent | `IsAccessible = true` via HTTP GET `/api/playlists/{id}/entries` | Wie oben: E2E prüft end-to-end korrekte Freischaltungs-Auflösung und DTO-Serialisierung. |
| Pflicht | **Szenario 3:** Benutzer ohne Quellenzugriff und ohne Unlock — Medium ist in Playlist NICHT zugänglich | `PlaylistApiTests` oder äquivalent | `IsAccessible = false` via HTTP GET `/api/playlists/{id}/entries` | E2E prüft, dass Zugriff korrekt verweigert wird. |
| Pflicht | **Szenario 4:** Medium hinzufügen: Benutzer mit Quellenzugriff, ohne Unlock — hinzugefügter Eintrag korrekt als zugänglich markiert | `PlaylistApiTests` oder äquivalent | HTTP POST `/api/playlists/{id}/add` mit mediaType=Movie/Episode/Season liefert Ergebnis mit `IsAccessible = true` | E2E prüft, dass der `AddMediaToPlaylistAsync`-Endpunkt das `IsAccessible`-Feld korrekt berechnet (kein Blockieren des Hinzufügens — das bleibt unverändert erlaubt). |
| Empfohlen | **Szenario 6:** Hierarchie-Auflösung: Movie mit MovieCollection-Unlock ist in Playlist zugänglich | `PlaylistApiTests` oder äquivalent | `IsAccessible = true` für Movie-Eintrag mit freigeschalteter Collection | E2E prüft, dass die hierarchische Auflösung (Movie → Collection) korrekt funktioniert. Unit-Tests prüfen die Logik; E2E prüft die Integration. |
| Empfohlen | **Szenario 7:** Hierarchie-Auflösung: TVShowEpisode mit TVShow-Unlock ist zugänglich | `PlaylistApiTests` oder äquivalent | `IsAccessible = true` für Episode-Eintrag mit freigeschalteter Show | Wie oben. |

Welche bestehenden E2E-Tests müssen angepasst werden?

Keine — falls Playlists-E2E-Tests existieren, testen sie wahrscheinlich Happy-Path-Szenarien (mit vollständigem Zugriff). Diese Tests sollten weiterhin grün werden, da die neue Logik den vollständigen Zugriff weiterhin als zugänglich markiert.

Falls es E2E-Tests gibt, die derzeit erwarten, dass ein Movie/Episode/Season in einer Playlist NICHT zugänglich ist (weil das alte Verhalten das so vorgab), müssen diese angepasst oder gelöscht werden.

**Begründung für E2E-Tests:** Die Zugriffsprüfung ist ein kritischer Geschäftsprozess, der über HTTP-Endpunkte von Clients ausgelöst wird. Unit-Tests prüfen die Geschäftslogik isoliert; E2E-Tests stellen sicher, dass die komplette HTTP-Integration (Request → Service → DTO-Serialisierung → HTTP-Response) korrekt funktioniert und dass Clients die richtige `IsAccessible`-Information erhalten.

---

## Offene Punkte

Keine.

**Begründung:** Der Nutzer hat explizit festgehalten, dass der Bug eindeutig identifiziert ist und die Lösung klar (Referenzimplementierung in `EnsureAccessAsync` und `IsUnlockedAsync` existiert bereits). Die Designentscheidung zur Service-Layer-Erweiterung (`GetMediaSourceIdsForUserAsync`) ist technisch sauber und konsistent mit bestehenden Mustern. Alle Programmabläufe, Klassenerweiterungen und Tests sind konkret spezifiziert. Keine weiteren Klärungen erforderlich.
