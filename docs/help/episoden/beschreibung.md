← [Zurück zur Übersicht](index.md)

# Episoden — Beschreibung

## Hintergrundbild für Episoden

### Zweck

Die Episode-Detailansicht wird durch dynamisch generierte Hintergrundbilder visuell aufgewertet. Statt eines statischen Banners wird aus dem Fanart einer Episode ein maßgeschneideres Hintergrundbild erzeugt, das proportional skaliert und zentriert auf einer Canvas dargestellt wird. Ist für eine Episode kein Fanart vorhanden, wird ersatzweise das Poster der Episode als Quellbild verwendet. Dies schafft ein konsistenteres visuelles Erlebnis und erhöht die Erkennbarkeit jeder Episode.

### Funktionsweise

Beim ersten Aufruf einer Episode-Detailseite lädt die Anwendung das Episode-Fanart (oder ersatzweise das Poster) und generiert daraus automatisch ein Hintergrundbild:

1. **Quellbild-Laden:** Das Fanart-Bild der Episode wird aus der Datenbank geladen; ist kein Fanart vorhanden oder sind dessen Bilddaten ungültig, wird stattdessen das Poster geladen.
2. **Proportionale Skalierung:** Das Bild wird auf maximal 1920×1080 Pixel skaliert, wobei das Seitenverhältnis erhalten bleibt.
3. **Dominante Farbe:** Die vorherrschende Farbe des Fanarts wird berechnet (mittels Histogramm-Sampling).
4. **Canvas-Füllung:** Eine 1920×1080 Pixel große Canvas wird mit der dominanten Farbe gefüllt und das skalierte Fanart wird zentriert darauf platziert.
5. **Tint-Overlay:** Ein Schleier-Effekt (40% opak schwarzes Overlay) wird angewendet, um die Lesbarkeit von Text über dem Hintergrund zu gewährleisten.
6. **Speicherung:** Das generierte Bild wird als JPEG (Qualität 85%) in der Datenbank gespeichert und mit der Episode verknüpft.
7. **Caching:** Die ID des generierten Bildes wird im Speicher gecacht, um bei wiederholten Zugriffen sofort verfügbar zu sein.

### Beispiele

**Ablauf beim Aufruf einer Episode:**
- Benutzer öffnet die Detailseite einer Episode (z. B. "Breaking Bad – Staffel 1 – Episode 1")
- Das System prüft, ob bereits ein generiertes Hintergrundbild existiert
- Falls nicht: Das Episode-Fanart wird verarbeitet und ein Hintergrundbild wird erzeugt
- Die Detailseite wird mit dem Hintergrundbild im Header-Bereich angezeigt (mit 40% Transparenz für Lesbarkeit)

### Update nach Scanner-Lauf

Wenn der Media-Scanner ein neues oder aktualisiertes Fanart **oder Poster** für eine Episode findet:
- Das generierte Hintergrundbild wird als „überarbeitungsbedürftig" markiert
- Beim nächsten Aufruf der Episode wird ein neues Hintergrundbild aus dem aktualisierten Fanart (bzw. Poster, falls kein Fanart vorhanden ist) erzeugt
- Das alte generierte Bild wird aus der Datenbank entfernt

### Einschränkungen

- **Fanart oder Poster erforderlich:** Ist für eine Episode weder Fanart noch Poster vorhanden, wird das Feature nicht aktiv und es wird stattdessen das existierende Banner/Fanart-Bild verwendet.
- **Erste Generierung:** Die erste Generierung eines Hintergrundbildes kann etwa 1 Sekunde dauern, da Bildverarbeitung notwendig ist. Danach ist das Bild gecacht und wird sofort geladen.
- **Datenbankgröße:** Jedes generierte Hintergrundbild belegt etwa 200 KB Datenbankplatz (JPEG, 1920×1080, Qualität 85%).
- **Backup-Ausnahme:** Generierte Bilder werden nicht in Backups mitgesichert. Nach einer Wiederherstellung werden sie automatisch beim nächsten Episode-Aufruf neu erzeugt.
