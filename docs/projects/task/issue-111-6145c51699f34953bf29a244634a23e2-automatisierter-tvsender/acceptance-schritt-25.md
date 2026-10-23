# Abnahme Schritt 25 — Videos niemals kuerzen (N22)

Prüfer: Devin (unabhängig, ohne Implementierungskontext)
Geprüfter Stand: `10e4f47` auf `task-issue-111-6145c51699f34953bf29a244634a23e2-automatisierter-tvsender-schritt-25-kein-kuerzen`
Gegenstand: N22 — ein begonnenes Video läuft immer vollständig (Überlauf über das Fenster-Ende
erlaubt), die Folge-Ausstrahlung verschiebt sich hinter das reale Ende; unter der Mindest-Restlücke
(`ProgramSchedule:MinStartRemainSeconds`, Default 30 s) wird nichts eingeplant; gegen höherpriore
Folgefenster (harte Grenze) defert ein nicht komplett passendes Item statt abgeschnitten zu werden.

## Ergebnis

**Status:** Abweichungen gefunden

Die Kern-Anforderung ist verifiziert erfüllt: kein Pfad schreibt mehr ein gekürztes `Slot.End`,
die Min-Rest- und Hard-Bound-Logik greift, `ShiftPastInvaders` verschiebt Folge-Occurrences real
und `AiringReproducible` akzeptiert verschobene Airings nur mit belastbarer Invasor-Evidenz.
Builds und volle Testsuite sind grün, die Gegenprobe beweist die Wirkung der neuen Tests.
Die Befunde sind zwei kleinere Code-Punkte (eine echte, aber schmale Edge-Case-Lücke) plus ein
Prozess-Hinweis — keine funktionale Kürzung, kein Datenrisiko.

## Verifikation

- **Diff-Review** `task/...-automatisisierter-tvsender...HEAD`: 6 Dateien, +385/−213.
  `ProgramScheduleOptions` (+`MinStartRemainSeconds`/`EffectiveMinStartRemain`, korrekt an
  `ProgramSchedule`-Section gebunden, `ServiceCollectionExtensions.cs:235`), `ProgramScheduleService`
  (`BoundaryAfter` → `(StartUtc, Hard)?`; `SelectAiringItems`/`SelectAiringItemsInterleaved` mit
  Min-Start-Rest + Hard-Bound; `ShiftPastInvaders` in beiden Materialisierungspfaden;
  `DisplaceAiringSuffix` + Invasor-Loser-Zweig entfernt; `AiringReproducible(-Interleaved)` mit
  Invasor-Evidenz), 2 Testdateien, `RELEASE_NOTES.md`, `docs/help/tv-sender.md`.
- **Build**: `dotnet build VideoPlayer.sln -c Release` in-place: **0 Fehler, 144 Warnungen — alle
  pre-existing** (BL0008/CS86xx/CS0436 in unverwandten Dateien; keine Warnung in `ProgramSchedule*`).
  Debug: `bin\Debug` ist durch die laufende Benutzer-App (`VideoWebPlayer` PID 54112, MSB3026/MSB3027)
  gesperrt — Prozess **nicht** beendet. Die Kompilierung selbst ist fehlerfrei (frische
  `obj/Debug/net10.0/VideoWebPlayer.dll`), und der Testlauf hat den vollständigen Debug-Build in ein
  Scratch-`OutDir` (`.scratch-testbin`) geschrieben — 0 Fehler über alle Projekte. Debug-Build damit
  verifiziert, nur nicht ins gesperrte `bin`. (Gleiche Lage wie bei Schritt 23, dort dokumentiert.)
- **Tests**: `dotnet test VideoWebPlayer.Tests/VideoWebPlayer.Tests.csproj -p:OutDir=<scratch>`,
  synchron, Dauer 5 m 37 s: **2182 grün, 1 Alt-Skip, 0 fehlgeschlagen** (gesamt 2183) — exakt die
  erwartete Zahl.
