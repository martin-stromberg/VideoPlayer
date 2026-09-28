# Services und Geschäftslogik — Bestandsaufnahme A6–A9

## Services für Geräte-Pairing (A6)

### `IPairingBootstrapService`
- **Datei:** `VideoWebPlayer/Services/PairingBootstrapService.cs`
- **Zweck:** QR-Bootstrap-Flow mit einmaligen Tickets

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `CreateBootstrapTicketAsync(userId, isAdmin)` | public | Erstellt einmaliges Ticket (15-Min-Gültig), speichert Hash |
| `ResolveBootstrapTicketAsync(ticket)` | internal | Validiert Ticket-Hash, prüft Verfallzeit, generiert JWT/Refresh-Token |
| `GetBootstrapTicketAsync(ticketCode)` | internal | Lädt Ticket-Daten aus DB |

**Wichtig für A6:** Generiert aus einmaligem Ticket ein JWT mit `sub = Benutzer-ID` und einen Refresh-Token. Das Device Token wird separat vergeben.

### `IDeviceTokenService`
- **Datei:** `VideoWebPlayer/Services/DeviceTokenService.cs`
- **Zweck:** Geräte-Token-Verwaltung

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `IssueAsync(createdByUserId, deviceName)` | public | Erzeugt zufälliges Device Token, speichert Hash + PairedDevice-Eintrag |
| `ValidateAsync(tokenHash)` | public | Prüft Token-Hash, gibt PairedDevice zurück |
| `RevokeAsync(deviceId)` | public | Markiert Gerät als widerrufen (entzieht Refresh, alte JWT bis 12h gültig) |
| `RevokeAllForDeviceAsync(deviceId)` | internal | Invalidiert alle Refresh-Tokens des Geräts |

**Wichtig für A6:** Tokens sind an Geräte (nicht Benutzer) gebunden. Widerruf invalidiert Refresh, nicht aktive JWTs.

### `IRefreshTokenService`
- **Datei:** `VideoWebPlayer/Services/RefreshTokenService.cs`
- **Zweck:** Sitzungs-Erneuerung mit Rotation und Reuse-Detection

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `RotateAsync(oldTokenHash, deviceTokenHash, userId)` | public | Validiert alten Token, generiert neuen, speichert Rotation + Reuse-Marker |
| `ValidateAsync(tokenHash)` | public | Prüft Token-Gültigkeit, prüft Reuse-Detection |
| `RevokeAsync(tokenHash)` | public | Invalidiert einzelnen Refresh-Token |

**Wichtig für A6:** Implementiert Rotation mit Reuse-Detection. Doppelter Refresh desselben Tokens triggert Reuse-Detection (Sicherheitsmerkmal gegen Token-Kompromittierung).

## Services für Playlists (A6, A7)

### `IPlaylistService`
- **Datei:** `VideoWebPlayer/Services/PlaylistService.cs`
- **Zweck:** CRUD und Verwaltung von Playlists

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `CreatePlaylistAsync(userId, name, description, sortMode)` | public | Erstellt Playlist für Benutzer |
| `GetPlaylistAsync(playlistId)` | public | Lädt Playlist (Berechtigungsprüfung über separate Methode) |
| `UpdatePlaylistAsync(playlistId, dto)` | public | Aktualisiert Metadaten |
| `DeletePlaylistAsync(playlistId)` | public | Löscht Playlist |
| `AddMediaAsync(playlistId, mediaType, mediaId)` | public | Fügt Eintrag hinzu |
| `RemoveMediaAsync(playlistId, mediaType, mediaId)` | public | Entfernt Eintrag |
| `GetPlaylistEntriesPagedAsync(playlistId, pageNum, pageSize)` | public | Lädt Einträge (paginiert) |
| `ReorderAsync(playlistId, entryId, order)` | public | Setzt manuelle Sortierreihenfolge |

