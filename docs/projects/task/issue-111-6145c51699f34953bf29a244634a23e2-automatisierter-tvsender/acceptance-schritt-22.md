# Abnahme Schritt 22 — Regel-Fenstergrenze (N16-Rest) + Bereinigen-Button (N18)

Prüfer: Devin (unabhängige Abnahme, nicht der Implementierer)
Geprüfter Stand: `90e060b` (`step: Schritt 22 Lifecycle-Artefakte archiviert`),
Arbeitsbaum nach den Gegenproben wieder sauber (`git status` zeigt nur das
vorbestehende untracked `VideoWebPlayer/Data/LiveStream/` — produktive
Live-Ausgabe des Anwenders, per AGENTS §8 unangetastet).

## Ergebnis

**Status:** Abweichungen gefunden

Der Kern der Anforderung ist korrekt und gut abgesichert umgesetzt:
Fenstergrenzen (`SelectAiringItems` + `BoundStarts`/`BoundaryAfter`),
Invasor-/Kompetitor-Trennung mit suffix-genauer Verdrängung
(`DisplaceAiringSuffix`), `AiringReproducible` mit derselben Grenze sowie die
Admin-Endpunkte `DeleteDisplacedSlotsAsync`/`ResetScheduleAsync` samt
Bestätigungsdialogen im Sendeplan-Tab sind im Code verifiziert und durch 7 neue
Tests abgedeckt, deren Wirksamkeit ich per Gegenprobe selbst bewiesen habe.
Dabei ist mir jedoch ein **reproduzierbarer Laufzeit-Crash** in der neuen
`consumed`-Buchhaltung von `AiringReproducible` aufgefallen (s. Abweichungen):
`writtenOffset = k - consumed` kann negativ werden und wird ungeprüft als
Listenindex benutzt → `ArgumentOutOfRangeException` mitten in `ReconcileAsync`.

### Verifiziert (Code-Review)

- **`SelectAiringItems`** (`ProgramScheduleService.cs:910-944`): übernimmt exakt
  die alte Auswahlsemantik (EpisodeCount > SlotDuration > 1 Item) und begrenzt
  sie zusätzlich auf `softEndUtc`: ein Item, das die Grenze kreuzen würde, wird
  nicht platziert — der Airing endet früher (Items sind unteilbar). Kopf-Items,
  die gar nicht ins Fenster passen, werden als `consumed` gezählt (Rotations-
  Stillstand-Schutz). Ohne Folge-Occurrence (`softEndUtc == null`) gilt wie
  zuvor nur die nominale Länge — „freie Zeit" ist per Definition unbeschränkt;
  kein unbegrenztes Überziehen in fremde Fenster.
- **`BoundStarts`/`BoundaryAfter`** (`ProgramScheduleService.cs:949-967`):
  Grenzen sind die Starts aller Occurrences item-tragender, nicht-Füller-Regeln
  — Regeln ohne Material bounden nicht (ihr Fenster ist faktisch frei).
  `p <= priority` boundet nur an gleich-/niedriger-priorisierte Fenster:
  streng-höher-priorisierte Occurrences materialisieren wegen der
  `OrderByDescending(Priority)`-Reihenfolge ohnehin zuerst und ihre belegten
  Ranges entscheiden den Konflikt — fachlich konsistent, der Nachfolger ist in
  **beiden** Prioritätslagen geschützt (höherer Prio-Invasor wird getrimmt,
  niedrigerer Prio-Invasor scheitert atomar an den bereits belegten Slots).
- **`MaterializeOccurrence`/`…Evicting`**: identische Auswahl über den Helper;
  Konflikt-Split sauber — `competitors = conflicts.Where(k => k.Start >=
  occurrence.StartUtc)` gehen durch `evicts()`, Invasoren (`Start < StartUtc`)
  weichen immer (`ProgramScheduleService.cs:642-653`).
- **`DisplaceAiringSuffix`** (`ProgramScheduleService.cs:300-314`): verdrängt
  genau die Slots mit `End > boundaryStartUtc` — der Kopf der Airing bleibt
  `Scheduled`, nur der invadierende Rest fällt; `RewindCursor` setzt die Regel
  auf den frühesten verdrängten Inhalt zurück (kein Episodensprung). Keine
  ganze Regel-Zukunft mehr — `DisplaceRuleFuture` greift nur noch für echte
  Kopf-an-Kopf-Kompetitoren. Ungrouped Losers (Füller, regellose Slots) werden
  einzeln verdrängt.
