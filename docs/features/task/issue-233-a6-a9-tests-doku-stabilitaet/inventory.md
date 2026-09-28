# Bestandsaufnahme: Automatische Tests, Dokumentation und Stabilisierung für Playlists (A6–A9)

Diese Bestandsaufnahme dokumentiert den tatsächlichen Zustand des Quellcodes und der Tests bezogen auf die Anforderungen A6 bis A9 des GitHub-Issues #233 und des Analyseberichts.

**Analysebericht:** [analyse-staging-und-playlists.md](../../../projects/task/issue-207-fd729906880143ee8539fa3e046c88f4-playlists-fuer-serien-staffeln/analyse-staging-und-playlists.md) (Abschnitte 4.6–4.9 mit Befunden F6–F9)

**Anforderung:** [requirement.md](requirement.md) (A6–A9)

---

## Zusammenfassung

### Was existiert bereits

- **Geräte-Pairing (Code-Exchange):** Vollständig implementiert mit Tests (`DevicePairingE2ETests`, `PairingServiceTests_*`)
- **QR-Bootstrap:** Vollständig implementiert (`PairingBootstrapE2ETests`, `PairingBootstrapEndpointsTests`)
- **Client-Bibliothek:** Alle Methoden vorhanden (`PairingExchangeAsync`, `PairingBootstrapAsync`, `RefreshAsync`, `LogoutAsync`, Playlist-Methoden)
- **Playlist-CRUD:** Vollständig über API und Client-Bibliothek
- **Playlist-Wiedergabe:** UI und API vorhanden
- **Playlist-Backfill:** Automatische Nachlieferung implementiert
- **Playlist-Cover:** Generierung und Upload vorhanden
- **Lokale Verzeichnisse:** Separate Tests für Scan, Klassifizierung, Streaming vorhanden
- **Testinfrastruktur:** `PairingWebApplicationFactory`, `DeviceClientTestBase`, `PlaylistsE2ETestBase` mit umfangreichen Hilfsmetho­den

### Was fehlt (Testlücken, zu implementieren durch A6–A9)

| Anforderung | Lücke | Gefunden in | Status |
|-------------|-------|------------|--------|
| **A6** | Kein E2E-Test kombiniert Gerät-Pairing mit Playlists | Befund F6 | ✗ Fehlt |
| **A7** | Kein E2E-Test kombiniert lokale Verzeichnisse mit Playlists | Befund F7 | ✗ Fehlt |
| **A8** | Gerätedokumentation vor Stand #232, fehlen QR-Bootstrap/Refresh/Self-Service/Playlist-Bezug | Befund F9 | ✗ Veraltet |
| **A9** | Zwei Browser-Tests flackern durch feste kurze Timeouts in `SelectSearchResultAsync` | Befund F8 | ⚠ Bekannt flaky |

### Test-Ausgangszustand

Siehe [inventory/tests.md](inventory/tests.md) für Details.

- **Lauf:** 28.09.2026, 10:05 UTC+02:00, Commit `7ebad05`
- **Testsuite:** `dotnet test VideoWebPlayer.Tests/VideoWebPlayer.Tests.csproj -c Release --no-build`
- **Ergebnis:** 1585 Tests, 0 Fehler, Dauer 4 m 14 s
- **Flackernde Tests:** `PlaylistMediaSearchE2ETests.SelectSeason_CascadesOnlyThatSeasonsEpisodes`, `PlaylistPlaybackE2ETests.PlaylistPreviousAtBeginningDoesNotShowEndReachedE2ETest` (dokumentiert, in diesem Lauf erfolgreich)

**Nachweis:** [inventory/test-results/baseline-run-2026-09-28.log](inventory/test-results/baseline-run-2026-09-28.log)

---

## Details

### [A6 — Automatische Prüfungen für Geräte × Playlists](inventory/tests.md)

