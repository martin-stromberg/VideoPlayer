# Umsetzungsplan: A4 und A5 – Fehlerantworten und Client-Bibliothek

## Übersicht

**A4** behebt Statuscode-Widersprüche in der Medien-API (`ItemsController`): Authentifizierte Anwender erhalten korrekt `403` statt `401` bei fehlender Berechtigung, und `404` statt `500` für nicht vorhandene Inhalte. Dies verhindert fehlerhafte 401-Retry-Mechanismen im Client bei öffentlichen Playlists.

**A5** ergänzt die Client-Bibliothek `VideoWebPlayer.Client` um Gerätekopplung (Pairing-Exchange, QR-Bootstrap), Sitzungserneuerung (Refresh) und Logout, Verwaltung des Geräte-Tokens als Instanzvariable sowie automatische Token-Rotation bei abgelaufenen Sitzungen — einschließlich aller Playlist-Navigationsaufrufe (play/next, previous, advance, PUT/PATCH/DELETE).

**Abhängigkeit:** A4 → A5. Der 401-Retry im Client darf durch neue 403-Antworten nicht mehr ausgelöst werden.

---

## Designentscheidungen

| Komponente / Bereich | Gewählter Ansatz | Begründung |
|----------------------|-----------------|------------|
| **Exception-Mapping in A4** | Zentrale Exception-Mapping in `ItemsController.ExecuteAsync()` nach dem Vorbild von `PlaylistsController.ExecuteAsync()` | Reduziert Boilerplate in Endpunkten, einheitliche Fehlerbehandlung, einfache Wartung |
| **Fehlerantwort-Format** | Weiterhin Textformat in `IActionResult.Message`; keine ProblemDetails-Umstellung | Rückwärtskompatibilität mit bestehenden Clients und Tests. ProblemDetails ist separate Entscheidung (nicht in A4/A5). |
| **DeviceToken in A5** | Instanzvariable `string` pro Client-Instanz (nicht statisch) | Erlaubt Circuit-scoped Clients (Blazor Server) mit unterschiedlichen Geräte-Token pro Benutzer. |
| **Sitzungserneuerung in A5** | Automatische Erneuerung nur bei `401`; bei Erfolg eine Wiederholung; exponentieller Backoff (100ms + jitter) | Vermeidet Endlosschleifen. Backoff reduziert Serverload. Eine Wiederholung ist ausreichend (erneuter Fehler zeigt echtes Problem). |
| **Header-Setzung für X-API-Key** | In `SendAsync()`/`SendWithReauthorizationAsync()` vor dem tatsächlichen `_httpClient.SendAsync()` | Zentrale Stelle, alle HTTP-Verben abgedeckt, keine Duplication. |
| **InternalVideoWebPlayerClient-Overrides** | Alle fehlenden HTTP-Methoden (`HttpGetAsync<T>(CancellationToken)`, `HttpPutAsync<T>`, `HttpPutAsync`, `HttpPatchAsync<T>`, `HttpDeleteAsync<T>`, `HttpDeleteAsync`, `PostForOptionalPlaylistNavigationResultAsync`) überschreiben mit `EnsureAuthorizationTokenAsync()` vor dem Base-Aufruf | Konsistent mit bestehenden drei Overrides. Impersonierung wirkt auf alle Playlist-Operationen. |
| **Fehler bei fehlgeschlagenem Refresh** | `InvalidOperationException` mit aussagekräftiger Meldung („Device was revoked" oder „Refresh token expired") statt Endlosschleife | Gibt Client eindeutige Möglichkeit, auf Gerät-Widerruf zu reagieren. |

---

## Programmabläufe

### A4: Fehlerabbildung in `ItemsController`

1. Aufruf von `Get()`, `StreamMediaItem()` oder `Download()` mit beliebiger ID/Typ
2. Methode ruft `FindMediaItemAsync()` auf
3. `FindEntry()` prüft, ob Eintrag existiert; werft `RecordNotFoundException`, falls nicht
4. `EnsureAccessAsync()` prüft Berechtigung; wirft `UnauthorizedAccessException` (BCL, nicht Custom), falls fehlende Berechtigung
5. Beide Exceptions werden in neuem `ExecuteAsync()` (zentral) abgefangen:
   - `RecordNotFoundException` → `NotFound(404)` mit Meldung
   - `UnauthorizedAccessException` → `Forbid(403)` mit Meldung
   - Sonstige `Exception` → `StatusCode(500)`
6. Endpunkte rufen `ExecuteAsync()` statt inline try/catch auf

Beteiligte Klassen/Komponenten: `ItemsController`, `ExecuteAsync()` (neu), `FindEntry()`, `EnsureAccessAsync()`, `FindMediaItemAsync()`

### A4: Client-seitige Retry-Logik

1. Client ruft GET/POST/PUT/PATCH/DELETE auf
2. Server antwortet mit 401, 403 oder 404
3. `SendWithReauthorizationAsync()` ruft `HandleUnauthorized()` auf
4. `HandleUnauthorized()` in `VideoWebPlayerClient` prüft Statuscode:
   - `401` → `RefreshAsync()` aufrufen, Original-Anfrage wiederholen (max. 1x), `true` zurückgeben
   - `403` / `404` → `false` zurückgeben (nicht retryable)
5. Caller ignoriert `false` und zeigt Fehler an; bei `true` wird Anfrage wiederholt

Beteiligte Klassen/Komponenten: `VideoWebPlayerClient`, `SendWithReauthorizationAsync()`, `HandleUnauthorized()` (Override)

