# Plan-Check: Strukturierte Updates-Seite

Status: Plan vollständig
Erstellt am: 2026-08-29

Hinweis zur Ausfuehrung: Der Lifecycle-Schritt wurde lokal durch den Hauptagenten ausgefuehrt, weil in dieser Umgebung keine separaten Unteragenten verfuegbar sind.

## Pruefgrundlage

- `requirement.md`
- `inventory.md`
- Detaildokumente unter `inventory/`
- `plan.md`
- Stichproben im Bestand zu vorhandenen E2E-Tests und Testabhaengigkeiten

## Ergebnis

Der Umsetzungsplan deckt die Anforderung und die Befunde der Bestandsaufnahme vollstaendig ab. Es wurden keine blockierenden Luecken gefunden. Die besonders geforderten Punkte sind im Plan verbindlich adressiert: deutsche UI-Texte, konkrete E2E-Szenarien fuer den Benutzerfluss, keine neuen externen Abhaengigkeiten, Retention 1 bis 10, Erhalt des bestehenden Installationsworkflows und Erhalt des Admin-Schutzes.

## Pruefpunkte

| Pruefpunkt | Bewertung | Begruendung |
|------------|-----------|-------------|
| UI-Texte bleiben deutsch | Erfuellt | Der Plan legt ausdruecklich fest, dass die Oberflaeche deutsch bleibt, und nennt deutsche Titel und Aktionen wie `Systemupdates`, `Nach Updates suchen`, `Daten aktualisieren`, `Update installieren`, `Standards zuruecksetzen` und `Konfiguration speichern`. |
| Exakter Benutzerfluss per E2E geplant | Erfuellt | Die Szenarien A bis D beschreiben Navigation als Admin, initiale Status-/Detailanzeige, `Nach Updates suchen`, `Daten aktualisieren`, Bearbeiten aller Konfigurationsfelder, Validierung, `Standards zuruecksetzen`, `Konfiguration speichern` sowie Desktop- und Mobile-Pruefungen. Reine Unit- oder Integrationstests werden ausdruecklich nicht als Ersatz akzeptiert. |
| Keine neuen externen Abhaengigkeiten | Erfuellt | Der Plan sieht die Erweiterung vorhandener Admin-CSS-Stile vor und schliesst Tailwind, CDN-Fonts und weitere Abhaengigkeiten aus. Fuer E2E-Tests verweist er auf die vorhandene Browser-Testinfrastruktur; im Bestand ist `Microsoft.Playwright` bereits in `VideoWebPlayer.Tests.csproj` referenziert. |
| Retention 1 bis 10 | Erfuellt | Der Plan fordert die Begrenzung von `RetainedUpdateBackupCount` sowohl im Formularmodell als auch serverseitig auf 1 bis 10. E2E-Szenario C prueft Retention 0 und 11 als ungueltig. |
| Installationsworkflow bleibt erhalten | Erfuellt | Der Plan bindet `Update installieren` an den bestehenden Installationsworkflow an und macht `UpdateAdminService.IsInstallable` zur autoritativen Aktivierungsregel. Der Controller-/Fassadenpfad fuer Check und Install bleibt erhalten. |
| Admin-Schutz bleibt erhalten | Erfuellt | Route und Einstieg bleiben `/admin/updates` und Admin-Dashboard. Der Plan verlangt, dass bestehende Admin-Pruefung, Authentifizierung, Antiforgery-Absicherung und Autorisierungstests fuer Check und Install erhalten bleiben. |
| Defaults und Reset konsistent | Erfuellt | Defaults sollen zentral aus der bestehenden `AutoUpdate`-Konfiguration und deren Fallbacks kommen. `Standards zuruecksetzen` nutzt diese Quelle und persistiert nicht automatisch. |
| Responsive Darstellung | Erfuellt | Der Plan fordert Desktop- und Mobile-Layouts ohne horizontales Scrollen, abgeschnittene Texte oder Ueberlappungen und verankert dies in E2E-Szenario D. |

## Nicht blockierende Hinweise

- Im Abschnitt `Abnahmekriterien fuer die Umsetzung` steht "Die drei E2E-Szenarien A bis D"; A bis D sind vier Szenarien. Fuer die Umsetzung sind alle vier geplanten Szenarien massgeblich.
- Die E2E-Umsetzung muss die vorhandene Playwright-/xUnit-Infrastruktur wiederverwenden. Neue Paket- oder CDN-Abhaengigkeiten waeren nicht vom Plan gedeckt.
- Bei der Umsetzung sollte der Installationsbutton nicht nur optisch aktiviert/deaktiviert werden; Formularziel, Antiforgery und serverseitige `IsInstallable`-Durchsetzung muessen unveraendert erhalten bleiben.