**Testhilfsklassen vorhanden:**
- `PairingWebApplicationFactory` — WebApplicationFactory mit Pairing-Settings
- `PairingTestDb` — Testbenutzer und -daten
- `PairingCryptoHelper` — ECDH-Verschlüsselung/Dekryptierung
- `DeviceClientTestBase` — Komplette Basis für Geräte-Client-Tests mit Seed-Methoden:
  - `CreateUserAndPairDeviceAsync()` — Benutzer + Device Pairing
  - `SeedTwoAccessibleMoviesAsync()` — Test-Filme für Gerät
  - `CreatePlaylistWithTwoMoviesAsync()` — Playlist über Client-Bibliothek
  - `RevokeDeviceAsync()` — Gerät widerrufen

**Existierende Bootstrap/Pairing-Tests:**
- `PairingBootstrapE2ETests` — QR-Ticket-Flow (Browser)
- `PairingBootstrapEndpointsTests` — Bootstrap-API-Tests
- `DevicePairingE2ETests` — Device-Pairing-E2E

**Existierende Playlist-Tests:**
- `PlaylistsE2ETests`, `PlaylistDetailE2ETests`, `PlaylistEntriesE2ETests` u.v.m. — Playlist-CRUD und -UI

**Fehlende Kombinationstests:**
- Gerät über QR-Bootstrap → Playlist abrufen → Wiedergabe starten → Fortschritt melden mit Playlist-ID → Cover abrufen mit `?access_token=JWT` → Token erneuern → Gerät widerrufen (Refresh schlägt fehl, alte JWT noch gültig)
- Mehrbenutzer-Szenario: zwei Geräte, zwei Benutzer, Sichtbarkeit von öffentlichen/privaten Playlists

**Was A6 implementieren muss:**
- E2E-Tests, die alle Hilfsklassen verwenden
- Tests sollten die komplette Nutzerlaufbahn abdecken
- Tests zum Widerruf und Refresh-Token-Verhalten
- Tests zur Playlist-Sichtbarkeit und -Isolation zwischen Benutzern

### [A7 — Automatische Prüfungen für lokale Verzeichnisse × Playlists](inventory/tests.md)

**Testhilfsklassen vorhanden:**
- `LocalMediaSourcePipelineE2ETests` — echte Temp-Verzeichnisse mit Test-Dateien
- `PlaylistsE2ETestBase` — Playlist-Operationen über UI/API
- `DeviceClientTestBase.SeedTwoAccessibleMoviesAsync()` — generiert Test-Medien

**Existierende lokale Verzeichnis-Tests:**
- `LocalMediaSourcePipelineE2ETests` — kompletter Scan-/Klassifizierungs-Workflow
- `LocalMediaSourceStreamingE2ETests` — Streaming aus lokalen Dateien
- `MediaSourceLocalDirectoryE2ETests` — UI-Operationen
- `LocalMediaSourceReaderTests` — LocalMediaSourceReader-Unit-Tests
- `MediaSourceScannerLocalTests` — Scan-Logik

**Existierende Playlist-Tests:** siehe A6

**Fehlende Kombinationstests:**
- Lokale Medienquelle → Playlist erstellen → Einträge hinzufügen → abspielen → Cover generieren
- Backfill nach Scan: neue Dateien hinzufügen → Scan → Backfill-Marker prüfen → Einträge auto-hinzufügen
- Quellenlöschung: Medienquelle löschen → Weiterschauen-Einträge mit Playlist-Bezug durch nächsten Titel ersetzen

**Services dokumentiert:**
- [inventory/services.md](inventory/services.md) — `IMediaSourceReader`, `LocalMediaSourceReader`, `MediaSourceClassifier`, `PlaylistBackfillCoordinator` u.v.m.

**Modelle dokumentiert:**
- [inventory/models.md](inventory/models.md) — `MediaSource`, `LocalMediaSource`, `PlaylistBackfillMarker`, `ContinueWatchingEntry` (mit `PlaylistId`) u.v.m.

**Was A7 implementieren muss:**
- E2E-Tests für lokale Verzeichnisse + Playlists
- Tests sollten Backfill, Cover-Generierung und Quellenlöschung abdecken
- Verifizierung, dass Quellen-Dispatcher quelltyp-unabhängig arbeitet

