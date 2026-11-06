# Umsetzungsplan: Sendeplan aus Regeln erzeugen (Schritt 3)

## Übersicht

Aus den aktiven `ProgramRule`-Datensätzen des einen ausgelieferten `TVChannel` wird ein rollierender, lückenlos belegter Sendeplan aus `ProgramSlot`-Datensätzen erzeugt (Horizont konfigurierbar, Standard 48 h) und von einem neuen `ProgramScheduleWorker` im konfigurierten Intervall fortgeschrieben. Neue Persistenz: `ProgramSlots`, `ProgramRuleCursors` (Fortsetzungsstand je Regel) und zwei `Setup`-Spalten (Horizont, Intervall) samt Migration, Backup-Registrierung und Admin-UI-Feldern auf `/admin/tv-sender`.

## Designentscheidungen

| Komponente / Bereich | Gewählter Ansatz | Begründung |
|----------------------|-----------------|------------|
| Sendeplatz-Referenzen | `ProgramSlot` referenziert `MediaItemId`, `MovieId`/`TVShowEpisodeId` und `ProgramRuleId` jeweils **ohne DB-FK** (weiche Referenzen wie `ProgramRule.MaterialId`/`PlaylistEntry.MediaId`), plus denormalisiertem `RuleType`-Snapshot. | Schritt 4 verlangt: gelöschte Videos/Regeln markieren Sendeplätze als „verdrängt" statt sie per Kaskade zu löschen; die Planhistorie muss nachvollziehbar bleiben. |
| Fortsetzungsstand | Eigene Entität `ProgramRuleCursor` je `ProgramRule` mit `LastContentType` (`Movie`/`TVShowEpisode`/`PlaylistEntry`) + `LastContentId`; echte FK-Kaskade auf `ProgramRules` (Regel-Löschung setzt den Fortschritt zurück — fachlich korrekt). | Persistenter Cursor überlebt Neustarts und regenerierte Läufe deterministisch; Ableiten aus Sendeplätzen wäre bei künftiger Verdrängung (Schritt 4) fragil. |
| Material-Expansion | Jede Regel expandiert ihr Material zu einer geordneten Liste abspielbarer Inhalte. Serie → Episoden in Sendedreihenfolge (Staffel nach `Id`, Episode nach `Number`); Filmsammlung → Filme nach `(ReleaseDate ?? PremieredAt, Id)`; Genre → Filme `(ReleaseDate ?? PremieredAt, Name, Id)` gefolgt von Episoden-Blöcken je Serie `(ShowName, ShowId, Staffel-Id, Number)`; Playlist → abspielbare Einträge in Playlist-Reihenfolge (`Manual`: `SortOrder, AddedAt`; sonst Release-Datum wie `SortPlaylistEntriesForModeAsync`); Film → der eine Film. | Deterministische, fachlich erwartbare Reihenfolgen; Staffel-Id-Reihenfolge und Playlist-Sortierung folgen den bestehenden Konventionen (`MediaHierarchyRegistry`, `PlaylistService`). |
| Verwaiste Materialien | Material, dessen `MaterialId` nicht mehr existiert, expandiert zu einer leeren Liste — die Regel erzeugt nichts, wird geloggt, wirft keinen Fehler. | Expliziter Hinweis aus der Abnahme von Schritt 2. |
| Umfang einer Ausstrahlung | `EpisodeCount` gesetzt → genau so viele aufeinanderfolgende Inhalte; sonst `SlotDuration` → Inhalte werden eingeplant, bis die kumulierte Laufzeit die Ziellänge erreicht (mindestens 1, der letzte darf überschreiten). Bei beidem gilt `EpisodeCount`. | Entspricht „Länge bzw. Anzahl der Folgen" aus Schritt 2; eine Sendung ist atomar (kein Cut mitten im Video). |
| Regelart-Semantik | `SeriesFixedTime`, `MoviePrimeTime`, `GenreRotation`, `Repeat`, `HolidaySpecial` unterscheiden sich nur in Material-Whitelist und Wiederholungsmuster — die Ausstrahlungsmechanik (Expansion + Cursor-Rotation) ist identisch; „Wiederholung" entsteht fachlich durch das erneute Durchlaufen derselben Materialliste. | Vermeidet Sonderlogik je Regelart; deckt die Anforderung vollständig ab. |
| Cursor bei erschöpftem Material | Rotation: Nach dem letzten Inhalt beginnt die Liste wieder vorn (der Sender läuft endlos weiter). | Ein fortlaufender Sender darf nicht verstummen; entspricht TV-Rotationslogik. |
| Konfliktauflösung | Ausstrahlungen werden nach `(Priority desc, RuleId asc, Start asc)` platziert; eine Ausstrahlung wird nur platziert, wenn **keiner** ihrer Sendeplätze bereits belegte Bereiche überlappt (persistierte Sendeplätze und bereits gewonnene Ausstrahlungen) — andernfalls entfällt sie komplett und ihr Cursor schreitet nicht fort. | Deterministisch; höhere Priorität gewinnt unabhängig von der zeitlichen Reihenfolge; Teilplatzierung würde Episoden-Folgen zerreißen. |
| Planungsfenster / rollierend | `coverageStart = max(jetzt, letztes belegtes Ende)`; Horizontende = `jetzt + Horizont`. Bestehende Sendeplätze werden in diesem Schritt nie verändert (Nachführung bei Änderungen ist Schritt 4). Ausstrahlungen mit Beginn in `[coverageStart − 24 h, Horizont]` werden berücksichtigt, damit eine gerade laufende Sendung ihren echten `Start` erhält; Sendeplätze, die vor `coverageStart` enden, entfallen. | Extend-only ist neustartfest, idempotent und minimal-invasiv; der 24-h-Rückblick deckt über Mitternacht laufende Sendungen ab. |
| Lückenfüllung | Lücken im Fenster werden Round-Robin über die aktiven `Filler`-Regeln (Reihenfolge `(Priority desc, Id asc)`) gefüllt; je Regel-Cursor wird der nächste Füller-Inhalt nur eingeplant, wenn er vollständig in die Restlücke passt — passt keiner, bleibt die Lücke bestehen und wird geloggt. | Inhalte werden nicht geschnitten; „lückenlos" ist erreicht, sobald mindestens ein passender Füller existiert. |
| „Vollständig freigegeben" | Ein Inhalt ist planbar, wenn **jeder Nicht-Admin-Benutzer** ihn sehen kann: Quellenzugriff (`MediaSourceUser`) für alle ODER Einzelfreischaltung des Freischalt-Ziels (`TVShow` bzw. `MovieCollection`) für alle (`IUnlockedMediaService.IsAccessible`-Semantik). Admins sehen ohnehin alles und zählen nicht als Einschränkung; ohne Nicht-Admin-Benutzer gilt alles als freigegeben. | Serverseitig erzwingbar, konsistent zur Zugriffsregelung des Projektplans; ein Quellenzugriff, der nur einem Teil der Zuschauer offensteht, ist im gemeinsamen Stream nicht darstellbar. |
| Zeitzone | `TimeOfDay` gilt in Server-Ortszeit; `Start`/`End` werden als UTC-`DateTime` persistiert (Konvention `DateTime.UtcNow`). | Weihnachts-Special um 20:15 soll 20:15 Ortszeit laufen — auch an DST-Tagen; UTC-Speicherung entspricht dem Rest des Modells. |
| Konfiguration | `Setup.ProgramScheduleHorizonHours` (Standard 48) und `Setup.ProgramScheduleIntervalMinutes` (Standard 15) über `ProgramSettingsService` mit Min-Validierung (≥1) und Admin-UI-Feldern auf `/admin/tv-sender`. | Etablierter Einstellungsmechanismus (gleiche Karte wie Bundesland); keine Datei-Konfiguration für Betriebsparameter. |
| Feiertagskalender | `GermanHolidayCalendar` als reine statische Berechnung (Computus + feste/landesspezifische Feiertage, `DE-*`-Filter, `null` = bundesweit). | Fest hinterlegter deutscher Kalender ohne externe Datenquelle; trivial testbar. |
| Worker-Muster | `ProgramScheduleWorker` (`BackgroundService`) folgt `MediaDurationProbeWorker`: `IServiceScopeFactory` + `IBackgroundProcessingGate`-Lease + `TimeProvider` + Startverzögerung; Intervall wird je Iteration aus `ProgramSettingsService` gelesen. | Etabliertes Muster; Intervall-Änderungen greifen ohne Neustart; Restore-Pause über das Gate. |

