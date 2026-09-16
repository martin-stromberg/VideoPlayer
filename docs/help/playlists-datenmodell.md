# Playlists – Datenmodell

## Übersicht

Das Playlist-Feature verwendet zwei Hauptentitäten:
1. **`Playlist`** (bestehend, Erweiterung — u. a. Cover-Felder `CoverPictureId`/`CoverPictureIsUserUploaded`)
2. **`PlaylistEntry`** (neu)

Eine `Playlist` gehört einem Benutzer und enthält eine Sammlung von `PlaylistEntry`-Einträgen.
Zusätzlich wurde die bestehende **`Picture`**-Entität um die Spalte `PlaylistId` erweitert, damit
sie als Coverbild einer Playlist dienen kann.

---

## Entität: `Playlist`

Repräsentiert eine Benutzer-Playlist.

### Spalten

| Spalte | Typ | Beschreibung |
|--------|-----|--------------|
| `Id` | `long` (PK) | Eindeutige Identifier |
| `UserId` | `string` (FK) | Verweis auf `ApplicationUser.Id` |
| `Name` | `string` | Name der Playlist (max. 255 Zeichen) |
| `Description` | `string?` | Optionale Beschreibung (max. 2000 Zeichen) |
| `SortMode` | `PlaylistSortMode` (Enum) | Sortiermodus: `ByReleaseDate` oder `Manual` |
| `CreatedAt` | `DateTime` | Erstellungszeitpunkt (UTC) |
| `UpdatedAt` | `DateTime` | Letzte Änderung (UTC) |
| `GenresManuallyOverridden` | `bool` | `true`, wenn der Besitzer die Genres manuell gesetzt hat (dann keine automatische Neuberechnung, siehe BR-21) |
| `CoverPictureId` | `long?` (FK) | Verweis auf `Pictures.Id` — das Coverbild der Playlist (hochgeladen oder generiert); `null` = kein Cover gesetzt |
| `CoverPictureIsUserUploaded` | `bool` | `true` = Cover wurde vom Besitzer hochgeladen, `false` = automatisch als Collage erzeugt |

### Navigation

| Navigation | Zieltyp | Multiplizität | Beschreibung |
|------------|---------|---------------|--------------|
| `PlaylistEntries` | `ICollection<PlaylistEntry>` | 1:n | Alle Einträge dieser Playlist |
| `CoverPicture` | `Picture?` | n:1 | Das aktuelle Coverbild (über `CoverPictureId`) |

### Constraints

- **Primary Key:** `Id`
- **Foreign Key:** `UserId` → `ApplicationUser.Id` (mit CascadeDelete)
- **Foreign Key:** `CoverPictureId` → `Pictures.Id` (optional, ohne explizites Löschverhalten — das
  Aufräumen des referenzierten Bildes übernimmt `PlaylistService`, siehe `playlists-business-rules.md`,
  BR-26)
- **Eindeutigkeit:** Pro Benutzer: `(UserId, Name)` darf nicht doppelt vorkommen (Case-Insensitive)

### Indizes

- Primary Index auf `Id`
- Index auf `UserId` (zum schnellen Abrufen aller Playlists eines Benutzers)
- Unique Index auf `(UserId, Name)` (Eindeutigkeit des Namens pro Benutzer, NOCASE)
- Index auf `CoverPictureId` (Fremdschlüssel-Index, Migration `AddPlaylistCoverFields`)

---

## Entität: `PlaylistEntry` (NEU)

Repräsentiert einen einzelnen Medieninhalt in einer Playlist.

### Spalten

| Spalte | Typ | Beschreibung |
|--------|-----|--------------|
| `Id` | `long` (PK) | Eindeutige Identifier |
| `PlaylistId` | `long` (FK) | Verweis auf `Playlist.Id` |
| `Playlist` | Navigation | Referenz zur Besitzer-Playlist |
| `MediaType` | `string` (Required) | Art des Medieninhalts (kanonische Schreibweise): `"Movie"`, `"TVShow"`, `"TVShowSeason"`, `"TVShowEpisode"`, `"MovieCollection"`. Input wird normalisiert, um case-insensitive Duplikat-Erkennung zu gewährleisten. |
| `MediaId` | `long` | ID des Medieninhalts in seiner Tabelle (z. B. Movie.Id, TVShow.Id) |
| `ParentMediaType` | `string?` | Medientyp des Sammelwerks, falls dieser Eintrag durch Cascade hinzugefügt wurde (z. B. `"TVShow"` oder `"TVShowSeason"` oder `"MovieCollection"`); `null` für Top-Level-Einträge |
| `ParentMediaId` | `long?` | ID des Sammelwerks (z. B. der Serie, wenn dieser Eintrag eine Episode ist); `null` für Top-Level-Einträge |
| `AddedAt` | `DateTime` | Zeitpunkt des Hinzufügens zur Playlist (UTC) |
| `SortOrder` | `long?` | Position im Sortiermodus `Manual` (aufsteigend, kleiner = weiter vorne); `null` = noch keine Position zugewiesen bzw. im `ByReleaseDate`-Modus irrelevant (siehe `playlists-business-rules.md`, BR-13 und `playlists-api.md`, Hinweis zu `SortOrder`) |

