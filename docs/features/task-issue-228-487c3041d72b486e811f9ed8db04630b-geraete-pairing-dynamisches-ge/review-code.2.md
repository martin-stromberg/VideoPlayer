# Code-Review

Zweite Iteration. Basisbranch: `staging` (Merge-Base `bfa8efd`). Die Implementierung liegt überwiegend uncommitted im Working Tree; geprüft wurden Merge-Base-Diff plus uncommitted geänderte/neue Quelldateien (`git status`). `dotnet build VideoPlayer.sln`: 0 C#-Fehler, 0 C#-Warnungen — nur Datei-Sperrfehler beim Kopieren (MSB3026/MSB3027) durch einen noch laufenden `testhost`-Prozess; die Kompilierung selbst ist sauber.

Alle 8 Befunde aus `review-code.1.md` sind behoben: `ExchangeAsync` aufgeteilt (`TryImportClientPublicKey`, `EncryptToken`), `HashHelper` unter `Services/Security`, Ablaufbedingung atomar in `ExecuteUpdateAsync`, `CreatedPairingCode` liefert `ExpiresAtUtc` (Devices.razor liest keine Config mehr), `StateHasChanged()` entfernt, `PairingTestDb` disposed den `ServiceProvider`, Catch-Blöcke auf `IOException`/`UnauthorizedAccessException` eingegrenzt, `CreateService`-Overloads zusammengeführt, `ApiTokenConfigurationTests` löst Scoped-Services über `IServiceScope` auf.

Zusatzprüfung `RaiseUiActionRequested`: Keine solchen Aktionen im Diff bzw. in der Codebasis vorhanden — nicht zutreffend.

## Ergebnis

**Status:** Befunde vorhanden

## Befunde

### PairingServiceTests_CodeCreation.cs / PairingServiceTests_Exchange.cs

- **Fehlende Kapselung / falsche Verantwortlichkeit** — Gemeinsam genutzte Test-Hilfsmethoden liegen als `internal static` in fachfremden Testklassen: `PairingServiceTests_CodeCreation.CreateService` (Zeile 38) wird von `PairingServiceTests_Exchange` und `PairingServiceTests_ActiveCodes` mitbenutzt; `PairingServiceTests_Exchange.DecryptToken` (Zeile 241) wird von `DevicePairingE2ETests` (Zeile 302) und `PairingExchangeContractTests_Flow` (Zeile 103) mitbenutzt. Wer die Methode sucht, findet sie nicht im `Helpers`-Ordner; die Testklassen bekommen dadurch eine Querverantwortung als Hilfsbibliothek.

  Empfehlung: Beide Helfer nach `VideoWebPlayer.Tests/Helpers/` auslagern — `CreateService` z. B. als Methode auf `PairingTestDb` (`fixture.CreatePairingService()`) oder als eigene `PairingServiceFactory`, `DecryptToken` als `PairingCryptoHelper.DecryptToken(...)`. Daneben in `PairingServiceTests_Exchange.cs` Zeilen 136–137 `GetService(typeof(ApplicationDbContext))` + Cast durch `GetRequiredService<ApplicationDbContext>()` ersetzen (konsistent zum Rest der Tests).

### PairingExchangeContractTestBase.cs / DevicePairingE2ETests.cs

- **Doppelter Code** — Der `WebApplicationFactory<Program>`-Setup-Block ist nahezu identisch dupliziert: Temp-DB-Pfad mit best-effort `File.Delete` (`PairingExchangeContractTestBase.cs` Zeilen 26–27 vs. `DevicePairingE2ETests.cs` Zeilen 41–42), JWT-Key-Generierung plus identische `UseSetting`-Aufrufe für ConnectionString/`Jwt:*` (Zeilen 29–40 vs. 44–56) und die `HttpsRedirectionOptions`-Überschreibung (Zeilen 41–44 vs. 57–60). Insgesamt ~20 Zeilen paralleler Setup-Code, die bei Änderungen (z. B. neuer Pflicht-Setting) auseinanderdriften.

  Empfehlung: Gemeinsame Factory-Methode in `Helpers/` auslagern (z. B. `PairingWebApplicationFactory.Create(string dbPath, Action<IWebHostBuilder>? configure = null)`), in der die gemeinsamen Settings gesetzt werden; die E2E-Klasse ergänzt nur ihre spezifischen Aufrufe (`UseKestrel`, `UseUrls`, `UseStaticWebAssets`, `AutoUpdate:HostedServicesEnabled`).

### PairingService.cs (PairingService)

- **Fehlerbehandlung** — In `TryImportClientPublicKey` (Zeilen 222–242) liegt die Kurvenprüfung `key.ExportParameters(false)` (Zeile 235) außerhalb des `try/catch`. Ein SubjectPublicKeyInfo-Blob, der importiert werden kann, dessen Parameterexport aber eine `CryptographicException` wirft (z. B. exotische/explizit parametrisierte EC-Kurven), erzeugt eine unbehandelte Exception und damit HTTP 500 statt der intendierten 400-Antwort.

  Empfehlung: Die ExportParameters-/Oid-Prüfung in den `try`-Block aufnehmen (bzw. den bestehenden `catch`-Filter darauf ausdehnen), damit jeder nicht-P256- oder nicht exportierbare Schlüssel kontrolliert `null` ergibt.

