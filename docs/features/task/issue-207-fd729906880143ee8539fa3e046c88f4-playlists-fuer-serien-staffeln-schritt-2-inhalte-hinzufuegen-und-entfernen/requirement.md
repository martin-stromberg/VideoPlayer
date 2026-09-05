# Anforderung: Playlists – Inhalte hinzufügen und entfernen (Schritt 2)

## Fachliche Zusammenfassung

Das Playlist-Feature wird um die Kernfunktionalität erweitert: Anwender können ihrer Playlist einzelne oder gruppierte Medieninhalte (Filme, Episoden, Staffeln, Serien, Filmsammlungen) hinzufügen und wieder entfernen. Hinzugefügte Filme aus Filmsammlungen, Episoden aus Staffeln oder Serien werden mit ihrem Quell-Sammelwerk verknüpft, sodass später hinzugefügte verwandte Inhalte automatisch dieser Verbindung zugeordnet werden können. Die Verwaltung erfolgt auf Ebene einzelner Titel mit Duplikatsprüfung pro Playlist und Berechtigungskontrolle (nur Besitzer).

## Betroffene Klassen und Komponenten

### Datenmodellklassen (Entity Framework)

- **`Playlist`** (bestehend, Erweiterung)
  - Neue Navigation `PlaylistEntries` zur Sammlung der Playlist-Einträge
  - Neue Konfigurationseigenschaft für maximale Anzahl Einträge (optional, konfigurierbar)

- **`PlaylistEntry`** (neu zu erstellen)
  - `Id` (long, PrimaryKey)
  - `PlaylistId` (long, ForeignKey zu `Playlist`)
  - `Playlist` (Navigation zu `Playlist`)
  - `MediaType` (string oder Enum: `"Movie"`, `"TVShowEpisode"`, `"TVShowSeason"`, `"TVShow"`, `"MovieCollection"`)
  - `MediaId` (long, ID des Medieninhalts)
  - `ParentMediaType` (string oder Enum, nullable: `"MovieCollection"`, `"TVShowSeason"`, `"TVShow"` oder null für top-level-Einträge)
  - `ParentMediaId` (long?, nullable: ID des Sammel-Eintrags wenn zutreffend)
  - `AddedAt` (DateTime, Erstellungszeitpunkt des Eintrags)
  - Composite Unique Constraint: `(PlaylistId, MediaType, MediaId)` um Duplikate zu verhindern

### Logikklassen / Services

- **`IPlaylistService`** (bestehend, Erweiterung)
  - `AddMediaToPlaylistAsync(long playlistId, string userId, string mediaType, long mediaId, CancellationToken)` → `DtoPlaylistEntry`
    - Fügt einen Medieninhalt hinzu (mit Cascade-Logik)
    - Rückgabe mit Duplikatsinformation / Erfolgsstatus
  
  - `RemoveMediaFromPlaylistAsync(long playlistId, string userId, string mediaType, long mediaId, CancellationToken)` → void
    - Entfernt einen Medieninhalt
  
  - `GetPlaylistEntriesAsync(long playlistId, string userId, CancellationToken)` → `DtoPlaylistEntry[]`
    - Liefert alle Einträge einer Playlist (noch unsortiert, Sortierung ist Schritt 3)

- **`PlaylistService`** (bestehend, Erweiterung)
  - Implementierung der neuen Methoden mit:
    - Berechtigungsprüfung (nur Besitzer)
    - Validierung des `mediaType` gegen die 5 unterstützten Typen
    - Cascade-Logik: Bei Hinzufügen von `TVShow` alle zugehörigen `TVShowSeason` und `TVShowEpisode` hinzufügen; bei `TVShowSeason` alle `TVShowEpisode`; bei `MovieCollection` alle `Movie`
    - Duplikatsprüfung pro Playlist
    - Optional: Prüfung gegen `MaxPlaylistItemCount` (konfigurierbar)
    - Prüfung, ob der Medieninhalt noch existiert (bei nicht existentem Medieninhalt 404)

