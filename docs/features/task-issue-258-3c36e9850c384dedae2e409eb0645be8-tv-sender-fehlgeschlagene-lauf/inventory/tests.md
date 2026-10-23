# Inventar: Test-Ausgangszustand

## Ausgangsbedingungen des Laufs

| Attribut | Wert |
|---|---|
| Datum/Uhrzeit | 2026-10-23, Lauf gestartet ca. 20:18 Uhr, beendet ca. 20:24 Uhr MESZ (UTC+02:00, „W. Europe Standard Time") |
| Branch | `task/issue-258-3c36e9850c384dedae2e409eb0645be8-tv-sender-fehlgeschlagene-lauf` (Basis: `staging`) |
| Commit | `7fdb3a18001b7a4fb4ed09cf191c853b14e8dfb3` (`git rev-parse HEAD`) |
| Uncommittete Änderungen (`git status --porcelain`) | nur `?? docs/features/task-issue-258-3c36e9850c384dedae2e409eb0645be8-tv-sender-fehlgeschlagene-lauf/` (neues Feature-Verzeichnis mit `requirement.md` und dieser Inventar-Dokumentation; keine Code-Änderungen) |
| .NET SDK (`dotnet --version`) | 10.0.402 |
| Projektüblicher Testbefehl | `dotnet test VideoWebPlayer.Tests/VideoWebPlayer.Tests.csproj` (volle Suite; laut AGENTS.md §7 können einzelne Playwright-E2E-Tests unter Last flackern) |

## Testläufe

| Lauf | Befehl | Arbeitsverzeichnis | Exit-Code | Erfolgreich | Fehlgeschlagen | Übersprungen | Nachweis |
|---|---|---|---|---|---|---|---|
| 1 (Ausgangslauf) | `dotnet test VideoWebPlayer.Tests/VideoWebPlayer.Tests.csproj` | Repo-Root `D:\Repositories\softwareschmiede\3c36e985-0c38-4ded-ae2e-409eb0645be8` | 0 | 2210 | 0 | 1 | `inventory/test-results/dotnet-test-run1-console.txt` (Zusammenfassung Zeile 155: „Bestanden!: Fehler: 0, erfolgreich: 2210, übersprungen: 1, gesamt: 2211, Dauer: 4 m 36 s") |

## Nachgewiesene bestehende Testfehler

**Keine.** Der Ausgangslauf ist vollständig grün (0 Fehler). Der Playwright-E2E-Test
`TVSenderAdminE2ETests.Admin_CreateEditDeactivateDeleteRule`, der die betroffene Seite
`/admin/tv-sender` inklusive der Detail-Tabellen `tvsender-probe-failed-detail` /
`tvsender-probe-pending-detail` abdeckt, ist im Ausgangslauf bestanden.

## Übersprungene / nicht ausgeführte Tests

| Test | Grund |
|---|---|
| `VideoWebPlayer.Tests.DevicePlaylistE2ETests_Playback.PairedDevice_ReportsProgressForMovieWithoutCollection_ContinueWatchingListStillLoads` | Statisch übersprungen per `[Fact(Skip = "…")]` in `VideoWebPlayer.Tests/DevicePlaylistE2ETests_Playback.cs:76`. Dokumentierter, bekannter Produktfehler (`api/continue-watching` antwortet mit HTTP 500, wenn ein Film zu keiner Filmsammlung gehört; `ContinueWatchingService.Create` ohne Null-Prüfung), bewusst außerhalb des damaligen Auftrags. Fachlich unabhängig von diesem Issue. |

Keine Abbrüche, keine Build- oder Setup-Fehler. Build-Warnungen sind im Konsolenlog
enthalten, betreffen den Testausgang nicht.

## Testlücken für dieses Issue

Aus dem Ist-Stand folgen diese Lücken (reine Bestandsaufnahme, keine Planung):

- Es existiert **keine Reset-Methode** auf `MediaDurationProbeService` — folglich auch
  kein Test dafür.
- `MediaDurationProbeDetailItem` trägt **keine Item-ID**; kein Test prüft eine
  Id-Projektion in `PendingItems`/`FailedItems`.
- `TVSenderAdmin.razor` hat **keine Aktions-Schaltflächen** in den Probe-Detailtabellen
  und keinen Reset-Bestätigungsdialog; `TVSenderAdminTests.cs` und
  `TVSenderAdminE2ETests.cs` prüfen entsprechend keine Reset-Aktion.
- Es gibt kein eigenes Statusmeldungs-Element auf der Probe-Karte (nur
  `scheduleStatus`/`rulesStatus`/`channelStatus` auf den anderen Tabs).

## Bestehende Testklassen und Hilfsmethoden für den Bereich

| Datei | Klasse | Umfang / relevante Hilfsmethoden |
|---|---|---|
| `VideoWebPlayer.Tests/Services/MediaDurationProbeServiceTests.cs` | `MediaDurationProbeServiceTests` | ~19 `[Fact]`-Tests zu `ProbePendingAsync` (Selektion, Retry-Delay, `MaxAttempts`, Permanent/Transient, Batch-Größe, Parallelität, SFTP-/HTTP-/Stream-Pfade, Signal). Shared in-memory SQLite + eigener DI-Container. Helfer: `SeedItemAsync`, `SeedSftpItemAsync`, `SaveAsync`, `ReloadAsync`; Fakes: `FakeDurationProbe`, `FakeFfprobeLocator`, `StubStreamTokenService`, `FakeScheduleSignal`, `StubMediaSourceReader`. Relevante Fälle: `…_MaxAttemptsReached_NotRetried`, `…_PermanentFailure_ExhaustsAttemptsAndStops`, `…_TransientFailure_RetriedAfterDelay`, `…_LegacyFailedItem_IsPickedUpAgain` |
| `VideoWebPlayer.Tests/Services/MediaDurationProbeServiceTests_Progress.cs` | `MediaDurationProbeServiceTests_Progress` | 7 `[Fact]`-Tests zu `GetProgressAsync` gegen echtes SQLite via `ProgramScheduleTestDb`-Fixture: Zähler-Trennung, `RecentItems`, `LastActivity`/`ProbedLastHour`, `PendingItems`, `FailedItems`, `detailItemCount`-Kappung bei vollen Zählern, leere Bibliothek, `ForbiddenAccessException`/`UnauthorizedAccessException`. Helfer: `CreateService(ProgramScheduleTestDb)`, `SeedItemAsync`; lokale Stubs `NullProbe`, `PresentLocator`, `StubMediaSourceReader` |
| `VideoWebPlayer.Tests/Services/MediaDurationProbeWorkerTests.cs` | `MediaDurationProbeWorkerTests` | 2 `[Fact]`-Tests: Worker verarbeitet ausstehende Items; Feature-Flag `TvSenderEnabled = false` → keine Probes. Stubs `AlwaysProbe`, `PresentLocator` |
| `VideoWebPlayer.Tests/Components/TVSenderAdminTests.cs` | `TVSenderAdminTests` (bUnit) | 6 `[Fact]`-Tests: vier Tabs, Tab-Wechsel mit `tvsender-probe-pending-detail`/`tvsender-probe-failed-detail` inkl. Chip-Toggles und `tvsender-probe-detail-row`-Inhalten, Regel-Editor-Overlay, Trailer-/Clipsource-Optionen, Sendeplan-Karten/Tages-Gruppierung. Helfer: `CreateContextAsync(fixture, adminId)` (bUnit-Kontext mit echtem `MediaDurationProbeService`, `ProgramRuleService`, `ProgramScheduleService`, `ProgramScheduleSignal`, `ProgramSettingsService`, `VideoWebPlayerClient` mit `EmptyListHandler`), `SeedProbeItemAsync`; Stubs `NullProbe`, `PresentLocator`, `StubMediaSourceReader` |
| `VideoWebPlayer.Tests/TVSenderAdminE2ETests.cs` | `TVSenderAdminE2ETests` (Playwright) | Konsolidierter Admin-E2E-Test `Admin_CreateEditDeactivateDeleteRule` seedet u. a. Probe-Items (`e2e-fertig.mkv` mit `Duration`, `e2e-defekt.mkv` mit `DurationProbeAttempts = 10`, `e2e-wartet.mkv` mit 1 Versuch) und prüft Chips, Rolling-Liste sowie beide aufklappbaren Detail-Tabellen; weitere Tests `Admin_GenreRule_MultiSelectAndSourceFilter` u. a. Basisklasse `PlaylistsE2ETestBase` (`SkipBrowser`-Guard) |
| `VideoWebPlayer.Tests/Helpers/ProgramScheduleTestDb.cs` | `ProgramScheduleTestDb` (Fixture) | Shared in-memory SQLite (`file:{name}?mode=memory&cache=shared`), `EnsureCreatedAsync`, setzt `Setup.TvSenderEnabled = true`. Helfer: `AddUserAsync(email, isAdmin)`, `EnsureSourceAsync`, `CreateMediaItemAsync(sourceId, duration)` (setzt `DurationProbedAt = UtcNow` — für Reset-Tests sind `DurationProbedAt`/`Attempts` ggf. nachträglich zu korrigieren), `CreateMovieAsync`, `CreateShowAsync`, `CreateRuleAsync`, `CreateSlotAsync`, `GetChannelAsync`, `SaveDiagAsync`, `AssertGapless` |
| `VideoWebPlayer.Tests/MediaProbeHttpIntegrationTests.cs` | `MediaProbeHttpIntegrationTests` | HTTP-Loopback-Probe-Integration (`IAsyncLifetime`) — benachbart, kein direkter Reset-Bezug |
| `VideoWebPlayer.Tests/Services/FfprobeMediaDurationProbeTests.cs`, `FfprobeLocatorTests.cs`, `SftpProbeUriBuilderTests.cs` | — | benachbarte Probe-Infrastruktur-Tests |
