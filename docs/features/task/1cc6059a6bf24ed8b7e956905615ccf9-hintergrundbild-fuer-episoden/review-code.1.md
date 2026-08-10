# Code-Review

## Ergebnis

**Status:** Befunde vorhanden

## Befunde

### VideoWebPlayer/Controllers/EpisodesController.cs (EpisodesController)

- **Doppelter Code** — Die Platzhalterbild-Fallback-Logik in `GetBackgroundImage` (Pfadaufbau `wwwroot/images/placeholder.png`, `IMemoryCache.GetOrCreateAsync` mit 10-Minuten-Absolute-Expiration, Rückgabe als `image/png`) ist nahezu identisch aus `PicturesController.GetPicture` (`VideoWebPlayer/Controllers/PicturesController.cs`, Zeilen 47–57) kopiert, lediglich der Cache-Key-String unterscheidet sich (`"EpisodesController.Placeholder"` vs. `"PicturesController.Placeholder"`).

  Empfehlung: Placeholder-Logik in eine gemeinsame Stelle auslagern (z. B. Methode `GetPlaceholderBytesAsync()` in `ApiBaseController` oder ein eigener `IPlaceholderImageProvider`-Service), die von beiden Controllern verwendet wird.

- **Testqualität** — Für den neuen öffentlichen Endpoint `GetBackgroundImage` existiert keine Testklasse. Für andere neu eingeführte Controller im Projekt existieren durchgängig dedizierte Test-/Autorisierungstests (z. B. `VideoWebPlayer.Tests/BackupsControllerAuthorizationTests.cs`, `VideoWebPlayer.Tests/UpdatesControllerAuthorizationTests.cs`). Für `EpisodesController` fehlt das Äquivalent vollständig.

  Empfehlung: `EpisodesControllerTests.cs` (bzw. `EpisodesControllerAuthorizationTests.cs`) ergänzen, das mindestens folgende Fälle abdeckt: 401 bei fehlender Anmeldung, 404 bei unbekannter Episode, Rückgabe des generierten Bilds wenn vorhanden, Fallback auf Banner/Fanart, Fallback auf Placeholder.

### VideoWebPlayer/Services/EpisodeBackgroundImage/EpisodeBackgroundImageOptions.cs (EpisodeBackgroundImageOptions)

- **Fehlerbehandlung** — `Validate()` wird im parameterlosen Konstruktor aufgerufen (Zeile 44). Wenn die Optionen über `services.Configure<EpisodeBackgroundImageOptions>(configuration.GetSection("EpisodeBackgroundImage"))` (siehe `VideoWebPlayer/Extensions/ServiceCollectionExtensions.cs`) gebunden werden, instanziiert die Options-Infrastruktur das Objekt zunächst über den parameterlosen Konstruktor (mit den Code-Default-Werten) und überträgt die Werte aus `appsettings.json` erst anschließend per Property-Setter. Die Validierung läuft damit ausschließlich gegen die hartkodierten Default-Werte, niemals gegen die tatsächlich konfigurierten Werte — ein ungültiger Wert in `appsettings.json` (z. B. `TintOpacity: 5.0`) würde die Validierung nicht auslösen. Kein anderes Options-Objekt im Projekt (z. B. `VideoWebPlayer/Services/Updates/UpdateBackupOptions.cs`) verwendet Konstruktor-Validierung.

  Empfehlung: Validierung nach dem Binding ausführen, z. B. über `services.AddOptions<EpisodeBackgroundImageOptions>().Bind(configuration.GetSection("EpisodeBackgroundImage")).ValidateOnStart()` mit einer `Validate(...)`-Regel, oder eine eigene `IValidateOptions<EpisodeBackgroundImageOptions>`-Implementierung registrieren. Die Validierung aus dem Konstruktor entfernen.

### VideoWebPlayer/Services/EpisodeBackgroundImage/EpisodeBackgroundImageService.cs (EpisodeBackgroundImageService)

