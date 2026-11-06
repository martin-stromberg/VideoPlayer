# Bestandsaufnahme: Datenmodell

## `MediaItem`
Datei: `VideoWebPlayer/Data/MediaItem.cs`

| Eigenschaft | Typ | Beschreibung / Zweck |
|-------------|-----|----------------------|
| `MediaCollectionId` | long | Besitzende Sammlung (physisches Verzeichnis) |
| `MediaCollection` | MediaCollection | Navigation |
| `Duration` | TimeSpan? | Gemessene Laufzeit (Duration-Probe) |
| `DurationProbedAt` | DateTime? | Zeitpunkt des letzten Probe-Versuchs |
| `DurationProbeAttempts` | int | Fehlgeschlagene Probe-Versuche |

Erbt von `MediaEntry` (`Id`, `Name`, `Path`, `CreatedAt`, `Changed`, `ClassifiedAt`).
Duration-Probing gilt automatisch für **alle** `MediaItem`s — Trailer-Dateien erhalten ihre
Laufzeit ohne Zusatzaufwand.

## `MediaCollection`
Datei: `VideoWebPlayer/Data/MediaCollection.cs`

| Eigenschaft | Typ | Beschreibung / Zweck |
|-------------|-----|----------------------|
| `ParentMediaCollectionId` | long? | Eltern-Collection (Unterordner-Baum) |
| `Classifyable` | bool | Vom Scanner gesetzt, sobald Kind-Collections fertig sind |
| `Name`, `Path` | string | Verzeichnisname/-pfad |

Jedes physische Unterverzeichnis ist eine eigene `MediaCollection` — ein `trailers/`-Ordner
ist also eine Kind-Collection mit `Name == "trailers"` und `ParentMediaCollectionId` auf den
Film-Ordner.

## `Movie`
Datei: `VideoWebPlayer/Data/Movie.cs`

| Eigenschaft | Typ | Beschreibung / Zweck |
|-------------|-----|----------------------|
| `MovieCollectionId` | long? | Zugehörige `MovieCollection` |
| `CollectionId` | long | Quell-`MediaCollection` (Scan-Ursprung) |
| `MediaSourceId` | long | Quell-Medienquelle |
| `MovieMediaItems` | ICollection&lt;MovieMediaItem&gt; | Verknüpfungen zu Mediendateien |
| `MediaItems` | IEnumerable&lt;MediaItem&gt; | Convenience über `MovieMediaItems` |
| `IsManuallyEdited` | bool | Manuell bearbeitet → kein NFO-Overwrite |

## `MovieMediaItem`
Datei: `VideoWebPlayer/Data/MovieMediaItem.cs` (+ `Data/Configurations/MovieMediaItemConfiguration.cs`)

| Eigenschaft | Typ | Beschreibung / Zweck |
|-------------|-----|----------------------|
| `MovieId` | long | PK-Teil + FK zu `Movie` |
| `MediaItemId` | long | PK-Teil + FK zu `MediaItem` |

Zusammengesetzter Primärschlüssel `(MovieId, MediaItemId)`. Konsumenten, die die
**Hauptdatei** eines Films auflösen und bei Trailer-Links gefiltert werden müssen
(`!IsTrailer`):
- `ProgramScheduleService.MovieMediaItemsAsync` (repräsentatives Item pro Film)
- `ItemsController.FindMediaItemAsync` (`"movie"` → Wiedergabe)
- `MediaSourceClassifier.BackfillMissingActorsAsync` (NFO-Auflösung)
- `MediaSourceClassifier.AssignPicturesToMovieAsync` (Bild-Zuordnung, stem-basiert)

## `ProgramRule`
Datei: `VideoWebPlayer/Data/ProgramRule.cs`

Relevante Felder: `RuleType` (`Filler`=5), `MaterialType` (`ProgramRuleMaterialType`),
`MaterialId` (weicher Verweis, kein FK), `Genres`/`MediaSourceFilters` (nur Genre-Material),
`Priority`, `IsActive`.

## `ProgramSlot`
Datei: `VideoWebPlayer/Data/ProgramSlot.cs`

`Start`/`End` (UTC), `MediaItemId`, `MovieId` (weich), `TVShowEpisodeId` (weich),
`ProgramRuleId` (weich), `RuleType` (denormalisiert → Füller-Erkennung), `Status`
(`Scheduled`/`Displaced`). `ProgramGuideService` löst den Titel über `MovieId`/`TVShowEpisodeId`
auf — ein Trailer-Slot mit `MovieId` zeigt den Filmnamen.

## `ProgramRuleCursor`
Datei: `VideoWebPlayer/Data/ProgramRuleCursor.cs`

`LastContentType` (`ProgramCursorContentType`) + `LastContentId` — Fortsetzungsstand je Regel.

## `Setup`
Datei: `VideoWebPlayer/Data/Setup.cs`

Sender-Einstellungen (`ProgramScheduleHorizonHours`, `ProgramScheduleIntervalMinutes`,
`HolidayFederalState`, Live-Stream-Parameter). **Nicht** gewählt für `TrailerLeadDays` —
die Anforderung nennt ausdrücklich den Konfigurationsschlüssel `ProgramSchedule:TrailerLeadDays`
(appsettings-Optionsmuster wie `MediaProbe:`/`Playlists:`).

## `VideoWebPlayerBackupData` (Persistenz/Restore)
Datei: `VideoWebPlayer/Services/Backups/VideoWebPlayerBackupData.cs`

- `OptionalRestoreTables` (HashSet): Tabellen, die in Alt-Backups fehlen dürfen.
- `OptionalRestoreColumns` (HashSet `"Tabelle.Spalte"`): Spalten, die in Alt-Backups fehlen dürfen.
- `OptionalRestoreBoolDefaults` / `…IntDefaults` / `…LongDefaults`: Einfüge-Defaults für fehlende Spalten.
- `IgnoredRestoreColumns`: Legacy-Spalten (`MovieMediaItems.MovieId1` bereits enthalten).

Neue Spalte `MovieMediaItems.IsTrailer` → `OptionalRestoreColumns` +
`OptionalRestoreBoolDefaults` (Default `false`).

## Migrationen
`VideoWebPlayer/Migrations/` — zuletzt `20261021131012_AddProgramRuleGenreAndSourceLinks`,
Snapshot `ApplicationDbContextModelSnapshot.cs`. Erzeugung via `dotnet ef migrations add`.
