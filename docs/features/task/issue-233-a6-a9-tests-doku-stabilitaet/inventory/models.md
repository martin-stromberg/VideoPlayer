# Datenmodelle — Bestandsaufnahme A6–A9

## Modelle für Geräte-Pairing (A6)

### `PairedDevice`
- **Datei:** `VideoWebPlayer/Data/PairedDevice.cs`
- **Zweck:** Darstellung eines gekoppelten Geräts in der Datenbank
- **Eigenschaften:**

| Eigenschaft | Typ | Beschreibung |
|-------------|-----|-------------|
| Id | long | Eindeutige ID |
| CreatedByUserId | string | ID des Benutzers, der das Gerät gekoppelt hat |
| DeviceName | string | Anzeigename des Geräts |
| TokenHash | string | SHA256-Hash des Geräte-Tokens (aus `X-API-Key`-Header) |
| CreatedAt | DateTime | Zeitstempel der Geräteerstellung |
| IsRevoked | bool | Flag, ob das Gerät widerrufen wurde (Refresh-Token ungültig) |
| RevokedAt | DateTime? | Zeitstempel des Widerrufs |
| Model | string? | Geräte-Modell (z.B. "MAUI", "TV") |

### `PairingCode`
- **Datei:** `VideoWebPlayer/Data/PairingCode.cs`
- **Zweck:** Einmaliger Pairing-Code (alt: Code-Exchange) oder Bootstrap-Ticket
- **Eigenschaften:**

| Eigenschaft | Typ | Beschreibung |
|-------------|-----|-------------|
| Id | long | Eindeutige ID |
| UserId | string | ID des Benutzers |
| Code | string | Der Pairing-Code oder Bootstrap-Ticket (Plaintext oder gekürzt) |
| Kind | string | Art des Codes: `CodeExchange` oder `Bootstrap` |
| TicketHash | string? | Bei Bootstrap: SHA256-Hash des einmaligen Tickets (verhindert Replay) |
| ExpiresAt | DateTime | Verfallszeit (z.B. 15 Min für Bootstrap) |
| UsedAt | DateTime? | Zeitstempel der Einlösung |
| CreatedAt | DateTime | Erstellungszeitpunkt |

### `RefreshToken`
- **Datei:** `VideoWebPlayer/Data/RefreshToken.cs`
- **Zweck:** Sitzungs-Erneuerungsnachweis für gekoppelte Geräte
- **Eigenschaften:**

| Eigenschaft | Typ | Beschreibung |
|-------------|-----|-------------|
| Id | long | Eindeutige ID |
| PairedDeviceId | long | Fremdschlüssel: zugehöriges gekoppeltes Gerät |
| TokenHash | string | SHA256-Hash des Tokens (Plaintext wird nicht gespeichert) |
| JwtId | string | Eindeutige ID des JWTs, das mit diesem Token gehört |
| ReusedTokenHash | string? | Bei Reuse-Detection: Hash des wiederverwendeten Tokens |
| IsRevoked | bool | Flag, ob dieser Refresh-Token widerrufen wurde |
| ExpiresAt | DateTime | Token-Ablaufdatum |
| CreatedAt | DateTime | Erstellungszeitpunkt |

## Modelle für Playlists (A6, A7)

### `Playlist`
- **Datei:** `VideoWebPlayer/Data/Playlist.cs`
- **Zweck:** Wiedergabeliste mit Metadaten
- **Eigenschaften:**

| Eigenschaft | Typ | Beschreibung |
|-------------|-----|-------------|
| Id | long | Eindeutige ID |
| UserId | string | ID des Besitzers |
| Name | string | Playlist-Name |
| Description | string? | Beschreibung |
| IsPublic | bool | Ob die Playlist öffentlich zugänglich ist |
| SortMode | string | Sortiermodus: `Manual`, `ByName`, `ByReleaseDate`, etc. |
| CoverPictureId | long? | Fremdschlüssel auf Cover-Bild (generiert oder hochgeladen) |
| CoverPictureIsUserUploaded | bool | Ob das Cover vom Benutzer hochgeladen wurde (vs. auto-generiert) |
| CreatedAt | DateTime | Erstellungszeitpunkt |

