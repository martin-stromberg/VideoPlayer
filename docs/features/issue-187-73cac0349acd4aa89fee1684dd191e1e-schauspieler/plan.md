# Umsetzungsplan: Schauspieler

## Zusammenfassung

Aufbau einer neuen `Actor`-Domäne, automatische Erfassung aus NFO-XML beim regulären Scan, resümierende Hintergrund-Nacherfassung für Altbestände, Navigation/Übersicht/Detailansicht im Blazor-Frontend sowie Backup/Restore-Unterstützung.

## Akzeptanzkriterien → geplante Tests

### 1. Datenerfassung

| Kriterium | Geplante Tests | Verantwortlicher Layer |
|-----------|----------------|------------------------|
| Schauspieler werden aus NFO-XML gelesen | Happy: `Movie` mit `<actor>`-Elementen wird geparst; Pro Film/Episode entstehen `Actor`-Einträge und `MovieActor`/`TVShowEpisodeActor`-Zuordnungen | `MediaSourceClassifier` (Unit/Integration) |
| Keine Schauspieler in NFO → keine leeren `Actor`-Einträge | Negativ: Film ohne `<actor>` ergibt 0 `Actor`-Einträge; kein NullReference | `MediaSourceClassifier` |
| Duplikate werden vermieden | Negativ: Gleicher Name in zwei Filmen erzeugt nur einen `Actor`-Datensatz | `MediaSourceClassifier` / `Actor` |
| Backup/Restore erfasst Schauspieler-Daten | Happy: Backup enthält `Actors`, `MovieActors`, `TVShowEpisodeActors` und Restore spielt sie wieder ein | `VideoWebPlayerBackupData` (Integration) |

### 2. Nacherfassung des Altbestands

| Kriterium | Geplante Tests |
|-----------|----------------|
| Altbestand wird nachträglich erfasst | Happy: `ActorsBackgroundWorker` findet 3 Filme/Episoden mit fehlendem `ActorsClassifiedAt`, klassifiziert sie und setzt das Flag |
| Unterbrechung wird fortgesetzt | Happy: Worker bricht nach Hälfte ab; bei Neustart werden nur noch offene Altmedien verarbeitet |
| Keine Doppelcode-Logik | Negativ: Worker ruft dieselbe Parse-Methode wie `MediaSourceClassifier` auf, nicht eigene Parser |

### 3. Menü & Übersicht

| Kriterium | Geplante Tests |
|-----------|----------------|
| Menüpunkt sichtbar | E2E/UI: `NavMenu` rendert Link auf `/actors` |
| Suche funktioniert | Service: `ActorSearchService` liefert bei "Scho" Schauspieler, die mit "Scho" beginnen |
| Buchstaben-Filter | Service: Filter-VM zeigt nur Buchstaben, für die mindestens ein Schauspieler existiert (z. B. kein "X", wenn keiner vorhanden) |

### 4. Detailansicht / Aggregation

| Kriterium | Geplante Tests |
|-----------|----------------|
| Alle Filme in Sammlung → nur Sammlung | Service: Schauspieler in 4/4 Filmen → Detail-DTO enthält nur `MovieCollection` |
| Ein Film in Sammlung → nur Film | Service: Schauspieler in 1/4 Filmen → Detail-DTO enthält genau diesen `Movie` |
| 50%-Schwelle | Service: 2/4 Filme (default 50%) → Sammlung; 1/4 (25%) → Einzelfilme |
| Schwellenwert konfigurierbar | Unit: `ActorAggregationOptions` mit anderem Wert (z. B. 30%) ändert Ergebnis |
| Serien analog | Service: Alle Episoden einer Staffel → Staffel; alle Staffeln einer Serie → Serie; sonst Einzelepisoden/Staffeln |

### 5. Autorisierung / Sichtbarkeit

| Kriterium | Geplante Tests |
|-----------|----------------|
| Benutzer sieht nur Schauspieler aus freigeschalteten Quellen | Negativ: Schauspieler, der nur in gesperrten Quellen vorkommt, taucht in Übersicht/Detail nicht auf |
| Keine Quellen-Details preisgeben | Negativ: Detail-DTO enthält keine Medien, auf die der Nutzer keinen Zugriff hat |

