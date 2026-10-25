# Enums — Bestandsaufnahme

**Befund:** Es existiert kein Enum für Altersfreigaben (kein `AgeRating*`, `Fsk*`, `Mpaa*`, `Certification*`). Für eine strukturierte Modellierung (Offene Frage 2: System + Stufe, vergleichbare Darstellung für „≤ FSK 12") müsste ein neuer Enum/Wertetyp angelegt werden.

## `MediaType`
Datei: `VideoWebPlayer/Data/MediaType.cs`

Medientypen für `PlaylistEntry`-Referenzen; als Zahlen in der Datenbank gespeichert.

| Wert | Bedeutung |
|------|-----------|
| `Movie` | Einzelner Film |
| `TVShowEpisode` | Einzelne Episode |
| `TVShowSeason` | Staffel (kaskadiert auf Episoden) |
| `TVShow` | Serie (kaskadiert auf Staffeln/Episoden) |
| `MovieCollection` | Filmsammlung (kaskadiert auf Filme) |

Gespiegelt als String-Konstanten in `MediaTypeValues` (`VideoWebPlayer.Client/Models/MediaTypeValues.cs`: `Movie`, `TVShowEpisode`, `TVShowSeason`, `TVShow`, `MovieCollection`) — für die JSON-Grenze und `MediaMetadataUpdateRequest.ObjectType` (dort case- und separator-insensitiv normalisiert via `NormalizeType`).

## `MediaSourceType`
Datei: `VideoWebPlayer/Data/MediaSourceType.cs`

Typ der Medienquelle — bestimmt den `IMediaSourceReader` über `MediaSourceReaderDispatcher`; relevant, weil NFO-Lesung quellunabhängig über `IMediaSourceReader` läuft.

| Wert | Bedeutung |
|------|-----------|
| `Sftp = 0` | Entfernter SFTP-Server (Default für Bestandsdaten) |
| `LocalDirectory = 1` | Lokales Verzeichnis auf dem Server (inkl. UNC-Pfade) |

## Weitere Enums im Projekt (nicht freigabe-relevant, aber vertraglich abgesichert)

`RecentEntryType` (doppelt: Client- und Server-Assembly, identische Member), `ManualBackupJobStatus`, `RestoreBackupJobStatus`, `FfmpegToolDownloadJobStatus`, `PairingCodeKind` — durch `VideoWebPlayer.Tests/EnumContractTests.cs` vertraglich gepinnt (Member, Reihenfolge, numerische Werte). Fällt ein neuer Enum als Vertrag an (z. B. Rating-System an der JSON-Grenze), wäre dort das Muster für einen Contract-Test.