### Navigation

| Navigation | Zieltyp | Multiplizität | Beschreibung |
|------------|---------|---------------|--------------|
| `Playlist` | `Playlist` | n:1 | Die Playlist, der dieser Eintrag angehört |

### Constraints

- **Primary Key:** `Id`
- **Foreign Key:** `PlaylistId` → `Playlist.Id` (mit CascadeDelete: Löschen der Playlist löscht alle Einträge)
- **Composite Unique Constraint:** `(PlaylistId, MediaType, MediaId)` — keine Duplikate pro Playlist
- **Validierung:** `MediaId > 0` (serverseitig geprüft)

### Indizes

- Primary Index auf `Id`
- Foreign Key Index auf `PlaylistId`
- **Composite Index** auf `(PlaylistId, MediaType, MediaId)` (für Duplikatsprüfung)
- Index auf `(PlaylistId, ParentMediaType, ParentMediaId)` (wird für die Fallback-Sortierung nach Hierarchie genutzt, siehe `playlists-business-rules.md`, BR-13)
- Index auf `(PlaylistId, SortOrder)` (`IX_PlaylistEntries_PlaylistId_SortOrder`, für die manuelle Sortierreihenfolge)

---

## Entität: `Picture` (Erweiterung für Playlist-Cover)

Die bestehende `Picture`-Entität (speichert Bilddaten samt `Type`, `ContentType`, `Width`,
`Height`) wurde für die Playlist-Abbildungen um eine Spalte erweitert:

| Spalte | Typ | Beschreibung |
|--------|-----|--------------|
| `PlaylistId` | `long?` | Rückverweis auf die `Playlist`, deren Cover dieses Bild ist — analog zum bestehenden `EpisodeId`-Muster für generierte Episoden-Hintergrundbilder |

Ein als Playlist-Cover verwendetes `Picture` trägt `Type = "cover"` und `PlaylistId` = ID der
Playlist. Zwei Varianten:

- **Hochgeladen** (`IsGeneratedBackground = false`): `ContentType` = MIME-Type des Uploads,
  `Width`/`Height` = ermittelte Bildabmessungen.
- **Generiert** (`IsGeneratedBackground = true`): `ContentType = "image/jpeg"`,
  `Width`/`Height` = konfigurierte Collage-Abmessungen (Standard 1600 × 520).

Die Playlist verweist über `Playlist.CoverPictureId` auf das Bild; `Picture.PlaylistId` dient als
Rückverweis für Abfragen und Cleanup. Index: `(PlaylistId, IsGeneratedBackground)` auf `Pictures`.

**Backup-Hinweis:** `Picture`-Zeilen mit `IsGeneratedBackground = true` werden nicht ins Backup
exportiert; beim Export wird `Playlist.CoverPictureId` daher nur für hochgeladene Cover
mitgeschrieben (siehe `playlists-business-rules.md`, BR-24/BR-26).

---

## Beziehungsdiagramm (ER-Diagramm)