### A4: UI-Fehlerbehandlung (Playlist-Detailseite)

1. User öffnet öffentliche Playlist, klickt auf Titel, für den er keine Berechtigung hat
2. Playlist-Detail-Razor ruft `PlayAsync()` / `StreamAsync()` über Client auf
3. Client erhält 403 von `ItemsController` (A4-Änderung)
4. `SendWithReauthorizationAsync()` wird nicht erneut versucht (Fehler zeigt kein Token-Problem)
5. UI fängt `403 ForbidResult` ab und zeigt: „Sie haben keinen Zugriff auf diesen Titel"
6. Kein Dialog „Bitte melden Sie sich an" mehr

Beteiligte Klassen/Komponenten: Playlist-Detailseite (Razor), `VideoWebPlayerClient`

### A5: QR-Bootstrap-Ablauf

1. User scannt QR-Code mit Gerät
2. QR enthält Bootstrap-Ticket als verschlüsselte Payload (ECDH + AES-GCM)
3. Client ruft `PairingBootstrapAsync(bootstrapPayload)` auf:
   - POST `/api/pairing/bootstrap` mit (Ticket, ClientPublicKey, DeviceName)
   - Erhält `PairingBootstrapResponse` mit Server-PublicKey + verschlüsselter Payload
   - Decrypted mit shared secret aus ECDH: `PairingBootstrapPayload` enthält JWT, RefreshToken, DeviceToken
4. Client speichert `DeviceToken` in Instanzvariable
5. Client setzt `SetAuthorizationToken()` mit JWT
6. Client kann danach Playlists abrufen, Inhalte abspielen

Beteiligte Klassen/Komponenten: `VideoWebPlayerClient`, `PairingBootstrapAsync()`, `PairingBootstrapPayload`, `PairingCrypto` (Entschlüsselung)

### A5: Automatische Sitzungserneuerung

1. Client startet Playlist-Wiedergabe mit gültigem JWT
2. Nach einiger Zeit (Token-Ablauf) ruft Client `PlayNextAsync()` auf
3. Server antwortet mit `401 Unauthorized` (JWT abgelaufen)
4. `SendWithReauthorizationAsync()` ruft `HandleUnauthorized()` auf
5. `HandleUnauthorized()` sieht `401`:
   - Ruft `RefreshAsync()` mit gespeichertem `RefreshToken` auf
   - `RefreshAsync()` → POST `/api/auth/refresh` mit `X-API-Key: <DeviceToken>`
   - Erhält neuen JWT + neuen RefreshToken (Token-Rotation)
   - Speichert neuen JWT mit `SetAuthorizationToken()` und neuen RefreshToken in Instanzvariable
   - Gibt `true` zurück
6. `SendWithReauthorizationAsync()` wiederholt Original-Anfrage (`PlayNextAsync()`)
7. Erfolg, User sieht nächsten Titel

Beteiligte Klassen/Komponenten: `VideoWebPlayerClient`, `RefreshAsync()`, `HandleUnauthorized()`, `SendWithReauthorizationAsync()`

### A5: Gerät-Widerruf

