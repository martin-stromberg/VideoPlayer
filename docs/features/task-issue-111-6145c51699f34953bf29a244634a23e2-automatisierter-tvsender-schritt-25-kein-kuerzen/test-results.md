# Testergebnisse — N22

**Status: Keine Fehler**

- `dotnet test VideoWebPlayer.Tests/VideoWebPlayer.Tests.csproj -c Release` (vollständig,
  synchron): **2182 erfolgreich, 1 übersprungen (Alt-Skip), 0 Fehler** (Dauer 6 m 4 s).
  Basis war ~2178 + 1 — plus 4 Netto-Neutests.
- Hinweis Konfiguration: `bin/Debug` ist durch die laufende Benutzer-App
  (`VideoWebPlayer`, aus Visual Studio gestartet) gesperrt — Build/Test lief daher in
  `-c Release` bzw. mit Scratch-`OutDir` (`artifacts/bdebug`). Die App wurde nicht beendet.
- Fokuslauf `FullyQualifiedName~ProgramScheduleServiceTests`: 82/82 grün.

## Neue/angepasste Tests (Deckung der Anforderung)

- `EnsureCoverage_ShortRemainder_OvershootingItem_Completes` — 3 min Rest + 45-min-Item →
  komplett, `End` > Fensterende, Nachfolger shifted.
- `EnsureCoverage_RemainderBelowMinRemain_PlansFiller` — Rest < 30 s → nichts geplant,
  Füller-Pfad.
- `EnsureCoverage_HardBound_ItemCannotComplete_DefersToNextOccurrence` — harte Grenze:
  defer statt Kürzung.
- `ProgramScheduleOptions_MinStartRemain_Normalizes`.
- Angepasst: `AdjacentWindows` (Überzug + Shift), `OvershootingTailStaysScheduled` (Invasor
  bleibt Scheduled, Tail ab Invasor-Ende), `ShiftedAndOvershootingAirings_StayStable`,
  `OversizedHeadItem_AirsAndOvershoots`, `HigherPriorityWins_Overlap` (Defer statt Drop,
  kein Cursor-Fortschritt).

## Fehlgeschlagene Tests

Keine.
