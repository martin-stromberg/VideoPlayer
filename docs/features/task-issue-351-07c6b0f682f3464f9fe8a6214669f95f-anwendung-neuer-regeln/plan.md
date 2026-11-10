# Umsetzungsplan: Anwendung neuer Regeln auf den bestehenden Sendeplan (Issue #351)

## Übersicht

`ReconcileAsync` wird so erweitert, dass eine Occurrence einer Regel ohne bisherige Belegung (neu angelegt, reaktiviert oder bislang nie materialisiert) auch in bereits beplanten Tagen materialisiert: Sie gewinnt Fensterkonflikte gegen gehaltene Slots gleicher Priorität, deren Ausstrahlungen daraufhin verdrängt und über die eigenen Occurrences zeitverschoben neu materialisiert werden statt die neue Occurrence atomar zu verwerfen. Die Verschiebe-, Cursor-Rückspul- und Sammlungs-Fortsetzungssemantik entsteht dabei fast vollständig aus vorhandenen Mechanismen (`DisplaceRuleFuture`, `RewindCursor`, `ShiftPastInvaders`, Bound-Deferral); neu sind lediglich die Gewinner-Festlegung bei Gleichpriorität, die zeitliche Iterationsreihenfolge und ein slot-genaues Displacement, das das Präfix eines teilüberlappten Blocks erhält.

## Scope-Klärung

Verbindliche fachliche Abgrenzung vor der Umsetzung — übernommen bzw. konkretisiert aus dem Abschnitt `Fachlicher Scope` der `requirement.md`:

| Feld | Festlegung |
|------|------------|
| Verarbeitungseinheit | Die Regel-Occurrence — eine aktive `ProgramRule` an einem nominalen lokalen Startzeitpunkt (`Occurrence` aus `BuildOccurrences`) — samt ihrer materialisierten Ausstrahlung (zusammenhängende `ProgramSlot`-Gruppe, `GroupAirings`-Block) innerhalb des Planungshorizonts. Sekundär betroffen: die Ausstrahlungsblöcke, deren Fenster die neue Occurrence überlappt. |
| Auslösende Ereignisse | Anlegen oder (Re-)Aktivieren einer `ProgramRule` über `ProgramRuleService.CreateRuleAsync`/`UpdateRuleAsync`/`SetRuleActiveAsync` (mit `IProgramScheduleSignal.NotifyScheduleChanged()`); wirksam beim nächsten `ProgramScheduleWorker`-Lauf bzw. `ExecutePassAsync` → `ReconcileAsync`. |
| Erwartete Wirkung | Nach dem nächsten Planungslauf tragen auch die bereits beplanten Tage `ProgramSlot`s der neuen Regel — sichtbar über `GetScheduleAsync`, den Sendeplan-Tab in `TVSenderAdmin.razor` und die Programmauskunft (`ProgramGuideService`/`ProgramGuideController`, `LiveTv.razor`). Bereits geplante Ausstrahlungen bleiben inhaltlich erhalten und werden höchstens zeitlich verschoben bzw. am Ende gekürzt (Tail-Titel defert auf die nächste Occurrence). |
| Umgang mit fehlenden Objekten | `ProgramRuleCursor` der neuen Regel existiert noch nicht → beim ersten Commit der Auswahl über `PersistCursors` erzeugt (bestehender Mechanismus). Freie Fenster für die verschobene Ausstrahlung existieren ggf. nicht → durch die Verschiebung selbst erzeugt; nicht platzierbare Reste gehen wie bisher in `SummaryCounters.RemainingGapSeconds` und an `FillGaps`. Frei werdende Zeitblöcke verworfener Sammlungs-Fortsetzungen → Auffüllung mit früheren Filmen desselben `collection:{id}`-Strangs über den vorhandenen `RewindCursor`-Mechanismus (Wiederverwendung). |

### Scope-Ausschlüsse

