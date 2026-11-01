# Anforderung — Issue #320: Interleaved-Prüfung invalidiert Ausstrahlungen mit Strang-Deferral oder Später-Take (konservativ)

Folgepunkt aus #315 (vertieft in `docs/features/task/issue-315-*/continue.md`, Punkt 2).
GitHub-Issue: martin-stromberg/VideoPlayer#320.

## Befunde (aus dem Issue, im Code verifiziert)

**Befund 1 — Cursor-Bias:** `headBase[s]` in `InterleavedAiringTailReproducible`
(`ProgramScheduleService.cs`) nutzte für Stränge ohne geschriebenen Beitrag den
persistierten Cursor (`PersistCursors` schreibt `LastPlaced` = letzter Take des
*gesamten* Plans; `NextIndexFor` mappt ihn auf die Ringposition dahinter). Ein Strang mit
ungeschriebenen Prefix-Picks in der Coverage-überspannenden Occurrence *und* Takes in
einer späteren Occurrence derselben Regel (bei täglichen Genre-Regeln der Normalfall)
bekam eine gegen Plan-Ende verzerrte Basis → Hypothesen auf falschen Ringpositionen →
`HeadPickInconsistent` → unnötige Verdrängung (konservativ).

**Befund 2 — Deferral-Lücke:** Die Prefix-Abbildung `(startOffset + q) % m` setzte dichte
Rotation voraus. `SelectAiringItemsInterleaved` lässt geordnete Stränge mid-Occurrence
deferieren (blockierter Kopf → `done[s]` für den Rest der Occurrence; Bound-Verbot
ebenso) — eine reale Prefix-Sequenz mit Deferral war für *keine* Hypothese darstellbar →
garantierte Fehlinvalidierung.

Beide Lücken wirken nur konservativ: Verdrängungs-Churn statt falscher Stabilität.
Nebenwirkung: verdrängte Duplikate sammeln sich bis zur manuellen Bereinigung
(`tvsender-cleanup-displaced`).

## Vorgehen (aus dem Issue übernommen)

1. Integrationstest, der die Erreichbarkeit beweist (Genre-Regel, Coverage beginnt
   mid-Occurrence, Strang mit geblocktem Kopf bzw. Take in späterer nicht-kontigüer
   Gruppe).
2. Bei Reproduktion zuerst der **provable-deferral-Arm** (Deferral nur bei beweisbar zum
   Pick-Zeitpunkt blockiertem Strang-Kopf — dieselbe `IsAllowedAt`-/Bound-Prüfung wie im
   Auswahl-Scan), danach die **occurrence-lokale Basis** für den Cursor-Bias (Begrenzung
   durch den ersten geschriebenen Pick des Strangs späterer Gruppen).

## Umsetzung

- **Reproduktionstests** `ProgramScheduleServiceTests_Issue320`:
  - `..._DeferredStrandInPrefix_...`: FSK-18-Strang deferiert am 20:00-Occurrence-Tag
    (Default-Fenster FSK18 ab 23:00), Coverage beginnt 21:00 mid-Occurrence → schlug vor
    dem Fix fehl (Verdrängung + Duplikate).
  - `..._UnwrittenPrefixTakeWithLaterGroupTake_...`: Strang nur mit ungeschriebenem
    Prefix-Take (Ende ≤ Coverage) + Takes in späteren Gruppen, abweichende
    Episodenlaufzeiten machen die verzerrte Basis fatal → schlug vor dem Fix fehl.
- **Fix** in `InterleavedAiringTailReproducible`:
  - Hypothesenparameter vom dichten Pick-Zähler `p` zur **Turn-Position** `j0 = r0·m +
    off(s0)` geändert; je Strang zählt `takeCounts[s]` die genommenen Prefix-Turns, der
    Rest ist Nothing-Run mit genau einem Deferral-Turn.
  - `ProvableDeferral`: Nothing-Turn nur, wenn der am Kopf anliegende Kandidat zum
    rekonstruierten Turn-Zeitpunkt blockiert war (geordnet: `!IsAllowedAt` oder Bound;
    ungeordnet: erlaubt **und** Bound — geblockte Kandidaten werden geskippt, nicht
    deferiert); die Deferral-Skips werden zum exakten Turn-Zeitpunkt konsumiert.
  - **Occurrence-lokale Basis:** Stränge ohne schriftlichen Beitrag bekommen als
    `headBase` die Ringposition ihres ersten geschriebenen Picks in späteren Gruppen
    (`ReproContext.LaterGroupSlots`, in `ReconcileWindow.Validate` befüllt); der Skip-
    Rücklauf wertet dazwischenliegende Positionen zur Startzeit dieses Picks. Ohne
    späteren Pick bleibt der persistierte Cursor Basis (konservativ).
  - Nebenbefund (separater Commit): `SelectAiringItemsInterleaved` terminierte bei
    `SlotDuration`-Regeln ohne Clip-Gruppen auf der letzten (ungebundenen) Occurrence nie
    — `accumulated >= target` brach nur die innere Lap, das äußere `while` nahm weiter
    Picks bis zum `DateTime`-Überlauf.
- Konservative Richtung bleibt: unbeweisbare Deferrals und Basen ohne spätere Pick-
  Evidenz führen weiter zur Verdrängung, nie zur falschen Beibehaltung.

## Abnahme-Status

- [x] Reproduktion durch Integrationstests bewiesen (beide Arme)
- [x] Provable-Deferral-Arm umgesetzt
- [x] Occurrence-lokale Basis umgesetzt
- [x] Volle Testsuite grün (siehe test-results.md)
