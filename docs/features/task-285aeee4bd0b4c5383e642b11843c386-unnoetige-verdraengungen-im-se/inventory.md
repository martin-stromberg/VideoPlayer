# Bestandsaufnahme: Unnötige Verdrängungen im Sendeplan vermeiden (Issue #310)

Bestandsaufnahme des Sendeplan-/Reconcile-Bereichs des automatischen TV-Senders, bezogen auf
die Anforderung, dass `ReconcileAsync` für die Regel weiterhin zulässige Sendeblöcke nicht
mehr verdrängt und neu plant (keine „durch sich selbst ersetzt"-Duplikate; Reconcile ohne
Eingabeänderung = No-Op).

## Zusammenfassung

- Der gesamte Prüf- und Verdrängungsmechanismus existiert bereits in
  `ProgramScheduleService.cs` (3139 Zeilen): `ReconcileAsync` lädt das Slot-Fenster
  (`LoadWindowAsync`, Basis-Lookback 24 h, Widening bis `MaxOccurrenceLookbackDays`,
  Default 7 d), `ReconcileWindow.Validate` markiert je Regel die erste invalide Ausstrahlung
  (`GroupAirings`-Gruppen), `AiringReproducible` +
  `FlatAiringTailReproducible`/`InterleavedAiringTailReproducible` simulieren die Auswahl
  exakt nach (inkl. Kopf-Item-Hypothesen, Occurrence-Anker, `_invaderStarts`-Evidenz,
  `StrandStartOffset`, Altersfenster), `SlotMatchesItem` vergleicht tolerant über
  `MediaItemAlternates` (Issue #290). Filler-Slots werden einzeln geprüft.
- Verdrängung läuft als Suffix-Verdrängung ab `firstInvalid` (gesamte Regel-Zukunft) plus
  Eviction-Verdrängung durch `MaterializeOccurrenceEvicting`-Verlierer; `RewindCursor` setzt
  den Strang-Cursor auf den frühesten verdrängten Inhalt zurück.
- Aufrufkette produktiv: `ProgramScheduleWorker` → `PurgeExpiredRulesAsync` →
  `ReconcileAsync` → `EnsureCoverageAsync` (Intervall aus `ProgramSettingsService`, Wake-up
  über `IProgramScheduleSignal`). `EnsureCoverageAsync` ist extend-only und verdrängt nie.
- Datenmodell: `ProgramSlot` (`Status` = `Scheduled`/`Displaced`, `IsClipFill`) und
  `ProgramRuleCursor` (`StrandKey`, `LastContentType`/`LastContentId`) — keine neuen Tabellen
  oder Spalten absehbar, beide bereits in `VideoWebPlayerBackupData` registriert.
- UI: `TVSenderAdmin.razor` zeigt den „Verdrängt"-Chip und bietet „Verdrängte bereinigen"
  (`DeleteDisplacedSlotsAsync`) — Beobachtungsort des Symptoms, voraussichtlich kein Eingriff.
- Tests: umfangreiche Reconcile-Suite existiert
  (`ProgramScheduleServiceTests_Reconcile` mit 25 Tests u. a. No-Op-/Idempotenz-Tests,
  `AssertReconcileNoOpAsync` aus Issue #290, E2E
  `TVSenderAdmin_RepeatGenerate_ProducesNoDisplacedSeries`); alle gegen echtes In-Memory-SQLite.
- Auffälligkeit für die Anforderung: Die Reproduzierbarkeits-Prüfung simuliert die
  Cursor-getriebene Auswahl exakt nach — ein zulässiger Block, der nicht exakt reproduzierbar
  ist (z. B. abweichende Ring-/Cursorposition), wird derzeit als invalid klassifiziert. Die
  Entscheidung „Prüfung lockern vs. Restfehler beheben" ist laut Anforderung offen.

Test-Ausgangszustand: `dotnet test VideoWebPlayer.Tests/VideoWebPlayer.Tests.csproj` —
**2713 erfolgreich, 0 fehlgeschlagen, 2 übersprungen** (2715 gesamt, Exit-Code 0, Dauer
5 min 38 s). Keine bekannten Testfehler. Übersprungen: ein Unix-only-Dateirechte-Test und
ein bewusst deaktivierter E2E-Test mit dokumentiertem Fremdbefund (ContinueWatching-500er).
Details und Nachweise in [tests.md](inventory/tests.md).

## Details

- [Datenmodell](inventory/models.md) — `ProgramSlot`, `ProgramRuleCursor`, `ProgramRule`
- [Logik](inventory/logic.md) — `ProgramScheduleService` (Reconcile-Interna im Detail),
  `ProgramScheduleWorker`, `ProgramScheduleSignal`, `ProgramScheduleAgeWindow`, `TVSenderAdmin.razor`
- [Enums](inventory/enums.md) — `ProgramSlotStatus`, `ProgramRuleType`,
  `ProgramCursorContentType`, `ProgramRuleMaterialType`, `ProgramRuleRecurrence`,
  `ProgramRuleValidityMode`, `ProgramScheduleUnknownRatingMode`
- [Interfaces](inventory/interfaces.md) — `IProgramScheduleService`, `IProgramScheduleSignal`
- [Tests](inventory/tests.md) — Test-Ausgangszustand, Testklassen, Hilfsmethoden, Nachweise
  unter `inventory/test-results/`
