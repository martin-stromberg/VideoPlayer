# Tests und Verifikationsbedarf

## Vorhandene Tests

Im Projekt existieren `VideoWebPlayer.Tests/UpdatesControllerAuthorizationTests.cs` fuer die Autorisierung der Update-Endpunkte sowie mehrere Backup- und Infrastrukturtests. Eine gezielte E2E-Testdatei fuer den Updates-Seitenfluss wurde im Bestand nicht gefunden.

## Abzudeckende Szenarien

- Admin kann `/admin/updates` erreichen; nicht autorisierte Anwender sehen keinen geschuetzten Inhalt.
- Seite rendert die drei Bereiche und Status-/Versionsdaten inklusive Fehler- und leerem Downloadzustand.
- `Check for Updates` ruft den bestehenden Check-Endpunkt auf und zeigt Erfolg oder Fehler.
- `Refresh Data` laedt den Snapshot neu, ohne Einstellungen ungewollt zu speichern.
- `Install Update` ist bei fehlender Version, unpassendem Zustand oder Busy/Lock deaktiviert und bei gueltigem Zustand aktiv.
- Alle Schalter und Eingabefelder sind bindbar; Intervall und Retention weisen ungueltige Werte ab.
- `Reset Defaults` setzt alle editierbaren Werte auf die vorgesehenen Defaults zurueck, ohne bereits zu speichern.
- `Save Configuration` persistiert die Werte und laedt den aktuellen Zustand erneut.
- Desktop- und mobile Viewports zeigen keine abgeschnittenen oder ueberlappenden Inhalte; Fokus und Tastaturbedienung bleiben nutzbar.

## Risiko

Die bestehenden Controller-Tests belegen nicht das Blazor-Markup, die Button-Zustaende oder den kombinierten Konfigurieren-/Reset-/Speichern-Fluss. Der Plan sollte deshalb konkrete E2E-Szenarien fuer `/admin/updates` vorsehen und, falls die bestehende Testinfrastruktur keine Seite abbilden kann, die notwendige Testumgebung als eigenes Arbeitspaket ausweisen.