**Für A6:** Wird als client library method aufgerufen, prüft Berechtigungen über Bearer-Token.

**Für A7:** Alle Methoden sind quelltyp-unabhängig. Einträge können aus lokalen oder SFTP-Quellen stammen.

### `IPlaylistEntryAccessResolver`
- **Datei:** `VideoWebPlayer/Services/PlaylistEntryAccessResolver.cs`
- **Zweck:** Prüfung der Wiedergabeberechtigung für Playlist-Einträge

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `CanPlayAsync(userId, playlistEntry)` | public | Prüft, ob Benutzer Eintrag abspielen darf |
| `CanPlayBatchAsync(userId, entries)` | public | Batch-Prüfung für mehrere Einträge |

**Wichtig:** Arbeitet über `MediaSourceId` und `UnlockedMediaEntries`/`MediaSourceUsers`, nicht über `MediaSourceType`. Quelltyp-unabhängig.

**Für A7:** Lokale Quellen haben genauso Zugriffskontrolle wie SFTP-Quellen.

### `IContinueWatchingService`
- **Datei:** `VideoWebPlayer/Services/ContinueWatchingService.cs`
- **Zweck:** Verwaltung von "Weiterschauen"-Einträgen

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `TrackProgressAsync(userId, mediaType, mediaId, position, playlistId?)` | public | Speichert Fortschritt, optional mit Playlist-Referenz |
| `GetContinueWatchingAsync(userId, paged)` | public | Lädt Continue-Watching-Liste des Benutzers |
| `GetProgressAsync(userId, mediaType, mediaId)` | public | Lädt Fortschritt eines Mediums |
| `ReplaceOrphanedEntriesAsync(playlistId, oldMediaId, newMediaId)` | internal | Bei Quellenlöschung: ersetzt Einträge mit Playlist-Bezug |

**Für A6:** `PlaylistId` kann beim Fortschritt übermittelt werden. Tests müssen prüfen, dass `playlistId` korrekt erfasst wird.

**Für A7:** Bei Löschen einer lokalen Quelle werden Weiterschauen-Einträge mit `playlistId` durch den nächsten verfügbaren Titel ersetzt (quelltyp-unabhängig).

### `IPlaylistBackfillCoordinator`
- **Datei:** `VideoWebPlayer/Services/PlaylistBackfillCoordinator.cs`
- **Zweck:** Orchestrierung automatischer Nachlieferung neuer Medien in Playlists

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `ProcessNewEntriesAsync(entries)` | public | Verarbeitet neu eingescannte Medien auf Backfill-Targets |
| `GetMarkersForSourceAsync(sourceId)` | public | Lädt alle Backfill-Marker für eine Quelle |

**Für A7:** Wird von `ApplicationDbContext.PlaylistBackfillMarking` Hook aufgerufen. Quelltyp-unabhängig.

### `IPlaylistCoverService` / `PlaylistCoverImageGenerator`
- **Datei:** `VideoWebPlayer/Services/PlaylistCover/`
- **Zweck:** Automatische Collage-Generierung und Upload-Verwaltung

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `GenerateCoverAsync(playlistId, entries)` | public | Generiert Collage aus Medienbildern |
| `UploadUserCoverAsync(playlistId, imageBytes)` | public | Speichert vom Benutzer hochgeladenes Cover |
| `DeleteCoverAsync(playlistId)` | public | Löscht Cover-Bild |
| `GetCoverAsync(playlistId)` | public | Lädt Cover (mit Access-Control) |

**Für A7:** Cover-Generierung nutzt nur Medien-Bilder aus `Pictures`/`Movies`/… (DB), nicht Dateisystem. Quelltyp-unabhängig.

## Services für lokale Verzeichnisse (A7)

### `IMediaSourceReader` (abstrahiert)
- **Datei:** `VideoWebPlayer/Services/MediaSources/`
- **Zweck:** Abstrahierte Schnittstelle für Medienquellen-Lesen

