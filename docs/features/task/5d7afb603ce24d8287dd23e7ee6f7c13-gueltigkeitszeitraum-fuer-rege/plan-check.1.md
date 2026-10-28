# Plan-Gegenprüfung

## Ergebnis

**Status:** Plan lückenhaft

Die Umsetzungsschritte decken die Anforderung vollständig ab (sechs Gültigkeitsmodi inkl.
einseitig offener Grenzen, Auto-Löschung nur im Worker, End-Sortierung + Kennzeichnung,
Backup-/Migrations-/SQLite-DoD). Alle im Plan genannten Klassen, Zeilen und
Erweiterungspunkte wurden gegen den Code verifiziert und stimmen. Die Lücke liegt
ausschließlich in der E2E-Testabdeckung: Die Dialog-Eingabeblöcke „Jährliches Fenster"
(Monats-Select + Tagesfeld) und „Monatliches Fenster" (Tagesfelder 1–31) werden von keinem
geplanten E2E-Test durchlaufen — ihre spezifischen Controls und die zugehörigen
`SaveRuleAsync`-Konversionspfade (Konventionsjahr 2000 mit `DaysInMonth`-Prüfung bzw.
Monatstag-Normalisierung) bleiben ohne E2E-Nachweis.

## Abgleich Akzeptanzkriterien

