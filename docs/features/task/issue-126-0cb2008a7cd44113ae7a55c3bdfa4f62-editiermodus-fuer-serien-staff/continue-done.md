# Erledigte Nacharbeiten

Erledigt am: 2026-08-20

## Offene Planelemente

- [x] In der Genre-Auswahlliste des Editiermodus zuerst die bereits zugewiesenen Genres des aktuell bearbeiteten Objekts listen. Danach folgen die weiteren verfuegbaren Genres.
- [x] Episoden-Datumsanzeige und -eingabe pruefen: In der Episodenansicht wird aktuell kein Datum angezeigt, obwohl Episoden vermutlich gepflegte Datumswerte besitzen. Geprueft und korrigiert: Episoden verwenden `ReleaseDate`, das aus NFO-`aired` befuellt wird; `PremieredAt` bleibt Fallback.
- [x] Den Editierbutton fuer Staffeln nur anzeigen, wenn der allgemeine Editiermodus bereits aktiviert ist.
- [x] Navigationsfehler ohne Stacktrace behandeln und Ursache beheben: Detailseiten laden interne Daten nicht mehr ohne authentifizierten Benutzerkontext und zeigen bei Ladefehlern eine fachliche Meldung statt Stacktrace.

## Code-Review-Befunde

Keine Befunde.

## Fehlgeschlagene Tests

Keine Fehler im abschliessenden Lauf.