## Geplante Dateien & Änderungen

### Datenmodell

- `VideoWebPlayer/Data/Actor.cs` – `Id`, `Name`, `NormalizedName`, `PictureId` (optional)
- `VideoWebPlayer/Data/MovieActor.cs` – Verknüpfung `Movie` ↔ `Actor`
- `VideoWebPlayer/Data/TVShowEpisodeActor.cs` – Verknüpfung `Episode` ↔ `Actor`
- Erweiterungen:
  - `Movie`: `ActorsClassifiedAt` (nullable `DateTime?`)
  - `TVShowEpisode`: `ActorsClassifiedAt` (nullable `DateTime?`)
  - `Setup`: `ActorCollectionThresholdPercent` (int, default 50)
- `ApplicationDbContext` mit neuen `DbSet` und EF-Konfigurationen
- Neue EF-Migration `AddActors`

### Parser / Dienst

- Erweiterung `Movie.LoadFromXml` & `TVShowEpisode.LoadFromXml` um `actor`-Elemente (nur Extraktion als Liste)
- Neuer Dienst `ActorNfoParser` (optional) oder direkte Verarbeitung in `MediaSourceClassifier`.
- `MediaSourceClassifier` erstellt `Actor` und Verknüpfungen; nutzt `GetOrCreateActorsAsync` analog `GetOrCreateGenresAsync`.
- `Movie`/`TVShowEpisode` bekommt `ActorsClassifiedAt = DateTime.UtcNow` nach erfolgreicher Erfassung.

### Hintergrund-Nacherfassung

- Neuer HostedService `ActorBackfillWorker`:
  - Wird beim Programmstart einmalig ausgeführt.
  - Frägt `Movies` und `TVShowEpisodes` mit `ActorsClassifiedAt == null` und vorhandener NFO ab.
  - Wiederverwendet `ActorNfoParser`/`MediaSourceClassifier`-Methoden.
  - Merkt letzte verarbeitete `Id` in `Setup.LastActorBackfillId`, damit Fortsetzung bei Abbruch möglich.
  - Respektiert `BackgroundProcessingGate` / `IHostedService` Pattern.

### API / Client

- `VideoWebPlayer/Controllers/ActorsController.cs`:
  - `GET /api/actors?search=...&initial=...`
  - `GET /api/actors/{id}` (Detail mit aggregierten Movies/Serien)
- `VideoWebPlayer.Client/Models/ActorDtos.cs`:
  - `ActorDto`, `ActorListDto`, `ActorDetailsDto`, `ActorMediaGroupDto`
- `VideoWebPlayer.Client/VideoWebPlayerClient.cs`:
  - `RequestActorsAsync(...)`, `RequestActorAsync(long id)`

### UI

- `VideoWebPlayer/Components/Layout/NavMenu.razor`: Link auf `/actors`
- `VideoWebPlayer/Components/Pages/Actors/Actors.razor`: Übersicht mit Suche + Buchstaben-Filter
- `VideoWebPlayer/Components/Pages/Actors/ActorDetails.razor`: Bild, Name, gruppierte Medien
- `ActorsViewModel.cs` / `ActorDetailsViewModel.cs` in `VideoWebPlayer/ViewModels/`

### Backup/Restore

- `VideoWebPlayerBackupData`: `Actors`, `MovieActors`, `TVShowEpisodeActors` in Export/Import-Logik ergänzen.
- OptionalRestoreColumns/Defaults für nullable `ActorsClassifiedAt` ergänzen.

### Hilfe/Dokumentation

- `docs/help/schauspieler/index.md`: Kurzanleitung
- `README.md`: Feature kurz erwähnen
- `docs/RELEASE_NOTES.md`: Eintrag

## Risiken & offene Entscheidungen

- Bilddaten für Schauspieler: Erst ohne Import; `PictureId` optional vorsehen.
- Schwelle: Default 50%, konfigurierbar über `Setup`.
- Altbestand-Flag: `ActorsClassifiedAt` statt bool, um Unterbrechungen robust fortsetzen zu können.
- Metadaten-Änderungen (neue Filme in Sammlung): Re-Klassifizierung erfolgt automatisch beim regulären Scan; Schauspieler-Zuordnungen werden neu aufgebaut.
