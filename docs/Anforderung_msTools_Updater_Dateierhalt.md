# Anforderung: Erhalt deployment-seitiger Dateien und Konfigurationsinhalte bei Programmupdates (msTools.Updater)

## Ziel

Von `msTools.Updater` installierte Programmupdates müssen deployment-seitig angepasste Dateien und Konfigurationsinhalte erhalten, statt sie vollständig durch die Paketversion zu ersetzen. Gleichzeitig müssen anwendungsseitige Verbesserungen derselben Dateien weiterhin per Update ausgeliefert werden können. Die Anwendung legt über eine konfigurierbare Liste fest, welche Dateien mit welcher Strategie geschützt werden; die Bibliothek führt den Schutz im generierten Installationsskript aus.

## Festgestellte Symptome

Das von `msTools.Updater` (beobachtet bei Version `0.10.4-rc.1`) generierte Installationsskript (`Updates/pending/update.ps1` unter Windows, `update.sh` unter Linux) entpackt das Release-Paket in ein Staging-Verzeichnis und kopiert anschließend alle Dateien per `Copy-Item -Recurse -Force` bzw. `cp -fr` ohne Ausschlussliste, ohne Backup und ohne Merge über das Anwendungsverzeichnis. Konkret beobachtete Folgen beim Update einer IIS-Installation:

- Die deployment-seitig angepasste `web.config` wird vollständig durch die Paketversion ersetzt. Das dort gepflegte `<environmentVariables>`-Element mit `Jwt:Key`, `Jwt:Issuer` und `Jwt:ApiToken:*` geht verloren.
- Die Anwendung startet daraufhin nicht mehr: Fehlende Pflicht-Konfiguration (`Jwt:Key`, `Jwt:ApiToken:*`) führt beim Start zu einer `InvalidOperationException`; API-Clients (Web-Clients, App) werden mit `401` abgelehnt.
- Muss der `Jwt:Key` neu erzeugt werden, werden zusätzlich alle bestehenden Benutzersitzungen invalidiert.
- Weitere deployment-seitige `web.config`-Anpassungen gehen ebenso verloren: `security/ipSecurity` (IP-Einschränkungen), `httpProtocol/customHeaders` und geänderte Attribute des `aspNetCore`-Elements (z. B. `requestTimeout`).
- Anpassungen in den im Paket enthaltenen `appsettings*.json`-Dateien gehen ebenfalls verloren; darin gepflegte Overrides (z. B. `AutoUpdate:AppPoolName`, Pfad- oder Verbindungsanpassungen) werden ersetzt. Deployment-seitig zusätzlich angelegte `appsettings*.json`-Dateien, die nicht im Paket enthalten sind (z. B. eine lokal erstellte `appsettings.Local.json`), bleiben dagegen erhalten.

## Reproduktion

1. Eine ASP.NET-Core-Anwendung mit `msTools.Updater`-Auto-Update als IIS-Site bereitstellen.
2. In der `web.config` der Site das `<environmentVariables>`-Element ergänzen (deployment-seitige Konfiguration, die nicht im Release-Paket enthalten ist) und bei Bedarf weitere Elemente wie `security/ipSecurity` anpassen.
3. Ein Programmupdate über `msTools.Updater` installieren (Windows-Variante für IIS-App-Pools des generierten Skripts).
4. Nach der Installation enthält die `web.config` exakt die Paketversion; die deployment-seitigen Elemente sind entfernt. Die Anwendung startet nicht bzw. hat ihre Konfiguration verloren.

Dasselbe Verhalten gilt für die Windows-Variante für Dienste/Executables und das Linux-`update.sh`, weil alle Skripte den Paketinhalt ohne Ausschluss und ohne Sicherung über das Anwendungsverzeichnis kopieren.

## Erwartetes Verhalten

- Über eine konfigurierbare Liste geschützter Dateien legt die Anwendung fest, welche deployment-seitigen Inhalte ein Update überleben.
- Geschützte Inhalte bleiben nach der Installation erhalten; Dateien ohne Schutzeintrag verhalten sich exakt wie bisher.
- Für strukturierte Dateiformate (XML wie `web.config`, JSON wie `appsettings*.json`) werden nur die konfigurierten Teilbereiche aus der Bestandsdatei übernommen — die Paketdatei bleibt maßgeblich, sodass anwendungsseitige Neuerungen in denselben Dateien weiterhin per Update ankommen.
- Ohne konfigurierte Schutzliste verhält sich die Installation exakt wie bisher (vollständiges Überschreiben).

## Notwendige Änderungen an msTools.Updater

### D1. Konfigurierbare Liste geschützter Dateien

`AutoUpdateOptions` wird um eine Liste geschützter Pfade erweitert. Je Eintrag:

- Pfad relativ zum Anwendungsverzeichnis,
- Strategie `Preserve` (Bestandsdatei vollständig erhalten) oder `Merge` (konfigurierte Inhalte aus der Bestandsdatei übernehmen),
- bei `Merge` die formatabhängigen Merge-Regeln (XML-Element-Pfade/-Attribute bzw. JSON-Schlüsselpfade, siehe D3 und D4).

Die Liste muss sowohl per Fluent-API (`AutoUpdateBuilder`) als auch per Konfigurationsbindung (`AutoUpdate:*`-Abschnitt) setzbar sein und gilt für beliebige Dateien — nicht nur `web.config`. Die konkrete API-Form (z. B. `PreserveFiles`, `ProtectedFiles`, Merge-Optionsobjekte) ist als Vorschlag zu verstehen; die endgültige Gestalt liegt beim Bibliotheksbetreuer.

### D2. Backup geschützter Dateien vor dem Überschreiben

Das generierte Skript sichert jede vorhandene geschützte Datei, bevor der Paketinhalt ins Anwendungsverzeichnis kopiert wird. Ablage der Sicherung im Updater-Workspace (z. B. unterhalb von `Updates/`), damit das Anwendungsverzeichnis nicht vermüllt und die Sicherung zur manuellen Wiederherstellung verfügbar bleibt.

### D3. XML-Merge

Für XML-Dateien (z. B. `web.config`) kann die Anwendung konfigurieren:

- eine Liste von Element-Pfaden/XPath-Ausdrücken, deren Elemente aus der Bestandsdatei in die neue Datei übernommen werden (z. B. `//aspNetCore/environmentVariables`, `//security/ipSecurity`, `//httpProtocol/customHeaders`),
- eine Liste von Attribut-Übernahmen aus der Bestandsdatei (z. B. `aspNetCore@requestTimeout`), ohne dass das gesamte Element eingefroren wird.

Die Paketdatei bleibt maßgeblich: Neue oder geänderte Elemente und Attribute der Paketversion kommen an; nur die konfigurierten Pfade und Attribute werden aus der Bestandsdatei übernommen. Fehlt die Bestandsdatei oder ein konfigurierter Pfad darin, gilt die Paketdatei unverändert.

### D4. JSON-Merge

Für JSON-Dateien (z. B. `appsettings*.json` und andere JSON-Dateien) kann die Anwendung eine Liste von JSON-Schlüsselpfaden konfigurieren (z. B. `Jwt:Key`, `ConnectionStrings:DefaultConnection`), deren Werte aus der Bestandsdatei in die neue Datei übernommen werden. Die Paketdatei bleibt maßgeblich: Neue Schlüssel der Paketversion kommen an; nur die konfigurierten Pfade werden aus der Bestandsdatei übernommen.

### D5. Preserve-Strategie

Für Dateien, die nie anwendungsseitig aktualisiert werden, kopiert das Skript die gesicherte Bestandsdatei nach dem Kopieren des Paketinhalts zurück über die neue Datei.

### D6. Reihenfolge-Garantie

Das Skript führt die Schritte strikt in dieser Reihenfolge zwischen dem Entpacken des Pakets und dem Neustart aus: **Backup der geschützten Dateien → Kopieren des Paketinhalts → Merge/Restore der geschützten Inhalte**. Der Merge-/Restore-Schritt läuft vor dem Entfernen von Lock/Descriptor und vor dem Start von Dienst, App-Pool oder Executable.

### D7. Fehlertoleranz

Fehler im Backup- oder Merge-Schritt führen nicht zum Skriptabbruch. Die `.ps1`-Skripte laufen mit `$ErrorActionPreference = "Stop"`, `update.sh` mit `set -euo pipefail` — ein nicht abgefangener Fehler würde die Anwendung gestoppt zurücklassen. Stattdessen gilt: Warnung ausgeben (`Write-Warning` bzw. in `update.log`, wo vorhanden), das Backup zur manuellen Wiederherstellung liegen lassen und den Neustart trotzdem ausführen.

### D8. Abwärtskompatibilität

Eine leere oder fehlende Schutzliste bedeutet exakt das bisherige Verhalten. Keine Breaking Changes an öffentlichen APIs, keine neuen Pflichtoptionen.

### D9. Plattformabdeckung

Der Mechanismus gilt für alle generierten Skriptvarianten: Windows IIS-App-Pool, Windows Dienst/Executable und Linux `.sh`.

## Anwendungsbeispiel: VideoWebPlayer (Empfehlung)

Die folgende Schutzliste dient als Referenzkonfiguration — sie verdeutlicht die Anforderungen, legt sie aber nicht fest:

- `web.config` mit `Merge`: Element-Pfade `//aspNetCore/environmentVariables` (deployment-seitige `Jwt:*`- und `AutoUpdate__*`-Werte), `//security/ipSecurity` (IP-Einschränkungen), `//httpProtocol/customHeaders`; Attribut-Übernahmen am `aspNetCore`-Element (z. B. `requestTimeout`). Die Paketdatei bleibt maßgeblich, damit anwendungsseitige `web.config`-Verbesserungen (z. B. das per Update verteilte `requestFiltering`-Limit) weiterhin ankommen.
- `appsettings*.json` mit `Merge` für deployment-seitig angepasste Schlüssel (z. B. `AutoUpdate:AppPoolName`, Pfad- oder Verbindungs-Overrides) — bzw. `Preserve`, falls eine solche Datei deployment-seitig vollständig geführt wird und keine Paket-Aktualisierung erhalten soll.

