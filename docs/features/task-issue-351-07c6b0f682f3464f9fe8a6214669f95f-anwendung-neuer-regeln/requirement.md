# Übersetzte Anforderung: Anwendung neuer Regeln auf den bestehenden Sendeplan (Issue #351)

## Fachliche Zusammenfassung

Die Sendeplan-Neubewertung (`ProgramScheduleService.ReconcileAsync` im Rahmen von `ExecutePassAsync`/`ProgramScheduleWorker`) wird so erweitert, dass eine neu angelegte oder reaktivierte `ProgramRule` auch innerhalb des bereits beplanten Horizonts materialisiert — nicht erst am Ende der Belegung über `EnsureCoverageAsync`. Belegt die neue Occurrence ein freies Fenster eines bereits geplanten Tages, wird sie dort eingefügt; überlappt ihr Ende (z. B. die letzte Serienepisode) den Kopf einer bereits geplanten Ausstrahlung, wird die bestehende Ausstrahlung hinter den neuen Block verschoben statt verworfen — höchstens ihr letzter Titel fällt aus dem Fenster. Wird dabei ein Film einer `MovieCollection` verworfen, kaskadiert der Verwurf auf alle im Plan folgenden Fortsetzungen desselben Sammlungs-Strangs; die frei werdenden Blöcke werden mit den jeweils früheren Sammlungsfilmen aufgefüllt und der Folgeplan zeitlich angepasst.

## Fachlicher Scope

- **Verarbeitungseinheit:** Die Regel-Occurrence — eine aktive `ProgramRule` an einem nominalen lokalen Startzeitpunkt (`Occurrence` aus `BuildOccurrences`) — samt ihrer materialisierten Ausstrahlung (zusammenhängende `ProgramSlot`-Gruppe, `GroupAirings`-Block) innerhalb des Planungshorizonts. Sekundär betroffen: die Ausstrahlungsblöcke, deren Fenster die neue Occurrence überlappt.
- **Auslösende Ereignisse:** Anlegen oder (Re-)Aktivieren einer `ProgramRule` über `ProgramRuleService.CreateRuleAsync`/`UpdateRuleAsync` (mit `IProgramScheduleSignal.NotifyScheduleChanged()`); wirksam wird die Änderung beim nächsten `ProgramScheduleWorker`-Lauf bzw. `ExecutePassAsync` → `ReconcileAsync`. Gleiches gilt fachlich für jede Regel, deren Occurrences bislang keine Belegung erzeugt haben.
- **Erwartete Wirkung:** Nach dem nächsten Planungslauf tragen auch die bereits beplanten Tage `ProgramSlot`s der neuen Regel — sichtbar über `GetScheduleAsync`, den Sendeplan-Tab in `TVSenderAdmin.razor` und die Programmauskunft (`ProgramGuideService`/`ProgramGuideController`, `LiveTv.razor`). Bereits geplante Ausstrahlungen bleiben inhaltlich erhalten und werden höchstens zeitlich verschoben bzw. am Ende gekürzt; der neue Programmblock erscheint nicht erst am neu angehängten Horizont-Tag.
- **Umgang mit fehlenden Objekten:**
  - `ProgramRuleCursor` der neuen Regel existiert noch nicht → beim ersten Commit der Auswahl über `PersistCursors` erzeugen (bestehender Mechanismus).
  - Freie Fenster für die verschobene Ausstrahlung existieren ggf. nicht → durch die Verschiebung selbst erzeugen; nicht platzierbare Reste gehen wie bisher in `SummaryCounters.RemainingGapSeconds` und an `FillGaps`.
  - Frei werdende Zeitblöcke verworfener Sammlungs-Fortsetzungen → Auffüllung mit früheren Filmen desselben `collection:{id}`-Strangs über den vorhandenen `RewindCursor`-Mechanismus (Wiederverwendung).
