# Bestandsaufnahme: Wechsel der Quellenansicht

## Zusammenfassung

Die Anwendung ist eine ASP.NET-Core-Blazor-Interactive-Server-Anwendung. Quellen werden im persistenten Navigationsmenue als `NavLink` auf `/mediasource/{id}` angeboten. Die Zielseite laedt Quelle, Genres und Titel ueber ein scoped ViewModel und rendert die Titel als `.media-box-link` mit `.media-title-text`.

Beim Wechsel von einer Quelle zu einer anderen bleibt die Route innerhalb derselben Komponentenklasse. Die Initialisierung der Quellenansicht erfolgt jedoch nur in `OnInitializedAsync`. Ein Parameterwechsel wird nicht separat verarbeitet. Das ViewModel behaelt seine `Entries`, Paging-, Such- und Genre-Zustaende. Daraus ergibt sich die zentrale technische Hypothese fuer den Fehler: Die zweite Quelle wird nicht vollstaendig neu initialisiert und die Titelliste wird nicht geleert bzw. neu geladen.

## Relevante Detaildokumente

- [Quellenwechsel und Menue](inventory/source-navigation.md)
- [Titelliste und Datenfluss](inventory/title-list-flow.md)
- [E2E- und Integrationstests](inventory/tests.md)

## Betroffene Grenzen

- UI: `NavMenu.razor` navigiert per Quellen-`NavLink`.
- UI: `MediaSourceDetails.razor` verarbeitet den Route-Parameter und rendert die Liste.
- Zustand: `MediaSourceDetailsViewModel` ist scoped und besitzt den Listen- und Filterzustand.
- API: `SourcesController` liefert Quellenmetadaten; `ItemsController` liefert quellenbezogene Titel.
- Tests: Playwright ist im Testprojekt vorhanden, aber der exakte Wechsel zwischen zwei Quellen ist nicht abgedeckt.

## Erwarteter Umsetzungsumfang

Die spaetere Planung muss einen Parameterwechsel auf derselben Seite, einen Reset aller quellenabhaengigen Listen-/Filterzustaende, das erneute Laden des ersten Titel-Chunks und einen Playwright-Test mit zwei Quellen und unterscheidbaren Titeln beruecksichtigen. Anmeldung und Quellenverwaltung muessen unveraendert bleiben.
