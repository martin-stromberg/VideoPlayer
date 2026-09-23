# Code-Review

Basisbranch: `staging` (ermittelt via `git reflog show` — `branch: Created from origin/staging`, Merge-Base `bfa8efd`). Die Implementierung liegt überwiegend uncommitted im Working Tree; geprüft wurden daher Merge-Base-Diff plus uncommitted geänderte/neue Quelldateien (`git status`). Beide Projekte (`VideoWebPlayer`, `VideoWebPlayer.Tests`) kompilieren fehlerfrei (0 Warnungen, 0 Fehler).

Zusatzprüfung `RaiseUiActionRequested`: Keine solchen Aktionen im Diff bzw. in der Codebasis vorhanden — nicht zutreffend.

## Ergebnis

**Status:** Befunde vorhanden

## Befunde

### PairingService.cs (PairingService)

- **God-Methode** — `ExchangeAsync` (Zeilen 150–213, ~63 Zeilen) erledigt fünf konzeptuell getrennte Aufgaben hintereinander: Request-Validierung, Import und Kurvenprüfung des Client-ECDH-Schlüssels, atomare Konsumierung des Pairing-Codes, Token-Ausstellung sowie AES-256-GCM-Verschlüsselung und Payload-Assemblierung.

  Empfehlung: In private Methoden auslagern, z. B. `TryImportClientPublicKey(...)` (Import + nistP256-Prüfung) und `EncryptToken(...)` (Nonce/Ciphertext/Tag + Base64-Payload). Die Orchestrierung in `ExchangeAsync` wird dadurch deutlich lesbarer.

- **Fehlende Kapselung / falsche Verantwortlichkeit** — `PairingService` ruft `DeviceTokenService.ComputeHash` (Zeilen 140, 176) auf, um Pairing-Codes zu hashen. SHA-256-Hashing ist keine Verantwortlichkeit des `DeviceTokenService`; die Methode wird dort nur intern als `static` geparkt und von einer fremden Klasse mitbenutzt.

  Empfehlung: Hash-Hilfsfunktion in eine gemeinsame Hilfsklasse auslagern (z. B. `Services/Security/HashHelper.Sha256Hex(...)`) und aus beiden Services sowie den Tests referenzieren.

- **Fehlerbehandlung / nicht-atomare Validierung** — In `ExchangeAsync` (Zeilen 177–190) wird `ExpiresAtUtc <= now` zuerst per `FirstOrDefaultAsync` gelesen, die Konsumierung dann per `ExecuteUpdateAsync` nur mit der Bedingung `ConsumedAtUtc == null` abgesichert. Zwischen Lese- und Update-Zugriff kann der Code ablaufen und wird trotzdem konsumiert.

  Empfehlung: Die Ablaufbedingung in die atomare `ExecuteUpdateAsync`-Where-Klausel aufnehmen (`c.Id == pairingCode.Id && c.ConsumedAtUtc == null && c.ExpiresAtUtc > now`), sodass Gültigkeit und Einmaligkeit in einer Operation geprüft werden.

### Devices.razor

- **Doppelter Code / hardcodierter Wert** — `codeTtlMinutes = Configuration.GetValue("Pairing:CodeTtlMinutes", 5)` (Zeile 160) dupliziert den Default `DefaultCodeTtlMinutes = 5` aus `PairingService`; driftet bei Änderung auseinander. Zusätzlich wird die Ablaufzeit des erzeugten Codes clientseitig geraten: `generatedCodeExpiresAtUtc = DateTime.UtcNow.AddMinutes(codeTtlMinutes)` (Zeile 183), obwohl der Server den tatsächlichen Wert in `PairingCode.ExpiresAtUtc` bereits persistiert hat.

  Empfehlung: `CreatePairingCodeAsync` so erweitern, dass neben dem Klartext-Code auch `ExpiresAtUtc` zurückgegeben wird (z. B. Result-Objekt oder Out-Parameter über `PairingCodeInfo`), und diesen Wert in der UI anzeigen — kein Config-Lesen und keine eigene TTL-Berechnung in der Komponente.

