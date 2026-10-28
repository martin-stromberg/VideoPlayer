# Test-Ergebnisse

## Ergebnis

**Status:** Keine Fehler

## Zusammenfassung

- Gesamt: 2624
- Bestanden: 2622
- Fehlgeschlagen: 0
- Übersprungen: 2

Ausgeführt mit:
`dotnet test VideoWebPlayer.Tests/VideoWebPlayer.Tests.csproj --collect:"XPlat Code Coverage" --logger "console;verbosity=normal" --results-directory VideoWebPlayer.Tests/TestResults` (Gesamtzeit ca. 5,2 Minuten)

Übersprungene Tests (vorhandene, plattform-/laufzeitbedingte Skips, keine neuen):
- `FfmpegToolDownloadServiceTests_Install.Run_OnUnix_SetsExecutableBit` (nur Unix)
- `DevicePlaylistE2ETests_Playback.PairedDevice_ReportsProgressForMovieWithoutCollection_ContinueWatchingListStillLoads`

Keine Wiederholungen nötig — alle Tests waren im ersten Lauf grün. Der im letzten Lauf
flackernde `PlaylistPlaybackE2ETests.PlaylistSkipLockedEntriesE2ETest` lief diesmal
beim ersten Versuch durch (14 s).

Die beiden neu ergänzten Tests zur früheren Abdeckungslücke sind vorhanden und bestanden:
- `TVSenderAdminTests.RulePanel_GenreRule_OrphanedMaterialId_ShowsNotFound`
- `TVSenderAdminTests.RulePanel_GenreRule_LegacyMaterialId_ShowsSingleGenreCount`

## Testabdeckung

**Abdeckung:** 95,7 % Zeilen (gesamt, Cobertura), 68,4 % Zweige

Coverage-Datei: `VideoWebPlayer.Tests/TestResults/1008ab49-37bc-437f-9620-c152a89e5103/coverage.cobertura.xml`

| Datei | Abdeckung |
|-------|-----------|
| VideoWebPlayer/Components/Pages/Admin/TVSenderAdmin.razor | 81,8 % Zeilen / 73,7 % Zweige |
| VideoWebPlayer/wwwroot/app.css | Nicht messbar (CSS) |

Hinweis: `TVSenderAdmin.razor` liegt mit 81,8 % über der 80-%-Schwelle und wird als
zentrale geänderte Datei trotzdem aufgeführt. `app.css` ist nicht instrumentierbar;
die neuen Klassen (`.rule-panel*`) werden indirekt über bUnit-Tests mitgeprüft
(Klassen-/Struktur-Assertions).

Die im letzten Bericht beanstandete Lücke ist geschlossen. Die neue Testsuite durchläuft
jetzt alle Fallback-Pfade des Genre-Fallbacks:
- `ResolveGenreNames` Legacy-Pfad über `MaterialId` (Z. 2163–2165): abgedeckt
- `MaterialSummaryLabel`-Zweig „Genre (nicht gefunden)" bei verwaistem `MaterialId`
  (Z. 2189–2190): abgedeckt
- Genre-Anzahl-, Quellen- und Clip-Gruppen-Suffixe (Z. 2191–2195): abgedeckt

## Fehlende Tests

Quelle: `Coverage-Daten`

- `VideoWebPlayer/Components/Pages/Admin/TVSenderAdmin.razor` — Zeile 2177 (0 Treffer):
  Der Zweig `return "Genre (nicht gefunden)"` in `MaterialLabel` (Detail-Label, nicht
  `MaterialSummaryLabel`) wird weiterhin von keinem Test durchlaufen. Kleinere Restlücke
  gegenüber der behobenen Hauptlücke; der identische Text wird über `MaterialSummaryLabel`
  bereits getestet.
