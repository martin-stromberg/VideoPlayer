# Interfaces – Bestandsaufnahme

## Befund

Es existiert **kein Interface für den Quellenzugriff**. `SftpMediaSourceReader` wird in `MediaSourceScanner`, `MediaSourceClassifier` und `ItemsController` als konkrete Klasse injiziert; die Test-Fakes (`FakeSftpMediaSourceReader`, `ThrowingSftpMediaSourceReader`, `SeriesSftpMediaSourceReader`, `BackfillSftpMediaSourceReader` sowie eine anonyme Ableitung in `MediaSourceClassifierActorBackfillTests`, Zeile ~500ff) erweitern die Klasse über `virtual`-Methoden.

Die `virtual`-Methoden (impliziter „Contract" für Fakes): `ReadRootDirectory`, `ReadDirectoryEntries`, `FileExistsAsync`, `ReadFileAsync`, `ReadFileStreamAsync`. Nicht virtuell und daher nicht fakebar: `ReadSubtree`, `GetSftpFileStream`.

## Bereits vorhandene Service-Interfaces (Konventionen im Projekt)

| Interface | Datei | Zweck |
|-----------|-------|-------|
| `IFavoritesService` | `VideoWebPlayer/Services/IFavoritesService.cs` | Favoriten-Service |
| `IGenreService` | `VideoWebPlayer/Services/IGenreService.cs` | Genre-Service |
| `IUnlockedMediaService` | `VideoWebPlayer/Services/IUnlockedMediaService.cs` | Freigabe-Logik (u. a. `GetUnlockedSourceIdsForUserAsync`) |
| `IMediaMetadataWriteCoordinator` | `VideoWebPlayer/Services/IMediaMetadataWriteCoordinator.cs` | Serialisierung von Metadaten-Schreibzugriffen |

Ein `IMediaSourceReader` oder eine Reader-Factory existiert nicht; auch kein `MediaSourceType`-Enum (siehe `models.md`).