- **Fehlerbehandlung** — In `EnsureBackgroundImageAsync` wird bei einer Regenerierung (`BackgroundImageRequiresUpdate == true`, Zeilen 100–127) ein neues `Picture` angelegt und die `GeneratedBackgroundImageId` der Episode darauf umgebogen. Das zuvor referenzierte, jetzt verwaiste generierte `Picture` (inklusive Bilddaten-Blob) wird nicht aus `_db.Pictures` entfernt. Bei jedem erneuten Fanart-Wechsel (ausgelöst über `MarkBackgroundImageForUpdateAsync` in `MediaSourceClassifier.cs`) akkumuliert dadurch ein weiterer verwaister Datensatz in der Datenbank.

  Empfehlung: Vor dem Setzen der neuen `GeneratedBackgroundImageId` das alte generierte `Picture` (`currentEpisode.GeneratedBackgroundImageId`, sofern vorhanden und `IsGeneratedBackground == true`) aus `_db.Pictures` entfernen bzw. in derselben Transaktion durch das neue ersetzen.

### VideoWebPlayer/Services/EpisodeBackgroundImage/EpisodeBackgroundImageGenerator.cs (EpisodeBackgroundImageGenerator)

- **Fehlende Kapselung** — In `GenerateBackgroundImageAsync` (Zeile 59) wird das bereits fertig kodierte JPEG erneut über `Image.Load(jpegBytes)` dekodiert, nur um `Width`/`Height` für das `Picture`-Objekt zu ermitteln. Diese Werte entsprechen aber bereits `_options.MaxWidth`/`_options.MaxHeight` (die Canvasgröße ändert sich durch `ApplyTintOverlay` nicht), sodass ein zusätzliches Decodieren unnötig ist.

  Empfehlung: `canvasWidth`/`canvasHeight` (bzw. `_options.MaxWidth`/`_options.MaxHeight`) direkt für `Width`/`Height` verwenden statt das JPEG erneut zu laden.

### VideoWebPlayer/Data/TVShowEpisode.cs, VideoWebPlayer/Data/Picture.cs

- **Namenskonventionen und Einheitlichkeit** — Die neue Fremdschlüssel-Property `GeneratedBackgroundImageId` (`TVShowEpisode.cs`) bricht das im Projekt durchgängig verwendete Namensmuster `{Bildart}PictureId` für Verweise auf `Picture` (vgl. `FanartPictureId`, `BannerPictureId`, `PosterPictureId` in derselben Klasse). Zusätzlich weicht `GeneratedBackgroundImageId` von den drei anderen neuen, zusammengehörigen Properties ab, die konsistent mit `BackgroundImage...` beginnen (`BackgroundImageRequiresUpdate`, `BackgroundImageGeneratedAt`). Ebenso bricht `EpisodeIdReference` (`Picture.cs`, Zeile 51) das im Projekt sonst ausnahmslos verwendete Muster `{Entität}Id` für Fremdschlüssel/Referenzen (vgl. `MediaItemId`, `TVShowSeasonId`, `MediaCollectionId`, `MediaSourceId`) und ist die einzige Property im gesamten Projekt mit dem Suffix `Reference`.

  Empfehlung: `GeneratedBackgroundImageId` in `GeneratedBackgroundPictureId` (passend zum `PictureId`-Muster) oder in `BackgroundImagePictureId` (passend zu den Schwester-Properties) umbenennen. `EpisodeIdReference` in `EpisodeId` umbenennen, um dem Namensmuster für Fremdschlüssel im Projekt zu entsprechen.

### VideoWebPlayer/Data/Picture.cs, VideoWebPlayer/Services/EpisodeBackgroundImage/EpisodeBackgroundImageGenerator.cs

