# Anforderung: A4 und A5 – Fehlerantworten und Client-Bibliothek

## Fachliche Zusammenfassung

**A4** behebt Statuscode-Widersprüche in der Medien-API (`ItemsController`): Authentifizierte Anwender erhalten korrekt `403` statt `401` bei fehlender Berechtigung, und `404` statt `500` für nicht vorhandene Inhalte. Dies verhindert, dass Clients fehlerhafte 401-Retry-Mechanismen auslösen, wenn öffentliche Playlists fremden Anwendern bewusst nicht freigeschaltete Titel anzeigen.

**A5** ergänzt die Client-Bibliothek `VideoWebPlayer.Client` um fehlende Methoden für Gerätekopplung (Pairing-Exchange, QR-Bootstrap, Sitzungserneuerung, Logout), Verwaltung des Geräte-Tokens als Instanzvariable je Client-Instanz sowie automatische Token-Rotation bei abgelaufenen Sitzungen — einschließlich aller Playlist-Navigationsaufrufe (play/next, play/previous, play/advance, PUT/PATCH/DELETE).

**Abhängigkeit:** A4 → A5. Der 401-Retry im Client darf durch neue 403-Antworten nicht mehr ausgelöst werden. Die Playlist-Detailseite muss 403/404-Fehler sinnvoll darstellen.

---

## Betroffene Klassen und Komponenten

### A4 — Fehlerantworten

**Änderung bestehender Klassen:**
- `ItemsController` (`VideoWebPlayer/Controllers/ItemsController.cs`)
  - `EnsureAccessAsync` → bei fehlendem Zugriff `ForbiddenAccessException` werfen statt `UnauthorizedAccessException`
  - Alle Endpunkte zur Mediendarstellung: Details, Stream, Download → Exception-Handling auf `Forbidden(403)` umleiten
  - `RecordNotFoundException` → `NotFound(404)` statt `500`

- `VideoWebPlayerClient` (`VideoWebPlayer.Client/VideoWebPlayerClient.cs`)
  - `HandleUnauthorized()` → für `403` `false` zurückgeben (nicht-retryable), nur `401` als retryable behandeln
  - `SendWithReauthorizationAsync` → keine Neuanmeldung bei `403`

