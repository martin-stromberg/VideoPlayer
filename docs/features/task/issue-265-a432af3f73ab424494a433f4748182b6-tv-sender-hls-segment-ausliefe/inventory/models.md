# Datenmodell — Bestandsaufnahme

Keine Datenmodelländerung ist Teil der Anforderung. Folgende vorhandene Klassen werden im betroffenen Pfad nur gelesen und sind hier der Vollständigkeit halber dokumentiert.

## `Setup`

Datei: `VideoWebPlayer/Data/Setup.cs`

| Eigenschaft | Typ | Beschreibung / Zweck |
|-------------|-----|----------------------|
| `TvSenderEnabled` | `bool` | Globaler Freigabeschalter des TV-Senders (Z. 80); gelesen von `ProgramSettingsService.GetTvSenderEnabledAsync` → `LiveStreamController.GetFile` (404) und `LiveStreamWorker` |
| `LiveStreamSegmentLengthSeconds` | `int` (Default 6) | Segmentlänge für den ffmpeg-HLS-Aufruf (Z. 105); gelesen von `LiveStreamCoordinator` |
| `LiveStreamSegmentDirectory` | `string?` | Konfiguriertes Segmentverzeichnis, leer = `Data/LiveStream` unter dem ContentRoot (Z. 113); gelesen von `LiveStreamSegmentStore.GetDirectoryAsync` |
| `LiveStreamSegmentRetentionMinutes` | `int` (Default 30) | Retention für den Segment-Sweep (Z. 119); gelesen von `LiveStreamCoordinator.SynchronizeAsync` |

## `TvSenderUserAccess`

Datei: `VideoWebPlayer/Data/TvSenderUserAccess.cs`

| Eigenschaft | Typ | Beschreibung / Zweck |
|-------------|-----|----------------------|
| `UserId` | `string` (`[Key]`) | Benutzer-Id (FK auf `AspNetUsers`) mit Freigabe für den TV-Sender (Z. 18); gelesen von `ProgramSettingsService.IsTvSenderAccessGrantedAsync` → 403-Entscheidung in `GetFile` |
| `User` | `ApplicationUser` | Navigation zum Benutzer (Z. 23) |

EF-Konfiguration: `VideoWebPlayer/Data/Configurations/TvSenderUserAccessConfiguration.cs`.