## Programmabläufe

### Rollierende Planerzeugung (`IProgramScheduleService.EnsureCoverageAsync`)

1. `TVChannel` über `IProgramRuleService.GetOrCreateChannelAsync` sicherstellen; aktive Regeln des Senders laden (`IsActive == true`, aufgeteilt in Füller- und Nicht-Füller-Regeln).
2. `horizonEnd = utcNow + GetProgramScheduleHorizonAsync()`; `lastEnd` = maximales `End` der Kanal-Sendeplätze; `coverageStart = max(utcNow, lastEnd ?? utcNow)`. Liegt `lastEnd` in der Vergangenheit (Downtime > Restplan), startet die Planung bei `utcNow` — Geschichte wird nicht zurückgeschrieben.
3. Zugriffs-Kontext laden: alle Nicht-Admin-Benutzer; `sourceIdsFullyShared` (Quellen mit `MediaSourceUser` für **jeden** Nicht-Admin), `unlockedTVShowIdsForAll`, `unlockedMovieCollectionIdsForAll` (`UnlockedMediaEntry` für jeden Nicht-Admin). Kein Nicht-Admin → alles freigegeben.
4. Pro Nicht-Füller-Regel Material einmalig expandieren (Filter: nur Einträge mit auflösbarem `MediaItem` mit `Duration != null` UND vollständiger Freigabe; verwaistes Material → leere Liste + Log).
5. Pro Regel Ausstrahlungstermine im Fenster `[coverageStart − 24 h, horizonEnd]` erzeugen (`Daily` täglich, `Weekly` am Wochentag, `Once` am `SingleDate`, `Holiday` per `GermanHolidayCalendar` + `HolidayFederalState`), jeweils `localDate + TimeOfDay` → UTC.
6. Ausstrahlungen nach `(Priority desc, RuleId asc, Start asc)` ordnen; blockierte Bereiche = persistierte Sendeplätze im Fenster; je Ausstrahlung Sendeplätze materialisieren (`EpisodeCount`/`SlotDuration`-Regel, Cursor-Fortschritt über die expandierte Liste, Wrap-around); bei Überlappung mit blockiertem Bereich → Ausstrahlung entfällt komplett, Cursor unverändert; sonst alle Sendeplätze platzieren (auch der, der vor `coverageStart` beginnt, aber hineinläuft) und Bereiche als belegt markieren.
7. Lücken in `[coverageStart, horizonEnd]` bestimmen und Round-Robin über Füller-Regeln füllen (nur vollständig passende Inhalte; Cursor je Regel nur bei Platzierung fortsetzen); verbleibende Restlücken loggen.
8. Alle `ProgramSlot`- und `ProgramRuleCursor`-Änderungen in einem `SaveChangesAsync` persistieren; Ergebnis als `ProgramScheduleRunSummary` zurückgeben (SlotsCreated, FillerPlaced, SkippedNoDuration, SkippedNoAccess, OrphanedRules, DroppedConflicts, RemainingGapSeconds).

