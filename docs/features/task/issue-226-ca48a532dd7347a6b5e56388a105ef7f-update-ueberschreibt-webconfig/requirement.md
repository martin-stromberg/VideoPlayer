# Übersetzte Anforderung: Update überschreibt `web.config` — JWT-/Konfigurationseinträge gehen verloren

## Fachliche Zusammenfassung

Bei jedem Update über `msTools.Updater` entpackt das generierte Installationsskript (`Updates/pending/update.ps1` unter Windows) das Release-Archiv `release-win-x64.zip` über dem Anwendungsverzeichnis und ersetzt dabei die `web.config` der IIS-Site vollständig durch die generierte Version aus dem Paket. Deployment-spezifische `<environmentVariables>`-Einträge im `<aspNetCore>`-Element — insbesondere `Jwt:Key`, `Jwt:Issuer`, `Jwt:ApiToken:Web` und `Jwt:ApiToken:Maui` — gehen dabei verloren. In Produktion wirft `AddVideoWebPlayerServices` beim Start eine `InvalidOperationException` bei fehlendem `Jwt:Key`; der API-Token-Gate (`ApiTokenCheckAttribute`) lehnt alle Requests von MAUI-App und externen Web-Clients mit `401` ab. Gefordert ist, dass deployment-spezifische Konfiguration ein Programmupdate überlebt — ohne dass nach jedem Update manuell nachgetragen oder Secrets neu generiert werden müssen (ein neuer `Jwt:Key` invalidiert zudem alle bestehenden Sessions).

## Betroffene Klassen und Komponenten

