# Test-Ergebnisse

## Ergebnis

**Status:** Keine Fehler

## E2E-Abdeckung

| Szenario | Test / Testklasse | Ergebnis |
|----------|-------------------|----------|
| Einzel-Reset aus der geöffneten Fehlerliste: Zeilen-Button klicken → Item verlässt `tvsender-probe-failed-detail`, `tvsender-probe-failed`-Chip zählt herunter, `tvsender-probe-pending`-Chip zählt hoch, Statusmeldung erscheint | `TVSenderAdminE2ETests.Admin_ProbeReset_SingleItemAndAllFailed` (eigener `[Fact]`, `TVSenderAdminE2ETests.cs:352`) | Bestanden [10 s] |
| Massen-Reset mit Bestätigungsdialog: „Alle zurücksetzen" → `admin-dialog` erscheint → Bestätigen → `Failed`-Chip „0 fehlgeschlagen", Fehlerliste entfällt | `TVSenderAdminE2ETests.Admin_ProbeReset_SingleItemAndAllFailed` (zweiter Abschnitt, zweites Failed-Seed `e2e-defekt-2.mkv`) | Bestanden |
| Dialog-Variante „Abbrechen" (laut plan-check.md bewusst nur per bUnit, kein E2E geplant) | `TVSenderAdminTests.ProbeResetAll_ConfirmDialog_ResetsAllAndShowsStatus` (bUnit) | Bestanden (bUnit; E2E nicht geplant — vertretbar laut Plan-Gegenprüfung) |

Beide geplanten E2E-Szenarien (Einzel-Reset, Massen-Reset mit Dialog) sind im eigenen `[Fact]` `Admin_ProbeReset_SingleItemAndAllFailed` enthalten und bestanden. `Admin_CreateEditDeactivateDeleteRule` läuft unverändert ebenfalls grün.

## Zusammenfassung

- Gesamt: 2224
- Bestanden: 2223
- Fehlgeschlagen: 0
- Übersprungen: 1

Ausgeführter Befehl (Arbeitsverzeichnis = Repository-Root `D:\Repositories\softwareschmiede\3c36e985-0c38-4ded-ae2e-409eb0645be8`, Exit-Code 0, Laufzeit 5,39 min):

```
dotnet test VideoWebPlayer.Tests/VideoWebPlayer.Tests.csproj --collect:"XPlat Code Coverage" --logger "console;verbosity=normal" --results-directory TestResults
```

Vorheriger Build: `dotnet build VideoPlayer.sln` — 0 Fehler, 0 Warnungen (Debug), Exit-Code 0.

Der eine übersprungene Test (`DevicePlaylistE2ETests_Playback.PairedDevice_ReportsProgressForMovieWithoutCollection_ContinueWatchingListStillLoads`) trägt ein dokumentiertes `[Fact(Skip = …)]`: Er sichert einen bekannten, auftragsfremden Befund ab (`api/continue-watching` antwortet mit 500, wenn ein Film zu keiner Filmsammlung gehört — `ContinueWatchingService.Create` ohne Null-Prüfung). Kein Zusammenhang mit diesem Schritt.

Auftragsbezogene Testnachweise (alle bestanden): `MediaDurationProbeServiceTests_Reset` (9 Tests inkl. Admin-Erzwingung, Failed-Guard, Kappungs-Test > 25 Items, Wiederaufnahme via `ProbePendingAsync_PicksUpResetItem`), `MediaDurationProbeServiceTests_Progress.GetProgressAsync_DetailItems_CarryMediaItemId`, `TVSenderAdminTests.ProbeFailedRow_ResetButton_ResetsItemAndRefreshes`, `TVSenderAdminTests.ProbeResetAll_ConfirmDialog_ResetsAllAndShowsStatus` sowie der E2E `TVSenderAdminE2ETests.Admin_ProbeReset_SingleItemAndAllFailed`. Dritte Runde nach den Review-Fixes (nur Testdateien betroffen) — keine Einzelwiederholungen erforderlich, da keine Fehlschläge auftraten.

## Testabdeckung

**Abdeckung:** 95 % (Zeilenabdeckung, XPlat Code Coverage / Cobertura: 142 927 von 150 475 Zeilen)

