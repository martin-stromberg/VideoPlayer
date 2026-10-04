# Bestandsaufnahme: Konfiguration

## `VideoWebPlayer/appsettings.json`

| Abschnitt / Schlüssel | Zeilen | Bestand |
|-----------------------|--------|---------|
| `Host:Address` / `Host:Port` | — | **Nicht definiert** — werden nur in `Program.cs` Zeile 54 gelesen (Fallback `localhost`/`5000`) und in `JsonKeys` geschützt (s. u.). |
| `Discovery` / `Mdns` | — | **Kein Abschnitt vorhanden.** |
| `Kestrel:Endpoints:Http:Url` | — | In `appsettings.json` nicht gesetzt; nur in `JsonKeys` gelistet (Z. 125). |
| `Kestrel:Limits:MaxRequestBodySize` | — | Nicht gesetzt; in `JsonKeys` (Z. 126), manuell via `KestrelLimits` angewendet. |
| `AutoUpdate:ProtectedFiles` | 94–166 | Zwei Merge-Regeln: `web.config` (XmlElements/XmlAttributes, Z. 95–113) und `appsettings*.json` mit `JsonKeys`-Liste (Z. 115–164). |
| `AutoUpdate:ProtectedFiles[1].JsonKeys` | 117–164 | Enthält u. a. `Host:Address` (Z. 127), `Host:Port` (Z. 128), `Kestrel:*` (Z. 125–126), `Jwt:*`, `Backups:*`, `Pairing:*`, `Auth:RefreshTokenTtlDays`. **Neue Discovery-Schlüssel müssen hier ergänzt werden**, damit lokale Werte ein Update überstehen. |
| `Serilog` / `Logging` | 42–75 | `VideoWebPlayer.Services` läuft auf `Debug` — ein Discovery-Dienst im Namespace `VideoWebPlayer.Services` würde auf Debug-Level loggen. |
| `Playlists`, `Backups`, `EpisodeBackgroundImage` | 5–41 | Beispiel-Abschnitte für Feature-Konfiguration. |

## `VideoWebPlayer/appsettings.Development.json`
- Nur Serilog-/Logging-Overrides und `AutoUpdate:Enabled=false`, `AutoUpdate:EnableAutomaticInstallation=false`. Keine Host-/Discovery-Schlüssel.

## `VideoWebPlayer/appsettings.Production.json`
- `Kestrel:Endpoints:Http:Url` = `http://*:5002` (Z. 22–28) — der tatsächliche HTTP-Port in Produktion weicht damit vom `Host:Port`-Fallback (5000) ab; die Discovery-Antwort könnte einen falschen Port melden, wenn nur `Host:Port` herangezogen wird.
- Serilog-/Logging-Overrides.

## `VideoWebPlayer/Properties/launchSettings.json`
- Profil `http`: `http://localhost:5039` (Development).
- `iisSettings.iisExpress.applicationUrl`: `http://localhost:57331`.

## `VideoWebPlayer/web.config`
- `hostingModel="outofprocess"`, `processPath="dotnet"`, `requestTimeout="02:00:00"` — unter IIS läuft die App als separater Kestrel-Prozess; ein In-Process-mDNS-Advertisement läge im Backend-Prozess, nicht in `w3wp`.
- `requestLimits maxAllowedContentLength="4294967295"`.

## `NuGet.config` (Repo-Root)
- Feeds: `nuget.org` und lokaler Feed `lib/packages` (enthält `msTools.Backup`, `msTools.Updater`). Ein mDNS-Paket käme aus nuget.org — laut Repo-Regeln mit Lizenz-, Release-Build- und CI-Prüfung.

## Zusammenspiel
- Überschreibungsreihenfolge: `appsettings.json` < `appsettings.{Env}.json` < `appsettings.Local.json` (`LocalConfigurationExtensions`) < User Secrets / Umgebungsvariablen / Kommandozeile.
- `appsettings.Local.json` wird niemals verpackt (`VideoWebPlayer.csproj` Zeilen 57–61: `CopyToOutputDirectory`/`CopyToPublishDirectory` = `Never`).
