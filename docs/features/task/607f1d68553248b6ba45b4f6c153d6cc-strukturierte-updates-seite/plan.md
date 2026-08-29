# Umsetzungsplan: Strukturierte Updates-Seite

## Ziel und Leitplanken

Die bestehende administrative Blazor-Seite unter `/admin/updates` wird visuell und strukturell an den Stitch-Entwurf angenaehert. Die bestehende Anwendungshuelle, Authentifizierung, Antiforgery-Absicherung und Update-Fassade bleiben erhalten. Das Update-Backend, der Download- und Installationsprozess sowie externe Frontend-Abhaengigkeiten werden nicht erweitert.

Die sichtbare Informationshierarchie besteht aus:

1. `Update-Status` mit aktuellem Zustand, installierter bzw. verfuegbarer Version, letzter Pruefung, Fehlermeldung und Aktionen.
2. `Versionsdetails` mit installierter Version, verfuegbarer Version, Prerelease-Status, Release-Datum und Downloadzustand.
3. `Konfiguration` mit `Automatisierung` sowie `Sicherheit & Backups`.

## Festlegungen aus Anforderung und Bestandsaufnahme

- Route und Einstieg bleiben `/admin/updates` bzw. der bestehende Link im Admin-Dashboard.
- Die Benutzeroberflaeche bleibt deutsch. Verwendet werden die zur bestehenden Anwendung passenden Titel und Aktionsnamen `Systemupdates` bzw. `Updates`, `Nach Updates suchen`, `Daten aktualisieren`, `Update installieren`, `Standards zuruecksetzen` und `Konfiguration speichern`. Die bestehende globale Navigation und das Layout werden nicht ersetzt.
- Die Defaultwerte werden zentral aus der bestehenden `AutoUpdate`-Konfiguration und deren Fallbacks bezogen. Der Reset verwendet dieselbe Quelle, damit Reset und erstmalige Initialisierung nicht auseinanderlaufen.
- `UpdateAdminService.IsInstallable` bleibt die autoritative Regel fuer den Status von `Update installieren`; die UI dupliziert diese Fachlogik nicht.
- `RetainedUpdateBackupCount` wird sowohl im Formularmodell als auch serverseitig auf den Bereich 1 bis 10 begrenzt. Das bestehende Intervall bleibt auf 1 bis 1440 Minuten begrenzt.
- Ein leerer letzter Download wird als expliziter Leerzustand dargestellt; Abruffehler bleiben sichtbar, ohne die restlichen Bereiche auszublenden.

## Umsetzungsschritte

### 1. Bestehende Seite neu strukturieren

Datei: `VideoWebPlayer/Components/Pages/Admin/Updates.razor`

- Bestehende Admin-Pruefung, Initialisierung, Polling und Snapshot-Ladevorgang beibehalten.
- Markup in klar getrennte Status-, Detail- und Konfigurationsbereiche ueberfuehren.
- Status, Versionsdaten, Fehler-/Lock-Hinweise und Leerzustaende aus dem vorhandenen Snapshot binden.
- `Nach Updates suchen` weiterhin ueber den geschuetzten bestehenden Check-Endpunkt ausloesen.
- `Daten aktualisieren` an `ReloadAsync` bzw. die bestehende Snapshot-Abfrage binden, ohne Konfigurationswerte zu speichern.
- `Update installieren` an den bestehenden Installationsworkflow anbinden und anhand des vom Service gelieferten Zustands deaktivieren bzw. aktivieren.
- Allen fuer E2E benoetigten interaktiven Elementen stabile, semantische Selektoren geben, bevorzugt ueber zugreifbare Rollen und Labels, ansonsten ueber eindeutige `data-testid`-Attribute.

### 2. Konfigurationsformular und Reset

Datei: `VideoWebPlayer/Components/Pages/Admin/Updates.razor`

