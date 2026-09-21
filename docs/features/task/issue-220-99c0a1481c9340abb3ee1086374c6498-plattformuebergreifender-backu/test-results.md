# Test-Ergebnisse

## Ergebnis

**Status:** Keine Fehler

Alle 286 ausgeführten Tests bestanden (235 Nicht-E2E + 45 E2E + 6 MarkdownLinkCheck). Alle geplanten E2E-Szenarien existieren und sind bestanden — die `BackupUploadE2ETests` umfassen inzwischen 11 Szenarien, einschließlich der in Iteration 3 hinzugekommenen Tests `BackupUpload_InterruptedSession_CanDiscardResumeHint`, `BackupUpload_NonJsonErrorResponse_ShowsFriendlyMessage` und `BackupUpload_NavigatingAwayDuringUpload_AbortsWithoutRedirectBack`.

Ausgeführte Befehle (Arbeitsverzeichnis: Repo-Root, `-c Release`):

- `dotnet build VideoPlayer.sln -c Release -p:NoWarn=NU1903` — 0 Fehler, 0 Warnungen
- `dotnet test VideoWebPlayer.Tests/VideoWebPlayer.Tests.csproj --no-build -c Release --filter "Category!=E2E" --collect:"XPlat Code Coverage" --logger "console;verbosity=normal"` — 235/235 bestanden
- `dotnet test VideoWebPlayer.Tests/VideoWebPlayer.Tests.csproj --no-build -c Release --filter "Category=E2E" --collect:"XPlat Code Coverage" --logger "console;verbosity=normal"` — 45/45 bestanden
- `dotnet test tools/MarkdownLinkCheck.Tests/MarkdownLinkCheck.Tests.csproj --no-build -c Release --logger "console;verbosity=normal"` — 6/6 bestanden

## E2E-Abdeckung

| Szenario | Test / Testklasse | Ergebnis |
|----------|-------------------|----------|
| Happy Path: gültige `.bak` hochladen → Fortschritt, `backupStatus`, Archiv-Eintrag, Historie „Upload" | `BackupUploadE2ETests.BackupUpload_ValidFile_ShowsProgressAndImportsBackup` | Bestanden |
| Multi-Chunk: mehrere Chunk-Requests mit Offset-Fortschaltung | `BackupUploadE2ETests.BackupUpload_LargeFile_UsesMultipleChunks` | Bestanden |
| Resume nach Abbruch: `RouteAsync`/`AbortAsync` auf `**/upload/chunk`, Fortsetzung ab Server-Offset | `BackupUploadE2ETests.BackupUpload_Interrupted_ResumesFromServerOffset` | Bestanden |
| Ungültige Datei (ZIP ohne `manifest.json`) → `backupError`, kein Archiv-Eintrag | `BackupUploadE2ETests.BackupUpload_InvalidFile_ShowsError` | Bestanden |
| Limit-Überschreitung (`Backups:MaxUploadSizeBytes` klein gesetzt) → Fehlermeldung, kein Import | `BackupUploadE2ETests.BackupUpload_ExceedingLimit_ShowsError` | Bestanden |
| Nicht-Admin (eingeloggter regulärer Benutzer) sieht „Nicht autorisiert", kein Upload-Control | `BackupUploadE2ETests.BackupUpload_NonAdmin_SeesNoUploadControl` | Bestanden |
| Unterbrochene Session: Resume-Hinweis in der UI bei vorhandener `localStorage`-Session | `BackupUploadE2ETests.BackupUpload_InterruptedSession_ShowsResumeHint` | Bestanden |
| Unterbrochene Session (Iteration 3): Resume-Hinweis lässt sich verwerfen | `BackupUploadE2ETests.BackupUpload_InterruptedSession_CanDiscardResumeHint` | Bestanden |
| Laufender Upload: Upload-Button während des Laufs deaktiviert | `BackupUploadE2ETests.BackupUpload_WhileRunning_DisablesUploadButton` | Bestanden |
| Nicht-JSON-Fehlerantwort (Iteration 3): freundliche Fehlermeldung statt Roh-Response | `BackupUploadE2ETests.BackupUpload_NonJsonErrorResponse_ShowsFriendlyMessage` | Bestanden |
| Navigation während Upload (Iteration 3): Upload bricht ab, keine Rück-Navigation | `BackupUploadE2ETests.BackupUpload_NavigatingAwayDuringUpload_AbortsWithoutRedirectBack` | Bestanden |

## Zusammenfassung

- Gesamt: 286 (235 Nicht-E2E + 45 E2E + 6 MarkdownLinkCheck)
- Bestanden: 286
- Fehlgeschlagen: 0
- Übersprungen: 0

## Testabdeckung

