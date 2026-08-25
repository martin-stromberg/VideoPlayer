# Veröffentlichungscheckliste

> **Dokumenttyp**: Checkliste  
> **Zielgruppe**: Maintainer  
> **Letzte Aktualisierung**: 2026-08-25

Der aktuelle Abarbeitungsstand ist in [docs/PUBLICATION_AUDIT.md](./PUBLICATION_AUDIT.md) dokumentiert. Vor einer öffentlichen Bereitstellung prüfen:

| Status | Punkt | Ergebnis |
|--------|-------|----------|
| [x] | `LICENSE` ist unverändert und wird als PolyForm Noncommercial License 1.0.0 ausgewiesen. | Lokal geprüft. |
| [x] | README und Installationsdokumentation nennen keine OSI-Open-Source-Freigabe. | Lokal geprüft. |
| [x] | Kommerzielle Nutzung verweist auf `mstromberg84+videoplayer@gmail.com`. | Lokal geprüft. |
| [x] | `dotnet run --no-restore --project tools/MarkdownLinkCheck/MarkdownLinkCheck.csproj -- --root .` läuft erfolgreich. | Bestanden: 61 Dateien, 101 lokale Links. |
| [x] | Der lokale Hook ist mit `git config core.hooksPath .githooks` aktivierbar. | Aktiviert; `core.hooksPath` liefert `.githooks`; Hook-Dateimodus `100755`. |
| [x] | `dotnet build VideoPlayer.sln` läuft erfolgreich. | Bestanden mit bestehenden Warnungen. |
| [x] | `dotnet test VideoWebPlayer.Tests/VideoWebPlayer.Tests.csproj` läuft erfolgreich. | Bestanden: 188/188 Tests. |
| [x] | `dotnet test tools/MarkdownLinkCheck.Tests/MarkdownLinkCheck.Tests.csproj` läuft erfolgreich. | Bestanden: 5/5 Tests. |
| [x] | `dotnet test VideoWebPlayer.Tests/VideoWebPlayer.Tests.csproj --filter ApiDocumentationContractTests` weist Health, Login und authentifizierten Lesezugriff nach. | Bestanden: 4/4 Tests. |
| [x] | `dotnet list VideoPlayer.sln package --vulnerable --include-transitive` meldet keine High-Advisories oder dokumentierte, freigegebene Ausnahmen. | Bestanden. |
| [x] | [API-Dokumentation](./API.md) deckt die Kernrouten ab. | Durch API-Vertragstest lokal nachgewiesen. |
| [x] | [Secrets Management](./SECRETS_MANAGEMENT.md) enthält nur synthetische Platzhalter. | Lokal geprüft. |
| [ ] | Arbeitsbaum und vereinbarter Historienumfang wurden mit den in [Secrets Management](./SECRETS_MANAGEMENT.md) dokumentierten Kommandos auf Secrets geprüft. | Web geprüft. Lokale Remote-URL enthielt ein Token und wurde bereinigt; Token-Rotation bleibt offen. |
| [ ] | Produktive Secrets wurden außerhalb des Repositorys erzeugt und dokumentiert rotiert. | Offen: muss außerhalb des Repositorys erfolgen. |

## Blocker vor `public`

- Linux-Frischclone-Hook-Test mit Erfolgsfall und fehlendem-`dotnet`-Fehlerpfad ausführen.
- GitHub-Repository-Einstellungen mit Maintainer-Zugriff prüfen.
- Produktive Secrets und das zuvor in der lokalen Remote-URL vorhandene GitHub-Token rotieren.
- Erst danach Repository auf `public` setzen und finalen Stand taggen oder dokumentieren.
