# Test-Ergebnisse

## Ergebnis

**Status:** Keine Fehler

## Testläufe

| Lauf | Befehl | Ergebnis | Nachweis |
|------|--------|----------|----------|
| Build (Vorlauf) | `dotnet build VideoPlayer.sln -c Release` | 0 Fehler, 0 Warnungen (inkrementell) | Konsole |
| Vollständiger Lauf + Coverage | `dotnet test VideoWebPlayer.Tests/VideoWebPlayer.Tests.csproj --no-build -c Release --collect:"XPlat Code Coverage"` | 351/351 bestanden | [TRX](test-results/test-results-full.trx), [Log](test-results/full-tests-console.log), [Coverage](test-results/c75c0267-2a25-47e1-bd8d-43701a95b11a/coverage.cobertura.xml) |
| E2E-Kategorie | `dotnet test ... --filter "Category=E2E"` | 53/53 bestanden | [TRX](test-results/test-results-e2e.trx), [Log](test-results/e2e-tests-console.log) |

Iteration 2: Die Läufe wurden nach den Code-Review-Fixes (Logging im `LocalMediaSourceReader`, `MediaEntryFilter`, differenzierte Pfadvalidierung `TryValidateLocalDirectory`, Disposal-Fix in `LocalMediaSourceClassifierTests`) und den neuen Tests (`MediaSourceAdminDetailsValidationTests`, `MediaEntryFilterTests`, E2E `Admin_SeesError_When_Local_Directory_Is_Not_Accessible`, `FileExistsAsync_InvalidFileName_LogsAndReturnsFalse`) wiederholt. Während der Testausführung wurden keine Codeänderungen vorgenommen.

## Fehlgeschlagene Tests

Keine. Sowohl der vollständige Lauf (351 Tests) als auch der gezielte E2E-Lauf (53 Tests) waren vollständig erfolgreich.

## E2E-Abdeckung

Alle im Plan (Abschnitt `E2E-Tests`) bzw. `plan-check.md` (Abschnitt `E2E-Abdeckung`) geforderten Szenarien existieren und sind bestanden:

| Szenario | Test / Testklasse | Ergebnis |
|----------|-------------------|----------|
| Admin legt Quelle vom Typ „Lokales Verzeichnis" an: Typauswahl, `Name`+`Path`, Speichern → Übersicht mit Typ-Kennzeichnung („Lokal"); SFTP-Felder bei lokalem Typ nicht sichtbar | `MediaSourceLocalDirectoryE2ETests.Admin_Can_Create_LocalDirectory_Source_And_SftpFields_Are_Hidden` (Playwright) | Bestanden |
| Ungültiger lokaler Pfad (nicht existierendes Verzeichnis) → sichtbare Fehlermeldung, Quelle wird nicht angelegt | `MediaSourceLocalDirectoryE2ETests.Admin_SeesError_When_Local_Directory_Does_Not_Exist` | Bestanden |
| Lokaler Pfad nicht zugreifbar → sichtbare Fehlermeldung, Quelle wird nicht angelegt (zusätzlicher Fehlerfall, neu in Iteration 2) | `MediaSourceLocalDirectoryE2ETests.Admin_SeesError_When_Local_Directory_Is_Not_Accessible` | Bestanden |
| Bearbeiten lokaler Quelle: Typ-Select deaktiviert, lokale Felder sichtbar, Speichern möglich | `MediaSourceLocalDirectoryE2ETests.Edit_LocalSource_TypeSelect_Is_Disabled` | Bestanden |
| Lokale Quelle durchläuft Scan + Klassifizierung im echten Host; Inhalte über API abrufbar | `LocalMediaSourcePipelineE2ETests.LocalSource_ScanAndClassify_ExposesTvShowViaApi` (HTTP-Ebene via `WebApplicationFactory`) | Bestanden |
| Streaming (`GET .../stream`) und Download (`GET .../download`) aus lokalem Verzeichnis liefern 200, `video/*`-Content-Type und Dateibytes | `LocalMediaSourceStreamingE2ETests.Stream_LocalMovie_ReturnsOkWithVideoContent`, `LocalMediaSourceStreamingE2ETests.Download_LocalMovie_ReturnsOkWithFileName` | Bestanden |
| Zugriff ohne `MediaSourceUsers`-Freigabe → HTTP 401 am echten Endpunkt | `LocalMediaSourceStreamingE2ETests.Stream_LocalMovie_WithoutSourceAccess_ReturnsUnauthorized` | Bestanden |

