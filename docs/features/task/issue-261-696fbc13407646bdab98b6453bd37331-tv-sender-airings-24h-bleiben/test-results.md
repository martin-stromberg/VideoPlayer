# Test-Ergebnisse

Zweiter vollständiger Lauf nach dem Refactoring aus dem Code-Review (`ReconcileWindow`-Klasse,
`ReproContext`-Record, Methoden-Umbenennungen, `MaxOccurrenceLookbackDays`-Clamp auf 30,
`AssertNoDisplacedAsync`-Helper, eine korrigierte Assertion — Semantik unverändert).

## Ergebnis

**Status:** Keine Fehler

## Ausgeführte Befehle

| Befehl | Exit-Code | Ergebnis |
|--------|-----------|----------|
| `dotnet build VideoPlayer.sln` (Debug) | 0 | erfolgreich, 0 Warnungen, 0 Fehler |
| `dotnet build VideoPlayer.sln -c Release` | 0 | erfolgreich, 0 Warnungen, 0 Fehler |
| `dotnet test VideoWebPlayer.Tests/VideoWebPlayer.Tests.csproj --no-build -c Release --filter "Category!=E2E" --collect:"XPlat Code Coverage"` | 0 | 2021/2021 bestanden (2:00 min), Coverage-Report `coverage.cobertura.xml` erfolgreich erzeugt — der Infrastruktur-Fehler aus Iteration 1 (Coverlet-Dateisperre durch fremden `vstest`-Prozess) trat nicht auf; vor dem Lauf war kein `vstest`/`testhost`-Prozess aktiv |
| `dotnet test VideoWebPlayer.Tests/VideoWebPlayer.Tests.csproj --no-build -c Release --filter "Category=E2E"` | 0 | 225/226 bestanden, 1 übersprungen (3:55 min) |

Nach dem Coverage-Lauf geprüft: keine Coverlet-Reste in `VideoWebPlayer.Tests/bin/Release/net10.0/`
(`VideoWebPlayer.dll`, `VideoWebPlayer.Client.dll`, `VideoWebPlayer.Tests.dll` enthalten keine
`Coverlet`-Marker), Originalmodule wurden sauber zurückkopiert — kein Rebuild erforderlich.

Protokolle/Ergebnisdateien: `test-results/run-non-e2e.log|.trx` (Coverage-Lauf),
`test-results/run-e2e.log|.trx`.

## E2E-Abdeckung

Keine geplanten E2E-Szenarien — die Anforderung berührt keinen Benutzerfluss (reine Service-Logik im
`ProgramScheduleService` plus neue Option `MaxOccurrenceLookbackDays`); laut `plan.md`/`plan-check.md`
erfolgt der Funktionsnachweis über Service-Integrationstests gegen echtes SQLite. Die vollständige
E2E-Suite (`Category=E2E`, Playwright) lief dennoch als Regression mit: 225/226 bestanden,
1 absichtlich übersprungen — identisch zur Baseline und zu Iteration 1.

| Szenario | Test / Testklasse | Ergebnis |
|----------|-------------------|----------|
| Langes Airing (> 24 h) bleibt geplant | `ProgramScheduleServiceTests_Reconcile.Reconcile_LongAiring_StaysScheduled` | Bestanden |
| Airing > 48 h (Tagesschleifen-Erweiterung) | `ProgramScheduleServiceTests_Reconcile.Reconcile_LongAiring_BeyondTwoDays_StaysScheduled` | Bestanden |
| Mehrteiliges > 24-h-Airing (Marathon-Block) | `ProgramScheduleServiceTests_Reconcile.Reconcile_MultiPartLongAiring_StaysScheduled` | Bestanden |
| Verschobenes langes Airing (`invaderStarts`-Evidenz) | `ProgramScheduleServiceTests_Reconcile.Reconcile_LongAiring_Shifted_StaysScheduled` | Bestanden |
| Teilmaterialisiertes langes Airing ohne Auslöser (`k > 0`) | `ProgramScheduleServiceTests_Reconcile.Reconcile_PartiallyMaterializedLongAiring_NoTrigger_StaysScheduled` | Bestanden |
| Echt veraltetes langes Airing bleibt `Displaced` | `ProgramScheduleServiceTests_Reconcile.Reconcile_LongAiring_ChangedRule_StillDisplaced` | Bestanden |
| Obergrenze `MaxOccurrenceLookbackDays` | `ProgramScheduleServiceTests_Reconcile.Reconcile_LongAiring_BeyondMaxLookback_Displaced` | Bestanden |
| Interleaved-Pfad (Genre-Regel) | `ProgramScheduleServiceTests_Interleaving.Reconcile_GenreRule_Interleaved_LongAiring_StaysScheduled` | Bestanden |
| Options-Normalisierung | `ProgramScheduleServiceTests_Boundary.ProgramScheduleOptions_MaxOccurrenceLookback_Normalizes` | Bestanden |

## Zusammenfassung

