# Detailinventar: Lizenzierung und Projektmetadaten

## Bestand

- `LICENSE:1-24` fuehrt PolyForm Noncommercial License 1.0.0 auf, erlaubt nichtkommerzielle Nutzung und verlangt fuer kommerzielle Nutzung eine separate Lizenz.
- `README.md:13-16` verlinkt `./LICENSE` und zeigt ein PolyForm-Noncommercial-Badge.
- `README.md:207-230` beschreibt den Schnellstart; `README.md:369-378` enthaelt den Lizenzabschnitt mit Kontaktadresse.
- `VideoWebPlayer/VideoWebPlayer.csproj:8-11` und die uebrigen Projektdateien setzen `PackageLicenseFile`, Copyright und Autoren. Die Projekte packen `../LICENSE` als Paketdatei.

## Betroffene Dateien

`LICENSE`, `README.md`, `VideoWebPlayer/*.csproj`, `VideoWebPlayer.Client/*.csproj`, `VideoWebPlayer.Maui/*.csproj`, `VideoWebPlayer.Tests/*.csproj` und `VideoWebPlayer.Maui.Tests/*.csproj`.

## Risiken

- Die Anforderung beschreibt eine individuelle Nutzungsregelung; ein Lizenzname allein ersetzt keine rechtliche Freigabe.
- README, Lizenzdatei, Paketmetadaten und gegebenenfalls GitHub-Metadaten duerfen keine unterschiedlichen Begriffe fuer private, edukative, Open-Source- oder kommerzielle Nutzung verwenden.
- Die vorhandene Lizenz ist kein Standard-SPDX-Text und sollte nicht stillschweigend als OSI-freie Open-Source-Lizenz bezeichnet werden.

## Verifikation

Lizenztext, README-Badge/-Abschnitt und gepackte Projektmetadaten auf identische Bezeichnung und Kontaktstelle pruefen; anschliessend einen Paket-/Build-Test ausfuehren.
