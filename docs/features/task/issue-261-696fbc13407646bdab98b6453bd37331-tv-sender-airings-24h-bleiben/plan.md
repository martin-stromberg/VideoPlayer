# Umsetzungsplan: TV-Sender – Airings > 24 h bleiben in `AiringReproducible` nicht invalid (Issue #261)

## Übersicht

`ProgramScheduleService.ReconcileAsync` wertet noch laufende Airings fälschlich als nicht
reproduzierbar, sobald ihr realer Beginn mehr als 24 h vor `coverageStart` liegt, und verdrängt
sie (`Displaced` + Neu-Materialisierung). Ursache ist der feste `OccurrenceLookback` (24 h), der
das Occurrence-Fenster in `BuildOccurrences` und das Slot-Ladefenster `loadFrom` begrenzt.
Umgesetzt wird ein dynamischer, je Aufrufstelle parametrisierter Lookback nur für
`ReconcileAsync`, gedeckelt durch eine neue konfigurierbare Obergrenze in
`ProgramScheduleOptions`; `EnsureCoverageAsync` bleibt beim 24-h-Lookback. Die Fensterverbreiterung
wird **validierungsgetrieben** ausgelöst: Ein Slot-Ebenen-Auslöser dient nur noch als
Kostenheuristik für den Regelfall; die Korrektheit stellt ein zweiter Validierungsdurchlauf auf
dem verbreiterten Fenster sicher, der genau dann läuft, wenn im Basisfenster eine
coverage-reichende Airing-Gruppe einer aktiven Regel invalid ist. Ergänzend wird die
Reproduzierbarkeitsprüfung um eine Mehr-Occurrence-Hypothese erweitert, weil lange Airings unter
dichten Regeln (z. B. täglich + > 24-h-Item) zwangsläufig lückenlose Same-Rule-Ketten über
mehrere Occurrences bilden, die `GroupAirings` zu einer Gruppe verschmilzt — die bisherige
Ein-Occurrence-Prüfung kann eine solche Gruppe auch mit unbegrenztem Fenster nie validieren.

## Designentscheidungen

