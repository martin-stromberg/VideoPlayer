# Plan-Gegenprüfung

Zweite Prüfrunde. Vorangegangener Bericht: `plan-check.1.md` (meldete zwei fehlende Testanforderungen — Interleaved-Pfad, `RewindCursor`-Semantik — sowie Hinweise). Beide Lücken sind geschlossen, alle Hinweise umgesetzt; der Plan wurde insgesamt neu gegen die Anforderung geprüft.

## Ergebnis

**Status:** Plan vollständig

## Abgleich Akzeptanzkriterien

| Akzeptanzkriterium | Umsetzung im Plan | Testnachweis im Plan | Status |
|--------------------|-------------------|----------------------|--------|
| Sendeplan-Erzeugung plant keinen Titel außerhalb seines zulässigen Zeitfensters (Test gegen echtes SQLite mit gemischten Freigaben und Fenstergrenzen) | Schritte 3–6: `SchedulableItem.AgeRatingMinAge`, Fensterprüfung in `SelectAiringItems`/`SelectAiringItemsInterleaved`, `FillGaps`/Clip-Fill (`fits`-Prädikat), beide Materialisierungspfade, identische Prüfung im Repro-Pfad | `ProgramScheduleServiceTests_AgeWindow` (gemischte Freigaben über Fenstergrenzen, Interleaved-Skip/-Defer, Füller/Clip-Fenster) auf `ProgramScheduleTestDb` (echtes SQLite) + `ProgramScheduleAgeWindowTests` (Unit) + E2E `Admin_AgeWindowedContent_ScheduledAtNight` | Abgedeckt |
| FSK- und MPAA-Werte werden einheitlich auf das Fensterschema abgebildet | Stufen-Zuordnung „größte konfigurierte Stufe ≤ `MinAge`" auf `AgeRatingMinAge`; `AgeRatingNormalizer` bleibt einzige Stufenquelle (`R`→17 ≙ FSK-16-Fenster, `NC-17`→18 ≙ FSK-18-Fenster) | `ProgramScheduleAgeWindowTests` (Zwischenwerte 8/13/17), MPAA-Mapping-Test in `ProgramScheduleServiceTests_AgeWindow` | Abgedeckt |
| Verhalten bei fehlender Freigabe konfigurierbar und dokumentiert; Standard definiert | Enum `ProgramScheduleUnknownRatingMode` (`Unrestricted`/`RestrictToNightWindow`), `Setups`-Spalte, Default `RestrictToNightWindow` (Anwender-Entscheidung), gilt auch für Material ohne `IAgeRated` (Standby explizit ausgenommen); Doku in `tv-sender.md` | Beide Modi im Service-Test; `ProgramSettingsServiceTests_AgeWindow` (Roundtrip/Normalisierung/`Enum.IsDefined`); E2E `Admin_UnknownRatingMode_SelectAndPersist` | Abgedeckt |
| Regel-Diagnose zeigt an, wenn eine Regel wegen des Jugendschutzfensters nichts planen kann | Schritte 7–8: `SummaryCounters.BlockedByAgeWindow`, `ProgramRuleDiagnosis.AgeWindowBlockedCount`, `GetRuleDiagnosesAsync`-Befüllung, `DiagnosisLabel`-Text | Diagnose-Test (`AgeWindowBlockedCount > 0`), `TVSenderAdminTests*`-Komponententest, E2E `Admin_RuleDiagnosis_ShowsAgeWindowReason` | Abgedeckt |
| Tages- und Wochenübergänge definiert: begonnener Beitrag läuft zu Ende, neue Sendeplätze folgen der Grenze | Designentscheidung „Prüfzeitpunkt": Zulässigkeit gegen lokale Startzeit des jeweiligen Sendeplatzes (`slotStart`, `TimeZoneInfo.Local`), konsistent zur Boundary-Regel „a started item always completes" | Test „Tages-/Wochenübergang" (begonnener Beitrag über Grenze, 05:30 erlaubt / 06:30 nicht) **explizit inkl. `Weekly`-Regel über der Grenze** — Hinweis aus Runde 1 umgesetzt | Abgedeckt |
| Hilfe-Doku (`docs/help/tv-sender.md`) und Release Notes aktualisiert | Schritt 11: `tv-sender.md` (Fensterschema, Konfiguration, Unbekannt-Modus inkl. Standby-Eingrenzung und Restlücken-Hinweis, Diagnose), `tv-sender-datenmodell.md`, `tv-sender-ablauf-technisch.md`, `README.md`, `RELEASE_NOTES.md` | n/a (Dokumentation) | Abgedeckt |
| Lückenlosigkeit: unpassende Titel werden verschoben bzw. durch passendes Material/Füller ersetzt | Differenzierte Skip-/Defer-Semantik; `FillGaps` mit Fensterprüfung (`PeekAt(offset)`); Clip-Fill im `fits`-Prädikat | Cursor/Rotation-Test (Wiederangebot im nächsten Fenster), Füller/Clip-Fenster-Test | Abgedeckt (im Default `RestrictToNightWindow` sind tagsüber Restlücken möglich — direkte Folge der Anwender-Entscheidung, als Seiteneffekt benannt und für Hilfe vorgesehen) |
| Cursor/Rotation klemmt nicht dauerhaft; übersprungene Titel werden erneut angeboten | Scan-basiertes Commit (`NextIndex` hinter letzte gescannte Position, `LastPlaced` = letztes *genommenes* Item); Rewind-Semantik definiert (unverändert, Skip-Positionen hinter Rewind-Index werden erneut angeboten) | Cursor/Rotation-Test, Episoden-Defer-Test, Interleaved-Skip/-Defer-Tests, Rewind-Test | Abgedeckt — Lücken aus Runde 1 geschlossen |
| Konfigurierbare Fenstertabelle (vier Grenzzeit-Spalten, Defaults 20/22/23/06:00) | Vier `TimeOnly`-`Setups`-Spalten + `ProgramScheduleAgeWindowSettings` + Validierung (Monotonie, `NightEnd < Fsk12`) + Admin-UI | `ProgramSettingsServiceTests_AgeWindow` (Validierungsfehler), E2E `Admin_AgeWindows_SetAndPersist` + `Admin_AgeWindows_InvalidInput_ShowsError` | Abgedeckt |
| Senderweit einheitlich, kein `ProgramRule`-Feld (Anwender-Entscheidung) | Designentscheidung begründet; keine Regel-Migration, kein Editor-Eingriff | n/a | Abgedeckt |
| DoD: `OptionalRestoreColumns` + EF-Migration + Alt-Backup-Regressionstest | Schritt 2: Migration `AddProgramScheduleAgeWindows`, fünf `Setups.*`-Einträge in `OptionalRestoreColumns` | `VideoWebPlayerBackupDataTests`-Erweiterung mit `LegacyBackupArchiveBuilder.RemoveColumnsAsync` | Abgedeckt |
| DoD: echtes SQLite statt EF InMemory | Tests laufen über `ProgramScheduleTestDb` (In-Memory-SQLite mit FK) | explizit benannt | Abgedeckt |
| DoD: serverseitige Berechtigungen | Kein neuer Endpunkt; `GetRuleDiagnosesAsync` bleibt Admin-only (`RequireAdminAsync`), Einstellungen über die bestehende Admin-geschützte Seite | `Diagnosis_NonAdmin_Throws` bleibt; bestehender `NonAdmin_SeesNotAuthorized`-E2E unverändert | Abgedeckt |
| DoD: Debug- **und** Release-Build, volle Testsuite | Schritt 12 nennt beide Builds und die volle Suite inkl. Flacker-Regel — Hinweis aus Runde 1 umgesetzt | Abschluss-Validierung | Abgedeckt |
| `ProgramGuideService`/Live-TV unverändert (Nicht-Anforderung) | Kein Umsetzungsschritt berührt diese Komponenten; `LiveStreamStandbyMediaItemId` explizit von der Unbekannt-Regel ausgenommen — Hinweis aus Runde 1 umgesetzt | n/a | Abgedeckt |