```
┌──────────────────────────────────────┐
│         ApplicationUser              │
│  (AspNetCore Identity)               │
├──────────────────────────────────────┤
│ Id (PK)                              │
│ UserName                             │
│ ...                                  │
└──────────────────────────────────────┘
         ▲
         │ (1:n)
         │ UserId
         │
┌──────────────────────────────────────┐          ┌──────────────────────────┐
│         Playlist                     │          │        Picture           │
├──────────────────────────────────────┤          ├──────────────────────────┤
│ Id (PK)                              │          │ Id (PK)                  │
│ UserId (FK)                          │          │ Type ("cover")           │
│ Name                                 │   (n:1)  │ Data, ContentType        │
│ Description                          │ ────────→│ Width, Height            │
│ SortMode                             │  Cover   │ IsGeneratedBackground    │
│ CreatedAt                            │          │ PlaylistId (Rueckverweis)│
│ UpdatedAt                            │          └──────────────────────────┘
│ CoverPictureId (FK, nullable)        │
│ CoverPictureIsUserUploaded           │
│ PlaylistEntries                      │
└──────────────────────────────────────┘
         ▲
         │ (1:n)
         │ PlaylistId
         │
┌──────────────────────────────────────┐
│      PlaylistEntry (NEU)             │
├──────────────────────────────────────┤
│ Id (PK)                              │
│ PlaylistId (FK) ──────┐              │
│ Playlist              │              │
│ MediaType             │              │
│ MediaId               │              │
│ ParentMediaType       │              │
│ ParentMediaId         │              │
│ AddedAt               │              │
│ SortOrder             │              │
│                       │              │
│ UC: (PlaylistId,      │              │
│      MediaType,       │              │
│      MediaId)         │              │
└──────────────────────────────────────┘

                    
Externe Verweise (keine FK, da Medien-Tabellen separate Entitäten sind):
                    
PlaylistEntry.MediaId  ──→ Movie.Id
                       ──→ TVShow.Id
                       ──→ TVShowSeason.Id
                       ──→ TVShowEpisode.Id
                       ──→ MovieCollection.Id
```

---

## Mermaid ER-Diagramm

```mermaid
erDiagram
    APPLICATIONUSER ||--o{ PLAYLIST : owns
    PLAYLIST ||--o{ PLAYLISTENTRY : contains
    PICTURE |o--o{ PLAYLIST : "cover of (CoverPictureId)"
    PLAYLISTENTRY }o--|| MOVIE : references
    PLAYLISTENTRY }o--|| TVSHOW : references
    PLAYLISTENTRY }o--|| TVSHOWSEASON : references
    PLAYLISTENTRY }o--|| TVSHOWPISODE : references
    PLAYLISTENTRY }o--|| MOVIECOLLECTION : references

    APPLICATIONUSER {
        string Id PK
        string UserName
    }

    PLAYLIST {
        long Id PK
        string UserId FK
        string Name
        string Description
        string SortMode
        datetime CreatedAt
        datetime UpdatedAt
        bool GenresManuallyOverridden
        long CoverPictureId FK
        bool CoverPictureIsUserUploaded
    }

    PLAYLISTENTRY {
        long Id PK
        long PlaylistId FK
        string MediaType
        long MediaId
        string ParentMediaType
        long ParentMediaId
        datetime AddedAt
        long SortOrder
    }

    PICTURE {
        long Id PK
        string Type
        bytes Data
        string ContentType
        bool IsGeneratedBackground
        long PlaylistId
    }

    MOVIE {
        long Id PK
        string Title
    }

    TVSHOW {
        long Id PK
        string Title
    }

    TVSHOWSEASON {
        long Id PK
        long TVShowId FK
        string Title
    }

    TVSHOWPISODE {
        long Id PK
        long TVShowSeasonId FK
        string Title
    }

    MOVIECOLLECTION {
        long Id PK
        string Title
    }
```

---

## Datenbankmigrationen

### Migration: `AddPlaylistCoverFields` (neu in Schritt 10)

**Betroffene Tabellen:**
- `Playlists` (neue Spalten `CoverPictureId`, `CoverPictureIsUserUploaded`, Index + Fremdschlüssel)
- `Pictures` (neue Spalte `PlaylistId`, Index)

**Beschreibung:** Fügt die Cover-Verwaltung hinzu. `CoverPictureId` ist ein optionaler
Fremdschlüssel auf `Pictures.Id` (kein explizites `OnDelete`-Verhalten — das Aufräumen des
referenzierten Bildes übernimmt der Service, siehe BR-26 in `playlists-business-rules.md`).
`CoverPictureIsUserUploaded` ist ein nicht-nullbares `bool` mit Standardwert `false`. Auf
`Pictures` wird `PlaylistId` (nullable, bewusst ohne Fremdschlüssel — Rückverweis analog zu
`Picture.EpisodeId`) sowie ein Index auf `(PlaylistId, IsGeneratedBackground)` angelegt.

