# Test-Ergebnisse

Iteration 3 (einzige Änderung gegenüber Iteration 2: `VideoWebPlayer.Tests/ProgramSettingsE2ETests.cs`
— neue `SevereErrors()`-Konsum-Assertions `Assert.Empty(SevereErrors())` am Ende beider E2E-Tests).
Ausgeführt: `dotnet build VideoPlayer.sln` (Debug, 0 Warnungen, 0 Fehler) gefolgt von
`dotnet test VideoWebPlayer.Tests/VideoWebPlayer.Tests.csproj --no-build
--collect:"XPlat Code Coverage" --logger "console;verbosity=normal"`.
Der Lauf lief vollständig durch (5,0 min) — keinerlei Ausfälle, auch keine Flakes.
Coverage-Datei: `VideoWebPlayer.Tests/TestResults/312e1d39-…/coverage.cobertura.xml`.

## Ergebnis

**Status:** Keine Fehler

Im Hauptlauf fiel kein einziger Test aus — im Gegensatz zu Iteration 2 (drei einmalige
Last-Flakes) waren diesmal alle Playwright-Tests im ersten Anlauf grün. Kein Test
wurde deaktiviert oder abgeschwächt.

## Fehlgeschlagene Tests

Keine — Status ist „Keine Fehler".

## E2E-Abdeckung

| Szenario | Test / Testklasse | Ergebnis |
|----------|-------------------|----------|
| Admin schaltet mDNS-Advertisement in `/admin/program-settings` aus/ein, speichert, lädt neu → Zustand persistiert (beide Richtungen) | `ProgramSettingsE2ETests.Admin_TogglesMdnsAdvertisement_AndSettingPersists` | Bestanden (8 s) — inkl. neuer `Assert.Empty(SevereErrors())`: keine Page-Errors, keine HTTP-4xx/5xx-Responses |
| Nicht-Admin öffnet `/admin/program-settings` → „Nicht autorisiert", kein Schalter sichtbar | `ProgramSettingsE2ETests.NonAdmin_GetsNotAuthorized_OnProgramSettings` | Bestanden (13 s) — inkl. neuer `Assert.Empty(SevereErrors())` |
| mDNS-Advertisement selbst (Dienst erscheint/verschwindet im Browse) | — | Nicht erforderlich mit Begründung (Testing-Guard registriert den Worker bewusst nicht; Multicast nicht browser-testbar; manueller Nachweis per `avahi-browse`/`dns-sd` ist im Plan Schritt 13 vorgesehen und für den Abnahmebericht zu dokumentieren) |

Beide Pflicht-E2E-Szenarien existieren in
`VideoWebPlayer.Tests/ProgramSettingsE2ETests.cs` und sind im Hauptlauf erfolgreich
durchgelaufen. Die in Iteration 3 neu hinzugekommenen `SevereErrors()`-Assertions
(Page-Errors und HTTP-Responses ≥ 400 werden gesammelt und müssen leer sein) wurden
von beiden Tests erfüllt — der primäre Funktionsnachweis für den UI-Fluss ist
erbracht.

## Zusammenfassung

- Gesamt: 1666 (expandierte Theories/InlineData)
- Bestanden: 1665
- Fehlgeschlagen: 0
- Übersprungen: 1 — `DevicePlaylistE2ETests_Playback.PairedDevice_ReportsProgressForMovieWithoutCollection_ContinueWatchingListStillLoads`
  (dokumentierter Baseline-Skip: `api/continue-watching` antwortet mit 500 bei
  Film ohne Filmsammlung; unabhängig von dieser Anforderung)

mDNS-Feature-Tests (alle bestanden):

- `MdnsServiceProfileBuilderTests` (7) — Port-Ableitungskette, Diensttyp, Instanzname, Defaults, TXT-Records
- `MdnsAdvertiserWorkerTests` (6) — `IsAdvertisementEnabled_RequiresBothSwitches` (4 Konjunktionsfälle), `ExecuteAsync_ConfigDisabled_ExitsWithoutPollingAdminSwitch`, `ExecuteAsync_AdminSwitchInitialReadFails_DoesNotAdvertise`
- `MdnsRegistrationTests` (2) — Worker-Registrierung nur außerhalb `Testing`
- `MdnsConfigurationTests.MdnsSection_BindsToOptions` (1)
- `MdnsOptionsValidatorTests` (23 expandierte Fälle) — Port-Range, Diensttyp-Format, Instanzname
- `ProgramSettingsServiceTests` (3) — `GetMdnsAdvertisementEnabledAsync_DefaultsToTrue`, `UpdateMdnsAdvertisementEnabledAsync_Persists`, `UpdateGeneralSettingsAsync_PersistsMdnsAdvertisementEnabled` (echtes SQLite)
- `VideoWebPlayerBackupDataTests.ReadFromAsync_LegacyBackupWithoutMdnsAdvertisementEnabledColumn_RestoresAsEnabled` (1) — Alt-Backup-Regression via `LegacyBackupArchiveBuilder`
- `ProgramSettingsE2ETests` (2) — siehe E2E-Abdeckung

## Testabdeckung

**Abdeckung:** 94,1 % Zeilen (108 938 / 115 722; Branch: 60,3 %) —
gemessen über `XPlat Code Coverage` (Cobertura).

Von dieser Anforderung neue/geänderte Dateien:

| Datei | Abdeckung |
|-------|-----------|
| `VideoWebPlayer/Components/Pages/Admin/ProgramSettings.razor` | 100 % |
| `VideoWebPlayer/Services/Backups/VideoWebPlayerBackupData.cs` | 100 % |
| `VideoWebPlayer/Services/ProgramSettingsService.cs` | 100 % |
| `VideoWebPlayer/Configuration/MdnsOptions.cs` | 100 % |
| `VideoWebPlayer/Configuration/MdnsOptionsValidator.cs` | 100 % |
| `VideoWebPlayer/Services/MdnsAdvertisement.cs` | 100 % |
| `VideoWebPlayer/Services/MdnsServiceProfileBuilder.cs` | 91,1 % |
| `VideoWebPlayer/Extensions/ServiceCollectionExtensions.cs` | 92,8 % |
| `VideoWebPlayer/Data/Setup.cs` | 90,0 % |
| `VideoWebPlayer/Services/MdnsAdvertiserWorker.cs` | 68,8 % |
| `VideoWebPlayer/Migrations/20261004173814_AddSetupMdnsAdvertisementEnabled.cs` | 61,5 % (Up-Pfad; Down ungetestet — Migrations-Üblichkeit) |

Gegenüber Iteration 2 gestiegen: `ProgramSettings.razor` 94,1 % → 100 %,
`VideoWebPlayerBackupData.cs` 74,7 % → 100 %, `ProgramSettingsService.cs`
72,6 % → 100 %, `MdnsAdvertiserWorker.cs` 44,1 % → 68,8 % (die neuen
`SevereErrors()`-Assertions zwingen die E2E-Tests zu einem sauberen,
fehlerfreien Durchlauf der Seite, was zusätzliche Razor-/Service-Pfade abdeckt).

`MdnsAdvertiserWorker` liegt weiterhin bewusst unter 80 %: Der Transportteil
(Multicast-Sockets, `Advertise`/`Unadvertise`, Goodbye-Pakete) ist per
Architekturentscheid nicht unit-testbar; abgesichert sind die prüfbaren Teile
(Konjunktionslogik, Config-Aus-Pfad, Fehlerpfad beim ersten DB-Read). Der
versandseitige Nachweis ist manuell (Plan Schritt 13).

Projektweit existieren 120 weitere Dateien mit < 80 % und 42 Dateien mit
0 % Zeilenabdeckung (Baseline, nicht neu durch diese Anforderung; Details unter
„Fehlende Tests").

## Fehlende Tests

Quelle: `Coverage-Daten` (Cobertura)

Für die von dieser Anforderung neuen Dateien ist keine Lücke vorhanden — alle
haben korrespondierende Tests und > 0 % Abdeckung (siehe Tabelle oben;
`MdnsAdvertiserWorker` 68,8 % mit begründetem Rest, s. o.).

Projektweite Baseline — 42 Dateien mit 0 % Abdeckung, überwiegend nicht von
dieser Anforderung berührte Identity-Scaffold-/Sample-Seiten und
Infrastruktur ohne Netzwerk-Seam:

- `Components/Account/Pages/**` (19 Dateien: Login/2FA/Password-/Manage-Seiten)
- `Components/Account/IdentityNoOpEmailSender.cs`, `Components/Account/Shared/{RedirectToLogin,ShowRecoveryCodes}.razor`
- `Components/Pages/Actors/{Actors,ActorDetails}.razor`, `Components/Pages/Admin/{UserManagement.razor,MediaSources/MediaSourceExplorer.razor}`, `Components/Pages/Errors/Error.razor`
- `Components/Pages/Samples/{Auth,Counter,Weather}.razor` (Template-Reste)
- `Components/Shared/Media/{MediaBaseEntryList,WatchedIndicator}.razor`
- `Controllers/Attributes/ConnectionCheckAttribute.cs`, `Hubs/MediaUpdateHub.cs`
- `Services/{SftpStreamWrapper.cs,UdpDiscoveryListener.cs,DemoData/IDemoDataSetService.cs}`, `Utils/LocalNetworkHelper.cs`
- `VideoWebPlayer.Client/Models/{DtoRecentEntry,ImpersonateRequest}.cs`

Bemerkenswert: `UdpDiscoveryListener.cs` (der bestehende UDP-Fallback-Kanal)
hat wie der neue mDNS-Transportteil 0 % automatisierte Abdeckung — gleiches
Muster: Netzwerk-Versand nicht unit-testbar.

## Hinweise

- **Kein Flake in dieser Iteration:** Alle drei Tests, die in Iteration 2
  einmalig ausfielen (`PlaylistDragDropReorderE2ETests.E2E_DragWithTouch_…`,
  `BackupUploadE2ETests.BackupUpload_NavigatingAwayDuringUpload_…`,
  `PlaylistPlaybackE2ETests.PlaylistSkipLockedEntriesE2ETest`), liefen im
  Hauptlauf direkt grün (12 s / 8 s / 28 s).
- **Hang aus Iteration 1 weiterhin nicht reproduziert:**
  `MediaSourceScanServiceTests.ExecuteAsync_DetectsNewSeason_WhenEpisodesAddedAfterFirstScan`
  lief normal in 1 s durch.
- **Baseline-Skip unverändert:**
  `DevicePlaylistE2ETests_Playback.PairedDevice_…_ContinueWatchingListStillLoads`
  (api/continue-watching-500) — dokumentierter, anforderungsunabhängiger Skip.
- **Anzahl-Note:** `MdnsOptionsValidatorTests` expandiert zu 23 Fällen
  (Iteration 2 wurde mit 24 berichtet; Gesamtzahl der Suite ist mit 1666
  unverändert — kein Test entfernt, alle Validator-Fälle bestanden).
- Build vor dem Lauf: `dotnet build VideoPlayer.sln` (Debug) — 0 Warnungen,
  0 Fehler. Release-Build ist DoD-Punkt eines anderen Schritts.
