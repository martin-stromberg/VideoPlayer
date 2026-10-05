# Code-Review

## Ergebnis

**Status:** Befunde vorhanden

## Befunde

### Program.cs (Resolver-Lambda für den UDP-Discovery-Listener)

- **Fehlende Kapselung / Testbarkeit** – Die ~30-zeilige Resolver-Lambda (Zeilen 61–91) enthält fachliche Logik: Scope-Erzeugung, Admin-Wert lesen, Options auflösen, `IServerAddressesFeature` auslesen, DNS-Auflösung, Aufruf von `DiscoveryResponseBuilder.Build`. Das ist verdrahtete Anwendungslogik in `Program.cs`, die als anonyme Methode nicht isoliert testbar ist (im Tasks-Dokument selbst als „Kein direkter Test" vermerkt).

  Empfehlung: In einen registrierten Dienst auslagern (z. B. `DiscoveryBaseUrlResolver` mit `Task<string> ResolveAsync(CancellationToken)`, injiziert `IServiceProvider`/`IOptions<DiscoveryOptions>`/`IServer`/`IConfiguration`), sodass `Program.cs` nur noch `resolver.ResolveAsync` an den Listener reicht und die Fehler-/Fallback-Logik unit-testbar wird.

- **Fehlerbehandlung – still geschluckte Exceptions** – Zwei `catch`-Blöcke ohne Exception-Typ (Zeilen 71–74 und 85–88) schlucken alle Exceptions kommentarlos. Der Fail-open-Kommentar erklärt die Absicht, aber ein persistenter DB-Fehler führt dazu, dass der Admin-Override dauerhaft ignoriert wird, ohne dass das irgendwo sichtbar ist; ebenso ein dauerhaft kaputter DNS-Lookup. Serilog ist in `Program.cs` bereits verfügbar.

  Empfehlung: `catch (Exception ex)` mit `Log.Warning(ex, "...")` (bzw. `Log.Debug`, wenn das Log-Aufkommen pro UDP-Broadcast zu hoch wäre — dann wenigstens einmalig bzw. gedrosselt loggen), damit der Fallback-Betrieb diagnostizierbar bleibt.

- **Blockierende und doppelte DNS-Auflösung** – `Dns.GetHostEntry(Dns.GetHostName())` (Zeile 83) ist ein synchroner, blockierender Netzwerkaufruf in einem `async`-Kontext und läuft bei jeder Discovery-Anfrage. Zusätzlich wird `Dns.GetHostName()` im Fallback-Zweig von `DiscoveryResponseBuilder.ResolveHost` (`DiscoveryResponseBuilder.cs` Zeile 127) noch einmal aufgerufen — pro Anfrage also bis zu zwei DNS-Abfragen.

  Empfehlung: `await Dns.GetHostEntryAsync(Dns.GetHostName())` verwenden und den Hostnamen einmal auflösen (z. B. als eigenen Parameter an `Build` übergeben oder aus dem `IPHostEntry` wiederverwenden), statt ihn zweimal zu ermitteln.

### DiscoveryResponseBuilder.cs (DiscoveryResponseBuilder)

- **Namens-/Dokumentationsabweichung – „pure" Logik mit I/O-Seiteneffekt** – Der Klassenkommentar (Zeile 8) beschreibt „Pure derivation logic", `ResolveHost` (Zeile 127) ruft aber `Dns.GetHostName()` auf — ein Environment-abhängiger Aufruf mitten in der angeblich reinen Ableitung. Konsequenz: Der Hostname-Fallback ist in Tests von der Ausführungsmaschine abhängig (`Build_UsesHostName_WhenNoLanAddress` vergleicht gegen denselben DNS-Aufruf und verdeckt den Seiteneffekt damit).

  Empfehlung: Den Hostnamen (analog zu `hostAddresses`) als Parameter an `Build` übergeben, damit die Klasse tatsächlich rein ist, oder den Klassenkommentar korrigieren und den Fallback bewusst als Environment-Zugriff dokumentieren.

### ProgramSettingsService.cs (ProgramSettingsService)

- **Long Parameter List** – `UpdateGeneralSettingsAsync` (Zeilen 144–151) hat mit dem neuen `discoveryPublicBaseUrl` nun 7 Parameter (`applicationTitle`, `scanProcessIntervalMinutes`, `mediaCollectionScanIntervalDays`, `continueWatchingEndThresholdSeconds`, `mdnsAdvertisementEnabled`, `discoveryPublicBaseUrl`, `cancellationToken`). Die Methode wurde bereits vor diesem Branch über die sinnvolle Grenze hinaus erweitert; der neue Parameter verschärft das.

  Empfehlung: Ein Parameterobjekt einführen (z. B. `record GeneralSettingsUpdate(...)` oder die vorhandene `SettingsModel`-Struktur spiegeln), sodass neue Einstellungen die Signatur nicht weiter aufblähen.

### Fehlermeldungstexte (DiscoveryOptionsValidator / AbsoluteHttpUrlAttribute / ProgramSettingsService)

- **Doppelter Code** – Die Fehlermeldung zur selben Regel liegt in drei Varianten vor: `DiscoveryOptionsValidator.cs` Zeile 15 („Discovery:PublicBaseUrl muss eine absolute http- oder https-URL sein…"), `AbsoluteHttpUrlAttribute.cs` Zeile 18 („Der Wert muss eine absolute http- oder https-URL sein…"), `ProgramSettingsService.cs` Zeile 155 („Die öffentliche Basis-URL muss eine absolute http- oder https-URL sein…"). Die Regel selbst ist zentral in `DiscoveryUrlRules` gekapselt, die zugehörige Botschaft aber nicht — bei einer Regel-/Formulierungsänderung müssen drei Stellen synchron gehalten werden.

  Empfehlung: Die gemeinsame Kernaussage („…absolute http- oder https-URL…" inkl. Beispiel) als Konstante in `DiscoveryUrlRules` hinterlegen; kontextspezifische Präfixe (Schlüsselname, Feldname) am Aufrufort ergänzen.

### ProgramSettings.razor (SaveAsync)

- **Fehlerbehandlung – Validierungsfehler über generischen Exception-Typ identifiziert** – `catch (ArgumentException ex)` (Zeile 162) interpretiert jede `ArgumentException` aus `UpdateGeneralSettingsAsync` als die bekannte URL-Validierungsmeldung und zeigt `ex.Message` im UI an. Würde im Service (oder in aufgerufenen EF-Code) künftig eine andere `ArgumentException` geworfen, landete eine interne Meldung ungefiltert als „Fachfehler" im Admin-UI.

  Empfehlung: Einen dedizierten Exception-Typ für die Fachvalidierung verwenden (z. B. `System.ComponentModel.DataAnnotations.ValidationException` oder ein eigener `InvalidDiscoveryUrlException`) und nur diesen fangen.

### ProgramSettingsServiceTests.cs (ProgramSettingsServiceTests)

- **Testqualität – mehrere fachliche Fälle in einem Fact** – `UpdateGeneralSettingsAsync_PersistsDiscoveryPublicBaseUrl` prüft zwei fachliche Fälle hintereinander (Speichern mit Trim-Normalisierung inkl. Rücklesen, danach Löschpfad über leere Eingabe) mit zwei Act-/Assert-Blöcken in einer Methode.

  Empfehlung: In zwei Tests aufteilen (`..._PersistsNormalizedUrl` und `..._ClearsOverrideOnEmptyInput`), damit ein Fehler eindeutig einem Fall zugeordnet wird.

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
- `VideoWebPlayer/Services/DiscoveryResponseBuilder.cs` (neu)
- `VideoWebPlayer/Services/ProgramSettingsService.cs`
- `VideoWebPlayer/Services/UdpDiscoveryListener.cs`
- `VideoWebPlayer/appsettings.json`
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
- `docs/features/task/issue-224-87e770863bfc407ca98fe6dde567bd3e-discovery-udp-antwort-meldet-k-tasks.md`
- `docs/features/task/issue-224-87e770863bfc407ca98fe6dde567bd3e-discovery-udp-antwort-meldet-k/` (Planungs-/Inventar-Dokumente, inhaltlich geprüft)