**SQL-Aktion (vereinfacht):**
```sql
ALTER TABLE Playlists ADD COLUMN CoverPictureId INTEGER NULL;
ALTER TABLE Playlists ADD COLUMN CoverPictureIsUserUploaded INTEGER NOT NULL DEFAULT 0;
ALTER TABLE Pictures ADD COLUMN PlaylistId INTEGER NULL;

CREATE INDEX IX_Playlists_CoverPictureId ON Playlists(CoverPictureId);
CREATE INDEX IX_Pictures_PlaylistId_IsGeneratedBackground ON Pictures(PlaylistId, IsGeneratedBackground);

ALTER TABLE Playlists ADD CONSTRAINT FK_Playlists_Pictures_CoverPictureId
    FOREIGN KEY (CoverPictureId) REFERENCES Pictures(Id);
```

**EF Core Konfiguration (`PlaylistConfiguration`):**
```csharp
builder
    .HasOne(p => p.CoverPicture)
    .WithMany()
    .HasForeignKey(p => p.CoverPictureId);
```

### Migration: `NormalizePlaylistEntryMediaTypes` (neu in Schritt 2.1)

**Betroffene Tabellen:**
- `PlaylistEntries.MediaType` (Wert-Migration)

**Beschreibung:** Normalisiert alle bestehenden `MediaType`-Werte auf ihre kanonischen Enum-Werte.
Dies stellt sicher, dass Duplikat-Prüfungen case-insensitiv funktionieren (z. B. `"movie"` wird zu `"Movie"`).

**SQL-Aktion (vereinfacht):**
```sql
UPDATE PlaylistEntries SET MediaType = 'Movie' WHERE LOWER(MediaType) = 'movie';
UPDATE PlaylistEntries SET MediaType = 'TVShow' WHERE LOWER(MediaType) = 'tvshow';
UPDATE PlaylistEntries SET MediaType = 'TVShowSeason' WHERE LOWER(MediaType) = 'tvshowseason';
UPDATE PlaylistEntries SET MediaType = 'TVShowEpisode' WHERE LOWER(MediaType) = 'tvshowepisode';
UPDATE PlaylistEntries SET MediaType = 'MovieCollection' WHERE LOWER(MediaType) = 'moviecollection';
```

### Migration: `AddPlaylistEntriesTable`

**Betroffene Tabellen:**
- `PlaylistEntries` (neue Tabelle)

**SQL-Aktion (vereinfacht):**
```sql
CREATE TABLE PlaylistEntries (
    Id BIGINT PRIMARY KEY IDENTITY(1,1),
    PlaylistId BIGINT NOT NULL,
    MediaType NVARCHAR(255) NOT NULL,
    MediaId BIGINT NOT NULL,
    ParentMediaType NVARCHAR(255) NULL,
    ParentMediaId BIGINT NULL,
    AddedAt DATETIME2 NOT NULL,
    FOREIGN KEY (PlaylistId) REFERENCES Playlists(Id) ON DELETE CASCADE,
    UNIQUE (PlaylistId, MediaType, MediaId)
);

CREATE INDEX IX_PlaylistEntries_PlaylistId ON PlaylistEntries(PlaylistId);
CREATE INDEX IX_PlaylistEntries_Parent ON PlaylistEntries(PlaylistId, ParentMediaType, ParentMediaId);
```

**Entity Framework Core Konfiguration (PlaylistEntryConfiguration):**
```csharp
public class PlaylistEntryConfiguration : IEntityTypeConfiguration<PlaylistEntry>
{
    public void Configure(EntityTypeBuilder<PlaylistEntry> builder)
    {
        builder.HasKey(e => e.Id);

        builder.Property(e => e.MediaType).IsRequired();
        builder.Property(e => e.MediaId).IsRequired();
        builder.Property(e => e.AddedAt).IsRequired();

        builder.HasOne(e => e.Playlist)
            .WithMany(p => p.PlaylistEntries)
            .HasForeignKey(e => e.PlaylistId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(e => new { e.PlaylistId, e.MediaType, e.MediaId })
            .IsUnique(true)
            .HasName("IX_PlaylistEntries_Unique_Key");

        builder.HasIndex(e => new { e.PlaylistId, e.ParentMediaType, e.ParentMediaId })
            .HasName("IX_PlaylistEntries_Parent");
    }
}
```

---

## Datenbeispiele

### Beispiel-Daten in `PlaylistEntry`

