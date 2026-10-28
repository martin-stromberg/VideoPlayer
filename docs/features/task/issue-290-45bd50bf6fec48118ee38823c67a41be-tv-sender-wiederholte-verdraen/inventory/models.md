# Datenmodell — Bestandsaufnahme (Issue #290)

## `ProgramSlot`
Datei: `VideoWebPlayer/Data/ProgramSlot.cs`

Ein Sendeplatz des Sendeplans: ein konkreter Inhalt (Film/Episode/Clip/Trailer) von `Start` bis `End` (UTC). Alle Referenzen auf Mediendatei, Inhalt und erzeugende Regel sind bewusst weiche Referenzen ohne DB-Fremdschlüssel — die Planhistorie überlebt das Löschen von Medien und Regeln.

| Eigenschaft | Typ | Beschreibung / Zweck |
|-------------|-----|----------------------|
| `Id` | `long` | Primärschlüssel |
| `TVChannelId` / `TVChannel` | `long` / `TVChannel` | Besitzender Sender (echte FK) |
| `Start` | `DateTime` | UTC-Start der Ausstrahlung |
| `End` | `DateTime` | UTC-Ende (`Start` + Laufzeit des Media-Items) |
| `MediaItemId` | `long` | Referenziertes `MediaItem` — weiche Referenz, kein FK |
| `MovieId` | `long?` | Film-Referenz bei Film-Ausstrahlung, sonst `null` — weiche Referenz; Trailer-Clips tragen die `MovieId` des Films |
| `TVShowEpisodeId` | `long?` | Episoden-Referenz bei Serien-Ausstrahlung — weiche Referenz |
| `ProgramRuleId` | `long?` | Erzeugende `ProgramRule` — weiche Referenz (`null` bleibt nicht stehen: die Id selbst bleibt nach Regel-Löschung erhalten, da kein FK existiert) |
| `RuleType` | `ProgramRuleType` | Denormalisierter Snapshot des Regeltyps — identifiziert Füller-Slots auch nach Regel-Löschung |
| `Status` | `ProgramSlotStatus` | `Scheduled` (belegt Sendezeit) / `Displaced` (durch Neubewertung invalidiert, nur Historie); Default `Scheduled` |
| `IsClipFill` | `bool` | Denormalisierter Snapshot: Slot wurde als Clip-Füllung (`FillAiringSegments`) erzeugt |
| `CreatedAt` | `DateTime` | Erzeugungszeitpunkt (Default `DateTime.UtcNow`) |

Hinweise:
- `EnsureCoverageAsync` betrachtet nur `Status == Scheduled` als Belegung (`ProgramScheduleService.cs:88, 123–124`); `Displaced` blockiert nichts.
- `ReconcileAsync` markiert `Displaced` nur bei `slot.End > coverageStart` — vollständig ausgestrahlte Slots bleiben unangetastet (`ProgramScheduleService.cs:249–255`).
- `GetScheduleAsync` liefert `Scheduled` und `Displaced` gemeinsam (`ProgramScheduleService.cs:400–406`); `DeleteDisplacedSlotsAsync` entfernt `Displaced` per `ExecuteDeleteAsync` (Z. 469–477).

## `ProgramRuleCursor`
Datei: `VideoWebPlayer/Data/ProgramRuleCursor.cs`

Persistierter Fortsetzungsstand einer Regel je Sendestrang — welches Element der expandierten Materialliste zuletzt eingeplant wurde. Echte FK mit Kaskade auf `ProgramRule`.

| Eigenschaft | Typ | Beschreibung / Zweck |
|-------------|-----|----------------------|
| `Id` | `long` | Primärschlüssel |
| `ProgramRuleId` / `ProgramRule` | `long` / `ProgramRule` | Besitzende Regel (Kaskade) |
| `StrandKey` | `string?` | Strang-Schlüssel (`show:{TVShowId}`, `other`, `clipgroup:{groupId}`); `null` = Gesamt-Cursor flacher Regeln; gefilterter Unique-Index erzwingt max. eine `NULL`-Zeile je Regel |
| `LastContentType` | `ProgramCursorContentType` | Art des referenzierten Inhalts |
| `LastContentId` | `long` | Id des zuletzt eingeplanten Inhalts (Film-/Episoden-/Playlist-Eintrags-/MediaItem-Id) |
| `UpdatedAt` | `DateTime` | Letzter Schreibzeitpunkt; `SelectStrandCursor` wählt bei Duplikaten die jüngste Zeile |

