# Umsetzungsplan: Gültigkeitszeitraum für Programmregeln

## Übersicht

`ProgramRule` erhält einen von `Recurrence` unabhängigen Gültigkeitsmodus mit sechs Ausprägungen:
uneingeschränkt (Standard, abwärtskompatibel), einmaliger Von–Bis-Zeitraum mit optional einseitig
offenen Grenzen, jährliches Monat-Tag-Fenster (Wrap über den Jahreswechsel), monatliches
Tag-im-Monat-Fenster (Wrap über den Monatswechsel), wöchentliche Wochentags-Mehrfachauswahl und ein
feiertagsbezogenes Fenster relativ zu einem benannten Feiertags-Anker (z. B. „Ostersonntag −3 bis
+0 Tage"). Der Sendeplan (`ProgramScheduleService`) berücksichtigt die Gültigkeit bei der
Occurrence-Bildung und beim Lückenfüllen durch Filler-Regeln für **alle** `ProgramRuleType`s
einschließlich `Filler`; abgelaufene einmalige Regeln (Bis-Datum überschritten) löscht ein
Bereinigungsschritt im Worker-Lauf hinter `TvSenderEnabled`; aktuell ungültige Regeln sortiert
`GetRulesAsync` ans Ende und die UI kennzeichnet sie per Warn-Chip plus Fenstertext in der
Meta-Zeile. Die Feiertagsberechnung erweitert den vorhandenen `GermanHolidayCalendar` (Computus
bereits implementiert und getestet — keine neue NuGet-Abhängigkeit). Betroffen: Datenmodell +
EF-Migration (neun neue Spalten), zwei neue Enums, `ProgramRuleService`, `ProgramScheduleService` +
`ProgramScheduleWorker`, `GermanHolidayCalendar`, Admin-UI `TVSenderAdmin.razor`,
`VideoWebPlayerBackupData` sowie Tests, E2E-Tests und Hilfe-Doku.

## Designentscheidungen

| Komponente / Bereich | Gewählter Ansatz | Begründung |
|----------------------|-----------------|------------|
| Modusmenge `ProgramRuleValidityMode` | Sechs Werte: `Unrestricted = 0`, `OneTime = 1`, `AnnualWindow = 2`, `MonthlyWindow = 3`, `WeeklyWindow = 4`, `HolidayWindow = 5`. | Anwender-Entscheidung: „wiederkehrend" umfasst mehrere Perioden — jährlich (Start-/Enddatum als Monat-Tag), monatlich (Start-/Endtag im Monat), wöchentlich (Mehrfachauswahl Wochentage) und feiertagsbezogen (Fenster relativ zu einem Feiertag). |
| Speicherung der Fensterdaten | Flache, modusabhängige Spalten direkt auf `ProgramRule` — selbes Muster wie `DayOfWeek`/`SingleDate`: `ValidFrom`/`ValidTo` (`DateOnly?`) für `OneTime` und `AnnualWindow`; `ValidFromDay`/`ValidToDay` (`int?`, 1–31) für `MonthlyWindow`; `ValidWeekdays` (`int?`, Bitmaske über `DayOfWeek` 0–6) für `WeeklyWindow`; `ValidHolidayAnchor` (`ProgramRuleHolidayAnchor?`) plus `ValidHolidayBeforeDays`/`ValidHolidayAfterDays` (`int?`) für `HolidayWindow`. Nicht moduspassende Felder werden beim Speichern auf `null` normalisiert. | Ein Tabellenmodell ohne Join-Entität; Migration bleibt eine einzige `AddProgramRuleValidity`; die Validierungs-/Normalisierungspraxis des Services bleibt identisch. |
| Fenster-Auswertung | Neue statische Klasse `ProgramRuleValidity` (Service Layer-Helfer): `IsValidOn(rule, localDay)` wertet je Modus aus — `OneTime`: `ValidFrom ≤ day ≤ ValidTo`, jede Grenze einzeln optional, beide fehlend → ungültig; `AnnualWindow`: Monat-Tag-Ordinal (`Monat*100+Tag`), `Von > Bis` → Jahreswechsel-Wrap; `MonthlyWindow`: Tages-Ordinal (`Day`), `Von > Bis` → Monatswechsel-Wrap; `WeeklyWindow`: Bit-Test des Tages-`DayOfWeek` in `ValidWeekdays`; `HolidayWindow`: `GermanHolidayCalendar.GetAnchorDate(anchor, year)` für `day.Year − 1`, `day.Year`, `day.Year + 1`, Tag liegt im Fenster `anchor − before ≤ day ≤ anchor + after`. `SeasonalGenreWindow`/`GenreCanonicalResolver` bleiben unverändert. | Die Genre-Mechanik arbeitet mit `DateTime`-Jahresdaten und anderer Grenzen-Semantik; die neuen Fensterarten (Monats-Wrap, Wochenmaske, Feiertags-Offsets) haben ohnehin eigene Auswertung. Drei-Jahres-Prüfung beim Feiertagsfenster deckt Fenster ab, die den Jahreswechsel überschreiten (z. B. 1. Weihnachtstag +5 Tage). |
| Transport der Gültigkeitsfelder | `ProgramRuleInput` erhält einen einzigen optionalen Parameter `Validity` vom neuen Record-Typ `ProgramRuleValidityInput` (Value Object: Modus + alle Fensterfelder); `null` bedeutet `Unrestricted`. | Neun flache Zusatzparameter würden das positionale Record unübersichtlich machen; die Bündelung hält Validierung und Normalisierung zusammenhängend und lässt bestehende Aufrufe kompilierbar. |
| Feiertags-Anker und -Berechnung | Neuer Enum `ProgramRuleHolidayAnchor` (in `VideoWebPlayer/Data/`) mit genau zwölf bundeslandunabhängigen Ankern: `NewYear`, `GoodFriday`, `EasterSunday`, `EasterMonday`, `AscensionDay`, `PentecostSunday`, `PentecostMonday`, `FirstAdvent`, `ChristmasEve`, `ChristmasDay`, `SecondChristmasDay`, `NewYearsEve`. `GermanHolidayCalendar` wird um die öffentliche Methode `GetAnchorDate(anchor, year)` erweitert — sie nutzt den vorhandenen privaten Computus (`EasterSunday`) und ergänzt `FirstAdvent`. Das Feiertagsfenster reicht 0–62 Tage je Richtung um den Anker (Validierungsgrenze). Bundeslandspezifische Anker (z. B. Fronleichnam, Buß- und Bettag) werden bewusst nicht umgesetzt; `IsValidOn` erhält daher keinen `federalState`-Parameter. | Anwender-Entscheidung: bundeslandunabhängige Minimalmenge und Fenster-Range 0–62. Die Anforderung „Computus selbst implementieren, kein NuGet" ist im Bestand bereits erfüllt — die getestete Implementierung wird erweitert statt dupliziert. |
| Grenzen des einmaligen Zeitraums | Einseitig offene Grenzen erlaubt: nur `ValidFrom` oder nur `ValidTo` ist zulässig, mindestens eine Grenze Pflicht; bei beiden gilt `ValidFrom ≤ ValidTo`. `IsExpired` trifft nur bei gesetztem und überschrittenem `ValidTo` zu — ein einseitig offener Zeitraum ohne Bis-Datum läuft nie ab. | Anwender-Entscheidung (Punkt 5). |
| Sortierung und Kennzeichnung | Ein einheitliches Kriterium `ProgramRuleValidity.IsValidOn(rule, localToday)`: aktuell ungültige Regeln — außerhalb eines wiederkehrenden Fensters, noch nicht begonnene oder abgelaufene einmalige Zeiträume — wandern in die End-Gruppe der Liste und erhalten den Warn-Chip „Derzeit ungültig" (`admin-chip-warning`) plus Fenstertext in `ValidityLabel`. | Anwender-Entscheidung (Punkt 6); ein Kriterium für Sortierung, Chip und Planung verhindert widersprüchliche Anzeigen. |
| Automatische Löschung | `PurgeExpiredRulesAsync(utcNow, ct)` am `IProgramScheduleService` (Muster `DeleteDisplacedSlotsAsync`, `Task<int>`); `ProgramScheduleWorker` ruft sie im `TvSenderEnabled`-Block vor `ReconcileAsync`. Kein Löschen beim Laden der Liste. | Anwender-Entscheidung (Punkt 4); Worker-Kontext ohne `userId` passt nicht zu `ProgramRuleService.DeleteRuleAsync`; das folgende `ReconcileAsync` verdrängt die Sendeplätze (Soft-Referenz). |
| Nicht existierende Monatstage | Ordinalvergleich auf `Day`: ein Tag 31 entfällt in Monaten ohne ihn; bei Wrap `Von ≥ X` läuft das Fenster bis zum tatsächlichen Monatsende. | Einfachste konsistente Semantik ohne Sonderregeln. |
| Zeitbasis | Alle Gültigkeitsvergleiche auf dem lokalen Tag (`TimeZoneInfo.Local`), abgeleitet aus dem übergebenen `utcNow`. | `BuildOccurrences` iteriert bereits lokale Tage; gleiche Basis für Planung, Sortierung und Bereinigung. |

## Programmabläufe

### Sendeplan-Ausbau mit Gültigkeitsprüfung (`EnsureCoverageAsync`)

1. `EnsureCoverageAsync` lädt wie bisher alle Regeln des Senders mit `IsActive` (inkl. der neuen
   Felder — keine Filteränderung am Laden nötig).
2. `BuildOccurrences` iteriert pro Regel die lokalen Tage des Horizonts und prüft jetzt
   **zusätzlich** zum `Recurrence`-Switch `ProgramRuleValidity.IsValidOn(rule, day)`: `Unrestricted`
   → immer; `OneTime` → inklusives Datumsfenster mit einseitig offenen Grenzen; `AnnualWindow` →
   Monat-Tag-Wrap-Vergleich; `MonthlyWindow` → Tages-Wrap-Vergleich; `WeeklyWindow` →
   Wochentags-Bitmask; `HolidayWindow` → Anker-Fenster für `day.Year ± 1`. Tage außerhalb der
   Gültigkeit erzeugen keine `Occurrence`.
3. `FillGaps` wählt Filler-Regeln pro Lückenposition:
   `ProgramRuleValidity.IsValidOn(filler.Rule, LocalDay(fillAt))` — ein Filler außerhalb seiner
   Gültigkeit füllt nicht (Filler besitzen keine Occurrences, deshalb eigener Check statt
   `BuildOccurrences`).
4. Materialisierung, Clip-Füllung, `PersistCursors` laufen unverändert.

Beteiligte Klassen/Komponenten: `ProgramScheduleService`, `ProgramRuleValidity`,
`GermanHolidayCalendar`, `ProgramRule`, `RuleState`, `Occurrence`

### Neu-Bewertung und Verdrängung (`ReconcileAsync`)

1. `ReconcileWindow` ruft `BuildOccurrences` mit denselben `activeRules` — durch die integrierte
   Gültigkeitsprüfung enthält `_occurrenceStarts` keine Starts außerhalb des Gültigkeitsfensters.
2. `Validate`/`AiringReproducible` verankern jede geplante Ausstrahlung an einem Occurrence-Start:
   Airings, deren Sendetag außerhalb der Gültigkeit liegt, finden keinen Anker und werden
   `Displaced` — automatisch, ohne weitere Änderung am Validierungsweg.
3. Ein Airing, das **innerhalb** des Fensters begann und über das Fensterende hinausläuft, bleibt
   reproduzierbar und läuft zu Ende (sein Anker-Occurrence liegt im Fenster).
4. `RewindCursor` funktioniert unverändert, weil `ReconcileAsync` `RuleState`s für alle Regeln (auch
   ungültige) aufbaut.

Beteiligte Klassen/Komponenten: `ProgramScheduleService`, `ReconcileWindow`, `ProgramRuleValidity`,
`ProgramSlot`

### Automatische Bereinigung abgelaufener Regeln

1. `ProgramScheduleWorker.ExecuteAsync`: im `GetTvSenderEnabledAsync == true`-Block (hinter dem
   `IBackgroundProcessingGate`) ruft der Worker `IProgramScheduleService.PurgeExpiredRulesAsync`
   **vor** `ReconcileAsync` auf.
2. `PurgeExpiredRulesAsync` bestimmt den lokalen Tag aus `utcNow`, lädt die Regeln des Senders mit
   `ValidityMode == OneTime` und entfernt per `RemoveRange` alle mit `ValidTo < localDay` (Regeln
   ohne `ValidTo` — einseitig offen — laufen nie ab und werden nicht berührt).
3. FK-Kaskade löscht `ProgramRuleCursor`- und Join-Zeilen (`ProgramRuleGenre`,
   `ProgramRuleMediaSource`, `ProgramRuleClipGroup`) mit; `ProgramSlot`-Zeilen bleiben als Historie
   erhalten (Soft-Referenz).
4. Log-Ausgabe im Stil von `DeleteDisplacedSlotsAsync` („{Deleted} abgelaufene Programmregeln
   bereinigt.").
5. Das unmittelbar folgende `ReconcileAsync` markiert die Slots der gelöschten Regeln als
   `Displaced` (bestehender Pfad „`rule is null`"), `EnsureCoverageAsync` füllt die Lücken neu.
   Beim Laden der Regelliste findet **keine** Löschung statt.

Beteiligte Klassen/Komponenten: `ProgramScheduleWorker`, `IProgramScheduleService`,
`ProgramScheduleService`, `ProgramRule`, `ProgramRuleCursor`, `ProgramSlot`

### Regelverwaltung mit Gültigkeitsfeldern

1. `SaveRuleAsync` (UI) baut `ProgramRuleInput` inkl. `Validity` (`ProgramRuleValidityInput`) aus den
   modusabhängigen `RuleEditModel`-Feldern.
2. `CreateRuleAsync`/`UpdateRuleAsync` rufen `ValidateRuleInputAsync`: `Enum.IsDefined` für `Mode`
   und (bei `HolidayWindow`) `HolidayAnchor`; modusabhängige Pflicht-/Wertregeln gemäß Abschnitt
   „Validierungsregeln"; bei `Unrestricted` werden alle Fensterfelder verworfen (Normalisierung auf
   `null`, Muster `DayOfWeek`/`SingleDate`).
3. Persistenz: `ValidityMode` immer; die Fensterfelder nur moduspassend, sonst `null`.
   `_scheduleSignal?.NotifyScheduleChanged()` unverändert.
4. `GetRulesAsync`: nach Laden (`OrderBy(Name)`) in-memory stabile Gruppierung
   `IsValidOn(rule, localToday)` — aktuell ungültige Regeln ans Ende, innerhalb der Gruppen nach
   Name.

Beteiligte Klassen/Komponenten: `ProgramRuleService`, `ProgramRuleInput`,
`ProgramRuleValidityInput`, `ProgramRuleValidity`, `IProgramScheduleSignal`

### Admin-Oberfläche (Regel-Editor und Liste)

1. `StartCreate` initialisiert `RuleEditModel.ValidityMode = Unrestricted`; `StartEdit` übernimmt
   Modus und sämtliche Fensterfelder aus der Regel (bei `AnnualWindow` Aufteilung in
   Monats-/Tagesfelder, bei `WeeklyWindow` Befüllen der Wochentags-Checkboxen).
2. Der Editor-Dialog erhält einen Gültigkeitsabschnitt: `rule-validity`-Select (sechs Optionen,
   `OnValidityModeChanged`) mit konditionalen Eingabeblöcken nach dem Muster `rule-weekday`/
   `rule-date`:
   - `OneTime`: zwei `InputDate` (`rule-valid-from`/`rule-valid-to`), beide optional, mindestens
     eines erforderlich.
   - `AnnualWindow`: je Grenze Monats-`<select>` (Klartext-Monatsnamen, Muster `rule-weekday`) +
     Tages-Zahlenfeld (`rule-valid-*-month`/`rule-valid-*-day`).
   - `MonthlyWindow`: zwei Tages-Zahlenfelder 1–31 (`rule-valid-monthly-from`/
     `rule-valid-monthly-to`) mit Hinweistext, dass ein Fenster über den Monatswechsel erlaubt ist.
   - `WeeklyWindow`: sieben Wochentags-Checkboxen (`rule-valid-weekday-*`) — Mehrfachauswahl,
     mindestens ein Tag.
   - `HolidayWindow`: Anker-`<select>` mit Klartext-Feiertagsnamen (`rule-valid-holiday-anchor`)
     plus zwei Zahlenfelder „Tage davor"/„Tage danach" (`rule-valid-holiday-before`/
     `rule-valid-holiday-after`).
3. Die Regelzeile zeigt den Gültigkeitsstatus: `ValidityLabel(rule)` als zusätzliche Meta-Zeile
   (Muster `RuleMetaLabel`/`DiagnosisLabel`) mit Fenstertext (z. B. „Zeitraum 01.03.–30.06.2026",
   „Jährlich 20.12.–06.01.", „Monatlich 25.–5.", „Wöchentlich Mo, Mi, Fr", „Ostersonntag −3 bis
   +0 Tage"); bei aktuell ungültigen Regeln ein Warn-Chip „Derzeit ungültig" (Muster
   `admin-chip-warning`) per `ProgramRuleValidity.IsValidOn`.
4. Die Liste rendert weiter in Lieferreihenfolge — die End-Sortierung kommt aus `GetRulesAsync`.

Beteiligte Klassen/Komponenten: `TVSenderAdmin.razor`, `RuleEditModel`, `ProgramRuleService`,
`ProgramRuleValidity`, `ProgramRuleHolidayAnchor`

## Neue Klassen

| Klasse | Typ | Zweck |
|--------|-----|-------|
| `ProgramRuleValidityMode` (`VideoWebPlayer/Data/`) | Enum | Gültigkeitsmodus: `Unrestricted = 0` (Default), `OneTime = 1`, `AnnualWindow = 2`, `MonthlyWindow = 3`, `WeeklyWindow = 4`, `HolidayWindow = 5` |
| `ProgramRuleHolidayAnchor` (`VideoWebPlayer/Data/`) | Enum | Benannte, bundeslandunabhängige Feiertags-Anker: `NewYear`, `GoodFriday`, `EasterSunday`, `EasterMonday`, `AscensionDay`, `PentecostSunday`, `PentecostMonday`, `FirstAdvent`, `ChristmasEve`, `ChristmasDay`, `SecondChristmasDay`, `NewYearsEve` |
| `ProgramRuleValidity` (`VideoWebPlayer/Services/`) | statische Klasse | Fenster-Auswertung je Modus: `IsValidOn(rule, DateOnly localDay)` und `IsExpired(rule, localDay)` für die Bereinigung; Grenzen inklusive; Wrap-Logik für Jahres- und Monatsfenster; Feiertagsfenster über `GermanHolidayCalendar.GetAnchorDate` für `day.Year ± 1` |
| `ProgramRuleValidityInput` (`VideoWebPlayer/Services/`) | Record (Value Object) | Gebündelte Gültigkeitseingabe: `Mode`, `From`/`To` (`DateOnly?`), `FromDay`/`ToDay` (`int?`), `Weekdays` (`int?`-Bitmask), `HolidayAnchor`, `HolidayBeforeDays`/`HolidayAfterDays` (`int?`) |
| `AddProgramRuleValidity` (`VideoWebPlayer/Migrations/`) | EF-Migration | Neun neue `ProgramRules`-Spalten (siehe „Datenbankmigrationen") |

## Änderungen an bestehenden Klassen

### `ProgramRule` (Datenmodellklasse)

- **Neue Eigenschaften:**
  - `ValidityMode` (`ProgramRuleValidityMode`) — Gültigkeitsmodus, Initialwert `Unrestricted`
  - `ValidFrom` (`DateOnly?`) — `OneTime`: Von-Datum (optional); `AnnualWindow`: Fensterbeginn als
    Monat-Tag (Jahr beim Speichern auf Konventions-Schaltjahr 2000 normalisiert, damit der 29.02.
    darstellbar bleibt)
  - `ValidTo` (`DateOnly?`) — `OneTime`: Bis-Datum, inklusive (optional); `AnnualWindow`: Fensterende
  - `ValidFromDay` (`int?`) — `MonthlyWindow`: Starttag im Monat (1–31)
  - `ValidToDay` (`int?`) — `MonthlyWindow`: Endtag im Monat (1–31, Wrap erlaubt)
  - `ValidWeekdays` (`int?`) — `WeeklyWindow`: Bitmaske, Bit `(int)DayOfWeek` (So=0 … Sa=6)
  - `ValidHolidayAnchor` (`ProgramRuleHolidayAnchor?`) — `HolidayWindow`: Feiertags-Anker
  - `ValidHolidayBeforeDays` (`int?`) — `HolidayWindow`: Tage vor dem Anker (≥ 0)
  - `ValidHolidayAfterDays` (`int?`) — `HolidayWindow`: Tage nach dem Anker (≥ 0)

### `ProgramRuleConfiguration` (EF-Konfiguration)

- `ValidityMode`: `IsRequired()` + `HasDefaultValue(Unrestricted)` — Bestandsdaten bleiben gültig.
- Alle Fensterfelder: optional (nullable); `ValidFrom`/`ValidTo` als `DateOnly?` (TEXT-Mapping wie
  `SingleDate`).

### `ProgramRuleInput` (Record)

- **Neuer Parameter** (am Ende, optional — bestehende positionale Aufrufe bleiben kompilierbar):
  `ProgramRuleValidityInput? Validity = null`.

### `ProgramRuleService` (Service)

- **Geänderte Methoden:**
  - `ValidateRuleInputAsync` — `Enum.IsDefined` für `Validity.Mode` und `Validity.HolidayAnchor`;
    modusabhängige Pflicht-/Wertregeln (siehe „Validierungsregeln"); Normalisierung nicht
    moduspassender Fensterfelder auf `null`.
  - `CreateRuleAsync` / `UpdateRuleAsync` — übernehmen `ValidityMode` und moduspassende
    Fensterfelder aus `input.Validity`.
  - `GetRulesAsync` — nach `OrderBy(Name)` in-memory stabile Gruppierung: aktuell gültige Regeln
    zuerst, aktuell ungültige ans Ende; lokaler Tag als Referenz.

### `IProgramRuleService` (Interface)

- **Geänderte Dokumentation:** `GetRulesAsync` — der Dokumentationskommentar („ordered by name",
  `IProgramRuleService.cs` Z. 29–30) wird an die neue Sortierung angepasst: aktuell gültige Regeln
  nach Name zuerst, aktuell ungültige Regeln ans Ende (innerhalb beider Gruppen nach Name).

### `ProgramScheduleService` (Service)

- **Geänderte Methoden:**
  - `BuildOccurrences` — pro Regel und Tag zusätzlich `ProgramRuleValidity.IsValidOn(rule, day)`;
    wird dadurch automatisch auch von `ReconcileWindow` und `GetRuleDiagnosesAsync` mitbewertet.
  - `FillGaps` — Filler-Auswahl pro Lückenposition auf aktuell gültige Filler-Regeln beschränken.
- **Neue Methoden:**
  - `PurgeExpiredRulesAsync(DateTimeOffset utcNow, CancellationToken)` → `Task<int>` — lädt
    `OneTime`-Regeln des Senders, entfernt jene mit gesetztem `ValidTo < localDay` (`RemoveRange` +
    `SaveChangesAsync`; FK-Kaskade für Cursor/Joins), Log-Ausgabe, Rückgabe der Anzahl. Nicht
    admin-gebunden (Worker-Kontext).

### `IProgramScheduleService` (Interface)

- **Neue Methoden:** `PurgeExpiredRulesAsync` — Signatur wie oben, Doku zum Aufrufkontext
  (Worker-Lauf vor `ReconcileAsync`).

### `ProgramScheduleWorker` (Hosted Service)

- **Geänderte Methoden:** `ExecuteAsync` — im aktivierten Block `PurgeExpiredRulesAsync` vor
  `ReconcileAsync` aufrufen.

### `GermanHolidayCalendar` (statische Klasse)

- **Neue Methoden:** `GetAnchorDate(ProgramRuleHolidayAnchor anchor, int year)` → `DateOnly` —
  liefert das konkrete Datum des Ankers im Jahr; nutzt den vorhandenen privaten `EasterSunday`-
  Computus (Oster-/Pfingst-Anker als Offsets) und `RepentanceDay`-Muster für `FirstAdvent` (Sonntag
  zwischen 27.11. und 03.12.); feste Anker als direkte `DateOnly`-Konstruktion.
- **Neue private Hilfsmethode:** `FirstAdvent(int year)` — 1. Advent berechnen.

### `TVSenderAdmin.razor` (UI-Komponente)

- **Geänderte Methoden:** `StartCreate` (Default `Unrestricted`), `StartEdit` (Übernahme aller
  Gültigkeitsfelder), `SaveRuleAsync` (baut `ProgramRuleValidityInput`: Jahresfenster → `DateOnly`
  im Konventionsjahr 2000 mit `DaysInMonth(2000, month)`-Prüfung, Monatsfenster-Tage 1–31,
  Wochentags-Checkboxen → Bitmaske, Feiertags-Offsets 0–62 → sonst `editError`),
  `RuleMetaLabel`-Bereich (Gültigkeitsanzeige).
- **Neue Methoden:** `OnValidityModeChanged` (Select-Handler, Muster `OnRecurrenceChanged`),
  `ValidityLabel(ProgramRule)` (Meta-Zeile mit Fenstertext je Modus),
  `HolidayAnchorLabel(ProgramRuleHolidayAnchor)` (Klartext-Ankernamen).
- **Neue Markup-Abschnitte:** `rule-validity`-Select + konditionale Blöcke (`rule-valid-from`,
  `rule-valid-to`, `rule-valid-*-month`, `rule-valid-*-day`, `rule-valid-monthly-*`,
  `rule-valid-weekday-*`, `rule-valid-holiday-anchor`, `rule-valid-holiday-before`,
  `rule-valid-holiday-after`); Warn-Chip in `rule-panel-badges`.
- **`RuleEditModel`-Erweiterung:** `ValidityMode`, `ValidFrom`/`ValidTo` (`DateTime?` für
  `InputDate`), `ValidFromMonth`/`ValidFromDay`/`ValidToMonth`/`ValidToDay` (`int`,
  Jahresfenster), `ValidMonthlyFromDay`/`ValidMonthlyToDay` (`int`, Monatsfenster),
  `SelectedWeekdays` (`HashSet<DayOfWeek>` oder `bool[]`, Wochenfenster),
  `ValidHolidayAnchor` (`ProgramRuleHolidayAnchor`), `ValidHolidayBeforeDays`/
  `ValidHolidayAfterDays` (`int`, Feiertagsfenster).

### `VideoWebPlayerBackupData` (Backup-Mechanik)

- `OptionalRestoreColumns` += `ProgramRules.ValidityMode`, `ProgramRules.ValidFrom`,
  `ProgramRules.ValidTo`, `ProgramRules.ValidFromDay`, `ProgramRules.ValidToDay`,
  `ProgramRules.ValidWeekdays`, `ProgramRules.ValidHolidayAnchor`,
  `ProgramRules.ValidHolidayBeforeDays`, `ProgramRules.ValidHolidayAfterDays` (Alt-Backups ohne
  diese Spalten validieren).
- `OptionalRestoreIntDefaults` += `(ProgramRules, ValidityMode, (int)ProgramRuleValidityMode.Unrestricted)`
  — Alt-Regeln werden uneingeschränkt gültig; alle übrigen neuen Spalten sind nullable und brauchen
  keinen Default-Eintrag (fehlende Spalte → NULL).

### `ProgramScheduleTestDb` (Testhelfer)

- `CreateRuleAsync` um optionale Gültigkeitsparameter erweitern (`validity`, ggf. einzelne
  Fensterparameter — am Ende, bestehende Aufrufe unverändert).

## Datenbankmigrationen

| Migrationsname | Betroffene Tabellen/Spalten | Beschreibung der Änderung |
|----------------|----------------------------|---------------------------|
| `AddProgramRuleValidity` | `ProgramRules`: +`ValidityMode` (int, NOT NULL, DEFAULT 0), +`ValidFrom` (nullable), +`ValidTo` (nullable), +`ValidFromDay` (int, nullable), +`ValidToDay` (int, nullable), +`ValidWeekdays` (int, nullable), +`ValidHolidayAnchor` (int, nullable), +`ValidHolidayBeforeDays` (int, nullable), +`ValidHolidayAfterDays` (int, nullable) | Gültigkeitsmodus und modusabhängige Fensterfelder; Default `Unrestricted` hält Bestandsdaten gültig. Erzeugt mit `dotnet ef migrations add`, Muster der bisherigen `Add*`-Migrationen; `ApplicationDbContextModelSnapshot` wird dabei aktualisiert. |

## Validierungsregeln

| Feld / Objekt | Regel | Fehlerfall |
|---------------|-------|------------|
| `Validity.Mode` | `Enum.IsDefined` | `ArgumentException` „Unbekannte Gültigkeitsart" |
| `From`/`To` bei `OneTime` | Mindestens eine Grenze Pflicht (einseitig offen erlaubt); bei beiden `From ≤ To` | `ArgumentException` (keine Grenze bzw. „Bis vor Von") |
| `From`/`To` bei `AnnualWindow` | Beide Pflicht (Monat-Tag-Fenster); Tag muss im Monat existieren (UI: `DaysInMonth`; Service erhält `DateOnly` → implizit gültig); `From > To` = erlaubter Jahreswechsel-Wrap | `ArgumentException` (fehlende Grenze); UI-`editError` bei ungültigem Tag |
| `FromDay`/`ToDay` bei `MonthlyWindow` | Beide Pflicht, jeweils 1–31; `FromDay > ToDay` = erlaubter Monatswechsel-Wrap | `ArgumentException` (fehlend/außerhalb 1–31) |
| `Weekdays` bei `WeeklyWindow` | Pflicht, Wertebereich 1–127 (mindestens ein Wochentag) | `ArgumentException` (leere/ungültige Maske) |
| `HolidayAnchor` bei `HolidayWindow` | `Enum.IsDefined` | `ArgumentException` (fehlender/unbekannter Anker) |
| `HolidayBeforeDays`/`HolidayAfterDays` bei `HolidayWindow` | Beide Pflicht, jeweils 0–62 Tage | `ArgumentException` (fehlend/außerhalb) |
| Fensterfelder bei `Unrestricted` bzw. nicht moduspassend | Werden ignoriert und auf `null` normalisiert (Muster `DayOfWeek`/`SingleDate`) | Kein Fehler |
| UI-Tagesfelder (Jahresfenster) | 1–31 und ≤ `DaysInMonth(2000, month)` | `editError` im Dialog vor dem Service-Aufruf |
| UI-Tagesfelder (Monatsfenster, Feiertags-Offsets) | 1–31 bzw. 0–62 | `editError` im Dialog vor dem Service-Aufruf |
| UI-Wochentags-Checkboxen | Mindestens eine Auswahl | `editError` im Dialog vor dem Service-Aufruf |

## Konfigurationsänderungen

Keine — die Gültigkeit ist eine Eigenschaft je `ProgramRule`-Datensatz; die Bereinigungsfrequenz
ergibt sich aus dem bestehenden `ProgramScheduleIntervalMinutes`-Worker-Intervall. Die
Feiertags-Anker sind bundeslandunabhängig, `HolidayFederalState` bleibt unberührt.

## Seiteneffekte und Risiken

- **`GetRuleDiagnosesAsync`:** Für aktuell ungültige Regeln liefert `BuildOccurrences` keinen
  `nextStart` → `evaluationStart` fällt auf die bestehende `TimeOfDay`-Fallback-Logik zurück; die
  Diagnose wird weiter erzeugt (konsistent zu inaktiven Regeln). Kein Bruch, aber neues Verhalten.
- **`ReconcileAsync`/`ReconcileWindow`:** Airings außerhalb des Gültigkeitsfensters werden künftig
  `Displaced` — gewünschter Effekt; bei einer wiederkehrenden Fenster-Regel, die ins Fenster
  zurückkehrt, bleibt der `ProgramRuleCursor` erhalten (keine Löschung) und setzt die Fortsetzung an
  der richtigen Stelle fort — positiver Seiteneffekt der Designentscheidung „nur einmalige Regeln
  löschen".
- **`FillGaps`:** Ohne den neuen Filler-Check würden ungültige Filler-Regeln weiter Lücken füllen —
  der Check ist Pflichtbestand der Umsetzung, kein optionales Extra (Gültigkeit gilt für alle
  `ProgramRuleType`s einschließlich `Filler`).
- **`WeeklyWindow` vs. `Recurrence.Weekday`/`Weekend`/`Weekly`:** Die Wochentags-Gültigkeit
  überschneidet sich fachlich mit den bestehenden Rhythmen, bleibt aber eine eigenständige Dimension
  (z. B. `Recurrence.Holiday` + `WeeklyWindow` ist eine sinnvolle Kombination). Kein Konflikt, aber
  mögliche redundante Konfiguration — akzeptiert.
- **`ProgramSlot.ProgramRuleId`-Soft-Referenz:** Nach der automatischen Löschung bleiben Slots als
  Historie und werden erst beim folgenden `ReconcileAsync` `Displaced` — dasselbe Zweiphasen-
  Verhalten wie bei manueller Löschung.
- **Feature-Flag `TvSenderEnabled`:** Bei deaktiviertem Sender pausiert auch die Bereinigung —
  abgelaufene einmalige Regeln bleiben dann bestehen (sichtbar in der degradierten End-Gruppe).
- **Sortierkriterium:** Aktuell ungültige Regeln wechseln die Gruppe tagesgenau (z. B. Wochenfenster
  ohne heutigen Wochentag) — gewünscht; die Listensortierung kann sich daher zwischen Aufrufen
  ändern, ohne dass eine Regel bearbeitet wurde.
- **`TVSenderAdminE2ETests`:** Der Dialog erhält einen neuen Abschnitt;
  `Admin_CreateEditDeactivateDeleteRule` sollte unverändert laufen (Default `Unrestricted`), ist
  aber auf Selektor-Stabilität zu prüfen.
- **`EnumContractTests`:** Pinnen die ProgramRule-Enums bisher nicht — `ProgramRuleValidityMode` und
  `ProgramRuleHolidayAnchor` bedürfen keiner Erweiterung, können aber analog aufgenommen werden.

## Umsetzungsreihenfolge

1. **Enums `ProgramRuleValidityMode` und `ProgramRuleHolidayAnchor` anlegen** (`VideoWebPlayer/Data/`)
   - Voraussetzungen: Keine.

2. **`GermanHolidayCalendar` um `GetAnchorDate` (+ `FirstAdvent`) erweitern**
   - Voraussetzungen: Schritt 1 (Anker-Enum).
   - Beschreibung: Öffentliche Anker-Datumsberechnung auf dem vorhandenen Computus; bestehende
     `GetHolidays`/`IsHoliday` bleiben unverändert.

3. **`ProgramRule` und `ProgramRuleConfiguration` erweitern** (`ValidityMode` + acht Fensterfelder +
   Defaults)
   - Voraussetzungen: Schritt 1.

4. **Statische Hilfsklasse `ProgramRuleValidity` anlegen** (`VideoWebPlayer/Services/`)
   - Voraussetzungen: Schritte 1–3 (nutzt `GermanHolidayCalendar.GetAnchorDate` für
     `HolidayWindow`).
   - Beschreibung: `IsValidOn` je Modus (Grenzen inklusive, offene Grenzen bei `OneTime`,
     Jahres-/Monats-Wrap, Wochentags-Bitmask, Feiertagsfenster `day.Year ± 1`) und `IsExpired`
     (`OneTime && ValidTo < localDay`).

5. **`ProgramRuleValidityInput`, `ProgramRuleInput` und `ProgramRuleService` erweitern**
   - Voraussetzungen: Schritte 1–4.
   - Beschreibung: Value-Object-Record; optionaler `Validity`-Parameter; `ValidateRuleInputAsync`-
     Regeln je Modus; `CreateRuleAsync`/`UpdateRuleAsync`-Persistenz mit Normalisierung;
     `GetRulesAsync`-Gruppierung inkl. Anpassung des
     `IProgramRuleService.GetRulesAsync`-Dokumentationskommentars an die neue Sortierung.

6. **`ProgramScheduleService`: Gültigkeitsprüfung einbauen**
   - Voraussetzungen: Schritte 3–4.
   - Beschreibung: Tages-Check in `BuildOccurrences`; Filler-Check pro Lückenposition in `FillGaps`.
     Damit decken `EnsureCoverageAsync`, `ReconcileWindow` und `GetRuleDiagnosesAsync` die Gültigkeit
     ab.

7. **`PurgeExpiredRulesAsync` an `IProgramScheduleService`/`ProgramScheduleService` + Worker-Aufruf**
   - Voraussetzungen: Schritte 3–4.
   - Beschreibung: Interface-Methode, Implementierung mit lokalem Tag, `RemoveRange`, Log;
     `ProgramScheduleWorker.ExecuteAsync` ruft sie vor `ReconcileAsync` im Enabled-Block auf.

8. **EF-Migration `AddProgramRuleValidity`**
   - Voraussetzungen: Schritt 3; `dotnet ef`-Tooling wie bei den bisherigen Migrationen im Projekt.
   - Beschreibung: Migration erzeugen, `ApplicationDbContextModelSnapshot` aktualisiert sich dabei.

9. **`VideoWebPlayerBackupData` ergänzen**
   - Voraussetzungen: Schritt 3.
   - Beschreibung: `OptionalRestoreColumns`-Einträge (neun Spalten) +
     `OptionalRestoreIntDefaults`-Eintrag für `ValidityMode`.

10. **Admin-UI `TVSenderAdmin.razor` erweitern**
    - Voraussetzungen: Schritte 1–5.
    - Beschreibung: `RuleEditModel`-Felder, Gültigkeitsabschnitt im Dialog mit sechs Modi und
      konditionalen Eingabeblöcken, `StartCreate`/`StartEdit`/`SaveRuleAsync`, `ValidityLabel` +
      Warn-Chip, `HolidayAnchorLabel`.

11. **Testhelfer und Unit-/Integrationstests**
    - Voraussetzungen: Schritte 2–9; Testinfrastruktur (`ProgramScheduleTestDb`, `PairingTestDb`,
      `LegacyBackupArchiveBuilder`, `ProgramScheduleWorkerTests`-Harness mit `TimeProvider`-Doubles)
      ist vorhanden; `ProgramScheduleTestDb.CreateRuleAsync` wird in diesem Schritt erweitert.
    - Beschreibung: Fenster-Unit-Tests je Modus, Anker-Tests, Service-Validierung, Sortierung,
      Planungs-/Reconcile-/Purge-Tests gegen echtes SQLite, Worker-Test, Alt-Backup-Regressionstest.

12. **E2E-Tests**
    - Voraussetzungen: Schritt 10 (und für den Purge-Fluss Schritt 7); `TVSenderAdminE2ETestBase`
      vorhanden.
    - Beschreibung: Playwright-Tests des Benutzerflusses (Editor-Abschnitt mit allen Modi, Liste,
      Purge über „Sendeplan jetzt erzeugen").

13. **Hilfe-Dokumentation und Abschluss**
    - Voraussetzungen: Schritte 1–12.
    - Beschreibung: `docs/help/tv-sender.md` (Regel-Editor), `docs/help/tv-sender-datenmodell.md`
      (neue Felder), `docs/help/tv-sender-ablauf-technisch.md` (Gültigkeitsprüfung, Bereinigung);
      `README.md`/Release Notes bei Bedarf; abschließend DoD-Lauf (`dotnet build` Debug + Release,
      `dotnet test`).

## Tests

### Neue Tests

| Test / Hilfsmethode | Testklasse | Was wird geprüft / bereitgestellt? |
|--------------------|------------|-------------------------------------|
| `IsValidOn_Unrestricted_AlwaysTrue` | `ProgramRuleValidityTests` (neu) | `Unrestricted` gilt an jedem Tag, auch ohne Felder |
| `IsValidOn_OneTime_BoundariesInclusive` | `ProgramRuleValidityTests` | Von- und Bis-Tag gelten, Tag davor/danach nicht |
| `IsValidOn_OneTime_OpenBounds` | `ProgramRuleValidityTests` | Nur Von (gilt ab Von unbegrenzt), nur Bis (gilt bis Bis), keine Grenze → ungültig (Schutz vor korrupten Daten) |
| `IsValidOn_Annual_InsideAndOutside` / `_YearCrossing` | `ProgramRuleValidityTests` | Inklusive Monat-Tag-Grenzen; über-Silvester-Fenster (z. B. 20.12.–06.01.) gilt im Dezember und Januar, nicht im Sommer |
| `IsValidOn_Monthly_InsideAndOutside` / `_MonthCrossing` / `_MissingDaySkipped` | `ProgramRuleValidityTests` | Tag-im-Monat-Fenster inklusive; Wrap (z. B. 25.–5.) gilt Ende und Anfang des Monats; Tag 31 entfällt im Februar |
| `IsValidOn_Weekly_WeekdayMask` / `_EmptyMask_NotValid` | `ProgramRuleValidityTests` | Nur gesetzte Wochentags-Bits gelten; leere/fehlende Maske → ungültig |
| `IsValidOn_Holiday_AroundAnchor` / `_YearCrossing` / `_MissingAnchor_NotValid` | `ProgramRuleValidityTests` | Fenster relativ zum Anker (z. B. Ostersonntag −3 bis +0: Gründonnerstag–Ostersonntag); Fenster über den Jahreswechsel (z. B. 1. Weihnachtstag +5 Tage → Anfang Januar); fehlende Anker-/Offset-Felder → ungültig |
| `IsExpired_OneTime_PastValidTo` / `_OpenEnd_NeverExpires` | `ProgramRuleValidityTests` | Ablauferkennung (Bis gestern → abgelaufen, Bis heute → nicht); fehlendes `ValidTo` → nie abgelaufen |
| `GetAnchorDate_ReturnsExpectedDates` | `GermanHolidayCalendarTests` (erweitern) | Bekannte Ankerdaten je Jahr (z. B. Ostersonntag, Pfingstsonntag, 1. Advent, Heiligabend, Neujahr) |
| `CreateRule_OneTimeWithoutBounds_Throws` / `_ValidFromAfterValidTo_Throws` / `_OpenBound_IsAllowed` | `ProgramRuleServiceTests_Validation` | Mindestens-eine-Grenze-Pflicht, Reihenfolge bei zwei Grenzen, einseitig offen zulässig |
| `CreateRule_AnnualWithoutBounds_Throws` / `_UnknownMode_Throws` | `ProgramRuleServiceTests_Validation` | Jahresfenster-Pflicht, `Enum.IsDefined` |
| `CreateRule_Monthly_InvalidDay_Throws` / `_WithoutDays_Throws` | `ProgramRuleServiceTests_Validation` | Tage 1–31, beide Pflicht |
| `CreateRule_Weekly_EmptyMask_Throws` / `_InvalidMask_Throws` | `ProgramRuleServiceTests_Validation` | Bitmaske 1–127 Pflicht |
| `CreateRule_Holiday_WithoutAnchorOrOffsets_Throws` / `_OffsetOutOfRange_Throws` | `ProgramRuleServiceTests_Validation` | Anker-`Enum.IsDefined`, Offsets Pflicht 0–62 |
| `CreateRule_Unrestricted_IgnoresValidityFields` / `_NonMatchingFields_NormalizedToNull` | `ProgramRuleServiceTests_Validation` | Normalisierung nicht moduspassender Felder auf `null` |
| `CreateAndUpdateRule_ValidityFieldsPersist` | `ProgramRuleServiceTests_Crud` | Modus + Fensterfelder werden gespeichert und zurückgegeben (alle Modi) |
| `GetRulesAsync_InvalidRule_SortsLast` | `ProgramRuleServiceTests_Crud` | Degradierte Regeln (außerhalb-Fenster, noch nicht begonnen, abgelaufen) am Ende, gültige nach Name davor |
| `EnsureCoverage_OneTimeRule_SchedulesOnlyWithinWindow` | `ProgramScheduleServiceTests_Generation` | Occurrences nur an Tagen im Zeitraum (inkl. einseitig offenem Fenster) |
| `EnsureCoverage_AnnualRule_SchedulesOnlyWithinWindow` | `ProgramScheduleServiceTests_Generation` | Tagesgenaue Ein-/Ausschaltung des Jahresfensters |
| `EnsureCoverage_MonthlyRule_SchedulesOnlyWithinWindow` | `ProgramScheduleServiceTests_Generation` | Tag-im-Monat-Fenster inkl. Wrap |
| `EnsureCoverage_WeeklyRule_SchedulesOnlyOnSelectedWeekdays` | `ProgramScheduleServiceTests_Generation` | Nur gesetzte Wochentage erzeugen Occurrences |
| `EnsureCoverage_HolidayRule_SchedulesOnlyAroundAnchor` | `ProgramScheduleServiceTests_Generation` | Fenster relativ zum Feiertag (Testdatum relativ zum berechneten Anker legen) |
| `EnsureCoverage_ExpiredOneTimeRule_ProducesNothing` | `ProgramScheduleServiceTests_Generation` | Komplett vergangenes Fenster → keine Slots |
| `EnsureCoverage_InvalidFiller_FillsOnlyWithinWindow` | `ProgramScheduleServiceTests_Generation` (oder `_Filler`) | Filler-Check in `FillGaps` pro Lückenposition |
| `Reconcile_RuleLeftWindow_DisplacesFutureAirings` | `ProgramScheduleServiceTests_Reconcile` | Verdrängung nach Fensteraustritt; laufender Rest-Airing bleibt |
| `PurgeExpiredRules_DeletesExpiredOneTimeRule` | `ProgramScheduleServiceTests_Purge` (neu) | Regel weg, `ProgramRuleCursor` per FK kaskadiert, Slots bleiben als Zeilen (echtes SQLite via `ProgramScheduleTestDb`) |
| `PurgeExpiredRules_SlotsDisplacedAfterReconcile` | `ProgramScheduleServiceTests_Purge` | Purge + `ReconcileAsync` → `Displaced`, Historie bleibt |
| `PurgeExpiredRules_KeepsOpenEndAndRecurringRules` | `ProgramScheduleServiceTests_Purge` | Einseitig offene `OneTime`-Regeln und alle wiederkehrenden Modi bleiben erhalten |
| `Worker_Run_PurgesExpiredRules` / `_DisabledFeature_SkipsPurge` | `ProgramScheduleWorkerTests` | Purge läuft im Enabled-Block bzw. nicht hinter deaktiviertem Flag |
| `LegacyBackupWithoutValidityColumns_RestoresRulesAsUnrestricted` | `VideoWebPlayerBackupDataTests` (oder `_ProgramRuleLinks`) | `LegacyBackupArchiveBuilder.RemoveColumnsAsync` entfernt die neun neuen Spalten → Restore liefert `Unrestricted`/NULL |

### Betroffene bestehende Tests

| Test / Testklasse | Grund der Anpassung |
|-------------------|---------------------|
| `ProgramScheduleTestDb.CreateRuleAsync` (Hilfsmethode) | Neue optionale Gültigkeitsparameter für Seeds (rückwärtskompatibel am Ende) |
| `ProgramRuleServiceTests_Crud.GetRulesAsync_ReturnsRulesOrderedByName` | Verhalten bleibt (alle Regeln `Unrestricted` → nur Hauptgruppe); ggf. um Gruppen-Assertion ergänzen |
| `ProgramRuleInput`-Konstruktoraufrufe in Tests und `SaveRuleAsync` | Neuer optionaler `Validity`-Parameter am Ende — kompilierbar ohne Änderung; nur Stellen, die die neuen Felder setzen wollen, werden angepasst |
| `SeasonalHomeServiceTests` / `ProgramScheduleServiceTests_Genres` | Keine Änderung — die Genre-Fenster-Mechanik bleibt unberührt; dienen als Referenz |
| `GermanHolidayCalendarTests` | Ergänzung um `GetAnchorDate`-Fälle; bestehende Bundesland-Tests unverändert |

### E2E-Tests (primärer Funktionsnachweis)

| Priorität | Szenario | Testdatei / Testklasse | Abgedecktes Akzeptanzkriterium | Warum E2E nötig ist |
|-----------|----------|------------------------|-------------------------------|-------------------|
| Pflicht | Regel mit einmaligem Gültigkeitszeitraum anlegen: `rule-validity` auf „Einmaliger Zeitraum" → Von/Bis-`InputDate`-Felder (inkl. einseitig offenem Fenster) → speichern → Zeitraum im Meta-Label der Regelzeile | `TVSenderAdminE2ETests` | Editor bietet die Gültigkeitsarten; eingegebener Zeitraum wird persistiert und angezeigt | Gesamter Benutzerfluss über den realen Dialog (konditionale Felder, Bindung, Speichern über `SaveRuleAsync` → Service → Reload) — nur E2E beweist die Verdrahtung |
| Pflicht | Wiederkehrende Regel außerhalb ihres Fensters (per Seed relativ zum Testdatum ungültig gesetzt, z. B. Jahresfenster morgen–morgen+10) → erscheint am Ende der Regelliste mit „Derzeit ungültig"-Chip und Fenstertext | `TVSenderAdminE2ETests` | Ans-Ende-Sortierung + Sichtbarkeits-Kennzeichnung in der Verwaltung | Sortierung und Chip sind reine UI-Effekte der Service-Ausgabe; der Test muss die reale Listendarstellung prüfen |
| Pflicht | Abgelaufene einmalige Regel wird nach „Sendeplan jetzt erzeugen" automatisch entfernt | `TVSenderAdminE2ETests` (oder neue `TVSenderAdminValidityE2ETests`) | Automatische Löschung im realen System (Worker-Lauf via `ScheduleSignal`) | Der Löschpfad läuft über den echten Worker (`PurgeExpiredRulesAsync` → `ReconcileAsync`); nur E2E deckt Intervall/Signal/Scope-Kette ab. Regel per Seed mit `ValidTo` gestern anlegen, Generate-Klick, Verschwinden aus der Liste abwarten |
| Wichtig | Regel mit Wochentags-Gültigkeit anlegen: `rule-validity` „Wöchentlich" → Wochentags-Checkboxen → speichern → Wochentage im Meta-Label | `TVSenderAdminE2ETests` | Mehrfachauswahl-Wochentage als konditionale Eingabe | Neuer Eingabetyp (Checkbox-Gruppe) im Benutzerfluss |
| Wichtig | Regel mit Feiertagsfenster anlegen: `rule-validity` „Feiertagsfenster" → Anker-Select + Tage davor/danach → speichern → Fenstertext im Meta-Label | `TVSenderAdminE2ETests` | Feiertagsbezogene Gültigkeit über den Dialog | Anker-Auswahl (benannte Entität per Klartext-Dropdown) und Offsets sind neuer UI-Fluss |
| Wichtig | Regel mit jährlichem Fenster anlegen: `rule-validity` „Jährlich" → je Grenze Monats-`<select>` + Tages-Zahlenfeld (Fenster inkl. Jahreswechsel-Wrap, z. B. 20.12.–06.01.) → speichern → Fenstertext im Meta-Label; anschließend Bearbeiten-Roundtrip: `StartEdit` befüllt Monats- und Tagesfelder aus dem gespeicherten `DateOnly` (Aufteilung aus dem Konventionsjahr 2000) | `TVSenderAdminE2ETests` | Jährlich wiederkehrendes Monat-Tag-Fenster über den Dialog | Monats-Select und Tagesfeld sind eigene Controls; die `SaveRuleAsync`-Konversion in `DateOnly` im Konventionsjahr 2000 inkl. `DaysInMonth(2000, month)`-Prüfung und die `StartEdit`-Aufteilung Monat/Tag sind nur über den realen Dialog nachweisbar |
| Wichtig | Regel mit monatlichem Fenster anlegen: `rule-validity` „Monatlich" → zwei Tages-Zahlenfelder 1–31 inkl. Wrap-Eingabe (z. B. 25.–5.) → speichern → Fenstertext im Meta-Label; anschließend Bearbeiten-Roundtrip: `StartEdit` befüllt beide Tagesfelder aus den gespeicherten `int`-Werten | `TVSenderAdminE2ETests` | Monatlich wiederkehrendes Tag-im-Monat-Fenster über den Dialog | Eigener Eingabeblock und eigener `int`-Konversionspfad in `SaveRuleAsync` — ein sonst ungetesteter UI-Pfad |
| Wichtig | Validierungsfehler im Dialog: `OneTime` ohne jede Grenze bzw. `ValidTo` vor `ValidFrom` → `rule-edit-error` zeigt die Fehlermeldung, Regel wird nicht angelegt | `TVSenderAdminE2ETests` | Serverseitige/UI-Validierung erreicht den Anwender als Dialog-Fehler | Fehlerfall ist über die UI auslösbar (`SaveRuleAsync` → `editError`) |

Welche bestehenden E2E-Tests müssen angepasst werden?

| Test / Testklasse | Grund der Anpassung |
|-------------------|---------------------|
| `TVSenderAdminE2ETests.Admin_CreateEditDeactivateDeleteRule` | Dialog erhält neuen Abschnitt — kein Funktionsbruch erwartet (Default `Unrestricted`); Selektor-Stabilität nach dem Umbau verifizieren und ggf. anpassen |

## Offene Punkte

Keine — beide Rückfragen sind entschieden: `ProgramRuleHolidayAnchor` umfasst die zwölf
bundeslandunabhängigen Anker (Designentscheidung „Feiertags-Anker und -Berechnung"), bundeslandspezifische
Anker und ein `federalState`-Bezug werden bewusst nicht umgesetzt; das Feiertagsfenster ist auf 0–62 Tage
je Richtung begrenzt (Abschnitt „Validierungsregeln").
