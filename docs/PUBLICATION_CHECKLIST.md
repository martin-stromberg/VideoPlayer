# Veröffentlichungscheckliste

> **Dokumenttyp**: Checkliste  
> **Zielgruppe**: Maintainer  
> **Letzte Aktualisierung**: 2026-08-25

Vor einer öffentlichen Bereitstellung prüfen:

- [ ] `LICENSE` ist unverändert und wird als PolyForm Noncommercial License 1.0.0 ausgewiesen.
- [ ] README und Installationsdokumentation nennen keine OSI-Open-Source-Freigabe.
- [ ] Kommerzielle Nutzung verweist auf `mstromberg84+videoplayer@gmail.com`.
- [ ] `dotnet run --no-restore --project tools/MarkdownLinkCheck/MarkdownLinkCheck.csproj -- --root .` läuft erfolgreich.
- [ ] Der lokale Hook ist mit `git config core.hooksPath .githooks` aktivierbar.
- [ ] `dotnet build VideoPlayer.sln` läuft ohne MAUI-Projekte.
- [ ] `dotnet test VideoWebPlayer.Tests/VideoWebPlayer.Tests.csproj` läuft erfolgreich.
- [ ] `dotnet test tools/MarkdownLinkCheck.Tests/MarkdownLinkCheck.Tests.csproj` läuft erfolgreich.
- [ ] Das MAUI-Repository baut und testet mit seiner eigenen Solution.
- [ ] `dotnet test VideoWebPlayer.Tests/VideoWebPlayer.Tests.csproj --filter ApiDocumentationContractTests` weist Health, Login und authentifizierten Lesezugriff nach.
- [ ] `dotnet list VideoPlayer.sln package --vulnerable --include-transitive` und derselbe Scan im MAUI-Repository melden keine High-Advisories oder dokumentierte, freigegebene Ausnahmen.
- [ ] [API-Dokumentation](./API.md) deckt die vom MAUI-Client verwendeten Routen ab.
- [ ] [Secrets Management](./SECRETS_MANAGEMENT.md) enthält nur synthetische Platzhalter.
- [ ] Arbeitsbaum und vereinbarter Historienumfang wurden im Web-Repository und im MAUI-Repository mit den in [Secrets Management](./SECRETS_MANAGEMENT.md) dokumentierten Kommandos auf Secrets geprüft.
- [ ] Produktive Secrets wurden außerhalb des Repositorys erzeugt und dokumentiert rotiert.