| Komponente / Bereich | Gewählter Ansatz | Begründung |
|----------------------|-----------------|------------|
| Korrekturansatz | **Variante A – dynamischer Lookback** (statt gesonderter Toleranz, Variante B) | Eine Toleranz bei fehlendem `occurrenceStarts`-Treffer müsste in `AiringReproducible`/`AiringReproducibleInterleaved` die Beweisstrenge abschwächen und riskiert, echt veraltete Airings (geänderte Regel/Uhrzeit) zu „retten" — genau davor schützt die `invaderStarts`-Evidenz bewusst. Der erweiterte Lookback lässt die Anker-/Evidenzlogik unverändert: beide Prüfpfade profitieren, weil sie nur gegen die gelieferten `occurrenceStarts`/`invaderStarts` prüfen (Akzeptanzkriterium 3). |
| Geltungsbereich des erweiterten Fensters | Nur `ReconcileAsync`; `BuildOccurrences` erhält einen `lookback`-Parameter, `EnsureCoverageAsync` übergibt weiterhin `OccurrenceLookback` | Ein vergrößertes Occurrence-Fenster in `EnsureCoverageAsync` würde sehr alte Occurrences neu materialisieren bzw. Cursor-Stand konsumieren — unerwünscht. Der Lookback wird daher je Aufrufstelle parametrisiert. |
| Auslöser der Verbreiterung | **Heuristik + validierungsgetriebene Verbreiterung (dreiphasig):** (1) Basis-Fenster (24 h) laden. (2) Feuert der Slot-Ebenen-Auslöser `scheduled.Any(s => s.Start <= loadFrom)`, wird `loadFrom` einmalig auf die Obergrenze verbreitert, `scheduled` neu geladen und **einmal** validiert. (3) Feuert der Auslöser nicht, wird zunächst auf dem Basisfenster validiert — **rein lesend, ohne Verdrängung**; nur wenn dabei mindestens eine in die Coverage reichende Airing-Gruppe einer **aktiven** Regel invalid ist, wird verbreitert, neu geladen und **erneut** validiert. Die Suffix-Verdrängung läuft erst nach dem finalen Validierungsdurchlauf. | Der Auslöser auf Slot-Ebene ist **keine** Korrektheitsbedingung: Bei einer teilmaterialisierten Occurrence (`MaterializeOccurrence` schreibt nur Items mit `End > coverageStart`, Z. 1144–1152) konsumieren Kopf-Items Cursor-Stand ohne Zeile; der `k`-Rückwärtslauf rekonstruiert den realen Beginn dann vor `loadFrom`, obwohl alle geschriebenen Slots der Gruppe nach `loadFrom` beginnen (`k > 0`-Fall). Ohne Slot mit `Start <= loadFrom` — erreichbar bei junger Planhistorie (kein Slot älter als 24 h) oder einer `Scheduled`-Lücke an der Fensterkante (`Displaced`-Zeilen werden nie geladen, Z. 177; `FillGaps` wirkt nur ab `coverageStart`, Z. 342) — feuert er nicht, obwohl Vor-Fenster-Kontext fehlt. Die validierungsgetriebene Verbreiterung macht das deterministisch: Jede Gruppe, die nur mit Vor-Fenster-Kontext reproduzierbar ist, wird im Basisdurchlauf invalid und erhält genau einen zweiten Durchlauf mit Vollfenster. **Ehrliche Einordnung:** Im lückenlosen Regelfall feuert der Auslöser ohnehin — sobald die Planhistorie älter als 24 h ist, überdeckt ein `Scheduled`-Slot `loadFrom`. Er bleibt als Kostenheuristik erhalten (erspart im Regelfall den zweiten Validierungsdurchlauf und lädt gleich das volle Fenster), trägt aber keine Korrektheitslast mehr. **Monotonie:** Die Verbreiterung fügt nur Occurrence-Kandidaten und `invaderStarts`-Evidenz hinzu — sie kann Gruppen von invalid nach valid drehen, nie umgekehrt; echt veraltete Gruppen finden weiterhin weder Anker noch Evidenz (AC2). Gelöschte/inaktive Regeln können durch mehr Kontext nie gerettet werden (keine Occurrences) — die Beschränkung auf invalid-Gruppen aktiver Regeln vermeidet nachweislich nutzlose Reloads. |
| Verdrängungszeitpunkt | Validierung und Verdrängung werden getrennt: Die Validierung liefert je Regel den Index des ersten invaliden Airings (`firstInvalid`), ohne Slot-Stati zu ändern; die Suffix-Verdrängung wird erst nach dem finalen Durchlauf angewendet | Die heutige Schleife (Z. 212–240) validiert und verdrängt in einem Zug. Die Verdrängungsentscheidungen sind je Regel unabhängig (eine Gruppe beeinflusst die Validität anderer Regeln nicht), sodass Sammeln-then-Anwenden semantisch äquivalent ist — und erst diese Trennung macht die Basis-Validierung für den Nicht-Auslöser-Fall kostenlos wiederholbar. `airings`/`fillerSlots`/`invaderStarts`/`occurrences`/`occurrenceStarts`/`boundStarts` werden aus dem finalen Ladezustand aufgebaut; `keptSlots`/`blocked`/`DisplaceRuleFuture`/`RewindCursor`/Materialisierung arbeiten unverändert auf dem Endzustand. |
| Verschmolzene Multi-Occurrence-Ketten | **Mehr-Occurrence-Hypothese** in `AiringReproducible`/`AiringReproducibleInterleaved`: Eine Airing-Gruppe ist reproduzierbar, wenn sie sich in Teil-Airings **streng aufsteigender Occurrence-Starts** derselben Regel zerlegen lässt; jedes Teil-Airing wird mit derselben Anker-/Invader-Logik wie heute geprüft | Für Airings ≥ Wiederholungsintervall (Kern-Szenario: tägliche Regel + > 24-h-Item bzw. Marathon-Block) erzeugen `ShiftPastInvaders` und die Lookback-Konsumation eine lückenlose Same-Rule-Kette, die `GroupAirings` verschmilzt; `expected.Count - k == airing.Count` (Z. 524) bzw. `picks.Count - p != airing.Count` (Z. 667) kann dagegen prinzipiell nicht bestehen — unabhängig von der Fensterbreite. Die Alternative „Gruppen an Occurrence-Grenzen vorab splitten" scheidet aus: Die realen Grenzen sind verschoben, nicht nominal — sie zu finden *ist* die Reproduzierbarkeitsfrage selbst; ein vorab festgelegter falscher Schnitt (z. B. Slot-Kante am nominalen Start einer Occurrence, die nichts materialisierte) liefe in ein falsches `Displaced`. Die rekursive Zerlegung probiert dagegen alle Zerlegungen und akzeptiert nur eine echte Occurrence-Sequenz. Die Alternative „Test ohne Verschmelzung" (`Recurrence.Once`) deckt das Kern-Szenario des Bug-Reports nicht ab und reicht allein nicht. Die `invaderStarts`-Schutzlogik bleibt unverändert je Teil-Airing (AC2): Ein verschobener Folgestart ist nur plausibel, wenn ein belegter Slot den nominalen Start strikt überdeckte und exakt am realen Start endete — `ShiftPastInvaders` (Z. 972–988) schiebt nur über strikt überdeckende Intervalle und stoppt an Slot-Grenzen, sodass in einer echten Kette der den nominalen Start enthaltende Slot (letzter Slot des vorangegangenen Teil-Airings bzw. ein Fremd-Invader) exakt am realen Start endet und vor dem nominalen Start beginnt; bei einer veralteten Kette fehlt die Evidenz weiterhin. |
| Re-Materialisierung alter Occurrences | Die `MaterializeOccurrenceEvicting`-Schleife iteriert nur Occurrences mit `StartUtc >= coverageStart - OccurrenceLookback` (Basisfenster); `occurrenceStarts` und `boundStarts` nutzen den vollen verbreiterten Satz | Occurrences aus dem erweiterten Rückblick dienen ausschließlich der Reproduzierbarkeitsprüfung. Würden sie auch re-materialisiert, könnten sie hinter dem gehaltenen Tail verschoben Inhalte schreiben, die beim nächsten Lauf nicht reproduzierbar wären — Oszillation. Das Basisfenster entspricht exakt dem bisherigen Materialisierungsverhalten. |
| Obergrenze des Lookbacks | Neue Option `MaxOccurrenceLookbackDays` in `ProgramScheduleOptions` (Default 7 Tage) mit `EffectiveMaxOccurrenceLookback`-Normalisierung | Deckelt Lade- und Occurrence-Aufwand bei pathologisch langen Airings ab; folgt dem vorhandenen `Effective…`-Muster (`TrailerLeadDays`, `MinStartRemainSeconds`). 7 Tage decken Marathon-/Special-Blöcke und mehrtägige Filme ab. Airings, die älter als die Obergrenze begannen, werden weiterhin verdrängt (bewusste, konfigurierbare Grenze — als Test festgelegt). |
| Tagesschleife in `BuildOccurrences` | `windowStartDay` aus `coverageStartUtc - lookback` (lokal) ableiten statt fix `coverageStart`-Tag − 1 | Die bisherige Schleife erzeugt unabhängig vom Lookback keine Occurrences älter als ~2 Tage — ein > 24-h-Lookback bliebe ohne sie wirkungslos. Die Ableitung aus dem Lookback deckt beide Aufrufstellen korrekt ab und schließt nebenbei die DST-Kante, in der `coverageStart - 24 h` zwei lokale Kalendertage zurückreichen kann. |

