# Datenmodell — Bestandsaufnahme

Betroffene Datenmodellklassen und ihr heutiger Stand. Es existiert noch **kein** `HiddenInMenu`-Merkmal.

## `MediaSource`
Datei: `VideoWebPlayer/Data/MediaSource.cs` (Encoding: Windows-1252, nicht UTF-8)

Erbt von `MediaEntry`. Eine Eigenschaft `HiddenInMenu` existiert **nicht**.

| Eigenschaft | Typ | Beschreibung / Zweck |
|-------------|-----|----------------------|
| `Host` | `string` | Hostname der Quelle (SFTP); non-nullable, Default `string.Empty` |
| `Port` | `int` | Port der Quelle |
| `Username` | `string?` | Benutzername für Authentifizierung |
| `Password` | `string?` | Passwort für Authentifizierung |
| `SourceType` | `MediaSourceType` | Quelltyp-Diskriminator (SFTP / lokales Verzeichnis), Default `Sftp` |
| `IconPictureId` | `long?` | Optionales Icon-Bild (Tabelle `MediaSourceIcons`) |
| `IconPicture` | `MediaSourceIcon?` | Navigation zum Icon |
| `LastScannedAt` | `DateTime?` | Zeitpunkt des letzten Scans |
| `MediaCollections` | `ICollection<MediaCollection>` | Collections der Quelle |
| `MediaSourceUsers` | `ICollection<MediaSourceUser>` | Benutzerfreigaben |

Geerbte Eigenschaften von `MediaEntry` (`VideoWebPlayer/Data/MediaEntry.cs`):

| Eigenschaft | Typ | Beschreibung / Zweck |
|-------------|-----|----------------------|
| `Id` | `long` | Eindeutige ID |
| `Name` | `string` | Anzeigename |
| `Path` | `string` | Pfad / Verbindungsinformation |
| `CreatedAt` | `DateTime` | Erstellungszeitpunkt |
| `ClassifiedAt` | `DateTime?` | Zeitpunkt der letzten Klassifizierung |
| `Changed` | `bool` | „Geändert"-Flag |

## `MediaSourceUser`
Datei: `VideoWebPlayer/Data/MediaSourceUser.cs` (globaler Namespace, kein `namespace VideoWebPlayer.Data` um die Klasse — die Datei importiert den Namespace)

Zuordnungstabelle Benutzer ↔ Quelle; Grundlage der Quellenliste in `SourcesController.GetSources`.

| Eigenschaft | Typ | Beschreibung / Zweck |
|-------------|-----|----------------------|
| `MediaSourceId` | `long` | Fremdschlüssel zur Quelle |
| `MediaSource` | `MediaSource` | Navigation |
| `UserId` | `string` | Fremdschlüssel zum Benutzer |
| `User` | `ApplicationUser` | Navigation |

## `Setup` (Vergleichsmuster `TvSenderEnabled`)
Datei: `VideoWebPlayer/Data/Setup.cs`

Kein direkt betroffenes Modell, aber das Namensvorbild der Anforderung: `public bool TvSenderEnabled { get; set; }` (Zeile 80) — globaler Release-Schalter des TV-Senders, analog mit Migration `AddSetupTvSenderEnabled`, `OptionalRestoreColumns`-Eintrag und `OptionalRestoreBoolDefaults`-Default `false` eingeführt.

## `DtoMediaSource`
Datei: `VideoWebPlayer.Client/Models/DtoSource.cs` (Namespace `VideoWebPlayer.Client.Models`)

Erbt von `DtoMediaEntry` (`VideoWebPlayer.Client/Models/DtoMovie.cs`, Zeile 14). `HiddenInMenu` existiert **nicht**.

| Eigenschaft | Typ | Beschreibung / Zweck |
|-------------|-----|----------------------|
| `CreatedAt` | `DateTime` | Erstellungszeitpunkt |
| `ClassifiedAt` | `DateTime?` | Zeitpunkt der letzten Klassifizierung |
| `Changed` | `bool` | „Geändert"-Flag |
| `LastScannedAt` | `DateTime?` | Zeitpunkt des letzten Scans |
| `IconPictureId` | `long?` | Optionales Icon-Bild |

Geerbt von `DtoMediaEntry`: `Id`, `Name`, `MediaSourceId`, `ReleaseDate`, `PremieredAt`, `EndedAt`, `PosterPictureId`, `BannerPictureId`, `FanartPictureId` u. a.

Hinweis: `DtoMediaSource` enthält bewusst **kein** `SourceType` (dokumentierte DTO-Grenze in `docs/help/medienquellen/datenmodell.md`).

## Persistenz-Infrastruktur

- `ApplicationDbContext.MediaSources` (`VideoWebPlayer/Data/ApplicationDbContext.cs`, Zeile 45): `DbSet<MediaSource>`.
- `ApplicationDbContext.MediaSourceUsers` (Zeile 49): `DbSet<MediaSourceUser>`; Konfiguration über `VideoWebPlayer/Data/Configurations/MediaSourceUserConfiguration.cs`.
- Migrationen unter `VideoWebPlayer/Migrations/`; Vorbild `20261023050115_AddSetupTvSenderEnabled.cs`: `AddColumn<bool>` mit `type: "INTEGER"`, `nullable: false`, `defaultValue: false`.
- `ApplicationDbContextModelSnapshot.cs` enthält aktuell kein `HiddenInMenu`-Property (zu `MediaSource` nur die o. g. Felder).
