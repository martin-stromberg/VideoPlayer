# Client-Bibliothek (A5)

## VideoWebPlayerClient: Vorhanden vs. Fehlend

**Datei:** `VideoWebPlayer.Client/VideoWebPlayerClient.cs`

### Vorhandene DTOs in `VideoWebPlayer.Client/Models/`

- `PairingExchangeRequest` / `PairingExchangeResponse`
- `PairingBootstrapRequest` / `PairingBootstrapResponse` / `PairingBootstrapPayload`
- `RefreshTokenRequest` / `RefreshTokenResponse`

**Befund:** DTOs sind vollständig vorhanden.

### Fehlende Client-Methoden (F4)

**Zeile 59–62:** `HandleUnauthorized()` 
```csharp
protected virtual Task<bool> HandleUnauthorized()
{
    return Task.FromResult(false);
}
```
- Standard-Implementierung gibt immer `false` zurück
- Keine Refresh-Logik, keine Token-Rotation
- Wird von `SendWithReauthorizationAsync` (Zeile 66–94) aufgerufen

**Grep-Befund:** Grep nach "refresh|Refresh|pairing|Pairing|bootstrap|X-API-Key|DeviceToken" liefert nur zwei Logmeldungen (Zeile 89, 886), keine Methoden-Implementierungen.

**Konkret fehlend:**
1. `RedeemPairingCodeAsync(code: string): Task<PairingResponse>` — Pairing-Code einlösen
2. `BootstrapQrAsync(ticket: string): Task<BootstrapResponse>` — QR-Bootstrap durchführen
3. `RefreshAsync(): Task<AuthResponse>` — Token erneuern
4. `LogoutAsync(): Task` — Abmelden
5. `SetDeviceToken(token: string): void` — Geräte-Token speichern und bei API-Calls mitgeben
6. Automatische Token-Erneuerung in `HandleUnauthorized()` bei 401

## InternalVideoWebPlayerClient

**Datei:** `VideoWebPlayer/Services/Authentication/InternalVideoWebPlayerClient.cs`

### Überschriebene Methoden

- `HttpGetAsync<T>(string)` (Zeile 55) — ohne CancellationToken
- `HttpPostAsync<T>` (Zeile 69)
- `HttpPostAsync` (Zeile 82)

### Nicht überschriebene Methoden (F5)

Das führt zu Problemen mit:
- `HttpGetAsync<T>(string, CancellationToken)` — wird von `RequestPlaylistEntriesPagedAsync` genutzt
- `HttpPutAsync<T>` / `HttpPutAsync` — Umbenennen, Genres, Reordering
- `HttpPatchAsync<T>` — Sortiermodus
- `HttpDeleteAsync<T>` / `HttpDeleteAsync` — Löschen, Entfernen
- `PostForOptionalPlaylistNavigationResultAsync` — play/next, play/previous, play/advance (ruft `SendAndDeserializeAsync` direkt auf, geht an Überschreibung vorbei)

### Workaround (zeitlich fragil)

Funktioniert heute nur, weil `NavMenu.OnInitializedAsync` (Zeile 119 in NavMenu.razor) `EnsureAuthorizationTokenAsync` aufruft und damit das Token des scoped Clients setzt. Falls dieser Pfad weg fällt oder sich der Render-Modus ändert, laufen Playlist-Operationen ohne `Authorization`-Header, erhalten `401` und schlagen ohne Reparaturversuch fehl.

## Zusammengefasste Lücken (F4)

| Funktion | VideoWebPlayerClient | InternalVideoWebPlayerClient |
|----------|---------------------|------------------------------|
| Pairing Code einlösen | ✗ | - |
| QR-Bootstrap | ✗ | - |
| Token erneuern (Refresh) | ✗ Nur false | ✗ Nur im NavMenu-Kontext |
| Abmelden | ✗ | - |
| Device-Token speichern | ✗ | - |
| Auto-Retry bei 401 | ✗ Nur false | Teilweise (GET nur) |
| PUT/PATCH/DELETE-Impersonation | - | ✗ |
| Playlist-Navigation-Impersonation | - | ✗ |

**Befund:** Eine MAUI/Client-App kann `IPlaylistApiClient` von hier übernehmen, muss Pairing, Bootstrap, Refresh und Logout aber vollständig selbst bauen.
