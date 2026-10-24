# Test-Ergebnisse

## Ergebnis

**Status:** Keine Fehler

## Fehlgeschlagene Tests

1 einmaliger Ausfall im Volllauf, als Flake verifiziert (siehe Zusammenfassung):

### `VideoWebPlayer.Tests/PlaylistPlaybackE2ETests.cs`

- **PlaylistSkipLockedEntriesE2ETest** — `System.TimeoutException: Timeout 30000ms exceeded` beim Warten auf `#playlist-detail-selected-entry` (`PlaylistsE2ETestBase.SelectEntryAsync`, Z. 285). Playwright-E2E, nicht im Anforderungsbereich (Playlist-Wiedergabe). In 3 von 3 isolierten Wiederholungen bestanden (10 s / 20 s / 10 s); der Fehlschlag im Volllauf (53 s) ist die bekannte Last-Flakiness einzelner Playwright-Tests. Kein Test wurde deaktiviert oder abgeschwächt.

## E2E-Abdeckung

Laut `plan.md`/`plan-check.md` sind keine neuen E2E-Tests erforderlich (kein geänderter Benutzerfluss; die geänderte Eigenschaft — Share-Flags des serverseitigen Handles — ist über HTTP prinzipiell nicht beobachtbar). Geplante/bestehende Szenarien:

| Szenario | Test / Testklasse | Ergebnis |
|----------|-------------------|----------|
| Segment-Abruf über HTTP — unverändertes Auslieferungsverhalten (200, `video/mp2t`, vollständiger Inhalt) | `LiveStreamE2ETests.PairedDevice_RequestSegmentWithAccessToken_ReturnsSegment` | Bestanden (7 s) |
| Löschbarkeit eines Segments bei offenem Auslieferungs-Stream (`FileShare.ReadWrite \| FileShare.Delete`) | Kein E2E geplant (begründet im Plan); Nachweis über `LiveStreamControllerTests.GetFile_TsSegment_FileCanBeDeletedWhileDeliveryStreamIsOpen` | Bestanden (340 ms) |

## Zusammenfassung

Voller Lauf (`dotnet test VideoWebPlayer.Tests/VideoWebPlayer.Tests.csproj --no-build --collect:"XPlat Code Coverage"`, Debug, nach `dotnet build VideoPlayer.sln` mit 0 Fehlern; Dauer 4,6 min):

- Gesamt: 2249
- Bestanden: 2247 (+ 1 Flake, isoliert 3/3 bestanden → faktisch 2248)
- Fehlgeschlagen: 1 (verifizierter Flake, siehe oben)
- Übersprungen: 1 — `DevicePlaylistE2ETests_Playback.PairedDevice_ReportsProgressForMovieWithoutCollection_ContinueWatchingListStillLoads` (bekannter Alt-Skip der Baseline, `[Fact(Skip = ...)]`)

Vergleich zur Baseline (2248 Tests, 2247 bestanden, 1 übersprungen): +1 Test (der neue Regressionstest), alle anforderungsrelevanten Tests im Lauf enthalten und grün:

- `LiveStreamControllerTests.GetFile_TsSegment_FileCanBeDeletedWhileDeliveryStreamIsOpen` — Bestanden (neu)
- `LiveStreamControllerTests.GetFile_TsSegment_ReturnsVideoMp2t` — Bestanden (auf `FileStreamResult` umgestellt)
- `LiveStreamE2ETests.PairedDevice_RequestSegmentWithAccessToken_ReturnsSegment` — Bestanden

## Testabdeckung

**Abdeckung:** 95,1 % Zeilen gesamt (145 619/153 112; Paket `VideoWebPlayer` 95,2 %, `VideoWebPlayer.Client` 79,9 %). Quelle: `test-results/71ba990b-…/coverage.cobertura.xml`; pro Datei aggregiert in `test-results/coverage-by-file.txt`.

Geänderte Datei: `VideoWebPlayer/Controllers/LiveStreamController.cs` — 85,2 % (52/61 Zeilen), über der 80-%-Schwelle.

