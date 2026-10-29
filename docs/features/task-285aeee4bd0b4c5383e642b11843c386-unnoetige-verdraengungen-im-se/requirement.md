# Anforderung: Unnötige Verdrängungen im Sendeplan vermeiden (Issue #310)

Quelle: GitHub-Issue #310, Branch `task-285aeee4bd0b4c5383e642b11843c386-unnoetige-verdraengungen-im-se`.
Kontext: verwandtes, bereits umgesetztes Issue #290 (stabile Neu-Bewertung, `SlotMatchesItem`,
`MediaItemAlternates`, `_invaderStarts`-Evidenz — siehe
`docs/features/task/issue-290-45bd50bf6fec48118ee38823c67a41be-tv-sender-wiederholte-verdraen-tasks.md`)
sowie Issue #297 (deterministische Ringmischung `MixUnorderedRing`).

## Fachliche Zusammenfassung

Im Sendeplan des automatischen TV-Senders häufen sich verdrängte Sendeplätze
(`ProgramSlot` mit `Status == ProgramSlotStatus.Displaced`), deren Neuplanung denselben Inhalt
wieder einplant — „durch sich selbst ersetzt" (Beobachtung: dieselbe Sendung z. B. fünfmal
gelistet, einmal „Geplant", viermal „Verdrängt"). Ursache ist die Neu-Bewertung des Sendeplans
(`ProgramScheduleService.ReconcileAsync`, im `ProgramScheduleWorker` periodisch und über
`IProgramScheduleSignal` getriggert): Erklärt sie eine bestehende Ausstrahlung für ungültig,
wird sie verdrängt und per `MaterializeOccurrenceEvicting` neu materialisiert — kommt dabei
derselbe Inhalt heraus, war die Regelanwendung unnötig.

Gefordert ist eine Gültigkeitsprüfung auf Ebene des Sendeblocks (fachlich eine Ausstrahlung;
technisch die lückenlose `ProgramSlot`-Gruppe einer `ProgramRule`, wie `GroupAirings` sie
bildet): Sind die vorhandenen Sendungen für die Regel weiterhin zulässig, bleibt der Block
unverändert bestehen und wird nicht neu geplant. Ergebnis: keine Selbst-Verdrängungs-Duplikate
mehr; ein Reconcile-Lauf ohne sachliche Eingabeänderung bleibt ein No-Op
(`SlotsDisplaced == 0`, `SlotsCreated == 0`).