- Gesamt: 2247 (2021 Non-E2E + 226 E2E)
- Bestanden: 2246
- Fehlgeschlagen: 0
- Übersprungen: 1 — `DevicePlaylistE2ETests_Playback.PairedDevice_ReportsProgressForMovieWithoutCollection_ContinueWatchingListStillLoads` (absichtlich per `[Fact(Skip = …)]` deaktiviert, bereits in der Baseline übersprungen, unabhängig von dieser Anforderung)

## Baseline-Vergleich

| Lauf | Baseline (`inventory/test-results/`) | Iteration 1 | Dieser Lauf |
|------|--------------------------------------|-------------|-------------|
| Non-E2E | 2012 bestanden, 0 fehlgeschlagen | 2021 bestanden | 2021 bestanden, 0 fehlgeschlagen (+ 9 Tests ggü. Baseline, alle grün, im TRX verifiziert) |
| E2E | 225 bestanden, 0 fehlgeschlagen, 1 übersprungen | 225/226, 1 übersprungen | 225 bestanden, 0 fehlgeschlagen, 1 übersprungen (identisch) |

Kein Flacker-Ausfall aufgetreten — keine Wiederholungsläufe nötig. Das Refactoring hat die
Testsuite gegenüber Iteration 1 ergebnisneutral durchlaufen lassen (2246/2247 in beiden Runden).

## Testabdeckung

**Abdeckung:** 94,6 % Zeilenabdeckung gesamt (139 527 / 147 520 Zeilen; Branch-Abdeckung 56,1 %),
gemessen mit `coverlet.collector` im Non-E2E-Lauf. Die diesmal erfolgreich erzeugte Datei
`coverage.cobertura.xml` wurde nach Auswertung wieder entfernt (21 MB, nicht commitwürdig).

Von der Anforderung betroffene Dateien:

| Datei | Abdeckung |
|-------|-----------|
| `VideoWebPlayer/Services/ProgramScheduleService.cs` | 92,5 % |
| `VideoWebPlayer/Configuration/ProgramScheduleOptions.cs` | 100 % |
| `VideoWebPlayer/Services/ProgramScheduleSignal.cs` | 100 % |
| `VideoWebPlayer/Services/ProgramScheduleWorker.cs` | 89,6 % |
| `VideoWebPlayer/Services/IProgramScheduleService.cs` | 75,0 % |

Darüber hinaus liegen 220 Dateien unter 80 % (überwiegend EF-Migrationen, Razor-/Identity-Seiten,
DTOs und Entitäten — die Razor-Seiten werden primär über die E2E-Suite getestet, deren Lauf nicht in
die Zeilenabdeckung einfließt); keine davon ist von dieser Änderung betroffen.

## Fehlende Tests

Quelle: `Coverage-Daten` (Cobertura-Report des Non-E2E-Laufs)

74 Dateien mit 0 % Zeilenabdeckung, gruppiert — keine davon von dieser Änderung betroffen:

- `VideoWebPlayer.Client/Models/` (5): `DtoMaxSortOrderResult.cs`, `DtoRecentEntry.cs`, `FavoriteEntry.cs`, `ImpersonateRequest.cs`, `UnlockedUsersRequest.cs`
- `VideoWebPlayer/Components/Account/` (31): Identity-Helper (`IdentityNoOpEmailSender.cs`, `IdentityRedirectManager.cs`, `IdentityUserAccessor.cs`) sowie sämtliche Identity-Razor-Seiten und Shared-Komponenten — UI, nur über E2E erreichbar
- `VideoWebPlayer/Components/Pages/` (17): u. a. `Admin/*.razor`, `Samples/*.razor`, `ActorDetails.razor`, `MediaSourceDetails.razor`, `Error.razor` — UI
- `VideoWebPlayer/Components/Playlists/` (2): `PlaylistDeleteConfirmationDialog.razor`, `PlaylistSortModeConfirmationDialog.razor`
- `VideoWebPlayer/Components/Shared/Media/` (4): `ActorList.razor`, `MediaBaseEntryList.razor`, `UnlockButton.razor`, `WatchedIndicator.razor`
- `VideoWebPlayer/Controllers/` (4): `BackupsController.cs`, `SourceGenresController.cs`, `UnlockedMediaController.cs`, `Attributes/ConnectionCheckAttribute.cs`
- `VideoWebPlayer/Events/` (2): `MediaSourceCreatedEvent.cs`, `MediaSourceUpdatedEvent.cs`
- `VideoWebPlayer/Hubs/` (1): `MediaUpdateHub.cs`
- `VideoWebPlayer/Services/` (6): `Backups/ManualBackupJobService.cs`, `Backups/VideoWebPlayerBackupDataSource.cs`, `Backups/VideoWebPlayerBackupRestoreGuard.cs`, `DemoData/FileSystemDemoDataSetService.cs`, `DemoData/IDemoDataSetService.cs`, `SftpStreamWrapper.cs`
- `VideoWebPlayer/Utils/` (1): `LocalNetworkHelper.cs`
- `VideoWebPlayer/ViewModels/` (1): `MediaSourceDetailsViewModel.cs`