Beteiligte Klassen: `ProgramScheduleService`, `IProgramRuleService`, `ProgramSettingsService`, `GermanHolidayCalendar`, `ApplicationDbContext`, `MediaHierarchyRegistry` (Release-Daten für Playlist-Sortierung).

### Hintergrund-Fortschreibung (`ProgramScheduleWorker`)

1. Nach `initialDelay` (Standard 15 s) Schleife: Scope öffnen → `IBackgroundProcessingGate.EnterOperationAsync("Sendeplan")` → `IProgramScheduleService.EnsureCoverageAsync(timeProvider.GetUtcNow())` → warten `GetProgramScheduleIntervalAsync()` (je Iteration frisch gelesen).
2. Fehler je Durchlauf loggen, Worker läuft weiter; `OperationCanceledException` beim Stoppen sauber schlucken.

Beteiligte Klassen: `ProgramScheduleWorker`, `IServiceScopeFactory`, `IBackgroundProcessingGate`, `IProgramScheduleService`, `ProgramSettingsService`, `TimeProvider`.

### Admin-Konfiguration (`TVSenderAdmin.razor`)

1. `OnInitializedAsync` lädt Horizont/Intervall über `ProgramSettingsService`.
2. Zwei Zahlenfelder in der Sender-Karte; `SaveChannelSettingsAsync` ruft zusätzlich `UpdateProgramScheduleSettingsAsync`; Fehlermeldungen landen wie bisher in `channelStatus`.

