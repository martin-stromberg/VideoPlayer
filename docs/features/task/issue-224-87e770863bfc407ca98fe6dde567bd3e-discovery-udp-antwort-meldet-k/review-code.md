# Code-Review

## Ergebnis

**Status:** Befunde vorhanden

## Befunde

### UdpDiscoveryListener.cs (UdpDiscoveryListener)

- **Fehlerbehandlung – CancellationToken wird nicht an den blockierenden Empfang durchgereicht** – `udp.ReceiveAsync()` (Zeile 59) wird ohne `CancellationToken` aufgerufen. `Stop()` (Zeile 47–50) cancelled zwar `_cts`, weckt den blockierenden Empfang aber nicht auf: Die Schleife terminiert erst, wenn das nächste Datagramm eintrifft und die `while`-Bedingung greift — bis dahin läuft die Hintergrund-Task weiter (inkl. gebundenem Socket). Der neu eingeführte `catch (OperationCanceledException)`-Filter (Zeile 68) ist damit nur über `_responseFactory(cancellationToken)` erreichbar, nie über den Empfang selbst. Außerdem wird `_cts` nach `Cancel()` nie disposed und `udp` nicht geschlossen.

  Empfehlung: `udp.ReceiveAsync(cancellationToken)` verwenden (Overload mit `ValueTask<UdpReceiveResult>` ist unter net10.0 verfügbar), damit `Stop()` den Empfang tatsächlich aufweckt und der OCE-Filter greift; alternativ den Socket beim Stoppen schließen. `_cts` nach `Cancel()` disposen.

- **Fehlerbehandlung – Bind-Fehler faulted die Hintergrund-Task unbeobachtet** – `new UdpClient(_port)` (Zeile 54) steht außerhalb der `try`-Blöcke: Schlägt das Binden fehl (z. B. Port 5001 bereits belegt), faulted die per `Task.Run` (Zeile 41) gestartete Task mit einer `SocketException`, die niemand beobachtet oder protokolliert — der Listener ist dann lautlos tot, obwohl die überarbeitete Fehlerbehandlung genau Diagnostizierbarkeit zum Ziel hat.

  Empfehlung: Das Binden in einen try-Block in `ListenAsync` verlegen und beim Fehlschlag `_logger.LogError` (einmalig, der Listener kann nicht starten) ausgeben, oder `ListenAsync` einen umgebenden try/catch spendieren.

### DiscoveryResponseBuilder.cs (DiscoveryResponseBuilder)

- **Toter Code – redundante Literal-Prüfungen** – `trimmed is "*" or "+" or "0.0.0.0" or "[::]"` (Zeile 172): Die IP-Literale `"0.0.0.0"` und `"[::]"` sind doppelt abgesichert — `IPAddress.TryParse` (Zeile 177) parst beide (`"[::]"` wird seit .NET Core 3.0 in Klammer-Notation akzeptiert, eigenhändig unter net10.0 verifiziert) und die anschließenden `Equals(IPAddress.Any)`-/`IPv6Any`- bzw. `InterNetworkV6`-Prüfungen lehnen sie ohnehin ab. Lediglich `"*"` und `"+"` sind nötig, weil `TryParse` dafür fehlschlägt.

  Empfehlung: Die beiden IP-Literale aus dem `is`-Pattern entfernen (oder als bewussten Fast-Path kommentieren), damit klar ist, welche Fälle nur das Pattern abdeckt.

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
- `VideoWebPlayer/Services/ProgramSettingsService.cs` (inkl. `GeneralSettingsUpdate`)
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
