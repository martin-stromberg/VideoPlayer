# Übersetzte Anforderung

## Aufgaben-ID
92a007c9-3c51-4c04-a200-a7652154291c

## Branch
task/issue-97-92a007c93c514c04a200a7652154291c-eigenes-repository-fuer-mstool

## Kurzbeschreibung
Das Projekt `msTools.Backup` samt zugehörigem Testprojekt `msTools.Backup.Tests` soll aus dem Haupt-Repository in ein eigenes privates Git-Repository ausgelagert werden, um die Bibliothek später als NuGet-Paket zu veröffentlichen.

## Anforderungsdetails

1. **Ziel-Repository**
   - Im Unterverzeichnis `Sub-Repository` des Haupt-Repositorys befindet sich das neue, noch private Repository `msTools.Backup`.
   - Das Projekt `msTools.Backup` und das Testprojekt `msTools.Backup.Tests` sollen dorthin verschoben werden.

2. **GitHub-Workflows im Ziel-Repository**
   - Erstelle im Ziel-Repository denselben Branch-basierten Workflow wie im Hauptprojekt:
     - Branch `staging` erzeugt RC-Versionen als Pre-releases.
     - Branch `main` erzeugt fertige Releases.
   - Die Workflow-Ausgaben sollen NuGet-Pakete (`.nupkg`) sein, die im GitHub-Release veröffentlicht werden.

3. **Lokale Paketintegration im Hauptprojekt**
   - Erstelle eine erste `.nupkg`-Datei der Bibliothek.
   - Lege diese im Haupt-Repository ab.
   - Binde sie im Haupt-Repository als lokale NuGet-Quelle ein.
   - Das Unterverzeichnis `Sub-Repository` soll lokal anschließend nicht mehr erhalten bleiben; nur die `.nupkg`-Datei wird genutzt.

## Akzeptanzkriterien

- [ ] `msTools.Backup` und `msTools.Backup.Tests` liegen im `Sub-Repository`-Ordner und bilden dort ein eigenständiges, baufbares Projekt.
- [ ] Im `Sub-Repository` existieren `.github/workflows/staging-ci.yml` und `.github/workflows/main-release.yml`, die `.nupkg`-Artefakte bauen und veröffentlichen.
- [ ] Eine `.nupkg`-Datei von `msTools.Backup` wurde lokal erstellt und im Haupt-Repository abgelegt.
- [ ] Das Haupt-Repository bindet die lokale `.nupkg` als PackageReference bzw. lokale NuGet-Quelle ein und baut ohne Fehler.
- [ ] Der `Sub-Repository`-Ordner wurde lokal entfernt; er ist nicht mehr Teil des Haupt-Repositorys.
