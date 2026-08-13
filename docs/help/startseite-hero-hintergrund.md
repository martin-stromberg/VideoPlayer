# Startseite – Zusammengesetzter Hero-Hintergrund

## Funktion

Der Kopfbereich der Startseite (Hero) zeigt ein automatisch erzeugtes Hintergrundbild an, das aus den ersten maximal fünf Einträgen der „Weiterschauen"-Liste zusammengesetzt wird.

## Darstellung

- Für jedes in „Weiterschauen" gefundene Medium wird ein vertikaler Streifen aus der Bildmitte entnommen.
- Die Streifenbreite beträgt ein Fünftel der Zielbreite (bei weniger als fünf Bildern entsprechend mehr).
- Die einzelnen Streifen werden nebeneinander gesetzt.
- Zwischen den Streifen ist ein weicher Übergang (Fade/Blend) integriert, sodass das Gesamtbild wie aus einem Guss wirkt.

## Technische Umsetzung

- Der Endpunkt `GET /api/pictures/hero-background` erzeugt das Bild serverseitig.
- Die Verarbeitung nutzt `SixLabors.ImageSharp` (bereits im Projekt enthalten).
- Das Bild wird nur für angemeldete Benutzer erzeugt.
- `Home.razor` lädt das Bild über `?access_token={token}` und setzt es als CSS-Variable `--home-hero-image`.
- Bestehende Verlaufs-Overlays bleiben erhalten und sorgen weiterhin für gute Lesbarkeit des Textes.
