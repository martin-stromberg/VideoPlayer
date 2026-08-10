# Offene Aufgaben

Erstellt am: 2026-08-09
Abbruchgrund: Maximale Iterationsanzahl erreicht

Die folgenden Aufgaben konnten im automatisierten Zyklus nicht abgeschlossen werden
und muessen manuell oder in einem erneuten Lauf bearbeitet werden.

## Offene Planelemente

- [ ] Die Chromium-Abnahme aller fuenf Stitch-Ansichten bei 1440, 1280, 768 und 390 px ist nicht durch Screenshots oder einen vergleichbaren Lauf belegt. Damit fehlen Nachweise fuer abgeschnittene beziehungsweise ueberlappende Inhalte und der direkte Vergleich mit den Stitch-Referenzen.
- [ ] Die geforderte Zustandsmatrix je Hauptansicht ist nicht dokumentiert. Offen sind insbesondere Standard-, Hover-, Fokus-, deaktivierte-, Lade- und Fehlerzustaende sowie Bildfehler, fehlende Metadaten und lange Titel.
- [ ] Die Navigation wurde nicht im Browser fuer angemeldete, abgemeldete und rollenbeschraenkte Benutzer nachgewiesen. Die vorhandenen `AuthorizeView`-/Claim-Bindungen sind sichtbar, ersetzen aber nicht den geforderten Laufnachweis.

## Code-Review-Befunde

Keine.

## Fehlgeschlagene Tests

Keine.

## Rückmeldung des Anwenders

- [x] Bitte restrukturiere die administrative Verwaltung der Anwendung.
  - [x] Im Programmmenü soll es dafür nur noch einen Aufruf "Einrichtung" geben, über welche ein Bereich mit den verschiedenen Einrichtungsseiten gibt.
	- [x] Der Bereich für die Backups soll moderner und übersichtlicher dargestellt werden. Ein Entwurf ist in der Datei stitch_video_collection_backup_dashboard.zip zu finden.
	- [x] Auch die Einstellungsseite für die Updates soll moderner aussehen. Der Entwurf ist in der Datei stitch_video_collection_update_dashboard.zip zu finden.
	- [x] Ein Entwurf fürdie rwaltung der Quelllen ist in der Datei stitch_video_collection_sources_dashboard.zip zu finden.
	- [x] Orientiere dich an den beiden Entwürfen, um auch die einfachereren Seiten der "Sicherheit" und "Genres" zu optimieren.
	- [x] Die Registrierung neuer Anwender soll auch nur noch in diesem Einrichtungsbereich aufrufbar sein, genauso wir die Programmeinstellungen mit den Scan-Einstellungen. Such dir dafür bitte einen passenderen Namen.
- [x] Du hast eigenmächtig entschieden, den angezeigten Titel der Anwendung auf "Martins Videosammlung" zu ändern. Sobald du den Einrichtunngsbereich für Administratoren wiederhergestellt hast, ermögliche dort bitte die Konfiguration des Anwendungstitels.
- [x] Beim Hochladen ener Backupdatei gibt es die Fehlermeldung "Tabelle UpdateSettings fehlt."

## Fortsetzung 2026-08-10

Die Kundenrückmeldung zur administrativen Einrichtung, zum konfigurierbaren Anwendungstitel und zum Restore älterer Backups wurde umgesetzt. Die drei oben genannten Browser- und Zustandsnachweise bleiben offen, weil sie in diesem Lauf nicht durch Chromium-Screenshots oder einen vergleichbaren Browser-Lauf belegt wurden.