## Programmabläufe

### `ReconcileAsync` mit dynamischem Lookback (geändert)

1. Regeln, Horizont, `coverageStart`, `RuleState`s und Cursor wie bisher laden (Z. 134–168).
2. `loadFrom = coverageStart - OccurrenceLookback` (Basis 24 h); `Scheduled`-Slots mit
   `End > loadFrom` laden.
3. **Auslöser-Heuristik (unverändert als Kostensparebene):** Enthält `scheduled` einen Slot mit
   `Start <= loadFrom`, liegt vor dem Basis-Fenster beginnender Kontext vor (Kopf-Slot einer
   langen Kette oder Fensterkante überdeckender Invader-Slot):
   `loadFrom = coverageStart - _maxOccurrenceLookback` setzen und `scheduled` mit dem
   verbreiterten Fenster neu laden.
4. **Strukturaufbau aus dem jeweils aktuellen `scheduled`** (als wiederholbarer Abschnitt, z. B.
   lokale Funktion): `reconcileLookback = coverageStart - loadFrom` (≥ 24 h, ≤ Obergrenze);
   `invaderStarts` aus `scheduled` (unveränderte Logik);
   `occurrences = BuildOccurrences(aktive Regeln ohne Filler, coverageStart, horizonEnd, federalState, reconcileLookback)`;
   `occurrenceStarts` und `boundStarts` daraus (unveränderte Logik);
   `airings = GroupAirings(Nicht-Filler-Slots)`, `fillerSlots`.
5. **Validierungsdurchlauf (rein lesend):** Je Airing-Gruppe wie bisher prüfen — Regel weg/
   inaktiv, keine `occurrenceStarts`, oder `AiringReproducible`/`AiringReproducibleInterleaved`
   `false` (erweitert um die Mehr-Occurrence-Hypothese, siehe eigener Ablauf) — und je Regel den
   Index des ersten invaliden Airings (`firstInvalid`) liefern, **ohne Slot-Stati zu ändern**.
   Vollständig gelaufene Gruppen werden wie bisher übersprungen.
6. **Validierungsgetriebene Verbreiterung — nur wenn Schritt 3 nicht feuerte:** Existiert unter
   den invaliden Gruppen mindestens eine, die in die Coverage reicht (`End > coverageStart`)
   und deren Regel gefunden und aktiv ist, kann die Invalidität an fehlendem
   Vor-Fenster-Kontext liegen (teilmaterialisierter Kopf, Occurrence-Start oder
   `invaderStarts`-Evidenz vor `loadFrom`): `loadFrom = coverageStart - _maxOccurrenceLookback`
   setzen, `scheduled` neu laden und Schritte 4–5 **einmalig** wiederholen. (Echter Veraltung
   kann die Verbreiterung nicht helfen — sie kostet dann nur einen gedeckelten Reload; Gruppen
   gelöschter/inaktiver Regeln lösen die Verbreiterung nicht aus, da mehr Kontext sie
   grundsätzlich nicht retten kann.)
7. **Verdrängung anwenden:** Je Regel mit `firstInvalid >= 0` die Suffix-Gruppen ab diesem Index
   über `Displace` verdrängen und `displacedRuleIds` füllen — exakt die bisherige
   Suffix-Semantik (Z. 230–239).
8. Filler-Validierung wie bisher (Z. 242–254) — fensterunabhängig (Regel weg/inaktiv oder
   Material nicht mehr in der Expansion).
9. Cursor-Rewind, `keptSlots`/`blocked` aus dem finalen `scheduled`,
   `MaterializeOccurrenceEvicting`-Schleife **nur** über
   `occurrences.Where(o => o.StartUtc >= coverageStart - OccurrenceLookback)` —
   Occurrences aus dem erweiterten Rückblick werden nicht re-materialisiert —, `DisplaceRuleFuture`,
   `FillGaps`, `PersistCursors`, `SaveChangesAsync` — unverändert.

Beteiligte Klassen/Komponenten: `ProgramScheduleService`, `ProgramScheduleOptions`, `ProgramSlot`,
`ProgramRule`, `RuleState`, `Occurrence`.

### Mehr-Occurrence-Reproduzierbarkeitsprüfung (geändert: `AiringReproducible`, `AiringReproducibleInterleaved`)