Zusätzlich bestanden alle 45 bereits vorhandenen E2E-Tests als Regression (8 neue Feature-E2E-Tests + 45 bestehende = 53 gesamt).

## Zusammenfassung

- Gesamt: 351
- Bestanden: 351
- Fehlgeschlagen: 0
- Übersprungen: 0
- Davon E2E (`Category=E2E`): 53 bestanden / 0 fehlgeschlagen
- Vergleich Iteration 1: 337 Tests (285 Unit + 52 E2E) → jetzt 351 Tests (+13 Unit, +1 E2E)

## Testabdeckung

**Abdeckung:** 92,8 % Zeilenabdeckung (72568/78199 Zeilen, XPlat Code Coverage / Cobertura über den vollständigen Testlauf inkl. E2E)

Abdeckung der zentralen Feature-Dateien:

| Datei | Abdeckung |
|-------|-----------|
| `VideoWebPlayer/Services/LocalMediaSourceReader.cs` | 84,5 % |
| `VideoWebPlayer/Services/MediaSourceReaderDispatcher.cs` | 90,9 % |
| `VideoWebPlayer/Services/MediaEntryFilter.cs` | 100,0 % |
| `VideoWebPlayer/Data/MediaSourceExtensions.cs` | 100,0 % |
| `VideoWebPlayer/Data/MediaSource.cs` | 90,0 % |
| `VideoWebPlayer/Components/Pages/Admin/MediaSources/MediaSourceAdminDetails.razor` | 57,5 % |
| `VideoWebPlayer/Components/Pages/Admin/MediaSources/MediaSourceAdmin.razor` | 45,8 % |
| `VideoWebPlayer/Components/Layout/NavMenu.razor` | 71,4 % |
| `VideoWebPlayer/Services/MediaSourceScanner.cs` | 77,1 % |
| `VideoWebPlayer/Services/MediaSourceClassifier.cs` | 63,9 % |
| `VideoWebPlayer/Controllers/ItemsController.cs` | 61,0 % |
| `VideoWebPlayer/Services/Backups/VideoWebPlayerBackupData.cs` | 73,6 % |

Alle Dateien unter 80 % Zeilenabdeckung (161 von 337 Dateien mit messbaren Zeilen):