## Nicht sinnvoll durch VideoWebPlayer lösbar

- Die Erzeugung des Installationsskripts liegt vollständig in `msTools.Updater` (`AutoUpdateScriptGenerator`); VideoWebPlayer kann den Ablauf der Dateiinstallation nicht selbst beeinflussen.
- Eine anwendungsseitige `IAutoUpdateScriptGenerator`-Implementierung könnte den generierten Skripttext nur als Zeichenkette nachträglich patchen — fragil, dupliziert Plattformwissen der Bibliothek und droht bei jedem Bibliotheks-Upgrade still zu brechen — oder müsste die Skripterzeugung komplett nachbilden. Der Mechanismus gehört in den Generator selbst.

## Akzeptanzkriterien

1. Konfigurierte `web.config`-Elemente (z. B. `environmentVariables`, `ipSecurity`, `customHeaders`) und -Attribute überleben eine Update-Installation unverändert.
2. Konfigurierte `appsettings*.json`-Schlüssel überleben die Installation; neue Schlüssel der Paketversion kommen an.
3. `Preserve`-Dateien bleiben unverändert erhalten.
4. Dateien ohne Schutzeintrag werden wie bisher durch die Paketversion ersetzt.
5. Anwendungsseitige Neuerungen in gemergten Dateien (neue Elemente, Attribute oder Schlüssel der Paketversion) kommen an.
6. Ein Fehler beim Backup- oder Merge-Schritt führt nicht zu einer gestoppten Anwendung; eine Warnung wird ausgegeben, und das Backup liegt zur manuellen Wiederherstellung vor.
7. Leere oder fehlende Schutzliste bedeutet das bisherige Verhalten.
8. Das Verhalten gilt auf Windows in beiden Skriptvarianten (IIS-App-Pool und Dienst/Executable) und auf Linux.

## Betroffene msTools.Updater-Komponenten

- `AutoUpdateScriptGenerator` / `IAutoUpdateScriptGenerator` (Skripterzeugung: Backup-, Merge- und Restore-Schritte)
- `AutoUpdateOptions` (neue Option für die Schutzliste)
- `AutoUpdateBuilder` / `BindConfiguration` (Fluent-API und Konfigurationsbindung)
- `IAutoUpdatePackageStore` / `FileSystemAutoUpdatePackageStore` (Ablageort der Sicherungen im Updater-Workspace)
- Alle drei Skript-Templates: `.ps1` IIS-App-Pool, `.ps1` Dienst/Executable, `.sh`

## Aufgaben im VideoWebPlayer nach Umsetzung

- Neue `msTools.Updater`-nupkg unter `lib/packages/` ablegen und die `PackageReference` in `VideoWebPlayer.csproj` erhöhen; Lizenz, Release-Build (Debug und Release) und CI-Skripte prüfen.
- Die Schutzliste für VideoWebPlayer-Deployments konfigurieren (konkrete `web.config`- und `appsettings*.json`-Merge-Regeln bzw. `Preserve`-Einträge gemäß dem Anwendungsbeispiel oben).
- Dokumentation und Release Notes aktualisieren: die derzeitigen Warnhinweise („wird beim Update überschrieben") auf das neue Verhalten umstellen und einen Migrationshinweis für Bestandsinstallationen geben.
- Tests für die Schutzlisten-Konfiguration und ggf. einen Ausführungstest des generierten Skripts ergänzen.

## Referenzen

- `VideoWebPlayer/web.config` — wird im Release-Paket ausgeliefert (enthält `requestFiltering`-Limit und `aspNetCore`-Attribute); deployment-seitige Ergänzungen wie `<environmentVariables>` gehen beim Update verloren.
- `docs/Anforderung_msTools_Updater_Installationsskript.md` — Präzedenzfall: Anforderung an dieselbe Bibliothek (Diagnosefähigkeit des Installationsskripts).
- VideoWebPlayer-Issue #226 — „Update überschreibt `web.config` — JWT-/Konfigurationseinträge gehen verloren".
- Betroffene `AutoUpdateOptions`-Schlüssel in diesem Kontext: `AppPoolName`, `SiteName`, `DownloadPath` (Workspace-Pfade `Updates/pending`, `Updates/staging`, `Updates/update.log`).
- Ziel-Repository: `martin-stromberg/msTools.Updater` — dort ist diese Anforderung als Issue abgelegt: [martin-stromberg/msTools.Updater#66](https://github.com/martin-stromberg/msTools.Updater/issues/66).