Flacher Pfad (`AiringReproducible`):

1. Bisherige Hypothesenfindung bleibt: Kopf-Item-Index `idx` per `FindItemIndex`, Rückwärtslauf
   über `k` übersprungene Kopf-Items → `airingStart` (rekonstruierter realer Beginn des ersten
   Teil-Airings), Abbruch bei `airingStart < earliestStart`.
2. Pro Kandidat `occurrenceStart <= airingStart` bleibt die `invaderStarts`-Evidenzpflicht
   bestehen (`occurrenceStart < airingStart` nur mit belegtem Slot, der den nominalen Start
   überdeckte und exakt bei `airingStart` endete).
3. `expected = SelectAiringItems(items, headIndex, rule, airingStart, BoundaryAfter(boundStarts, occurrenceStart, rule.Priority), minStartRemain)`
   liefert die Item-Sequenz des Kandidaten-Airings. Die **konsumierte Slot-Zahl**
   `c = expected.Count - k` muss nun `1 <= c <= airing.Count` erfüllen (bisher: `== airing.Count`).
4. Die ersten `c` Slots der Gruppe müssen wie bisher item- und grenzenweise matchen.
5. **Neu:** Ist `c < airing.Count`, wird der Rest `airing[c..]` rekursiv als Folge-Airings
   geprüft — mit denselben Regeln, aber Kandidaten-Occurrences **streng nach** dem verwendeten
   `occurrenceStart` (eine spätere Airing-Gruppe gehört materialisierungsseitig zu einer späteren
   Occurrence; Occurrences, die nichts materialisierten, werden dabei implizit übersprungen).
   Bei `c == airing.Count` endet die Zerlegung erfolgreich.
6. Terminierung: Jede Rekursionsstufe konsumiert mindestens einen Slot → Tiefe ≤ Gruppenlänge;
   die Kandidatenmenge je Stufe ist durch `occurrenceStarts` begrenzt. Zur Begrenzung des
   Verzweigungsaufwands bei pathologischen Gruppen dient eine Memoization über
   (Rest-Listenanfang, untere Occurrence-Schranke).

Interleaved-Pfad (`AiringReproducibleInterleaved`): analog — `picks.Count - p` ist die
konsumierte Slot-Zahl; statt `!= airing.Count → continue` gilt `1 <= picks.Count - p <= airing.Count`
mit Slot-für-Slot-Match des Präfixes und rekursiver Restprüfung gegen Occurrences
`> occurrenceStart`. `strandOf`/`firstIndex`/`countInAiring`/`maxPrefix` und die
`StrandStartOffset`-Kandidaten werden je Rekursionsstufe auf der Rest-Liste neu bestimmt.

Beteiligte Klassen/Komponenten: `ProgramScheduleService` (`AiringReproducible`,
`AiringReproducibleInterleaved`, `SelectAiringItems`, `SelectAiringItemsInterleaved`,
`BoundaryAfter`, `FindItemIndex`, `StrandState`).

### `EnsureCoverageAsync` (Signaturanpassung, Verhalten unverändert)

1. Ablauf wie bisher; `BuildOccurrences` wird mit dem bestehenden `OccurrenceLookback` (24 h)
   als Argument aufgerufen.

Beteiligte Klassen/Komponenten: `ProgramScheduleService`, `BuildOccurrences`.

## Neue Klassen

Keine — die Umsetzung erweitert ausschließlich `ProgramScheduleService` und
`ProgramScheduleOptions`.

## Änderungen an bestehenden Klassen

### `ProgramScheduleService` (Service)

- **Neue Felder:** `_maxOccurrenceLookback` (`TimeSpan`) — Obergrenze des dynamischen
  Lookbacks, im Konstruktor aus `scheduleOptions.EffectiveMaxOccurrenceLookback` gelesen
  (Muster `_trailerLead`/`_minStartRemain`, Z. 60–62).
- **Geänderte Methoden:**
  - `BuildOccurrences` — neuer Parameter `TimeSpan lookback`; `windowStartDay` wird aus
    `coverageStartUtc - lookback` in lokaler Zeitzone abgeleitet (ersetzt das fixe
    `coverageStart`-Tag − 1); der Fensterfilter lautet `startUtc >= coverageStartUtc - lookback`.
  - `ReconcileAsync` — dreiphasiger Ablauf: Slot-Laden, Strukturaufbau (`invaderStarts`,
    `occurrences`/`occurrenceStarts`/`boundStarts`, `airings`/`fillerSlots`) und die
    Airing-Validierung werden als wiederholbare Abschnitte organisiert (lokale Funktionen);
    die Validierung sammelt `firstInvalid` je Regel **ohne** Slot-Stati zu ändern. Verbreiterung
    auf `coverageStart - _maxOccurrenceLookback` (a) präventiv bei Auslöser-Treffer
    `scheduled.Any(s => s.Start <= loadFrom)` vor der Validierung oder (b) nachträglich nach der
    Basis-Validierung, wenn invalid-Gruppen aktiver Regeln in die Coverage reichen; danach ein
    finaler Validierungsdurchlauf, erst dann Suffix-Verdrängung. Zusammen mit dem
    `reconcileLookback`-Argument für `BuildOccurrences` und dem Basisfenster-Filter der
    `MaterializeOccurrenceEvicting`-Schleife (siehe Programmablauf).
  - `AiringReproducible` — Mehr-Occurrence-Hypothese: konsumierte Slot-Zahl
    `c = expected.Count - k` darf ein echtes Präfix der Gruppe sein; Rest-Validierung rekursiv
    gegen Occurrences `> occurrenceStart` (siehe Programmablauf). Anker-, Rückwärtslauf- und
    `invaderStarts`-Evidenzlogik bleiben unverändert.
  - `AiringReproducibleInterleaved` — analoge Erweiterung: `picks.Count - p` als konsumiertes
    Präfix, rekursive Restprüfung gegen Occurrences `> occurrenceStart`.
  - `EnsureCoverageAsync` — `BuildOccurrences`-Aufruf um das Argument `OccurrenceLookback`
    ergänzt; keine Verhaltensänderung.