| Datei | Abdeckung |
|-------|-----------|
| `VideoWebPlayer.Client/Models/ContinueWatchingMutationResult.cs` | 0.0 % |
| `VideoWebPlayer.Client/Models/DtoRecentEntry.cs` | 0.0 % |
| `VideoWebPlayer.Client/Models/ImpersonateRequest.cs` | 0.0 % |
| `VideoWebPlayer/Components/Account/IdentityNoOpEmailSender.cs` | 0.0 % |
| `VideoWebPlayer/Components/Account/IdentityUserAccessor.cs` | 0.0 % |
| `VideoWebPlayer/Components/Account/Pages/ConfirmEmail.razor` | 0.0 % |
| `VideoWebPlayer/Components/Account/Pages/ConfirmEmailChange.razor` | 0.0 % |
| `VideoWebPlayer/Components/Account/Pages/ExternalLogin.razor` | 0.0 % |
| `VideoWebPlayer/Components/Account/Pages/ForgotPassword.razor` | 0.0 % |
| `VideoWebPlayer/Components/Account/Pages/LoginWith2fa.razor` | 0.0 % |
| `VideoWebPlayer/Components/Account/Pages/LoginWithRecoveryCode.razor` | 0.0 % |
| `VideoWebPlayer/Components/Account/Pages/Manage/ChangePassword.razor` | 0.0 % |
| `VideoWebPlayer/Components/Account/Pages/Manage/DeletePersonalData.razor` | 0.0 % |
| `VideoWebPlayer/Components/Account/Pages/Manage/Disable2fa.razor` | 0.0 % |
| `VideoWebPlayer/Components/Account/Pages/Manage/Email.razor` | 0.0 % |
| `VideoWebPlayer/Components/Account/Pages/Manage/EnableAuthenticator.razor` | 0.0 % |
| `VideoWebPlayer/Components/Account/Pages/Manage/ExternalLogins.razor` | 0.0 % |
| `VideoWebPlayer/Components/Account/Pages/Manage/GenerateRecoveryCodes.razor` | 0.0 % |
| `VideoWebPlayer/Components/Account/Pages/Manage/Index.razor` | 0.0 % |
| `VideoWebPlayer/Components/Account/Pages/Manage/PersonalData.razor` | 0.0 % |
| `VideoWebPlayer/Components/Account/Pages/Manage/ResetAuthenticator.razor` | 0.0 % |
| `VideoWebPlayer/Components/Account/Pages/Manage/SetPassword.razor` | 0.0 % |
| `VideoWebPlayer/Components/Account/Pages/Manage/TwoFactorAuthentication.razor` | 0.0 % |
| `VideoWebPlayer/Components/Account/Pages/RegisterConfirmation.razor` | 0.0 % |
| `VideoWebPlayer/Components/Account/Pages/ResendEmailConfirmation.razor` | 0.0 % |
| `VideoWebPlayer/Components/Account/Pages/ResetPassword.razor` | 0.0 % |
| `VideoWebPlayer/Components/Account/Shared/ExternalLoginPicker.razor` | 0.0 % |
| `VideoWebPlayer/Components/Account/Shared/ManageLayout.razor` | 0.0 % |
| `VideoWebPlayer/Components/Account/Shared/ManageNavMenu.razor` | 0.0 % |
| `VideoWebPlayer/Components/Account/Shared/RedirectToLogin.razor` | 0.0 % |
| `VideoWebPlayer/Components/Account/Shared/ShowRecoveryCodes.razor` | 0.0 % |
| `VideoWebPlayer/Components/Pages/Actors/ActorDetails.razor` | 0.0 % |
| `VideoWebPlayer/Components/Pages/Actors/Actors.razor` | 0.0 % |
| `VideoWebPlayer/Components/Pages/Admin/GenreAdmin.razor` | 0.0 % |
| `VideoWebPlayer/Components/Pages/Admin/MediaSources/MediaSourceExplorer.razor` | 0.0 % |
| `VideoWebPlayer/Components/Pages/Admin/ProgramSettings.razor` | 0.0 % |
| `VideoWebPlayer/Components/Pages/Admin/Security.razor` | 0.0 % |
| `VideoWebPlayer/Components/Pages/Admin/UserManagement.razor` | 0.0 % |
| `VideoWebPlayer/Components/Pages/Errors/Error.razor` | 0.0 % |
| `VideoWebPlayer/Components/Pages/Samples/Auth.razor` | 0.0 % |
| `VideoWebPlayer/Components/Pages/Samples/Counter.razor` | 0.0 % |
| `VideoWebPlayer/Components/Pages/Samples/Weather.razor` | 0.0 % |
| `VideoWebPlayer/Components/Shared/Media/MediaBaseEntryList.razor` | 0.0 % |
| `VideoWebPlayer/Components/Shared/Media/VideoPlayer.razor` | 0.0 % |
| `VideoWebPlayer/Components/Shared/Media/WatchedIndicator.razor` | 0.0 % |
| `VideoWebPlayer/Controllers/Attributes/ConnectionCheckAttribute.cs` | 0.0 % |
| `VideoWebPlayer/Controllers/Exceptions/RecordNotFoundException.cs` | 0.0 % |
| `VideoWebPlayer/Controllers/SourceIconsController.cs` | 0.0 % |
| `VideoWebPlayer/Data/BlockedLoginIp.cs` | 0.0 % |
| `VideoWebPlayer/Data/MediaSourceIcon.cs` | 0.0 % |
| `VideoWebPlayer/Hubs/MediaUpdateHub.cs` | 0.0 % |
| `VideoWebPlayer/Services/DemoData/IDemoDataSetService.cs` | 0.0 % |
| `VideoWebPlayer/Services/SftpStreamWrapper.cs` | 0.0 % |
| `VideoWebPlayer/Services/UdpDiscoveryListener.cs` | 0.0 % |
| `VideoWebPlayer/Utils/LocalNetworkHelper.cs` | 0.0 % |
| `VideoWebPlayer/Services/SftpMediaSourceReader.cs` | 10.3 % |
| `VideoWebPlayer/Controllers/ContinueWatchingController.cs` | 11.1 % |
| `VideoWebPlayer/Services/HomeBackgroundImage/HomeBackgroundImageGenerator.cs` | 13.0 % |
| `VideoWebPlayer/Components/Account/IdentityRevalidatingAuthenticationStateProvider.cs` | 13.3 % |
| `VideoWebPlayer/Services/InternalConnectionService.cs` | 21.4 % |
| `VideoWebPlayer/Components/Shared/Media/ActorList.razor` | 23.1 % |
| `VideoWebPlayer/Services/DemoData/FileSystemDemoDataSetService.cs` | 24.2 % |
| `VideoWebPlayer/Components/Shared/Home/SeasonalGenreList.razor` | 26.0 % |
| `VideoWebPlayer/Components/Pages/TV/TVShowDetails.razor` | 27.0 % |
| `VideoWebPlayer/Data/Genre.cs` | 27.3 % |
| `VideoWebPlayer/Data/Movie.cs` | 27.6 % |
| `VideoWebPlayer/Controllers/FavoritesController.cs` | 29.7 % |
| `VideoWebPlayer/Services/Backups/ManualBackupJobService.cs` | 29.9 % |
| `VideoWebPlayer/Services/LoginIpBlockService.cs` | 30.1 % |
| `VideoWebPlayer.Client/VideoWebPlayerClient.cs` | 31.4 % |
| `VideoWebPlayer/Services/RecentEntryService.cs` | 33.3 % |
| `VideoWebPlayer/Components/Shared/Home/ContinueWatchingList.razor` | 33.8 % |
| `VideoWebPlayer/Controllers/PicturesController.cs` | 34.3 % |
| `VideoWebPlayer/Controllers/SourceGenresController.cs` | 36.9 % |
| `VideoWebPlayer/Services/FavoritesService.cs` | 37.2 % |
| `VideoWebPlayer/Migrations/20260830194227_UpdateMediaItemNavigations.cs` | 37.8 % |
| `VideoWebPlayer/Components/Shared/Home/RecentEntriesList.razor` | 39.6 % |
| `VideoWebPlayer/Migrations/20260307042726_RemoveMediaSourceTextIcon.cs` | 40.0 % |
| `VideoWebPlayer/Controllers/AuthController.cs` | 41.7 % |
| `VideoWebPlayer/Services/Updates/VideoWebPlayerUpdateSourceFactory.cs` | 42.9 % |
| `VideoWebPlayer/Components/Account/Pages/Shared/LoginForm.razor` | 44.4 % |
| `VideoWebPlayer/Components/Pages/Admin/MediaSources/MediaSourceAdmin.razor` | 45.8 % |
| `VideoWebPlayer/Services/GenreService.cs` | 45.8 % |
| `VideoWebPlayer/Migrations/20260830182331_MakePictureMediaItemIdNullable.cs` | 45.9 % |
| `VideoWebPlayer/Services/ContinueWatchingWorker.cs` | 46.7 % |
| `VideoWebPlayer/Migrations/20250810141338_UpdateMediaModels2.cs` | 47.7 % |
| `VideoWebPlayer/Components/Pages/Admin/Backups.razor` | 48.2 % |
| `VideoWebPlayer/Data/Entities/WatchedEntry.cs` | 50.0 % |
| `VideoWebPlayer/Data/GenreName.cs` | 50.0 % |
| `VideoWebPlayer/Data/MediaSourceUser.cs` | 50.0 % |
| `VideoWebPlayer/Data/MovieGenre.cs` | 50.0 % |
| `VideoWebPlayer/Data/TVShowGenre.cs` | 50.0 % |
| `VideoWebPlayer/Migrations/20250817090813_UpdateMediaModels21.cs` | 50.0 % |
| `VideoWebPlayer/Migrations/20260810152703_RenameEpisodeBackgroundImageColumns.cs` | 50.0 % |
| `VideoWebPlayer/Components/Shared/Home/FavoritesList.razor` | 51.9 % |
| `VideoWebPlayer/Controllers/AdminSourcesController.cs` | 52.6 % |
| `VideoWebPlayer/Services/Backups/RestoreInProgressMiddleware.cs` | 52.9 % |
| `VideoWebPlayer/Services/EpisodeBackgroundImage/EpisodeBackgroundImageOptionsValidator.cs` | 53.8 % |
| `VideoWebPlayer/Components/Pages/Movies/MovieCollectionDetails.razor` | 54.5 % |
| `VideoWebPlayer/Migrations/20250810140938_UpdateMediaModels1.cs` | 54.6 % |
| `VideoWebPlayer/Components/Pages/Admin/MediaSources/MediaSourceAdminDetails.razor` | 57.5 % |
| `VideoWebPlayer/Components/Account/IdentityRedirectManager.cs` | 58.3 % |
| `VideoWebPlayer/Services/MediaUpdateNotificationService.cs` | 58.7 % |
| `VideoWebPlayer/Data/TVShowEpisode.cs` | 58.8 % |
| `VideoWebPlayer/Services/Authentication/InternalVideoWebPlayerClient.cs` | 59.0 % |
| `VideoWebPlayer/Controllers/UnlockedMediaController.cs` | 59.5 % |
| `VideoWebPlayer/Migrations/20250814180536_UpdateMediaModels8.cs` | 60.0 % |
| `VideoWebPlayer/Migrations/20250816072812_UpdateMediaModels13.cs` | 60.0 % |
| `VideoWebPlayer/Migrations/20260305091241_AddMediaSourceIcon.cs` | 60.0 % |
| `VideoWebPlayer/Components/Account/IdentityComponentsEndpointRouteBuilderExtensions.cs` | 60.2 % |
| `VideoWebPlayer/Services/ProgramSettingsService.cs` | 60.3 % |
| `VideoWebPlayer/Controllers/ItemsController.cs` | 61.0 % |
| `VideoWebPlayer/Migrations/20250816071104_UpdateMediaModels12.cs` | 61.1 % |
| `VideoWebPlayer/Migrations/20250817132350_UpdateMediaModels22.cs` | 61.1 % |
| `VideoWebPlayer/Migrations/20260810144513_AddPictureGeneratedBackgroundProperties.cs` | 61.5 % |
| `VideoWebPlayer/Migrations/20250817135431_UpdateMediaModels23.cs` | 61.9 % |
| `VideoWebPlayer/Migrations/20250814162930_UpdateMediaModels7.cs` | 62.3 % |
| `VideoWebPlayer/Migrations/20250812173712_UpdateMediaModels5.cs` | 62.4 % |
| `VideoWebPlayer/Migrations/20260810144442_AddEpisodeBackgroundImageProperties.cs` | 62.8 % |
| `VideoWebPlayer/Components/Pages/MediaSources/MediaSourceDetails.razor` | 63.0 % |
| `VideoWebPlayer/Migrations/20250817074737_UpdateMediaModels19.cs` | 63.2 % |
| `VideoWebPlayer/Services/Backups/VideoWebPlayerBackupFacade.cs` | 63.6 % |
| `VideoWebPlayer/Migrations/20250815153324_UpdateMediaModels10.cs` | 63.6 % |
| `VideoWebPlayer/Migrations/20250817060241_UpdateMediaModels16.cs` | 63.6 % |
| `VideoWebPlayer/Migrations/20250818183736_UpdateMediaModels24.cs` | 63.6 % |
| `VideoWebPlayer/Migrations/20260225191438_AddMediaCollectionClassifyable.cs` | 63.6 % |
| `VideoWebPlayer/Migrations/20260810051034_AddApplicationTitleToSetup.cs` | 63.6 % |
| `VideoWebPlayer/Migrations/20260827164650_AddContinueWatchingEndThresholdSeconds.cs` | 63.6 % |
| `VideoWebPlayer/Migrations/20260922180232_AddMediaSourceSourceType.cs` | 63.6 % |
| `VideoWebPlayer/Services/MediaSourceClassifier.cs` | 63.9 % |
| `VideoWebPlayer/Data/ApplicationDbContext.cs` | 64.5 % |
| `VideoWebPlayer/Migrations/20250810082431_AddIsAdminToApplicationUser.cs` | 65.0 % |
| `VideoWebPlayer/Migrations/20250813051648_UpdateMediaModels6.cs` | 65.0 % |
| `VideoWebPlayer/Migrations/20260307073741_AddScanSettingsToSetup.cs` | 65.0 % |
| `VideoWebPlayer/Migrations/20260831050031_AddActorRoleAndOrder.cs` | 65.8 % |
| `VideoWebPlayer/Migrations/20260820183537_AddManualMetadataEditFlag.cs` | 66.0 % |
| `VideoWebPlayer/Components/Account/Shared/StatusMessage.razor` | 66.7 % |
| `VideoWebPlayer/Data/UnlockedMediaEntry.cs` | 66.7 % |
| `VideoWebPlayer/Services/WhitelistIpMiddleware.cs` | 66.7 % |
| `VideoWebPlayer/Controllers/ActorsController.cs` | 67.5 % |
| `VideoWebPlayer/Migrations/20250817063844_UpdateMediaModels18.cs` | 67.6 % |
| `VideoWebPlayer/Controllers/Attributes/ApiTokenCheckAttribute.cs` | 69.4 % |
| `VideoWebPlayer/Migrations/20260822103156_AddContinueWatchingListOrder.cs` | 69.6 % |
| `VideoWebPlayer/Data/Entities/ContinueWatchingEntry.cs` | 70.0 % |
| `VideoWebPlayer/Migrations/20260305103130_AddMediaSourceIconUpload.cs` | 70.7 % |
| `VideoWebPlayer/Controllers/SourcesController.cs` | 70.8 % |
| `VideoWebPlayer/Components/Layout/NavMenu.razor` | 71.4 % |
| `VideoWebPlayer/Services/Backups/VideoWebPlayerBackupDataFactory.cs` | 71.4 % |
| `VideoWebPlayer.Client/Models/DtoSource.cs` | 72.7 % |
| `VideoWebPlayer/Controllers/BackupsController.cs` | 73.0 % |
| `VideoWebPlayer/Services/Backups/VideoWebPlayerBackupData.cs` | 73.6 % |
| `VideoWebPlayer/Components/Shared/Media/UnlockButton.razor` | 74.2 % |
| `VideoWebPlayer/Components/Account/Pages/Login.razor` | 75.0 % |
| `VideoWebPlayer/Migrations/20250812164844_UpdateMediaModels4.cs` | 75.0 % |
| `VideoWebPlayer/Services/Backups/VideoWebPlayerBackupRestoreGuard.cs` | 75.0 % |
| `VideoWebPlayer/Components/Account/Pages/Register.razor` | 75.4 % |
| `VideoWebPlayer/Services/Updates/UpdateSettingsInitializer.cs` | 76.9 % |
| `VideoWebPlayer/Services/MediaSourceScanner.cs` | 77.1 % |
| `VideoWebPlayer/Services/ActorBackfillWorker.cs` | 77.8 % |
| `VideoWebPlayer/Services/WatchedStatusService.cs` | 77.9 % |
| `VideoWebPlayer/Services/Authentication/IAuthService.cs` | 79.7 % |

