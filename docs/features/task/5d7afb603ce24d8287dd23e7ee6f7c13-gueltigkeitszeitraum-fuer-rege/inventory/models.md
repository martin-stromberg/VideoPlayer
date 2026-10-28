# Datenmodell — Bestandsaufnahme

## `ProgramRule`
Datei: `VideoWebPlayer/Data/ProgramRule.cs`; EF-Config: `VideoWebPlayer/Data/Configurations/ProgramRuleConfiguration.cs`; DbSet: `ApplicationDbContext.ProgramRules` (Zeile 204)

| Eigenschaft | Typ | Beschreibung / Zweck |
|-------------|-----|----------------------|
| `Id` | `long` | PK |
| `TVChannelId` / `TVChannel` | `long` / `TVChannel` | FK auf den Sender, `DeleteBehavior.Cascade` |
| `Name` | `string` (≤ `NameMaxLength` = 255, NOCASE) | Regelname; Unique-Index `(TVChannelId, Name)` |
| `RuleType` | `ProgramRuleType` | Regelart (required) |
| `Recurrence` | `ProgramRuleRecurrence` | Sende-Rhythmus (required): `Daily`, `Weekly`, `Once`, `Holiday`, `Weekday`, `Weekend` |
| `DayOfWeek` | `DayOfWeek?` | Pflicht nur bei `Recurrence == Weekly` (Dienst-Validierung; in DB optional) |
| `SingleDate` | `DateOnly?` | Einmal-Datum; Pflicht nur bei `Recurrence == Once` (Dienst-Validierung; in DB optional) |
| `TimeOfDay` | `TimeOnly` | Sendezeit, required |
| `EpisodeCount` / `SlotDuration` | `int?` / `TimeSpan?` | Umfang der Ausstrahlung; mindestens eines Pflicht (Dienst-Validierung) |
| `MaterialType` | `ProgramRuleMaterialType` | Materialart, required |
| `MaterialId` | `long` | Material-Referenz, required, **bewusst kein harter FK** (polymorph wie `PlaylistEntry.MediaType`/`MediaId`); bei `Genre` erste gewählte Genre-Id |
| `Genres` | `ICollection<ProgramRuleGenre>` | Genre-Mehrfachauswahl (nur `MaterialType.Genre`) |
| `MediaSourceFilters` | `ICollection<ProgramRuleMediaSource>` | Quell-Filter für Genre-Material; leer = alle Quellen |
| `ClipGroups` | `ICollection<ProgramRuleClipGroup>` | Zugeordnete Clip-Gruppen (Restzeit-Auffüllung) |
| `PlaceholderGrace` | `TimeSpan` | Karenzzeit; Default `DefaultPlaceholderGrace` = 15 min (Konstante `ProgramRule.DefaultPlaceholderGrace`) |
| `Priority` | `int` | Konfliktlösung (höher gewinnt), Default 0 |
| `IsActive` | `bool` | Aktiv-Flag, Default `true`; derzeit **einzige** Schaltstelle, ob eine Regel Sendeplätze erzeugt |
| `CreatedAt` / `UpdatedAt` | `DateTime` | Zeitstempel |

**Für die Anforderung relevant:** Es existieren **keine** Gültigkeitsfelder (`ValidityMode`, `ValidFrom`, `ValidTo` o. ä.). Die EF-Config (`ProgramRuleConfiguration`, Zeilen 21–30) setzt Defaults nur für `Priority`, `PlaceholderGrace`, `IsActive`; neue Spalten bräuchten dort analoge Defaults plus EF-Migration unter `VideoWebPlayer/Migrations/` (bisher letzte: `20261027130009_AddProgramScheduleAgeWindows`).

## `TVChannel`
Datei: `VideoWebPlayer/Data/TVChannel.cs`

