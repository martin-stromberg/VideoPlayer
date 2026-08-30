# Wiedergabe und Endschwelle

## Bestehender Ablauf

1. `VideoPlayer.razor` bindet ein HTML-Videoelement und ruft `continueWatching.attach` mit Media-Typ, Media-ID und Authentifizierungstoken auf.
2. `wwwroot/js/continueWatching.js` sendet Position und Dauer regelmaessig an `POST /api/continue-watching/progress`.
3. `ContinueWatchingController.ReportProgress` loest `movieId` oder `episodeId` aus dem Media-Typ auf und delegiert an `ContinueWatchingService.ReportProgressAsync`.
4. Der Service puffert den Fortschritt; `ProcessBufferedEntryAsync` liest `ProgramSettingsService.GetContinueWatchingEndThresholdAsync`.
5. Liegt die Restdauer innerhalb der Schwelle, wird der Continue-Watching-Eintrag entfernt und das naechste Medium optional angelegt. Sonst wird die Position aktualisiert.

## Konsequenz fuer die Anforderung

Der Zeitpunkt fuer das Setzen des Gesehen-Status ist serverseitig bereits klar abgegrenzt: der Endschwellen-Zweig in `ContinueWatchingService.ProcessBufferedEntryAsync`. Der Browser muss fuer den Status keine eigene fachliche Schwellenlogik erhalten. Der bestehende Wert `Setup.ContinueWatchingEndThresholdSeconds` ist bereits durch eine Migration vorhanden und soll unveraendert verwendet werden.

## Zu pruefende Details

- Verhalten bei wiederholten Requests innerhalb der Endschwelle muss idempotent sein.
- Das automatische Setzen gilt sowohl fuer Filme als auch Episoden.
- Das Setzen muss vor oder zusammen mit dem Entfernen des Continue-Watching-Eintrags erfolgreich gespeichert werden.
- Die bestehende Logik fuer Folgemedien und SignalR-Updates darf nicht verloren gehen.
- `ended` und `timeupdate` koennen denselben Vorgang mehrfach ausloesen; der Persistenzzugriff muss Wiederholungen tolerieren.