- **Inkonsistente Validierung** — Die Längenprüfung des Gerätenamens läuft in `ExchangeAsync` (Zeile 181) auf dem ungetrimmten String (`request.DeviceName.Length > MaxDeviceNameLength`), in `DeviceTokenService.RenameAsync` (Zeilen 117–118) dagegen nach dem `Trim()`. Derselbe fachliche Wert wird an zwei Stellen mit unterschiedlicher Semantik geprüft: Ein Name wie `"  " + 199 Zeichen` wird beim Exchange mit 400 abgelehnt, beim Umbenennen aber akzeptiert.

  Empfehlung: Validierung vereinheitlichen — im Exchange den Namen vor der Längenprüfung trimmen (oder die Prüfung zentral in `DeviceTokenService`/`IssueAsync` kapseln), damit Exchange und Rename denselben Wertebereich akzeptieren.

### PairingExchangeContractTestBase.cs / DevicePairingE2ETests.cs (Test-Infrastruktur)

- **Testqualität / latente Cleanup-Lücke** — Die Helfer `UnblockIp` (`PairingExchangeContractTestBase.cs` Zeile 92) und `UnblockLocalIp` (`DevicePairingE2ETests.cs` Zeile 263) rufen `ILoginIpBlockService.Unblock` auf, das nur persistierte (tatsächlich gesperrte) Einträge entfernt (`LoginIpBlockService.cs` Zeilen 176–187: Rückgabe `false`, wenn kein DB-Eintrag existiert). Im `_cache` liegende Fehlerzähler unterhalb der Block-Schwelle bleiben bestehen — der Helfer suggeriert also einen vollständigen Reset, leistet ihn aber nicht. Aktuell hinterlässt `PairingExchangeContractTests_Validation.Exchange_Returns400OnMissingOrEmptyCode` einen Fehlerzähler auf `198.51.100.23` ohne Cleanup; sobald ein weiterer Test dieselbe IP nutzt und Fehler addiert, wird die Sperre früher als erwartet ausgelöst (reihenfolgeabhängige Flakiness).

  Empfehlung: Entweder `Unblock` so erweitern, dass der Cache-Eintrag der IP immer entfernt wird (unabhängig vom persistierten Sperrstatus), oder die Tests so umbauen, dass jede verwendete IP pro Test frisch ist (z. B. GUID-basierte IPs aus Test-Reserve-Bereichen) bzw. der Fehlerzähler explizit zurückgesetzt werden kann.

## Geprüfte Dateien

Liste aller geprüften Quelldateien:
- `VideoWebPlayer/Data/PairedDevice.cs`
- `VideoWebPlayer/Data/PairingCode.cs`
- `VideoWebPlayer/Data/Configurations/PairedDeviceConfiguration.cs`
- `VideoWebPlayer/Data/Configurations/PairingCodeConfiguration.cs`
- `VideoWebPlayer/Data/ApplicationDbContext.cs` (Diff: neue DbSets)
- `VideoWebPlayer/Services/DeviceTokenService.cs`
- `VideoWebPlayer/Services/PairingService.cs`
- `VideoWebPlayer/Services/Security/HashHelper.cs`
- `VideoWebPlayer/Controllers/PairingController.cs`
- `VideoWebPlayer/Controllers/Attributes/ApiTokenCheckAttribute.cs`
- `VideoWebPlayer/Extensions/ServiceCollectionExtensions.cs` (Diff: DI-Registrierungen)
- `VideoWebPlayer/Components/Pages/Admin/Devices.razor`
- `VideoWebPlayer/Components/Pages/Admin/AdminIndex.razor` (Diff: neuer Admin-Tile)
- `VideoWebPlayer.Client/Models/PairingExchangeRequest.cs`
- `VideoWebPlayer.Client/Models/PairingExchangeResponse.cs`
- `VideoWebPlayer/Migrations/20260923074815_AddDevicePairing.cs` (generiert, konsistent zur Konfiguration)
- `VideoWebPlayer/Migrations/20260923074815_AddDevicePairing.Designer.cs` / `ApplicationDbContextModelSnapshot.cs` (generiert, stichprobenartig)
- `VideoWebPlayer.Tests/DeviceTokenServiceTests.cs`
- `VideoWebPlayer.Tests/PairingServiceTests_CodeCreation.cs`
- `VideoWebPlayer.Tests/PairingServiceTests_Exchange.cs`
- `VideoWebPlayer.Tests/PairingServiceTests_ActiveCodes.cs`
- `VideoWebPlayer.Tests/PairingExchangeContractTests_Flow.cs`
- `VideoWebPlayer.Tests/PairingExchangeContractTests_JsonContract.cs`
- `VideoWebPlayer.Tests/PairingExchangeContractTests_Validation.cs`
- `VideoWebPlayer.Tests/DevicePairingE2ETests.cs`
- `VideoWebPlayer.Tests/Helpers/PairingTestDb.cs`
- `VideoWebPlayer.Tests/Helpers/PairingExchangeContractTestBase.cs`
- `VideoWebPlayer.Tests/ApiTokenConfigurationTests.cs` (Diff)
- `VideoWebPlayer.Tests/ApiDocumentationContractTests.cs` (Diff: neuer Endpoint-Eintrag)

Dokumentationsdateien (`docs/API.md`, `docs/GUIDE_Installation.md`, `docs/SECRETS_MANAGEMENT.md`, `docs/help/einrichtung.md`, Feature-Docs) wurden nicht als Quellcode reviewt.
