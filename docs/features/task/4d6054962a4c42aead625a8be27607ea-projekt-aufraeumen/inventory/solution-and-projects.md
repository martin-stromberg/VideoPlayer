# Solution und Projekte

## Quelle

- `VideoPlayer.sln`
- alle `*.csproj` außerhalb von `bin/` und `obj/`

## Aktuelle Solution

`VideoPlayer.sln` enthält fünf Projekte:

| Projekt | Pfad | Rolle | Status |
|---|---|---|---|
| `VideoWebPlayer` | `VideoWebPlayer/VideoWebPlayer.csproj` | ASP.NET-Core-Webanwendung und API | erhalten |
| `VideoWebPlayer.Client` | `VideoWebPlayer.Client/VideoWebPlayer.Client.csproj` | gemeinsamer Client, Modelle und API-Aufrufe | erhalten |
| `VideoWebPlayer.Tests` | `VideoWebPlayer.Tests/VideoWebPlayer.Tests.csproj` | Server-/Integrations- und Playwright-Tests | erhalten |
| `VideoWebPlayer.Maui` | `VideoWebPlayer.Maui/VideoWebPlayer.Maui.csproj` | mobile MAUI-Anwendung | erhalten |
| `VideoWebPlayer.Maui.Tests` | `VideoWebPlayer.Maui.Tests/VideoWebPlayer.Maui.Tests.csproj` | Tests für mobile bzw. gemeinsame Logik | erhalten |

Die Solution-Einträge verweisen auf vorhandene Projektdateien. Ein verwaister Solution-Eintrag ist im Ausgangszustand nicht erkennbar.

## Projekte außerhalb der Solution

| Projekt/Ordner | Inhalt | Vorläufige Bewertung |
|---|---|---|
| `Videos` | separates MAUI-Template/ältere App, 39 Dateien | Löschkandidat, keine Solution- oder Projektverweise gefunden |
| `WebPlayer` | ältere Webanwendung mit eigenem Client, 96 Dateien | Löschkandidat, nicht in Solution und nicht von Kernprojekten referenziert |
| `WebPlayerApi` | ältere API, 28 Dateien | Löschkandidat, nicht in Solution und nicht von Kernprojekten referenziert |
| `WebPlayerApi.Common` | gemeinsames Modellprojekt der älteren API, 6 Dateien | nur zusammen mit `WebPlayer`/`WebPlayerApi` entfernbar |

Die Bewertung ist vor der Umsetzung nochmals gegen Laufzeitdeployment, CI-Skripte und externe Verweise zu prüfen.