- **`AiringReproducible`** (`ProgramScheduleService.cs:498-554`): nutzt denselben
  Helper mit derselben Grenze — ein boundary-getrimmter Airing reproduziert sich
  selbst und bleibt stabil (Stabilität über 3 Worker-Takte testbewiesen). Der
  `writtenOffset`-Abbildung „k Vorgänger = consumed übersprungene + ungeschriebene
  Vergangenheits-Items" ist inhaltlich konsistent — **aber** siehe Abweichung 1.
- **N18-Endpunkte** (`ProgramScheduleService.cs:428-463`): beide rufen
  `RequireAdminAsync` **serverseitig** (nicht nur Button versteckt); Non-Admin →
  `ForbiddenAccessException` (testbewiesen). `DeleteDisplacedSlotsAsync` löscht
  kanal-begrenzt nur `Status == Displaced` via `ExecuteDelete`, liefert Anzahl.
  `ResetScheduleAsync` löscht alle Slots des Kanals **und** die Cursor nur der
  kanal-eigenen Regeln (`ruleIds`-Scope korrekt). UI: beide Buttons im
  Sendeplan-Tab mit Overlay-Bestätigungsdialog (Abbrechen via Overlay-Klick +
  `stopPropagation`), Ergebnis-Anzeige, `ReloadScheduleAsync`; Reset stößt via
  `ScheduleSignal.NotifyScheduleChanged()` den Neuaufbau an — Worker läuft
  Reconcile+EnsureCoverage pro Intervall/Signal (`ProgramScheduleWorker.cs:69-70`),
  die vorbestehenden Alt-Überstände heilt also der Reconcile-Pfad.
- **Doku**: `docs/help/tv-sender.md` (Fenstergrenzen + Aufräumen-Abschnitt,
  fachlich zutreffend), `docs/RELEASE_NOTES.md` (verständlich, korrekt).

### Verifiziert (Build & Tests)

- `dotnet build VideoPlayer.sln` **Debug** und **Release**: je 0 Fehler,
  144 Warnungen — Warnungsstand identisch, **keine Warnung in den geänderten
  Dateien**. Builds liefen über `--artifacts-path`-Scratch-Verzeichnisse, weil
  die laufende Benutzer-App (PID 50164,
  `VideoWebPlayer\bin\Debug\net10.0\VideoWebPlayer.exe` dieses Repos) die
  Standard-Ausgabe sperrt — Prozess selbstverständlich NICHT beendet
  (Self-Hosting-Regel). Scratch-Ordner anschließend entfernt.
- `dotnet test VideoWebPlayer.Tests/VideoWebPlayer.Tests.csproj` vollständig,
  synchron (5 m 15 s): **2158 gesamt — 2157 bestanden, 0 Fehler,
  1 übersprungen** — exakt die Implementierer-Angabe; der Skip ist der bekannte
  Alt-Skip (`PairedDevice_ReportsProgressForMovieWithoutCollection_…`).
- `dotnet format VideoPlayer.sln --verify-no-changes --severity error`: sauber,
  Exit 0.
- **Gegenprobe** (`BoundaryAfter` → `return null`, d. h. altes unbegrenztes
  Überziehen): **3 der 7 Boundary-Tests fehlgeschlagen** —
  `EnsureCoverage_AdjacentWindows_OvershootDoesNotKillSuccessor` (die exakte
  N16-Rest-Reproduktion: Nachfolger-Fenster wieder leer),
  `Reconcile_BoundaryTrimmedAiring_StaysStableAcrossRuns` (Oszillation kehrt
  zurück: getrimmter Tail wird erneut verdrängt) und
  `EnsureCoverage_OversizedHeadItem_IsConsumedNotStalling`. Die Tests sichern
  die Logik tatsächlich ab. Danach `git checkout` → sauber.
- **Eigene Gegenprobe zum Abweichungs-Fund** (temporärer Repro-Test, inzwischen
  entfernt): Crash unten unter Abweichungen dokumentiert und live bewiesen.
- Tests laufen gegen echtes SQLite (`ProgramScheduleTestDb`, shared in-memory
  `SqliteConnection`). Schema unverändert → keine Migrations-/Backup-Pflichten.
  Keine neuen NuGet-Abhängigkeiten.

## Abweichungen