- **Unverändert:** `OccurrenceLookback`-Konstante (24 h) bleibt als Basis-Fenster;
  `GroupAirings` (die Verschmelzung ist fachlich korrekt — eine Kette IST eine Folge von
  Airings, keine einzelne), `ShiftPastInvaders`, `MaterializeOccurrence(Evicting)`,
  `Displace`, `RewindCursor`, `IProgramScheduleService`.

### `ProgramScheduleOptions` (Konfigurationsklasse)

- **Neue Eigenschaften:**
  - `MaxOccurrenceLookbackDays` (`int`, Default `7`) — konfigurierbare Obergrenze des
    dynamischen Reconcile-Lookbacks in Tagen.
  - `EffectiveMaxOccurrenceLookback` (`TimeSpan`, get-only) — normalisierter Wert;
    `MaxOccurrenceLookbackDays <= 0` fällt auf den Default 7 Tage zurück (Muster
    `EffectiveTrailerLead`/`EffectiveMinStartRemain`).

## Datenbankmigrationen

Keine.

## Validierungsregeln

Keine — keine neuen Eingaben. Die Options-Normalisierung (`<= 0` → Default) folgt dem
`Effective…`-Muster und ist kein Validierungsfall.

## Konfigurationsänderungen

| Eintrag | Typ | Standardwert | Zweck |
|---------|-----|--------------|-------|
| `ProgramSchedule:MaxOccurrenceLookbackDays` (`ProgramScheduleOptions.MaxOccurrenceLookbackDays`) | `int` (Tage) | `7` | Obergrenze des dynamischen Lookbacks in `ReconcileAsync`; begrenzt Slot-Lade- und Occurrence-Aufwand bei extrem langen Airings |

`appsettings.json` bleibt unverändert (Default greift); ein expliziter Eintrag ist optional.
Dokumentation: Die neue Option wird in `docs/help/tv-sender.md` (Abschnitt
Konfiguration/Einstellungen) und in `docs/RELEASE_NOTES.md` nachgetragen (DoD `AGENTS.md` §7).

## Seiteneffekte und Risiken

- **Lade- und Validierungsaufwand in `ReconcileAsync`:** Drei Fälle — (a) Auslöser feuert
  (Regelfall: lückenloser Plan älter als 24 h → stets ein `Scheduled`-Slot mit
  `Start <= loadFrom`): ein gedeckelter Reload, ein Validierungsdurchlauf — wie bisher geplant.
  (b) Auslöser feuert nicht, alle Gruppen valid: kein Reload, ein zusätzlicher
  Validierungsdurchlauf im Speicher (billig, keine DB-Zugriffe). (c) Auslöser feuert nicht +
  invalid-Gruppe aktiver Regel in der Coverage: ein Reload (gedeckelt durch
  `MaxOccurrenceLookbackDays`) + erneute Validierung — kostet zusätzliche `ProgramSlot`-Zeilen
  (bei ~45-min-Slots grob 250–500 über 7 Tage) und einen Occurrence-Aufbau; nur die
  Verbreiterung kann eine solche Gruppe legitim retten, echt veraltete bleiben invalid.
  Die Monotonie des Mechanismus (Verbreiterung fügt nur Kandidaten/Evidenz hinzu) garantiert,
  dass im Regelfall (a) das Ergebnis exakt dem bisher geplanten entspricht.
- **Restgrenze bleibt bestehen (ehrlich dokumentiert):** Airings, deren realer Beginn älter als
  `MaxOccurrenceLookbackDays` vor `coverageStart` liegt, werden weiterhin verdrängt — bewusste,
  konfigurierbare Grenze (Test `Reconcile_LongAiring_BeyondMaxLookback_Displaced`).
- **Mehr-Occurrence-Zerlegung:** Die Rekursion verzweigt über (Occurrence-Kandidaten ×
  Präfix-Hypothesen) je Stufe; Tiefe ≤ Gruppenlänge. Ohne Memoization wäre der Aufwand bei
  pathologischen Gruppen (lange Kette, viele Occurrences im 7-Tage-Fenster) superlinear —
  daher ist die Memoization über (Rest-Listenanfang, Occurrence-Schranke) Teil der Umsetzung.
