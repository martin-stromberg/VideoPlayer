# Aktuelle Projektstruktur

Die Solution `VideoPlayer.sln` enthaelt die aktuellen Bestandteile der
Videoverwaltung:

- `VideoWebPlayer` — ASP.NET-Core-Blazor-Webanwendung und API
- `VideoWebPlayer.Client` — gemeinsam genutzte Client- und API-Modelle
- `VideoWebPlayer.Maui` — mobile .NET-MAUI-Anwendung fuer den Zugriff auf die Webanwendung
- `VideoWebPlayer.Tests` — Tests fuer die Webanwendung
- `VideoWebPlayer.Maui.Tests` — Tests fuer die MAUI-Anwendung

Die Webanwendung stellt die API bereit, die von `VideoWebPlayer.Maui` ueber
`VideoWebPlayer.Client` verwendet wird. Diese Kommunikation und die
Anwendungskonfiguration bleiben von der Projektbereinigung unveraendert.

## Entwicklung und Start

Fuer die Webentwicklung wird `VideoWebPlayer` als Startprojekt verwendet. Die
MAUI-Anwendung bleibt fuer mobile Builds und Tests erhalten. Sie wurde im
Rahmen der Projektbereinigung nicht gestartet; plattformspezifische
Laufzeitpruefungen sind daher nicht Teil dieser Dokumentation.

Beim Aufraeumen entfernte historische Projektbereiche werden nicht mehr als
Bestandteil der aktuellen Solution oder des Repositories vorausgesetzt.

← [Zurueck zur Dokumentationsuebersicht](index.md)
