# Detail: Bestandstests mit alter (Anti-Überzugs-)Semantik

`VideoWebPlayer.Tests/ProgramScheduleServiceTests_Boundary.cs`:

| Test | Alte Erwartung | Neue Erwartung |
|------|----------------|----------------|
| `EnsureCoverage_AdjacentWindows_OvershootDoesNotKillSuccessor` (Z. 28–54) | Kein Slot kreuzt 16:00; Nachfolger startet 16:00 | Vorgänger-Episode (Beginn < 16:00) läuft über 16:00 hinaus; Nachfolger-Slots beginnen am Überstand-Ende (shifted) |
| `Reconcile_PartialOccurrence_InvadingPredecessorTail_Materializes` (Z. 60–94) | Invasor `Displaced`; Folge-Airing ab 16:00 | Invasor bleibt `Scheduled` (begonnenes Video läuft aus); Folge-Airing ab Invasor-Ende 16:37 |
| `Reconcile_BoundaryTrimmedAiring_StaysStableAcrossRuns` (Z. 99–126) | 0 Displaced über 3 Reconcile-Läufe | bleibt: mit neuer Selektion + Reproduzierbarkeit muss weiterhin 0 gelten |
| `EnsureCoverage_OversizedHeadItem_IsConsumedNotStalling` (Z. 131–157) | 300-min-Head wird konsumiert (nicht eingeplant) | konsumieren entfällt: der 300-min-Film wird eingeplant und überzieht das Folgefenster; Nachfolger shifted. Test umbenennen/umkehren |

Daneben zu durchsuchen beim Implementieren: `ProgramScheduleServiceTests_Conflicts.cs`,
`..._Reconcile.cs`, `..._Generation.cs`, `..._Interleaving.cs` — Assertions auf
„kein End > Grenze" bzw. „displaced Invasor".

## Neue Tests (Pflicht aus der Anforderung)

1. Rest 3 min vor weicher Grenze + 45-min-Item → Item komplett (`End >` Fensterende), kein
   gekürzter Slot; Folge-Regel shifted/trimmt auf das Item-Ende.
2. Rest < MinStartRemain (Default 30 s) → nichts geplant, Füller-Pfad füllt die Lücke.
3. Shifted Airing bleibt über Reconcile-Läufe stabil (Reproduzierbarkeit).
4. Invasor im Reconcile bleibt `Scheduled`, Folge-Materialisierung beginnt an dessen Ende.
5. (Konfig) `MinStartRemainSeconds = 0/negativ` → Default 30 s greift.