- [ ] **Crash in `AiringReproducible` bei `writtenOffset < 0`**
  (`ProgramScheduleService.cs:532-542`): `writtenOffset = k - consumed` ist
  negativ, sobald `SelectAiringItems` mehr oversized Kopf-Items konsumiert als
  der Rückwärtslauf Vorgänger gezählt hat (z. B. k = 0 und das Kopf-Item der
  Airing passt selbst nicht in das aktuell geltende Fenster — erreichbar, wenn
  eine neue/engere Folge-Occurrence den Bound verschärft). Der Count-Check
  `expected.Count - writtenOffset != airing.Count` fängt das **nicht** ab (bei
  negativem Offset kann die Gleichung durchaus erfüllt sein), danach wirft
  `expected[writtenOffset + i]` eine `ArgumentOutOfRangeException` — bewiesen
  per temporärem Repro-Test: Regel R täglich 16:00 mit einem 90-min-Item,
  boundende Regel X täglich 17:00 (gleiche Priorität), geseedeter 2-Slot-Airing
  ab exakt 16:00 → `ReconcileAsync` wirft aus `AiringReproducible`
  (Zeile 542, Aufruf Z. 211). Folge: Der Worker fängt die Exception zwar
  (`ProgramScheduleWorker.cs:76-79`), aber Reconcile **und** das anschließende
  EnsureCoverage entfallen für den ganzen Takt; der Zustand persistiert (die
  Airing wird nie verdrängt), der Abbruch wiederholt sich jedes Intervall —
  die Selbstheilung des Plans ist damit faktisch lahmgelegt, solange ein
  solcher Datensatz existiert. Mindest-Fix: `writtenOffset < 0` zusätzlich in
  die `continue`-Bedingung aufnehmen.

## Hinweise

- **`Commit(consumed)` vor der Konfliktprüfung**: In beiden
  Materialisierungs-Pfaden (`ProgramScheduleService.cs:855-857` bzw. `620-622`)
  konsumieren oversized Kopf-Items den Cursor, auch wenn der Airing danach
  komplett am Konflikt scheitert — die Items werden ohne Ausstrahlung
  übersprungen. Zusätzlich ist der Skip permanent (Cursor persistiert), obwohl
  ein späteres, weiteres Fenster das Item fassen könnte — die
  „kann nie in dieses Fenster passen"-Begründung gilt nur für **diesen** Bound.
  Dokumentierte Designentscheidung, fachlich diskutabel, kein Blocker.
- **`consumed <= count` erlaubt `count + 1` Konsume**: Sind **alle** Items für
  das Fenster oversized, wickelt der Ring einmal zu viel — das erste
  oversized Item wird doppelt gezählt, `NextIndex` springt um ein Item zu weit.
  Kleine Rotations-Ungenauigkeit, kein korrekter Slot-Verlust.
- **`RequireAdminAsync`-Fehlertext** ist Copy-Paste („…die Planungsdiagnose
  abrufen") und passt nicht zu den neuen Lösch-Endpunkten — kosmetisch.
- **Extend-Pfad-Asymmetrie (bewusst)**: Ein niedriger-priorisierter Airing, der
  in ein streng-höher-priorisiertes Fenster überziehen würde, wird nicht
  getrimmt, sondern scheitert atomar (die Occurrence des höher-priorisierten
  Fensters materialisiert zuerst). Im Code dokumentiert und identisch zum
  Vor-Fix-Verhalten — der gemeldete Defekt (blockierter Nachfolger) ist in
  beide Prioritätsrichtungen behoben; kein Rückschritt.
- **Race Worker vs. Bereinigen/Zurücksetzen**: Die neuen Endpunkte laufen ohne
  `IBackgroundProcessingGate`; ein parallel materialisierender Worker-Lauf kann
  direkt nach dem Löschen neue Slots schreiben. Für „Plan zurücksetzen" ist das
  ohnehin die gewünschte Neuplanung — harmlos, der Vollständigkeit halber
  vermerkt.
- **`handover.md` hat keinen Schritt-22-Abschnitt und keine Bearbeiter-Zeile**
  (AGENTS §2/§5); `steps.md` steht korrekt auf „In Arbeit" (Abnahme lief).
  Der Code-Stand ließ sich trotzdem vollständig prüfen — prozessuale Lücke,
  die beim Schritt-Abschluss nachgepflegt werden sollte.
- **Self-Hosting-Bedingung**: Debug-Build und Tests liefen über
  `--artifacts-path`-Scratch-Verzeichnisse (laufende Benutzer-App sperrt
  `bin\Debug`); sämtliche Scratch-Ordner und der temporäre Repro-Test wurden
  entfernt, `git status` sauber bis auf das vorbestehende untracked
  `VideoWebPlayer/Data/LiveStream/`.