- Die vorhandenen Felder fuer automatische Pruefung, Vorabversionen, automatische Installation, Pruefintervall, Dienstname, Sicherung vor Installation, Abbruch bei Sicherungsfehler, Sicherungspfad und Anzahl aufzubewahrender Sicherungen weiterverwenden und in die zwei Konfigurationsgruppen einordnen.
- Eingaben mit Labels, Tastaturfokus und sichtbaren Validierungsfehlern versehen.
- `Standards zuruecksetzen` setzt das lokale Bearbeitungsmodell auf die zentral ermittelten Standardwerte zurueck, loest aber keine Persistierung aus und verwirft ungespeicherte Aenderungen sichtbar im Formular.
- `Konfiguration speichern` verwendet weiterhin das bestehende Update-Modell und laedt nach erfolgreichem Speichern den aktuellen Snapshot erneut.
- Die bestehende Bestaetigung beim erstmaligen Aktivieren des Prerelease-Kanals beibehalten oder in die neue Formularstruktur integrieren.

### 3. Defaults, Validierung und Persistenzschutz

Dateien: `VideoWebPlayer/Services/Updates/UpdateSettingsService.cs`, `VideoWebPlayer/Data/UpdateSettings.cs` sowie zugehoerige Formular-/Optionsmodelle und Tests.

- Eine gemeinsame Default-Ermittlung verwenden oder extrahieren, die Konfigurationswerte und dokumentierte Fallbacks liefert.
- Den Reset-Pfad auf diese Default-Ermittlung aufsetzen; keine hardcodierten, vom Runtime-Default abweichenden UI-Werte einfuehren.
- Retention serverseitig auf 1 bis 10 validieren und normalisieren. Ungueltige Werte duerfen nicht gespeichert werden.
- Intervallvalidierung auf 1 bis 1440 Minuten sowie bestehende Laengen-/Pfadregeln erhalten.
- Speichern, Runtime-Mapping und Backup-Options-Mapping auf unveraenderte Feldbedeutung pruefen.

### 4. Responsive Gestaltung und Accessibility

Datei: `VideoWebPlayer/wwwroot/app.css` sowie seitenspezifische Styles, falls im Bestand vorgesehen.

- Die vorhandenen globalen Admin-Stile erweitern, statt Tailwind, CDN-Fonts oder weitere Abhaengigkeiten einzufuehren.
- Dunkle kontrastreiche Flaechen, dezente Umrandungen, rote Primaeraktionen und blaue Informationsakzente am Entwurf ausrichten.
- Desktop-Layout fuer Status/Details und zweispaltige Konfiguration umsetzen; unterhalb des mobilen Breakpoints alle Bereiche und Konfigurationsgruppen untereinander anordnen.
- Lange Werte, Fehlermeldungen und Labels umbrechen lassen; keine festen Breiten verwenden, die horizontales Scrollen, Abschneiden oder Ueberlappung erzeugen.
- Disabled-Zustaende, sichtbare Fokuszustaende und ausreichende Kontraste sicherstellen.

### 5. Tests

Bestehende Controller-, Service- und Validierungstests erweitern. Da im Inventory keine Updates-E2E-Suite gefunden wurde, wird die vorhandene Browser-Testinfrastruktur verwendet; falls sie keine Blazor-Seite abbildet, ist deren minimale Konfiguration als Teil dieses Arbeitspakets einzurichten. Keine reine Unit-/Integration-Abdeckung ersetzt die folgenden E2E-Szenarien.

#### E2E-Szenario A: Navigation und initiale Seite

1. Als Admin anmelden und `/admin` oeffnen.
2. Den bestehenden Updates-Einstieg aus der Navigation bzw. Admin-Kachel anklicken.
3. Assertions: URL `/admin/updates`; Seitentitel `Systemupdates` oder `Updates`; Bereiche `Update-Status`, `Versionsdetails` und `Konfiguration` sichtbar; installierte/verfuegbare Version, letzte Pruefung, Prerelease-Status, Release-Datum und letzter Download bzw. dessen Leerzustand sichtbar.
4. Einen nicht installierbaren Snapshot verwenden und assertieren, dass `Update installieren` deaktiviert ist; einen installierbaren Snapshot verwenden und assertieren, dass die Aktion aktiviert ist.

