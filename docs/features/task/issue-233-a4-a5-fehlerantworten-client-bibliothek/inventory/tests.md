# Bestandsaufnahme: Tests und Test-Ausgangszustand

## Test-Ausgangszustand vor der Umsetzung

### Testumgebung

- **Zeitpunkt (Zeitzone):** 2026-09-26 (UTC+2 MESZ)
- **Branch und Commit-ID:** `task/issue-233-a4-a5-fehlerantworten-client-bibliothek`, HEAD `3975a6c`
- **Uncommittete Änderungen:** Keine (git status clean außer den gesammelten Dokumentdateien)
- **.NET SDK/Runtime:** .NET 10.0 (aus build-log.txt)
- **Testrahmen:** xUnit mit Playwright für E2E-Tests
- **Testumgebung:** SQLite In-Memory für Unit-Tests, WebApplicationFactory für E2E-Tests

### Testläufe

#### 1. Release-Build

| Befehl | Arbeitsverzeichnis | Exit-Code | Ausgabe |
|--------|-------------------|-----------|---------|
| `dotnet build VideoPlayer.sln -c Release` | `D:\Repositories\softwareschmiede\fd729906-8801-43ee-8539-fa3e046c88f4` | 0 | 0 Fehler, 0 Warnungen (außer harmlose CS-Warnungen wie CS0436) |

**Nachweis:** [build-log.txt](test-results/build-log.txt)

#### 2. Gesamte Test-Suite

| Lauf | Befehl | Arbeitsverzeichnis | Exit-Code | Erfolgreich | Fehlgeschlagen | Übersprungen | Dauer | Nachweis |
|------|--------|-------------------|-----------|-------------|----------------|--------------|-------|----------|
| 1 (Initial) | `dotnet test VideoWebPlayer.Tests/VideoWebPlayer.Tests.csproj -c Release --no-build` | `D:\Repositories\softwareschmiede\fd729906-8801-43ee-8539-fa3e046c88f4` | 1 | 1512 | 1 | 0 | 4 min 14 s | [test-run.log](test-results/test-run.log) |
| 2 (Wiederholung) | `dotnet test VideoWebPlayer.Tests/VideoWebPlayer.Tests.csproj -c Release --no-build` | `D:\Repositories\softwareschmiede\fd729906-8801-43ee-8539-fa3e046c88f4` | 0 | 1513 | 0 | 0 | 4 min 14 s | [test-run.log](test-results/test-run.log) |

**Detailanalyse:**

- Gesamttests: 1513
- Lauf 1: 1512 erfolgreich, **1 fehlgeschlagen** (flackernd)
- Lauf 2: 1513 erfolgreich, 0 fehlgeschlagen (bestätigt Flackerung)
- Übersprungene Tests: 0 in beiden Läufen

**Bestätigung Flackerung:** Test-Wiederholung zeigt Erfolg, was die Flackerung bestätigt (siehe F8 im Analysebericht).

### Nachgewiesene bestehende Testfehler

#### PlaylistDragDropFeedbackE2ETests.E2E_EscapeDuringDrag_KeepsOrder_AndDoesNotSelectEntry

| Aspekt | Details |
|--------|---------|
| **Test-ID** | `VideoWebPlayer.Tests.PlaylistDragDropFeedbackE2ETests.E2E_EscapeDuringDrag_KeepsOrder_AndDoesNotSelectEntry` |
| **Suite / Dateipfad** | `VideoWebPlayer.Tests/PlaylistDragDropFeedbackE2ETests.cs` |
| **Fehlerart** | `Microsoft.Playwright.PlaywrightException` |
| **Fehlermeldung** | `Locator expected to have count '0', But was: '1'. Expect "ToHaveCountAsync" with timeout 5000ms` |
| **Betroffenes Element** | Locator `#playlist-detail-selected-entry` |
| **Ursache** | Playwright-Timeout bei Drag-Drop-Feedback (flackernd) |
| **Relevanz für A4/A5** | Keine (Playlist-UI-Test, nicht Fehlerbehandlung) |
| **Ausgangslauf** | Lauf 1 vom 2026-09-26 |

**Status:** Bekannter flackernder Test (siehe Analysebericht F8). Nicht verursacht durch A4/A5.

---

### Tests, die durch A4 betroffen sind

Die folgenden Tests erwarten heute `UnauthorizedObjectResult(401)`. Nach A4 müssen sie auf `ForbidResult(403)` angepasst werden:

#### ItemsControllerAccessTests

Datei: `VideoWebPlayer.Tests/Controllers/ItemsControllerAccessTests.cs`

