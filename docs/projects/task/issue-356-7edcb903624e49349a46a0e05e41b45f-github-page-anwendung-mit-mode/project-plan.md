# Projektplan: Öffentliche GitHub-Page mit Demoinstallation (Issue #356)

## Übersicht

Das Repository erhält eine statische, öffentlich erreichbare Präsentationsseite
unter `https://martin-stromberg.github.io/VideoPlayer/`, die VideoWebPlayer mit
modernem, responsivem Design (hell und dunkel) vorstellt und aus dem Repository
heraus gepflegt wird. Damit aussagekräftige Screenshots der Oberfläche entstehen
können, wird zusätzlich eine Demoinstallation geschaffen: Bei der Registrierung
des ersten Anwenders kann ein Demodatenbestand angelegt werden — eine lokale
Medienquelle mit fiktiven Filmen und Serien, einer kleinen Dummy-Videodatei und
kompakten Anzeigebildern. Betroffen sind zwei Bereiche: die Anwendung selbst
(Demodaten-Mechanismus bei der Erstregistrierung) sowie Repository-Artefakte
(Seiten-Quellen, Deployment-Workflow, Dokumentation).

## Grobe Vorgehensentscheidungen

| Bereich | Entscheidung | Begründung |
|---------|-------------|------------|
| Reihenfolge | Demoinstallation zuerst, Präsentationsseite danach | Die Seite braucht echte Screenshots aus einer laufenden Demoinstallation (belegte Vorgabe B11). Ohne Demodaten gäbe es nur Platzhalter; die Abhängigkeit ist damit eindeutig. |
| Demoinhalte | Dateiablage + echter Scan statt direkter Datenbank-Persistenz | Die bestehende Scan-/Klassifizierungs-Pipeline ist end-to-end bewiesen (lokale Quelle mit NFO-/Poster-Konventionen → Filme, Serien, Staffeln, Episoden, Anzeigebilder). Direkte Persistenz müsste das komplette Verknüpfungsmodell von Hand nachbauen und würde bei Modelländerungen still veralten. |
| Demodaten-Mechanismus | Den bestehenden, dateibasierten Demodaten-Mechanismus vervollständigen statt eines zweiten Wegs | Der Mechanismus samt Auswahl im Registrierungsformular existiert bereits, ist aber ein Stub (kein Quelltyp, keine Inhalte, kein Scan-Anstoß, keine mitgelieferten Bestände). Erweiterung ist der natürliche, vom Produkt vorgesehene Punkt. |
| Lokale Medienquelle | Kein eigener Entwicklungsschritt | `MediaSourceType.LocalDirectory` ist vollständig implementiert und released. Die Demoinstallation nutzt den vorhandenen Quelltyp lediglich. |
| Präsentationsseite | Reine statische Seite (HTML/CSS/JS ohne Generator) im Root-Ordner `pages/`, einzelne Landing-Page auf Deutsch, Deployment nur von `main` über GitHub Actions | Alle Punkte sind vom Anwender bestätigt (B5–B10). Kein Site-Generator hält neue Build-Abhängigkeiten fern; `pages/` trennt öffentliches Artefakt sauber von der internen `docs/`-Dokumentation. |
| Veröffentlichung | Seite folgt dem bestehenden Staging-Pfad (Schritt-Branch → Projekt-Basisbranch → PR gegen staging → main); kein Vorschau-Deployment | Bestätigt (B6). Die Seite geht erst bei Erreichen von `main` live — Inhalte müssen daher vor der Promotion sichtgeprüft sein. |

## Entwicklungsschritte

### Schritt 1: Demoinstallation — mitgelieferter Demodatenbestand bei der Erstregistrierung

**Beschreibung:**
Bei der Registrierung des ersten Anwenders soll ein Demodatenbestand ausgewählt
und angelegt werden können, der die Installation sofort mit fiktiven, aber
realistisch wirkenden Inhalten füllt — ohne echte Mediendaten und ohne externe
Dienste. Der bestehende Auswahldialog im Registrierungsformular bleibt
unverändert; neu ist, dass ein mitgelieferter Demodatenbestand existiert und
dass das Anlegen tatsächlich eine gefüllte Bibliothek erzeugt.

Der Demodatenbestand wird als versionierter Bestand im Repository gepflegt und
mit der Anwendung ausgeliefert, sodass ein geklontes Projekt lokal sofort eine
Demoinstallation ermöglicht. Er richtet eine Medienquelle vom Typ „lokales
Verzeichnis" ein (der Quelltyp existiert bereits im Produkt) und gewährt dem
ersten Anwender Zugriff darauf. Die fiktiven Filme und Serien entstehen über
eine mitgelieferte bzw. beim Anlegen erzeugte Verzeichnisstruktur aus
Videodateien, Metadaten-Dateien (NFO-Konventionen für Filme, Serien und
Episoden) und Anzeigebildern, die der normale Einlese- und Klassifizierungslauf
der Anwendung zu Bibliothekseinträgen verarbeitet. Nach der Registrierung müssen
die Inhalte ohne manuellen Scan und ohne Warten auf den Hintergrund-Intervall
sichtbar sein — der Scan wird beim Anlegen des Bestands angestoßen.

