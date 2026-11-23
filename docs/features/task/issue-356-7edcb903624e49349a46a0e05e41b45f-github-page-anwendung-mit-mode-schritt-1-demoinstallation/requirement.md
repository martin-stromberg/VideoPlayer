# Übersetzte Anforderung: Demoinstallation — mitgelieferter Demodatenbestand bei der Erstregistrierung

**Kontext:** Entwicklungsschritt 1 des Projektplans Issue #356
(`docs/projects/task/issue-356-…/project-plan.md`).
**Branch:** `task/issue-356-…-github-page-anwendung-mit-mode-schritt-1-demoinstallation`
**Basisbranch:** `task/issue-356-7edcb903624e49349a46a0e05e41b45f-github-page-anwendung-mit-mode`

---

## Fachliche Zusammenfassung

Bei der Registrierung des ersten Anwenders kann ein mitgelieferter Demodatenbestand
ausgewählt und angelegt werden. Der bestehende Auswahldialog in `Register.razor`
bleibt unverändert; neu ist, dass der Bestand im Repository versioniert mitgeliefert
wird und das Anlegen eine echte, gefüllte Bibliothek erzeugt: eine Medienquelle vom
Typ `LocalDirectory`, ein auf der Platte materialisiertes Demo-Medienverzeichnis
(Dummy-Videodatei, NFO-Metadaten, Anzeigebilder) und ein beim Anlegen angestoßener
Scan, der die Inhalte ohne Warten auf den Hintergrund-Intervall in die Bibliothek
überführt.

## Fachlicher Scope

- **Verarbeitungseinheit:** Der **Demodatenbestand** — ein versioniertes
  Definitionspaket (JSON-Definition + Medien-Template-Struktur + Dummy-Video) unter
  `DemoDataSets/`, das per `IDemoDataSetService.ApplyAsync` auf eine frische
  Installation angewendet wird.
- **Auslösende Ereignisse:** Registrierung des ersten Benutzers über
  `/Account/Register` mit ausgewähltem Demodatenbestand
  (`InputModel.DemoDataSetId`); `isFirstUser` ist wahr.
- **Erwartete Wirkung:**
  - Eine `MediaSource` mit `SourceType = LocalDirectory` und absolutem Pfad auf das
    materialisierte Demo-Medienverzeichnis existiert; `MediaSourceUser` gewährt dem
    ersten Anwender Zugriff.
  - Das Demo-Medienverzeichnis samt Videodateien, NFO-Dateien und Anzeigebildern
    liegt auf der Platte.
  - Die Bibliothek zeigt unmittelbar nach der Registrierung fiktive Filme und
    Serien inkl. Staffeln/Episoden mit Anzeigebildern — ohne manuellen Scan und
    ohne Warten auf `MediaSourceScanService`-Intervall (Scan wird von
    `ApplyAsync` angestoßen).
  - Die Dummy-Videodatei ist ein echtes, im Browser abspielbares Containerformat
    (h264/mp4), möglichst klein; Wiedergabe-Ansicht ist screenshot-tauglich.
  - Installation ohne Auswahl bleibt leer (bestehendes Verhalten).
- **Umgang mit fehlenden Objekten:**
  - `DemoDataSets/`-Ordner samt Bestand existiert nicht → **erzeugen**
    (JSON-Definition + Template-Verzeichnisbaum + Dummy-Video-Datei im Repo).
  - `SourceType`-Feld in `DemoMediaSourceDefinition` fehlt → **erzeugen**
    (Erweiterung des JSON-Formats; `LocalDirectory` unterstützen).
  - Demo-Medienverzeichnis auf der Zielplatte existiert nicht → **erzeugen**
    beim Anlegen (Materialisierung der Template-Struktur + Generierung der
    Anzeigebilder).
  - Dummy-Videodatei existiert nicht → **erzeugen** (vorproduziert, im Repo
    beigelegt — Festlegung 1).
  - Anzeigebilder existieren nicht → **erzeugen** zur Laufzeit per SkiaSharp
    beim Anlegen (Festlegung 2).
  - Scan-Trigger nach dem Anlegen fehlt → **erzeugen** (Anstoßen von
    `MediaSourceScanCoordinator.RunFullScanAsync` in `ApplyAsync`).
  - `MediaSourceUser`-Zuordnung existiert nicht → **erzeugen** (bestehender
    Mechanismus in `ApplyAsync`).
