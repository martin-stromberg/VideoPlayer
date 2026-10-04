# Bestandsaufnahme: Datenmodell

Für die Anforderung (mDNS-Advertisement des Servers) existiert kein dediziertes Datenmodell und keine Discovery-bezogene Entität. Einzige für die offene Frage „Admin-schaltbar über `Setup`?" relevante Klasse:

## `Setup`
Datei: `VideoWebPlayer/Data/Setup.cs`

| Eigenschaft | Typ | Beschreibung / Zweck |
|-------------|-----|----------------------|
| `Id` | `int` | Primärschlüssel. |
| `DataVersion` | `int` | Aktuelle Datenversion. |
| `GenresChanged` | `bool` | Markiert Genre-Änderungen. |
| `ApplicationTitle` | `string` | Anwendungstitel (Default „Martins Videosammlung"). |
| `ScanProcessIntervalMinutes` | `int` | Scan-Intervall in Minuten (Default 60). |
| `MediaCollectionScanIntervalDays` | `int` | Re-Scan-Intervall für Sammlungen in Tagen (Default 7). |
| `ContinueWatchingEndThresholdSeconds` | `int` | Sekunden vor Videoende, ab denen die Position nicht mehr gespeichert wird (Default 30). |
| `ActorCollectionThresholdPercent` | `int` | Schwellwert für Sammlungsanzeige auf Schauspieler-Detailseiten (Default 50). |
| `PlaylistBackfillLastSweepAt` | `DateTime?` | Zeitpunkt (UTC) des letzten täglichen Playlist-Backfill-Sweeps; `null` = noch nie gelaufen. |

Bemerkungen:
- Wird über `ProgramSettingsService` (`VideoWebPlayer/Services/ProgramSettingsService.cs`) als Singleton-Zeile verwaltet; `DbSet<Setup>` `Setups` im `ApplicationDbContext`.
- Keine Discovery-/Netzwerk-Eigenschaft. Eine Admin-Einstellung für mDNS würde eine EF-Migration und eine Erweiterung von `VideoWebPlayerBackupData` (`OptionalRestoreColumns`) erfordern — siehe `requirement.md`, offene Frage 6.
- Es existiert keine `Discovery`-/`Mdns`-Entität und keine `DiscoverySettings`-Optionsklasse. Optionsklassen-Konvention siehe `VideoWebPlayer/Configuration/PlaylistSettings.cs` bzw. `EpisodeBackgroundImageOptions`.