**Implementierungen:**
- `SftpMediaSourceReader` — SFTP-Verbindung
- `LocalMediaSourceReader` — Lokales Dateisystem

| Methode | Beschreibung |
|---------|-------------|
| `OpenFileStreamAsync(path)` | Öffnet Datei-Stream (für Streaming) |
| `GetDirectoryEntriesAsync(path)` | Listet Verzeichnis |
| `GetFileInfoAsync(path)` | Lädt Metadaten |

**Wichtig für A7:** Der Dispatcher nutzt `MediaSourceType` für die Wahl der Implementierung.

### `IMediaSourceReaderDispatcher`
- **Datei:** `VideoWebPlayer/Services/MediaSources/MediaSourceReaderDispatcher.cs`
- **Zweck:** Routing zwischen Reader-Implementierungen basierend auf `MediaSourceType`

| Methode | Kurzbeschreibung |
|---------|------------------|
| `GetReaderAsync(sourceId)` | Lädt Reader (Impl-Klasse) basierend auf `SourceType` |
| `GetReaderForUrlAsync(baseUrl)` | Lädt Reader von URL (für Test-Mocks) |

**Für A7:** Dispatcher ist the key abstraction. Alle höheren Services nutzen ihn und sind damit quelltyp-unabhängig.

### `IMediaSourceReader` Implementations

#### `LocalMediaSourceReader`
- **Datei:** `VideoWebPlayer/Services/MediaSources/LocalMediaSourceReader.cs`
- **Zweck:** Dateisystem-Operationen für lokale Verzeichnisse

| Methode | Kurzbeschreibung |
|---------|------------------|
| `OpenFileStreamAsync(relativePath)` | Öffnet lokale Datei als Stream |
| `GetDirectoryEntriesAsync(relativePath)` | Listet lokale Verzeichnis-Inhalte |
| `GetFileInfoAsync(relativePath)` | Prüft Dateigröße, Datum, etc. |

**Für A7:** Wird durch `MediaSourceReaderDispatcher` für `SourceType = LocalDirectory` gewählt.

#### `SftpMediaSourceReader`
- **Datei:** `VideoWebPlayer/Services/MediaSources/SftpMediaSourceReader.cs`
- **Zweck:** SFTP-Operationen

| Methode | Kurzbeschreibung |
|---------|------------------|
| `OpenFileStreamAsync(remotePath)` | Verbindet zu SFTP, öffnet Datei |
| `GetDirectoryEntriesAsync(remotePath)` | SFTP-Verzeichnis-Listing |

### `MediaSourceClassifier`
- **Datei:** `VideoWebPlayer/Services/MediaSourceClassifier.cs`
- **Zweck:** Klassifiziert eingelesene Dateien in Media-Entitäten (Movie, Episode, etc.)

| Methode | Kurzbeschreibung |
|---------|------------------|
| `ClassifyAsync(sourceId, entries)` | Parst Pfade, erzeugt Movies/Episodes/… in DB |

**Für A7:** Quelltyp-unabhängig. Nutzt `MediaSourceReaderDispatcher`, nicht direkt `LocalMediaSourceReader`.

## Events und Hooks

### `ApplicationDbContext.PlaylistBackfillMarking` Hook
- **Datei:** `VideoWebPlayer/Data/ApplicationDbContext.PlaylistBackfillMarking.cs`
- **Zweck:** Bei jedem `SaveChanges`: neu eingefügte Media-Entitäten markieren für Backfill

**Workflow:**
1. `SaveChanges` wird aufgerufen
2. Hook extrahiert neu eingefügte `Movie`/`Episode`/… aus ChangeTracker
3. Erzeugt `PlaylistBackfillMarker` für alle Playlists, die diese Medienserie tracken
4. `PlaylistBackfillCoordinator` verarbeitet Marker asynchron

**Für A7:** Quelltyp-unabhängig. Gilt für SFTP- und lokale Quellen gleich.