- **Zulässigkeit der Zerlegung (AC2):** Jedes Teil-Airing benötigt weiterhin eigenen Anker
  (nominaler Occurrence-Start ≤ rekonstruierter realer Beginn) und bei Verschiebung
  `invaderStarts`-Evidenz. Die Erweiterung lockert nur die Zählbedingung
  (`== airing.Count` → `<= airing.Count` mit Restprüfung); eine veraltete Kette findet keine
  passende Occurrence-Sequenz und bleibt `Displaced`. Die Schutzfunktion gegen „stale" Anker
  ist nicht aufgeweicht.
- **`invaderStarts`:** Enthält bei verbreitertem Fenster mehr Kandidaten; die Schutzlogik
  (`invaderStart < occurrenceStart`) ist unverändert — ältere echte Verschiebungen werden
  korrekt als Evidenz akzeptiert (gewünscht), falsche Anker bleiben ausgeschlossen.
- **Nachgelagerter Verdrängungszeitpunkt:** Sammeln-then-Anwenden ändert die Ergebnismenge
  nicht — die `firstInvalid`-Entscheidungen je Regel sind unabhängig voneinander, und
  `Displace` wird auf demselben `ordered`-Stand wie heute ausgeführt. Risiko: minimal,
  abgesichert durch die volle Reconcile-Regressionssuite.
- **`EnsureCoverageAsync`:** Verhaltensneutral; der parametrisierte Lookback bleibt 24 h —
  keine Materialisierung sehr alter Occurrences, keine Cursor-Seiteneffekte.
- **`boundStarts`/`BoundaryAfter`:** Zusätzliche alte Bound-Starts beeinflussen
  `BoundaryAfter(boundStarts, afterUtc, …)` nicht (es wird der erste Bound strikt nach
  `afterUtc` gesucht).
- **Tagesschleifen-Umstellung:** `windowStartDay` aus `coverageStartUtc - lookback` ist für
  das 24-h-Basisfenster nahezu äquivalent zum bisherigen −1-Tag und deckt den DST-Sonderfall
  (24 h = 2 lokale Kalendertage) korrekter ab — Verhalten in `EnsureCoverageAsync` im
  Regressionslauf verifizieren.
- **Airings jenseits der Obergrenze:** Beginn älter als `MaxOccurrenceLookbackDays` → weiterhin
  `Displaced` + Neu-Einplanung — bewusste, konfigurierbare Grenze (als Test
  `Reconcile_LongAiring_BeyondMaxLookback_Displaced` festgelegt, kein stiller Fehler); dabei
  akkumulieren `Displaced`-Zeilen wie heute — in `docs/help/tv-sender.md` zu vermerken.

## Umsetzungsreihenfolge

1. **Reproduzierende Tests schreiben (TDD — rot)**
   - Voraussetzungen: Keine (Fixture `ProgramScheduleTestDb` mit `CreateMovieAsync`,
     `CreateShowAsync`, `CreateGenreAsync`/`LinkShowGenreAsync`, `CreateRuleAsync`,
     `CreateService(scheduleOptions)` existiert).
   - Beschreibung: Neue Tests in `ProgramScheduleServiceTests_Reconcile.cs` und
     `ProgramScheduleServiceTests_Interleaving.cs` gemäß Abschnitt „Neue Tests". Kern-Muster:
     tägliche Regel + Item bzw. Item-Kette > 24 h → `EnsureCoverageAsync` vor der Occurrence →
     `ReconcileAsync` zu einem Zeitpunkt > 24 h nach dem Occurrence-Start →
     `SlotsDisplaced == 0` und Slots bleiben `Scheduled` (aktuell rot — teils wegen des
     Lookbacks, teils wegen der verschmolzenen Kette). Vollständige Suite laufen lassen:
     neue Tests rot, alle bisherigen grün.

2. **`ProgramScheduleOptions` um `MaxOccurrenceLookbackDays`/`EffectiveMaxOccurrenceLookback` erweitern**
   - Voraussetzungen: Keine.
   - Beschreibung: Neue int-Eigenschaft mit Default 7 und `Effective…`-Normalisierung nach dem
     `TrailerLeadDays`-Muster; XML-Doku im bestehenden Stil.

3. **Fenster-Umbau in `ProgramScheduleService` (Lookback-Infrastruktur + dreiphasiger Reconcile-Ablauf)**
   - Voraussetzungen: Schritt 2 (`EffectiveMaxOccurrenceLookback` für `_maxOccurrenceLookback`).
   - Beschreibung: Feld `_maxOccurrenceLookback` im Konstruktor lesen; `BuildOccurrences` um
     Parameter `lookback` erweitern (Tagesschleifen-Start und Fensterfilter daraus ableiten);
     `ReconcileAsync`: Laden von `scheduled`, Strukturaufbau und die Gruppen-Validierung als
     wiederholbare Abschnitte organisieren (lokale Funktionen); die Validierung liefert
     `firstInvalid` je Regel ohne zu verdrängen; Auslöserprüfung
     `scheduled.Any(s => s.Start <= loadFrom)` mit bedingtem Reload **vor** der Validierung,
     sowie nachträgliche Verbreiterung + Re-Validierung bei invaliden Coverage-Gruppen aktiver
     Regeln im Nicht-Auslöser-Fall; Suffix-Verdrängung erst nach dem finalen Durchlauf;
     `reconcileLookback` an `BuildOccurrences` übergeben; Materialisierungsschleife auf
     `o.StartUtc >= coverageStart - OccurrenceLookback` filtern;
     `EnsureCoverageAsync`: `OccurrenceLookback` als Argument übergeben.

