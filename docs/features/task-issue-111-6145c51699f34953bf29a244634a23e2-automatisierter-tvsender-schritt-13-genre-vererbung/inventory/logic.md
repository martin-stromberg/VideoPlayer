# Logik

## `MediaSourceClassifier`
Datei: `VideoWebPlayer/Services/MediaSourceClassifier.cs`

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `GetOrCreateGenresAsync` | private (Z. 1382) | Zentraler Genre-Erzeugungspfad beim Scan. Lädt pro Name alle `Genre`-Zeilen der Quelle inkl. `AlternateNames` (Z. 1396–1400, `ToListAsync` **innerhalb** der Name-Schleife). Name nicht gefunden → `new Genre { Name, MediaSourceId }` (Z. 1405) — `IsHidden=false`, keine `AlternateNames`, **kein Konfigurationserbe**. Gefundene Zeile mit `GenreName`-Einträgen (`a.Name != genre.Name`, exakter Vergleich, Z. 1412) → rekursive Auflösung der Alternativnamen in derselben Quelle (Z. 1418), Zeile selbst wird **nicht** dem Ergebnis hinzugefügt. **Kein Zyklenschutz.** |
| `ReloadGenres` | public (Z. 1334) | Baut `MovieGenres`/`TVShowGenres` und `GenreNames` für alle `!IsManuallyEdited`-Movies/TVShows über `GetOrCreateGenresAsync` neu auf. |
| `CheckReloadGenres` | internal (Z. 1734) | Konsumiert `Setup.GenresChanged` → `ReloadGenres`; bei Fehler Flag zurücksetzen. Aufgerufen aus `MediaSourceScanService` (Z. 132) und `MediaSourceAdmin.razor` (Z. 251). |
| Aufrufer von `GetOrCreateGenresAsync` | — | `CreateOrUpdateTVShow` (Z. 522), `ProcessCollectionAsMovieAsync` (Z. 844), `ReloadGenres` (Z. 1344, 1366). |

Konstruktor-Signatur (Z. 59): `ApplicationDbContext`, `IMediaSourceReader`,
`RecentEntryService`, `EventManager`, `EpisodeBackgroundImageService`,
`ILogger<MediaSourceClassifier>`, `HttpClient`, optional
`MediaUpdateNotificationService?`, `IMediaMetadataWriteCoordinator?`.

## `MediaMetadataEditorService`
Datei: `VideoWebPlayer/Services/MediaMetadataEditorService.cs`

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `NormalizeGenresAsync` | private (Z. 196) | Zweiter Genre-Erzeugungspfad (Metadaten-Edit via `UpdateAsync` → `ApplyMovieGenresAsync`/`ApplyTVShowGenresAsync`). Fehlende Namen → `new Genre { Name, MediaSourceId }` (Z. 214) — ebenfalls ohne Konfigurationserbe. **Keine** Alternativnamen-Umlenkung — gibt die Zeilen direkt zurück. |
| `GetGenreOptionsAsync` | public (Z. 39) | Distinkte Namen pro `Name.Trim()` (ci-Gruppierung). |

## `GenreAdmin` (Razor-Seite)
Datei: `VideoWebPlayer/Components/Pages/Admin/GenreAdmin.razor`

| Methode | Kurzbeschreibung |
|---------|------------------|
| `ReloadGenres` (Z. 175) | Lädt alle `Genre` inkl. `AlternateNames`; gruppiert `Name.Trim().ToLower()`, Anzeige = `.First()` je Gruppe. |
| `EditGenre` (Z. 185) | Kopiert Zeile in `editGenre`-Editormodell. |
| `SaveGenre` (Z. 218) | Filtert `g.Name == editGenre.Name` **exakt** (Z. 224) — Case-Mismatch zur ci-Gruppierung. Wendet `StartDate`/`EndDate`/`IsHidden` + ersetzt `GenreName`-Alternativnamen auf **alle** gefundenen Zeilen an; ruft `MarkGenresAsChangedAsync`. |

## `GenreCanonicalResolver` (statisch, intern)
Datei: `VideoWebPlayer/Services/GenreCanonicalResolver.cs`

| Methode | Kurzbeschreibung |
|---------|------------------|
| `IsVisible(Genre, DateTime)` | `!IsHidden` und saisonales Fenster (nur bei beiden Daten; jährlich rollierend, Semantik wie `GenreService.GetSeasonalGenresAsync`). |
| `HasEffectiveAlternates(Genre)` | `GenreName`-Einträge ≠ Eigenname (ci, getrimmt) vorhanden. |
| `ResolveCanonicalNames(genres, now)` | Pro Zeile die kanonischen Namen: Alternativnamen → rekursiv quellbezogen (zyklensicher via `visiting`-Set); sichtbare Zeile ohne Alternativen → Eigenname; unsichtbare ohne Alternativen → nichts. |
| `BuildOptions(genres, now)` | Deduplizierte wählbare kanonische Optionen (TV-Sender-Auswahl). |

Verwendet von `ProgramRuleService` (Z. 93: Genre-Material-Optionen) und
`ProgramScheduleService` (Z. 1118/1124: Genre-Expansion).

## `GenreService`
Datei: `VideoWebPlayer/Services/GenreService.cs`

| Methode | Kurzbeschreibung |
|---------|------------------|
| `MarkGenresAsChangedAsync` | Setzt `Setup.GenresChanged = true` (Triggert `CheckReloadGenres` im nächsten Scan-Zyklus). |
| `GetSeasonalGenresAsync` | Liefert Zeilen innerhalb des saisonalen Fensters. |

## `SourceGenresController`
Datei: `VideoWebPlayer/Controllers/SourceGenresController.cs`

| Methode | Kurzbeschreibung |
|---------|------------------|
| `GetGenresForSource` | `!IsHidden`-Filter (Z. 155) — falsch sichtbare Zeilen erscheinen hier. |
| `GetGenresPerSource` | Ohne `IsHidden`-Filter (Z. 83). |

## `DataUpgradeManager`
Datei: `VideoWebPlayer/Services/DataUpgradeManager.cs` (Encoding wie Genre.cs)

`CurrentVersion = 8`; `EnsureUpToDateAsync` führt fehlende `Upgrade_n`-Methoden aus;
Upgrades 5 und 6 rufen bereits `_classifier.ReloadGenres` auf — etabliertes Muster für
einmalige, datenbankweite Genre-Neuzuordnung nach App-Start.

## `VideoWebPlayerBackupData`
`Services/Backups/VideoWebPlayerBackupData.cs`: Restore kopiert Zeilen 1:1 — kein
Genre-Erzeugungspfad; keine Änderung nötig, solange das Schema unverändert bleibt.
