# Umsetzungsplan: Zusammengesetztes Hintergrundbild für den Kopfbereich der Startseite

## Zusammenfassung

Auf dem Server wird aus den ersten maximal fünf Bildern der „Weiterschauen"-Liste ein zusammengesetztes JPEG-Hintergrundbild erzeugt. Das Bild besteht aus nebeneinandergelegten vertikalen Mittelstreifen, deren Breite an die Anzahl der verfügbaren Bilder angepasst ist. Zwischen den Streifen wird ein weicher Übergang eingebaut. Über die API kann die Startseite das Bild als `background-image` des Hero-Bereichs laden.

## Zielgrößen

- Zielbreite: 1600 px
- Zielhöhe: 520 px (entspricht dem maximalen Hero-Layout in `app.css`)
- Max. Streifen: 5
- Übergangsbreite: 32 px (weicher Fade zwischen Streifen)
- Ausgabeformat: JPEG, Qualität 85

## Technische Schritte

### 1. Generator-Service

- Neue Datei `VideoWebPlayer/Services/HomeBackgroundImage/HomeBackgroundImageGenerator.cs`.
- Ermittelt die ersten 5 `PosterPictureId` aus `ContinueWatchingService.GetListAsync(User)`.
- Lädt die Bilddaten aus `DbContext.Pictures`.
- Verarbeitet mit `SixLabors.ImageSharp`:
  - Streifenbreite = `targetWidth / Anzahl` (letzter Streifen erhält eventuellen Rest).
  - Jedes Quellbild wird zentriert auf `Streifenbreite x Zielhöhe` zugeschnitten (Resize/Crop mit `AnchorPositionMode.Center`).
  - Ein neues Canvas der Zielgröße wird aufgebaut.
  - Pixelweise Mischung der Streifen in 32-px-Übergangsbändern: Gewichtung geht in den Übergangsbereichen linear von 100 % aktueller Streifen zu 0 % und umgekehrt beim Nachbarstreifen.
- Rückgabe als `byte[]?`; bei fehlenden Bildern oder Fehler `null`.

### 2. API-Endpoint

- In `PicturesController` neuer Action `[HttpGet("hero-background")]`.
- Prüft `CheckLogedIn()`.
- Ruft `HomeBackgroundImageGenerator.GenerateAsync(User, ...)` auf.
- Liefert das generierte Bild als `image/jpeg` oder den bestehenden Platzhalter/NotFound.

### 3. Dependency Injection

- `HomeBackgroundImageGenerator` in `ServiceCollectionExtensions.cs` als `services.AddScoped<HomeBackgroundImageGenerator>();` registrieren.

### 4. Startseite

- `Home.razor` erweitern:
  - Lädt `applicationTitle` und setzt zusätzlich `heroBackgroundUrl` auf `/api/pictures/hero-background?access_token={Client.AuthorizationToken}` (nur wenn authentifiziert).
- `app.css`:
  - `.home-hero` erhält `background-size: cover` und `background-position: center` für das generierte Bild (letzte Schicht hinter den bestehenden Verläufen).

## Tests

- `dotnet build` für das Webprojekt ausführen.
- Falls schnell machbar: Unit-Test mit simulierten Bilddaten in `VideoWebPlayer.Tests`.

## Offene Punkte

*Keine.*
