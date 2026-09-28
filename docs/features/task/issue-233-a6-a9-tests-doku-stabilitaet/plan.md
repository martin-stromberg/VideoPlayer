# Umsetzungsplan: A6–A9 — Automatische Tests, Dokumentation und Stabilisierung für Playlists auf gekoppelten Geräten

**Anforderung:** [requirement.md](requirement.md)  
**Bestandsaufnahme:** [inventory.md](inventory.md)  
**Branch:** `task/issue-233-a6-a9-tests-doku-stabilitaet`  
**Issue:** GitHub #233

---

## Übersicht

Die Umsetzung besteht aus vier unabhängigen Arbeitsblöcken:

- **A9** (Testinfrastruktur-Stabilisierung, Grundlage): Flackernde Browser-Tests in `SelectSearchResultAsync` stabilisieren, indem feste Timeouts durch explizites Warten auf beobachtbare DOM-Zustände ersetzt werden.
- **A6** (reine E2E-Testarbeit): Automatisierte Tests für den vollständigen Ablauf eines gekoppelten Geräts mit Playlists schreiben (Bootstrap, Playlist-Abruf, Wiedergabe, Fortschritt, Token-Erneuerung, Widerruf, Mehrbenutzer-Isolation).
- **A7** (reine E2E-Testarbeit): Automatisierte Tests für Playlists mit lokalen Verzeichnissen als Medienquelle schreiben (Titel aus lokalem Verzeichnis, Backfill nach Scan, Cover-Collage, Quellenlöschung mit Playlist-Ersatz).
- **A8** (reine Dokumentation): Gerätedokumentation unter `docs/help/geraete/` auf den Stand von PR #232 (QR-Bootstrap) und #236 (Client-Bibliothek) aktualisieren; Playlist-Bezug hinzufügen; veraltete oder falsche Aussagen korrigieren.

**Umfang:** Reine Testarbeit und Dokumentation — **keine Änderungen am Produktivcode außer A9**. A9 ändert nur die Testhelfer-Methode `SelectSearchResultAsync` in `PlaylistsE2ETestBase.cs`.

