# Admin- und Berechtigungslogik

## Aktuelles Modell

`VideoWebPlayer/Data/ApplicationUser.cs` erweitert Identity-User um `IsAdmin`. `VideoWebPlayer/Data/ApplicationUserClaimsPrincipalFactory.cs` fuegt bei Admin-Benutzern den Claim `IsAdmin=True` hinzu.

`VideoWebPlayer/Extensions/ServiceCollectionExtensions.cs` registriert:

```csharp
services.AddAuthorization(options =>
{
    options.AddPolicy("AdminOnly", policy => policy.RequireClaim("IsAdmin", "True"));
});
```

## Vorhandene Adminseiten

Adminseiten liegen unter `VideoWebPlayer/Components/Pages/Admin`. Sie nutzen typischerweise:

- `@inject AuthenticationStateProvider AuthStateProvider`
- `var auth = await AuthStateProvider.GetAuthenticationStateAsync()`
- `isAdmin = user.HasClaim("IsAdmin", "True")`
- UI-Fallback `<div class="alert alert-danger">Nicht autorisiert.</div>`

Beispiele:

- `ProgramSettings.razor`
- `Backups.razor`
- `Security.razor`
- `MediaSources/*`

## Navigation

`VideoWebPlayer/Components/Layout/NavMenu.razor` zeigt Admin-Menueintraege nur, wenn `context.User.HasClaim("IsAdmin", "True")` gilt. Dort sind bereits `Programmeinstellungen`, `Quellen`, `Genres`, `Backups` und `Sicherheit` verlinkt.

## Serverseitige Absicherung

`VideoWebPlayer/Controllers/BackupsController.cs` ist mit `[Authorize(Policy = "AdminOnly")]` auf Controller-Ebene geschuetzt. Das ist der staerkere Mechanismus fuer sicherheitsrelevante Aktionen und sollte fuer Update-Aktionsendpunkte uebernommen werden.

## Relevanz fuer Update-UI

Die neue Update-Seite sollte:

- nur im Admin-Menue sichtbar sein,
- in der Razor-Komponente den `IsAdmin`-Claim pruefen,
- alle serverseitigen Update-Aktionsendpunkte mit `[Authorize(Policy = "AdminOnly")]` schuetzen,
- fuer POST-Aktionen Antiforgery nutzen, sofern sie als Formular-Posts umgesetzt werden.

## Hinweis zu alten Projekten

Im Repository gibt es daneben ein aelteres `WebPlayer`-Projekt mit rollenbasierter Admin-Logik (`Roles="admin"`). Fuer die aktuelle Anforderung ist nach Code- und Projektstruktur `VideoWebPlayer` relevant; dessen Admin-Mechanismus ist Claim-basiert.
