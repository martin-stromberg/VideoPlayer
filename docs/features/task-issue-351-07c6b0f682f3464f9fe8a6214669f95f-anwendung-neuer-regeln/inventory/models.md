# Datenmodell

## `ProgramRule`

Datei: `VideoWebPlayer/Data/ProgramRule.cs`

| Eigenschaft | Typ | Beschreibung / Zweck |
|-------------|-----|----------------------|
| `Id` | `long` | Regel-Id; relevant als Tie-Break in `Evicts` (kleinere Id gewinnt bei gleicher `Priority`) — neue Regeln tragen immer die höchste Id |
| `TVChannelId` / `TVChannel` | `long` / `TVChannel` | Besitzender Kanal (FK) |
| `Name` | `string` | Regelname (`NameMaxLength = 255`) |
| `RuleType` | `ProgramRuleType` | Regelart (Serien-Slot, Primetime-Film, Genre-Rotation, Wiederholung, Feiertag, Filler) |
| `Recurrence` | `ProgramRuleRecurrence` | Wiederholungsmuster (täglich, wöchentlich, einmalig, Feiertag, Werktag, Wochenende) |
| `DayOfWeek` | `DayOfWeek?` | Wochentag bei `Weekly` |
| `SingleDate` | `DateOnly?` | Einzelsdatum bei `Once` |
| `TimeOfDay` | `TimeOnly` | Nominaler lokaler Sendezeitpunkt der Occurrence (`BuildOccurrences`) |
| `EpisodeCount` | `int?` | Episoden je Ausstrahlung (alternativ zu `SlotDuration`; mind. eines von beiden) |
| `SlotDuration` | `TimeSpan?` | Soll-Länge des Sendeplatzes; nominale Fensterlänge für `WindowLostToTakeover` und `UnwrittenHeadroom` |
| `MaterialType` / `MaterialId` | `ProgramRuleMaterialType` / `long` | Polymorphe Materialreferenz (weich, kein FK): Serie, Film, Filmsammlung, Genre, Playlist, Trailer, Clip-Quelle |
| `Genres` | `ICollection<ProgramRuleGenre>` | Genre-Auswahl bei Genre-Material |
| `MediaSourceFilters` | `ICollection<ProgramRuleMediaSource>` | Quellenfilter bei Genre-Material (leer = alle) |
| `ClipGroups` | `ICollection<ProgramRuleClipGroup>` | Zugeordnete Clip-Gruppen (Platzhalter-Auffüllung) |
| `PlaceholderGrace` | `TimeSpan` | Karenzzeit: Rest < Grace → keine weiteren Titel, Rest geht an Clip-Füllung (Default 15 min) |
| `Priority` | `int` | Konfliktauflösung zwischen Regeln (höher gewinnt); Basis von `Evicts`, `outranked`-Verschiebung und harten Bounds |
| `IsActive` | `bool` | Aktiv-Flag; inaktive Regeln erzeugen keine Occurrences, ihre Airings werden verdrängt |
| `ValidityMode` + `ValidFrom`/`ValidTo`/`ValidFromDay`/`ValidToDay`/`ValidWeekdays`/`ValidHolidayAnchor`/`ValidHolidayBeforeDays`/`ValidHolidayAfterDays` | `ProgramRuleValidityMode` + diverse | Gültigkeitsfenster der Occurrence-Erzeugung (`ProgramRuleValidity.IsValidOn`) |
| `CreatedAt` / `UpdatedAt` | `DateTime` | Zeitstempel |

## `ProgramSlot`

Datei: `VideoWebPlayer/Data/ProgramSlot.cs`

