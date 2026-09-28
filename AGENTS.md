# Zusammenarbeit mehrerer KI-Agenten in diesem Repository

Diese Datei gilt für jeden Agenten, der hier arbeitet (Claude Code, Devin, Codex, ...). Sie regelt, wie
mehrere Agenten sich abwechseln oder parallel arbeiten, ohne einander in die Quere zu kommen. Sie ersetzt
nicht die fachlichen Prozessbeschreibungen (`lifecycle`, `project`), sondern ergänzt sie um die
Zusammenarbeit. Der Anwender entscheidet, wer was macht; Agenten können einander nicht direkt erreichen,
alle Absprachen laufen über die Dateien im Repository oder über den Anwender.

## 1. Der Zustand steht im Repository

Nichts, was ein anderer Agent zum Weiterarbeiten braucht, darf nur im Kontext eines Agenten existieren.
Maßgeblich sind: `steps.md` (Status je Schritt), `handover.md` (Bearbeiter und Übergabestand), die
Abnahmeberichte `acceptance-*.md` und die Git-Historie. Ein Agent, der einsteigt, liest zuerst `handover.md`
des Projekts (`docs/projects/{projekt}/handover.md`), dann `git log` des Branches.

## 2. Ein Bearbeiter je Schritt

- Vor Beginn eines Schritts trägt der Agent sich in der Tabelle „Bearbeiter“ in `handover.md` ein und
  committet das auf dem Projekt-Branch. Steht dort schon ein anderer Agent mit Status „aktiv“, wird der
  Schritt nicht angefasst, sondern beim Anwender nachgefragt.
- Ein Schritt hat zu jedem Zeitpunkt genau einen Bearbeiter. Wechselt der Bearbeiter, geschieht das über
  eine Übergabe (Abschnitt 5), nicht durch stilles Weiterarbeiten.
- `steps.md` bleibt unverändert im bisherigen Format. Das Statusskript liest dessen erste Tabelle und
  erwartet in „Status“ genau die Werte `Offen`, `In Arbeit`, `Fertig`, `Blockiert`. Bearbeiter gehören
  deshalb nicht dorthin.

## 3. Kein gemeinsames Arbeitsverzeichnis

- Jeder Agent arbeitet in seinem eigenen Arbeitsverzeichnis (eigener Worktree oder eigener Klon), nie im
  selben Verzeichnis wie ein anderer Agent.
- Jeder Schritt hat seinen eigenen Branch `{projekt-branch}-schritt-{n}-{slug}` (mit `-` statt `/`, weil
  der Projekt-Branch selbst schon `/` enthält). Auf dem Branch eines anderen Agenten wird nicht committet,
  solange dessen Schritt „aktiv“ ist. Historie fremder Commits wird nie umgeschrieben.
- EF-Migrationen kollidieren im Model-Snapshot (`ApplicationDbContextModelSnapshot.cs`). Schritte mit
  Migration werden deshalb nacheinander gemergt; wer später mergt, erzeugt seine Migration nach dem Rebase
  neu, statt Snapshot-Konflikte von Hand zu lösen.

## 4. Wer implementiert, prüft nicht

- Die Abnahme eines Schritts macht ein anderer Agent als der, der ihn umgesetzt hat (oder, wenn nur einer
  verfügbar ist, ein frischer Lauf ohne Kontext des Implementierers). Der Implementierer schreibt keinen
  eigenen Abnahmebericht.
- Der Prüfer verlässt sich nicht auf den Selbstbericht, sondern prüft den tatsächlichen Code, führt die
  Tests selbst aus und baut Angriffs- bzw. Gegenbeispiele selbst nach (nicht nur die vorhandenen Tests
  laufen lassen). Bei Tests, die etwas absichern sollen: kurz die abgesicherte Stelle kaputtmachen und
  prüfen, dass der Test wirklich fehlschlägt.
- Bericht: `acceptance-schritt-{n}.md` im Projektordner mit `## Ergebnis` (`**Status:** Erfüllt` oder
  `Abweichungen gefunden`), `## Abweichungen` (Checkboxen) und `## Hinweise`. Ältere Runden werden als
  `acceptance-schritt-{n}.{k}.md` archiviert, nicht überschrieben. Nach höchstens 3 Nachbesserungsrunden
  wird `blocked.md` geschrieben und der Anwender entscheidet.
- Behebt jemand eine Abweichung direkt selbst, ohne dass ein Prüfer sie erneut ansieht, steht das
  ausdrücklich im Abnahmebericht, samt Beleg (z. B. Test, der ohne die Behebung fehlschlägt).

