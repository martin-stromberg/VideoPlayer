# Test-Ergebnisse

Zeitpunkt: 2026-10-25, Builds ~14:07 Uhr, Testläufe ~14:12–14:19 Uhr (MEZ). Getesteter Stand: Implementierung Iteration 3 uncommittet im Working Tree (Baseline-Commit `3bbbd49` + Plan-Commit `e6f281d` + uncommittete Änderungen; u. a. neu `Helpers/TVSenderAdminE2ETestBase.cs` — die drei TV-Sender-E2E-Klassen erben jetzt davon —, Validierungsverschärfungen in `ProgramClipGroupService`/`ProgramRuleService.ValidateClipGroupInputAsync`, `FillAiringSegments`-Signaturänderung, UI-Hinweise Grace-Feld in `TVSenderAdminClipGroups.razor`/`TVSenderAdmin.razor`, drei neue Tests).

## Ergebnis

**Status:** Fehler vorhanden

Begründung: Ein einmaliger Testfehlschlag im Gesamtlauf (Bestand-E2E außerhalb des Features, in beiden Einzelwiederholungen bestanden → Flake, getrennt dokumentiert unten). **Alle geplanten E2E-Pflichtszenarien existieren und haben bestanden.** Die E2E-Umstellung auf die neue Basisklasse `TVSenderAdminE2ETestBase` hat keine Bestandstests gebrochen: Alle 11 Tests der drei erbenden Klassen (`TVSenderAdminE2ETests` 4, `TVSenderAdminE2ETests_FfmpegDownload` 2, `TVSenderAdminClipGroupsE2ETests` 5) sind grün.

## Ausgeführte Befehle

| Lauf | Befehl | Ergebnis |
|------|--------|----------|
| Build Debug | `dotnet build VideoPlayer.sln` (Repo-Root) | 0 Fehler, 0 Warnungen (inkrementell, 1,5 s) |
| Build Release | `dotnet build VideoPlayer.sln -c Release` | 0 Fehler, 0 Warnungen (inkrementell, 1,4 s) |
| 1 (volle Suite + Coverage) | `dotnet test VideoWebPlayer.Tests/VideoWebPlayer.Tests.csproj --no-build --collect:"XPlat Code Coverage" --logger "console;verbosity=normal" --logger "trx;LogFileName=full-coverage-iter3.trx" --results-directory docs/features/task/issue-277-…/test-results` | 2.346 gesamt: 2.343 bestanden, 1 fehlgeschlagen, 2 übersprungen (~5,8 min) |
| 2 (Wiederholung 1) | `dotnet test … --no-build --filter "FullyQualifiedName~PlaylistMediaSearchE2ETests.Search_NoMatches_ShowsEmptyResultsMessage" --logger "trx;LogFileName=rerun1-mediasearch-empty.trx"` | 1 bestanden (9 s) |
| 3 (Wiederholung 2) | dto. `--logger "trx;LogFileName=rerun2-mediasearch-empty.trx"` | 1 bestanden (9 s) |

Der volle Lauf ohne Filter schließt die bekannte E2E-Trait-Lücke ein (`TVSenderAdminClipGroupsE2ETests`, `TVSenderAdminE2ETests`, `TVSenderAdminE2ETests_FfmpegDownload` tragen — auch über die neue Basisklasse `TVSenderAdminE2ETestBase` — kein `Category=E2E`-Trait und würden bei `Category!=E2E`-Filterläufen mitlaufen; hier unproblematisch, da ungefiltert).

Nachweise: [TRX Gesamtlauf](test-results/full-coverage-iter3.trx), [Konsolenlog](test-results/full-coverage-iter3.log), [TRX Rerun 1](test-results/rerun1-mediasearch-empty.trx), [TRX Rerun 2](test-results/rerun2-mediasearch-empty.trx), Coverage: `test-results/4b7d6fee-b384-4a67-9b2f-eb6bd441b6d5/coverage.cobertura.xml`.

## Fehlgeschlagene Tests

### PlaylistMediaSearchE2ETests (Bestand, `VideoWebPlayer.Tests/PlaylistMediaSearchE2ETests.cs`)

- **Search_NoMatches_ShowsEmptyResultsMessage** — `PlaywrightException`: `Locator(".media-search-empty")` innerhalb 5 s nicht sichtbar; der Aria-Snapshot zeigt die fertig gerenderte Suchseite inkl. Suchfeld — die Leere-Ergebnis-Meldung war zum Prüfzeitpunkt noch nicht erschienen (timing-/lastbedingt). Fehlgeschlagen nur im Gesamtlauf, **bestanden in Wiederholung 1 und 2 → Flake**. Neu beobachtet: nicht in der `inventory/tests.md`-Liste dokumentierter preexisting-Flakes, auch nicht in den Iteration-1/2-Läufen geflackt. Liegt außerhalb des Features (Mediensuche in Playlists, keine Berührung mit Clip-Gruppen-Code).

