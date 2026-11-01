# Enums — Bestandsaufnahme Senderplanung

## `ProgramRuleType`

Datei: `VideoWebPlayer/Data/ProgramRuleType.cs`

| Wert | Bedeutung |
|------|-----------|
| `SeriesFixedTime = 0` | Serien-Slot zu fester Zeit |
| `MoviePrimeTime = 1` | Spielfilm zur Primetime |
| `GenreRotation = 2` | Rotation durch Genre-/Formatinhalte — der Szenario-Regeltyp |
| `Repeat = 3` | Wiederholung |
| `HolidaySpecial = 4` | Feiertagssendung |
| `Filler = 5` | Füllmaterial; bildet keine Occurrences, füllt nur Lücken (`FillGaps`) |

## `ProgramRuleMaterialType`

Datei: `VideoWebPlayer/Data/ProgramRuleMaterialType.cs`

| Wert | Bedeutung |
|------|-----------|
| `TVShow = 0` | Ganze Serie |
| `Movie = 1` | Einzelner Film |
| `MovieCollection = 2` | Filmsammlung |
| `Genre = 3` | Genre (kanonisch über Quellen dedupliziert) — Szenario-Material; treibt das Strang-Interleaving in `RuleState.SetItems` |
| `Playlist = 4` | Benutzer-Playlist |
| `Trailer = 5` | „Trailer kommender Filme" — globales Material, expandiert erst nach der Materialisierung (`ExpandTrailerStatesAsync`) |
| `MediaSourceClips = 6` | Unklassifizierte Videos einer Quelle (Clip-Quelle) |

## `ProgramRuleRecurrence`

Datei: `VideoWebPlayer/Data/ProgramRuleRecurrence.cs`

| Wert | Bedeutung |
|------|-----------|
| `Daily = 0` | Täglich — Szenario-Wert |
| `Weekly = 1` | Wöchentlich am `DayOfWeek` |
| `Once = 2` | Einmalig am `SingleDate` |
| `Holiday = 3` | An Feiertagen (`GermanHolidayCalendar`) |
| `Weekday = 4` | Montag–Freitag |
| `Weekend = 5` | Samstag/Sonntag |

## `ProgramRuleValidityMode`

Datei: `VideoWebPlayer/Data/ProgramRuleValidityMode.cs`

| Wert | Bedeutung |
|------|-----------|
| `Unrestricted = 0` | Jeden Tag gültig (Default; Szenario-Wert) |
| `OneTime = 1` | Einmaliger Datumsbereich, Grenzen einseitig offen möglich |
| `AnnualWindow = 2` | Jährlich wiederkehrendes Monat-Tag-Fenster |
| `MonthlyWindow = 3` | Monatlich wiederkehrendes Tag-Fenster |
| `WeeklyWindow = 4` | Wochentags-Bitmask |
| `HolidayWindow = 5` | Fenster um einen Feiertagsanker |

## `ProgramSlotStatus`

Datei: `VideoWebPlayer/Data/ProgramSlotStatus.cs`

| Wert | Bedeutung |
|------|-----------|
| `Scheduled = 0` | Teil des wirksamen Plans; belegt Sendezeit |
| `Displaced = 1` | Vom Reconcile verdrängt; bleibt als Historie/Evidence, blockiert nichts |

## `ProgramCursorContentType`

Datei: `VideoWebPlayer/Data/ProgramCursorContentType.cs`

| Wert | Bedeutung |
|------|-----------|
| `Movie = 0` | Film — Inhalt ungeordneter Ringe (`MixUnorderedRing`) |
| `TVShowEpisode = 1` | Serienepisode — geordneter Strang (`PreservesOrder`) |
| `PlaylistEntry = 2` | Playlist-Eintrag — geordneter Strang |
| `Trailer = 3` | Trailer-MediaItem (Trailer-Ring, weder geordnet noch gemischt) |
| `MediaItem = 4` | Rohes MediaItem (Clip-Quellen, Clip-Gruppen) — ungeordneter Ring |

## `ProgramScheduleUnknownRatingMode`

Datei: `VideoWebPlayer/Data/ProgramScheduleUnknownRatingMode.cs`

Persistiert als `Setup.ProgramScheduleUnknownRatingMode` (int). Wert 0 muss restriktiv
bleiben (Legacy-Backups/korrupte Werte fallen auf restriktiv zurück). Getestet in
Szenario 1.h in beiden Modi.

| Wert | Bedeutung |
|------|-----------|
| `RestrictToNightWindow = 0` | Inhalt ohne bekannte Freigabe nur im FSK-18-Nachtfenster (Default) |
| `Unrestricted = 1` | Inhalt ohne bekannte Freigabe jederzeit planbar |

## `ProgramScheduleContentScope`

Datei: `VideoWebPlayer/Data/ProgramScheduleContentScope.cs`

| Wert | Bedeutung |
|------|-----------|
| `SharedOnly = 0` | Nur für alle Nicht-Admins freigegebene Inhalte (Default) |
| `AllContent = 1` | Alle expandierten Inhalte — Publikumsprüfung entfällt |

## `AiringInvalidReason`

Datei: `VideoWebPlayer/Services/AiringInvalidReason.cs` (`internal`)

Stufig geordnete Diagnose der Reproduzierbarkeitsprüfung — spätere Stufe = höherer
Wert, `ReproContext.NoteFailure` behält das Maximum. Nur Logging/Diagnose.

| Wert | Bedeutung |
|------|-----------|
| `None = 0` | Reproduzierbar |
| `RuleMissing` | Regel existiert nicht mehr |
| `RuleInactive` | Regel ist inaktiv |
| `NoOccurrenceAnchor` | Keine Occurrence im geladenen Fenster als Anker |
| `OutsideOccurrenceWindow` | Rekonstruierter Start liegt vor allen nutzbaren Occurrences |
| `ClipChainBroken` | Slot-Kette der Clip-Ausstrahlung nicht lückenlos |
| `TitlelessAiring` | Ausstrahlung ohne Titel-Slot (nur Clips) |
| `ItemNotInExpansion` | Slot-Inhalt nicht in der aktuellen Expansion der Regel |
| `StrandSequenceBroken` | Strang-Reihenfolge/-Kontiguität verletzt |
| `AnchorWithoutInvaderEvidence` | Verschobener Start ohne Besetzungs-Evidence |
| `HeadPickInconsistent` | Keine Hypothese legt den Kopf-Pick auf die beobachtete Ringposition |
| `SlotSequenceMismatch` | Re-simulierte Pick-Sequenz passt nicht zur Slot-Sequenz |
| `RemainderNotReproducible` | Präfix passte, Rest der verschmolzenen Kette nicht |
