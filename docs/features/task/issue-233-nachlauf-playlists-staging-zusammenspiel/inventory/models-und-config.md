# Datenmodell und Konfiguration

## Backup-Restore: OptionalRestoreTables und OptionalRestoreColumns

**Datei:** `VideoWebPlayer/Services/Backups/VideoWebPlayerBackupData.cs`

### OptionalRestoreTables (Zeile 23–36)

Derzeit vorhanden:
- UpdateSettings
- UnlockedMediaEntries
- WatchedEntries
- Actors, MovieActors, TVShowEpisodeActors
- Playlists, PlaylistEntries, PlaylistEntryExclusions, PlaylistGenres, PlaylistBackfillMarkers

**Fehlen (F1):**
- PairedDevices
- PairingCodes
- RefreshTokens

Diese drei Tabellen sind neue EF-Entitäten (gemäß ApplicationDbContext.cs Zeilen 185, 189, 193), werden aber nicht als optional beim Restore gekennzeichnet. Folge: Alt-Backups ohne diese Tabellen werden mit `InvalidDataException` abgelehnt.

### OptionalRestoreColumns (Zeile 38–69)

Vorhanden: 40 Einträge für Playlists, ContinueWatchingEntries, Bilder, TVShow/Episode/Movie-Eigenschaften, MediaSources.SourceType  
**Struktur:** `"TableName.ColumnName"`-Einträge für Backups vor bestimmtem Datum; Defaults sind in `OptionalRestoreBoolDefaults`, `OptionalRestoreIntDefaults`, `OptionalRestoreLongDefaults` registriert.

**Fehlen:** Keine Spalteneinträge für die Geräte-Tabellen nötig, da die Tabellen komplett fehlen (nicht einzelne Spalten).

## Medienquellen und Backup

**Datei:** `VideoWebPlayer/Services/Backups/VideoWebPlayerBackupData.cs` Zeile 68, 102

`MediaSources.SourceType` ist korrekt in `OptionalRestoreColumns` und `OptionalRestoreIntDefaults` eingetragen mit Default `(int)MediaSourceType.Sftp`. Das ermöglicht Alt-Backups ohne diesen Enum-Wert.

## Konfiguration (Pairing, Session, Playlists)

**Neue Konfigurationsschlüssel (in appsettings.json):**

- `Pairing:CodeLength` (default im Code)
- `Pairing:CodeTtlMinutes` (default im Code)
- `Pairing:BootstrapTicketTtlMinutes` (default 5)
- `Pairing:BootstrapMaxTicketsPerHour` (default 10)
- `Pairing:BootstrapAdminOnly` (default false)
- `Auth:RefreshTokenTtlDays` (default 30)
- `Playlists:*` (diverse Limits und Parameter)

Diese sind in Release Notes (Englisch Zeile 13, Deutsch **fehlt**, siehe A3).

## Kontroller-Attribute und API-Token-Gate

**PlaylistsController:** Zeile 17–20  
- `[BearerTokenCheck]` auf Klassenebene
- Kein `[ApiTokenCheck]`
- Konsistent mit allen anderen Medien-Controllern

**AuthController:** Zeile 58, 98, 127  
- `[ApiTokenCheck]` mit Scope auf 3 Endpunkten: `login`, `refresh`, `logout` (MauiOnly) und `impersonate` (AnyClient)
- Das `X-API-Key`-Gate ist bewusst nur für Sitzungsausstellung, nicht für Playlist-Zugriff

**Befund:** Keine Inkonsistenz bei Playlists. Das Design ist konsistent: API-Token-Gate für Authentifizierung, JWT für Autorisierung.
