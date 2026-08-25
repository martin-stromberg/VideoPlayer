# Detailinventar: Installation und Laufzeitvoraussetzungen

## Bestand

- Backend und Backend-Tests zielen auf `net10.0` (`VideoWebPlayer/VideoWebPlayer.csproj`, `VideoWebPlayer.Tests/VideoWebPlayer.Tests.csproj`).
- MAUI verwendet `net10.0-android`, `net10.0-ios`, `net10.0-maccatalyst` und unter Windows `net10.0-windows10.0.19041.0`; Minimalversionen stehen in `VideoWebPlayer.Maui/VideoWebPlayer.Maui.csproj:34-38`.
- SQLite ist eingebettet und wird ueber `VideoWebPlayer/appsettings.json:2-4` konfiguriert. NuGet.org und die lokale Quelle `lib/packages` stehen in `NuGet.config`.
- `docs/GUIDE_Installation.md` beschreibt Clone, Restore, MAUI-Workload, Secrets, Start, Medienquellen, Deployment und Fehlerbehebung.
- `README.md:207-230` bietet einen kuerzeren Schnellstart; mehrere README- und Guide-Links verwenden jedoch `Docs/...`, obwohl der Repository-Ordner `docs/` klein geschrieben ist.

## Luecken

- Installationsschritte sind auf 2024 datiert und nennen teilweise nicht vorhandene Dokumente bzw. alte Solution-/Projektannahmen.
- Die tatsaechlichen Startports kommen aus `VideoWebPlayer/Properties/launchSettings.json` (`http://localhost:5039`), waehrend die Anleitung wiederholt Ports 5000/5001 nennt.
- Die Anleitung muss zwischen Backend-Entwicklung, MAUI-Entwicklung und optionalem Deployment trennen und die lokale NuGet-Quelle sowie die nicht auf NuGet veroeffentlichte DLL beruecksichtigen.
- Erfolgspruefung, notwendige Schreibrechte fuer `Data/` und Umgang mit fehlenden Secrets muessen als reproduzierbare Schritte beschrieben werden.

## Verifikation

Eine saubere Einrichtung ohne lokale User-Secrets oder Build-Artefakte nachvollziehen: Voraussetzungen pruefen, klonen, restore, Secrets als Platzhalter setzen, Backend starten, Health-/Login-Fluss pruefen und mindestens einen Test-/Build-Befehl ausfuehren. Nicht unterstuetzte Plattformen muessen klar als optional gekennzeichnet sein.
