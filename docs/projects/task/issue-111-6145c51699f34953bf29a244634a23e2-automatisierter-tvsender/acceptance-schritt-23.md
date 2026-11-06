# Abnahme Schritt 23 — Regel-Features (N19–N21)

Prüfer: Devin (unabhängig, ohne Implementierungskontext)
Geprüfter Stand: `f41bb6d` auf `task-issue-111-6145c51699f34953bf29a244634a23e2-automatisierter-tvsender-schritt-23-regel-features`
Gegenstand: `nachbesserungen.md` §N19 (Serien-Interleaving), §N20 (Werktag/Wochenende), §N21 (Regel-Editor als Overlay)

## Ergebnis

**Status:** Abweichungen gefunden

## Verifikation

- **Diff-Review** `task/...-automatisisierter-tvsender...HEAD`: 21 Dateien, Kern in
  `VideoWebPlayer/Services/ProgramScheduleService.cs` (+463), dazu Entity `ProgramRuleCursor.StrandKey`,
  Konfiguration (Composite-Unique `(ProgramRuleId, StrandKey)`), manuelle Migration
  `20261022190000_AddProgramRuleCursorStrandKey`, Snapshot, BackupData (`OptionalRestoreColumns`),
  `ProgramRuleRecurrence` (+Weekday/Weekend), UI (`TVSenderAdmin.razor`, `app.css`), Hilfe/Release-Notes.
- **Build**: Debug + Release jeweils 0 Fehler, identisch 144 Warnungen — alle pre-existing
  (BL0008/CS86xx in unverwandten Dateien), keine neue Warnung in geänderten Dateien.
  `bin/Debug` war durch die laufende Benutzer-App (`VideoWebPlayer.exe` PID 58256) und Visual
  Studio (PID 37576) gesperrt (MSB3027) — Prozess **nicht** beendet, stattdessen
  `-p:OutDir=<scratch>` verifiziert (wie in Schritt 21/22).
- **Tests**: `dotnet test VideoWebPlayer.Tests` synchron, 2167 gesamt — 2164 grün, 1 Alt-Skip,
  2 E2E-Flakes (`PlaylistMediaSearchE2ETests.AddMedia_Search_ReturnsAll5Types`,
  `PlaylistPlaybackE2ETests.PlaylistSkipLockedEntriesE2ETest`, beide Playwright, fachfremd) —
  im Einzel-Retry beide grün ⇒ effektiv 2166 grün + 1 Skip, deckt sich mit dem
  Implementierer-Bericht (2165+1 Flake+1 Skip).
  Hinweis: Testlauf musste aus einem OutDir **innerhalb** des Repos laufen
  (`.scratch-test-out`), weil diverse Tests `VideoPlayer.sln` aufwärts von
  `AppContext.BaseDirectory` suchen — OutDir außerhalb erzeugt falsche Fehler.
  Scratch-Verzeichnis nach dem Lauf gelöscht.
- **Gegenprobe**: `for (var s = 0; …)` → `s = 1` in `SelectAiringItemsInterleaved`
  (Occurrence startet bei Strang 1 statt 0) ⇒ **alle 4 Interleaving-Tests schlagen fehl**
  (Positions-, Cursor-, Fortsetzungs- und Reconcile-Test). Tests sichern das Verhalten
  tatsächlich ab. Danach `git checkout --` — Arbeitsbaum wieder sauber.
- **Migration (manuell erzeugt)**: `Up`/`Down` konsistent — alter Unique-Index
  `IX_ProgramRuleCursors_ProgramRuleId` wird gedroppt, `StrandKey` TEXT nullable hinzugefügt,
  neuer Unique `IX_ProgramRuleCursors_ProgramRuleId_StrandKey`. Snapshot- und
  Designer-Target-Model stimmen mit Entity + Konfiguration überein; Zeitstempel ordnet die
  Migration korrekt hinter `20261022074936_AddMovieMediaItemIsTrailer` ein.
- **N20**: `BuildOccurrences` Weekend = `DayOfWeek.Saturday or Sunday`, Weekday = Negation —
  disjunkt, keine Doppel-Occurrences möglich; lokale Tageslogik (`day.DayOfWeek` vor
  UTC-Konvertierung) korrekt. UI-Select + `RecurrenceLabel` + Validierung
  (`Enum.IsDefined`, `DayOfWeek`/`SingleDate` bleiben Weekly/Once vorbehalten) vollständig.
