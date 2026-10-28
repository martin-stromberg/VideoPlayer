# Plan-Gegenprüfung

## Ergebnis

**Status:** Plan vollständig

Zweite Prüfrunde. Die in `plan-check.1.md` gemeldeten Lücken sind behoben: Der
E2E-Abschnitt enthält jetzt eigene Szenarien für den Dialog-Fluss „Jährliches
Fenster" (plan.md Z. 449, inkl. Bearbeiten-Roundtrip der `StartEdit`-Aufteilung
Monat/Tag aus dem Konventionsjahr 2000) und „Monatliches Fenster" (Z. 450, inkl.
Wrap-Eingabe und Edit-Roundtrip); beide stehen auch in der Task-Liste (#40/#41).
Die zuvor nur als Hinweis genannte Anpassung des
`IProgramRuleService.GetRulesAsync`-Dokumentationskommentars ist jetzt
expliziter Planbestandteil (Änderungen-Abschnitt Z. 187–191,
Umsetzungsschritt 5 Z. 343–345, Task #12; verifiziert: Kommentar „ordered by
name" in `IProgramRuleService.cs` Z. 29–30).

## Abgleich Akzeptanzkriterien

| Akzeptanzkriterium | Umsetzung im Plan | Testnachweis im Plan | Status |
|--------------------|-------------------|----------------------|--------|
| Gültigkeit als von `Recurrence` unabhängige Dimension je `ProgramRule` | Enum `ProgramRuleValidityMode` (6 Werte), neun flache Spalten, `ProgramRuleValidityInput`-Value-Object, EF-Migration `AddProgramRuleValidity` — Schritte 1, 3, 5, 8 | `CreateAndUpdateRule_ValidityFieldsPersist`, Persistenz-Assertions in `ProgramRuleServiceTests_Crud` | Abgedeckt |
| Uneingeschränkt = Standard, abwärtskompatibel für Bestandsdaten und Alt-Backups | `Unrestricted = 0`, `IsRequired().HasDefaultValue(Unrestricted)`, Migrations-Default 0, `OptionalRestoreIntDefaults`-Eintrag | `IsValidOn_Unrestricted_AlwaysTrue`, `LegacyBackupWithoutValidityColumns_RestoresRulesAsUnrestricted` | Abgedeckt |
| Einmaliger Zeitraum Von–Bis, inklusive Grenzen, einseitig offene Grenzen erlaubt | `OneTime`: `ValidFrom ≤ day ≤ ValidTo`, jede Grenze einzeln optional, mindestens eine Pflicht, `ValidFrom ≤ ValidTo` | `IsValidOn_OneTime_*`, `CreateRule_OneTime*_Throws`/`_OpenBound_IsAllowed`, `EnsureCoverage_OneTimeRule_*`, E2E-Pflicht-Test (Z. 444) | Abgedeckt |
| Jährlich wiederkehrendes Monat-Tag-Fenster inkl. Jahreswechsel-Wrap | `AnnualWindow`: Monat-Tag-Ordinal `Monat*100+Tag`, `Von > Bis` = Wrap; Speicherung als `DateOnly` im Konventionsjahr 2000 | `IsValidOn_Annual_*`, `EnsureCoverage_AnnualRule_*`, E2E-Test Z. 449 (Dialog + Edit-Roundtrip) | Abgedeckt |
| Monatlich wiederkehrendes Tag-im-Monat-Fenster inkl. Monatswechsel-Wrap | `MonthlyWindow`: `ValidFromDay`/`ValidToDay` 1–31, `FromDay > ToDay` = Wrap, Tag 31 entfällt in kürzeren Monaten | `IsValidOn_Monthly_*`, `EnsureCoverage_MonthlyRule_*`, E2E-Test Z. 450 (Dialog + Edit-Roundtrip) | Abgedeckt |
| Wöchentliche Wochentags-Mehrfachauswahl | `WeeklyWindow`: `ValidWeekdays`-Bitmaske über `DayOfWeek` 0–6, Pflicht 1–127 | `IsValidOn_Weekly_*`, `CreateRule_Weekly_*_Throws`, `EnsureCoverage_WeeklyRule_*`, E2E-Test Z. 447 | Abgedeckt |
| Feiertagsfenster relativ zu benanntem Anker, ohne neue NuGet-Abhängigkeit | `HolidayWindow` + `ProgramRuleHolidayAnchor` (12 bundeslandunabhängige Anker), `GermanHolidayCalendar.GetAnchorDate` auf vorhandenem Computus (verifiziert: `EasterSunday` privat Z. 85, `RepentanceDay`-Muster Z. 76), Fenster `day.Year ± 1`, Offsets 0–62 | `IsValidOn_Holiday_*`, `GetAnchorDate_ReturnsExpectedDates`, `CreateRule_Holiday_*_Throws`, `EnsureCoverage_HolidayRule_*`, E2E-Test Z. 448 | Abgedeckt |
| Außerhalb der Gültigkeit keine Sendeplätze — für alle `ProgramRuleType`s inkl. `Filler` | `IsValidOn`-Check in `BuildOccurrences` (verifiziert: `applies`-Switch wertet nur `Recurrence`, Z. 1626–1635) + eigener Filler-Check in `FillGaps` pro Lückenposition | `EnsureCoverage_*_SchedulesOnlyWithinWindow` je Modus, `EnsureCoverage_ExpiredOneTimeRule_ProducesNothing`, `EnsureCoverage_InvalidFiller_FillsOnlyWithinWindow`, `Reconcile_RuleLeftWindow_DisplacesFutureAirings` | Abgedeckt |
| Abgelaufene einmalige Regel wird automatisch gelöscht — nur im Worker-Lauf hinter `TvSenderEnabled` | `PurgeExpiredRulesAsync(utcNow, ct)` am `IProgramScheduleService`, `ProgramScheduleWorker.ExecuteAsync` ruft sie im Enabled-Block vor `ReconcileAsync` (verifiziert: Z. 61–72 hinter `IBackgroundProcessingGate`); explizit „kein Löschen beim Laden der Liste" | `PurgeExpiredRules_*` (3 Tests, echtes SQLite via `ProgramScheduleTestDb`), `Worker_Run_PurgesExpiredRules`/`_DisabledFeature_SkipsPurge`, E2E-Pflicht-Test über „Sendeplan jetzt erzeugen" | Abgedeckt |
| Geplante Sendeplätze der gelöschten/ungültigen Regel werden verdrängt, Historie bleibt | `ProgramSlot.ProgramRuleId`-Soft-Referenz (verifiziert: kein FK); folgendes `ReconcileAsync` setzt `Displaced` über bestehenden Pfad `rule is null` (Z. 1117); FK-Kaskade für `ProgramRuleCursor`/Join-Zeilen | `PurgeExpiredRules_SlotsDisplacedAfterReconcile`, `Reconcile_RuleLeftWindow_DisplacesFutureAirings` | Abgedeckt |
| Ungültige wiederkehrende Regeln bleiben erhalten und sortieren ans Ende | `GetRulesAsync`: in-memory stabile Gruppierung `IsValidOn(rule, localToday)` nach `OrderBy(Name)` (verifiziert: bisher einzige Sortierung Z. 91; UI rendert Lieferreihenfolge) — auf alle aktuell ungültigen Regeln ausgeweitet (Anwender-Entscheidung) | `GetRulesAsync_InvalidRule_SortsLast`, E2E-Pflicht-Test (End-Sortierung + Chip + Fenstertext) | Abgedeckt |
| Kennzeichnung des Gültigkeitsstatus in der Regelauflistung | Warn-Chip „Derzeit ungültig" (`admin-chip-warning`, verifiziert `app.css` Z. 509 / `rule-panel-badges` TVSenderAdmin.razor Z. 133) + `ValidityLabel`-Metazeile je Modus (Muster `RuleMetaLabel`/`DiagnosisLabel`) | E2E-Pflicht-Test prüft Chip + Fenstertext in der realen Liste | Abgedeckt |
| Serverseitige Durchsetzung/Validierung (kein reines UI-Verstecken) | `ValidateRuleInputAsync`: `Enum.IsDefined` für Modus und Anker, modusabhängige Pflicht-/Wertregeln, Normalisierung nicht moduspassender Felder auf `null` — hinter bestehendem `RequireAdminAsync` | `ProgramRuleServiceTests_Validation`: neue Tests je Modus inkl. Normalisierung; E2E-Fehlerfall (`rule-edit-error`, verifiziert TVSenderAdmin.razor Z. 380) | Abgedeckt |
| UI: Gültigkeitsauswahl und modusabhängige Eingaben im Regel-Editor | `rule-validity`-Select (6 Optionen) + konditionale Blöcke je Modus, `RuleEditModel`-Erweiterung, `StartCreate`/`StartEdit`/`SaveRuleAsync` (verifiziert: Z. 1884/1891/2053/2325) | E2E-Tests für alle sechs Modi (Einmalig/Jährlich/Monatlich/Wöchentlich/Feiertag) + Fehlerfall | Abgedeckt |
| DoD: EF-Migration, `VideoWebPlayerBackupData`, echte SQLite-Tests, Hilfe-Doku, Debug+Release-Build | Migration `AddProgramRuleValidity` (9 Spalten); `OptionalRestoreColumns` += 9, `OptionalRestoreIntDefaults` += `ValidityMode`; Purge-/Planungstests über `ProgramScheduleTestDb` (SQLite shared-memory, echte FKs); `docs/help/tv-sender*.md` in Schritt 13; DoD-Lauf Schritt 13/Task #44 | `LegacyBackupWithoutValidityColumns_RestoresRulesAsUnrestricted` via `LegacyBackupArchiveBuilder.RemoveColumnsAsync` | Abgedeckt |

## E2E-Abdeckung

| Benutzerfluss / Akzeptanzkriterium | Geplanter E2E-Test | Status |
|------------------------------------|--------------------|--------|
| Regel mit einmaligem Zeitraum anlegen (inkl. einseitig offener Grenzen) → persistiert + Meta-Label | Pflicht-Test `TVSenderAdminE2ETests` (Z. 444) | Abgedeckt |
| Aktuell ungültige Regel → End-Sortierung + „Derzeit ungültig"-Chip + Fenstertext | Pflicht-Test `TVSenderAdminE2ETests` (Z. 445) | Abgedeckt |
| Abgelaufene einmalige Regel nach „Sendeplan jetzt erzeugen" automatisch entfernt (Worker-Purge) | Pflicht-Test (Z. 446) — Mechanismus verifiziert (`GenerateNowAsync` Z. 1412 feuert `NotifyScheduleChanged`) | Abgedeckt |
| Jährliches Fenster über Dialog anlegen (Monats-`<select>` + Tagesfeld, Jahreswechsel-Wrap) + Edit-Roundtrip | Wichtig-Test `TVSenderAdminE2ETests` (Z. 449) — Lücke aus Runde 1 behoben | Abgedeckt |
| Monatliches Fenster über Dialog anlegen (Tagesfelder 1–31, Wrap) + Edit-Roundtrip | Wichtig-Test `TVSenderAdminE2ETests` (Z. 450) — Lücke aus Runde 1 behoben | Abgedeckt |
| Wöchentliche Gültigkeit über Checkbox-Gruppe anlegen | Wichtig-Test (Z. 447) | Abgedeckt |
| Feiertagsfenster über Anker-Select + Offsets anlegen | Wichtig-Test (Z. 448) | Abgedeckt |
| Validierungsfehler im Dialog (`OneTime` ohne Grenze / `ValidTo` vor `ValidFrom`) | Wichtig-Test (`rule-edit-error`, Z. 451) | Abgedeckt |
| Bestehender Dialog-Fluss bleibt stabil | `Admin_CreateEditDeactivateDeleteRule` (verifiziert `TVSenderAdminE2ETests.cs` Z. 84) auf Selektor-Stabilität prüfen | Abgedeckt |

## Hinweise

- Die E2E-Seed-Pfade für die Pflicht-Tests („Regel außerhalb des Fensters",
  „`ValidTo` gestern") setzen Gültigkeitsfelder direkt in der Datenbank.
  `TVSenderAdminE2ETestBase.SeedMovieRuleAsync` (Z. 141) kennt diese Felder
  noch nicht — der Plan nennt nur die analoge Erweiterung von
  `ProgramScheduleTestDb.CreateRuleAsync`. Bei der Umsetzung entweder den
  E2E-Seed-Helfer um Gültigkeitsparameter ergänzen oder die Felder per
  `Services.CreateScope()`/`ApplicationDbContext` im Test setzen (Muster
  `UpdateSetupAsync`, Z. 40–48). Reine Implementierungsdetail-Lücke, kein
  fehlender Testnachweis.
- Die bUnit-Komponententests `TVSenderAdminTests.cs` (u. a.
  `RulePanel_StatusBadge_BeforeName` Z. 389,
  `RulePanel_MetaLine_WithoutDuration_HasNoTrailingSeparator` Z. 417,
  `RulePanel_PriorityBadge_*` Z. 346/367) sind in „Betroffene bestehende
  Tests" nicht aufgeführt. Sie brechen voraussichtlich nicht (alle
  Assertionen sind `Contains`-basiert; geseedete Regeln bleiben `Unrestricted`
  → kein neuer Chip), sollten aber beim Umbau von `rule-panel-badges`/
  `rule-panel-meta` mitlaufen und ggf. in der Betroffenen-Tabelle nachgetragen
  werden.
- Alle im Plan genannten Code-Positionen wurden (erneut bzw. aus Runde 1
  übernommen) verifiziert und stimmen: `BuildOccurrences` Z. 1615,
  `FillGaps` Z. 1970, `ReconcileWindow` Z. 1090/1117, `EnsureCoverageAsync`
  Z. 83, `GetRuleDiagnosesAsync` Z. 424/447, `ProgramScheduleWorker` Z. 61–72
  (Enabled-Block hinter `IBackgroundProcessingGate`), `GetRulesAsync`
  `OrderBy(Name)` Z. 91, `IProgramRuleService`-Doku Z. 29–30,
  `GermanHolidayCalendar.EasterSunday` Z. 85 / `RepentanceDay` Z. 76,
  `admin-chip-warning` app.css Z. 509, `rule-panel-badges` app.css Z. 937 /
  TVSenderAdmin.razor Z. 133, `rule-edit-error` Z. 380,
  `Admin_CreateEditDeactivateDeleteRule` TVSenderAdminE2ETests.cs Z. 84,
  `SeedMovieRuleAsync` TVSenderAdminE2ETestBase.cs Z. 141 — kein neues
  NuGet-Paket nötig.
- Die End-Sortierung wird bewusst auf alle aktuell ungültigen Regeln
  ausgeweitet (Anwender-Entscheidung); die Anforderung nennt nur ungültige
  wiederkehrende Regeln — die Erweiterung deckt die Anforderung ab.
- `EnumContractTests` pinnen ProgramRule-Enums bisher nicht; die Aufnahme der
  neuen Enums ist laut Plan optional (im Seiteneffekt-Abschnitt korrekt
  benannt).
