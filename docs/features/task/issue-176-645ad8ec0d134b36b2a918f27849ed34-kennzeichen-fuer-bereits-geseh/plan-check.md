# Plan-Gegenprüfung

## Ergebnis

**Status:** Plan vollständig

## Abgleich Akzeptanzkriterien

| Akzeptanzkriterium | Umsetzung im Plan | Testnachweis im Plan | Status |
|--------------------|-------------------|----------------------|--------|
| Quelleninhalt zeigt für gesehene Filme und Episoden ein Auge rechts oben. | Abschnitte 2 und 4 reichern die Quellen-, Sammlungs- und Serien-/Staffeldaten benutzerbezogen mit `WatchedAt` an und binden den gemeinsamen Indikator in `MediaSourceDetails`, `MovieCollectionDetails`, `TVShowDetails` und `MediaBaseEntryList` ein. | Abschnitt 7 enthält getrennte E2E-Szenarien für Filme in Sammlungen und Episoden in Serien-/Staffelansichten einschließlich positiver und negativer Karten. | Abgedeckt |
| Startseitenlisten zeigen für gesehene Filme und Episoden ein Auge rechts oben. | Abschnitte 2 und 4 binden `WatchedAt` in `FavoritesList`, `RecentEntriesList`, `ContinueWatchingList` und `SeasonalGenreList` ein; nicht unterstützte Medientypen bleiben unmarkiert. | Abschnitt 7 weist jede der vier Listen als eigenen beziehungsweise klar benannten parametrisierten E2E-Fall mit gesehenen und ungesehenen Titeln aus. | Abgedeckt |
| Alle weiteren Stellen mit Titelauflistungen verwenden dieselbe Kennzeichnung. | Abschnitt 4 verwendet die gemeinsame Overlay-Klasse und verlangt einen Repository-weiten Audit aller `MediaBox`-, `.media-box`-, `.episode-box`- sowie Film-/Episodenlisten; jede weitere fachliche Liste muss denselben `WatchedAt`-Wert anbinden. | Die E2E-Matrix deckt alle im Inventar ermittelten Quellen-, Detail- und Startseitenlisten ab; Abschnitt 7 fordert getrennt identifizierbare Fälle. | Abgedeckt |
| Filme können als gesehen gespeichert und angezeigt werden. | Abschnitte 1 bis 4 planen Film-Fremdschlüssel, Serviceoperation, DTO-Anreicherung und Anzeige. | Abschnitte 6 und 7 enthalten Persistenz-/Service-Nachweise sowie sichtbare Filmkarten- und Filmabschluss-E2E-Tests. | Abgedeckt |
| Episoden können als gesehen gespeichert und angezeigt werden. | Abschnitte 1 bis 4 planen Episoden-Fremdschlüssel, Serviceoperation, DTO-Anreicherung und Anzeige. | Abschnitte 6 und 7 enthalten Persistenz-/Service-Nachweise sowie sichtbare Episodenkarten- und Episodenabschluss-E2E-Tests. | Abgedeckt |
| Beim Erreichen der letzten Sekunden wird der zugehörige Film beziehungsweise die Episode automatisch als gesehen markiert. | Abschnitt 3 integriert `MarkWatchedAsync` für beide Titeltypen in den vorhandenen serverseitigen Endschwellen-Zweig und erhält Folgemedien- und SignalR-Verhalten. | Abschnitt 7 definiert getrennte, konkrete E2E-Flüsse für Film und Episode vom ungesehenen Ausgangszustand über die Wiedergabe bis zur anschließenden sichtbaren Kennzeichnung. | Abgedeckt |
| Für die automatische Markierung wird der konfigurierte relevante Zeitraum verwendet. | Abschnitt 3 verwendet ausschließlich `ProgramSettingsService.GetContinueWatchingEndThresholdAsync`; eine zweite Browser-Schwelle ist ausgeschlossen. | Abschnitte 5 bis 7 setzen einen nicht standardmäßigen Wert von 17 Sekunden ein und prüfen 18 Sekunden negativ sowie 17 Sekunden und darunter positiv. | Abgedeckt |
| Beim Markieren als gesehen wird ein Zeitpunkt gespeichert. | `WatchedEntry.WatchedAt`, UTC-Zeitgeber und Upsert-Verhalten sind in Abschnitten 1 bis 3 konkret vorgesehen. | Abschnitt 6 prüft unverändertes UTC-`WatchedAt` beim Lesen; die Abschluss-E2E-Tests prüfen genau einen persistierten UTC-Zeitpunkt. | Abgedeckt |
| Ein Benutzer sieht ausschließlich seinen eigenen Gesehen-Status. | Abschnitt 2 begrenzt Schreiben und gebündelte Leseabfragen auf die serverseitig ermittelte Benutzer-ID und verbietet eine Browser-Benutzer-ID. | Abschnitte 5 bis 7 planen zwei Benutzer, Service-/DTO-/Controller-Tests für jeden Datenfluss sowie einen Browserwechsel für Quellen-/Detailflüsse und mindestens einen Startseitenfall. | Abgedeckt |
| Ohne Gesehen-Zeitpunkt für den aktuellen Benutzer erscheint kein Auge. | Abschnitt 4 rendert das DOM-Element nur bei `WatchedAt != null`; Sammlungen, Serien und Staffeln erhalten stets `null`. | Alle Listenfälle in Abschnitt 7 enthalten ungesehene Vergleichstitel; die Quelle-/Detail- und Startseitenszenarien prüfen das fehlende Auge explizit. | Abgedeckt |
| Die Kennzeichnung bleibt nach erneuter Anzeige der Titelauflistung erhalten. | Abschnitt 4 lädt den Status nach Navigation oder Reload erneut serverseitig und schließt rein lokalen Komponentenstatus aus. | Die Quellen-, Staffel-, Startseiten- und Abschluss-E2E-Fälle in Abschnitt 7 enthalten Reload beziehungsweise erneute Navigation und prüfen den Indikator danach erneut. | Abgedeckt |
| Bestehende Anzeige und Wiedergabe ungesehener Titel bleiben unverändert. | Abschnitte 3 und 4 lassen den Fortschrittsfluss außerhalb der Schwelle unverändert, rendern ohne Zeitpunkt kein Overlay und schützen bestehende Kartenaktionen. | Abschnitte 6 bis 8 planen negative Schwellentests, Bedienbarkeits-/Layout-E2E-Tests sowie alle im Inventar genannten Continue-Watching-, DbContext- und `MediaBox`-Regressionstests. | Abgedeckt |

