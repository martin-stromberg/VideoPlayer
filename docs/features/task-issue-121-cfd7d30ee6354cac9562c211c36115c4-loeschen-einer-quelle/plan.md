# Umsetzungsplan: Löschen einer Quelle

## Offene Punkte

Keine.

## Vorgehen

### 1. Backend: `ApplicationDbContext.DeleteMediaSourceAsync` erweitern

- Signatur ergänzen um `IProgress<double>? progress` und `CancellationToken cancellationToken`.
- Datensätze in einer Datenbanktransaktion löschen.
- Reihenfolge:
  1. `MovieMediaItems` für alle `MediaItems` der Quelle löschen (via `MediaItem.MediaCollection.MediaSourceId`).
  2. `TVShowEpisodeMediaItems` für alle `MediaItems` der Quelle löschen.
  3. Alle `MediaItems` der Quelle löschen.
  4. Alle `MediaCollections` der Quelle löschen.
  5. `TVShowGenres` für alle `TVShows` der Quelle löschen.
  6. `TVShowEpisodes` über `TVShowSeason.TVShow.MediaSourceId` löschen.
  7. `TVShowSeasons` über `TVShow.MediaSourceId` löschen.
  8. `TVShows` der Quelle löschen.
  9. `MovieGenres` für alle `Movies` der Quelle löschen.
  10. `Movies` der Quelle löschen.
  11. `MovieCollections` der Quelle löschen.
  12. `MediaSource` löschen.
  13. `MediaSourceDeletedEvent` publizieren.
- Nach jedem Schritt Fortschritt melden (`0` bis `1`).
- `SaveChangesAsync` am Ende der Transaktion aufrufen.
- Exception sauber behandeln und Transaktion rollbacks.

### 2. UI: `MediaSourceAdmin.razor` anpassen

- Pro Zeile Löschzustand speichern (z.B. `Dictionary<long, DeletionState>`).
- `DeletionState` enthält `IsDeleting` und `Progress`.
- Beim Klick auf „Löschen“:
  - `IsDeleting` setzen.
  - `Progress<T>` an `DeleteMediaSourceAsync` übergeben.
  - Während des Löschens die Buttons ausblenden und einen Bootstrap-Progress-Bar anzeigen.
- Nach erfolgreichem Löschen:
  - `LoadSources()` aufrufen (Zeile verschwindet).
- Bei Fehler:
  - `IsDeleting` zurücksetzen.
  - Fehlermeldung anzeigen.

### 3. UI: `MediaSourceAdminDetails.razor` anpassen

- Gleiches Verhalten wie in der Übersicht:
  - Buttons ausblenden.
  - Fortschrittsbalken anzeigen.
  - Nach Erfolg zurück zur Übersicht navigieren.
  - Fehler anzeigen.

### 4. Tests

- Bestehende Tests des Projekts `VideoWebPlayer` bzw. `VideoWebPlayer.Tests` ausführen.
- Build auf Fehler prüfen.

### 5. Dokumentation

- Hilfedokument unter `docs/help/` anlegen, das den neuen Löschprozess kurz beschreibt.
- `README.md` aktualisieren, falls der Admin-Bereich dort erwähnt wird.

## Technische Hinweise

- Verwendung von `ExecuteDeleteAsync` für effiziente Bulk-Löschungen.
- SQLite als Datenbank-Provider; `ExecuteDeleteAsync` wird unterstützt.
- Fortschrittsmeldung über `IProgress<T>` aus dem DbContext an den Blazor-Renderer.
