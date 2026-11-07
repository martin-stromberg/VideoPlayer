# Umsetzungsplan: Ständige Wiederholungen — faire Strang-Rotation bei der Interleaved-Ausstrahlungsbelegung

## Übersicht

Die Strang-Auswahl interleaveter `ProgramRule`-Ausstrahlungen (`SelectAiringItemsInterleaved` in `ProgramScheduleService.cs`) wird so korrigiert, dass der Startstrang über aufeinanderfolgende Ausstrahlungstage rotiert — aber nur dort, wo die Stranganzahl die pro Ausstrahlung tatsächlich bediente Strangzahl übersteigt (verhungernde Stränge). Fenster, in denen alle Stränge bedient werden (typische Serien-Stränge), behalten ihre stabile Tagesstruktur. Betroffen ist ausschließlich `ProgramScheduleService.cs` plus eine neue Schritt-Testdatei; kein Datenmodell-, Interface-, Konfigurations- oder UI-Eingriff.

## Scope-Klärung

Verbindliche fachliche Abgrenzung — übernommen bzw. konkretisiert aus `requirement.md` → `Fachlicher Scope`:

| Feld | Festlegung |
|------|------------|
| Verarbeitungseinheit | Die Titel-Belegung einer einzelnen Ausstrahlung (Occurrence) einer `ProgramRule` mit mehreren Strängen (`RuleState.IsInterleaved`), konkret die Wahl des Startstrangs in `SelectAiringItemsInterleaved`. Nur interleaved Regeln; flache Ein-Strang-Regeln (`SelectAiringItems`) und Filler bleiben unverändert. |
| Auslösende Ereignisse | Kein Nutzertrigger. Der Planungslauf wendet die korrigierte Logik an: `EnsureCoverageAsync` (Erstbefüllung/Lückenfüllung) und `ReconcileAsync`/`ReconcileWindow` (Neuplanung verdrängter Ausstrahlungen), beide produktiv ausschließlich über `ExecutePassAsync` im `ProgramScheduleWorker`. |
| Erwartete Wirkung | Der geschriebene `ProgramSlot`-Plan zeigt über aufeinanderfolgende Ausstrahlungstage wechselnde Stränge, sobald nicht alle Stränge pro Ausstrahlung bedient werden können (z. B. viele `collection:{id}`-Stränge + `SlotDuration` + Clip-Grace): wechselnde Filmsammlungen/Filme statt täglicher Wiederholung derselben ersten Stränge. Fenster ohne verhungernde Stränge behalten ihre feste Slot-Struktur. |
| Umgang mit fehlenden Objekten | Es werden keine neuen fachlichen Objekte benötigt. `ProgramRuleCursors`-Zeilen je (Regel, Strang) existieren bereits bzw. werden wie bisher über `PersistCursors` angelegt und fortgeschrieben — **wiederverwenden**, nicht neu einführen. Fehlende Stränge in der Expansion entstehen nicht: `RuleState.SetItems` gruppiert das Material vollständig. |

### Scope-Ausschlüsse