- **Gegenprobe**: `ShiftPastInvaders` auf `return startUtc;` gesetzt → 5 von 11 Boundary-Tests
  schlagen fehl (`EnsureCoverage_AdjacentWindows_OvershootCompletesAndShiftsSuccessor`,
  `EnsureCoverage_ShortRemainder_OvershootingItem_Completes`,
  `Reconcile_ShiftedAndOvershootingAirings_StayStableAcrossRuns`,
  `Reconcile_PartialOccurrence_OvershootingTailStaysScheduled_SuccessorShifts`,
  `EnsureCoverage_OversizedHeadItem_AirsAndOvershootsSuccessor`). Die neuen Tests sichern den
  Shift-Pfad also wirklich ab. Danach `git checkout --` — Arbeitsbaum sauber.
- **Kein Kürzungs-Pfad mehr**: beide Materialisierungspfade schreiben `End = start + item.Duration`
  (`ProgramScheduleService.cs:762`, `:1092`) ohne jede Kappung; `FillGaps` platziert Füller nur wenn
  sie in die Lücke passen (bzw. Horizont-Überstand), ebenfalls volle Dauer (`:1373`, `:1380`).
  `slot.End` wird nirgends auf eine Grenze gestutzt — verifiziert per Volltextsuche über alle
  `End`-Zuweisungen.
- **Min-Rest / Defer**: `SelectAiringItems` bricht bei `bound.StartUtc - slotStart < minStartRemain`
  (Start nur bei ≥30 s Rest, `>=` korrekt implementiert) und bei `b.Hard && !komplett-vor-Grenze`
  (`:1265`-`1270`); Cursor wird nur für tatsächlich platzierte Items committed (`CommitTaken`),
  deferrierte Items bleiben im Ring — belegt durch `EnsureCoverage_RemainderBelowMinRemain_PlansFiller`
  (15 s < 30 s → Füller statt Slot) und `EnsureCoverage_HardBound_ItemCannotComplete_DefersToNextOccurrence`
  (Sonntag nichts geplant, Samstag volle Ringfolge, kein Cursor-Eintrag). `Conflicts`-Test bestätigt
  `DroppedConflicts == 0` bei harter Grenze.
- **Shift real**: `ShiftPastInvaders` läuft vor der Item-Auswahl über alle belegten Intervalle
  (`blocked` enthält Kept-Slots + bereits materialisierte neue Ranges), verkettet bei Bedarf; die
  Nachfolge-Ausstrahlung beginnt am realen Ende des Überlaufs — belegt durch
  `..._ShortRemainder_OvershootingItem_Completes` (19:57 + 45 min → 20:42; Folge-Slots starten
  exakt `slot.End`) und `..._AdjacentWindows_OvershootCompletesAndShiftsSuccessor` (16:16).
- **Invasor-Evidenz**: `invaderStarts` (`:184`-`186`) bildet `End → min(Start)` über alle geladenen
  Scheduled-Slots; ein verschobenes Airing wird nur akzeptiert, wenn ein Slot exakt am realen Start
  endet und vor dem nominalen Start beginnt (Abdeckung des nominalen Starts, `:519`-`521` / `:649`-`652`).
  Stale Airings ohne echten Invasor (z. B. geänderte `TimeOfDay`) fallen korrekt durch — Kandidaten
  `o > airingStart` kommen gar nicht erst in die Schleife. Gegenprobe indirekt belegt (Shift aus →
  Evidenz-Check rettet nichts, Airings werden verdrängt).
- **Aufräumen**: `DisplaceAiringSuffix` ist vollständig entfernt (kein Treffer mehr), der
  Invasor-Loser-Spezialzweig im Reconcile-Loop ist ersetzt; `RewindCursor`/`CommitTaken` haben nur
  noch legitime Aufrufer. Kein kompilierbarer toter Pfad.

## Abweichungen

