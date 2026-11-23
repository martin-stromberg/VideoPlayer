# Übersetzte Anforderung: Öffentliche GitHub-Page mit moderner Präsentation und Demoinstallation (Issue #356)

**Aufgaben-ID:** 7edcb903-624e-4934-9a46-a0e05e41b45f
**Branch:** task/issue-356-7edcb903624e49349a46a0e05e41b45f-github-page-anwendung-mit-mode
**Issue:** martin-stromberg/VideoPlayer #356 (Original-Issue + verbindlicher Anwender-Kommentar)
**Quellen:** `issue.md`, `docs/features/task/issue-356-…/requirement.md`, `…/plan.md`, `…/inventory.md` inkl. `inventory/`

---

## 1. Ziel

Das Repository erhält eine statische, öffentlich erreichbare Präsentationsseite unter `https://martin-stromberg.github.io/VideoPlayer/`, die VideoWebPlayer mit modernem, responsivem Design (hell und dunkel) vorstellt und aus dem Repository heraus gepflegt wird. Damit aussagekräftige Screenshots der Oberfläche entstehen können, wird zusätzlich eine **Demoinstallation** gefordert: Bei der Registrierung des ersten Anwenders sollen Demodaten generiert werden können — eine lokale Medienquelle mit fiktiven Filmen und Serien, einer kleinen Dummy-Videodatei und kleinen, aber für Screenshots ausreichenden Anzeigebildern. Die Screenshots der Seite entstehen in einer solchen lokalen Demoinstallation des geklonten Projekts.

## 2. Fachliche Zusammenfassung

Die Anforderung hat zwei fachliche Teile:

- **Teil A — Präsentationsseite (Repository-Artefakt + CI/CD):** Eine eigenständige statische Website (HTML/CSS/JS, keine C#-Änderung an der Seitenlogik), versioniert im neuen Root-Ordner `pages/`, ausgeliefert über einen neuen GitHub-Actions-Workflow nach GitHub Pages bei Push auf `main`. Inhalte: Zweck der Anwendung, zentrale Features, Screenshot-Galerie, Download-/Release-Verweis, Verweise auf Repository und Dokumentation.
- **Teil B — Demoinstallation (Anwendungscode):** Beim Registrieren des ersten Anwenders kann ein Demodatenbestand angelegt werden. Der Bestand richtet eine Medienquelle vom Typ `LocalDirectory` ein, hinterlegt eine minimale Videodatei (dynamisch erzeugt, falls möglich; sonst beigelegt) sowie kleine Anzeigebilder und fiktive Film-/Serienangaben, sodass die Bibliothek gefüllt und screenshot-tauglich aussieht.

Wichtiger Befund aus der Vorarbeit zum Scope: Die im Issue-Kommentar genannte Voranforderung „lokales Verzeichnis als Quelle" ist **bereits umgesetzt und released** (`MediaSourceType.LocalDirectory`, Commit `b592bce`, siehe Abschnitt 5.2). Neu ist daher nur der Demodatenbestand selbst und die dafür nötige Erweiterung des bestehenden Demodaten-Mechanismus.

## 3. Belegte Vorgaben (vom Anwender bestätigt — verbindlich)

Aus dem Issue-Text, dem verbindlichen Issue-Kommentar und den per „Folge den Empfehlungen" bestätigten Plan-Entscheidungen:

| # | Vorgabe | Quelle |
|---|---------|--------|
| B1 | Statische Seite, ausgeliefert über GitHub Pages; Deployment passend zu den bestehenden CI/CD-Workflows | Issue |
| B2 | Modernes, responsives Design, klare Typografie, konsistenter Stil; helle und dunkle Darstellung; an das Erscheinungsbild der Anwendung angelehnt | Issue |
| B3 | Inhalte: prägnante Vorstellung, zentrale Features, Screenshots, Download-/Release-Verweise, Verweise auf Repository und Dokumentation | Issue |
| B4 | Inhalte und Screenshots werden aus dem Repository heraus gepflegt | Issue |
| B5 | Seiten-Quellen liegen in einem eigenen Root-Ordner `pages/` | bestätigt |
| B6 | Deployment nur von `main` (kein Staging-Vorschau-Deployment); Veröffentlichung folgt dem bestehenden Staging-Pfad | bestätigt |
| B7 | Sprache der Seite: Deutsch | bestätigt |
| B8 | Umfang: einzelne Landing-Page mit Anker-Abschnitten | bestätigt |
| B9 | Release-Verweis: statischer Link auf die Releases-Übersicht | bestätigt |
| B10 | Hell/Dunkel: `prefers-color-scheme` plus manueller Umschalter mit `localStorage` | bestätigt |
| B11 | Screenshots entstehen in einer lokalen Ausführung des geklonten Projekts — konkret: in der Demoinstallation | Issue-Kommentar |
| B12 | Demodaten werden mit der Registrierung des ersten Anwenders generiert | Issue-Kommentar |
| B13 | Der Demodatenbestand richtet eine lokale Datenquelle (Verzeichnispfad) ein | Issue-Kommentar |
| B14 | Filme und Serien werden mit fiktiven Angaben eingerichtet | Issue-Kommentar |
| B15 | Als Videodatei dient eine Dummy-Videodatei — falls möglich bei der Generierung dynamisch produziert, alternativ vorab produziert dem Projekt beigelegt; möglichst klein | Issue-Kommentar |
| B16 | Anzeigebilder für Filme und Serien möglichst klein, aber gut genug für Screenshots | Issue-Kommentar |
| B17 | Lokales Verzeichnis als Quellentyp einrichtbar (neben SFTP) — **im Code bereits erfüllt**, siehe 5.2 | Issue-Kommentar + Code-Befund |

## 4. Fachlicher Scope

- **Verarbeitungseinheiten:**
  1. Die **Projektseite** — eigenständiger statischer Website-Quellordner `pages/` (HTML, CSS, JS, Bild-Assets inkl. Screenshots) plus Deployment-Pfad über GitHub Actions nach GitHub Pages plus Pflege-Dokumentation. Die bestehende `docs/`-Dokumentation ist kein veröffentlichter Seiteninhalt.
  2. Die **Demoinstallation** — der Demodatenbestand, der beim Registrieren des ersten Anwenders erzeugt wird: lokale Medienquelle, Demo-Medienverzeichnis mit Dummy-Videodatei(en) und Anzeigebildern, fiktive Film-/Serien-Einträge (sichtbar nach Einlesen/Klassifizierung).
- **Auslösende Ereignisse:**
  - Seite: `push` auf `main` mit Änderungen unter `pages/` (über den Staging-Pfad Feature → staging → main) sowie manueller `workflow_dispatch`.
  - Demodaten: Registrierung des ersten Benutzers (`Register.razor`, `isFirstUser`), bei Auswahl des Demodatenbestands im Registrierungsformular.
- **Erwartete Wirkung:**
  - Öffentlich erreichbare, responsive Seite unter `https://martin-stromberg.github.io/VideoPlayer/` in heller und dunkler Darstellung, angelehnt an die `--vp-*`-Farbwelt (Primärrot `#e50914`, Sekundär `#00a8e8`, Inter/Montserrat-Stack), mit allen geforderten Inhalten.
  - Frisch registrierte Erstinstallation kann mit Demodaten befüllt werden; die Bibliothek zeigt fiktive Filme und Serien mit Anzeigebildern, abspielbar über die Dummy-Videodatei — Screenshot-tauglich ohne echte Mediendaten.
- **Umgang mit fehlenden Objekten:**
  - GitHub-Pages-Aktivierung (per `gh api` verifiziert **nicht** gesetzt, `has_pages: false`): **aktivieren** — primär `actions/configure-pages` (`enablement`), Fallback manuell durch den Eigentümer (Settings → Pages → Source: GitHub Actions); externe Voraussetzung.
  - `github-pages`-Environment: wird von `actions/deploy-pages` selbst **erzeugt** — keine Vorarbeit.
  - Screenshots: existieren nicht — **erzeugen** in der Demoinstallation, als versionierte Bilddateien unter `pages/assets/` ablegen.
  - `DemoDataSets`-Ordner mit Inhalt: existiert nicht — **erzeugen** (JSON-Definition + Medienverzeichnis). Ohne Ordner/Inhalt zeigt `FileSystemDemoDataSetService.GetAvailableAsync()` eine leere Liste und das Registrierungsformular blendet die Auswahl aus (`Register.razor` Zeilen 47–59, 80–85).
  - Dummy-Videodatei und Anzeigebilder: existieren nicht — **erzeugen** (dynamisch bei Generierung bevorzugt, sonst beigelegt; B15/B16).
  - Logo/Favicon für die Seite: **wiederverwenden** (`Images/app.png`, `VideoWebPlayer/wwwroot/favicon.png` — in `pages/` kopieren, da das Pages-Artefakt nur diesen Ordner enthält).
- **Scope-Ausschlüsse:** siehe Abschnitt 11 (explizite Nicht-Ziele).

## 5. Fachliche Anforderungen

### 5.1 Teil A — Präsentationsseite (belegt)

- F-A1: Eine statische Landing-Page (Deutsch) mit Anker-Abschnitten: Vorstellung/Zweck der Anwendung, zentrale Features, Screenshot-Galerie, Download-/Release-Verweis (statischer Link auf `https://github.com/martin-stromberg/VideoPlayer/releases`), Verweise auf Repository und Dokumentation.
- F-A2: Responsives, modernes Design mit klarer Typografie und konsistentem Stil; helle und dunkle Darstellung; angelehnt an das Erscheinungsbild der Anwendung (`--vp-*`-Palette, `#e50914`/`#00a8e8`, Inter/Montserrat-`font-family`-Stack).
- F-A3: Theme-Umschaltung: `prefers-color-scheme` als Default plus manueller Umschalter mit `localStorage`-Persistenz.
- F-A4: Seiten-Quellen unter `pages/`; alle internen Pfade relativ bzw. basis-pfadfest (Seite läuft unter `/VideoPlayer/`).
- F-A5: Deployment über GitHub Actions (Pages-Artefakt-Deployment), Trigger `push` auf `main` mit `paths`-Filter auf `pages/` plus `workflow_dispatch`; Workflow-Konventionen des Repos übernehmen (`permissions`-Block minimal: `contents: read`, `pages: write`, `id-token: write`; `concurrency`-Gruppe; `timeout-minutes`).
- F-A6: Die Pflege der Seite (Inhalte ändern, Screenshots erneuern, Deployment-Pfad inkl. manuellem Fallback für die Pages-Aktivierung) ist im Repository dokumentiert (z. B. `docs/GITHUB_PAGE.md`), verlinkt aus `docs/INDEX.md` und `README.md`; Eintrag in `docs/RELEASE_NOTES.md` gemäß Projektkonvention.

### 5.2 Teil B — Lokales Verzeichnis als Medienquelle (bereits erfüllt — kein neuer Umsetzungsbedarf)

- F-B1 (Issue-Kommentar): „Es soll auch möglich sein, einen lokalen Verzeichnispfad einzurichten."
- **Nachweis im Code (bereits erfüllt):** `MediaSourceType.LocalDirectory` (`VideoWebPlayer/Data/MediaSourceType.cs`), `LocalMediaSourceReader`, `MediaSourceReaderDispatcher` (`VideoWebPlayer/Services/`), Admin-UI mit Quelltyp-Auswahl (`MediaSourceAdminDetails.razor`, `MediaSourceAdmin.razor`), Migration `AddMediaSourceSourceType`, Backup-Eintrag in `VideoWebPlayerBackupData` (`OptionalRestoreColumns`), Unit- und E2E-Tests (`LocalMediaSourceReaderTests`, `MediaSourceLocalDirectoryE2ETests`, `LocalMediaSourceStreamingE2ETests`), Release-Notes-Eintrag (`docs/RELEASE_NOTES.md`, „Medienquellen unterstützen jetzt lokale Verzeichnisse"), Commit `b592bce`.
- **Konsequenz:** Die Anforderung ist im Ist-Stand abgedeckt; sie bleibt als fachliche Voraussetzung für die lokale Demo-Datenquelle relevant (F-C1). Neu aufgenommen wird sie nur, um den Bezug zu Teil C herzustellen — es ist kein neuer Quelltyp zu bauen.

### 5.3 Teil C — Demoinstallation (belegt)

- F-C1: Beim Registrieren des ersten Anwenders kann ein Demodatenbestand angelegt werden (Auslöser: Registrierungsformular; Mechanismus `IDemoDataSetService` existiert bereits — `Register.razor` Zeilen 18, 47–59, 84, 111–123).
- F-C2: Der Demodatenbestand richtet eine Medienquelle vom Typ `LocalDirectory` (Verzeichnispfad) ein und gewährt dem ersten Anwender Zugriff (`MediaSourceUser`).
- F-C3: Filme und Serien werden mit fiktiven Angaben eingerichtet (Titel, Beschreibungen, Staffeln/Episoden etc. — Umfang so, dass Bibliothek und Detailansichten gefüllt und screenshot-tauglich wirken).
- F-C4: Als Videodatei wird eine Dummy-Videodatei hinterlegt — vorzugsweise bei der Generierung dynamisch produziert, alternativ vorab produziert und dem Projekt beigelegt; möglichst klein.
- F-C5: Anzeigebilder für Filme und Serien sind möglichst klein, aber ausreichend gut für Screenshots.
- F-C6: Die Demoinstallation entsteht aus der lokalen Ausführung des geklonten Projekts heraus — kein externer Dienst, keine echten Mediendaten nötig.

## 6. Abläufe

### Ablauf 1: Veröffentlichung der Seite

1. Änderung an `pages/` gelangt über den Staging-Pfad (Feature-Branch → `staging` → Promotion) auf `main`.
2. `push`-Trigger (mit `paths`-Filter) startet den Pages-Workflow: `checkout` → `configure-pages` (aktiviert Pages beim ersten Lauf) → `upload-pages-artifact` (Ordner `pages/`) → `deploy-pages` (Environment `github-pages`).
3. Seite ist unter `https://martin-stromberg.github.io/VideoPlayer/` erreichbar. `workflow_dispatch` erlaubt manuelle Neuveröffentlichung.

### Ablauf 2: Erstregistrierung mit Demodaten

1. Erster Benutzer öffnet `/Account/Register`; `isFirstUser` ist wahr, `GetAvailableAsync()` liefert den Demodatenbestand → Auswahlfeld wird angezeigt.
2. Benutzer registriert sich mit ausgewähltem Demodatenbestand → Benutzer wird Admin (`IsAdmin = true`), `ApplyAsync` legt Quelle, Medienverzeichnis-Bezug und Inhalte an.
3. Ergebnis: lokale Medienquelle eingerichtet und dem Benutzer freigegeben; Bibliothek zeigt fiktive Filme/Serien mit Anzeigebildern; Videos sind über die Dummy-Datei abspielbar.

### Ablauf 3: Screenshot-Produktion und Seitenpflege

1. Geklontes Projekt lokal ausführen, Erstregistrierung mit Demodatenbestand → Demoinstallation steht.
2. Screenshots der vereinbarten Ansichten aufnehmen, optimiert unter `pages/assets/screenshots/` ablegen, Galerie in `index.html` verdrahten.
3. Inhaltspflege (Texte, Features, Screenshots) erfolgt per Commit über den Staging-Pfad; das Verfahren ist in der Pflege-Dokumentation beschrieben.

## 7. Betroffene Bereiche

### Neue / geänderte Repository-Artefakte (Teil A)

- `pages/` (neu): `index.html`, `assets/css/site.css`, `assets/js/theme.js`, `assets/img/` (Logo-/Favicon-Kopien), `assets/screenshots/`.
- `.github/workflows/pages.yml` (neu): Pages-Build/Deploy.
- Dokumentation: `docs/GITHUB_PAGE.md` (neu), Einträge/Verweise in `docs/INDEX.md`, `README.md`, `docs/STAGING_WORKFLOW.md` („CI Workflow Files"), `docs/RELEASE_NOTES.md`.
- Keine Site-Tests im Solution-Scope; Absicherung über `MarkdownLinkCheck` (pre-commit + CI), Workflow-Lauf und Sichtprüfung.

### Betroffene Anwendungsteile (Teil C)

- `VideoWebPlayer/Services/DemoData/` — `IDemoDataSetService` / `FileSystemDemoDataSetService` erweitern: Der aktuelle `ApplyAsync` legt nur `MediaSource`- und `MediaSourceUser`-Datensätze an, setzt **kein `SourceType`** (würde `Sftp`-Default erhalten), erzeugt keine Dateien/Inhalte. Für F-C1–F-C5 ist eine Erweiterung nötig (Quelltyp `LocalDirectory`, Demo-Medienverzeichnis, Videodatei, Anzeigebilder, fiktive Film-/Serien-Daten bzw. eine Dateistruktur, die Scan/Klassifizierung zu fiktiven Titeln führt).
- `DemoDataSets/` (neu, ContentRoot/BaseDirectory — Ablageort gemäß `FileSystemDemoDataSetService.GetDemoDataFolderPath`): JSON-Definition des Demodatenbestands plus Medienassets (Dummy-Video, Anzeigebilder, ggf. NFO-Struktur).
- `Register.razor`: Auswahldialog existiert bereits; ggf. nur inhaltliche Anpassungen (Anzeigename/Beschreibung des Bestands).
- Ggf. `VideoWebPlayer.csproj`/Publish-Profil: damit `DemoDataSets/` in die lokale Ausführung bzw. Auslieferung gelangt.
- Abhängig von der Generierungs-Variante: FFmpeg-Integration (`MediaProbe:FfmpegPath`, `tools/ffmpeg/`, `PATH`) für dynamische Videoproduktion — `ffmpeg` wird nicht mitgeliefert.
- Tests: neue Unit-/Integrationstests für die Demodaten-Generierung (Quelltyp korrekt `LocalDirectory`, Inhalte angelegt, Pfad-Validierung); E2E-Abdeckung des Registrierungs-Auswahlpfads prüfen/ergänzen.
- Help-/Dokumentation (`docs/help/`, `README.md`): Demoinstallation dokumentieren.

## 8. Implementierungsansatz (Ableitung aus Vorarbeit; als Annahmen markiert)

- **Seite:** reine HTML/CSS-Umsetzung ohne Generator; CSS-Variablen spiegeln die `--vp-*`-Tokens aus `VideoWebPlayer/wwwroot/app.css`; helle Palette wird neu abgeleitet (Anwendung ist ausschließlich dunkel — keine Referenzpalette); gleicher `font-family`-Stack ohne externe Webfont-Einbindung; kleines `theme.js` für `data-theme`/`localStorage`.
- **Demodaten:** Das bestehende JSON-Definitionsformat (`DemoDataSetDefinition`/`DemoMediaSourceDefinition` in `FileSystemDemoDataSetService`) bietet den natürlichen Erweiterungspunkt — *Annahme*: Erweiterung um Quelltyp, Medienverzeichnis-Ablage und Inhalte statt eines zweiten Mechanismus.
- **Inhaltserzeugung — zwei naheliegende Varianten, offen:**
  - (a) Dateiablage: Demo-Medienverzeichnis mit Dummy-Video, NFO-Dateien und Anzeigebildern; die bestehende Scan-/Klassifizierungs-Pipeline (`MediaSourceScanner`/`MediaSourceClassifier` über `IMediaSourceReader`/`LocalMediaSourceReader`) erzeugt die fiktiven Filme/Serien daraus.
  - (b) Direkte Persistenz: Demodaten-Service schreibt `Movie`/`TVShow`/`TVShowSeason`/`TVShowEpisode`/`Picture`-Entitäten direkt.
- **Dummy-Video:** dynamische Produktion setzt `ffmpeg` voraus (Konventionen: `MediaProbe:FfmpegPath`, `tools/ffmpeg/`, `PATH`); ohne `ffmpeg` greift die Alternative „vorproduzierte Datei beilegen" (B15 lässt beides zu).

## 9. Konfiguration

- Keine neue Laufzeitkonfiguration für die Seite; Inhalte liegen versioniert unter `pages/` und werden per Commit/Staging-Pfad veröffentlicht.
- Externe Einstellung: GitHub-Pages-Quelle auf Repository-Ebene (Settings → Pages → Source: „GitHub Actions"), primär automatisch via `actions/configure-pages`.
- Demodaten: Auswahl pro Installation beim Registrierungsformular (keine Appsettings-Anforderung genannt); Ablageort der Definitionen gemäß bestehender Service-Konvention (`DemoDataSets/` unter ContentRoot/BaseDirectory) — *Annahme*.

## 10. Akzeptanzkriterien

Belegt aus dem Issue (Teil A) bzw. abgeleitet aus dem verbindlichen Kommentar (Teil C):

- AK-1: Die Page ist unter `https://martin-stromberg.github.io/VideoPlayer/` öffentlich erreichbar.
- AK-2: Das Design ist responsiv, modern und präsentiert die Anwendung ansprechend (hell und dunkel; Umschalter persistent).
- AK-3: Zweck, zentrale Features und Download-/Release-Verweis sind auf der Page vorhanden (sowie Screenshots, Repository- und Dokumentationsverweise).
- AK-4: Die Aktualisierung der Seite (Workflow bzw. Pflege) ist im Repository dokumentiert.
- AK-5 (Teil C, abgeleitet): Bei der Registrierung des ersten Anwenders kann ein Demodatenbestand erzeugt werden; danach existiert eine lokale Medienquelle mit fiktiven Filmen und Serien inkl. Anzeigebildern, deren Videos über die Dummy-Videodatei abspielbar sind.
- AK-6 (Teil C, abgeleitet): Screenshots für die Page werden aus dieser Demoinstallation gewonnen; Dummy-Video und Anzeigebilder sind möglichst klein.

## 11. Explizite Nicht-Ziele (Scope-Ausschlüsse)

Bestätigt bzw. direkt aus der Anforderung ableitbar:

- Keine Veröffentlichung der `docs/`-Dokumentation als Website — die Präsentationsseite ist ein eigenes Artefakt, kein Docs-Portal (bestätigt).
- Kein dynamischer Seiteninhalt: keine Build-/Laufzeit-Generierung von Release-Versionen, Feature-Listen oder Screenshots; keine clientseitige GitHub-API-Anzeige der aktuellen Version (bestätigt).
- Keine Custom Domain, kein Suchindex, kein Mehrsprachigkeits-Konzept (bestätigt; Sprache Deutsch).
- Kein Site-Generator-Tooling (kein Jekyll/SSG; bestätigt).
- Keine Änderung am Release-Prozess — die Seite verlinkt auf Releases, erzeugt sie nicht (bestätigt).
- Kein Staging-Vorschau-Deployment der Seite (bestätigt: nur `main`).
- Keine Unterseiten — einzelne Landing-Page mit Anker-Abschnitten (bestätigt).
- Keine neuen SFTP-/Quelltyp-Funktionen — `LocalDirectory` existiert bereits (Code-Befund, 5.2).
- Keine echten Mediendaten im Repository; Demoinhalte sind fiktiv und minimal gehalten (aus B14–B16 abgeleitet).
- Keine Änderung am Registrierungs-/Rechtemodell selbst — die Erstbenutzer-Logik (`IsAdmin`, Demo-Auswahl nur für ersten Benutzer) bleibt wie bestehend (aus B12 abgeleitet).

## 12. Annahmen (nicht belegt, plausibilisiert)

- A-1: Der bestehende `IDemoDataSetService`-Mechanismus (JSON-Definition + `ApplyAsync`) ist der vorgesehene Erweiterungspunkt für die Demoinstallation — er deckt aktuell nur `MediaSource`-Anlage ohne `SourceType` und ohne Inhalte ab und ist daher zu erweitern.
- A-2: Der Demodatenbestand wird als mitgelieferter Bestand im Repository gepflegt (Ordner `DemoDataSets/` mit JSON + Assets), sodass ein geklontes Projekt lokal sofort eine Demoinstallation ermöglicht (B11/B12 legen das nahe).
- A-3: Die fiktiven Film-/Serien-Daten werden so angelegt, dass sie der normalen Verarbeitung standhalten (Scan/Klassifizierung bzw. direkte Persistenz) und keine echten Metadaten-Dienste (TMDB o. ä.) erfordern.
- A-4: Workflow-Konventionen werden übernommen: Name/Header-Kommentar, `concurrency`-Gruppe mit `cancel-in-progress: false`, `timeout-minutes`, `ubuntu-latest`, Actions-Major-Versionen passend zur Bestandslinie.
- A-5: Die Pages-Aktivierung kann durch `actions/configure-pages` automatisiert werden; ist das nicht möglich, ist der manuelle Fallback dokumentiert und erfolgt durch den Eigentümer.
- A-6: Testnachweise folgen der DoD (Backup-Kompatibilität neuer Spalten/Felder, echte SQLite-Tests für Constraints, serverseitige Berechtigung) — soweit neue Persistenz entsteht.
- A-7: Da kein eigenes Test-Framework für statische Seiten existiert, ist für Teil A keine neue Testsuite vorgesehen; Verifikation über Workflow-Lauf, Erreichbarkeits- und Sichtprüfung (wie im bisherigen Plan vorgesehen).

## 13. Offene Fragen

- O-1: **Variante der Inhaltserzeugung:** Dateiablage mit Scan/Klassifizierung (a) oder direkte DB-Persistenz der Film-/Serien-Entitäten (b)? Variante (a) nutzt die produktive Pipeline (näher an echten Screenshots), erzeugt aber Laufzeit-Abhängigkeiten (Scan muss nach Registrierung laufen); Variante (b) ist deterministischer. Zu entscheiden.
- O-2: **Dummy-Videodatei — dynamisch oder beigelegt?** Dynamische Produktion benötigt `ffmpeg` zur Laufzeit der Generierung (im Dev-Clone vorhanden? via `MediaProbe:FfmpegPath`/`tools/ffmpeg/`/`PATH`); die beigelegte Variante vergrößert das Repo minimal. B15 erlaubt beides — Entscheidung mit Blick auf Verfügbarkeit und Reproduzierbarkeit.
- O-3: **Ablageort und Pfad-Semantik der Demo-Medienquelle:** Unter `DemoDataSets/<id>/media` neben der Definition? `MediaSource.Path` benötigt bei `LocalDirectory` einen gültigen Serverpfad — relativ zum ContentRoot auflösen oder absolut erzeugen? (Bei `LocalDirectory` werden OS-Pfade persistiert.)
- O-4: **Auslieferung des Demodatenbestands:** Soll `DemoDataSets/` nur für Dev-Clones verfügbar sein oder auch in das Publish-/Release-Artefakt? `GetDemoDataFolderPath` prüft beide Orte; csproj-/Publish-Regeln festzulegen.
- O-5: **Umfang/Screen der Demoinhalte:** Wie viele Filme/Serien/Staffeln/Episoden sind für screenshots-taugliche Ansichten nötig (Bibliothek, Wiedergabe, ggf. TV-Sender/Live-TV, Einrichtung)? Mindestbestand festlegen.
- O-6: **Zeitpunkt des Scans:** Wird nach `ApplyAsync` ein Scan/Klassifizierungslauf angestoßen (`MediaSourceScanCoordinator`-Mechanismus), oder geschieht das über den regulären Hintergrund-Scan? Relevant für „sofort sichtbar" nach Registrierung.
- O-7: **Anzeigebilder-Format:** Belegt ist „möglichst klein, gut genug für Screenshots" — konkrete Auflösung/Format (z. B. WebP/JPEG, Zielgrößenordnung) ist Implementierungsdetail; Absprache zur Obergrenze sinnvoll.
- O-8: **Mehrere Demodatenbestände:** `GetAvailableAsync`/`InputSelect` unterstützen bereits mehrere Sets — genügt ein einziger Bestand („Demo"), oder sollen weitere Varianten vorgesehen werden? Annahme: ein Bestand.
- O-9: **Fehlerfall:** `ApplyAsync`-Fehler bricht aktuell mit Fehlermeldung ab, der Benutzer ist aber bereits angelegt (`Register.razor` Zeilen 104–122) — Ist dieses Verhalten für die Demoinstallation akzeptabel (Nachregistrierung/erneutes Anlegen?), oder soll die Registrierung transaktional bündeln?