**Abdeckung:** 92,4 % (Zeilen, gemergte Cobertura-Daten aus Nicht-E2E- und E2E-Lauf mit `XPlat Code Coverage`; Quelldatei-Ebene, ohne generierte Migrations-/Designer-Dateien und `Program.cs` als Einstiegspunkt)

Feature-relevante Dateien: `KestrelLimits.cs` 100,0 %, `BackupUploadSessionService.cs` 88,5 %, `BackupsController.cs` 73,0 %, `VideoWebPlayerBackupFacade.cs` 63,6 %, `Backups.razor` 48,7 % (UI-Anteile nur über E2E erreichbar).

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
| `VideoWebPlayer/Components/Pages/Admin/MediaSources/MediaSourceAdminDetails.razor` | 0.0 % |
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
| `VideoWebPlayer/Data/MediaSourceExtensions.cs` | 0.0 % |
| `VideoWebPlayer/Data/MediaSourceIcon.cs` | 0.0 % |
| `VideoWebPlayer/Events/MediaSourceCreatedEvent.cs` | 0.0 % |
| `VideoWebPlayer/Events/MediaSourceUpdatedEvent.cs` | 0.0 % |
| `VideoWebPlayer/Hubs/MediaUpdateHub.cs` | 0.0 % |
| `VideoWebPlayer/Services/DemoData/IDemoDataSetService.cs` | 0.0 % |
| `VideoWebPlayer/Services/SftpStreamWrapper.cs` | 0.0 % |
| `VideoWebPlayer/Services/UdpDiscoveryListener.cs` | 0.0 % |
| `VideoWebPlayer/Utils/LocalNetworkHelper.cs` | 0.0 % |
| `VideoWebPlayer/Services/SftpMediaSourceReader.cs` | 10.2 % |
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
| `VideoWebPlayer/Services/Backups/ManualBackupJobService.cs` | 29.9 % |
| `VideoWebPlayer/Services/LoginIpBlockService.cs` | 30.1 % |
| `VideoWebPlayer.Client/VideoWebPlayerClient.cs` | 32.2 % |
| `VideoWebPlayer/Services/RecentEntryService.cs` | 33.3 % |
| `VideoWebPlayer/Controllers/PicturesController.cs` | 34.3 % |
| `VideoWebPlayer/Controllers/SourceGenresController.cs` | 36.9 % |
| `VideoWebPlayer/Services/FavoritesService.cs` | 37.2 % |
| `VideoWebPlayer/Controllers/FavoritesController.cs` | 37.8 % |
| `VideoWebPlayer/Components/Shared/Home/RecentEntriesList.razor` | 39.6 % |
| `VideoWebPlayer/Components/Shared/Home/ContinueWatchingList.razor` | 41.5 % |
| `VideoWebPlayer/Controllers/AuthController.cs` | 41.7 % |
| `VideoWebPlayer/Components/Pages/Admin/MediaSources/MediaSourceAdmin.razor` | 41.9 % |
| `VideoWebPlayer/Services/Updates/VideoWebPlayerUpdateSourceFactory.cs` | 42.9 % |
| `VideoWebPlayer/Components/Account/Pages/Shared/LoginForm.razor` | 44.4 % |
| `VideoWebPlayer/Services/GenreService.cs` | 45.8 % |
| `VideoWebPlayer/Services/ContinueWatchingWorker.cs` | 46.7 % |
| `VideoWebPlayer/Components/Pages/Admin/Backups.razor` | 48.7 % |
| `VideoWebPlayer/Controllers/ItemsController.cs` | 49.5 % |
| `VideoWebPlayer/Data/Entities/WatchedEntry.cs` | 50.0 % |
| `VideoWebPlayer/Data/GenreName.cs` | 50.0 % |
| `VideoWebPlayer/Data/MediaSourceUser.cs` | 50.0 % |
| `VideoWebPlayer/Data/MovieGenre.cs` | 50.0 % |
| `VideoWebPlayer/Data/TVShowGenre.cs` | 50.0 % |
| `VideoWebPlayer/Services/MediaUpdateNotificationService.cs` | 50.0 % |
| `VideoWebPlayer/Controllers/AdminSourcesController.cs` | 52.6 % |
| `VideoWebPlayer/Services/Backups/RestoreInProgressMiddleware.cs` | 52.9 % |
| `VideoWebPlayer/Services/EpisodeBackgroundImage/EpisodeBackgroundImageOptionsValidator.cs` | 53.8 % |
| `VideoWebPlayer/Components/Pages/Movies/MovieCollectionDetails.razor` | 54.5 % |
| `VideoWebPlayer/Components/Shared/Home/FavoritesList.razor` | 55.7 % |
| `VideoWebPlayer/Components/Account/IdentityRedirectManager.cs` | 58.3 % |
| `VideoWebPlayer/Data/TVShowEpisode.cs` | 58.8 % |
| `VideoWebPlayer/Controllers/UnlockedMediaController.cs` | 59.5 % |
| `VideoWebPlayer/Components/Account/IdentityComponentsEndpointRouteBuilderExtensions.cs` | 60.2 % |
| `VideoWebPlayer/Services/ProgramSettingsService.cs` | 60.3 % |
| `VideoWebPlayer/Services/MediaSourceClassifier.cs` | 60.7 % |
| `VideoWebPlayer/Data/ApplicationDbContext.cs` | 60.8 % |
| `VideoWebPlayer/Components/Pages/MediaSources/MediaSourceDetails.razor` | 63.0 % |
| `VideoWebPlayer/Services/Backups/VideoWebPlayerBackupFacade.cs` | 63.6 % |
| `VideoWebPlayer/Data/UnlockedMediaEntry.cs` | 66.7 % |
| `VideoWebPlayer/Components/Account/Shared/StatusMessage.razor` | 66.7 % |
| `VideoWebPlayer/Services/WhitelistIpMiddleware.cs` | 66.7 % |
| `VideoWebPlayer/Controllers/ActorsController.cs` | 67.5 % |
| `VideoWebPlayer/Controllers/Attributes/ApiTokenCheckAttribute.cs` | 69.4 % |
| `VideoWebPlayer/Components/Layout/NavMenu.razor` | 69.6 % |
| `VideoWebPlayer/Data/Entities/ContinueWatchingEntry.cs` | 70.0 % |
| `VideoWebPlayer/Controllers/SourcesController.cs` | 70.8 % |
| `VideoWebPlayer/Services/Backups/VideoWebPlayerBackupData.cs` | 71.1 % |
| `VideoWebPlayer/Services/Backups/VideoWebPlayerBackupDataFactory.cs` | 71.4 % |
| `VideoWebPlayer.Client/Models/DtoSource.cs` | 72.7 % |
| `VideoWebPlayer/Controllers/BackupsController.cs` | 73.0 % |
| `VideoWebPlayer/Components/Shared/Media/UnlockButton.razor` | 74.2 % |
| `VideoWebPlayer/Components/Account/Pages/Login.razor` | 75.0 % |
| `VideoWebPlayer/Services/Backups/VideoWebPlayerBackupRestoreGuard.cs` | 75.0 % |
| `VideoWebPlayer/Components/Account/Pages/Register.razor` | 75.4 % |
| `VideoWebPlayer/Services/Updates/UpdateSettingsInitializer.cs` | 76.9 % |
| `VideoWebPlayer/Services/MediaSourceScanner.cs` | 77.1 % |
| `VideoWebPlayer/Services/ActorBackfillWorker.cs` | 77.8 % |
| `VideoWebPlayer/Services/WatchedStatusService.cs` | 77.9 % |
| `VideoWebPlayer/Services/Authentication/IAuthService.cs` | 79.7 % |