#### E2E-Szenario B: Nach Updates suchen und Daten aktualisieren

1. Auf der Updates-Seite den Ausgangsstatus und den Zeitstempel erfassen.
2. `Nach Updates suchen` aktivieren.
3. Assertions: der bestehende Check-Endpunkt wird aufgerufen, ein Erfolgs- oder verstaendlicher Fehlerstatus erscheint und die Seite bleibt bedienbar.
4. Einen geaenderten Backend-Snapshot bereitstellen und `Daten aktualisieren` aktivieren.
5. Assertions: der neue Snapshot und Zeitstempel werden angezeigt; die aktuell ungespeicherten Formularwerte werden nicht ungewollt persistiert.

#### E2E-Szenario C: Konfiguration bearbeiten, Standards zuruecksetzen und speichern

1. Auf der Updates-Seite alle drei Schalter sowie Pruefintervall, Dienstname, Sicherungspfad und Anzahl aufzubewahrender Sicherungen aendern.
2. Assertions: jede Aenderung bleibt lokal im Formular sichtbar; ungueltige Werte (Retention 0 und 11, Intervall 0) zeigen Validierungsfehler und blockieren das Speichern.
3. Gueltige Werte setzen und `Standards zuruecksetzen` aktivieren.
4. Assertions: alle editierbaren Felder zeigen die zentralen Defaultwerte; der Persistenzdienst wurde nicht aufgerufen.
5. Erneut gueltige, abweichende Werte eintragen und `Konfiguration speichern` aktivieren.
6. Assertions: Speichern wird einmal aufgerufen, Erfolg wird angezeigt und ein erneuter Snapshot zeigt die gespeicherten Werte.

#### E2E-Szenario D: Responsiveness und abgeschnittene Texte

1. Szenario A bis C auf Desktop-Viewport ausfuehren und per Screenshot bzw. DOM-Pruefung verifizieren, dass keine Inhalte ueberlappen.
2. Die Seite in einem mobilen Viewport, mindestens 375 x 812, oeffnen.
3. Assertions: die drei Bereiche und alle beschrifteten Eingaben sind untereinander erreichbar; `document.documentElement.scrollWidth <= document.documentElement.clientWidth`; sichtbare Texte haben keinen horizontal abgeschnittenen Inhalt; Fokus per Tastatur erreicht die Aktionen in sinnvoller Reihenfolge.

#### Ergaenzende automatisierte Tests

- Unit-/Service-Tests fuer Default-Ermittlung, Reset-Modell, Retention 1 bis 10 und Ablehnung ungueltiger Persistenzwerte.
- Bestehende Autorisierungstests fuer Check und Install beibehalten.
- Komponenten-/Integrationstests fuer Fehlerstatus, Leerzustand des Downloads und Install-Button bei UpdateAvailable, ReadyToInstall, Busy und Lock.

## Abnahmekriterien fuer die Umsetzung

- Die drei E2E-Szenarien A bis D existieren und laufen erfolgreich; fehlende oder nicht ausfuehrbare Szenarien gelten als Fehler.
- Die Seite ist ueber die bestehende Navigation erreichbar und nutzt die bestehende Admin-Absicherung.
- Check, Refresh, Reset und Save verhalten sich entsprechend den oben beschriebenen Schritten.
- Retention akzeptiert nur 1 bis 10; Intervall und uebrige Formularregeln werden client- und serverseitig durchgesetzt.
- Update installieren respektiert ausschliesslich die fachliche Installierbarkeitsregel der Fassade.
- Desktop und mobile Darstellung zeigen alle Inhalte ohne Ueberlappung oder Abschneiden.

## Offene Punkte

Keine. Die abweichenden Defaults, die Retention-Grenze, die Installationsregel und die Zieltexte sind in diesem Plan verbindlich festgelegt.
