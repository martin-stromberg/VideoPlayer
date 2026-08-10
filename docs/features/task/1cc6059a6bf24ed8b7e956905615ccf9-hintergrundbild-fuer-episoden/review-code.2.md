# Code-Review

## Ergebnis

**Status:** Befunde vorhanden

## Hinweis zur zweiten Iteration

Alle 6 in `review-code.1.md` gemeldeten Befunde wurden verifiziert und sind behoben:

1. Platzhalterbild-Duplikation (`EpisodesController`/`PicturesController`) → in `ApiBaseController.GetPlaceholderBytesAsync(...)` zusammengeführt, beide Controller nutzen die gemeinsame Methode.
2. Fehlende Tests für `EpisodesController.GetBackgroundImage` → `VideoWebPlayer.Tests/EpisodesControllerTests_GetBackgroundImage.cs` deckt 401/404/generiertes Bild/Fallback/Placeholder ab.
3. Optionsvalidierung im Konstruktor (griff nie gegen die tatsächlich gebundenen Werte) → durch `EpisodeBackgroundImageOptionsValidator : IValidateOptions<...>` ersetzt, registriert über `services.AddOptions<...>().Bind(...).ValidateOnStart()`.
4. Verwaiste `Picture`-Datensätze bei Regenerierung → `EnsureBackgroundImageAsync` entfernt das alte generierte `Picture` jetzt vor dem Speichern des neuen (verifiziert durch `Test_EnsureBackgroundImage_Regenerates_WhenFlagSet`).
5. Unnötiges erneutes Decodieren des JPEGs nur zur Breiten-/Höhenermittlung → `Picture.Width`/`Height` werden jetzt direkt aus `_options.MaxWidth`/`MaxHeight` gesetzt.
6. Redundante Diskriminierung über `Picture.Type == "generated-background"` parallel zu `IsGeneratedBackground` → `Type` wird für generierte Bilder nur noch als beschreibendes Label ("background") gesetzt; sämtliche Logik (Controller, Service, `VideoWebPlayerBackupDataProvider.BuildTableFilter`) wertet ausschließlich `IsGeneratedBackground` aus.

Zusätzlich wurde die Namensinkonsistenz aus Befund 5 der ersten Runde teilweise behoben (`GeneratedBackgroundImageId` → `GeneratedBackgroundPictureId`, `EpisodeIdReference` → `EpisodeId`, jeweils über eine eigene Migration `RenameEpisodeBackgroundImageColumns` nachgezogen). Ein Rest dieser Inkonsistenz bleibt bei der Navigationsproperty bestehen (siehe Befund unten).

Bei der erneuten Prüfung wurde ein neuer, funktional relevanter Befund gefunden (fehlender `access_token`-Query-Parameter, siehe unten), der die generierten Hintergrundbilder im Browser unbrauchbar macht.

## Befunde

### VideoWebPlayer/Components/Pages/TV/TVShowDetails.razor (TVShowDetails)

- **Fehlerbehandlung / Funktionsfehler** — `GetHeaderBackgroundUrl()` (neu hinzugefügt) baut für generierte Hintergrundbilder die URL `$"/api/episodes/{selectedEpisode!.Id}/background-image"` **ohne** den Query-Parameter `access_token`. Alle anderen Bild-/Stream-URLs in derselben Datei (`GetBannerUrl`, `GetPosterUrl`, `GetStreamUrl`, `GetDownloadUrl`) hängen konsequent `?access_token={Client.AuthorizationToken}` an. Der `EpisodesController` ist wie `PicturesController` mit `[BearerTokenCheck]` versehen; `BearerTokenCheckAttribute.OnActionExecuting` (`VideoWebPlayer/Controllers/Attributes/BearerTokenCheckAttribute.cs`, Zeilen 28–39) akzeptiert ausschließlich einen `Authorization`-Header oder den Query-Parameter `access_token` — es gibt **keinen** Fallback auf Cookie-/Identity-Authentifizierung. Ein vom Browser über `background-image: url(...)` ausgelöster Request ohne diesen Parameter erhält daher immer `401 Unauthorized`, wodurch das Feature (generiertes Hintergrundbild anzeigen) in der Praxis nie sichtbar wird und stattdessen implizit auf den Fallback-Pfad des `<img>`-Fehlerverhaltens (leeres/gebrochenes Hintergrundbild, kein automatischer Fallback auf Banner) hinausläuft. Kein bestehender Test deckt diesen Pfad ab (die Controller-Tests rufen die Action direkt auf, nicht über die von der Razor-Komponente erzeugte URL).

  Empfehlung: In `GetHeaderBackgroundUrl()` denselben Query-Parameter anhängen wie in `GetBannerUrl`/`GetPosterUrl`, z. B. `$"/api/episodes/{selectedEpisode!.Id}/background-image?access_token={Client.AuthorizationToken}"`.

### VideoWebPlayer/Data/TVShowEpisode.cs, VideoWebPlayer/Data/Configurations/TVShowEpisodeConfiguration.cs

- **Namenskonventionen und Einheitlichkeit** — Die Navigationsproperty `GeneratedBackgroundImage` (`TVShowEpisode.cs`, Zeile 49) bricht das im Projekt für alle Bild-Navigationsproperties durchgängig verwendete Muster `{Bildart}Picture` (vgl. `PosterPicture`, `BannerPicture`, `FanartPicture` in `MediaBaseEntry.cs`, Zeilen 68–76), obwohl die zugehörige Fremdschlüssel-Property in Runde 1 bereits konsistent zu `GeneratedBackgroundPictureId` umbenannt wurde. Die Inkonsistenz wurde also nur beim Fremdschlüssel behoben, nicht bei der Navigationsproperty.

  Empfehlung: `GeneratedBackgroundImage` in `GeneratedBackgroundPicture` umbenennen (inkl. Anpassung in `TVShowEpisodeConfiguration.cs`, Zeile 20, und in `ApplicationDbContextModelSnapshot.cs`).