## Fehlende Tests

Quelle: `Coverage-Daten`

Alle folgenden Quelldateien haben 0 % Zeilenabdeckung (gemergte Coverage aus Nicht-E2E- und E2E-Lauf). Es handelt sich ausschließlich um **vor der Umsetzung bereits ungetestete** Dateien (überwiegend Identity-Scaffold-Seiten, Demo-/Sample-Seiten, DTOs) — keine davon wurde durch dieses Feature angelegt oder geändert. Alle neuen Feature-Dateien sind abgedeckt (`KestrelLimits.cs` 100,0 %, `BackupUploadSessionService.cs` 88,5 %; `backupUpload.js` ist JS und wird über die E2E-Tests `BackupUploadE2ETests` funktional nachgewiesen, Coverlet erfasst kein JavaScript).

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
- `VideoWebPlayer/Components/Pages/Admin/MediaSources/MediaSourceAdminDetails.razor` — 0 % Abdeckung
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
- `VideoWebPlayer/Data/MediaSourceExtensions.cs` — 0 % Abdeckung
- `VideoWebPlayer/Data/MediaSourceIcon.cs` — 0 % Abdeckung
- `VideoWebPlayer/Events/MediaSourceCreatedEvent.cs` — 0 % Abdeckung
- `VideoWebPlayer/Events/MediaSourceUpdatedEvent.cs` — 0 % Abdeckung
- `VideoWebPlayer/Hubs/MediaUpdateHub.cs` — 0 % Abdeckung
- `VideoWebPlayer/Services/DemoData/IDemoDataSetService.cs` — 0 % Abdeckung
- `VideoWebPlayer/Services/SftpStreamWrapper.cs` — 0 % Abdeckung
- `VideoWebPlayer/Services/UdpDiscoveryListener.cs` — 0 % Abdeckung
- `VideoWebPlayer/Utils/LocalNetworkHelper.cs` — 0 % Abdeckung
