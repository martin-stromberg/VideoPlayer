# Interfaces / Contracts

Für den UDP-Discovery-Bereich existiert **kein eigenes Projekt-Interface** —
`UdpDiscoveryListener` ist eine konkrete Klasse ohne Abstraktion und wird direkt in
`Program.cs` instanziiert (nicht über DI). Relevante genutzte bzw. als Muster
vorgesehene Contracts:

## `IValidateOptions<MdnsOptions>` (Microsoft.Extensions.Options)
Implementiert von: `VideoWebPlayer/Configuration/MdnsOptionsValidator.cs`

| Methode | Parameter | Rückgabewert | Zweck |
|---------|-----------|--------------|-------|
| `Validate` | `string? name`, `MdnsOptions options` | `ValidateOptionsResult` | Start-Zeit-Validierung der `Mdns`-Sektion; registriert als Singleton in `ServiceCollectionExtensions.cs` Zeile 280, aktiviert per `ValidateOnStart()` (Zeile 283) |

Muster für eine eventuelle Discovery-Options-Klasse samt Validator.

## `IServer` / `IServerAddressesFeature` (Microsoft.AspNetCore.Hosting.Server[.Features])
Konsumiert von: `VideoWebPlayer/Services/MdnsAdvertiserWorker.cs` (Zeile 178)

| Methode / Member | Parameter | Rückgabewert | Zweck |
|------------------|-----------|--------------|-------|
| `IServer.Features` | – | `IFeatureCollection` | Zugriff auf Server-Features |
| `IServerAddressesFeature.Addresses` | – | `ICollection<string>` | Tatsächlich gebundene Adressen; erst nach `IHostApplicationLifetime.ApplicationStarted` befüllt |

Beleg dafür, wie das Projekt gebundene Serveradressen liest — für den
`UdpDiscoveryListener` heute nicht erreichbar, da er vor `app.Run()` startet.

## `IHostApplicationLifetime` (Microsoft.Extensions.Hosting)
Konsumiert von: `MdnsAdvertiserWorker` (Zeilen 23, 144–152)

| Member | Zweck |
|--------|-------|
| `ApplicationStarted` (`CancellationToken`) | Signal, ab dem `IServerAddressesFeature.Addresses` zuverlässig gefüllt ist |
