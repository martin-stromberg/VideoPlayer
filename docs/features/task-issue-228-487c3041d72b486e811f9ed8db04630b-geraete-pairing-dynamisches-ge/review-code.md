# Code-Review

Dritte Iteration. Basisbranch: `staging` (Merge-Base `bfa8efd`). Die Implementierung liegt überwiegend uncommitted im Working Tree; geprüft wurden Merge-Base-Diff plus uncommitted geänderte/neue Quelldateien (`git status`). `dotnet build VideoPlayer.sln`: 0 Fehler; die vorhandenen Warnungen betreffen ausschließlich Dateien außerhalb des Diffs (keine neuen Warnungen durch diesen Branch).

Alle 5 Befunde aus `review-code.2.md` sind behoben: `PairingCryptoHelper` und `PairingWebApplicationFactory` liegen unter `VideoWebPlayer.Tests/Helpers/` und werden von allen Testklassen genutzt (`fixture.CreatePairingService(...)` ersetzt die früheren `CreateService`-Helfer, `PairingCryptoHelper.DecryptToken` ersetzt die duplizierte Entschlüsselung); die Kurvenprüfung `ExportParameters` liegt jetzt im `try/catch` (`PairingService.cs` Zeilen 232–249); die Gerätename-Längenprüfung trimmt vor dem Vergleich (Zeile 187); `Unblock` entfernt den Cache-Eintrag immer (`LoginIpBlockService.cs` Zeile 181); die Widerruf-Bestätigung und `ToLocalTime()`-Anzeige sind in `Devices.razor` umgesetzt; der Fallback-Gerätename enthält den Ausstellungszeitpunkt (`DeviceTokenService.cs` Zeile 72). Die Nachbesserungen haben keine neuen kritischen Probleme eingeführt.

Zusatzprüfung `RaiseUiActionRequested`: Keine solchen Aktionen im Diff bzw. in der Codebasis vorhanden — nicht zutreffend.

## Ergebnis

**Status:** Befunde vorhanden

## Befunde

### LoginIpBlockService.cs (LoginIpBlockService)

- **Inkonsistente Behandlung desselben Konzepts** — `Unblock` (Zeilen 176–189) arbeitet auf dem rohen String-Schlüssel, während alle anderen Methoden den Cache-/DB-Schlüssel über `Normalize(IPAddress)` ableiten (`IsBlocked` Zeile 90, `RegisterFailure` Zeile 120, `RegisterSuccess` Zeile 146). Der in dieser Iteration vorgezogene `_cache.TryRemove(ip, out _)` (Zeile 181) und der DB-Lookup `db.BlockedLoginIps.Find(ip)` (Zeile 184) verfehlen den Eintrag, wenn ein Aufrufer eine nicht-normalisierte Form übergibt (z. B. IPv4-mapped-IPv6 `"::ffff:203.0.113.77"`, während der Cache unter `"203.0.113.77"` führt). Alle aktuellen Aufrufer (`Security.razor`, Test-Helfer) übergeben bereits normalisierte Strings — der Defekt ist latent, aber die Methode bietet keine Normalisierung an, die der Rest der Klasse selbstverständlich anwendet.

  Empfehlung: In `Unblock` den Eingabestring normalisieren, bevor Cache und DB befragt werden — z. B. `if (!IPAddress.TryParse(ip, out var parsed)) return false; var key = Normalize(parsed);` und anschließend `key` für `TryRemove`/`Find` verwenden.

### PairingService.cs (PairingService)

- **Fehlende Validierung von Vorbedingungen** — `CreatePairingCodeAsync` (Zeilen 152–174) übernimmt `Pairing:CodeLength` und `Pairing:CodeTtlMinutes` ungeprüft aus der Konfiguration. `codeLength <= 0` erzeugt entweder eine `ArgumentOutOfRangeException` in `RandomNumberGenerator.GetString` (negativ → unbehandelte Exception, HTTP 500 beim Admin-Klick) oder einen leeren, nie konsumierbaren Code (0 → `Code = ""`); `ttlMinutes <= 0` erzeugt einen sofort abgelaufenen Code. Eine fehlerhafte Konfiguration führt so zu einem still kaputten Feature statt zu einem klaren Fallback auf die Defaults.

  Empfehlung: Die gelesenen Werte auf einen sinnvollen Bereich klemmen bzw. bei ungültigen Werten auf die Defaults zurückfallen, z. B. `var codeLength = Math.Max(4, _configuration.GetValue("Pairing:CodeLength", DefaultCodeLength))` und analog `Math.Max(1, ...)` für die TTL.

### PairingWebApplicationFactory.cs (PairingWebApplicationFactory)

- **Überflüssiger Code** — `CreateTempDbPath` (Zeilen 15–20) ruft `File.Delete` auf einem Pfad auf, der ein frisches `Guid.NewGuid()` im Dateinamen enthält — die Datei kann per Konstruktion nicht existieren. Der `try/catch`-Block ist damit toter Code (die sinnvolle Löschung der tatsächlich angelegten DB-Datei erfolgt bereits in den `Dispose`-Methoden der Aufrufer).

  Empfehlung: `File.Delete`-Aufruf samt `try/catch` aus `CreateTempDbPath` entfernen; die Methode liefert dann schlicht den eindeutigen Pfad.

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
- `VideoWebPlayer/Services/LoginIpBlockService.cs` (Diff: `Unblock` räumt Cache immer)
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
- `VideoWebPlayer.Tests/LoginIpBlockServiceTests.cs`
- `VideoWebPlayer.Tests/Helpers/PairingCryptoHelper.cs`
- `VideoWebPlayer.Tests/Helpers/PairingWebApplicationFactory.cs`
- `VideoWebPlayer.Tests/Helpers/PairingExchangeContractTestBase.cs`
- `VideoWebPlayer.Tests/Helpers/PairingTestDb.cs`
- `VideoWebPlayer.Tests/ApiTokenConfigurationTests.cs` (Diff)
- `VideoWebPlayer.Tests/ApiDocumentationContractTests.cs` (Diff: neuer Endpoint-Eintrag)

Dokumentationsdateien (`docs/API.md`, `docs/GUIDE_Installation.md`, `docs/SECRETS_MANAGEMENT.md`, `docs/help/einrichtung.md`, Feature-Docs) wurden nicht als Quellcode reviewt.