## 5. Übergabe bei Pause, Limit oder Wechsel

Vor jeder absehbaren Unterbrechung (und spätestens, wenn ein Limit droht) tut der Agent Folgendes:

1. Arbeitsstand committen, auch unfertig (Präfix `step:` in der Commit-Nachricht), damit nichts nur im
   Arbeitsverzeichnis liegt.
2. Den Abschnitt seines Schritts in `handover.md` aktualisieren (Vorlage: `docs/projects/HANDOVER_VORLAGE.md`):
   erledigt, offen, getroffene Entscheidungen, Änderungen außerhalb des Auftrags, wie die Tests laufen.
3. Status in der Bearbeiter-Tabelle auf „pausiert“ setzen.

Der übernehmende Agent setzt den Status auf „aktiv“ mit seinem Namen und beginnt mit der Prüfung, was
tatsächlich im Code steht (nicht nur, was die Übergabe behauptet).

## 6. Commit-Autoren

Alle Agenten committen aktuell unter „Softwareschmiede Bot“, nur die `Co-Authored-By`-Zeile verrät den
Urheber. Vereinbart wird: Der Name des Autors nennt den Agenten, z. B. `Claude (Softwareschmiede Bot)` oder
`Devin (Softwareschmiede Bot)`, die E-Mail-Adresse bleibt die des Bots. Gesetzt wird das je Aufruf über
die Umgebungsvariablen `GIT_AUTHOR_NAME` und `GIT_COMMITTER_NAME`, nicht über die Git-Konfiguration. Die
`Co-Authored-By`-Zeile bleibt zusätzlich bestehen. Commit-Nachrichten sind deutsch.

## 7. Definition of Done je Schritt

Ein Schritt ist erst „Fertig“, wenn alles gilt:

- `dotnet build VideoPlayer.sln` in Debug UND Release fehlerfrei (Release wird in der CI gebaut und
  verhält sich anders: z. B. brach NuGet `SixLabors.ImageSharp` 4.x dort den Build).
- Volle Testsuite `dotnet test VideoWebPlayer.Tests/VideoWebPlayer.Tests.csproj` grün. Einzelne
  Playwright-Tests flackern gelegentlich unter Last. Ein einmaliger Ausfall wird einzeln (mehrfach)
  wiederholt und im Bericht ehrlich genannt. Kein Test wird deshalb deaktiviert oder abgeschwächt.
- Neue Tabellen und Spalten stehen in `VideoWebPlayerBackupData` (`OptionalRestoreTables` bzw.
  `OptionalRestoreColumns`) samt Regressionstest, der ein Alt-Backup ohne sie wiederherstellt. Dazu eine
  EF-Migration.
- Tests zu Unique-Indizes, Fremdschlüsseln und Löschverhalten laufen gegen echtes SQLite, nicht gegen EF
  InMemory (der erzwingt beides nicht).
- Berechtigungen werden serverseitig erzwungen. Eine ausgeblendete Schaltfläche ist keine Absicherung.
- Hilfe-Dokumentation unter `docs/help/`, `README.md` und Release Notes sind bei Bedarf aktualisiert.
- Keine neuen NuGet-Hauptversionen ohne Prüfung von Lizenz, Release-Build und CI-Skripten.
- Änderungen außerhalb des Auftrags (z. B. ein Fehler, der unterwegs auffällt) stehen in eigenen Commits
  und werden im Handover und im Bericht genannt, damit der Prüfer sie gezielt ansehen kann.

## 8. Dateien anderer

Untracked Dateien, die der Agent nicht selbst angelegt hat (z. B. lokale Notizen oder Rückmeldungen des
Anwenders unter `docs/features/task/`), werden weder gelöscht noch verändert oder committet. Aufräumen
betrifft nur, was der Agent selbst erzeugt hat.

## 9. Prozessbeschreibungen

Die Ablaufbeschreibung der Skills `lifecycle` und `project` existiert derzeit an zwei Stellen, beide nicht
von Git verfolgt: lokal unter `.agents/skills/lifecycle/` (für Codex/Devin; der Ordner steht in der
`.gitignore`) und im Benutzerverzeichnis von Claude Code (`~/.claude/commands/`). Beide sollen inhaltlich
gleich sein, sonst arbeiten die Agenten nach unterschiedlichen Regeln. Ein Agent, der eine Abweichung
bemerkt, gleicht nicht eigenmächtig an, sondern weist den Anwender darauf hin.
