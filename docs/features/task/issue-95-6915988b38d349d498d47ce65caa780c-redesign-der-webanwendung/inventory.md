# Bestandsaufnahme: Redesign der Webanwendung

## Ergebnis

Das Repository enthaelt zwei Web-Implementierungen mit aehnlicher Fachlichkeit:

- `VideoWebPlayer/` ist die aktuelle, serverseitige Blazor-Webanwendung auf .NET 10. Sie besitzt die vollstaendigste Mediennavigation, Admin-Flaechen, Player- und Detailansichten und ist daher der wahrscheinlichste Umsetzungsschwerpunkt.
- `WebPlayer/` ist eine aeltere .NET-8-Loesung aus serverseitigem Host und separatem Blazor-WebAssembly-Client. Sie enthaelt ebenfalls Home-, Detail-, Quellen- und Player-Seiten sowie eigene Layout- und CSS-Dateien.

Eine eindeutige Zuordnung des Stitch-Entwurfs zu nur einer der beiden Implementierungen ist aus dem aktuellen Repository nicht ableitbar. Diese Entscheidung muss vor der Planung geklaert werden, um ein Redesign nicht doppelt oder im falschen Frontend umzusetzen.

## Detaildokumente

- [Architektur und Frontend-Oberflaechen](inventory/architecture-and-surfaces.md)
- [Stitch-Referenz und Designsystem](inventory/stitch-reference.md)
- [Bestehende UI-Konventionen und technische Anknuepfungspunkte](inventory/ui-conventions.md)
- [Risiken, Abhaengigkeiten und offene Entscheidungen](inventory/risks-and-open-questions.md)

## Relevante Bestandsdaten

| Bereich | Befund |
|---|---|
| Hauptanwendung | `VideoWebPlayer/VideoWebPlayer.csproj`, .NET 10, serverseitiges Blazor |
| Parallele Anwendung | `WebPlayer/WebPlayer/WebPlayer.csproj` plus `WebPlayer.Client`, .NET 8 |
| Gemeinsame Fachlichkeit | Medienquellen, Medienobjekte, Sammlungen, Serien, Authentifizierung und Admin-Funktionen |
| Aktuelle globale Styles | `VideoWebPlayer/wwwroot/app.css`, `VideoWebPlayer/wwwroot/css/site.css`, `WebPlayer/WebPlayer/wwwroot/app.css`, `app-dark.css` |
| Layouts | `VideoWebPlayer/Components/Layout/`, `WebPlayer/WebPlayer/Components/Layout/` |
| Medienkomponenten | `VideoWebPlayer/Components/Shared/Media/` und `Components/Shared/Home/` |
| Referenzartefakt | `stitch_private_media_library.zip`, fuenf HTML-Screens mit Screenshots und `DESIGN.md` |
| Tests | `VideoWebPlayer.Tests/`, `WebPlayer.Tests/` sowie MAUI-Tests; keine erkennbaren visuellen Browser-Tests im Bestand |

## Technische Bewertung

Das Redesign kann weitgehend innerhalb der Frontend-Grenzen umgesetzt werden. Fachlogik und Datenmodell liegen getrennt in API-, Service- und Datenbereichen. Die groessten Aenderungsflaechen sind Layout, Navigation, globale CSS-Token, gemeinsame Medienkarten, Home-Abschnitte, Detail-Header und Player-Overlay. Authentifizierungs-, Admin- und Statusanzeigen muessen bei der visuellen Umgestaltung erhalten bleiben.

Die Bestandsaufnahme wurde wegen fehlender verfuegbarer Unteragenten direkt anhand der Repository-Dateien und des Stitch-Zips durchgefuehrt.
