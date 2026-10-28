# Tasks: Gültigkeitszeitraum für Programmregeln

| # | Bereich | Aufgabe | Status | Testnachweis |
|---|---------|---------|--------|--------------|
| 1 | Datenmodell | Enum `ProgramRuleValidityMode` anlegen (`Unrestricted = 0`, `OneTime = 1`, `AnnualWindow = 2`, `MonthlyWindow = 3`, `WeeklyWindow = 4`, `HolidayWindow = 5`) | Offen | — |
| 2 | Datenmodell | Enum `ProgramRuleHolidayAnchor` anlegen (`NewYear`, `GoodFriday`, `EasterSunday`, `EasterMonday`, `AscensionDay`, `PentecostSunday`, `PentecostMonday`, `FirstAdvent`, `ChristmasEve`, `ChristmasDay`, `SecondChristmasDay`, `NewYearsEve`) | Offen | — |
| 3 | Datenmodell | `ProgramRule` um `ValidityMode` und acht Fensterfelder erweitern (`ValidFrom`/`ValidTo` `DateOnly?`, `ValidFromDay`/`ValidToDay` `int?`, `ValidWeekdays` `int?`, `ValidHolidayAnchor`, `ValidHolidayBeforeDays`/`ValidHolidayAfterDays` `int?`) | Offen | — |
| 4 | Datenmodell | `ProgramRuleConfiguration`: `ValidityMode` required + Default `Unrestricted`, alle Fensterfelder optional | Offen | — |
| 5 | Datenmodell | EF-Migration `AddProgramRuleValidity` erzeugen (neun neue `ProgramRules`-Spalten; `dotnet ef migrations add`) | Offen | — |
| 6 | Logik | `GermanHolidayCalendar` um `GetAnchorDate(ProgramRuleHolidayAnchor, int year)` erweitern (+ private `FirstAdvent`-Berechnung auf vorhandenem Computus) | Offen | — |
| 7 | Logik | Statische Hilfsklasse `ProgramRuleValidity` anlegen (`IsValidOn` je Modus inkl. offener Grenzen, Jahres-/Monats-Wrap, Wochentags-Bitmask, Feiertagsfenster `day.Year ± 1`; `IsExpired` nur bei überschrittenem `ValidTo`) | Offen | — |
| 8 | Logik | Record `ProgramRuleValidityInput` (Value Object: Modus + alle Fensterfelder) anlegen; `ProgramRuleInput` um optionalen Parameter `Validity` erweitern | Offen | — |
| 9 | Validierung | `ValidateRuleInputAsync`: `Enum.IsDefined` für Modus und Anker, modusabhängige Pflicht-/Wertregeln (OneTime: ≥1 Grenze + `From ≤ To`; Monthly: Tage 1–31; Weekly: Maske 1–127; Holiday: Offsets 0–62), Normalisierung nicht moduspassender Felder auf `null` | Offen | — |
| 10 | Logik | `CreateRuleAsync`/`UpdateRuleAsync`: Gültigkeitsfelder moduspassend persistieren | Offen | — |
| 11 | Logik | `GetRulesAsync`: stabile Gruppierung aktuell gültiger vor ungültigen Regeln (`IsValidOn`, lokaler Tag) | Offen | — |
| 12 | Logik | `IProgramRuleService.GetRulesAsync`-Dokumentationskommentar an die neue Sortierung anpassen (bisher „ordered by name": jetzt gültige Regeln nach Name, aktuell ungültige ans Ende) | Offen | — |
| 13 | Logik | `ProgramScheduleService.BuildOccurrences`: `ProgramRuleValidity.IsValidOn` pro Regel/Tag einbauen | Offen | — |
| 14 | Logik | `ProgramScheduleService.FillGaps`: Filler nur an gültigen Tagen wählen | Offen | — |
| 15 | Logik | `IProgramScheduleService.PurgeExpiredRulesAsync` + Implementierung in `ProgramScheduleService` (lokaler Tag, `RemoveRange`, Log, `Task<int>`; nur `OneTime` mit `ValidTo < localDay`) | Offen | — |
| 16 | Logik | `ProgramScheduleWorker.ExecuteAsync`: Purge-Aufruf vor `ReconcileAsync` im `TvSenderEnabled`-Block | Offen | — |
| 17 | Backup | `VideoWebPlayerBackupData.OptionalRestoreColumns`: neun neue `ProgramRules`-Spalten eintragen | Offen | — |
| 18 | Backup | `OptionalRestoreIntDefaults`: `ProgramRules.ValidityMode` → `Unrestricted` | Offen | — |
| 19 | UI | `RuleEditModel` um alle Gültigkeitsfelder erweitern (`ValidityMode`, `ValidFrom`/`ValidTo` `DateTime?`, Jahresfenster-Monats-/Tagesfelder, `ValidMonthlyFromDay`/`ValidMonthlyToDay`, `SelectedWeekdays`, `ValidHolidayAnchor`, `ValidHolidayBeforeDays`/`ValidHolidayAfterDays`) | Offen | — |
| 20 | UI | Dialog: `rule-validity`-Select mit sechs Optionen + `OnValidityModeChanged` + konditionale Eingabeblöcke (`InputDate` bei einmalig; Monats-Select + Tagesfeld bei jährlich; Tagesfelder bei monatlich; Wochentags-Checkboxen bei wöchentlich; Anker-Select + Offset-Felder bei Feiertagsfenster) | Offen | — |
| 21 | UI | `StartCreate`/`StartEdit`: Gültigkeitsfelder initialisieren bzw. aus Regel übernehmen | Offen | — |
| 22 | UI | `SaveRuleAsync`: `ProgramRuleValidityInput` befüllen (Jahresfenster → `DateOnly` im Konventionsjahr 2000 mit `DaysInMonth`-Prüfung; Monatstage 1–31; Wochentags-Maske; Offsets 0–62 → sonst `editError`) | Offen | — |
| 23 | UI | `ValidityLabel(rule)` + `HolidayAnchorLabel` + Warn-Chip „Derzeit ungültig" in der Regelzeile (`rule-panel-meta`/`rule-panel-badges`) | Offen | — |
| 24 | Tests | `ProgramScheduleTestDb.CreateRuleAsync` um optionale Gültigkeitsparameter erweitern | Offen | — |
| 25 | Tests | `ProgramRuleValidityTests`: alle Modi — Grenzen inklusiv, offene Grenzen, Jahres-/Monats-Wrap, fehlender Monatstag, Wochentags-Maske, Feiertagsfenster inkl. Jahreswechsel, `IsExpired` | Offen | — |
| 26 | Tests | `GermanHolidayCalendarTests`: `GetAnchorDate` für bekannte Ankerdaten (Ostern, Pfingsten, 1. Advent, feste Anker) | Offen | — |
| 27 | Tests | `ProgramRuleServiceTests_Validation`: Modus-/Pflicht-/Wert-Validierung je Modus inkl. offener Grenzen und Normalisierung | Offen | — |
| 28 | Tests | `ProgramRuleServiceTests_Crud`: Persistenz der Felder (alle Modi) + `GetRulesAsync`-End-Sortierung | Offen | — |
| 29 | Tests | `ProgramScheduleServiceTests_Generation`: Occurrences nur im Fenster (einmalig inkl. offen, jährlich, monatlich, wöchentlich, Feiertagsfenster, abgelaufen, Filler-Fenster) | Offen | — |
| 30 | Tests | `ProgramScheduleServiceTests_Reconcile`: Regel außerhalb des Fensters → künftige Airings `Displaced` | Offen | — |
| 31 | Tests | `ProgramScheduleServiceTests_Purge` (neu): Löschung abgelaufener Regeln, FK-Kaskade der Cursor, Slots `Displaced` nach Reconcile, offene/wiederkehrende Regeln bleiben — gegen echtes SQLite | Offen | — |
| 32 | Tests | `ProgramScheduleWorkerTests`: Purge im Enabled-Block bzw. nicht bei deaktiviertem Flag | Offen | — |
| 33 | Tests | Alt-Backup-Regressionstest: `LegacyBackupArchiveBuilder.RemoveColumnsAsync` ohne die neun Spalten → Regeln restaurieren als `Unrestricted`/NULL | Offen | — |
| 34 | E2E-Tests | `TVSenderAdminE2ETests`: Regel mit einmaligem Zeitraum (inkl. einseitig offenem Fenster) über den Dialog anlegen, Meta-Label prüfen | Offen | — |
| 35 | E2E-Tests | `TVSenderAdminE2ETests`: wiederkehrende Regel außerhalb des Fensters → End-Sortierung + „Derzeit ungültig"-Chip + Fenstertext | Offen | — |
| 36 | E2E-Tests | Abgelaufene einmalige Regel verschwindet nach „Sendeplan jetzt erzeugen" (Worker-Purge) | Offen | — |
| 37 | E2E-Tests | Wochentags-Gültigkeit über Checkbox-Gruppe anlegen, Meta-Label prüfen | Offen | — |
| 38 | E2E-Tests | Feiertagsfenster über Anker-Select + Offsets anlegen, Meta-Label prüfen | Offen | — |
| 39 | E2E-Tests | Dialog-Fehlerfall: `OneTime` ohne Grenze bzw. `ValidTo < ValidFrom` → `rule-edit-error` | Offen | — |
| 40 | E2E-Tests | `TVSenderAdminE2ETests`: Regel mit jährlichem Fenster über den Dialog anlegen (Monats-Select + Tagesfeld je Grenze, Jahreswechsel-Wrap), Meta-Label prüfen; Edit-Roundtrip (`StartEdit`-Aufteilung Monat/Tag aus Konventionsjahr 2000) mitprüfen | Offen | — |
| 41 | E2E-Tests | `TVSenderAdminE2ETests`: Regel mit monatlichem Fenster über den Dialog anlegen (Tagesfelder 1–31 inkl. Wrap-Eingabe), Meta-Label prüfen; Edit-Roundtrip (`StartEdit` befüllt Tagesfelder) mitprüfen | Offen | — |
| 42 | E2E-Tests | `Admin_CreateEditDeactivateDeleteRule` auf Selektor-Stabilität nach Dialog-Umbau prüfen/anpassen | Offen | — |
| 43 | Dokumentation | `docs/help/tv-sender.md`, `tv-sender-datenmodell.md`, `tv-sender-ablauf-technisch.md` um Gültigkeit (alle Modi, Feiertags-Anker, Bereinigung) ergänzen; `README.md`/Release Notes bei Bedarf | Offen | — |
| 44 | Abschluss | DoD-Lauf: `dotnet build VideoWebPlayer.sln` Debug + Release, volle `dotnet test`-Suite grün | Offen | — |