Nachfolgend alle Quelldateien unter 80 % Zeilenabdeckung (generierte Dateien — `Migrations/`, `*.Designer.cs` — und Einstiegspunkte — `Program.cs` — ausgenommen: 53 Dateien):

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
| VideoWebPlayer/Components/Account/IdentityRevalidatingAuthenticationStateProvider.cs | 10,0 % |
| VideoWebPlayer/Services/SftpMediaSourceReader.cs | 11,4 % |
| VideoWebPlayer/Components/Shared/Media/ActorList.razor | 15,8 % |
| VideoWebPlayer/Services/DemoData/FileSystemDemoDataSetService.cs | 24,3 % |
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
| VideoWebPlayer/Services/FavoritesService.cs | 39,8 % |
| VideoWebPlayer/Services/MdnsAdvertiserWorker.cs | 44,1 % |
| VideoWebPlayer/Components/Account/Pages/Shared/LoginForm.razor | 46,2 % |
| VideoWebPlayer/Components/Pages/Admin/Backups.razor | 47,5 % |
| VideoWebPlayer/Components/Pages/Admin/MediaSources/MediaSourceAdmin.razor | 47,5 % |
| VideoWebPlayer/Services/RecentEntryService.cs | 49,3 % |
| VideoWebPlayer/Components/Account/Pages/Login.razor | 50,0 % |
| VideoWebPlayer/Controllers/MediaFileContentTypes.cs | 50,0 % |
| VideoWebPlayer/Data/GenreName.cs | 50,0 % |
| VideoWebPlayer/Data/MediaSourceUser.cs | 50,0 % |
| VideoWebPlayer/Data/MovieGenre.cs | 50,0 % |
| VideoWebPlayer/Data/TVShowGenre.cs | 50,0 % |
| VideoWebPlayer/Services/InternalConnectionService.cs | 50,0 % |
| VideoWebPlayer/Components/Playlists/PlaylistGenreEditor.razor | 50,9 % |
| VideoWebPlayer/Components/Account/IdentityComponentsEndpointRouteBuilderExtensions.cs | 51,1 % |
| VideoWebPlayer/Services/Updates/VideoWebPlayerUpdateSourceFactory.cs | 52,0 % |
| VideoWebPlayer/Components/Account/Shared/StatusMessage.razor | 52,9 % |
| VideoWebPlayer/Components/Shared/Home/FavoritesList.razor | 54,5 % |
| VideoWebPlayer/Data/TVShowEpisode.cs | 55,6 % |
| VideoWebPlayer/Services/Backups/RestoreInProgressMiddleware.cs | 56,7 % |
| VideoWebPlayer/Components/Pages/Movies/MovieCollectionDetails.razor | 57,4 % |
| VideoWebPlayer/Components/Pages/Admin/MediaSources/MediaSourceAdminDetails.razor | 57,4 % |
| VideoWebPlayer/Controllers/AdminSourcesController.cs | 57,7 % |
| VideoWebPlayer/Controllers/PicturesController.cs | 57,7 % |
| VideoWebPlayer/Components/Pages/Admin/Security.razor | 58,7 % |
| VideoWebPlayer/Services/MediaUpdateNotificationService.cs | 58,7 % |
| VideoWebPlayer/Services/GenreService.cs | 58,8 % |
| VideoWebPlayer/Services/EpisodeBackgroundImage/EpisodeBackgroundImageOptionsValidator.cs | 60,0 % |
| VideoWebPlayer/Components/Pages/MediaSources/MediaSourceDetails.razor | 60,3 % |
| VideoWebPlayer/Controllers/UnlockedMediaController.cs | 60,4 % |
| VideoWebPlayer/Components/Account/IdentityRedirectManager.cs | 60,5 % |
| VideoWebPlayer/Services/Backups/VideoWebPlayerBackupFacade.cs | 61,4 % |
| VideoWebPlayer/Components/Account/Shared/AccountLayout.razor | 61,5 % |
| VideoWebPlayer/Data/Genre.cs | 61,5 % |
| VideoWebPlayer/Components/Pages/Admin/GenreAdmin.razor | 62,4 % |
| VideoWebPlayer/Data/Entities/WatchedEntry.cs | 62,5 % |
| VideoWebPlayer/Controllers/ActorsController.cs | 64,5 % |
| VideoWebPlayer/Services/Backups/VideoWebPlayerBackupDataFactory.cs | 64,7 % |
| VideoWebPlayer/Services/LiveStreamWorker.cs | 64,8 % |
| VideoWebPlayer/Services/ProgramGuideEntryText.cs | 65,0 % |
| VideoWebPlayer/Controllers/SourceIconsController.cs | 65,5 % |
| VideoWebPlayer/Data/ApplicationDbContext.cs | 65,8 % |
| VideoWebPlayer/Components/Account/IdentityUserAccessor.cs | 66,7 % |
| VideoWebPlayer/Data/UnlockedMediaEntry.cs | 66,7 % |
| VideoWebPlayer/Components/Account/Pages/Register.razor | 67,5 % |
| VideoWebPlayer.Client/VideoWebPlayerClient.cs | 67,9 % |
| VideoWebPlayer/Controllers/SourcesController.cs | 68,4 % |
| VideoWebPlayer/Extensions/LocalConfigurationExtensions.cs | 70,3 % |
| VideoWebPlayer/Controllers/BackupsController.cs | 72,5 % |
| VideoWebPlayer.Client/Models/DtoSource.cs | 72,7 % |
| VideoWebPlayer/Controllers/AuthController.cs | 72,7 % |
| VideoWebPlayer/Components/Shared/Media/VideoPlayer.razor | 73,3 % |
| VideoWebPlayer/Components/Pages/Admin/TVSenderAdmin.razor | 74,8 % |
| VideoWebPlayer/Components/Playlists/PlaylistSortModeIcon.razor | 75,0 % |
| VideoWebPlayer/Components/Shared/Media/UnlockButton.razor | 75,0 % |
| VideoWebPlayer/Data/Entities/ContinueWatchingEntry.cs | 75,0 % |
| VideoWebPlayer/Services/IProgramScheduleService.cs | 75,0 % |
| VideoWebPlayer/Services/DiscoveryBaseUrlResolver.cs | 75,5 % |
| VideoWebPlayer/Services/Updates/UpdateSettingsInitializer.cs | 76,5 % |
| VideoWebPlayer/Services/Backups/VideoWebPlayerBackupData.cs | 76,8 % |
| VideoWebPlayer/Components/Layout/NavMenu.razor | 77,0 % |
| VideoWebPlayer/Services/Authentication/IAuthService.cs | 77,1 % |
| VideoWebPlayer/Services/ActorBackfillWorker.cs | 77,3 % |
| VideoWebPlayer/Data/ApplicationDbContext.PlaylistBackfillMarking.cs | 77,6 % |
| VideoWebPlayer/Controllers/ItemsController.cs | 77,7 % |
| VideoWebPlayer/Components/Pages/Admin/AdminIndex.razor | 77,8 % |
| VideoWebPlayer/Services/StaticAssetVersioner.cs | 77,8 % |
| VideoWebPlayer/Services/WatchedStatusService.cs | 78,9 % |
| VideoWebPlayer/Controllers/ProgramGuideController.cs | 78,9 % |
| VideoWebPlayer/Services/HomeBackgroundImage/HomeBackgroundImageGenerator.cs | 79,2 % |
| VideoWebPlayer/Services/ContinueWatchingWorker.cs | 79,5 % |
| VideoWebPlayer/Components/Pages/Admin/ProgramSettings.razor | 79,8 % |

## Fehlende Tests

Quelle: `Coverage-Daten` — Quelldateien mit 0 % Zeilenabdeckung (generierte Dateien, Konfigurationsdateien und Einstiegspunkte ausgenommen). Sämtliche Lücken sind Altbestand und stehen in keinem Zusammenhang mit dieser Anforderung:

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

## Hinweise

- Build vor dem Lauf: `dotnet build VideoPlayer.sln` (Debug) — 0 Fehler, 0 Warnungen.
- Testbefehl: `dotnet test VideoWebPlayer.Tests/VideoWebPlayer.Tests.csproj --no-build --collect:"XPlat Code Coverage" --logger "console;verbosity=normal"` (Coverage-Befehl erfolgreich; Log: `test-results/test-run-coverage.log`).
- Wiederholungsläufe des Flakes: `dotnet test … --no-build --filter "FullyQualifiedName=VideoWebPlayer.Tests.PlaylistPlaybackE2ETests.PlaylistSkipLockedEntriesE2ETest"` → 3/3 bestanden (12 s, 22 s, 12 s Gesamtdauer).
- Während des Testlaufs wurden keine Codeänderungen vorgenommen.
