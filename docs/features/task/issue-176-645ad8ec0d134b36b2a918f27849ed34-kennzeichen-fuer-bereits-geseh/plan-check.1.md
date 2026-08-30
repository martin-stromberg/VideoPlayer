# Plan-Gegenprüfung

## Ergebnis

**Status:** Plan lückenhaft

## Abgleich Akzeptanzkriterien

| Akzeptanzkriterium | Umsetzung im Plan | Testnachweis im Plan | Status |
|--------------------|-------------------|----------------------|--------|
| Quelleninhalt zeigt fuer gesehene Filme und Episoden ein Auge rechts oben. | Abschnitt 2 reichert Quellen-DTOs benutzerbezogen an; Abschnitt 4 bindet `MediaBaseEntryList` und `MediaSourceDetails` an das Overlay in `MediaBox` an. | Abschnitt 5 plant Quelleninhalt-E2E fuer gesehene und ungesehene Filme/Episoden sowie Desktop-/Mobile-Pruefung der Position. | Abgedeckt |
| Startseitenlisten zeigen fuer gesehene Filme und Episoden ein Auge rechts oben. | Abschnitt 2 erweitert die Startseiten-Datenfluesse; Abschnitt 4 nennt `FavoritesList`, `RecentEntriesList`, `SeasonalGenreList` und `ContinueWatchingList`. | Abschnitt 5 nennt nur pauschal den Status in den relevanten Startseitenlisten; die vier Listen und die jeweils unterstuetzten Titeltypen werden nicht als konkrete Szenarien ausgewiesen. | Lücke |
| Alle weiteren Titelauflistungen verwenden dieselbe Kennzeichnung. | Die laut Inventar betroffenen Listen werden auf den gemeinsamen `MediaBox`-Status umgestellt. | Der geplante E2E-Test spricht nur von relevanten Listen und definiert keine vollstaendige, gegen das Inventar pruefbare Abdeckungsmatrix. | Lücke |
| Filme koennen als gesehen gespeichert und angezeigt werden. | Abschnitte 1 bis 4 decken Film-Fremdschluessel, Service, Statusanreicherung und Anzeige ab. | Persistenz-/Service-Test fuer Filme und Quelleninhalt-E2E fuer gesehene Filme sind vorgesehen. | Abgedeckt |
| Episoden koennen als gesehen gespeichert und angezeigt werden. | Abschnitte 1 bis 4 decken Episoden-Fremdschluessel, Service, Statusanreicherung und Anzeige ab. | Persistenz-/Service-Test fuer Episoden und Quelleninhalt-E2E fuer gesehene Episoden sind vorgesehen. | Abgedeckt |
| Beim Erreichen der letzten Sekunden wird Film bzw. Episode automatisch als gesehen markiert. | Abschnitt 3 integriert die Markierung in den vorhandenen Endschwellen-Zweig fuer beide Titeltypen. | Service-Tests fuer Endschwelle und Wiederholbarkeit sowie ein allgemeiner Wiedergabe-E2E sind genannt; fuer Film und Episode fehlen jedoch konkrete E2E-Ablaufe mit Ausgangszustand, Wiedergabeaktion, anschliessender Navigation bzw. Aktualisierung und sichtbarem Auge. | Lücke |
| Die konfigurierte relevante Endschwelle wird verwendet. | Abschnitt 3 verwendet unveraendert `GetContinueWatchingEndThresholdAsync`. | Abschnitt 5 plant Markierung innerhalb, keine Markierung ausserhalb und einen Wiedergabe-E2E bis zur konfigurierten Schwelle. | Abgedeckt |
| Beim Markieren wird ein Zeitpunkt gespeichert. | `WatchedEntry.WatchedAt` als UTC-Zeitpunkt und dessen Speicherung/Aktualisierung sind in Abschnitten 1 bis 3 vorgesehen. | Persistenz-/Service-Tests pruefen Speichern und erneutes Lesen von `WatchedAt`. | Abgedeckt |
| Ein Benutzer sieht ausschliesslich seinen eigenen Gesehen-Status. | Statusspeicherung und gebuendelte Abfragen sind in Abschnitt 2 strikt auf den authentifizierten Benutzer begrenzt. | Service-Test fuer Benutzer A/B und Quelleninhalt-E2E mit Auge nur fuer den aktuellen Benutzer sind vorgesehen. | Abgedeckt |
| Ohne Gesehen-Zeitpunkt fuer den aktuellen Benutzer erscheint kein Auge. | Abschnitt 4 rendert das Overlay nur bei gesetztem Status. | Quelleninhalt-E2E fuer gesehene und ungesehene Titel sowie ein Test fuer fehlenden Status sind vorgesehen. | Abgedeckt |
| Die Kennzeichnung bleibt nach erneuter Anzeige erhalten. | Abschnitt 4 fordert die erneute Abfrage des gespeicherten Status statt eines fluechtigen Komponentenwerts. | Persistenztest und Startseiten-E2E einschliesslich erneuter Anzeige sind vorgesehen. | Abgedeckt |
| Anzeige und Wiedergabe ungesehener Titel bleiben unveraendert. | Abschnitte 3 und 4 erhalten Continue-Watching-/Folgemedienlogik und geben ohne Status kein Overlay aus. | Negative Schwellen- und Ohne-Status-Tests sowie bestehende Continue-Watching-/`MediaBox`-E2E-Szenarien sind vorgesehen, aber die betroffenen vorhandenen Tests aus dem Inventar werden nicht vollstaendig benannt. | Lücke |

