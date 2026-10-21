# Datenmodell (Client-DTOs und Sender-Entitäten)

## `DtoProgramGuideNow`
Datei: `VideoWebPlayer.Client/Models/DtoProgramGuideNow.cs`

| Eigenschaft | Typ | Beschreibung |
|-------------|-----|--------------|
| `ServerTimeUtc` | `DateTimeOffset` | Serverzeitpunkt der Auskunft |
| `Now` | `DtoProgramGuideEntry?` | laufende Sendung, `null` bei Sendelücke/ohne Sender |
| `Next` | `DtoProgramGuideEntry?` | nächste geplante Sendung, `null` wenn nichts folgt |

## `DtoProgramGuideEntry`
Datei: `VideoWebPlayer.Client/Models/DtoProgramGuideEntry.cs`

| Eigenschaft | Typ | Beschreibung |
|-------------|-----|--------------|
| `SlotId` | `long` | Id des `ProgramSlot` |
| `Title` | `string` | Anzeigetitel („Show — Season, E{n}: episode" bzw. Filmname), „Unbekannt" bei unauflösbarer Referenz |
| `MediaType` | `string?` | `MediaTypeValues.Movie`/`TVShowEpisode` oder `null` |
| `MediaId` | `long?` | Film-/Episoden-Id oder `null` |
| `MediaItemId` | `long` | Id des Videodatei-Medieneintrags |
| `IsFiller` | `bool` | aus Füller-Regel |
| `StartUtc` / `EndUtc` | `DateTimeOffset` | Sendefenster (UTC) |
| `DurationSeconds` | `double` | Sendelänge |
| `PositionSeconds` | `double?` | bereits gesendete Sekunden — nur bei laufender Sendung |
| `RemainingSeconds` | `double?` | Sekunden bis Sendeende — nur bei laufender Sendung |

## `ProgramSlot` (Server-Entität, nur Kontext)
Datei: `VideoWebPlayer/Data/` (ProgramSlot.cs)

`TVChannelId`, `Start`/`End` (UTC), `MediaItemId`, `MovieId`/`TVShowEpisodeId` (weiche Referenzen), `RuleType` (Snapshot), `Status` (`Scheduled`/`Displaced`). Die Auskunft filtert auf `Scheduled` — die UI braucht keinen eigenen Zugriff.

## `TVChannel` (Server-Entität, nur Kontext)

`Name`; genau ein ausgelieferter Sender.

## `Setup` (Server-Entität, nur Kontext)

Enthält bereits die Stream-Einstellungen aus Schritt 6 (`LiveStreamSegmentLengthSeconds`, `LiveStreamSegmentDirectory`, `LiveStreamSegmentRetentionMinutes`) — für die UI keine neuen Felder nötig.