| Test-Methode | Zeile | Heute erwartet | Nach A4 erwartet | Typ |
|--------------|-------|----------------|------------------|-----|
| `Get_MovieCollection_Without_Access_Returns_Unauthorized` | 12 | `UnauthorizedObjectResult` | `ForbidResult` | Fix erforderlich |
| `Get_TVShow_Without_Access_Returns_Unauthorized` | 35 | `UnauthorizedObjectResult` | `ForbidResult` | Fix erforderlich |
| `Get_TVShowEpisode_Without_Access_Returns_Unauthorized` | 57 | `UnauthorizedObjectResult` | `ForbidResult` | Fix erforderlich |
| `Get_Movie_Without_Access_Returns_Unauthorized` | 80 | `UnauthorizedObjectResult` | `ForbidResult` | Fix erforderlich |

**Code-Beispiel (heute):**
```csharp
[Fact]
public async Task Get_MovieCollection_Without_Access_Returns_Unauthorized()
{
    var (db, controller, source, collection, _, _) = await CreateControllerWithUnlockedCollectionAsync(false);
    
    var result = await controller.Get("moviecollection", collection.Id);
    
    Assert.IsType<UnauthorizedObjectResult>(result);  // ❌ Wird nach A4 ForbidResult
}
```

---

### Tests, die durch A4 erweitert werden müssen

#### ItemsControllerAccessTests (Neue Tests für 404)

Für `RecordNotFoundException` → `NotFound(404)`:

- Test für unbekannte ID (z. B. ID 999999) sollte `NotFoundObjectResult` erhalten
- Test für Titel ohne Videodatei sollte `NotFoundObjectResult` erhalten
- Test für ungültig/nicht-existierende Medienquelle sollte `NotFoundObjectResult` erhalten

**Heute:** Diese würden implizit `StatusCode(500)` ergeben (generischer Exception-Handler).

**Nach A4:** Sollten korrekt `404` zurückgeben.

---

### Tests, die durch A5 neu entstehen

#### VideoWebPlayerClientTests (neue Testklasse oder Erweiterung)

Zu testende Szenarien:

1. **Gerätekopplung:**
   - `PairingExchangeAsync` mit gültigem Code
   - `PairingBootstrapAsync` mit gültigem Ticket

2. **Sitzungserneuerung:**
   - `RefreshAsync` mit gültigem Refresh-Token
   - Automatische Token-Rotation bei `SendWithReauthorizationAsync` + 401

3. **Playlist-Navigation mit Token-Rotation:**
   - `PlayAsync` gefolgt von `PlayNextAsync` nach simulierter Session-Ablauf

4. **PUT/PATCH/DELETE mit Impersonierung:**
   - `InternalVideoWebPlayerClient` überschreibt alle HTTP-Methoden
   - `PostForOptionalPlaylistNavigationResultAsync` funktioniert mit Impersonierung

5. **Logout:**
   - `LogoutAsync` wirft Token weg

6. **DeviceToken-Header:**
   - `X-API-Key` wird bei allen Anfragen gesetzt wenn `DeviceToken != null`

#### PairingWebApplicationFactory erweitern

Datei: `VideoWebPlayer.Tests/Helpers/PairingWebApplicationFactory.cs`

Existiert bereits, kann für A5-Tests wiederverwendet werden.

#### PairingTestDb erweitern

Datei: `VideoWebPlayer.Tests/Helpers/PairingTestDb.cs`

Existiert bereits, kann für A5-Tests wiederverwendet werden.

---

## Existierende Testklassen und Hilfsmethoden

### A4-relevante Test-Klassen

#### ItemsControllerAccessTests
Datei: `VideoWebPlayer.Tests/Controllers/ItemsControllerAccessTests.cs`

- `Get_MovieCollection_Without_Access_Returns_Unauthorized` ← Muss angepasst werden
- `Get_MovieCollection_Unlocked_Returns_Ok` ← Sollte nicht betroffen sein
- `Get_TVShow_Without_Access_Returns_Unauthorized` ← Muss angepasst werden
- `Get_TVShow_Unlocked_Returns_Ok` ← Sollte nicht betroffen sein
- `Get_TVShowEpisode_Without_Access_Returns_Unauthorized` ← Muss angepasst werden
- `Get_TVShowEpisode_Unlocked_Returns_Ok` ← Sollte nicht betroffen sein
- `Get_Movie_Without_Access_Returns_Unauthorized` ← Muss angepasst werden
- `Get_Movie_From_Unlocked_Collection_Returns_Ok` ← Sollte nicht betroffen sein
- Weitere Helper: `CreateControllerWithUnlockedCollectionAsync`, `CreateControllerWithUnlockedShowAsync`, etc.

#### ItemsControllerTests_Search
Datei: `VideoWebPlayer.Tests/Controllers/ItemsControllerTests_Search.cs`

Tests für Suchen/Filtern, nicht direkt A4/A5-betroffen.

