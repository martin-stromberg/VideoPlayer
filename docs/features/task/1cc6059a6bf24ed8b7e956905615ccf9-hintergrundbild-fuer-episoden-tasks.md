# Tasks: Dynamische Hintergrundbild-Generierung für Episoden

| # | Bereich | Aufgabe | Status | Testnachweis |
|---|---------|---------|--------|--------------|
| 1 | Datenmodell | `TVShowEpisode`: Properties `GeneratedBackgroundImageId`, `BackgroundImageRequiresUpdate`, `BackgroundImageGeneratedAt` und Navigation `GeneratedBackgroundImage` hinzufügen | Offen | — |
| 2 | Datenmodell | `Picture`: Properties `IsGeneratedBackground` und `EpisodeIdReference` hinzufügen | Offen | — |
| 3 | Datenmodell | `ApplicationDbContext.OnModelCreating()`: Entity-Mappings für neue Properties und Navigations erweitern | Offen | — |
| 4 | Datenmodell | EF Core Migration: `AddEpisodeBackgroundImageProperties` erstellen | Offen | — |
| 5 | Datenmodell | EF Core Migration: `AddPictureGeneratedBackgroundProperties` erstellen | Offen | — |
| 6 | Logik | NuGet-Pakete hinzufügen: `SixLabors.ImageSharp` (v3+) und `Nito.AsyncEx` (v5.1.2+) | Offen | — |
| 7 | Logik | `EpisodeBackgroundImageOptions` Konfigurationsklasse erstellen | Offen | — |
| 8 | Logik | `EpisodeBackgroundImageGenerator` Service implementieren (alle Bildverarbeitungsmethoden) | Offen | — |
| 9 | Logik | `EpisodeBackgroundImageService` Service implementieren (Lazy-Load, Persistierung, Caching, Thread-Safety) | Offen | — |
| 10 | Logik | `MediaSourceClassifier.AssignPicturesToTVShowEpisodeAsync()` erweitern: `MarkBackgroundImageForUpdateAsync()` Aufruf hinzufügen | Offen | — |
| 11 | Logik | `VideoWebPlayerBackupDataProvider.ExportAsync()` erweitern: Filter `!picture.IsGeneratedBackground` für Picture-Export | Offen | — |
| 12 | Konfiguration | `appsettings.json` Sektion `EpisodeBackgroundImage` mit Standardwerten hinzufügen | Offen | — |
| 13 | Konfiguration | `Program.cs`: Service-Registrierung für `EpisodeBackgroundImageGenerator`, `EpisodeBackgroundImageService`, `IMemoryCache` hinzufügen | Offen | — |
| 14 | Konfiguration | Database Migration ausführen: `dotnet ef database update` | Offen | — |
| 15 | API | Endpoint `GET /api/episodes/{episodeId}/background-image` implementieren (neuer Controller oder Erweiterung) | Offen | — |
| 16 | UI | `TVShowDetails.razor`: Service injizieren, `OnInitializedAsync` erweitern, Hinterbild-URL laden | Offen | — |
| 17 | UI | `TVShowDetails.razor`: Header-Background auf generiertes Bild ändern (mit Fallback auf Banner/Fanart) | Offen | — |
| 18 | UI | `TVShowDetails.razor`: CSS-Klasse oder inline Styling für Schleier-Effekt (opacity 0.4) anwenden | Offen | — |
| 19 | Tests | `EpisodeBackgroundImageGeneratorTests` erstellen: Test-Hilfsmethoden (`CreateTestImage()`, `CreateTestPicture()`) | Offen | — |
| 20 | Tests | `EpisodeBackgroundImageGeneratorTests`: Test `Test_ResizeImage_KeepAspectRatio` | Offen | — |
| 21 | Tests | `EpisodeBackgroundImageGeneratorTests`: Test `Test_GetDominantColor_ReturnsCorrectColor` | Offen | — |
| 22 | Tests | `EpisodeBackgroundImageGeneratorTests`: Test `Test_CreateCanvasWithScaledImage_PlacesImageCentered` | Offen | — |
| 23 | Tests | `EpisodeBackgroundImageGeneratorTests`: Test `Test_ApplyTintOverlay_OpacityApplied` | Offen | — |
| 24 | Tests | `EpisodeBackgroundImageGeneratorTests`: Test `Test_GenerateBackgroundImage_WithValidFanart_ReturnsImage` | Offen | — |
| 25 | Tests | `EpisodeBackgroundImageGeneratorTests`: Test `Test_GenerateBackgroundImage_WithMissingFanart_ReturnsNull` | Offen | — |
| 26 | Tests | `EpisodeBackgroundImageServiceTests` erstellen: Hilfsmethoden (`CreateMockDbContext()`, `CreateTestEpisode()`) | Offen | — |
| 27 | Tests | `EpisodeBackgroundImageServiceTests`: Test `Test_EnsureBackgroundImage_LazyLoads_OnFirstCall` | Offen | — |
| 28 | Tests | `EpisodeBackgroundImageServiceTests`: Test `Test_EnsureBackgroundImage_UsesCached_OnSubsequentCall` | Offen | — |
| 29 | Tests | `EpisodeBackgroundImageServiceTests`: Test `Test_EnsureBackgroundImage_ThreadSafe_ParallelRequests` | Offen | — |
| 30 | Tests | `EpisodeBackgroundImageServiceTests`: Test `Test_MarkBackgroundImageForUpdate_SetsFlag_OnNewFanart` | Offen | — |
| 31 | Tests | `EpisodeBackgroundImageServiceTests`: Test `Test_EnsureBackgroundImage_Regenerates_WhenFlagSet` | Offen | — |
| 32 | Tests | `EpisodeBackgroundImageServiceTests`: Test `Test_EnsureBackgroundImage_ReturnsFallback_OnGenerationError` | Offen | — |
| 33 | E2E-Tests | `TVShowDetailsPageTests` erweitern/erstellen: Test `Test_Episode_DisplaysGeneratedBackgroundImage` (Happy Path) | Offen | — |
| 34 | E2E-Tests | `TVShowDetailsPageTests`: Test `Test_Episode_FallsBackToPlaceholder_WhenGenerationFails` | Offen | — |
| 35 | E2E-Tests | `MediaScannerE2ETests` erweitern/erstellen: Test `Test_NewFanart_UpdatesBackgroundImageRequiresUpdateFlag` | Offen | — |
| 36 | E2E-Tests | `BackupE2ETests` erweitern/erstellen: Test `Test_Backup_ExcludesGeneratedBackgroundImages` | Offen | — |
| 37 | E2E-Tests | `BackupE2ETests`: Test `Test_Restore_RegeneratesBackgroundImages_OnNextEpisodeLoad` | Offen | — |
| 38 | Integration | Bestehende `MediaSourceClassifierTests` prüfen/anpassen (falls vorhanden) | Offen | — |
| 39 | Integration | Bestehende `VideoWebPlayerBackupDataProviderTests` prüfen/anpassen (falls vorhanden) | Offen | — |

**Hinweise:**
- Status wird durch `/review-plan` oder `/review` aktualisiert.
- Testnachweis wird gefüllt, wenn der Test erfolgreich läuft oder der Code-Review positiv ausfällt.
- Task-Reihenfolge folgt der Umsetzungsreihenfolge aus `plan.md`.
