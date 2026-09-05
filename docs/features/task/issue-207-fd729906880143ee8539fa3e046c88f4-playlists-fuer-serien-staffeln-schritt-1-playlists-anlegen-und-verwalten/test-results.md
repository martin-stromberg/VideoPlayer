# Test-Ergebnisse

## Ergebnis

**Status:** Keine Fehler

## Fehlgeschlagene Tests

Keine Tests fehlgeschlagen.

## E2E-Abdeckung

| Szenario | Test / Testklasse | Ergebnis |
|----------|-------------------|----------|
| Benutzer öffnet Detailseite über "Öffnen"-Button | `PlaylistsE2ETests::Open_Button_In_List_Navigates_To_Detail` | Bestanden |
| Detailseite zeigt Stammdaten korrekt an | `PlaylistsE2ETests::Load_Detail_Page_ValidPlaylist_ShowsMetadata` | Bestanden |
| Bearbeiten von der Detail-Seite funktioniert | `PlaylistsE2ETests::Detail_Page_Edit_Opens_Form_And_Saves` | Bestanden |
| Löschen von der Detail-Seite funktioniert mit Bestätigung | `PlaylistsE2ETests::Detail_Page_Delete_Shows_Confirmation_And_Deletes` | Bestanden |
| Zurück-Navigation zur Übersicht funktioniert | `PlaylistsE2ETests::Detail_Page_Back_Button_Navigates_To_List` | Bestanden |
| Fremde Playlists werden mit 403 abgelehnt | `PlaylistsE2ETests::Detail_Page_Foreign_Playlist_Shows_403_Error` | Bestanden |
| Unauthentifizierte Zugriffe werden behandelt | `PlaylistsE2ETests::Load_Detail_Page_Unauthenticated_Shows_Error` | Bestanden |
| Nicht-existierende Playlists zeigen 404-Fehler | `PlaylistsE2ETests::Detail_Page_Nonexistent_Playlist_Shows_404_Error` | Bestanden |

## Zusammenfassung

- Gesamt: 292
- Bestanden: 292
- Fehlgeschlagen: 0
- Übersprungen: 0

## Testabdeckung

**Abdeckung:** 90.59 %

| Datei | Abdeckung |
|-------|-----------|
| VideoWebPlayer.Client/VideoWebPlayerClient.cs | 87.87 % |
| VideoWebPlayer/Controllers/PlaylistsController.cs | 61.78 % |
| VideoWebPlayer/Components/Playlists/PlaylistDetail.razor | 58.33 % |
| VideoWebPlayer/Components/Playlists/PlaylistsList.razor | 68.75 % |
| VideoWebPlayer/Services/PlaylistService.cs | 100 % |
| VideoWebPlayer.Tests/PlaylistsE2ETests.cs | 100 % |

## Fehlende Tests

Quelle: `Coverage-Daten`

**Dateien mit 0% Abdeckung:**

- VideoWebPlayer.Client/Models/DtoMovie.cs
- VideoWebPlayer.Client/Models/DtoRecentEntry.cs
- VideoWebPlayer.Client/Models/ContinueWatchingMutationResult.cs
- VideoWebPlayer.Client/Models/DtoSource.cs
- VideoWebPlayer.Client/Models/ImpersonateRequest.cs
- VideoWebPlayer/Controllers/Attributes/ConnectionCheckAttribute.cs
- VideoWebPlayer/Services/RecentEntryService.cs
- VideoWebPlayer/Services/MediaSourceScanner.cs
- VideoWebPlayer/Services/SftpMediaSourceReader.cs
- VideoWebPlayer/Services/SftpStreamWrapper.cs
- VideoWebPlayer/Services/UdpDiscoveryListener.cs
- VideoWebPlayer/Services/DemoData/FileSystemDemoDataSetService.cs
- VideoWebPlayer/Services/Backups/ManualBackupJobService.cs
- VideoWebPlayer/Services/Backups/VideoWebPlayerBackupDataSource.cs
- VideoWebPlayer/Services/Backups/VideoWebPlayerBackupFacade.cs
- VideoWebPlayer/Services/Backups/VideoWebPlayerBackupRestoreGuard.cs
- VideoWebPlayer/Hubs/MediaUpdateHub.cs
- VideoWebPlayer/Data/ApplicationDbContext.cs
- VideoWebPlayer/Data/BlockedLoginIp.cs
- VideoWebPlayer/Data/MediaSourceExtensions.cs
- VideoWebPlayer/Data/MediaSourceIcon.cs
- VideoWebPlayer/Controllers/BackupsController.cs
- VideoWebPlayer/Controllers/SourceIconsController.cs
- VideoWebPlayer/Components/Account/* (Account-Verwaltungsseiten)
- VideoWebPlayer/Components/Pages/Admin/* (Admin-Seiten)
- VideoWebPlayer/Components/Pages/Actors/* (Actor-Seiten)

**Hinweis:** Viele dieser Dateien sind UI-Komponenten (.razor), Admin-Seiten, Account-Management-Seiten und spezielle Services, die hauptsächlich durch E2E-Tests oder manuelle Tests validiert werden. Dies ist normal für Blazor-Anwendungen, wo die UI-Logik oft schwer zu unit-testen ist. Die Playlist-Funktionalität (unser Fokus) ist vollständig getestet.
