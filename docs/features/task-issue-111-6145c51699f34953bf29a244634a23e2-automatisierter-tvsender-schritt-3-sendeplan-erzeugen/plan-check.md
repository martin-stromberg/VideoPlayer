# Plan-Gegenprüfung

## Ergebnis

**Status:** Plan vollständig

## Abgleich Akzeptanzkriterien

| Akzeptanzkriterium | Umsetzung im Plan | Testnachweis im Plan | Status |
|--------------------|-------------------|----------------------|--------|
| Lückenloser Plan über den Horizont | `ProgramSlot`-Erzeugung + Lückenfüllung, Abschnitt „Programmabläufe" | `EnsureCoverage_PlanIsGapless_WhenFillerExists`, `OnlyFillerRule_FillsEntireHorizon` | Abgedeckt |
| Wiederholungsmuster Daily/Weekly/Once/Holiday | Termin-Erzeugung + `GermanHolidayCalendar` + `HolidayFederalState` | `Weekly/Once/Holiday`-Tests, `GermanHolidayCalendarTests` | Abgedeckt |
| Serien-Fortsetzung in Sendedreihenfolge, je Regel | `ProgramRuleCursor`, Expansion „Staffel-Id → Episode.Number" | `EnsureCoverage_SeriesRule_AdvancesEpisodesInAirOrder`, `..._WrapsAround` | Abgedeckt |
| Umfang: Folgenanzahl bzw. Länge | Materialisierungsregel `EpisodeCount`/`SlotDuration` | `EnsureCoverage_SlotDuration_FillsUntilTarget` | Abgedeckt |
| Prioritäts-Konfliktauflösung | Sortierung `(Priority desc, Id asc, Start asc)`, Komplett-Entfall des Verlierers | `ProgramScheduleServiceTests_Conflicts` | Abgedeckt |
| Lückenfüllung per Füller-Regeln | Round-Robin über Füller-Regeln, fit-entirely | `ProgramScheduleServiceTests_Filler` | Abgedeckt |
| Nur bekannte Laufzeit | Filter `Duration != null` in Expansion | `EnsureCoverage_ItemWithoutDuration_IsSkipped` | Abgedeckt |
| Nur vollständig freigegebene Inhalte | Freigabe-Filter (alle Nicht-Admins; Quelle ODER Freischaltung) | `ProgramScheduleServiceTests_Access` | Abgedeckt |
| Rollierende Fortschreibung ohne Eingriff | `ProgramScheduleWorker` + Extend-only `EnsureCoverageAsync`, neustartfest | `EnsureCoverage_SecondRun_ExtendsNotDuplicates`, `ProgramScheduleWorkerTests` | Abgedeckt |
| Verwaiste Materialien fachlich behandeln | Expansion prüft Existenz, leere Liste + Log | `EnsureCoverage_OrphanedMaterial_ProducesNothing` | Abgedeckt |
| Konfiguration über etablierte Mechanismen | `Setup`-Spalten + `ProgramSettingsService` + Admin-UI-Felder | Settings-Tests + erweiterter E2E | Abgedeckt |
| Migration + Backup-Registrierung + Regression | `AddProgramSchedule`, `OptionalRestore*` | Alt-Backup-Regressionstest | Abgedeckt |
| Ein Sender, kanalfähiges Modell | `TVChannelId` am `ProgramSlot`, `GetOrCreateChannelAsync` | implizit in allen Generation-Tests | Abgedeckt |

## Fehlende oder unvollständige Testanforderungen

— (Status ist „Plan vollständig".)

## E2E-Abdeckung

| Benutzerfluss / Akzeptanzkriterium | Geplanter E2E-Test | Status |
|------------------------------------|--------------------|--------|
| Horizont/Intervall über Admin-UI konfigurieren | Erweiterung `TVSenderAdminE2ETests.Admin_CreateEditDeactivateDeleteRule` | Abgedeckt |
| Planerzeugung selbst | Kein Nutzerfluss (Hintergrund-Service ohne UI) | Nicht erforderlich — Service-Tests gegen echtes SQLite tragen den Nachweis |
| Nicht-Admin-Gate | `NonAdmin_SeesNotAuthorized` (bestehend) | Abgedeckt |

## Fehlende oder unvollständige Planbestandteile

— (Status ist „Plan vollständig".)

## Hinweise

- Die Zugriffsregel „vollständig freigegeben" ist im Plan als Designentscheidung präzisiert (alle Nicht-Admin-Benutzer; Quellenzugriff ODER Einzelfreischaltung; ohne Nicht-Admins alles freigegeben) — eine bewusst konservative, serverseitig prüfbare Auslegung.
- Extend-only-Planung: Reaktion auf Regel-Änderungen (Verdrängen, Wieder-Einplanen ausgelassener Folgen) ist bewusst Schritt 4 und kein Bestandteil dieses Plans.
