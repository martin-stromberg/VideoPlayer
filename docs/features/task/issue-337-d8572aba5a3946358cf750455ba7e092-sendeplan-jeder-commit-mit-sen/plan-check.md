# Plan-Gegenprüfung

## Ergebnis

**Status:** Plan vollständig

Zweite Prüfrunde gegen den nachgebesserten Plan (vorheriges Ergebnis archiviert als `plan-check.1.md`). Beide gemeldeten Lücken und beide Hinweise sind sauber eingearbeitet; die stichprobenartige Code-Verifikation wurde wiederholt und alle Plan-Behauptungen bestätigt (Code unverändert auf Commit `b2e4108`).

## Abgleich Akzeptanzkriterien

| Akzeptanzkriterium | Umsetzung im Plan | Testnachweis im Plan | Status |
|--------------------|-------------------|----------------------|--------|
| Burst scope-freier `NotifyScheduleChanged()` → genau ein `ExecutePassAsync`-Lauf (statt je Commit) | Festes Bündelfenster (Leading-Edge) zentral im Singleton `ProgramScheduleSignal`; Fensterdauer `ProgramScheduleOptions.ScheduleChangedDebounceSeconds` (Default 5 s) | `Notify_BurstWithinWindow_WakesOnce`, `Notify_AfterWindowDelivery_StartsNewWindow`, `Notify_OutsideScan_WakesAfterDebounceWindow` | Abgedeckt |
| Materialisierter Sendeplan spiegelt weiterhin sämtliche Änderungen (nur verzögert ≤ Fenster) | Unveränderte Zustellinfrastruktur (1-Slot-Kanäle); alle Aufrufstellen bleiben unverändert | `Notify_OutsideScan_WakesAfterDebounceWindow`; E2E-Regression `TVSenderAdminE2ETests` (DB-Polling, großzügige Timeouts) | Abgedeckt |
| Alle auslösenden Ereignisse erfasst (SaveChanges-Hook Z. 85/94/133/142, `DeleteMediaSourceAsync` Z. 448, `ProgramRuleService` 4×, `ProgramClipGroupService` 5×, `MediaDurationProbeService` Z. 128, `TVSenderAdmin` 3×, `ProgramSettings` Z. 234) | Zentrale Bündelung im Singleton → alle ~16 Stellen ohne eigene Änderung (verifiziert: Aufrufstellen stimmen exakt mit dem Bestand überein) | Indirekt über Signal-Tests; Bestandstests zu verifizieren | Abgedeckt |
| `BeginScan`-Bündelung bleibt unverändert (Scan-Scopes, `MediaDurationProbeWorker`-Backlog) | Scope-Pfad unverändert; Ausschluss 3 (bestätigt) | Bestehende `Notify_DuringScan_IsSuppressed_UntilScanEnds`, `MediaDurationProbeWorker_BundlesBacklogSignals_SingleWake`, `RunFullScanAsync_DefersScheduleWake_UntilScanEnds` | Abgedeckt |
| `NotifyLiveScheduleChanged` (materialisierter Pfad, Issue #315) bleibt unmittelbar | Ausdrücklich unverändert; Ausschluss 2 begründet Mitbündelung nur für `_liveChannel` bei Eingabe-Commits | `Notify_BatchedWake_AlsoFeedsLiveChannel`; bestehende `LiveStreamWorkerTests` | Abgedeckt |
| „Jetzt generieren" bleibt unverzögert (4-s-Warte + Neu laden, `TVSenderAdmin.razor` Z. 1532–1558) | Neuer Sofort-Pfad `NotifyScheduleChangedNow()`; `GenerateNowAsync` (Z. 1541) umgestellt; Reset (Z. 1590) und Kanaleinstellungen (Z. 2028) bleiben bewusst gebündelt — Entscheidung dokumentiert (Ausschluss 1, bestätigt) | `NotifyScheduleChangedNow_WakesImmediately`, `NotifyScheduleChangedNow_DuringScan_IsSuppressed`, `NotifyScheduleChangedNow_ConsumesPendingBatch` | Abgedeckt |
| Flush-vs-Scan-Race (Fenster endet in offenem Scope) | Überführung in `_suppressedNotifications` mit derselben Doppelcheck-Behandlung wie `NotifyScheduleChanged` Z. 90–95 | `PendingBatch_FlushedDuringScan_IsSuppressedUntilScanEnds` | Abgedeckt |
| Verbrauch ausstehender Bündelung durch `EndScan`-Wecken (kein doppelter Weckruf beim Interleaving „Meldung vor `BeginScan` + unterdrückte Meldung im Scan + `EndScan`-Wecken + späterer Flush") | `EndScan` löscht die Markierung, wenn das Scan-Ende weckt; Programmablauf „Scan-Scope" beschreibt das Interleaving explizit (plan.md Z. 67) | **`EndScan_Wake_ConsumesPendingBatch_LateFlushDoesNotWake`** (neu, Z. 179) + Akzeptanzszenario 7 | Abgedeckt (Lücke aus Runde 1 geschlossen) |
| Konfigurierbarkeit der Verzögerung (offene Frage 4) | `ProgramSchedule:ScheduleChangedDebounceSeconds`, int, Default 5, `EffectiveScheduleChangedDebounce`-Normalisierung (`<0`→5 s, `0`→sofort, `>0`→Wert); `appsettings.json` ergänzt | `DebounceDisabled_DeliversImmediately` | Abgedeckt |
| Interface-Erweiterung erfasst alle Implementierungen | `GatedScheduleSignal` (`MediaDurationProbeWorkerTests` Z. 280) und `FakeScheduleSignal` (`MediaDurationProbeServiceTests` Z. 710) werden nachgezogen — verifiziert: exakt diese zwei Test-Doubles implementieren `IProgramScheduleSignal`; `GatedScheduleSignal._inner` wird mit `ScheduleChangedDebounceSeconds = 0` konstruiert (Z. 101, 147) | Umsetzungsschritte 3, 5 | Abgedeckt |
| Keine DB-Änderung / kein Backup-Schema-Eingriff | „Keine Datenbankmigrationen" — korrekt, reine Laufzeitlogik | — | Abgedeckt |
| Dokumentation gemäß Definition of Done (AGENTS.md §7: `docs/help/`, `README.md`, Release Notes bei Bedarf) | Neuer Umsetzungsschritt 6 (Z. 157–159): `docs/help/tv-sender-ablauf-technisch.md` Abschnitt „Gebündelte Auslöse-Signale" (Z. 937–960) wird um Bündelfenster, `NotifyScheduleChangedNow` und Live-Mitbündelung ergänzt; `docs/RELEASE_NOTES.md`-Eintrag nach dem Muster #290/#315 | Tasks 17–18 in `…-tasks.md` | Abgedeckt (Lücke aus Runde 1 geschlossen) |

## E2E-Abdeckung

| Benutzerfluss / Akzeptanzkriterium | Geplanter E2E-Test | Status |
|------------------------------------|--------------------|--------|
| „Jetzt generieren" (`GenerateNowAsync`) | Kein neuer E2E-Test; bestehender `TVSenderAdminE2ETests`-Pfad als Regression | Nicht erforderlich mit Begründung — Sofort-Pfad unverändert, materialisiertes Ergebnis wird per DB-Polling abgewartet; Begründung im Plan nachvollziehbar |
| Burst-Bündelung bei Admin-Edits | Keiner | Nicht erforderlich mit Begründung — internes Zeitverhalten ohne geänderte UI-Interaktion; über die UI faktisch nicht beobachtbar (Worker-Lauf im Hintergrund); Unit-Tests am Signal sind der korrekte Nachweisort |
| Sendeplan-Reset / Kanaleinstellungen / `ProgramSettings` (bleiben gebündelt) | Keiner; bestehende E2E absorbieren +5 s über großzügige DB-Poll-Timeouts (60–90 s) | Nicht erforderlich mit Begründung — sichtbares Ergebnis identisch, nur Worker-Weckung verzögert |

## Hinweise

- **Nachbesserungen aus Runde 1 — vollständig eingearbeitet:**
  - *EndScan-Verbrauchs-Interleaving:* neuer Test `EndScan_Wake_ConsumesPendingBatch_LateFlushDoesNotWake` (Z. 179), Programmablauf „Scan-Scope" beschreibt das Interleaving explizit (Z. 67), Akzeptanzszenario 7 (Z. 205), Task 16.
  - *Dokumentation:* neuer Umsetzungsschritt 6 (Z. 157–159) für `docs/help/tv-sender-ablauf-technisch.md` (Z. 937–960) und `docs/RELEASE_NOTES.md`; Tasks 17–18.
  - *Flush-Teardown-Reihenfolge:* Fenster-Läuft-Flag wird vor dem Löschen der Ausstehend-Markierung zurückgesetzt bzw. danach erneut geprüft — im Programmablauf (Z. 51) und in den Klassenänderungen (Z. 84) benannt, inkl. Begründung des verlorenen Weckens im Teardown-Spalt.
  - *`GatedScheduleSignal._inner`:* wird mit `ProgramScheduleOptions { ScheduleChangedDebounceSeconds = 0 }` konstruiert (Z. 101, Umsetzungsschritt 3 Z. 147, Task 9).
- **Verifizierte Plan-Behauptungen (Code-Stand `b2e4108`, unverändert seit Runde 1):** `ProgramScheduleSignal` entspricht exakt der Bestandsaufnahme (Singleton via `ServiceCollectionExtensions.cs` Z. 269–270, `_channel`/`_liveChannel` `Bounded(1)`/`DropWrite`, `_activeScans`/`_suppressedNotifications` Z. 79–80, Doppelcheck-Race Z. 90–95, `EndScan`-Weckbedingung Z. 121–129). `ProgramScheduleOptions` existiert mit `Effective*`-Muster und ist an die Sektion `ProgramSchedule` gebunden (`ServiceCollectionExtensions.cs` Z. 245); `appsettings.json` enthält die Sektion bereits (Z. 220–222). `TimeProvider.System` ist als Singleton registriert (Z. 286) — DI befüllt den geplanten optionalen Konstruktor automatisch; in Test-ServiceCollections ohne `IOptions`-/`TimeProvider`-Registrierung greifen die Parameter-Defaults. `ControllableTimeProvider` (`VideoWebPlayer.Tests/Helpers/ControllableTimeProvider.cs`) existiert und feuert via `TickTimers()` alle `CreateTimer`-Timer — `Task.Delay(span, timeProvider)` ist damit deterministisch testbar. `TVSenderAdmin.razor`: `GenerateNowAsync` mit `NotifyScheduleChanged()` (Z. 1541) + `Task.Delay(4 s)` (Z. 1542) sowie die Stellen Z. 1590/2028 stimmen; `ProgramSettings.razor` Z. 234 stimmt. Beide Referenztests (`MediaDurationProbeWorker_BundlesBacklogSignals_SingleWake`, `RunFullScanAsync_DefersScheduleWake_UntilScanEnds`) existieren und sind im Baseline-Lauf bestanden.
- **Konsistenz:** `…-tasks.md` wurde synchron nachgezogen (Task 16 = neuer Interleaving-Test, Tasks 17–18 = Dokumentation, Task 9 = `_inner` im Sofort-Modus).
- `appsettings.Testing.json` enthält keinen `ProgramSchedule`-Schlüssel — E2E läuft mit Default 5 s, was der Plan-Begründung („Timeouts absorbieren +5 s") entspricht; kein Handlungsbedarf.
