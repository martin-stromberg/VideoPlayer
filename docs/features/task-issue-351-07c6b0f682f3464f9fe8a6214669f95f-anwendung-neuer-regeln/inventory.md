# Bestandsaufnahme: Anwendung neuer Regeln auf den bestehenden Sendeplan (Issue #351)

Analysiert wurde der Sendeplan-Bereich (`ProgramScheduleService`/`ProgramRule`) bezogen auf die
Anforderung, dass eine neu angelegte `ProgramRule` auch in bereits beplanten Tagen materialisiert —
mit Verschiebe- statt Verwurf-Semantik für belegte Fenster und Sammlungs-Kaskade für verworfene
`MovieCollection`-Filme.

## Zusammenfassung

- **Kernmechanismus vorhanden:** `ReconcileAsync` materialisiert Occurrences aller aktiven
  Nicht-Filler-Regeln über `MaterializeOccurrenceEvicting` im gesamten Horizont neu — auch in
  bereits beplanten Tagen. Ein Verdrängungs-Pfad mit Verlierer-Rückgabe,
  `DisplaceRuleFuture`-Suffix-Verdrängung und `RewindCursor`-Strang-Rückspulung existiert bereits.
- **Verifizierte Ursache des Symptoms:** Eine neue Regel verliert jeden Fensterkonflikt gegen
  gehaltene Slots gleicher Priorität. `Evicts` lässt bei Gleichstand die kleinere `ProgramRule.Id`
  gewinnen (`ProgramScheduleService.cs:398`) und `MaterializeOccurrenceEvicting` verwirft bei
  **einem einzigen** nicht verdrängbaren Konkurrenten (z. B. die in die Primetime hineinragende
  letzte Episode) die gesamte Occurrence atomar (`summary.DroppedConflicts++`,
  `ProgramScheduleService.cs:1177-1181`). Eine neu angelegte Regel trägt stets die höchste Id und
  kann daher nie einen gleichprioren Konkurrenten verdrängen — sie materialisiert nur in Fenstern
  ohne Nicht-Filler-Konkurrenz (Filler-Slots verlieren immer) bzw. erst am neu angehängten
  Horizont-Tag über `EnsureCoverageAsync`.
- **Verschiebe-Semantik existiert teilweise:** Verliert ein gehaltener Block, wird er heute
  komplett samt Regel-Suffix verdrängt (`DisplaceRuleFuture`, `ProgramScheduleService.cs:360`)
  und über die eigenen späteren Occurrences neu materialisiert — für strikt niederpriorere
  Verlierer noch im selben Lauf (belegt durch `ProgramScheduleServiceTests_PriorityTakeover`).
  Eine „Verschiebe denselben Block hinter den neuen Block, kürze nur das Ende"-Semantik gibt es
  nicht; die End-Kürzung ergibt sich heute indirekt über den Bound-Check der Auswahl.
- **Sammlungs-Kaskade fehlt:** Es gibt keinen Mechanismus, der beim Verwurf eines `Sequenced`-
  Films (`collection:{id}`-Strang) dessen spätere Fortsetzungen im Plan mitverwirft und die
  freien Blöcke mit früheren Sammlungsfilmen auffüllt. `RewindCursor` spult zwar je Strang zum
  Vorgänger des frühesten verdrängten Inhalts zurück, gilt aber nur für tatsächlich `Displaced`
  gesetzte Slots und nur innerhalb derselben Regel.
- **Modelle reichen aus (verifiziert):** `ProgramSlot.Status` (`Scheduled`/`Displaced`),
  `IsClipFill`, `SchedulableItem.Sequenced` + `StrandKey` (`collection:{id}`),
  `ProgramRuleCursor.StrandKey` tragen alle benötigten Informationen — kein erkennbarer
  Schema-/Migrations- und kein Backup-/Restore-Bedarf (Annahme der Anforderung bestätigt).
- **Kein neues UI nötig:** Auslöser (`ProgramRuleService.CreateRuleAsync`/`UpdateRuleAsync`/
  `SetRuleActiveAsync` → `IProgramScheduleSignal.NotifyScheduleChanged()`) und Sichtbarkeit
  (`GetScheduleAsync`, `TVSenderAdmin.razor` Sendeplan-Tab, `ProgramGuideService`/`LiveTv.razor`)
  sind vorhanden; „Jetzt generieren" ruft `NotifyScheduleChangedNow()`.

**Test-Ausgangszustand:** `dotnet build VideoPlayer.sln -c Debug` fehlerfrei (139 Warnungen,
bestehend); `dotnet test VideoWebPlayer.Tests/VideoWebPlayer.Tests.csproj -c Debug
--filter "Category!=E2E"` → **2695 Tests: 2694 bestanden, 0 fehlgeschlagen, 1 übersprungen**
(unix-only). Nachweis und Details: [tests.md](inventory/tests.md). Tests mit Trait
`Category=E2E` wurden in diesem Lauf nicht ausgeführt (laufen in der CI als eigener Job).

## Details

- [Datenmodell](inventory/models.md) — `ProgramRule`, `ProgramSlot`, `ProgramRuleCursor`,
  `Movie`/`MovieCollection`-Bezug, `Setup`-Einstellungen
- [Logik](inventory/logic.md) — `ProgramScheduleService` (Reconcile, Materialisierung, Stränge,
  Clip-Füllung, Lückenfüllung), `ProgramScheduleWorker`, `ProgramRuleService`, Signal
- [Enums](inventory/enums.md) — `ProgramSlotStatus`, `ProgramRuleType`, `ProgramRuleRecurrence`,
  `ProgramRuleMaterialType`, `ProgramRuleValidityMode`, `ProgramCursorContentType`,
  `AiringInvalidReason`
- [Interfaces](inventory/interfaces.md) — `IProgramScheduleService`, `IProgramScheduleSignal`,
  `IProgramRuleService` (relevante Methoden)
- [Tests](inventory/tests.md) — Test-Ausgangszustand mit Nachweisen, betroffene Testklassen
  und Hilfsmethoden
