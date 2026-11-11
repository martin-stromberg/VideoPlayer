# Plan-Gegenprüfung

Zweiter Lauf nach Nachplanung. Geprüft wurde die aktualisierte `plan.md` (Frische-Kriterium
zweistufig: (a) keine `ProgramRuleCursor`-Zeilen, (b) `winner.UpdatedAt > kept.CreatedAt`).
Die Befunde des ersten Laufs (`plan-check.md`) wurden gegen die aktuelle Fassung nachgeprüft;
die neue Stufe (b) wurde zusätzlich auf neue Probleme durchsucht (Konvergenz über mehrere
Frische-Runden, `UpdatedAt`-Seiteneffekte, UTC-/Kind-Konsistenz, Reihenfolge gegenüber
`Protected`/Filler/Gleichregel-Schutz) — codeverifiziert.

## Ergebnis

**Status:** Plan vollständig

## Abgleich Akzeptanzkriterien

| Akzeptanzkriterium | Umsetzung im Plan | Testnachweis im Plan | Status |
|--------------------|-------------------|----------------------|--------|
| Neu angelegte Regel materialisiert in bereits beplanten Tagen (freies Fenster wird eingefügt) | `Evicts`-Stufe (a) `!cursors.ContainsKey(winner.Id)` + Occurrence-Ordnung `Priority desc → StartUtc → Rule.Id` | `Reconcile_NewRuleFreeWindow_MaterializesInAlreadyPlannedDays` | Abgedeckt |
| Reaktivierte Regel materialisiert in bereits beplanten Tagen (auslösendes Ereignis der Anforderung) | `Evicts`-Stufe (b) `winner.UpdatedAt > kept.CreatedAt` — schließt die Lücke des ersten Laufs: Cursor überleben die Deaktivierung, aber `SetRuleActiveAsync`/`UpdateRuleAsync` setzen `UpdatedAt` (codeverifiziert: `ProgramRuleService.cs:177`, `:252`; Feld `ProgramRule.cs:210`) | `Reconcile_ReactivatedRule_MaterializesInAlreadyPlannedDays` — jetzt vollständig bestimmt: `R2.Id > R1.Id` (Seniorität allein würde verlieren), De-/Re-Aktivierung über produktionsidentischen `SetRuleActiveAsync`-Helfer, Lauf-1/2/3-Sequenz | Abgedeckt |
| Bearbeitete Regel (`UpdateRuleAsync` als auslösendes Ereignis) | Derselbe Stufe-(b)-Nachweis; `UpdateRuleAsync` setzt `UpdatedAt` (`ProgramRuleService.cs:177`) | Mechanismusdeckung über den Reaktivierungs-Test (identischer Vergleichspfad); siehe Hinweis | Abgedeckt |
| Überlapp: gehaltene Ausstrahlung wird hinter den neuen Block verschoben statt verworfen; höchstens der letzte Titel entfällt | Slot-genaue `DisplaceRuleFuture`-Generalisierung + Re-Materialisierung über eigene Occurrences (`ShiftPastInvaders`), Tail-Deferral über `SelectItems`-Bound | `Reconcile_NewRuleTailOverlap_ShiftsPrimetimeBlock`, `Reconcile_NewRuleOverlap_ShiftedBlockTailDefers` | Abgedeckt |
| Sammlungs-Kaskade: verworfener `MovieCollection`-Film kaskadiert; freie Blöcke mit früheren Sammlungsfilmen aufgefüllt; Folgeplan angepasst | `DisplaceRuleFuture`-Suffix + `RewindCursor` (`collection:{id}`-Strang) — Variante (b), begründet | `Reconcile_DroppedCollectionFilm_SequelsDisplaced_PredecessorsRefill` | Abgedeckt |
| Gewinner bei Gleichpriorität (frische Regel / jüngere Regeländerung) | Zwei Frische-Stufen vor Seniorität in `Evicts`; `cursors` ist `Dictionary<long, …>` im Scope (codeverifiziert: `ProgramScheduleService.cs:206-210`), `kept` ist `ProgramSlot` mit `CreatedAt` (`Evicts`-Signatur `:387`) | Tail-Overlap-Test, Reaktivierungs-Test, Fresh-vs-fresh-Test | Abgedeckt |
| Kein Eingriff in ausgestrahlte Vergangenheit / laufenden Sendeplatz (`Protected`, Issue #315) | `Protected(kept) → false` bleibt erste Prüfung vor allen neuen Stufen; Ausschluss 4 (bestätigt) | `Reconcile_NewRuleOverlap_RunningSlotStaysProtected` | Abgedeckt |
| Keine Neuverhandlung valider Inhalte: nur Einfügen, Verschieben, End-Kürzung | Mechanik verschiebt/verdrängt, ersetzt keine Inhalte; Ausschluss 5 (bestätigt) | Inhaltserhalt-Assertions in Tail-Overlap-/Defers-/Kaskaden-Tests | Abgedeckt |
| Prioritäts-Übernahme (#314/`WindowLostToTakeover`) unverändert; niedriger priore neue Regel verschiebt keinen höherprioren Block | `Priority` bleibt Primärschlüssel; Frische-Stufen greifen nur bei `winner.Priority == loser.Priority`; Ausschlüsse 1 + 6 (bestätigt) | `Reconcile_NewRuleLowerPriority_DoesNotShiftHigherPriorityAiring` (freies Präfix explizit assertet — Hinweis aus Lauf 1 umgesetzt), Suite `_PriorityTakeover` zur Re-Bewertung | Abgedeckt |
| Kein neues UI, automatische Anwendung im regulären Planungslauf; `ResetScheduleAsync` bleibt Fallback | Kein UI-Eingriff; Auslöser `NotifyScheduleChanged` → `ExecutePassAsync` → `ReconcileAsync` | Reale Abnahme als Akzeptanzszenario 1 festgeschrieben | Abgedeckt |
| Umgang mit fehlenden Objekten: `ProgramRuleCursor` via `PersistCursors`; Reste → `RemainingGapSeconds`/`FillGaps`; Auffüllung via `RewindCursor` | In Scope-Klärung und Programmablauf übernommen, alles Bestandsmechanismen | Idempotenz-Test (`AssertGapless`), Kaskaden-Test | Abgedeckt |
| Idempotenz / Konvergenz: Folgelauf No-Op, keine Oszillation, keine Selbst-Verdrängungs-Zwillinge | Senioritäts-Fallback deterministisch; Konvergenz der Frische-Stufen begründet (verdrängte Belegung wird mit `CreatedAt > winner.UpdatedAt` neu materialisiert, Gewinner erhält Cursor-Zeilen → beide Stufen erlöschen); Fresh-vs-fresh-Übergang als begrenzter Churn benannt | `Reconcile_NewRuleIsIdempotent_SecondRunNoOp`, `Reconcile_EqualPriorityEstablishedRules_DeterministicNoOscillation`, `Reconcile_FreshVsFresh_SameWindow_ConvergesDeterministic` | Abgedeckt |
| Fresh-vs-fresh-Randfall (zwei neue gleichpriore Regeln, selbes Fenster) | Designentscheidung benannt: Lauf 1 Iterationsreihenfolge, Lauf 2 verdrängt die noch cursorlose Regel, ab Lauf 3 Seniorität → ältere Regel gewinnt endgültig; max. zwei Gewinnerwechsel, konvergent (Ablauf codeverifiziert nachvollziehbar) | `Reconcile_FreshVsFresh_SameWindow_ConvergesDeterministic` — schließt die Lücke des ersten Laufs | Abgedeckt |
| Keine Fensterkonflikt-Verdrängung durch etablierte Regel ohne Re-Aktivierung/Bearbeitung (Ausschluss 7) | Mechanismus konsistent: ohne `UpdatedAt`-Bump bleibt Stufe (b) gegen jede nach der letzten Änderung materialisierte Belegung wirkungslos; Stufe (a) greift nie (Cursor vorhanden) | Indirekt über `Reconcile_EqualPriorityEstablishedRules_*` (beide Seiten `UpdatedAt` älter als die Belegung → Seniorität, No-Op); expliziter Ausschluss-7-Negativtest optional, s. Hinweis | Abgedeckt |
| Testvorgabe: realistischer Bestand (Serien 3–8 Staffeln à 10–20 Episoden, variierende Längen, Doppelfolgen, Sammlungen; echtes SQLite) | `CreateMultiSeasonShowAsync`, `CreateCollectionAsync`, `SeedRealisticLibraryAsync` in `ProgramScheduleTestDb`; `CreateRuleAsync` um `createdUtc`/`updatedUtc`, neuer `SetRuleActiveAsync`-Helfer | `Reconcile_NewRuleRealisticLibrary_FullScenario` | Abgedeckt |
| Beantwortung der offenen Fragen der Anforderung | Alle fünf in „Offene Punkte" beantwortet; Antwort zu Frage 2 ist jetzt konsistent mit dem Mechanismus (reaktiviert/bearbeitet = Stufe b; Belegungsverlust ohne Ereignis = Ausschluss 7) — Widerspruch des ersten Laufs behoben | — | Abgedeckt (Rest-Inkonsistenz im Status-Label s. Hinweise) |
| Slot-genaue Verdrängung mit Präfix-Erhalt bei später startender neuer Occurrence (Ausschluss 3) | `DisplaceRuleFuture(loserRuleId, loser.Start)` statt `airing[0].Start`; deterministische Loser-Reihenfolge | `Reconcile_NewRuleLaterStart_PreservesAiringPrefix` — Geometrie jetzt festgelegt (Occurrence-Start streng innerhalb eines Slots, Konflikt ab Folge-Slot) | Abgedeckt |

## E2E-Abdeckung

| Benutzerfluss / Akzeptanzkriterium | Geplanter E2E-Test | Status |
|------------------------------------|--------------------|--------|
| Regel anlegen/aktivieren in `TVSenderAdmin.razor` → Wirkung im Sendeplan-Tab/`LiveTv.razor` | Keine neuen E2E-Tests — begründet: kein neuer/geänderter Benutzerfluss, reine Planungs-Semantik auf Service-Ebene (echtes SQLite); verbindliche reale End-to-End-Abnahme über Akzeptanzszenario 1 (Anwendung starten, Regel über UI anlegen, Sendeplan prüfen) | Nicht erforderlich mit Begründung (reale Abnahme vorgesehen) |

## Hinweise

- **Veraltete Status-Angabe in „Offene Punkte":** Die Antwort zu Frage 2 verweist auf
  „(Scope-Ausschluss 7, Status `offen`)", die Ausschluss-Tabelle und die Anwender-Bestätigung
  sagen `bestätigt (11.11.2026, Issue-Kommentar)`. Nur das Label ist veraltet — die fachliche
  Festlegung ist konsistent. Bei der Umsetzung oder dem Planungscommit das Wort `offen`
  dort zu `bestätigt` korrigieren (keine inhaltliche Nachplanung nötig).
- **Befund 1 des ersten Laufs (reaktivierte Regeln) — geschlossen und gegen neue Probleme geprüft:**
  - *Konvergenz:* Verifiziert. Gewinnt die frische/bearbeitete Regel, werden die verdrängten
    Slots mit `CreatedAt = DateTime.UtcNow` neu materialisiert (`ProgramScheduleService.cs:1195`)
    → Stufe (b) erlischt gegen sie; der Gewinner erhält Cursor-Zeilen → Stufe (a) erlischt.
    Weitere Frische-Runden entstehen ausschließlich durch erneutes Bearbeiten/Aktivieren
    (nutzergebunden, endlich) — im Risiko-Abschnitt korrekt benannt.
  - *`UpdatedAt`-Seiteneffekt:* Jede `UpdateRuleAsync`-Änderung (auch Umbenennung) ist eine
    Frische-Runde. Als bewusste Konsequenz dokumentiert und fachlich gedeckt — die Anforderung
    nennt `UpdateRuleAsync` selbst als auslösendes Ereignis.
  - *UTC-/Kind-Konsistenz:* `ProgramRule.UpdatedAt` (`ProgramRule.cs:210`), `ProgramSlot.CreatedAt`
    (`ProgramSlot.cs:93`) und alle Schreibstellen verwenden `DateTime.UtcNow`; beide Werte werden
    aus SQLite kind-konsistent (unspecified) geladen — der Vergleich ist numerisch konsistent.
  - *Reihenfolge in `Evicts`:* `Protected → Filler → unauflösbarer Verlierer → Gleichregel →
    Priorität → Frische-Stufen → Seniorität` ist im Plan korrekt fixiert; kritisch ist der
    Gleichregel-Check **vor** den Frische-Stufen — ohne ihn würde eine bearbeitete Regel ihre
    eigene gehaltene Belegung verdrängen. In allen drei Plan-Abschnitten konsistent beschrieben.
  - *Backup/Restore:* `ProgramRule.UpdatedAt`/`CreatedAt` und `ProgramSlot.CreatedAt` sind
    Kernspalten (nicht in `OptionalRestoreColumns`) → in Backups seit Tabelleneinführung
    enthalten, Restore erhält die Werte → keine Schein-Frische nach einem Restore.
- **Befund 2 des ersten Laufs (Widerspruch in „Offene Punkte", Frage 2) — geschlossen:** Die
  Antwort unterscheidet jetzt sauber: reaktiviert/bearbeitet ⇒ Stufe (b) trägt die Materialisierung;
  etablierte Regel ohne Re-Aktivierung/Bearbeitung ⇒ ausdrücklich nicht frisch (Ausschluss 7).
  Deckt sich mit dem Mechanismus (Cursor bleiben, `UpdatedAt` alt → beide Stufen greifen nicht).
- **Befund 3 (unterbestimmter Reaktivierungs-Test) — geschlossen:** Id-Verhältnis `R2.Id > R1.Id`,
  produktionsidentischer `SetRuleActiveAsync`-Helfer (`IsActive` + `UpdatedAt`) und die
  Lauf-1/2/3-Sequenz sind festgelegt; neue `createdUtc`/`updatedUtc`-Parameter an
  `CreateRuleAsync` machen die Zeitstempel deterministisch steuerbar.
- **Befund 4 (fehlender Fresh-vs-fresh-Test) — geschlossen:** Test
  `Reconcile_FreshVsFresh_SameWindow_ConvergesDeterministic` plus Designentscheidung und
  Risiko-Eintrag; der beschriebene Ablauf (Lauf 1 Iterationsreihenfolge → Lauf 2 verdrängt die
  noch cursorlose Regel → ab Lauf 3 Seniorität) ist gegen den Code nachvollziehbar
  (`newSlots`-Konflikt ⇒ atomarer Verwurf, `MaterializeOccurrenceEvicting`, Zeile 1177).
- **Optionaler Zusatztest (kein Blocker):** Ein dedizierter Negativtest zum Ausschluss-7-Fall
  (etablierte Regel verliert Belegung durch Invalidierung, wird nicht bearbeitet/aktiviert,
  trifft auf gleichpriore gehaltene Belegung → gewinnt nicht) sowie ein Stufe-(b)-Test über den
  `UpdateRuleAsync`-Pfad würden die Ausschluss-/Trigger-Abdeckung explizit machen; der
  Mechanismus ist bereits über `Reconcile_EqualPriorityEstablishedRules_*` und den
  Reaktivierungs-Test belegt.
- **Kleinere Formulierungsungenauigkeit:** Das „Tail-Verlierer"-Beispiel (Slot beginnt vor
  `occurrence.StartUtc`) kann über `conflicts` praktisch nicht auftreten — `ShiftPastInvaders`
  schiebt den Start hinter jeden überspannenden Slot (`ProgramScheduleService.cs:1484` ff.),
  sodass `competitors` faktisch alle Konflikt-Slots umfasst. Der Plan-Aussage „verhalten sich
  wie bisher" ist damit in jede Richtung sicher; bei der Umsetzung nicht weiter beachten.
