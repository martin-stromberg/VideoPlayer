# Anforderung: Löschen einer Quelle

## Ausgangslage

Im Administrationsbereich „Einrichtung“ wird eine Übersicht der Medien-Quellen (`MediaSource`) angezeigt. Jede Zeile enthält Aktionsbuttons, darunter „Löschen“. Wird dieser Button betätigt, lädt die Seite lange und endet in einem nicht dargestellten Fehler. Die Quelle bleibt bestehen.

## Ziel

Der Löschvorgang für eine `MediaSource` soll asynchron, transparent für den Anwender und vollständig erfolgreich ablaufen.

## Funktionale Anforderungen

### UI-Verhalten

1. Klick auf „Löschen“ einer Quelle:
   - Alle Aktionsbuttons der betroffenen Zeile werden ausgeblendet.
   - An deren Stelle wird ein Fortschrittsbalken angezeigt.
2. Nach erfolgreicher Löschung:
   - Die betroffene Zeile verschwindet aus der Übersicht.

### Fachlicher Löschprozess (asynchron)

Bei Löschung einer `MediaSource` ist folgende Reihenfolge und Vollständigkeit einzuhalten:

1. Für die `MediaSource` alle zugehörigen `MediaCollection`s ermitteln.
2. Für jede `MediaCollection` alle `MediaItem`s ermitteln und aus der Datenbank entfernen.
3. Ist eine `MediaCollection` somit leer, ebenfalls die `MediaCollection` löschen.
4. Aus jedem gelöschten `MediaItem` die Verknüpfungen in `TVShowEpisodeMediaItem` und `MovieMediaItem` entfernen.
5. Nach Bereinigung aller `MediaCollection`s die `MediaSource` selbst löschen.
6. Für die zu löschende `MediaSource` zusätzlich löschen:
   - `Movie`
   - `MovieCollection`
   - `TVShow`
   - `TVShowSeason`
   - `TVShowEpisode`
   - sowie die zugehörigen `TVShowGenre` und `MovieGenre`

### Nicht-funktionale Anforderungen

- Der Prozess läuft asynchron ab (keine blockierende UI).
- Der Fortschritt wird im UI sichtbar gemacht.
- Fehler werden geeignet behandelt (keine endlosen Ladezeiten).
