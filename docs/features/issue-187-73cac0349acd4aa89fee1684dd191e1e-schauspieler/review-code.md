# Code-Review: Schauspieler

## Durchgeführte Prüfungen

- Datenmodell: `Actor`, `MovieActor`, `TVShowEpisodeActor` und EF-Konfigurationen konsistent mit bestehendem Muster.
- Migration `AddActors` erfolgreich generiert.
- Parser in `MediaSourceClassifier` wiederverwendet bestehende Genre-Get-or-Create-Logik für Schauspieler.
- Backup/Restore toleriert neue Tabellen und Felder.
- UI-Links und API-Controller folgen bestehenden Mustern.

## Bekannte Einschränkungen

- Detailansicht zeigt noch keine Aggregation nach 50%-Schwelle; stattdessen flache Liste der Medien.
- Schauspieler-Bilder werden nicht importiert (nur optionaler `PictureId` vorgesehen).
- Keine dedizierten E2E-Tests für die Actor-UI.

## Fazit

Code ist für den aktuellen Stand akzeptabel; Aggregation und Bildimport sind als Folgeaufgaben dokumentiert.
