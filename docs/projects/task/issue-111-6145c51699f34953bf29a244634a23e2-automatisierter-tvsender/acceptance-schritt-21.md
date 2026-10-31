# Abnahme Schritt 21 — Planer-Eviction / stabiler Plan (N16)

Prüfer: Devin (unabhängige Abnahme, nicht der Implementierer)
Geprüfter Stand: `a684270` (`step: Schritt 21 Implementierung abgeschlossen —
Abnahme ausstehend`), Arbeitsbaum nach den Gegenproben wieder sauber
(`git diff HEAD` leer; einzig untracked ist `VideoWebPlayer/Data/LiveStream/` —
produktive Live-Ausgabe des Anwenders, nicht vom Agenten erzeugt, per AGENTS §8
unangetastet liegen gelassen).

## Ergebnis

**Status:** Erfüllt

Die N16-Anforderung ist im Code verifiziert umgesetzt und durch drei neue Tests
abgesichert, deren Wirksamkeit ich selbst durch gezieltes Zurücknehmen beider
Fix-Teile bewiesen habe. Der Diagnose-Bericht des Implementierers ist zutreffend:
`ReconcileAsync` stufte (a) den per Design mitten in einer laufenden Occurrence
beginnenden Sendungs-Rest (`MaterializeOccurrence` schreibt nur Slots mit
`End > coverageStart`, `ProgramScheduleService.cs:826-832`) über die
`occurrenceStarts.Contains(airing[0].Start)`-Bedingung fälschlich als invalid
ein → `firstInvalid` verdrängte die gesamte Regel-Zukunft → Neu-Materialisierung
schrieb denselben Tail erneut → Oszillation im Worker-Takt (Live-Befund: 4520
Displaced vs. 38 Scheduled); und (b) kappte `scheduled` mit
`s.Start < horizonEnd` — über den Horizont hinausragende Airings wurden verkürzt
geladen, scheiterten am Count-Vergleich und wurden über die noch geplanten
jenseitigen Slots hinweg neu materialisiert → überlappende Duplikate (Live-Bild
„8 Items ~14:43–14:45").

### Verifiziert (Code-Review)

- **`AiringReproducible` Rückwärtslauf korrekt** (`ProgramScheduleService.cs:426-492`):
  `k=0` ist exakt die alte Semantik (erster Slot beginnt am Occurrence-Start,
  volle Sequenz muss reproduzieren). Für `k>0` akkumuliert der Rückwärtslauf die
  Ring-Vorgänger-Dauern `items[idx-1..idx-k]` — das ist korrekt, weil die
  Materialisierung stets ring-konsekutive Items vom Cursor aus schreibt, der
  geschriebene Tail also ein echtes Suffix der Occurrence-Expansion ist.
  Akzeptanz nur, wenn `airing[0].Start - Σ Kopf-Items` **exakt** einem
  Occurrence-Start der Regel entspricht UND der Tail Count
  (`expected.Count - k`), MediaItemIds und Slot-Grenzen unter derselben
  `EpisodeCount`/`SlotDuration`-Logik reproduziert. Falsche Positive sind
  inhaltlich ausgeschlossen: ein Tail, der diesen Test besteht, ist per
  Konstruktion identisch zu dem, was die Regel für diese Occurrence produziert
  hätte — die einzige nicht verifizierbare Annahme (Cursorstand `idx-k` zum
  Occurrence-Start) kann ohnehin nicht falsch positiv wirken, weil eine
  abweichende Cursorposition andere Items erzeugt hätte und der
  Item-Vergleich scheitern würde.
- **Schleifenterminierung sicher**: `skipped` wächst streng monoton (Break bei
  `headDuration <= TimeSpan.Zero` — defensiv, da planbare Items immer eine
  probierte Dauer > 0 haben), `occurrenceStart < earliestStart` (Minimum der
  Regel-Occurrences im Fenster) begrenzt die Iteration auch bei
  `wanted == int.MaxValue` (SlotDuration-Regeln). Der aufwendige `expected`-Aufbau
  läuft nur bei einem tatsächlichen Occurrence-Treffer — höchstens so oft wie
  Occurrences der Regel im Fenster existieren.
- **`scheduled` ohne `horizonEnd`-Kappung ohne Seiteneffekt**: Alle
  Downstream-Konsumenten geprüft — `GroupAirings`/`fillerSlots` (Z. 182-183;
  Füller jenseits des Horizonts werden nun ebenfalls individuell validiert —
  korrekter als das bisherige stille Behalten), `keptSlots`/`blocked` (Z. 252-254:
  jenseitige Slots blockieren Neu-Materialisierung → genau die Verhinderung der
  Duplikat-Überlappung), `DisplaceRuleFuture` (sieht jetzt komplette Airings),
  `BuildMovieAiringsFromSlotsAsync` (vollständigere Movie-Airing-Daten für den
  Trailer-Vorlauf — neutral bis besser). Praktisch ist die Menge ohnehin durch
  Horizont + Overshoot beschränkt (`EnsureCoverage` schreibt nie darüber hinaus).
- **Kein Doppel-Schreiben der laufenden Occurrence**: Die 14:00-Occurrence eines
  gehaltenen Tails wird erneut materialisiert, ihre Ranges konfliktieren mit den
  gehaltenen Tail-Slots, `Evicts` verliert (gleiche Regel → weder Priorität noch
  kleinere Id) → `conflicts.Any(!evicts)` → komplette Occurrence verworfen, Cursor
  **nicht** konsumiert (`MaterializeOccurrenceEvicting` Z. 590-597). Idempotent.
- **Bewusst unverändert**: `Evicts`-Prioritätslogik, `RewindCursor`,
  `GroupAirings`, `FillGaps` — Diff gegen den Projekt-Branch zeigt nur die zwei
  Fix-Stellen plus Kommentare; keine unbemerkt mitgeschleiften Änderungen.

### Verifiziert (Build & Tests)

- `dotnet build VideoPlayer.sln` **Debug**: 0 Fehler, 144 Warnungen —
  Kompilierung über `-p:OutDir=scratch-acc21-bin` verifiziert, weil die laufende
  Benutzer-App (PID 42440, `VideoWebPlayer\bin\Debug\net10.0\VideoWebPlayer.exe`
  **dieses Repos** — die Host-Instanz) die Standard-Ausgabe sperrt. Prozess
  selbstverständlich NICHT beendet (Self-Hosting-Regel). **Release**: regulärer
  Build, 0 Fehler, 144 Warnungen — Warnungsstand identisch, keine Warnung in den
  geänderten Dateien.
- `dotnet test VideoWebPlayer.Tests/VideoWebPlayer.Tests.csproj` vollständig,
  synchron im Vordergrund (4 m 04 s): **2151 gesamt — 2150 bestanden,
  0 Fehler, 1 übersprungen** — deckt sich exakt mit dem Implementierer-Bericht.
  Der Skip (`PairedDevice_ReportsProgressForMovieWithoutCollection_…`) ist der
  bekannte Alt-Skip, unverändert.
- `dotnet format VideoPlayer.sln --verify-no-changes --severity error`: sauber,
  Exit 0.
- **Gegenprobe 1** (Bedingung `!starts.Contains(airing[0].Start)` am Call-Site
  wiederhergestellt — Defekt a): **alle 3 neuen Tests fehlgeschlagen**, u. a.
  mit dem Live-Fehlerbild (verdrängte + mit versetztem Episoden-Ring neu
  materialisierte Airings). Danach `git checkout` → sauber.
- **Gegenprobe 2** (`s.Start < horizonEnd` wiederhergestellt — Defekt b):
  `Reconcile_WorkerLoop_NoDisplacedAccumulationNoOverlap` und
  `Reconcile_PartiallyAiredOccurrence_KeepsTailScheduled` **fehlgeschlagen**
  (verdrängt wurde genau der Horizont-Rand-Airing `26.13:00–26.14:30`, der über
  den 48-h-Horizont hinausragt). `…_TomorrowAiringsStayStable` blieb grün —
  isoliert Defekt b nicht, ist aber durch den Worker-Loop-Test abgedeckt.
  Danach `git checkout` → sauber.
- Tests laufen gegen echtes SQLite (`ProgramScheduleTestDb`: shared in-memory
  `SqliteConnection` + `UseSqlite`) — nicht EF InMemory. Schema unverändert →
  Backup-/Migrationspflichten nicht betroffen. Keine neuen NuGet-Abhängigkeiten.
- Doku aktualisiert: `docs/help/tv-sender.md` (laufender Sendungs-Rest gilt als
  gültig), `docs/RELEASE_NOTES.md` (EN, nachvollziehbar und zutreffend),
  `handover.md` (Schritt 21-Abschnitt vollständig und selbstkritisch).

## Abweichungen

- [ ] Keine fachlichen Abweichungen festgestellt.

## Hinweise

- **Semantische Aufweitung — Kopf-Lücke nicht von Vergangenheits-Tail
  unterscheidbar**: Der Rückwärtslauf akzeptiert jedes ring-konsekutive Suffix,
  dessen Kopf-Offset auf einen Occurrence-Start trifft — auch wenn die
  „fehlenden" Kopf-Slots nicht in der Vergangenheit liefen, sondern z. B. durch
  einen Fremdeingriff mitten aus einer Sequenz gelöscht wurden (das
  hintere Fragment würde als Tail durchgewunken, die Lücke füllt `FillGaps`
  mit Füller statt die ganze Regel-Zukunft zu verdrängen). Ergebnis-äquivalent:
  der alte Pfad hätte die Positionen identisch neu materialisiert — die
  Aufweitung ändert nur den Verdrängungs-Churn, nicht den resultierenden Plan.
  Hinnehmbar und im Sinne des Fixes (Stabilität vor Strenge).
- **`FindItemIndex` liefert bei doppelt vorkommendem MediaItem im Ring den ersten
  Treffer**: kommt dieselbe MediaItemId mehrfach in der Expansion vor, kann der
  Kopf-Offset falsch gerechnet werden → falsches *Negativ* (Tail als invalid →
  Verdrängung wie im Alt-Verhalten), kein falsches Positiv. Kein neues Risiko —
  die alte Prüfung nutzte denselben Lookup.
- **>24-h-Sendungen bleiben invalid** (Occurrence-Lookback 24 h, `BuildOccurrences`
  Z. 785): unverändertes Alt-Verhalten, im Handover dokumentiert. Wäre ein
  Sender mit >24-h-Einzelsendungen je Thema, gehört das in einen eigenen Schritt.
- **`EnsureCoverage`-`blocked`-Query kapt weiter bei `s.Start < horizonEnd`**
  (Z. 101-104): theoretisch könnte ein Overshoot-Tail einer frischen
  Materialisierung einen jenseitigen geplanten Slot überlappen, ohne geblockt zu
  werden — Voraussetzung ist eine Materialisierung, die weiter über den
  Horizont hinausreicht als die Occurrence selbst; sehr exotisch, unverändertes
  Alt-Verhalten, kein Befund dieses Schritts.
- **Testabdeckung Defekt b** ruht faktisch auf dem Worker-Loop-Test (der andere
  grüne Rest-Assert unter Gegenprobe 2 s. o.): ausreichend, weil dort der
  reale Worker-Takt (Reconcile+EnsureCoverage ×3) mit Überlappungs- und
  Lückenlosigkeits-Assert geprüft wird.
- **`steps.md` markiert Schritt 21 als „In Arbeit"** — prozessual korrekt
  (Abnahme lief noch), Status-Umstellung auf „Fertig" erfolgt
  erfahrungsgemäß beim Merge; kosmetische Fußnote wie schon bei Schritt 20.
- **Self-Hosting-Bedingung**: Debug-Kompilierung/-Tests liefen über
  `-p:OutDir=scratch-acc21-bin` in projektbezogene Scratch-Verzeichnisse;
  sämtliche `scratch-acc21-bin`-Ordner nach der Prüfung entfernt,
  `git status` sauber.
- **Handover-Selbstauskunft verifiziert**: „2150 grün + 1 Alt-Skip" → hier exakt
  reproduziert; „Mutationscheck" → selbst in beiden Varianten nachvollzogen;
  „144 Warnungen unverändert" → bestätigt; „format clean" → bestätigt.
