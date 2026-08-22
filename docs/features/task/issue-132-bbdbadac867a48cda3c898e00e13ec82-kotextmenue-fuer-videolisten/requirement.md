# Übersetzte Anforderung

## Metadaten

- **Aufgaben-ID:** bbdbadac-867a-48cd-a3c8-98e00e13ec82
- **Branch:** `task/issue-132-bbdbadac867a48cda3c898e00e13ec82-kotextmenue-fuer-videolisten`
- **Titel:** Kontextmenü für Videolisten

## Ziel

Einträge in horizontalen Videolisten sollen schnelle Aktionen direkt aus der Liste heraus ermöglichen. Das Kontextmenü wird durch dreisekündiges Gedrückthalten eines Eintrags geöffnet.

## Funktionale Anforderungen

1. Für Einträge in der Liste „Weiterschauen“ wird nach dreisekündigem Gedrückthalten ein Kontextmenü angezeigt.
2. Das Kontextmenü der Liste „Weiterschauen“ bietet die Aktion „Ausblenden“.
3. „Ausblenden“ entfernt das ausgewählte Video aus der Liste.
4. Das Kontextmenü der Liste „Weiterschauen“ bietet die Aktion „Überspringen“.
5. „Überspringen“ ersetzt den ausgewählten Eintrag durch die nächste Episode.
6. Die nächste Episode wird beim Überspringen an der Position des vorherigen Eintrags eingefügt und nicht an den Anfang der Liste verschoben.
7. Existiert keine nächste Episode, entfernt „Überspringen“ den ausgewählten Eintrag aus der Liste.
8. Für Einträge in der Favoritenliste wird nach dreisekündigem Gedrückthalten ein Kontextmenü angezeigt.
9. Das Kontextmenü der Favoritenliste bietet die Aktion „Entfernen“.
10. „Entfernen“ entfernt den ausgewählten Favoriten aus der Liste.
11. Für Einträge in der Liste „Neu im Programm“ wird kein Kontextmenü angeboten.

## Interaktionsanforderungen

1. Das Kontextmenü darf erst nach einer Haltezeit von drei Sekunden geöffnet werden.
2. Die Halteinteraktion muss sich auf den einzelnen Listeneintrag beziehen.
3. Für die Liste „Neu im Programm“ darf die dreisekündige Halteinteraktion kein Kontextmenü öffnen.

## Nichtfunktionale Anforderungen

- Die bestehenden regulären Abläufe der Videolisten müssen erhalten bleiben.
- Die Position des Eintrags muss beim Ersetzen durch die nächste Episode stabil bleiben.
- Die Änderungen sollen sich in die vorhandenen Bedien- und UI-Konventionen der Anwendung einfügen.
- Das Kontextmenü und seine Aktionen müssen auf den unterstützten Bildschirmgrößen bedienbar und ohne überlappende Inhalte dargestellt werden.

## Akzeptanzkriterien

- [ ] Ein dreisekündiges Gedrückthalten eines Eintrags in „Weiterschauen“ öffnet ein Kontextmenü.
- [ ] Das Menü „Weiterschauen“ enthält „Ausblenden“ und entfernt das Video bei Auswahl.
- [ ] Das Menü „Weiterschauen“ enthält „Überspringen“.
- [ ] „Überspringen“ ersetzt den Eintrag durch die nächste Episode an derselben Listenposition.
- [ ] Ohne nächste Episode wird der Eintrag durch „Überspringen“ entfernt.
- [ ] Ein dreisekündiges Gedrückthalten eines Favoriten öffnet ein Kontextmenü mit „Entfernen“.
- [ ] „Entfernen“ löscht den ausgewählten Favoriten aus der Favoritenliste.
- [ ] Ein dreisekündiges Gedrückthalten in „Neu im Programm“ öffnet kein Kontextmenü.
- [ ] Die reguläre Navigation und Wiedergabe der Videolisten bleiben unverändert nutzbar.

## Abgrenzung

- Das Kontextmenü wird ausschließlich für „Weiterschauen“ und die Favoritenliste eingeführt.
- Die Liste „Neu im Programm“ erhält kein Kontextmenü.
- Eine Änderung des regulären Ablaufs, bei dem eine nächste Episode an den Anfang der Liste gesetzt wird, ist außerhalb des Überspringen-Kontextmenüs nicht Bestandteil dieser Anforderung.

## Offene Punkte

- Welche konkreten UI-Komponenten und visuellen Zustände sollen für das Kontextmenü verwendet werden?
- Wie soll die Halteinteraktion bei beginnendem Scrollen oder einer vorzeitigen Fingerbewegung behandelt werden?
- Welche Rückmeldung soll nach dem Ausblenden, Überspringen oder Entfernen angezeigt werden?