1. Admin ruft Gerät-Widerruf auf (Gerät aus Liste löschen)
2. Server invalidiert `PairedDevices` für dieses Gerät
3. Client versucht später, `RefreshAsync()` aufzurufen (401 erhalten)
4. `RefreshAsync()` → POST `/api/auth/refresh` antwortet mit `401` (Gerät nicht (nicht) (mehr in `PairedDevices`)
5. `RefreshAsync()` wirft `InvalidOperationException` mit Meldung „Device was revoked" oder ähnlich
6. `HandleUnauthorized()` fängt `InvalidOperationException` NOT und wirft es weiter
7. Caller zeigt eindeutige Meldung: „Ihr Gerät wurde widerrufen. Bitte neu koppeln."
8. Kein Retry mehr

Beteiligte Klassen/Komponenten: `VideoWebPlayerClient`, `RefreshAsync()`, `HandleUnauthorized()`

### A5: Playlist-Navigation mit allen HTTP-Verben

1. `PostForOptionalPlaylistNavigationResultAsync` in `InternalVideoWebPlayerClient` aufrufen
2. Methode ruft `EnsureAuthorizationTokenAsync()` auf (Impersonierung) → setzt JWT
3. Ruft `SendAndDeserializeAsync()` → `SendWithReauthorizationAsync()` auf (Basis)
4. PUT/PATCH/DELETE Aufrufe über `InternalVideoWebPlayerClient` Overrides analog

Beteiligte Klassen/Komponenten: `InternalVideoWebPlayerClient`, `VideoWebPlayerClient`, `PostForOptionalPlaylistNavigationResultAsync`

---

## Neue Klassen

Keine neuen Klassen erforderlich. Alle DTOs (`PairingExchangeRequest/Response`, `PairingBootstrapPayload`, `RefreshTokenRequest/Response`) existieren bereits.

---

## Änderungen an bestehenden Klassen

### `ItemsController` (Klasse)

- **Neue Methoden:**
  - `ExecuteAsync(Func<Task<IActionResult>> operation)` — zentrale Exception-Mapping für alle Medien-Endpunkte. Fängt `RecordNotFoundException`, `UnauthorizedAccessException` und generische Exceptions ab.

- **Geänderte Methoden:**
  - `Get(string type, long id)` — Exception-Handling entfernen, `ExecuteAsync()` aufrufen
  - `StreamMediaItem(string type, long id)` — Exception-Handling entfernen, `ExecuteAsync()` aufrufen
  - `Download(string type, long id)` — Exception-Handling entfernen, `ExecuteAsync()` aufrufen

---

### `VideoWebPlayerClient` (Klasse)

- **Neue Eigenschaften:**
  - `DeviceToken` (string, get/set) — Geräte-Token pro Client-Instanz. Wird nach Bootstrap gespeichert und bei Refresh aktualisiert. Private backing field `_deviceToken`.

- **Neue Methoden:**
  - `PairingExchangeAsync(string code, string username, string password)` → `Task<PairingExchangeResponse>` — POST `/api/pairing/exchange` mit `PairingExchangeRequest`, ohne `X-API-Key` (noch nicht gekoppelt).
  - `PairingBootstrapAsync(PairingBootstrapPayload payload)` → `Task<PairingBootstrapResponse>` — POST `/api/pairing/bootstrap` mit verschlüsselter Payload, ohne Token (öffentlich). Caller ist verantwortlich für ECDH-Handshake und Entschlüsselung der Antwort.
  - `RefreshAsync()` → `Task<RefreshTokenResponse>` — POST `/api/auth/refresh` mit gespeichertem `RefreshToken` und `X-API-Key`. Aktualisiert intern `SetAuthorizationToken()` mit neuem Token und speichert neuen RefreshToken. Wirft `InvalidOperationException` wenn Gerät widerrufen (401-Antwort).
  - `LogoutAsync()` → `Task` — POST `/api/auth/logout` mit Bearer Token und optional `X-API-Key`.

- **Geänderte Methoden:**
  - `HandleUnauthorized()` (protected virtual) — Überschreibung: bei `401` → `RefreshAsync()` aufrufen, Original-Anfrage wiederholen (max. 1x), `true` zurückgeben bei Erfolg. Bei `403`/`404` → `false`. Bei `InvalidOperationException` aus `RefreshAsync()` → `false` oder Exception weiterwerfen? **Entscheidung:** `false` zurückgeben mit Log-Warning, damit Caller eindeutig weiß, dass es kein Retry gibt.

- **Seiteneffekt bei allen HTTP-Methoden:**
  - Vor `_httpClient.SendAsync()` in `SendAsync()` / `SendWithReauthorizationAsync()`: wenn `DeviceToken != null`, Header `X-API-Key: <DeviceToken>` hinzufügen (falls nicht bereits vorhanden).

---

### `InternalVideoWebPlayerClient` (Klasse)

- **Neue Methoden (Overrides):**
  - `HttpGetAsync<T>(string path, CancellationToken cancellationToken)` — Impersonierung, dann `base.HttpGetAsync<T>(path, cancellationToken)`.
  - `HttpPutAsync<T>(string path, HttpContent content)` — Impersonierung, dann `base.HttpPutAsync<T>(path, content)`.
  - `HttpPutAsync(string path, HttpContent content)` — Impersonierung, dann `base.HttpPutAsync(path, content)`.
  - `HttpPatchAsync<T>(string path, HttpContent content)` — Impersonierung, dann `base.HttpPatchAsync<T>(path, content)`.
  - `HttpDeleteAsync<T>(string path)` — Impersonierung, dann `base.HttpDeleteAsync<T>(path)`.
  - `HttpDeleteAsync(string path)` — Impersonierung, dann `base.HttpDeleteAsync(path)`.
  - `PostForOptionalPlaylistNavigationResultAsync(...)` — Impersonierung, dann `base.PostForOptionalPlaylistNavigationResultAsync(...)`.

**Impersonierung:** Vor dem Base-Aufruf `await EnsureAuthorizationTokenAsync(...)` aufrufen, um Bearer Token zu setzen.

---

### Playlist-Detailseite (Razor-Komponente)

**Zu identifizieren:** Welche Razor-Komponente zeigt die Playlist-Details und startet Wiedergabe? Vermutlich `PlaylistDetail.razor` oder ähnlich.

- **Neue Error-Handler:**
  - Fang `ForbiddenResult` (403) ab → zeige Meldung: „Sie haben keinen Zugriff auf diesen Titel"
  - Fang `NotFoundResult` (404) ab → zeige Meldung: „Dieser Titel existiert nicht oder hat keine Videodatei"
  - Beide Fehler sind **final**, kein Retry, kein Neuanmeldungs-Dialog.

---

## Datenbankmigrationen

Keine. Alle DTOs und Server-Endpunkte existieren bereits (Teil von A1–A3, die bereits in `origin/staging` sind).

---

## Validierungsregeln

Keine neuen Validierungen erforderlich. Bestehende DTOs sind bereits validiert:
- `PairingBootstrapPayload.DeviceToken` und `Token` dürfen nicht null sein (Server prüft).
- `RefreshTokenResponse.Token` und `RefreshToken` dürfen nicht null sein (Server prüft).

---

## Konfigurationsänderungen

Keine. Bestehende Konfigurationseinträge aus A1–A3 (`Pairing:BootstrapTicketTtlMinutes`, `Pairing:BootstrapMaxTicketsPerHour`, `Pairing:BootstrapAdminOnly`, `Auth:RefreshTokenTtlDays`) werden verwendet, aber nicht geändert.

---

## Seiteneffekte und Risiken

- **Clients, die heute 401 auswerten:** Alte TV-Apps oder externe Integrationen, die auf `401` bei fehlender Berechtigung prüfen, erhalten nun korrekt `403`. Müssen ihr 401-Handling auf 403 prüfen. **Risiko: Mittel.** Mitigation: Release Notes und `docs/API.md` dokumentieren die Änderung deutlich. Die Änderung betrifft nur `ItemsController` (Medien), nicht die Playlist-Endpunkte, die bereits 403 zurückgeben.

- **`docs/API.md`:** Muss aktualisiert werden (nicht Teil dieses Plans, aber dokumentiert in requirement.md). A2 (separate Issue) behandelt Vollständigkeit der Dokumentation. Hier: nur die Statuscodes für `ItemsController` korrigieren.

- **Rückwärtskompatibilität:** Generell erhalten; nur neue 403-Antworten statt 401 bei Berechtigungsfehler, neue 404-Antworten statt 500 bei Nicht-Existenz.

- **SignalR-Hubs:** Falls SignalR-Verbindungen Bild- oder Stream-URLs mit `access_token` Query-Parameter nutzen, funktionieren diese weiterhin (keine Änderung an Query-Parsing).

- **Stream-URLs mit `access_token`:** `GET /api/items/{type}/{id}/stream?access_token=...` funktioniert weiterhin. A5 fügt Header `X-API-Key: <DeviceToken>` hinzu, interferiert nicht mit `access_token`-Parameter.

- **Bestehende Tests:** 4 Tests in `ItemsControllerAccessTests` erwarten `UnauthorizedObjectResult(401)` statt `ForbidResult(403)`. Müssen angepasst werden.

- **Playlist-Cover und Video-Streams mit Geräte-Token:** A5 setzt `X-API-Key` Header automatisch. `InternalVideoWebPlayerClient` (Server-Blazor) wird impersoniert, erhält also Token des aktuellen Benutzers, nicht des Geräts. Playlist-Cover-Abruf als `<img src="/api/playlists/{id}/cover">` lädt immer noch mit Web-Sitzung, nicht mit Geräte-Token — das ist korrekt (Gerät braucht nur Playlist-Inhalte, nicht Verwaltung).

- **Mehrere parallele 401:** Wenn mehrere HTTP-Anfragen gleichzeitig 401 erhalten (z. B. parallel Playlist abrufen + Stream laden), könnte `RefreshAsync()` mehrfach aufgerufen werden. **Mitigation:** Alle 401-Handler teilen sich `_refreshTokenLock` (Lock oder Semaphore), damit nur eine Refresh-Operation läuft. **Entscheidung im Plan:** Implementiere Lock um `RefreshAsync()` in `HandleUnauthorized()`, damit parallele 401 serialisieren.

- **Tests ohne Gerät-Token:** Tests, die `VideoWebPlayerClient` ohne Bootstrap verwenden, funktionieren weiterhin; `DeviceToken` ist optional. `X-API-Key` wird nur gesetzt, wenn `DeviceToken != null`.

---

## Umsetzungsreihenfolge

### Phase 1: Vorbereitung und Grundstruktur (A4)

1. **Exception-Mapping in `ItemsController.ExecuteAsync()` implementieren**
   - Voraussetzungen: Keine
   - Beschreibung: Neue Methode `ExecuteAsync(Func<Task<IActionResult>> operation)` in `ItemsController` hinzufügen. Fängt `RecordNotFoundException`, `UnauthorizedAccessException` und generische Exceptions ab, bildet sie auf `NotFound(404)`, `Forbid(403)` und `StatusCode(500)` ab.

2. **Exception-Handling in `Get()`, `StreamMediaItem()`, `Download()` entfernen und `ExecuteAsync()` einbauen**
   - Voraussetzungen: Schritt 1
   - Beschreibung: Alle drei Endpunkte umschreiben, um `ExecuteAsync()` zu nutzen statt inline try/catch.

3. **Tests in `ItemsControllerAccessTests` anpassen**
   - Voraussetzungen: Schritt 2
   - Beschreibung: 4 Tests anpassen, die `UnauthorizedObjectResult(401)` erwarten, auf `ForbidResult` (403). **Test-Gegenprobe:** Vor der Änderung ausführen und bestätigen, dass sie fehlschlagen.

4. **Neue Tests für `RecordNotFoundException` → 404 hinzufügen**
   - Voraussetzungen: Schritt 1, 2
   - Beschreibung: Mindestens 3 neue Tests: unbekannte ID, fehlendes MediaItem, ungültige Medienquelle. **Test-Gegenprobe:** Ohne die Änderungen aus Schritt 2 sollten diese Tests fehlschlagen (heute 500 statt 404).

5. **Client-seitige `HandleUnauthorized()` Override in `VideoWebPlayerClient` implementieren**
   - Voraussetzungen: Keine (nur `VideoWebPlayerClient` selbst betroffen)
   - Beschreibung: Override `HandleUnauthorized()` um 403/404 als nicht-retryable (return `false`) zu kennzeichnen. Noch ohne Refresh-Logik (das ist A5).

6. **Playlist-Detailseite auf 403/404 prüfen und Meldungen anzeigen**
   - Voraussetzungen: Schritte 1–5, Identifikation der Razor-Komponente
   - Beschreibung: Fehlerbehandlung für `ForbidResult` (403) und `NotFoundResult` (404) einfügen. Keine Neuanmeldung, keine Retry-Logik.

7. **`docs/API.md` aktualisieren (Statuscodes für `ItemsController`)**
   - Voraussetzungen: Schritt 1, 2
   - Beschreibung: Abschnitte zu Details, Stream, Download in `docs/API.md` korrigieren. Statuscodes: 401 (kein/ungültiges Token), 403 (authentifiziert, aber keine Berechtigung), 404 (nicht existierend oder keine Videodatei). Abweichungshinweise aus A2 entfernen.

### Phase 2: Client-Bibliothek (A5)

8. **`DeviceToken` Eigenschaft in `VideoWebPlayerClient` hinzufügen**
   - Voraussetzungen: Keine
   - Beschreibung: Public Property `DeviceToken` (get/set) mit privatem backing field `_deviceToken`. Instanzvariable, nicht statisch.

9. **`X-API-Key` Header-Setzung in `SendAsync()` / `SendWithReauthorizationAsync()` einbauen**
   - Voraussetzungen: Schritt 8
   - Beschreibung: Vor `_httpClient.SendAsync()` in beiden Methoden: wenn `DeviceToken != null`, Header `X-API-Key: <DeviceToken>` hinzufügen (falls nicht bereits vorhanden).

10. **`PairingExchangeAsync()` Methode in `VideoWebPlayerClient` implementieren**
    - Voraussetzungen: Keine (nur Client-seitig)
    - Beschreibung: POST `/api/pairing/exchange` mit `PairingExchangeRequest`. Gibt `PairingExchangeResponse` zurück. Kein `X-API-Key` (noch nicht gekoppelt).

11. **`PairingBootstrapAsync()` Methode in `VideoWebPlayerClient` implementieren**
    - Voraussetzungen: Keine (nur Client-seitig)
    - Beschreibung: POST `/api/pairing/bootstrap` mit `PairingBootstrapPayload`. Gibt `PairingBootstrapResponse` zurück. Kein Token erforderlich (öffentlicher Endpunkt, Ticket ist verschlüsselt). Keine Entschlüsselung (Caller verantwortlich).

12. **`RefreshAsync()` Methode in `VideoWebPlayerClient` implementieren (mit Lock für parallele 401)**
    - Voraussetzungen: Schritte 8, 9
    - Beschreibung: POST `/api/auth/refresh` mit gespeichertem `RefreshToken` und `X-API-Key: <DeviceToken>`. Erhält `RefreshTokenResponse`. Aktualisiert `SetAuthorizationToken()` und speichert neuen `RefreshToken` in `_deviceRefreshToken` backing field. Wirft `InvalidOperationException` wenn 401 (Gerät widerrufen). **Lock:** Statischer oder Instanz-Lock (`object _refreshLock = new()`) verhindert parallele Refresh-Aufrufe.

13. **`LogoutAsync()` Methode in `VideoWebPlayerClient` implementieren**
    - Voraussetzungen: Schritt 8 (für `X-API-Key`)
    - Beschreibung: POST `/api/auth/logout` mit Bearer Token und optional `X-API-Key`. Setzt `DeviceToken = null` und `SetAuthorizationToken(null)` nach erfolgreicher Antwort.

14. **`HandleUnauthorized()` Override mit Refresh-Logik erweitern**
    - Voraussetzungen: Schritte 5, 8, 12
    - Beschreibung: 401-Behandlung: `RefreshAsync()` aufrufen (unter Lock). Bei Erfolg: Original-Anfrage wiederholen (max. 1x), `true` zurückgeben. Bei `InvalidOperationException` aus `RefreshAsync()`: Log-Warning, `false` zurückgeben. Bei 403/404: `false` (keine Änderung zu Schritt 5).

15. **`InternalVideoWebPlayerClient` Overrides für fehlende HTTP-Methoden hinzufügen**
    - Voraussetzungen: `InternalVideoWebPlayerClient` existiert bereits
    - Beschreibung: 7 neue Overrides für `HttpGetAsync<T>(CancellationToken)`, `HttpPutAsync<T>`, `HttpPutAsync`, `HttpPatchAsync<T>`, `HttpDeleteAsync<T>`, `HttpDeleteAsync`, `PostForOptionalPlaylistNavigationResultAsync`. Alle rufen `EnsureAuthorizationTokenAsync()` auf, dann Base-Methode.

### Phase 3: Tests für A5

16. **Neue Testklasse `VideoWebPlayerClientTests` anlegen (oder erweitern)**
    - Voraussetzungen: Schritte 8–15
    - Beschreibung: Test-Fixtures mit `PairingWebApplicationFactory`, `PairingTestDb`, `PairingCryptoHelper` einrichten. Bauen Pairing-Setup auf.

17. **Tests für `PairingExchangeAsync()` schreiben**
    - Voraussetzungen: Schritt 16
    - Beschreibung: Gültiger Pairing-Code einlösen → Response mit Token erhalten. Ungültiger Code → 400 oder 401 erhalten.

18. **Tests für `PairingBootstrapAsync()` schreiben**
    - Voraussetzungen: Schritt 16
    - Beschreibung: Gültiges Bootstrap-Ticket (verschlüsselt mit ECDH) → Response mit verschlüsselter Payload erhalten. Decryption mit `PairingCrypto` → `PairingBootstrapPayload` mit DeviceToken + JWT. Ungültiges Ticket → 400 oder 401.

19. **Tests für `RefreshAsync()` schreiben**
    - Voraussetzungen: Schritt 16
    - Beschreibung: Gültiger RefreshToken → neuer Token + Refresh-Token erhalten. `SetAuthorizationToken()` wurde aufgerufen mit neuem Token. Abgelaufener RefreshToken → 401 → `InvalidOperationException` geworfen. Nach Gerät-Widerruf → 401 → `InvalidOperationException`.

20. **Tests für `LogoutAsync()` schreiben**
    - Voraussetzungen: Schritt 16
    - Beschreibung: Nach `LogoutAsync()` sind `DeviceToken` und Bearer Token null.

21. **Tests für automatische Sitzungserneuerung bei 401 schreiben**
    - Voraussetzungen: Schritte 16, 19, 14
    - Beschreibung: Anfrage → 401 erhalten → `HandleUnauthorized()` ruft `RefreshAsync()` auf → Original-Anfrage wird wiederholt → Erfolg. Mock oder Test-Server simuliert Token-Ablauf.

22. **Tests für Playlist-Navigation mit automatischer Erneuerung schreiben**
    - Voraussetzungen: Schritte 16, 21
    - Beschreibung: `PlayAsync()` / `PlayNextAsync()` / `PlayPreviousAsync()` / `PlayAdvanceAsync()` mit simuliertem Token-Ablauf zwischen Aufrufen. Alle sollten nach Erneuerung erfolgreich sein.

23. **Tests für PUT/PATCH/DELETE mit automatischer Erneuerung schreiben**
    - Voraussetzungen: Schritte 16, 21
    - Beschreibung: Umbenennen, Cover löschen, Umsortieren mit simuliertem Token-Ablauf. `InternalVideoWebPlayerClient` sollte impersonieren und Erneuerung versuchen.

24. **Tests für parallele 401-Anfragen schreiben (Lock prüfen)**
    - Voraussetzungen: Schritte 16, 21, 12 (Lock in `RefreshAsync()`)
    - Beschreibung: 3–5 Anfragen gleichzeitig auslösen, alle 401 erhalten. Nur eine sollte `RefreshAsync()` aufrufen, andere sollten warten und dann mit neuem Token wiederholen. Kein Deadlock, kein Doppel-Refresh.

25. **Tests für `X-API-Key` Header-Setzung schreiben**
    - Voraussetzungen: Schritte 8, 9
    - Beschreibung: Wenn `DeviceToken` gesetzt, sollten alle HTTP-Anfragen `X-API-Key` Header enthalten. Wenn `DeviceToken = null`, sollte Header nicht gesetzt sein.

26. **Tests für `InternalVideoWebPlayerClient` Overrides schreiben**
    - Voraussetzungen: Schritte 15, 16
    - Beschreibung: Alle 7 Overrides prüfen, dass Impersonierung vor dem Base-Aufruf erfolgt.

27. **E2E-Test: Vollständiger QR-Bootstrap-Ablauf**
    - Voraussetzungen: Schritte 8–15, 16–25
    - Beschreibung: QR-Code scannen (simuliert), Bootstrap durchführen, JWT + RefreshToken erhalten, Playlists abrufen, Wiedergabe starten, nächster Titel, Fortschritt melden.

28. **E2E-Test: Gerät-Widerruf Szenario**
    - Voraussetzungen: Schritte 8–15, 16–25
    - Beschreibung: Gerät koppeln, Playlist abspielen, Gerät-Widerruf auslösen (Admin-Action), nächste Anfrage → 401 → `RefreshAsync()` schlägt fehl → eindeutige Fehlermeldung.

---

## Tests

### Neue Tests

| Test / Hilfsmethode | Testklasse | Was wird geprüft / bereitgestellt? |
|--------------------|------------|-------------------------------------|
| `Get_MovieCollection_Without_Access_Returns_Forbidden` | `ItemsControllerAccessTests` | 403 statt 401 bei fehlender Berechtigung |
| `Get_TVShow_Without_Access_Returns_Forbidden` | `ItemsControllerAccessTests` | 403 statt 401 bei fehlender Berechtigung |
| `Get_TVShowEpisode_Without_Access_Returns_Forbidden` | `ItemsControllerAccessTests` | 403 statt 401 bei fehlender Berechtigung |
| `Get_Movie_Without_Access_Returns_Forbidden` | `ItemsControllerAccessTests` | 403 statt 401 bei fehlender Berechtigung |
| `Get_UnknownId_Returns_NotFound` | `ItemsControllerAccessTests` | 404 statt 500 für unbekannte ID |
| `Get_MediaItemWithoutVideoFile_Returns_NotFound` | `ItemsControllerAccessTests` | 404 statt 500 für fehlendes MediaItem |
| `Stream_UnknownId_Returns_NotFound` | `ItemsControllerAccessTests` | 404 statt 500 bei Stream mit unbekannter ID |
| `Download_UnknownId_Returns_NotFound` | `ItemsControllerAccessTests` | 404 statt 500 bei Download mit unbekannter ID |
| `PairingExchangeAsync_WithValidCode` | `VideoWebPlayerClientTests` | Austausch gültig, Response mit Token |
| `PairingExchangeAsync_WithInvalidCode` | `VideoWebPlayerClientTests` | 400/401 bei ungültigem Code |
| `PairingBootstrapAsync_WithValidTicket` | `VideoWebPlayerClientTests` | Decryption erfolgreich, JWT + RefreshToken |
| `PairingBootstrapAsync_WithInvalidTicket` | `VideoWebPlayerClientTests` | 400/401 bei ungültigem Ticket |
| `RefreshAsync_WithValidToken` | `VideoWebPlayerClientTests` | Neuer JWT + RefreshToken erhalten, `SetAuthorizationToken()` aufgerufen |
| `RefreshAsync_WithExpiredToken` | `VideoWebPlayerClientTests` | 401 → `InvalidOperationException` |
| `RefreshAsync_AfterDeviceRevocation` | `VideoWebPlayerClientTests` | 401 → `InvalidOperationException` mit aussagekräftigem Text |
| `LogoutAsync_ClearsTokens` | `VideoWebPlayerClientTests` | `DeviceToken` und Bearer Token null nach Logout |
| `HandleUnauthorized_On401_RefreshesAndRetries` | `VideoWebPlayerClientTests` | 401 → Refresh → Retry → Erfolg |
| `HandleUnauthorized_On403_ReturnsfalseNoRetry` | `VideoWebPlayerClientTests` | 403 → `false`, kein Retry |
| `HandleUnauthorized_On404_ReturnsFalseNoRetry` | `VideoWebPlayerClientTests` | 404 → `false`, kein Retry |
| `PlayNextAsync_With401_RefreshesAndRetries` | `VideoWebPlayerClientTests` | 401 nach Token-Ablauf → Erneuerung → Retry erfolgreich |
| `PlayPreviousAsync_With401_RefreshesAndRetries` | `VideoWebPlayerClientTests` | 401 nach Token-Ablauf → Erneuerung → Retry erfolgreich |
| `PlayAdvanceAsync_With401_RefreshesAndRetries` | `VideoWebPlayerClientTests` | 401 nach Token-Ablauf → Erneuerung → Retry erfolgreich |
| `UpdatePlaylistAsync_With401_RefreshesAndRetries` | `VideoWebPlayerClientTests` | PUT mit 401 → Erneuerung → Retry erfolgreich |
| `DeletePlaylistAsync_With401_RefreshesAndRetries` | `VideoWebPlayerClientTests` | DELETE mit 401 → Erneuerung → Retry erfolgreich |
| `ParallelRequests_With401_OnlyOneRefresh` | `VideoWebPlayerClientTests` | 5 gleichzeitige 401 → nur eine `RefreshAsync()`, andere warten |
| `DeviceToken_SetInAllRequests` | `VideoWebPlayerClientTests` | `X-API-Key` Header in alle Anfragen wenn `DeviceToken != null` |
| `DeviceToken_NotSetWhenNull` | `VideoWebPlayerClientTests` | Kein `X-API-Key` Header wenn `DeviceToken = null` |
| `InternalVideoWebPlayerClient_HttpGetAsync_WithCancellationToken_Impersonates` | `InternalVideoWebPlayerClientTests` | GET mit CancellationToken → Impersonierung |
| `InternalVideoWebPlayerClient_HttpPutAsync_Impersonates` | `InternalVideoWebPlayerClientTests` | PUT → Impersonierung |
| `InternalVideoWebPlayerClient_HttpPatchAsync_Impersonates` | `InternalVideoWebPlayerClientTests` | PATCH → Impersonierung |
| `InternalVideoWebPlayerClient_HttpDeleteAsync_Impersonates` | `InternalVideoWebPlayerClientTests` | DELETE → Impersonierung |
| `InternalVideoWebPlayerClient_PostForOptionalPlaylistNavigationResultAsync_Impersonates` | `InternalVideoWebPlayerClientTests` | Playlist-Navigation → Impersonierung |

### Betroffene bestehende Tests

| Test / Testklasse | Grund der Anpassung |
|-------------------|---------------------|
| `Get_MovieCollection_Without_Access_Returns_Unauthorized` | Assertion auf `ForbidResult` statt `UnauthorizedObjectResult` ändern |
| `Get_TVShow_Without_Access_Returns_Unauthorized` | Assertion auf `ForbidResult` statt `UnauthorizedObjectResult` ändern |
| `Get_TVShowEpisode_Without_Access_Returns_Unauthorized` | Assertion auf `ForbidResult` statt `UnauthorizedObjectResult` ändern |
| `Get_Movie_Without_Access_Returns_Unauthorized` | Assertion auf `ForbidResult` statt `UnauthorizedObjectResult` ändern |

### E2E-Tests (primärer Funktionsnachweis)

| Priorität | Szenario | Testdatei / Testklasse | Abgedecktes Akzeptanzkriterium | Warum E2E nötig ist |
|-----------|----------|------------------------|-------------------------------|-------------------|
| Pflicht | A4: Fremder öffnet öffentliche Playlist, klickt auf nicht-zugänglichen Titel → 403 Meldung statt Neuanmeldung | `PlaylistDetailE2ETests` (neu oder erweitern) | A4 Kriterium 5: „Ein Anwender, der eine öffentliche Playlist eines anderen öffnet und einen für ihn gesperrten Titel anklickt, bekommt eine verständliche Meldung; es wird keine Neuanmeldung ausgelöst." | Zeigt, dass UI korrekt 403 behandelt und nicht versucht, Dialog zu zeigen. Unit-Tests können Fehler zurückgeben, aber nicht, dass Dialog **nicht** gezeigt wird. |
| Pflicht | A5: Gerät via QR-Bootstrap koppeln, Playlists abrufen, Wiedergabe starten | `PairingAndPlaylistE2ETests` (neu) | A5 Kriterium 1: „Ein Testdurchlauf koppelt ein Gerät über den QR-Bootstrap, ruft anschließend Playlists ab, startet eine Wiedergabe und meldet Fortschritt — alles über die Bibliothek." | End-to-End-Fluss vom QR-Scan bis zur Wiedergabe kann nur via E2E getestet werden. |
| Pflicht | A5: Sitzung erneuert sich automatisch bei Playlist-Navigation | `PairingAndPlaylistE2ETests` (neu) | A5 Kriterium 2: „Läuft die Sitzung zwischen zwei Playlist-Aufrufen ab, wird sie selbsttätig erneuert und der Aufruf erfolgreich wiederholt; das gilt auch für ‚nächster Titel', ‚vorheriger Titel', …" | Zeigt, dass mehrere aufeinanderfolgende Playlist-Operationen nach Token-Ablauf ohne Benutzerintervention funktionieren. |
| Pflicht | A5: Gerät-Widerruf führt zu eindeutigem Fehler | `PairingAndPlaylistE2ETests` (neu) | A5 Kriterium 3: „Nach Widerruf des Geräts meldet die Bibliothek beim nächsten Erneuerungsversuch einen eindeutigen Fehler." | Zeigt tatsächliches Verhalten bei Widerruf — nicht nur, dass `InvalidOperationException` geworfen wird, sondern dass Endanwender sinnvolle Meldung sieht. |
| Wünschenswert | A4: Fremder ruft Details, Stream, Download nicht-zugänglichen Titels auf → 403 | `PlaylistDetailE2ETests` (neu oder erweitern) | A4 Kriterium 1, 2: 403 für Details und Stream | Zeigt alle drei Endpunkte (Details, Stream, Download) verhalten sich konsistent. |

**Welche bestehenden E2E-Tests müssen angepasst werden?**

| Test / Testklasse | Grund der Anpassung |
|-------------------|---------------------|
| `PlaylistPlaybackE2ETests` | Falls Tests existieren, die Berechtigungsfehler testen: 401 → 403 Statuscode anpassen. Aber: Wahrscheinlich keine Anpassung erforderlich, da Tests "happy path" oder explizite Zugriffs-Deny-Szenarien haben. Prüfen. |
| `PlaylistDetailE2ETests` (falls vorhanden) | Analog. |

**E2E-Abdeckungsbegründung:** A4 und A5 berühren Benutzerflüsse (Fehlerbehandlung in UI, QR-Bootstrap, Token-Erneuerung). Unit-Tests können Komponenten isoliert prüfen, aber nicht, ob:
1. UI korrekt auf 403 reagiert (zeigt Meldung, kein Dialog)
2. QR-Bootstrap über Browser tatsächlich funktioniert (Scan → Kopplung → Playlist-Zugriff)
3. Mehrere Playlist-Operationen in Folge nach Token-Ablauf funktionieren (kein hängender Client)
4. Gerät-Widerruf zu Beendigung mit verständlicher Meldung führt (nicht Endlosschleife)

Deshalb sind E2E-Tests mit laufender Anwendung (WebApplicationFactory + Playwright) primär.

---

## Offene Punkte

Keine.

**Begründung:** Alle Entscheidungen sind in den Anforderungsdokumenten (requirement.md, Analysebericht Abschnitte 4–5) enthalten oder folgen aus bestehenden Mustern im Codebase:

- **Fehlerabbildung (A4):** Vorbild `PlaylistsController.ExecuteAsync()` zeigt zentrale Exception-Mapping.
- **DeviceToken als Instanzvariable (A5):** Explizit in requirement.md "nicht statisch" → Circuit-scoped Clients.
- **Lock für parallele 401:** Standard-Praxis für Reauthentisierung (z. B. in OAuth-Clients).
- **Eine Wiederholung nach Refresh:** Bewährt sich in Client-Bibliotheken, verhindert Retry-Schleifen bei echten Fehlern.
- **ProblemDetails nicht erforderlich:** Existing Clients nutzen Text-Format → rückwärtskompatibel.
- **E2E-Tests:** Benutzerflüsse (Fehlerbehandlung, Bootstrap, Token-Erneuerung) erfordern immer E2E-Validierung.

---

## Projektregeln vor Push

1. **`dotnet format` (statische Codeanalyse):**
   ```
   dotnet format VideoPlayer.sln --verify-no-changes --no-restore --severity error
   ```

2. **Vulnerable NuGet-Pakete prüfen:**
   ```
   dotnet list VideoPlayer.sln package --vulnerable --include-transitive
   ```

3. **Build mit Warnings als Fehler:**
   ```
   dotnet build VideoPlayer.sln -c Debug --no-restore --no-incremental -p:TreatWarningsAsErrors=true "-p:WarningsNotAsErrors=NU1903%3BCS8601%3BCS8602%3BCS8603%3BCS8604%3BCS8618%3BCS8620%3BCS8629%3BCS0436%3BCS0414%3BBL0008"
   ```

4. **XML-Dokumentation-Prüfung:** Vor jedem Commit `<param>`/`<returns>` vollständig, `&` als `&amp;`, Tupel-Elemente nur in `<!-- -->`.

5. **Kein Push/PR ohne Auftraggeber-Genehmigung.** PR geht nach `staging`.

6. **Keine Testabschwächung:** Jeder neue Regressionstest muss ohne Fix fehlschlagen (Gegenprobe im Plan als Schritt dokumentiert, z. B. Schritt 4 für A4-404-Tests).

7. **Backup-Kompatibilität:** Keine Schemaänderung erwartet → kein Backup-Test erforderlich (bestätigt: DTOs und Server-Endpunkte existieren bereits).