## Fehlende oder unvollständige Testanforderungen

Keine.

## E2E-Abdeckung

| Benutzerfluss / Akzeptanzkriterium | Geplanter E2E-Test | Status |
|------------------------------------|--------------------|--------|
| Gesehenen und ungesehenen Film im Quellenpfad öffnen. | `Quelle -> Film-Sammlung` prüft Benutzer A und B, Auge beziehungsweise fehlendes Auge, Position rechts oben und Persistenz nach Reload. | Abgedeckt |
| Gesehene und ungesehene Episode im Quellenpfad öffnen. | `Quelle -> Serie/Staffel` prüft Benutzer A und B, Auge beziehungsweise fehlendes Auge, Position rechts oben und Persistenz nach Reload. | Abgedeckt |
| Favoriten mit gesehenen und ungesehenen Filmen/Episoden aufrufen. | Eigener `FavoritesList`-Fall mit beiden Titeltypen und negativen Vergleichskarten. | Abgedeckt |
| Neue Einträge mit gesehenen und ungesehenen Filmen/Episoden aufrufen. | Eigener `RecentEntriesList`-Fall mit beiden Titeltypen und negativen Vergleichskarten. | Abgedeckt |
| Weiterschauen-Liste mit gesehenen und ungesehenen Filmen/Episoden aufrufen. | Eigener `ContinueWatchingList`-Fall mit beiden Titeltypen sowie weiterhin bedienbarem Kontextmenü und Kartennavigation. | Abgedeckt |
| Saisonale Liste mit gesehenem und ungesehenem Film sowie anderen Medientypen aufrufen. | Eigener `SeasonalGenreList`-/`MediaBaseEntryList`-Fall; nur der gesehene Einzel-Film erhält das Auge. | Abgedeckt |
| Film bis zur konfigurierten Endschwelle wiedergeben und Kennzeichnung anschließend sehen. | Eigenständiger Filmabschluss-E2E mit 18-/17-Sekunden-Grenze, Persistenzprüfung, Navigation zur Sammlung und Reload. | Abgedeckt |
| Episode bis zur konfigurierten Endschwelle wiedergeben und Kennzeichnung anschließend sehen. | Eigenständiger Episodenabschluss-E2E mit 18-/17-Sekunden-Grenze, Persistenzprüfung und anschließender Staffelansicht. | Abgedeckt |
| Benutzerbezogene Sichtbarkeit im Browser prüfen. | Benutzerwechsel A zu B ist für Quellen-/Detailflüsse und mindestens einen Startseitenfall ausdrücklich gefordert. | Abgedeckt |
| Auge auf Desktop und Mobile positionieren, ohne Bedienung oder Layout zu beeinträchtigen. | Responsive E2E-Fälle bei 1440x900 und 390x844 prüfen Bounding Boxes, Overflow, Klick, Hover/Fokus, Long-Press und Episoden-Download. | Abgedeckt |

## Fehlende oder unvollständige Planbestandteile

Keine.

## Hinweise

Der Plan deckt die Anforderung und die in der Bestandsaufnahme ermittelten Risiken vollständig ab. Insbesondere sind die Datenbankinvarianten, die benutzerbezogene Statusanreicherung aller bekannten Listen-Datenflüsse, konkrete E2E-Benutzerflüsse und die betroffenen Regressionstests in der Umsetzungsreihenfolge verankert.
