# Anforderung

## Ziel

Anwender sollen in allen Titelauflistungen unmittelbar erkennen koennen, ob ein Film oder eine Episode bereits angesehen wurde. Der Gesehen-Status soll pro Benutzer gespeichert und bei Erreichen des konfigurierten Zeitraums am Ende der Wiedergabe automatisch gesetzt werden.

## Umfang

- Einen Gesehen-Status fuer Filme und Episoden unterstuetzen.
- Beim Setzen des Gesehen-Status zusaetzlich den Zeitpunkt des Setzens speichern.
- Den Gesehen-Status benutzerbezogen speichern, sodass Benutzer jeweils ihren eigenen Status sehen.
- Den Status automatisch setzen, sobald bei der Videowiedergabe die letzten Sekunden erreicht werden.
- Fuer die Ermittlung der letzten Sekunden den bereits in der Einrichtung angegebenen relevanten Zeitraum verwenden.
- In jeder Darstellung, in der Titel in Listen angezeigt werden, kenntlich machen, wenn ein Titel einen Gesehen-Zeitpunkt fuer den aktuell angemeldeten Benutzer besitzt. Dies umfasst insbesondere den Quelleninhalt und Listen auf der Startseite.
- Bei einem vorhandenen Gesehen-Zeitpunkt ein Auge-Symbol in der rechten oberen Ecke der Titelanzeige darstellen.

## Fachliche Regeln

- Ein Titel gilt fuer einen Benutzer als gesehen, wenn fuer diesen Benutzer ein Gesehen-Zeitpunkt hinterlegt ist.
- Der Status ist unabhaengig davon zu speichern und auszuwerten, ob es sich um einen Film oder eine Episode handelt.
- Ein Gesehen-Zeitpunkt darf nicht als globaler Titelstatus interpretiert werden; er gehoert immer zur Kombination aus Benutzer und Titel.
- Das Auge-Symbol wird nur angezeigt, wenn fuer den aktuell betrachteten Benutzer ein Gesehen-Zeitpunkt vorhanden ist.
- Das Auge-Symbol ist in der rechten oberen Ecke der jeweiligen Titelanzeige zu platzieren und muss in den betroffenen Listen sichtbar sein.
- Das automatische Setzen erfolgt beim Erreichen des konfigurierten Zeitraums vor dem Ende des Videos.

## Nicht-Ziele

- Keine Aenderung am konfigurierten relevanten Zeitraum.
- Keine automatische Markierung als ungesehen ohne ausdruecklich definierte Funktion dafuer.
- Keine globale, von allen Benutzern geteilte Gesehen-Markierung.
- Keine Anforderung, den exakten Zeitpunkt des letzten Abspielens zu speichern, wenn der Titel nicht als gesehen markiert wurde.

## Akzeptanzkriterien

- In den Titelauflistungen des Quelleninhalts ist bei bereits gesehenen Filmen und Episoden ein Auge-Symbol in der rechten oberen Ecke sichtbar.
- In den Titelauflistungen der Startseite ist bei bereits gesehenen Filmen und Episoden ein Auge-Symbol in der rechten oberen Ecke sichtbar.
- Alle weiteren Stellen, an denen Titel in Listen angezeigt werden, verwenden dieselbe Kennzeichnung fuer den Gesehen-Status.
- Filme koennen als gesehen gespeichert und angezeigt werden.
- Episoden koennen als gesehen gespeichert und angezeigt werden.
- Beim Erreichen der letzten Sekunden eines Videos wird der zugehoerige Film bzw. die zugehoerige Episode automatisch als gesehen markiert.
- Fuer die automatische Markierung wird der in der Einrichtung konfigurierte relevante Zeitraum beruecksichtigt.
- Beim Markieren als gesehen wird ein Zeitpunkt gespeichert.
- Ein Benutzer sieht ausschliesslich seinen eigenen Gesehen-Status; der Status eines Benutzers wird bei anderen Benutzern nicht angezeigt.
- Ein Titel ohne Gesehen-Zeitpunkt fuer den aktuell angemeldeten Benutzer zeigt kein Auge-Symbol.
- Die Kennzeichnung bleibt bei erneuter Anzeige der Titelauflistung erhalten, sofern der Gesehen-Zeitpunkt gespeichert wurde.
- Die bestehende Anzeige und Wiedergabe von Filmen und Episoden bleibt fuer Titel ohne Gesehen-Status unveraendert.

## Betroffene Benutzerfluesse

- Benutzer oeffnet eine Titelauflistung und erkennt den Gesehen-Status direkt am Titel.
- Benutzer startet die Wiedergabe eines Films oder einer Episode und erreicht den konfigurierten Zeitraum vor dem Ende.
- Die Anwendung speichert den Gesehen-Zeitpunkt automatisch und zeigt die Kennzeichnung anschliessend in den Titelauflistungen an.

## Randbedingungen

- Die Kennzeichnung muss fuer Filme und Episoden einheitlich verstaendlich sein.
- Das Auge-Symbol darf die uebrigen Titelinformationen nicht unlesbar machen oder die Bedienung der Titelanzeige beeintraechtigen.
- Die Anzeige muss den Status des aktuell angemeldeten Benutzers verwenden.
