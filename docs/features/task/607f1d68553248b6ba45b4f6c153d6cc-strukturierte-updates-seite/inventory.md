# Bestandsaufnahme: Strukturierte Updates-Seite

Erstellt am: 2026-08-29
Branch: `task/607f1d68553248b6ba45b4f6c153d6cc-strukturierte-updates-seite`

## Ergebnis

Die Updates-Funktion ist bereits als administrative Blazor-Seite mit bestehendem Backend vorhanden. Die Route ist `/admin/updates`; die Seite wird ueber das Admin-Dashboard `/admin` erreicht. Die Anforderung ist daher primaer eine Umstrukturierung und Neugestaltung der vorhandenen UI. Update-Status, manuelle Aktionen und die Konfiguration muessen an die bestehende Service-/Controller-Schnittstelle angebunden bleiben.

Die wichtigsten Bestandteile sind:

- `VideoWebPlayer/Components/Pages/Admin/Updates.razor`: bestehende Seite, Datenbindung, Admin-Pruefung, Polling, Statusdarstellung und Einstellungsformular.
- `VideoWebPlayer/Controllers/UpdatesController.cs`: POST-Endpunkte fuer Pruefung und Installation inklusive Antiforgery und Redirect-Ergebnissen.
- `VideoWebPlayer/Services/Updates/UpdateAdminService.cs`: Snapshot-Fassade, Pruefung, Download/Installation sowie Regeln fuer Busy- und Installierbarkeit.
- `VideoWebPlayer/Services/Updates/UpdateSettingsService.cs` und `VideoWebPlayer/Data/UpdateSettings.cs`: Persistenz, Defaults, Normalisierung und Mapping auf die Runtime-Optionen.
- `VideoWebPlayer/Components/Pages/Admin/AdminIndex.razor`: bestehender Einstieg ueber den Kachel-Link `/admin/updates`.
- `VideoWebPlayer/wwwroot/app.css`: vorhandene globale Admin- und Responsive-Stile.

## Relevante Detaildokumente

- [Bestehende Architektur und Datenfluss](inventory/architecture.md)
- [Stitch-Vorlage und UI-Abgleich](inventory/design-template.md)
- [Persistenz, Defaults und Validierung](inventory/settings-and-validation.md)
- [Tests und Verifikationsbedarf](inventory/testing.md)

## Abgrenzung

Es wurde kein Bedarf fuer ein neues Update-Backend festgestellt. Die vorhandene Fassade und die HTTP-Aktionen decken `Check`, `Install` und das Laden bzw. Speichern der Einstellungen bereits ab. Fuer `Refresh Data` ist die bestehende Snapshot-Abfrage der Seite wiederzuverwenden. Ein Reset der Defaults ist in der aktuellen Seite bzw. Fassade nicht vorhanden und benoetigt eine klar definierte Umsetzung.

## Offene Punkte fuer die Planung

1. Die bestehende Seite verwendet deutsche Beschriftungen und `PageTitle` "Updates"; der Entwurf fordert die englischen Abschnitts-/Aktionsbezeichnungen und den Seitentitel "System Updates". Die Zieltexte und die Kompatibilitaet mit dem bestehenden UI sind festzulegen.
2. Die aktuelle Persistenz erzwingt beim Speichern `CheckIntervalMinutes >= 1`, Retention aber nur `>= 0`; die Anforderung verlangt fuer Retention den Bereich 1 bis 10. Die Validierung und der Persistenzschutz muessen synchron angepasst werden.
3. Defaultwerte kommen aus `AutoUpdate:*`-Konfiguration bzw. Konstanten. Im Entwurf stehen Beispielwerte `60`, `3`, `/var/backups/cineprive` und `cineprive-media-server`, die nicht mit allen aktuellen Defaults uebereinstimmen. Es ist zu entscheiden, ob die Anwendungskonfiguration oder die Vorlage massgeblich ist.
4. Die Installationsregel ist bereits in `UpdateAdminService.IsInstallable` implementiert: Zustand `UpdateAvailable` oder `ReadyToInstall` und eine nichtleere verfuegbare Version; Busy-/Lock-Zustaende blockieren zusaetzlich. Diese Regel muss in der neuen UI sichtbar und testbar bleiben.
5. Die vorhandenen Tests decken Controller-Autorisierung ab, aber es wurde keine bestehende E2E-Abdeckung fuer den kompletten Updates-Benutzerfluss gefunden.
