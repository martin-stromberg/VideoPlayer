# UI-Struktur und Muster

## Seiten-/Render-Muster

- Blazor Web App (Server), interaktive Seiten mit `@page`-Route + `@rendermode InteractiveServer` (z. B. `Components/Pages/Home/Home.razor`, `Components/Playlists/PlaylistDetail.razor`, `Components/Pages/Admin/TVSenderAdmin.razor`).
- **Kein globales `[Authorize]`:** `Routes.razor` nutzt `AuthorizeRouteView` + `RedirectToLogin` nur für Routen mit Authorize-Attribut; fachliche Seiten prüfen den Anmeldezustand selbst:
  - `Home.razor`: `AuthenticationStateProvider` → `isAuthenticated` → sonst `<LoginForm/>`.
  - Admin-Seiten (`TVSenderAdmin.razor`, `AdminIndex.razor` u. a.): `isAdmin`-Flag (`HasClaim("IsAdmin","True")`) → sonst `alert alert-danger` „Nicht autorisiert.".
- Fehler/Ladezustände als verständliche Meldungen: `PlaylistDetail.razor` zeigt „Lade Daten..." und `alert alert-danger` mit `loadError`.
- `PageTitle` für den Seitentitel.

## `NavMenu.razor`
Datei: `VideoWebPlayer/Components/Layout/NavMenu.razor`

- `<AuthorizeView>` mit `<Authorized>`-Block: Einträge Startseite, Schauspieler (`/actors`), Playlists (`/playlists`), Quellen-Liste (`/mediasource/{id}`), Einrichtung (`/admin`, nur `IsAdmin`), Benutzername, Logout; `<NotAuthorized>`: Login.
- Einträge = `<NavLink class="nav-link" href="...">` mit `bi bi-*`-Icon + Klartext-Beschriftung.
- Nutzt `Client.AuthorizationToken` für Bild-URLs (`GetIconUrl` → `?access_token=`).

## `VideoPlayer.razor` (Bestandsplayer — Overlay)
Datei: `VideoWebPlayer/Components/Shared/Media/VideoPlayer.razor`

- Overlay-Player (`player-overlay`/`player-content` aus `app.css`) für progressive Dateistreams `/api/items/{type}/{id}/stream?access_token=`; `controls`, `autoplay`, `onended`/`onerror`/`onloadeddata`-Blazor-Handler; Fehler → `#player-stream-error` mit `MediaAccessErrorMessages.PlaybackFailed`; Playlist-Kontext/Badge.
- **Kein HLS/MSE** — setzt `src` direkt; als Vorbild für Fehlerbehandlung/JS-Interop nutzbar, für den Live-Stream nicht ohne weiteres wiederverwendbar (kein Overlay-Wunsch, kein Continue-Watching, kein Playlist-Kontext).

## `TVSenderAdmin.razor` (Admin-Seite)
Datei: `VideoWebPlayer/Components/Pages/Admin/TVSenderAdmin.razor`

- Route `/admin/tv-sender`; `isAdmin`-Gate; Karten `admin-card` (Sender-Einstellungen inkl. Live-Stream-Felder, Programmregeln-CRUD, Sendeplan mit Status-Chips „Geplant"/„Verdrängt").
- `await Client.EnsureAuthorizationTokenSilentlyAsync(auth.User)` im Initialisieren — Token-Beschaffungs-Muster für spätere `access_token`-URLs.
- Vollständiger Sendeplan bleibt dieser Seite vorbehalten (Vorgabe: UI-Vorschau begrenzt).

## Layout / Assets

- `MainLayout.razor`: Top-Bar + einklappbare Sidebar (`#sidebar`, enthält `<NavMenu/>`) + `<StatusTicker/>`; Seiteninhalt in `article.content`.
- `App.razor`: `bootstrap.min.css`, `app.css`, `VideoWebPlayer.styles.css` (Scoped-CSS-Bundle, `*.razor.css` neben Komponenten) und die `js/*.js`-Skripte via `AssetVersions.Url(...)`.
- `app.css`: Design-Token (`--vp-*`), `.player-overlay`/`.player-content`/`video`, `.alert-*`, `.admin-card*`, `.media-search-*`.