## Fehlende oder unvollständige Testanforderungen

Keine — die beiden aus Runde 1 gemeldeten fehlenden Testanforderungen sind geschlossen:

- Interleaved-Auswahlpfad: „Interleaved — Skip im „other"-Strang" und „Interleaved — Defer im geordneten Strang" decken `skip[s]`- bzw. `done[s]`-Mechanik ab.
- `InterleavedAiringTailReproducible`: „Interleaved — Reproduzierbarkeit" prüft Reproduzierbarkeit bei unveränderten Einstellungen (`AssertNoDisplacedAsync`) und Displacement + Rematerialisierung nach Fenster-Verschärfung.

## E2E-Abdeckung

| Benutzerfluss / Akzeptanzkriterium | Geplanter E2E-Test | Status |
|------------------------------------|--------------------|--------|
| Admin setzt Fensterzeiten in den Einstellungen; Werte persistieren nach Reload | `TVSenderAdminE2ETests.Admin_AgeWindows_SetAndPersist` (Pflicht) | Abgedeckt |
| Admin wählt Unbekannt-Modus; Wert persistiert | `TVSenderAdminE2ETests.Admin_UnknownRatingMode_SelectAndPersist` (Pflicht) | Abgedeckt |
| Regel-Diagnose zeigt Fenster-Grund sichtbar in der Admin-UI | `TVSenderAdminE2ETests.Admin_RuleDiagnosis_ShowsAgeWindowReason` (Pflicht) | Abgedeckt |
| Validierungsfehler (nicht monotone Fensterzeiten) sichtbar beim Speichern | `TVSenderAdminE2ETests.Admin_AgeWindows_InvalidInput_ShowsError` (Pflicht) | Abgedeckt |
| Gesamtfluss: FSK-18-Film erscheint nicht am Tag, sondern im Nachtfenster im Sendeplan | `TVSenderAdminE2ETests.Admin_AgeWindowedContent_ScheduledAtNight` (Pflicht — aus „empfohlen" hochgestuft, Hinweis Runde 1 umgesetzt) | Abgedeckt |

## Fehlende oder unvollständige Planbestandteile

Keine — der aus Runde 1 gemeldete fehlende Planbestandteil ist geschlossen:

- `RewindCursor`/`RewindRing`-Konsistenz mit dem Scan-basierten Commit: als eigene Designentscheidung definiert (unverändertes `RewindTo(idx)` auf frühesten displaced Inhalt; mit-konsumierte Skip-Positionen dahinter werden erneut angeboten — fachlich begründet, Out-of-Window-Placement ausgeschlossen), im Reconcile-Ablauf und in „Änderungen an bestehenden Klassen" vermerkt, als Risiko benannt, Verifikation in Schritt 6, Absicherung über den Rewind-Test.

## Hinweise

- Positiv: Der Plan adressiert weiterhin das kritische Seed-Problem (2526 Bestandstests würden mit Default `RestrictToNightWindow` kippen; `ProgramScheduleTestDb` setzt `Unrestricted` bzw. `MinAge = 0`), die Reproduzierbarkeits-Falle (lückentolerante Verifikation mit Unzulässigkeits-Beweis je Zwischenposition; gemeinsame `ProgramScheduleAgeWindow`-Auswertung als Pflicht) und die `ProgramRuleDiagnosis`-Signaturänderung für Komponententests.
- Bei Umsetzung besonders sorgfältig: die `LastPlaced`-Invariante (nie auf übersprungene Position zeigen, sonst „verliert" `NextIndexFor` den Titel dauerhaft) und die identische Fenster-Auswertung in Auswahl und Repro-Pfad — beides ist im Plan präzise beschrieben, aber fehleranfällig.
- `inventory/logic.md`, `models.md`, `enums.md`, `interfaces.md` sind russischsprachig verfasst (inhaltlich konsistent zur deutschen `inventory.md`) — nur als Feststellung, keine Planlücke.