| Akzeptanzkriterium | Umsetzung im Plan | Testnachweis im Plan | Status |
|--------------------|-------------------|----------------------|--------|
| Gültigkeit als von `Recurrence` unabhängige Dimension je `ProgramRule` | Enum `ProgramRuleValidityMode` (6 Werte), 9 flache Spalten auf `ProgramRule`, EF-Migration `AddProgramRuleValidity` — Umsetzungsreihenfolge Schritte 1, 3, 8 | `CreateAndUpdateRule_ValidityFieldsPersist`, Persistenz-Assertions in `ProgramRuleServiceTests_Crud` | Abgedeckt |
| Uneingeschränkt = Standard, abwärtskompatibel für Bestandsdaten und Alt-Backups | `Unrestricted = 0`, `IsRequired().HasDefaultValue(Unrestricted)` in `ProgramRuleConfiguration`, Migrations-Default 0, `OptionalRestoreIntDefaults`-Eintrag | `IsValidOn_Unrestricted_AlwaysTrue`, `LegacyBackupWithoutValidityColumns_RestoresRulesAsUnrestricted` | Abgedeckt |
| Einmaliger Zeitraum Von–Bis, inklusive Grenzen, einseitig offene Grenzen erlaubt | `OneTime`: `ValidFrom ≤ day ≤ ValidTo`, jede Grenze einzeln optional, mindestens eine Pflicht, `ValidFrom ≤ ValidTo` | `IsValidOn_OneTime_BoundariesInclusive`, `_OpenBounds`, `CreateRule_OneTime*_Throws`/`_OpenBound_IsAllowed`, `EnsureCoverage_OneTimeRule_*`, E2E-Pflicht-Test (Dialog inkl. einseitig offenem Fenster) | Abgedeckt |
| Jährlich wiederkehrendes Monat-Tag-Fenster inkl. Jahreswechsel-Wrap | `AnnualWindow`: Monat-Tag-Ordinal `Monat*100+Tag`, `Von > Bis` = Wrap; Speicherung als `DateOnly` im Konventionsjahr 2000 (29.02. darstellbar) | `IsValidOn_Annual_InsideAndOutside`/`_YearCrossing`, `EnsureCoverage_AnnualRule_*` | Lücke (E2E für Dialog-Fluss fehlt) |
| Monatlich wiederkehrendes Tag-im-Monat-Fenster inkl. Monatswechsel-Wrap | `MonthlyWindow`: `ValidFromDay`/`ValidToDay` 1–31, `FromDay > ToDay` = Wrap, Tag 31 entfällt in kürzeren Monaten | `IsValidOn_Monthly_*`, `EnsureCoverage_MonthlyRule_*` | Lücke (E2E für Dialog-Fluss fehlt) |
| Wöchentliche Wochentags-Mehrfachauswahl | `WeeklyWindow`: `ValidWeekdays`-Bitmaske über `DayOfWeek` 0–6, Pflicht 1–127 | `IsValidOn_Weekly_*`, `CreateRule_Weekly_*_Throws`, `EnsureCoverage_WeeklyRule_*`, E2E-Test (Checkbox-Gruppe) | Abgedeckt |
| Feiertagsbezogenes Fenster relativ zu benanntem Anker, ohne neue NuGet-Abhängigkeit | `HolidayWindow` + Enum `ProgramRuleHolidayAnchor` (12 bundeslandunabhängige Anker), `GermanHolidayCalendar.GetAnchorDate` auf vorhandenem Computus (verifiziert: `EasterSunday` privat Z. 85, `RepentanceDay`-Muster Z. 76), Fenster `day.Year ± 1`, Offsets 0–62 | `IsValidOn_Holiday_*`, `GetAnchorDate_ReturnsExpectedDates`, `CreateRule_Holiday_*_Throws`, `EnsureCoverage_HolidayRule_*`, E2E-Test (Anker-Select + Offsets) | Abgedeckt |
| Außerhalb der Gültigkeit keine Sendeplätze — für alle `ProgramRuleType`s inkl. `Filler` | `IsValidOn`-Check in `BuildOccurrences` (verifiziert: `applies`-Switch wertet nur `Recurrence`, Z. 1626–1635) + eigener Filler-Check in `FillGaps` pro Lückenposition (verifiziert: Filler werden aus `BuildOccurrences` ausgeschlossen, Z. 131/1090; `fillAt`-Schleife Z. 2005) | `EnsureCoverage_*_SchedulesOnlyWithinWindow` je Modus, `EnsureCoverage_ExpiredOneTimeRule_ProducesNothing`, `EnsureCoverage_InvalidFiller_FillsOnlyWithinWindow`, `Reconcile_RuleLeftWindow_DisplacesFutureAirings` | Abgedeckt |
| Abgelaufene einmalige Regel wird automatisch gelöscht — nur im Worker-Lauf hinter `TvSenderEnabled`, nicht beim Listenladen | `PurgeExpiredRulesAsync(utcNow, ct)` am `IProgramScheduleService`, `ProgramScheduleWorker.ExecuteAsync` ruft sie im Enabled-Block vor `ReconcileAsync` (verifiziert: Z. 63–72 hinter `IBackgroundProcessingGate`); explizit „kein Löschen beim Laden der Liste" | `PurgeExpiredRules_*` (3 Tests, echtes SQLite via `ProgramScheduleTestDb`), `Worker_Run_PurgesExpiredRules`/`_DisabledFeature_SkipsPurge`, E2E-Pflicht-Test über „Sendeplan jetzt erzeugen" | Abgedeckt |
| Geplante Sendeplätze der gelöschten/ungültigen Regel werden verdrängt, Historie bleibt | `ProgramSlot.ProgramRuleId`-Soft-Referenz (verifiziert: kein FK); folgendes `ReconcileAsync` setzt `Displaced` über bestehenden Pfad `rule is null` (verifiziert: Z. 1117); FK-Kaskade für `ProgramRuleCursor`/Join-Zeilen | `PurgeExpiredRules_SlotsDisplacedAfterReconcile`, `Reconcile_RuleLeftWindow_DisplacesFutureAirings` | Abgedeckt |
| Ungültige wiederkehrende Regeln bleiben erhalten und sortieren ans Ende | `GetRulesAsync`: in-memory stabile Gruppierung `IsValidOn(rule, localToday)` nach `OrderBy(Name)` (verifiziert: bisher einzige Sortierung Z. 91; UI rendert Lieferreihenfolge) — planerweitert auf alle aktuell ungültigen Regeln (Anwender-Entscheidung) | `GetRulesAsync_InvalidRule_SortsLast`, E2E-Pflicht-Test (End-Sortierung + Chip + Fenstertext) | Abgedeckt |
| Kennzeichnung des Gültigkeitsstatus in der Regelauflistung | Warn-Chip „Derzeit ungültig" (`admin-chip-warning`, verifiziert in `app.css`/`rule-panel-badges` Z. 133) + `ValidityLabel`-Metazeile mit Fenstertext je Modus (Muster `RuleMetaLabel`/`DiagnosisLabel`, verifiziert Z. 162–165/2268) | E2E-Pflicht-Test prüft Chip + Fenstertext in der realen Liste | Abgedeckt |
| Serverseitige Durchsetzung/Validierung (kein reines UI-Verstecken) | `ValidateRuleInputAsync`: `Enum.IsDefined` für Modus und Anker, modusabhängige Pflicht-/Wertregeln, Normalisierung nicht moduspassender Felder auf `null` — hinter bestehendem `RequireAdminAsync` (verifiziert Z. 112/150) | `ProgramRuleServiceTests_Validation`: 7 neue Tests je Modus inkl. Normalisierung; E2E-Fehlerfall (`rule-edit-error`) | Abgedeckt |
| UI: Gültigkeitsauswahl und modusabhängige Eingaben im Regel-Editor | `rule-validity`-Select (6 Optionen) + konditionale Blöcke je Modus, `RuleEditModel`-Erweiterung, `StartCreate`/`StartEdit`/`SaveRuleAsync` (verifiziert: Z. 1884/1891/2053/2325) | E2E-Tests für Einmalig/Wöchentlich/Feiertag/Fehlerfall; **fehlt: Jährlich und Monatlich** | Lücke (teilweise) |
| DoD: EF-Migration, `VideoWebPlayerBackupData` (`OptionalRestoreColumns` + Default + Alt-Backup-Regressionstest), echte SQLite-Tests, Hilfe-Doku | Migration `AddProgramRuleValidity` (9 Spalten); `OptionalRestoreColumns` += 9 Einträge, `OptionalRestoreIntDefaults` += `ValidityMode` (verifiziert: Arrays Z. 51/151, `PlaceholderGrace`-Vorbild Z. 101/169); Purge-/Planungstests über `ProgramScheduleTestDb` (SQLite shared-memory, echte FKs); `docs/help/tv-sender*.md` in Schritt 13 | `LegacyBackupWithoutValidityColumns_RestoresRulesAsUnrestricted` via `LegacyBackupArchiveBuilder.RemoveColumnsAsync` (verifiziert vorhanden) | Abgedeckt |

