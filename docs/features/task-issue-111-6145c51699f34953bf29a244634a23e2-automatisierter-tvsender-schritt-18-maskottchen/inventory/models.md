# Bestandsaufnahme: Datenmodell

## `ProgramRule`
Datei: `VideoWebPlayer/Data/ProgramRule.cs`

| Eigenschaft | Typ | Beschreibung / Zweck |
|-------------|-----|----------------------|
| `RuleType` | `ProgramRuleType` | Regelart (Filler = 5 für Füller-Regeln) |
| `MaterialType` | `ProgramRuleMaterialType` | Art der Materialreferenz |
| `MaterialId` | `long` | Weiche Referenz auf das Material (kein DB-FK, überlebt Löschen) |
| `Genres` | `ICollection<ProgramRuleGenre>` | Mehrfach-Genreauswahl (nur Genre-Material) |
| `MediaSourceFilters` | `ICollection<ProgramRuleMediaSource>` | Quell-Filter (nur Genre-Material) |
| `IsActive`, `Priority`, `EpisodeCount`, `SlotDuration`, `Name` | — | Steuerung der Expansion/Platzierung |

## `ProgramSlot`
Datei: `VideoWebPlayer/Data/ProgramSlot.cs`

| Eigenschaft | Typ | Beschreibung / Zweck |
|-------------|-----|----------------------|
| `MediaItemId` | `long` | Weiche Referenz auf die Videodatei (kein FK) |
| `MovieId` | `long?` | Film-Referenz oder `null` (bereits nullable — rohe Items möglich) |
| `TVShowEpisodeId` | `long?` | Episoden-Referenz oder `null` |
| `ProgramRuleId` | `long?` | Erzeugende Regel (weich) |
| `RuleType` | `ProgramRuleType` | Snapshot — kennzeichnet Füller-Slots |
| `Status` | `ProgramSlotStatus` | `Scheduled`/`Displaced` |
| `Start`, `End`, `CreatedAt`, `TVChannelId` | — | Sendeplatz-Begrenzung |

## `MediaItem`
Datei: `VideoWebPlayer/Data/MediaItem.cs` (Basisklasse `MediaEntry` mit `Name`, `Path`)

| Eigenschaft | Typ | Beschreibung / Zweck |
|-------------|-----|----------------------|
| `MediaCollectionId` | `long` | Ordner-Referenz → `MediaCollection.MediaSourceId` liefert die Quelle |
| `Duration` | `TimeSpan?` | ermittelte Laufzeit (Probe-Ergebnis; `null` = noch nicht/fehlgeschlagen) |
| `DurationProbedAt` | `DateTime?` | Zeitpunkt des letzten Probe-Versuchs |
| `DurationProbeAttempts` | `int` | Fehlversuchszähler (Retry bis `MaxAttempts`) |

## `MediaCollection`
Datei: `VideoWebPlayer/Data/MediaCollection.cs`

| Eigenschaft | Typ | Beschreibung / Zweck |
|-------------|-----|----------------------|
| `MediaSourceId` | `long` | Quell-Referenz |
| `Classifyable` | `bool` | **scanner-intern**: `true`, wenn Scan abgeschlossen + Kinder classifyable — kein Benutzer-Schalter |
| `ParentMediaCollectionId` | `long?` | Baumstruktur |

## `MovieMediaItem`
Datei: `VideoWebPlayer/Data/MovieMediaItem.cs`

| Eigenschaft | Typ | Beschreibung / Zweck |
|-------------|-----|----------------------|
| `MovieId`, `MediaItemId` | `long` | Verknüpfung Film↔Datei |
| `IsTrailer` | `bool` | Trailer-Marker (Schritt 17) — Trailer erzeugen keinen eigenen `Movie`-Satz |

## `MediaSource`
Datei: `VideoWebPlayer/Data/MediaSource.cs` (`MediaEntry` → `Name`)

| Eigenschaft | Typ | Beschreibung / Zweck |
|-------------|-----|----------------------|
| `SourceType` | `MediaSourceType` | `Sftp`/`LocalDirectory` |
| `MediaSourceUsers` | `ICollection<MediaSourceUser>` | Benutzer-Zuordnung — „für alle Nicht-Admins geteilt" = Planvoraussetzung |

## `MediaSourceUser`
Datei: `VideoWebPlayer/Data/MediaSourceUser.cs` — Zuordnung Benutzer↔Quelle; Grundlage von
`AccessContext.FullySharedSourceIds` in `ProgramScheduleService`.
