# Enums

## `AiringInvalidReason`

Datei: `VideoWebPlayer/Services/AiringInvalidReason.cs` (internal, stage-geordnet — spätere Prüfstufe = höherer Wert; nur Diagnose/Logging).

| Wert | Bedeutung |
|------|-----------|
| `None` | Keine Ablehnung — Ausstrahlung gültig. |
| `RuleMissing` | Erzeugende Regel existiert nicht mehr. |
| `RuleInactive` | Regel existiert, ist aber inaktiv. |
| `NoOccurrenceAnchor` | Kein Occurrence-Start der Regel im geladenen Fenster als Anker. |
| `OutsideOccurrenceWindow` | Nur für die Stufenordnung erhalten (entfernte Re-Simulation); wird nicht mehr produziert. |
| `ClipChainBroken` | Nur für die Stufenordnung erhalten; Gruppen sind lückenlos per Konstruktion. |
| `TitlelessAiring` | Ausstrahlung ohne einzigen Titel-Slot (nur Clips). |
| `ItemNotInExpansion` | Slot-Inhalt nicht (mehr) in der Expansion der Regel. |
| `StrandSequenceBroken` | Ring-Kontiguität eines geordneten Strangs innerhalb der Gruppe gebrochen — Nebenbefund der Anforderung: vom alten Flachring-Algorithmus geschriebene Reihenfolgen können hiermit verworfen worden sein. |
| `AnchorWithoutInvaderEvidence` | Verschobener Start ohne beweisbare Invader-Kette, außerhalb des Headroom. |
| `HeadPickInconsistent` | Nur für die Stufenordnung erhalten (kein Pick-Hypothesen-Check mehr). |
| `SlotSequenceMismatch` | Laufzeit oder Altersfenster am geschriebenen Start passt nicht mehr. |
| `RemainderNotReproducible` | Nur für die Stufenordnung erhalten (titelweise Validierung prüft jeden Slot einzeln). |

## `ProgramRuleType`

Datei: `VideoWebPlayer/Data/ProgramRuleType.cs`

| Wert | Bedeutung |
|------|-----------|
| `SeriesFixedTime` | Serien-Festplatz. |
| `MoviePrimeTime` | Primetime-Film. |
| `GenreRotation` | Genre-Rotation (typischer Träger interleaveter Stränge). |
| `Repeat` | Wiederholung. |
| `HolidaySpecial` | Feiertags-Special. |
| `Filler` | Füller-Regel (keine Occurrences; von der Rotation ausgenommen). |

## `ProgramRuleMaterialType`

Datei: `VideoWebPlayer/Data/ProgramRuleMaterialType.cs`

| Wert | Bedeutung |
|------|-----------|
| `TVShow` | Einzelne Serie (ein Strang). |
| `Movie` | Einzelner Film (ein Strang). |
| `MovieCollection` | Filmsammlung (geordneter Flachring). |
| `Genre` | Genre-Union — einziger Materialtyp mit mehreren Strängen (`show:{id}`/`collection:{id}`/`other`). |
| `Playlist` | Benutzer-Playlist (Flachring). |
| `Trailer` | Trailer-Material (expandiert verzögert). |
| `MediaSourceClips` | Clip-Quelle (Füller-Material). |

## `ProgramCursorContentType`

Datei: `VideoWebPlayer/Data/ProgramCursorContentType.cs`

| Wert | Bedeutung |
|------|-----------|
| `Movie` | Film. |
| `TVShowEpisode` | Serienepisode (geordneter Strang). |
| `PlaylistEntry` | Playlist-Eintrag (geordneter Strang). |
| `Trailer` | Trailer-MediaItem. |
| `MediaItem` | Unklassifiziertes MediaItem (Clips). |

## `ProgramSlotStatus`

Datei: `VideoWebPlayer/Data/ProgramSlotStatus.cs`

| Wert | Bedeutung |
|------|-----------|
| `Scheduled` | Belegt Sendezeit im Plan. |
| `Displaced` | Von der Re-Bewertung verdrängt; nur Evidenz/Historie. |

## `ProgramRuleRecurrence`

Datei: `VideoWebPlayer/Data/ProgramRuleRecurrence.cs` (Kontext: `BuildOccurrences`, Z. 1622–1631)

| Wert | Bedeutung |
|------|-----------|
| `Daily` | Täglich. |
| `Weekly` | Wöchentlich an `DayOfWeek`. |
| `Once` | Einmalig an `SingleDate`. |
| `Holiday` | An Feiertagen (`GermanHolidayCalendar`). |
| `Weekday` | Montag–Freitag. |
| `Weekend` | Samstag/Sonntag. |
