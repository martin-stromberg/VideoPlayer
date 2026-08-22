# Weiterblättern und Positionsstabilität

## Vorhandene Next-Episode-Regel

`ContinueWatchingService.GetNextEpisodeAsync()` ist aktuell privat und wird beim normalen Abschluss einer Wiedergabe verwendet. Die Reihenfolge lautet:

1. nächste Episode derselben Staffel mit `Number > current.Number`, aufsteigend nach `Number`
2. ansonsten nächste Staffel derselben Serie, lexikographisch nach `TVShowSeason.Name`
3. erste Episode der Folgestaffel, aufsteigend nach `Number`
4. `null`, wenn keine Folgeepisode existiert

Diese Logik ist in `docs/help/weiterschauen/ablauf-technisch.md` und `business-rules.md` beschrieben und sollte für „Überspringen“ nicht parallel neu implementiert werden.

## Aktuelles Abschlussverhalten

`ProcessBufferedEntryAsync()` entfernt einen abgeschlossenen Eintrag und legt die nächste Episode mit `UpsertAsync()` neu an. `UpsertAsync()` setzt `UpdatedAt = DateTime.UtcNow`; dadurch landet das neue Element am Anfang der nach `UpdatedAt` sortierten Liste. Dieses Verhalten ist für den normalen Wiedergabeabschluss dokumentiert, widerspricht aber der neuen Anforderung für das Kontextmenü.

## Konsequenz für „Überspringen“

Der Plan muss eine explizite Strategie für die Position festlegen. Mögliche technische Varianten sind:

- eine serverseitige Replace-Operation, die die alte Continue-Watching-Zeile innerhalb einer definierten Reihenfolge durch die nächste Episode ersetzt und die Reihenfolge stabil repräsentiert;
- eine durch die UI verwaltete Listenposition, sofern die fachliche Persistenz nicht positioniert werden muss;
- ein separates Ordnungsfeld oder eine stabile Sortierregel, falls Position über Seiten-/SignalR-Neuladen hinweg garantiert werden soll.

Die vorhandene Entität besitzt aktuell kein Ordnungsfeld. Das ist ein zentraler Planungsbefund und sollte vor der Implementierung entschieden werden.

## Verhalten ohne nächste Episode

Die bestehende Next-Episode-Logik liefert `null`; beim Überspringen muss der alte Eintrag dann gelöscht und kein neuer Eintrag angelegt werden. Dieser Pfad benötigt eine eigene Transaktion bzw. atomare Serviceoperation, damit kein veralteter Eintrag zurückbleibt.

## Filme

Die Anforderung spricht von der nächsten Episode, aber „Weiterschauen“ enthält laut DTO und Service auch Filme. Der Plan muss klären, ob „Überspringen“ ausschließlich Episoden erlaubt oder auch die vorhandene `GetNextMovieAsync()`-Logik für Filme verwenden soll. Ohne diese Entscheidung besteht ein Risiko für eine inkonsistente Menüanzeige.