| # | Einschränkung / Ausschluss | Begründung | Status |
|---|----------------------------|------------|--------|
| 1 | Bereits mit dem alten Offset-0-Verhalten falsch belegte kommende Slots werden **nicht** automatisch ersetzt. Stattdessen: dokumentierter einmaliger Plan-Reset durch den Anwender (`ResetScheduleAsync` über die Admin-Seite). | `SlotStillValid` ist pick-reihenfolge-agnostisch (Inventory `logic.md`): alte Belegungen validieren weiter als gültig. Eine Validierungserweiterung müsste die komplette Zwei-Lauf-Auswahllogik inkl. Bedient-Prüfung nachbilden und könnte bei verschobenen Starts (anderes `airingStartUtc` → anderer Offset) korrekt geplante Ausstrahlungen fehlinvalidieren — unverhältnismäßiger Eingriff mit eigenem Fehlerpotential. Der Plan-Reset ist ein bestehender, unterstützter Admin-Mechanismus und stößt die Neuanlage aller kommenden Tage mit der korrigierten Logik an. | bestätigt (2026-11-07, Anwender) |
| 2 | Nebenbefund Altdaten: Vom alten Flachring-Algorithmus geschriebene, inhaltlich gültige Reihenfolgen, die die Ring-Kontiguitätsprüfung als `StrandSequenceBroken` verwarf, werden **nicht** robuster behandelt — abgetrennt als mögliche Folgeaufgabe. | Die tolerantere Behandlung von Alt-Reihenfolgen erfordert einen eigenen Mechanismus in `SlotStillValid` (Toleranzfenster/Historienvergleich), der mit der Startstrang-Rotation nichts zu tun hat. Kleiner Scope wird bevorzugt; die Anforderung selbst stuft den Punkt als „kein eigener Korrekturauftrag" ein. | bestätigt (2026-11-07, Anwender) |
| 3 | Keine Änderung der inneren Strang-Ordnung: geordnete Stränge (`PreservesOrder` — Filmsammlungen in Releasedatum-, Serien in Folgenreihenfolge) setzen am Fortsetzungscursor fort; keine Folge wird übersprungen oder vorgezogen. | Steht so im fachlichen Scope der `requirement.md`. | bestätigt |
| 4 | Keine Änderung an der Abbruch-Semantik der Auswahl (Clip-Grace/`PlaceholderGrace`-Restzeit-Abbruch, Bound-Prüfung, Altersfenster-Deferral/Skip) und keine Änderung am `StrandOrderKey`-Hash-Mix, `MixUnorderedRing`/`ItemOrderKey`. | Steht so im fachlichen Scope der `requirement.md`. | bestätigt |
| 5 | Keine neue Konfiguration, keine UI-Änderung, keine Schaltbarkeit. | Steht so im fachlichen Scope der `requirement.md` — Korrektur der Planungssemantik. | bestätigt |

## Designentscheidungen

| Komponente / Bereich | Gewählter Ansatz | Begründung |
|----------------------|-----------------|------------|
| Rotationsbedingung (wann rotiert wird) | **Bedarfsgesteuerte Rotation per Bedient-Prüfung (Zwei-Lauf-Auswertung)** statt pauschal `strandCount > 1`: `SelectAiringItemsInterleaved` läuft zunächst mit `startOffset = 0`. Bedient der Lauf weniger Stränge als nicht-leere Stränge vorhanden sind (mindestens ein Strang verhungert), läuft die Auswahl ein zweites Mal mit dem rotierten `startOffset`, und dessen Ergebnis gilt. Bei `wanted < strandCount` (`EpisodeCount` < Strangzahl) ist das Verhungern statisch sicher — dort entfällt der Probelauf, der rotierte Offset gilt direkt wie bisher. | Eine pauschale Rotation bei `m > 1` würde die fachlich gewollte stabile Tagesstruktur typischer Serienfenster zerstören (2–3 Stränge, die pro Abend alle bedient werden, hätten täglich wechselnde Führungsposition) und bräche `EnsureCoverage_GenreRule_InterleavesShows_StableSlotsAcrossDays`, `..._LeadStrand_FollowsHashOrder_NotAlphabetical` und `EnsureCoverage_WeekOfAirings_StrandStructureStableAndOrderContinues` — Tests, die genau dieses gewollte Verhalten festnageln. Die Bedient-Prüfung trifft die fachliche Bedingung „Stranganzahl > platzierende Titelzahl" exakt, ohne die Laufzeit/Grace/Bounds-Schätzung raten zu müssen: Die tatsächlich platzierende Zahl entsteht erst im Auswahllauf. Die Methode ist eine reine Funktion (kein mutierter Strang-State, `scanned`-Array ist lokal, Abbruch über `maxLap`/`noProgressLaps` beschränkt) — ein zweiter Lauf ist sicher und bestimmt (identische Eingaben → identische Entscheidung, reproduzierbar in Coverage- und Reconcile-Pfad). |
| Rotationsanker (wie rotiert wird) | **UTC-Datum des realen, ggf. verschobenen `airingStartUtc`** — die bestehende `StrandStartOffset`-Formel `((startUtc.Date - UnixEpoch).Days % strandCount)` bleibt der Mechanismus; nur seine Aufrufbedingung ändert sich. | `BuildOccurrences` erzeugt pro Regel maximal eine Occurrence je lokalem Tag — die in den offenen Fragen diskutierte Alternative „pro Occurrence" ist praktisch deckungsgleich mit „pro Ausstrahlungstag" und benötigt keinen zusätzlichen Ordinalparameter. Der Anker ist eine reine Funktion von `airingStartUtc`, das in beiden Materialisierungspfaden (`MaterializeOccurrence`, `MaterializeOccurrenceEvicting`) gleichartig über `ShiftPastInvaders` bestimmt wird — damit ist die Wahl reproduzierbar. Können Coverage- und Reconcile-Materialisierung für dieselbe Occurrence unterschiedliche `airingStartUtc` (und damit Offsets) sehen, ist das unschädlich: `SlotStillValid` prüft die Pick-Reihenfolge nicht (Inventory `logic.md`), die Re-Bewertung erzeugt dann einfach eine eigene gültige Belegung. Ein über UTC-Mitternacht verschobener Start rotiert auf seinem neuen Tag weiter — fachlich konsistent mit der bisherigen Semantik. |
| Umgang mit bereits falsch belegten Zukunftsslots | **Kein Mechanismus-Eingriff; dokumentierter einmaliger Plan-Reset** (`ResetScheduleAsync`, Admin-Seite) als Anwenderaktion. Alternativen „bewusst liegen lassen" (Slots laufen stundenlang falsch weiter) und „Validierungserweiterung" (siehe Scope-Ausschluss 1: hoher Eingriff, Fehlinvalidierungsrisiko) werden verworfen. | Kleinster Eingriff, der die issue-seitige Erwartung (falsch belegte kommende Tage werden ersetzt) mit einem bestehenden, unterstützten Weg erfüllt — ohne neue Invalidierungslogik mit eigener Fehlerquelle. Siehe Offene Punkte Nr. 1. |