| Datei | Abdeckung |
|-------|-----------|
| VideoWebPlayer.Client/Models/DtoRecentEntry.cs | 0,0 % |
| VideoWebPlayer.Client/Models/ImpersonateRequest.cs | 0,0 % |
| VideoWebPlayer/Components/Pages/Actors/ActorDetails.razor | 0,0 % |
| VideoWebPlayer/Components/Pages/Actors/Actors.razor | 0,0 % |
| VideoWebPlayer/Components/Pages/Admin/MediaSources/MediaSourceExplorer.razor | 0,0 % |
| VideoWebPlayer/Components/Pages/Admin/UserManagement.razor | 0,0 % |
| VideoWebPlayer/Components/Pages/Errors/Error.razor | 0,0 % |
| VideoWebPlayer/Components/Shared/Media/MediaBaseEntryList.razor | 0,0 % |
| VideoWebPlayer/Components/Shared/Media/WatchedIndicator.razor | 0,0 % |
| VideoWebPlayer/Controllers/Attributes/ConnectionCheckAttribute.cs | 0,0 % |
| VideoWebPlayer/Hubs/MediaUpdateHub.cs | 0,0 % |
| VideoWebPlayer/Services/DemoData/IDemoDataSetService.cs | 0,0 % |
| VideoWebPlayer/Services/SftpStreamWrapper.cs | 0,0 % |
| VideoWebPlayer/Utils/LocalNetworkHelper.cs | 0,0 % |
| VideoWebPlayer/Services/SftpMediaSourceReader.cs | 11,4 % |
| VideoWebPlayer/Components/Shared/Media/ActorList.razor | 15,8 % |
| VideoWebPlayer/Services/DemoData/FileSystemDemoDataSetService.cs | 24,3 % |
| VideoWebPlayer/Services/InternalConnectionService.cs | 25,0 % |
| VideoWebPlayer/Components/Shared/Home/SeasonalGenreList.razor | 26,5 % |
| VideoWebPlayer/Components/Pages/TV/TVShowDetails.razor | 26,6 % |
| VideoWebPlayer/Services/Backups/ManualBackupJobService.cs | 29,5 % |
| VideoWebPlayer/Components/Shared/Home/RecentEntriesList.razor | 31,2 % |
| VideoWebPlayer/Controllers/Exceptions/ForbiddenAccessException.cs | 33,3 % |
| VideoWebPlayer/Controllers/Exceptions/RecordNotFoundException.cs | 33,3 % |
| VideoWebPlayer/Services/Exceptions/ContinueWatchingConfirmationRequiredException.cs | 33,3 % |
| VideoWebPlayer/Services/Exceptions/DiscoveryUrlValidationException.cs | 33,3 % |
| VideoWebPlayer/Services/Exceptions/ManualSortOrderConfirmationRequiredException.cs | 33,3 % |
| VideoWebPlayer/Services/Exceptions/PlaylistAccessDeniedException.cs | 33,3 % |
| VideoWebPlayer/Services/Exceptions/PlaylistNameAlreadyExistsException.cs | 33,3 % |
| VideoWebPlayer/Services/Exceptions/PlaylistNotInManualSortModeException.cs | 33,3 % |
| VideoWebPlayer/Services/Exceptions/UploadedCoverReplacementConfirmationRequiredException.cs | 33,3 % |
| VideoWebPlayer/Controllers/SourceGenresController.cs | 34,2 % |
| VideoWebPlayer/Controllers/FavoritesController.cs | 39,2 % |
| VideoWebPlayer/Services/FavoritesService.cs | 39,8 % |
| VideoWebPlayer/Components/Pages/Admin/Backups.razor | 43,5 % |
| VideoWebPlayer/Services/MdnsAdvertiserWorker.cs | 44,1 % |
| VideoWebPlayer/Components/Pages/Admin/MediaSources/MediaSourceAdmin.razor | 47,5 % |
| VideoWebPlayer/Services/RecentEntryService.cs | 49,3 % |
| VideoWebPlayer/Controllers/MediaFileContentTypes.cs | 50,0 % |
| VideoWebPlayer/Data/GenreName.cs | 50,0 % |
| VideoWebPlayer/Data/MediaSourceUser.cs | 50,0 % |
| VideoWebPlayer/Data/MovieGenre.cs | 50,0 % |
| VideoWebPlayer/Data/TVShowGenre.cs | 50,0 % |
| VideoWebPlayer/Components/Playlists/PlaylistGenreEditor.razor | 50,9 % |
| VideoWebPlayer/Services/Updates/VideoWebPlayerUpdateSourceFactory.cs | 52,0 % |
| VideoWebPlayer/Components/Pages/Admin/Security.razor | 54,3 % |
| VideoWebPlayer/Data/TVShowEpisode.cs | 55,6 % |
| VideoWebPlayer/Services/Backups/RestoreInProgressMiddleware.cs | 56,7 % |
| VideoWebPlayer/Components/Pages/Movies/MovieCollectionDetails.razor | 57,4 % |
| VideoWebPlayer/Components/Pages/Admin/MediaSources/MediaSourceAdminDetails.razor | 57,4 % |
| VideoWebPlayer/Controllers/AdminSourcesController.cs | 57,7 % |
| VideoWebPlayer/Controllers/PicturesController.cs | 57,7 % |
| VideoWebPlayer/Components/Shared/Home/FavoritesList.razor | 58,2 % |
| VideoWebPlayer/Services/MediaUpdateNotificationService.cs | 58,7 % |
| VideoWebPlayer/Services/GenreService.cs | 58,8 % |
| VideoWebPlayer/Components/Pages/MediaSources/MediaSourceDetails.razor | 59,6 % |
| VideoWebPlayer/Services/EpisodeBackgroundImage/EpisodeBackgroundImageOptionsValidator.cs | 60,0 % |
| VideoWebPlayer/Controllers/UnlockedMediaController.cs | 60,4 % |
| VideoWebPlayer/Services/Backups/VideoWebPlayerBackupFacade.cs | 61,4 % |
| VideoWebPlayer/Data/Genre.cs | 61,5 % |
| VideoWebPlayer/Services/WhitelistIpMiddleware.cs | 61,5 % |
| VideoWebPlayer/Components/Pages/Admin/GenreAdmin.razor | 62,4 % |
| VideoWebPlayer/Data/Entities/WatchedEntry.cs | 62,5 % |
| VideoWebPlayer/Controllers/ActorsController.cs | 64,5 % |
| VideoWebPlayer/Services/Backups/VideoWebPlayerBackupDataFactory.cs | 64,7 % |
| VideoWebPlayer/Services/LiveStreamWorker.cs | 64,8 % |
| VideoWebPlayer/Services/ProgramGuideEntryText.cs | 65,0 % |
| VideoWebPlayer/Controllers/SourceIconsController.cs | 65,5 % |
| VideoWebPlayer/Data/ApplicationDbContext.cs | 65,8 % |
| VideoWebPlayer/Data/UnlockedMediaEntry.cs | 66,7 % |
| VideoWebPlayer.Client/VideoWebPlayerClient.cs | 67,9 % |
| VideoWebPlayer/Controllers/SourcesController.cs | 68,4 % |
| VideoWebPlayer/Extensions/LocalConfigurationExtensions.cs | 70,3 % |
| VideoWebPlayer/Controllers/BackupsController.cs | 72,5 % |
| VideoWebPlayer.Client/Models/DtoSource.cs | 72,7 % |
| VideoWebPlayer/Controllers/AuthController.cs | 72,7 % |
| VideoWebPlayer/Components/Shared/Media/VideoPlayer.razor | 73,3 % |
| VideoWebPlayer/Components/Pages/Admin/TVSenderAdmin.razor | 74,8 % |
| VideoWebPlayer/Data/Entities/ContinueWatchingEntry.cs | 75,0 % |
| VideoWebPlayer/Components/Shared/Media/UnlockButton.razor | 75,0 % |
| VideoWebPlayer/Components/Playlists/PlaylistSortModeIcon.razor | 75,0 % |
| VideoWebPlayer/Services/IProgramScheduleService.cs | 75,0 % |
| VideoWebPlayer/Services/DiscoveryBaseUrlResolver.cs | 75,5 % |
| VideoWebPlayer/Services/Backups/VideoWebPlayerBackupData.cs | 75,6 % |
| VideoWebPlayer/Services/Updates/UpdateSettingsInitializer.cs | 76,5 % |
| VideoWebPlayer/Components/Layout/NavMenu.razor | 77,0 % |
| VideoWebPlayer/Services/Authentication/IAuthService.cs | 77,1 % |
| VideoWebPlayer/Services/ActorBackfillWorker.cs | 77,3 % |
| VideoWebPlayer/Data/ApplicationDbContext.PlaylistBackfillMarking.cs | 77,6 % |
| VideoWebPlayer/Controllers/ItemsController.cs | 77,7 % |
| VideoWebPlayer/Components/Pages/Admin/AdminIndex.razor | 77,8 % |
| VideoWebPlayer/Services/StaticAssetVersioner.cs | 77,8 % |
| VideoWebPlayer/Services/WatchedStatusService.cs | 78,9 % |
| VideoWebPlayer/Controllers/ProgramGuideController.cs | 78,9 % |
| VideoWebPlayer/Services/PlaylistBackfillWorker.cs | 79,1 % |
| VideoWebPlayer/Services/HomeBackgroundImage/HomeBackgroundImageGenerator.cs | 79,2 % |
| VideoWebPlayer/Services/ContinueWatchingWorker.cs | 79,5 % |
| VideoWebPlayer/Components/Pages/Admin/ProgramSettings.razor | 79,8 % |