## Neue Klassen

| Klasse | Typ | Zweck |
|--------|-----|-------|
| `ProgramSlot` | Datenmodellklasse | Ein konkreter Sendeplatz: `Id`, `TVChannelId` (FK+Kaskade), `Start`, `End` (UTC), `MediaItemId` (weich), `MovieId`/`TVShowEpisodeId` (weich), `ProgramRuleId` (weich, nullable), `RuleType` (Snapshot), `CreatedAt` |
| `ProgramRuleCursor` | Datenmodellklasse | Fortsetzungsstand je Regel: `Id`, `ProgramRuleId` (FK+Kaskade, unique), `LastContentType`, `LastContentId`, `UpdatedAt` |
| `ProgramCursorContentType` | Enum | `Movie`, `TVShowEpisode`, `PlaylistEntry` — Inhaltsart des Cursor-Verweises |
| `ProgramSlotConfiguration` / `ProgramRuleCursorConfiguration` | `IEntityTypeConfiguration` | EF-Mapping (Index `(TVChannelId, Start)`; Unique `ProgramRuleId`) |
| `IProgramScheduleService` | Interface | `EnsureCoverageAsync(DateTimeOffset utcNow, CancellationToken)` → `ProgramScheduleRunSummary` |
| `ProgramScheduleService` | Klasse (scoped) | Planerzeugung, Expansion, Konflikte, Füller, Cursor (Service-Layer-Muster) |
| `ProgramScheduleRunSummary` | Record | Ergebnis eines Planungslaufs (für Log/Tests) |
| `GermanHolidayCalendar` | statische Klasse | Deutscher Feiertagskalender (Computus + Landesfeiertage je `DE-*`-Code) |
| `ProgramScheduleWorker` | `BackgroundService` | Rollierende Fortschreibung im konfigurierten Intervall |

## Änderungen an bestehenden Klassen

### `Setup` (Datenmodellklasse)
- **Neue Eigenschaften:** `ProgramScheduleHorizonHours` (`int`, Default 48) — Planungshorizont; `ProgramScheduleIntervalMinutes` (`int`, Default 15) — Generierungsintervall.

### `ApplicationDbContext` (DbContext)
- **Neue DbSets:** `ProgramSlots`, `ProgramRuleCursors`.

### `ProgramSettingsService` (Service)
- **Neue Methoden:** `GetProgramScheduleHorizonAsync` (→ `TimeSpan`, Default 48 h bei Wert ≤ 0), `GetProgramScheduleIntervalAsync` (→ `TimeSpan`, Default 15 min), `UpdateProgramScheduleSettingsAsync(int horizonHours, int intervalMinutes)` mit Min-Validierung (ArgumentException bei < 1).

### `TVSenderAdmin.razor` (UI)
- **Neue Felder:** Zahlenfelder `tvsender-horizon-hours` / `tvsender-interval-minutes` in der Sender-Karte; Laden in `OnInitializedAsync`; `SaveChannelSettingsAsync` persistiert sie zusätzlich.

### `ServiceCollectionExtensions`
- **Neue Registrierungen:** `AddScoped<IProgramScheduleService, ProgramScheduleService>()`, `AddHostedService<ProgramScheduleWorker>()`.

### `VideoWebPlayerBackupData` (Backup)
- `OptionalRestoreTables`: `+ ProgramSlots`, `+ ProgramRuleCursors`.
- `OptionalRestoreColumns`: `+ Setups.ProgramScheduleHorizonHours`, `+ Setups.ProgramScheduleIntervalMinutes`.
- `OptionalRestoreIntDefaults`: `+ (Setups, ProgramScheduleHorizonHours, 48)`, `+ (Setups, ProgramScheduleIntervalMinutes, 15)`.

