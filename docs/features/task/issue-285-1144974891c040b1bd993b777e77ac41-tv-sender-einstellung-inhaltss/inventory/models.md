# Datenmodell — Bestandsaufnahme

Betroffene Modellklassen zur Anforderung „Inhaltsspektrum" (senderweite Einstellung, die die
Zuschauer-Freigabeprüfung der Sendeplanung optional aufhebt).

## `Setup`

Datei: `VideoWebPlayer/Data/Setup.cs`

Ein-Zeilen-Tabelle `Setups` (Einzeilen-Zwang über Unique-Index `UX_Setups_SingleRow`,
Migration `20261025073905_AddSetupsSingleRowIndex`, Spalten-Defaults in den
Property-Initialisierern). Hier liegen bereits alle senderweiten Admin-Einstellungen.

| Eigenschaft | Typ | Beschreibung / Zweck |
|-------------|-----|----------------------|
| `Id` | `int` | Primärschlüssel |
| `DataVersion` | `int` | Datenversion der Anwendung |
| `GenresChanged` | `bool` | Invalidierungsflag „Genres geändert" |
| `ApplicationTitle` | `string` | Anwendungstitel, Default `"Martins Videosammlung"` |
| `ScanProcessIntervalMinutes` | `int` | Scan-Intervall, Default 60 |
| `MediaCollectionScanIntervalDays` | `int` | Re-Scan-Intervall, Default 7 |
| `ContinueWatchingEndThresholdSeconds` | `int` | Weiterschauen-Schwelle, Default 30 |
| `ActorCollectionThresholdPercent` | `int` | Sammlungs-Schwelle Schauspielerseite, Default 50 |
| `MdnsAdvertisementEnabled` | `bool` | mDNS-Admin-Schalter, Default `true` |
| `DiscoveryPublicBaseUrl` | `string?` | Admin-Override der öffentlichen Basis-URL |
| `PlaylistBackfillLastSweepAt` | `DateTime?` | Zeitpunkt des letzten Backfill-Sweeps |
| `TvSenderEnabled` | `bool` | Feature-Flag TV-Sender, Default `false` |
| `HolidayFederalState` | `string?` | Bundesland für Feiertagsregeln, `null` = bundesweit |
| `ProgramScheduleHorizonHours` | `int` | Planungshorizont, Default 48 |
| `ProgramScheduleIntervalMinutes` | `int` | Generierungsintervall, Default 15 |
| `LiveStreamSegmentLengthSeconds` | `int` | HLS-Segmentlänge, Default 6 |
| `LiveStreamSegmentDirectory` | `string?` | Segment-Verzeichnis, `null` = Standard |
| `LiveStreamSegmentRetentionMinutes` | `int` | Segment-Aufbewahrung, Default 30 |
| `LiveStreamStandbyMediaItemId` | `long?` | Standby-Clip (weiche Referenz, kein FK), `null` = eingebetteter Default |

**Keine** Eigenschaft für ein Inhaltsspektrum/eine Freigabe-Aufhebung vorhanden — die
Anforderung verlangt eine neue Spalte samt EF-Migration.

## `TVChannel`

Datei: `VideoWebPlayer/Data/TVChannel.cs`

| Eigenschaft | Typ | Beschreibung / Zweck |
|-------------|-----|----------------------|
| `Id` | `long` | Primärschlüssel |
| `Name` | `string` | Sendername (`NameMaxLength = 255`, `required`) |
| `CreatedAt` / `UpdatedAt` | `DateTime` | Zeitstempel |
| `ProgramRules` | `ICollection<ProgramRule>` | Regeln des Senders |

Kanalfähiges Modell, ausgeliefert wird genau ein Sender (`GetOrCreateChannelAsync`).
Enthält keine Einstellungs-Spalten — die Alternative zu `Setup` für die neue Einstellung
(offene Frage der Anforderung).

## `ProgramRule`

Datei: `VideoWebPlayer/Data/ProgramRule.cs`

