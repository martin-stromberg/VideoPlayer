# Tasks: TV-Sender — Einstellung „Inhaltsspektrum" (Freigabe-Prüfung der Planung optional aufheben)

| # | Bereich | Aufgabe | Status | Testnachweis |
|---|---------|---------|--------|--------------|
| 1 | Datenmodell | Enum `ProgramScheduleContentScope` in `VideoWebPlayer/Data/ProgramScheduleContentScope.cs` anlegen (`SharedOnly = 0`, `AllContent = 1`) | Offen | — |
| 2 | Datenmodell | `Setup.ProgramScheduleContentScope`-Eigenschaft mit Initialisierer `SharedOnly` hinzufügen | Offen | — |
| 3 | Datenmodell | EF-Migration `AddSetupProgramScheduleContentScope` erzeugen (`Setups.ProgramScheduleContentScope`, `INTEGER`, `NOT NULL`, `defaultValue: 0`) inkl. Model-Snapshot | Offen | — |
| 4 | Logik | `ProgramSettingsService.GetProgramScheduleContentScopeAsync` hinzufügen (Normalisierung ungültiger Werte auf `SharedOnly`) | Offen | — |
| 5 | Logik | `ProgramSettingsService.UpdateProgramScheduleContentScopeAsync` hinzufügen (`Enum.IsDefined`-Validierung → `ArgumentException`; `GetOrCreateSetupAsync` + `SaveChangesAsync`) | Offen | — |
| 6 | Logik | `ProgramScheduleService.LoadAccessContextAsync` um die Scope-Auswertung erweitern (`AllContent` → früh `Everything`-`AccessContext`; `SharedOnly` unverändert) | Offen | — |
| 7 | Persistenz/Backup | `VideoWebPlayerBackupData.OptionalRestoreColumns`: Eintrag `Setups.ProgramScheduleContentScope` | Offen | — |
| 8 | Persistenz/Backup | `VideoWebPlayerBackupData.OptionalRestoreIntDefaults`: Eintrag `(Setups, ProgramScheduleContentScope, (int)SharedOnly)` | Offen | — |
| 9 | UI | `TVSenderAdmin.razor`: Feld `contentScope` + Laden via `GetProgramScheduleContentScopeAsync` in `OnInitializedAsync` | Offen | — |
| 10 | UI | `TVSenderAdmin.razor`: `select` `#tvsender-content-scope` mit zwei Klartext-Optionen + Hinweistext `#tvsender-content-scope-hint` (`form-text`) in der Unterkarte „Planung" | Offen | — |
| 11 | UI | `TVSenderAdmin.razor`: `SaveChannelSettingsAsync` um `UpdateProgramScheduleContentScopeAsync(contentScope)` und `ScheduleSignal.NotifyScheduleChanged()` erweitern | Offen | — |
| 12 | Tests | `ProgramSettingsServiceContentScopeTests` anlegen: `GetProgramScheduleContentScope_DefaultsToSharedOnly`, `UpdateProgramScheduleContentScope_Roundtrip`, `UpdateProgramScheduleContentScope_InvalidValue_Throws` | Offen | — |
| 13 | Tests | `ProgramScheduleServiceTests_ContentScope` anlegen: `EnsureCoverage_AllContent_SchedulesUnreleasedMovieAndEpisode`, `EnsureCoverage_AllContent_SchedulesClipSourceItems`, `EnsureCoverage_AllContent_SchedulesClipGroupItems`, `EnsureCoverage_AllContent_SchedulesTrailers` | Offen | — |
| 14 | Tests | `ProgramScheduleServiceTests_ContentScope`: `EnsureCoverage_SharedOnlyDefault_StillFiltersUnreleased`, `EnsureCoverage_ScopeSwitch_TakesEffectNextRun`, `Reconcile_SwitchBackToSharedOnly_DisplacesUnreleasedSlots`, `GetRuleDiagnoses_AllContent_ReportsNoAccessExclusions` | Offen | — |
| 15 | Tests | `VideoWebPlayerBackupDataTests_ProgramScheduleContentScope` anlegen: `ReadFromAsync_LegacyBackupWithoutContentScopeColumn_RestoresSharedOnly` (Alt-Backup-Regression) | Offen | — |
| 16 | Tests | `TVSenderAdminTests_ContentScope` (bUnit) anlegen: `ContentScope_RendersOptionsAndHint_DefaultsToSharedOnly`, `ContentScope_SavePersistsSelection` | Offen | — |
| 17 | E2E-Tests | `TVSenderAdminE2ETests.Admin_ContentScope_SelectAndPersist`: Auswahl + Hinweistext + DB-Persistenz über die echte Seite | Offen | — |
| 18 | E2E-Tests | `TVSenderAdminE2ETests.Admin_ContentScope_AllContent_SchedulesUnreleased`: nicht freigegebener Film wird erst nach Umschalten eingeplant und im Sendeplan-Tab angezeigt (darf mit #17 in einem Browser-Run kombiniert werden) | Offen | — |
| 19 | Dokumentation | `docs/help/tv-sender.md` aktualisieren (Planungs-Gruppe Z. 104–109, Freigabe-Aussage Z. 125–129, Clip-Quelle Z. 150–164, Clip-Gruppen Z. 191–202, Diagnose Z. 313–317) | Offen | — |
| 20 | Dokumentation | `docs/help/tv-sender-ablauf-technisch.md` und `docs/help/tv-sender-datenmodell.md` aktualisieren (`access.Allows`-Erwähnung Z. 82; neue `Setup`-Spalte in der Tabelle Z. 111–121) | Offen | — |
| 21 | Dokumentation | `docs/RELEASE_NOTES.md` (EN+DE) ergänzen; `README.md` nur bei Bedarf | Offen | — |
| 22 | Abschluss | `dotnet build VideoPlayer.sln` in Debug und Release fehlerfrei; volle Suite `dotnet test VideoWebPlayer.Tests/VideoWebPlayer.Tests.csproj` grün | Offen | — |
