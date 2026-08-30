# Bestandsaufnahme: Schauspieler

## Branch

`task/issue-187-73cac0349acd4aa89fee1684dd191e1e-schauspieler`

## Technologie-Stack

- ASP.NET Core 10 / Blazor Server
- Entity Framework Core mit SQLite (Migrations im Ordner `VideoWebPlayer/Migrations/`)
- msTools.Backup für Backup/Restore
- SFTP-Reader für Metadaten-Import (NFO-XML)

## Bestehende Domänen- und Service-Schicht

| Bereich | Wesentliche Dateien | Hinweise |
|---------|---------------------|----------|
| Datenbankkontext | `VideoWebPlayer/Data/ApplicationDbContext.cs` | Enthält `DbSet` für MediaSources, MediaCollections, MediaItems, Movies, MovieCollections, TVShows, TVShowSeasons, TVShowEpisodes, Genres, etc. |
| Medien-Entities | `VideoWebPlayer/Data/Movie.cs`, `TVShowEpisode.cs`, `TVShow.cs`, `MediaBaseEntry.cs`, `MediaCollection.cs` | `Movie` und `TVShowEpisode` haben bereits `LoadFromXml` für NFO-XML. `Movie.LoadFromXml` liest Genre/Studio/Regisseur/Credits, aber keine einzelnen Schauspieler. |
| NFO-Parser / Klassifizierung | `VideoWebPlayer/Services/MediaSourceClassifier.cs` | Liest `tvshow.nfo` und `<film>.nfo`; baut Movies, Episodes, Staffeln, Genres auf. Keine Schauspieler-Verarbeitung. |
| Scan-Dienst | `VideoWebPlayer/Services/MediaSourceScanner.cs` | Datei-Scanner, der MediaCollections/Items anlegt und `Classifyable` setzt. |
| Backup/Restore | `VideoWebPlayer/Services/Backups/VideoWebPlayerBackupData.cs`, `VideoWebPlayerBackupDataFactory.cs` | Objektbasierter Backup als JSON; tabellenzentriert. Neue Tabellen müssen hier bekannt sein. |
| Client-Modelle | `VideoWebPlayer.Client/Models/DtoSource.cs` etc. | DTOs für Quellen; Genre-DTO als Vergleich. |
| API-Controller | `VideoWebPlayer/Controllers/SourcesController.cs` etc. | `ApiBaseController` mit `Create<TDto>(entity)` Mapping. |
| Navigation | `VideoWebPlayer/Components/Layout/NavMenu.razor` | Reine Quellen-Navigation; neuer Menüpunkt nötig. |
| UI-Komponenten | `VideoWebPlayer/Components/Pages/MediaSources/MediaSourceDetails.razor`, `Components/Shared/Media/MediaBaseEntryList.razor` | Listen-/Detailmuster vorhanden. |
| Migrations | `VideoWebPlayer/Migrations/` | Viele aufeinanderfolgende Migrationen; aktuellste scheint `20260810051034_AddApplicationTitleToSetup` o. ä. |
| Tests | `VideoWebPlayer.Tests/MediaSourceScanServiceTests.cs`, weitere Controller-Tests | Testinfrastruktur mit In-Memory/SQLite vorhanden. |

## Offene Punkte / Annahmen

- Schauspieler-Bild (Portrait): Im issue.md nicht spezifiziert. Mögliche Quellen: NFO- `<thumb>` pro Darsteller, extern, oder initiales Platzhalterbild. Erster Implementierungsschritt ohne Bild-Import, dafür Platz für `PictureId` vorsehen.
- Schwellenwert 50%: Soll konfigurierbar sein, idealerweise in `Setup` als `ActorCollectionThresholdPercent`.
- Hintergrund-Nacherfassung: Basiert auf Flag in `Movie`/`TVShowEpisode` (z. B. `ActorsClassifiedAt` oder `ActorsClassifyable`). Worker muss beim Start laufen.
- Berechtigungen: Schauspielerübersicht soll öffentlich für eingeloggte Nutzer sichtbar sein, Detailansicht filtert nach sichtbaren Quellen/Freigaben.