## Fehlende oder unvollständige Testanforderungen

- [ ] E2E-Test für den Dialog-Fluss „Jährliches Fenster" fehlt (Akzeptanzkriterium „jährlich wiederkehrend"): `rule-validity` auf „Jährlich" → Monats-`<select>` + Tages-Zahlenfeld je Grenze → speichern → Fenstertext im Meta-Label. Einziger Nachweis der `SaveRuleAsync`-Konversion in `DateOnly` im Konventionsjahr 2000 inkl. `DaysInMonth(2000, month)`-Prüfung und der Monats-Select-Bindung — diese Controls und der Konversionspfad werden von keinem geplanten E2E-Test berührt.
- [ ] E2E-Test für den Dialog-Fluss „Monatliches Fenster" fehlt (Akzeptanzkriterium „monatlich wiederkehrend"): `rule-validity` auf „Monatlich" → zwei Tages-Zahlenfelder 1–31 (inkl. Wrap-Eingabe wie 25.–5.) → speichern → Fenstertext im Meta-Label. Der Eingabeblock und die `int`-Konversion in `SaveRuleAsync` sind eigene, sonst ungetestete UI-Pfade.

## E2E-Abdeckung

| Benutzerfluss / Akzeptanzkriterium | Geplanter E2E-Test | Status |
|------------------------------------|--------------------|--------|
| Regel mit einmaligem Zeitraum anlegen (inkl. einseitig offener Grenzen) → persistiert + Meta-Label | Pflicht-Test in `TVSenderAdminE2ETests` (Select + `InputDate`-Felder + Speichern + Label) | Abgedeckt |
| Aktuell ungültige Regel → End-Sortierung + „Derzeit ungültig"-Chip + Fenstertext in der Liste | Pflicht-Test in `TVSenderAdminE2ETests` (Seed ungültig gesetzt, reale Listendarstellung) | Abgedeckt |
| Abgelaufene einmalige Regel wird nach „Sendeplan jetzt erzeugen" automatisch entfernt (Worker-Purge) | Pflicht-Test (Seed `ValidTo` gestern → `GenerateNowAsync` → `NotifyScheduleChanged` → Worker) — Mechanismus verifiziert (`GenerateNowAsync` Z. 1412 feuert Signal) | Abgedeckt |
| Wöchentliche Gültigkeit über Checkbox-Gruppe anlegen | Wichtig-Test (`rule-valid-weekday-*` + Meta-Label) | Abgedeckt |
| Feiertagsfenster über Anker-Select + Offsets anlegen | Wichtig-Test (`rule-valid-holiday-*` + Meta-Label) | Abgedeckt |
| Validierungsfehler im Dialog (`OneTime` ohne Grenze / `ValidTo` vor `ValidFrom`) | Wichtig-Test (`rule-edit-error`) | Abgedeckt |
| Jährliches Fenster über Dialog anlegen (Monats-Select + Tagesfeld) | — kein Test geplant | Lücke |
| Monatliches Fenster über Dialog anlegen (Tagesfelder 1–31) | — kein Test geplant | Lücke |
| Bestehender Dialog-Fluss bleibt stabil | `Admin_CreateEditDeactivateDeleteRule` auf Selektor-Stabilität prüfen (im Plan als Anpassungsbedarf genannt) | Abgedeckt |