**Wichtig für A6:** `Playlist` ist benutzer-spezifisch (`UserId`). Ein gekoppeltes Gerät sieht die Playlists des JWT-Benutzers.

**Wichtig für A7:** `Playlist` ist quelltyp-unabhängig. Einträge können aus SFTP- oder lokalen Verzeichnissen stammen.

### `PlaylistEntry`
- **Datei:** `VideoWebPlayer/Data/PlaylistEntry.cs`
- **Zweck:** Ein Eintrag (Musik, Episode, Film) in einer Playlist
- **Eigenschaften:**

| Eigenschaft | Typ | Beschreibung |
|-------------|-----|-------------|
| Id | long | Eindeutige ID |
| PlaylistId | long | Fremdschlüssel auf Playlist |
| MediaType | string | Typ des Mediums: `Movie`, `TVShow`, `TVShowSeason`, `TVShowEpisode`, `MovieCollection`, `Music` |
| MediaId | long | ID des Mediums (in `Movies`, `TVShows`, etc.) |
| SortOrder | int | Manuelle Sortierreihenfolge |
| AddedAt | DateTime | Zeitstempel des Hinzufügens zur Playlist |

**Wichtig für A6/A7:** `PlaylistEntry` speichert `MediaType` und `MediaId`, nicht die konkrete Medienquelle. Der Zugriff wird über `PlaylistEntryAccessResolver` ermittelt, der quelltyp-unabhängig arbeitet.

### `PlaylistBackfillMarker`
- **Datei:** `VideoWebPlayer/Data/PlaylistBackfillMarker.cs`
- **Zweck:** Marker für automatische Nachlieferung neuer Medien in Playlists
- **Eigenschaften:**

| Eigenschaft | Typ | Beschreibung |
|-------------|-----|-------------|
| Id | long | Eindeutige ID |
| PlaylistId | long | Fremdschlüssel auf Playlist |
| SeriesFolderId | string | Ordner-ID der Serie (z.B. bei SFTP) |
| LocalFolderId | string? | Lokaler Pfad (bei lokalen Verzeichnissen) |
| LastProcessedEntryTimestamp | DateTime? | Zeitstempel der letzten verarbeiteten Datei |
| CreatedAt | DateTime | Erstellungszeitpunkt |

**Wichtig für A7:** `PlaylistBackfillMarker` wurde erweitert, um lokale Verzeichnisse zu unterstützen (Spalte `LocalFolderId`). Die Backfill-Logik ist quelltyp-unabhängig.

### `ContinueWatchingEntry`
- **Datei:** `VideoWebPlayer/Data/ContinueWatchingEntry.cs`
- **Zweck:** Weiterschauen-Eintrag mit Fortschrittserfassung
- **Eigenschaften:**

| Eigenschaft | Typ | Beschreibung |
|-------------|-----|-------------|
| Id | long | Eindeutige ID |
| UserId | string | ID des Benutzers |
| MediaType | string | Typ des Mediums |
| MediaId | long | ID des Mediums |
| PositionInSeconds | long | Aktuelle Spielposition |
| DurationInSeconds | long? | Gesamtdauer (ggf. null) |
| PlaylistId | long? | **NEU:** Fremdschlüssel auf Playlist (wenn aus Playlist abgespielt) |
| LastWatchedAt | DateTime | Zuletzt abgespielt am |
| CreatedAt | DateTime | Erstellungszeitpunkt |

**Kritisch für A6/A7:** `PlaylistId` wurde hinzugefügt, um die Herkunft einer Wiedergabe zu tracken. Beim Löschen einer Medienquelle können "Weiterschauen"-Einträge mit Playlist-Bezug durch den nächsten verfügbaren Titel derselben Playlist ersetzt werden.

## Modelle für lokale Verzeichnisse (A7)

### `MediaSource`
- **Datei:** `VideoWebPlayer/Data/MediaSource.cs`
- **Zweck:** Medienquelle (SFTP oder lokal)
- **Eigenschaften:**

