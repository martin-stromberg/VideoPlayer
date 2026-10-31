# Testergebnisse

## Voller Testlauf (nach Umsetzung)

- Befehl: `dotnet test VideoWebPlayer.Tests/VideoWebPlayer.Tests.csproj --no-build` (Debug-Build zuvor: 0 Fehler)
- Ergebnis: 2111 gesamt — 2107 bestanden, 1 übersprungen (Alt-Skip), **3 fehlgeschlagen**
- Analyse der 3 Fehler:
  1. `GetAllowedMaterialTypes_ReturnsExpectedSets` — echter Erwartungswert-Update (Trailer neu) → Test angepasst, danach grün.
  2. `MediaSearchSelectorTests.EventCallback_OnMediaSelected_InvokedWithCorrectParameters` — bUnit-Timeout-Flacke → Einzelwiederholung grün.
  3. `LiveTvE2ETests.LiveTv_ProgramGap_ShowsFriendlyHintAndUpcoming` — Playwright-Flacke → Einzelwiederholung grün.
- Nachlauf: die drei Einzeltests erneut ausgeführt → **3/3 grün**.
- Neue Trailer-Tests: 12/12 grün (Erkennung Suffix + Ordner, keine Hauptdatei, Füller-Bevorzugung,
  Vorlauf-Fenster/Rückfall, kein Duration-Probed, Reconcile, Regel-Validierung, Alt-Backup).

## Relevante neue Tests

| Test | Nachweis |
|------|----------|
| `ClassifyAllAsync_TrailerSuffix_LinksToMovie_NotAsMovie` | AK 1, 2 |
| `ClassifyAllAsync_TrailersFolder_LinksToParentMovie` | AK 1, 2 |
| `ClassifyAllAsync_TrailerSuffix_IsNeverRepresentativeMediaItem` | AK 2 |
| `EnsureCoverage_TrailerFiller_AirsTrailerBeforeScheduledMovie` | AK 4 |
| `EnsureCoverage_TrailerOfUnscheduledMovie_IsNotAired` | AK 4 |
| `EnsureCoverage_BeyondLeadWindow_FallsBackToNormalFiller` | AK 4 |
| `EnsureCoverage_TrailerWithoutDuration_IsNotAired` | AK 3/4 |
| `Reconcile_KeptTrailerSlot_StaysScheduled` | Stabilität |
| `GetAllowedMaterialTypes_Filler_IncludesTrailer`, `CreateRule_TrailerMaterial_*` | AK 5 |
| `ReadFromAsync_LegacyBackupWithoutIsTrailerColumn_RestoresSuccessfully` | AK 6 |