### `MediaItem`
- Zugriffspfad auf die Quelle für den Freigabe-Filter: `MediaItem.MediaCollection.MediaSourceId` (keine Modelländerung nötig).

## Datenbankmigrationen

| Migrationsname | Betroffene Tabellen/Spalten | Beschreibung der Änderung |
|----------------|----------------------------|---------------------------|
| `AddProgramSchedule` | `ProgramSlots` (neu), `ProgramRuleCursors` (neu), `Setups.ProgramScheduleHorizonHours`, `Setups.ProgramScheduleIntervalMinutes` | Sendeplatz- und Cursor-Tabellen (FKs/Indizes wie in den Konfigurationen), zwei Setup-Int-Spalten mit Defaults |

## Validierungsregeln

| Feld / Objekt | Regel | Fehlerfall |
|---------------|-------|------------|
| `ProgramScheduleHorizonHours` | ≥ 1 | `ArgumentException` in `UpdateProgramScheduleSettingsAsync` |
| `ProgramScheduleIntervalMinutes` | ≥ 1 | `ArgumentException` in `UpdateProgramScheduleSettingsAsync` |
| Planbare Inhalte | `MediaItem.Duration` gesetzt UND vollständige Freigabe | Inhalt wird übersprungen + geloggt (kein Fehlerwurf) |
| Verwaistes `MaterialId` | Existenz beim Expandieren prüfen | Regel erzeugt nichts + Log |

## Konfigurationsänderungen

| Eintrag | Typ | Standardwert | Zweck |
|---------|-----|--------------|-------|
| `Setup.ProgramScheduleHorizonHours` | `int` | 48 | Planungshorizont in Stunden |
| `Setup.ProgramScheduleIntervalMinutes` | `int` | 15 | Generierungsintervall des Workers in Minuten |

## Seiteneffekte und Risiken

- **Planungslast:** Expansion lädt Material- und Freigabe-Daten pro Lauf; bei großen Mediatheken ist das ein begrenzter Satz von Sammel-Queries (kein N+1) — Risiko gering, Intervall 15 min.
- **Zeitzone/DST:** `TimeOfDay` in Ortszeit → UTC-Konversion; an DST-Tagen kann die effektive UTC-Zeit springen — bewusstes Verhalten.
- **Schritt-4-Abgrenzung:** Extend-only bedeutet, dass Regel-Änderungen erst ab dem freien Planungsende wirken — die Reaktion auf Änderungen (Verdrängen) ist explizit Schritt 4.
- **Bestehende Tests:** Neue `DbSet`s ändern das Schema — `PairingTestDb` nutzt `EnsureCreated`, deckt neue Tabellen automatisch ab; Backup-Tests bleiben grün, solange die neuen Tabellen in `OptionalRestoreTables` stehen.

## Umsetzungsreihenfolge

1. **Entitäten + Konfigurationen + DbSets**
   - Voraussetzungen: Keine
   - `ProgramSlot`, `ProgramRuleCursor`, `ProgramCursorContentType`, beide `IEntityTypeConfiguration`s, `DbSet`s im `ApplicationDbContext`.
2. **Setup-Spalten + ProgramSettingsService-Methoden**
   - Voraussetzungen: Schritt 1 nicht nötig (unabhängig), aber `Setup` existiert
   - `Setup.ProgramScheduleHorizonHours`/`ProgramScheduleIntervalMinutes`, Getter/Updater.
3. **EF-Migration**
   - Voraussetzungen: 1, 2
   - `dotnet ef migrations add AddProgramSchedule --project VideoWebPlayer`; Snapshot-Prüfung.
4. **Backup-Registrierung**
   - Voraussetzungen: 1, 2
   - `OptionalRestoreTables`/`OptionalRestoreColumns`/`OptionalRestoreIntDefaults`.
5. **`GermanHolidayCalendar`**
   - Voraussetzungen: Keine
   - Statische Berechnung + Tests.