- **N21**: Editor als `.admin-dialog-overlay` + `.admin-dialog.rule-editor-dialog`,
  `@onclick="CancelEdit"` auf dem Overlay + `@onclick:stopPropagation` innen — exakt das
  App-Muster (GenreAdmin, ConfirmationDialog, PlaylistForm). Kein gesondertes
  Focus-Management — gibt es auch in den bestehenden Dialogen nicht („wie üblich" erfüllt).
  bUnit-Test prüft Overlay-Rendern + Background-Klick-Abbruch; E2E-Assert umgestellt.
- **N19 Fachprüfung**: Strang-Aufbau in `RuleState.SetItems` nur für
  `MaterialType == Genre && RuleType != Filler`; Strang-Reihenfolge deterministisch
  (Item-Reihenfolge: Filme-`other` zuerst, dann Serien alphabetisch nach `TVShow.Name`/
  `TVShowId`/`Season.Id`/`Number`). Occurrence startet stets bei Strang 0 ⇒ stabile
  Pick-Position je Serie über Tage; Strang-Cursor persistiert `LastPlaced` je StrandKey
  ⇒ `NextIndexFor` setzt je Strang fortlaufend fort (kein Zurückspringen; getestet bis
  zum Ring-Wrap). `RewindCursor`/`PersistCursors`/`FindCursor` arbeiten je StrangKey,
  `null` = bisheriger Gesamt-Cursor ⇒ Alt-Zeilen bleiben wirksam (flache Regeln finden
  ihre NULL-Zeile wieder). `AiringReproducibleInterleaved` (Präfix-Hypothesen + Simulation,
  konservativ) und Reconcile-Pfad decken den Multi-Strang-Fall ab; Füller bleiben bewusst
  flach. Neue Tests real: 4 Interleaving-, 2 Generierungs-, 2 Validierungs-, 1
  Legacy-Backup-Test (StrandKey-Spalte im Archiv entfernt → NULL nach Restore).

## Abweichungen

- [ ] **Strang-Verhungern bei `EpisodeCount` < Strang-Anzahl.** Jede Occurrence startet das
  Round-Robin bei Strang 0 (`SelectAiringItemsInterleaved`, ProgramScheduleService.cs:1172)
  und `EpisodeCount` zählt die Gesamtzahl der Picks: Ist `wanted < m` (z. B. Genre-Regel mit
  `EpisodeCount = 1` und drei Serien — eine legale Konfiguration, Validierung verlangt nur
  „Länge oder Anzahl > 0"), wird **ausschließlich Strang 0** (der `other`-Film-Strang bzw.
  die alphabetisch erste Serie) je Occurrence ausgestrahlt; die übrigen Stränge werden nie
  erreicht und verhungern dauerhaft — inkl. ihrer Cursor (nie LastPlaced → nie persistiert).
  Das ist eine Regression gegenüber dem alten flachen Ring, der das gesamte Material
  rotierte, und ein Silent-Failure (keine Warnung/Diagnose). Denkbare Gegenentwürfe:
  Startstrang je Occurrence rotieren (kostet Positionsstabilität für `wanted < m`) oder
  Mindestumfang dokumentieren/validieren (Strang-Anzahl ist datenabhängig — zur
  Validierungszeit schwer prüfbar). Mit `SlotDuration` statt `EpisodeCount` tritt das
  Problem nicht auf (alle Stränge kommen zum Zug). Entscheidung beim Anwender.

## Hinweise

- **Unique-Abschwächung unter SQLite**: Der neue Composite-Index lässt mehrere
  `NULL`-StrandKey-Zeilen je Regel zu (NULL-Werte gelten als distinct). Der alte Index
  garantierte genau eine Cursor-Zeile je Regel. Im Code wird nur eine NULL-Zeile geschrieben
  (`PersistCursors` matcht per `StrandKey == null`); die Einschränkung ist im
  Konfigurations-Kommentar dokumentiert — bewusster Trade-off, kein Fehler.
- **Alt-Cursor-Reset bei Multi-Strang-Genre-Regeln**: Bestehende Genre-Regeln mit mehreren
  Serien besitzen eine NULL-Zeile, die keinem Strang zugeordnet werden kann — alle Stränge
  starten bei Folge 1 / erstem Film, die NULL-Zeile bleibt als toter Datensatz liegen.
  Für die neue Interleaving-Semantik ist ein sauberer Neustart die sinnvollste
  Interpretation; verhaltenswirksam (Fortschritt beginnt von vorn), erwähnenswert.
- **Verwaiste Strang-Zeilen werden nie aufgeräumt**: Schrumpft ein Genre auf einen Strang
  oder wechselt die Materialart, bleiben `show:*`-/`other`-Zeilen dauerhaft in
  `ProgramRuleCursors` (werden ignoriert). Analog bleibt eine NULL-Zeile nach Umstellung
  auf Multi-Strang ungenutzt. Rein kosmetisch.
- **„Stabile Position" ≠ fixe Uhrzeit**: Die Position einer Serie in der Pick-Reihenfolge
  ist tagesstabil; ihre Uhrzeit verschiebt sich, wenn vorgelagerte Stränge unterschiedlich
  lange/unterschiedlich viele Beiträge liefern (z. B. variierende Episodenlängen oder ein
  `done`-Strang komprimiert die Folgepositionen). Entspricht dem N19-Positiv-Beispiel
  (Positions-Stabilität), ist aber bei ungleichmäßigen Längen keine fixe Uhrzeit.
- **`s--`-Retry** in `SelectAiringItemsInterleaved` (ProgramScheduleService.cs:1188):
  consumed-Head-Picks rücken die Pick-Position relativ zum Strang-Index — die
  `p % m`-Hypothesen in `AiringReproducibleInterleaved` bilden das nur näherungsweise ab;
  unklare Fälle werden konservativ verworfen (Displacement + Neu-Materialisierung statt
  stale Airing). Absicht laut Kommentar.
- **`RewindCursor`-Semantik unverändert absolut**: `strand.NextIndex > idx` vergleicht
  absoluten Fortschritt mit Ringindex — nach Ring-Wrap (NextIndex ≥ Items.Count) immer
  wahr. Bereits im alten Flach-Code so; kein neues Verhalten.
- **Untracked `VideoWebPlayer/Data/LiveStream/`** (live.m3u8 + .ts-Segmente): Laufzeitdaten
  der laufenden Benutzer-App, nicht vom Implementierer — unangetastet (AGENTS §8).
- Handover/steps.md/Release-Notes/`docs/help/tv-sender.md` gepflegt und konsistent;
  Commit-Historie sauber (plan → step → feat), keine Fremd-Dateien committet.

## Nachtrag — Abweichung behoben

Der Pruefbefund "EpisodeCount < Strang-Anzahl verhungert übrige Serien" wurde direkt behoben:
`SelectAiringItemsInterleaved` rotiert den Start-Strang je Occurrence-Tag, aber nur wenn
`wanted < m` — SlotDuration-Airings behalten die tagesstabile Position
(Beleg: Interleaving-Tests 78/78 gruen, Commit fix: Interleaving).
