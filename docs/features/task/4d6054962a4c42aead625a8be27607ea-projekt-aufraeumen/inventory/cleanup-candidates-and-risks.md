# Bereinigungskandidaten und Risiken

## Vorläufige Kandidaten

| Kandidat | Begründung | notwendige Prüfung |
|---|---|---|
| `Videos/` | nicht in `VideoPlayer.sln`, keine Projektverweise aus den Kernprojekten | externe Startskripte, CI und Dokumentation |
| `WebPlayer/` | ältere Webanwendung außerhalb der Solution | externe Deployment- oder Nutzerverweise |
| `WebPlayerApi/` | ältere API außerhalb der Solution | externe Deployment- oder Nutzerverweise |
| `WebPlayerApi.Common/` | nur vom alten Graphen verwendet | gemeinsam mit den beiden alten Projekten entfernen |
| `WebPlayer/WebPlayer.Client/` | Teil des alten `WebPlayer`-Graphen | gemeinsam mit `WebPlayer` entfernen |

## Nicht als Kandidaten behandeln

- `VideoWebPlayer/`
- `VideoWebPlayer.Client/`
- `VideoWebPlayer.Maui/`
- `VideoWebPlayer.Tests/`
- `VideoWebPlayer.Maui.Tests/`
- `lib/msTools.Updater/`
- `Images/`, solange keine Prüfung der Laufzeit- und Buildreferenzen abgeschlossen ist

## Risiken und offene Prüfungen

- Eine Textsuche im Repository kann keine Verweise aus externen Pipelines, Installationsskripten oder Deployment-Konfigurationen außerhalb des Repositorys erfassen.
- `VideoWebPlayer.Maui.Tests` referenziert direkt die Webanwendung. Das ist ungewöhnlich, aber kein Beleg für eine entbehrliche Testabhängigkeit.
- Die in der Solution gesetzten x86-/x64-Konfigurationen bilden für MAUI nicht automatisch alle mobilen Zielplattformen ab. Die verbindlichen Buildziele müssen in der Planung festgelegt werden.
- Das Entfernen der alten API darf keine fachliche API-Übernahme oder Vertragsänderung auslösen; die aktuelle MAUI-Kommunikation läuft über `VideoWebPlayer` und `VideoWebPlayer.Client`.
