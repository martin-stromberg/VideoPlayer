# Datenmodell

Für die Anforderung relevante Datenmodellklassen des Sendeplans („TV-Sender").

## `ProgramSlot`

Datei: `VideoWebPlayer/Data/ProgramSlot.cs`

Ein materialisierter Sendeplatz — die atomare, gespeicherte Einheit des Plans. Airings sind
Laufzeit-Gruppierungen aus aneinander anschließenden `ProgramSlot`s derselben Regel
(`GroupAirings`), keine eigene Entität.

| Eigenschaft | Typ | Beschreibung / Zweck |
|-------------|-----|----------------------|
| `Id` | `long` | Primärschlüssel |
| `TVChannelId` | `long` | FK auf den Kanal |
| `TVChannel` | `TVChannel` | Navigation zum Kanal |
| `Start` | `DateTime` | UTC-Beginn; ein Slot, der den Coverage-Start überspannt, behält seinen wahren Beginn (`MaterializeOccurrence`, Z. 1143) |
| `End` | `DateTime` | UTC-Ende; `End > coverageStart` entscheidet, ob ein Slot in `Displace` überhaupt verdrängt werden darf |
| `MediaItemId` | `long` | Referenz auf die abgespielte Mediendatei; primärer Schlüssel für `FindItemIndex` |
| `MovieId` | `long?` | Inhaltsbezug Film — Fallback-Schlüssel in `FindItemIndex`, Basis der Trailer-Vorlauf-Auswahl |
| `TVShowEpisodeId` | `long?` | Inhaltsbezug Episode — Fallback-Schlüssel in `FindItemIndex` |
| `ProgramRuleId` | `long?` | Erzeugende Regel (weiche Referenz — bleibt auch nach Regellöschung am Slot) |
| `RuleType` | `ProgramRuleType` | Denormalisierte Regelart; `Filler`-Slots werden in `ReconcileAsync` einzeln statt als Airing geprüft |
| `Status` | `ProgramSlotStatus` | `Scheduled` (Default) oder `Displaced` — verdrängte Slots werden nie gelöscht |
| `CreatedAt` | `DateTime` | Erstellungszeitpunkt |

## `ProgramRule`

Datei: `VideoWebPlayer/Data/ProgramRule.cs`

Die wiederkehrende Senderegel; `ReconcileAsync` prüft jedes Airing gegen die aktuell expandierte
Regel.

| Eigenschaft | Typ | Beschreibung / Zweck |
|-------------|-----|----------------------|
| `Id` | `long` | Primärschlüssel |
| `TVChannelId` | `long` | FK auf den Kanal |
| `Name` | `string` (required) | Anzeigename (auch in Diagnose-Logausgaben) |
| `RuleType` | `ProgramRuleType` | Serienfenster, Primetime-Film, Genre-Rotation, Filler usw. |
| `Recurrence` | `ProgramRuleRecurrence` | Wiederholungsmuster — steuert die Tagesschleife in `BuildOccurrences` |
| `DayOfWeek` | `DayOfWeek?` | Wochentag bei `Recurrence.Weekly` |
| `SingleDate` | `DateOnly?` | Datum bei `Recurrence.Once` |
| `TimeOfDay` | `TimeOnly` | Lokale nominale Startzeit der Occurrence |
| `EpisodeCount` | `int?` | Wunsch-Anzahl Items je Airing (gewinnt gegenüber `SlotDuration`) |
| `SlotDuration` | `TimeSpan?` | Ziel-Dauer des Airing-Fensters, wenn kein `EpisodeCount` gesetzt |
| `MaterialType` | `ProgramRuleMaterialType` | Art der Materialreferenz (`MaterialId`) |
| `MaterialId` | `long` | Referenz auf Serie, Film, Sammlung, Genre, Playlist, Clip-Quelle |
| `Genres` | `ICollection<ProgramRuleGenre>` | Genre-Auswahl (bei `MaterialType.Genre`); leer → Fallback auf `MaterialId` |
| `MediaSourceFilters` | `ICollection<ProgramRuleMediaSource>` | Einschränkung auf Medienquellen; leer = alle |
| `Priority` | `int` | Konfliktauflösung: höher gewinnt; steuert außerdem harte/weiche Bounds in `BoundaryAfter` |
| `IsActive` | `bool` | Inaktive Regeln erzeugen keine Occurrences; ihre Airings werden verdrängt, aber ihr Cursor wird zurückgespult |
| `CreatedAt` | `DateTime` | Erstellungszeitpunkt |
| `UpdatedAt` | `DateTime` | Änderungszeitpunkt |

## `ProgramRuleCursor`

Datei: `VideoWebPlayer/Data/ProgramRuleCursor.cs`

Persistierter Fortsetzungsstand je Regel (und je Strang bei Interleaved-Regeln) — die
Sendereihenfolge überlebt Coverage-Läufe und Verdrängungen (`RewindCursor`).

| Eigenschaft | Typ | Beschreibung / Zweck |
|-------------|-----|----------------------|
| `Id` | `long` | Primärschlüssel |
| `ProgramRuleId` | `long` | FK auf die Regel |
| `ProgramRule` | `ProgramRule` | Navigation |
| `StrandKey` | `string?` | Strang-Schlüssel (`null` = flacher Ring; `"show:{id}"` je Serie, `"other"` für sonstiges Genre-Material) |
| `LastContentType` | `ProgramCursorContentType` | Art des zuletzt platzierten Inhalts |
| `LastContentId` | `long` | Id des zuletzt platzierten Inhalts |
| `UpdatedAt` | `DateTime` | Tiebreaker bei `SelectStrandCursor` (höchste UpdatedAt, dann höchste Id) |