| Eigenschaft | Typ | Beschreibung / Zweck |
|-------------|-----|----------------------|
| `Id` | `long` | PK |
| `Name` | `string` (≤255) | Sendername; aktuell genau ein Sender (`GetOrCreateChannelAsync` legt „TV-Sender" an) |
| `ProgramRules` | `ICollection<ProgramRule>` | Regeln des Senders (Kaskade) |
| `CreatedAt` / `UpdatedAt` | `DateTime` | Zeitstempel |

## `ProgramRuleCursor`
Datei: `VideoWebPlayer/Data/ProgramRuleCursor.cs`; EF-Config: `ProgramRuleCursorConfiguration.cs`

| Eigenschaft | Typ | Beschreibung / Zweck |
|-------------|-----|----------------------|
| `Id` | `long` | PK |
| `ProgramRuleId` / `ProgramRule` | `long` / `ProgramRule` | **Echter FK mit `DeleteBehavior.Cascade`** — Löschen einer Regel verwirkt deren Fortsetzungsstand (relevant für die automatische Löschung abgelaufener Regeln) |
| `StrandKey` | `string?` | Sendestrang-Schlüssel (`show:{id}`, `other`, `clipgroup:{id}` bzw. `null` für den Gesamt-Cursor); gefilterter Unique-Index auf `(ProgramRuleId, StrandKey)` |
| `LastContentType` / `LastContentId` | `ProgramCursorContentType` / `long` | Zuletzt gesendetes Element der expandierten Materialliste |
| `UpdatedAt` | `DateTime` | Zeitstempel |

## `ProgramSlot`
Datei: `VideoWebPlayer/Data/ProgramSlot.cs`; EF-Config: `ProgramSlotConfiguration.cs`

| Eigenschaft | Typ | Beschreibung / Zweck |
|-------------|-----|----------------------|
| `Id` | `long` | PK |
| `TVChannelId` / `TVChannel` | `long` / `TVChannel` | FK auf den Sender, Kaskade |
| `Start` / `End` | `DateTime` (UTC, `UtcOnRead`-Konverter) | Sendefenster |
| `MediaItemId` | `long` | **Soft-Referenz** ohne FK |
| `MovieId` / `TVShowEpisodeId` | `long?` | **Soft-Referenzen** ohne FK |
| `ProgramRuleId` | `long?` | **Soft-Referenz ohne FK** — ein gelöschter Regelbezug kaskadiert nicht; Sendeplätze bleiben als Historie erhalten. Der nächste `ReconcileAsync`-Lauf markiert Slots einer nicht mehr existierenden (oder inaktiven) Regel als `Displaced` (`ProgramScheduleService` Zeilen 289, 1117) |
| `RuleType` | `ProgramRuleType` | Denormalisierte Kopie der Regelart (Filler-Erkennung nach Regel-Löschung) |
| `Status` | `ProgramSlotStatus` | `Scheduled` (Default) / `Displaced` |
| `IsClipFill` | `bool` | Clip-Füllung-Snapshot |
| `CreatedAt` | `DateTime` | Zeitstempel |

## `Genre` (saisonales Referenzmodell für das jährliche Fenster)
Datei: `VideoWebPlayer/Data/Genre.cs` (**Achtung: Datei ist Windows-1252-codiert, nicht UTF-8** — enthält Byte `0xfc` („ü") in einem Kommentar)

| Eigenschaft | Typ | Beschreibung / Zweck |
|-------------|-----|----------------------|
| `StartDate` / `EndDate` | `DateTime?` | Optionales saisonales Sichtbarkeitsfenster — beide gesetzt = jährlich wiederkehrendes, inklusives Fenster (`SeasonalGenreWindow.GetExpiry`, `GenreCanonicalResolver.IsVisible`); nur eine Grenze = unbegrenzt. `null` = immer sichtbar |
| `IsHidden` | `bool` | Ausgeblendet-Flag |
| `AlternateNames` | `ICollection<GenreName>` | Umleitung auf kanonische Namen |

Das Genre-Modell speichert volle `DateTime`-Werte und rollt das komplette Fenster per `AddYears(1)` ins laufende Jahr — ein über Silvester reichendes Fenster (z. B. 20.12.–06.01.) ist so abbildbar und getestet (`SeasonalHomeServiceTests.Window_HandlesYearCrossingMissingAndInvalidBoundaries`).

## `ProgramRuleGenre` / `ProgramRuleMediaSource` / `ProgramRuleClipGroup`
Dateien: `VideoWebPlayer/Data/ProgramRuleGenre.cs`, `ProgramRuleMediaSource.cs`, `ProgramRuleClipGroup.cs`

Join-Entitäten der Regel (Composite-PKs); beide FKs jeweils `DeleteBehavior.Cascade` — beim (automatischen) Löschen einer Regel verschwinden ihre Link-Zeilen automatisch.

## `Setup` (Auszug)
Datei: `VideoWebPlayer/Data/Setup.cs` (Zeile 80)

| Eigenschaft | Typ | Beschreibung / Zweck |
|-------------|-----|----------------------|
| `TvSenderEnabled` | `bool` | Feature-Flag: Nur bei `true` ruft `ProgramScheduleWorker` `ReconcileAsync`/`EnsureCoverageAsync` — eine Bereinigung im Worker-Lauf würde bei deaktiviertem Flag pausieren |
| `ProgramScheduleIntervalMinutes` / `ProgramScheduleHorizonHours` / `HolidayFederalState` | `int` / `int` / `string?` | Intervall des Worker-Laufs, Planungshorizont, Feiertags-Bundesland |

## DbSets in `ApplicationDbContext`
`TVChannels` (Z. 200), `ProgramRules` (Z. 204), `ProgramSlots` (Z. 209), `ProgramRuleCursors` (Z. 214), `ProgramRuleGenres` (Z. 219), `ProgramRuleMediaSources` (Z. 224), `TvSenderUserAccess`, `ProgramClipGroups`, `ProgramClipGroupItems`, `ProgramRuleClipGroups` (Z. 244), `Genres`/`GenreNames`. Konfiguration via `ApplyConfigurationsFromAssembly` (Z. 770).

## Backup-Tabellen/-Spalten (`VideoWebPlayerBackupData`)
Datei: `VideoWebPlayer/Services/Backups/VideoWebPlayerBackupData.cs`

- `OptionalRestoreTables` (Z. 23–49): `ProgramRules`, `ProgramSlots`, `ProgramRuleCursors`, `ProgramRuleGenres`, `ProgramRuleMediaSources`, `ProgramRuleClipGroups` u. a. bereits eingetragen.
- `OptionalRestoreColumns` (Z. 51–117): u. a. `ProgramRules.PlaceholderGrace` (Z. 101), `ProgramSlots.Status`, `ProgramSlots.IsClipFill`, `ProgramRuleCursors.StrandKey`.
- Defaults je Spaltentyp: `OptionalRestoreBoolDefaults` (Z. 127), `OptionalRestoreLongDefaults` (Z. 146), `OptionalRestoreIntDefaults` (Z. 151), `OptionalRestoreTimeSpanDefaults` (Z. 167 — enthält `ProgramRules.PlaceholderGrace`), `OptionalRestoreTimeOnlyDefaults` (Z. 172). Neue `ProgramRule`-Spalten müssten hier mit Alt-Backup-Default (uneingeschränkt gültig) eingetragen werden; es gibt keinen `DateOnly`-Defaults-Typ bisher.