## Programmabläufe

### Interleaved Titel-Auswahl mit bedarfsgesteuerter Startstrang-Rotation

1. `MaterializeOccurrence` (Coverage-Pfad) bzw. `MaterializeOccurrenceEvicting` (Reconcile-Pfad) bestimmen wie bisher `airingStart` über `ShiftPastInvaders` und rufen `SelectItems` auf.
2. `SelectItems` dispatcht bei `state.IsInterleaved` auf `SelectAiringItemsInterleaved`.
3. `SelectAiringItemsInterleaved` bestimmt `wanted`/`target` wie bisher aus `EpisodeCount`/`SlotDuration`.
4. **Startstrang-Wahl (geändert):**
   - `wanted < strandCount` (nur `EpisodeCount`-Regeln): `startOffset` = Tagesrotation `((airingStartUtc.Date - UnixEpoch).Days % m)` — Verhalten wie heute, ohne Probelauf.
   - `wanted >= strandCount` (inkl. `SlotDuration`, `wanted = int.MaxValue`): erster Auswahllauf mit `startOffset = 0`. Bedient das Ergebnis (`Picks`) weniger Stränge als nicht-leere Stränge existieren (`strands.Count(s => s.Items.Count > 0)`), läuft die Auswahl erneut mit dem Tagesrotations-Offset; das zweite Ergebnis gilt. Andernfalls gilt das erste Ergebnis — die stabile Tagesstruktur bleibt erhalten.
5. Die Lap-Schleife selbst (Round-Robin ab `startOffset`, `done`/`scanned`/`skipsInRow`, Clip-Grace-Abbruch, Altersfenster-Defer/Skip, Bound-Prüfung, `noProgressLaps`-Wächter) bleibt unverändert; sie wird dazu in eine aufrufbare Teillogik (z. B. lokale Funktion oder private Hilfsmethode mit `startOffset`-Parameter) extrahiert, damit beide Läufe denselben Code benutzen.
6. `CommitTaken` verteilt das zurückgegebene `Picks`/`Scanned`-Ergebnis wie bisher je Strang an `CommitScan`; `PersistCursors` schreibt die Strang-Cursor unverändert. Der rotierte Startstrang ändert am Cursor-Mechanismus nichts.
7. `InsertClipFill`/`FillAiringSegments` und `FillGaps` laufen unverändert nach.

Beteiligte Klassen/Komponenten: `ProgramScheduleService`, `RuleState`, `StrandState`, `StrandPick`, `InterleavedSelection`, `SelectionResult`, `ProgramScheduleWorker` (produktiver Aufrufer über `ExecutePassAsync`).

