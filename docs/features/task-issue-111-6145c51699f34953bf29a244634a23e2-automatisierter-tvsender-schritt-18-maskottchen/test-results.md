# Testergebnisse: Maskottchen-Interstitials (N12, Schritt 18)

## Ausgangszustand (Baseline, vor Umsetzung)

- `dotnet test VideoWebPlayer.Tests/VideoWebPlayer.Tests.csproj` (Debug, synchron):
  **2110 grün / 1 fehlgeschlagen / 1 übersprungen** (2112 gesamt) —
  [baseline-dotnet-test.log](inventory/test-results/baseline-dotnet-test.log)
- Fehlschlag: `PlaylistPlaybackE2ETests.PlaylistSkipLockedEntriesE2ETest` — bekannter
  Baseline-Flake, Einzel-Retry grün ([baseline-retry-skiplocked.log](inventory/test-results/baseline-retry-skiplocked.log)).
- Übersprungen: 1 bekannter Alt-Skip (`PairedDevice_ReportsProgressForMovieWithoutCollection…`).

## Ergebnis nach Umsetzung

- `dotnet test VideoWebPlayer.Tests/VideoWebPlayer.Tests.csproj` (Debug, synchron, vollständig):
  **2124 grün / 1 fehlgeschlagen / 1 übersprungen** (2126 gesamt) —
  [final-dotnet-test.log](inventory/test-results/final-dotnet-test.log)
- Fehlschlag: `TVSenderAdminE2ETests.Admin_CreateEditDeactivateDeleteRule` — Fehler **in der
  neuen E2E-Erweiterung selbst** (Quellname je Seed variabel, `HasText "E2E Quelle"` traf
  nicht). Behoben: Auswahl nun namensunabhängig über erste Option; Einzel-Retry
  **grün** ([final-retry-tvsender-e2e.log](inventory/test-results/final-retry-tvsender-e2e.log)).
- Neue Tests (14): `ProgramScheduleServiceTests_ClipSource` (8: Füllen, Ausschluss
  klassifizierter Items, Laufzeit-/Nicht-Video-Filter, Zugriff, Cursor-Rotation,
  Orphaned-Quelle, Reconcile, Diagnose), `ProgramRuleServiceTests_ClipSource` (4:
  Zulässigkeit Filler/Repeat-Ausschluss, Create-Erfolg, Ablehnung Nicht-Filler + unbekannte
  Quelle), `ProgramGuideServiceTests.GetNowAsync_BareMediaItemSlot_FallsBackToItemName`,
  `TVSenderAdminTests.RuleEditor_ClipSourceSelect_…` (bUnit). E2E-Nachweis über die
  Erweiterung des konsolidierten `Admin_CreateEditDeactivateDeleteRule`.
- Builds: `dotnet build VideoPlayer.sln` Debug **und** Release je 0 Fehler, 144 Warnungen
  (Bestand, keine neuen); `dotnet format --verify-no-changes --severity error` sauber.
- Clip-Produktion verifiziert per `ffprobe` je Datei (h264, 1280×720, 25 fps, AAC,
  Dauern 10–20 s) — Ausgaben im Generator-Protokoll.

## Fehlgeschlagene Tests

Keine verbleibenden.