6. **`IProgramScheduleService`/`ProgramScheduleService` + `ProgramScheduleRunSummary`**
   - Voraussetzungen: 1, 2, 5
   - Kernlogik wie unter „Programmabläufe".
7. **`ProgramScheduleWorker` + DI-Registrierungen**
   - Voraussetzungen: 2, 6
   - Worker-Klasse, `ServiceCollectionExtensions`.
8. **Admin-UI-Felder**
   - Voraussetzungen: 2
   - `TVSenderAdmin.razor` Sender-Karte.
9. **Tests**
   - Voraussetzungen: 1–8
   - Testklassen wie unten; E2E-Erweiterung.
10. **Dokumentation**
    - `docs/help/tv-sender.md`, `docs/RELEASE_NOTES.md`, README-Prüfung.

## Tests

### Neue Tests

| Test / Hilfsmethode | Testklasse | Was wird geprüft / bereitgestellt? |
|--------------------|------------|-------------------------------------|
| `GetHolidays_ReturnsNationwideHolidays` | `GermanHolidayCalendarTests` | Feste bundesweite Feiertage + Oster-Gerüst (bekannte 2026-Daten: Karfreitag 03.04., Ostermontag 06.04., Himmelfahrt 14.05., Pfingstmontag 25.05., Fronleichnam 04.06.) |
| `GetHolidays_StateSpecific` | `GermanHolidayCalendarTests` | Fronleichnam in `DE-NW` ja / `DE-HH` nein; Reformationstag `DE-BB` ja / `DE-BW` nein; Frauentag `DE-BE`/`DE-MV`; Buß- und Bettag nur `DE-SN` (18.11.2026); `null` → nur bundesweit |
| `EnsureCoverage_DailyMovieRule_SchedulesAtTimeOfDayEachDay` | `ProgramScheduleServiceTests_Generation` | Tägliche Film-Regel → Sendeplätze täglich zur Sendezeit, Horizont abgedeckt |
| `EnsureCoverage_WeeklyRule_OnlyOnDayOfWeek` / `Once_OnlyOnSingleDate` / `Holiday_OnlyOnHoliday` | `ProgramScheduleServiceTests_Generation` | Wiederholungsmuster-Bindung an Kalender (inkl. Bundesland) |
| `EnsureCoverage_SeriesRule_AdvancesEpisodesInAirOrder` | `ProgramScheduleServiceTests_Generation` | `EpisodeCount=2` → S1E1,S1E2; zweiter Lauf → S1E3…; Cursor persistiert |
| `EnsureCoverage_SeriesExhausted_WrapsAround` | `ProgramScheduleServiceTests_Generation` | Nach letzter Episode beginnt die Serie wieder vorn |
| `EnsureCoverage_SlotDuration_FillsUntilTarget` | `ProgramScheduleServiceTests_Generation` | `SlotDuration` ohne `EpisodeCount`: Items bis Ziellänge (mind. 1, Überschreiten erlaubt) |
| `EnsureCoverage_PlanIsGapless_WhenFillerExists` | `ProgramScheduleServiceTests_Generation` | Mit Füller-Regel: keine Lücke/Überlappung im Horizont; Slot-Kette ist lückenlos |
| `EnsureCoverage_OnlyFillerRule_FillsEntireHorizon` | `ProgramScheduleServiceTests_Filler` | Ohne andere Regeln: kompletter Horizont aus Füllern |
| `EnsureCoverage_FillerItemTooLong_IsSkipped` / `NoFittingFiller_LeavesGap` | `ProgramScheduleServiceTests_Filler` | Nur passende Füller; Restlücke bleibt + Summary |
| `EnsureCoverage_HigherPriorityWins_Overlap` | `ProgramScheduleServiceTests_Conflicts` | Gleiche Zeit, Priorität entscheidet; Verlierer komplett entfallen, Cursor unverändert |
| `EnsureCoverage_EqualPriority_LowerRuleIdWins` | `ProgramScheduleServiceTests_Conflicts` | Deterministischer Tiebreak |
| `EnsureCoverage_ItemWithoutDuration_IsSkipped` | `ProgramScheduleServiceTests_Access` | `Duration == null` → nicht eingeplant (Füller schließt) |
| `EnsureCoverage_NotFullyReleased_IsExcluded` / `UnlockedForAll_IsScheduled` / `NoNonAdminUsers_EverythingSchedulable` | `ProgramScheduleServiceTests_Access` | Quelle nicht für alle Nicht-Admins freigegeben → ausgeschlossen; `UnlockedMediaEntry` für alle → planbar |
| `EnsureCoverage_OrphanedMaterial_ProducesNothing` | `ProgramScheduleServiceTests_Generation` | `MaterialId` auf gelöschte Serie → kein Sendeplatz, kein Fehler |
| `EnsureCoverage_SpanningOccurrence_KeepsTrueStart` | `ProgramScheduleServiceTests_Generation` | Ausstrahlung beginnt vor `coverageStart`, läuft hinein → echter `Start` |
| `EnsureCoverage_SecondRun_ExtendsNotDuplicates` | `ProgramScheduleServiceTests_Generation` | Extend-only: erneuter Lauf erzeugt keine doppelten Sendeplätze |
| `GetProgramScheduleHorizon/Interval_Defaults` / `Update_Validates` / `Update_Persists` | `ProgramSettingsServiceTests` (Erweiterung oder neue Klasse) | Defaults 48 h/15 min, Min-Validierung, Persistenz |
| `Worker_RunsEnsureCoverage_OnInterval` | `ProgramScheduleWorkerTests` | Worker ruft `EnsureCoverageAsync` nach Start und im Intervall (Manueller `TimeProvider`/Test-Provider analog `PlaylistBackfillWorkerTests`) |
| `ReadFromAsync_LegacyBackupWithoutProgramScheduleTables_RestoresSuccessfully` | `VideoWebPlayerBackupDataTests` | Alt-Backup ohne `ProgramSlots`/`ProgramRuleCursors`/neue Setup-Spalten stellt wieder her |
| `ProgramScheduleTestDb`-Hilfsklasse | `Helpers` | SQLite-Fixture + Seed-Helper (Benutzer, Quellenzugriff, Serien/Filme mit `MediaItem.Duration`) — Muster `PairingTestDb`/`PlaylistServiceTestBase` |

