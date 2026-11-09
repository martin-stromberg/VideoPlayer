# Übersetzte Anforderung: Sendeplan aus Regeln erzeugen (Schritt 3)

Quelle: `docs/projects/task/issue-111-6145c51699f34953bf29a244634a23e2-automatisierter-tvsender/project-plan.md`, Abschnitt „### Schritt 3: Sendeplan aus Regeln erzeugen" samt den Vorgehensentscheidungen in „Grobe Vorgehensentscheidungen".

## Fachliche Zusammenfassung

Das System erzeugt aus den aktiven `ProgramRule`-Datensätzen des `TVChannel` einen fortlaufenden, lückenlos belegten Sendeplan aus konkreten Sendeplätzen (`ProgramSlot`): jeder Sendeplatz hat `Start`, `End` und genau einen zugeordneten Inhalt (Film oder Serienepisode, aufgelöst bis zum konkreten `MediaItem`). Der Plan deckt rollierend einen konfigurierbaren Horizont (Standard ~48 Stunden) ab und wird von einem Hintergrund-Worker in konfigurierbarem Intervall ohne Benutzereingriff fortgeschrieben. Eingeplant werden nur Inhalte mit bekannter `MediaItem.Duration` und nur Inhalte, die für das gesamte berechtigte Publikum freigegeben sind.

## Akzeptanzkriterien (aus der Anforderung abgeleitet)

1. **Lückenloser Plan:** Für den Planungshorizont `[jetzt, jetzt + Horizont]` existiert zu jedem Zeitpunkt genau ein Sendeplatz (keine Lücken, keine Überlappungen).
2. **Regel-Auswertung je Wiederholungsmuster:** `Daily` → täglich zur `TimeOfDay`; `Weekly` → am konfigurierten `DayOfWeek`; `Once` → am `SingleDate`; `Holiday` → an Feiertagen des deutschen Feiertagskalenders unter Berücksichtigung des konfigurierten Bundeslands (`Setup.HolidayFederalState`, `null` = nur bundesweit).
3. **Serien-Fortsetzung:** Regeln vom Typ `SeriesFixedTime` schreiten je Ausstrahlung um `EpisodeCount` Episoden in Sendedreihenfolge (Staffel, dann `TVShowEpisode.Number`) fort; der Fortsetzungsstand wird je Regel geführt und überlebt einzelne Planungsläufe sowie Neustarts.
4. **Slot-Umfang:** Mit `EpisodeCount` werden n Folgen hintereinander eingeplant; mit `SlotDuration` wird Material eingeplant, bis die Ziellänge erreicht ist (mindestens ein Inhalt; der letzte Inhalt darf die Ziellänge überschreiten).
5. **Konfliktauflösung:** Überschneiden sich Sendeplatz-Fenster konkurrierender Regeln, gewinnt die höhere `Priority`; bei Gleichstand deterministisch (niedrigere `ProgramRule.Id`).
6. **Lückenfüllung:** Verbleibende Lücken werden aus dem Material der aktiven `Filler`-Regeln befüllt, fortlaufend je Regel-Cursor.
7. **Nur bekannte Laufzeit:** Nur Inhalte mit gesetztem `MediaItem.Duration` werden eingeplant; Inhalte ohne ermittelbare Dauer werden übersprungen (fachlich nachvollziehbar, z. B. Log).
8. **Nur vollständig freigegebene Inhalte:** Ein Inhalt ist nur planbar, wenn jeder Benutzer, für den der Sender zugänglich ist, ihn sehen kann (Quellenzugriff über `MediaSourceUser` oder Einzelfreischaltung über `UnlockedMediaEntry` — analog `IUnlockedMediaService.IsAccessible`). Nicht vollständig freigegebene Inhalte werden bereits bei der Planung ausgeschlossen.
9. **Rollierende Fortschreibung:** Ein Hintergrund-Worker schreibt den Plan im konfigurierten Intervall fort, sodass die Belegung den Horizont laufend abdeckt — ohne Anwender-Eingriff, auch über Anwendungsneustarts hinweg.
10. **Verwaiste Materialien:** Regeln, deren `MaterialId` nicht mehr existiert (bewusst kein DB-FK), werden bei der Expansion übersprungen; sie erzeugen weder Sendeplätze noch Fehler.
11. **Konfiguration:** Planungshorizont und Generierungsintervall sind über die etablierten Einstellungsmechanismen (`Setup` + `ProgramSettingsService`, Admin-UI auf der TV-Sender-Seite) konfigurierbar; das Bundesland ist bereits konfigurierbar.
12. **Datenhaltung:** Neue Tabellen/Spalten erhalten eine EF-Migration und Backup-Registrierung (`OptionalRestoreTables`/`OptionalRestoreColumns`) mit Alt-Backup-Regressionstest.
13. **Ein Sender:** Die Planung arbeitet kanalfähig (`TVChannel`-Bezug), ausgeliefert/bedient wird genau ein Sender.

