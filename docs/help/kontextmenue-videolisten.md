# Kontextmenü für Videolisten

Mit einem dreisekündigen Gedrückthalten eines Eintrags in den horizontalen Listen "Weiterschauen" und "Favoriten" lässt sich ein Kontextmenü öffnen, ohne das Video öffnen zu müssen.

## Aktionen

- **Weiterschauen**
  - *Ausblenden* entfernt den Eintrag.
  - *Überspringen* ersetzt den Eintrag durch die nächste Episode bzw. den nächsten Film und behält dabei die Listenposition bei. Gibt es kein Folgemedium, wird der Eintrag entfernt.
- **Favoriten**
  - *Entfernen* löscht den Favoriten aus der Liste.
- **Neu im Programm** erhält kein Kontextmenü.

## Bedienung

- Halte einen Listeneintrag drei Sekunden, um das Menü zu öffnen.
- Kurzes Antippen, Bewegungen über ca. 10 px, `PointerCancel` oder Scrollen brechen das Menü nicht aus und führen zur normalen Navigation.
- `Escape`, Klick außerhalb, Auswahl einer Aktion oder erneutes Pointer-Abbrechen schließen das Menü.
- Das Menü wird per JavaScript an den Viewport-Rändern ausgerichtet, um auf kleinen Bildschirmen nicht abgeschnitten zu werden.