- **Scope-Ausschlüsse:**
  - Kein Eingriff in vollständig ausgestrahlte Vergangenheit (`slot.End <= coverageStart`) und kein Schneiden des laufenden Sendeplatzes — der `Protected`-Schutz aus Issue #315 bleibt unverändert.
  - Keine Neuverhandlung gültiger Inhalte: `SlotStillValid`-valide Slots werden nicht wegen der neuen Regel durch andere Inhalte ersetzt; es geht ausschließlich um Einfügen, Verschieben und End-Kürzung.
  - Kein neues UI-Element und keine Pflicht-Aktion des Anwenders: die Anwendung erfolgt automatisch im regulären Planungslauf; `ResetScheduleAsync` („Plan zurücksetzen") bleibt als manueller Fallback unverändert.
  - Die Prioritäts-Übernahme für echte Rangkonflikte (streng höhere `Priority` übernimmt ein Fenster komplett, Issue #314 / `WindowLostToTakeover`) ist nicht Gegenstand der Anforderung; ihr Zusammenspiel mit der neuen Verschiebe-Semantik ist als offene Frage geführt.

## Betroffene Klassen und Komponenten

- `VideoWebPlayer/Services/ProgramScheduleService.cs`
  - `ReconcileAsync` — Occurrence-Schleife, `Evicts`-Prädikat, `DisplaceRuleFuture`, `Protected`
  - `MaterializeOccurrenceEvicting` — bisheriges Verhalten: nicht verdrängbare Überlappung lässt die komplette neue Occurrence fallen (`summary.DroppedConflicts`)
  - `ShiftPastInvaders`, `WindowLostToTakeover`, `BoundaryAfter`/`BoundStarts` — Verschiebe- und Fensterende-Semantik
  - `ReconcileWindow` (`Reload`/`Rebuild`/`Validate`, `Airings`, `Occurrences`, `BoundStarts`) und `GroupAirings` — Blockgrenzen und Anker der gehaltenen Ausstrahlungen
  - `RewindCursor`, `PersistCursors` — Fortsetzungsstand der Stränge (`show:{id}`, `collection:{id}`, `clipgroup:{id}`)
  - `InsertClipFill`/`FillAiringSegments` — Neuverteilung der Platzhalter-Clips nach Verschiebung; `FillGaps` — Restlücken
  - `SummaryCounters`/`ProgramScheduleReconcileSummary` — ggf. Zähler für verschobene Ausstrahlungen
  - `RuleState`/`StrandState`/`SchedulableItem` (`Sequenced`, `PreservesOrder`) — geordnete Sammlungs- und Episoden-Stränge (Issue #334)
- `VideoWebPlayer/Data/ProgramRule.cs`, `ProgramSlot.cs` (`ProgramSlotStatus.Displaced`), `ProgramRuleCursor.cs` — bestehende Modelle, voraussichtlich ohne Schema-Änderung
- `IProgramScheduleService` — voraussichtlich unverändert (interne Verhaltensänderung)
- Tests: neue `ProgramScheduleServiceTests_*`-Datei(en) unter `VideoWebPlayer.Tests/`; `VideoWebPlayer.Tests/Helpers/ProgramScheduleTestDb.cs` um einen praxisnahen Bestand zu erweitern (Filme und `MovieCollection`s; Serien mit 3–8 Staffeln à 10–20 Episoden; nahezu, aber nicht exakt gleiche Episodenlängen sowie vereinzelte Episoden mit doppelter Länge)
- Dokumentation: `docs/help/tv-sender-ablauf-technisch.md` (Abschnitt Neu-Bewertung), `docs/RELEASE_NOTES.md`, `README.md` bei Bedarf

## Implementierungsansatz

- Erweiterung des Reconcile-Materialisierungspfads: Occurrences von Regeln ohne bisherige Belegung sollen nicht nur in garantiert freiem Raum materialisieren. Überlappt die materialisierte Ausstrahlung gehaltene Slots, sollen die betroffenen gehaltenen Ausstrahlungen `Displaced` und hinter dem Ende des neuen Blocks verschoben neu materialisiert werden (Verschiebe-Semantik analog `ShiftPastInvaders`), statt die neue Occurrence wegen eines einzelnen nicht verdrängbaren Konkurrenten atomar zu verwerfen. Passt der verschobene Block nicht mehr vollständig in sein Fenster (Bound), entfällt nur der überstehende Rest — in der Regel der letzte Titel.
- Annahme (in der Bestandsaufnahme zu verifizieren): Heute verliert eine neue Regel jeden Fensterkonflikt gegen gehaltene Slots gleicher Priorität, weil `Evicts` bei Gleichstand die kleinere `ProgramRule.Id` gewinnen lässt und `MaterializeOccurrenceEvicting` bei jedem nicht verdrängbaren Konkurrenten die gesamte Occurrence verwirft; bereits eine in die Primetime hineinragende letzte Episode genügt. Ergänzend zu prüfen: Iterationsreihenfolge (`Priority` desc, `Id`, `StartUtc`) und ob die Neu-Materialisierung des verdrängten Blocks noch im selben Lauf oder erst im Folgelauf erfolgt.
- Sammlungs-Kaskade: Entfällt beim Verschieben/Kürzen ein Film mit `Sequenced`-Kennzeichnung (`collection:{id}`-Strang), sind alle späteren Fortsetzungen dieses Strangs im Planfenster ebenfalls zu verdrängen. Für die frei werdenden Blöcke ist eine der beiden vom Kunden genannten Varianten nach Aufwand-Nutzen zu wählen: (a) den gesamten Block verwerfen oder (b) nur die Filme ab der Fortsetzung verwerfen und die jeweils vorherigen Sammlungsfilme an deren Stelle erneut einplanen (unter der Annahme annähernd gleicher Länge); danach werden die folgenden Filme zeitlich angepasst.
- Abhängigkeiten: `ReconcileWindow` für gehaltene Airings und Bound-Berechnung; `AnchorProvable`/`SlotStillValid` bleiben die Gültigkeitsbasis; `ProgramRuleCursor`-Persistenz trägt den Strang-Rewind. Kein erkennbarer Schema-/Migrations- und Backup-/Restore-Bedarf (Annahme, zu verifizieren).
- Testvorgabe: ausgiebige Verifikation gegen echten Bestand — großzügiger Film-/Filmsammlungs- und Serienbestand (3–8 Staffeln, 10–20 Episoden je Staffel, nahezu gleiche Episodenlängen mit gelegentlichen Doppelfolgen); Tests laufen gegen echtes SQLite über `ProgramScheduleTestDb`.

## Konfiguration

Keine neue Konfiguration: Das Verhalten gilt automatisch für jede neu belegende Occurrence im Reconcile-Lauf. Die Wahl zwischen den beiden Varianten für Fortsetzungs-Blöcke („gesamter Block verwerfen" vs. „ab Fortsetzung verwerfen und frühere Sammlungsfilme auffüllen") ist eine Implementierungsentscheidung nach Aufwand-Nutzen und keine Nutzer-Einstellung.

## Offene Fragen

- Gilt die Verschiebe-Semantik nur bei gleicher oder niedrigerer `Priority` des Inhabers — oder soll eine neue Regel mit niedrigerer `Priority` ebenfalls einen höherprioren Block verschieben dürfen (heute würde `WindowLostToTakeover` ihre Occurrence auf die nächste Wiederholung verweisen)?
- Soll das Verhalten generell für jede Occurrence ohne bisherige Belegung gelten (z. B. auch für bestehende Regeln, die durch ein neu gültiges `ValidityMode`-Fenster erstmals Occurrences erzeugen), nicht nur für frisch angelegte Regeln? Technisch läuft beides über denselben Pfad.
- Darf die verschobene Ausstrahlung über ihren bisherigen Fensterbereich hinausragen (Overshoot gegen den nächsten Bound-Start), oder endet sie hart dort — und gilt die End-Kürzung dann pro Titel oder pro Ausstrahlung?
- Bedeutet „die jeweils vorherigen Filme der Sammlung werden anstelle der Fortsetzungen eingefügt" eine echte Wiederholung bereits geplanter Inhalte (selbe `MovieId`, zweiter Sendeplatz)? Wie verträgt sich das mit der Ring-/Cursor-Semantik des `collection:{id}`-Strangs?
- „Danach werden die darauf folgenden Filme zeitlich angepasst": Ist gemeint, die Slots der Folge-Ausstrahlungen bei gleichem Inhalt nur zeitlich zu verschieben, oder sie über den regulären Re-Materialisierungspfad neu zu planen?
