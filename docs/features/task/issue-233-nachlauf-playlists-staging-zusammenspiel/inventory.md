# Bestandsaufnahme: Nachlauf Playlists × Staging-Änderungen (Issue #233)

Diese Bestandsaufnahme analysiert den bestehenden Code bezogen auf die neun Anforderungen A1–A9 aus GitHub-Issue #233. Der Schwerpunkt liegt auf Backup-Restore, API-Dokumentation, Release Notes, Medien-API-Fehlerbehandlung, Client-Bibliothek, E2E-Tests und Dokumentation.

## Zusammenfassung

### Wesentliche Befunde

- **A1 (Backup-Restore / Alt-Backups):** Die drei neuen Tabellen `PairedDevices`, `PairingCodes`, `RefreshTokens` sind **nicht** in `OptionalRestoreTables` eingetragen (VideoWebPlayerBackupData.cs Zeile 23-36), obwohl sie EF-Entitäten sind. Alt-Backups ohne diese Tabellen werden beim Restore abgelehnt.

- **A2 (API-Dokumentation):** 17 Playlist-Endpunkte fehlen in `docs/API.md`, insbesondere alle zur Wiedergabe (play, play/next, play/previous, play/advance) und zum Cover-Management (upload, regenerate, preview, delete). Auch die Session-Endpunkte (`POST /api/pairing/bootstrap`, `POST /api/auth/refresh`, `POST /api/auth/logout`) fehlen. Der Vertragstest enthält keine Playlist-Routen.

- **A3 (Deutsche Release Notes):** Der deutsche Abschnitt der Release Notes fehlen sämtliche 8 Einträge zum QR-Bootstrap, die im englischen Teil vorhanden sind (Zeilen 38–50 in Englisch, Zeilen 98–105 in Deutsch). Kritisch fehlt auch der Hinweis auf die DB-Schema-Änderung und die vier neuen Config-Schlüssel.

- **A4 (Fehlerantworten Medien-API):** `ItemsController` gibt 401 statt 403 für fehlende Medienberechtigung zurück und 500 statt 404 für unbekannte IDs. `PlaylistsController` macht es richtig (ExecuteAsync mit Exception-Mapping).

- **A5 (Client-Bibliothek):** `VideoWebPlayerClient` hat DTOs für Pairing/Bootstrap/Refresh vorhanden, aber **keine** Methoden dafür. `HandleUnauthorized()` gibt nur `false` zurück, keine automatische Token-Erneuerung. `InternalVideoWebPlayerClient` überschreibt nur HttpGetAsync, nicht PUT/PATCH/DELETE und die Playlist-Navigation.

- **A6 (Tests: Gerät + Playlists):** Keine Kombinationstests von Pairing/Bootstrap mit Playlists. Bestehende Pairing-Helfer (`Helpers/PairingWebApplicationFactory`, `Helpers/PairingTestDb`) sind vorhanden, aber werden nicht mit Playlists kombiniert.

- **A7 (Tests: Lokale Verzeichnisse + Playlists):** Keine Kombinationstests von lokalen Medienquellen mit Playlists. Bestehende Helfer (`LocalMediaSourcePipelineE2ETests`, `PlaylistsE2ETestBase`) sind vorhanden, werden aber nicht kombiniert.

- **A8 (Geräte-Dokumentation):** `docs/help/geraete/*` ist auf Stand vor QR-Bootstrap: Bootstrap, Refresh-Tokens, Self-Service-Ablauf und Playlist-Bezug fehlen komplett.

- **A9 (Flackernde Tests):** `PlaylistsE2ETestBase.SelectSearchResultAsync` (Zeile 133-144) wartet nur 5 Sekunden auf Suchergebnis (Timeout=5000 bei Zeile 138), unter Last zu kurz. Methode verwendet feste `WaitForTimeoutAsync(1000)` statt auf Zustand zu warten.

### Test-Ausgangszustand

**Zeitpunkt (mit Zeitzone):** 2026-09-26, ca. 14:30 UTC+2  
**Branch und Commit-ID:** `task/issue-233-nachlauf-playlists-staging-zusammenspiel`, Commit b7db299 (`Playlists für Serien, Staffeln, Episoden, Filme und Filmsammlungen (#207) (#234)`)  
**Uncommittete Änderungen:** Nur `docs/features/task/customer-feedback.md` und `docs/features/task/issue-233-nachlauf-playlists-staging-zusammenspiel/` (neue Datei)  
**Testumgebung:** .NET 10.0.401, xUnit, Playwright  
**Testbefehl und Ergebnis:** Siehe [inventory/tests.md](inventory/tests.md)

---

## Details

- [Datenmodell und Konfiguration](inventory/models-und-config.md)
- [Logik und Services](inventory/logic-und-services.md)
- [API-Dokumentation und Fehlerbehandlung](inventory/api-dokumentation.md)
- [Client-Bibliothek](inventory/client-library.md)
- [Tests](inventory/tests.md)
