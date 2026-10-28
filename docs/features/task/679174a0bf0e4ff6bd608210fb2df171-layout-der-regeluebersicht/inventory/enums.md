# Enums — Bestandsaufnahme

Alle drei Enums bleiben unverändert; für die Anzeige existieren bereits deutsche
Label-Mapping-Methoden in `TVSenderAdmin.razor` (`RuleTypeLabel` Z. 2205,
`RecurrenceLabel` Z. 2216, `MaterialLabel(type, id)` Z. 2178).

## `ProgramRuleType`
Datei: `VideoWebPlayer/Data/ProgramRuleType.cs`

| Wert | Bedeutung | `RuleTypeLabel`-Text |
|------|-----------|----------------------|
| `SeriesFixedTime = 0` | Serie zur festen Sendezeit | „Serien-Sendezeit" |
| `MoviePrimeTime = 1` | Film zur Primetime | „Film zur Primetime" |
| `GenreRotation = 2` | Rotation durch Genre-/Formatinhalte | „Genre-/Formatrotation" |
| `Repeat = 3` | Wiederholung zuvor gewählten Materials | „Wiederholung" |
| `HolidaySpecial = 4` | Ausstrahlung an Feiertagen | „Feiertags-Special" |
| `Filler = 5` | Füllmaterial für Lücken | „Platzhalter/Füller" |

## `ProgramRuleRecurrence`
Datei: `VideoWebPlayer/Data/ProgramRuleRecurrence.cs`

| Wert | Bedeutung | `RecurrenceLabel`-Text |
|------|-----------|------------------------|
| `Daily = 0` | Täglich | „Täglich" |
| `Weekly = 1` | Wöchentlich am `DayOfWeek` | „Wöchentlich ({Wochentag})" |
| `Once = 2` | Einmalig am `SingleDate` | „Einmalig ({dd.MM.yyyy})" |
| `Holiday = 3` | An Feiertagen (deutscher Kalender) | „An Feiertagen" |
| `Weekday = 4` | Werktags Mo–Fr | „Werktags (Mo–Fr)" |
| `Weekend = 5` | Sa + So | „Wochenende (Sa+So)" |

## `ProgramRuleMaterialType`
Datei: `VideoWebPlayer/Data/ProgramRuleMaterialType.cs`

| Wert | Bedeutung | `MaterialLabel`-Präfix |
|------|-----------|------------------------|
| `TVShow = 0` | Ganze Serie (`MaterialId` → `TVShows`) | „Serie" |
| `Movie = 1` | Einzelner Film (`MaterialId` → `Movies`) | „Film" |
| `MovieCollection = 2` | Filmsammlung (`MaterialId` → `MovieCollections`) | „Filmsammlung" |
| `Genre = 3` | Genre-Material, Mehrfachauswahl über `ProgramRule.Genres`; `MaterialId` hält das erste gewählte Genre | „Genre" (Listenform: „Genre: a, b — Quellen: …") |
| `Playlist = 4` | Benutzer-Playlist (`MaterialId` → `Playlists`) | „Playlist" |
| `Trailer = 5` | Trailer anstehender Filme; globales Material ohne `MaterialId` | „Trailer anstehender Filme" (Festtext) |
| `MediaSourceClips = 6` | Uneingeordnete Videodateien einer Quelle (`MaterialId` → `MediaSources`) | „Clip-Quelle" |
