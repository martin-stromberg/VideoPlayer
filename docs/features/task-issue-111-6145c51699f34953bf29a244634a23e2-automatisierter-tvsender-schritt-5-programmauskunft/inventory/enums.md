# Bestandsaufnahme: Enums

## `ProgramSlotStatus`

Datei: `VideoWebPlayer/Data/ProgramSlotStatus.cs`

| Wert | Bedeutung |
|------|-----------|
| `Scheduled` | Slot belegt Sendezeit — für die Auskunft relevant |
| `Displaced` | Durch Re-Bewertung verdrängt; nur Planhistorie — darf in der Auskunft nicht als laufend/geplant erscheinen |

## `ProgramRuleType`

Datei: `VideoWebPlayer/Data/ProgramRuleType.cs`

| Wert | Bedeutung |
|------|-----------|
| `SeriesFixedTime` | Serien-Sendezeit |
| `MoviePrimeTime` | Film zur Primetime |
| `GenreRotation` | Genre-/Formatrotation |
| `Rerun` | Wiederholung |
| `HolidaySpecial` | Feiertags-Special |
| `Filler` | Platzhalter/Füller |

Als `ProgramSlot.RuleType`-Snapshot am Slot erhalten — kann in der Auskunft Füller-Slots
kennzeichnen (optional, nicht verlangt).

## `MediaTypeValues`

Datei: `VideoWebPlayer.Client/Models/MediaTypeValues.cs`

String-Konstanten für Medientypen in DTOs (z. B. `"movie"`, `"tvshow"`/`"episode"`-Familie) —
Namenskonvention für den `MediaType` des Auskunfts-DTO. (Konkrete Konstanten beim Implementieren
gegen die Datei prüfen.)