### `IPlaylistBackfillSignal`
- **Datei:** `VideoWebPlayer/Services/IPlaylistBackfillSignal.cs`
- **Zweck:** Signal für manuellen Scan-Start (statt Hook-basiert)

| Methode | Kurzbeschreibung |
|---------|------------------|
| `BeginScan()` | Markiert, dass ein Scan beginnt |
| `EndScan(sourceId)` | Markiert Scan-Ende, triggert Backfill |

**Aufrufer:**
1. `MediaSourceScanService.ScanSourceAsync()` — Hintergrund-Scan
2. `MediaSourceAdmin.razor` — Manueller Komplett-Scan
3. `MediaSourceExplorer.razor` — "Neu erfassen" im Explorer

## Controllers (Public API, relevant für A6)

### `PlaylistsController`
- **Datei:** `VideoWebPlayer/Controllers/PlaylistsController.cs`
- **Authentifizierung:** `[BearerTokenCheck]` (kein `[ApiTokenCheck]`)
- **Wichtig:** Alle Playlist-Endpunkte sind für gekoppelte Geräte zugänglich (mit Bearer-Token, der vom Bootstrap-Flow stammt)

**Endpunkte (relevant für A6):**

| Methode | Endpunkt | Zweck |
|---------|----------|-------|
| `GetAsync` | `GET /api/playlists/{id}` | Playlist laden |
| `CreateAsync` | `POST /api/playlists` | Playlist erstellen |
| `UpdateAsync` | `PUT /api/playlists/{id}` | Playlist aktualisieren |
| `DeleteAsync` | `DELETE /api/playlists/{id}` | Playlist löschen |
| `AddMediaAsync` | `POST /api/playlists/{id}/entries` | Eintrag hinzufügen |
| `RemoveMediaAsync` | `DELETE /api/playlists/{id}/entries/{mediaType}/{mediaId}` | Eintrag entfernen |
| `GetEntriesPagedAsync` | `GET /api/playlists/{id}/entries/paged` | Einträge paginiert |
| `PlayAsync` | `POST /api/playlists/{id}/play` | Wiedergabe starten |
| `PlayNextAsync` | `POST /api/playlists/{id}/play/next` | Nächster Titel |
| `PlayPreviousAsync` | `POST /api/playlists/{id}/play/previous` | Vorheriger Titel |
| `PlayAdvanceAsync` | `POST /api/playlists/{id}/play/advance` | Zu Titel springen |
| `GetCoverAsync` | `GET /api/playlists/{id}/cover` | Cover laden (mit `?access_token=JWT` Unterstützung) |

### `ContinueWatchingController`
- **Datei:** `VideoWebPlayer/Controllers/ContinueWatchingController.cs`
- **Endpunkt:** `POST /api/continue-watching/progress` — Fortschritt melden (mit optionalem `playlistId`)

**Für A6:** Gerät meldet Fortschritt mit `playlistId` in Body.

### `AuthController`
- **Datei:** `VideoWebPlayer/Controllers/AuthController.cs`
- **Relevante Endpunkte:**

| Endpunkt | Authentifizierung | Zweck |
|----------|-------------------|-------|
| `POST /api/auth/refresh` | `[ApiTokenCheck]` (`MauiOnly`) | Refresh-Token einlösen |
| `POST /api/auth/logout` | `[ApiTokenCheck]` (`MauiOnly`) | Logout (invalidiert Refresh) |

**Wichtig für A6:** Geräte rufen diese Endpunkte mit `X-API-Key` auf, nicht mit Bearer-Token.

### `PairingBootstrapController`
- **Datei:** `VideoWebPlayer/Controllers/PairingBootstrapController.cs`
- **Endpunkt:** `POST /api/pairing/bootstrap` — QR-Ticket einlösen

**Für A6:** Geräte rufen diesen Endpunkt auf, erhalten verschlüsseltes JWT + Refresh-Token.

