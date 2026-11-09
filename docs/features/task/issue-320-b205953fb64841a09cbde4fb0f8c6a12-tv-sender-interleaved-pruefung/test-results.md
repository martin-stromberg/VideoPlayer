# Testergebnisse — Issue #320

## Reproduktion (vor dem Fix)

`ProgramScheduleServiceTests_Issue320` — beide Szenarien aus dem Issue:

- `Reconcile_UnchangedRules_DeferredStrandInPrefix_StaysScheduled`: Genre-Rotation mit
  FSK-18-Strang (geblockter Kopf ab 20:00 → Deferral in der Coverage-überspannenden
  Occurrence), Coverage-Start 21:00 mid-Occurrence → `ReconcileAsync` verdrängte die
  unveränderte Ausstrahlung (Displaced: Rest der laufenden Occurrence + komplette
  Tagesgruppen danach).
- `Reconcile_UnchangedRules_UnwrittenPrefixTakeWithLaterGroupTake_StaysScheduled`:
  Strang mit ungeschriebenem Prefix-Take und Takes in späteren, nicht-kontigüen Gruppen,
  abweichende Episodenlaufzeiten → persistierter Plan-End-Cursor verzerrte die Basis →
  Verdrängung der unveränderten Ausstrahlung.

Nebenbefund beim Seeding: `SelectAiringItemsInterleaved` terminierte bei
`SlotDuration`-Interleaved-Regeln ohne Clip-Gruppen auf ungebundener (letzter) Occurrence
nicht — `DateTime`-Überlauf in `slotStart += item.Duration`. Separat behoben
(äußere Lap stoppt jetzt bei `accumulated >= target`).

## Nach dem Fix

- Beide Issue-320-Tests: grün (keine Verdrängung über zwei aufeinanderfolgende
  Reconcile-Läufe, kein `HeadPickInconsistent` im Log).
- Volle Testsuite `dotnet test VideoWebPlayer.Tests/VideoWebPlayer.Tests.csproj`
  (Debug): 2830 von 2833 grün, 2 übersprungen — einziger Fehlschlag der Playwright-E2E-
  Test `BackupUpload_NavigatingAwayDuringUpload_AbortsWithoutRedirectBack` (fachlich
  unabhängig, bekannte Flake-Kategorie), beim Einzelrerun grün.
- `dotnet build VideoPlayer.sln` Debug und Release: fehlerfrei.
