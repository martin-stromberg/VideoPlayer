# Test-Ergebnisse

## Ergebnis

**Status:** Keine Fehler

- Build: `dotnet build VideoPlayer.sln -c Release -p:NoWarn=NU1903` — erfolgreich, 0 Fehler (gesamte Solution: `VideoWebPlayer`, `VideoWebPlayer.Client`, `VideoWebPlayer.Tests`, `tools/*`).
- Unit: `dotnet test VideoWebPlayer.Tests/VideoWebPlayer.Tests.csproj --no-build -c Release --filter "Category!=E2E" --collect:"XPlat Code Coverage"` — 341 bestanden, 0 fehlgeschlagen.
- Integration: `--filter "Category=Integration"` — 0 Tests entsprechen dem Filter (Suite ist faktisch leer, bekannter Ausgangszustand laut `inventory/tests.md`).
- E2E: `--filter "Category=E2E" --collect:"XPlat Code Coverage"` — 60 bestanden, 0 fehlgeschlagen (Playwright/Chromium, Kestrel).

## E2E-Abdeckung

Geplante Pflichtszenarien aus `plan.md` (Abschnitt „E2E-Tests") / `plan-check.md` (Abschnitt „E2E-Abdeckung") plus Szenarien aus Iteration 2 (Umbenennen-Aktion) und Iteration 3 (Widerruf-Bestätigungsdialog), Testklasse `VideoWebPlayer.Tests/DevicePairingE2ETests.cs` (`[Trait("Category","E2E")]`):

| Szenario | Test / Testklasse | Ergebnis |
|----------|-------------------|----------|
| Admin meldet sich an, öffnet `/admin/devices`, erzeugt Pairing-Code; Code + TTL werden angezeigt | `DevicePairingE2ETests.Admin_Creates_Pairing_Code_And_Sees_Code_With_Ttl` | Bestanden |
| Gesamtfluss: UI-Code → ECDH-Exchange → entschlüsseltes Token → `POST /api/auth/login` mit `X-API-Key` erfolgreich | `DevicePairingE2ETests.Pairing_Flow_UiCode_Exchange_Login_Succeeds` | Bestanden |
| Admin widerruft Gerät in `/admin/devices` (mit Bestätigungsdialog, Iteration 3) → Login mit Geräte-Token → `401` | `DevicePairingE2ETests.Revoked_Device_Token_Login_Fails` | Bestanden |
| Ungültiger Code → Fehler; IP nach wiederholten Fehlversuchen gesperrt und in `/admin/security` sichtbar (beide Teilnachweise im selben Szenario) | `DevicePairingE2ETests.Invalid_Codes_Block_Ip_And_Show_In_Security` | Bestanden |
| Nicht-Admin ruft `/admin/devices` auf → „Nicht autorisiert.", keine Pairing-Code-Schaltfläche, keine Geräteliste/Widerruf-Aktionen | `DevicePairingE2ETests.NonAdmin_Sees_NotAuthorized_On_Devices_Page` | Bestanden |
| Admin benennt gekoppeltes Gerät in `/admin/devices` um (Iteration 2, `RenameAsync` in `IDeviceTokenService`) | `DevicePairingE2ETests.Admin_Renames_Paired_Device` | Bestanden |
| Widerruf erfordert Bestätigungsdialog; Abbrechen lässt Gerät aktiv (Iteration 3, Review-Befund) | `DevicePairingE2ETests.Revoke_Requires_Confirmation_And_Can_Be_Cancelled` | Bestanden |

## Zusammenfassung

- Gesamt: 401
- Bestanden: 401
- Fehlgeschlagen: 0
- Übersprungen: 0

Aufschlüsselung: 341 Nicht-E2E (`Category!=E2E`), 60 E2E (`Category=E2E`), 0 Integration (Filter `Category=Integration` matcht keinen Test — Suite leer, wie im Ausgangszustand dokumentiert).

## Testabdeckung

**Abdeckung:** 93,08 % (Zeilenabdeckung gesamt; `XPlat Code Coverage`/Coverlet, Cobertura-Reports `VideoWebPlayer.Tests/TestResults/8304f87e-a2dc-48cd-b2c2-e4aec8c6e66e/coverage.cobertura.xml` [Unit-Lauf] und `VideoWebPlayer.Tests/TestResults/b4e732cb-fef7-4810-b75d-8323ac252d24/coverage.cobertura.xml` [E2E-Lauf], pro Zeile gemergt)

Feature-relevante Dateien (diese Anforderung):

