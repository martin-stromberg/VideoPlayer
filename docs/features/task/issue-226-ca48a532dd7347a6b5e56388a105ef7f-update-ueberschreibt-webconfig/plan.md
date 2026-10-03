# Umsetzungsplan: Update überschreibt `web.config` — JWT-/Konfigurationseinträge gehen verloren

## Übersicht

Das von `msTools.Updater` generierte Installationsskript (`Updates/pending/update.ps1` bzw. `update.sh`) kopiert das Release-Paket per `Copy-Item -Recurse -Force` / `cp -fr` ohne Ausschlüsse, Backup oder Merge über das Anwendungsverzeichnis und ersetzt dabei die deployment-seitig angepasste `web.config` — samt `<environmentVariables>` (`Jwt:Key`, `Jwt:ApiToken:*`), aber auch weiteren IIS-Anpassungen (`security/ipSecurity`, `httpProtocol/customHeaders`, geänderte `aspNetCore`-Attribute wie `requestTimeout`) — sowie deployment-seitig angepasste `appsettings*.json`.

**Strategie (Anwender-Entscheidung):** Dieser Auftrag implementiert den Dateierhalt **nicht** anwendungsseitig (der früher geplante `WebConfigPreservingScriptGenerator`-Ansatz entfällt). Kernlieferung ist stattdessen das Anforderungsdokument `docs/Anforderung_msTools_Updater_Dateierhalt.md` an den Betreuer von `msTools.Updater` (Präzedenzfall: `docs/Anforderung_msTools_Updater_Installationsskript.md`), damit die Bibliothek den Dateierhalt-/Merge-Mechanismus selbst trägt — konfigurierbar für beliebige Dateien (`Preserve`) und für strukturierte Formate (`Merge` für XML wie `web.config` und JSON wie `appsettings*.json`). Das Dokument wird zusätzlich als deutschsprachiges Issue im `msTools.Updater`-Repository (`martin-stromberg/msTools.Updater`) angelegt. Ergänzend werden sofort Dokumentations-Klarstellungen umgesetzt (update-gefährdete `web.config`-Anpassungen, maschinenweite Umgebungsvariablen als update-sichere Übergangslösung, `AutoUpdate:AppPoolName`-Doku). Alle Code-Änderungen (nupkg-Upgrade, Konfiguration der Schutzliste, ggf. `appsettings.Local.json`) sind explizit auf „nach der Bibliotheksänderung" vertagt.

## Designentscheidungen