Der Prüfpunkt existiert bereits: `ReconcileWindow.Validate` bewertet jede Ausstrahlung über
`AiringReproducible` (Flat-/Interleaved-Tail-Checks mit dem toleranten Vergleich
`SlotMatchesItem` aus Issue #290). Die Anforderung zielt darauf, dass diese Prüfung zulässige
Blöcke nicht mehr fälschlich für ungültig erklärt — ob dafür die Prüfung gelockert
(Zulässigkeits- statt exakter Reproduzierbarkeits-Kriterium) oder Restfehler in der
Reproduzierbarkeits-Simulation behoben werden müssen, ist Gegenstand der Umsetzung
(siehe „Offene Fragen").

## Betroffene Klassen und Komponenten

- `ProgramScheduleService` (`VideoWebPlayer/Services/ProgramScheduleService.cs`):
  - `ReconcileAsync` — Fenster laden (`LoadWindowAsync`), Suffix-Verdrängung über
    `InvalidGroups`, Neu-Materialisierung über `MaterializeOccurrenceEvicting`.
  - `ReconcileWindow` (`Rebuild`, `Validate`, `Reload`) — Gruppierung der Scheduled-Slots zu
    Ausstrahlungen (`GroupAirings`), Occurrence-Anker (`_occurrenceStarts`), Invader-Evidenz
    (`_invaderStarts`), Einzelprüfung der `FillerSlots`.
  - `AiringReproducible` (beide Overloads), `FlatAiringTailReproducible`,
    `InterleavedAiringTailReproducible`, `SlotMatchesItem` — die eigentliche
    Block-Gültigkeitsprüfung; Erweiterungspunkt der Anforderung.
  - `RewindCursor`, `ShiftPastInvaders`, `BoundStarts`, `InsertClipFill`,
    `ClipMediaItemIds` — mitbetroffene Mechanik rund um Verdrängung und Neuplanung.
- Interne Typen derselben Datei: `RuleState`, `StrandState`, `ClipGroupState`,
  `SchedulableItem` (`MediaItemAlternates`), `MediaItemChoice`/`MediaItemVariant`,
  `ReproContext`, `SummaryCounters`, `Occurrence`.
- `ProgramScheduleWorker` — Aufrufkette `PurgeExpiredRulesAsync` → `ReconcileAsync` →
  `EnsureCoverageAsync`; `EnsureCoverageAsync` selbst verdrängt nicht, plant aber hinter
  dem Reconcile an.
- Datenmodell: `ProgramSlot` (`Status`, `IsClipFill`), `ProgramRuleCursor` — kein neues
  Datenmodell oder Migration absehbar.
- UI: `TVSenderAdmin.razor` (Sendeplan-Anzeige mit „Verdrängt"-Chip, Aktion „Verdrängte
  bereinigen" → `DeleteDisplacedSlotsAsync`) — voraussichtlich keine Änderung, aber
  Beobachtungsort des gemeldeten Symptoms.
- Tests: `ProgramScheduleTestDb` (u. a. `AssertReconcileNoOpAsync` aus Issue #290),
  Reconcile-Testklassen `ProgramScheduleServiceTests_*`, E2E-Test
  `TVSenderAdmin_RepeatGenerate_ProducesNoDisplacedSeries` — neue Reproduktions- und
  Regressionstests gegen echtes SQLite.

## Implementierungsansatz

- Erweiterungspunkt ist die Block-Validierung in `ReconcileWindow.Validate` /
  `AiringReproducible`: Hier entscheidet sich, ob ein Sendeblock verdrängt und neu geplant
  wird. Die Anforderung verlangt, dass ein Block, dessen Sendungen für die Regel zulässig
  sind, bestehen bleibt.
- Voraussichtliches Vorgehen (Annahme): zuerst ein fail-first-Reproduktionstest gegen echtes
  SQLite, der eine Selbst-Verdrängung nachweist (verdrängter Slot wird durch identischen
  Inhalt ersetzt), dann die Behebung. Denkbare Richtungen:
  a) Restfehler der bestehenden Reproduzierbarkeitsprüfung diagnostizieren und beheben
     (z. B. Occurrence-Ankerung, Cursor-/Ring-Position, Clip-Fill-Anteile,
     Altersfenster-Bewertung, Zeitbasis), sodass zulässige Blöcke nicht mehr als invalid
     klassifiziert werden.
  b) Die Prüfung fachlich auf „Zulässigkeit" ausrichten: Mitgliedschaft der hinterlegten
     Inhalte im expandierten Regelmaterial (inkl. `MediaItemAlternates`), lückenlose
     Verkettung, zulässige Reihenfolge (`PreservesOrder`-Stränge) und gültiger
     Occurrence-Anker — ohne die exakte Cursor-getriebene Auswahl erneut simulieren zu
     müssen.
  Welche Richtung dem Fehlerbild entspricht, entscheidet die Reproduktion; beide bleiben
  im bestehenden Muster (kein neuer Service, kein neues Interface absehbar).
- Wechselwirkungen, die bei jeder Lösung zu prüfen sind: Suffix-Verdrängung ab
  `firstInvalid` (eine invalide Ausstrahlung zieht die gesamte Regel-Zukunft nach sich),
  `RewindCursor` (Cursor-Rücklauf auf verdrängte Inhalte), `_invaderStarts`-Evidenz aus
  `Displaced`-Zeilen (Bereinigung verändert die Beweislage), Clip-Fill-Slots
  (`IsClipFill`) und die Einzelprüfung der Filler-Slots.
- Abhängigkeiten: ausschließlich bestehende Komponenten (`ProgramScheduleService`,
  `ProgramRuleService`/`_ruleService`, `ProgramSettingsService`, EF-Core-SQLite). Keine
  neuen NuGet-Pakete, kein UI-Eingriff absehbar.

## Konfiguration

Kein Konfigurationsbedarf erkennbar — das Vermeiden unnötiger Verdrängungen soll
unbedingt gelten. (Ein umschaltbarer strenger/toleranter Prüfmodus über
`ProgramSettingsService` wäre denkbar, ist aber aus der Anforderung nicht ableitbar.)

## Offene Fragen

- Was zählt fachlich als „für die Regel zulässig"? Annahme: Der Inhalt stammt aus dem
  aktuell expandierten Material der Regel, die Sendereihenfolge geordneter Stränge ist
  unverletzt, die Verkettung ist lückenlos, ein zulässiger Occurrence-Anker existiert und
  das Jugendschutzfenster wird eingehalten. Grenzfall: Der Block ist zulässig, läge aber an
  einer anderen Ring-/Cursor-Position als die Neusimulation — behalten oder neu planen?
- Was bedeutet „durch sich selbst ersetzt" genau — identische `MediaItemId` am selben
  Sendeplatz, oder gleicher Inhalt (`MovieId`/`TVShowEpisodeId`) mit anderer Datei bzw.
  leicht verschobener Zeit? Das bestimmt das Akzeptanzkriterium (z. B. „kein `Displaced`-
  Slot, zu dem ein `Scheduled`-Slot mit gleichem Inhalt und gleicher Zeit existiert").
- Sollen die bereits angehäuften `Displaced`-Duplikate automatisch bereinigt werden, oder
  genügt die vorhandene Admin-Aktion „Verdrängte bereinigen" (`DeleteDisplacedSlotsAsync`)?
  Zu beachten: Die Bereinigung entfernt `_invaderStarts`-Evidenz und kann einmalig echte
  Neuplanungen auslösen (dokumentierte Wechselwirkung aus Issue #290).
- Betrifft die Beobachtung nur Titel-Ausstrahlungen oder auch Clip-Fill-Blöcke
  (`IsClipFill`) und Filler-Slots? Annahme: alle Slot-Arten, da die Anforderung den
  „Sendeblock" allgemein nennt.
- Ist ein Diagnose-Ausbau gewünscht (z. B. Zähler/Log, welcher Teil der Prüfung einen Block
  für ungültig erklärte), damit künftige Selbst-Verdrängungen im Betrieb nachweisbar
  bleiben? Annahme: sinnvoll, aber nicht Kern der Anforderung.