- **Scope-Ausschlüsse:**
  - Keine Atomarität mit der Benutzeranlage: schlägt `ApplyAsync` fehl, bleibt
    der Benutzer angelegt und eine Fehlermeldung erscheint im Formular
    (bestätigt — Festlegung 4, Anwender-Anforderung Issue #356).
  - Kein neuer Quelltyp: `MediaSourceType.LocalDirectory` wird nur verwendet
    (bestätigt — Festlegung 5, Anwender-Anforderung Issue #356).
  - Keine echten Mediendaten, keine externen Metadaten-Dienste (TMDB bleibt
    deaktiviert) — alle Inhalte sind fiktiv und lokal (bestätigt —
    Anwender-Anforderung, Issue #356).
  - Keine Änderung am Auswahldialog/`Register.razor`-Interaktionsmodell; die
    Auswahl bleibt rein dem ersten Benutzer vorbehalten (bestätigt —
    Anwender-Anforderung, Issue #356).
  - Kein Quellen-Icon (`DemoMediaSourceDefinition.Icon` bleibt ungenutzt) —
    von der Anforderung nicht gefordert (Annahme, dokumentiert).
  - Kein Lösch-/Deinstallationspfad für den Demodatenbestand — die Quelle ist
    danach eine normale lokale Medienquelle und über den Bestand verwaltbar
    (Annahme, dokumentiert).

## Betroffene Klassen und Komponenten

- `VideoWebPlayer/Services/DemoData/FileSystemDemoDataSetService.cs` —
  Erweiterung: `SourceType` aus der Definition setzen, Template-Verzeichnis
  materialisieren, Anzeigebilder generieren, Scan anstoßen.
- `VideoWebPlayer/Services/DemoData/` — neue Hilfsklasse(n) für
  Verzeichnis-Materialisierung und Bildgenerierung (SkiaSharp).
- `VideoWebPlayer/DemoDataSets/` (neu) — versionierter Bestand: JSON-Definition,
  Medien-Template (NFO-Dateien), Dummy-Videodatei.
- `VideoWebPlayer/VideoWebPlayer.csproj` — Copy-Regel erweitern, damit
  `DemoDataSets/` inkl. Unterverzeichnisse (mp4, nfo) in Output und Publish
  gelangt.
- `MediaSourceScanCoordinator` — wird von `ApplyAsync` für den Sofort-Scan
  verwendet (nur Aufruf, keine Änderung).
- `Register.razor` — unverändert (Auswahldialog existiert).
- `VideoWebPlayer.Tests/` — neue Tests:
  Service-/E2E-Tests für `ApplyAsync` (Quelltyp, Medienverzeichnis, gefüllte
  Bibliothek) und HTTP-E2E für den Registrierungs-Auswahlpfad.
- `docs/help/` — Dokumentation der Demoinstallation.

## Implementierungsansatz

- Erweiterung des bestehenden JSON-Definitionsformats
  (`DemoDataSetDefinition`/`DemoMediaSourceDefinition`) um `sourceType` und eine
  Inhaltsangabe (Template-Verzeichnis + Videodatei relativ zum Bestand).
- `ApplyAsync`: Template-Struktur aus dem Bestand in ein schreibbares
  Zielverzeichnis kopieren (`Data/DemoMedia/…` unter `ContentRootPath`, analog
  zur DB-Ablage `Data/WebVideoPlayer.db`), Videodateien aus dem beigelegten
  Dummy-Video erzeugen, Anzeigebilder per SkiaSharp generieren, `MediaSource`
  mit `SourceType = LocalDirectory` + absolutem Pfad anlegen,
  `MediaSourceUser` verknüpfen, danach
  `MediaSourceScanCoordinator.RunFullScanAsync` anstoßen.
- Der normale Scan-/Klassifizierungs-Lauf (`MediaSourceScanner` +
  `MediaSourceClassifier` über `LocalMediaSourceReader`) erzeugt aus den
  NFO-/Bild-Konventionen die `Movie`/`TVShow`/`TVShowSeason`/`TVShowEpisode`-
  und `Picture`-Einträge — nachweisbar derselbe Pfad wie
  `LocalMediaSourcePipelineE2ETests`.

## Konfiguration

Keine neue Laufzeitkonfiguration. Ablageort der Bestände folgt der bestehenden
Konvention (`DemoDataSets/` unter `ContentRootPath`/`AppContext.BaseDirectory`);
das materialisierte Medienverzeichnis liegt unter `Data/DemoMedia/`.

## Offene Fragen

Keine — die Projekt-Festlegungen (Dummy-Video beilegen, Bilder per SkiaSharp,
Umfang 2–3 Filme + 1–2 Serien à 1 Staffel à 2–3 Episoden, Fehlerverhalten
beibehalten, `LocalDirectory` wiederverwenden) decken alle ableitbaren Punkte ab.

## Akzeptanzkriterien

- AK-1: Beim Registrieren des ersten Anwenders erscheint der Demodatenbestand
  zur Auswahl; nach der Registrierung existiert eine Medienquelle vom Typ
  `LocalDirectory` mit Zugriffsrecht (`MediaSourceUser`) für den Anwender.
- AK-2: Die Bibliothek zeigt unmittelbar danach fiktive Filme und Serien (inkl.
  Staffeln/Episoden) mit Anzeigebildern — ohne manuellen Scan und ohne Warten
  auf den Hintergrund-Intervall.
- AK-3: Videos des Demobestands sind über die Dummy-Videodatei im Browser
  abspielbar (echtes Containerformat, möglichst klein).
- AK-4: Der Demodatenbestand liegt im Repository und wird beim lokalen Start
  bzw. der Auslieferung mitgeliefert; eine Installation ohne Auswahl bleibt
  unverändert leer.
- AK-5: Neue automatisierte Tests belegen: korrekter Quelltyp, angelegtes
  Medienverzeichnis mit Inhalten, gefüllte Bibliothek nach dem Anlegen; der
  Auswahlpfad im Registrierungsformular ist abgedeckt.
