# Plan-Gegenprüfung

## Ergebnis

**Status:** Plan vollständig

## Abgleich Akzeptanzkriterien

| Akzeptanzkriterium | Umsetzung im Plan | Testnachweis im Plan | Status |
|--------------------|-------------------|----------------------|--------|
| 1. Re-Bewertung bei Regel-Änderung/-Deaktivierung/-Löschung | `ReconcileAsync` + `IProgramScheduleSignal`-Trigger in allen `ProgramRuleService`-CRUD-Methoden + periodischer Worker-Lauf | `Reconcile_DeactivatedRule_DisplacesFutureKeepsPast`, `Reconcile_DeletedRule_DisplacesSlotsAndRefillsGap`, `Reconcile_ChangedTimeOfDay_ReplansAtNewTime`, `Reconcile_ChangedMaterial_ReplansWithNewMaterial`, `RuleService_*_NotifiesScheduleSignal` | Abgedeckt |
| 2. Gültige Sendeplätze bleiben (inkl. laufende/gesendete) | Validierungssweep: nur ungültige Ausstrahlungen + Suffix werden `Displaced`; `End <= now` nie angefasst; gültige laufende bleiben `Scheduled` | `Reconcile_NoChanges_LeavesEverythingScheduled`, `Reconcile_DeactivatedRule_DisplacesFutureKeepsPast` | Abgedeckt |
| 3. Ungültige → „verdrängt" markiert, ersetzt, nicht gelöscht | `ProgramSlotStatus`/`Status`-Spalte + Neu-Materialisierung; kein `Remove` | alle `Reconcile_*`-Tests | Abgedeckt |
| 4. Verdrängte Serienfolgen wieder einplanen, ohne Sprung | Cursor-Rücksetzung auf Vorgänger des frühesten verdrängten, noch expandierbaren Inhalts | `Reconcile_DisplacedSeries_ReschedulesAtNextOccurrenceWithoutSkip` | Abgedeckt |
| 5. Neue Episoden berücksichtigen | Expansion liefert neue Reihenfolge → Simulation invalidiert → Neuplanung; Marker-Hook-Signal als Trigger | `Reconcile_NewEpisodeInserted_ReplansInAirOrder`, `SaveChanges_NewEpisode_NotifiesScheduleSignal` | Abgedeckt |
| 6. Gelöschtes Video → verdrängt, keine Kaskade, Lücke regelkonform/Füller | Weiche `MediaItemId` referenziert nichts mehr → Ausstrahlung ungültig → `Displaced` (Zeile bleibt); `FillGaps` | `Reconcile_DeletedMediaItem_DisplacesSlotAndContinues` | Abgedeckt |
| 7. Automatisiert | Signal (CRUD, Marker-Hook, Quell-Löschung, Scan-Ende) + periodischer Worker hinter `IBackgroundProcessingGate` | Signal-Tests, `ProgramScheduleWorkerTests` | Abgedeckt |
| 8. Nachvollziehbar (verdrängte sichtbar) | `ProgramSlot.Status` persistiert + Sendeplan-Karte in `TVSenderAdmin` mit Status-Chip | `GetSchedule_ReturnsUpcomingSlotsWithStatus` + Pflicht-E2E | Abgedeckt |
| Datenhaltung (Migration + Backup-Registrierung + Alt-Backup-Test) | `AddProgramSlotStatus`, `OptionalRestoreColumns`, Regressionstest | `VideoWebPlayerBackupDataTests`-Neuzugang | Abgedeckt |

## E2E-Abdeckung

| Benutzerfluss / Akzeptanzkriterium | Geplanter E2E-Test | Status |
|------------------------------------|--------------------|--------|
| Administrator sieht verdrängte Sendeplätze (einziger neuer Benutzerfluss) | Erweiterung `TVSenderAdminE2ETests` (Sendeplan-Karte, „Verdrängt"-Chip, Seed via DbContext) | Abgedeckt |
| Regel ändern/deaktivieren/löschen (bestehender Fluss, neue serverseitige Wirkung) | Kein neuer E2E — Wirkung ist rein serverseitig/datengetrieben, durch Service-Tests + Signal-Tests abgedeckt; der bestehende E2E `Admin_CreateEditDeactivateDeleteRule` bleibt gültig | Nicht erforderlich: keine neue Interaktion, nur geändertes Systemverhalten im Hintergrund |

## Hinweise

- Der Validierungssweep (Ausstrahlungs-Simulation statt Dirty-Flags) deckt planrelevante Regeländerungen auch dann ab, wenn ein Signal verloren geht (Absturz zwischen Commit und Worker-Lauf) — der Plan ist dadurch robuster als ein reines Trigger-Design.
- Bewusst akzeptierte Grenze: Bulk-`ExecuteDeleteAsync`-Pfade außer `DeleteMediaSourceAsync` werden nur periodisch erkannt (Intervall-Latenz); im Plan als dokumentierte Einschränkung vermerkt.
- `EnsureCoverageAsync` bleibt fachlich „extend-only"; einzig die Abdeckungs-Queries lernen den `Scheduled`-Filter — bestehende Tests bleiben unverändert gültig.
