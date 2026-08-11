# Anforderung: Zusammengesetztes Hintergrundbild für den Kopfbereich der Startseite

## Aufgaben-ID
355cb013-b477-48f3-b6f9-e2a80fbc7164

## Branch
task/issue-108-355cb013b47748f3b6f9e2a80fbc7164-zusammengesetztes-hintergrundb

## Ziel
Für den Kopfbereich der Startseite soll ein zusammengesetztes Hintergrundbild erzeugt werden, das aus mehreren Hintergrundbildern kombiniert wird.

## Funktionale Anforderungen

1. **Bildauswahl**
   - Verwende die ersten maximal fünf Hintergrundbilder aus der „Weiterschauen"-Liste.

2. **Streifen extrahieren**
   - Aus jedem der ausgewählten Bilder wird ein vertikaler Streifen aus der Bildmitte entnommen.
   - Die Breite jedes Streifens beträgt ein Fünftel der Zielbreite des zusammengesetzten Bildes.
   - Falls weniger als fünf Bilder gefunden werden, entfällt der entsprechende Anteil; jeder verfügbare Streifen nimmt dann einen größeren Anteil der Zielbreite ein (z. B. 1/n bei n Bildern).

3. **Zusammensetzen**
   - Die Streifen werden nebeneinander platziert, sodass sie zusammen die volle Zielbreite ausfüllen.

4. **Weicher Übergang**
   - Zwischen den Abschnitten ist ein weicher Übergang (Blend/Fade) zu integrieren, damit das Gesamtbild keine sichtbaren harten Kanten aufweist und wie aus einem Guss wirkt.

## Akzeptanzkriterien

- Das zusammengesetzte Bild deckt die gesamte Zielbreite des Kopfbereichs der Startseite ab.
- Es werden maximal fünf Bilder aus der „Weiterschauen"-Liste berücksichtigt.
- Jeder Streifen stammt aus der Mitte des jeweiligen Quellbildes.
- Die Streifen sind nebeneinander angeordnet und füllen die Zielbreite vollständig aus.
- Zwischen den Streifen ist ein sichtbarer, weicher Übergang (z. B. Fade/Blend) vorhanden.

## Offene Punkte

*Keine.*