### Betroffene bestehende Tests

| Test / Testklasse | Grund der Anpassung |
|-------------------|---------------------|
| `TVSenderAdminE2ETests.Admin_CreateEditDeactivateDeleteRule` | Erweiterung: neue Einstellungsfelder befüllen und Speichern prüfen (kein neuer Test — E2E-Konsolidierung) |

### E2E-Tests (primärer Funktionsnachweis)

Der Schritt enthält genau eine neue UI-Interaktion: zwei Zahlenfelder in der bestehenden Sender-Karte von `/admin/tv-sender`. Der bestehende konsolidierte E2E-Test `Admin_CreateEditDeactivateDeleteRule` wird um das Setzen und Speichern dieser Felder erweitert (befüllen → `tvsender-save-settings` → Statusmeldung), ohne neue Testmethode (Projektkonvention: minimale Anzahl FlaUI-/Playwright-Tests bei gleicher Abdeckung).

| Priorität | Szenario | Testdatei / Testklasse | Abgedecktes Akzeptanzkriterium | Warum E2E nötig ist |
|-----------|----------|------------------------|-------------------------------|-------------------|
| Pflicht | Horizont-/Intervall-Felder setzen und speichern | `TVSenderAdminE2ETests.Admin_CreateEditDeactivateDeleteRule` | Konfigurierbarkeit über etablierte Einstellungsmechanismen | Sichtbarer Admin-Eingriff über UI |
| Pflicht | Bestehende Gate-Prüfung | `TVSenderAdminE2ETests.NonAdmin_SeesNotAuthorized` (unverändert) | Admin-Zugriff bleibt erzwungen | Bereits vorhanden |

Bestehende E2E-Tests, die angepasst werden müssen: nur der oben genannte. Die eigentliche Planerzeugung ist kein Nutzerfluss (Hintergrund-Service ohne UI-Oberfläche in diesem Schritt) — Nachweis über Service-Tests gegen echtes SQLite.

## Offene Punkte

Keine.
