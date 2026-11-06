# Bestandsaufnahme: Datenmodell

## `Setup`
Datei: `VideoWebPlayer/Data/Setup.cs`

| Eigenschaft | Typ | Beschreibung / Zweck |
|-------------|-----|----------------------|
| `Id` | `int` | Primärschlüssel der Singleton-Zeile |
| `ApplicationTitle` | `string` | Anwendungstitel |
| `ScanProcessIntervalMinutes` | `int` | Scan-Intervall (Default 60) |
| `MediaCollectionScanIntervalDays` | `int` | Re-Scan-Intervall (Default 7) |
| `ContinueWatchingEndThresholdSeconds` | `int` | Weiterschau-Schwelle (Default 30) |
| `ActorCollectionThresholdPercent` | `int` | Darstellungsschwelle (Default 50) |
| `MdnsAdvertisementEnabled` | `bool` | mDNS-Admin-Schalter (Default true) |
| `DiscoveryPublicBaseUrl` | `string?` | Admin-Override der öffentlichen Basis-URL |
| `PlaylistBackfillLastSweepAt` | `DateTime?` | Zeitstempel des letzten Backfill-Sweeps |
| `HolidayFederalState` | `string?` | Bundesland für Feiertagsregeln |
| `ProgramScheduleHorizonHours` | `int` | Sendeplan-Horizont (Default 48 h) |
| `ProgramScheduleIntervalMinutes` | `int` | Sendeplan-Generierungsintervall (Default 15 min) |

Neue Stream-Einstellungen folgen diesem Muster (int/string?-Spalten + Migration + `OptionalRestoreColumns` + `OptionalRestoreIntDefaults`).

## `ProgramSlot`
Datei: `VideoWebPlayer/Data/ProgramSlot.cs`

| Eigenschaft | Typ | Beschreibung / Zweck |
|-------------|-----|----------------------|
| `Id` | `long` | Primärschlüssel |
| `TVChannelId` / `TVChannel` | `long` / `TVChannel` | Besitzender Sender (FK + Kaskade) |
| `Start` / `End` | `DateTime` | UTC-Sendezeit des Slots |
| `MediaItemId` | `long` | Weiche Referenz auf die Videodatei (kein DB-FK) |
| `MovieId` / `TVShowEpisodeId` | `long?` | Weiche Inhaltsreferenzen (kein DB-FK) |
| `ProgramRuleId` | `long?` | Weiche Referenz auf die erzeugende Regel |
| `RuleType` | `ProgramRuleType` | Snapshot der Regelart (`Filler` erkennbar) |
| `Status` | `ProgramSlotStatus` | `Scheduled` belegt Sendezeit, `Displaced` ist Historie |
| `CreatedAt` | `DateTime` | Erstellungszeitpunkt |

## `MediaItem`
Datei: `VideoWebPlayer/Data/MediaItem.cs` (erbt `MediaEntry`)

| Eigenschaft | Typ | Beschreibung / Zweck |
|-------------|-----|----------------------|
| `MediaCollectionId` / `MediaCollection` | `long` / `MediaCollection` | Besitzende Collection (FK) |
| `Duration` | `TimeSpan?` | Ermittelte Laufzeit (Schritt 1) |
| `DurationProbedAt` | `DateTime?` | Zeitpunkt der Laufzeitermittlung |
| `Path` (geerbt) | `string` | Gespeicherter Dateipfad; der eigentliche Dateiname wird per `Path.GetFileName(Path)` extrahiert und relativ zur Collection aufgelöst (`ItemsController.OpenMediaFileAsync`) |

## `MediaCollection`
Datei: `VideoWebPlayer/Data/MediaCollection.cs`

| Eigenschaft | Typ | Beschreibung / Zweck |
|-------------|-----|----------------------|
| `MediaSourceId` / `MediaSource` | `long` / `MediaSource` | Besitzende Quelle (FK; für Reader-Dispatch eager zu laden) |
| `Path` (geerbt) | `string` | Verzeichnispfad innerhalb der Quelle |

## `MediaSource`
Datei: `VideoWebPlayer/Data/MediaSource.cs` (Encoding: ISO-8859/CP1252!)

| Eigenschaft | Typ | Beschreibung / Zweck |
|-------------|-----|----------------------|
| `Path` (geerbt) | `string` | Quell-Root (bei `LocalDirectory` das Basisverzeichnis) |
| `Host` / `Port` / `Username` / `Password` | `string`/`int`/`string?` | SFTP-Zugangsdaten |
| `SourceType` | `MediaSourceType` | `Sftp` (0, Default) oder `LocalDirectory` (1) |

## `MediaSourceType`
Datei: `VideoWebPlayer/Data/MediaSourceType.cs`

| Wert | Bedeutung |
|------|-----------|
| `Sftp` | Entfernter SFTP-Server |
| `LocalDirectory` | Lokales Verzeichnis auf dem Server (inkl. UNC) |

## `DtoProgramGuideNow` / `DtoProgramGuideEntry`
Dateien: `VideoWebPlayer.Client/Models/DtoProgramGuideNow.cs`, `DtoProgramGuideEntry.cs`

| Eigenschaft | Typ | Beschreibung / Zweck |
|-------------|-----|----------------------|
| `ServerTimeUtc` | `DateTimeOffset` | Serverzeit der Auskunft |
| `Now` / `Next` | `DtoProgramGuideEntry?` | Laufende / nächste Sendung (`null` bei Lücke) |
| `SlotId` | `long` | `ProgramSlot.Id` |
| `MediaItemId` | `long` | Referenz auf die Videodatei — die Referenz für die Stream-Auslieferung |
| `MediaType` / `MediaId` | `string?`/`long?` | `MediaTypeValues.Movie`/`TVShowEpisode` + Inhalts-Id |
| `IsFiller` | `bool` | Füller-Slot |
| `StartUtc` / `EndUtc` | `DateTimeOffset` | Sendezeit |
| `DurationSeconds` | `double` | Slot-Länge |
| `PositionSeconds` / `RemainingSeconds` | `double?` | Nur bei laufender Sendung gesetzt — Start-Offset für ffmpeg |
| `Title` | `string` | Aufgelöster Titel oder „Unbekannt" |
