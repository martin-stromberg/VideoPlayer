# Test-Ergebnisse

## Ergebnis

**Status:** Keine Fehler

Iteration 2 (Stand Commit `9a09d2a`). Build: `dotnet build VideoPlayer.sln -c Debug`
und `-c Release` jeweils 0 Fehler. Volle Testsuite:
`dotnet test VideoWebPlayer.Tests/VideoWebPlayer.Tests.csproj --no-build --collect:"XPlat Code Coverage" --logger "console;verbosity=normal"`
(Laufzeit 5,5 min). Alle Tests beim ersten Lauf bestanden — kein Flake, keine
Wiederholung nötig. Der übersprungene Test ist ein dokumentierter, per
`Skip`-Attribut begründeter Bestandsbefund.

## Zusammenfassung

- Gesamt: 2238
- Bestanden: 2237
- Fehlgeschlagen: 0
- Übersprungen: 1 (dokumentierter `Skip`-Befund, siehe Hinweise)

## Hinweise

- **Übersprungen:** `DevicePlaylistE2ETests_Playback.PairedDevice_ReportsProgressForMovieWithoutCollection_ContinueWatchingListStillLoads`
  — bewusst per `[Fact(Skip = …)]` deaktiviert; dokumentierter Bestandsbefund
  (`api/continue-watching` → HTTP 500 bei Film ohne Filmsammlung), wird laut
  dortigem Vermerk nicht in diesem Lauf behoben.
- **Neuer Test seit Iteration 1 (Commit `9a09d2a`, bestanden, 554 ms):**
  `VideoWebPlayerBackupDataTests_StrandKey.ReadFromAsync_BackupWithNullAndStrandKeyRows_KeepsAllStrandRows`.
  Damit Gesamtzahl 2238 statt 2237.
- **Tests der Anforderung (alle bestanden):**
  `ProgramRuleCursorUniquenessTests` (3), `ProgramRuleCursorNullStrandKeyMigrationTests` (2),
  `ProgramScheduleServiceTests_CursorSelection` (3),
  `VideoWebPlayerBackupDataTests_StrandKey` (u. a. die Dedup-Tests
  `ReadFromAsync_BackupWithDuplicateNullStrandKeyRows_KeepsYoungest`,
  `ReadFromAsync_LegacyBackupWithoutStrandKeyAndDuplicates_KeepsYoungest`,
  `ReadFromAsync_BackupWithNullAndStrandKeyRows_KeepsAllStrandRows` sowie der
  Regressionstest `ReadFromAsync_LegacyBackupWithoutStrandKeyColumn_RestoresSuccessfully`).
