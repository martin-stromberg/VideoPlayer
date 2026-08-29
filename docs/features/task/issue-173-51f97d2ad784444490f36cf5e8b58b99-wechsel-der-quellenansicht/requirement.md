# Anforderung

## Ziel

Beim Wechsel zwischen Quellen ueber das Menue soll die Titelliste immer die Titel der zuletzt ausgewaehlten Quelle anzeigen.

## Umfang

- Fehler beim Wechsel der Quellenansicht analysieren und beheben.
- Beim Aufruf einer anderen Quelle ueber das Menue die Titelliste auf diese Quelle aktualisieren.
- Sicherstellen, dass keine Titel der zuvor aufgerufenen Quelle in der neuen Quellenansicht verbleiben.
- Den exakten Benutzerfluss durch E2E-Tests absichern: anmelden, erste Quelle oeffnen und Titel pruefen, zweite Quelle oeffnen und deren Titel pruefen.

## Nicht-Ziele

- Keine Aenderung an der Anmeldung oder an der Verwaltung von Quellen.
- Keine Aenderung an der Titelverwaltung, ausser sie ist fuer die korrekte Aktualisierung nach einem Quellenwechsel zwingend erforderlich.
- Keine Aenderung am Verhalten beim erstmaligen Aufruf einer Quelle, sofern dieses bereits korrekt funktioniert.

## Akzeptanzkriterien

- Ein angemeldeter Anwender kann eine Quelle ueber das Menue oeffnen und sieht die Titelliste dieser Quelle.
- Nach dem Wechsel zu einer anderen Quelle ueber das Menue wird die Titelliste aktualisiert und zeigt die Titel der zuletzt aufgerufenen Quelle.
- Titel der zuvor aufgerufenen Quelle werden nach dem Quellenwechsel nicht mehr angezeigt.
- Der Wechsel zwischen mindestens zwei Quellen ist durch einen E2E-Test fuer den beschriebenen Benutzerfluss abgedeckt.
- Der E2E-Test weist nach, dass nach dem Wechsel die Titel der zweiten Quelle angezeigt werden und die Titel der ersten Quelle nicht mehr angezeigt werden.