## Betroffene Klassen und Komponenten

- **Datenmodell:** neu `ProgramSlot` (Sendeplatz), neu Fortsetzungsstand-Entität (z. B. `ProgramRuleCursor`), Erweiterung `Setup` um Horizont/Intervall; `ApplicationDbContext` um neue `DbSet`s; neue `IEntityTypeConfiguration`s; neue EF-Migration.
- **Logik/Services:** neu `IProgramScheduleService`/`ProgramScheduleService` (Planerzeugung, Material-Expansion, Konfliktauflösung, Lückenfüllung, Cursor-Führung), neu Feiertagskalender (z. B. `GermanHolidayCalendar`), neu `ProgramScheduleWorker` (`BackgroundService`) nach dem Muster `MediaDurationProbeWorker`/`PlaylistBackfillWorker` (Scope-Factory, `IBackgroundProcessingGate`, `TimeProvider`).
- **Konfiguration:** `ProgramSettingsService` (Getter/Updater für Horizont/Intervall), `TVSenderAdmin.razor` (Eingabefelder), ggf. `appsettings` für Worker-Defaults.
- **Backup:** `VideoWebPlayerBackupData` (`OptionalRestoreTables`, `OptionalRestoreColumns`).
- **Tests:** neue Testklassen für Planerzeugung, Feiertagskalender, Freigabe-Filter, Cursor-Fortschritt, Konflikte, Füller, Backup-Regression; Erweiterung bestehender E2E-/Settings-Tests.

## Implementierungsansatz

- Neuer Sendeplan-Service als scoped Service; Worker hält keinen eigenen DbContext (Scope-Factory-Muster wie `PlaylistBackfillCoordinator`/`MediaDurationProbeWorker`), hält Schreibzugriffe hinter `IBackgroundProcessingGate`.
- Regel → konkrete Ausstrahlungstermine innerhalb des Horizonts; Material je Regelart zu einer geordneten, abspielbaren Inhaltsliste expandieren (Serie → Episoden; Filmsammlung → Filme; Genre → Filme + Episoden; Playlist → spielbare `PlaylistEntry`s; Film → ein Film); verwaiste `MaterialId` → leere Liste.
- Inhalte ohne `Duration` oder ohne vollständige Freigabe werden beim Expandieren herausgefiltert.
- Konflikte zwischen Terminen werden vor dem Persistieren nach `Priority` (dann `Id`) aufgelöst; Lücken anschließend aus Füller-Material gefüllt.
- Fortschritts-Cursor wird je Regel persistiert, damit Serien-/Rotationsfortschritt neustart- und regenerationsfest ist.

## Konfiguration

Anwendungseinstellungen (persistiert in `Setup`, verwaltet über `ProgramSettingsService`, Admin-UI auf `/admin/tv-sender`): `ScheduleHorizonHours` (Standard 48) und `ScheduleGenerationIntervalMinutes` (Standard 15). `HolidayFederalState` existiert bereits.

## Offene Fragen

Keine — die Vorgehensentscheidungen im Projektplan sind verbindlich; verbleibende Interpretationsdetails (exakte Semantik „vollständig freigegeben", Rotationsverhalten bei erschöpftem Material, Umgang bei Regel-Konfliktverlierern) werden im Umsetzungsplan als Designentscheidungen festgelegt.
