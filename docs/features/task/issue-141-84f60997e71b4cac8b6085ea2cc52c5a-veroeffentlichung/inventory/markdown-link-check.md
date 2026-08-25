# Detailinventar: Markdown-Linkcheck und Git-Hook

## Bestand

- Es gibt keinen versionierten Hook-Pfad im Repository; `.git/hooks` enthaelt nur Git-Beispieldateien.
- Die CI-Workflows unter `.github/workflows/` decken Build, Tests, Formatierung, Vulnerability Scan und Release ab, aber keinen dedizierten lokalen Markdown-Linkcheck.
- Das Repository enthaelt rund 60 Markdown-Dateien ausserhalb der Lifecycle-Artefakte. README und `docs/INDEX.md` enthalten zahlreiche relative Verweise, darunter veraltete Gross-/Kleinschreibung (`Docs`/`docs`) und Links auf offenbar nicht vorhandene Dateien.
- `NuGet.config` stellt NuGet.org und `lib/packages` bereit; fuer einen reinen lokalen Linkcheck ist keine externe Paket- oder Netzwerkquelle erforderlich.

## Betroffene Flaechen

Neue Skript-/Konfigurationsdatei fuer den Check, versionierter Hook-Installationspfad, lokale Qualitaetsdokumentation und optional ein CI-Workflow-Schritt. Der Check muss Markdown-Dateien sammeln, relative Pfade normalisieren, Fragmente behandeln und bei fehlenden Dateien mit Datei und Zeile fehlschlagen.

## Design-Risiken

- Ein normaler Git-Clone verteilt `.git/hooks` nicht. Die Einrichtung muss deshalb explizit dokumentiert und reproduzierbar sein, etwa ueber `core.hooksPath`.
- Der Hook darf keine Netzwerkzugriffe fuer externe URLs benoetigen; `http`, `https`, Mailto, Anchors ohne Ziel und Codebeispiele muessen sauber klassifiziert werden.
- Windows und Linux/macOS muessen denselben Exit-Code und vergleichbare Meldungen liefern. Vorhandene Shell-Skripte sind Linux-orientiert, die lokale Umgebung ist Windows/PowerShell.
- Markdown-Syntax, URL-Encoding, Verzeichnisse, Dateien ohne Endung und case-sensitive Dateisysteme koennen unterschiedliche Ergebnisse liefern.

## Verifikation

Mindestens ein positiver Lauf, ein absichtlich fehlender relativer Link mit erwarteter Fehlermeldung und ein Test fuer externe Links ohne Netzwerkzugriff. Danach Hook-Setup in einem frischen Clone bzw. separaten Arbeitsverzeichnis pruefen.