4. **Mehr-Occurrence-Hypothese in `AiringReproducible`/`AiringReproducibleInterleaved`**
   - Voraussetzungen: Keine fachlichen (kann parallel zu Schritt 3 laufen; die neuen Tests
     werden erst nach Schritten 3 + 4 grün).
   - Beschreibung: Konsumierte Slot-Zahl als Präfix zulassen und Rest-Liste rekursiv gegen
     Occurrences `> occurrenceStart` prüfen (beide Pfade); Memoization über
     (Rest-Listenanfang, Occurrence-Schranke); Methoden-Kommentare anpassen.

5. **Dokumentation**
   - Voraussetzungen: Schritt 2 (Optionsname steht fest).
   - Beschreibung: `ProgramSchedule:MaxOccurrenceLookbackDays` in `docs/help/tv-sender.md`
     dokumentieren (inkl. Hinweis, dass Airings jenseits der Obergrenze verdrängt und neu
     eingeplant werden); Eintrag in `docs/RELEASE_NOTES.md` (Korrektur + neue Option).

6. **Testsuite grün fahren + Regressionsprüfung**
   - Voraussetzungen: Schritte 1–4 (Schritt 5 kann parallel).
   - Beschreibung: Neue Tests müssen grün sein; volle Suite
     `dotnet test VideoWebPlayer.Tests/VideoWebPlayer.Tests.csproj` — besondere Aufmerksamkeit
     auf `Reconcile_ShiftedAndOvershootingAirings_StayStableAcrossRuns`,
     `Reconcile_PartiallyAiredOccurrence_*`, `Reconcile_WorkerLoop_NoDisplacedAccumulationNoOverlap`,
     die Interleaved- und Generation-Suiten (Mehr-Occurrence-Prüfung und der geänderte
     Verdrängungszeitpunkt dürfen im Basis-Fenster nichts verändern — die meisten
     Bestandstests laufen mit < 24-h-Planhistorie und decken damit genau den neuen
     Nicht-Auslöser-Pfad ab). Build in Debug UND Release
     (`dotnet build VideoPlayer.sln`, DoD `AGENTS.md` §7).

## Tests

### Neue Tests

