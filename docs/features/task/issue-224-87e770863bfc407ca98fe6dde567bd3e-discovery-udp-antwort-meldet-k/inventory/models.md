# Datenmodell / Konfigurationsmodelle

Für die UDP-Discovery-Antwort existiert kein eigenes Datenmodell und keine gebundene
Options-Klasse — die bisherige Antwortadresse wird ausschließlich aus
`IConfiguration`-Schlüsseln (`Host:Address`, `Host:Port`) per String-Interpolation in
`Program.cs` gebaut. Folgende bestehende Modelle sind fachlich benachbart bzw. als
Muster relevant:

## `MdnsOptions`
Datei: `VideoWebPlayer/Configuration/MdnsOptions.cs`

| Eigenschaft | Typ | Beschreibung / Zweck |
|-------------|-----|----------------------|
| `Enabled` | `bool` (Standard `true`) | Betreiber-Master-Switch für mDNS-Advertisement; wirkt nur in Konjunktion mit dem Admin-Schalter `Setup.MdnsAdvertisementEnabled` |
| `InstanceName` | `string` (Standard `"VideoWebPlayer"`) | DNS-SD-Instanzname |
| `ServiceType` | `string` (Standard `"_videowebplayer._tcp.local."`) | Angekündigter DNS-SD-Diensttyp |
| `Port` | `int?` (Standard `null`) | Angekündigter Port-Override; `null` löst die Ableitungskette in `MdnsServiceProfileBuilder` aus |

Wird per `services.AddOptions<MdnsOptions>().Bind(configuration.GetSection("Mdns")).ValidateOnStart()`
in `ServiceCollectionExtensions.cs` (Zeilen 280–283) gebunden; Validierung über
`MdnsOptionsValidator` (`IValidateOptions<MdnsOptions>`).

## `MdnsAdvertisement`
Datei: `VideoWebPlayer/Services/MdnsAdvertisement.cs`

`internal sealed record` — Ergebnistyp der reinen Ableitungslogik
`MdnsServiceProfileBuilder.Build` (netzwerkfrei testbar).

| Eigenschaft | Typ | Beschreibung / Zweck |
|-------------|-----|----------------------|
| `InstanceName` | `string` (required) | Instanzname für `ServiceProfile` |
| `ServiceType` | `string` (required) | Diensttyp wie konfiguriert |
| `Port` | `int` (required) | Aufgelöster Ankündigungs-Port |
| `HostName` | `string` (required) | Maschinen-Hostname via `Dns.GetHostName()` |
| `TxtRecords` | `IReadOnlyDictionary<string,string>` (required) | Feste TXT-Records `path=/`, `app=VideoWebPlayer` |

## `Setup` (Ausschnitt)
Datei: `VideoWebPlayer/Data/Setup.cs` (Zeile 53)

| Eigenschaft | Typ | Beschreibung / Zweck |
|-------------|-----|----------------------|
| `MdnsAdvertisementEnabled` | `bool` (Standard `true`) | Admin-Schalter für mDNS-Advertisement; über `ProgramSettingsService` verwaltet, in `VideoWebPlayerBackupData` (`OptionalRestoreColumns` Zeile 74, `OptionalRestoreBoolDefaults` Zeile 97) eingetragen; Migrationsbeleg: `VideoWebPlayer/Migrations/20261004173814_AddSetupMdnsAdvertisementEnabled.cs` |

Muster-Beleg dafür, wie eine admin-pflegbare Einstellung (`/admin/program-settings`)
aktuell aussehen würde — für die Discovery-Basis-URL ist laut Anforderung keine
DB-Einstellung erkennbar erforderlich (offene Frage Nr. 6).
