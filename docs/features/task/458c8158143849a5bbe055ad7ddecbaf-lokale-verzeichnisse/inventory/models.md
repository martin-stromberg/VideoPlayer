# Datenmodell – Bestandsaufnahme

Betroffene Modelle für die Anforderung „Lokales Verzeichnis als Medienquelle". Es existiert aktuell kein Quelltyp-Feld; `MediaSource` enthält ausschließlich SFTP-Verbindungsdaten.

## `MediaSource`
Datei: `VideoWebPlayer/Data/MediaSource.cs` (Achtung: Datei ist Windows-1252-kodiert, nicht UTF-8)

Erbt von `MediaEntry`.

| Eigenschaft | Typ | Beschreibung / Zweck |
|-------------|-----|----------------------|
| `Host` | `string` (required, Default `string.Empty`) | Hostname des SFTP-Servers |
| `Port` | `int` | SFTP-Port (UI-Vorgabe 22) |
| `Username` | `string?` | Benutzername für SFTP-Authentifizierung |
| `Password` | `string?` | Passwort für SFTP-Authentifizierung |
| `IconPictureId` | `long?` | FK auf `MediaSourceIcons` (optionales Quellen-Icon) |
| `IconPicture` | `MediaSourceIcon?` | Navigation zum Icon |
| `LastScannedAt` | `DateTime?` | Zeitpunkt des letzten Scans |
| `MediaCollections` | `ICollection<MediaCollection>` | Navigation: Collections der Quelle |
| `MediaSourceUsers` | `ICollection<MediaSourceUser>` | Navigation: Benutzerfreigaben |

**Keine** Typ-/Diskriminator-Eigenschaft vorhanden — kein `SourceType`, kein Enum. Im Modell-Snapshot (`VideoWebPlayer/Migrations/ApplicationDbContextModelSnapshot.cs`, Zeilen 698–745) sind die Spalten `Host` (required), `Port`, `Username` (nullable), `Password` (nullable), `Path` (required), `Name` (required), `CreatedAt`, `ClassifiedAt`, `Changed`, `IconPictureId`, `LastScannedAt` für die Tabelle `MediaSources` definiert.

## `MediaEntry` (Basisklasse)
Datei: `VideoWebPlayer/Data/MediaEntry.cs`

| Eigenschaft | Typ | Beschreibung / Zweck |
|-------------|-----|----------------------|
| `Id` | `long` | Primärschlüssel |
| `Name` | `string` | Anzeigename |
| `Path` | `string` | „Pfad oder Verbindungsinformation" — bei SFTP der Remote-Pfad; würde bei lokalen Quellen als lokaler Root-Pfad dienen |
| `CreatedAt` | `DateTime` | Erstellungszeitpunkt (vom Scanner für Änderungserkennung verwendet) |
| `ClassifiedAt` | `DateTime?` | Zeitpunkt der letzten Klassifizierung |
| `Changed` | `bool` | Änderungsflag für Klassifizierung |

## `MediaCollection`
Datei: `VideoWebPlayer/Data/MediaCollection.cs` (erbt `MediaEntry`)

| Eigenschaft | Typ | Beschreibung / Zweck |
|-------------|-----|----------------------|
| `MediaSourceId` | `long` | FK zur Quelle |
| `ParentMediaCollectionId` | `long?` | FK auf Eltern-Collection (`null` = Root) |
| `LastScannedAt` | `DateTime?` | Letzter Scan |
| `ScanDueAt` | `DateTime?` | Nächster fälliger Scan (vom `MediaSourceScanner` abgefragt) |
| `Classifyable` | `bool` | Vollständig gescannt / bereit zur Klassifizierung |
| `MediaSource` | `MediaSource` | Navigation (Reader greifen darüber auf `Host`/`Port`/`Path` zu) |
| `ParentMediaCollection` | `MediaCollection?` | Navigation nach oben |
| `ChildCollections` | `ICollection<MediaCollection>` | Navigation zu Kindern |
| `MediaItems` | `ICollection<MediaItem>` | Enthaltene Dateien |
| `Skip` | `bool` (`internal set`) | Scan-Überspringflag |

## `MediaItem`
Datei: `VideoWebPlayer/Data/MediaItem.cs` (erbt `MediaEntry`)

| Eigenschaft | Typ | Beschreibung / Zweck |
|-------------|-----|----------------------|
| `MediaCollectionId` | `long` | FK zur Collection |
| `MediaCollection` | `MediaCollection` | Navigation |

## `MediaSourceIcon`
Datei: `VideoWebPlayer/Data/MediaSourceIcon.cs`

| Eigenschaft | Typ | Beschreibung / Zweck |
|-------------|-----|----------------------|
| `Id` | `long` | Primärschlüssel |
| `Data` | `byte[]` | Bilddaten |
| `ContentType` | `string` | MIME-Typ (Default `image/png`) |

## `MediaSourceUser`
Datei: `VideoWebPlayer/Data/MediaSourceUser.cs`

| Eigenschaft | Typ | Beschreibung / Zweck |
|-------------|-----|----------------------|
| `MediaSourceId` | `long` | FK zur Quelle (Teil des Composite Keys) |
| `MediaSource` | `MediaSource` | Navigation |
| `UserId` | `string` | FK zum Benutzer (Teil des Composite Keys) |
| `User` | `ApplicationUser` | Navigation |

## `DtoMediaSource`
Datei: `VideoWebPlayer.Client/Models/DtoSource.cs` (erbt `DtoMediaEntry`)

| Eigenschaft | Typ | Beschreibung / Zweck |
|-------------|-----|----------------------|
| `CreatedAt` | `DateTime` | Erstellungszeitpunkt |
| `ClassifiedAt` | `DateTime?` | Klassifizierungszeitpunkt |
| `Changed` | `bool` | Änderungsflag |
| `LastScannedAt` | `DateTime?` | Letzter Scan |
| `IconPictureId` | `long?` | Icon-FK |

Kein Quelltyp im DTO vorhanden. Mapping erfolgt reflektionsbasiert über `ApiBaseController.Create<T>` (`VideoWebPlayer/Controllers/ApiBaseController.cs`, Zeile 75) — gleichnamige Properties werden automatisch kopiert; eine neue `SourceType`-Eigenschaft müsste nur im DTO ergänzt werden, um exponiert zu werden. Verwendet u. a. in `SourcesController.GetSources`/`GetSource` (`VideoWebPlayer/Controllers/SourcesController.cs`, Zeilen 58, 99) und `MediaSourceDetailsViewModel` (`VideoWebPlayer/ViewModels/MediaSourceDetailsViewModel.cs`, Zeile 21).

## Migrationen

Verzeichnis `VideoWebPlayer/Migrations/` enthält 60+ vorhandene EF-Core-Migrationen (letzte: `20260831050031_AddActorRoleAndOrder`). Für eine neue `SourceType`-Spalte ist eine weitere Migration erforderlich — aktuell existiert keine.