## Fehlende Tests

Quelle: `Coverage-Daten`

Dateien mit 0 % Zeilenabdeckung (55, unverändert zu Iteration 1 — sämtlich preexisting, keine neuen Feature-Dateien darunter):

- `VideoWebPlayer.Client/Models/ContinueWatchingMutationResult.cs` — 0 % Abdeckung
- `VideoWebPlayer.Client/Models/DtoRecentEntry.cs` — 0 % Abdeckung
- `VideoWebPlayer.Client/Models/ImpersonateRequest.cs` — 0 % Abdeckung
- `VideoWebPlayer/Components/Account/IdentityNoOpEmailSender.cs` — 0 % Abdeckung
- `VideoWebPlayer/Components/Account/IdentityUserAccessor.cs` — 0 % Abdeckung
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
- `VideoWebPlayer/Components/Account/Shared/ExternalLoginPicker.razor` — 0 % Abdeckung
- `VideoWebPlayer/Components/Account/Shared/ManageLayout.razor` — 0 % Abdeckung
- `VideoWebPlayer/Components/Account/Shared/ManageNavMenu.razor` — 0 % Abdeckung
- `VideoWebPlayer/Components/Account/Shared/RedirectToLogin.razor` — 0 % Abdeckung
- `VideoWebPlayer/Components/Account/Shared/ShowRecoveryCodes.razor` — 0 % Abdeckung
- `VideoWebPlayer/Components/Pages/Actors/ActorDetails.razor` — 0 % Abdeckung
- `VideoWebPlayer/Components/Pages/Actors/Actors.razor` — 0 % Abdeckung
- `VideoWebPlayer/Components/Pages/Admin/GenreAdmin.razor` — 0 % Abdeckung
- `VideoWebPlayer/Components/Pages/Admin/MediaSources/MediaSourceExplorer.razor` — 0 % Abdeckung
- `VideoWebPlayer/Components/Pages/Admin/ProgramSettings.razor` — 0 % Abdeckung
- `VideoWebPlayer/Components/Pages/Admin/Security.razor` — 0 % Abdeckung
- `VideoWebPlayer/Components/Pages/Admin/UserManagement.razor` — 0 % Abdeckung
- `VideoWebPlayer/Components/Pages/Errors/Error.razor` — 0 % Abdeckung
- `VideoWebPlayer/Components/Pages/Samples/Auth.razor` — 0 % Abdeckung
- `VideoWebPlayer/Components/Pages/Samples/Counter.razor` — 0 % Abdeckung
- `VideoWebPlayer/Components/Pages/Samples/Weather.razor` — 0 % Abdeckung
- `VideoWebPlayer/Components/Shared/Media/MediaBaseEntryList.razor` — 0 % Abdeckung
- `VideoWebPlayer/Components/Shared/Media/VideoPlayer.razor` — 0 % Abdeckung
- `VideoWebPlayer/Components/Shared/Media/WatchedIndicator.razor` — 0 % Abdeckung
- `VideoWebPlayer/Controllers/Attributes/ConnectionCheckAttribute.cs` — 0 % Abdeckung
- `VideoWebPlayer/Controllers/Exceptions/RecordNotFoundException.cs` — 0 % Abdeckung
- `VideoWebPlayer/Controllers/SourceIconsController.cs` — 0 % Abdeckung
- `VideoWebPlayer/Data/BlockedLoginIp.cs` — 0 % Abdeckung
- `VideoWebPlayer/Data/MediaSourceIcon.cs` — 0 % Abdeckung
- `VideoWebPlayer/Hubs/MediaUpdateHub.cs` — 0 % Abdeckung
- `VideoWebPlayer/Services/DemoData/IDemoDataSetService.cs` — 0 % Abdeckung
- `VideoWebPlayer/Services/SftpStreamWrapper.cs` — 0 % Abdeckung
- `VideoWebPlayer/Services/UdpDiscoveryListener.cs` — 0 % Abdeckung
- `VideoWebPlayer/Utils/LocalNetworkHelper.cs` — 0 % Abdeckung