### Re-Bewertung (unverändert, Konsistenz-Nachweis)

1. `ReconcileAsync` lädt `ReconcileWindow`, `Validate` prüft je Slot über `SlotStillValid` Mitgliedschaft, Laufzeit, Altersfenster und Ring-Kontiguität geordneter Stränge.
2. Die Pick-Reihenfolge/der Startstrang wird nicht geprüft — rotierte Belegungen validieren daher genauso wie nicht-rotierte; es entstehen keine `StrandSequenceBroken`-Fehlinvalidierungen.
3. Umgekehrt bleiben alte Offset-0-Belegungen valide — Ersatz nur über den dokumentierten Plan-Reset (Scope-Ausschluss 1).

Beteiligte Klassen/Komponenten: `ProgramScheduleService`, `ReconcileWindow`, `SlotStillValid`, `AiringInvalidReason`.

## Neue Klassen

Keine. Die Korrektur ist eine Verhaltensänderung innerhalb von `ProgramScheduleService` (Auslagerung der Lap-Schleife erfolgt als private Teillogik derselben Klasse, keine neue fachliche Klasse).

## Änderungen an bestehenden Klassen

### `ProgramScheduleService` (Service)

- **Geänderte Methoden:**
  - `SelectAiringItemsInterleaved` — Zentrale Korrektur: Ermittlung des effektiven `startOffset` wird bedarfsgesteuert. Bei `wanted >= strandCount` Probelauf mit Offset 0, Auswertung der Anzahl bedienter Stränge gegen die Anzahl nicht-leerer Stränge, bei Verhungern zweiter Lauf mit Tagesrotations-Offset. Rückgabewert (`InterleavedSelection`) unverändert.
  - `StrandStartOffset` — Semantik konkretisiert: liefert den Tagesrotations-Offset; die Bedingung `wanted < strandCount` wandert an den Aufrufort (Aufrufer entscheidet per Bedient-Prüfung statt statischem `wanted`-Vergleich). Signatur kann vereinfacht werden (z. B. nur noch `(strandCount, startUtc)`) oder bleibt mit klarerer Benennung — Umsetzungsdetail, das gewählte Verhalten steht in den Designentscheidungen.
- **Neue private Teillogik:** Extraktion der Lap-Schleife aus `SelectAiringItemsInterleaved` in eine aufrufbare Einheit mit `startOffset`-Parameter (z. B. private statische Methode `RunInterleavedLaps` oder lokale Funktion), damit Probelauf und Rotationlauf denselben Code teilen.
- **Dokumentationskommentare:** Der Kommentar an `StrandStartOffset` („SlotDuration airings keep identical day-to-day positions") und der Kommentar in `SelectAiringItemsInterleaved` („each occurrence starting again at the first strand") sind fachlich überholt und werden an die bedarfsgesteuerte Rotation angepasst.

## Datenbankmigrationen

Keine. Es gibt keinen Schema-Eingriff; `ProgramRuleCursors` trägt die Strang-Cursor bereits.

## Validierungsregeln

Keine. Es werden keine neuen oder geänderten Eingaben verarbeitet.

## Konfigurationsänderungen

Keine. Das Verhalten ist eine Korrektur der Planungssemantik und wird nicht schaltbar gemacht.

## Seiteneffekte und Risiken