- **E2E-Abdeckung:** Keine geplanten E2E-Szenarien — die Anforderung enthält keinen
  Benutzerfluss (Plan/`plan-check.md`: „Nicht erforderlich mit Begründung").
- **Coverage der geänderten Dateien:** `ProgramRuleCursorConfiguration.cs` 100 %,
  `ProgramRuleCursor.cs` 100 %, `ProgramScheduleService.cs` 90,3 %,
  `VideoWebPlayerBackupData.cs` 76,8 %, neue Migration
  `20261024072426_EnforceProgramRuleCursorNullStrandKeyUnique.cs` 82,8 %
  (Up-Pfad im Migrations-Test abgedeckt, Down nicht — erwartbar).
- **Coverage-Blindspot:** Der Coverage-Collector instrumentiert nur den
  Testhost-Prozess. Code, der ausschließlich über Playwright-E2E-Tests (eigener
  Server-Prozess) ausgeführt wird — v. a. Razor-Seiten und Account-Komponenten —,
  erscheint mit 0 % bzw. niedriger Abdeckung, obwohl er funktional getestet ist.
  Ebenso stehen EF-Migrationsdateien nur teilweise unter Abdeckung, weil nur die
  neueste Migration im Migrations-Test läuft.

## Testabdeckung

**Abdeckung:** 95,1 % (145.504/153.032 Zeilen, 568 Dateien; Quelle:
`VideoWebPlayer.Tests/TestResults/48ded1b6-8fc9-4cc3-a1ed-9d847258df05/coverage.cobertura.xml`)

| Datei | Abdeckung |
|-------|-----------|
| `VideoWebPlayer.Client/Models/DtoRecentEntry.cs` | 0,0 % |
| `VideoWebPlayer.Client/Models/ImpersonateRequest.cs` | 0,0 % |
| `VideoWebPlayer/Components/Account/IdentityNoOpEmailSender.cs` | 0,0 % |
| `VideoWebPlayer/Components/Account/Pages/ConfirmEmail.razor` | 0,0 % |
| `VideoWebPlayer/Components/Account/Pages/ConfirmEmailChange.razor` | 0,0 % |
| `VideoWebPlayer/Components/Account/Pages/ExternalLogin.razor` | 0,0 % |
| `VideoWebPlayer/Components/Account/Pages/ForgotPassword.razor` | 0,0 % |
| `VideoWebPlayer/Components/Account/Pages/LoginWith2fa.razor` | 0,0 % |
| `VideoWebPlayer/Components/Account/Pages/LoginWithRecoveryCode.razor` | 0,0 % |
| `VideoWebPlayer/Components/Account/Pages/Manage/ChangePassword.razor` | 0,0 % |
| `VideoWebPlayer/Components/Account/Pages/Manage/DeletePersonalData.razor` | 0,0 % |
| `VideoWebPlayer/Components/Account/Pages/Manage/Disable2fa.razor` | 0,0 % |
| `VideoWebPlayer/Components/Account/Pages/Manage/Email.razor` | 0,0 % |
| `VideoWebPlayer/Components/Account/Pages/Manage/EnableAuthenticator.razor` | 0,0 % |
| `VideoWebPlayer/Components/Account/Pages/Manage/ExternalLogins.razor` | 0,0 % |
| `VideoWebPlayer/Components/Account/Pages/Manage/GenerateRecoveryCodes.razor` | 0,0 % |
| `VideoWebPlayer/Components/Account/Pages/Manage/Index.razor` | 0,0 % |
| `VideoWebPlayer/Components/Account/Pages/Manage/PersonalData.razor` | 0,0 % |
| `VideoWebPlayer/Components/Account/Pages/Manage/ResetAuthenticator.razor` | 0,0 % |
| `VideoWebPlayer/Components/Account/Pages/Manage/SetPassword.razor` | 0,0 % |
| `VideoWebPlayer/Components/Account/Pages/Manage/TwoFactorAuthentication.razor` | 0,0 % |
| `VideoWebPlayer/Components/Account/Pages/RegisterConfirmation.razor` | 0,0 % |
| `VideoWebPlayer/Components/Account/Pages/ResendEmailConfirmation.razor` | 0,0 % |
| `VideoWebPlayer/Components/Account/Pages/ResetPassword.razor` | 0,0 % |
| `VideoWebPlayer/Components/Account/Shared/RedirectToLogin.razor` | 0,0 % |
| `VideoWebPlayer/Components/Account/Shared/ShowRecoveryCodes.razor` | 0,0 % |
| `VideoWebPlayer/Components/Pages/Actors/ActorDetails.razor` | 0,0 % |
| `VideoWebPlayer/Components/Pages/Actors/Actors.razor` | 0,0 % |
| `VideoWebPlayer/Components/Pages/Admin/MediaSources/MediaSourceExplorer.razor` | 0,0 % |
| `VideoWebPlayer/Components/Pages/Admin/UserManagement.razor` | 0,0 % |
| `VideoWebPlayer/Components/Pages/Errors/Error.razor` | 0,0 % |
| `VideoWebPlayer/Components/Pages/Samples/Auth.razor` | 0,0 % |
| `VideoWebPlayer/Components/Pages/Samples/Counter.razor` | 0,0 % |
| `VideoWebPlayer/Components/Pages/Samples/Weather.razor` | 0,0 % |
| `VideoWebPlayer/Components/Shared/Media/MediaBaseEntryList.razor` | 0,0 % |
| `VideoWebPlayer/Components/Shared/Media/WatchedIndicator.razor` | 0,0 % |
| `VideoWebPlayer/Controllers/Attributes/ConnectionCheckAttribute.cs` | 0,0 % |
| `VideoWebPlayer/Hubs/MediaUpdateHub.cs` | 0,0 % |
| `VideoWebPlayer/Services/DemoData/IDemoDataSetService.cs` | 0,0 % |
| `VideoWebPlayer/Services/SftpStreamWrapper.cs` | 0,0 % |
| `VideoWebPlayer/Utils/LocalNetworkHelper.cs` | 0,0 % |
| `VideoWebPlayer/Components/Account/IdentityRevalidatingAuthenticationStateProvider.cs` | 10,0 % |
| `VideoWebPlayer/Services/SftpMediaSourceReader.cs` | 11,4 % |
| `VideoWebPlayer/Components/Shared/Media/ActorList.razor` | 15,8 % |
| `VideoWebPlayer/Services/DemoData/FileSystemDemoDataSetService.cs` | 24,3 % |
| `VideoWebPlayer/Services/InternalConnectionService.cs` | 25,0 % |
| `VideoWebPlayer/Components/Shared/Home/SeasonalGenreList.razor` | 26,5 % |
| `VideoWebPlayer/Components/Pages/TV/TVShowDetails.razor` | 26,6 % |
| `VideoWebPlayer/Services/Backups/ManualBackupJobService.cs` | 29,5 % |
| `VideoWebPlayer/Components/Shared/Home/RecentEntriesList.razor` | 31,2 % |
| `VideoWebPlayer/Controllers/Exceptions/ForbiddenAccessException.cs` | 33,3 % |
| `VideoWebPlayer/Controllers/Exceptions/RecordNotFoundException.cs` | 33,3 % |
| `VideoWebPlayer/Services/Exceptions/ContinueWatchingConfirmationRequiredException.cs` | 33,3 % |
| `VideoWebPlayer/Services/Exceptions/DiscoveryUrlValidationException.cs` | 33,3 % |
| `VideoWebPlayer/Services/Exceptions/ManualSortOrderConfirmationRequiredException.cs` | 33,3 % |
| `VideoWebPlayer/Services/Exceptions/PlaylistAccessDeniedException.cs` | 33,3 % |
| `VideoWebPlayer/Services/Exceptions/PlaylistNameAlreadyExistsException.cs` | 33,3 % |
| `VideoWebPlayer/Services/Exceptions/PlaylistNotInManualSortModeException.cs` | 33,3 % |
| `VideoWebPlayer/Services/Exceptions/UploadedCoverReplacementConfirmationRequiredException.cs` | 33,3 % |
| `VideoWebPlayer/Controllers/SourceGenresController.cs` | 34,2 % |
| `VideoWebPlayer/Migrations/20260830194227_UpdateMediaItemNavigations.cs` | 38,0 % |
| `VideoWebPlayer/Controllers/FavoritesController.cs` | 39,2 % |
| `VideoWebPlayer/Services/FavoritesService.cs` | 39,8 % |
| `VideoWebPlayer/Migrations/20260307042726_RemoveMediaSourceTextIcon.cs` | 41,7 % |
| `VideoWebPlayer/Services/MdnsAdvertiserWorker.cs` | 44,1 % |
| `VideoWebPlayer/Components/Account/Pages/Shared/LoginForm.razor` | 46,2 % |
| `VideoWebPlayer/Migrations/20260830182331_MakePictureMediaItemIdNullable.cs` | 46,2 % |
| `VideoWebPlayer/Components/Pages/Admin/Backups.razor` | 47,5 % |
| `VideoWebPlayer/Components/Pages/Admin/MediaSources/MediaSourceAdmin.razor` | 47,5 % |
| `VideoWebPlayer/Migrations/20250810141338_UpdateMediaModels2.cs` | 47,7 % |
| `VideoWebPlayer/Services/RecentEntryService.cs` | 49,3 % |
| `VideoWebPlayer/Components/Account/Pages/Login.razor` | 50,0 % |
| `VideoWebPlayer/Controllers/MediaFileContentTypes.cs` | 50,0 % |
| `VideoWebPlayer/Data/GenreName.cs` | 50,0 % |
| `VideoWebPlayer/Data/MediaSourceUser.cs` | 50,0 % |
| `VideoWebPlayer/Data/MovieGenre.cs` | 50,0 % |
| `VideoWebPlayer/Data/TVShowGenre.cs` | 50,0 % |
| `VideoWebPlayer/Migrations/20250817090813_UpdateMediaModels21.cs` | 50,0 % |
| `VideoWebPlayer/Migrations/20260810152703_RenameEpisodeBackgroundImageColumns.cs` | 50,0 % |
| `VideoWebPlayer/Components/Playlists/PlaylistGenreEditor.razor` | 50,9 % |
| `VideoWebPlayer/Components/Account/IdentityComponentsEndpointRouteBuilderExtensions.cs` | 51,1 % |
| `VideoWebPlayer/Services/Updates/VideoWebPlayerUpdateSourceFactory.cs` | 52,0 % |
| `VideoWebPlayer/Components/Account/Shared/StatusMessage.razor` | 52,9 % |
| `VideoWebPlayer/Migrations/20261022190000_AddProgramRuleCursorStrandKey.cs` | 53,6 % |
| `VideoWebPlayer/Components/Pages/Admin/Security.razor` | 54,3 % |
| `VideoWebPlayer/Migrations/20250810140938_UpdateMediaModels1.cs` | 54,5 % |
| `VideoWebPlayer/Data/TVShowEpisode.cs` | 55,6 % |
| `VideoWebPlayer/Services/Backups/RestoreInProgressMiddleware.cs` | 56,7 % |
| `VideoWebPlayer/Components/Pages/Movies/MovieCollectionDetails.razor` | 57,4 % |
| `VideoWebPlayer/Components/Pages/Admin/MediaSources/MediaSourceAdminDetails.razor` | 57,4 % |
| `VideoWebPlayer/Controllers/AdminSourcesController.cs` | 57,7 % |
| `VideoWebPlayer/Controllers/PicturesController.cs` | 57,7 % |
| `VideoWebPlayer/Migrations/20260907043451_AddSortOrderToPlaylistEntries.cs` | 57,9 % |
| `VideoWebPlayer/Components/Shared/Home/FavoritesList.razor` | 58,2 % |
| `VideoWebPlayer/Migrations/20250816072812_UpdateMediaModels13.cs` | 58,3 % |
| `VideoWebPlayer/Migrations/20260305091241_AddMediaSourceIcon.cs` | 58,3 % |
| `VideoWebPlayer/Migrations/20261005171612_AddSetupDiscoveryPublicBaseUrl.cs` | 58,3 % |
| `VideoWebPlayer/Services/MediaUpdateNotificationService.cs` | 58,7 % |
| `VideoWebPlayer/Services/GenreService.cs` | 58,8 % |
| `VideoWebPlayer/Migrations/20260913154406_AddPlaylistIdToContinueWatchingEntry.cs` | 59,0 % |
| `VideoWebPlayer/Migrations/20261021033436_AddMediaItemDuration.cs` | 59,3 % |
| `VideoWebPlayer/Components/Pages/MediaSources/MediaSourceDetails.razor` | 59,6 % |
| `VideoWebPlayer/Migrations/20250814180536_UpdateMediaModels8.cs` | 59,6 % |
| `VideoWebPlayer/Migrations/20250816071104_UpdateMediaModels12.cs` | 60,0 % |
| `VideoWebPlayer/Migrations/20250817132350_UpdateMediaModels22.cs` | 60,0 % |
| `VideoWebPlayer/Services/EpisodeBackgroundImage/EpisodeBackgroundImageOptionsValidator.cs` | 60,0 % |
| `VideoWebPlayer/Controllers/UnlockedMediaController.cs` | 60,4 % |
| `VideoWebPlayer/Components/Account/IdentityRedirectManager.cs` | 60,5 % |
| `VideoWebPlayer/Migrations/20260810144513_AddPictureGeneratedBackgroundProperties.cs` | 60,7 % |
| `VideoWebPlayer/Migrations/20250817135431_UpdateMediaModels23.cs` | 61,4 % |
| `VideoWebPlayer/Services/Backups/VideoWebPlayerBackupFacade.cs` | 61,4 % |
| `VideoWebPlayer/Components/Account/Shared/AccountLayout.razor` | 61,5 % |
| `VideoWebPlayer/Data/Genre.cs` | 61,5 % |
| `VideoWebPlayer/Migrations/20250815153324_UpdateMediaModels10.cs` | 61,5 % |
| `VideoWebPlayer/Migrations/20250817060241_UpdateMediaModels16.cs` | 61,5 % |
| `VideoWebPlayer/Migrations/20250818183736_UpdateMediaModels24.cs` | 61,5 % |
| `VideoWebPlayer/Migrations/20260225191438_AddMediaCollectionClassifyable.cs` | 61,5 % |
| `VideoWebPlayer/Migrations/20260810051034_AddApplicationTitleToSetup.cs` | 61,5 % |
| `VideoWebPlayer/Migrations/20260827164650_AddContinueWatchingEndThresholdSeconds.cs` | 61,5 % |
| `VideoWebPlayer/Migrations/20260914210317_AddPlaylistCoverFields.cs` | 61,5 % |
| `VideoWebPlayer/Migrations/20260920024035_AddPlaylistIsPublic.cs` | 61,5 % |
| `VideoWebPlayer/Migrations/20260922180232_AddMediaSourceSourceType.cs` | 61,5 % |
| `VideoWebPlayer/Migrations/20261004173814_AddSetupMdnsAdvertisementEnabled.cs` | 61,5 % |
| `VideoWebPlayer/Migrations/20261021053951_AddProgramSlotStatus.cs` | 61,5 % |
| `VideoWebPlayer/Migrations/20261021122743_AddMediaItemDurationProbeAttempts.cs` | 61,5 % |
| `VideoWebPlayer/Migrations/20261022074936_AddMovieMediaItemIsTrailer.cs` | 61,5 % |
| `VideoWebPlayer/Migrations/20261023050115_AddSetupTvSenderEnabled.cs` | 61,5 % |
| `VideoWebPlayer/Services/WhitelistIpMiddleware.cs` | 61,5 % |
| `VideoWebPlayer/Migrations/20250817074737_UpdateMediaModels19.cs` | 61,9 % |
| `VideoWebPlayer/Migrations/20250814162930_UpdateMediaModels7.cs` | 62,1 % |
| `VideoWebPlayer/Migrations/20260810144442_AddEpisodeBackgroundImageProperties.cs` | 62,2 % |
| `VideoWebPlayer/Migrations/20250812173712_UpdateMediaModels5.cs` | 62,2 % |
| `VideoWebPlayer/Components/Pages/Admin/GenreAdmin.razor` | 62,4 % |
| `VideoWebPlayer/Data/Entities/WatchedEntry.cs` | 62,5 % |
| `VideoWebPlayer/Migrations/20261021065049_AddSetupLiveStreamSettings.cs` | 63,3 % |
| `VideoWebPlayer/Migrations/20250810082431_AddIsAdminToApplicationUser.cs` | 63,6 % |
| `VideoWebPlayer/Migrations/20250813051648_UpdateMediaModels6.cs` | 63,6 % |
| `VideoWebPlayer/Migrations/20260307073741_AddScanSettingsToSetup.cs` | 63,6 % |
| `VideoWebPlayer/Migrations/20260913160621_FixContinueWatchingEntryNullPlaylistUniqueness.cs` | 63,6 % |
| `VideoWebPlayer/Controllers/ActorsController.cs` | 64,5 % |
| `VideoWebPlayer/Services/Backups/VideoWebPlayerBackupDataFactory.cs` | 64,7 % |
| `VideoWebPlayer/Services/LiveStreamWorker.cs` | 64,8 % |
| `VideoWebPlayer/Migrations/20260831050031_AddActorRoleAndOrder.cs` | 65,0 % |
| `VideoWebPlayer/Services/ProgramGuideEntryText.cs` | 65,0 % |
| `VideoWebPlayer/Migrations/20260820183537_AddManualMetadataEditFlag.cs` | 65,3 % |
| `VideoWebPlayer/Controllers/SourceIconsController.cs` | 65,5 % |
| `VideoWebPlayer/Data/ApplicationDbContext.cs` | 65,8 % |
| `VideoWebPlayer/Components/Account/IdentityUserAccessor.cs` | 66,7 % |
| `VideoWebPlayer/Data/UnlockedMediaEntry.cs` | 66,7 % |
| `VideoWebPlayer/Migrations/20250817063844_UpdateMediaModels18.cs` | 67,3 % |
| `VideoWebPlayer/Components/Account/Pages/Register.razor` | 67,5 % |
| `VideoWebPlayer/Program.cs` | 67,7 % |
| `VideoWebPlayer.Client/VideoWebPlayerClient.cs` | 67,9 % |
| `VideoWebPlayer/Migrations/20260822103156_AddContinueWatchingListOrder.cs` | 68,0 % |
| `VideoWebPlayer/Controllers/SourcesController.cs` | 68,4 % |
| `VideoWebPlayer/Migrations/20260305103130_AddMediaSourceIconUpload.cs` | 69,8 % |
| `VideoWebPlayer/Extensions/LocalConfigurationExtensions.cs` | 70,3 % |
| `VideoWebPlayer/Controllers/BackupsController.cs` | 72,5 % |
| `VideoWebPlayer.Client/Models/DtoSource.cs` | 72,7 % |
| `VideoWebPlayer/Controllers/AuthController.cs` | 72,7 % |
| `VideoWebPlayer/Components/Shared/Media/VideoPlayer.razor` | 73,3 % |
| `VideoWebPlayer/Migrations/20250812164844_UpdateMediaModels4.cs` | 74,5 % |
| `VideoWebPlayer/Components/Pages/Admin/TVSenderAdmin.razor` | 74,8 % |
| `VideoWebPlayer/Components/Playlists/PlaylistSortModeIcon.razor` | 75,0 % |
| `VideoWebPlayer/Components/Shared/Media/UnlockButton.razor` | 75,0 % |
| `VideoWebPlayer/Data/Entities/ContinueWatchingEntry.cs` | 75,0 % |
| `VideoWebPlayer/Migrations/20260830193357_ResetActorsClassifiedAt.cs` | 75,0 % |
| `VideoWebPlayer/Services/IProgramScheduleService.cs` | 75,0 % |
| `VideoWebPlayer/Services/DiscoveryBaseUrlResolver.cs` | 75,5 % |
| `VideoWebPlayer/Migrations/20260920165219_AddPlaylistBackfillMarkers.cs` | 75,6 % |
| `VideoWebPlayer/Services/Updates/UpdateSettingsInitializer.cs` | 76,5 % |
| `VideoWebPlayer/Services/Backups/VideoWebPlayerBackupData.cs` | 76,8 % |
| `VideoWebPlayer/Migrations/20260924103134_AddPairingBootstrapAndRefreshTokens.cs` | 76,8 % |
| `VideoWebPlayer/Components/Layout/NavMenu.razor` | 77,0 % |
| `VideoWebPlayer/Services/Authentication/IAuthService.cs` | 77,1 % |
| `VideoWebPlayer/Services/ActorBackfillWorker.cs` | 77,3 % |
| `VideoWebPlayer/Data/ApplicationDbContext.PlaylistBackfillMarking.cs` | 77,6 % |
| `VideoWebPlayer/Controllers/ItemsController.cs` | 77,7 % |
| `VideoWebPlayer/Components/Pages/Admin/AdminIndex.razor` | 77,8 % |
| `VideoWebPlayer/Migrations/20260905220309_NormalizePlaylistEntryMediaTypes.cs` | 77,8 % |
| `VideoWebPlayer/Services/StaticAssetVersioner.cs` | 77,8 % |
| `VideoWebPlayer/Services/WatchedStatusService.cs` | 78,9 % |
| `VideoWebPlayer/Controllers/ProgramGuideController.cs` | 78,9 % |
| `VideoWebPlayer/Services/PlaylistBackfillWorker.cs` | 79,1 % |
| `VideoWebPlayer/Services/HomeBackgroundImage/HomeBackgroundImageGenerator.cs` | 79,2 % |
| `VideoWebPlayer/Services/ContinueWatchingWorker.cs` | 79,5 % |
| `VideoWebPlayer/Components/Pages/Admin/ProgramSettings.razor` | 79,8 % |

## Fehlende Tests

Quelle: `Coverage-Daten`

- `VideoWebPlayer.Client/Models/DtoRecentEntry.cs` — 0 % Abdeckung
- `VideoWebPlayer.Client/Models/ImpersonateRequest.cs` — 0 % Abdeckung
- `VideoWebPlayer/Components/Account/IdentityNoOpEmailSender.cs` — 0 % Abdeckung
- `VideoWebPlayer/Components/Account/Pages/ConfirmEmail.razor` — 0 % Abdeckung
- `VideoWebPlayer/Components/Account/Pages/ConfirmEmailChange.razor` — 0 % Abdeckung
- `VideoWebPlayer/Components/Account/Pages/ExternalLogin.razor` — 0 % Abdeckung
- `VideoWebPlayer/Components/Account/Pages/ForgotPassword.razor` — 0 % Abdeckung
- `VideoWebPlayer/Components/Account/Pages/LoginWith2fa.razor` — 0 % Abdeckung
- `VideoWebPlayer/Components/Account/Pages/LoginWithRecoveryCode.razor` — 0 % Abdeckung
- `VideoWebPlayer/Components/Account/Pages/Manage/ChangePassword.razor` — 0 % Abdeckung
- `VideoWebPlayer/Components/Account/Pages/Manage/DeletePersonalData.razor` — 0 % Abdeckung
- `VideoWebPlayer/Components/Account/Pages/Manage/Disable2fa.razor` — 0 % Abdeckung
- `VideoWebPlayer/Components/Account/Pages/Manage/Email.razor` — 0 % Abdeckung
- `VideoWebPlayer/Components/Account/Pages/Manage/EnableAuthenticator.razor` — 0 % Abdeckung
- `VideoWebPlayer/Components/Account/Pages/Manage/ExternalLogins.razor` — 0 % Abdeckung
- `VideoWebPlayer/Components/Account/Pages/Manage/GenerateRecoveryCodes.razor` — 0 % Abdeckung
- `VideoWebPlayer/Components/Account/Pages/Manage/Index.razor` — 0 % Abdeckung
- `VideoWebPlayer/Components/Account/Pages/Manage/PersonalData.razor` — 0 % Abdeckung
- `VideoWebPlayer/Components/Account/Pages/Manage/ResetAuthenticator.razor` — 0 % Abdeckung
- `VideoWebPlayer/Components/Account/Pages/Manage/SetPassword.razor` — 0 % Abdeckung
- `VideoWebPlayer/Components/Account/Pages/Manage/TwoFactorAuthentication.razor` — 0 % Abdeckung
- `VideoWebPlayer/Components/Account/Pages/RegisterConfirmation.razor` — 0 % Abdeckung
- `VideoWebPlayer/Components/Account/Pages/ResendEmailConfirmation.razor` — 0 % Abdeckung
- `VideoWebPlayer/Components/Account/Pages/ResetPassword.razor` — 0 % Abdeckung
- `VideoWebPlayer/Components/Account/Shared/RedirectToLogin.razor` — 0 % Abdeckung
- `VideoWebPlayer/Components/Account/Shared/ShowRecoveryCodes.razor` — 0 % Abdeckung
- `VideoWebPlayer/Components/Pages/Actors/ActorDetails.razor` — 0 % Abdeckung
- `VideoWebPlayer/Components/Pages/Actors/Actors.razor` — 0 % Abdeckung
- `VideoWebPlayer/Components/Pages/Admin/MediaSources/MediaSourceExplorer.razor` — 0 % Abdeckung
- `VideoWebPlayer/Components/Pages/Admin/UserManagement.razor` — 0 % Abdeckung
- `VideoWebPlayer/Components/Pages/Errors/Error.razor` — 0 % Abdeckung
- `VideoWebPlayer/Components/Pages/Samples/Auth.razor` — 0 % Abdeckung
- `VideoWebPlayer/Components/Pages/Samples/Counter.razor` — 0 % Abdeckung
- `VideoWebPlayer/Components/Pages/Samples/Weather.razor` — 0 % Abdeckung
- `VideoWebPlayer/Components/Shared/Media/MediaBaseEntryList.razor` — 0 % Abdeckung
- `VideoWebPlayer/Components/Shared/Media/WatchedIndicator.razor` — 0 % Abdeckung
- `VideoWebPlayer/Controllers/Attributes/ConnectionCheckAttribute.cs` — 0 % Abdeckung
- `VideoWebPlayer/Hubs/MediaUpdateHub.cs` — 0 % Abdeckung
- `VideoWebPlayer/Services/DemoData/IDemoDataSetService.cs` — 0 % Abdeckung
- `VideoWebPlayer/Services/SftpStreamWrapper.cs` — 0 % Abdeckung
- `VideoWebPlayer/Utils/LocalNetworkHelper.cs` — 0 % Abdeckung
