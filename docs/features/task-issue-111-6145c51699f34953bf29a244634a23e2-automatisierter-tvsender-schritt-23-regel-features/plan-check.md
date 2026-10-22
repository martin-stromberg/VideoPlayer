# Plan-Gegenprüfung

## Ergebnis

**Status:** Plan vollständig

## Abgleich Akzeptanzkriterien

| Akzeptanzkriterium | Umsetzung im Plan | Testnachweis im Plan | Status |
|--------------------|-------------------|----------------------|--------|
| N19: Mehrere Serien pro Tag, jede eine Folge, stabile Uhrzeit | Strang-Modell + `SelectAiringItemsInterleaved` (Round-Robin, fester Strang-Start je Occurrence) | `EnsureCoverage_GenreRule_InterleavesShows_StableSlotsAcrossDays` | Abgedeckt |
| N19: fortlaufende Episoden je Serie | eigener Cursor je Strang, persistiert via `ProgramRuleCursor.StrandKey` | `..._EpisodeOrderPerShow`, `..._SecondRun_ContinuesEachShowCursor` | Abgedeckt |
| N19: keine Doppelbelegung | Boundaries/atomare Materialisierung unverändert; Test assertiert Intervalle | Teil des Interleaving-Tests | Abgedeckt |
| N19: Reconcile-Kompatibilität | `AiringReproducible` Interleaved-Pfad + `RewindCursor` je Strang | Reconcile-Idempotenz- und Rewind-Tests | Abgedeckt |
| N20: `Weekday`/`Weekend` in `BuildOccurrences`, Validierung, UI, Beschriftung | Enum + Switch + Select + `RecurrenceLabel`; Validierung via `Enum.IsDefined` | `EnsureCoverage_WeekdayRule_*`, `..._WeekendRule_*`, Validierungstests | Abgedeckt |
| N21: Editor als Overlay-Panel mit App-Konvention | `admin-dialog-overlay`/`admin-dialog`, Overlay-Klick = Abbrechen | `RuleEditor_RendersAsOverlay` (bUnit) + angepasster E2E-Fluss | Abgedeckt |
| DoD: neue Spalte in Backup + Migration + Alt-Backup-Regressionstest | `OptionalRestoreColumns`, Migration `AddProgramRuleCursorStrandKey` | `ReadFromAsync_LegacyBackupWithoutStrandKeyColumn_*` | Abgedeckt |
| DoD: serverseitige Absicherung | Validierung/Admin-Zwang bleiben serverseitig (`ProgramRuleService`, `ProgramScheduleService`); Overlay ist reine UI-Form | bestehende Authorization-Tests unverändert | Abgedeckt |

## E2E-Abdeckung

| Benutzerfluss / Akzeptanzkriterium | Geplanter E2E-Test | Status |
|------------------------------------|--------------------|--------|
| N21: Regel-Editor als bedienbares Overlay | `TVSenderAdminE2ETests` — bestehender Anlage-/Bearbeitungs-Fluss wird auf Overlay-Assertions umgestellt | Abgedeckt |
| N19/N20: serverseitige Planlogik | kein eigener E2E — kein Benutzerfluss; SQLite-Integrationstests sind primärer Nachweis (begründet im Plan) | Nicht erforderlich mit Begründung |

## Hinweise

- Debug-Build durch laufende Benutzer-App gesperrt → Verifikation via `-p:OutDir=artifacts/schritt23-bin` (etabliert); Release-Build regulär.
- Migration bevorzugt per `dotnet ef` (Konfiguration Release); Fallback: manuell nach Muster.
