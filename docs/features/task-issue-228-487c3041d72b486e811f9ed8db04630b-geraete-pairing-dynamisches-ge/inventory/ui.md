# Admin-UI / Razor-Komponenten — Bestandsaufnahme

Bezug: Anforderung „Geräte-Pairing mit dynamischem Geräte-Token" (`../requirement.md`).

## `Security.razor` (`/admin/security`)

Datei: `VideoWebPlayer/Components/Pages/Admin/Security.razor`

- `@rendermode InteractiveServer`; injiziert `ILoginIpBlockService`, `AuthenticationStateProvider`, `IJSRuntime`.
- Admin-Prüfung clientseitig über Claim `"IsAdmin" == "True"` in `OnInitializedAsync` (Zeilen 91–98); bei Nicht-Admin nur „Nicht autorisiert."-Hinweis.
- Zeigt Statistik-Karten (`admin-stat-card`) und Tabelle gesperrter IPs mit `Entsperren`-Aktion (`Unblock`, Zeilen 112–119).
- Schreibt bei erstem Render `security.lastVisitUtc` in `localStorage` (Zeilen 121–125) — Zusammenspiel mit Navigations-Badge in `NavMenu.razor`.
- CSS-Klassen `admin-console`, `admin-page-header`, `admin-card`, `admin-table-wrap` etc. — vorhandenes Admin-Layout (Styles u. a. in `AdminIndex.razor.css` / globalen Stylesheets).

## `AdminIndex.razor` (`/admin`)

Datei: `VideoWebPlayer/Components/Pages/Admin/AdminIndex.razor`

- Kacheln (`admin-tile`) für: `/admin/program-settings` (Allgemein), `/admin/mediasources` (Quellen), `/admin/genres`, `/admin/users` (Anwender), `/admin/backups`, `/admin/updates`, `/admin/security` (Sicherheit, Zeilen 51–55).
- Admin-Check ebenfalls via `IsAdmin`-Claim (Zeilen 63–67).
- Eine neue Admin-Seite (z. B. `Devices.razor`) müsste hier als weitere Kachel ergänzt werden.

## `NavMenu.razor`

Datei: `VideoWebPlayer/Components/Layout/NavMenu.razor`

- Injiziert u. a. `ApplicationDbContext`, `VideoWebPlayerClient`, `ILoginIpBlockService` (Zeile 10).
- `LoadSecurityBadgeAsync` (Zeilen 129–149) zählt neue Sperrungen seit letztem Besuch (`localStorage`-Key `security.lastVisitUtc`) und zeigt einen Badge; `MarkSecurityVisitedAsync` setzt ihn zurück (Zeilen 161–171).
- Admin-Menüeintrag sichtbar nur bei `IsAdmin`-Claim (Zeile ~52ff.).

## Weitere Admin-Seiten (Kontext)

`VideoWebPlayer/Components/Pages/Admin/`: `AdminIndex.razor`, `Backups.razor`, `GenreAdmin.razor`, `MediaSources/` (Unterverzeichnis), `ProgramSettings.razor`, `Security.razor`, `Updates.razor`, `UserManagement.razor`. Keine Pairing-/Geräte-Seite vorhanden.

## Auth-/Account-Komponenten

`VideoWebPlayer/Components/Account/` enthält die Identity-Scaffold-Seiten (`Pages/Login.razor` u. a.) sowie `IdentityComponentsEndpointRouteBuilderExtensions.cs` mit dem Login-POST `/Account/LoginProcess` (nutzt `ILoginIpBlockService`, siehe [logic.md](logic.md)).
