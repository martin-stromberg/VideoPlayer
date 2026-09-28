# Tests und Testinfrastruktur — Bestandsaufnahme A6–A9

## Test-Ausgangszustand vor der Umsetzung

- **Zeitpunkt (mit Zeitzone):** 28.09.2026, 10:05 UTC+02:00
- **Branch und Commit-ID:** `task/issue-233-a6-a9-tests-doku-stabilitaet`, Commit `7ebad05` (Nachlauf Playlists × Staging: Fehlerantworten der Medien-API, Client-Bibliothek (#233) (#236))
- **Uncommittete Änderungen im getesteten Stand:** 
  - Unverfolgte Datei: `docs/features/task/customer-feedback.md` (nicht berührt)
  - Unverfolgte Datei: `docs/features/task/issue-233-a6-a9-tests-doku-stabilitaet/` (zu erstellendes Verzeichnis, daher nicht relevant für Testausgangszustand)
- **Testumgebung und Runtime-/SDK-Versionen:** 
  - .NET SDK 10.0
  - Playwright (verfügbar, Headless Chrome)
  - SQLite (in-memory oder Temp-Datei pro Test)
  - Windows 11 Pro, PowerShell / Bash
- **Ermittelte Testsuiten und Quellen der Testbefehle:**
  - Haupttestsuite: `VideoWebPlayer.Tests/VideoWebPlayer.Tests.csproj`
  - Befehl: `dotnet test VideoWebPlayer.Tests/VideoWebPlayer.Tests.csproj -c Release --no-build`
  - Testlauf-Konfiguration: Release-Build, keine zusätzlichen Filter

### Testläufe

| Lauf | Befehl inkl. Filter | Arbeitsverzeichnis | Exit-Code | Erfolgreich | Fehlgeschlagen | Übersprungen | Nachweis |
|------|--------------------|--------------------|-----------|-------------|----------------|--------------|----------|
| 1 (Baseline) | `dotnet test VideoWebPlayer.Tests/VideoWebPlayer.Tests.csproj -c Release --no-build` | `D:\Repositories\softwareschmiede\fd729906-8801-43ee-8539-fa3e046c88f4` | 0 | 1585 | 0 | 0 | [Log](test-results/baseline-run-2026-09-28.log) |

**Zusammenfassung:** Gesamtdauer ca. 4 Minuten 14 Sekunden. Alle Tests erfolgreich, keine Fehler, keine übersprungenen Tests.

### Nachgewiesene bestehende Testfehler

Keine nachgewiesenen Testfehler im Ausgangslauf. Der Analysebericht dokumentiert zwei flackernde Playlist-E2E-Tests (`PlaylistMediaSearchE2ETests.SelectSeason_CascadesOnlyThatSeasonsEpisodes`, `PlaylistPlaybackE2ETests.PlaylistPreviousAtBeginningDoesNotShowEndReachedE2ETest`), die in etwa 25% der Durchläufe scheitern. Im Baseline-Lauf dieser Bestandsaufnahme sind beide Tests erfolgreich durchgelaufen. Das flackernde Verhalten ist dokumentiert; die Tests werden durch A9 stabilisiert.

### Testlücken und Ausführungsprobleme

Keine Infrastruktur-Fehler oder Ausführungshindernisse. Folgende Testlücken sind nach der Analyse dokumentiert:

1. **F6 (Kombination Geräte × Playlists):** Kein Test kombiniert Geräte-Pairing mit Playlist-Funktionen. Tests mit Pairing und Bootstrap existieren, Tests mit Playlists existieren, aber kein durchgängiger E2E-Test eines gekoppelten Geräts mit Playlist-Abruf, Wiedergabe und Fortschrittserfassung existiert. Zu beheben durch A6.

2. **F7 (Kombination lokale Verzeichnisse × Playlists):** Kein Test kombiniert lokale Verzeichnisse als Medienquelle mit Playlist-Funktionen. Tests mit lokalen Verzeichnissen existieren, Tests mit Playlists existieren, aber kein durchgängiger Test einer Playlist, deren Einträge aus lokalen Verzeichnissen stammen. Zu beheben durch A7.

3. **F8 (Flackernde Browser-Tests):** Die beiden Playlist-Search-E2E-Tests nutzen feste Timeouts in `SelectSearchResultAsync` und sind anfällig für Verzögerungen unter Last. Zu beheben durch A9 (Stabilisierung mit Playwright-State-Waits).

## Testklassen und Hilfsmethoden

### Testhilfsklassen für A6 (Geräte × Playlists)

#### `PairingWebApplicationFactory`
- **Datei:** `VideoWebPlayer.Tests/Helpers/PairingWebApplicationFactory.cs`
- **Zweck:** WebApplicationFactory mit voreingestellten Settings für Pairing-Tests (Jwt-Key, API-Token, Pairing-Settings)
- **Kern-Methoden:**
  - `Create(dbPath, builderAction)` — Erzeugt eine Factory mit Standard-Pairing-Konfiguration
  - `CreateTempDbPath(prefix)` — Erzeugt einen Temp-DB-Pfad mit eindeutigem Prefix

#### `PairingTestDb`
- **Datei:** `VideoWebPlayer.Tests/Helpers/PairingTestDb.cs`
- **Zweck:** Testbenutzer und Testdaten für Pairing-Szenarien
- **Kern-Methoden:** (Seed-Methoden für Benutzer, Geräte, Bootstrap-Tickets)

#### `PairingCryptoHelper`
- **Datei:** `VideoWebPlayer.Tests/Helpers/PairingCryptoHelper.cs`
- **Zweck:** ECDH-Verschlüsselung und Dekryptierung von Bootstrap-Payloads
- **Kern-Methoden:**
  - `DecryptToken()` — Dekryptiert Bootstrap-Payload mit clientseitigem Schlüssel

#### `DeviceClientTestBase`
- **Datei:** `VideoWebPlayer.Tests/Helpers/DeviceClientTestBase.cs`
- **Zweck:** Basis für Tests, die VideoWebPlayerClient als gekoppeltes Gerät gegen einen gehosteten Server fahren
- **Kern-Methoden:**
  - `CreateUserAsync(email, isAdmin)` — Erstellt Testbenutzer
  - `CreateBootstrapTicketAsync(userId)` — Erzeugt Bootstrap-Ticket
  - `PairDeviceAsync(ticket, deviceName)` — Führt kompletten QR-Bootstrap durch, wendet Token an
  - `CreateUserAndPairDeviceAsync(email, isAdmin)` — Kombiniert Benutzer-Erstellung + Device-Pairing
  - `SeedTwoAccessibleMoviesAsync(userId)` — Seed zwei Test-Filme mit Zugriff für Benutzer
  - `CreatePlaylistWithTwoMoviesAsync()` — Erstellt Playlist mit zwei Filmen via Client-Bibliothek
  - `RevokeDeviceAsync(deviceToken)` — Widerruft Gerät server-seitig
  - `ExpireSession()` — Invallidiert Bearer-Token für 401-Tests

### Testhilfsklassen für A7 (lokale Verzeichnisse × Playlists)

#### `LocalMediaSourcePipelineE2ETests`
- **Datei:** `VideoWebPlayer.Tests/LocalMediaSourcePipelineE2ETests.cs`
- **Zweck:** E2E-Tests für lokale Verzeichnisse als Medienquelle
- **Testmethoden:** (Verschiedene Szenarien: Scan, Klassifizierung, Filterung)
- **Wichtig:** Diese Klasse nutzt echte Temp-Verzeichnisse und echte Mediendateien (Videos/Bilder)

#### `PlaylistsE2ETestBase`
- **Datei:** `VideoWebPlayer.Tests/Helpers/PlaylistsE2ETestBase.cs`
- **Zweck:** Basis für Playlist-Browser-E2E-Tests mit Playwright
- **Kern-Methoden:**
  - `LoginAsync(email)` — Browser-Login
  - `SelectSearchResultAsync(searchTerm, mediaType, mediaId)` — **KRITISCH FÜR A9:** Live-Suche und Auswahl. Nutzt feste Timeouts (5s auf Suchergebnis, 1s Delay). Needs stabilization.
  - `ShowAddModeAsync()` — Schaltet in "Titel hinzufügen"-Modus
  - `ShowEntriesAsync()` — Schaltet in "Titel der Playlist"-Modus
  - `SelectEntryAsync()` — Wählt Eintrag aus
  - `PlayEntryFromHeaderAsync()` — Startet Wiedergabe vom Header
  - `CreatePlaylistViaUiAsync()` — Erstellt Playlist über UI
  - `SeedMovieAsync()`, `SeedTvShowWithSeasonsAsync()` — Seed Medien in DB
  - `SeedMoviesIntoPlaylistAsync()` — Seed Filme + Einträge in Playlist

### Existierende Pairing-/Bootstrap-Tests

#### `PairingBootstrapE2ETests`
- **Datei:** `VideoWebPlayer.Tests/PairingBootstrapE2ETests.cs`
- **Zweck:** Browser-Tests für QR-Bootstrap-Flow (Profile-Seite)
- **Testmethoden:**
  - `Profile_DevicesPage_CreateTicket_Bootstrap_Succeeds` — QR-Code generieren und Bootstrap durchführen

#### `DevicePairingE2ETests`
- **Datei:** `VideoWebPlayer.Tests/DevicePairingE2ETests.cs`
- **Zweck:** E2E-Tests für Geräte-Pairing (alt: Code-Exchange)

#### `PairingBootstrapEndpointsTests`
- **Datei:** `VideoWebPlayer.Tests/PairingBootstrapEndpointsTests.cs`
- **Zweck:** API-Tests für Bootstrap-Endpunkte
- **Kern-Tests:**
  - Bootstrap-Ticket-Erstellung, Gültigkeit, Ablauf, Höchstzahl pro Stunde
  - Bootstrap-Ticket-Einlösung mit ECDH-Verschlüsselung
  - Refresh-Token-Rotation
  - Geräte-Widerruf

### Existierende Playlist-Tests

#### `PlaylistMediaSearchE2ETests`
- **Datei:** `VideoWebPlayer.Tests/PlaylistMediaSearchE2ETests.cs`
- **Zweck:** Browser-E2E-Tests für Live-Suche in Playlist-UI
- **Testmethoden:** (u.a. `SelectSeason_CascadesOnlyThatSeasonsEpisodes` — **FLAKY, zu stabilisieren durch A9**)

#### `PlaylistPlaybackE2ETests`
- **Datei:** `VideoWebPlayer.Tests/PlaylistPlaybackE2ETests.cs`
- **Zweck:** Browser-E2E-Tests für Playlist-Wiedergabe
- **Testmethoden:** (u.a. `PlaylistPreviousAtBeginningDoesNotShowEndReachedE2ETest` — **FLAKY, zu stabilisieren durch A9**)

#### `PlaylistsE2ETests`
- **Datei:** `VideoWebPlayer.Tests/PlaylistsE2ETests.cs`
- **Zweck:** Allgemeine Playlist-Browser-E2E-Tests

#### `PlaylistDetailE2ETests`, `PlaylistEntriesE2ETests` u.a.
- **Datei:** Verschiedene Dateien mit Pattern `Playlist*E2ETests.cs`
- **Zweck:** Spezifische Playlist-UI-Tests (Detail, Einträge, Layout, Reorder, etc.)

### Existierende Unit-Tests für Playlist-Backfill und Cover

#### `PlaylistBackfillMarkerHookTests`
- **Datei:** `VideoWebPlayer.Tests/Services/PlaylistBackfillMarkerHookTests.cs`
- **Zweck:** Unit-Tests für automatische Backfill-Markerierung bei neuen Medieneintragen

#### `PlaylistCoverGeneratorTests`
- **Datei:** `VideoWebPlayer.Tests/Services/PlaylistCover/PlaylistCoverGeneratorTests.cs`
- **Zweck:** Unit-Tests für Playlist-Cover-Collage-Generierung

### Existierende Lokale-Verzeichnis-Tests

#### `MediaSourceLocalDirectoryE2ETests`
- **Datei:** `VideoWebPlayer.Tests/MediaSourceLocalDirectoryE2ETests.cs`
- **Zweck:** E2E-Tests für lokale Verzeichnisse (UI-Operationen)

#### `LocalMediaSourceStreamingE2ETests`
- **Datei:** `VideoWebPlayer.Tests/LocalMediaSourceStreamingE2ETests.cs`
- **Zweck:** E2E-Tests für Streaming aus lokalen Verzeichnissen

#### `LocalMediaSourcePipelineE2ETests`
- **Datei:** `VideoWebPlayer.Tests/LocalMediaSourcePipelineE2ETests.cs`
- **Zweck:** E2E-Tests für komplette lokale Verzeichnis-Pipeline

#### `LocalMediaSourceReaderTests` (Services)
- **Datei:** `VideoWebPlayer.Tests/Services/LocalMediaSourceReaderTests.cs`
- **Zweck:** Unit-Tests für LocalMediaSourceReader-Logik

#### `MediaSourceScannerLocalTests` (Services)
- **Datei:** `VideoWebPlayer.Tests/Services/MediaSourceScannerLocalTests.cs`
- **Zweck:** Unit-Tests für lokale Scan-Logik

## Zusammenfassung der Testabdeckung vor A6–A9

| Bereich | Abdeckung | Status | Notizen |
|---------|-----------|--------|---------|
| Geräte-Pairing (Code-Exchange) | Unit + E2E | ✓ Vollständig | `DevicePairingE2ETests`, Unit-Tests für PairingService |
| QR-Bootstrap | Unit + E2E | ✓ Vollständig | `PairingBootstrapE2ETests`, `PairingBootstrapEndpointsTests` |
| Refresh-Token-Rotation | Unit | ✓ Vorhanden | `RefreshTokenServiceTests` |
| Client-Bibliothek (Pairing) | Unit | ✓ Vorhanden | Tests für `VideoWebPlayerClient.Pairing.cs` |
| Client-Bibliothek (Playlists) | Unit | ✓ Vorhanden | Tests für `IPlaylistApiClient` |
| Playlist-CRUD | E2E + Unit | ✓ Vollständig | Mehrere E2E-Tests |
| Playlist-Wiedergabe | E2E | ✓ Vorhanden | `PlaylistPlaybackE2ETests` (teilweise flaky) |
| Playlist-Search | E2E | ⚠ Vorhanden aber flaky | `PlaylistMediaSearchE2ETests` (A9 fixiert) |
| Lokale Verzeichnisse | Unit + E2E | ✓ Vollständig | Separate Testsuite ohne Playlist-Kombination |
| **Geräte × Playlists** | Keine | ✗ Fehlt | **Zu implementieren durch A6** |
| **Lokale Verzeichnisse × Playlists** | Keine | ✗ Fehlt | **Zu implementieren durch A7** |