- **`IMediaService`** oder analoge Klasse (evtl. Erweiterung)
  - Hilfsmethoden zur Abfrage von Medieninhalten nach Typ und ID
  - Hilfsmethoden für Cascade-Abfragen (Episoden einer Staffel, Filme einer Sammlung etc.)

### DTOs (Client-Modelle)

- **`DtoPlaylistEntry`** (neu zu erstellen)
  - `Id` (long)
  - `PlaylistId` (long)
  - `MediaType` (string)
  - `MediaId` (long)
  - `MediaTitle` (string, für UI-Anzeige)
  - `ParentMediaType` (string?, nullable)
  - `ParentMediaId` (long?, nullable)
  - `ParentMediaTitle` (string?, nullable, für UI-Anzeige)
  - `AddedAt` (DateTime)

- **`DtoAddMediaToPlaylistRequest`** (neu zu erstellen)
  - `mediaType` (string, erforderlich)
  - `mediaId` (long, erforderlich)

- **`DtoRemoveMediaFromPlaylistRequest`** (neu zu erstellen, optional)
  - `mediaType` (string, erforderlich)
  - `mediaId` (long, erforderlich)

### API-Controller

- **`PlaylistsController`** (bestehend, Erweiterung)
  - `POST /api/playlists/{id}/entries` — Fügt einen Medieninhalt hinzu
    - Request: `DtoAddMediaToPlaylistRequest`
    - Antwort: `DtoPlaylistEntry` oder strukturierte Multiplex-Antwort mit Duplikatsinformation
    - Status: `200 OK` (erfolgreich), `400 Bad Request` (ungültiger Medientyp), `409 Conflict` (Duplikat mit Hinweis), `404 Not Found` (Medieninhalt nicht vorhanden)

  - `DELETE /api/playlists/{id}/entries/{mediaType}/{mediaId}` — Entfernt einen Medieninhalt
    - Antwort: `204 No Content`
    - Status: `404 Not Found` (Eintrag nicht in Playlist), `403 Forbidden` (Zugriff verweigert)

  - `GET /api/playlists/{id}/entries` — Liefert alle Einträge
    - Antwort: `DtoPlaylistEntry[]` (noch unsortiert)
    - Status: `404 Not Found` (Playlist nicht gefunden), `403 Forbidden` (Zugriff verweigert)

### Migrationen (EF Core)

- Neue Migration zur Erstellung der `PlaylistEntry`-Tabelle und der zugehörigen Indices
- Composite Unique Constraint auf `(PlaylistId, MediaType, MediaId)`

### Tests

- Unit-Tests für `PlaylistService`:
  - Hinzufügen eines Medieninhalts (Erfolgsfall)
  - Hinzufügen mit Duplikatsprüfung (Konflikt-Handling)
  - Cascade-Hinzufügen (Serie → alle Staffeln/Episoden, Staffel → alle Episoden, Sammlung → alle Filme)
  - Berechtigungsprüfung (Nicht-Besitzer)
  - Nicht existierender Medieninhalt
  - Entfernen eines Eintrags (Erfolgsfall)
  - Entfernen mit Berechtigungsprüfung
  - Optional: Maximale Anzahl Einträge überschritten

- Integration-Tests für `PlaylistsController`:
  - POST-Endpoint mit verschiedenen Inhaltstypen
  - GET-Endpoint zur Listabruf
  - DELETE-Endpoint mit Berechtigungsprüfung
  - Fehlerbehandlung (400, 404, 409, 403)

### UI-Komponenten

- Erweiterung von **`PlaylistDetail.razor`** oder neue Komponente
  - Einfache, unsortierte Auflistung der vorhandenen Einträge (ohne Infinity-List)
  - Entfernen-Button für jeden Eintrag
  - UI-Feedback für Duplikate (z. B. Toast-Nachricht)
  - Eingabe-/Auswahlmöglichkeit zum Hinzufügen von Inhalten:
    - Dropdown/Auswahl des Medientyps
    - Such- oder Auswahlfeld für den Medieninhalt
    - Button zum Hinzufügen