| Eigenschaft | Typ | Beschreibung / Zweck |
|-------------|-----|----------------------|
| `Id` | `long` | Slot-Id |
| `TVChannelId` / `TVChannel` | `long` / `TVChannel` | Besitzender Kanal (FK) |
| `Start` / `End` | `DateTime` | UTC-Sendezeitraum (`End` = `Start` + Laufzeit des MediaItems); Intervalle der Blockade-/Verschiebe-Logik |
| `MediaItemId` | `long` | Referenziertes MediaItem (weiche Referenz, kein FK) |
| `MovieId` / `TVShowEpisodeId` | `long?` | Inhaltsreferenz (weich); Grundlage der Inhaltsauflösung in `FindItemIndex`/`LocateInStrands` und der Film-Ausstrahlungskarte (`airingsByMovie`) |
| `ProgramRuleId` | `long?` | Erzeugende Regel (weich, `null` nach Regel-Löschung) |
| `RuleType` | `ProgramRuleType` | Denormalisierter Snapshot der Regelart — identifiziert Filler-Slots auch nach Regel-Löschung (`Evicts`: Filler verliert immer) |
| `Status` | `ProgramSlotStatus` | `Scheduled` (belegt Sendezeit) / `Displaced` (Historie + Verschiebe-Evidenz; blockiert nichts) |
| `IsClipFill` | `bool` | Denormalisierte Markierung von Clip-Füll-Slots (einzige Erzeugungsstelle: `FillAiringSegments`); Clip-Slots tragen keine eigene FSK — `SlotMinAge` liefert `null` |
| `CreatedAt` | `DateTime` | Zeitstempel |

## `ProgramRuleCursor`

Datei: `VideoWebPlayer/Data/ProgramRuleCursor.cs`

| Eigenschaft | Typ | Beschreibung / Zweck |
|-------------|-----|----------------------|
| `Id` | `long` | Cursor-Id |
| `ProgramRuleId` / `ProgramRule` | `long` / `ProgramRule` | **Echte** FK-Kaskade — Regel-Löschung verwirft den Fortsetzungsstand |
| `StrandKey` | `string?` | Strang-Schlüssel: `null` = Gesamt-Cursor (nicht-interleaved), sonst `show:{id}`, `collection:{id}`, `clipgroup:{id}`, `other`; gefilterter Unique-Index erzwingt max. eine `null`-Zeile je Regel |
| `LastContentType` | `ProgramCursorContentType` | Art der `LastContentId` (Movie, TVShowEpisode, PlaylistEntry, Trailer, MediaItem) |
| `LastContentId` | `long` | Zuletzt eingeplantes Element des Strangs; `NextIndexFor` leitet die Ringposition daraus ab; `RewindRing` setzt ihn auf den Vorgänger des frühesten verdrängten Inhalts |
| `UpdatedAt` | `DateTime` | Zeitstempel (Tie-Break der Cursor-Wahl `SelectStrandCursor`) |

## `Movie` / `MovieCollection` (relevante Felder)

Dateien: `VideoWebPlayer/Data/Movie.cs`, `VideoWebPlayer/Data/MovieCollection.cs`

| Eigenschaft | Typ | Beschreibung / Zweck |
|-------------|-----|----------------------|
| `Movie.MovieCollectionId` | `long?` | Sammlungszugehörigkeit → `StrandKey = "collection:{id}"` und `Sequenced = true` in `TryMovieItem` (geordneter Strang in Releasedatum-Reihenfolge) |
| `Movie.ReleaseDate` / `PremieredAt` | `DateTime?` (geerbt von `MediaBaseEntry`) | `ReleaseDate ?? PremieredAt` bestimmt die Sendereihenfolge der Sammlung (`ExpandMovieCollectionAsync`) |
| `Movie.AgeRatingMinAge` | `int?` | FSK-Minimum für die Altersfenster-Prüfung |

`MovieCollection` ist ein `MediaBaseEntry` mit `ICollection<Movie> Movies`.

## `Setup` (programmplan-relevante Felder)

Datei: `VideoWebPlayer/Data/Setup.cs` (Zugriff über `ProgramSettingsService`)

| Eigenschaft | Typ | Beschreibung / Zweck |
|-------------|-----|----------------------|
| `TvSenderEnabled` | `bool` | Feature-Flag: der Worker führt `ExecutePassAsync` nur innerhalb dieses Blocks aus |
| `ProgramScheduleHorizonHours` | `int` | Planungshorizont (Default 48 h; Produktionsszenario 7 Tage) |
| `ProgramScheduleIntervalMinutes` | `int` | Worker-Intervall (Default 15 min) |
| `ProgramScheduleContentScope` | `ProgramScheduleContentScope` | `SharedOnly` (nur für alle Nicht-Admins freigegebene Inhalte) vs. `AllContent` |
| `ProgramScheduleUnknownRatingMode` + `ProgramScheduleAgeWindow{12,16,18}Local` + `ProgramScheduleAgeWindowNightEndLocal` | Enum + `TimeOnly` | Jugendschutz-Fenster (`ProgramScheduleAgeWindowRule`) |