### VideoWebPlayer/Services/EpisodeBackgroundImage/EpisodeBackgroundImageService.cs (EpisodeBackgroundImageService)

- **Doppelter Code** — In `EnsureBackgroundImageAsync` sind zwei nahezu identische Blöcke enthalten, die je ein bestehendes generiertes `Picture` nachladen und cachen: der optimistische Pfad außerhalb des Locks (Zeilen 61–72) und der erneute Check nach Erwerb des Locks (Zeilen 81–90). Beide führen dieselbe Abfolge aus (`_db.Pictures.AsNoTracking().FirstOrDefaultAsync(...)`, Null-Prüfung, `CachePictureId(...)`, `return`), unterscheiden sich nur darin, dass der äußere Pfad zusätzlich den Cache für die Picture-ID konsultiert.

  Empfehlung: Beide Blöcke in eine private Hilfsmethode extrahieren, z. B. `Task<Picture?> TryLoadExistingPictureAsync(long pictureId, CancellationToken ct)`, die von beiden Stellen aufgerufen wird; die Cache-Lookup-Logik bleibt nur im äußeren Aufruf.

- **God-Methode** — `EnsureBackgroundImageAsync` (Zeilen 56–137, ca. 80 Zeilen) übernimmt in einer einzigen Methode mehrere klar trennbare Aufgaben: schneller Cache-/DB-Check ohne Lock, Sperren pro Episode, erneuter DB-Check nach Lock-Erwerb, Laden des Fanarts, Bildgenerierung inkl. Fehlerbehandlung, Entfernen des verwaisten alten Bildes, Persistieren des neuen Bildes und Aktualisieren des Episode-Zustands, sowie Cache-Pflege.

  Empfehlung: Die Methode in kleinere, benannte Schritte aufteilen (z. B. `TryLoadExistingPictureAsync`, `GenerateAndPersistPictureAsync`, `RemoveObsoleteGeneratedPictureAsync`), sodass `EnsureBackgroundImageAsync` im Wesentlichen nur noch den Ablauf orchestriert.

### VideoWebPlayer.Tests/EpisodesControllerTests_GetBackgroundImage.cs

- **Namenskonventionen und Einheitlichkeit** — Der Dateiname (und identisch der Klassenname) `EpisodesControllerTests_GetBackgroundImage` verwendet einen Unterstrich als Trenner und weicht damit vom im Projekt für Controller-Tests etablierten Muster `{Controller}Controller{Aspekt}Tests` in reinem PascalCase ohne Unterstrich ab (vgl. `BackupsControllerAuthorizationTests.cs`, `UpdatesControllerAuthorizationTests.cs`).

  Empfehlung: Datei und Klasse in `EpisodesControllerBackgroundImageTests.cs` (bzw. `EpisodesControllerTests`, falls weitere Endpoints ergänzt werden sollen) umbenennen, ohne Unterstrich.

## Geprüfte Dateien

Neu (untracked):
- `VideoWebPlayer/Controllers/EpisodesController.cs`
- `VideoWebPlayer/Data/Configurations/PictureConfiguration.cs`
- `VideoWebPlayer/Services/EpisodeBackgroundImage/EpisodeBackgroundImageGenerator.cs`
- `VideoWebPlayer/Services/EpisodeBackgroundImage/EpisodeBackgroundImageOptions.cs`
- `VideoWebPlayer/Services/EpisodeBackgroundImage/EpisodeBackgroundImageOptionsValidator.cs`
- `VideoWebPlayer/Services/EpisodeBackgroundImage/EpisodeBackgroundImageService.cs`
- `VideoWebPlayer.Tests/EpisodesControllerTests_GetBackgroundImage.cs`
- `VideoWebPlayer.Tests/Services/EpisodeBackgroundImage/EpisodeBackgroundImageGeneratorTests.cs`
- `VideoWebPlayer.Tests/Services/EpisodeBackgroundImage/EpisodeBackgroundImageServiceTests.cs`
- `VideoWebPlayer/Migrations/20260810144442_AddEpisodeBackgroundImageProperties.cs` (+ `.Designer.cs`)
- `VideoWebPlayer/Migrations/20260810144513_AddPictureGeneratedBackgroundProperties.cs` (+ `.Designer.cs`)
- `VideoWebPlayer/Migrations/20260810152703_RenameEpisodeBackgroundImageColumns.cs` (+ `.Designer.cs`)

Geändert (modified, working tree):
- `VideoWebPlayer.Client/Models/DtoMovie.cs`
- `VideoWebPlayer.Tests/MediaSourceScanServiceTests.cs`
- `VideoWebPlayer.Tests/VideoWebPlayerBackupDataProviderTests.cs`
- `VideoWebPlayer/Components/Pages/TV/TVShowDetails.razor`
- `VideoWebPlayer/Controllers/ApiBaseController.cs`
- `VideoWebPlayer/Controllers/PicturesController.cs`
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
- `VideoWebPlayer/Controllers/Attributes/BearerTokenCheckAttribute.cs`
- `VideoWebPlayer/Data/MediaBaseEntry.cs`
