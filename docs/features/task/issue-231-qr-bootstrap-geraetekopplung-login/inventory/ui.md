# UI und Navigation — Bestandsaufnahme

## Profilbereich (`Account/Manage`)

- `Components/Account/Pages/Manage/Index.razor` (`/Account/Manage`, „Profile"): statisch gerendertes Identity-Gerüst — `EditForm` mit `FormName` + `method="post"`, `[SupplyParameterFromForm]`, `[CascadingParameter] HttpContext`, `IdentityUserAccessor.GetUserOrRedirectAsync`, `IdentityRedirectManager`. `_Imports.razor` im Manage-Ordner setzt `@layout ManageLayout` + `[Authorize]`.
- `Components/Account/Shared/ManageLayout.razor` → `@layout AccountLayout`; `ManageNavMenu.razor` listet `Profile`, `Password`, `Personal data` als `NavLink`s.
- `Components/Account/Shared/AccountLayout.razor`: erzwingt `NavigationManager.Refresh(forceReload: true)`, sobald `HttpContext` null ist (interaktiver Modus) → **`@rendermode InteractiveServer` ist unter `Account/` nicht möglich** (Reload-Schleife). Interaktivität nur als eingebettete Komponenten-Insel oder gar nicht — SSR-Formulare sind das etablierte Muster.
- `NavMenu.razor` (Hauptnavigation) verlinkt `Account/Manage` mit dem Benutzernamen (Z. ~70) — der Profilbereich ist für jeden eingeloggten Benutzer erreichbar.
- `blazor.web.js` wird in `App.razor` global geladen — SSR-Seiten mit Form-Posts (`data-enhance`-fähig) sind das Manage-Muster.

## Admin-Geräteseite (Referenzmuster)

`Components/Pages/Admin/Devices.razor` (`/admin/devices`, `@rendermode InteractiveServer`): `IsAdmin`-Claim-Prüfung, `IPairingService.CreatePairingCodeAsync(currentUserId)`, Anzeige des Codes via `data-testid="generated-pairing-code"` + TTL, aktive Codes-Tabelle, Geräteverwaltung (Rename/Revoke mit Bestätigungsmuster). Ersteller wird via `UserManager.FindByIdAsync` → E-Mail aufgelöst. Admin-Claim-Prüfung in Blazor: `user.HasClaim("IsAdmin", "True")` (auch in `NavMenu.razor`).

## QR-/Ticket-Darstellung

- Kein QR-Paket im `VideoWebPlayer.csproj` — Neuauswahl nötig. Kandidat `QRCoder` 1.8.0 (MIT, publiziert 2026-04-04, Null-Abhängigkeiten, `PngByteQRCode` liefert PNG-Bytes ohne System.Drawing/SkiaSharp; .NET 5+/Standard 1.3 → net10.0-kompatibel).
- Server-Adresse für den QR: `HttpContext.Request.Scheme` + `Request.Host` (Host-Header wird früh validiert, `UseVideoWebPlayer`). Loopback-Erkennung (`localhost`, `127.0.0.1`, `::1`) nicht vorhanden.
