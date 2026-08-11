# Betroffene Dateien

## Startseite / UI

| Datei | Rolle |
|-------|-------|
| `VideoWebPlayer/Components/Pages/Home/Home.razor` | Startseite mit dem `.home-hero`-Bereich, wird um den `background-image` der Hero-Section erweitert. |
| `VideoWebPlayer/Components/Shared/Home/ContinueWatchingList.razor` | Zeigt „Weiterschauen" an; liefert das Vorbild für Bild-URLs (`/api/pictures/{id}`) und Datenzugriff. |
| `VideoWebPlayer/wwwroot/app.css` | Definitionen für `.home-hero`; erweiterbar um `background-image` bzw. Overlay. |

## API / Controller

| Datei | Rolle |
|-------|-------|
| `VideoWebPlayer/Controllers/PicturesController.cs` | Liefert einzelne Bilder; ist Basis für einen neuen Endpoint `/api/pictures/hero-background` (bzw. separater Controller). |
| `VideoWebPlayer/Controllers/ContinueWatchingController.cs` | REST-Endpoint `GET api/continue-watching`; nutzt `ContinueWatchingService.GetListAsync(User)`. |
| `VideoWebPlayer/Controllers/ApiBaseController.cs` | Stellt `CheckLogedIn()`, `CurrentUser` und Placeholder-Logik bereit. |

## Services / Daten

| Datei | Rolle |
|-------|-------|
| `VideoWebPlayer/Services/ContinueWatchingService.cs` | Liefert `List<ContinueWatchingDto>` für den aktuellen Benutzer. `GetListAsync(ClaimsPrincipal, ...)` ist der Einstieg. |
| `VideoWebPlayer/Data/Picture.cs` | Entität für Bilddaten (`Data`, `ContentType`, `Width`, `Height`). |
| `VideoWebPlayer/Data/ApplicationDbContext.cs` | EF Core DbContext mit `Pictures`-DbSet. |
| `VideoWebPlayer/Services/EpisodeBackgroundImage/EpisodeBackgroundImageGenerator.cs` | Bestehende ImageSharp-Generatorklasse als Muster für Laden, Skalieren, Kodieren. |
| `VideoWebPlayer/Extensions/ServiceCollectionExtensions.cs` | Registriert Services; neue Generator-Klasse muss hier eingebunden werden. |

## Abhängigkeiten

- `SixLabors.ImageSharp` (bereits in `VideoWebPlayer.csproj` referenziert).
- `Microsoft.EntityFrameworkCore` ( bereits vorhanden).
