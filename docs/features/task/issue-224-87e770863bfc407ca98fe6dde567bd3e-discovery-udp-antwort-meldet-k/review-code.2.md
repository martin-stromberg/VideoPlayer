# Code-Review

## Ergebnis

**Status:** Befunde vorhanden

## Befunde

### UdpDiscoveryListener.cs (UdpDiscoveryListener)

- **Fehlerbehandlung – still geschluckte Exceptions inkl. `OperationCanceledException`** – Der typenlose `catch { }`-Block (Zeile 62) schluckt nun auch alle Exceptions aus dem neuen `await _responseFactory(cancellationToken)` (Zeile 57) kommentarlos. Der Block ist älter als der Branch, umschließt aber jetzt verdrahtete Anwendungslogik: Ein Fehler in der URL-Auflösung, der nicht vom Resolver selbst abgefangen wird (z. B. ein `ObjectDisposedException` beim App-Shutdown oder ein Bug in einer anderen `Func`-Implementierung), bleibt unsichtbar, und `OperationCanceledException` wird geschluckt statt gefiltert. Zwar bricht die `while`-Bedingung die Schleife dennoch korrekt ab, aber der Fail-open-Pfad ist hier – anders als im `DiscoveryBaseUrlResolver` – komplett ohne Logging und damit nicht diagnostizierbar.

  Empfehlung: `catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { break; }` für den Abbruch ergänzen und den Rest als `catch (Exception)` mit mindestens gedrosseltem Logging (bzw. bewusst dokumentiertem Schweigen nur für transiente `SocketException`) behandeln, analog zum Logging im `DiscoveryBaseUrlResolver`.

### DiscoveryResponseBuilder.cs (DiscoveryResponseBuilder)

- **Fehlerbehandlung/Validierung – Lücke bei IPv6-Literalen in Klammern** – `IsUsableHost` (Zeilen 166–189) filtert Loopback/IPv6 nur, wenn `IPAddress.TryParse(trimmed, …)` erfolgreich ist. `TryParse` akzeptiert aber keine Klammer-Notation: Ein `Host:Address`-Wert wie `[::1]`, `[0:0:0:0:0:0:0:1]` oder `[fe80::1]` schlägt beim Parsen fehl, gilt damit als „usable" und wird unverändert gemeldet (`http://[::1]:5000`) – obwohl das Dokument und der Klassenkommentar zusichern, dass Loopback- und IPv6-Adressen nie gemeldet werden. Der Pfad über `boundAddresses`/Kestrel-URLs ist nicht betroffen (`DnsSafeHost` liefert IPv6 ungeklammert), die Lücke betrifft nur den rohen `Host:Address`-Wert.

  Empfehlung: Vor `TryParse` umschließende Klammern entfernen (z. B. `trimmed.Trim('[', ']')` nur wenn beide vorhanden) oder den Wert über `Uri.TryCreate($"http://{trimmed}")` normalisieren und `uri.Host`/`DnsSafeHost` prüfen, damit IPv6-Literale konsistent erkannt werden.

### DiscoveryBaseUrlResolverTests.cs (DiscoveryBaseUrlResolverTests)

- **Ressourcenleck im Test – nicht disposed `ServiceProvider`** – `services.BuildServiceProvider()` (Zeile 36) und der Provider in `ThrowingScopeFactory` (Zeile 121) werden nie disposed; `provider` hält scoped Services (DbContext/EF-Registrierungen) und sollte als `await using`/`using` verwaltet werden, wie in `DiscoveryConfigurationTests` vorgelebt.

  Empfehlung: `await using var provider = services.BuildServiceProvider();` in `ResolveAsync_ReturnsAdminBaseUrl_WhenSet` und `using` für den Provider in `ThrowingScopeFactory`.

- **Testqualität – Testname beschreibt anderen Pfad als ausgeführt** – `ResolveAsync_UsesBoundAddress_WhenNothingConfigured` (Zeilen 78–96) nutzt `ThrowingScopeFactory()`: Das Lesen des Admin-Overrides schlägt hier mit einer Exception fehl (Fail-open-Pfad), es ist nicht „nichts konfiguriert". Ein echter „nichts konfiguriert"-Fall (leere `Setups`-Zeile bzw. `null`-Rückgabe des Settings-Service) wird nicht getestet.

  Empfehlung: Entweder den Test umbenennen (`..._WhenSettingsReadFails`) oder den Scope so aufbauen, dass `GetDiscoveryPublicBaseUrlAsync` tatsächlich `null` liefert, damit der ohnehin vorhandene Fail-open-Test (`..._AndLogsWarning_WhenSettingsReadFails`) und der Normalpfad getrennt abgedeckt sind.

## Geprüfte Dateien

- `VideoWebPlayer/Program.cs`
- `VideoWebPlayer/Components/Pages/Admin/ProgramSettings.razor`
- `VideoWebPlayer/Components/Shared/AbsoluteHttpUrlAttribute.cs` (neu)
- `VideoWebPlayer/Configuration/DiscoveryOptions.cs` (neu)
- `VideoWebPlayer/Configuration/DiscoveryOptionsValidator.cs` (neu)
- `VideoWebPlayer/Configuration/DiscoveryUrlRules.cs` (neu)
- `VideoWebPlayer/Data/Setup.cs`
- `VideoWebPlayer/Extensions/ServiceCollectionExtensions.cs`
- `VideoWebPlayer/Migrations/ApplicationDbContextModelSnapshot.cs`
- `VideoWebPlayer/Migrations/20261005171612_AddSetupDiscoveryPublicBaseUrl.cs` (neu)
- `VideoWebPlayer/Migrations/20261005171612_AddSetupDiscoveryPublicBaseUrl.Designer.cs` (neu)
- `VideoWebPlayer/Services/Backups/VideoWebPlayerBackupData.cs`
- `VideoWebPlayer/Services/DiscoveryBaseUrlResolver.cs` (neu)
- `VideoWebPlayer/Services/DiscoveryResponseBuilder.cs` (neu)
- `VideoWebPlayer/Services/Exceptions/DiscoveryUrlValidationException.cs` (neu)
- `VideoWebPlayer/Services/ProgramSettingsService.cs`
- `VideoWebPlayer/Services/UdpDiscoveryListener.cs`
- `VideoWebPlayer/appsettings.json`
- `VideoWebPlayer.Tests/DiscoveryBaseUrlResolverTests.cs` (neu)
- `VideoWebPlayer.Tests/DiscoveryConfigurationTests.cs` (neu)
- `VideoWebPlayer.Tests/DiscoveryOptionsValidatorTests.cs` (neu)
- `VideoWebPlayer.Tests/DiscoveryResponseBuilderTests.cs` (neu)
- `VideoWebPlayer.Tests/DiscoveryUrlRulesTests.cs` (neu)
- `VideoWebPlayer.Tests/UdpDiscoveryListenerTests.cs` (neu)
- `VideoWebPlayer.Tests/ProgramSettingsE2ETests.cs`
- `VideoWebPlayer.Tests/ProgramSettingsServiceTests.cs`
- `VideoWebPlayer.Tests/Services/Backups/VideoWebPlayerBackupDataTests.cs`
- `README.md`
- `docs/API.md`
- `docs/GUIDE_Installation.md`
- `docs/INSTALL_AVAHI.md`
- `docs/RELEASE_NOTES.md`
- `docs/help/einrichtung.md`
