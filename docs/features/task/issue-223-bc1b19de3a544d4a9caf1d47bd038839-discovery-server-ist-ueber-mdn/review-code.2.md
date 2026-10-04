# Code-Review

## Ergebnis

**Status:** Befunde vorhanden

Iteration 2: Alle fünf Befunde aus `review-code.1.md` sind korrekt behoben und haben keine
neuen Probleme eingeführt. Verbleibt ein kleiner Toter-Code-Befund in den neuen E2E-Tests.
`dotnet build VideoPlayer.sln` (Debug) fehlerfrei, 0 Warnungen; die 69 betroffenen
Unit-Tests (`Mdns*`, `ProgramSettingsService`, `VideoWebPlayerBackupDataTests`) sind grün.

## Befunde

### ProgramSettingsE2ETests.cs (ProgramSettingsE2ETests)

- **Toter Code** — `_consoleMessages` (Z. 25) wird über drei Event-Handler befüllt
  (`_page.Console`, `_page.PageError`, `_page.Response`, Z. 78–83), aber an keiner Stelle
  gelesen. In `UpdatesPageE2ETests` wird dieselbe Sammlung über `SevereErrors()` (Z. 463–467)
  in Assertions ausgewertet — hier fehlt der Konsument, die ~10 Zeilen Handler-Plumbing
  laufen ins Leere.

  Empfehlung: Entweder die Sammlung samt Handlern entfernen oder wie in `UpdatesPageE2ETests`
  konsumieren (z. B. schwere Fehler nach dem Speichern asserten oder im Fehlerfall an die
  Assert-Meldung anhängen).

## Verifikation der Iteration-1-Befunde (behoben)

- **Early-Return bei `Mdns:Enabled=false`:** `MdnsAdvertiserWorker.cs` Z. 82–87 — nach
  `WaitForApplicationStartedAsync` und vor der Schleife, mit Info-Log. Test
  `ExecuteAsync_ConfigDisabled_ExitsWithoutPollingAdminSwitch` verifiziert `Times.Never`
  für `CreateScope` und `Features`. Keine neue Race: Der Lesezugriff auf den
  Admin-Schalter bleibt sequentiell in `ExecuteAsync`.
- **Fail-closed `_adminSwitchEnabled`:** Z. 32 mit `false` initialisiert + Kommentar.
  Test `ExecuteAsync_AdminSwitchInitialReadFails_DoesNotAdvertise` provoziert den
  Fehlschlag der ersten DB-Lesung und verifiziert deterministisch, dass kein
  Advertise-Versuch (`IServer.Features`) erfolgt — `TryAdvertise` kann ohne erfolgreiche
  Lesung gar nicht erreicht werden. Kein falscher Goodbye: `Unadvertise` nur bei
  `_advertised == true` (Z. 112–113 und 133–134).
- **Atomare Speicherung:** `UpdateGeneralSettingsAsync` um `mdnsAdvertisementEnabled`
  erweitert (Z. 137–156), ein `SaveChangesAsync` für das gesamte Formular;
  `ProgramSettings.razor` Z. 136–141 ruft nur noch diese Methode. SQLite-Test
  `UpdateGeneralSettingsAsync_PersistsMdnsAdvertisementEnabled` deckt beide Richtungen ab.
- **`TryGetPort`-Umbenennung:** `MdnsServiceProfileBuilder.cs` Z. 71 — name matches the
  http/https-Verhalten.
- **Label-Regex:** `MdnsOptionsValidator.cs` Z. 15 — folgendes Muster lehnt
  führende/folgende Bindestriche ab; Tests Z. 38–40 decken `_-x._tcp`, `_x-._tcp`,
  `_-abc-._udp` ab.

  ```
  ^_[a-z0-9](?:[a-z0-9-]{0,13}[a-z0-9])?\._(?:tcp|udp)(?:\.local\.?)?
  ```

## Hinweise (keine Befunde)