| Komponente / Bereich | Gewählter Ansatz | Begründung |
|----------------------|-----------------|------------|
| Lösungsort des Dateierhalts | Anforderung an `msTools.Updater`; kein In-Repo-Fix | Anwender-Entscheidung: „dann wird es direkt richtig gemacht" — die Bibliothek trägt den Mechanismus (z. B. `PreserveFiles`/Merge-Optionen in `AutoUpdateOptions` oder im generierten Skript), VideoWebPlayer konfiguriert ihn nur noch. Der In-Repo-`IAutoUpdateScriptGenerator`-Override wäre technisch möglich (verifiziert: `TryAddSingleton` in `UseAutoUpdate`, `AutoUpdateScriptGenerator` ist `sealed`, aber komponierbar), wäre aber ein fragiler Text-Patch des generierten Skripts, der Plattformwissen der Bibliothek dupliziert und bei jedem Bibliotheks-Upgrade still zu brechen droht |
| Schutzumfang | Konfigurierbare Liste geschützter Pfade/Dateien mit `Preserve`-/`Merge`-Strategie — **nicht** auf `environmentVariables` oder `web.config` beschränkt | Anwender-Antworten: Es sollen weitere deployment-seitige `web.config`-Elemente geschützt werden (`security/ipSecurity`, `httpProtocol/customHeaders`, geänderte `aspNetCore`-Attribute) **und** zusätzlich `appsettings*.json`. Die Anforderung an die Bibliothek verlangt daher einen generischen Mechanismus: `Preserve` für beliebige Dateien, `Merge` für strukturierte Formate (XML wie `web.config`, JSON wie `appsettings*.json`) — nicht hartcodiert auf einen Block oder ein Format. Die konkrete VideoWebPlayer-Schutzliste wird im Dokument als Anwendungsbeispiel/Empfehlung benannt |
| Merge statt Vollschutz für `web.config`/`appsettings*.json` | Für strukturierte Dateien element-/attribut- bzw. schlüsselbezogener Merge (neue Datei bleibt maßgeblich, geschützte XML-Elemente/Attribute bzw. JSON-Pfade werden aus der Bestandsdatei übernommen) | Kompletter Dateierhalt würde verhindern, dass künftige anwendungsseitige Verbesserungen derselben Dateien per Update ankommen (das `requestFiltering`-Limit wurde selbst per Update verteilt; neue `appsettings`-Standardschlüssel sollen ebenfalls ankommen). Vollschutz (`Preserve`) bleibt als Strategie für Dateien sinnvoll, die nie anwendungsseitig aktualisiert werden |
| `appsettings.Local.json` jetzt einführen? | **Vertagt** — nach der Bibliotheksänderung neu bewerten | Entspricht dem Geist der Entscheidung („erst nur die Anforderung, dann direkt richtig"): Eine lokale Konfigurationsdatei löst nur die Secret-Teilmenge — der jetzt verbindlich erweiterte Schutzumfang (`ipSecurity`, `customHeaders`, Attribute) bleibt davon unberührt und wird ohnehin nur durch den Bibliotheks-Mechanismus abgedeckt. Würde man sie jetzt als empfohlene Secret-Ablage dokumentieren und nach der Integration wieder relativieren (geschützte `environmentVariables` sind dann gleichwertig und näher an den Bestandsinstallationen), entstünde Doku- und Empfehlungs-Doppelpflege. Als update-sichere Übergangslösung reicht die bereits dokumentierte Variante maschinenweiter `Jwt__*`-Umgebungsvariablen. Ob die Datei nach der Integration noch Mehrwert hat (v. a. für `appsettings`-Overrides, die nicht gemerged werden sollen), wird im vertagten Teil neu bewertet (siehe Abschnitt „Vertagt auf nach der msTools.Updater-Änderung") |
| Sofortmaßnahmen im Repo | Nur Dokumentations-Klarstellungen | Keine Code-Änderung ist nötig, die der Anwender-Entscheidung vorgreift; die dokumentierte Umgebungsvariablen-Variante schließt die Lücke bis zur neuen Bibliotheksversion |
| `AutoUpdate:AppPoolName` | Nur dokumentieren (Konfigurationsschlüssel), keine UI-/DB-Erweiterung | Anwender-Antwort: außerhalb dieses Auftrags. Für IIS-Deployments ist `AutoUpdate:AppPoolName` der einzige Weg zur IIS-Skriptvariante, da der Dienst-Probe keine App-Pools erkennt — diese Lücke wird in `TECH_Auto_Update.md`/`help/updates.md` geschlossen |
| Name und Form des Anforderungsdokuments | `docs/Anforderung_msTools_Updater_Dateierhalt.md`, Struktur analog zum Präzedenzfall | Konsistente Form für denselben Adressaten (Betreuer von `martin-stromberg/msTools.Updater`); bewährtes Format mit Ziel, Symptomen, nummerierten Anforderungen, Akzeptanzkriterien, betroffenen Komponenten |
| Übergabe des Anforderungsdokuments | Repo-Dokument unter `docs/` **plus** deutschsprachiges Issue in `martin-stromberg/msTools.Updater` via `gh issue create` | Anwender-Entscheidung: Das Repo-Dokument bleibt bestehen (Präzedenzfall identisch), zusätzlich wird ein Issue im Updater-Repository angelegt, damit die Anforderung dort verfolgbar ist. In der Planung verifiziert: `gh` 2.92.0 installiert, als `martin-stromberg` authentifiziert, `gh repo view martin-stromberg/msTools.Updater` erfolgreich. Fallback bei Nichterreichbarkeit: manueller Hinweis an den Anwender (siehe Seiteneffekte/Risiken) |