- [ ] **Veralteter Kommentar + effektiv toter Filter** in `MaterializeOccurrenceEvicting`
  (`ProgramScheduleService.cs:776`-`779`): Der Kommentar beschreibt noch das entfernte Verhalten
  („they always yield (the caller trims their airing's suffix)"), und die Unterscheidung
  `competitors = conflicts.Where(k => k.Start >= occurrence.StartUtc)` ist seit `ShiftPastInvaders`
  leer — ein Kept-Slot, der vor dem Occurrence-Start beginnt und eine Range überlappt, kann nicht
  mehr existieren (der Shift schiebt den Airing-Start hinter sein Ende). Rein kosmetisch, aber
  irreführend für den nächsten Bearbeiter.
- [ ] **`startOffset`-Inkonsistenz bei verschobenem Interleaved-Airing über eine
  UTC-Datumsgrenze** (`ProgramScheduleService.cs:604`-`606` vs. `:1198`-`1200`): Die
  Reproduktionsprüfung leitet den Strang-Start-Offset aus dem *nominalen* `occurrenceStart.Date` ab,
  die Materialisierung nutzt aber `airingStartUtc.Date` (den verschobenen realen Start). Verschiebt
  ein Überlauf ein Interleaved-Airing (`EpisodeCount < Strangzahl`, Genre-Regel) über Mitternacht
  UTC — bei deutschen Abend-Sendezeiten (UTC+1/+2) durchaus erreichbar —, rekonstruiert die Prüfung
  mit der falschen Strang-Rotation → False-Negative → das identische Airing wird bei *jedem*
  Reconcile verdrängt und identisch neu materialisiert (Churn wie bei der alten Oszillation,
  nur ohne sichtbare Planänderung). Kein Kürzungs-Verstoß, aber ein Stabilitäts-Mangel; Fix wäre,
  den Offset aus `simStart` (oder beiden Datums-Kandidaten) zu versuchen.
- [ ] **Prozess**: In `handover.md` fehlt die Bearbeiter-Zeile für Schritt 25 (Tabelle endet bei 23;
  auch 24 hat keine Zeile — wurde in `acceptance-schritt-24.md` bereits gerügt), und es gibt keinen
  Übergabestand-Abschnitt zu Schritt 25. `steps.md` steht noch auf „In Arbeit" — vermutlich bis zur
  Abnahme korrekt, aber der Handover-Eintrag gemäß AGENTS.md §2/§5 fehlt.

## Hinweise

- Die laufende Benutzer-App (`VideoWebPlayer.exe`, PID 54112; gestartet aus `bin\Debug` laut Lock-
  Meldung „Microsoft Visual Studio (37576), VideoWebPlayer (54112)") wurde **nicht** angefasst.
  Release-Build lief in-place durch; Debug + Tests liefen gegen `.scratch-testbin` (Ordner nach dem
  Lauf wieder entfernt).
- `invaderStarts` wird vor den Verdrängungen des Laufs aus `scheduled` aufgebaut: ein in derselben
  Runde verdrängter Invasor liefert weiterhin Evidenz für ein verschobenes Airing dahinter — das
  Airing bleibt an seiner verschobenen Position gültig und hinterlässt eine Füller-Lücke statt
  zurückzurutschen. Konservative Richtung (kein Stale-Content-Durchwinken: Inhalt wird voll
  resimuliert), höchstens suboptimal.
- Die Atomarität ist erhalten: Überlappt ein neu materialisiertes Airing eine bereits belegte Range
  mitten im Fenster (höherpriore Ausstrahlung zuerst materialisiert), wird das ganze Airing
  verworfen statt teilweise geschrieben — `DroppedConflicts++`, kein Teilschnitt.
- Die neue Semantik „höherpriore Folge = harte Grenze, sonst weich" ist in `BoundaryAfter`
  (`:1294`-`1304`) sauber implementiert; `boundStarts` bleibt nominal-start-basiert (konservativ für
  verschobene Folgen).
- Grenzfälle geprüft: `MinStartRemainSeconds <= 0` → Default 30 s (`EffectiveMinStartRemain`);
  degenerierte Zero-Duration-Items terminieren über die Ring-/Lap-Wächter (`:1274`-`1276`,
  `:1227`-`1231`); Füller-Slots zählen ebenfalls als Invasor-Evidenz, was fachlich stimmt (ein Füller
  kann einen Occurrence-Start legitim belegen).
