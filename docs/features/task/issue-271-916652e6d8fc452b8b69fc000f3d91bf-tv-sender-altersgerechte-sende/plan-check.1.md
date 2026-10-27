# Plan-Gegenprüfung

## Ergebnis

**Status:** Plan lückenhaft

## Abgleich Akzeptanzkriterien

| Akzeptanzkriterium | Umsetzung im Plan | Testnachweis im Plan | Status |
|--------------------|-------------------|----------------------|--------|
| Sendeplan-Erzeugung plant keinen Titel außerhalb seines zulässigen Zeitfensters (echtes SQLite, gemischte Freigaben/Fenstergrenzen) | Schritte 3–6: `SchedulableItem.AgeRatingMinAge`, Fensterprüfung in `SelectAiringItems`/`SelectAiringItemsInterleaved`, `FillGaps`, Clip-Fill, beide Materialisierungspfade, Repro-Pfad | `ProgramScheduleServiceTests_AgeWindow` (gemischte Freigaben, echtes SQLite), `ProgramScheduleAgeWindowTests` (Unit) | Abgedeckt mit Einschränkung — kein expliziter Test für den Interleaved-Pfad (siehe fehlende Testanforderungen) |
| FSK- und MPAA-Werte werden einheitlich auf das Fensterschema abgebildet | Stufen-Zuordnung „größte Stufe ≤ `MinAge`" über `AgeRatingMinAge`; `AgeRatingNormalizer` bleibt einzige Quelle | `ProgramScheduleAgeWindowTests` (Zwischenwerte 8/13/17), MPAA-Mapping-Test (`R`→17, `PG-13`→13, `NC-17`→18) | Abgedeckt |
| Verhalten bei fehlender Freigabe konfigurierbar und dokumentiert; Standard definiert | Enum `ProgramScheduleUnknownRatingMode`, `Setup`-Spalte, Default `RestrictToNightWindow` (Anwender-Klärung), gilt auch für Material ohne `IAgeRated`; Doku in `tv-sender.md` | Beide Modi im Service-Test; `ProgramSettingsServiceTests_AgeWindow` (Roundtrip/Normalisierung/`Enum.IsDefined`); E2E `Admin_UnknownRatingMode_SelectAndPersist` | Abgedeckt |
| Regel-Diagnose zeigt „kein altersgerechtes Material im Fenster" | Schritte 7–8: `SummaryCounters.BlockedByAgeWindow`, `ProgramRuleDiagnosis.AgeWindowBlockedCount`, `DiagnosisLabel`-Text | Diagnose-Test (`AgeWindowBlockedCount > 0`), `TVSenderAdminTests*`-Komponententest, E2E `Admin_RuleDiagnosis_ShowsAgeWindowReason` | Abgedeckt |
| Tages-/Wochenübergänge definiert: begonnener Beitrag läuft zu Ende, neue Sendeplätze folgen der Grenze | Design: Zulässigkeit gegen lokale Startzeit des Sendeplatzes (`slotStart`), konsistent zur Boundary-Regel | Test „Tages-/Wochenübergang": begonnener Beitrag über Grenze hinaus, 05:30 erlaubt / 06:30 nicht | Abgedeckt (Hinweis: Wochenübergang nur implizit — konkret wird nur die Tagesgrenze geprüft) |
| Hilfe-Doku (`docs/help/tv-sender.md`) und Release Notes aktualisiert | Schritt 11: `tv-sender.md` (Fensterschema, Konfiguration, Unbekannt-Modus, Diagnose), `tv-sender-datenmodell.md`, `tv-sender-ablauf-technisch.md`, `README.md`, `RELEASE_NOTES.md` | n/a (Dokumentation) | Abgedeckt |
| Lückenlosigkeit: unpassende Titel werden verschoben/durch passendes Material-Füller ersetzt | Skip/Defer-Semantik in der Auswahl; `FillGaps` mit Fensterprüfung (`PeekAt(offset)`); Clip-Fill im `fits`-Prädikat | Cursor/Rotation-Test inkl. Lückenlosigkeit mit passendem Restmaterial; Füller/Clip-Fenster-Test | Abgedeckt (Hinweis: im Default `RestrictToNightWindow` können tagsüber Restlücken entstehen — vom Plan als Folge der Anwender-Entscheidung benannt und für Hilfe vorgesehen) |
| Cursor/Rotation klemmt nicht dauerhaft; übersprungene Titel werden erneut angeboten | Differenzierte Skip-/Defer-Semantik; Scan-basiertes Commit mit mitkonsumierten Positionen | Cursor/Rotation-Test (Wiederangebot beim nächsten Fenster), Episoden-Defer-Test | Abgedeckt mit Einschränkung — Interleaved-Mechanik (`skip[s]`/`done[s]`) ohne expliziten Test |
| Konfigurierbare Fenstertabelle (vier Grenzzeit-Spalten 20/22/23/06:00) | Vier `TimeOnly`-`Setups`-Spalten + `ProgramScheduleAgeWindowSettings` + Validierung (Monotonie, `NightEnd < Fsk12`) + Admin-UI | Settings-Tests (Validierungsfehler), E2E `Admin_AgeWindows_SetAndPersist` + `Admin_AgeWindows_InvalidInput_ShowsError` | Abgedeckt |
| Senderweit einheitlich, kein `ProgramRule`-Feld (Anwender-Klärung) | Designentscheidung begründet; keine Regel-Migration, kein Editor-Eingriff | n/a | Abgedeckt |
| DoD: `OptionalRestoreColumns` + Alt-Backup-Regressionstest bei neuen Spalten | Schritt 2: fünf `Setups.*`-Einträge | `VideoWebPlayerBackupDataTests`-Erweiterung mit `LegacyBackupArchiveBuilder.RemoveColumnsAsync` | Abgedeckt |
| DoD: echtes SQLite statt InMemory | Tests gegen `ProgramScheduleTestDb` (In-Memory-SQLite mit FK) | explizit benannt | Abgedeckt |
| DoD: serverseitige Berechtigungen | Kein neuer Endpunkt; `GetRuleDiagnosesAsync` bleibt Admin-only, Einstellungen über bestehende Admin-Seite | `Diagnosis_NonAdmin_Throws` bleibt unverändert; bestehender `NonAdmin_SeesNotAuthorized`-E2E | Abgedeckt |
| `ProgramGuideService`/Live-TV unverändert | Kein Umsetzungsschritt berührt diese Komponenten | n/a | Abgedeckt |