## Programmabläufe

Dieser Auftrag führt **keine** neuen Programmabläufe im Code ein — die Lieferung ist ein Anforderungsdokument plus Dokumentations-Klarstellungen. Die folgenden Abschnitte beschreiben den fachlichen Zielablauf, den das Anforderungsdokument an `msTools.Updater` spezifiziert, und den Übergabe-/Fortsetzungsablauf.

### Zielablauf: Update-Installation mit Dateierhalt (Inhalt der Anforderung)

1. `AutoUpdateInstaller` löst `IAutoUpdateScriptGenerator` auf und erzeugt das Installationsskript — jetzt unter Berücksichtigung einer konfigurierten Liste geschützter Dateien/Merge-Regeln (aus `AutoUpdateOptions` bzw. Konfigurationsbindung).
2. Das generierte Skript führt in dieser Reihenfolge aus: Dienst/App-Pool stoppen → Paket nach Staging entpacken → **Backup der vorhandenen geschützten Dateien** → Dateien ins Anwendungsverzeichnis kopieren → **Merge/Restore der geschützten Inhalte** (bei `Merge`: konfigurierte XML-Elemente/Attribute bzw. JSON-Pfade aus der Bestandsdatei in die neue übernehmen; bei `Preserve`: Bestandsdatei über die neue kopieren) → Lock/Descriptor entfernen → Dienst/App-Pool/Executable starten.
3. Fehler im Backup-/Merge-Schritt führen **nicht** zum Skriptabbruch (`$ErrorActionPreference = "Stop"` würde sonst die Anwendung gestoppt lassen): Warnung ausgeben, Backup zur manuellen Wiederherstellung liegen lassen, Neustart trotzdem ausführen.
4. Gilt für beide Windows-Skriptvarianten (IIS-App-Pool und Dienst/Executable) sowie das Linux-`.sh`-Skript.
5. Leere/fehlende Konfiguration → exakt das bisherige Verhalten (Abwärtskompatibilität).

Beteiligte Komponenten (Bibliothek): `AutoUpdateScriptGenerator`, `AutoUpdateOptions`, `AutoUpdateBuilder`/`BindConfiguration`, `FileSystemAutoUpdatePackageStore`, Skript-Templates (`.ps1` App-Pool, `.ps1` Dienst/Executable, `.sh`).

### Übergabe und Fortsetzung

1. `docs/Anforderung_msTools_Updater_Dateierhalt.md` wird erstellt und im VideoWebPlayer-Repo unter `docs/` abgelegt (Ablageort analog zum Präzedenzfall).
2. Zusätzlich wird ein deutschsprachiges Issue im `msTools.Updater`-Repository (`martin-stromberg/msTools.Updater`) via `gh issue create` angelegt — Inhalt: das Anforderungsdokument bzw. eine kompakte Issue-Fassung mit Verweis auf das Repo-Dokument.
3. Umsetzung in `msTools.Updater` → neue nupkg-Version.
4. Fortsetzung im VideoWebPlayer-Repo: siehe Abschnitt „Vertagt auf nach der msTools.Updater-Änderung".

## Inhalt des Anforderungsdokuments `docs/Anforderung_msTools_Updater_Dateierhalt.md`

Struktur und Nummerierung analog zum Präzedenzfall (`docs/Anforderung_msTools_Updater_Installationsskript.md`). Das Dokument muss fachlich vollständig und ohne Repo-Interna lesbar sein:

| Abschnitt | Inhalt |
|-----------|--------|
| `## Ziel` | Deployment-seitige Dateien und Konfigurationsinhalte müssen ein Programmupdate überleben; gleichzeitig müssen anwendungsseitige Verbesserungen derselben Dateien weiterhin per Update ausgeliefert werden können |
| `## Festgestellte Symptome` | Konkretes Fehlerbild: `web.config` wird vollständig ersetzt; `Jwt:Key`/`Jwt:ApiToken:*` aus `<environmentVariables>` gehen verloren → `InvalidOperationException` beim Start (Produktions-Pflichtprüfung), `401` für MAUI-/Web-Clients, bei Neugenerierung des `Jwt:Key` alle Sessions invalidiert. Weitere Verluste: `security/ipSecurity`, `httpProtocol/customHeaders`, geänderte `aspNetCore`-Attribute, deployment-seitige `appsettings*.json`-Anpassungen |
| `## Reproduktion` | IIS-Site mit `<environmentVariables>` in der `web.config` deployen → Update über `msTools.Updater` installieren → `web.config` ist die Paketversion, Anwendung startet nicht mehr |
| `## Erwartetes Verhalten` | Konfigurierte deployment-seitige Dateien/Inhalte bleiben erhalten; nicht konfigurierte Dateien verhalten sich wie bisher; anwendungsseitige Neuerungen in gemergten Dateien kommen an |
| `## Notwendige Änderungen an msTools.Updater` | Nummerierte Anforderungen (Vorschlag D1–D9): **D1** konfigurierbare Liste geschützter Pfade in `AutoUpdateOptions` (je Eintrag: Pfad relativ zum Anwendungsverzeichnis + Strategie `Preserve`/`Merge`), setzbar per Fluent-API und Konfigurationsbindung (`AutoUpdate:*`) — die Liste gilt für beliebige Dateien, nicht nur `web.config`; **D2** Backup vorhandener geschützter Dateien vor dem Überschreiben (in den Updater-Workspace, z. B. unter `Updates/`, damit das Anwendungsverzeichnis nicht vermüllt); **D3** XML-Merge: konfigurierbare Liste von Element-Pfaden/XPath-Ausdrücken, deren Elemente aus der Bestandsdatei in die neue übernommen werden (z. B. `//aspNetCore/environmentVariables`, `//security/ipSecurity`, `//httpProtocol/customHeaders`), plus konfigurierbare Attribut-Übernahmen aus der Bestandsdatei (z. B. `aspNetCore@requestTimeout`), ohne das ganze Element einzufrieren; die Paketdatei bleibt maßgeblich; **D4** JSON-Merge: konfigurierbare Liste von JSON-Pfaden/Schlüsseln (z. B. `Jwt:Key`, `ConnectionStrings:DefaultConnection`), deren Werte aus der Bestandsdatei in die neue übernommen werden — für `appsettings*.json` und andere JSON-Dateien; die Paketdatei bleibt maßgeblich, neue Paket-Schlüssel kommen an; **D5** `Preserve`-Strategie: Bestandsdatei nach dem Kopieren über die Paketdatei zurückkopieren (für Dateien ohne anwendungsseitige Updates); **D6** Reihenfolge-Garantie: Backup → Kopieren → Merge/Restore strikt zwischen Entpacken und Neustart; **D7** Fehlertoleranz: Backup-/Merge-Fehler brechen das Skript nie ab (`Write-Warning`/`update.log`, Backup bleibt liegen, Neustart erfolgt) — relevant, weil die Skripte mit `ErrorActionPreference = "Stop"` laufen; **D8** Abwärtskompatibilität: leere Liste = bisheriges Verhalten, keine Breaking Changes an öffentlichen APIs, keine neuen Pflichtoptionen; **D9** Plattformabdeckung: beide Windows-Varianten (App-Pool, Dienst/Executable) und Linux `.sh` honorieren die Konfiguration gleichermaßen |
| `## Anwendungsbeispiel: VideoWebPlayer (Empfehlung)` | Konkrete Schutzliste als Referenzkonfiguration — verdeutlicht die Anforderungen, ohne sie festzulegen: `web.config` mit `Merge` (Element-Pfade `//aspNetCore/environmentVariables`, `//security/ipSecurity`, `//httpProtocol/customHeaders`; Attribut-Übernahmen wie `aspNetCore@requestTimeout`); `appsettings*.json` mit `Merge` für deployment-seitig angepasste Schlüssel (z. B. `AutoUpdate:AppPoolName`, Pfad-/Verbindungs-Overrides) bzw. `Preserve`, falls eine solche Datei deployment-seitig vollständig geführt wird |
| `## Nicht sinnvoll durch VideoWebPlayer lösbar` | Skripterzeugung liegt vollständig in der Bibliothek; eine anwendungsseitige `IAutoUpdateScriptGenerator`-Implementierung könnte den Skripttext nur als String patchen (fragil, dupliziert Plattformwissen, bricht still bei Bibliotheks-Upgrades) oder die Generierung komplett nachbilden — der Mechanismus gehört in den Generator selbst |
| `## Akzeptanzkriterien` | Messbare Kriterien: konfigurierte `web.config`-Elemente/Attribute und `appsettings*.json`-Schlüssel überleben das Update; `Preserve`-Dateien bleiben unverändert; nicht geschützte Dateien werden ersetzt; anwendungsseitige Neuerungen in gemergten Dateien kommen an; Merge-Fehler führt nicht zu gestoppter Anwendung + Backup liegt vor; leere Konfiguration = Altsverhalten; gilt auf Windows (beide Varianten) und Linux |
| `## Betroffene msTools.Updater-Komponenten` | `AutoUpdateScriptGenerator`, `AutoUpdateOptions`, `AutoUpdateBuilder`/`BindConfiguration`, `FileSystemAutoUpdatePackageStore`, alle drei Skript-Templates |
| `## Aufgaben im VideoWebPlayer nach Umsetzung` | nupkg-Erhöhung unter `lib/packages/` + `PackageReference`, Konfiguration der Schutzliste (konkrete `web.config`- und `appsettings*.json`-Merge-Regeln bzw. `Preserve`-Einträge gemäß Anwendungsbeispiel), Dokumentation, Tests, Release Notes |
| `## Referenzen` | `VideoWebPlayer/web.config`, `docs/Anforderung_msTools_Updater_Installationsskript.md` (Präzedenz), Issue #226, betroffene `AutoUpdateOptions`-Schlüssel, Ziel-Repository `martin-stromberg/msTools.Updater` (Issue-Ablage) |

