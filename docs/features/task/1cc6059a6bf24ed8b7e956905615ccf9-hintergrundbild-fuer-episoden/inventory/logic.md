# Bestandsaufnahme: Logik-Klassen und Services

## `MediaSourceClassifier`
Datei: `VideoWebPlayer/Services/MediaSourceClassifier.cs`

Verantwortlich für Klassifizierung und Verarbeitung gescannter MediaItems und MediaCollections.

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `ClassifyAllAsync(CancellationToken)` | `public` | Klassifiziert alle MediaItems und MediaCollections |
| `ClassifyMediaItemsAsync(CancellationToken)` | `public` | Klassifiziert nur MediaItems |
| `ClassifyMediaCollectionsAsync(CancellationToken)` | `public` | Klassifiziert nur MediaCollections |
| `ClassifyCollectionTreeAsync(long, CancellationToken)` | `public` | Klassifiziert Collection inkl. Unter-Collections |
| `ReloadGenres(CancellationToken)` | `public` | Aktualisiert Genre-Mappings für Filme und TV-Shows |
| `CheckReloadGenres(CancellationToken)` | `internal` | Prüft, ob Genres neu geladen werden müssen |
| `ProcessEpisodesForTVShowAsync(TVShow, CancellationToken)` | `private` | Verarbeitet Episoden für eine TV-Show |
| `AssignPicturesToTVShowEpisodeAsync(TVShowEpisode, MediaCollection, string, CancellationToken)` | `private` | Weist Bilder einer Episode zu |
| `AssignPicturesToTVShowSeasonAsync(TVShow, TVShowSeason, MediaCollection, CancellationToken, bool)` | `private` | Weist Bilder einer Staffel zu |
| `AssignPicturesToTVShowAsync(TVShow, MediaCollection, CancellationToken, bool)` | `private` | Weist Bilder einer TV-Show zu |
| `ProcessCollectionAsTVShowAsync(MediaCollection, CancellationToken)` | `private` | Verarbeitet Collection als TV-Show |
| `ProcessCollectionAsMovieAsync(MediaCollection, CancellationToken)` | `private` | Verarbeitet Collection als Film |

**Abonnierte Events:** Keine direkt erkennbar

**Publizierte Events:** 
- Über `EventManager.Publish<BackgroundProcessingStatusEvent>()` zur Benachrichtigung über Klassifizierungsstatus

**Besonderheiten:**
- Nutzt Thread-Safety durch statische Locks für Klassifizierungs-Queue
- Liest NFO-Dateien aus SFTP-Quellen
- Erstellt TVShow, TVShowSeason, TVShowEpisode, Movie, MovieCollection Einträge
- Weist Bilder basierend auf Dateinamen-Konventionen zu (z.B. `episode-fanart.jpg`)
- Integration mit `EventManager` zur Statusmitteilung

---

## `MediaSourceScanner`
Datei: `VideoWebPlayer/Services/MediaSourceScanner.cs`

Scannt Medienquellen nach neuen oder aktualisierten Elementen.

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `ScanAllSourcesAsync(CancellationToken)` | `public` | Scannt alle konfigurierten Medienquellen |
| `ScanNextMediaCollection(CancellationToken)` | `public` | Scannt nächste Collections mit ScanDueAt zeitplan |
| `ScanMediaCollectionAsync(long, CancellationToken)` | `public` | Scannt spezifische Collection |
| `ScanCollectionTreeAsync(long, CancellationToken)` | `public` | Scannt Collection inkl. Unter-Collections |

**Abhängigkeiten:**
- `ApplicationDbContext` für Datenbankzugriffe
- `SftpMediaSourceReader` für Fernzugriff auf Dateisystem
- `ProgramSettingsService` für Scan-Intervalle

**Besonderheiten:**
- Nutzt TimeProvider für konsistente UTC-Zeit
- Markiert MediaItems als `Changed = true`, wenn sich CreatedAt ändert
- Setzt `Classifyable`-Flag basierend auf Kind von Unter-Collections