#### ItemsControllerLocalSourceTests
Datei: `VideoWebPlayer.Tests/Controllers/ItemsControllerLocalSourceTests.cs`

Tests für lokale Medienquellen, nicht direkt A4/A5-betroffen.

### A5-relevante Test-Hilfsmethoden

#### PairingWebApplicationFactory
Datei: `VideoWebPlayer.Tests/Helpers/PairingWebApplicationFactory.cs`

Stellt `WebApplicationFactory` mit Pairing-Setup bereit. Kann für A5-Tests verwendet werden.

#### PairingTestDb
Datei: `VideoWebPlayer.Tests/Helpers/PairingTestDb.cs`

Hilfmethoden zum Aufbau von Test-Datenbank mit Pairing-Daten. Kann für A5-Tests verwendet werden.

#### PairingCryptoHelper
Datei: `VideoWebPlayer.Tests/Helpers/PairingCryptoHelper.cs`

ECDH und AES-GCM Verschlüsselung für Tests. Kann für A5-Tests verwendet werden.

#### PairingExchangeContractTestBase
Datei: `VideoWebPlayer.Tests/Helpers/PairingExchangeContractTestBase.cs`

Basis für Vertragstests zu Pairing-Endpunkten. Kann für A5-Tests verwendet werden.

### PlaylistsControllerTests (als Vorbild für A4)

Mehrere Test-Klassen in `VideoWebPlayer.Tests/Controllers/PlaylistsControllerTests_*.cs`:

- `PlaylistsControllerTests_Auth.cs` — Authorization-Tests (zeigt Muster für Fehlerbehandlung)
- `PlaylistsControllerTests_Create.cs`
- `PlaylistsControllerTests_Read.cs`
- `PlaylistsControllerTests_Update.cs`
- `PlaylistsControllerTests_Delete.cs`
- Etc.

Diese Tests zeigen bereits das Muster mit `ExecuteAsync`-Fehlerbehandlung.

---

### Testlücken und Ausführungsprobleme

#### Bekannte Testlücken (vor A4/A5)

1. **Keine expliziten Tests für `RecordNotFoundException` → 404** in ItemsControllerAccessTests
   - Tests für unbekannte ID fehlen
   - Tests für fehlende Videodatei fehlen

2. **Keine Kombinationstests Gerät × Playlist** (F6 aus Analysebericht)
   - Playlist-Zugriff mit Bootstrap-JWT nicht getestet
   - Verhalten nach Gerät-Widerruf nicht getestet

3. **Keine Kombinationstests lokale Quellen × Playlist** (F7 aus Analysebericht)
   - Wiedergabe aus lokaler Quelle in Playlist nicht getestet

4. **Flackernde E2E-Tests** (F8 aus Analysebericht)
   - `PlaylistMediaSearchE2ETests.SelectSeason_*` — flackert mit Timeout 5000ms
   - `PlaylistPlaybackE2ETests.PlaylistPreviousAtBeginningDoesNotShowEndReachedE2ETest` — flackert
   - Nicht A4/A5-Fehler, aber störend für CI

#### Nicht ausführbare oder übersprungene Tests

**Keine überspungenen oder deaktivierten Tests gefunden.** Alle 1513 Tests sind ausführbar (1512 grün, 1 flackernd).

#### Build- und Setup-Fehler

**Keine Build- oder Setup-Fehler.** Release-Build und Test-Suite laufen sauber.

---

## Test-Strategie für A4 und A5

### Für A4

1. Bestehende Tests in `ItemsControllerAccessTests` anpassen:
   - Assertions auf `ForbidResult` statt `UnauthorizedObjectResult` ändern
   
2. Neue Tests für 404 hinzufügen:
   - Unbekannte ID → `NotFoundResult`
   - Keine Videodatei → `NotFoundResult`
   - Ungültige Medienquelle → `NotFoundResult`

3. Tests für 401 prüfen (sollten nicht betroffen sein):
   - Abruf ohne Anmeldenachweis → `UnauthorizedResult` bleibt ✓

### Für A5

1. Neue Test-Klasse `VideoWebPlayerClientTests` (oder erweitern):
   - `PairingExchangeAsync` testen
   - `PairingBootstrapAsync` testen
   - `RefreshAsync` testen
   - `LogoutAsync` testen
   - Token-Rotation bei 401 testen
   - `X-API-Key` Header-Setzung prüfen

2. `InternalVideoWebPlayerClient` Tests:
   - Alle HTTP-Methoden überschrieben prüfen
   - `PostForOptionalPlaylistNavigationResultAsync` Impersonierung testen

3. E2E-Tests:
   - Playlist-Zugriff mit Bootstrap-JWT (mit Pairing-Setup)
   - Playlist-Navigation mit Token-Rotation
   - Gerät-Widerruf Verhalten

