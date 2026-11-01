# Bestandsaufnahme: Senderplanung fehlerhaft

Analysiert wurde die Sendeplan-Zusammenstellung (`ProgramScheduleService` samt
Datenmodell, Optionen, Worker und Tests) bezogen auf die Anforderung
„Senderplanung fehlerhaft" (`requirement.md`, Quelle `issue.md`): zwei belegte
Soll-Abweichungen (Lookback-Konsum beendeter Occurrences; Verdrängung bei
Materialwachstum) innerhalb eines umfangreich vorhandenen, durch Tests
festgeschriebenen Soll-Verhaltens.

## Zusammenfassung

- **Der komplette Planmechanismus existiert bereits.** `ProgramScheduleService`
  (3541 Zeilen, `VideoWebPlayer/Services/ProgramScheduleService.cs`) deckt alle
  in der Anforderung beschriebenen Mechanismen ab: Occurrence-Aufbau in lokaler
  Zeit mit 24-h-Rückblick (`OccurrenceLookback`, Z. 26), Strang-Interleaving für
  Genre-Material (`RuleState.SetItems`, Z. 1739), deterministische
  Hash-Mischung ungeordneter Ringe (`MixUnorderedRing`/`ItemOrderKey`/
  `Fnv1aHash`, Z. 1815–1848), persistierte Strang-Cursor
  (`ProgramRuleCursor` mit `StrandKey`, Unique-Index inkl. NULL-Variante),
  Karenzzeit (`RuleState.ClipGrace` → Abbruch der Titelauswahl,
  Z. 1723/2348/2249), Overshoot über das Periodenende, Clip-Füllung mit
  Round-Robin über Clip-Gruppen (`InsertClipFill`, Z. 3346), Altersfenster mit
  Skip/Deferral-Semantik (`ProgramScheduleAgeWindow.IsAllowedAt` in
  `SelectAiringItems`/`SelectAiringItemsInterleaved`), Trailer-Vorlauf
  (`FindLeadMatchOffset`/`_trailerLead`) und Reconcile mit
  Reproduzierbarkeitsprüfung (`AiringReproducible`/
  `FlatAiringTailReproducible`/`InterleavedAiringTailReproducible`,
  Z. 678–1226) samt Suffix-Verdrängung und `RewindCursor`.
- **Belegte Abweichung 1 — Lookback-Konsum.** In `MaterializeOccurrence`
  (Z. 2069) produziert eine im Rückblick komplett beendete Occurrence zwar
  keine Slot-Zeilen (`End > coverageStart`, Z. 2098), durchläuft aber
  `SelectItems` und `CommitTaken` (Z. 2084/2129): die gescannten
  Ringpositionen (Taken + Skips) werden committed und der Cursor persistiert —
  die gestrige, nie geplante Ausstrahlung verbraucht die ersten
  Ringpositionen. Testbeweis: `[6,7,8,1,2]` statt `[1..5]`.
- **Belegte Abweichung 2 — Mengenwachstum verdrängt Bestand.** Der gemeinsame
  Film-Strang `"other"` (und jeder Clip-/Film-Ring) ist ein ungeordneter Ring,
  dessen Ordnung `MixUnorderedRing` per `ItemOrderKey` über die jeweils
  aktuelle Item-Menge sortiert — ein neues Item verändert die gesamte
  Ringordnung. Die Reproduzierbarkeitsprüfung re-simuliert positionsbasiert
  (`FlatAiringTailReproducible`, Z. 753; `InterleavedAiringTailReproducible`,
  Z. 932) und schlägt dann fehl → `ReconcileAsync` verdrängt den kompletten
  Suffix ab der ersten invaliden Ausstrahlung (Z. 297–306). Testbeweis: je 18
  verdrängte Slots in den Szenarien 1.d und 1.e.
- **Keine Schemaänderung nötig** — Datenmodell, Interfaces und Konfiguration
  sind vollständig vorhanden (laut Anforderung nicht ableitbar; Backup-
  Registrierung aller Tabellen/Spalten in `VideoWebPlayerBackupData` bereits
  vorhanden).
- **Die Soll-Spezifikation liegt als Testcode vor** — 16 Tests in fünf neuen,
  uncommitteten Dateien plus `PrimetimeSeed`-Helper; sie laufen gegen echtes
  SQLite und den produktiven Service, gesteuert nur über `utcNow`.
- **Test-Ausgangszustand** (Nachweis: [inventory/tests.md](inventory/tests.md)):
  Release-Build fehlerfrei; Unit-Suite (`Category!=E2E`) 2607 Tests —
  **2603 grün, 3 rot, 1 übersprungen**; E2E-Suite (`Category=E2E`) 248 Tests —
  **247 grün, 0 rot, 1 übersprungen**; `Category=Integration` enthält keinen
  einzigen Test. Die 3 roten Tests sind exakt die dokumentierten
  Soll-Abweichungen; alle übrigen Szenarien (1.a–1.c, 1.f–1.l) sind grün und
  dienen als Regressionssuite.

## Details

- [Datenmodell](inventory/models.md) — `ProgramRule`, `ProgramSlot`,
  `ProgramRuleCursor`, `ProgramClipGroup`, Join-Entitäten, Backup-Registrierung
- [Logik](inventory/logic.md) — `ProgramScheduleService` (alle relevanten
  Methoden mit Zeilenangaben), `ProgramScheduleWorker`, Altersfenster,
  `ProgramScheduleOptions`, `ProgramRuleValidity`, Settings/RuleService-Anteile
- [Enums](inventory/enums.md) — `ProgramRuleType`, `ProgramRuleMaterialType`,
  `ProgramRuleRecurrence`, `ProgramRuleValidityMode`, `ProgramSlotStatus`,
  `ProgramCursorContentType`, `ProgramScheduleUnknownRatingMode`,
  `ProgramScheduleContentScope`, `AiringInvalidReason`
- [Interfaces](inventory/interfaces.md) — `IProgramScheduleService` inkl.
  Summary-Records, Randinterfaces `IProgramRuleService`,
  `IProgramScheduleSignal`
- [Tests](inventory/tests.md) — Test-Ausgangszustand mit Lauf-Nachweisen
  (TRX/Konsolenlogs unter `inventory/test-results/`), die 3 nachgewiesenen
  Fehlschläge, die 16 neuen Szenario-Tests, Hilfsklassen und bestehende
  Regressionstests des Bereichs
