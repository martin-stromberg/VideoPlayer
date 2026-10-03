# Umsetzungsplan: Update überschreibt `web.config` — Integration `msTools.Updater` 0.11.0-rc.1

## Übersicht

`msTools.Updater` wird von `0.10.4-rc.1` auf `0.11.0-rc.1` gehoben (nupkg liegt bereits unter `lib/packages/`) und der neue Dateierhalt-Mechanismus `AutoUpdateOptions.ProtectedFiles` wird über die Konfiguration aktiviert: `web.config` und `appsettings*.json` erhalten `Merge`-Einträge, sodass deployment-seitige Anpassungen (`environmentVariables`, `security/ipSecurity`, `httpProtocol`, `aspNetCore`-Attribute, deployment-eigene JSON-Schlüssel) eine Update-Installation überleben, während Paket-Verbesserungen derselben Dateien weiterhin ankommen. Ergänzend wird `appsettings.Local.json` als optionale, nie paketierte Konfigurationsquelle eingeführt (V3-Bewertung: Mehrwert bestätigt). Kein UI-Eingriff, keine Datenbankmigration — betroffen sind Paketreferenzen, `appsettings.json`, eine neue Konfigurationsquelle, Tests und Dokumentation.

## Designentscheidungen

| Komponente / Bereich | Gewählter Ansatz | Begründung |
|----------------------|-----------------|------------|
| Ablage der Schutzliste | `AutoUpdate:ProtectedFiles` in `VideoWebPlayer/appsettings.json` per Konfigurationsbindung — **keine** Fluent-Aufrufe (`PreserveFile`/`MergeFile`/`ProtectFile`) in `AutoUpdateExtensions` | Projektkonvention („Im Code steht nur, was nicht aus der Konfiguration gebunden werden kann"). Verifizierter Vorrang: Explizite Fluent-Einträge **ersetzen** die gesamte gebundene Liste all-or-nothing (`ReapplyExplicitValues` nach der Bindung) — Mischen ist unmöglich. Die gebundene Liste wird mit jedem Release versioniert ausgeliefert, bleibt deployment-seitig anpassbar und deckt den Anwendungsfall vollständig ab |
| `httpProtocol`-Schutz | `XmlElements`-Eintrag `//system.webServer/httpProtocol` — **nicht** `//httpProtocol/customHeaders` | Der XML-Merge hängt fehlende Elemente an den Elternknoten (letztes Pfadsegment wird abgestreift). `//httpProtocol` existiert in der Paket-`web.config` nicht — `//httpProtocol/customHeaders` würde still übersprungen. `//system.webServer` existiert immer; `httpProtocol` ist komplett deployment-seitig, das ganze Element zu übernehmen ist korrekt. Alle `XmlElements`-Pfade bleiben in der ElementTree-XPath-Teilmenge (keine Attributprädikate) → Linux-kompatibel |
| `XmlElements` für `ipSecurity`/`environmentVariables` | `//security/ipSecurity` und `//aspNetCore/environmentVariables` | Deren Eltern (`//security` via `requestFiltering`, `//aspNetCore`) existieren in der Paket-`web.config` — der Anhänge-Fallback funktioniert verlässlich |
| `XmlAttributes` am `aspNetCore`-Element | `requestTimeout`, `startupTimeLimit`, `shutdownTimeLimit`, `stdoutLogEnabled`, `stdoutLogFile`, `forwardWindowsAuthToken`, `disableStartUpFailedPage`, `processesPerApplication` — **nicht** `processPath`, `arguments`, `hostingModel` | `processPath`/`arguments` werden vom Publish-Transform gesetzt, `hostingModel="outofprocess"` ist per `AspNetCoreHostingModel` erzwungen — ein Zurückschreiben deployment-seitiger Werte würde den Publish brechen. Nicht vorhandene Attribute sind beim Merge No-ops, daher ist eine großzügige Liste deployment-eigener Attribute sicher |
| `JsonKeys`-Auswahl für `appsettings*.json` | Nur deployment-eigene Infrastruktur-/Secret-Schlüssel (konkrete Liste unter „Konfigurationsänderungen"). Bewusst **ausgenommen**: `AutoUpdate:ProtectedFiles` selbst sowie ganze Subtrees (`Serilog` oberhalb von `MinimumLevel:Default`, `Playlists`, `EpisodeBackgroundImage`) | Jeder `JsonKeys`-Eintrag übernimmt den Bestandswert **bei jedem Update** — auch wenn das Deployment den Schlüssel nie angefasst hat; Paket-Verbesserungen am selben Schlüssel werden damit dauerhaft blockiert. `ProtectedFiles` muss paket-seitig erweiterbar bleiben (künftige Releases können Schutz nachliefern); Subtree-Einträge würden neue Paket-Defaults einfrieren. Overrides außerhalb der Liste deckt `appsettings.Local.json` ab |
| `appsettings.Local.json` (V3-Neubewertung) | **Umsetzen** — optionale, nie paketierte JSON-Konfigurationsquelle | Verbleibender Mehrwert bestätigt: `JsonKeys` ist eine geschlossene, paket-eigene Liste — deployment-seitige Overrides außerhalb der Liste gehen weiterhin bei jedem Update verloren, und eine deployment-seitige Erweiterung der `ProtectedFiles`-Liste selbst überlebt ebenfalls nicht. `appsettings.Local.json` ist der einzige Mechanismus, der *beliebige* deployment-seitige Konfiguration dauerhaft erhält, und zugleich eine update-sichere Secret-Ablage ohne Maschinenumgebung. Die Datei ist schon heute update-sicher (nicht im Paket); `CopyToPublishDirectory="Never"` im csproj garantiert das dauerhaft. Der `appsettings*.json`-Wildcard-Eintrag erfasst eine nur-lokale Datei deployment-seitig mit — inhaltlich ein No-op, aber mit Backup nach `Updates/backup/` und Neuformatierung durch den JSON-Merge (dokumentiert in V5) |
| Position der lokalen Konfigurationsquelle | In `builder.Configuration.Sources` direkt nach der **letzten `JsonConfigurationSource`, deren `Path`-Dateiname dem Muster `appsettings*.json` entspricht** (`Path.GetFileName`, `OrdinalIgnoreCase`) — **nicht** „letzte JSON-Quelle generell". Fallback, falls keine solche Quelle existiert: vor der ersten `EnvironmentVariablesConfigurationSource` | Das naive Kriterium „letzte `JsonConfigurationSource`" würde in Development **hinter** den User Secrets landen (`CreateBuilder` registriert sie ebenfalls als `JsonConfigurationSource` **nach** `appsettings.{Environment}.json`, `Path` = `…/secrets.json` im User-Secrets-Ordner) und diese schlagen — gegen das Vorrangziel. Das Dateinamen-Muster trifft zuverlässig nur die Paket-`appsettings`-Dateien. Umgebungsvariablen bleiben der stärkste deployment-seitige Override (u. a. der dokumentierte update-sichere Secret-Weg `Jwt__*`); die lokale Datei schlägt Paket-Dateien, aber keine User Secrets, Prozess-/Maschinenvariablen oder Kommandozeile |
| Skript-Ausführungstest | Entfällt — stattdessen statischer Inhaltstest des generierten Skripts | Ausführung des `update.ps1` erforderte einen echten Windows-Dienst/App-Pool und ein echtes Paket — unverhältnismäßiger Aufwand gegenüber dem zusätzlichen Erkenntnisgewinn. Der Inhaltstest prüft Reihenfolge (Backup → Paketkopie → Merge/Restore), Backup-Pfad und serialisierte Schutzliste; das Laufzeitverhalten ist bibliotheksseitig verifiziert |
| `SECRETS_MANAGEMENT`-Empfehlung (Vorentscheid für V5) | Maschinenweite `Jwt__*`-Umgebungsvariablen bleiben die empfohlene Primärablage für Secrets; geschützte `web.config`-`environmentVariables` werden als gleichwertige Alternative beschrieben | Secrets außerhalb des Anwendungsverzeichnisses bleiben die robusteste Ablage (unabhängig von Merge-Korrektheit und Dateirechten). Bestehende IIS-Installationen mit `environmentVariables` können diese beibehalten — der Schutz greift ab dem Folge-Update |

## Programmabläufe

### Bindung der Schutzliste beim Anwendungsstart

1. `WebApplication.CreateBuilder(args)` (`Program.cs`) lädt `appsettings.json`, `appsettings.{Environment}.json` und die Standardquellen.
2. `builder.AddLocalJsonConfiguration()` (neu, `LocalConfigurationExtensions`) fügt die optionale Quelle `appsettings.Local.json` ein (Details siehe eigener Ablauf).
3. `builder.AddVideoWebPlayerAutoUpdate()` (`AutoUpdateExtensions.cs`) ruft `UseAutoUpdate` auf. Intern läuft zuerst der `configure`-Delegat (Quelle via `VideoWebPlayerUpdateSourceFactory`, `WithUpdateUnitName`), danach `Configuration.GetSection("AutoUpdate").Bind(options)` — dabei wird `AutoUpdate:ProtectedFiles` in `AutoUpdateOptions.ProtectedFiles` gebunden (`Strategy` als `"Merge"`/`"Preserve"`, `XmlElements`/`XmlAttributes`/`JsonKeys` als Stringlisten). Da keine Fluent-Aufrufe erfolgen, greift allein die Bindung.
4. `AutoUpdateOptionsValidator` prüft die Liste beim Start: ungültige Einträge (leerer/rooted/`..`-Pfad, `Merge` ohne Regel, fehlerhaftes `{xpath}@{attr}`-Format) erzeugen eine `OptionsValidationException` — die Anwendung startet dann bewusst nicht (Fail-fast statt still ignoriertem Schutzverlust).
5. `UpdateSettingsService.ApplyToRuntimeOptions` mutiert `ProtectedFiles` nicht (verifiziert, Zeilen 167–181) — die Schutzliste ist rein dateibasiert und nicht über die Admin-UI veränderbar.

Beteiligte Klassen: `Program`, `LocalConfigurationExtensions`, `AutoUpdateExtensions`, `AutoUpdateHostBuilderExtensions` (Bibliothek), `AutoUpdateOptions`, `AutoUpdateProtectedFile`, `AutoUpdateOptionsValidator`, `UpdateSettingsService`.

### Update-Installation mit Dateierhalt (Zielablauf im generierten Skript)

1. `AutoUpdateInstaller.PrepareAsync` lässt die **laufende (alte)** Version über `IAutoUpdateScriptGenerator.GenerateAsync` das Installationsskript erzeugen (`Updates/pending/update.ps1` bzw. `update.sh`). Mit `ProtectedFiles` enthält das Skript die serialisierte `$protectedFiles`-Tabelle bzw. `protected_files_json`.
2. Das Skript führt strikt in dieser Reihenfolge aus: Dienst/App-Pool stoppen → Paket nach Staging entpacken → `Backup-AutoUpdateProtectedFiles` (Wildcard-Treffer im Anwendungsverzeichnis → `Updates/backup/<relativer Pfad>`) → Paketinhalt kopieren → `Invoke-AutoUpdateProtectedFiles` (`Preserve`: Backup über die neue Datei kopieren; `Merge`: `Merge-AutoUpdateXmlContent`/`Merge-AutoUpdateJsonContent` unter Windows, `python3`-Heredoc unter Linux) → Lock-/Descriptor-Entfernung → Neustart.
3. Fehler im Backup-/Merge-Schritt brechen nicht ab: `Write-Warning` (Windows) bzw. `update.log`-Eintrag (Linux); Backups bleiben unter `Updates/backup/` zur manuellen Wiederherstellung liegen (keine Retention/kein Aufräumen).
4. **Migrationsrelevanz:** Das Update *auf* diese Version läuft noch mit dem Skript der alten Version — der Schutz greift erst ab dem ersten Update *nach* dieser Version. Bestandsinstallationen tragen `web.config`-/`appsettings`-Anpassungen einmalig nach dem Update auf diese Version nach (Dokumentation in V5).
5. **Linux:** `Merge` erfordert `python3` auf dem Zielsystem (sonst übersprungen, Warnung, Update läuft weiter); `Preserve` und Backups laufen mit Bordmitteln. Der Linux-XML-Merge unterstützt nur die ElementTree-XPath-Teilmenge — die geplanten Pfade verwenden keine Attributprädikate und sind kompatibel.

Beteiligte Komponenten (Bibliothek): `AutoUpdateInstaller`, `IAutoUpdateScriptGenerator`/`AutoUpdateScriptGenerator`, `FileSystemAutoUpdatePackageStore`, Skript-Templates (`.ps1` App-Pool, `.ps1` Dienst/Executable, `.sh`).

### Lokale Konfigurationsquelle `appsettings.Local.json`

1. `LocalConfigurationExtensions.AddLocalJsonConfiguration(this WebApplicationBuilder)` sucht in `builder.Configuration.Sources` die **letzte `JsonConfigurationSource`, deren `Path`-Dateiname dem Muster `appsettings*.json` entspricht** (`Path.GetFileName`, `StringComparison.OrdinalIgnoreCase` — trifft `appsettings.json` und `appsettings.{Environment}.json` aus `CreateBuilder`, aber nicht die in Development ebenfalls als `JsonConfigurationSource` registrierten User Secrets `…\secrets.json`). Existiert keine solche Quelle, wird als Anker die erste `EnvironmentVariablesConfigurationSource` verwendet (davor eingefügt); existiert auch diese nicht, wird angehängt. An der ermittelten Position wird eine `JsonConfigurationSource` für `appsettings.Local.json` eingefügt (`optional: true`, `reloadOnChange: true`, Pfad relativ zum Content-Root). Damit liegt die Quelle in Development korrekt **vor** User Secrets — nicht dahinter — sowie vor Umgebungsvariablen und Kommandozeile.
2. Fehlt die Datei, ändert sich nichts. Ist sie vorhanden, überlagern ihre Werte die Paket-`appsettings*.json`-Werte, aber nicht User Secrets, Umgebungsvariablen oder Kommandozeile.
3. Die Datei ist nie Teil des Pakets (`CopyToPublishDirectory="Never"` im csproj, `.gitignore`-Eintrag) — sie übersteht Updates vollständig, ohne auf `ProtectedFiles` angewiesen zu sein.

Beteiligte Klassen: `LocalConfigurationExtensions`, `Program`, `JsonConfigurationSource`/`FileConfigurationSource` (Framework).

## Neue Klassen

| Klasse | Typ | Zweck |
|--------|-----|-------|
| `LocalConfigurationExtensions` (`VideoWebPlayer/Extensions/`) | statische Erweiterungsklasse | `AddLocalJsonConfiguration(this WebApplicationBuilder)` — registriert die optionale JSON-Quelle `appsettings.Local.json` an der richtigen Position in `builder.Configuration.Sources` |
| `AutoUpdateProtectedFilesTests` (`VideoWebPlayer.Tests/Services/`) | Testklasse (xUnit v3) | Bindungs-, Validierungs-, Skript-Inhalts- und `web.config`-Elternpfad-Guard-Tests der Schutzliste |
| `LocalConfigurationTests` (`VideoWebPlayer.Tests/Extensions/`) | Testklasse (xUnit v3) | Lade-, Vorrang- und Positionsverhalten der lokalen Konfigurationsquelle |

## Änderungen an bestehenden Klassen

### `Program` (Top-Level-Statements, `VideoWebPlayer/Program.cs`)

- **Neuer Aufruf:** `builder.AddLocalJsonConfiguration()` direkt nach `WebApplication.CreateBuilder(args)` (vor `UseSerilog` und den Service-Registrierungen), damit die Quelle für alle Konfigurationsleser gilt.

### `AutoUpdateExtensions` (`VideoWebPlayer/Extensions/AutoUpdateExtensions.cs`)

- **Keine Änderung** — bewusste Entscheidung: Kein Fluent-Aufruf (`PreserveFile`/`MergeFile`/`ProtectFile`), weil ein einzelner expliziter Eintrag die gesamte gebundene `AutoUpdate:ProtectedFiles`-Liste ersetzen würde (all-or-nothing-Vorrang).

### Weitere betroffene Dateien (keine Klassen)

- `VideoWebPlayer/VideoWebPlayer.csproj` (Z. 53): `PackageReference` `msTools.Updater` `0.10.4-rc.1` → `0.11.0-rc.1`; `Content Update`-Eintrag `appsettings.Local.json` mit `CopyToPublishDirectory="Never"` und `CopyToOutputDirectory="Never"` (verhindert versehentliches Paketieren, falls die Datei lokal existiert; **`Update` statt `Include`** — der Web-SDK-Default-Glob `**/*.json` erfasst die Datei bereits, ein `Include` erzeugt `NETSDK1022`-Duplikatfehler, sobald die Datei existiert).
- `VideoWebPlayer.Tests/VideoWebPlayer.Tests.csproj` (Z. 22): `PackageReference` `msTools.Updater` `0.10.4-rc.1` → `0.11.0-rc.1`.
- `VideoWebPlayer/appsettings.json`: `AutoUpdate:ProtectedFiles`-Array ergänzen (Inhalt siehe „Konfigurationsänderungen").
- `lib/packages/`: `msTools.Updater.0.10.4-rc.1.nupkg` löschen; `msTools.Updater.0.11.0-rc.1.nupkg` (derzeit untracked) committen.
- `.gitignore`: Eintrag für `appsettings.Local.json` ergänzen.

## Datenbankmigrationen

Keine.

## Validierungsregeln

| Feld / Objekt | Regel | Fehlerfall |
|---------------|-------|------------|
| `AutoUpdate:ProtectedFiles[*].Path` | nicht leer, nicht rooted, keine `..`-Segmente, keine ungültigen Pfadzeichen außer `*`/`?` | `OptionsValidationException` beim Start (Bibliotheks-Validator `AutoUpdateOptionsValidator`) |
| `AutoUpdate:ProtectedFiles[*]` mit `Strategy: "Merge"` | mindestens ein Eintrag in `XmlElements`, `XmlAttributes` oder `JsonKeys` | `OptionsValidationException` beim Start |
| `AutoUpdate:ProtectedFiles[*].XmlAttributes[*]` | Format `{element-xpath}@{attribut}` — genau ein `@`, nicht am Ende | `OptionsValidationException` beim Start |
| Ausgelieferte `ProtectedFiles`-Liste in `appsettings.json` | muss den Bibliotheks-Validator fehlerfrei passieren | Abgesichert durch Test (`ShippedAppsettingsProtectedFiles_AreValid`) — ohne Test würde ein Konfigurationsfehler erst beim Kunden-Start auffallen |
| `XmlElements`/`XmlAttributes`-Pfade | nur ElementTree-kompatible XPath-Teilmenge (keine Attributprädikate wie `//add[@name='x']`) — sonst auf Linux wirkungslos (stille leere Treffermenge) | Abgesichert durch Pfadwahl in der Schutzliste + Test, der die ausgelieferte Liste prüft |
| Elternpfade der `web.config`-Merge-Einträge | Für jeden `XmlElements`-Eintrag `//…/child` muss der **Elternpfad** `//…` (letztes Segment abgestreift) in der ausgelieferten `VideoWebPlayer/web.config` mindestens ein Element treffen; für jeden `XmlAttributes`-Eintrag `//elem@attr` muss `//elem` existieren — sonst läuft der Merge-Eintrag lautlos ins Leere | Abgesichert durch Guard-Test `ShippedWebConfig_ContainsAllMergeParents` — ohne Test würde eine künftige `web.config`-Änderung (z. B. Entfernen von `//security`) den `ipSecurity`-Schutz ohne Fehler und ohne Laufzeitwarnung deaktivieren |
| `appsettings.Local.json` | bei Vorhandensein valides JSON (`AddJsonFile`-Standardverhalten) | Start-Fehler bei ungültigem JSON — Standardverhalten, kein eigener Validator nötig |

## Konfigurationsänderungen

| Eintrag | Typ | Standardwert | Zweck |
|---------|-----|--------------|-------|
| `AutoUpdate:ProtectedFiles` (neu, in `appsettings.json`) | `AutoUpdateProtectedFile[]` | Bibliotheks-Default: leere Liste (= bisheriges Verhalten); geplanter Wert: zwei `Merge`-Einträge (s. u.) | Schutzliste für das generierte Update-Installationsskript |
| `appsettings.Local.json` | optionale JSON-Konfigurationsquelle | nicht vorhanden | Beliebige deployment-seitige Overrides außerhalb der `JsonKeys`-Liste; update-sichere Secret-Ablage |
| `Content Update`-Eintrag `appsettings.Local.json` im csproj | `CopyToPublishDirectory="Never"`, `CopyToOutputDirectory="Never"` — **`Update` statt `Include`** (Web-SDK-Default-Glob `**/*.json` erfasst die Datei bereits; `Include` erzeugt `NETSDK1022`, sobald die Datei existiert) | — | Garantiert, dass die lokale Datei nie ins Release-Paket gelangt |
| `.gitignore`-Eintrag `appsettings.Local.json` | Ignore-Regel | — | Lokale Datei gehört nicht ins Repository |

Konkrete Schutzliste (`AutoUpdate:ProtectedFiles` in `VideoWebPlayer/appsettings.json`):

```json
"ProtectedFiles": [
  {
    "Path": "web.config",
    "Strategy": "Merge",
    "XmlElements": [
      "//aspNetCore/environmentVariables",
      "//security/ipSecurity",
      "//system.webServer/httpProtocol"
    ],
    "XmlAttributes": [
      "//aspNetCore@requestTimeout",
      "//aspNetCore@startupTimeLimit",
      "//aspNetCore@shutdownTimeLimit",
      "//aspNetCore@stdoutLogEnabled",
      "//aspNetCore@stdoutLogFile",
      "//aspNetCore@forwardWindowsAuthToken",
      "//aspNetCore@disableStartUpFailedPage",
      "//aspNetCore@processesPerApplication"
    ]
  },
  {
    "Path": "appsettings*.json",
    "Strategy": "Merge",
    "JsonKeys": [
      "ConnectionStrings:DefaultConnection",
      "AllowedHosts",
      "Jwt:Key",
      "Jwt:Issuer",
      "Jwt:ApiToken",
      "Jwt:ApiToken:Web",
      "Jwt:ApiToken:Maui",
      "Kestrel:Endpoints:Http:Url",
      "Kestrel:Limits:MaxRequestBodySize",
      "Host:Address",
      "Host:Port",
      "Backups:Path",
      "Backups:AutomaticBackupsEnabled",
      "Backups:MaxUploadSizeBytes",
      "Backups:Retention:SonCount",
      "Backups:Retention:FatherCount",
      "Backups:Retention:GrandfatherCount",
      "AutoUpdate:Enabled",
      "AutoUpdate:EnableAutomaticDownload",
      "AutoUpdate:EnableAutomaticInstallation",
      "AutoUpdate:AllowPrereleaseUpdates",
      "AutoUpdate:ScheduledInstallTime",
      "AutoUpdate:StopHostAfterScriptStart",
      "AutoUpdate:DownloadPath",
      "AutoUpdate:ServiceName",
      "AutoUpdate:GitHubToken",
      "AutoUpdate:AppPoolName",
      "AutoUpdate:SiteName",
      "AutoUpdate:WorkspaceUser",
      "AutoUpdate:WorkspaceGroup",
      "AutoUpdate:ExecutablePath",
      "AutoUpdate:MaxAssetBytes",
      "AutoUpdate:SourceCheck:Interval",
      "AutoUpdate:SourceCheck:TimeRanges",
      "AutoUpdate:Backup:Enabled",
      "AutoUpdate:Backup:Path",
      "AutoUpdate:Backup:RetainedBackupCount",
      "AutoUpdate:Backup:CancelInstallationOnFailure",
      "Serilog:MinimumLevel:Default",
      "Logging:LogLevel:Default",
      "Pairing:CodeLength",
      "Pairing:CodeTtlMinutes",
      "Pairing:BootstrapTicketTtlMinutes",
      "Pairing:BootstrapMaxTicketsPerHour",
      "Pairing:BootstrapAdminOnly",
      "Auth:RefreshTokenTtlDays"
    ]
  }
]
```

Auswahlkriterium für `JsonKeys`: Schlüssel, die ein Deployment plausibly für seine Umgebung setzt oder setzen muss (Verbindungen, Endpunkte, Tokens, Dienst-/Poolnamen, Ablagepfade, Update-Verhalten, Loglevel, dokumentierte Admin-Tuning-Knöpfe). Nicht enthaltene Schlüssel fehlen in der Bestandsdatei typischerweise oder sollen paket-seitig weiterentwickelt werden. `AutoUpdate:ProtectedFiles` ist bewusst nicht enthalten (die Liste bleibt paket-eigen, damit künftige Releases Schutz nachliefern können); deployment-seitige Erweiterungen der Schutzliste sind über Umgebungsvariablen (`AutoUpdate__ProtectedFiles__{n}__Path` usw.) möglich und werden in `TECH_Auto_Update.md` dokumentiert.

## Seiteneffekte und Risiken

- **Migrationslücke (zentral):** Das Update *auf* diese Version wird vom Skript der alten Version installiert — der Schutz greift erst ab dem ersten Update *nach* dieser Version. Bestandsinstallationen müssen deployment-seitige `web.config`-/`appsettings`-Anpassungen einmalig nachtragen. Migrationshinweis ist fester Bestandteil von V5 (Doku + Release Notes, beide Sprachen).
- **`JsonKeys` friert aufgezählte Werte ein:** Der Merge übernimmt aufgezählte Pfade immer aus der Bestandsdatei — Paket-Änderungen an denselben Schlüsseln erreichen die Installation nicht mehr. Die Liste ist deshalb auf deployment-eigene Schlüssel beschränkt; das Verhalten wird in `TECH_Auto_Update.md` dokumentiert.
- **`ProtectedFiles` ist paket-eigen:** Deployment-seitige Ergänzungen der Schutzliste in `appsettings.json` überleben kein Update (bewusst — siehe Designentscheidung). Umgehung über Umgebungsvariablen ist möglich und wird dokumentiert.
- **Linux-`Merge` benötigt `python3`:** Fehlt es, wird der Merge mit Logeintrag übersprungen (Update läuft weiter, Backups liegen in `Updates/backup/`). Betrifft faktisch nur `appsettings*.json` (`web.config` ist unter Linux nicht wirksam). Wird als Betriebsvoraussetzung dokumentiert; ob Zielsysteme `python3` haben, ist ein offener Punkt.
- **XML-Merge hängt nur an existierende Elternelemente an:** Fehlt der Elternknoten in der Paketdatei, wird der Merge-Eintrag still übersprungen — deshalb `//system.webServer/httpProtocol` statt `//httpProtocol/customHeaders`. Analog gilt: `//security` muss in der Paket-`web.config` bestehen bleiben (aktuell via `requestFiltering` garantiert); würde es entfernt, ginge der `ipSecurity`-Schutz still verloren — neben dem Doku-Hinweis in `TECH_Auto_Update.md` zusätzlich per Guard-Test gegen die ausgelieferte `web.config` abgesichert (`ShippedWebConfig_ContainsAllMergeParents`), damit ein solcher Schutzverlust nicht unbemerkt durchgeht.
- **JSON-Merge schreibt Dateien neu:** Formatierung kann sich ändern; JSON-Kommentare führen zum Parse-Fehler → pro-Datei-Warnung, Merge für diese Datei übersprungen (Update läuft weiter). Die Repo-`appsettings*.json` sind kommentarfrei; deployment-seitig mit Kommentaren versehene Dateien werden dokumentiert (Kommentare vermeiden bzw. `appsettings.Local.json` nutzen).
- **`Updates/backup/` ohne Retention:** Sicherungen geschützter Dateien werden nicht aufgeräumt und wachsen mit jedem Update. Als manueller Restore-/Aufräumpfad dokumentiert (Doku V5).
- **Fail-fast bei ungültiger Schutzliste:** Eine fehlerhafte `AutoUpdate:ProtectedFiles`-Konfiguration lässt die Anwendung nicht starten (`OptionsValidationException`). Gewollt, aber dokumentationspflichtig; die ausgelieferte Liste wird per Test abgesichert.
- **`appsettings.Local.json` mit `reloadOnChange`:** Änderungen wirken ohne Neustart (Standardverhalten von `AddJsonFile`); implizit auch für die `AutoUpdate`-Bindung relevant nur bis zum Options-Snapshot beim Start — keine besondere Behandlung nötig.
- **Wildcard erfasst `appsettings.Local.json` deployment-seitig:** Der `appsettings*.json`-`ProtectedFiles`-Eintrag matcht auch eine nur-lokale `appsettings.Local.json` — sie wird bei jedem Update nach `Updates/backup/` gesichert (evtl. darin liegende Secrets landen damit auch im Backup-Verzeichnis) und vom JSON-Merge neu geschrieben (Reformatierung; JSON-Kommentare → Parse-Fehler → Warnung, Datei bleibt erhalten). Inhaltlich ein No-op; wird in `TECH_Auto_Update.md` und `SECRETS_MANAGEMENT.md` dokumentiert (Kommentare vermeiden).
- **Altes nupkg entfernen:** `lib/packages/msTools.Updater.0.10.4-rc.1.nupkg` wird gelöscht — Builds älterer Stände dieses Repos mit der alten Referenz schlagen danach fehl (nur branch-lokal relevant).
- **Lizenz:** `0.11.0-rc.1` ist MIT (nuspec verifiziert) — unverändert zu `0.10.4-rc.1`, kein Lizenzkonflikt.

## Umsetzungsreihenfolge

1. **Paket-Integration (V1)**
   - Voraussetzungen: `lib/packages/msTools.Updater.0.11.0-rc.1.nupkg` liegt vor (untracked); `NuGet.config` bindet `lib/packages` bereits als lokale Quelle ein.
   - Beschreibung: `PackageReference` in `VideoWebPlayer.csproj` (Z. 53) und `VideoWebPlayer.Tests.csproj` (Z. 22) auf `0.11.0-rc.1` erhöhen; `lib/packages/msTools.Updater.0.10.4-rc.1.nupkg` löschen; `msTools.Updater.0.11.0-rc.1.nupkg` committen. `dotnet restore` + `dotnet build VideoPlayer.sln` in Debug **und** Release (DoD: Release wird in der CI gebaut). CI-Kompatibilität: alle Workflows nutzen denselben lokalen Feed, keine versionsharten Verdrahtungen (verifiziert) — keine Workflow-Änderungen nötig.

2. **Schutzliste konfigurieren (V2)**
   - Voraussetzungen: Schritt 1 (die `AutoUpdateProtectedFile`-API existiert erst ab `0.11.0-rc.1`; eine `ProtectedFiles`-Sektion würde unter `0.10.4` still ignoriert und die neuen Schlüssel wären untestbar).
   - Beschreibung: `AutoUpdate:ProtectedFiles`-Array mit den zwei `Merge`-Einträgen (siehe „Konfigurationsänderungen") im `AutoUpdate`-Abschnitt von `VideoWebPlayer/appsettings.json` ergänzen. Keine Codeänderung in `AutoUpdateExtensions.cs`.

3. **`appsettings.Local.json` einführen (V3)**
   - Voraussetzungen: Keine — unabhängig von den anderen Schritten (die Quelle steht auch ohne `ProtectedFiles` für sich).
   - Beschreibung: `LocalConfigurationExtensions` mit `AddLocalJsonConfiguration` anlegen (JSON-Quelle `appsettings.Local.json`, `optional: true`, `reloadOnChange: true`, eingefügt direkt nach der letzten `JsonConfigurationSource` mit `appsettings*.json`-Dateiname in `builder.Configuration.Sources` — **nicht** letzte JSON-Quelle generell, sonst landete sie in Development hinter User Secrets; Fallback: vor erster `EnvironmentVariablesConfigurationSource`); Aufruf in `Program.cs` nach `WebApplication.CreateBuilder(args)`; `Content Update`-Eintrag `appsettings.Local.json` mit `CopyToPublishDirectory="Never"`/`CopyToOutputDirectory="Never"` im csproj (`Update` statt `Include` — sonst `NETSDK1022`-Duplikatfehler durch den Web-SDK-Default-Glob `**/*.json`); `.gitignore`-Eintrag.

4. **Tests ergänzen (V4)**
   - Voraussetzungen: Schritte 1–3 (die Tests prüfen die ausgelieferte Konfiguration und die neue Quelle).
   - Beschreibung: Neue Testklasse `AutoUpdateProtectedFilesTests` (Bindungstest per `Host.CreateApplicationBuilder` + `AddInMemoryCollection` + `UseAutoUpdate` → `AutoUpdateOptions.ProtectedFiles`; Validierungstest der echten `appsettings.json` gegen `AutoUpdateOptionsValidator`; Guard-Test `ShippedWebConfig_ContainsAllMergeParents` — echte `VideoWebPlayer/web.config` gegen die Eltern-/Elementpfade aller `XmlElements`/`XmlAttributes` der ausgelieferten Liste; Skript-Inhaltstest über `IAutoUpdateScriptGenerator` — Reihenfolge Backup → Kopieren → Merge/Restore, `Updates/backup/`-Pfad, serialisierte Einträge; ggf. `IAutoUpdatePlatformResolver`-Testdouble für ein Windows-Skript). Regression in `UpdateSettingsServiceTests`: `ApplyToRuntimeOptions` lässt `ProtectedFiles` unverändert. `LocalConfigurationTests` für die neue Quelle (Laden bei Vorhandensein, optional bei Fehlen, Positions-Guard gegenüber User Secrets/`EnvironmentVariables`/`CommandLine`, `Jwt__*`-Umgebungsvariable schlägt lokale Datei).

5. **Vollverifikation**
   - Voraussetzungen: Schritte 1–4.
   - Beschreibung: `dotnet build VideoPlayer.sln` in Debug und Release; volle Testsuite `dotnet test VideoWebPlayer.Tests/VideoWebPlayer.Tests.csproj` grün (DoD).

6. **Doku-Nachzug (V5)**
   - Voraussetzungen: Schritte 1–4 (die Doku beschreibt das tatsächliche Verhalten; die Schutzliste ist final).
   - Beschreibung: Alle 12 verifizierten Stellen aus `inventory/dokumentation.md` umstellen: `TECH_Auto_Update.md` (`ProtectedFiles`-Eintrag in der Konfigurationstabelle inkl. Vorrang Fluent > Bindung, Warnblock Z. 94–101 auf neues Verhalten, Backup-Absatz ~Z. 126, Hinweis auf Elternknoten-Abhängigkeit und `Updates/backup/` ohne Retention); `GUIDE_Installation.md` (~Z. 108: `environmentVariables` jetzt update-geschützt, Migrationshinweis); `SECRETS_MANAGEMENT.md` (~Z. 65: Empfehlung final — maschinenweite `Jwt__*` Primärvariante, geschützte `environmentVariables` gleichwertig); `docs/help/updates.md` (Z. 38, 71, 77); `docs/help/backups.md` (~Z. 61, ~96); `RELEASE_NOTES.md` (Z. 5 EN, Z. 74 DE: „Bekannte Einschränkung" → Fix-Eintrag plus Migrationshinweis und `python3`-Bedingung). Zusätzlich wird die neue Konfigurationsquelle `appsettings.Local.json` dokumentiert — ohne Doku ist sie für Admins nicht auffindbar: `SECRETS_MANAGEMENT.md` (~Z. 65 — als weitere update-sichere Ablage für deployment-spezifische Secrets und beliebige Overrides; Empfehlungshierarchie bleibt: maschinenweite `Jwt__*` primär), `GUIDE_Installation.md` („Produktive Konfiguration" ~Z. 88–108 — Dateiname, Ablageort im Content-Root, Zweck als Override-Auffang), `TECH_Auto_Update.md` (Vorrang relativ zu `appsettings*.json`/User Secrets/Umgebungsvariablen/Kommandozeile, `optional`/`reloadOnChange`, Paket-Ausschluss via `CopyToPublishDirectory="Never"` + `.gitignore`, sowie der Wildcard-Seiteneffekt: `appsettings.Local.json` wird vom `appsettings*.json`-`ProtectedFiles`-Eintrag erfasst → Backup nach `Updates/backup/` inkl. evtl. Secrets und Neuformatierung durch den JSON-Merge — Kommentare vermeiden), `docs/help/updates.md` (Hinweis auf `appsettings.Local.json` als update-sicheren Ort für Overrides außerhalb der `JsonKeys`-Liste). Danach `dotnet test tools/MarkdownLinkCheck.Tests` ausführen.
   - Optional (eigener Commit, außerhalb des Auftrags — Restaufgabe 11): veraltete Abschnitte in `TECH_Auto_Update.md` (Z. 8–20, 114–123: DLL-Referenz `lib/msTools.Updater/`, `main-release.yml`/`create-update-manifest.sh`) und `GUIDE_Installation.md` (Z. 16, 182–184) auf nupkg/`build-and-package`-Stand bringen; im Handover benennen.

## Tests

### Neue Tests

| Test / Hilfsmethode | Testklasse | Was wird geprüft / bereitgestellt? |
|--------------------|------------|-------------------------------------|
| `ConfigurationBindsProtectedFiles` | `AutoUpdateProtectedFilesTests` | `AddInMemoryCollection` mit `AutoUpdate:ProtectedFiles:*`-Einträgen → `UseAutoUpdate` → `GetRequiredService<AutoUpdateOptions>()` enthält die Einträge mit korrekten `Path`/`Strategy`/`XmlElements`/`XmlAttributes`/`JsonKeys` |
| `ShippedAppsettingsProtectedFiles_AreValid` | `AutoUpdateProtectedFilesTests` | Die echte `VideoWebPlayer/appsettings.json` (Repo-Pfad relativ zum Testprojekt) wird geladen, `AutoUpdate` gebunden und gegen `AutoUpdateOptionsValidator` validiert → `Succeeded` (sichert die ausgelieferte Schutzliste gegen Tippfehler/Formatfehler ab — sonst `OptionsValidationException` erst beim Kunden-Start) |
| `GeneratedScript_ContainsBackupAndMergeInOrder` | `AutoUpdateProtectedFilesTests` | `IAutoUpdateScriptGenerator` per DI mit gesetzten `ProtectedFiles` → `GenerateAsync` → Skripttext enthält die serialisierte Schutzliste, den Backup-Schritt **vor** dem Paketkopiervorgang und den Merge-/Restore-Schritt danach (Reihenfolge per `IndexOf`-Vergleich) sowie den `backup`-Ablagepfad. Ggf. `IAutoUpdatePlatformResolver`-Testdouble (TryAdd-Muster aus `MsToolsUpdaterIntegrationTests`), um das Windows-Skript plattformunabhängig zu prüfen |
| `ShippedWebConfig_ContainsAllMergeParents` | `AutoUpdateProtectedFilesTests` | Liest `AutoUpdate:ProtectedFiles` aus der echten `VideoWebPlayer/appsettings.json` (Repo-Pfad relativ zum Test-Output wie bei `ShippedAppsettingsProtectedFiles_AreValid`), parst die **echte** `VideoWebPlayer/web.config` (`XDocument` + `XPathSelectElements`, `System.Xml.XPath`) und prüft für jeden `XmlElements`-Eintrag, dass der Elternpfad (XPath ohne letztes `/`-Segment) mindestens ein Element trifft — `//aspNetCore`, `//security`, `//system.webServer` — sowie für jeden `XmlAttributes`-Eintrag, dass das Element vor `@` existiert (`//aspNetCore`). Schlägt fehl, sobald eine Repo-Änderung einen benötigten Elternknoten entfernt (Merge würde sonst lautlos ins Leere laufen). Zusätzlich prüft der Test die `XmlElements`-Pfade der ausgelieferten Liste auf ElementTree-Kompatibilität (kein `[`, kein `@`) — `System.Xml.XPath` würde ein künftiges Attributprädikat sonst nicht abfangen, unter Linux wäre der Eintrag still wirkungslos |
| `ApplyToRuntimeOptions_LeavesProtectedFilesUnchanged` | `UpdateSettingsServiceTests` (bestehende Klasse) | Regression: `AutoUpdateOptions` mit gefüllter `ProtectedFiles`-Liste → `ApplyToRuntimeOptions` → Liste unverändert (DB-Laufzeitmutation darf die Schutzliste nie überschreiben) |
| `LocalFile_IsInsertedBeforeUserSecretsAndEnvironmentVariables` | `LocalConfigurationTests` | Positions-Guard auf `builder.Configuration.Sources`: simulierte Development-Quellenfolge (User-Secrets-`JsonConfigurationSource` mit `Path` `secrets.json` direkt hinter `appsettings.{Env}.json` eingefügt, plus `EnvironmentVariables`-/`CommandLine`-Quelle) → nach `AddLocalJsonConfiguration` liegt der Index der lokalen Quelle **hinter** der letzten `appsettings*.json`-Quelle und **vor** User-Secrets-, `EnvironmentVariables`- und `CommandLine`-Quelle |
| `LocalFile_OverridesPackagedValues_WhenPresent` | `LocalConfigurationTests` | `appsettings.Local.json` mit Testschlüssel im Temp-Content-Root → Wert schlägt den `appsettings.json`-Wert |
| `LocalFile_Missing_IsIgnored` | `LocalConfigurationTests` | Fehlende `appsettings.Local.json` → Konfiguration lädt fehlerfrei, Werte kommen aus `appsettings.json` |
| `LocalFile_LosesAgainstUserSecrets` | `LocalConfigurationTests` | Development-Quellenfolge wie oben mit User-Secrets-JSON-Datei, die denselben Testschlüssel setzt wie `appsettings.Local.json` → der User-Secrets-Wert gewinnt (Verhaltensnachweis des Positions-Guards, Vorrangziel „vor User Secrets") |
| `LocalFile_LosesAgainstEnvironmentVariable` | `LocalConfigurationTests` | Prozessweite `Jwt__*`-Umgebungsvariable (`Environment.SetEnvironmentVariable("Jwt__Key", …)` vor Builder-Aufruf, `try/finally`-Cleanup) setzt denselben Schlüssel wie `appsettings.Local.json` → der Umgebungsvariablen-Wert gewinnt — sichert das Vorrangziel „Umgebungsvariablen bleiben stärkster Override" ab |
| Temp-Dir-Setup | beide neuen Testklassen | Muster aus `MsToolsUpdaterIntegrationTests` (`Path.GetTempPath()` + `Guid`, `IDisposable`-Cleanup) |

### Betroffene bestehende Tests

| Test / Testklasse | Grund der Anpassung |
|-------------------|---------------------|
| `UpdateSettingsServiceTests` | erhält einen zusätzlichen Regressionstest (`ApplyToRuntimeOptions_LeavesProtectedFilesUnchanged`) — keine bestehende Methode muss geändert werden |

Keine bestehenden Tests brechen erwartbar: Die Bibliotheks-API ist additiv (`AutoUpdateScriptGenerator`-ctor hat Default-Parameter `options = null`; `HasProtectedFiles` ist bei leerer Liste `false` → Skripte unverändert). `MsToolsUpdaterIntegrationTests` bleibt unberührt.

### E2E-Tests (primärer Funktionsnachweis)

Keine E2E-Tests erforderlich — **Begründung:** Die Anforderung berührt keinen über UI oder Nutzeraktion erreichbaren Ablauf. Der Dateierhalt läuft im vom Updater generierten Betriebssystem-Skript (`update.ps1`/`update.sh`), während der Anwendungshost gestoppt ist — ein Playwright-Test kann diesen Pfad prinzipiell nicht beobachten. Der primäre Funktionsnachweis ist der Skript-Inhaltstest (Reihenfolge und Serialisierung der Schutzliste) plus der Validierungstest der ausgelieferten Konfiguration; das Laufzeitverhalten des Skripts ist in der Bibliothek (`0.11.0-rc.1`, decompiliert verifiziert) umgesetzt und dort getestet. Die bestehenden `UpdatesPageE2ETests` bleiben unverändert gültig.

Angepasste bestehende E2E-Tests: **Keine.**

## Offene Punkte

Keine — der einzige verbliebene Punkt (`python3`-Verfügbarkeit auf Linux-Zielsystemen) wurde vom Anwender zugunsten des empfohlenen Vorschlags entschieden: `python3` wird als Betriebsvoraussetzung für den Linux-`Merge` in `TECH_Auto_Update.md`, `GUIDE_Installation.md` und den Release Notes dokumentiert, samt `Updates/backup/` als manuellem Restore-Pfad; kein `Preserve`-Fallback.