## Neue Klassen

Keine — dieser Auftrag enthält nach der Strategieänderung keine Code-Änderungen. (Im vertagten Teil können Konfigurations-/Optionsklassen bzw. eine `LocalConfigurationExtensions` entstehen — Entscheidung nach der Bibliotheksänderung, siehe Abschnitt „Vertagt auf nach der msTools.Updater-Änderung".)

## Änderungen an bestehenden Klassen

Keine — siehe „Neue Klassen". (Vertagte Änderungen an `Program.cs`, `AutoUpdateExtensions`, `VideoWebPlayer.csproj` sind im Vertagt-Abschnitt aufgeführt.)

## Datenbankmigrationen

Keine.

## Validierungsregeln

Keine — die einzigen „Eingaben" dieses Auftrags sind Markdown-Dokumente. Fachliche Vollständigkeit des Anforderungsdokuments wird durch die Inhaltsvorgabe (Abschnitt „Inhalt des Anforderungsdokuments") sichergestellt.

## Konfigurationsänderungen

| Eintrag | Typ | Standardwert | Zweck |
|---------|-----|--------------|-------|
| `AutoUpdate:AppPoolName` / `AutoUpdate:SiteName` | bestehende `AutoUpdateOptions`-Schlüssel — **nur Dokumentation**, keine Code-Änderung | nicht gesetzt | Für IIS-Deployments zwingend (der Dienst-Probe erkennt keine App-Pools); werden jetzt in `TECH_Auto_Update.md` und `help/updates.md` als Konfigurationsschlüssel dokumentiert |
| Schutzliste/Merge-Regeln (Name wird durch die Bibliothek festgelegt, z. B. `AutoUpdate:PreserveFiles`) | **vertagt** | — | Konfiguration beginnt erst nach dem nupkg-Upgrade; die Anforderung verlangt Konfigurierbarkeit per `AutoUpdate:*`-Bindung und Fluent-API |