Angenommen:
- Playlist ID=1 gehört Benutzer "user1"
- Serie "The Crown" hat ID=100, mit Staffel 1 (ID=200) und Episoden (ID=1001, 1002)
- Film "The Matrix" hat ID=500

**Szenario:** Benutzer fügt:
1. Film "The Matrix" hinzu
2. Serie "The Crown" hinzu

**Resultierende PlaylistEntries:**

| Id | PlaylistId | MediaType | MediaId | ParentMediaType | ParentMediaId | AddedAt |
|----|----------|-----------|---------|-----------------|---------------|---------|
| 1 | 1 | Movie | 500 | null | null | 2026-09-05 14:00:00Z |
| 2 | 1 | TVShow | 100 | null | null | 2026-09-05 14:05:00Z |
| 3 | 1 | TVShowSeason | 200 | TVShow | 100 | 2026-09-05 14:05:00Z |
| 4 | 1 | TVShowEpisode | 1001 | TVShowSeason | 200 | 2026-09-05 14:05:00Z |
| 5 | 1 | TVShowEpisode | 1002 | TVShowSeason | 200 | 2026-09-05 14:05:00Z |

**Fallback-Sortierung nach Hierarchie (genutzt, wenn kein Erscheinungsdatum vorhanden ist, siehe BR-13):**
- Film: The Matrix (1 Eintrag, top-level)
- Serie: The Crown (1 Eintrag, top-level)
  - Staffel 1 (1 Eintrag, Parent=Serie)
    - Episode 1 (1 Eintrag, Parent=Staffel)
    - Episode 2 (1 Eintrag, Parent=Staffel)

---

## Kardinalitäten

| Beziehung | Multiplizität | Beschreibung |
|-----------|--------------|--------------|
| ApplicationUser → Playlist | 1:n | Ein Benutzer hat mehrere Playlists |
| Playlist → PlaylistEntry | 1:n | Eine Playlist enthält mehrere Einträge |
| PlaylistEntry → Playlist | n:1 | Jeder Eintrag gehört zu genau einer Playlist |
| Playlist → Picture (Cover) | n:1 (optional) | Eine Playlist referenziert höchstens ein Cover-Bild; ein `Picture` kann als Cover mehrerer Kontexte dienen (kein Unique-Constraint auf `CoverPictureId`) |
| Picture → Playlist (Rückverweis) | n:1 (optional, ohne FK) | `Picture.PlaylistId` ist nur eine Spalte, kein Fremdschlüssel |

**Keine Fremdschlüssel zu Medien:** `PlaylistEntry` speichert nur die ID und den Typ des Medieninhalts, nicht eine Fremdschlüssel-Referenz. Dies ist bewusst, um Entkopplung zu erreichen (Medien-Tabellen sind separaten Services/Bounded Contexts).

---

## Performance-Hinweise

### Indizes

1. **Clustered Index** auf `(PlaylistId)` — schnelle Abfrage aller Einträge einer Playlist
2. **Unique Index** auf `(PlaylistId, MediaType, MediaId)` — Duplikatsprüfung bei Insert
3. **Non-Clustered Index** auf `(PlaylistId, ParentMediaType, ParentMediaId)` — genutzt für die Fallback-Sortierung nach Hierarchie (siehe BR-13 in `playlists-business-rules.md`)

### Query-Optimierungen

Typische Abfragen:
```csharp
// Abfrage: Alle Einträge einer Playlist
var entries = db.PlaylistEntries
    .Where(e => e.PlaylistId == id)
    .ToList();
// → Nutzt Clustered Index auf PlaylistId

// Abfrage: Duplikat-Prüfung
var isDuplicate = db.PlaylistEntries
    .Any(e => e.PlaylistId == id && e.MediaType == type && e.MediaId == mid);
// → Nutzt Unique Index auf (PlaylistId, MediaType, MediaId)

// Abfrage: Alle Kind-Einträge einer Serie (Beispiel, aktuell nicht als eigenständige Abfrage im Code)
var childEntries = db.PlaylistEntries
    .Where(e => e.PlaylistId == id && e.ParentMediaType == "TVShow" && e.ParentMediaId == seriesId)
    .ToList();
// → Nutzt Non-Clustered Index auf (PlaylistId, ParentMediaType, ParentMediaId)
```

---

## Kaskadierende Löschungen

### Cascade-Delete-Verhalten