- **Überflüssiger Code** — `ReloadAsync` (Zeilen 171–175) ruft nach `LoadAsync()` manuell `StateHasChanged()` auf. Blazor löst nach Abschluss eines `@onclick`-Event-Handlers automatisch ein Re-Rendering aus; der Aufruf ist wirkungslos.

  Empfehlung: `StateHasChanged()`-Aufruf entfernen.

### PairingTestDb.cs (PairingTestDb)

- **Fehlende Ressourcenfreigabe** — `Dispose` (Zeilen 47–51) gibt `_scope` und `Connection` frei, nicht aber den erzeugten `ServiceProvider` `_provider` (Zeile 14, erstellt in Zeile 39). Der Provider inkl. DbContextPool läuft in jedem Test-Fixture bis zum Prozessende weiter. Außerdem sind die `?.`-Aufrufe auf den non-nullable Feldern überflüssig.

  Empfehlung: `_provider.Dispose()` in `Dispose` ergänzen und die Null-Conditional-Operatoren entfernen.

### PairingExchangeContractTestBase.cs / DevicePairingE2ETests.cs

- **Exceptions still geschluckt** — Leere Catch-Blöcke beim Löschen der Temp-Datenbankdatei: `PairingExchangeContractTestBase.cs` Zeilen 27 und 98 (`catch { }`), `DevicePairingE2ETests.cs` Zeilen 42 und 103 (`catch { /* ... */ }`). Jegliche Exception (auch unerwartete) wird kommentarlos verworfen.

  Empfehlung: Auf die erwarteten Fehler einschränken (`catch (IOException)` / `catch (UnauthorizedAccessException)`) oder zumindest den Grund dokumentieren; pauschales `catch { }` vermeiden.

### PairingServiceTests_CodeCreation.cs

- **Toter Parameter / doppelter Code** — Der optionale Parameter `configValues` in `CreateService(PairingTestDb, Dictionary<string, string?>? configValues = null)` (Zeile 34) wird von keinem Aufrufer mit einem Wert belegt (alle 12 Aufrufe übergeben nur `fixture` bzw. `db`) — spekulative Allgemeinheit. Der zweite Overload `CreateService(ApplicationDbContext)` (Zeilen 42–48) dupliziert zudem den ConfigurationBuilder-Block nahezu identisch.

  Empfehlung: Zu einer einzigen Methode zusammenführen, die einen `ApplicationDbContext` entgegennimmt (Fixture-Aufrufer übergeben `fixture.Db`), und den ungenutzten `configValues`-Parameter entfernen — oder die Overloads auf eine Variante mit Default-Parameter reduzieren.

### ApiTokenConfigurationTests.cs

- **Kopplung / DI-Lifetime-Smell** — `CreateAttributeFixtureAsync` (Zeilen ~200 ff. im Diff) registriert `ApplicationDbContext` und `IDeviceTokenService` als scoped, löst sie aber über `services.GetRequiredService<...>()` am Root-`ServiceProvider` auf. Das funktioniert nur, weil `BuildServiceProvider()` ohne `ValidateScopes` aufgerufen wird; mit Scope-Validierung würde der Test fehlschlagen, und der Fehlerfall (scoped aus Root) bleibt unentdeckt.

  Empfehlung: Einen `IServiceScope` erzeugen und die Services aus `scope.ServiceProvider` auflösen (Scope im `AttributeFixture` mit-disposen).

## Geprüfte Dateien

Liste aller geprüften Quelldateien:
- `VideoWebPlayer/Data/PairedDevice.cs`
- `VideoWebPlayer/Data/PairingCode.cs`
- `VideoWebPlayer/Data/Configurations/PairedDeviceConfiguration.cs`
- `VideoWebPlayer/Data/Configurations/PairingCodeConfiguration.cs`
- `VideoWebPlayer/Data/ApplicationDbContext.cs` (Diff: neue DbSets)
- `VideoWebPlayer/Services/DeviceTokenService.cs`
- `VideoWebPlayer/Services/PairingService.cs`
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