| Datei | Abdeckung |
|-------|-----------|
| `VideoWebPlayer/Services/PairingService.cs` | 100,0 % |
| `VideoWebPlayer/Controllers/PairingController.cs` | 100,0 % |
| `VideoWebPlayer/Services/Security/HashHelper.cs` | 100,0 % |
| `VideoWebPlayer/Components/Pages/Admin/AdminIndex.razor` | 100,0 % |
| `VideoWebPlayer/Services/DeviceTokenService.cs` (inkl. `RenameAsync`) | 96,4 % |
| `VideoWebPlayer/Components/Pages/Admin/Devices.razor` (inkl. Widerruf-Bestätigungsdialog, Iteration 3) | 93,2 % |
| `VideoWebPlayer/Migrations/20260923074815_AddDevicePairing.cs` | 91,1 % (generierter EF-Core-Code) |
| `VideoWebPlayer/Services/LoginIpBlockService.cs` (neue `LoginIpBlockServiceTests`, Iteration 3) | 82,8 % |
| `VideoWebPlayer/Controllers/Attributes/ApiTokenCheckAttribute.cs` | 82,9 % |
| `VideoWebPlayer/Data/PairedDevice.cs`, `PairingCode.cs`, `PairedDeviceConfiguration.cs`, `PairingCodeConfiguration.cs` | jeweils 100,0 % |
| `VideoWebPlayer.Client/Models/PairingExchangeRequest.cs`, `PairingExchangeResponse.cs` | jeweils 100,0 % |