## Fehlende oder unvollständige Planbestandteile

- [ ] E2E-Testabschnitt um zwei Szenarien ergänzen: Dialog-Anlage einer Regel mit jährlichem Fenster und einer Regel mit monatlichem Fenster (Setup, Moduswahl, konditionale Eingaben, Speichern, sichtbares Ergebnis im Meta-Label). Beide Szenarien können den Edit-Roundtrip (`StartEdit` befüllt gespeicherte Felder, inkl. `DateOnly`→Monat/Tag-Aufteilung) gleich mit abdecken.

## Hinweise

- Alle im Plan genannten Code-Positionen wurden stichprobenartig geprüft und stimmen:
  `BuildOccurrences` Z. 1615 (nur `Recurrence`-Switch), `FillGaps` Z. 1970,
  `ReconcileWindow.Rebuild`/`Validate` Z. 1090/1117, `EnsureCoverageAsync` `IsActive`-Filter
  Z. 83, `ReconcileAsync` `activeRules` Z. 169, `GetRuleDiagnosesAsync` Z. 424/447,
  `DeleteDisplacedSlotsAsync`-Logmuster Z. 484–491, `ProgramScheduleWorker` Z. 63–72,
  `ProgramRuleService.GetRulesAsync` `OrderBy(Name)` Z. 91, `ProgramRuleInput` positionales
  Record (optionaler End-Parameter bleibt kompilierbar), `TVSenderAdmin` Z. 1884/1891/2053/
  2268/2325, `VideoWebPlayerBackupData` Z. 51/151/169, `ProgramScheduleTestDb.CreateRuleAsync`
  Z. 263, `LegacyBackupArchiveBuilder.RemoveColumnsAsync`, `SeasonalGenreWindow.GetExpiry`,
  `GenreCanonicalResolver.IsVisible`, `GermanHolidayCalendar`-Computus (privat) — kein neues
  NuGet-Paket nötig.
- Die End-Sortierung wird im Plan bewusst auf alle aktuell ungültigen Regeln ausgeweitet
  (Anwender-Entscheidung Punkt 6); die Anforderung nennt nur ungültige wiederkehrende
  Regeln — die Erweiterung deckt die Anforderung ab.
- `IProgramRuleService.GetRulesAsync` dokumentiert aktuell „ordered by name"; die geänderte
  Sortier-Doku sollte bei der Umsetzung mitgezogen werden (im Plan nicht explizit als Schritt).
- Optional ergänzbar: E2E-Sichtprüfung im Sendeplan-Tab, dass eine ungültige Regel keine
  Slots erzeugt — fachlich bereits durch die `EnsureCoverage_*`-Service-Tests abgedeckt.
- `EnumContractTests` pinnen ProgramRule-Enums bisher nicht; Aufnahme der neuen Enums ist
  laut Plan optional (im Seiteneffekt-Abschnitt korrekt benannt).