---

## `EventManager`
Datei: `VideoWebPlayer/Services/EventManager.cs`

Zentral verwalteter Event-Bus mit Publish/Subscribe-Muster.

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `Subscribe<TEvent>(Action<TEvent>)` | `public` | Registriert Event-Handler |
| `SubscribeDisposable<TEvent>(Action<TEvent>)` | `public` | Registriert Handler mit Dispose-Unterstützung |
| `Unsubscribe<TEvent>(Action<TEvent>)` | `public` | Entfernt Event-Handler |
| `Publish<TEvent>(TEvent)` | `public` | Publiziert Event an alle registrierten Handler |

**Thread-Safety:** Ja, nutzt `lock` für Dictionary-Zugriffe

**Event-Typen in Verwendung:**
- `BackgroundProcessingStatusEvent` (von MediaSourceClassifier publiziert)

---

## `VideoWebPlayerBackupDataProvider`
Datei: `VideoWebPlayer/Services/Backups/VideoWebPlayerBackupDataProvider.cs`

Verwaltet Export und Restore der VideoWebPlayer-Datenbank für Backups.

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `ExportAsync(Stream, BackupExportContext, CancellationToken)` | `public` | Exportiert Datenbankinhalt in Backup-Format |
| `ValidateAsync(Stream, BackupValidationContext, CancellationToken)` | `public` | Validiert Backup-Struktur und Schema |
| `RestoreAsync(Stream, BackupRestoreContext, CancellationToken)` | `public` | Stellt Datenbank aus Backup wieder her |
| `ProviderId` (Property) | `public` | Eindeutige ID: "VideoWebPlayer.ApplicationDbContext" |

**Besonderheiten:**
- Exportiert alle Tabellen im JSON-Format (ZIP-Archive)
- Exportiert auch Genre-Icon-Dateien aus `wwwroot/images/genres/`
- Nutzt Transaktionen für sichere Restores
- Behandelt SQLite Foreign-Key-Constraints speziell
- **Wichtig für Feature:** Generierte Hintergrundbilder sollten wahrscheinlich aus Backups ausgeschlossen werden (aktuell nicht implementiert)

**Optionale Restore-Tabellen:** `UpdateSettings`

**Optionale Restore-Spalten:** `Setups.ApplicationTitle`

---

## Noch nicht vorhanden (gemäß Anforderung)

Die folgenden Services müssen neu implementiert werden:

### `EpisodeBackgroundImageGenerator`
Service zur technischen Bildverarbeitung (Skalierung, Farbextraktion, Canvas-Erstellung).

**Geplante Methoden:**
- `GenerateBackgroundImageAsync(TVShowEpisode, CancellationToken) : Task<Picture>`
- `ResizeImage(byte[], int, int) : byte[]`
- `GetDominantColor(byte[]) : System.Drawing.Color`
- `CreateCanvasWithScaledImage(byte[], int, int, System.Drawing.Color) : byte[]`
- `ApplyTintOverlay(byte[], System.Drawing.Color, float) : byte[]`

**Konfigurierbare Properties:**
- `MaxWidth`, `MaxHeight` für Canvas-Maßstäbe

### `EpisodeBackgroundImageService`
Business-Logic für Lazy-Loading, Persistierung, Caching, Thread-Safety.

**Geplante Methoden:**
- `EnsureBackgroundImageAsync(TVShowEpisode, CancellationToken) : Task<Picture?>`
- `MarkBackgroundImageForUpdateAsync(long episodeId, CancellationToken) : Task`

**Geplante Features:**
- In-Memory Cache (`IMemoryCache`) zur Vermeidung redundanter Zugriffe
- Thread-Safe durch Locks/AsyncLock für parallele Requests
- Lazy-Loading: Generierung nur bei Bedarf
- Persistierung in Datenbank

### `EpisodeBackgroundImageMapper` (optional)
DTO-Mapper für Blazor-Integration.

**Geplante Entities:**
- `EpisodeBackgroundImageDto` mit URL zum generierten Bild