Alle Dateien mit < 80 % Zeilenabdeckung (0 %-Dateien siehe Abschnitt „Fehlende Tests"; `Migrations/*` als generierter EF-Core-Code und `Program.cs` als Einstiegspunkt ausgenommen):

| Datei | Abdeckung |
|-------|-----------|
| `VideoWebPlayer/Services/SftpMediaSourceReader.cs` | 10,3 % |
| `VideoWebPlayer/Controllers/ContinueWatchingController.cs` | 11,1 % |
| `VideoWebPlayer/Services/HomeBackgroundImage/HomeBackgroundImageGenerator.cs` | 13,0 % |
| `VideoWebPlayer/Components/Account/IdentityRevalidatingAuthenticationStateProvider.cs` | 13,3 % |
| `VideoWebPlayer/Services/InternalConnectionService.cs` | 21,4 % |
| `VideoWebPlayer/Components/Shared/Media/ActorList.razor` | 23,1 % |
| `VideoWebPlayer/Services/DemoData/FileSystemDemoDataSetService.cs` | 24,2 % |
| `VideoWebPlayer/Components/Shared/Home/SeasonalGenreList.razor` | 26,0 % |
| `VideoWebPlayer/Components/Pages/TV/TVShowDetails.razor` | 27,0 % |
| `VideoWebPlayer/Data/Genre.cs` | 27,3 % |
| `VideoWebPlayer/Data/Movie.cs` | 27,6 % |
| `VideoWebPlayer/Services/Backups/ManualBackupJobService.cs` | 29,9 % |
| `VideoWebPlayer.Client/VideoWebPlayerClient.cs` | 32,2 % |
| `VideoWebPlayer/Services/RecentEntryService.cs` | 33,3 % |
| `VideoWebPlayer/Components/Shared/Home/ContinueWatchingList.razor` | 33,8 % |
| `VideoWebPlayer/Controllers/PicturesController.cs` | 34,3 % |
| `VideoWebPlayer/Controllers/SourceGenresController.cs` | 36,9 % |
| `VideoWebPlayer/Services/FavoritesService.cs` | 37,2 % |
| `VideoWebPlayer/Controllers/FavoritesController.cs` | 37,8 % |
| `VideoWebPlayer/Controllers/AuthController.cs` | 41,7 % |
| `VideoWebPlayer/Services/Updates/VideoWebPlayerUpdateSourceFactory.cs` | 42,9 % |
| `VideoWebPlayer/Components/Account/Pages/Shared/LoginForm.razor` | 44,4 % |
| `VideoWebPlayer/Services/GenreService.cs` | 45,8 % |
| `VideoWebPlayer/Components/Pages/Admin/MediaSources/MediaSourceAdmin.razor` | 45,8 % |
| `VideoWebPlayer/Services/ContinueWatchingWorker.cs` | 46,7 % |
| `VideoWebPlayer/Components/Pages/Admin/Backups.razor` | 48,2 % |
| `VideoWebPlayer/Data/GenreName.cs` | 50,0 % |
| `VideoWebPlayer/Data/MediaSourceUser.cs` | 50,0 % |
| `VideoWebPlayer/Data/MovieGenre.cs` | 50,0 % |
| `VideoWebPlayer/Data/TVShowGenre.cs` | 50,0 % |
| `VideoWebPlayer/Data/Entities/WatchedEntry.cs` | 50,0 % |
| `VideoWebPlayer/Controllers/AdminSourcesController.cs` | 52,6 % |
| `VideoWebPlayer/Services/Backups/RestoreInProgressMiddleware.cs` | 52,9 % |
| `VideoWebPlayer/Services/EpisodeBackgroundImage/EpisodeBackgroundImageOptionsValidator.cs` | 53,8 % |
| `VideoWebPlayer/Components/Pages/Movies/MovieCollectionDetails.razor` | 54,5 % |
| `VideoWebPlayer/Components/Shared/Home/FavoritesList.razor` | 55,7 % |
| `VideoWebPlayer/Components/Pages/Admin/MediaSources/MediaSourceAdminDetails.razor` | 58,0 % |
| `VideoWebPlayer/Components/Account/IdentityRedirectManager.cs` | 58,3 % |
| `VideoWebPlayer/Services/MediaUpdateNotificationService.cs` | 58,7 % |
| `VideoWebPlayer/Data/TVShowEpisode.cs` | 58,8 % |
| `VideoWebPlayer/Services/Authentication/InternalVideoWebPlayerClient.cs` | 59,0 % |
| `VideoWebPlayer/Controllers/UnlockedMediaController.cs` | 59,5 % |
| `VideoWebPlayer/Components/Account/IdentityComponentsEndpointRouteBuilderExtensions.cs` | 60,2 % |
| `VideoWebPlayer/Services/ProgramSettingsService.cs` | 60,3 % |
| `VideoWebPlayer/Components/Pages/Admin/Security.razor` | 60,7 % |
| `VideoWebPlayer/Controllers/ItemsController.cs` | 61,0 % |
| `VideoWebPlayer/Services/MediaSourceClassifier.cs` | 63,8 % |
| `VideoWebPlayer/Data/ApplicationDbContext.cs` | 64,8 % |
| `VideoWebPlayer/Components/Pages/MediaSources/MediaSourceDetails.razor` | 65,0 % |
| `VideoWebPlayer/Services/WhitelistIpMiddleware.cs` | 66,7 % |
| `VideoWebPlayer/Data/UnlockedMediaEntry.cs` | 66,7 % |
| `VideoWebPlayer/Components/Account/Shared/StatusMessage.razor` | 66,7 % |
| `VideoWebPlayer/Controllers/ActorsController.cs` | 67,5 % |
| `VideoWebPlayer/Data/Entities/ContinueWatchingEntry.cs` | 70,0 % |
| `VideoWebPlayer/Controllers/SourcesController.cs` | 70,8 % |
| `VideoWebPlayer/Components/Layout/NavMenu.razor` | 71,4 % |
| `VideoWebPlayer.Client/Models/DtoSource.cs` | 72,7 % |
| `VideoWebPlayer/Controllers/BackupsController.cs` | 73,0 % |
| `VideoWebPlayer/Services/Backups/VideoWebPlayerBackupData.cs` | 73,6 % |
| `VideoWebPlayer/Components/Shared/Media/UnlockButton.razor` | 74,2 % |
| `VideoWebPlayer/Services/Backups/VideoWebPlayerBackupRestoreGuard.cs` | 75,0 % |
| `VideoWebPlayer/Components/Account/Pages/Login.razor` | 75,0 % |
| `VideoWebPlayer/Components/Account/Pages/Register.razor` | 75,4 % |
| `VideoWebPlayer/Services/Updates/UpdateSettingsInitializer.cs` | 76,9 % |
| `VideoWebPlayer/Services/MediaSourceScanner.cs` | 77,1 % |
| `VideoWebPlayer/Services/ActorBackfillWorker.cs` | 77,8 % |
| `VideoWebPlayer/Services/WatchedStatusService.cs` | 77,9 % |
| `VideoWebPlayer/Services/Authentication/IAuthService.cs` | 79,7 % |

## Fehlende Tests

Quelle: `Coverage-Daten` (Cobertura, gemergt aus Unit- und E2E-Lauf). Dateien mit 0 % Zeilenabdeckung; generierte Dateien (`Migrations/*Designer.cs`), Konfigurationsdateien und Einstiegspunkte sind nicht als Lücke gewertet.

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
- `VideoWebPlayer/Data/MediaSourceIcon.cs` — 0 % Abdeckung
- `VideoWebPlayer/Hubs/MediaUpdateHub.cs` — 0 % Abdeckung
- `VideoWebPlayer/Services/DemoData/IDemoDataSetService.cs` — 0 % Abdeckung
- `VideoWebPlayer/Services/SftpStreamWrapper.cs` — 0 % Abdeckung
- `VideoWebPlayer/Services/UdpDiscoveryListener.cs` — 0 % Abdeckung
- `VideoWebPlayer/Utils/LocalNetworkHelper.cs` — 0 % Abdeckung