Als Videodatei dient eine Dummy-Videodatei, die möglichst klein ist, aber ein
echtes, im Browser abspielbares Format hat (die Wiedergabe-Ansicht soll für
Screenshots taugen, nicht nur der reine Datenstrom). Die Anzeigebilder
(Poster o. ä.) sind ebenfalls möglichst klein, aber gut genug, damit Bibliothek
und Detailansichten ansprechend und screenshot-tauglich aussehen. Der Umfang des
Bestands ist so gewählt, dass Bibliothek, Kategorien und Detailansichten gefüllt
wirken (mehrere Filme, mindestens eine Serie mit Staffel und mehreren Episoden,
mit Titeln, Beschreibungen, Genres).

Die Funktion ist abgesichert: Beim Anlegen wird der korrekte Quelltyp gesetzt,
das Demo-Medienverzeichnis samt Inhalten entsteht auf der Platte, und die
Bibliothek enthält danach die fiktiven Filme und Serien. Die
Demoinstallation ist in der Hilfe-/Projektdokumentation beschrieben (Zweck,
Inhalt, Verhalten bei der Erstregistrierung).

**Akzeptanzkriterien:**

- Beim Registrieren des ersten Anwenders erscheint der Demodatenbestand zur
  Auswahl; nach der Registrierung existiert eine Medienquelle vom Typ „lokales
  Verzeichnis" mit Zugriffsrecht für den Anwender.
- Die Bibliothek zeigt unmittelbar danach fiktive Filme und Serien (inkl.
  Staffeln/Episoden) mit Anzeigebildern — ohne manuellen Scan und ohne Warten
  auf den Hintergrund-Intervall.
- Videos des Demobestands sind über die Dummy-Videodatei im Browser abspielbar
  (echtes Containerformat, möglichst klein).
- Der Demodatenbestand liegt im Repository und wird beim lokalen Start bzw.
  der Auslieferung mitgeliefert; eine Installation ohne Auswahl bleibt
  unverändert leer.
- Neue automatisierte Tests belegen: korrekter Quelltyp, angelegtes
  Medienverzeichnis mit Inhalten, gefüllte Bibliothek nach dem Anlegen; der
  Auswahlpfad im Registrierungsformular ist abgedeckt.