| Eigenschaft | Typ | Beschreibung / Zweck |
|-------------|-----|----------------------|
| `Id` | `long` | Primärschlüssel |
| `TVChannelId` / `TVChannel` | `long` / `TVChannel` | Kanal-FK + Navigation |
| `Name` | `string` | Regelname (je Sender eindeutig) |
| `RuleType` | `ProgramRuleType` | Serien-Sendezeit, Primetime, Genre-Rotation, Wiederholung, Feiertags-Special, Füller |
| `Recurrence` | `ProgramRuleRecurrence` | Wiederholungsart |
| `DayOfWeek` / `SingleDate` / `TimeOfDay` | `DayOfWeek?` / `DateOnly?` / `TimeOnly` | Zeitparameter der Wiederholung |
| `EpisodeCount` | `int?` | Anzahl Folgen je Ausstrahlung |
| `SlotDuration` | `TimeSpan?` | Sendeplatz-Länge (Voraussetzung für Clip-Gruppen) |
| `MaterialType` / `MaterialId` | `ProgramRuleMaterialType` / `long` | polymorphe Materialreferenz (kein FK) |
| `Genres` / `MediaSourceFilters` / `ClipGroups` | `ICollection<…>` | Genre-Auswahl, Quellfilter, Clip-Gruppen-Zuordnung |
| `PlaceholderGrace` | `TimeSpan` | Karenzzeit für Clip-Auffüllung, Default `DefaultPlaceholderGrace` (15 min) |
| `Priority` | `int` | Konfliktpriorität |
| `IsActive` | `bool` | Regel aktiv |
| `CreatedAt` / `UpdatedAt` | `DateTime` | Zeitstempel |

Die Regel kennt keine Freigabe-Option — die Anforderung beschreibt die Einstellung
bewusst als global („Per-Regel-Override" ist offene Frage, nicht Teil der Anforderung).

## `ProgramSlot`

Datei: `VideoWebPlayer/Data/ProgramSlot.cs`

| Eigenschaft | Typ | Beschreibung / Zweck |
|-------------|-----|----------------------|
| `Id` | `long` | Primärschlüssel |
| `TVChannelId` / `TVChannel` | `long` / `TVChannel` | Kanal-FK + Navigation |
| `Start` / `End` | `DateTime` | Sendefenster (UTC) |
| `MediaItemId` | `long` | weiche Referenz auf das ausgestrahlte `MediaItem` |
| `MovieId` / `TVShowEpisodeId` | `long?` | optionale Inhaltszuordnung (Titel-Slots) |
| `ProgramRuleId` | `long?` | erzeugende Regel |
| `RuleType` | `ProgramRuleType` | Denormalisierte Regelart |
| `Status` | `ProgramSlotStatus` | `Scheduled` oder `Displaced` |
| `CreatedAt` | `DateTime` | Zeitstempel |

## Freigabe-Grundlagen des `AccessContext`

Die Zuschauer-Freigabe, die `AccessContext.Allows` bündelt, steht auf zwei Tabellen:

- `MediaSourceUser` (`VideoWebPlayer/Data/MediaSourceUser.cs`): `MediaSourceId` + `UserId` —
  regulärer Medienquellenzugriff je Benutzer.
- `UnlockedMediaEntry` (`VideoWebPlayer/Data/UnlockedMediaEntry.cs`): `UserId` +
  `MovieCollectionId?`/`TVShowId?` — Einzelfreischaltung einer Filmsammlung bzw. Serie.

Getrennt davon steuert `TvSenderUserAccess` (`VideoWebPlayer/Data/TvSenderUserAccess.cs`:
`UserId` als PK) die Sichtbarkeit des Senders selbst (Navigation, `/live-tv`,
`/api/program-guide`, `/api/live-stream`) — ausgewertet über
`ProgramSettingsService.IsTvSenderAccessGrantedAsync`. Dieser Stream-/Seiten-Zugriff hängt
**nicht** am `AccessContext` der Planung; die Anforderung ändert nur die Planung.
