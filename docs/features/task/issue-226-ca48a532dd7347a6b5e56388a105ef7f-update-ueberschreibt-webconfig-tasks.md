# Tasks: Update überschreibt `web.config` — JWT-/Konfigurationseinträge gehen verloren

| # | Bereich | Aufgabe | Status | Testnachweis |
|---|---------|---------|--------|--------------|
| 1 | Anforderung | `docs/Anforderung_msTools_Updater_Dateierhalt.md` anlegen: Ziel, festgestellte Symptome, Reproduktion, erwartetes Verhalten (Struktur analog `docs/Anforderung_msTools_Updater_Installationsskript.md`) | Offen | — |
| 2 | Anforderung | Nummerierte Anforderungen D1–D9 formulieren: konfigurierbare Schutzliste für beliebige Dateien (`Preserve`/`Merge`), Backup vor Überschreiben, XML-Merge (Element-Pfade + Attribut-Übernahmen), JSON-Merge (Schlüsselpfade für `appsettings*.json`), `Preserve`-Strategie, Reihenfolge-Garantie, Fehlertoleranz, Abwärtskompatibilität, Plattformabdeckung (beide Windows-Varianten + Linux) | Offen | — |
| 3 | Anforderung | Abschnitte „Anwendungsbeispiel: VideoWebPlayer (Empfehlung)" (konkrete Schutzliste: `web.config`-Merge + `appsettings*.json`-Merge/`Preserve`), „Nicht sinnvoll durch VideoWebPlayer lösbar", „Akzeptanzkriterien", „Betroffene msTools.Updater-Komponenten", „Aufgaben im VideoWebPlayer nach Umsetzung" und „Referenzen" ergänzen | Offen | — |
| 4 | Übergabe | Issue in `martin-stromberg/msTools.Updater` via `gh issue create` anlegen (Titel deutsch, Inhalt = Anforderungsdokument bzw. kompakte Fassung mit Verweis); vorab `gh auth status` + `gh repo view` verifizieren; Issue-URL im Bericht/Dokument vermerken; Fallback bei nicht verfügbarem `gh`: manueller Hinweis an den Anwender | Offen | — |
| 5 | Dokumentation | `docs/GUIDE_Installation.md` („Produktive Konfiguration"): IIS-`environmentVariables`-Variante als update-gefährdet kennzeichnen, maschinenweite `Jwt__*`-Umgebungsvariablen als update-sichere Übergangslösung empfehlen, Verweis auf das Anforderungsdokument | Offen | — |
| 6 | Dokumentation | `docs/SECRETS_MANAGEMENT.md`: `environmentVariables`/`web.config` als update-gefährdete Secret-Ablage markieren, update-sichere Produktionsvariante eindeutig benennen | Offen | — |
| 7 | Dokumentation | `docs/help/backups.md` (IIS-`web.config`-Absatz): Überschreiben auch bei Programmupdates (nicht nur manuellem Deployment) dokumentieren | Offen | — |
| 8 | Dokumentation | `docs/help/updates.md`: Hinweis auf Ersetzen deployment-seitiger Dateien (`web.config`, `appsettings*.json`) bei Update-Installation; `AutoUpdate:AppPoolName`/`SiteName` als Konfigurationsschlüssel für IIS-Deployments dokumentieren | Offen | — |
| 9 | Dokumentation | `docs/TECH_Auto_Update.md`: `AppPoolName`/`SiteName` in die Konfigurationstabelle aufnehmen; Verweis auf `docs/Anforderung_msTools_Updater_Dateierhalt.md`; Klarstellen, dass das `BeforeInstall`-Backup nur Daten sichert | Offen | — |
| 10 | Dokumentation | `docs/RELEASE_NOTES.md`: bekannte Einschränkung + Übergangsempfehlung (maschinenweite Umgebungsvariablen) ergänzen | Offen | — |
| 11 | Dokumentation | Optional (eigener Commit, außerhalb des Auftrags): veraltete Abschnitte in `TECH_Auto_Update.md`/`GUIDE_Installation.md` bereinigen (nupkg unter `lib/packages` statt DLL-Referenz, `build-and-package`-Action statt `main-release.yml`/`create-update-manifest.sh`) — im Handover benennen | Offen | — |
| 12 | Verifikation | `dotnet test tools/MarkdownLinkCheck.Tests` nach den Doku-Änderungen ausführen | Offen | — |

## Vertagt auf nach der msTools.Updater-Änderung

Diese Aufgaben beginnen erst, wenn eine `msTools.Updater`-Version mit Dateierhalt-/Merge-Funktionalität als nupkg vorliegt.

| # | Bereich | Aufgabe | Status | Testnachweis |
|---|---------|---------|--------|--------------|
| V1 | Integration | Neue `msTools.Updater`-nupkg unter `lib/packages/` ablegen, `PackageReference` in `VideoWebPlayer.csproj` erhöhen; Lizenz, Release-Build (Debug + Release) und CI-Kompatibilität prüfen | Vertagt | — |
| V2 | Konfiguration | Schutzliste/Merge-Regeln gemäß Anwendungsbeispiel im Anforderungsdokument konfigurieren: `web.config` (`environmentVariables`, `security/ipSecurity`, `httpProtocol/customHeaders`, `aspNetCore`-Attribut-Übernahmen wie `requestTimeout`) und `appsettings*.json` (Merge für deployment-seitig angepasste Schlüssel bzw. `Preserve` für Dateien ohne Paket-Aktualisierung) | Vertagt | — |
| V3 | Konfiguration | `appsettings.Local.json` neu bewerten (`LocalConfigurationExtensions`, `Program.cs`, `CopyToPublishDirectory="Never"`, `.gitignore`) und nur bei verbleibendem Mehrwert umsetzen | Vertagt | — |
| V4 | Tests | Bindungs-/Konfigurationstest der Schutzliste; ggf. Windows-gateter Ausführungstest des generierten Skripts (Muster `MsToolsUpdaterIntegrationTests`); ggf. `LocalConfigurationTests` | Vertagt | — |
| V5 | Dokumentation | Warnhinweise auf neues Verhalten aktualisieren; Migrationshinweis für Bestandsinstallationen; `SECRETS_MANAGEMENT.md`-Empfehlung final festlegen; `RELEASE_NOTES.md` um Fix-Eintrag ergänzen | Vertagt | — |
