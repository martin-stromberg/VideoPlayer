# Enums — Bestandsaufnahme

## Explizit nicht vorhanden (verifiziert)

Keine der von der Anforderung genannten Enums existiert: `ScheduleRuleType`, `ScheduleRecurrence`, `ScheduleSlotStatus`, `LiveStreamFormat`.

## `MediaType`

Datei: `VideoWebPlayer/Data/MediaType.cs`

Diskriminator für `PlaylistEntry.MediaType` und Hierarchie-Auflösung (`MediaHierarchyRegistry`). String-Pendant: `VideoWebPlayer.Client/Models/MediaTypeValues.cs` (Konstantenklasse, selbe Werte als `string` — JSON-Grenze).

| Wert | Bedeutung |
|------|-----------|
| `Movie` | Einzelner Film |
| `TVShowEpisode` | Einzelne Serienepisode |
| `TVShowSeason` | Staffel (kaskadiert zu Episoden) |
| `TVShow` | Serie (kaskadiert zu Staffeln und Episoden) |
| `MovieCollection` | Filmsammlung (kaskadiert zu Filmen) |

## `PlaylistSortMode`

Datei: `VideoWebPlayer/Data/PlaylistSortMode.cs` — String-Pendant: `PlaylistSortModeValues` (Client).

| Wert | Bedeutung |
|------|-----------|
| `ByReleaseDate` | Automatische Sortierung nach Erscheinungsdatum (Default) |
| `Manual` | Manuelle Sortierung durch den Anwender (`PlaylistEntry.SortOrder`) |

## `MediaSourceType`

Datei: `VideoWebPlayer/Data/MediaSourceType.cs` — **numerische Werte persistiert** (Alt-Backup-Default `(int)MediaSourceType.Sftp`).

| Wert | Bedeutung |
|------|-----------|
| `Sftp` = 0 | Entfernter SFTP-Server (Default) |
| `LocalDirectory` = 1 | Lokales Verzeichnis auf dem Server (inkl. UNC) |

## `PairingCodeKind`

Datei: `VideoWebPlayer/Data/PairingCodeKind.cs` — **vertraglich gepinnt** (`EnumContractTests`: Mitglieder und Reihenfolge festgeschrieben; Alt-Backup-Default `(int)PairingCodeKind.AdminCode`).

| Wert | Bedeutung |
|------|-----------|
| `AdminCode` = 0 | Kurzlebiges Admin-Pairing für `api/pairing/exchange` |
| `BootstrapTicket` = 1 | Bootstrap-Ticket (QR + Shortcode) für `api/pairing/bootstrap` |
| `DeviceInitiated` = 2 | Reserviert, noch nicht implementiert |

## Vertraglich gepinnte Enums (`EnumContractTests`)

`VideoWebPlayer.Tests/EnumContractTests.cs` schreibt Mitglieder, Reihenfolge und numerische Werte folgender Enums fest — neue Vertrags-Enums des Senders sollten ebenfalls gepinnt werden:

- `RecentEntryType` (existiert doppelt in Client- und Server-Assembly, beide müssen gleich sein): `Movie`, `MovieCollection`, `TVShow`, `TVShowSeason`, `TVShowEpisode`
- `ManualBackupJobStatus` / `RestoreBackupJobStatus`: `Idle`, `Queued`, `Running`, `Succeeded`, `Failed`
- `PairingCodeKind` (siehe oben)

## Weitere enum-artige Konstanten

- `ApiTokenScope` (`Controllers/Attributes/ApiTokenCheckAttribute.cs`): Scopes für das API-Token-Gate (`AnyClient` u. a.).
- `MediaBoxAction` (`Components/Shared/Media/`): Kontextmenü-Aktionen.
- `ManualBackupJobStatus`, `RestoreBackupJobStatus` (`Services/Backups/`): Jobstatus für die Admin-Anzeige.
- `RecentEntryType` (Client + Server, siehe oben).
