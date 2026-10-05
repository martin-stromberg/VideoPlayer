# Test-Ergebnisse

## Ergebnis

**Status:** Keine Fehler

## E2E-Abdeckung

Geplante E2E-Szenarien aus `plan.md` / `plan-check.md` (alle in `ProgramSettingsE2ETests`, alle im Lauf enthalten und bestanden):

| Szenario | Test / Testklasse | Ergebnis |
|----------|-------------------|----------|
| Admin pflegt öffentliche Basis-URL unter `/admin/program-settings` (Feld `#discoveryPublicBaseUrl` füllen → „Gespeichert." → Service-Lesung → Reload → Löschpfad) | `ProgramSettingsE2ETests.Admin_SavesDiscoveryPublicBaseUrl_AndSettingPersists` | Bestanden |
| Ungültige Admin-Eingabe wird blockiert (Validierungsmeldung sichtbar, kein „Gespeichert.", DB unverändert) | `ProgramSettingsE2ETests.Admin_EntersInvalidDiscoveryPublicBaseUrl_ValidationBlocksSave` | Bestanden |
| Nicht-Admin sieht das neue Feld nicht (Assertion `#discoveryPublicBaseUrl` Count 0) | `ProgramSettingsE2ETests.NonAdmin_GetsNotAuthorized_OnProgramSettings` (bestehend, erweitert) | Bestanden |
| UDP-Broadcast → Antwort mit erreichbarer URL (kein E2E möglich, Listener unter `Testing` abgeschaltet; Ersatz: echte Loopback-UDP-Tests) | `UdpDiscoveryListenerTests.Start_AnswersDiscoveryRequest_WithResolvedAddress`, `UdpDiscoveryListenerTests.Start_ResolvesAddress_PerRequest`, `UdpDiscoveryListenerTests.Start_LogsWarning_AndKeepsListening_WhenFactoryThrows` (neu aus Review-Nachbesserung) | Bestanden (Unit-/Integrationstests als begründeter Ersatz) |

## Zusammenfassung

`VideoWebPlayer.Tests` (`dotnet test ... --no-build --collect:"XPlat Code Coverage"`, nach erfolgreichem `dotnet build VideoPlayer.sln` mit 0 Fehlern und 0 Warnungen):

- Gesamt: 1735
- Bestanden: 1734
- Fehlgeschlagen: 0
- Übersprungen: 1 (`DevicePlaylistE2ETests_Playback.PairedDevice_ReportsProgressForMovieWithoutCollection_ContinueWatchingListStillLoads` — bekannter, dokumentierter Baseline-Skip / Produktbug, unverändert)

Zusätzlich `tools/MarkdownLinkCheck.Tests`: Gesamt 6, Bestanden 6, Fehlgeschlagen 0.

Gegenüber Iteration 2 (1729 Tests) +6 Tests aus den Code-Review-Nachbesserungen der 3. Iteration (u. a. `UdpDiscoveryListenerTests.Start_LogsWarning_AndKeepsListening_WhenFactoryThrows` für die neue Logging-/Catch-Struktur, IPv6-Klammer-Literale in `DiscoveryResponseBuilderTests`, umbenannter/korrigierter `DiscoveryBaseUrlResolverTests`-Fall). Keine Wiederholungsläufe nötig — kein einziger Fehlschlag im ersten Lauf.

## Testabdeckung

**Abdeckung:** 94,2 % (Zeilen, Cobertura über `XPlat Code Coverage`; Branch-Abdeckung 60,5 %)

Feature-relevante Dateien: `DiscoveryUrlRules.cs` 100 %, `DiscoveryOptions.cs` 100 %, `DiscoveryOptionsValidator.cs` 100 %, `AbsoluteHttpUrlAttribute.cs` 100 %, `DiscoveryResponseBuilder.cs` 92,5 %, `UdpDiscoveryListener.cs` 84,4 %, `ProgramSettings.razor` 84,4 %, `ProgramSettingsService.cs` 76,8 %, `DiscoveryBaseUrlResolver.cs` 75,5 %, `Program.cs` 67,7 %, Migration `AddSetupDiscoveryPublicBaseUrl` 58,3 % (`Down()` ungetestet, üblich), `DiscoveryUrlValidationException.cs` 33,3 % (triviale Exception-Klasse).

`UdpDiscoveryListener.cs` fiel von 93,1 % (Iteration 2) auf 84,4 %, weil die Review-Nachbesserung neue Catch-Zweige (`OperationCanceledException`-Abbruch, `SocketException`-Filter, Warning-Logging) hinzufügte; der Logging-Pfad ist durch den neuen Test `Start_LogsWarning_AndKeepsListening_WhenFactoryThrows` abgedeckt, die Socket-/Cancellation-Zweige nur partiell.

Dateien unter 80 % Zeilenabdeckung (ohne `Program.cs`, `Migrations/*Designer.cs`, Model-Snapshot):

| Datei | Abdeckung |
|-------|-----------|
| VideoWebPlayer.Client/Models/DtoRecentEntry.cs | 0 % |
| VideoWebPlayer.Client/Models/ImpersonateRequest.cs | 0 % |
| VideoWebPlayer/Components/Account/IdentityNoOpEmailSender.cs | 0 % |
| VideoWebPlayer/Components/Account/Pages/ConfirmEmail.razor | 0 % |
| VideoWebPlayer/Components/Account/Pages/ConfirmEmailChange.razor | 0 % |
| VideoWebPlayer/Components/Account/Pages/ExternalLogin.razor | 0 % |
| VideoWebPlayer/Components/Account/Pages/ForgotPassword.razor | 0 % |
| VideoWebPlayer/Components/Account/Pages/LoginWith2fa.razor | 0 % |
| VideoWebPlayer/Components/Account/Pages/LoginWithRecoveryCode.razor | 0 % |
| VideoWebPlayer/Components/Account/Pages/Manage/ChangePassword.razor | 0 % |
| VideoWebPlayer/Components/Account/Pages/Manage/DeletePersonalData.razor | 0 % |
| VideoWebPlayer/Components/Account/Pages/Manage/Disable2fa.razor | 0 % |
| VideoWebPlayer/Components/Account/Pages/Manage/Email.razor | 0 % |
| VideoWebPlayer/Components/Account/Pages/Manage/EnableAuthenticator.razor | 0 % |
| VideoWebPlayer/Components/Account/Pages/Manage/ExternalLogins.razor | 0 % |
| VideoWebPlayer/Components/Account/Pages/Manage/GenerateRecoveryCodes.razor | 0 % |
| VideoWebPlayer/Components/Account/Pages/Manage/Index.razor | 0 % |
| VideoWebPlayer/Components/Account/Pages/Manage/PersonalData.razor | 0 % |
| VideoWebPlayer/Components/Account/Pages/Manage/ResetAuthenticator.razor | 0 % |
| VideoWebPlayer/Components/Account/Pages/Manage/SetPassword.razor | 0 % |
| VideoWebPlayer/Components/Account/Pages/Manage/TwoFactorAuthentication.razor | 0 % |
| VideoWebPlayer/Components/Account/Pages/RegisterConfirmation.razor | 0 % |
| VideoWebPlayer/Components/Account/Pages/ResendEmailConfirmation.razor | 0 % |
| VideoWebPlayer/Components/Account/Pages/ResetPassword.razor | 0 % |
| VideoWebPlayer/Components/Account/Shared/RedirectToLogin.razor | 0 % |
| VideoWebPlayer/Components/Account/Shared/ShowRecoveryCodes.razor | 0 % |
| VideoWebPlayer/Components/Pages/Actors/ActorDetails.razor | 0 % |
| VideoWebPlayer/Components/Pages/Actors/Actors.razor | 0 % |
| VideoWebPlayer/Components/Pages/Admin/MediaSources/MediaSourceExplorer.razor | 0 % |
| VideoWebPlayer/Components/Pages/Admin/UserManagement.razor | 0 % |
| VideoWebPlayer/Components/Pages/Errors/Error.razor | 0 % |
| VideoWebPlayer/Components/Pages/Samples/Auth.razor | 0 % |
| VideoWebPlayer/Components/Pages/Samples/Counter.razor | 0 % |
| VideoWebPlayer/Components/Pages/Samples/Weather.razor | 0 % |
| VideoWebPlayer/Components/Shared/Media/MediaBaseEntryList.razor | 0 % |
| VideoWebPlayer/Components/Shared/Media/WatchedIndicator.razor | 0 % |
| VideoWebPlayer/Controllers/Attributes/ConnectionCheckAttribute.cs | 0 % |
| VideoWebPlayer/Hubs/MediaUpdateHub.cs | 0 % |
| VideoWebPlayer/Services/DemoData/IDemoDataSetService.cs | 0 % |
| VideoWebPlayer/Services/SftpStreamWrapper.cs | 0 % |
| VideoWebPlayer/Utils/LocalNetworkHelper.cs | 0 % |
| VideoWebPlayer/Components/Account/IdentityRevalidatingAuthenticationStateProvider.cs | 10 % |
| VideoWebPlayer/Services/SftpMediaSourceReader.cs | 11,4 % |
| VideoWebPlayer/Components/Shared/Media/ActorList.razor | 15,8 % |
| VideoWebPlayer/Services/DemoData/FileSystemDemoDataSetService.cs | 24,3 % |
| VideoWebPlayer/Services/InternalConnectionService.cs | 25 % |
| VideoWebPlayer/Components/Shared/Home/SeasonalGenreList.razor | 26,5 % |
| VideoWebPlayer/Components/Pages/TV/TVShowDetails.razor | 26,6 % |
| VideoWebPlayer/Services/Backups/ManualBackupJobService.cs | 29,5 % |
| VideoWebPlayer/Components/Shared/Home/RecentEntriesList.razor | 31,2 % |
| VideoWebPlayer/Controllers/FavoritesController.cs | 31,4 % |
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
| VideoWebPlayer/Migrations/20260830194227_UpdateMediaItemNavigations.cs | 38 % |
| VideoWebPlayer/Services/GenreService.cs | 38,2 % |
| VideoWebPlayer/Services/FavoritesService.cs | 39,8 % |
| VideoWebPlayer/Migrations/20260307042726_RemoveMediaSourceTextIcon.cs | 41,7 % |
| VideoWebPlayer/Components/Pages/Admin/Backups.razor | 43,5 % |
| VideoWebPlayer/Services/MdnsAdvertiserWorker.cs | 44,1 % |
| VideoWebPlayer/Components/Account/Pages/Shared/LoginForm.razor | 46,2 % |
| VideoWebPlayer/Migrations/20260830182331_MakePictureMediaItemIdNullable.cs | 46,2 % |
| VideoWebPlayer/Components/Pages/Admin/MediaSources/MediaSourceAdmin.razor | 47,5 % |
| VideoWebPlayer/Migrations/20250810141338_UpdateMediaModels2.cs | 47,7 % |
| VideoWebPlayer/Components/Pages/Admin/GenreAdmin.razor | 47,7 % |
| VideoWebPlayer/Services/RecentEntryService.cs | 49,3 % |
| VideoWebPlayer/Components/Account/Pages/Login.razor | 50 % |
| VideoWebPlayer/Data/GenreName.cs | 50 % |
| VideoWebPlayer/Data/MediaSourceUser.cs | 50 % |
| VideoWebPlayer/Data/MovieGenre.cs | 50 % |
| VideoWebPlayer/Data/TVShowGenre.cs | 50 % |
| VideoWebPlayer/Migrations/20250817090813_UpdateMediaModels21.cs | 50 % |
| VideoWebPlayer/Migrations/20260810152703_RenameEpisodeBackgroundImageColumns.cs | 50 % |
| VideoWebPlayer/Components/Playlists/PlaylistGenreEditor.razor | 50,9 % |
| VideoWebPlayer/Components/Account/IdentityComponentsEndpointRouteBuilderExtensions.cs | 51,1 % |
| VideoWebPlayer/Services/Updates/VideoWebPlayerUpdateSourceFactory.cs | 52 % |
| VideoWebPlayer/Components/Account/Shared/StatusMessage.razor | 52,9 % |
| VideoWebPlayer/Data/Genre.cs | 53,8 % |
| VideoWebPlayer/Migrations/20250810140938_UpdateMediaModels1.cs | 54,5 % |
| VideoWebPlayer/Data/TVShowEpisode.cs | 55,6 % |
| VideoWebPlayer/Services/Backups/RestoreInProgressMiddleware.cs | 56,7 % |
| VideoWebPlayer/Components/Pages/Movies/MovieCollectionDetails.razor | 57,4 % |
| VideoWebPlayer/Components/Pages/Admin/MediaSources/MediaSourceAdminDetails.razor | 57,4 % |
| VideoWebPlayer/Controllers/AdminSourcesController.cs | 57,7 % |
| VideoWebPlayer/Controllers/PicturesController.cs | 57,7 % |
| VideoWebPlayer/Migrations/20260907043451_AddSortOrderToPlaylistEntries.cs | 57,9 % |
| VideoWebPlayer/Components/Shared/Home/FavoritesList.razor | 58,2 % |
| VideoWebPlayer/Migrations/20250816072812_UpdateMediaModels13.cs | 58,3 % |
| VideoWebPlayer/Migrations/20260305091241_AddMediaSourceIcon.cs | 58,3 % |
| VideoWebPlayer/Migrations/20261005171612_AddSetupDiscoveryPublicBaseUrl.cs | 58,3 % |
| VideoWebPlayer/Components/Pages/Admin/Security.razor | 58,7 % |
| VideoWebPlayer/Services/MediaUpdateNotificationService.cs | 58,7 % |
| VideoWebPlayer/Migrations/20260913154406_AddPlaylistIdToContinueWatchingEntry.cs | 59 % |
| VideoWebPlayer/Components/Pages/MediaSources/MediaSourceDetails.razor | 59,6 % |
| VideoWebPlayer/Migrations/20250814180536_UpdateMediaModels8.cs | 59,6 % |
| VideoWebPlayer/Migrations/20250816071104_UpdateMediaModels12.cs | 60 % |
| VideoWebPlayer/Migrations/20250817132350_UpdateMediaModels22.cs | 60 % |
| VideoWebPlayer/Services/EpisodeBackgroundImage/EpisodeBackgroundImageOptionsValidator.cs | 60 % |
| VideoWebPlayer/Controllers/UnlockedMediaController.cs | 60,4 % |
| VideoWebPlayer/Components/Account/IdentityRedirectManager.cs | 60,5 % |
| VideoWebPlayer/Migrations/20260810144513_AddPictureGeneratedBackgroundProperties.cs | 60,7 % |
| VideoWebPlayer/Migrations/20250817135431_UpdateMediaModels23.cs | 61,4 % |
| VideoWebPlayer/Services/Backups/VideoWebPlayerBackupFacade.cs | 61,4 % |
| VideoWebPlayer/Components/Account/Shared/AccountLayout.razor | 61,5 % |
| VideoWebPlayer/Migrations/20250815153324_UpdateMediaModels10.cs | 61,5 % |
| VideoWebPlayer/Migrations/20250817060241_UpdateMediaModels16.cs | 61,5 % |
| VideoWebPlayer/Migrations/20250818183736_UpdateMediaModels24.cs | 61,5 % |
| VideoWebPlayer/Migrations/20260225191438_AddMediaCollectionClassifyable.cs | 61,5 % |
| VideoWebPlayer/Migrations/20260810051034_AddApplicationTitleToSetup.cs | 61,5 % |
| VideoWebPlayer/Migrations/20260827164650_AddContinueWatchingEndThresholdSeconds.cs | 61,5 % |
| VideoWebPlayer/Migrations/20260914210317_AddPlaylistCoverFields.cs | 61,5 % |
| VideoWebPlayer/Migrations/20260920024035_AddPlaylistIsPublic.cs | 61,5 % |
| VideoWebPlayer/Migrations/20260922180232_AddMediaSourceSourceType.cs | 61,5 % |
| VideoWebPlayer/Migrations/20261004173814_AddSetupMdnsAdvertisementEnabled.cs | 61,5 % |
| VideoWebPlayer/Services/WhitelistIpMiddleware.cs | 61,5 % |
| VideoWebPlayer/Migrations/20250817074737_UpdateMediaModels19.cs | 61,9 % |
| VideoWebPlayer/Migrations/20250814162930_UpdateMediaModels7.cs | 62,1 % |
| VideoWebPlayer/Migrations/20260810144442_AddEpisodeBackgroundImageProperties.cs | 62,2 % |
| VideoWebPlayer/Migrations/20250812173712_UpdateMediaModels5.cs | 62,2 % |
| VideoWebPlayer/Data/Entities/WatchedEntry.cs | 62,5 % |
| VideoWebPlayer/Migrations/20250810082431_AddIsAdminToApplicationUser.cs | 63,6 % |
| VideoWebPlayer/Migrations/20250813051648_UpdateMediaModels6.cs | 63,6 % |
| VideoWebPlayer/Migrations/20260307073741_AddScanSettingsToSetup.cs | 63,6 % |
| VideoWebPlayer/Migrations/20260913160621_FixContinueWatchingEntryNullPlaylistUniqueness.cs | 63,6 % |
| VideoWebPlayer/Controllers/ActorsController.cs | 64,5 % |
| VideoWebPlayer/Services/Backups/VideoWebPlayerBackupDataFactory.cs | 64,7 % |
| VideoWebPlayer/Data/ApplicationDbContext.cs | 64,9 % |
| VideoWebPlayer/Migrations/20260831050031_AddActorRoleAndOrder.cs | 65 % |
| VideoWebPlayer/Migrations/20260820183537_AddManualMetadataEditFlag.cs | 65,3 % |
| VideoWebPlayer/Controllers/SourceIconsController.cs | 65,5 % |
| VideoWebPlayer/Components/Account/IdentityUserAccessor.cs | 66,7 % |
| VideoWebPlayer/Data/UnlockedMediaEntry.cs | 66,7 % |
| VideoWebPlayer/Migrations/20250817063844_UpdateMediaModels18.cs | 67,3 % |
| VideoWebPlayer/Components/Account/Pages/Register.razor | 67,5 % |
| VideoWebPlayer.Client/VideoWebPlayerClient.cs | 67,8 % |
| VideoWebPlayer/Migrations/20260822103156_AddContinueWatchingListOrder.cs | 68 % |
| VideoWebPlayer/Controllers/SourcesController.cs | 68,4 % |
| VideoWebPlayer/Migrations/20260305103130_AddMediaSourceIconUpload.cs | 69,8 % |
| VideoWebPlayer/Extensions/LocalConfigurationExtensions.cs | 70,3 % |
| VideoWebPlayer/Controllers/BackupsController.cs | 72,5 % |
| VideoWebPlayer.Client/Models/DtoSource.cs | 72,7 % |
| VideoWebPlayer/Controllers/AuthController.cs | 72,7 % |
| VideoWebPlayer/Components/Shared/Media/VideoPlayer.razor | 73 % |
| VideoWebPlayer/Components/Layout/NavMenu.razor | 73,7 % |
| VideoWebPlayer/Migrations/20250812164844_UpdateMediaModels4.cs | 74,5 % |
| VideoWebPlayer/Services/Backups/VideoWebPlayerBackupData.cs | 74,7 % |
| VideoWebPlayer/Components/Playlists/PlaylistSortModeIcon.razor | 75 % |
| VideoWebPlayer/Components/Shared/Media/UnlockButton.razor | 75 % |
| VideoWebPlayer/Data/Entities/ContinueWatchingEntry.cs | 75 % |
| VideoWebPlayer/Migrations/20260830193357_ResetActorsClassifiedAt.cs | 75 % |
| VideoWebPlayer/Services/DiscoveryBaseUrlResolver.cs | 75,5 % |
| VideoWebPlayer/Migrations/20260920165219_AddPlaylistBackfillMarkers.cs | 75,6 % |
| VideoWebPlayer/Services/Updates/UpdateSettingsInitializer.cs | 76,5 % |
| VideoWebPlayer/Services/ProgramSettingsService.cs | 76,8 % |
| VideoWebPlayer/Migrations/20260924103134_AddPairingBootstrapAndRefreshTokens.cs | 76,8 % |
| VideoWebPlayer/Services/Authentication/IAuthService.cs | 77,1 % |
| VideoWebPlayer/Services/ActorBackfillWorker.cs | 77,3 % |
| VideoWebPlayer/Controllers/ItemsController.cs | 77,3 % |
| VideoWebPlayer/Services/HomeBackgroundImage/HomeBackgroundImageGenerator.cs | 77,4 % |
| VideoWebPlayer/Components/Pages/Admin/AdminIndex.razor | 77,8 % |
| VideoWebPlayer/Migrations/20260905220309_NormalizePlaylistEntryMediaTypes.cs | 77,8 % |
| VideoWebPlayer/Services/StaticAssetVersioner.cs | 77,8 % |
| VideoWebPlayer/Data/ApplicationDbContext.PlaylistBackfillMarking.cs | 78,2 % |
| VideoWebPlayer/Services/MediaSourceClassifier.cs | 78,7 % |
| VideoWebPlayer/Services/WatchedStatusService.cs | 78,9 % |
| VideoWebPlayer/Services/ContinueWatchingWorker.cs | 79,5 % |

## Fehlende Tests

Quelle: Coverage-Daten (Dateien mit 0 % Zeilenabdeckung im Coverage-Bericht). Alle Einträge sind Bestandslücken, keine neuen Dateien dieses Features — identisch mit Iteration 2.

- `VideoWebPlayer.Client/Models/DtoRecentEntry.cs` – 0 % Zeilenabdeckung, keine Testausführung
- `VideoWebPlayer.Client/Models/ImpersonateRequest.cs` – 0 % Zeilenabdeckung, keine Testausführung
- `VideoWebPlayer/Components/Account/IdentityNoOpEmailSender.cs` – 0 % Zeilenabdeckung, keine Testausführung
- `VideoWebPlayer/Components/Account/Pages/ConfirmEmail.razor` – 0 % Zeilenabdeckung, keine Testausführung
- `VideoWebPlayer/Components/Account/Pages/ConfirmEmailChange.razor` – 0 % Zeilenabdeckung, keine Testausführung
- `VideoWebPlayer/Components/Account/Pages/ExternalLogin.razor` – 0 % Zeilenabdeckung, keine Testausführung
- `VideoWebPlayer/Components/Account/Pages/ForgotPassword.razor` – 0 % Zeilenabdeckung, keine Testausführung
- `VideoWebPlayer/Components/Account/Pages/LoginWith2fa.razor` – 0 % Zeilenabdeckung, keine Testausführung
- `VideoWebPlayer/Components/Account/Pages/LoginWithRecoveryCode.razor` – 0 % Zeilenabdeckung, keine Testausführung
- `VideoWebPlayer/Components/Account/Pages/Manage/ChangePassword.razor` – 0 % Zeilenabdeckung, keine Testausführung
- `VideoWebPlayer/Components/Account/Pages/Manage/DeletePersonalData.razor` – 0 % Zeilenabdeckung, keine Testausführung
- `VideoWebPlayer/Components/Account/Pages/Manage/Disable2fa.razor` – 0 % Zeilenabdeckung, keine Testausführung
- `VideoWebPlayer/Components/Account/Pages/Manage/Email.razor` – 0 % Zeilenabdeckung, keine Testausführung
- `VideoWebPlayer/Components/Account/Pages/Manage/EnableAuthenticator.razor` – 0 % Zeilenabdeckung, keine Testausführung
- `VideoWebPlayer/Components/Account/Pages/Manage/ExternalLogins.razor` – 0 % Zeilenabdeckung, keine Testausführung
- `VideoWebPlayer/Components/Account/Pages/Manage/GenerateRecoveryCodes.razor` – 0 % Zeilenabdeckung, keine Testausführung
- `VideoWebPlayer/Components/Account/Pages/Manage/Index.razor` – 0 % Zeilenabdeckung, keine Testausführung
- `VideoWebPlayer/Components/Account/Pages/Manage/PersonalData.razor` – 0 % Zeilenabdeckung, keine Testausführung
- `VideoWebPlayer/Components/Account/Pages/Manage/ResetAuthenticator.razor` – 0 % Zeilenabdeckung, keine Testausführung
- `VideoWebPlayer/Components/Account/Pages/Manage/SetPassword.razor` – 0 % Zeilenabdeckung, keine Testausführung
- `VideoWebPlayer/Components/Account/Pages/Manage/TwoFactorAuthentication.razor` – 0 % Zeilenabdeckung, keine Testausführung
- `VideoWebPlayer/Components/Account/Pages/RegisterConfirmation.razor` – 0 % Zeilenabdeckung, keine Testausführung
- `VideoWebPlayer/Components/Account/Pages/ResendEmailConfirmation.razor` – 0 % Zeilenabdeckung, keine Testausführung
- `VideoWebPlayer/Components/Account/Pages/ResetPassword.razor` – 0 % Zeilenabdeckung, keine Testausführung
- `VideoWebPlayer/Components/Account/Shared/RedirectToLogin.razor` – 0 % Zeilenabdeckung, keine Testausführung
- `VideoWebPlayer/Components/Account/Shared/ShowRecoveryCodes.razor` – 0 % Zeilenabdeckung, keine Testausführung
- `VideoWebPlayer/Components/Pages/Actors/ActorDetails.razor` – 0 % Zeilenabdeckung, keine Testausführung
- `VideoWebPlayer/Components/Pages/Actors/Actors.razor` – 0 % Zeilenabdeckung, keine Testausführung
- `VideoWebPlayer/Components/Pages/Admin/MediaSources/MediaSourceExplorer.razor` – 0 % Zeilenabdeckung, keine Testausführung
- `VideoWebPlayer/Components/Pages/Admin/UserManagement.razor` – 0 % Zeilenabdeckung, keine Testausführung
- `VideoWebPlayer/Components/Pages/Errors/Error.razor` – 0 % Zeilenabdeckung, keine Testausführung
- `VideoWebPlayer/Components/Pages/Samples/Auth.razor` – 0 % Zeilenabdeckung, keine Testausführung
- `VideoWebPlayer/Components/Pages/Samples/Counter.razor` – 0 % Zeilenabdeckung, keine Testausführung
- `VideoWebPlayer/Components/Pages/Samples/Weather.razor` – 0 % Zeilenabdeckung, keine Testausführung
- `VideoWebPlayer/Components/Shared/Media/MediaBaseEntryList.razor` – 0 % Zeilenabdeckung, keine Testausführung
- `VideoWebPlayer/Components/Shared/Media/WatchedIndicator.razor` – 0 % Zeilenabdeckung, keine Testausführung
- `VideoWebPlayer/Controllers/Attributes/ConnectionCheckAttribute.cs` – 0 % Zeilenabdeckung, keine Testausführung
- `VideoWebPlayer/Hubs/MediaUpdateHub.cs` – 0 % Zeilenabdeckung, keine Testausführung
- `VideoWebPlayer/Services/DemoData/IDemoDataSetService.cs` – 0 % Zeilenabdeckung, keine Testausführung
- `VideoWebPlayer/Services/SftpStreamWrapper.cs` – 0 % Zeilenabdeckung, keine Testausführung
- `VideoWebPlayer/Utils/LocalNetworkHelper.cs` – 0 % Zeilenabdeckung, keine Testausführung