### [A8 — Dokumentation aktualisieren](inventory/documentation-status.md)

**Aktueller Zustand:** Gerätedokumentation unter `docs/help/geraete/` ist veraltet (vor PR #232 mit QR-Bootstrap).

**Zu aktualisieren:**

| Datei | Lücken | Umfang |
|-------|--------|--------|
| `beschreibung.md` | QR-Bootstrap, Self-Service (Falschaussage korrigieren), Playlist-Zugriff | Mittel |
| `api.md` | Bootstrap-, Refresh-, Logout-Endpunkte; Scope-Aussagen | Klein |
| `datenmodell.md` | `RefreshTokens`-Tabelle, `PairingCodes`-Spalten, `ContinueWatchingEntries.PlaylistId` | Klein |
| `ablauf-technisch.md` | QR-Bootstrap-Flow, Token-Generierung, Rotation, Widerruf-Verhalten | Groß |
| `architektur.md` | Code-Exchange vs. Bootstrap, Token-Management, Self-Service | Mittel |
| `business-rules.md` | Widerruf + Playlist-Konsequenzen, Mehrbenutzer-Isolation | Klein |
| `playlists.md` / `playlists-api.md` | Geräte-Zugriff, Cover-Query-Auth | Klein |
| `index.md` | `client-bibliothek.md` verlinken | Trivial |

**Weiterführende Checks:**
- Link-Validierung aller Querverweise (mit `MarkdownLinkCheck`)
- Konsistenz zwischen Beschreibung und Code

**Was A8 implementieren muss:**
- Reine Dokumentation, kein Code-Change
- Alle oben genannten Datei-Abschnitte ergänzen/korrigieren
- Link-Validierung durchlaufen

### [A9 — Flackernde Tests stabilisieren](inventory/tests.md)

**Betroffene Tests:**
- `PlaylistMediaSearchE2ETests.SelectSeason_CascadesOnlyThatSeasonsEpisodes` — scheitert in ~25% der Läufe
- `PlaylistPlaybackE2ETests.PlaylistPreviousAtBeginningDoesNotShowEndReachedE2ETest` — scheitert in ~25% der Läufe

**Ursache:** Feste kurze Timeouts in `SelectSearchResultAsync` (Zeilen 133–144 in `PlaylistsE2ETestBase.cs`):
- Zeile 138: `await resultLocator.WaitForAsync(new LocatorWaitForOptions { Timeout = 5000 });` — 5s auf Suchergebnis (unter Last zu kurz)
- Zeile 143: `await Page.WaitForTimeoutAsync(1000);` — 1s feste Wartezeit

**Lösung (nach Anforderung):**
- `WaitForAsync()` mit Playwright-Standard-Timeout (30s) verwenden statt 5s
- `Page.WaitForTimeoutAsync(1000)` ersetzen durch Warten auf beobachtbaren Zustand:
  - `Locator.IsVisibleAsync()` für Sichtbarkeit
  - `Locator.IsEnabledAsync()` für Verfügbarkeit
  - `Page.WaitForLoadStateAsync()` für Netzwerk-Idle
  - Beispiel: statt 1s Delay auf `#playlist-entries-status` Sichtbarkeit warten

**Beteiligter Code:**
- `VideoWebPlayer.Tests/Helpers/PlaylistsE2ETestBase.cs` — Zeilen 133–144

**Was A9 implementieren muss:**
- Code-Change: `SelectSearchResultAsync` stabilisieren
- Tests: 20 aufeinanderfolgende Durchläufe beider Tests ohne Fehler
- Keine Tests deaktivieren oder abschwächen

---

## Detaildokumente

- [inventory/tests.md](inventory/tests.md) — Testausgangszustand, bestehende Testklassen und Hilfsmethoden
- [inventory/models.md](inventory/models.md) — Datenmodelle für Geräte, Playlists, lokale Quellen
- [inventory/services.md](inventory/services.md) — Services und Controller für Pairing, Bootstrap, Playlists, Backfill
- [inventory/documentation-status.md](inventory/documentation-status.md) — Detaillierte Analyse der Dokumentationslücken für A8
- [inventory/test-results/baseline-run-2026-09-28.log](inventory/test-results/baseline-run-2026-09-28.log) — Ausgangslauf aller 1585 Tests

---

## Abhängigkeiten und Vorbedingungen

- **A4** (Fehlerantworten 403/404) — ✓ bereits implementiert (PR #235)
- **A5** (Client-Bibliothek mit Pairing, Bootstrap, Refresh) — ✓ bereits implementiert (PR #236)
- **Testinfrastruktur** — ✓ vollständig vorhanden
- **Lokale Verzeichnisse** — ✓ als Medienquelle implementiert (PR #225)
- **Geräte-Pairing** — ✓ implementiert (PR #229)
- **QR-Bootstrap** — ✓ implementiert (PR #232)

---

## Bekannte Fehler und Befunde

Aus dem Analysebericht dokumentiert (für Bestandsaufnahme relevant):

| Befund | Auswirkung | Status |
|--------|-----------|--------|
| **F1** (Alt-Backups nicht wiederherstellbar) | Nicht Teil A6–A9; vor Push beheben | ✗ Offen |
| **F2** (API-Dokumentation unvollständig) | Nicht Teil A6–A9; Playlist-Endpunkte fehlen in `docs/API.md` | ✗ Offen |
| **F3** (401 statt 403 bei Zugriffsfehler) | Nicht Fokus A6–A9; betrifft ItemsController | ⚠ Dokumentiert |
| **F4** (Client-Bibliothek mit Pairing) | ✓ beheben durch A5 | ✓ Gelöst |
| **F5** (InternalVideoWebPlayerClient unvollständig) | Latent, nicht heute reproduzierbar | ⚠ Dokumentiert |
| **F6** (Geräte × Playlists Testkombination) | **Zu beheben durch A6** | ✗ Fehlt |
| **F7** (Lokale Verzeichnisse × Playlists Testkombination) | **Zu beheben durch A7** | ✗ Fehlt |
| **F8** (Flackernde Browser-Tests) | **Zu beheben durch A9** | ⚠ Bekannt |
| **F9** (Gerätedokumentation veraltet) | **Zu beheben durch A8** | ✗ Veraltet |

---

## Nächste Schritte

### Für A6
1. Neue Test-Klasse erstellen oder in bestehende E2E-Test-Suite integrieren
2. `DeviceClientTestBase` als Basis verwenden
3. Folgende Szenarien abdecken:
   - Bootstrap-Ticket erzeugen → Geräte-JWT erhalten → Playlists abrufen
   - Playlist-Wiedergabe starten → Fortschritt mit `playlistId` melden
   - Cover mit `?access_token=JWT` abrufen
   - Geräte-Widerruf: alte JWT gültig, Refresh ungültig
   - Zweites Gerät, zweiter Benutzer: Isolation testen

### Für A7
1. Neue Test-Klasse erstellen oder in bestehende E2E-Test-Suite integrieren
2. `LocalMediaSourcePipelineE2ETests` + `PlaylistsE2ETestBase` kombinieren
3. Folgende Szenarien abdecken:
   - Lokale Medienquelle → Playlist mit Einträgen → Abspielen
   - Backfill nach Scan: neue Dateien → Einträge auto-hinzufügen
   - Cover-Generierung aus lokalen Mediabildern
   - Quellenlöschung: Weiterschauen mit Playlist-Bezug ersetzen

### Für A8
1. Jede Dokumentations-Datei aktualisieren (siehe [documentation-status.md](inventory/documentation-status.md))
2. Querverweise prüfen und neu verlinken
3. Link-Validierung durchlaufen (`MarkdownLinkCheck`)

### Für A9
1. `PlaylistsE2ETestBase.SelectSearchResultAsync()` stabilisieren
2. Feste Timeouts durch Warten auf Zustände ersetzen
3. 20 aufeinanderfolgende Durchläufe testen
4. Full-Suite 3× hintereinander laufen lassen