## Fehlende oder unvollständige Testanforderungen

- [ ] Konkrete E2E-Szenarien fuer jede betroffene Startseitenliste (`FavoritesList`, `RecentEntriesList`, `SeasonalGenreList`, `ContinueWatchingList`) festlegen: Testdaten und angemeldeten Benutzer vorbereiten, Liste oeffnen bzw. neu laden und fuer die jeweils unterstuetzten Film-/Episodentypen Auge rechts oben sowie fehlendes Auge beim ungesehenen Titel pruefen.
- [ ] Eine vollstaendige E2E-Abdeckungsmatrix fuer alle im Inventar ermittelten Titelauflistungen festlegen, damit "alle weiteren Stellen" nicht nur durch die Formulierung "relevante Listen" abgedeckt wird.
- [ ] Den automatischen Benutzerfluss fuer Film und Episode konkretisieren: Titel anfangs ungesehen, nicht standardmaessige Endschwelle konfigurieren, Wiedergabe bis in die Schwelle ausloesen, Titelauflistung anschliessend oeffnen bzw. aktualisieren und das persistierte Auge sichtbar pruefen.
- [ ] Datenmodelltests fuer die geforderte Genau-ein-Titel-Invariante und Eindeutigkeit vorsehen: weder beide Fremdschluessel noch beide Fremdschluessel `null`, keine doppelten Eintraege fuer dieselbe Benutzer-/Titel-Kombination.
- [ ] Die laut Inventar betroffenen bestehenden Regressionstests vollstaendig und namentlich in den Plan aufnehmen, insbesondere `ContinueWatchingE2ETests`, `ContinueWatchingContextMenuActionTests`, `ContinueWatchingServiceGetNextEpisodeTests`, `ContinueWatchingServiceSignalRTests`, `ApplicationDbContextTests`, `MediaBoxContextMenuInteractionE2ETests` und `MediaBoxContextMenuPositionE2ETests`.
- [ ] Notwendige Testdaten, Fixtures und Hilfsmethoden fuer zwei Benutzer, Film, Episode, `WatchedEntry`, konfigurierbare Endschwelle, Browser-Anmeldung und erneutes Laden der Listen benennen bzw. deren Erweiterung einplanen.

## E2E-Abdeckung

| Benutzerfluss / Akzeptanzkriterium | Geplanter E2E-Test | Status |
|------------------------------------|--------------------|--------|
| Quelleninhalt mit gesehenem/ungesehenem Film und gesehener/ungesehener Episode oeffnen. | Quelleninhalt-E2E prueft Auge bzw. fehlendes Auge; Desktop-/Mobile-Szenario prueft Position und Bedienbarkeit. | Abgedeckt |
| Startseite mit gesehenen Filmen und Episoden in allen betroffenen Listen oeffnen. | Nur pauschal als "Status in den relevanten Listen" beschrieben; keine Szenarien je `FavoritesList`, `RecentEntriesList`, `SeasonalGenreList` und `ContinueWatchingList`. | Lücke |
| Derselbe Titel wird von Benutzer A als gesehen und von Benutzer B ohne Kennzeichnung angezeigt. | Quelleninhalt-E2E sieht ein Auge nur beim aktuellen Benutzer. | Abgedeckt |
| Film bis zur konfigurierten Endschwelle wiedergeben und Kennzeichnung danach in einer Liste sehen. | Allgemeiner Wiedergabe-E2E bis zur Schwelle ist genannt, aber Setup, betroffene Liste und Aktualisierungs-/Navigationsschritt fehlen. | Lücke |
| Episode bis zur konfigurierten Endschwelle wiedergeben und Kennzeichnung danach in einer Liste sehen. | Nicht als eigenes konkretes E2E-Szenario ausgewiesen. | Lücke |
| Gespeicherte Kennzeichnung nach erneuter Anzeige sehen. | Startseiten-E2E einschliesslich erneuter Anzeige ist vorgesehen. | Abgedeckt |
| Ungesehene Titel ohne Auge und mit unveraenderter Kartenbedienung verwenden. | Quelleninhalt-E2E fuer ungesehene Titel sowie bestehende `MediaBox`-E2E-Szenarien sind vorgesehen. | Abgedeckt |

## Fehlende oder unvollständige Planbestandteile

- [ ] Die Persistenzmodellierung muss nicht nur den gleichzeitigen Film- und Episodenbezug ausschliessen, sondern entsprechend dem Inventar explizit erzwingen, dass genau einer der beiden Fremdschluessel gesetzt ist.

## Hinweise

Die fachlichen Hauptpfade und die gemeinsame UI-Anbindung sind im Plan weitgehend enthalten. Fuer die Nachplanung sollten vor allem die E2E-Szenarien als ausfuehrbare Tests mit Setup, Nutzeraktion und sichtbarem Ergebnis formuliert und die im Inventar genannten Regressionstests sowie Testhilfen konkret zugeordnet werden.