| Eigenschaft | Typ | Beschreibung |
|-------------|-----|-------------|
| Id | long | Eindeutige ID |
| Name | string | Name der Quelle |
| SourceType | int | `0` = SFTP, `1` = LocalDirectory |
| Host | string? | SFTP-Host (nur bei SFTP) |
| Port | int | SFTP-Port |
| Path | string | SFTP-Pfad oder lokaler Pfad |
| Username | string? | SFTP-Benutzer (nur bei SFTP) |
| Password | string? | SFTP-Passwort (verschlüsselt) |
| IsPublic | bool | Ob die Quelle global sichtbar ist |
| CreatedAt | DateTime | Erstellungszeitpunkt |

**Wichtig für A7:** `SourceType` unterscheidet zwischen SFTP und lokalen Verzeichnissen. Die Quellen-Abstraktionen (`IMediaSourceReader`, `MediaSourceReaderDispatcher`) arbeiten quelltyp-unabhängig.

### `LocalMediaSource`
- **Datei:** `VideoWebPlayer/Data/LocalMediaSource.cs`
- **Zweck:** Zusätzliche Konfiguration für lokale Verzeichnis-Quellen
- **Eigenschaften:**

| Eigenschaft | Typ | Beschreibung |
|-------------|-----|-------------|
| MediaSourceId | long | Fremdschlüssel auf MediaSource (1:1) |
| RootDirectory | string | Absoluter lokaler Pfad |
| IncludeSubdirectories | bool | Ob Unterordner rekursiv gescannt werden |
| ScanIntervalMinutes | int | Auto-Scan-Intervall |
| LastScannedAt | DateTime? | Zeitstempel des letzten Scans |

**Wichtig für A7:** Wird genutzt, um lokale Verzeichnis-spezifische Einstellungen zu speichern, ohne das SFTP-Modell zu überlasten.

## Modelle für lokale Medien (A7)

Alle Medienmodelle sind quelltyp-unabhängig und nutzen `MediaSourceId`:

| Modell | Datei | Schlüssel-Eigenschaft | Zweck |
|--------|-------|----------------------|-------|
| `Movie` | VideoWebPlayer/Data/Movie.cs | `MediaSourceId` | Film mit Quelle |
| `TVShow` | VideoWebPlayer/Data/TVShow.cs | `MediaSourceId` | Fernsehserie |
| `TVShowSeason` | VideoWebPlayer/Data/TVShowSeason.cs | `MediaSourceId` | Staffel |
| `TVShowEpisode` | VideoWebPlayer/Data/TVShowEpisode.cs | `MediaSourceId` | Episode |
| `MovieCollection` | VideoWebPlayer/Data/MovieCollection.cs | `MediaSourceId` | Filmsammlung |
| `Music` | VideoWebPlayer/Data/Music.cs | `MediaSourceId` | Musikstück |

**Für A7:** Alle diese Modelle funktionieren quelltyp-unabhängig, da sie nur auf `MediaSourceId` verweisen.

## DTOs für Client-Bibliothek (A5, relevant für A6)

### Pairing/Bootstrap DTOs
- `PairingExchangeRequest`, `PairingExchangeResponse`
- `PairingBootstrapRequest`, `PairingBootstrapResponse`, `PairingBootstrapPayload`
- `RefreshTokenRequest`, `RefreshTokenResponse`

**Datei:** `VideoWebPlayer.Client/Models/`

### Playlist DTOs (via `IPlaylistApiClient`)
- `DtoPlaylist` — Playlist-Metadaten
- `DtoPlaylistEntry` — Eintrag in Playlist
- `DtoPlaylistAddResult` — Ergebnis beim Hinzufügen
- `DtoPlaylistEntriesPagedResult` — Paginiertes Abfrageergebnis
- `DtoPlaylistPlaybackStart` — Wiedergabe-Start-Info
- `DtoPlaylistPlaybackNavigation` — Navigation (next/prev/advance)
- `DtoPlaylistNavigationMode` — Navigations-Modus

**Datei:** `VideoWebPlayer.Client/Models/` und `VideoWebPlayer/ViewModels/`