| # | Einschränkung / Ausschluss | Begründung | Status |
|---|----------------------------|------------|--------|
| 1 | Eine neue Regel mit **niedrigerer** `Priority` darf einen gehaltenen Block **höherer** `Priority` nicht verschieben; ihre Occurrence verweist wie bisher über `WindowLostToTakeover`/Konfliktverwurf auf ihre nächste Wiederholung. Die „Frisch"-Privilegierung gilt nur im Gleichprioritäts-Fall. | Die Übernahme-Semantik strikt höherer Priorität (Issue #314) bleibt das einzige Rangordnungs-Mittel; eine Umkehr der Rangfolge würde bestehende Pläne unberechenbar machen. | offen |
| 2 | Die Sammlungs-Kaskade wirkt nur innerhalb **derselben Regel** (Strang `collection:{id}`): Filme derselben `MovieCollection`, die über eine andere Regel (z. B. eine zweite Genre-Rotation) eingeplant sind, werden nicht mitverworfen. | Der Fortsetzungsstand ist regelgebunden (`ProgramRuleCursor` je Regel+Strang); ein regelübergreifender Filminhalt-Abgleich wäre ein neuer Mechanismus mit eigener Fehlerfläche (Mehrfach-Ausstrahlungen sind fachlich zulässig). | offen |
| 3 | Beginnt die neue Occurrence **später** als der überlappte Bestandsblock (Konflikt erst mitten im Block), bleibt nur das Slot-Präfix vor der Occurrence erhalten; der abgeschnittene Rest des Tagesblocks entfällt und rückt per Cursor-Rückspulung an den Folge-Occurrences nach — eine Verschiebung innerhalb desselben Tages ist dann nicht möglich, weil die Occurrence des Verlierers zeitlich bereits vorüber ist. | Ausstrahlungen sind atomar und werden nur an ihrem nominalen Occurrence-Start bzw. hinter Invadern materialisiert; ein erneutes Materialisieren „mitten im Tag" ohne Occurrence-Anker würde die Anker-Beweisbarkeit (`AnchorProvable`) der Folgeläufe gefährden. | offen |
| 4 | Kein Eingriff in vollständig ausgestrahlte Vergangenheit (`slot.End <= coverageStart`) und kein Schneiden des laufenden Sendeplatzes — der `Protected`-Schutz aus Issue #315 bleibt unverändert. | Übernommen aus der Anforderung. | offen |
| 5 | `SlotStillValid`-valide Slots werden nicht wegen der neuen Regel durch andere Inhalte ersetzt; es geht ausschließlich um Einfügen, Verschieben und End-Kürzung. Kein neues UI-Element und keine Pflicht-Aktion: die Anwendung erfolgt automatisch im regulären Planungslauf; `ResetScheduleAsync` bleibt manueller Fallback. | Übernommen aus der Anforderung. | offen |
| 6 | Die Prioritäts-Übernahme für echte Rangkonflikte (streng höhere `Priority` übernimmt ein Fenster komplett, Issue #314 / `WindowLostToTakeover`) bleibt unverändert. | Übernommen aus der Anforderung; das Zusammenspiel ergibt sich durch die Rangordnung (siehe Ausschluss 1). | offen |

## Designentscheidungen

| Komponente / Bereich | Gewählter Ansatz | Begründung |
|----------------------|-----------------|------------|
| Gewinner eines Gleichprioritäts-Konflikts (`Evicts`) | Neue Stufe vor dem Id-Tie-Break: Bei `winner.Priority == loser.Priority` gewinnt die Occurrence, wenn ihre Regel **keine `ProgramRuleCursor`-Zeilen** besitzt (nie materialisiert: frisch angelegt oder noch nie belegt — „keine bisherige Belegung" als persistenter, laufstabiler Beweis). Sonst entscheidet weiterhin die Seniorität (`winner.Id < loser.Id`). Zusätzlich expliziter Gleichregel-Schutz: `loser.Id == winner.Id` → nie verdrängbar. | Die Cursor-Abfrage `cursors` ist im Reconcile-Lauf bereits geladen und ist über den ganzen Lauf stabil — eine frische Regel gewinnt damit **alle** ihre Occurrences des Laufs konsistent (jeden beplanten Tag, nicht nur die erste). Etablierte Regeln behalten die Senioritätsregel → deterministischer Gewinner je Regelpaar, keine Oszillation zwischen Läufen (Konvergenzbeweis: nach der ersten Anwendung besitzt die frische Regel Cursor-Zeilen und wird „etabliert"). Alternativen: „immer die materialisierende Occurrence" (oszilliert zwischen gleichprioren Dauer-Konfliktpartnern) und „höhere Id gewinnt" (benachteiligt reaktivierte, ältere Regeln gegen jüngere Belegung — Seniorität löst deren Fall ohnehin). |
| Iterationsreihenfolge der Occurrence-Schleife | `Priority desc → StartUtc → Rule.Id` (bisher `Priority desc → Rule.Id → StartUtc`). | Nur wenn die gewinnende (frühere) Occurrence vor der verlierenden (späteren) Occurrence desselben Tages materialisiert, kann die verdrängte Ausstrahlung noch im selben Lauf hinter dem neuen Block verschoben neu materialisiert werden (`ShiftPastInvaders` schiebt ihren Start hinter die frischen `newSlots`). Mit Id-Ordnung läge die Regel-Id der neuen Regel immer hinten — die Tages-Ausstrahlung des Verlierers ginge verloren statt sich zu verschieben. `Priority` bleibt Primärschlüssel → Issue-#314-Übernahme unverändert. |
| Verschiebe-Semantik für den Verlierer | Kein neuer Mechanismus: Verlierer laufen durch den bestehenden Pfad — `DisplaceRuleFuture` (Regel-Suffix) + `RewindCursor` (Strang-Rückspulung); die eigenen späteren Occurrences der Verlierer-Regel re-materialisieren den Inhalt zeitverschoben im selben Lauf. Nicht mehr passende Tail-Titel defern über die Bound-Prüfung von `SelectItems` auf die nächste Occurrence — „höchstens der letzte Film des Blocks entfällt" (und wird am Folgetag als Erstes nachgeholt). | Entspricht exakt Kunden-Variante (b) „Filme ab der Fortsetzung verwerfen, vorherige Sammlungsfilme einsetzen, Folgeinhalte zeitlich anpassen": `RewindCursor` spult den `collection:{id}`-Strang auf den Vorgänger des frühesten verdrängten Films zurück, die Folgeblöcke werden dadurch automatisch mit den jeweils vorherigen Sammlungsfilmen belegt und alle späteren Fortsetzungen im Plan bereits durch das Suffix-Displacement verworfen — die geforderte Kaskade entsteht ohne neuen Code. Variante (a) „gesamten Block verwerfen" liefert schlechteren Erhalt ohne Ersparnis. |
| Umfang des Displacement eines überlappten Blocks | Slot-genau ab dem ersten konfligierenden Slot (`DisplaceRuleFuture` wird um „alle Slots der Regel-Airings mit `Start >= fromStartUtc`" generalisiert — für den bisherigen Aufruf mit `airing[0].Start` verhaltensgleich). | Hält das nicht überlappte Präfix eines Bestandsblocks Scheduled (maximaler Erhalt der Primetime-Filme) statt den ganzen Block zu verwerfen; Anker- und Ring-Kontiguitätsvalidierung des Präfixes bleiben intakt, weil Gruppenkopf und Inhalte unverändert sind. |
| Verhalten gegen `newSlots`-Konflikte | Unverändert: Überlapp mit frisch materialisierten Slots desselben Laufs verwirft die Occurrence atomar (`DroppedConflicts`). | Frisch geschriebene Belegung desselben Laufs ist der zeitliche Gewinner — doppelt verdrängen würde unbestimmte Ketten erzeugen. |

## Programmabläufe

### Neu-/Re-Materialisierung einer Occurrence ohne Belegung in einem beplanten Tag

1. `ProgramRuleService.CreateRuleAsync`/`SetRuleActiveAsync` legt die Regel an/aktiviert sie und ruft `NotifyScheduleChanged()`; der `ProgramScheduleWorker` führt `ExecutePassAsync` → `ReconcileAsync` aus.
2. `ReconcileAsync` lädt Regeln, Cursor, `ReconcileWindow` und validiert wie bisher (`window.Reload`, Suffix-Verdrängung invalider Ausstrahlungen, Filler-Einzelprüfung, `RewindCursor`, `keptSlots`/`blocked`).
3. Occurrence-Schleife in neuer Ordnung `Priority desc → StartUtc → Rule.Id`; je Occurrence `MaterializeOccurrenceEvicting` mit `bound = BoundaryAfter(...)`.
4. In `MaterializeOccurrenceEvicting`: `airingStart = ShiftPastInvaders(occurrence.StartUtc, blocked, outranked)` (unverändert), `WindowLostToTakeover` (unverändert), Auswahl `SelectItems`, `ranges`.
5. Konfliktprüfung: `competitors` (gehaltene Slots mit `Start >= occurrence.StartUtc`) laufen durch das erweiterte `Evicts` — eine frische Regel (keine `ProgramRuleCursor`-Zeilen) gewinnt gegen jeden gleichprioren Konkurrenten; `Protected`-, `Filler`-, Prioritäts- und Gleichregel-Regeln unverändert.
6. Bei Erfolg werden die `ProgramSlot`-Zeilen geschrieben (`newSlots`, `blocked.AddRange`, `CommitTaken`); zurückgelieferte `losers` (inkl. der verdrängten Konkurrenten und etwaiger Tail-Slots) verarbeitet die Schleife: je Verlierer-Ausstrahlung `DisplaceRuleFuture(loserRuleId, loser.Start)` — slot-genau ab dem ersten Konflikt-Slot, Präfix bleibt — plus `RewindCursor`.
7. Iteriert die Schleife später die **eigene** Occurrence der Verlierer-Regel (gleicher Tag, späterer `StartUtc`), ist deren Belegung bereits verdrängt: `ShiftPastInvaders` schiebt den realen Start hinter den neuen Block; `SelectItems` setzt am zurückgespulten Strang-Zeiger an — die Ausstrahlung erscheint verschoben, der letzte Titel defert ggf. über den Bound-Check auf die nächste Occurrence. Iteriert sie eine Folge-Occurrence (Folgetag), wiederholt sich dasselbe — die Verschiebung pflanzt sich durch den Horizont fort.
8. Nachläufe unverändert: `BuildMovieAiringsFromSlotsAsync` → `InsertClipFill` (nur `newSlots`) → `FillGaps` → `PersistCursors` → `SaveChangesAsync`.

Beteiligte Klassen/Komponenten: `ProgramScheduleService` (`ReconcileAsync`, `MaterializeOccurrenceEvicting`, `DisplaceRuleFuture`, `Evicts`, `RewindCursor`, `ShiftPastInvaders`, `WindowLostToTakeover`, `BoundaryAfter`, `SelectItems`, `CommitTaken`), `ReconcileWindow` (`Airings`, `BoundStarts`, `Occurrences`), `RuleState`/`StrandState`, `ProgramRuleCursor`.

### Sammlungs-Kaskade bei verworfenem `collection:{id}`-Film

1. Ein verdrängter Film eines geordneten `collection:{id}`-Strangs (direkt oder als Tail-Deferral des verschobenen Blocks) geht als `Displaced`-Slot in `RewindCursor` ein.
2. `RewindCursor`/`RewindRing` setzt den Strang-Cursor auf den Vorgänger des frühesten verdrängten, noch expandierbaren Sammlungsfilms.
3. Alle späteren Ausstrahlungen derselben Regel wurden bereits durch `DisplaceRuleFuture` verdrängt; ihre Occurrences re-materialisieren fortlaufend ab dem zurückgespulten Inhalt — der nächste Block trägt den (vorher verworfenen) „vorherigen" Sammlungsfilm anstelle der Fortsetzung, alle folgenden Filme rücken entsprechend nach und werden zeitlich neu angepasst.
4. Folgelauf: `AnchorProvable`/`ShiftProvable` beweisen die verschobenen Köpfe über die Besetzt-Evidenz (Scheduled ∪ Displaced); `SlotStillValid` bestätigt die Ring-Kontiguität des geordneten Strangs → No-Op.

Beteiligte Klassen/Komponenten: `ProgramScheduleService` (`DisplaceRuleFuture`, `RewindCursor`, `RewindRing`, `MaterializeOccurrenceEvicting`, `SelectItems`), `StrandState` (`PreservesOrder`, `RewindTo`), `ProgramRuleCursor`, `ReconcileWindow` (`AnchorProvable`, `SlotStillValid`).

## Neue Klassen

Keine — die Umsetzung erweitert ausschließlich bestehende Logik und Testhelfer.

## Änderungen an bestehenden Klassen

### `ProgramScheduleService` (`VideoWebPlayer/Services/ProgramScheduleService.cs`)

- **Geänderte Methode `ReconcileAsync` — lokale Funktion `Evicts` (Zeilen 387–400):**
  - Neuer expliziter Gleichregel-Schutz vor dem Priority-Vergleich: `loser.Id == winner.Id` → `false` (eine Regel verdrängt ihre eigene gehaltene Ausstrahlung nie; bisher implizit über den Id-Tie-Break abgesichert).
  - Neuer Gleichprioritäts-Fall: `winner.Priority == loser.Priority` → `true`, wenn `cursors` für `winner.Id` keinen Eintrag enthält (Regel ohne bisherige Belegung); sonst wie bisher `winner.Id < loser.Id`.
  - Unverändert: `Protected(kept)` → `false`; `kept.RuleType == Filler` → `true`; Verlierer-Regel unauflösbar → `true`; `winner.Priority > loser.Priority` → `true`; `winner.Priority < loser.Priority` → `false`.
- **Geänderte Methode `ReconcileAsync` — Occurrence-Schleife (Zeile 405):**
  - Ordnung `OrderByDescending(Priority).ThenBy(Rule.Id).ThenBy(StartUtc)` → `OrderByDescending(Priority).ThenBy(StartUtc).ThenBy(Rule.Id)`.
- **Geänderte Methode `ReconcileAsync` — lokale Funktion `DisplaceRuleFuture` (Zeilen 360–385) und ihr Aufruf (Zeile 420):**
  - `DisplaceRuleFuture` generalisiert auf „alle Slots der Airings der Regel mit `slot.Start >= fromStartUtc`" (bisher „Airings mit `a[0].Start >= fromStartUtc` komplett"), Filler-Teil unverändert slot-genau.
  - Aufruf mit `loser.Start` (erster konfligierender Slot) statt `airing[0].Start` → Präfix des teilüberlappten Blocks bleibt Scheduled; Tail-Verlierer (deren Slot vor `occurrence.StartUtc` beginnt) verhalten sich wie bisher (der Tail-Slot selbst ist der erste Konflikt).
- **`MaterializeOccurrenceEvicting` (Zeilen 1111–1203):** keine Codeänderung — die neue Semantik entsteht über `Evicts`, die Reihenfolge und `DisplaceRuleFuture`; Signatur unverändert.
- **`IProgramScheduleService`/`ProgramScheduleReconcileSummary`/`SummaryCounters`:** unverändert — `SlotsDisplaced`/`SlotsCreated`/`DroppedConflicts`/`RemainingGapSeconds` decken die neuen Effekte ab; kein neuer Zähler.

### `ProgramScheduleTestDb` (`VideoWebPlayer.Tests/Helpers/ProgramScheduleTestDb.cs`)

- **Neue Methode `CreateMultiSeasonShowAsync(string name, int seasonCount, int episodesPerSeasonMin, int episodesPerSeasonMax, TimeSpan baseDuration, int durationJitterSeconds = 30, int doubleLengthEveryNth = 0, long sourceId = 1, int? ageRatingMinAge = null)`:** legt eine Serie mit `seasonCount` Staffeln (3–8) an, je Staffel deterministisch gestreut `episodesPerSeasonMin..episodesPerSeasonMax` Episoden (10–20), Laufzeit `baseDuration` ± index-abhängigem Jitter, jede `doubleLengthEveryNth`-te Episode (≠ 0) mit doppelter Laufzeit; gibt `(TVShow, List<TVShowSeason>, List<TVShowEpisode>)` zurück. Deterministische Streuung (kein `Random` ohne Seed) für reproduzierbare Tests.
- **Neue Methode `CreateCollectionAsync(string name, int movieCount, TimeSpan duration, DateTime? releaseDateStart = null)`:** `MovieCollection` + `movieCount` Filme mit aufsteigendem `ReleaseDate` (aus `ProgramScheduleServiceTests_PriorityTakeover.SeedCollectionAsync` in die Fixture heben; der Test-Helfer dort delegiert anschließend).
- **Neue Methode `SeedRealisticLibraryAsync(...)`:** komfortabler Bestand — mehrere Serien via `CreateMultiSeasonShowAsync` (3–8 Staffeln, 10–20 Episoden, Jitter + gelegentliche Doppelfolgen), mehrere `MovieCollection`s (6–10 Filme) und Einzelfilme — für die Praxis-Tests.

### `ProgramScheduleServiceTests_PriorityTakeover` (`VideoWebPlayer.Tests/ProgramScheduleServiceTests_PriorityTakeover.cs`)

- **Geänderte Hilfsmethode `SeedCollectionAsync`:** delegiert an `ProgramScheduleTestDb.CreateCollectionAsync` (kein Verhaltensunterschied).

### `TVShow`-Expansion (keine Codeänderung)

- `ExpandShowAsync` liefert bereits Staffel-/Episoden-Sequenz über die gesamte Serie — die mehrstaffeligen Seeds nutzen denselben `show:{id}`-Strang; kein Produktionscode-Eingriff nötig (Annahme verifizieren: `ExpandShowAsync` ordnet Episoden über Staffeln hinweg in Sendereihenfolge).

## Datenbankmigrationen

Keine — `ProgramSlot.Status`, `ProgramRuleCursor.StrandKey` und `Movie.MovieCollectionId` tragen alle benötigten Informationen bereits (Bestandsaufnahme, `models.md`).

## Validierungsregeln

Keine — keine neuen oder geänderten Eingaben.

## Konfigurationsänderungen

Keine — das Verhalten gilt automatisch für jede Occurrence im Reconcile-Lauf; die Variantenwahl der Sammlungs-Kaskade ist eine Implementierungsentscheidung (Variante b), keine Nutzer-Einstellung.

## Seiteneffekte und Risiken

- **Iterationsreihenfolge (StartUtc statt Rule.Id als Sekundärschlüssel):** Bei gleichprioren Regeln mit unterschiedlichen Occurrence-Zeiten entscheidet nun die Tageszeit, welche Occurrence zuerst materialisiert — bestehende Boundary-/Reconcile-Tests, die implizit von Id-Ordnung profitieren, können andere (korrekte) Ergebnisse liefern und müssen einzeln bewertet werden.
- **Konflikt-Ergebnis gleichprioren Konkurrenten:** Eine Regel ohne Cursor-Zeilen verdrängt jetzt gleichpriore gehaltene Ausstrahlungen, wo sie bisher atomar verworfen wurde (`DroppedConflicts`). Bestehende Tests, die einen Regel-Neuzugang nach einer ersten Belegung bei Gleichpriorität erwarten (Occurrence verworfen), müssen auf das neue (gewünschte) Ergebnis umgestellt werden.
- **Mehr `Displaced`-Zeilen:** Suffix-Verdrängung + Re-Materialisierung erzeugt wie bei jeder Umplanung `Displaced`-Zeilen; identisch re-materialisierte Zwillinge sind der bekannte Selbst-Verdrängungs-Fall und werden über `CleanupSelfDisplacedSlotsAsync` beim Pass-Start beseitigt — `Reconcile_WorkerLoop_NoDisplacedAccumulationNoOverlap`-Semantik muss weiterhin greifen.
- **Kaskaden-Churn:** Ein einziger gewonnener Fensterkonflikt verdrängt das Regel-Suffix des Verlierers; bei großen Horizonten (7 Tage) sind das viele Slots pro Lauf — akzeptabel, da konvergent (Folgelauf No-Op), aber Laufzeit/Log-Aufkommen in Worker-Tests prüfen.
- **Anker-Beweisbarkeit:** Verschobene Ausstrahlungen müssen im Folgelauf über `AnchorProvable`/`ShiftProvable` weiterhin valide bleiben — die Gewinner-Slots dienen als Invader-Evidenz; das verschobene Materialisieren jenseits des nominalen Fensterendes (gleichpriorer Invader) ist vom `WindowLostToTakeover`-Check bewusst ausgenommen und muss testweise abgesichert werden.
- **Interleaved/Clip-Gruppen:** `StrandRotationOffset` liest nur gehaltene+neue eigene Slots — frische Regeln starten deterministisch bei Strang 0; Clip-Füll-Slots des Verlierers werden mit der Ausstrahlung verdrängt und über `InsertClipFill` für die `newSlots` neu verteilt.

## Umsetzungsreihenfolge

1. **Testinfrastruktur erweitern (`ProgramScheduleTestDb`)**
   - Voraussetzungen: Keine (bestehende Helfer `CreateShowAsync`, `CreateMovieAsync`, `EnsureSourceAsync`).
   - Beschreibung: `CreateMultiSeasonShowAsync`, `CreateCollectionAsync` (Verallgemeinerung aus `ProgramScheduleServiceTests_PriorityTakeover`), `SeedRealisticLibraryAsync` anlegen; `SeedCollectionAsync` im PriorityTakeover-Test auf die neue Fixture-Methode delegieren.

2. **Schnelle Verifikationsbasis: neue Tests als Rot-Phase**
   - Voraussetzungen: Schritt 1.
   - Beschreibung: Neue Testklasse `ProgramScheduleServiceTests_NewRules` (VideoWebPlayer.Tests) mit den unten gelisteten Szenarien anlegen. Kern-Tests (freies Fenster, Tail-Überlapp mit Verschiebung, Sammlungs-Kaskade, Idempotenz) schlagen vor der Implementierung erwartbar fehl.

3. **`Evicts` erweitern + Occurrence-Ordnung + `DisplaceRuleFuture`-Generalisierung**
   - Voraussetzungen: Keine (reine Logikänderung in `ProgramScheduleService.cs`); testgetrieben durch Schritt 2.
   - Beschreibung: Drei punktuelle Änderungen wie unter „Änderungen an bestehenden Klassen" beschrieben — Gleichregel-Schutz + Frisch-Regel-Stufe in `Evicts`, Ordnung der Occurrence-Schleife auf `Priority desc → StartUtc → Rule.Id`, slot-genaue Verdrängung ab Konfliktposition in `DisplaceRuleFuture` samt geändertem Aufruf (`loser.Start`).

4. **Bestehende Suite fahren und Abweichungen bewerten**
   - Voraussetzungen: Schritt 3.
   - Beschreibung: `dotnet test VideoWebPlayer.Tests/VideoWebPlayer.Tests.csproj --filter "Category!=E2E"`; jeden Bruch einzeln bewerten (legitim neues Verhalten vs. Fehler), Erwartungen ggf. anpassen — keine Abschwächung von Tests. Insbesondere `ProgramScheduleServiceTests_Boundary`, `_Reconcile`, `_PriorityTakeover`, `_CollectionOrder`, `_Stability`, `_StrandRotation` sowie alle `*E2ETests`-Klassen ohne E2E-Trait im Blick behalten.

5. **Dokumentation**
   - Voraussetzungen: Schritt 3.
   - Beschreibung: `docs/help/tv-sender-ablauf-technisch.md` — Abschnitt „Neu-Bewertung (`ReconcileAsync`)" und „Stabile Neu-Bewertung" um Frisch-Regel-Gewinnersemantik, chronologische Iteration, slot-genaue Verdrängung und Sammlungs-Kaskade ergänzen; `docs/RELEASE_NOTES.md` Eintrag; `README.md` nur bei Bedarf.

6. **Abschluss-Verifikation**
   - Voraussetzungen: Schritte 1–5.
   - Beschreibung: `dotnet build VideoPlayer.sln` in Debug **und** Release fehlerfrei; volle Testsuite `dotnet test VideoWebPlayer.Tests/VideoWebPlayer.Tests.csproj` grün (einmalige Playwright-Flackerer einzeln wiederholen und benennen); reale Abnahme gemäß Akzeptanzszenario 1.

## Tests

### Neue Tests

| Test / Hilfsmethode | Testklasse | Was wird geprüft / bereitgestellt? |
|--------------------|------------|-------------------------------------|
| `CreateMultiSeasonShowAsync` | `ProgramScheduleTestDb` (Helfer) | Serie mit 3–8 Staffeln à 10–20 Episoden, leicht variierende Laufzeiten, gelegentlich doppelte Folgen (deterministisch). |
| `CreateCollectionAsync` | `ProgramScheduleTestDb` (Helfer) | `MovieCollection` mit Filmen in Releasedatum-Reihenfolge (aus PriorityTakeover-Helfer verallgemeinert). |
| `SeedRealisticLibraryAsync` | `ProgramScheduleTestDb` (Helfer) | Großzügiger Bestand: mehrere Mehrstaffel-Serien, mehrere Sammlungen, Einzelfilme. |
| `Reconcile_NewRuleFreeWindow_MaterializesInAlreadyPlannedDays` | `ProgramScheduleServiceTests_NewRules` | Neue Regel (Vorabend 17:00) auf 7-Tage-Plan: Slots an allen bereits beplanten Tagen, nicht erst am Horizont-Ende; bestehende Primetime-Slots unverändert; `DroppedConflicts == 0`. |
| `Reconcile_NewRuleTailOverlap_ShiftsPrimetimeBlock` | `ProgramScheduleServiceTests_NewRules` | Letzte Episode des Vorabendblocks ragt in die 20:15-Primetime: Primetime-Block komplett zeitverschoben hinter dem neuen Block, alle Filme erhalten, Start = Ende der letzten Episode; Folgetage konsistent. |
| `Reconcile_NewRuleOverlap_ShiftedBlockTailDefers` | `ProgramScheduleServiceTests_NewRules` | Verschiebung so groß, dass der letzte Titel des verschobenen Blocks vor seinem Bound nicht mehr startet: er defert und erscheint als Erster am Folgetag (Sendereihenfolge bleibt lückenlos, keine Duplikate). |
| `Reconcile_DroppedCollectionFilm_SequelsDisplaced_PredecessorsRefill` | `ProgramScheduleServiceTests_NewRules` | Sammlungs-Primetime: ein verdrängter Sammlungsfilm kaskadiert — alle späteren Fortsetzungs-Blöcke der Regel sind `Displaced`/neu materialisiert und tragen die jeweils früheren Sammlungsfilme; kein Sammlungsfilm sendet vor seinem Vorgänger (geordnete Strang-Abfolge über den Horizont). |
| `Reconcile_NewRuleLaterStart_PreservesAiringPrefix` | `ProgramScheduleServiceTests_NewRules` | Neue Occurrence beginnt mitten im Bestandsblock: Slots des Blocks vor dem Occurrence-Start bleiben unverändert Scheduled (slot-genaue Verdrängung), der Rest rückt per Cursor an Folge-Occurrences nach. |
| `Reconcile_NewRuleIsIdempotent_SecondRunNoOp` | `ProgramScheduleServiceTests_NewRules` | Nach der Verschiebung: zweiter `ReconcileAsync`-Lauf ist vollständiger No-Op (`AssertReconcileNoOpAsync`), keine Selbst-Verdrängungs-Zwillinge (`AssertNoSelfDisplacedAsync`), Plan lückenlos (`AssertGapless`). |
| `Reconcile_ReactivatedRule_MaterializesInAlreadyPlannedDays` | `ProgramScheduleServiceTests_NewRules` | Deaktivierte und wieder aktivierte Regel belegt im selben Lauf die bereits geplanten Tage (Seniorität/Fresh-Regel greift). |
| `Reconcile_NewRuleLowerPriority_DoesNotShiftHigherPriorityAiring` | `ProgramScheduleServiceTests_NewRules` | Neue Regel mit niedrigerer `Priority` verdrängt einen gehaltenen höherprioren Block nicht: dessen Slots bleiben Scheduled (`SlotsDisplaced` ohne deren Anteil), die Occurrence defert/verfällt. |
| `Reconcile_EqualPriorityEstablishedRules_DeterministicNoOscillation` | `ProgramScheduleServiceTests_NewRules` | Zwei etablierte gleichpriore Regeln mit überlappenden Nominalfenstern: deterministischer Gewinner (Seniorität), Folgelauf No-Op — kein Ping-Pong. |
| `Reconcile_NewRuleOverlap_RunningSlotStaysProtected` | `ProgramScheduleServiceTests_NewRules` | `coverageStart` liegt in einer laufenden Ausstrahlung: `Protected`-Slot läuft zu Ende, neue Occurrence schiebt dahinter statt zu schneiden. |
| `Reconcile_NewRuleRealisticLibrary_FullScenario` | `ProgramScheduleServiceTests_NewRules` | Praxis-Bestand (Serien 3–8 Staffeln à 10–20 Episoden mit Jitter/Doppelfolgen, Sammlungen, Filme): Primetime-Genre-Rotation + neue Vorabend-Rotation → mehrere `ExecutePassAsync`-Iterationen mit fortschreitender Zeit; lückenlose Abdeckung, geordnete Strang-Abfolge, keine Duplikate, Stabilität. |

### Betroffene bestehende Tests

| Test / Testklasse | Grund der Anpassung |
|-------------------|---------------------|
| `ProgramScheduleServiceTests_Boundary`, `ProgramScheduleServiceTests_Reconcile`, `ProgramScheduleServiceTests_PriorityTakeover`, `ProgramScheduleServiceTests_CollectionOrder`, `ProgramScheduleServiceTests_Stability`, `ProgramScheduleServiceTests_StrandRotation`, `ProgramScheduleServiceTests_Issue315`, `ProgramScheduleServiceTests_Issue320` und alle sendeplan-nahen `*E2ETests`-Klassen | Neuordnung der Occurrence-Schleife und die Frisch-Regel-Verdrängung können in Szenarien mit gleichprioren Regeln und nachträglich angelegten Regeln legitimerweise andere Ergebnisse liefern; jede Abweichung einzeln bewerten und Erwartung anpassen — kein Test wird deaktiviert oder abgeschwächt. |
| `ProgramScheduleServiceTests_PriorityTakeover.SeedCollectionAsync` | Delegiert an die neue Fixture-Methode `CreateCollectionAsync` (kein Testergebnis-Eingriff). |

### E2E-Tests (primärer Funktionsnachweis)

Keine neuen E2E-Tests erforderlich: Die Anforderung führt **keinen neuen oder geänderten Benutzerfluss** ein — Auslöser (Regel anlegen/aktivieren in `TVSenderAdmin.razor`) und sichtbare Wirkung (Sendeplan-Tab, `ProgramGuideController`, `LiveTv.razor`) sind unveränderte, bereits durch `TVSenderAdminE2ETests`/`TVSenderAdminClipGroupsE2ETests` abgedeckte Oberflächen; das neue Verhalten ist eine reine Planungs-Semantik des `ProgramScheduleService`, die auf Service-Ebene (echtes SQLite, `ProgramScheduleTestDb`) nachzuweisen ist. Die verbindliche reale End-to-End-Abnahme (Anwendung starten, Regel über die UI anlegen, Sendeplan prüfen) ist als Akzeptanzszenario 1 festgelegt.

| Priorität | Szenario | Testdatei / Testklasse | Abgedecktes Akzeptanzkriterium | Warum E2E nötig ist |
|-----------|----------|------------------------|-------------------------------|-------------------|
| — | Keine neuen UI-Flüsse (Begründung siehe oben) | — | — | — |

Welche bestehenden E2E-Tests müssen angepasst werden? Keine.

## Akzeptanzszenarien

| # | Szenario | Auslöser | Ausgangszustand | Schritte | Sichtbare Wirkung | Nachweis |
|---|----------|----------|------------------|----------|-------------------|----------|
| 1 | Happy Path: neue Vorabendregel materialisiert in beplanten Tagen | Admin legt in `TVSenderAdmin.razor` eine neue Regel (Genre-Rotation, täglich 17:00, 195 Min) an → `NotifyScheduleChanged` → nächster Worker-Lauf | 7-Tage-Plan existiert bereits (Primetime 20:15–00:00 belegt); neue Regel besitzt noch keine `ProgramRuleCursor`-Zeilen; der 17:00-Slot ist frei | Regel anlegen → `ExecutePassAsync`/`ReconcileAsync` → `GetScheduleAsync` | Alle bereits geplanten Tage zeigen den Vorabendblock 17:00–20:15; die Primetime steht zeitverschoben dahinter (letzte Episode ragt legal hinein), inhaltlich unverändert; sichtbar im Sendeplan-Tab und in `LiveTv.razor` | Tests `Reconcile_NewRuleFreeWindow_*` + `Reconcile_NewRuleTailOverlap_*`; **reale Abnahme**: Anwendung tatsächlich starten, Regel anlegen, Sendeplan-Tab prüfen (durch Orchestrator/Anwender, dokumentiert) |
| 2 | Letzter Primetime-Film entfällt am Tag → Sammlungs-Kaskade | Neue Regel deren Block-Ende die Primetime so stark verschiebt, dass der letzte Sammlungsfilm des Tagesblocks nicht mehr passt | Primetime-Sammlungsregel eingeplant über mehrere Tage (Film 1..n in Releasedatum-Reihenfolge) | Regel anlegen → `ReconcileAsync` → Folge-Occurrences re-materialisieren | Der letzte Film des Tages entfällt dort; am Folgetag sendet derselbe Film zuerst; jeder spätere Block trägt die jeweils früheren Sammlungsfilme; kein Film sendet vor seinem Vorgänger; Plan bleibt lückenlos | Tests `Reconcile_DroppedCollectionFilm_*` + `Reconcile_NewRuleOverlap_ShiftedBlockTailDefers` |
| 3 | Niederpriorere neue Regel gegen höherprioren Bestand | Neue Regel mit `Priority` unter dem gehaltenen Block | Höherpriores Sonderprogramm belegt das Fenster | Regel anlegen → `ReconcileAsync` | Der höherpriore Block bleibt unverändert; die neue Occurrence erscheint erst an ihrem nächsten freien Wiederholungstermin | Test `Reconcile_NewRuleLowerPriority_*` |
| 4 | Idempotenz/Stabilität nach der Umplanung | Folgelauf ohne sachliche Änderung | Zustand aus Szenario 1/2 | `ReconcileAsync` erneut ausführen | Keine neuen Verdrängungen oder Neumaterialisierungen (`SlotsDisplaced == 0`, `SlotsCreated` nur für neue Horizont-Kante), keine Selbst-Verdrängungs-Zwillinge | Test `Reconcile_NewRuleIsIdempotent_*` |
| 5 | Reaktivierte Regel | Admin aktiviert eine deaktivierte Regel erneut (`SetRuleActiveAsync`) | Regel besitzt Cursor-Zeilen, aber keine gehaltenen Slots; Tage bereits beplant | Aktivieren → `ReconcileAsync` | Die Regel erscheint an allen bereits geplanten Tagen, gehaltene gleichpriore Blöcke verschieben sich hinter ihre Ausstrahlungen | Test `Reconcile_ReactivatedRule_*` |
| 6 | Wiederaufnahme nach Unterbrechung | n. a. (kein asynchroner Fremdprozess: `ReconcileAsync` ist transaktional ein `SaveChangesAsync`; ein Abbruch mitten im Lauf hinterlässt den alten konsistenten Plan) | Lauf bricht vor `SaveChangesAsync` ab | Nächster Worker-Lauf | Plan unverändert konsistent; der Folgelauf wendet die Regel vollständig an — der Anwender sieht das Ergebnis nach dem nächsten Intervall im Sendeplan-Tab | Bestehender Transaktionsrahmen; kein Zusatz nötig |

## Offene Punkte

Keine — die offenen Fragen der `requirement.md` sind durch die Designentscheidungen verbindlich beantwortet:

- *Prioritätsrichtung der Verschiebe-Semantik* → Nur gleiche oder niedrigere Priorität des Inhabers wird verschoben; strikt höhere `Priority` behält die Issue-#314-Übernahme (Scope-Ausschluss 1).
- *Geltungsbereich* → Das Verhalten gilt für jede Occurrence einer Regel ohne `ProgramRuleCursor`-Zeilen — neu angelegt, noch nie materialisiert oder deren Belegung verworfen wurde; reaktivierte Regeln gewinnen zusätzlich über die Seniorität (offene Frage 2, durch denselben Mechanismus abgedeckt).
- *Overshoot/End-Kürzung* → Verschobene Ausstrahlungen dürfen über ihr nominales Fensterende hinauslaufen (gleichpriorer Invader ist kein `outranked`-Takeover); die End-Kürzung erfolgt pro Titel über den Bound-Check — ein begonnener Titel läuft zu Ende, ein nicht mehr startbarer defert auf die nächste Occurrence (offene Frage 3).
- *„Vorherige Filme der Sammlung einfügen"* → Ja, echte Wiederholung bereits geplanter Inhalte: `RewindCursor` spult den `collection:{id}`-Strang zurück, die Folge-Occurrences belegen ihre Blöcke mit denselben `MovieId`s erneut — ring-/cursor-konform, da die alten Slots `Displaced` sind (offene Frage 4).
- *„Folgefilme zeitlich angepasst"* → Über den regulären Re-Materialisierungspfad (`MaterializeOccurrenceEvicting` der Folge-Occurrences mit `ShiftPastInvaders`), nicht durch Slot-Verschieben am persistierten Modell (offene Frage 5).