## Fehlende oder unvollständige Testanforderungen

- [ ] Expliziter Test für die Fensterprüfung im Interleaved-Auswahlpfad (`SelectAiringItemsInterleaved`): Skip im ungeordneten „other"-Strang (`skip[s]++` mit vollem Ringumlauf-Abbruch) und Defer (`done[s]`) bei geordnetem Strang — Bezug: AC „kein Titel außerhalb des Fensters" + „Cursor klemmt nicht". Die geplanten Tests („Cursor/Rotation", „Episoden-Defer") benennen den Interleaved-Pfad nicht; die `skip[s]`/`done[s]`-Mechanik unterscheidet sich strukturell vom flachen Ring-Offset und wäre sonst ungetestet geändert.
- [ ] Expliziter Test für `InterleavedAiringTailReproducible` mit aktiver Fensterprüfung (Reproduzierbarkeit eines Interleaved-Airings nach Fenster-Änderung bzw. bei unverändertem Fenster) — Bezug: AC „kein Titel außerhalb des Fensters" + Seiteneffekt „massenhafte Fehl-Displacements". Der geplante Reconcile-Test benennt nur den allgemeinen Fall.

## E2E-Abdeckung

| Benutzerfluss / Akzeptanzkriterium | Geplanter E2E-Test | Status |
|------------------------------------|--------------------|--------|
| Admin konfiguriert Fensterzeiten in den Einstellungen und Werte persistieren | `TVSenderAdminE2ETests.Admin_AgeWindows_SetAndPersist` (Pflicht) | Abgedeckt |
| Admin wählt Unbekannt-Modus und Wert persistiert | `TVSenderAdminE2ETests.Admin_UnknownRatingMode_SelectAndPersist` (Pflicht) | Abgedeckt |
| Regel-Diagnose zeigt Fenster-Grund sichtbar in der Admin-UI | `TVSenderAdminE2ETests.Admin_RuleDiagnosis_ShowsAgeWindowReason` (Pflicht) | Abgedeckt |
| Validierungsfehler (nicht monotone Fensterzeiten) sichtbar in der UI | `TVSenderAdminE2ETests.Admin_AgeWindows_InvalidInput_ShowsError` (Pflicht) | Abgedeckt |
| Gesamtfluss: FSK-18-Titel erscheint nur im Nachtfenster im Sendeplan (Admin-Ansicht) | `TVSenderAdminE2ETests.Admin_AgeWindowedContent_ScheduledAtNight` („Empfohlen") | Abgedeckt — Hinweis: einziger UI-End-to-End-Nachweis für AC 1, Priorität „Pflicht" erwägen |

## Fehlende oder unvollständige Planbestandteile

- [ ] Konsistenz der Cursor-Rewind-Mechanik `RewindCursor`/`RewindRing` (Reconcile-Displacement, `ProgramScheduleService.cs` ~839–888) mit dem neuen Scan-basierten Commit ist weder als Änderung noch als Risiko benannt. Da übersprungene Positionen mit-konsumiert werden (`NextIndex` zeigt hinter die letzte *gescannte* Position, `LastPlaced` auf das letzte *genommene* Item), muss der Rewind zu `LastPlaced` definiert werden: Setzt er `NextIndex` zurück, werden bereits mit-konsumierte Skip-Positionen erneut angeboten — die fachliche Konsequenz (Ausstrahlungsreihenfolge vs. Wiederangebot) ist ungeklärt. Die Behauptung „`LastPlaced`-basierte Cursorpersistenz bleibt unverändert korrekt" betrifft nur `PersistCursors`, nicht den Rewind-Pfad.

## Hinweise

- `LiveStreamStandbyMediaItemId` wird nicht über die Sendeplan-Materialisierung ausgewählt (Standby greift im Live-TV-Ausfallpfad, der explizit unverändert bleibt). Die Plan-Aussage „Standby folgt dem Unbekannt-Modus" hat daher keine Wirkung — im Plan bzw. in der Hilfe klarstellen, dass die Regel nur für materialisierte Sendeplätze gilt.
- Der Test „Tages-/Wochenübergang" konkretisiert nur die Tagesgrenze (05:30/06:30). Da das AC Wochenübergänge explizit nennt, sollte mindestens eine `Weekly`-Regel über die Fenstergrenze im selben Test abgedeckt oder benannt werden.
- DoD-Ergänzung: Der Plan enthält keinen Schritt/Hinweis auf den Release-Build (`dotnet build -c Release`); bei Umsetzung beachten (DoD verlangt Debug **und** Release).
- Mit Default `RestrictToNightWindow` sind tagsüber Restlücken (`RemainingGapSeconds`) möglich, wenn alles verfügbare Füllmaterial freigabe-los ist — Spannung zum AC-Nebensatz „der Plan bleibt lückenlos", fachlich aber direkte Folge der Anwender-Klärung. Der Plan benennt das bereits als Seiteneffekt; in der Hilfe unbedingt dokumentieren (vorgesehen).
- Prüfen, ob `BlockedByAgeWindow` zusätzlich in `ProgramScheduleRunSummary`/`ProgramScheduleReconcileSummary` übernommen werden soll (Sichtbarkeit in Logs/Zusammenfassungen) — im Plan nur als `SummaryCounters`-/Diagnose-Grundlage vorgesehen.
- Positiv vermerkt: Der Plan adressiert das kritische Seed-Problem (alle 2526 Bestandstests würden mit `RestrictToNightWindow`-Default kippen; `ProgramScheduleTestDb`-`Setup`-Seed muss `Unrestricted` setzen), die Reproduzierbarkeits-Falle und die `ProgramRuleDiagnosis`-Signaturänderung für Komponententests.
