# Datenmodell und Persistenz

## Bestehende Struktur

- `VideoWebPlayer/Data/ApplicationDbContext.cs` registriert `DbSet<ContinueWatchingEntry>` neben `Movies` und `TVShowEpisodes`.
- `VideoWebPlayer/Data/Entities/ContinueWatchingEntry.cs` speichert `UserId`, optional `MovieId` oder `TVShowEpisodeId`, Position, Dauer, `UpdatedAt` und Listenreihenfolge.
- `VideoWebPlayer/Data/Configurations/ContinueWatchingEntryConfiguration.cs` indexiert die benutzerbezogenen Movie-/Episode-Schluessel und konfiguriert Kaskadenloeschungen.
- Die Datenbank wird ueber EF-Core-Migrationen unter `VideoWebPlayer/Migrations/` fortgeschrieben.

## Relevante Beziehungen

`Movie` und `TVShowEpisode` erben von `MediaBaseEntry`. Ein Film verweist ueber `MediaSourceId` und optional `MovieCollectionId` auf seine Quelle/Sammlung. Eine Episode verweist ueber `TVShowSeasonId` auf Staffel und Serie. Der aktuelle Continue-Watching-Datensatz nutzt getrennte optionale Fremdschluessel, sodass dieselbe Strategie fuer einen Gesehen-Datensatz moeglich ist.

## Fehlender Baustein

Es gibt keine Entitaet, kein DbSet, kein DTO-Feld und keine Abfrage fuer einen Gesehen-Zeitpunkt. Der neue Persistenzbaustein muss daher:

- den aktuellen Benutzer referenzieren,
- genau einen Film oder eine Episode referenzieren,
- den Zeitpunkt des Markierens speichern,
- Abfragen nach Benutzer und Titel performant unterstuetzen,
- bei Loeschung eines Films/einer Episode keine verwaisten Statusdaten hinterlassen.

## Beruehrte Stellen

- `ApplicationDbContext` und EF-Konfiguration
- neue oder erweiterte Datenentitaet
- neue Migration und Model Snapshot
- `DeleteMediaSourceAsync` in `ApplicationDbContext`, weil dort Continue-Watching- und andere benutzerbezogene Verweise vor dem Medienloeschen bereinigt werden
- Backup-/Restore-Code, falls die neue Tabelle ueber die bestehende Backup-Infrastruktur mitgesichert werden soll
- API- und Listen-DTOs fuer die Statusanreicherung