## Seiteneffekte und Risiken

- **Weiterhin update-gefährdete Installationen:** Bis zur neuen `msTools.Updater`-Version geht deployment-seitige `web.config`-/`appsettings`-Anpassung bei jedem Update verloren. Mitigiert durch die Dokumentations-Klarstellung (maschinenweite `Jwt__*`-Umgebungsvariablen als update-sichere Übergangslösung) — Restrisiko bleibt für IIS-spezifische Elemente (`ipSecurity`, `customHeaders`), für die es keine Umgebungsvariablen-Alternative gibt.
- **Anforderung gegen decompilierte Interna:** Das Dokument beschreibt funktionale Anforderungen, keine Skripttext-Details — dadurch robust gegenüber Implementierungsänderungen der Bibliothek.
- **Doku-Doppelzustand:** Die jetzt ergänzten Warnhinweise („wird beim Update überschrieben") müssen nach der Bibliotheksintegration erneut angefasst werden — im Vertagt-Abschnitt als eigene Aufgabe vermerkt.
- **Veraltete Doku-Abschnitte:** `TECH_Auto_Update.md` (DLL-Pfad statt nupkg, alte Workflow-Namen) und `GUIDE_Installation.md` (DLL-Referenz) sind laut Bestandsaufnahme veraltet. Bereinigung ist nicht auftragsgegenstand — falls angefasst, nur in eigenem Commit und im Handover benannt.
- **Scope-Konflikt mit parallel laufender Diagnose-Anforderung:** Der Präzedenzfall (`update.log`-Logging) ist unter Windows noch unerfüllt. Das neue Anforderungsdokument verlangt für Merge-Fehler nur „Warnung + `update.log` wo vorhanden", um die Scopes sauber getrennt zu halten.
- **`gh`-Verfügbarkeit für den Issue-Schritt:** Das Issue im `msTools.Updater`-Repo wird via `gh issue create` angelegt. In der Planung verifiziert: `gh` 2.92.0 installiert, `gh auth status` = eingeloggt als `martin-stromberg` (Scopes u. a. `repo`), `gh repo view martin-stromberg/msTools.Updater` erfolgreich. Ist `gh` im Implementierungslauf dennoch nicht verfügbar/authentifiziert oder das Repo nicht erreichbar, wird der Schritt **nicht** als Blocker behandelt — Fallback: Repo-Dokument bleibt bestehen, der Anwender legt das Issue manuell an; der Umstand ist im Handover/Implementierungsbericht zu vermerken.

## Umsetzungsreihenfolge

1. **Anforderungsdokument `docs/Anforderung_msTools_Updater_Dateierhalt.md` erstellen**
   - Voraussetzungen: Keine — Präzedenzfall (`docs/Anforderung_msTools_Updater_Installationsskript.md`) und Bestandsaufnahme (insb. `inventory/deployment.md` mit den verifizierten Bibliotheks-Interna) liegen vor.
   - Beschreibung: Dokument nach der Gliederung im Abschnitt „Inhalt des Anforderungsdokuments" verfassen. Funktionale Anforderungen formulieren (was, nicht wie im Skripttext); Suggestion der API-Form (`PreserveFiles`/Merge-Regeln) nur als Vorschlag kennzeichnen — die endgültige Gestalt entscheidet der Bibliotheksbetreuer.

2. **Issue im `msTools.Updater`-Repository anlegen**
   - Voraussetzungen: Schritt 1 (der Issue-Inhalt ist das fertige Anforderungsdokument); `gh` CLI installiert und authentifiziert — vor dem Anlegen verifizieren: `gh auth status` und `gh repo view martin-stromberg/msTools.Updater` (laut Bestandsaufnahme ist `github.com/martin-stromberg/msTools.Updater` das nuspec-Repository der Bibliothek; in der Planung bereits erfolgreich geprüft).
   - Beschreibung: Deutschsprachiges Issue via `gh issue create --repo martin-stromberg/msTools.Updater` anlegen; Titel deutsch (z. B. „Update-Installation überschreibt deployment-seitige Dateien — konfigurierbarer Dateierhalt/Merge erforderlich"); Inhalt: das Anforderungsdokument bzw. eine kompakte Issue-Fassung mit Verweis auf `docs/Anforderung_msTools_Updater_Dateierhalt.md`. Issue-URL im Handover/Implementierungsbericht und als Querverweis im Repo-Dokument vermerken. **Fallback:** Ist `gh` nicht verfügbar/authentifiziert oder das Repo nicht erreichbar, kein Abbruch — manuellen Hinweis an den Anwender ausgeben und im Bericht vermerken (siehe Seiteneffekte/Risiken).

3. **Dokumentations-Klarstellungen**
   - Voraussetzungen: Schritt 1 (die Doku verweist auf das Anforderungsdokument); optional Schritt 2 (die Issue-URL kann als Querverweis erwähnt werden).
   - Beschreibung:
     - `docs/GUIDE_Installation.md` („Produktive Konfiguration"): IIS-`environmentVariables`-Variante benennen, aber als **beim Programmupdate gefährdet** kennzeichnen; maschinenweite `Jwt__*`-Umgebungsvariablen als update-sichere Übergangslösung empfehlen; Hinweis, dass mit einer künftigen `msTools.Updater`-Version geschützte `web.config`-Inhalte unterstützt werden (Verweis auf das Anforderungsdokument); Klarstellung, dass auch andere `web.config`-Anpassungen (z. B. `ipSecurity`, `customHeaders`) derzeit verloren gehen.
     - `docs/SECRETS_MANAGEMENT.md`: Dieselbe Klarstellung für Secrets — `environmentVariables` in der `web.config` als update-gefährdete Ablage markieren, update-sichere Produktionsvariante (maschinenweite Umgebungsvariablen) eindeutig benennen.
     - `docs/help/backups.md` (IIS-`web.config`-Absatz um Zeile 61): Überschreiben nicht nur beim manuellen Deployment, sondern **bei jedem Programmupdate** erwähnen; auf die künftige Bibliothekslösung verweisen.
     - `docs/help/updates.md`: Hinweis, dass eine Update-Installation deployment-seitige Dateien (`web.config`, `appsettings*.json`) ersetzt; `AutoUpdate:AppPoolName`/`SiteName` als Konfigurationsschlüssel für IIS-Deployments dokumentieren (kein UI-Feld — außerhalb des Auftrags).
     - `docs/TECH_Auto_Update.md`: `AppPoolName`/`SiteName` in die Konfigurationstabelle aufnehmen; Verweis auf `docs/Anforderung_msTools_Updater_Dateierhalt.md` (geplanter Dateierhalt-Mechanismus); Hinweis, dass das `BeforeInstall`-Backup nur Daten sichert, keine Konfigurationsdateien.
     - `docs/RELEASE_NOTES.md`: Bekannte-Einschränkung-Eintrag (Update ersetzt deployment-seitige Dateien; Übergangsempfehlung maschinenweite Umgebungsvariablen).
     - Optional, **eigener Commit** (außerhalb des Auftrags): veraltete Abschnitte in `TECH_Auto_Update.md`/`GUIDE_Installation.md` bereinigen (nupkg/`lib/packages` statt `lib/msTools.Updater/msTools.Updater.dll`, `build-and-package`-Action statt `main-release.yml`/`create-update-manifest.sh`) und im Handover benennen.

4. **Doku-Änderungen verifizieren**
   - Voraussetzungen: Schritte 1–3.
   - Beschreibung: `dotnet test tools/MarkdownLinkCheck.Tests` ausführen (projektüblicher Befehl laut `GUIDE_Installation.md`), damit die neuen/angefassten Markdown-Verweise nicht brechen. Build/Test der Anwendung ist nicht erforderlich (kein Code geändert); `dotnet build` schadet nicht, ist aber nicht DoD-relevant für diese Doku-Lieferung — DoD-Build-/Testanforderung greift bei Code-Fortsetzung.

## Vertagt auf nach der msTools.Updater-Änderung

Diese Arbeiten sind **nicht** Teil der jetzigen Umsetzung und beginnen erst, wenn eine `msTools.Updater`-Version mit Dateierhalt-/Merge-Funktionalität als nupkg vorliegt:

1. **nupkg-Integration:** Neue `msTools.Updater`-nupkg unter `lib/packages/` ablegen, `PackageReference` in `VideoWebPlayer.csproj` erhöhen; Lizenz, Release-Build (`dotnet build VideoPlayer.sln` Debug + Release) und CI-Skripte prüfen.
2. **Schutz-Konfiguration:** Konkrete Schutzliste für VideoWebPlayer-Deployments gemäß Anwendungsbeispiel im Anforderungsdokument festlegen — `web.config` mit Merge-Regeln (`environmentVariables`, `security/ipSecurity`, `httpProtocol/customHeaders`, `aspNetCore`-Attribut-Übernahmen wie `requestTimeout`) und `appsettings*.json` mit Merge-Regeln für deployment-seitig angepasste Schlüssel bzw. `Preserve` für Dateien ohne Paket-Aktualisierung; Eintrag in `appsettings.json`/Konfigurationsdoku.
3. **Neubewertung `appsettings.Local.json`:** Prüfen, ob die optionale lokale Konfigurationsdatei (ursprünglich Ansatz 3: `LocalConfigurationExtensions`, Aufruf in `Program.cs`, `CopyToPublishDirectory="Never"`, `.gitignore`) neben dem Bibliotheks-Mechanismus noch Mehrwert hat — v. a. für deployment-seitige `appsettings`-Overrides, die nicht gemerged werden sollen, und als nie ausgelieferte Secret-Ablage. Erst dann ggf. umsetzen.
4. **Tests:** Bindungs-/Konfigurationstest für die Schutzliste; ggf. Windows-gateter Ausführungstest des generierten Skripts (Muster `MsToolsUpdaterIntegrationTests`); ggf. `LocalConfigurationTests` bei Umsetzung von Punkt 3.
5. **Doku-Nachzug:** Warnhinweise aus Schritt 3 auf das neue Verhalten aktualisieren; Migrationshinweis für Bestandsinstallationen (vorhandene `environmentVariables` bleiben bei korrekter Konfiguration automatisch erhalten — kein manueller Umzug nötig); `SECRETS_MANAGEMENT.md`-Empfehlung final festlegen; `RELEASE_NOTES.md` um den eigentlichen Fix-Eintrag ergänzen.

## Tests

### Neue Tests

Keine — dieser Auftrag liefert ausschließlich Dokumente. Als Verifikation der Doku-Änderungen dient der Lauf von `tools/MarkdownLinkCheck.Tests` (Schritt 3). Tests für den Dateierhalt entstehen erst nach der Bibliotheksintegration (vertagt, s. o.).

### Betroffene bestehende Tests

Keine — es wird kein Code geändert; die Bestandssuite (1607 Tests, Stand der Bestandsaufnahme) bleibt unberührt.

### E2E-Tests (primärer Funktionsnachweis)

Keine E2E-Tests erforderlich — **Begründung:** Die Anforderung berührt in dieser Ausprägung keinen über UI oder Nutzeraktion erreichbaren Ablauf. Die Kernlieferung ist ein Anforderungsdokument an eine externe Bibliothek; die begleitenden Änderungen sind reine Markdown-Klarstellungen. Ein echter Funktionsnachweis des Dateierhalts kann ohnehin erst nach der Bibliotheksänderung erfolgen und ist dort (Bibliothek) bzw. in der vertagten Integration (Skript-Ausführungstest) verortet — ein Playwright-Test kann den Installationspfad prinzipiell nicht beobachten, weil das Skript in einem separaten Prozess läuft, während der Anwendungshost beendet ist. Die bestehenden `UpdatesPageE2ETests` bleiben unverändert gültig.

## Offene Punkte

Keine.