Bezug zum Abgleich: `RewindCursor`/`RewindRing` setzt den Cursor auf den Vorgänger des frühesten noch expandierbaren verdrängten Inhalts zurück (`ProgramScheduleService.cs:967–1016`); `PersistCursors` schreibt pro Strang mit `LastPlaced` (Z. 2074–2106); `NextIndexFor` leitet den Ring-Startindex ab (Z. 1578–1585).

## `ProgramRule`
Datei: `VideoWebPlayer/Data/ProgramRule.cs`

Programmregel eines Senders: Wiederholungsmuster, Sendezeit, Länge/Folgenanzahl, Materialreferenz, Priorität.

| Eigenschaft | Typ | Beschreibung / Zweck |
|-------------|-----|----------------------|
| `Id` | `long` | Primärschlüssel |
| `TVChannelId` / `TVChannel` | `long` / `TVChannel` | Besitzender Sender |
| `Name` | `string` | Regelname (≤ `NameMaxLength` = 255) |
| `RuleType` | `ProgramRuleType` | Serien-Sendezeit, Primetime-Film, Genre-Rotation, Wiederholung, Feiertags-Special, Füller |
| `Recurrence` | `ProgramRuleRecurrence` | Wiederholungsmuster |
| `DayOfWeek` | `DayOfWeek?` | Wochentag bei `Weekly` |
| `SingleDate` | `DateOnly?` | Datum bei `Once` |
| `TimeOfDay` | `TimeOnly` | Geplante lokale Sendezeit |
| `EpisodeCount` | `int?` | Folgen je Ausstrahlung (Alternative zu `SlotDuration`; mind. eines muss gesetzt sein) |
| `SlotDuration` | `TimeSpan?` | Periodenlänge (Alternative zu `EpisodeCount`) |
| `MaterialType` / `MaterialId` | `ProgramRuleMaterialType` / `long` | Polymorphe Materialreferenz ohne FK |
| `Genres` | `ICollection<ProgramRuleGenre>` | Genre-Auswahl bei Genre-Material |
| `MediaSourceFilters` | `ICollection<ProgramRuleMediaSource>` | Quell-Filter bei Genre-Material (leer = alle Quellen) |
| `ClipGroups` | `ICollection<ProgramRuleClipGroup>` | Zugeordnete Clip-Gruppen (nur bei `SlotDuration` sinnvoll) |
| `PlaceholderGrace` | `TimeSpan` | Karenzzeit am Periodenende (Default `DefaultPlaceholderGrace` = 15 min) |
| `Priority` | `int` | Konfliktlösung: höher gewinnt, Gleichstand über kleinere `Id` |
| `IsActive` | `bool` | Inaktive Regeln erzeugen keine Occurrences; ihre `Scheduled`-Slots werden invalidiert |
| `CreatedAt` / `UpdatedAt` | `DateTime` | Zeitstempel |

## `TVChannel`
Datei: `VideoWebPlayer/Data/TVChannel.cs`

| Eigenschaft | Typ | Beschreibung / Zweck |
|-------------|-----|----------------------|
| `Id` | `long` | Primärschlüssel |
| `Name` | `string` | Sendername (≤ 255) |
| `ProgramRules` | `ICollection<ProgramRule>` | Regeln des Senders |
| `CreatedAt` / `UpdatedAt` | `DateTime` | Zeitstempel |

## `MediaItem` (nur laufzeitrelevante Felder)
Datei: `VideoWebPlayer/Data/MediaItem.cs`

| Eigenschaft | Typ | Beschreibung / Zweck |
|-------------|-----|----------------------|
| `Duration` | `TimeSpan?` | Gemessene Laufzeit; `null` = nicht planbar (Z. 23) |
| `DurationProbedAt` | `DateTime?` | Zeitpunkt des letzten Probe-Versuchs; `null` mit `Duration == null` = noch nie probiert (Z. 31) |
| `DurationProbeAttempts` | `int` | Bisherige Versuche; ≥ `MediaProbeOptions.MaxAttempts` = dauerhaft gescheitert (Z. 38) |