- **`UpdateMdnsAdvertisementEnabledAsync` (ProgramSettingsService.cs Z. 173) hat keinen
  Produktiv-Aufrufer mehr** — die Seite speichert jetzt atomar über
  `UpdateGeneralSettingsAsync`. Die Methode wurde im Plan (Schritt 7) explizit als
  Methodenpaar gefordert und ist per SQLite-Test abgesichert; die Abweichung vom
  ursprünglichen Plan ist in `review.md` dokumentiert. Bewusst als Hinweis, nicht als
  Befund, eingeordnet.
- **Re-Evaluation:** Bei dauerhaftem DB-Lesefehler bleibt der zuletzt gelesene Zustand
  bestehen (Warnlog, kein Flappen) — dokumentiert und für einen Advertiser die
  sinnvollere Wahl als Goodbye bei transienten Fehlern.
- **Reihenfolge `WaitForApplicationStartedAsync` vor dem `Enabled`-Check** bedeutet, dass
  der Worker bei `Mdns:Enabled=false` erst den App-Start abwartet, bevor er sich beendet —
  harmlos, keine Ressourcen gebunden.
- **Zusätzliche Konsistenz:** `Mdns:*`-Schlüssel in die `JsonKeys`-Merge-Liste des
  Updaters in `appsettings.json` aufgenommen (verhindert Konfigurationsverlust beim
  Update, konsistent mit Issue #244); Worker-Registrierung korrekt hinter
  `!env.IsEnvironment("Testing")`; `InternalsVisibleTo` für `VideoWebPlayer.Tests`
  vorhanden; Block-Namespace-Stil der neuen `Configuration`-Dateien entspricht
  `PlaylistSettings.cs`; E2E-Browser-Fallback (`_skipBrowser`/`Assert.Fail`) entspricht dem
  Muster der übrigen E2E-Tests.
- `ProgramSettingsE2ETests` wurde nicht ausgeführt (Playwright-Browser nötig); Code ist
  konsistent mit den vorhandenen E2E-Basismustern.

## Geprüfte Dateien

- `VideoWebPlayer/Configuration/MdnsOptions.cs`
- `VideoWebPlayer/Configuration/MdnsOptionsValidator.cs`
- `VideoWebPlayer/Services/MdnsAdvertisement.cs`
- `VideoWebPlayer/Services/MdnsAdvertiserWorker.cs`
- `VideoWebPlayer/Services/MdnsServiceProfileBuilder.cs`
- `VideoWebPlayer/Services/ProgramSettingsService.cs`
- `VideoWebPlayer/Services/Backups/VideoWebPlayerBackupData.cs`
- `VideoWebPlayer/Data/Setup.cs`
- `VideoWebPlayer/Extensions/ServiceCollectionExtensions.cs`
- `VideoWebPlayer/Migrations/20261004173814_AddSetupMdnsAdvertisementEnabled.cs`
- `VideoWebPlayer/Migrations/20261004173814_AddSetupMdnsAdvertisementEnabled.Designer.cs`
- `VideoWebPlayer/Migrations/ApplicationDbContextModelSnapshot.cs`
- `VideoWebPlayer/Components/Pages/Admin/ProgramSettings.razor`
- `VideoWebPlayer/appsettings.json`
- `VideoWebPlayer/VideoWebPlayer.csproj`
- `VideoWebPlayer.Tests/MdnsAdvertiserWorkerTests.cs`
- `VideoWebPlayer.Tests/MdnsConfigurationTests.cs`
- `VideoWebPlayer.Tests/MdnsOptionsValidatorTests.cs`
- `VideoWebPlayer.Tests/MdnsRegistrationTests.cs`
- `VideoWebPlayer.Tests/MdnsServiceProfileBuilderTests.cs`
- `VideoWebPlayer.Tests/ProgramSettingsServiceTests.cs`
- `VideoWebPlayer.Tests/ProgramSettingsE2ETests.cs`
- `VideoWebPlayer.Tests/Services/Backups/VideoWebPlayerBackupDataTests.cs`
