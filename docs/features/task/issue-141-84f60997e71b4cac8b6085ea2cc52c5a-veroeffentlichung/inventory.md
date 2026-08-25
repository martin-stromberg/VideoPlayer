# Bestandsaufnahme: Veroeffentlichung

## Scope

Die Anforderung betrifft Repository- und Entwicklungsinfrastruktur:

- Lizenztext und konsistente Lizenzdarstellung.
- Versionierter Markdown-Linkcheck als Git-Hook mit reproduzierbarem Aufruf.
- Eine belastbare Installations- und Konfigurationsanleitung.
- Vor der Veroeffentlichung eine Pruefung auf Geheimnisse und private Konfiguration.

Die bestehende Laufzeitlogik, Datenmodelle und UI sind fuer die Anforderung nicht primaer betroffen.

## Repository-Struktur

- Root-Metadaten: `README.md`, `LICENSE`, `.gitignore`, `NuGet.config`, `VideoPlayer.sln`.
- Backend: `VideoWebPlayer/VideoWebPlayer.csproj`, Ziel `net10.0`, ASP.NET Core/Blazor, SQLite.
- Gemeinsames Client-Projekt: `VideoWebPlayer.Client/VideoWebPlayer.Client.csproj`.
- MAUI-Anwendung: `VideoWebPlayer.Maui/VideoWebPlayer.Maui.csproj` mit Android, iOS, MacCatalyst und Windows Target Frameworks, soweit das Betriebssystem dies zulaesst.
- Tests: `VideoWebPlayer.Tests/` und `VideoWebPlayer.Maui.Tests/`.
- CI: mehrere Workflows unter `.github/workflows/`, darunter Build, Tests, Formatierung, Vulnerability Scan und Release-Erstellung.
- Dokumentation: `docs/`, insbesondere `docs/GUIDE_Installation.md`, `docs/INDEX.md` und `docs/SECRETS_MANAGEMENT.md`.
- Lokale Abhaengigkeiten: `lib/packages/` und `lib/msTools.Updater/`; NuGet-Quellen sind in `NuGet.config` festgelegt.

## Ist-Zustand und Luecken

| Bereich | Vorhanden | Relevante Luecke oder Auswirkung |
|---|---|---|
| Lizenz | `LICENSE` enthaelt PolyForm Noncommercial 1.0.0; README nennt dieselbe Lizenz | Lizenztext, Kontakt und genaue Nutzungsgrenzen muessen rechtlich und redaktionell konsistent bestaetigt werden. |
| Lizenz in Projekten | Alle geprueften .csproj-Dateien referenzieren `LICENSE` als PackageLicenseFile bzw. Pack-Inhalt | Eine Aenderung am Lizenznamen oder Text muss die NuGet-Paketmetadaten und alle Projekte beruecksichtigen. |
| Markdown-Linkcheck | Kein versionierter Hook unter `.githooks/`, kein erkennbarer Markdown-Linkcheck im Root und keine dedizierte CI-Pruefung | Einrichtung, Plattformverhalten, Fehlerausgabe und Scope muessen neu festgelegt werden. |
| Qualitaetspruefung | Bestehende CI nutzt `dotnet format`, Build, Tests und Vulnerability Scans | Der neue lokale Check muss in einen vorhandenen lokalen oder CI-Aufruf integrierbar sein, ohne externe Linkpruefung zu erzwingen. |
| Installation | `docs/GUIDE_Installation.md` existiert | Verweist auf teils nicht vorhandene `Docs/...`-Pfade, ist auf Stand 2024 datiert und enthaelt mehrere nicht verifizierte Plattform-/Startannahmen. |
| Geheimnisse | User Secrets werden im Backend vorgesehen; `.gitignore` ignoriert typische lokale Dateien | `docs/SECRETS_MANAGEMENT.md` enthaelt beispielhafte tokenartige Werte und beschreibt einen hardkodierten MAUI-Token. Vor Public Release ist eine Bereinigung und Historienpruefung erforderlich. |
| Branch/Repository | Aktiver Feature-Branch ist `task/issue-141-84f60997e71b4cac8b6085ea2cc52c5a-veroeffentlichung`; Arbeitsbaum enthaelt nur die vorhandenen Feature-Artefakte als unversionierte Aenderung | Die Inventarartefakte gehoeren zum Feature-Branch und duerfen nicht mit fremden Arbeitsbaum-Aenderungen vermischt werden. |

## Umsetzungsflaechen

1. Lizenz: `LICENSE`, Lizenzabschnitt und Lizenzbadge in `README.md`, `PackageLicenseFile`/`None Include` in den fuenf Projektdateien.
2. Linkpruefung: neue versionierte Skripte/Hook-Konfiguration, gegebenenfalls `.githooks/`, `.gitconfig`-Einrichtung, lokale Qualitaetsbefehle und ein passender CI-Schritt.
3. Dokumentation: `README.md`, `docs/GUIDE_Installation.md`, `docs/INDEX.md` sowie die Verweise auf die tatsaechlich vorhandenen Pfade.
4. Geheimnisschutz: `docs/SECRETS_MANAGEMENT.md`, Konfigurationsdateien, MAUI-Services, `.gitignore` und Git-Historie; konkrete Werte duerfen nicht in neue oeffentliche Beispiele uebernommen werden.
5. Verifikation: Markdown-Linkcheck selbst, Hook-Fehlerfall, saubere Installationsschritte, Build/Tests sowie ein Repository-Scan nach Credentials.

## Offene Entscheidungen fuer die Planung

- Der konkrete Lizenztext und die zustaendige Kontaktstelle sind fachlich zu bestaetigen; technisch ist aktuell PolyForm Noncommercial 1.0.0 mit einer E-Mail-Adresse eingetragen.
- Fuer den Hook muessen Zielbetriebssysteme und die Einrichtungsmethode festgelegt werden. Git-Hooks werden nicht automatisch mit einem Clone verteilt; ein versionierter Hook-Pfad plus Setup-Schritt ist daher wahrscheinlich erforderlich.
- Die verbindlichen Entwicklungsziele muessen aus den Projektdateien abgeleitet werden: Backend/Tests `net10.0`, MAUI plattformabhaengige `net10.0-*`-Targets, Visual Studio bzw. .NET CLI und NuGet inklusive lokaler Quelle.
- Es ist zu entscheiden, ob die vorhandenen tokenartigen Beispiele nur Platzhalter sind oder als kompromittiert behandelt und aus der Historie entfernt/rotiert werden muessen. Das ist vor einer oeffentlichen Freigabe kein rein redaktioneller Punkt.
- Der Linkcheck soll lokale/repositoryinterne Ziele pruefen; externe HTTP(S)-Links sollten explizit ausgeschlossen oder nur optional behandelt werden.

## Detaildokumente

- [Lizenzierung und Projektmetadaten](inventory/licensing.md)
- [Markdown-Linkcheck und Git-Hook](inventory/markdown-link-check.md)
- [Installation und Laufzeitvoraussetzungen](inventory/installation.md)
- [Veroeffentlichung und Geheimnisschutz](inventory/publication-security.md)
