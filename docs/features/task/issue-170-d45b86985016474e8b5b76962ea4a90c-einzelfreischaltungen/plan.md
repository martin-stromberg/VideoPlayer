# Umsetzungsplan – Einzelfreischaltungen

## Ziel

Administratoren sollen einzelne Serien (`TVShow`) oder Filmsammlungen (`MovieCollection`) für andere Anwender freischalten können. Freigeschaltete Elemente erscheinen in der „Neu im Programm“-Liste, machen ihre Quelle im Menü sichtbar und sind in der Quellen-Auflistung sichtbar. Ist die Quelle selbst nicht freigegeben, werden nur freigeschaltete Elemente dieser Quelle angezeigt.

## Architekturentscheidungen

1. **Freischaltmodell: globale Markierung pro Element (nicht benutzerspezifisch)**
   - Die Anforderung spricht von „für andere Anwender freischalten“ im Plural und ohne Auswahl einzelner Benutzer.
   - Es wird eine neue Tabelle `UnlockedMediaEntry` (analog `FavoriteEntry`) angelegt, die pro freigeschalteter Serie/Sammlung einen Datensatz speichert.
   - Dadurch bleibt die Domäne einfach und konsistent zu den Favoriten; Erweiterung auf benutzerspezifische Freigaben später möglich.

2. **Sichtbarkeit**
   - Ein Element ist für einen angemeldeten Benutzer sichtbar, wenn **eine** der folgenden Bedingungen gilt:
     - Die Quelle ist über `MediaSourceUser` für den Benutzer freigegeben.
     - Das Element ist in `UnlockedMediaEntry` eingetragen.
   - In der Quellen-Auflistung (`ItemsController.Get`) wird für Quellen, die dem Benutzer nicht freigegeben sind, nur deren freigeschaltete Elemente gelistet.

3. **Quelle im Menü**
   - `SourcesController.GetSources` liefert zusätzlich Quellen, die dem Benutzer nicht direkt freigegeben sind, aber mindestens ein freigeschaltetes Element enthalten.

## Geplante Datei-/Codeänderungen

### Datenbank / EF Core

1. **Neue Entität `UnlockedMediaEntry`**
   - Speicherort: `VideoWebPlayer/Data/UnlockedMediaEntry.cs`
   - Felder: `Id`, `MovieCollectionId`, `TVShowId`, `CreatedAt`.
   - Konfiguration in `VideoWebPlayer/Data/Configurations/UnlockedMediaEntryConfiguration.cs`.
2. **DbSet in `ApplicationDbContext`**
   - `DbSet<UnlockedMediaEntry> UnlockedMediaEntries`.
3. **EF-Migration**
   - `dotnet ef migrations add AddUnlockedMediaEntries`.
   - Migration-Dateien werden unter `VideoWebPlayer/Migrations/` erstellt.

### Server / API

4. **Neues Interface `IUnlockedMediaService` und Implementierung `UnlockedMediaService`**
   - Speicherort: `VideoWebPlayer/Services/IUnlockedMediaService.cs`, `VideoWebPlayer/Services/UnlockedMediaService.cs`
   - Methoden:
     - `Task<bool> IsUnlockedAsync(long? movieCollectionId, long? tvShowId, ...)`
     - `Task<bool> ToggleUnlockedAsync(DtoMediaEntry entry, CancellationToken)`
     - `Task<long[]> GetUnlockedMediaSourceIdsAsync(string userId, CancellationToken)` – Quellen, die indirekt durch mindestens ein freigeschaltetes Element sichtbar sind.
5. **Neuer Controller `UnlockedMediaController`**
   - Speicherort: `VideoWebPlayer/Controllers/UnlockedMediaController.cs`
   - `POST api/unlocked/toggle` – Admin-only, toggelt Freischaltung eines `DtoMediaEntry`.
   - `GET api/unlocked/{type}/{id}` – Liefert Freischaltstatus eines Elements.
6. **Erweiterung `ItemsController`**
   - `GET api/items` filtert TVShows und MovieCollections auf:
     - Quelle freigegeben **oder** Element freigeschaltet.
     - Falls Quelle **nicht** freigegeben, nur freigeschaltete Elemente dieser Quelle.
   - `GET api/items/{type}/{id}` setzt `IsUnlocked` auf den DTOs.
   - `FindMediaItemAsync` erlaubt Streamen, wenn Quelle freigegeben **oder** übergeordnete Serie/Sammlung/Episode freigeschaltet ist.
7. **Erweiterung `RecentEntryService`**
   - `GetRecentEntriesAsync` berücksichtigt freigeschaltete Elemente, deren Quelle nicht dem Benutzer gehört.
8. **Erweiterung `SourcesController`**
   - `GetSources` liefert Quellen mit direkter Freigabe oder mit mindestens einem freigeschalteten Element.
9. **Service-Registrierung**
   - `UnlockedMediaService` in `ServiceCollectionExtensions.cs` oder `Program.cs` registrieren.

### DTOs

10. **DTO-Erweiterungen**
    - `DtoMediaEntry` erhält `bool IsUnlocked` (optional, in `VideoWebPlayer.Client/Models/DtoMovie.cs`).
    - `DtoTVShow` und `DtoMovieCollection` zeigen `IsUnlocked` korrekt an.

### UI (Blazor)

11. **Neuer Unlock-Button**
    - Speicherort: `VideoWebPlayer/Components/Shared/Media/UnlockButton.razor`
    - Akzeptiert `DtoMediaEntry` und Event-Callback.
    - Nur für `DtoTVShow` und `DtoMovieCollection` sichtbar und nur für Administratoren.
    - Zeigt offenes/geschlossenes Schloss-Symbol (z. B. 🔓 / 🔒).
12. **Integration in Detailansichten**
    - `TVShowDetails.razor` – neben `.favorite-btn`.
    - `MovieCollectionDetails.razor` – neben `.favorite-btn`.
13. **Client-Methode in `VideoWebPlayerClient`**
    - `ToggleUnlockedAsync(DtoMediaEntry)` ruft `api/unlocked/toggle`.
    - `RequestUnlockedStatusAsync(type, id)` optional.
14. **Styling**
    - `app.css` – `.unlock-btn` analog `.favorite-btn` positionieren.

### Tests

15. **Unit-/Service-Test**
    - `VideoWebPlayer.Tests/Services/UnlockedMediaServiceTests.cs`
    - Prüft: Toggle, Sichtbarkeit, Abfrage.
16. **E2E-Test**
    - `VideoWebPlayer.Tests/UnlockMediaE2ETests.cs`
    - Admin ruft `MovieCollectionDetails` auf, klickt Unlock-Symbol, erwartet Quelle erscheint im Menü für normalen Benutzer.

## Abhängigkeiten

- Keine externen Pakete nötig.
- Playwright muss ggf. installiert sein (`pwsh bin/Debug/net8.0/playwright.ps1 install`), falls E2E-Tests laufen.

## Offene Punkte

Keine – die oben getroffenen Architekturentscheidungen basieren auf der Anforderung.