| Test / Hilfsmethode | Testklasse | Was wird geprüft / bereitgestellt? |
|--------------------|------------|-------------------------------------|
| `Reconcile_LongAiring_StaysScheduled` | `ProgramScheduleServiceTests_Reconcile` | Akzeptanzkriterium 1 + verschmolzene Kette: `CreateMovieAsync` mit `Duration` > 24 h (≈ 30 h), tägliche `MoviePrimeTime`-Regel (`episodeCount: 1`); `EnsureCoverageAsync` vor der Occurrence — die Folge-Occurrence wird im 24-h-Lookback verschoben hinter dem Tail materialisiert → `GroupAirings` verschmilzt zu einer 2-Slot-Kette. `ReconcileAsync` > 24 h nach dem nominalen Occurrence-Start → `SlotsDisplaced == 0`, beide Slots bleiben `Scheduled` (Mehr-Occurrence-Zerlegung greift); zweiter Reconcile-Lauf bleibt ruhig (keine Oszillation, Muster `Reconcile_PartiallyAiredOccurrence_KeepsTailScheduled`). Schlägt im Ist-Zustand fehl. |
| `Reconcile_LongAiring_BeyondTwoDays_StaysScheduled` | `ProgramScheduleServiceTests_Reconcile` | Wie oben, aber Item-Dauer > 48 h und `ReconcileAsync` > 2 Tage nach dem Occurrence-Start — deckt zusätzlich die Tagesschleifen-Erweiterung ab (Ist-Grenze `coverageStart`-Tag − 1). |
| `Reconcile_MultiPartLongAiring_StaysScheduled` | `ProgramScheduleServiceTests_Reconcile` | **Mehrteiliges** > 24-h-Airing (Marathon-Block aus der Anforderung): `CreateShowAsync` mit Episoden à 45 min, Regel `Daily` mit `slotDuration` ≥ 30 h (oder hohem `episodeCount`) → Airing als Kette vieler Einzel-Slots; Kopf-Slots enden vor `loadFrom`, die Kette reicht über `coverageStart` — kein Einzelslot überspannt beide Grenzen → prüft den Auslöser-Pfad (`Start <= loadFrom`). `ReconcileAsync` > 24 h nach dem Occurrence-Start → `SlotsDisplaced == 0`. Schlägt im Ist-Zustand fehl. |
| `Reconcile_LongAiring_Shifted_StaysScheduled` | `ProgramScheduleServiceTests_Reconcile` | Akzeptanzkriterium 4 (`invaderStarts`-Evidenz): Vorgänger-Airing überschießt den Occurrence-Start (Muster `EnsureCoverage_OversizedHeadItem_AirsAndOvershootsSuccessor`), das > 24-h-Airing startet real verspätet; `ReconcileAsync` > 24 h nach dem nominalen Start → bleibt `Scheduled` (der Invader-Slot endet außerhalb des Basis-Ladefensters und muss mitgeladen werden — der `Start <= loadFrom`-Auslöser feuert über den Fensterkante überdeckenden Invader-Slot). |
| `Reconcile_PartiallyMaterializedLongAiring_NoTrigger_StaysScheduled` | `ProgramScheduleServiceTests_Reconcile` | **Restlücke `k > 0` (deterministische Abdeckung):** Junge Planhistorie — `EnsureCoverageAsync` zu `t₀` mit einer Occurrence, die innerhalb von `t₀ − 24 h` liegt (tail-only materialisiert: Kopf-Items enden vor `t₀` ohne Zeile) und deren realer Beginn vor `coverageStart − 24 h` liegt; Item-Sequenz so gewählt, dass die erste geschriebene Slot-Zeile nach `coverageStart − 24 h` beginnt. `ReconcileAsync` zu `coverageStart` mit `coverageStart − t₀ < 24 h` → kein geladener Slot beginnt `<= loadFrom` → der Auslöser feuert nachweislich nicht. Erwartung: `SlotsDisplaced == 0` — die validierungsgetriebene Verbreiterung (Basis-Validierung invalid → Reload auf Obergrenze → erneute Validierung) rettet das Airing. Schlägt im Ist-Zustand fehl **und** würde auch mit einem reinen Auslöser-Mechanismus ohne Validierungs-Rückfall fehlschlagen. Alternative Konstruktion (gleichwertig, falls die Zeitkonstellation instabil ist): `Displaced`-Block per `CreateSlotAsync` auf der Fensterkante statt junger Historie — derselbe Zustand „kein Slot `Start <= loadFrom`". |
| `Reconcile_LongAiring_ChangedRule_StillDisplaced` | `ProgramScheduleServiceTests_Reconcile` | Akzeptanzkriterium 2: > 24-h-Airing (verschmolzene Kette) wird nach `TimeOfDay`-Änderung der Regel (oder Deaktivierung) weiterhin verdrängt — weder Fensterverbreiterung noch Mehr-Occurrence-Hypothese noch der Validierungs-Rückfall retten echt veraltete Airings. |
| `Reconcile_LongAiring_BeyondMaxLookback_Displaced` | `ProgramScheduleServiceTests_Reconcile` | Ehrliche Grenze: `CreateService(Options.Create(new ProgramScheduleOptions { MaxOccurrenceLookbackDays = 1 }))` → 30-h-Airing wird verdrängt und neu eingeplant — dokumentiert und prüft die Optionsverdrahtung sowie das Verhalten jenseits der Obergrenze. |
| `Reconcile_GenreRule_Interleaved_LongAiring_StaysScheduled` | `ProgramScheduleServiceTests_Interleaving` | Akzeptanzkriterium 3 (Interleaved-Pfad, Pick-Kette): `SeedGenreWindowAsync`-Muster mit zwei Serien, deren Episoden-Dauern so gewählt sind, dass die Round-robin-Pick-Kette einer Occurrence > 24 h umfasst (z. B. `CreateShowAsync(duration: ~13 h)` bei `episodeCount: 2`, oder `slotDuration` > 24 h mit Lang-Episoden) → unter der täglichen Regel verschmolzene Interleaved-Kette; `ReconcileAsync` > 24 h nach dem Occurrence-Start → `SlotsDisplaced == 0`. Schlägt im Ist-Zustand fehl (`occurrenceStarts.Where(o <= airing[0].Start)` leer bzw. `picks.Count - p != airing.Count`). |
| `ProgramScheduleOptions_MaxOccurrenceLookback_Normalizes` | `ProgramScheduleServiceTests_Boundary` | `EffectiveMaxOccurrenceLookback`: `<= 0` → Default 7 Tage, sonst `TimeSpan.FromDays(Wert)` — Muster `ProgramScheduleOptions_MinStartRemain_Normalizes`. |

### Betroffene bestehende Tests

Keine — Signaturen öffentlicher Schnittstellen, Entitäten und bestehendes Verhalten im
24-h-Basisfenster bleiben unverändert. Die Mehr-Occurrence-Prüfung ist eine Obermenge der
bisherigen Prüfung: Jede bisher gültige Ein-Occurrence-Gruppe validiert weiter identisch
(`c == airing.Count`-Fall). Der nachgelagerte Verdrängungszeitpunkt ist ergebnisgleich
(Entscheidungen je Regel unabhängig). Die meisten Reconcile-Bestandstests laufen mit
< 24-h-Planhistorie — sie durchlaufen damit automatisch den neuen Nicht-Auslöser-Pfad
(Basis-Validierung ohne Heuristik-Treffer) und sichern ihn als Regression ab. Die komplette
Suite läuft als Regression grün.

### E2E-Tests (primärer Funktionsnachweis)

Keine E2E-Tests erforderlich — die Anforderung berührt keinen Benutzerfluss: Die Korrektur
liegt vollständig in der internen Sendeplan-Logik des `ProgramScheduleWorker`
(`ReconcileAsync`/`EnsureCoverageAsync`); es ändern sich weder UI noch API-Vertrag noch
anwendersichtbare Abläufe. Der Funktionsnachweis erfolgt über Service-Integrationstests gegen
echtes SQLite (`ProgramScheduleTestDb`), die exakt den produktiven Worker-Pfad
(`EnsureCoverageAsync` → `ReconcileAsync`-Kadenz) durchlaufen — inklusive Oszillations-Nachweis
über mehrere Läufe. Ein Playwright-Test könnte hierüber nichts zusätzlich nachweisen.

## Offene Punkte

Keine.