**Abhängigkeiten:**
- A4 (Fehlerantworten 403/404) — bereits umgesetzt (PR #235) ✓
- A5 (Client-Bibliothek mit Pairing, Bootstrap, Refresh) — bereits umgesetzt (PR #236) ✓
- Existierende Testinfrastruktur (`PairingWebApplicationFactory`, `DeviceClientTestBase`, `PlaylistsE2ETestBase`) — bereits vorhanden ✓

---

## Designentscheidungen

Keine — alle Arbeitsblöcke folgen bestehenden Testmustern und Dokumentationskonventionen des Projekts.

**Korrektur des Orchestrators (gegen den echten Code geprüft):** Die Ablaufbeschreibungen in
„Programmabläufe" unten enthalten mehrere sachliche Fehler, die der Planer ohne Beleg im Code
angenommen hat. Verbindlich sind IMMER der tatsächliche Code und die im Plan zitierten Fundstellen der
Bestandsaufnahme, nicht die Ablauf-Prosa unten:
- Der Bootstrap-Ticket-Controller heißt `PairingController` (nicht `PairingBootstrapController`),
  `Route("api/[controller]")` → `api/pairing`, Endpunkte `exchange` und `bootstrap`.
- Der Standardwert für die Gültigkeit eines Bootstrap-Tickets ist **5 Minuten**
  (`PairingBootstrapService.DefaultTicketTtlMinutes = 5`, überschreibbar über
  `Pairing:BootstrapTicketTtlMinutes`), **nicht 15 Minuten**.
- Es gibt keine Klasse `PlaylistCoverService`; die Cover-Collage entsteht über
  `PlaylistCoverImageGenerator`/`PlaylistService` (in der Bestandsaufnahme nachschlagen).
- `ContinueWatchingController` hat die Route `api/continue-watching` mit dem Endpunkt `POST
  api/continue-watching/progress` (nicht `POST /api/continue-watching`).
- Vor dem Schreiben jedes Tests prüft der Implementierer jeden im Ablauf genannten Klassen-/Methodennamen,
  jede Route und jeden Zeit-/Zahlenwert gegen den tatsächlichen Code (Controller, Services, Konfiguration),
  nicht gegen die Ablaufbeschreibung unten. Weitere, hier nicht gefundene Abweichungen sind nicht
  ausgeschlossen.

---

## Programmabläufe

Die geplanten Tests decken folgende Benutzerabläufe ab. Die Tests selbst schreiben keinen Produktivcode, sondern verifizieren vorhandene Implementierung.

### A6: Geräte-Bootstrap und Playlist-Zugriff (E2E-Ablauf)

1. Benutzer wird erstellt oder existiert bereits
2. Server erzeugt Bootstrap-Ticket (15 Min. gültig)
3. Gerät erhält QR-Code mit verschlüsseltem Ticket
4. Gerät dekryptiert Ticket, sendet `POST /api/pairing/bootstrap` mit Ticket + ECDH-PublicKey
5. Server erzeugt JWT (12h Gültig, `sub` = Benutzer-ID) + Refresh-Token, verschlüsselt mit Client-Key
6. Gerät dekryptiert Response, speichert JWT + Refresh-Token
7. Gerät ruft `GET /api/playlists` mit JWT-Header ab → erhält Playlists des angemeldeten Benutzers
8. Gerät wählt Playlist, ruft `GET /api/playlists/{id}/entries` ab
9. Gerät startet Wiedergabe (ggf. über WebSocket oder HTTP), meldet Fortschritt via `POST /api/continue-watching` mit `playlistId`
10. Server speichert `ContinueWatchingEntry` mit `PlaylistId`
11. Benutzer sieht Fortschritt in "Weiterschauen"-Liste mit Playlist-Namen
12. Gerät ruft `GET /api/playlists/{id}/cover?access_token=JWT` ab (Query-Parameter-Auth statt Header)
13. Server antwortet mit Playlist-Cover-Bild
14. **Erneuerung:** Gerät sendet `POST /api/auth/refresh` mit Refresh-Token
15. Server erzeugt neues JWT + neuen Refresh-Token (Token-Rotation), invalidiert alten Refresh-Token
16. Gerät speichert neue Tokens
17. **Widerruf:** Benutzer/Admin widerruft Gerät server-seitig
18. Server markiert Gerät als `IsRevoked = true`, invalidiert Refresh-Token
19. Gerät versucht `POST /api/auth/refresh` → erhält 401 Unauthorized
20. Gerät versucht HTTP-Request mit altem JWT → läuft noch (bis zu 12h) bis JWT-Ablauf
21. **Mehrbenutzer-Isolation:** Zweites Gerät mit zweitem Benutzer (B) koppeln
22. Benutzer B's Gerät sieht nur B's Playlists + öffentliche Playlists
23. Benutzer B kann A's private Playlists nicht sehen, nicht ändern

Beteiligte Klassen/Komponenten (Produktion): `PairingBootstrapController`, `AuthController`, `PlaylistsController`, `ContinueWatchingController`, `PlaylistCoverService`, `RefreshTokenService`, `VideoWebPlayerClient` (aus A5)

Beteiligte Test-Klassen: `DeviceClientTestBase`, `PairingWebApplicationFactory`, `PairingTestDb`, neue E2E-Testklasse für A6

### A7: Playlist mit lokalen Verzeichnissen (E2E-Ablauf)

1. Benutzer wird erstellt oder existiert bereits
2. Test erzeugt echtes lokales Verzeichnis mit Test-Dateien (Videos, Bilder)
3. `MediaSource` wird angelegt mit `SourceType = LocalDirectory` und echtem Pfad
4. `MediaSourceScanner` scannt Verzeichnis, erkennt Titel (Videos) und Bilder
5. Neue Playlist wird erstellt
6. Test wählt Titel aus gescanntem Verzeichnis, fügt sie zur Playlist hinzu
7. Playlist-Wiedergabe wird gestartet → Streaming aus lokaler Datei funktioniert
8. Test verifies: Titel spielt aus lokaler Quelle ab (nicht SFTP)
9. **Backfill nach Scan:** Test erzeugt neue Videodatei im Verzeichnis
10. Test startet `MediaSourceScanner` erneut ("Neu erfassen")
11. `PlaylistBackfillMarker` wird gesetzt für Playlist
12. `PlaylistBackfillCoordinator` liefert neue Titel automatisch nach
13. Test verifies: Neue Titel sind in Playlist vorhanden
14. **Cover-Generierung:** Playlist-Cover wird aus Bildern lokaler Inhalte erzeugt (über `PlaylistCoverService`)
15. Test verifies: Cover-Collage existiert und referenziert lokale Bilder
16. **Quellenlöschung mit Ersatz:** Test erstellt `ContinueWatchingEntry` für einen Titel aus lokaler Quelle, mit `PlaylistId` gesetzt
17. Test löscht die lokale Medienquelle server-seitig
18. `ContinueWatchingService` ersetzt Einträge der gelöschten Quelle durch nächsten Titel derselben Playlist
19. Test verifies: Weiterschauen-Eintrag zeigt jetzt nächsten Titel statt des gelöschten

Beteiligte Klassen/Komponenten (Produktion): `LocalMediaSourceReader`, `MediaSourceScanner`, `MediaSourceReaderDispatcher`, `PlaylistBackfillCoordinator`, `PlaylistCoverService`, `ContinueWatchingService`

Beteiligte Test-Klassen: `LocalMediaSourcePipelineE2ETests`, `PlaylistsE2ETestBase`, neue E2E-Testklasse für A7

### A9: Stabilisierung von `SelectSearchResultAsync` (Testinfrastruktur-Änderung)

1. `SelectSearchResultAsync` wird aufgerufen mit Suchbegriff, Medientyp, Medien-ID
2. **Alt (fehlerhaft):** Locator wartet max. 5s auf Suchergebnis; bei Timeout Fehler; dann 1s feste Wartezeit auf Button-Click
3. **Neu (stabil):** Locator wartet max. 30s (Playwright-Standard) auf Suchergebnis-Element präsent
4. Nach Click wird nicht fest 1s gewartet, sondern explizit auf beobachtbaren Zustand gewartet:
   - Button `#playlist-mode-add-button` ist clickable/enabled, oder
   - Element `#playlist-entries-status` ist sichtbar (zeigt "Eintrag hinzugefügt"), oder
   - Page hat `loadstate: idle` erreicht
5. Test verifies: Beide bisherigen flackernden Tests laufen 20x hintereinander ohne Fehler
6. Full-Test-Suite läuft 3x hintereinander ohne Fehler

Beteiligte Test-Helfer: `PlaylistsE2ETestBase.SelectSearchResultAsync`

---

## Neue Klassen

Keine neuen Klassen erforderlich. Alle Tests werden in bestehenden oder neuen Test-Klassen (unter `VideoWebPlayer.Tests/`) geschrieben.

---

## Änderungen an bestehenden Klassen

### `VideoWebPlayer.Tests/Helpers/PlaylistsE2ETestBase.cs` (Testhelfer)

**Methode `SelectSearchResultAsync` (Zeilen ~133–144):**
- **Aktuell:** Nutzt feste Timeouts: `WaitForAsync(Timeout = 5000)` + `Task.Delay(1000)`
- **Änderung:** 
  - `WaitForAsync()` auf Playwright-Standard-Timeout (30s) setzen (oder weglassen, da 30s Default)
  - `Task.Delay(1000)` ersetzen durch eines der folgenden Patterns (tatsächlich wählt der Implementierer basierend auf DOM-Struktur):
    - `await resultElement.IsEnabledAsync()` — warte bis Button enabled
    - `await Page.Locator("#playlist-entries-status").IsVisibleAsync()` — warte bis Status-Element sichtbar
    - `await Page.WaitForLoadStateAsync("networkidle")` — warte bis Netzwerk idle
- **Keine anderen Änderungen an der Methode:** Funktionalität bleibt gleich, nur Timeouts stabilisiert
- **Rückgabewert und Parameter:** Unverändert

---

## Datenbankmigrationen

Keine. Alle verwendeten Datenmodelle (`MediaSource`, `LocalMediaSource`, `PairingCodes`, `RefreshTokens`, `ContinueWatchingEntry`, `PlaylistBackfillMarker`) sind bereits durch A4 und A5 implementiert.

---

## Validierungsregeln

Keine neuen Validierungsregeln erforderlich. Alle Tests nutzen bestehende Validierungen des Produktivcode.

---

## Konfigurationsänderungen

Keine neuen Konfigurationseinträge erforderlich. Tests nutzen Standard-Testkonfiguration (`PairingWebApplicationFactory` mit Default-Settings).

---

## Seiteneffekte und Risiken

- **Risk-1 (Testfehler offenbaren Produktfehlverhalten):** Wenn während der E2E-Tests in A6/A7 echtes Fehlverhalten der Anwendung entdeckt wird (z. B. Playlist-Sichtbarkeit funktioniert nicht), wird das als **Befund für Folgelauf** dokumentiert und **NICHT in diesem Lauf behoben**. Tests werden nicht abgeschwächt.
- **Risk-2 (Playwright-Standard-Timeout):** Nach Erhöhung der Timeout auf 30s könnten Tests länger laufen. Erwartet: vernachlässigbarer Impact, da Tests weiterhin schnell erfolgreich werden (unter Load die bisherigen 5s wären unzureichend).
- **Risk-3 (DOM-Selektor-Stabilität):** Bei Änderung der Selektoren in der UI (`#playlist-mode-add-button`, `#playlist-entries-status`) müssen die Tests angepasst werden. Das ist normal und Teil der Testwartung.

---

## Umsetzungsreihenfolge

Die Blöcke müssen in dieser Reihenfolge implementiert werden:

### Phase 1: Testinfrastruktur-Stabilisierung (A9)

1. **A9.1: `SelectSearchResultAsync` stabilisieren**
   - Voraussetzungen: `VideoWebPlayer.Tests/Helpers/PlaylistsE2ETestBase.cs` (bereits vorhanden)
   - Beschreibung:
     - Zeilen ~138 (WaitForAsync für Suchergebnis): Timeout von 5000ms auf Playwright-Standard (30s) erhöhen; alternativ weglassen, da Default
     - Zeile ~143 (Task.Delay(1000)): Durch explizites Warten auf beobachtbaren Zustand ersetzen:
       - Möglichkeit A: `await Page.Locator("#playlist-entries-status").IsVisibleAsync()` (Status-Element sichtbar)
       - Möglichkeit B: `await resultElement.IsEnabledAsync()` (wenn resultElement vor der Methode ermittelt)
       - Möglichkeit C: `await Page.WaitForLoadStateAsync("networkidle")` (Netzwerk-Idle)
       - **Auswahl:** Möglichkeit A (Status-Element) ist am explizitesten und vermittelt klar, welcher Zustand erwartet wird
     - Code: Keine andere Logik ändern

2. **A9.2: Stabilisierung verifizieren**
   - Voraussetzungen: A9.1 abgeschlossen
   - Beschreibung:
     - Test `PlaylistMediaSearchE2ETests.SelectSeason_CascadesOnlyThatSeasonsEpisodes` läuft 20x hintereinander (via Testrunner oder PowerShell-Loop)
     - Test `PlaylistPlaybackE2ETests.PlaylistPreviousAtBeginningDoesNotShowEndReachedE2ETest` läuft 20x hintereinander
     - Gesamte `VideoWebPlayer.Tests` Suite läuft 3x hintereinander: `dotnet test VideoWebPlayer.Tests/VideoWebPlayer.Tests.csproj -c Release --no-build`
     - Alle Durchläufe sind erfolgreich (0 Fehler)

### Phase 2: Automatische Tests für Geräte × Playlists (A6)

3. **A6.1: Test-Klasse für Geräte-Pairing und Playlists erstellen**
   - Voraussetzungen: 
     - A9 abgeschlossen (stabile Testinfrastruktur)
     - `VideoWebPlayer.Tests/Helpers/DeviceClientTestBase.cs` (vorhanden)
     - `VideoWebPlayer.Tests/Helpers/PairingWebApplicationFactory.cs` (vorhanden)
     - `VideoWebPlayer.Client` mit Methoden: `PairingBootstrapAsync`, `GetPlaylistsAsync`, `GetPlaylistEntriesAsync`, `CreatePlaylistAsync`, `AddPlaylistEntryAsync`, `RefreshAsync`, `LogoutAsync` (bereits aus A5)
   - Beschreibung:
     - Neue Test-Klasse: `VideoWebPlayer.Tests/PairingWithPlaylistsE2ETests.cs` (oder integriert in `DevicePairingE2ETests.cs`)
     - Basis: Erbt von `DeviceClientTestBase`
     - Konkrete Testmethoden (siehe A6.2–A6.6 unten)

4. **A6.2: Test — QR-Bootstrap und Playlist-Abruf**
   - Voraussetzungen: A6.1 (Klasse vorhanden)
   - Testname: `PairingBootstrap_GetPlaylistsAsDevice_ReturnsUserPlaylists`
   - Beschreibung:
     - Setup: `CreateUserAndPairDeviceAsync("user@example.com", isAdmin: false)`
     - Seed: `SeedTwoAccessibleMoviesAsync(userId)` + Playlist mit 2 Einträgen erstellen
     - Test:
       - Device-Client (mit JWT aus Bootstrap) ruft `GetPlaylistsAsync()` ab
       - Verifies: Rückgabe enthält die angelegte Playlist
       - Verifies: `PlaylistName` ist korrekt
     - Akzeptanzkriterium: ✓ „Ein Test koppelt ein Gerät per QR-Bootstrap und ruft danach die Playlists des Anwenders ab."

5. **A6.3: Test — Playlist-Wiedergabe und Fortschrittserfassung**
   - Voraussetzungen: A6.1 (Klasse vorhanden)
   - Testname: `PlaylistPlayback_WithDevice_ReportsProgressWithPlaylistId`
   - Beschreibung:
     - Setup: `CreateUserAndPairDeviceAsync()` + Playlist mit Einträgen via `CreatePlaylistWithTwoMoviesAsync()`
     - Test:
       - Device-Client startet Wiedergabe (ggf. über Playlist-Entry-ID)
       - Device-Client meldet Fortschritt: `await client.ReportPlaybackProgressAsync(playlistId: ..., entryId: ..., progress: ...)`
       - Server speichert `ContinueWatchingEntry` mit `PlaylistId`
       - Verifies: Eintrag in DB vorhanden mit `PlaylistId != null` und `PlaylistName` gesetzt
     - Akzeptanzkriterium: ✓ „Ein Test startet aus einer Playlist eine Wiedergabe, holt den nächsten Titel und meldet Fortschritt mit Playlist-Bezug."

6. **A6.4: Test — Titelbild mit Query-Parameter-Auth abrufen**
   - Voraussetzungen: A6.1 (Klasse vorhanden)
   - Testname: `PlaylistCover_WithDeviceQueryAuthToken_ReturnsImage`
   - Beschreibung:
     - Setup: Device gekoppelt, Playlist mit Cover vorhanden
     - Test:
       - Device-Client ruft `GET /api/playlists/{id}/cover?access_token=JWT` ab (manueller HTTP-Call oder Client-Methode, falls vorhanden)
       - Verifies: HTTP 200, Response ist valides Bild (JPEG/PNG)
     - Akzeptanzkriterium: ✓ „Ein Test ruft das Titelbild einer Playlist mit dem Anmeldenachweis als Abfrageparameter ab."

7. **A6.5: Test — Geräte-Widerruf blockiert Refresh, JWT läuft aber**
   - Voraussetzungen: A6.1 (Klasse vorhanden)
   - Testname: `DeviceRevoke_BlocksRefreshButKeepsOldJwtValid`
   - Beschreibung:
     - Setup: Device gekoppelt, JWT + Refresh-Token vorhanden
     - Test (Part 1):
       - `RevokeDeviceAsync(deviceId)` aufrufen (Server-Seite)
       - Device versucht `RefreshAsync()` (POST /api/auth/refresh mit Refresh-Token)
       - Verifies: HTTP 401 Unauthorized → Refresh blockiert
     - Test (Part 2):
       - Device versucht HTTP-Request mit altem JWT (z. B. GET /api/playlists mit Bearer JWT)
       - Verifies: HTTP 200 OK → JWT läuft noch (bis zu 12h)
       - (Optional: JWT-Ablauf simulieren und verify, dass es nach Ablauf fehlschlägt)
     - Akzeptanzkriterium: ✓ „Ein Test belegt, dass nach dem Widerruf des Geräts die Sitzungserneuerung abgelehnt wird, die bereits laufende Sitzung aber bis zu ihrem Ablauf weiterarbeitet."

8. **A6.6: Test — Mehrbenutzer-Isolation**
   - Voraussetzungen: A6.1 (Klasse vorhanden)
   - Testname: `MultiUser_DeviceSeesOnlyUserPlaylistsAndPublic`
   - Beschreibung:
     - Setup:
       - Benutzer A erstellen, Gerät A koppeln
       - Benutzer B erstellen, Gerät B koppeln
       - Playlist A erstellen (Benutzer A, privat) mit Eintrag
       - Playlist B erstellen (Benutzer B, privat) mit Eintrag
       - Playlist Public erstellen (öffentlich) mit Eintrag
     - Test (A's Sicht):
       - Device A ruft `GetPlaylistsAsync()` ab
       - Verifies: Enthält Playlist A + Public, nicht Playlist B
     - Test (B's Sicht):
       - Device B ruft `GetPlaylistsAsync()` ab
       - Verifies: Enthält Playlist B + Public, nicht Playlist A
     - Test (Modifikation-Block):
       - Device B versucht, Eintrag in Playlist A zu ändern/löschen (falls API das erlaubt zu versuchen)
       - Verifies: HTTP 403 Forbidden (falls Playlist nicht sichtbar) oder HTTP 401 (falls nicht berechtigt)
     - Akzeptanzkriterium: ✓ „Ein Test belegt, dass ein zweiter Anwender über ein eigenes Gerät nur seine eigenen und die öffentlichen Playlists sieht und fremde nicht ändern kann."

9. **A6.7: Regressionsschutz — alle bestehenden Playlist-Tests bleiben grün**
   - Voraussetzungen: A6.1–A6.6 abgeschlossen
   - Beschreibung:
     - Alle bestehenden Playlist-E2E-Tests (`PlaylistsE2ETests`, `PlaylistDetailE2ETests`, `PlaylistPlaybackE2ETests`, `PlaylistMediaSearchE2ETests` etc.) laufen unverändert durch
     - Verifies: Keine Regression durch neue Testklasse

### Phase 3: Automatische Tests für lokale Verzeichnisse × Playlists (A7)

10. **A7.1: Test-Klasse für lokale Verzeichnisse und Playlists erstellen**
    - Voraussetzungen:
      - A9 abgeschlossen (stabile Testinfrastruktur)
      - `VideoWebPlayer.Tests/LocalMediaSourcePipelineE2ETests.cs` (vorhanden)
      - `VideoWebPlayer.Tests/Helpers/PlaylistsE2ETestBase.cs` (vorhanden)
      - Lokale Verzeichnis-Unterstützung in `MediaSourceReaderDispatcher` (aus PR #225, vorhanden)
    - Beschreibung:
      - Neue Test-Klasse: `VideoWebPlayer.Tests/PlaylistsWithLocalMediaSourceE2ETests.cs` (oder integriert in `LocalMediaSourcePipelineE2ETests.cs`)
      - Basis: Kombiniert Pattern aus `LocalMediaSourcePipelineE2ETests` + `PlaylistsE2ETestBase`
      - Konkrete Testmethoden (siehe A7.2–A7.5 unten)

11. **A7.2: Test — Lokale Medienquelle mit Playlist-Aufbau und Wiedergabe**
    - Voraussetzungen: A7.1 (Klasse vorhanden)
    - Testname: `LocalMediaPlaylist_AddTitleFromLocalDir_PlaylistPlaysSuccessfully`
    - Beschreibung:
      - Setup:
        - Temp-Verzeichnis erstellen mit 1 Test-Video + 1 Test-Bild
        - Benutzer erstellen
        - `MediaSource` mit `SourceType = LocalDirectory` + echtem Temp-Pfad anlegen
        - `MediaSourceScanner` starten, Titel erkennen
      - Test:
        - Playlist erstellen
        - Titel aus Scanner-Ergebnis zur Playlist hinzufügen
        - Wiedergabe starten (API oder UI)
        - Verifies: Video spielt erfolgreich (kein Fehler, kein Fallback zu SFTP)
      - Akzeptanzkriterium: ✓ „Ein Test legt eine Medienquelle vom Typ ‚lokales Verzeichnis' mit echten Dateien an, fügt einen Titel daraus einer Playlist hinzu und spielt ihn aus der Playlist heraus ab."

12. **A7.3: Test — Backfill nach Scan**
    - Voraussetzungen: A7.1 (Klasse vorhanden)
    - Testname: `LocalMediaPlaylist_BackfillAfterRescan_AddsNewTitle`
    - Beschreibung:
      - Setup (aus A7.2):
        - Temp-Verzeichnis mit 1 Datei
        - Playlist mit 1 Eintrag
      - Test:
        - Neue Video-Datei zum Temp-Verzeichnis hinzufügen
        - `MediaSourceScanner.ScanAsync()` erneut aufrufen ("Neu erfassen")
        - `PlaylistBackfillMarker` wird gesetzt (Server-seitig)
        - `PlaylistBackfillCoordinator` führt Backfill durch (ggf. manuell auslösen oder warten)
        - Verifies: Neue Titel ist jetzt in Playlist vorhanden (DB-Abfrage oder API `GET /api/playlists/{id}/entries`)
      - Akzeptanzkriterium: ✓ „Ein Test belegt, dass ein neu hinzugekommener Titel in einem lokalen Verzeichnis nach dem ‚Neu erfassen' selbsttätig in eine Playlist nachgeliefert wird."

13. **A7.4: Test — Cover-Generierung aus lokalen Bildern**
    - Voraussetzungen: A7.1 (Klasse vorhanden)
    - Testname: `LocalMediaPlaylist_CoverGeneratedFromLocalImages`
    - Beschreibung:
      - Setup:
        - Temp-Verzeichnis mit Bild-Dateien (PNG/JPG)
        - Playlist mit Einträgen aus lokalen Dateien
      - Test:
        - `PlaylistCoverService.GenerateCoverAsync(playlistId)` aufrufen oder Trigger (z. B. Playlist-Edit)
        - Verifies: Cover-Bild wird erzeugt (Collage aus lokalen Bildern, nicht SFTP-Fallback)
        - Verifies: Cover ist abrufbar via `GET /api/playlists/{id}/cover`
      - Akzeptanzkriterium: ✓ „Ein Test belegt, dass das Titelbild einer Playlist aus Bildern lokaler Inhalte erzeugt wird."

14. **A7.5: Test — Quellenlöschung mit Weiterschauen-Ersatz**
    - Voraussetzungen: A7.1 (Klasse vorhanden)
    - Testname: `LocalMediaPlaylist_DeleteSourceReplacesPlaylistContinueWatching`
    - Beschreibung:
      - Setup:
        - Temp-Verzeichnis mit 2 Test-Videos (Video1, Video2)
        - Benutzer + `MediaSource` (lokal)
        - Playlist mit Video1 + Video2 hinzugefügt
        - `ContinueWatchingEntry` für Video1 erstellt (mit `PlaylistId` gesetzt)
      - Test:
        - `MediaSource` löschen (server-seitig)
        - Server-Logik sollte `ContinueWatchingEntry` für Video1 durch Video2 ersetzen (bei Löschung: nächster Titel derselben Playlist)
        - Verifies: Weiterschauen-Eintrag zeigt jetzt Video2 statt Video1
        - Verifies: `ContinueWatchingEntry.MediaId` = Video2.Id, `PlaylistId` noch gesetzt
      - Akzeptanzkriterium: ✓ „Ein Test belegt, dass beim Löschen einer lokalen Medienquelle ein Weiterschauen-Eintrag mit Playlist-Bezug durch den nächsten verfügbaren Titel derselben Playlist ersetzt wird."

15. **A7.6: Test — Testhilfen-Wiederverwendung**
    - Voraussetzungen: A7.1–A7.5 abgeschlossen
    - Beschreibung:
      - Alle Tests nutzen nur bestehende Hilfsklassen: `LocalMediaSourcePipelineE2ETests`-Pattern + `PlaylistsE2ETestBase`
      - Keine neuen Testhilfen implementiert
      - Verifies: Code-Review zeigt keine Duplikation
    - Akzeptanzkriterium: ✓ „Die vorhandenen Testhilfen für lokale Verzeichnisse und für Playlists werden wiederverwendet."

16. **A7.7: Regressionsschutz — alle bestehenden lokale-Verzeichnis-Tests bleiben grün**
    - Voraussetzungen: A7.1–A7.6 abgeschlossen
    - Beschreibung:
      - Alle bestehenden Tests unter `LocalMediaSourcePipelineE2ETests`, `LocalMediaSourceStreamingE2ETests` etc. laufen unverändert durch
      - Verifies: Keine Regression durch neue Testklasse

### Phase 4: Gerätedokumentation aktualisieren (A8)

17. **A8.1: `docs/help/geraete/beschreibung.md` aktualisieren**
    - Voraussetzungen: Keine (unabhängig von Code)
    - Beschreibung:
      - **Zu korrigieren:** Zeile ~30 „Die Verwaltung liegt im Einrichtungsbereich unter `Einrichtung` > `Geräte` und ist nur für Administratoren sichtbar"
        - **Neu:** Verwaltung im Admin-Bereich UND Self-Service über `Profil` > `Geräte` (abhängig von Setting `Pairing:BootstrapAdminOnly`, Standard: false)
      - **Zu ergänzen — QR-Bootstrap als Alternative zu Code-Exchange:**
        - Abschnitt: „Kopplung" mit zwei Subsections:
          1. Code-Exchange (alt, wiederverwendbar)
          2. QR-Bootstrap (neu, einmalig)
      - **Zu ergänzen — Sitzungs-Erneuerung:**
        - Abschnitt: „Sitzungs-Verwaltung": Refresh-Token-Rotation, Ablauf nach 12h, Widerruf sofort (Refresh blockiert, JWT läuft bis Ablauf)
      - **Zu ergänzen — Playlist-Zugriff:**
        - Abschnitt: „Geräte und Playlists": Gekoppeltes Gerät sieht Playlists des JWT-Benutzers, öffentliche Playlists sind sichtbar, Mehrbenutzer-Isolation
    - Akzeptanzkriterium A8: ✓ „Beschreibung … deckt den QR-Bootstrap, die Sitzungs-Erneuerung und das Abmelden ab."

18. **A8.2: `docs/help/geraete/api.md` aktualisieren**
    - Voraussetzungen: Keine
    - Beschreibung:
      - **Zu korrigieren:** Zeile ~5 „Das Feature exponiert einen **einzigen** öffentlichen Endpunkt" → **False seit PR #232**
        - **Neu:** Liste **5 öffentliche Endpunkte:**
          1. `POST /api/pairing/exchange` (Code-Exchange, alt)
          2. `POST /api/pairing/bootstrap` (QR-Bootstrap, neu)
          3. `POST /api/auth/refresh` (Sitzungs-Erneuerung)
          4. `POST /api/auth/logout` (Abmelden)
          5. Spielweise auf `GET /api/playlists/{id}/cover?access_token=JWT` (Query-Parameter-Auth, neu)
      - **Zu korrigieren:** Zeile ~8 „Im Scope `MauiOnly` (**derzeit nur** `POST /api/auth/login`)" → **False**
        - **Neu:** Scope `MauiOnly` enthält: `POST /api/auth/login`, `POST /api/auth/refresh`, `POST /api/auth/logout`, `POST /api/pairing/exchange`, `POST /api/pairing/bootstrap`
    - Akzeptanzkriterium A8: ✓ „Die Aussage ‚nur ein öffentlicher Endpunkt' … sind korrigiert."

19. **A8.3: `docs/help/geraete/datenmodell.md` aktualisieren**
    - Voraussetzungen: Keine
    - Beschreibung:
      - **Zu ergänzen — neue Spalten in `PairingCodes`:**
        - `Kind` (enum: `CodeExchange` vs. `Bootstrap`)
        - `TicketHash` (Hash des einmaligen Tickets, für Reuse-Detection)
      - **Zu ergänzen — neue Tabelle `RefreshTokens`:**
        - Spalten: `Id`, `PairedDeviceId` (FK), `TokenHash` (Hash des Tokens, nicht Plaintext), `JwtId` (Koppelung zum JWT-JTI), `ReusedTokenHash` (für Reuse-Detection), `IsRevoked`, `ExpiresAt`, `CreatedAt`, `RotatedAt`
        - Zweck: Tracking von Token-Rotation und Reuse-Detection
      - **Zu ergänzen — neue Spalte in `ContinueWatchingEntries`:**
        - `PlaylistId` (Nullable FK zu `Playlists`, für Wiedergabe mit Playlist-Kontext)
      - Alle Spalten mit Zweck erklären
    - Akzeptanzkriterium A8: (Teil von „decken QR-Bootstrap, Sitzungs-Erneuerung ab")

20. **A8.4: `docs/help/geraete/ablauf-technisch.md` aktualisieren**
    - Voraussetzungen: Keine
    - Beschreibung:
      - **Zu ergänzen — QR-Bootstrap-Flow (neu):**
        - 1. Benutzer erzeugt Ticket auf Profile-Seite → Server speichert `PairingCode` mit `Kind = Bootstrap`, Hash des Tickets (15 Min. gültig)
        - 2. QR-Code wird angezeigt (enthält verschlüsseltes Ticket + ECDH-PublicKey des Clients)
        - 3. Gerät dekryptiert QR → erhält Ticket + Client-Schlüssel
        - 4. Gerät sendet `POST /api/pairing/bootstrap` mit Ticket + ECDH-PublicKey
        - 5. Server validiert Ticket (Hash-Vergleich, Gültigkeitsdauer), erzeugt JWT + Refresh-Token, verschlüsselt mit Client-PublicKey
        - 6. Gerät empfängt verschlüsselte Response, dekryptiert mit eigenem PrivateKey
      - **Zu ergänzen — JWT-Generierung:**
        - `sub` Claim = Benutzer-ID (JWT-Benutzer, nicht Ticketernsteller)
        - `exp` Claim = now + 12h
        - Signing: Standard JWT-Schlüssel
      - **Zu ergänzen — Refresh-Token-Rotation:**
        - 1. Gerät sendet `POST /api/auth/refresh` mit Refresh-Token
        - 2. Server validiert Token (Hash-Vergleich, Reuse-Detection prüfen)
        - 3. Server erzeugt neues JWT + neuen Refresh-Token
        - 4. **Rotation:** Alter Refresh-Token wird invalidiert (via `RefreshTokens.RotatedAt` oder ähnlich)
        - 5. **Reuse-Detection:** Falls derselbe alte Token zweimal in kurzer Zeit kommt → Sicherheitsmaßnahme (ggf. alle Tokens invalidieren)
      - **Zu ergänzen — Geräte-Widerruf:**
        - 1. Admin/Benutzer ruft `DELETE /api/devices/{deviceId}` auf oder markiert als widerrufen
        - 2. Server setzt `PairedDevices.IsRevoked = true`, invalidiert alle Refresh-Tokens des Geräts
        - 3. Gerät versucht `POST /api/auth/refresh` → **HTTP 401 Unauthorized** (Refresh-Token ungültig)
        - 4. Gerät nutzt alten JWT weiter → **HTTP 200 OK** bis JWT-Ablauf (bis zu 12h)
        - 5. Nach JWT-Ablauf: Gerät kann nicht mehr auf API zugreifen
    - Akzeptanzkriterium A8: ✓ „Technischer Ablauf … deckt QR-Bootstrap, Token-Rotation, Widerruf ab."

21. **A8.5: `docs/help/geraete/architektur.md` aktualisieren**
    - Voraussetzungen: Keine
    - Beschreibung:
      - **Zu ergänzen — Code-Exchange vs. QR-Bootstrap:**
        - Code-Exchange: Wiederverwendbar (Pairing-Code wird mehrfach genützt, Gerät speichert Code lokal)
        - Bootstrap: Einmalig (Ticket gilt 15 Min., wird nach Einlösung ungültig)
        - Use Cases: Code-Exchange für Desktop-TV, Bootstrap für Mobile/Tablet (QR-Scan)
      - **Zu ergänzen — Refresh-Token-Management:**
        - Tokens werden als Hash gespeichert (Plaintext wird nie in DB)
        - Rotation bei jedem Refresh: alter Token → neuer Token
        - Reuse-Detection: doppelter Refresh desselben Tokens triggert Sicherheitsmaßnahme
      - **Zu ergänzen — Unterschied Geräteverwaltung vs. Pairing:**
        - **Geräte-Admin:** Wer darf Geräte widerrufen? (Admin-Bereich, evtl. Self-Service über Profil)
        - **Pairing:** Technischer Prozess (Ticket-Austausch, JWT-Generierung)
      - **Zu ergänzen — Self-Service-Kopplung:**
        - Setting `Pairing:BootstrapAdminOnly` (Standard: false)
        - Wenn false: Benutzer können über Profilseite Tickets erzeugen (koppeln sich selbst)
        - Wenn true: Nur Administratoren können Tickets erzeugen (Admin-Bereich)
    - Akzeptanzkriterium A8: ✓ „Architektur … deckt Code-Exchange vs. Bootstrap, Token-Management, Self-Service ab."

22. **A8.6: `docs/help/geraete/business-rules.md` aktualisieren**
    - Voraussetzungen: Keine
    - Beschreibung:
      - **Zu ergänzen — Widerruf und Playlist-Konsequenzen:**
        - Geräte-Widerruf: Refresh-Token sofort ungültig (401 on Refresh)
        - Alte JWTs: Bleiben bis zu 12h gültig (keine sofortige Invalidierung)
        - **Playlist-Folge:** Laufende Playlist-Wiedergabe läuft bis JWT-Ablauf weiter, neue Refresh-Versuche schlagen fehl
      - **Zu ergänzen — Mehrbenutzer-Isolation:**
        - Gekoppeltes Gerät sieht Playlists des JWT-Benutzers (nicht des Ticketerstellers)
        - öffentliche Playlists sichtbar auf allen Geräten
        - Benutzer-Wechsel: Neuer Benutzer loggt sich auf Gerät an → Gerät wechselt zu dessen Playlists
        - Fremdmodifikation: Ein Benutzer kann fremde private Playlists nicht ändern/löschen (403 bei Versuch)
    - Akzeptanzkriterium A8: ✓ „Business Rules … decken Widerruf + Playlist-Konsequenzen, Mehrbenutzer-Isolation ab."

23. **A8.7: `docs/help/playlists.md` (oder `playlists-api.md`) aktualisieren**
    - Voraussetzungen: Keine
    - Beschreibung:
      - **Zu ergänzen — Geräte-Zugriff auf Playlists:**
        - Gekoppeltes Gerät ruft `GET /api/playlists` auf → erhält Playlists des JWT-Benutzers + öffentliche
        - Benutzer-Wechsel: Neuer Benutzer-JWT → neue Playlists-Liste
      - **Zu ergänzen — öffentliche Playlists:**
        - öffentliche Playlists sind auf allen Geräten sichtbar (auch wenn Gerät von anderem Benutzer gekoppelt)
      - **Zu ergänzen — Playlist-Fortschritt mit `playlistId`:**
        - `ContinueWatchingEntry` speichert optional `PlaylistId` (FK)
        - Fortschritt wird mit Playlist-Kontext ermittelt/angezeigt
      - **Zu ergänzen — Cover-Query-Auth:**
        - `GET /api/playlists/{id}/cover?access_token=JWT` (Query-Parameter statt Header, für Bilder-Lazy-Load in Browser)
    - Akzeptanzkriterium A8: (Teil der Playlist-Dokumentation)

24. **A8.8: `docs/help/geraete/index.md` aktualisieren**
    - Voraussetzungen: Keine
    - Beschreibung:
      - **Zu ergänzen — Link zu `client-bibliothek.md`:**
        - Navigationsliste unter Index um Punkt „Client-Bibliothek" mit Link zu `docs/help/geraete/client-bibliothek.md` (aus A5, bereits vorhanden)
    - Akzeptanzkriterium A8: (Trivial, Teil der Link-Validierung)

25. **A8.9: Link-Validierung durchführen**
    - Voraussetzungen: A8.1–A8.8 abgeschlossen
    - Beschreibung:
      - Tool `MarkdownLinkCheck` (oder vergleichbar) nutzen, um alle `*.md` unter `docs/help/geraete/` auf gültige interne Links zu prüfen
      - Alle Querverweise müssen funktionieren:
        - Links innerhalb von `geraete/` (z. B. auf `beschreibung.md`)
        - Links zu anderen Dokumentationen (z. B. auf `../playlists.md`)
      - Verifies: Link-Check erfolgreich (0 broken Links)
    - Akzeptanzkriterium A8: ✓ „Alle Querverweise sind gültig (Link-Prüfung läuft durch)."

---

## Tests

### Neue Tests

#### A9 — Stabilisierung (Testinfrastruktur)

| Test / Hilfsmethode | Testklasse | Was wird geprüft / bereitgestellt? |
|---------------------|--------------|--------------------------------------|
| `SelectSearchResultAsync` (angepasst) | `PlaylistsE2ETestBase.cs` | Hilfsmethode wartet explizit auf beobachtbare DOM-Zustände statt feste Timeouts |
| (kein neuer Test nötig, bestehende Tests nutzen angepasste Hilfsmethode) | — | Verifizierung erfolgt über 20x Durchlauf der zwei flackernden Tests |

#### A6 — Geräte × Playlists (E2E)

| Test / Hilfsmethode | Testklasse | Was wird geprüft / bereitgestellt? |
|---------------------|--------------|--------------------------------------|
| `PairingBootstrap_GetPlaylistsAsDevice_ReturnsUserPlaylists` | `PairingWithPlaylistsE2ETests` | Device ruft Playlists ab nach QR-Bootstrap |
| `PlaylistPlayback_WithDevice_ReportsProgressWithPlaylistId` | `PairingWithPlaylistsE2ETests` | Device spielt Playlist ab und meldet Fortschritt mit PlaylistId |
| `PlaylistCover_WithDeviceQueryAuthToken_ReturnsImage` | `PairingWithPlaylistsE2ETests` | Device ruft Playlist-Cover mit Query-Parameter-Auth ab |
| `DeviceRevoke_BlocksRefreshButKeepsOldJwtValid` | `PairingWithPlaylistsE2ETests` | Nach Widerruf: Refresh blockiert, altes JWT läuft bis Ablauf |
| `MultiUser_DeviceSeesOnlyUserPlaylistsAndPublic` | `PairingWithPlaylistsE2ETests` | Mehrbenutzer-Isolation: Gerät B sieht nur B's Playlists + öffentliche |

#### A7 — Lokale Verzeichnisse × Playlists (E2E)

| Test / Hilfsmethode | Testklasse | Was wird geprüft / bereitgestellt? |
|---------------------|--------------|--------------------------------------|
| `LocalMediaPlaylist_AddTitleFromLocalDir_PlaylistPlaysSuccessfully` | `PlaylistsWithLocalMediaSourceE2ETests` | Titel aus lokalem Verzeichnis in Playlist abspielen |
| `LocalMediaPlaylist_BackfillAfterRescan_AddsNewTitle` | `PlaylistsWithLocalMediaSourceE2ETests` | Neue Titel nach Scan automatisch in Playlist |
| `LocalMediaPlaylist_CoverGeneratedFromLocalImages` | `PlaylistsWithLocalMediaSourceE2ETests` | Playlist-Cover aus lokalen Bildern generiert |
| `LocalMediaPlaylist_DeleteSourceReplacesPlaylistContinueWatching` | `PlaylistsWithLocalMediaSourceE2ETests` | Quellenlöschung ersetzt Weiterschauen durch nächsten Titel |

#### A8 — Dokumentation (keine Tests)

| Test / Hilfsmethode | Testklasse | Was wird geprüft / bereitgestellt? |
|---------------------|--------------|--------------------------------------|
| Link-Validierung (`MarkdownLinkCheck`) | — | Alle Links in `docs/help/geraete/*.md` sind gültig |

---

### Betroffene bestehende Tests

Keine. Die Änderung an `SelectSearchResultAsync` (A9) wird durch die 20x Durchlauf-Verifikation und die Full-Suite 3x Lauf bereits abgedeckt; keine anderen bestehenden Test-Signaturen ändern sich.

---

### E2E-Tests (primärer Funktionsnachweis)

Alle A6 und A7 Tests sind E2E-Tests (Playwright-basiert für A6 optional, aber API-basiert sicherlich erforderlich).

| Priorität | Szenario | Testdatei / Testklasse | Abgedecktes Akzeptanzkriterium | Warum E2E nötig ist |
|-----------|----------|------------------------|-------------------------------|-------------------|
| Pflicht | QR-Bootstrap → Playlist-Abruf | `PairingWithPlaylistsE2ETests.PairingBootstrap_GetPlaylistsAsDevice_ReturnsUserPlaylists` | A6 AK 1 | Benutzerfluss: Gerät koppeln → auf Playlists zugreifen. Unit-Tests auf Service-Ebene können nicht garantieren, dass HTTP-Client, Auth-Handler, Controller, DB alle zusammen arbeiten. |
| Pflicht | Playlist-Wiedergabe + Fortschritt melden | `PairingWithPlaylistsE2ETests.PlaylistPlayback_WithDevice_ReportsProgressWithPlaylistId` | A6 AK 2 | Benutzerfluss: Titel aus Playlist spielen → Fortschritt mit PlaylistId speichern. Benötigt Interaction zwischen Client, Playlist-Service, ContinueWatchingService, DB. |
| Pflicht | Titelbild mit Query-Parameter-Auth | `PairingWithPlaylistsE2ETests.PlaylistCover_WithDeviceQueryAuthToken_ReturnsImage` | A6 AK 3 | Benutzerfluss: Gerät ruft Bild mit Query-Parameter statt Header ab. HTTP-Semantik kritisch; Unit-Tests können Header-Alternative nicht vollständig verifizieren. |
| Pflicht | Widerruf blockiert Refresh | `PairingWithPlaylistsE2ETests.DeviceRevoke_BlocksRefreshButKeepsOldJwtValid` | A6 AK 4 | Benutzerfluss: Widerruf-Aktion → Refresh-Fehler, alte JWT noch gültig. Security-kritisch; muss End-to-End (DB-Markierung → AuthController → RefreshTokenService) funktionieren. |
| Pflicht | Mehrbenutzer-Isolation | `PairingWithPlaylistsE2ETests.MultiUser_DeviceSeesOnlyUserPlaylistsAndPublic` | A6 AK 5, 7 | Benutzerfluss: Zwei Benutzer, zwei Geräte → Sichtbarkeits- und Schreib-Isolation. Unit-Tests können nicht garantieren, dass QueryFilter + AuthZ-Check in echtem HTTP-Context arbeiten. |
| Pflicht | Lokale Quelle + Playlist | `PlaylistsWithLocalMediaSourceE2ETests.LocalMediaPlaylist_AddTitleFromLocalDir_PlaylistPlaysSuccessfully` | A7 AK 1 | Benutzerfluss: Lokale Datei hinzufügen → abspielen. Benötigt FileSystem-Zugriff, Scanner, Reader-Dispatcher, Playlist-Service, Streaming-Handler zusammen. |
| Pflicht | Backfill nach Scan | `PlaylistsWithLocalMediaSourceE2ETests.LocalMediaPlaylist_BackfillAfterRescan_AddsNewTitle` | A7 AK 2 | Benutzerfluss: Neue Datei → Scan → automatische Playlist-Nachlieferung. Koordination zwischen Scanner, Backfill-Service, PlaylistService nicht durch Unit-Tests allein nachweisbar. |
| Pflicht | Cover aus lokalen Bildern | `PlaylistsWithLocalMediaSourceE2ETests.LocalMediaPlaylist_CoverGeneratedFromLocalImages` | A7 AK 3 | Benutzerfluss: Cover-Generierung nutzt lokal Medien (nicht SFTP-Fallback). FileSystem + Image-Service + Playlist-Service-Integration. |
| Pflicht | Quellenlöschung + Ersatz | `PlaylistsWithLocalMediaSourceE2ETests.LocalMediaPlaylist_DeleteSourceReplacesPlaylistContinueWatching` | A7 AK 4 | Benutzerfluss: Quelle löschen → Weiterschauen mit Playlist-Bezug durch nächsten Titel ersetzen. Cascade-Logik zwischen MediaSourceService, ContinueWatchingService, PlaylistService. |

**Betroffene bestehende E2E-Tests:**

Keine. Die bestehenden Playlist-E2E-Tests (`PlaylistsE2ETests`, `PlaylistPlaybackE2ETests`, `PlaylistMediaSearchE2ETests` usw.) brauchen nicht angepasst werden. A9 ändert nur die Hilfsmethode `SelectSearchResultAsync` intern (Timeout-Verhalten), nicht ihre Signatur oder ihr Testverhalten.

---

## Offene Punkte

Keine. Der ursprüngliche Punkt zu A9 (welcher DOM-Zustand nach dem Klick auf ein Suchergebnis beobachtet
werden soll) ist durch Lesen des tatsächlichen Codes beantwortet, nicht offen:

`SelectSearchResultAsync` (`VideoWebPlayer.Tests/Helpers/PlaylistsE2ETestBase.cs`, aktuell Zeilen
133-144) wartet bereits auf einen echten Zustand — `await Page.WaitForSelectorAsync("#playlist-entries-status")`,
das `<div id="playlist-entries-status">` aus `PlaylistEntriesList.razor` (Zeile 29) — und hängt danach
zusätzlich eine feste Sekunde (`await Page.WaitForTimeoutAsync(1000)`) an. Das ist die eigentliche
Fehlerquelle, nicht ein fehlender Zustand: `entriesStatusMessage` wird gesetzt, **bevor**
`LoadInitialPageAsync()` die Liste neu lädt (`PlaylistEntriesList.razor`, `OnMediaSelectedAsync`, ca.
Zeile 546-548), sodass der Status-Text erscheinen kann, während die neue Kachel noch nicht im DOM steht
— die feste Sekunde sollte genau dieses Fenster überbrücken, tut das unter Last aber nicht zuverlässig.

**Entscheidung für A9:** Die feste Wartezeit `WaitForTimeoutAsync(1000)` am Ende von
`SelectSearchResultAsync` durch Warten auf die tatsächlich neu erschienene Playlist-Kachel ersetzen,
z. B. `await Page.Locator($".playlist-entry-row[data-media-type='{mediaType}'][data-media-id='{mediaId}']").WaitForAsync(...)`
(exaktes Selektor-/Attributmuster anhand von `PlaylistEntriesList.razor` verifizieren) — das ist der
wirklich gewünschte Endzustand ("Eintrag ist zur Playlist hinzugefügt und sichtbar"), nicht nur "der
Server hat geantwortet". Der bestehende Wait auf `#playlist-entries-status` bleibt als Zwischenschritt
erhalten (er schließt aus, dass ein Fehler-Status fälschlich als Erfolg gewertet wird). Sonderfall
Duplikat/Fehler: Wenn `entriesStatusIsError` gesetzt ist (Titel bereits vorhanden, kein Zugriff usw.),
erscheint keine neue Kachel — die Methode muss auch diesen Fall abdecken (z. B. auf Status-Text ODER
neue Kachel warten, je nach erwartetem Testausgang), das im Implementierungsschritt konkretisieren und
mit den bestehenden Aufrufern abgleichen, die einen Fehlerfall erwarten.

---

## Zusammenfassung

- **Umfang:** 4 Arbeitsblöcke (A9, A6, A7, A8), reine Testarbeit + Dokumentation, **keine Produktivcode-Änderung außer A9 (Testhelfer)**
- **Reihenfolge:** A9 → A6 + A7 → A8 (A6/A7 parallel möglich)
- **Neue Tests:** ~12 E2E-Tests insgesamt (5 für A6, 4 für A7, 3 Verifikationen für A9)
- **Neue Dokumentations-Abschnitte:** ~15 Abschnitte in 5 Dokumentations-Dateien (A8)
- **Keine Migrationen, Validierungen, Konfigurationsänderungen erforderlich**
- **Risiken:** Wenn beim Testen echtes Fehlverhalten der Anwendung entdeckt wird → Befund für Folgelauf, nicht in diesem Lauf beheben