Bezug zum Bug: `MovieMediaItemsAsync`/`EpisodeMediaItemsAsync` wählen je Inhalt den Repräsentanten mit `OrderBy(l.Duration is null ? 1 : 0).ThenBy(l.MediaItemId)` (`ProgramScheduleService.cs:2150–2154, 2163–2167`) — sobald einem zweiten verlinkten Item erstmals eine `Duration` zugewiesen wird, kann das gewählte `MediaItemId` wechseln.

## `Setup` (sendeplanrelevante Spalten)
Datei: `VideoWebPlayer/Data/Setup.cs` (Auswertung in `VideoWebPlayer/Services/ProgramSettingsService.cs`)

| Eigenschaft | Typ | Beschreibung / Zweck |
|-------------|-----|----------------------|
| `TvSenderEnabled` | `bool` | Feature-Flag; Worker prüfen es je Tick (`ProgramScheduleWorker.cs:63`, `MediaDurationProbeWorker.cs:65`) |
| `ProgramScheduleHorizonHours` | `int` | Planungshorizont, Fallback 48 h (`GetProgramScheduleHorizonAsync`, Z. 345–350) |
| `ProgramScheduleIntervalMinutes` | `int` | Worker-Takt, Fallback 15 min (`GetProgramScheduleIntervalAsync`, Z. 357–362) |
| `ProgramScheduleContentScope` | `int` (Enum) | `SharedOnly`/`AllContent` — Zuschauer-Freigabeprüfung der Expansion (Z. 392–398) |
| `ProgramScheduleAgeWindow{12,16,18}Local` / `…NightEndLocal` | `TimeOnly` | Jugendschutzfenster-Schema (Z. 426–435) |
| `ProgramScheduleUnknownRatingMode` | `int` (Enum) | Verhalten für Material ohne bekannte Freigabe (Z. 469–475) |
| `HolidayFederalState` | `string?` | Bundesland für `Holiday`-Regeln (Z. 317) |

## Clip-Gruppen-Tabellen
Dateien: `VideoWebPlayer/Data/ProgramClipGroup.cs`, `ProgramClipGroupItem.cs`, `ProgramRuleClipGroup.cs`

- `ProgramClipGroup`: benannte Clip-Gruppe (`Id`, `Name` ≤ 255, `IsSystem`); reservierte System-Gruppe „Trailer" (`TrailerGroupId = 1`, `TrailerGroupName = "Trailer"`, per Migration geseedet).
- `ProgramClipGroupItem`: Composite-Join `ProgramClipGroupId` + `MediaItemId` (beide FKs kaskadieren).
- `ProgramRuleClipGroup`: Composite-Join `ProgramRuleId` + `ProgramClipGroupId` (beide FKs kaskadieren).

## `Genre` (nur expansionsrelevante Felder)
Datei: `VideoWebPlayer/Data/Genre.cs`

Von `GenreCanonicalResolver`/`ExpandGenreAsync` gelesen: `Name`, `MediaSourceId`, `IsHidden`, `StartDate`/`EndDate` (jährlich wiederkehrendes saisonales Fenster), `AlternateNames` (Weiterleitung auf kanonische Namen).

## Backup-Registrierung
Datei: `VideoWebPlayer/Services/Backups/VideoWebPlayerBackupData.cs`

- `OptionalRestoreTables` enthält `TVChannels`, `ProgramRules`, `ProgramSlots`, `ProgramRuleCursors`, `ProgramRuleGenres`, `ProgramRuleMediaSources`, `TvSenderUserAccess`, `ProgramClipGroups`, `ProgramClipGroupItems`, `ProgramRuleClipGroups` (Z. 39–48).
- `OptionalRestoreColumns` enthält u. a. `ProgramSlots.Status`, `ProgramSlots.IsClipFill`, `ProgramRuleCursors.StrandKey`, `ProgramRules.PlaceholderGrace` sowie alle `Setups.ProgramSchedule*`-Spalten (Z. 61–87).
- Restore-Defaults: `ProgramSlots.Status → Scheduled` (`OptionalRestoreIntDefaults`, Z. 159), `ProgramSlots.IsClipFill → false` (`OptionalRestoreBoolDefaults`, Z. 143), `ProgramRules.PlaceholderGrace → 15 min` (`OptionalRestoreTimeSpanDefaults`).
- Restore-Dedup von `ProgramRuleCursors` mit `StrandKey NULL`: jüngste Zeile je `ProgramRuleId` gewinnt (Z. 499–548) — dieselbe Gewinnerlogik wie `SelectStrandCursor`.
