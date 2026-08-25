# Aktuelle Projektstruktur

Die Solution `VideoPlayer.sln` enthält die Bestandteile des Web-Repositorys:

- `VideoWebPlayer` - ASP.NET-Core-Blazor-Webanwendung und API
- `VideoWebPlayer.Client` - gemeinsam genutzte Client- und API-Modelle
- `VideoWebPlayer.Tests` - Tests für Webanwendung, API-Vertrag und Services
- `tools/MarkdownLinkCheck` - lokaler Markdown-Linkcheck
- `tools/MarkdownLinkCheck.Tests` - Tests für den Linkcheck

## Zuständigkeiten

- Web-Repository: Backend, Weboberfläche, Webtests, API-Vertrag, Installations- und Veröffentlichungsdokumentation.

Die Webanwendung stellt die API bereit, die im dokumentierten Vertrag in [../API.md](../API.md) beschrieben ist.

## Entwicklung und Start

```bash
dotnet build VideoPlayer.sln
dotnet test VideoWebPlayer.Tests/VideoWebPlayer.Tests.csproj
```

[Zurück zur Dokumentationsübersicht](index.md)