Keiner der dokumentierten Flakes (`FfmpegLiveStreamSegmenterE2ETests.SecondJob_PlaylistContinuesAndMarksDiscontinuity`, `BackupUploadE2ETests.BackupUpload_NavigatingAwayDuringUpload_AbortsWithoutRedirectBack`, `PlaylistPlaybackE2ETests.PlaylistSkipCollectionEntriesE2ETest`, `LiveTvE2ETests.LiveTv_ProgramGap_ShowsFriendlyHintAndUpcoming`) ist in diesem Lauf fehlgeschlagen.

## E2E-Abdeckung

Geplante Pflicht-Szenarien aus `plan.md` (Abschnitt „E2E-Tests") und `plan-check.md` (Abschnitt „E2E-Abdeckung"); alle Ergebnisse aus dem ungefilterten Gesamtlauf (`full-coverage-iter3.trx`):

| Szenario | Test / Testklasse | Ergebnis |
|----------|-------------------|----------|
| Gruppen-CRUD (anlegen, umbenennen, löschen); reservierte „Trailer"-Gruppe sichtbar und nicht bearbeitbar; Name „Trailer" abgewiesen (AK 1, 2) | `TVSenderAdminClipGroupsE2ETests.ClipGroupsTab_CreateRenameDeleteGroup` + `ClipGroupsTab_TrailerGroupVisible_ButProtected` | Bestanden |
| Clip-Zuordnung über Kandidaten-Auswahl, „keine Laufzeit"-Kennzeichnung, Entfernen (AK 1) | `TVSenderAdminClipGroupsE2ETests.ClipGroupsTab_AssignAndRemoveClipViaCandidates` | Bestanden |
| Regel mit ≥ 1 Gruppe (inkl. „Trailer") + Karenzzeit anlegen/bearbeiten/speichern; Sichtbarkeit in Regel-Liste (AK 2, 3) | `TVSenderAdminClipGroupsE2ETests.RuleEditor_ShowsClipGroupSelectionAndGraceField` | Bestanden |
| Sendeplan zeigt Clip-Slots nach „Jetzt generieren" mit `MediaItem.Name`-Fallback (AK 4) | `TVSenderAdminClipGroupsE2ETests.GeneratedSchedule_ContainsVisibleClipSlots` | Bestanden |
| Nicht-Admin-Zugriff auf neue Verwaltungsfläche | `TVSenderAdminE2ETests.NonAdmin_SeesNotAuthorized` (Bestand, laut plan-check „nicht erforderlich mit Begründung") | Bestanden |
| Sichtbarkeitsregeln im Regel-Dialog (`rule-clip-groups`/`rule-grace-minutes` nur bei `RuleType != Filler`/`SlotDuration`) | bUnit `TVSenderAdminTests_ClipGroups.RuleEditor_ShowsClipGroupSelection_GraceField_AndMaterialLabelSuffix` (plan-check: „Teil des Regel-Dialog-E2E, zusätzlich per bUnit abgesichert") | Bestanden |

Regression der Basisklassen-Umstellung (Iteration 3): Alle übrigen Tests der drei erbenden E2E-Klassen bestanden — `TVSenderAdminE2ETests`: `Admin_CreateEditDeactivateDeleteRule`, `Admin_ProbeReset_SingleItemAndAllFailed`, `Admin_GenreRule_MultiSelectAndSourceFilter` (inkl. der angepassten 5-Tab-Assertion mit neuem „Clip-Gruppen"-Tab); `TVSenderAdminE2ETests_FfmpegDownload`: `Admin_DownloadsFfmpegTools_WhenMissing`, `Admin_DownloadFails_ShowsErrorAlert`.

## Zusammenfassung

- Gesamt: 2.346 (voller Lauf, ungefiltert)
- Bestanden: 2.343
- Fehlgeschlagen: 1 (in beiden Einzelwiederholungen bestanden → Flake, Bestand-Test außerhalb des Features)
- Übersprungen: 2 (beide preexisting/dokumentiert: `DevicePlaylistE2ETests_Playback.PairedDevice_ReportsProgressForMovieWithoutCollection_…` — ContinueWatching-500-Befund; `FfmpegToolDownloadServiceTests_Install.Run_OnUnix_SetsExecutableBit` — plattformbedingt)

Feature-Tests (alle im Gesamtlauf bestanden): `ProgramScheduleServiceTests_ClipGroups` (19, inkl. aller geplanten `EnsureCoverage_*`-/`Reconcile_*`-Fälle), `ProgramClipGroupServiceTests` (9, darunter neu `AddClips_UnknownItem_Rejected`), `ProgramClipGroupTests` (5, echtes SQLite: Kaskaden/NOCASE-Unique/Composite-PK), `ProgramRuleServiceTests_ClipGroups` (7, darunter neu `NoSlotDuration_RejectsGrace`), `TVSenderAdminTests_ClipGroups` (5, bUnit, darunter neu `CandidateList_Empty_ExplainsSelectionRule`), `VideoWebPlayerBackupDataTests_ClipGroups` (2, Alt-Backup-Regression + Roundtrip), `ProgramGuideServiceTests_ClipGroups` (1), `TVSenderAdminClipGroupsE2ETests` (5, s. E2E-Abdeckung). Summe: 53 Feature-Tests (Iteration 2: 50 + 3 neue), alle grün.

Steuernachweis Verträglichkeit (AK 5): `ProgramScheduleServiceTests_Boundary` (12) und `_Generation` unverändert grün — Regeln ohne Gruppen verhalten sich unverändert.

## Testabdeckung

**Abdeckung:** 95,2 % Zeilen (152.018/159.629), 66,4 % Branches (Cobertura, `test-results/4b7d6fee-b384-4a67-9b2f-eb6bd441b6d5/coverage.cobertura.xml`)

Feature-relevante Dateien (pro Datei über alle Klassen aggregiert):

| Datei | Abdeckung |
|-------|-----------|
| `VideoWebPlayer/Components/Pages/Admin/TVSenderAdminClipGroups.razor` | 85,9 % (330/384) |
| `VideoWebPlayer/Components/Pages/Admin/TVSenderAdmin.razor` | 77,6 % (1.590/2.048) |
| `VideoWebPlayer/Services/ProgramClipGroupService.cs` | 94,4 % (338/358) |
| `VideoWebPlayer/Services/IProgramClipGroupService.cs` | 80,0 % (24/30, Interface-Default-Implementierungen) |
| `VideoWebPlayer/Services/ProgramScheduleService.cs` | 92,8 % (3.292/3.548) |
| `VideoWebPlayer/Services/ProgramRuleService.cs` | 94,9 % (480/506) |
| `VideoWebPlayer/Services/ProgramRuleInput.cs` | 100 % |
| `VideoWebPlayer/Services/ProgramGuideService.cs` | 100 % |
| `VideoWebPlayer/Services/Backups/VideoWebPlayerBackupData.cs` | 77,0 % (1.258/1.634) |
| `VideoWebPlayer/Services/Backups/VideoWebPlayerBackupDataFactory.cs` | 64,7 % (22/34) |
| `VideoWebPlayer/Services/IProgramScheduleService.cs` | 75,0 % (36/48) |
| `VideoWebPlayer/Migrations/20261025112747_AddProgramClipGroups.cs` | 88,7 % (172/194, Down-Pfad ungetestet) |
| `VideoWebPlayer/Data/ApplicationDbContext.cs` | 66,1 % (472/714, überwiegend Bestand-Pfade) |
| `VideoWebPlayer/Data/ProgramClipGroup.cs`, `…Item.cs`, `ProgramRuleClipGroup.cs`, `ProgramRule.cs`, alle `*Configuration.cs` (inkl. der drei neuen), `…Designer.cs` | 100 % |

Projektweit liegen 187 von 582 Dateien unter 80 % (überwiegend Bestand: Account-/Sample-Seiten, UI-Komponenten); die Tabelle zeigt die feature-relevanten Dateien inkl. derer unter 80 %.

## Fehlende Tests

Quelle: `Coverage-Daten` + `Dateinamen-Konvention`

- Alle neuen Feature-Quelldateien haben korrespondierende Testdateien (`ProgramClipGroupServiceTests`, `ProgramScheduleServiceTests_ClipGroups`, `ProgramRuleServiceTests_ClipGroups`, `TVSenderAdminTests_ClipGroups`, `VideoWebPlayerBackupDataTests_ClipGroups`, `ProgramGuideServiceTests_ClipGroups`, `TVSenderAdminClipGroupsE2ETests`); die neue Hilfsklasse `Helpers/TVSenderAdminE2ETestBase.cs` ist reine Testinfrastruktur und wird durch die 11 grünen Tests der drei erbenden Klassen verifiziert.
- Projektweit 41 Dateien mit 0 % Zeilenabdeckung (Bestand, nicht feature-bezogen — u. a. `Components/Account/Pages/**`, `Components/Pages/Samples/**`, `Components/Pages/Actors/**`, `Components/Pages/Admin/UserManagement.razor`, `Components/Pages/Admin/MediaSources/MediaSourceExplorer.razor`, `Components/Shared/Media/MediaBaseEntryList.razor`, `Components/Shared/Media/WatchedIndicator.razor`, `VideoWebPlayer.Client/Models/DtoRecentEntry.cs`, `…/ImpersonateRequest.cs`, `Services/SftpStreamWrapper.cs`, `Services/DemoData/IDemoDataSetService.cs`, `Hubs/MediaUpdateHub.cs`, `Controllers/Attributes/ConnectionCheckAttribute.cs`, `Utils/LocalNetworkHelper.cs`); vollständige Liste im Coverage-XML.
- Kein Feature-relevanter Code mit 0 % Abdeckung.
