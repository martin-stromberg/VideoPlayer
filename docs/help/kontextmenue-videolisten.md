# Kontextmenue fuer Videolisten

Mit einem dreisekuendigen Gedrueckthalten eines Eintrags in den horizontalen Listen "Weiterschauen" und "Favoriten" laesst sich ein Kontextmenue oeffnen, ohne das Video oeffnen zu muessen.

## Aktionen

- **Weiterschauen**
  - *Ausblenden* entfernt den Eintrag.
  - *Ueberspringen* ersetzt den Eintrag durch die naechste Episode bzw. den naechsten Film und behaelt dabei die Listenposition bei. Gibt es kein Folgemedium, wird der Eintrag entfernt.
- **Favoriten**
  - *Entfernen* loescht den Favoriten aus der Liste.
- **Neu im Programm** erhaelt kein Kontextmenue.

## Bedienung

- Halte einen Listeneintrag drei Sekunden, um das Menue zu oeffnen.
- Kurzes Antippen, Bewegungen ueber ca. 10 px, `PointerCancel` oder Scrollen brechen das Menue nicht aus und fuehren zur normalen Navigation.
- `Escape`, Klick ausserhalb, Auswahl einer Aktion oder erneutes Pointer-Abbrechen schliessen das Menue.
- Das Menue wird per JavaScript an den Viewport-Raendern ausgerichtet, um auf kleinen Bildschirmen nicht abgeschnitten zu werden.