- **Data Clumps / redundante Zustandskodierung** — Generierte Bilder werden über zwei unabhängige Felder als solche markiert: `Picture.Type == "generated-background"` (String, gesetzt in `EpisodeBackgroundImageGenerator.GenerateBackgroundImageAsync`, Zeile 62) und `Picture.IsGeneratedBackground` (bool, Zeile 63). Beide kodieren denselben Fakt. Da an mehreren Stellen (`EpisodesController.cs` Zeile 52, `VideoWebPlayerBackupDataProvider.cs` `BuildTableFilter`) ausschließlich `IsGeneratedBackground` ausgewertet wird, besteht das Risiko, dass `Type` und `IsGeneratedBackground` bei künftigen Änderungen auseinanderlaufen, ohne dass dies auffällt.

  Empfehlung: Entweder auf das bestehende `Type`-Feld (`Type == "generated-background"`) als alleinige Diskriminierung setzen und `IsGeneratedBackground` entfernen, oder umgekehrt `Type` für generierte Bilder nicht mehr separat pflegen und stattdessen konsequent nur `IsGeneratedBackground` verwenden.

## Geprüfte Dateien

Hinweis: `main` liegt im Vergleich zum Feature-Branch weit zurück (viele branchfremde, bereits über andere PRs gemergte Commits liegen dazwischen). Der einzige branch-eigene Commit (`602b763`) enthält ausschließlich Planungsdokumente. Die eigentliche Implementierung des Features liegt als unkommitierte Arbeitsverzeichnis-Änderungen vor; diese wurden geprüft:

Neu (untracked):
- `VideoWebPlayer/Controllers/EpisodesController.cs`
- `VideoWebPlayer/Data/Configurations/PictureConfiguration.cs`
- `VideoWebPlayer/Services/EpisodeBackgroundImage/EpisodeBackgroundImageGenerator.cs`
- `VideoWebPlayer/Services/EpisodeBackgroundImage/EpisodeBackgroundImageOptions.cs`
- `VideoWebPlayer/Services/EpisodeBackgroundImage/EpisodeBackgroundImageService.cs`
- `VideoWebPlayer.Tests/Services/EpisodeBackgroundImage/EpisodeBackgroundImageGeneratorTests.cs`
- `VideoWebPlayer.Tests/Services/EpisodeBackgroundImage/EpisodeBackgroundImageServiceTests.cs`
- `VideoWebPlayer/Migrations/20260810144442_AddEpisodeBackgroundImageProperties.cs` (+ `.Designer.cs`)
- `VideoWebPlayer/Migrations/20260810144513_AddPictureGeneratedBackgroundProperties.cs` (+ `.Designer.cs`)

Geändert (modified, working tree):
- `VideoWebPlayer.Client/Models/DtoMovie.cs`
- `VideoWebPlayer.Tests/MediaSourceScanServiceTests.cs`
- `VideoWebPlayer.Tests/VideoWebPlayerBackupDataProviderTests.cs`
- `VideoWebPlayer/Components/Pages/TV/TVShowDetails.razor`
- `VideoWebPlayer/Data/Configurations/TVShowEpisodeConfiguration.cs`
- `VideoWebPlayer/Data/Picture.cs`
- `VideoWebPlayer/Data/TVShowEpisode.cs`
- `VideoWebPlayer/Extensions/ServiceCollectionExtensions.cs`
- `VideoWebPlayer/Migrations/ApplicationDbContextModelSnapshot.cs`
- `VideoWebPlayer/Services/Backups/VideoWebPlayerBackupDataProvider.cs`
- `VideoWebPlayer/Services/MediaSourceClassifier.cs`
- `VideoWebPlayer/VideoWebPlayer.csproj`
- `VideoWebPlayer/appsettings.json`
- `VideoWebPlayer/wwwroot/app.css`

Zur Kontexteinordnung mitgeprüft (bestehender Code, unverändert):
- `VideoWebPlayer/Controllers/ApiBaseController.cs`
- `VideoWebPlayer/Controllers/PicturesController.cs`
- `VideoWebPlayer/Services/Updates/UpdateBackupOptions.cs`
