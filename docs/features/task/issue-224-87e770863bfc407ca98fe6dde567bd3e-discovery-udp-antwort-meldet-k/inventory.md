# Bestandsaufnahme: UDP-Discovery-Antwort soll die erreichbare Basis-URL melden (Issue #224)

Analysiert wurde der Discovery-Bereich des Servers (`UdpDiscoveryListener`, `Program.cs`,
`Host:`/`App:`-Konfiguration, mDNS-Feature als Vergleichsmuster) bezogen auf die Anforderung,
dass die Antwort auf `VIDEOWEBPLAYER_DISCOVERY`-Broadcasts künftig die aus Clientsicht
erreichbare öffentliche Basis-URL statt `http://localhost:5000` enthalten soll.

## Zusammenfassung

- `UdpDiscoveryListener` (`VideoWebPlayer/Services/UdpDiscoveryListener.cs`) ist eine
  schlanke Klasse ohne DI, Interface, Logging oder Tests: Der Konstruktor nimmt die
  Antwortadresse als fertigen `string` entgegen; der Versand erfolgt in einer
  `Task.Run`-Schleife mit `UdpClient` auf dem festen Port 5001.
- Die Antwortadresse wird einmalig beim Start in `Program.cs` Zeile 54 als
  `http://{Host:Address ?? "localhost"}:{Host:Port ?? "5000"}` gebaut — einmalig, nicht pro
  Anfrage, und bevor gebundene Serveradressen verfügbar sind (`app.Run()` folgt erst in
  Zeile 59). `Host:Address` hat sonst keine weitere Lesestelle; `Host:Port` wird zusätzlich
  von `MdnsServiceProfileBuilder` in der Port-Ableitungskette gelesen.
- `Program.cs` Zeile 51 schirmt den Listener mit `!IsEnvironment("Testing")` ab; dasselbe
  Muster gilt für `MdnsAdvertiserWorker` in `ServiceCollectionExtensions.cs` Zeilen 297–301.
- Das mDNS-Feature liefert das etablierte Muster für die geforderte Ableitung:
  netzwerkfreie `*Builder`-Logik (`MdnsServiceProfileBuilder` mit Port-Kette
  `Mdns:Port` → gebundene Adressen → `Kestrel:Endpoints:Http:Url` → `Host:Port` → 5000 und
  `Dns.GetHostName()`), Laufzeit-`Worker` (`MdnsAdvertiserWorker`) mit
  `IServerAddressesFeature` nach `ApplicationStarted`, Options-Typ `MdnsOptions` mit
  `IValidateOptions`/`ValidateOnStart()`, zweistufiger Schalter über `Mdns:Enabled` und
  `Setup.MdnsAdvertisementEnabled`.
- Konfiguration: `Host:Address`/`Host:Port` stehen bereits in
  `AutoUpdate:ProtectedFiles[1].JsonKeys` (`appsettings.json` Zeilen 133–134); neue Schlüssel
  müssen dort ergänzt werden. `App:BaseUrl` existiert als undokumentierter HttpClient-Fallback
  (`ServiceCollectionExtensions.cs` Zeile 206), steht aber weder in einer `appsettings*.json`
  noch in `JsonKeys`. `appsettings.Local.json` wird über `AddLocalJsonConfiguration`
  geladen.
- Dokumentationsstellen zum Discovery-Verhalten: `docs/API.md` Zeilen 1032–1049,
  `docs/GUIDE_Installation.md` Zeilen 53 und 135–145, `docs/help/einrichtung.md` Zeilen 24–30,
  `README.md` Zeilen 46 und 85, `docs/RELEASE_NOTES.md`, `docs/INSTALL_AVAHI.md`.
- Für `UdpDiscoveryListener` existieren keinerlei Tests; für das mDNS-Feature gibt es eine
  vollständige Unit-Test-Klasse pro Baustein (`MdnsServiceProfileBuilderTests`,
  `MdnsAdvertiserWorkerTests`, `MdnsRegistrationTests`, `MdnsConfigurationTests`,
  `MdnsOptionsValidatorTests`) sowie `AutoUpdateProtectedFilesTests`, der die ausgelieferte
  `JsonKeys`-Liste validiert.

Test-Ausgangszustand: Voller Basislauf `dotnet test VideoWebPlayer.Tests/VideoWebPlayer.Tests.csproj`
(Debug) grün — 1665 bestanden, 0 fehlgeschlagen, 1 absichtlich übersprungen
(`PairedDevice_ReportsProgressForMovieWithoutCollection_ContinueWatchingListStillLoads`,
bekannter dokumentierter Produktfehler). Zusatzsuite `tools/MarkdownLinkCheck.Tests`:
6/6 grün. Keine preexisting Fehlschläge. Details und Nachweis:
[inventory/tests.md](inventory/tests.md) sowie `inventory/test-results/`.

## Details

- [Datenmodell / Konfigurationsmodelle](inventory/models.md)
- [Logik](inventory/logic.md)
- [Interfaces / Contracts](inventory/interfaces.md)
- [Tests](inventory/tests.md)