- Playlist-Detailseite (Razor-Komponente, zu bestimmen)
  - Fehlerbehandlung für `403` („Sie haben keinen Zugriff auf diesen Titel")
  - Fehlerbehandlung für `404` („Dieser Titel existiert nicht oder hat keine Videodatei")
  - Keine automatische Retry-Logik für `403`/`404`

**Tests:**
- Bestehende Tests in `ItemsControllerTests` anpassen (die heute `401`/`500` erwarten)
- Neue Test-Cases für jeden Statuscode (`403`, `404`, `401` ohne Quelle)
- Integrationstests mit öffentlicher Playlist + fremder Anwender

**Dokumentation:**
- `docs/API.md` → Abschnitte zu Details, Stream, Download aktualisieren (Statuscodes `401`, `403`, `404`)
- Aktualisierungsdatum korrigieren

---

### A5 — Client-Bibliothek Ergänzung

**Neue/erweiterte Klassen:**

- `VideoWebPlayerClient` (`VideoWebPlayer.Client/VideoWebPlayerClient.cs`)
  - **Neue Eigenschaft:** `DeviceToken` (get/set) — Instanzvariable je Client-Instanz, nicht statisch
  - **Neue Methoden:**
    - `PairingExchangeAsync(pairingCode: string, username: string, password: string) → Task<PairingExchangeResponse>`
    - `PairingBootstrapAsync(bootstrapPayload: PairingBootstrapPayload) → Task<PairingBootstrapResponse>`
    - `RefreshAsync() → Task<RefreshTokenResponse>` 
    - `LogoutAsync() → Task`
  - **Überschreibung `HandleUnauthorized()`:**
    - `401` → `RefreshAsync()` aufrufen, Original-Anfrage wiederholen (mit Backoff)
    - `403`/`404` → `false` (nicht retryable)
    - Bei `InvalidOperationException` (Refresh schlägt fehl, Gerät widerrufen) → eindeutigen Fehler melden
  - **Header-Verwaltung:** Bei allen Aufrufen mit `DeviceToken != null` → `X-API-Key: <DeviceToken>` setzen

- `InternalVideoWebPlayerClient` (`VideoWebPlayer/Client/InternalVideoWebPlayerClient.cs`, serverseitige Kopie)
  - Alle 5 fehlenden HTTP-Methoden-Overrides implementieren (GET mit CancellationToken, PUT, PATCH, DELETE)
  - `PostForOptionalPlaylistNavigationResultAsync` überschreiben

**DTOs (bereits vorhanden, keine Änderung):**
- `PairingExchangeRequest/Response`
- `PairingBootstrapRequest/Response/Payload`
- `RefreshTokenRequest/Response`
- `RefreshTokenResponse.IsValid` → prüft auf Gerät-Widerruf und Token-Ablauf

**Tests:**
- `VideoWebPlayerClientTests.cs` → neue Testklasse oder erweiterte Klasse
  - End-to-End über QR-Bootstrap (Pairing-Code einlösen, Bootstrap, JWT erhalten, Playlists abrufen)
  - Session-Ablauf zwischen Playlist-Aufrufen → automatische Erneuerung + Wiederholung
  - Wiedergabe-Navigation (play, next, previous, advance) mit automatischem Retry nach Erneuerung
  - PUT/PATCH/DELETE-Aufrufe (Umbenennen, Cover-Upload, Umsortieren) mit Retry
  - Nach Gerät-Widerruf → eindeutiger Fehler (nicht endloses Retry)
  - GET-Anfrage mit CancellationToken und Abbruchmarke funktoniert weiterhin

---

## Implementierungsansatz

### A4 — Fehlerantworten

1. **Exception-Mapping in `ItemsController`:**
   - `EnsureAccessAsync` → neue Exception-Klasse `ForbiddenAccessException` einführen oder `PlaylistAccessDeniedException` wiederverwenden
   - In `ExecuteAsync` (falls vorhanden) oder am Endpunkt-Ende: `ForbiddenAccessException` → `Forbid("Sie haben keine Berechtigung für diesen Inhalt")`
   - `RecordNotFoundException` → `NotFound("Dieser Inhalt existiert nicht oder hat keine Videodatei")`

2. **Client-seitige Retry-Logik:**
   - `HandleUnauthorized()` in `VideoWebPlayerClient` überschreiben
   - Nur `401` → Refresh + Retry; `403`/`404` → `false`

3. **UI-Integration:**
   - Playlist-Detailseite auf `ForbiddenResult` (403) prüfen → Meldung anzeigen
   - `NotFoundResult` (404) → andere Meldung anzeigen
   - Kein automatischer Retry/Neuanmeldungs-Dialog

### A5 — Client-Bibliothek

1. **DeviceToken-Verwaltung:**
   - Neue Instanzvariable `string DeviceToken { get; set; }` in `VideoWebPlayerClient`
   - Bei der Initialisierung pro Circuit/Instanz setzen (nicht statisch global)
   - Beim Pairing-Exchange oder Bootstrap automatisch speichern

2. **Neue Aufrufe implementieren:**
   - `PairingExchangeAsync` → `POST /api/pairing/exchange` mit `X-API-Key`
   - `PairingBootstrapAsync` → `POST /api/pairing/bootstrap` ohne Token (verschlüsselt)
   - `RefreshAsync` → `POST /api/auth/refresh` mit `X-API-Key` + aktuellem Refresh-Token
   - `LogoutAsync` → `POST /api/auth/logout` mit Bearer + optional `X-API-Key`

3. **Token-Rotation in `HandleUnauthorized()`:**
   - Eingabe: HTTP 401 auf beliebiger Anfrage
   - `RefreshAsync()` aufrufen
   - Falls erfolgreich → Original-Anfrage mit neuem Bearer-Token wiederholen (exponentieller Backoff, max. 1 Retry)
   - Falls `RefreshAsync` → `401` oder andere finale Fehler → `InvalidOperationException` werfen mit aussagekräftigem Text

4. **HTTP-Header bei allen Aufrufen:**
   - Wenn `DeviceToken` gesetzt: Header `X-API-Key: <DeviceToken>` hinzufügen
   - Bestehende Bearer-Token-Header bleiben unverändert

5. **Playlist-Navigationsmethoden absichern:**
   - `PostForOptionalPlaylistNavigationResultAsync` in `InternalVideoWebPlayerClient` überschreiben → `SendWithReauthorizationAsync` nutzen
   - `HttpPutAsync` / `HttpPatchAsync` / `HttpDeleteAsync` → entsprechend in `InternalVideoWebPlayerClient` überschreiben

---

## Konfiguration

**A4:**
- Keine Konfiguration erforderlich. Verhalten ist hart codiert.

**A5:**
- **Client-Bibliothek (`VideoWebPlayer.Client`):**
  - `DeviceToken`-Setter öffentlich (wird von der aufrufenden App gesetzt, z. B. nach Bootstrap)
  - Refresh-Retry: max. 1 Wiederholung (hart codiert, nicht konfigurierbar)
  - Exponentieller Backoff zwischen Retry: Standard 100 ms + jitter (hart codiert)
  
- **Server-seitige Limits (bereits vorhanden, keine Änderung):**
  - `Pairing:BootstrapTicketTtlMinutes` (Bootstrap-Ticket-Ablauf)
  - `Auth:RefreshTokenTtlDays` (Refresh-Token-Lebensdauer)
  - `Pairing:BootstrapAdminOnly` (Self-Service-Kopplung per QR-Code Zugriff)

---

## Akzeptanzkriterien

### A4 — Verständliche Fehlerantworten der Medien-API

- [ ] Abruf von Details, Videostrom und Download eines nicht freigeschalteten Titels durch einen angemeldeten Anwender ergibt „kein Zugriff" (403).
- [ ] Abruf mit unbekannter Kennung ergibt „nicht gefunden" (404).
- [ ] Abruf eines Titels ohne hinterlegte Videodatei ergibt „nicht gefunden" (404).
- [ ] Abruf ohne Anmeldenachweis ergibt weiterhin „nicht angemeldet" (401).
- [ ] Ein Anwender, der eine öffentliche Playlist eines anderen öffnet und einen für ihn gesperrten Titel anklickt, bekommt eine verständliche Meldung; es wird keine Neuanmeldung ausgelöst.
- [ ] `docs/API.md` und die Verhaltensbeschreibung stimmen überein.
- [ ] Bestehende Tests, die heute 401/500 erwarten, sind mitgezogen; neue Tests sichern jeden Fall ab.

### A5 — Client-Bibliothek um Gerätekopplung, QR-Bootstrap und Sitzungserneuerung ergänzen

- [ ] Ein Testdurchlauf koppelt ein Gerät über den QR-Bootstrap, ruft anschließend Playlists ab, startet eine Wiedergabe und meldet Fortschritt — alles über die Bibliothek.
- [ ] Läuft die Sitzung zwischen zwei Playlist-Aufrufen ab, wird sie selbsttätig erneuert und der Aufruf erfolgreich wiederholt; das gilt auch für „nächster Titel", „vorheriger Titel", „automatisch weiterschalten", für das Umbenennen, Löschen, Umsortieren und die Cover-Aufrufe.
- [ ] Nach Widerruf des Geräts meldet die Bibliothek beim nächsten Erneuerungsversuch einen eindeutigen Fehler.
- [ ] Die Aufrufe der Bibliothek, die heute an der Sitzungsabsicherung vorbeilaufen, sind einbezogen (siehe technische Notiz: Aufrufe mit PUT/PATCH/DELETE, Abrufe mit Abbruchmarke und die drei Wiedergabe-Navigationsaufrufe).
- [ ] Die Weboberfläche verhält sich unverändert.

---

## Betroffene Bereiche

- **A4:** `ItemsController`, `VideoWebPlayerClient` (HandleUnauthorized), Playlist-Detailseite, `docs/API.md`, Test-Suite
- **A5:** `VideoWebPlayer.Client/VideoWebPlayerClient`, `InternalVideoWebPlayerClient`, Sitzungsverwaltung, Playlist-Aufrufe, Test-Suite

---

## Abhängigkeiten und Hinweise

- **Abhängigkeit A4 → A5:** Die neuen Statuscodes aus A4 dürfen nicht die Token-Refresh-Logik in A5 auslösen. Nur `401` = retryable; `403`/`404` = final.
- **Server-Endpunkte:** Keine Änderung außer den Statuscodes in A4. A5 nutzt bestehende Endpunkte.
- **InternalVideoWebPlayerClient:** Nur Server-Blazor (Circuit-scoped). Die MAUI/TV-App liegt in anderem Repo.
- **Geräte-Token:** Instance-Variable je Client, nicht statisch.
- **Bereits vorhanden:** DTOs für Pairing/Bootstrap/Refresh, `PairingBootstrapPayload`-Struktur.

---

## Offene Fragen

Keine. Alle Entscheidungen und Hintergrund-Details sind im Analysebericht Abschnitte 4–5 und im Issue #233 dokumentiert.

---

## Verbindliche Quellen (Ergänzung)

Verbindlich für Ziel, Akzeptanzkriterien und Abgrenzung sind zusätzlich:

- GitHub-Issue #233 (`gh issue view 233`), Entwürfe A4 und A5.
- Analysebericht `docs/projects/task/issue-207-fd729906880143ee8539fa3e046c88f4-playlists-fuer-serien-staffeln/analyse-staging-und-playlists.md`, Abschnitt 4 (Befunde F3, F4, F5 mit Belegen und Fundstellen) und Abschnitt 5 (Entwürfe A4, A5).
- Umfang dieses Laufs: nur A4 und A5. A6 bis A9 sind nicht Teil.