**Abhängigkeiten:** Keine (fachliche Voraussetzung „lokales Verzeichnis als
Quelltyp" ist bereits umgesetzt und released).

**Betroffene Bereiche:** Demodaten-Mechanismus, Erstregistrierung,
Medienquellen, Scan-/Klassifizierungs-Pipeline, Auslieferung/Packaging,
Tests, Hilfe-Dokumentation

### Schritt 2: Öffentliche GitHub-Page — statische Präsentationsseite mit Deployment

**Beschreibung:**
Das Repository erhält eine statische, öffentlich erreichbare
Präsentationsseite unter `https://martin-stromberg.github.io/VideoPlayer/`,
die VideoWebPlayer vorstellt. Die Seite ist eine einzelne Landing-Page auf
Deutsch mit Anker-Abschnitten: prägnante Vorstellung und Zweck der Anwendung,
zentrale Features, Screenshot-Galerie, Download-/Release-Verweis (statischer
Link auf die Releases-Übersicht) sowie Verweise auf Repository und
Dokumentation. Das Design ist modern und responsiv mit klarer Typografie und
konsistentem Stil, an das Erscheinungsbild der Anwendung angelehnt, und
unterstützt helle wie dunkle Darstellung: Systemeinstellung als Standard plus
manueller Umschalter mit persistenter Auswahl.

Die Seiten-Quellen liegen in einem eigenen Root-Ordner `pages/` und werden
vollständig aus dem Repository heraus gepflegt — kein Site-Generator, keine
dynamisch erzeugten Inhalte. Alle internen Pfade sind basis-pfadfest, da die
Seite unter dem Projekt-Unterpfad `/VideoPlayer/` läuft. Logo und Favicon der
Anwendung werden in den Seiten-Ordner übernommen (das veröffentlichte Artefakt
enthält nur `pages/`). Die Screenshots in der Galerie entstehen in einer
lokalen Demoinstallation des geklonten Projekts (siehe Schritt 1) und werden
als optimierte, versionierte Bilddateien abgelegt.

Das Deployment erfolgt über einen neuen GitHub-Actions-Workflow nach GitHub
Pages (Artefakt-Deployment): Auslöser ist ein Push auf `main`, der Dateien
unter `pages/` ändert (Pfad-Filter), sowie manuelle Auslösung. Der Workflow
hält die Workflow-Konventionen des Repositories ein (minimale Berechtigungen,
Concurrency-Gruppe, Timeouts) und aktiviert GitHub Pages beim ersten Lauf
selbst; für den Fall, dass die automatische Aktivierung nicht greift, ist ein
manueller Fallback dokumentiert. Die Veröffentlichung folgt dem bestehenden
Staging-Pfad — die Seite geht erst bei Erreichen von `main` live; ein
Vorschau-Deployment gibt es nicht.

Die Pflege der Seite ist im Repository dokumentiert: Inhalte ändern,
Screenshots aus der Demoinstallation erneuern (inkl. Aufnahme-Verfahren),
Deployment-Pfad inklusive Pages-Aktivierung und Fallback; die Dokumentation
ist aus den bestehenden Doku-Einstiegspunkten verlinkt, und ein
Release-Notes-Eintrag folgt der Projektkonvention.

**Akzeptanzkriterien:**

- Die Seite ist unter `https://martin-stromberg.github.io/VideoPlayer/`
  öffentlich erreichbar (Nachweis nach Erreichen von `main`; zuvor lokale
  Sichtprüfung hell/dunkel und schmaler Viewport).
- Design responsiv und modern; helle und dunkle Darstellung, Umschalter mit
  persistenter Auswahl, Systemeinstellung als Standard.
- Alle geforderten Inhalte vorhanden: Zweck, zentrale Features,
  Screenshot-Galerie (mit echten Screenshots aus der Demoinstallation),
  statischer Release-Verweis, Verweise auf Repository und Dokumentation.
- Workflow deployt bei Push auf `main` mit Änderungen unter `pages/` sowie
  manuell; GitHub-Pages-Aktivierung ist automatisiert bzw. der manuelle
  Fallback dokumentiert.
- Pflege-Dokumentation vorhanden und verlinkt; Markdown-Link-Prüfung und
  übrige Repo-Checks bleiben grün.

**Abhängigkeiten:** 1 (die Screenshot-Galerie braucht eine befüllte
Demoinstallation als Aufnahmequelle)

**Betroffene Bereiche:** Präsentationsseite (statisches Artefakt),
CI/CD / Deployment, Dokumentation, Screenshots/Bild-Assets

## Übersichtstabelle

| # | Titel | Abhängigkeiten | Betroffene Bereiche |
|---|-------|-----------------|----------------------|
| 1 | Demoinstallation — mitgelieferter Demodatenbestand bei der Erstregistrierung | Keine | Demodaten-Mechanismus, Erstregistrierung, Medienquellen, Scan-Pipeline, Packaging, Tests, Hilfe-Doku |
| 2 | Öffentliche GitHub-Page — statische Präsentationsseite mit Deployment | 1 | Präsentationsseite, CI/CD, Dokumentation, Bild-Assets |

## Offene Punkte

| # | Offener Punkt | Empfohlener Vorschlag | Entscheidung |
|---|---------------|----------------------|--------------|
| 1 | Dummy-Videodatei: dynamisch bei der Generierung erzeugen (setzt ffmpeg voraus, das nicht gebündelt und nicht in jeder Umgebung verfügbar ist) oder vorproduziert dem Projekt beilegen? Die Vorgabe erlaubt beides, verlangt aber ein echtes abspielbares Format. | Eine minimale, vorproduzierte mp4-Datei (wenige Sekunden, h264) im Repository beilegen. Begründung: deterministisch, keine Laufzeitabhängigkeit von ffmpeg, erfüllt die Browser-Abspielbarkeit garantiert; die dynamische Variante kann später ergänzt werden, ohne den Bestand zu ändern. | Behandelt (23.11.2026): Beilegen — vom Anwender in der Anforderung ausdrücklich als Alternative genannt („oder alternativ vorab produziert dem Projekt beigelegt"). |
| 2 | Anzeigebilder: zur Laufzeit per SkiaSharp erzeugen oder als Dateien beilegen? | Zur Laufzeit erzeugen (SkiaSharp ist Projekt-Dependency, produktiver Generierungscode existiert bereits). Spart Repo-Ballast und erfüllt „möglichst klein, gut genug für Screenshots"; beilegen bleibt Fallback, falls die Qualität nicht reicht. | Behandelt (23.11.2026): Laufzeit-Erzeugung per SkiaSharp; die Anforderung lässt die Herstellung offen und verlangt nur „möglichst klein, gut genug". |
| 3 | Umfang des Demodatenbestands (Anzahl Filme/Serien/Episoden) ist nicht festgelegt. | Vorschlag: 2–3 Filme und 1–2 Serien mit je einer Staffel à 2–3 Episoden — genug, um Bibliothek, Kategorien und Detailansichten für Screenshots zu füllen, ohne das Repository aufzublähen. | Behandelt (23.11.2026): 2–3 Filme + 1–2 Serien à 2–3 Episoden — erfüllt die Anforderung „Filme und Serien mit fiktiven Angaben" minimal. |
| 4 | Fehler beim Anlegen der Demodaten: Der erste Benutzer ist dann bereits registriert, die Demodaten aber nicht vorhanden (kein Rollback im Bestand). | Das bestehende Fehlerverhalten beibehalten (Fehlermeldung im Formular, Benutzer bleibt angelegt); ein erneuter Anlege-Versuch ist über die vorhandenen Mechanismen möglich. Atomare Registrierung wäre eine separate Verbesserung außerhalb dieses Projekts. | Behandelt (23.11.2026): Bestehendes Fehlerverhalten beibehalten; Atomarität ist kein Teil der Anforderung. |