- **Doppelter Auswahllauf bei `SlotDuration`-Regeln mit ≥2 Strängen:** Bei jeder interleaved `SlotDuration`-Materialisierung läuft die Auswahl im Probelauf-Modus bis zu zweimal. Der Lauf ist beschränkt (`wanted`/`target`, `noProgressLaps > maxLap`) und rein lesend; Mehrkosten sind konstant klein und nur auf dem interleaved Pfad. Kein Risiko für den Flachpfad.
- **Ergebnis des Rotationslaufs kann vom Probelauf abweichen:** Der zweite Lauf mit rotiertem Offset kann in Randfällen (z. B. rotierter Start trifft altersfenster-blockierten oder bound-beschnittenen Strang) eine geringfügig andere Titelmenge/-folge liefern. Das ist deterministisch und fachlich akzeptabel — es wird bewusst immer das rotierte Ergebnis verwendet (kein „besseres-von-beiden"-Voting, das weitere Komplexität ohne fachlichen Nutzen brächte).
- **Verschobener Start über UTC-Mitternacht:** Ein per `ShiftPastInvaders` über das UTC-Tagesende geschobener Start rotiert auf dem neuen Tag — gewollte Konsequenz des Tagesankers (identisch zur bisherigen `StrandStartOffset`-Semantik).
- **Bestandstests mit `SlotDuration` + mehreren Strängen:** Tests, deren Szenario Stränge verhungern lässt, ändern ihr erwartetes Bild (siehe „Betroffene bestehende Tests" — Prüfliste). Tests, in denen alle Stränge bedient werden, bleiben unverändert grün.
- **Cursor-Fortschreibung:** Unverändert, aber kritisch — Regressionstests müssen die `CommitScan`-/`PersistCursors`-Korrektheit über die Rotation hinweg belegen (siehe Tests).
- **Altdaten/Altpläne:** Bestehende falsch belegte Zukunftsslots bleiben bis zum Ablauf oder bis zum dokumentierten Plan-Reset stehen (Scope-Ausschluss 1) — muss in Hilfe/Release Notes transparent gemacht werden.

## Umsetzungsreihenfolge

1. **Neue Schritt-Testdatei anlegen + Repro-Test schreiben (rot)**
   - Voraussetzungen: `ProgramScheduleTestDb` (inkl. `CreateCollectionAsync`, `CreateMovieAsync` mit `collectionId`, `CreateClipGroupAsync`, `AddClipToGroupAsync`, `CreateRuleAsync`), `PrimetimeSeed` — alle vorhanden.
   - Beschreibung: `VideoWebPlayer.Tests/ProgramScheduleServiceTests_StrandRotation.cs` anlegen; Dateiname in `ProgramScheduleConventionTests.StepTestFiles` eintragen. Repro-Test: Genre-Regel mit `slotDuration` + Clip-Gruppe (aktiviert `ClipGrace`) + vielen Ein-/Zwei-Film-`collection:{id}`-Strängen → `EnsureCoverageAsync` über mehrere Tage → Assertion, dass aufeinanderfolgende Tage unterschiedliche Sammlungen führen (schlägt im Ist-Stand fehl).

2. **Bedarfsgesteuerte Rotation in `SelectAiringItemsInterleaved` umsetzen**
   - Voraussetzungen: Schritt 1 (Repro-Test als roter Nachweis).
   - Beschreibung: Lap-Schleife in eine `startOffset`-parametrisierte Teillogik extrahieren; Bedient-Prüfung (bediente Stränge < nicht-leere Stränge → zweiter Lauf mit Tagesrotations-Offset); `wanted < m` weiterhin direkt rotiert; `StrandStartOffset`-Semantik und Kommentare anpassen.

3. **Übrige neue Tests schreiben**
   - Voraussetzungen: Schritt 2.
   - Beschreibung: Reihenfolge-/Cursor-Erhalt über die Rotation (geordnete Sammlungsstränge in Release-Reihenfolge, `ProgramRuleCursors` je Strang), Stabilität ohne Verhungern (alle Stränge bedient → identische Tagesstruktur), Reconcile-Idempotenz des rotierten Plans (`AssertNoDisplaced`), ggf. `EpisodeCount < m`-Rotations-Regression.

4. **Bestandstests prüfen und ggf. anpassen**
   - Voraussetzungen: Schritt 2.
   - Beschreibung: Volle ProgramSchedule-Testklassen laufen lassen; die in „Betroffene bestehende Tests" genannte Prüfliste abarbeiten — erwartet sind keine Anpassungen, bei Strang-Verhungern in einem Szenario ggf. Erwartungsbild korrigieren (mit Begründung im Kommentar).

5. **Dokumentation**
   - Voraussetzungen: Schritt 2.
   - Beschreibung: `docs/help/tv-sender-ablauf-technisch.md` (Strang-Rotation/Bedient-Prüfung, Hinweis auf einmaligen Plan-Reset für Altbelegungen) und `docs/RELEASE_NOTES.md` aktualisieren; `docs/help/tv-sender.md` bei Bedarf.

6. **DoD-Nachweis**
   - Voraussetzungen: Schritte 1–5.
   - Beschreibung: `dotnet build VideoPlayer.sln` in Debug **und** Release fehlerfrei; volle Testsuite `dotnet test VideoWebPlayer.Tests/VideoWebPlayer.Tests.csproj` grün.

## Tests

### Neue Tests

| Test / Hilfsmethode | Testklasse | Was wird geprüft / bereitgestellt? |
|--------------------|------------|-------------------------------------|
| `EnsureCoverage_ManyCollectionStrands_RotatesLeadStrandAcrossDays` | `ProgramScheduleServiceTests_StrandRotation` (neue Datei `ProgramScheduleServiceTests_StrandRotation.cs`, Eintrag in `ProgramScheduleConventionTests.StepTestFiles`) | **Repro-Test:** Genre-Regel, `slotDuration` (z. B. 4 h), Clip-Gruppe (Grace), N `collection:{id}`-Stränge mit ~110-min-Filmen → über ≥5 Ausstrahlungstage führen wechselnde Sammlungen; mindestens zwei unterschiedliche Sammlungen kommen als erstes Titel zum Zug; langfristig jede Sammlung bedient. Läuft gegen echtes SQLite (`ProgramScheduleTestDb`). |
| `EnsureCoverage_ManyCollectionStrands_ReleaseOrderAndCursorsPreserved` | `ProgramScheduleServiceTests_StrandRotation` | Geordnete Sammlungsstränge (≥2 Filme, `releaseDate` gesetzt) behalten über die Rotation hinweg strikte Release-Reihenfolge je Sammlung; `ProgramRuleCursors` enthält je bedienten `collection:{id}`-Strang genau eine Zeile, die monoton fortschreitet. |
| `EnsureCoverage_AllStrandsServed_NoRotation_StableStructure` | `ProgramScheduleServiceTests_StrandRotation` | Kontrollfall: wenige Stränge (z. B. 2 Sammlungen kurzer Filme), die pro Ausstrahlung alle bedient werden → identische Titel-Typ-/Strang-Sequenz jeden Tag (keine Rotation). Sichert die Designentscheidung „nur bei Verhungern". |
| `Reconcile_RotatedPlan_StaysScheduledNoFalseDisplacement` | `ProgramScheduleServiceTests_StrandRotation` | Nach rotierter Erstbefüllung `ReconcileAsync` aufrufen → `AssertNoDisplacedAsync`: die neue Belegungsreihenfolge wird von der pick-agnostischen Validierung nicht verworfen. |
| `EnsureCoverage_EpisodeCountBelowStrandCount_StillRotates` | `ProgramScheduleServiceTests_StrandRotation` | Regression des Altwegs: `episodeCount < strandCount` rotiert den Startstrang weiter über die Tage (kein Verhaltenssprung). |
| Hilfsmethode `SeedCollectionRotationScenarioAsync` (o. ä.) | `ProgramScheduleServiceTests_StrandRotation` | Seedet Kanal, Genre, N `MovieCollection`s je 1–2 Filme (`CreateCollectionAsync`, `CreateMovieAsync` mit `collectionId`, `LinkMovieGenreAsync`), Clip-Gruppe und die `slotDuration`-Genre-Regel; gibt Fixture, Service, Regel, Sammlungs-Zuordnung (Film → CollectionId) zurück. |

### Betroffene bestehende Tests

Erwartet wird **keine** Anpassung — die bedarfsgesteuerte Bedingung hält alle Szenarien stabil, in denen jeder Strang bedient wird. Folgende Tests nageln das Ist-Verhalten fest und müssen unverändert grün bleiben (Nachweis, keine Anpassung):

| Test / Testklasse | Grund der Prüfung |
|-------------------|---------------------|
| `ProgramScheduleServiceTests_Interleaving.EnsureCoverage_GenreRule_InterleavesShows_StableSlotsAcrossDays` | 2 Stränge, `episodeCount: 2` — beide Stränge bedient → kein Verhungern → Offset bleibt 0. Nachweis der neuen Semantik „nur bei Verhungern rotieren". |
| `ProgramScheduleServiceTests_Interleaving.EnsureCoverage_GenreRule_LeadStrand_FollowsHashOrder_NotAlphabetical` | 3 Stränge, `episodeCount: 3` — alle bedient → Pick 0 bleibt Hash-Min-Strang. |
| `ProgramScheduleServiceTests_PrimetimeRolling.EnsureCoverage_WeekOfAirings_StrandStructureStableAndOrderContinues` | 2 Stränge (`other`, `show:{id}`), `slotDuration` — beide bedient (3 Titel/Abend) → stabile Tagesstruktur. |
| `ProgramScheduleServiceTests_Interleaving.EnsureCoverage_GenreRule_Interleaving_EpisodeOrderPerShow`, `..._SecondRun_ContinuesEachShowCursor`, `Reconcile_GenreRule_Interleaved_NoChanges_LeavesEverythingScheduled`, `..._LongAiring_StaysScheduled`, `Reconcile_GenreInterleaved_IdenticalUtcNow_SecondRunIsNoOp` | Dieselben Szenarien ohne Verhungern — Idempotenz/Cursor-Verhalten unverändert. |

Prüfliste (Szenarien mit `SlotDuration` + mehreren Strängen, bei denen Verhungern nicht ausgeschlossen ist — Ergebnis offen, ggf. Erwartungsbild anpassen):

| Test / Testklasse | Grund der Prüfung |
|-------------------|---------------------|
| `ProgramScheduleServiceTests_Issue320` (`Reconcile_UnchangedRules_DeferredStrandInPrefix_StaysScheduled`, `..._UnwrittenPrefixTakeWithLaterGroupTake_StaysScheduled`) | 2-Strang-`slotDuration`-Regeln; falls ein Strang (FSK-18-deferriert) in keiner Ausstrahlung bedient wird, ändert sich das Belegungsbild — zu prüfen, ob die Defer-Semantik (geordneter Strang ruht, `done[s]`) die Bedient-Prüfung ungewollt auslöst. |
| `ProgramScheduleServiceTests_ClipGroups.EnsureCoverage_InterleavedRule_ClipFillDistributed` | 2 `show:`-Stränge + `slotDuration` + Clip-Gruppe — prüfen, ob beide Stränge bedient werden. |
| `ProgramScheduleServiceTests_PrimetimeGrace`, `..._PrimetimeAgeWindow`, `..._PrimetimeTrailer`, `..._PrimetimeChanges` | Primetime-`slotDuration`-Szenarien mit wechselnder Strang-/Materialzusammensetzung — auf Erwartungsbilder prüfen, die feste Tagespositionen bei verhungernden Strängen annehmen. |

### E2E-Tests (primärer Funktionsnachweis)

Keine E2E-Tests erforderlich — und zwar nicht nur „zusätzlich zu Unit-Tests", sondern begründet: Die Änderung berührt keinen über UI oder Nutzeraktion erreichbaren Ablauf. Es gibt keine neue/geänderte Benutzerinteraktion, keine neue Schaltfläche, keinen geänderten Dialog; der Auslöser ist der Hintergrunddienst (`ProgramScheduleWorker`), die Wirkung ist der Inhalt des persistierten Sendeplans. Der komplette Benutzerfluss „Regel existiert → Worker plant → Plan enthält rotierende Stränge" ist serverseitig und wird durch die neuen Service-Level-Tests gegen echtes SQLite (`ProgramScheduleTestDb`, exakt der produktive `EnsureCoverageAsync`/`ReconcileAsync`/`ExecutePassAsync`-Pfad) nachgewiesen. Die bestehenden Playwright-Admin-Tests (`TVSenderAdmin*`) bleiben unberührt — die Admin-Seite zeigt den Plan nur an, ihr Verhalten ändert sich nicht.

Bestehende E2E-Tests müssen nicht angepasst werden: „Keine."

## Akzeptanzszenarien

| # | Szenario | Auslöser | Ausgangszustand | Schritte | Sichtbare Wirkung | Nachweis |
|---|----------|----------|------------------|----------|-------------------|----------|
| 1 | Happy Path: verhungernde Stränge rotieren | Zeitgesteuerter `ProgramScheduleWorker`-Pass (`ExecutePassAsync`) — kein Nutzertrigger | Genre-Regel mit `slotDuration` + Clip-Gruppe (Grace) und vielen `collection:{id}`-Ein-/Zwei-Film-Strängen; `ProgramRuleCursors` teils noch nicht vorhanden (werden über `PersistCursors` angelegt) | Worker-Pass → `EnsureCoverageAsync` → `MaterializeOccurrence` je Occurrence → `SelectItems` → `SelectAiringItemsInterleaved`: Probelauf bedient < nicht-leere Stränge → zweiter Lauf mit Tagesrotations-Offset → `CommitTaken`/`PersistCursors` | Der `ProgramSlot`-Plan zeigt an aufeinanderfolgenden Ausstrahlungstagen wechselnde führende Sammlungen/Filme statt der täglichen Wiederholung derselben zwei Titel; je Sammlung unveränderte Release-Reihenfolge | Neuer Test `EnsureCoverage_ManyCollectionStrands_RotatesLeadStrandAcrossDays` + `..._ReleaseOrderAndCursorsPreserved` (echtes SQLite); reale Abnahme auf lokaler Instanz möglich |
| 2 | Keine Rotation ohne Verhungern | Wie #1 | Serienfenster-Regel: 2–3 `show:{id}`-Stränge, deren Episoden pro Ausstrahlung alle bedient werden (`episodeCount`/Fenster ≥ Strangzahl) | Wie #1, aber Probelauf bedient alle Stränge → Offset 0, kein Zweitlauf | Identische Slot-Positionen jeden Tag; jede Serie setzt ihre Folgen fortlaufend fort | Bestehende Tests `..._StableSlotsAcrossDays`, `..._LeadStrand_FollowsHashOrder`, `..._StrandStructureStableAndOrderContinues` + neuer `..._AllStrandsServed_NoRotation_StableStructure` |
| 3 | Re-Bewertung des rotierten Plans (keine Fehlinvalidierung) | `ReconcileAsync` im nächsten Worker-Pass | Von #1 rotiert geschriebener Plan | `ReconcileWindow.Reload` → `Validate` → `SlotStillValid` je Slot | Alle Slots bleiben `Scheduled`; kein `Displaced`, keine `StrandSequenceBroken`-Einträge im Log | Neuer Test `Reconcile_RotatedPlan_StaysScheduledNoFalseDisplacement` |
| 4 | Altbelegung aus Offset-0-Ära bleibt bis zum Reset stehen | `ReconcileAsync` | Vor der Korrektur geschriebene, fachlich valide Zukunftsslots im alten Muster | `SlotStillValid` validiert sie weiter (pick-agnostisch) → keine automatische Neuplanung | Plan bleibt im alten Muster bis Ablauf oder bis der Anwender in der Admin-Seite „Plan zurücksetzen" auslöst (`ResetScheduleAsync`) — danach entsteht der Plan mit korrigierter Logik neu | Bestehende `ResetSchedule_RemovesAllSlotsAndCursors`-Absicherung + Dokumentationshinweis; bewusste Einschränkung (Scope-Ausschluss 1, bestätigt 2026-11-07) |

## Offene Punkte

Keine. Die beiden ursprünglich offenen Punkte (Ersetzung bereits falsch belegter kommender Tage; Nebenbefund Altdaten) wurden dem Anwender am 2026-11-07 vorgelegt und in der empfohlenen Form bestätigt — sie stehen jetzt als Scope-Ausschlüsse Nr. 1 und 2 mit Status `bestätigt` in der Scope-Klärung:

- **Punkt 1 → bestätigt:** Keine Validierungserweiterung. Dokumentierter einmaliger Plan-Reset durch den Anwender (`ResetScheduleAsync` über die Admin-Seite) ersetzt die falsch belegten kommenden Tage.
- **Punkt 2 → bestätigt:** Nebenbefund Altdaten (`StrandSequenceBroken` auf Flachring-Altpläne) wird als Folgeaufgabe abgetrennt, nicht in diesem Schritt umgesetzt.

Weitere offene Punkte: Keine. Rotationsanker (UTC-Datum des `airingStartUtc`) und Rotationsbedingung (bedarfsgesteuert per Bedient-Prüfung) sind in den Designentscheidungen begründet festgelegt worden — siehe dort auch, warum die Alternative „pauschal bei `strandCount > 1`" verworfen wurde.
