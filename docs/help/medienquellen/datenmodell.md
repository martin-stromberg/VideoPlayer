← [Zurück zur Übersicht](index.md)

# Medienquellen — Datenmodell

## Entitäten

### `MediaSource` (Tabelle `MediaSources`)

Erbt von `MediaEntry` (`Id`, `Name`, `Path`, `CreatedAt`, `ClassifiedAt`, `Changed`).

| Eigenschaft | Typ | Beschreibung |
|-------------|-----|--------------|
| `Host` | `string` (non-nullable) | SFTP-Host; bei lokalen Quellen `string.Empty` |
| `Port` | `int` | SFTP-Port (Vorgabe `22`); bei lokalen Quellen `0` |
| `Username` | `string?` | SFTP-Benutzername; bei lokalen Quellen `null` |
| `Password` | `string?` | SFTP-Passwort; bei lokalen Quellen `null` |
| `SourceType` | `MediaSourceType` (`int`-Spalte, non-nullable, Default `0`) | Quelltyp-Diskriminator, neu durch Migration `AddMediaSourceSourceType` |
| `IconPictureId` | `long?` | Optionales Icon-Bild (`MediaSourceIcons`) |
| `IconPicture` | `MediaSourceIcon?` | Navigation zum Icon |
| `LastScannedAt` | `DateTime?` | Zeitpunkt des letzten Scans |
| `HiddenInMenu` | `bool` (non-nullable, Default `false`) | Menüeintrag der Quelle im `NavMenu` unterdrücken; neu durch Migration `AddMediaSourceHiddenInMenu` |
| `MediaCollections` | `ICollection<MediaCollection>` | Gescannte Collections der Quelle |
| `MediaSourceUsers` | `ICollection<MediaSourceUser>` | Benutzerfreigaben |

### `MediaSourceType` (Enum)

| Wert | Konstante | Bedeutung |
|------|-----------|-----------|
| `0` | `Sftp` | Entfernter SFTP-Server — Default, gilt automatisch für Bestandsdaten |
| `1` | `LocalDirectory` | Lokales Verzeichnis auf dem Server (inkl. UNC-Pfade) |

## Beziehungen

- `MediaSource` 1:n `MediaCollection` — die `Path`-Semantik hängt vom `SourceType` ab: Bei `LocalDirectory` enthalten `MediaCollection.Path`/`MediaItem.Path` OS-Dateisystempfade, bei `Sftp` SFTP-Pfade.
- `MediaSource` 1:n `MediaSourceUser` — Benutzerfreigaben (typunabhängig).
- `MediaSource` n:1 `MediaSourceIcon` — optionales Icon.

## Diagramm

```mermaid
erDiagram
    MEDIA_SOURCE {
        long Id
        string Name
        string Path
        string Host
        int Port
        string Username
        string Password
        int SourceType
        long IconPictureId
        datetime LastScannedAt
        bool HiddenInMenu
    }
    MEDIA_COLLECTION {
        long Id
        long MediaSourceId
        long ParentMediaCollectionId
        string Path
    }
    MEDIA_ITEM {
        long Id
        long MediaCollectionId
        string Path
    }
    MEDIA_SOURCE_USER {
        long MediaSourceId
        string UserId
    }
    MEDIA_SOURCE ||--o{ MEDIA_COLLECTION : "hat"
    MEDIA_COLLECTION ||--o{ MEDIA_ITEM : "enthält"
    MEDIA_SOURCE ||--o{ MEDIA_SOURCE_USER : "freigegeben für"
```

## Hinweise zur Migration und zum Restore

- Die Migration `AddMediaSourceSourceType` (`VideoWebPlayer/Migrations/20260922180232_AddMediaSourceSourceType.cs`) fügt `MediaSources.SourceType` als non-nullable `INTEGER` mit Default `0` hinzu; Bestandsdaten erhalten `Sftp`.
- `VideoWebPlayerBackupData` trägt `MediaSources.SourceType` in `OptionalRestoreColumns` und `OptionalRestoreIntDefaults` (Default `0`) ein: Backups älterer Versionen ohne die Spalte sind weiter restorierbar, neue Backups enthalten die Spalte automatisch (Entity-Iteration über `DbContext.Model`).
- `DtoMediaSource` enthält bewusst kein `SourceType` — kein API-Consumer benötigt den Typ; das reflektionsbasierte `ApiBaseController.Create<T>` kopiert nur typgleiche Properties.
- Die Migration `AddMediaSourceHiddenInMenu` fügt `MediaSources.HiddenInMenu` als non-nullable `INTEGER` mit Default `false` hinzu; Bestandsdaten bleiben dadurch sichtbar. `VideoWebPlayerBackupData` trägt die Spalte in `OptionalRestoreColumns` und `OptionalRestoreBoolDefaults` (Default `false`) ein: Backups älterer Versionen ohne die Spalte stellen die Quellen als sichtbar wieder her.
- `HiddenInMenu` ist rein kosmetisch: `NavMenu.razor` filtert den Menüeintrag clientseitig, während `SourcesController.GetSources`/`GetSource` die Quelle weiterhin liefern (Zugriffssteuerung, Scanner, Planungslogik und Direktaufruf `/mediasource/{id}` sind unverändert). `DtoMediaSource` enthält `HiddenInMenu`, damit externe Clients das Flag auswerten können.

## Altersfreigabe-Felder an `Movies` und `TVShows`

Bei der Klassifizierung persistiert der `MediaSourceClassifier` die Altersfreigabe auf `Movie` und `TVShow` (gemeinsames Interface `IAgeRated`). `TVShowSeason`, `TVShowEpisode` und `MovieCollection` tragen keine eigenen Felder — die Detailansicht zeigt bei Staffeln und Episoden die Serienfreigabe (Anzeige-Vererbung).

| Eigenschaft | Typ | Beschreibung |
|-------------|-----|--------------|
| `AgeRating` | `string?` | Kanonischer Anzeigewert („FSK 12", „PG-13"); bei unbekannten Stufen der getrimmte Rohwert |
| `AgeRatingRaw` | `string?` | Unveränderter Quellwert aus der NFO (`<mpaa>`/`<certification>`) bzw. von TMDB („{Land}:{Certification}") |
| `AgeRatingMinAge` | `int?` | Vergleichbares Mindestalter für spätere Filter-/Zugriffsregeln; `null` bei unbekannter Stufe |
| `AgeRatingClassifiedAt` | `DateTime?` | Marker der Altersfreigabe-Klassifizierung für den Backfill (Muster `ActorsClassifiedAt`) |

- Die Migration `AddAgeRatings` fügt alle acht Spalten (je vier auf `Movies` und `TVShows`) nullable hinzu — keine Defaults nötig.
- `VideoWebPlayerBackupData.OptionalRestoreColumns` enthält alle acht Spalten: Backups aus Versionen vor dem Feature sind weiter restorierbar.
- DTO-Grenze: `DtoMovie`/`DtoTVShow` enthalten `AgeRating` und `AgeRatingMinAge` (automatische Befüllung durch `ApiBaseController.Create<T>`); `AgeRatingRaw` wird nicht nach außen gegeben. `MediaMetadataUpdateRequest.AgeRating` macht den Wert im Admin-Editiermodus pflegbar.