- Optional: Neue Komponente **`PlaylistMediaSelector.razor`** oder **`PlaylistAddMediaDialog.razor`**
  - Modal oder Inline-Form zur Auswahl und zum Hinzufügen von Medieninhalten

## Implementierungsansatz

### Datenpersistierung

1. **Neue Tabelle `PlaylistEntry`** mit den oben beschriebenen Spalten
2. **Composite Unique Index** auf `(PlaylistId, MediaType, MediaId)` zur Duplikatsprüfung auf DB-Ebene
3. **Foreign Key** von `PlaylistEntry.PlaylistId` zu `Playlist.Id` mit Cascade-Delete (beim Löschen einer Playlist werden alle zugehörigen Einträge gelöscht)
4. **Optional: Trigger oder Scheduled Job** zur Bereinigung verwaister Einträge, wenn ein Medieninhalt gelöscht wird (stille Entfernung ohne Benachrichtigung)

### Geschäftslogik

1. **Berechtigungsprüfung**: Vor jedem Zugriff auf eine Playlist wird geprüft, ob der aktuelle Benutzer der Besitzer ist (analog `PlaylistService.GetPlaylistAsync`).

2. **Cascade-Logik**:
   - Bei `AddMediaToPlaylistAsync(playlistId, userId, "TVShow", tvShowId)`:
     - Lade alle `TVShowSeason` für `tvShowId`
     - Für jede Staffel: lade alle `TVShowEpisode`
     - Füge ein `PlaylistEntry` für jeden hinzu mit `ParentMediaType`/`ParentMediaId` gesetzt
   - Analog für `TVShowSeason` und `MovieCollection`

3. **Duplikatsprüfung**:
   - Vor dem Einfügen von `PlaylistEntry` wird geprüft, ob bereits ein Eintrag mit `(PlaylistId, MediaType, MediaId)` existiert
   - Falls ja: Rückgabe mit 409 Conflict oder spezieller Response (z. B. `{ "status": "skipped", "reason": "duplicate" }`)
   - Falls nein: Eintrag wird eingefügt

4. **Maximale Anzahl Einträge (optional, konfigurierbar)**:
   - Konfigurationsschlüssel: z. B. `Playlists:MaxPlaylistItemCount` (null = unbegrenzt, Standard)
   - Bei Überschreitung: 400 Bad Request mit aussagekräftiger Fehlermeldung

5. **Entfernen (Idempotent)**:
   - `RemoveMediaFromPlaylistAsync` findet und löscht den Eintrag
   - Falls nicht vorhanden: Fehler 404 oder stille Rückgabe (zu klären)

### Externe Abhängigkeiten

- Bestehendes `IAuthService` zum Abrufen des aktuellen Benutzers
- Bestehendes DbContext (`ApplicationDbContext`) für EF Core-Operationen
- Bestehendes Medien-API/Service zum Abfragen von `TVShow`, `TVShowSeason`, `TVShowEpisode`, `Movie`, `MovieCollection`

## Konfiguration

### Anwendungseinstellungen (appsettings.json oder benutzerdefinierte Config-Klasse)

```json
{
  "Playlists": {
    "MaxPlaylistsPerUser": null,
    "MaxPlaylistItemCount": null
  }
}
```

- **`MaxPlaylistItemCount`** (int?, Standard: null)
  - `null`: Keine Obergrenze
  - `> 0`: Maximale Anzahl der Einträge pro Playlist
  - Die Konfiguration wird **optional** (nachgelagert) implementiert; für diesen Schritt kann die Unterstützung noch offen bleiben oder als Future-Work markiert werden

### Berechtigungen

- Alle Playlist-Operationen setzen voraus, dass der Benutzer authentifiziert ist (Bearer Token)
- Der Zugriff auf eine Playlist, die einem anderen Benutzer gehört, wird mit HTTP 403 (Forbidden) abgelehnt

## Offene Fragen / Klärungsbedarf

