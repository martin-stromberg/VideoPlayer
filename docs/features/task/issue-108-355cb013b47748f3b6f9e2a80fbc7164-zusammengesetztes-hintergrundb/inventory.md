# Bestandsaufnahme: Zusammengesetztes Hintergrundbild

## Ausgangslage

Die Webanwendung `VideoWebPlayer` zeigt auf der Startseite (`Components/Pages/Home/Home.razor`) einen Kopfbereich (`.home-hero`), der aktuell nur mit CSS-Verläufen hinterlegt ist. Die „Weiterschauen"-Liste wird über `Components/Shared/Home/ContinueWatchingList.razor` dargestellt; dieselben Bild-IDs sollen nun für einen Hero-Hintergrund verwendet werden.

## Technologie-Stack

- ASP.NET Core Blazor (.NET 10)
- Entity Framework Core mit SQLite (`ApplicationDbContext`)
- `SixLabors.ImageSharp` (3.1.11) für serverseitige Bildverarbeitung
- Authentifizierung per JWT-Bearer-Token

## Wichtige Dateien und Komponenten

Details zu den betroffenen Dateien in [inventory/related-files.md](inventory/related-files.md).

## Bilddaten

- Bilder werden in der `Pictures`-Tabelle mit `Data` (byte[]), `ContentType`, `Width`, `Height` gespeichert.
- Einzelbilder werden bereits über `PicturesController.GetPicture(long id)` ausgeliefert.
- Ein generierter Bildmechanismus existiert bereits als Vorbild: `EpisodeBackgroundImageGenerator`.

## Offene Punkte

Keine. Zielgröße und Weichheitsgrad des Übergangs können in der Umsetzung festgelegt werden (direkt im Plan/Implementierung).