Wenn eine `Playlist` gelöscht wird:
- **Alle zugehörigen `PlaylistEntry`-Reihen werden automatisch gelöscht**
- Foreign Key: `PlaylistEntry.PlaylistId` → `Playlist.Id` mit `DeleteBehavior.Cascade`
- **Das Cover-`Picture` wird service-seitig gelöscht:** `PlaylistService.DeletePlaylistAsync` entfernt
  die über `CoverPictureId` referenzierte `Picture`-Zeile zusammen mit der Playlist. Der
  `CoverPictureId`-Fremdschlüssel hat kein `OnDelete`-Verhalten; das Cleanup liegt bewusst im
  Service (analog zu `TVShowEpisode.GeneratedBackgroundPicture`).

Beim **Ersetzen eines Covers** (neuer Upload oder Regenerierung):
- `PlaylistService.ReplaceCoverPictureAsync` fügt das neue `Picture` ein, aktualisiert
  `CoverPictureId`/`CoverPictureIsUserUploaded` und löscht das bisherige `Picture` in **einem**
  `SaveChangesAsync`-Aufruf. Verwaiste Cover-Bilder entstehen dadurch nicht.

Wenn ein Medieninhalt gelöscht wird:
- **Zugehörige `PlaylistEntry`-Reihen werden NICHT automatisch gelöscht** (kein FK)
- Stattdessen: Beim nächsten Laden der Playlist werden verwaiste Einträge identifiziert und gelöscht (siehe `GetPlaylistEntriesAsync()`)

---

## Constraints und Validierung

| Constraint | Ort | Typ | Beschreibung |
|-----------|-----|-----|--------------|
| Unique (PlaylistId, MediaType, MediaId) | DB | Eindeutigkeit | Keine Duplikate pro Playlist; MediaType ist normalisiert, daher case-insensitive Duplikat-Erkennung |
| Foreign Key (PlaylistId) | DB | Referenzielle Integrität | PlaylistEntry muss zu existierender Playlist gehören |
| MediaId > 0 | Service | Business Logic | Positive IDs nur |
| MediaType normalisiert | Service | Normalisierung | MediaType-Input wird auf kanonischen Enum-Wert normalisiert vor Speicherung |
| ParentMediaType in {TVShow, TVShowSeason, MovieCollection, null} | Service | Enumeration | Nur gültige Parent-Typen |
| Foreign Key (CoverPictureId) | DB | Referenzielle Integrität | `Playlist.CoverPictureId` muss auf existierende `Pictures.Id` zeigen (oder NULL sein) |
| Cover-Upload validiert | Service | Business Logic | Größe, erlaubter MIME-Type und Dekodierbarkeit via ImageSharp — siehe `PlaylistCoverValidator` bzw. BR-22 in `playlists-business-rules.md` |

---

## Schemaentwicklung (zukünftig)

Mögliche zukünftige Änderungen für späteren Ausbau:

1. **Automatische Sortierung (`ByReleaseDate`) — bereits umgesetzt, ohne Schemaänderung:**
   Die Sortierung nach Erscheinungsdatum mit Fallback auf Hierarchie und `AddedAt` wird zur
   Laufzeit berechnet (siehe `playlists-business-rules.md`, BR-13) und benötigt keine zusätzlichen
   Spalten an `PlaylistEntry`.

2. **Manuelle Sortierung — bereits umgesetzt:**
   - `SortOrder` (long, nullable) existiert auf `PlaylistEntry` und trägt die manuelle
     Sortierreihenfolge, wenn `SortMode = Manual` ist (siehe Spaltenliste oben).

3. **Erweiterte Metadaten (zukünftig):**
   - `Notes` (string, nullable) — Benutzer-Notizen pro Eintrag
   - `Watched` (bool) — Benutzer hat Eintrag bereits gesehen
   - `Rating` (decimal, nullable) — Benutzerbewertung

4. **Zugriffs-/Lizenzprüfung — bereits umgesetzt, ohne Schemaänderung:**
   `DtoPlaylistEntry.IsAccessible` wird zur Laufzeit über `IUnlockedMediaService` ermittelt
   (siehe `playlists-api.md`) und benötigt keine zusätzlichen Spalten an `PlaylistEntry` selbst:
   Die Freischaltungsdaten für Serien und Filmsammlungen liegen bereits in der bestehenden
   `UnlockedMediaEntry`-Tabelle, die pro Anfrage befragt wird (siehe `einzelfreischaltungen.md`).

Änderungen 2–3 würden Migrationen erfordern, würden aber keine Breaking Changes darstellen.
