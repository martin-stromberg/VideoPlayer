# Bestandsaufnahme

## Kontext

Die Bestandsaufnahme bezieht sich auf `requirement.md` und den Repository-Zustand am 2026-08-24 auf Branch `task/4d6054962a4c42aead625a8be27607ea-projekt-aufraeumen`.

Ziel ist die Bereinigung nicht mehr benötigter Projekte und Dateien bei unverändertem Erhalt der Webanwendung, der MAUI-Anwendung, ihrer API-Kommunikation und der relevanten Tests.

## Ergebnis

`VideoPlayer.sln` enthält bereits ausschließlich den aktuellen `VideoWebPlayer`-Projektverbund aus Webanwendung, gemeinsamem Client, MAUI-Anwendung und den beiden Testprojekten. Die Verweise innerhalb dieses Verbunds sind konsistent und zeigen auf vorhandene Dateien.

Außerhalb der Solution liegen jedoch vier ältere Projektbereiche: `Videos`, `WebPlayer`, `WebPlayerApi` und `WebPlayerApi.Common`. Der alte Web-/API-Bereich bildet einen separaten Projektgraphen und wird von keinem erhaltenen Projekt referenziert. Diese Bereiche sind die primären Bereinigungskandidaten, müssen aber vor dem Löschen gegen externe Skripte und Deployment-Verwendungen geprüft werden.

Die Kernabhängigkeit `VideoWebPlayer.Client` wird sowohl von Web als auch MAUI verwendet. Die Webanwendung besitzt außerdem eine direkte Dateiabhängigkeit auf `lib/msTools.Updater/msTools.Updater.dll`; diese Datei und ihr Ordner dürfen nicht als Altlast entfernt werden.

## Detaildokumente

- [Solution und Projekte](inventory/solution-and-projects.md)
- [Abhängigkeiten und Referenzen](inventory/dependency-graph.md)
- [Erhaltene Laufzeitoberflächen](inventory/retained-runtime-surfaces.md)
- [Bereinigungskandidaten und Risiken](inventory/cleanup-candidates-and-risks.md)

## Relevante Ausgangsstellen

- `VideoPlayer.sln:6-14`: fünf aktuelle Solution-Projekte
- `VideoWebPlayer/VideoWebPlayer.csproj:50`: Referenz auf `VideoWebPlayer.Client`
- `VideoWebPlayer.Maui/VideoWebPlayer.Maui.csproj:76`: Referenz auf `VideoWebPlayer.Client`
- `VideoWebPlayer/Program.cs:28-40`: Dienstregistrierung, Anwendungspipeline und Discovery
- `VideoWebPlayer.Client/VideoWebPlayerClient.cs:165-171`: gemeinsamer API-Authentifizierungsaufruf
- `VideoWebPlayer.Maui/MauiProgram.cs:20-35`: mobile `HttpClient`-Registrierung und Laufzeit-Basisadresse
- `VideoWebPlayer/VideoWebPlayer.csproj:54-58`: lokale `msTools.Updater`-DLL

## Vorschlag für die Planung

1. Externe Referenzen auf die vier alten Bereiche sowie unversionierte Build-/Startartefakte prüfen.
2. Die alten Projektbereiche als Einheit entfernen, falls diese Prüfung unauffällig ist.
3. Solution und Projektverweise nach der Entfernung maschinell validieren.
4. Webanwendung, gemeinsame Clientbibliothek, Tests und ein geeignetes MAUI-Ziel bauen bzw. testen; die API-Kommunikation über einen vorhandenen Client-/Integrationstest absichern.

## Offene Punkte für die Planung

- Welche Build-Ziele der MAUI-Anwendung sind in der Zielumgebung verbindlich verfügbar und müssen erfolgreich geprüft werden?
- Gibt es außerhalb des Repositorys Deployment-, CI- oder Startskripte, die `Videos`, `WebPlayer` oder `WebPlayerApi` noch verwenden?
