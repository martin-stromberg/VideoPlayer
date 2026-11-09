# Bestandsaufnahme — Tests

## Test-Ausgangszustand vor der Umsetzung

- Zeitpunkt (mit Zeitzone): 21.10.2026, ca. 17:00 MESZ (UTC+2)
- Branch und Commit-ID: `task-issue-111-6145c51699f34953bf29a244634a23e2-automatisierter-tvsender-schritt-11-probe-fortschritt` @ `b663c63` („step: Schritt 11 (Fortschrittsanzeige N6) In Arbeit")
- Uncommittete Änderungen im getesteten Stand: keine (Arbeitsverzeichnis sauber; die Lifecycle-Artefakte unter `docs/features/…schritt-11-probe-fortschritt/` sind untracked und berühren keinen Produktivcode)
- Testumgebung und Runtime-/SDK-Versionen: .NET SDK 10.0.402, .NET 10.0.13, Windows, Playwright-E2E gegen `WebApplicationFactory` + Kestrel (in-process)
- Ermittelte Testsuiten und Quellen der Testbefehle: `dotnet test VideoWebPlayer.Tests/VideoWebPlayer.Tests.csproj` (xUnit + FluentAssertions-frei/Assert-Stil des Projekts; E2E via Playwright) — Vorgabe aus `AGENTS.md`/Projektregeln

### Wichtiger Umgebungshinweis (Debug-Ausgabe gesperrt)

`dotnet build VideoPlayer.sln` (Debug) scheitert derzeit mit MSB3027 am Kopieren von
`VideoWebPlayer.Client.dll` nach `VideoWebPlayer/bin/Debug/net10.0/` — die Datei ist durch
die laufende Benutzer-Instanz `VideoWebPlayer.exe` (PID 55720, gestartet 16:35 aus VS heraus)
gesperrt. Der Prozess wird nicht beendet (laufende Instanz des Anwenders). Baseline und
Nachweis-Läufe erfolgen deshalb in `-c Release` (ungeblockt; die E2E-Tests starten die App
in-process über `WebApplicationFactory`, daher gleichwertig). Die Debug-Kompilierbarkeit wird
zusätzlich über einen Build mit Scratch-`OutDir` nachgewiesen.

### Testläufe

| Lauf | Befehl inkl. Filter | Arbeitsverzeichnis | Exit-Code | Erfolgreich | Fehlgeschlagen | Übersprungen | Nachweis |
|------|--------------------|--------------------|-----------|-------------|----------------|--------------|----------|
| Baseline (gefiltert) | `dotnet test VideoWebPlayer.Tests/VideoWebPlayer.Tests.csproj -c Release --filter "FullyQualifiedName~MediaDurationProbe\|FullyQualifiedName~TVSenderAdmin\|FullyQualifiedName~ProgramSchedule\|FullyQualifiedName~ProgramRule"` | Repo-Root | 0 | 131 | 0 | 0 | `TRX` (`test-results/baseline-probe-fortschritt.trx`) |
| Build Release | `dotnet build VideoPlayer.sln -c Release` | Repo-Root | 0 | – | – | – | 0 Fehler, 142 Warnungen (Bestand) |
| Build Debug | `dotnet build VideoPlayer.sln` | Repo-Root | 1 | – | – | – | MSB3027/MSB3021: `VideoWebPlayer.Client.dll` durch laufende `VideoWebPlayer.exe` (PID 55720) gesperrt — Umgebungsproblem, kein Codeproblem |

### Nachgewiesene bestehende Testfehler

Keine — der Baseline-Lauf war vollständig grün (131/131). Der aus früheren Schritten bekannte
E2E-Flake `TVSenderAdminE2ETests.Admin_CreateEditDeactivateDeleteRule` (FillAsync-/Settings-Race)
trat in diesem Lauf nicht auf; er ist als instabil dokumentiert und bei Ausfall einzeln zu
wiederholen.

### Testlücken und Ausführungsprobleme

- Die vollständige Suite (~2000 Tests) wurde für die Bestandsaufnahme nicht gelaufen — sie ist
  dem Abschlusslauf vorbehalten (Projektregel: voller Lauf synchron, Debug+Release-Build).
- Debug-Build siehe Umgebungshinweis oben.

## Testklassen

### `MediaDurationProbeServiceTests` (`VideoWebPlayer.Tests/Services/MediaDurationProbeServiceTests.cs`)
- `ProbePendingAsync_ProbesPendingVideoItems_WritesDuration` — Laufzeit + `DurationProbedAt` werden geschrieben
- `ProbePendingAsync_SuccessfulProbe_NotifiesScheduleSignal` / `_FailedProbe_DoesNotNotifyScheduleSignal` — Signal-Verhalten
- `ProbePendingAsync_TransientFailure_RetriedAfterDelay` — Retry-Gate via `RetryDelaySeconds`
- `ProbePendingAsync_LegacyFailedItem_IsPickedUpAgain` — Altbestand (Attempts=0) wird wieder aufgegriffen
- `ProbePendingAsync_MaxAttemptsReached_NotRetried` / `_PermanentFailure_ExhaustsAttemptsAndStops` — Erschöpfung
- `ProbePendingAsync_SkipsAlreadyProbedItems` / `_SkipsNonVideoItems` — Selektionsgrenzen (Video-Extensions!)
- `ProbePendingAsync_LocalSource_UsesFileProbe` / `_SftpSource_StreamsViaOpenFileStream` / `_SftpSource_NullStream_CountsTransientAttempt` / `_ProbeThrows_MarksTransientAndContinues` — Quell-/Fehlerpfade
- `ProbePendingAsync_NoFfprobe_LeavesItemsUnprobed` / `_RespectsBatchSize` / `_ParallelProbes_RunsConcurrently` / `_SequentialProbes_RunOneAtATime`
- Helfer: `SeedItemAsync` (Source→Collection→Item-Kette), private Fakes `FakeDurationProbe`, `FakeFfprobeLocator`, `FakeScheduleSignal`, `StubMediaSourceReader`; echtes In-Memory-SQLite (shared cache)

### `MediaDurationProbeWorkerTests` (`VideoWebPlayer.Tests/Services/MediaDurationProbeWorkerTests.cs`)
- `ExecuteAsync_ProcessesPendingItems` — Worker treibt `ProbePendingAsync` ohne Benutzerinteraktion

### `FfprobeMediaDurationProbeTests` (`VideoWebPlayer.Tests/Services/FfprobeMediaDurationProbeTests.cs`)
- Exit-/Timeout-/Parsing-Fälle der ffprobe-Anbindung

### `ProgramScheduleServiceTests_Diagnosis` (`VideoWebPlayer.Tests/ProgramScheduleServiceTests_Diagnosis.cs`)
- `Diagnosis_SchedulableMovie_CountsSchedulable` / `_MovieWithoutMediaItem_CountsNoMediaItem` / `_UnprobedItem_CountsProbePending` / `_FailedProbeBelowMax_CountsProbePending` / `_ExhaustedProbe_CountsProbeExhausted` / `_UnsharedContent_CountsNoAccess` / `_DeletedMaterial_MarksMissing` / `_GenreResolvingToNothing_MarksMissing` / `_InactiveRule_StillDiagnosed` / `_NonAdmin_Throws` — Referenz für Admin-Gate-Tests (`ForbiddenAccessException`, `UnauthorizedAccessException`)

### `TVSenderAdminE2ETests` (`VideoWebPlayer.Tests/TVSenderAdminE2ETests.cs`, Playwright)
- `Admin_CreateEditDeactivateDeleteRule` — konsolidierter Lebenszyklus inkl. Diagnose-Spalte, „Jetzt generieren", Sendeplan-Chips (bekannter Flake: FillAsync-Timing)
- `Admin_GenreRule_MultiSelectAndSourceFilter` — Genre-Mehrfachauswahl + Quell-Filter
- `NonAdmin_SeesNotAuthorized` — Admin-Gate der Seite

## Hilfsmethoden

### `ProgramScheduleTestDb` (`VideoWebPlayer.Tests/Helpers/ProgramScheduleTestDb.cs`)
- `CreateAsync(dbName, ct)` — shared in-memory SQLite mit `EnsureCreated`
- `CreateService()` / `CreateGuideService()` — Service-Instanzen über dem Kontext
- `AddUserAsync(email, isAdmin)` — Benutzer mit `IsAdmin`-Flag (für Admin-Gate-Tests)
- `EnsureSourceAsync(id)` / `GrantSourceAccessAsync` / `CreateMediaItemAsync(sourceId, duration)` (setzt `DurationProbedAt = UtcNow`) / `CreateMovieAsync` / `CreateShowAsync` / `CreateGenreAsync` / `CreateRuleAsync` / `GetChannelAsync`
- **Hinweis für eigene Tests:** `CreateMediaItemAsync` erzeugt `.mkv`-Items mit gesetztem
  `DurationProbedAt`; „nie geprüft" muss durch `DurationProbedAt = null` + Save hergestellt
  werden (Muster in `Diagnosis_UnprobedItem_CountsProbePending`).

### `PlaylistsE2ETestBase` (`VideoWebPlayer.Tests/Helpers/PlaylistsE2ETestBase.cs`)
- `WebApplicationFactory<Program>` + Kestrel auf `127.0.0.1:0`, eigene SQLite-Datei je Test
- `LoginAsync`, `MakeUserAdminAsync`, `GrantMediaSourceAccessForUserAsync`, `SeedTvShowWithSeasonsAsync`, `DriveUntilStateAsync`, `Page`, `Services`, `SkipBrowser`
