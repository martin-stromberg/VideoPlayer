# Projektplan-Gegenprüfung

## Ergebnis

**Status:** Projektplan vollständig

## Abgleich Anforderung ↔ Entwicklungsschritte

| Anforderungspunkt | Abgedeckt durch Schritt(e) | Status |
|--------------------|-----------------------------|--------|
| B1 / F-A5: Statische Seite, Auslieferung über GitHub Pages, Deployment passend zu den CI/CD-Workflows (Artefakt-Deployment, Workflow-Konventionen: minimale permissions, concurrency, timeout) | 2 | Abgedeckt |
| B2 / F-A2: Modernes, responsives Design, klare Typografie, konsistenter Stil, hell/dunkel, an das Erscheinungsbild der Anwendung angelehnt | 2 | Abgedeckt |
| B3 / F-A1: Inhalte — Vorstellung/Zweck, zentrale Features, Screenshot-Galerie, Download-/Release-Verweis, Verweise auf Repository und Dokumentation | 2 | Abgedeckt |
| B4 / Ablauf 3: Inhalte und Screenshots werden aus dem Repository heraus gepflegt (Pflegeverfahren dokumentiert) | 2 | Abgedeckt |
| B5 / F-A4: Seiten-Quellen in eigenem Root-Ordner `pages/`; interne Pfade basis-pfadfest (`/VideoPlayer/`) | 2 | Abgedeckt |
| B6: Deployment nur von `main`, kein Staging-Vorschau-Deployment, Veröffentlichung über bestehenden Staging-Pfad | 2 | Abgedeckt |
| B7 / B8 / F-A1: Sprache Deutsch, einzelne Landing-Page mit Anker-Abschnitten | 2 | Abgedeckt |
| B9: Statischer Release-Verweis auf die Releases-Übersicht | 2 | Abgedeckt |
| B10 / F-A3: Hell/Dunkel über `prefers-color-scheme` plus manueller Umschalter mit `localStorage`-Persistenz | 2 | Abgedeckt |
| B11 / AK-6: Screenshots entstehen in der lokalen Demoinstallation des geklonten Projekts | 2 (mit Abhängigkeit auf 1) | Abgedeckt |
| B12 / F-C1: Demodaten beim Registrieren des ersten Anwenders generierbar (vorhandener Auswahldialog bleibt unverändert) | 1 | Abgedeckt |
| B13 / F-C2: Demodatenbestand richtet lokale Datenquelle (Verzeichnispfad, Typ `LocalDirectory`) ein und gewährt dem ersten Anwender Zugriff | 1 | Abgedeckt |
| B14 / F-C3: Fiktive Filme und Serien (Titel, Beschreibungen, Staffeln/Episoden, Umfang screenshot-tauglich) | 1 | Abgedeckt |
| B15 / F-C4: Dummy-Videodatei, möglichst klein, abspielbar; dynamisch oder beigelegt — Entscheidung „beilegen" in „Offene Punkte" getroffen, von der Anforderung erlaubt | 1 | Abgedeckt |
| B16 / F-C5: Anzeigebilder möglichst klein, aber gut genug für Screenshots — Herstellung (Laufzeit-Erzeugung per SkiaSharp) in „Offene Punkte" entschieden | 1 | Abgedeckt |
| B17 / F-B1: Lokales Verzeichnis als Quellentyp — bereits implementiert und released; im Plan begründet kein eigener Schritt, nur Nutzung in Schritt 1 | 1 (Voraussetzung, begründet) | Abgedeckt |
| F-A6 / AK-4: Pflege-Dokumentation (`docs/GITHUB_PAGE.md` o. ä.), verlinkt aus Doku-Einstiegspunkten und README, Release-Notes-Eintrag | 2 | Abgedeckt |
| F-C6: Demoinstallation aus lokaler Ausführung des geklonten Projekts — kein externer Dienst, keine echten Mediendaten; Bestand versioniert und mitgeliefert | 1 | Abgedeckt |
| Ablauf 1 (Veröffentlichung): Push auf `main` mit `paths`-Filter, `configure-pages` aktiviert Pages, `workflow_dispatch` für manuelle Auslösung | 2 | Abgedeckt |
| Ablauf 2 (Erstregistrierung): Auswahl im Formular, Quelle + Inhalte angelegt, Bibliothek gefüllt — inkl. Anforderung „sofort sichtbar ohne manuellen Scan/Intervall" (Scan-Anstoß beim Anlegen) | 1 | Abgedeckt |
| Ablauf 3 (Screenshot-Produktion/Pflege): Aufnahme in Demoinstallation, optimierte Ablage unter `pages/`, Galerie-Verdrahtung, dokumentiertes Verfahren | 2 | Abgedeckt |
| Umgang mit fehlenden Objekten: `DemoDataSets/`-Bestand erzeugen und ausliefern (Packaging), Dummy-Video/Anzeigebilder erzeugen, Logo/Favicon nach `pages/` kopieren, Pages-Aktivierung automatisieren bzw. manuellen Fallback dokumentieren | 1, 2 | Abgedeckt |
| Tests/Absicherung: neue Tests für Quelltyp, Medienverzeichnis, gefüllte Bibliothek, Auswahlpfad im Registrierungsformular; Repo-Checks (Markdown-Link-Prüfung) bleiben grün | 1, 2 | Abgedeckt |
| Hilfe-/Projektdokumentation zur Demoinstallation (`docs/help/`, `README.md`) | 1 („Hilfe-/Projektdokumentation") | Abgedeckt |
| Konfiguration: keine neue Laufzeitkonfiguration; Pages-Quelle via `configure-pages` bzw. dokumentierter manueller Fallback; Demodaten-Auswahl ohne Appsettings | 1, 2 | Abgedeckt |
| Nicht-Ziele eingehalten: kein Docs-Portal, kein dynamischer Inhalt, keine Custom Domain/Suche/Mehrsprachigkeit, kein Generator, keine Release-Prozess-Änderung, kein Vorschau-Deployment, keine Unterseiten, kein neuer Quelltyp, keine echten Mediendaten, keine Änderung am Registrierungs-/Rechtemodell | Übergreifend (Plan verletzt keinen Ausschluss; „Offene Punkte" Nr. 4 hält Atomarität explizit außerhalb des Projekts) | Abgedeckt |

## Abhängigkeitsprüfung

| Prüfpunkt | Befund |
|-----------|--------|
| Zyklen in Abhängigkeiten | Keine gefunden (Schritt 1: keine; Schritt 2 → 1, nur Rückwärtsverweis auf existierenden Schritt) |
| Reihenfolge konsistent zu inhaltlichen Abhängigkeiten | Ja — die Screenshot-Galerie (Schritt 2) braucht eine befüllte Demoinstallation (Schritt 1); die belegte Vorgabe B11 macht die Reihenfolge zwingend, und der Plan setzt sie korrekt um. Keine verdeckte inhaltliche Abhängigkeit gefunden: Schritt 1 benötigt nichts aus Schritt 2. |

## Fehlende oder unvollständige Punkte

Keine.

## Hinweise

- Schritt 1 nennt „Hilfe-/Projektdokumentation" pauschal; die Anforderung nennt konkret `docs/help/` und `README.md`. Der Lifecycle-Schritt sollte diese Ziele explizit aufgreifen. Ebenso ist in Schritt 1 kein eigener Release-Notes-Eintrag genannt (in Schritt 2 dagegen schon) — nach Projektkonvention ist bei einer neuen Anwendungsfunktion einer zu erwarten; dies ist im Schritt zu ergänzen, ohne den Plan selbst zu ändern.
- Die Anforderung listet `docs/STAGING_WORKFLOW.md` („CI Workflow Files") als zu aktualisierenden Bereich; Schritt 2 fordert nur „Verlinkung aus den bestehenden Doku-Einstiegspunkten". Beim Abarbeiten sollte die Workflow-Liste in `docs/STAGING_WORKFLOW.md` explizit geprüft/ergänzt werden.
- Die „Offenen Punkte" im Plan sind alle mit Datum und Entscheidung geschlossen und widersprechen keiner Vorgabe (B15 erlaubt beide Varianten, B16 lässt die Herstellung offen).
