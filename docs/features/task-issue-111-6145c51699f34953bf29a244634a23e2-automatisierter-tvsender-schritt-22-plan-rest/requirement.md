# Anforderung – Schritt 22 (N16-Rest + N18)

## Ausgangslage (Live-Befund, `nachbesserungen.md` Nachtrag zu N16)

Nach dem Schritt-21-Fix oszilliert der Planer nicht mehr (0 neue Displaced seit Neustart
16:22), aber **Regel 4 „Vorbendserien" (GenreRotation, täglich 16:00, 4 h SlotDuration,
10.205 planbare Items, Quell-Filter Quelle 1, Genres Quelle 1+2) erzeugt gar keine Slots** —
weder scheduled noch displaced.

**Diagnostizierte Ursache (verifiziert anhand der Live-Datenbank `WebVideoPlayer.db`):**
Alle 5 Regeln sind `GenreRotation`/täglich/Priorität 0 und kacheln den Tag lückenlos
(00:00→06:00→10:00→16:00→20:00→00:00). Bei `SlotDuration`-Regeln akkumuliert die
Materialisierung Items bis `≥ Ziel` — das letzte Item überzieht das nominelle Fenster per
Design in das Fenster der Folgeregel (Regel 3 endet 16:37–16:45 lokal statt 16:00; Regel 5
endet 01:24/02:23 lokal statt 00:00). Die atomare Konfliktprüfung verwirft dann die
**gesamte** Occurrence der nachfolgenden Regel (`ranges.Any(r => newSlots/blocked.Overlaps)`
bzw. `conflicts.Any(!evicts)`; bei Gleichpriorität gewinnt die kleinere Regel-Id).
Betroffen sind Regel 4 (überlappt von Regel 3, Id 3 < 4), Regel 6 (von Regel 5, Id 5 < 6)
und Regel 7 (eigener Überstand über Regel 3, Id 3 < 7) — alle 0 scheduled, passend zum
Live-Befund.

## Aufgabe A — N16-Rest (Priorität, BUG)

Die Materialisierung muss so umgebaut werden, dass Occurrences benachbarter Regeln
koexistieren:

1. **Fenstergrenze (softEnd):** Jede Occurrence besitzt ihr Fenster
   `[StartUtc, nächster Occurrence-Start einer materialisierten Regel)`. Eine Ausstrahlung
   darf ihre nominelle Länge nur in **freie** Zeit überziehen — ein Item, das die Grenze
   überschreiten würde, wird nicht mehr platziert (Airing endet früher). Ein führendes
   Item, das selbst in ein leeres Fenster nicht passt, wird konsumiert ohne Slot
   (dauerhaft übersprungen — sonst blockiert es die Rotation für immer).
2. **Invasor-Trimm in `ReconcileAsync`:** Belegte Slots, die vor dem Occurrence-Start
   begannen und in das Fenster hineinragen (Altbestand/Überstand), weichen — es wird nur
   das **Suffix** ihrer Airing-Gruppe ab der Grenze verdrängt (`Displaced`, Cursor-Rewind
   via `RewindCursor`), nicht die komplette Regel-Zukunft. Priorität gilt dabei nicht —
   die Fenstergrenze ist regelübergreifend. Echte Konkurrenten (Slot-Start ≥
   Occurrence-Start) bleiben beim bisherigen `evicts`-Verhalten (Priorität/Id).
3. **`AiringReproducible`** wendet denselben Grenz-Trim beim Nachbau der erwarteten
   Sequenz an, sonst würden gekappte Airings beim nächsten Lauf als ungültig verdrängt
   (neue Oszillation).
4. `EnsureCoverageAsync` nutzt dieselbe Fenstergrenze für neu materialisierte Airings;
   belegte Bestandsslots bleiben dort weiterhin harte Konflikte (extend-only).

**Repro-Test (Pflicht):** Occurrence „heute 16:00, jetzt 16:30" mit teilweise
vergangener Occurrence + planbarem Material + überstehendem Vorgänger → Rest-Slots
müssen entstehen. Ebenso: angrenzende Tageskachelung (zwei Regeln hintereinander,
Vorgänger überzieht) → beide Regeln erzeugen Slots; zweiter Lauf idempotent.

## Aufgabe B — N18: „Verdrängte bereinigen"-Button

Im Sendeplan-Tab (`TVSenderAdmin.razor`): Button **„Verdrängte bereinigen"**, der
serverseitig alle `ProgramSlots` mit `Status = Displaced` löscht. Anforderungen:

- Service-Methode in `IProgramScheduleService`/`ProgramScheduleService` (dort ist die
  Schedule-Logik; `ProgramRuleService` ist Regel-CRUD) — mit `userId`-basierter
  Admin-Prüfung serverseitig (`RequireAdminAsync`-Muster).
- Bestätigungsdialog (Muster: Regel-Löschen-Dialog `deleteCandidate`).
- Anzeige der gelöschten Anzahl im Statusfeld.
- Optional, nur wenn sauber separierbar: zweite, klar markierte Aktion
  **„Plan zurücksetzen"** (alle Slots + `ProgramRuleCursors` löschen).

## Randbedingungen

- `dotnet build` Debug + Release fehlerfrei; `dotnet test` vollständig grün.
- Neue Tests auf echtem SQLite (`ProgramScheduleTestDb`).
- Keine neuen Compilerwarnungen (xUnit1051 → `TestContext.Current.CancellationToken`,
  vollständige XML-`<param>`-Doku).