Nicht aufgeführt (ignoriert gemäß Vorgabe): generierte Dateien (`VideoWebPlayer/Migrations/*`, `*.Designer.cs`), Identity-Scaffolding (`Components/Account/*`), Sample-Seiten (`Components/Pages/Samples/*`) und der Einstiegspunkt `Program.cs`. Pro Datei sind die Zeilen aller darin liegenden Klassen zusammengeführt (dedupliziert nach Zeilennummer, Zeile gilt als abgedeckt, wenn sie in mindestens einer Klasse getroffen wurde).

Für diesen Schritt geänderte/berührte Dateien liegen ausreichend abgedeckt vor: `MediaDurationProbeService.cs` 95,8 %, `MediaDurationProbeProgress.cs` 100 %, `MediaDurationProbeWorker.cs` 82,7 %. `TVSenderAdmin.razor` (74,8 %; Vorrunden: 76,1 % / 74,6 % — normaler E2E-Streuungsbereich) ist eine sehr große Datei mit vielen auftragsfremden Pfaden; die neuen Reset-Pfade sind in Service-, bUnit- und E2E-Tests abgedeckt.

## Fehlende Tests

Quelle: Coverage-Daten (Dateien mit 0 % Zeilenabdeckung, generierte Dateien ausgenommen)

