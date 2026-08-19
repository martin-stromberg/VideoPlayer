# Bestandsaufnahme

## Betroffene Oberfläche

Die Anforderung betrifft den Admin-Bereich **Einrichtung > Quellen** (`/admin/mediasources`) in `VideoWebPlayer`.

## Wichtige Dateien

- UI (Liste): [`VideoWebPlayer/Components/Pages/Admin/MediaSources/MediaSourceAdmin.razor`](inventory/ui-and-backend.md)
- UI (Detailansicht): [`VideoWebPlayer/Components/Pages/Admin/MediaSources/MediaSourceAdminDetails.razor`](inventory/ui-and-backend.md)
- Geschäftslogik/Datenbank: [`VideoWebPlayer/Data/ApplicationDbContext.cs`](inventory/data-model.md)

## Festgestelltes Problem

- `ApplicationDbContext.DeleteMediaSourceAsync` löscht nur `MediaCollections` und `MediaItems`.
- Verknüpfungen (`MovieMediaItems`, `TVShowEpisodeMediaItems`) sowie `Movies`, `MovieCollections`, `TVShows`, `TVShowSeasons`, `TVShowEpisodes`, `MovieGenres` und `TVShowGenres` bleiben bestehen.
- Beim Versuch, die `MediaSource` zu löschen, verbleiben abhängige Datensätze in der Datenbank, was zu Fremdschlüssel-Constraint-Verletzungen führt.
- Der Fehler wird im UI nicht dargestellt; stattdessen lädt die Seite bis zum Timeout.

## Datenmodell-Zusammenfassung

Details und Löschabhängigkeiten stehen in [`inventory/data-model.md`](inventory/data-model.md).

## Aktueller UI-Fluss

1. `MediaSourceAdmin.razor` listet Quellen mit Aktionsbuttons.
2. „Löschen“ ruft `DeleteSource(source)`.
3. `DeleteSource` ruft `DbContext.DeleteMediaSourceAsync(source)`.
4. Bei Erfolg wird `LoadSources()` aufgerufen; Fehler werden nicht behandelt.

Mehr Details in [`inventory/ui-and-backend.md`](inventory/ui-and-backend.md).
