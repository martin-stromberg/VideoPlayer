# Tasks: TV-Sender — altersgerechte Sendezeiten (Jugendschutzfenster, Issue #271)

| # | Bereich | Aufgabe | Status | Testnachweis |
|---|---------|---------|--------|--------------|
| 1 | Datenmodell | `ProgramScheduleUnknownRatingMode`-Enum unter `VideoWebPlayer/Data/` anlegen (`Unrestricted`, `RestrictToNightWindow`; Muster `ProgramScheduleContentScope`) | Offen | — |
| 2 | Datenmodell | `ProgramScheduleAgeWindowSettings`-Record unter `VideoWebPlayer/Services/` anlegen (vier `TimeOnly`-Werte FSK 12/16/18 + Nachtende, `Default`-Instanz) | Offen | — |
| 3 | Logik | `ProgramScheduleAgeWindow` (static) unter `VideoWebPlayer/Services/` anlegen: `IsAllowedAt(minAge, utcStart, settings, mode)`, Stufen-Zuordnung (größte Stufe ≤ `MinAge`), Nachtfenster über Tageswechsel, Diagnose-Zählung blockierter Kandidaten | Offen | — |
| 4 | Datenmodell | `Setup`: `ProgramScheduleAgeWindow12Local`/`16Local`/`18Local`/`NightEndLocal` (`TimeOnly`, Defaults 20:00/22:00/23:00/06:00) + `ProgramScheduleUnknownRatingMode` ergänzen | Offen | — |
| 5 | Migration | EF-Migration `AddProgramScheduleAgeWindows` erzeugen (fünf neue `Setups`-Spalten) | Offen | — |
| 6 | Backup | `VideoWebPlayerBackupData.OptionalRestoreColumns` um die fünf neuen `Setups.*`-Spalten erweitern | Offen | — |
| 7 | Einstellungen | `ProgramSettingsService`: `GetProgramScheduleAgeWindowsAsync` (Normalisierung auf Default) + `UpdateProgramScheduleAgeWindowsAsync` (Validierung: monotone Stufen, `NightEnd` vor FSK-12-Start) | Offen | — |
| 8 | Einstellungen | `ProgramSettingsService`: `GetProgramScheduleUnknownRatingModeAsync` + `UpdateProgramScheduleUnknownRatingModeAsync` (`Enum.IsDefined`-Muster) | Offen | — |
| 9 | Logik | `SchedulableItem`-Record um `AgeRatingMinAge` (`int?`) erweitern | Offen | — |
| 10 | Logik | `TryMovieItem`/`ToItem` + `ExpandMovieAsync`/`ExpandMovieCollectionAsync`: `Movie.AgeRatingMinAge` in die Projektion übernehmen | Offen | — |
| 11 | Logik | `ExpandShowAsync`/`ExpandGenreAsync`/`ExpandPlaylistAsync`: `TVShow.AgeRatingMinAge` für Episoden-Items über Season-/Show-Join mitladen | Offen | — |
| 12 | Logik | `StrandState`/`RuleState`: `PreservesOrder` aus `ContentType` ableiten (`TVShowEpisode`/`PlaylistEntry` geordnet) + Scan-basierte Commit-Variante mit zwei Eingängen (`NextIndex` hinter letzte gescannte Position; `LastPlaced` explizit = letztes *genommenes* Item, nie eine übersprungene Position — sonst konsumiert `NextIndexFor` einen nie gesendeten Titel) | Offen | — |
| 13 | Logik | `SelectAiringItems`: Fensterprüfung pro `slotStart` (lokal via `TimeZoneInfo.Local`), ungeordnet ⇒ Ring-Skip (`ring++` ohne `selected++`), geordnet ⇒ `break`; Scan-Umfang + Scan-Trace (Ringposition je genommenem Item) in die Rückgabe aufnehmen | Offen | — |
| 14 | Logik | `SelectAiringItemsInterleaved`: Fensterprüfung je Strang (geordnet ⇒ `done[s]`, ungeordneter „other"-Strang ⇒ `skip[s]`-Offset, voller Umlauf ohne Zulässiges ⇒ `done[s]`) | Offen | — |
| 15 | Logik | `SelectItems`/`CommitTaken`: Fensterparameter + Scan-Umfang je Strang + Scan-Trace durchreichen, Commit auf neue Semantik umstellen | Offen | — |
| 16 | Logik | `RewindCursor`/`RewindRing`: Semantik unter Scan-Commit verifizieren (mit-konsumierte Skip-Positionen hinter dem Rewind-Punkt werden erneut angeboten — definiert, keine Codeänderung erwartet; ggf. Kommentar im Code präzisieren) | Offen | — |
| 17 | Logik | `MaterializeOccurrence`/`MaterializeOccurrenceEvicting`: Fenster-Einstellungen durchreichen | Offen | — |
| 18 | Logik | `EnsureCoverageAsync`/`ReconcileAsync`/`GetRuleDiagnosesAsync`: Fenster-Einstellungen einmal pro Lauf via `_settings` laden | Offen | — |
| 19 | Logik | `FillGaps`: Füller-Items gegen `fillAt` prüfen, unzulässige per `PeekAt(offset)` weitersuchen | Offen | — |
| 20 | Logik | `ClipFillContext` + `NextClipItem`/`FillAiringSegments`: Fensterprüfung gegen `pos` ins `fits`-Prädikat aufnehmen (Clips/Trailer ⇒ `AgeRatingMinAge = null` ⇒ Unbekannt-Modus) | Offen | — |
| 21 | Logik | `ReproContext` + `AiringReproducible`/`FlatAiringTailReproducible`/`InterleavedAiringTailReproducible`: identische Fenster-Re-Simulation einbauen | Offen | — |
| 22 | Logik | Lückentoleranz in den Tail-Checks: `InterleavedAiringTailReproducible` — Kontiguität für ungeordnete Stränge lockern (Zwischenpositionen = übersprungen, wenn zum Pick-Zeitpunkt unzulässig), Kopf-Rekonstruktion auf Scan-Umfang; `FlatAiringTailReproducible` — „genommene Items zurück" von „Ringpositionen zurück" trennen (Skip-Positionen: keine Dauer, zählen nicht als genommen) | Offen | — |
| 23 | Diagnose | `SummaryCounters.BlockedByAgeWindow`-Zähler ergänzen + in `ProgramScheduleRunSummary`/`ProgramScheduleReconcileSummary` übernehmen (Log-Sichtbarkeit) | Offen | — |
| 24 | Diagnose | `ProgramRuleDiagnosis`: `AgeWindowBlockedCount` (`int`) ergänzen + in `GetRuleDiagnosesAsync` befüllen (Fenster-Zulässigkeit gegen lokale `rule.TimeOfDay`-Startzeit) | Offen | — |
| 25 | UI | `TVSenderAdmin.razor` Planungs-Karte: vier Zeit-Eingabefelder (`tvsender-agewindow-12`/`-16`/`-18`/`-nightend`) + Modus-`select` (Muster `tvsender-content-scope`), Laden beim Init, Speichern in `SaveChannelSettingsAsync` | Offen | — |
| 26 | UI | `DiagnosisLabel`: Grund „nicht altersgerecht im Sendefenster" bei `AgeWindowBlockedCount > 0` ausgeben | Offen | — |
| 27 | Tests | `ProgramScheduleTestDb`: `CreateMovieAsync`/`CreateShowAsync` um `ageRatingMinAge`-Parameter erweitern; Setup-Seed explizit auf `ProgramScheduleUnknownRatingMode = Unrestricted` setzen (oder `MinAge 0` als Seed-Default), da der produktive Default `RestrictToNightWindow` alle freigabelosen Seeds ins Nachtfenster schieben würde — so bleiben die 2526 bestehenden Tests planbar | Offen | — |
| 28 | Tests | `ProgramScheduleAgeWindowTests` (Unit): `IsAllowedAt` — Stufen-Zuordnung 0/6/8/12/13/16/17/18, Nachtfenster über Tageswechsel, `null` je Modus | Offen | — |
| 29 | Tests | `ProgramScheduleServiceTests_AgeWindow` (echtes SQLite): gemischte Freigaben über Fenstergrenzen — Tages-Occurrence plant nur zulässige Titel, FSK-16-Titel ab 22:00 | Offen | — |
| 30 | Tests | `ProgramScheduleServiceTests_AgeWindow`: MPAA-Mapping (`R`→17 ≙ 22:00, `PG-13`→13 ≙ 20:00, `NC-17`→18 ≙ 23:00) | Offen | — |
| 31 | Tests | `ProgramScheduleServiceTests_AgeWindow`: Unbekannt-Modus — `RestrictToNightWindow` nur 23:00–06:00, `Unrestricted` jederzeit | Offen | — |
| 32 | Tests | `ProgramScheduleServiceTests_AgeWindow`: Cursor — übersprungener Titel wird im nächsten zulässigen Fenster erneut angeboten, keine Verklemmung, Plan bleibt lückenlos | Offen | — |
| 33 | Tests | `ProgramScheduleServiceTests_AgeWindow`: geordneter Serienstrang (flach) — blockierter Kopf deferred, Episodenreihenfolge bleibt strikt | Offen | — |
| 34 | Tests | `ProgramScheduleServiceTests_AgeWindow`: Interleaved-Skip — Genre-Regel mit `other`-Strang, FSK-18-Kopf-Film wird zu Tageszeit übersprungen (`skip[s]`), Fenster füllt sich mit zulässigem Material, kein Klemmen, kein Out-of-Window-Slot; Film kommt frühestens im Nachtfenster/nächstem Umlauf | Offen | — |
| 35 | Tests | `ProgramScheduleServiceTests_AgeWindow`: Interleaved-Defer — geordneter Show-Strang mit FSK-18-Kopf-Episode ruht (`done[s]`), Episodenreihenfolge und Strang-Cursor unverändert; nach Fenster-Öffnung wird die Episode genommen | Offen | — |
| 36 | Tests | `ProgramScheduleServiceTests_AgeWindow`: Interleaved-Reproduzierbarkeit — Airing mit Fenster-Skip im `other`-Strang bleibt bei unveränderten Einstellungen reproduzierbar (`AssertNoDisplacedAsync`); nach Fenster-Verschärfung wird es `Displaced` + rematerialisiert | Offen | — |
| 37 | Tests | `ProgramScheduleServiceTests_AgeWindow`: Rewind — ungeordneter Ring mit Skip-Position hinter dem displaced Index: displaced Titel werden in Ring-Reihenfolge wieder ausgestrahlt, übersprungene Position wird erneut geprüft und erscheint nur im zulässigen Fenster | Offen | — |
| 38 | Tests | `ProgramScheduleServiceTests_AgeWindow`: Tages- **und** Wochenübergang — begonnener Beitrag läuft über Fenstergrenze zu Ende; 05:30 erlaubt, 06:30 blockiert; explizit eine `Weekly`-Regel über der Grenze | Offen | — |
| 39 | Tests | `ProgramScheduleServiceTests_AgeWindow`: `FillGaps`/Clip-Fill respektiert Fenster und Unbekannt-Modus | Offen | — |
| 40 | Tests | `ProgramScheduleServiceTests_AgeWindow`: Reconcile — geänderte Fenster-Einstellung displaced verstoßende Slots und rematerialisiert; unverändert stabil (`AssertNoDisplacedAsync`) | Offen | — |
| 41 | Tests | `ProgramScheduleServiceTests_Diagnosis` erweitern: Tages-Regel mit ausschließlich hoch eingestuftem Material ⇒ `AgeWindowBlockedCount > 0` | Offen | — |
| 42 | Tests | `ProgramSettingsServiceTests_AgeWindow`: Get/Update-Roundtrip, Default-Normalisierung korrumpierter Werte, Validierungsfehler (Reihenfolge, `NightEnd`, `Enum.IsDefined`) | Offen | — |
| 43 | Tests | `VideoWebPlayerBackupDataTests` erweitern: Alt-Backup ohne die fünf neuen `Setups`-Spalten restauriert fehlerfrei (`LegacyBackupArchiveBuilder.RemoveColumnsAsync`) | Offen | — |
| 44 | Tests | `TVSenderAdminTests*` (Komponententest): `DiagnosisLabel` zeigt neuen Grund; `ProgramRuleDiagnosis`-Konstruktionsstellen an neuen Parameter anpassen | Offen | — |
| 45 | E2E-Tests | `TVSenderAdminE2ETests`: `Admin_AgeWindows_SetAndPersist` — Fensterzeiten setzen, speichern, Reload | Offen | — |
| 46 | E2E-Tests | `TVSenderAdminE2ETests`: `Admin_UnknownRatingMode_SelectAndPersist` — Modus-Select persistieren | Offen | — |
| 47 | E2E-Tests | `TVSenderAdminE2ETests`: `Admin_RuleDiagnosis_ShowsAgeWindowReason` — Diagnose-Grund in Regel-Zeile sichtbar | Offen | — |
| 48 | E2E-Tests | `TVSenderAdminE2ETests`: `Admin_AgeWindows_InvalidInput_ShowsError` — nicht monotone Fensterzeiten ⇒ Fehlermeldung | Offen | — |
| 49 | E2E-Tests | `TVSenderAdminE2ETests`: `Admin_AgeWindowedContent_ScheduledAtNight` — FSK-18-Film erscheint im Sendeplan nur im Nachtfenster (Pflicht — einziger UI-End-to-End-Nachweis für AC 1) | Offen | — |
| 50 | Dokumentation | `docs/help/tv-sender.md`: Fensterschema, Konfiguration, Unbekannt-Modus inkl. Konsequenz für Clips/Füller und Eingrenzung auf materialisierte Sendeplätze (kein Standby), Restlücken-Hinweis, Diagnosegrund | Offen | — |
| 51 | Dokumentation | `docs/help/tv-sender-datenmodell.md` + `docs/help/tv-sender-ablauf-technisch.md`: neue `Setup`-Felder und Auswahl-/Commit-/Rewind-Semantik nachziehen | Offen | — |
| 52 | Dokumentation | `README.md` und `docs/RELEASE_NOTES.md` bei Bedarf aktualisieren | Offen | — |
| 53 | Validierung | DoD-Abschluss: `dotnet build VideoPlayer.sln` Debug **und** `-c Release` fehlerfrei + volle Testsuite `dotnet test VideoWebPlayer.Tests/VideoWebPlayer.Tests.csproj` grün | Offen | — |
