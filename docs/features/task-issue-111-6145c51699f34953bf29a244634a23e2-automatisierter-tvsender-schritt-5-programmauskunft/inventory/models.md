# Bestandsaufnahme: Datenmodell

## `ProgramSlot`

Datei: `VideoWebPlayer/Data/ProgramSlot.cs`

Konkreter Sendeplatz des `TVChannel`; belegt `Start`–`End` (UTC) im rollierenden Sendeplan.
Referenzen auf Mediendatei, Inhalt und erzeugende Regel sind bewusst weich (keine DB-FKs), damit
Planhistorie Löschungen überlebt.

| Eigenschaft | Typ | Beschreibung / Zweck |
|-------------|-----|----------------------|
| `Id` | `long` | Primärschlüssel |
| `TVChannelId` | `long` | FK auf `TVChannel` |
| `TVChannel` | `TVChannel` | Navigation |
| `Start` | `DateTime` | UTC-Start der Ausstrahlung |
| `End` | `DateTime` | UTC-Ende (`Start` + `MediaItem.Duration`) |
| `MediaItemId` | `long` | Weiche Referenz auf `MediaItem` (kein FK) |
| `MovieId` | `long?` | Weiche Referenz auf `Movie` (bei Film-Slots) |
| `TVShowEpisodeId` | `long?` | Weiche Referenz auf `TVShowEpisode` (bei Episoden-Slots) |
| `ProgramRuleId` | `long?` | Weiche Referenz auf erzeugende `ProgramRule` |
| `RuleType` | `ProgramRuleType` | Denormalisierter Snapshot des Regeltyps (Füller-Erkennung) |
| `Status` | `ProgramSlotStatus` | `Scheduled` (Standard) oder `Displaced` (verdrängt, nur Historie) |
| `CreatedAt` | `DateTime` | Anlagezeitpunkt |

Konfiguration: `VideoWebPlayer/Data/Configurations/ProgramSlotConfiguration.cs`.
DbSet: `ApplicationDbContext.ProgramSlots`.

## `TVChannel`

Datei: `VideoWebPlayer/Data/TVChannel.cs`

| Eigenschaft | Typ | Beschreibung / Zweck |
|-------------|-----|----------------------|
| `Id` | `long` | Primärschlüssel |
| `Name` | `string` | Sendername (max. 255) |
| `CreatedAt`/`UpdatedAt` | `DateTime` | Zeitstempel |
| `ProgramRules` | `ICollection<ProgramRule>` | Regeln des Senders |

Modell ist kanalfähig, ausgeliefert wird genau ein Sender (`IProgramRuleService.GetOrCreateChannelAsync`
legt ihn bei Bedarf an).

## `Movie` / `TVShowEpisode` / `TVShowSeason` / `TVShow`

Dateien: `VideoWebPlayer/Data/Movie.cs`, `TVShowEpisode.cs`, `TVShowSeason.cs`, `TVShow.cs`

Für die Titelauflösung relevant (Muster aus `TVSenderAdmin.razor`):

- `Movie.Name` — Filmtitel (`Db.Movies`).
- `TVShowEpisode.Name`, `TVShowEpisode.Number`, Navigation `TVShowSeason` → `TVShow`;
  Anzeigeformat der Admin-Ansicht: `$"{ShowName} — {SeasonName}, E{Number}: {EpisodeName}"`
  (`Db.TVShowEpisodes` inkl. `TVShowSeason.TVShow`).

## `ApplicationDbContext`

Datei: `VideoWebPlayer/Data/ApplicationDbContext.cs`

Relevante DbSets: `ProgramSlots`, `TVChannels`, `ProgramRules`, `Movies`, `TVShowEpisodes`,
`TVShowSeasons`, `TVShows`. Event-Hook-Infrastruktur (`EventManager`) markiert Medienänderungen
für den Sendeplan (Schritt 4).

Keine Schema-Änderung nötig: alle für die Programmauskunft benötigten Daten stehen bereits in
`ProgramSlot` + Inhaltstabellen.