- `VideoWebPlayer/web.config` — im Publish transformierte und ins Release-Zip ausgelieferte Datei (`requestFiltering`/`maxAllowedContentLength`, ANCM-Handler, `hostingModel="outofprocess"`, `requestTimeout`). Zentrales betroffenes Artefakt.
- `.github/actions/build-and-package/action.yml` — Publish- und Zip-Schritte für `release-win-x64.zip`/`release-linux-x64.zip`; relevanter Eingriffspunkt für einen Paket-Ausschluss oder Template-Auslieferung von `web.config`.
- `msTools.Updater` (externe Bibliothek, `lib/packages/msTools.Updater.0.10.4-rc.1.nupkg`): `AutoUpdateScriptGenerator`/`IAutoUpdateScriptGenerator` erzeugt das Installationsskript, das das Paket entpackt und Dateien ersetzt. Skriptseitige Sicherung/Wiederherstellung bzw. Merge des `environmentVariables`-Blocks liegt vollständig in der Bibliothek (Präzedenz: `docs/Anforderung_msTools_Updater_Installationsskript.md`, Abschnitt „Nicht durch VideoWebPlayer lösbar"). Alternativ wäre eine eigene `IAutoUpdateScriptGenerator`-Implementierung im VideoWebPlayer denkbar, sofern die DI-Registrierung überschrieben werden kann (zu prüfen, Annahme).
- `VideoWebPlayer/Program.cs` bzw. `VideoWebPlayer/Extensions/ServiceCollectionExtensions.cs` (`AddVideoWebPlayerServices`, Zeilen 48–59) — Konfigurationspipeline und Konsum der `Jwt:*`-Werte; Eingriffspunkt für eine zusätzliche optionale JSON-Konfigurationsquelle (z. B. `builder.Configuration.AddJsonFile`).
- `VideoWebPlayer/appsettings.json` / `appsettings.Production.json` — werden ebenfalls im Paket ausgeliefert und beim Update überschrieben; für deployment-spezifische Secrets strukturell ungeeignet.
- `VideoWebPlayer/VideoWebPlayer.csproj` — Publish-Metadaten; relevant, damit eine neue lokale Konfigurationsdatei nicht in den Publish-Output und damit nicht ins Release-Paket gelangt.
- `VideoWebPlayer/Controllers/Attributes/ApiTokenCheckAttribute.cs` — betroffener Laufzeitmechanismus, selbst kein Eingriffspunkt.
- Dokumentation: `docs/GUIDE_Installation.md`, `docs/SECRETS_MANAGEMENT.md`, `docs/TECH_Auto_Update.md`, `docs/help/backups.md` (IIS-/`web.config`-Abschnitt), `docs/help/updates.md`, `README.md`, `docs/RELEASE_NOTES.md`.
- Tests: `VideoWebPlayer.Tests` — je nach Ansatz Tests für die neue Konfigurationsquelle (optionale lokale Datei wird geladen; fehlt sie, läuft der Start ohne Fehler) und/oder für den Paket-Ausschluss.

## Implementierungsansatz

Die Anforderung nennt vier mögliche Ansätze; die endgültige Richtung ist zu entscheiden (siehe Offene Fragen). Technische Einordnung:

1. **Update-Skript sichert/restauriert `web.config` oder mergt den `environmentVariables`-Block** (vom Kunden als direktester Fix genannt): Erfordert eine Änderung an `AutoUpdateScriptGenerator` in `msTools.Updater` (separates Repository, neue nupkg-Version unter `lib/packages/`) oder eine eigene `IAutoUpdateScriptGenerator`-Implementierung im VideoWebPlayer, die das Basisskript um Backup/Merge-Schritte ergänzt. Ein Merge muss XML-basiert erfolgen, damit gleichzeitig mitgelieferte `web.config`-Änderungen der Anwendung (z. B. Handler- oder `requestFiltering`-Anpassungen) nicht verloren gehen.
2. **`web.config` nicht im Release-Paket ausliefern**: Änderung an `build-and-package/action.yml` (Ausschluss beim `zip`-Schritt oder Umbenennung in eine Template-Datei, z. B. `web.config.template`). Konsequenz: Erstinstallationen benötigen weiterhin eine `web.config` für ANCM — Installationsdoku und ggf. Template-Handling anpassen. Zudem werden künftige legitime `web.config`-Verbesserungen (z. B. das `requestFiltering`-Limit) nicht mehr per Update verteilt.
3. **Secrets aus `web.config` herauslösen** (strukturelle Lösung): Zusätzliche optionale Konfigurationsdatei im Content-Root (z. B. `secrets.json` oder `appsettings.Local.json`) per `AddJsonFile(..., optional: true)` einbinden; die Datei ist nie im Paket und überlebt jedes Update. In `VideoWebPlayer.csproj` sicherstellen, dass sie nicht in den Publish-Output kopiert wird (`CopyToPublishDirectory`/`Content`-Metadaten). Löst den Secret-Verlust unabhängig vom Updater-Verhalten; andere deployment-seitige `web.config`-Anpassungen (z. B. IIS-spezifische Einstellungen) bleiben jedoch weiterhin überschreibbar.
4. **Dokumentierter Workaround**: Maschinenweite Umgebungsvariablen (`Jwt__Key` usw.) als empfohlene Produktionsvariante in `GUIDE_Installation.md`/`SECRETS_MANAGEMENT.md` festlegen — dies ist faktisch bereits der dokumentierte Weg; reine Doku-Änderung ohne Code-Eingriff, lässt den `web.config`-Verlust anderer Anpassungen aber bestehen.

Kombinationen sind möglich und vom Kunden nicht ausgeschlossen (z. B. Ansatz 3 als Primärlösung plus Ansatz 4 als dokumentierter alternativer Weg, oder Ansatz 1 zum Schutz aller `web.config`-Anpassungen).

Kein neuer UI-Ablauf und keine Datensatz-Auswahl/-Identifikation betroffen; der Eingriff liegt im Installations-/Konfigurationspfad.

## Konfiguration

- Kein neuer konfigurierbarer Schalter zwingend erforderlich.
- Bei Ansatz 3: neue optionale Datei im Content-Root (Arbeitsvorschlag `appsettings.Local.json` oder `secrets.json`); die Einbindungsreihenfolge relativ zu Umgebungsvariablen ist festzulegen und zu dokumentieren (Vorschlag: nach den standardmäßigen `appsettings`-Quellen, aber so, dass maschinenweite `Jwt__*`-Umgebungsvariablen weiterhin Vorrang haben — Annahme, zu bestätigen).
- Bei Ansatz 1/2: ggf. Dokumentation einer Liste updategeschützter Dateien; eine neue `AutoUpdate`-Option käme nur infrage, falls die Bibliothek um einen konfigurierbaren Dateierhalt erweitert wird.
- In jedem Fall: `SECRETS_MANAGEMENT.md` und `GUIDE_Installation.md` müssen die als empfohlen geltende Produktionsvariante eindeutig benennen; `docs/help/backups.md` enthält einen `web.config`-Abschnitt, der auf den geänderten Update-Umgang abzustimmen ist.

## Offene Fragen

1. Welche Lösungsrichtung ist gewünscht — Update-seitige Sicherung/Merge der `web.config`, Ausschluss aus dem Release-Paket, Herauslösen der Secrets in eine nicht ausgelieferte Datei, dokumentierter Workaround oder eine Kombination?
2. Dürfen im Rahmen dieser Anforderung Änderungen an `msTools.Updater` erfolgen (separates Repository `martin-stromberg/msTools.Updater`; Ergebnis wäre eine neue nupkg unter `lib/packages/`), oder muss die Lösung vollständig im VideoWebPlayer-Repository liegen? Falls Letzteres: Ist das Überschreiben der `IAutoUpdateScriptGenerator`-Registrierung aus `UseAutoUpdate` durch die Anwendung vorgesehen/möglich (zu verifizieren)?
3. Soll beim Skript-/Merge-Ansatz nur der `environmentVariables`-Block erhalten bleiben oder die gesamte deployment-seitige `web.config` (z. B. zusätzliche IIS-Einstellungen wie IP-Einschränkungen)? Vollständiger Dateierhalt verhindert, dass künftige `web.config`-Änderungen der Anwendung per Update ausgeliefert werden.
4. Sind außer `web.config` weitere deployment-seitig angepasste Dateien betroffen, die ebenfalls geschützt werden müssen (z. B. `appsettings.Production.json`, das ebenfalls überschrieben wird)?
5. Soll beim strukturellen Ansatz eine Migrationshilfe für bestehende Installationen vorgesehen werden (z. B. dokumentiertes Umziehen der `environmentVariables`-Werte in die neue lokale Datei), damit bereits betroffene IIS-Sites ohne manuelle Fehlersuche umstellen können?
