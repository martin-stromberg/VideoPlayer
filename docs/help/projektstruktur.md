# Aktuelle Projektstruktur

Die Solution `VideoPlayer.sln` enthält die Bestandteile des Web-Repositorys:

- `VideoWebPlayer` - ASP.NET-Core-Blazor-Webanwendung und API
- `VideoWebPlayer.Client` - gemeinsam genutzte Client- und API-Modelle
- `VideoWebPlayer.Tests` - Tests für Webanwendung, API-Vertrag und Services
- `tools/MarkdownLinkCheck` - lokaler Markdown-Linkcheck
- `tools/MarkdownLinkCheck.Tests` - Tests für den Linkcheck

Die MAUI-Anwendung wurde in ein eigenes Repository ausgelagert. In dieser Arbeitskopie liegt der vorhandene Klon unter `Sub-Repository/`; er besitzt eigene Git-Metadaten und wird vom Web-Repository ignoriert.

## Zuständigkeiten

- Web-Repository: Backend, Weboberfläche, Webtests, API-Vertrag, Installations- und Veröffentlichungsdokumentation.
- MAUI-Repository: `VideoWebPlayer.Maui`, `VideoWebPlayer.Maui.Tests`, kopierte `VideoWebPlayer.Client`-Übergabeschnittstelle und mobile Build-/Testdokumentation.

Die Webanwendung stellt die API bereit, die MAUI über den dokumentierten Vertrag in [../API.md](../API.md) verwendet.

## Entwicklung und Start

Für Webentwicklung und CI werden keine MAUI-Workloads benötigt:

```bash
dotnet build VideoPlayer.sln
dotnet test VideoWebPlayer.Tests/VideoWebPlayer.Tests.csproj
```

Mobile Builds und MAUI-spezifische Tests laufen im separaten MAUI-Repository.

[Zurück zur Dokumentationsübersicht](index.md)