1. **Duplitat-Response-Format**:
   - Soll bei einem Duplikat eine 409-Antwort mit strukturiertem Fehler (z. B. `{ "error": "Titel bereits in Playlist" }`) zurückgegeben werden, oder ein alternativer Status wie 200 mit `{ "status": "skipped", "reason": "duplicate" }`?

2. **Entfernen (Remove) ohne Erfolgsbestätigung**:
   - Falls ein Eintrag nicht in der Playlist vorhanden ist: 404 zurückgeben oder stille 204?
   - Gilt Idempotenz (z. B. doppeltes Entfernen bricht nicht ab)?

3. **Kaskadierendes Entfernen**:
   - Wenn ein Benutzer eine `TVShow` hinzufügt, dann später eine einzelne `TVShowEpisode` aus derselben Serie manuell entfernt: Sollte die Episode nur aus der Playlist entfernt werden, oder auch die Serie bei Bedarf automatisch aktualisiert werden?
   - (Annahme: Jeder Eintrag ist unabhängig, manuelle Entfernung beeinflusst nicht andere Einträge)

4. **Medienbestand-Synchronisation**:
   - Sollen verwaiste Playlist-Einträge (deren Medieninhalt gelöscht wurde) aktiv abgefragt und bereinigt werden (z. B. im Service bei jeder Listierung), oder wird dies durch einen Background-Job/Trigger gelöst?

5. **Maximal-Items-Konfiguration – Timing**:
   - Soll die optionale Maximalbegrenzung schon in diesem Schritt implementiert werden, oder ist dies ein Punkt für einen späteren Schritt?

6. **Response bei Cascade-Hinzufügen mit teilweisem Fehler**:
   - Wenn Benutzer eine `TVShow` hinzufügt und einige Episoden bereits existieren: Alle überspringen, oder nur die duplizierten überspringen und die neuen hinzufügen?
   - (Annahme: Duplikate werden übersprungen, neue werden hinzugefügt; strukturierte Response informiert über Anzahl erfolgreicher und übersprungener Einträge)

7. **Title / Metadaten in `DtoPlaylistEntry`**:
   - Soll die `DtoPlaylistEntry` auch Metadaten wie Poster, Beschreibung etc. enthalten, oder reichen ID, Typ und Titel?
   - (Annahme für diesen Schritt: Nur Basis-Metadaten; detaillierte Anzeige mit Bildern etc. ist Schritt 3)

## Rahmenbedingungen (aus übergreifenden Vorgehensentscheidungen)

1. **Unterstützte Inhaltstypen**: Ausschließlich `Movie`, `TVShowEpisode`, `TVShowSeason`, `TVShow`, `MovieCollection`. Keine weiteren Typen.

2. **Keine feste fachliche Obergrenze**: Die Anzahl der Titel pro Playlist ist unbegrenzt. Eine optionale Konfiguration (`MaxPlaylistItemCount`) kann später hinzugefügt werden und ist standardmäßig deaktiviert (null).

3. **Playlist-Einträge bei Medienlöschung**: Wenn ein Medieninhalt aus dem Bestand verschwindet, wird der entsprechende Eintrag aus der Playlist still entfernt (ohne Benachrichtigung an den Benutzer). Dies kann durch einen Trigger, einen Background-Job oder bei der Abfrage implementiert werden.

4. **Listendarstellung nicht Teil dieses Schritts**: Die vollständige, performante Anzeige mit Sortierung (automatisch oder manuell) ist Gegenstand späterer Schritte. Für diesen Schritt genügt eine einfache, unsortierte Auflistung der Einträge in der UI, sowie die serverseitige Persistierung und ein Abruf-Endpoint.

5. **Berechtigungen**: Nur der Besitzer der Playlist darf Inhalte hinzufügen oder entfernen. Alle Berechtigungsprüfungen erfolgen serverseitig.

6. **Beliebige Kombinationen**: Eine Playlist darf eine beliebige Mischung aus Serien, Staffeln, Episoden, Filmen und Filmsammlungen enthalten.