- `VideoWebPlayer/Controllers/Attributes/ConnectionCheckAttribute.cs` — kein Test für das Filter-Attribut
- `VideoWebPlayer/Utils/LocalNetworkHelper.cs` — kein Test für die Netzwerk-Hilfsmethoden
- `VideoWebPlayer/Services/SftpStreamWrapper.cs` — SFTP-Stream-Wrapper ohne Abdeckung (Integration schwer testbar)
- `VideoWebPlayer/Hubs/MediaUpdateHub.cs` — SignalR-Hub ohne Abdeckung
- `VideoWebPlayer/Components/Pages/Errors/Error.razor` — Fehlerseite ohne Abdeckung
- `VideoWebPlayer/Components/Pages/Admin/UserManagement.razor` — Admin-Seite ohne bUnit-/E2E-Abdeckung
- `VideoWebPlayer/Components/Pages/Admin/MediaSources/MediaSourceExplorer.razor` — Explorer-Seite ohne Abdeckung
- `VideoWebPlayer/Components/Pages/Actors/ActorDetails.razor` und `Components/Pages/Actors/Actors.razor` — Actor-Seiten ohne Abdeckung
- `VideoWebPlayer/Components/Shared/Media/MediaBaseEntryList.razor` und `WatchedIndicator.razor` — Shared-Komponenten ohne Abdeckung
- `VideoWebPlayer.Client/Models/DtoRecentEntry.cs` und `ImpersonateRequest.cs` — Client-DTOs ohne Abdeckung

Alle genannten Lücken sind auftragsfremd (Bestand, nicht Teil dieses Schritts); sie werden als Abdeckungslücken gemeldet, erfordern aber keine Nachbesserung im Rahmen von Issue #258.
