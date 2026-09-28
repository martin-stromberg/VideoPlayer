# A6–A9: Automatische Tests, Dokumentation und Stabilisierung für Playlists auf gekoppelten Geräten

**Issue:** GitHub #233  
**Branch:** task/issue-233-a6-a9-tests-doku-stabilitaet  
**Analyse:** docs/projects/task/issue-207-fd729906880143ee8539fa3e046c88f4-playlists-fuer-serien-staffeln/analyse-staging-und-playlists.md (Abschnitte 4.6–4.9, 5.6–5.9)

---

## A6 — Automatische Prüfungen für Playlists auf gekoppelten Geräten

**Priorität:** Mittel  
**Eigener Lifecycle-Lauf:** Ja

### Fachliche Zusammenfassung

Die Integration von Geräte-Pairing mit Playlist-Verwaltung soll durch automatisierte Tests belegt werden. Bisher sind diese beiden Funktionsbereiche isoliert getestet; es existiert kein Test, der einen kompletten Ablauf eines gekoppelten Geräts durchläuft: QR-Bootstrap, Playlist-Zugriff, Wiedergabe-Start, Fortschrittserfassung mit Playlist-Referenz, Token-Erneuerung und Geräte-Widerruf.

### Betroffene Klassen und Komponenten

- **Test-Klassen (neu):** Kombinierte E2E-Test-Suite für Geräte-Pairing × Playlists
- **Test-Hilfsklassen (wiederverwendet):** 
  - `Helpers/PairingWebApplicationFactory` (Geräte-Setup)
  - `Helpers/PairingTestDb` (Testdaten)
  - `Helpers/PairingCryptoHelper` (Bootstrap-Ticket-Verarbeitung)
  - `Helpers/PlaylistsE2ETestBase` (Playlist-Operationen)
- **API-Client (wiederverwendet):** `VideoWebPlayer.Client` mit implementierten Methoden:
  - `PairingBootstrapAsync`
  - `RefreshAsync`
  - `LogoutAsync`
  - Setter für `X-API-Key`-Header (Geräte-Token)
- **Services (existierend):** 
  - `PairingService`, `RefreshTokenService`
  - `PlaylistService`, `ContinueWatchingService`
  - `PlaylistCoverService`
- **Controller (existierend):** `PlaylistsController`, `ContinueWatchingController`, `AuthController`, `PairingBootstrapController`

### Implementierungsansatz

1. **QR-Bootstrap-Test:** Test mit `PairingWebApplicationFactory` erzeugt Bootstrap-Ticket, löst es ein, extrahiert JWT + Refresh-Token aus Response
2. **Playlist-Abruf mit Geräte-JWT:** Abruf der Playlists des Anwenders mittels JWT (ohne Pairing-Code)
3. **Wiedergabe mit Fortschritt:** Startet Wiedergabe aus Playlist, ruft nächsten Titel ab, meldet Fortschritt via `ContinueWatchingEntry` mit `playlistId`
4. **Titelbild-Abruf:** Ruft Playlist-Cover mit Query-Parameter `?access_token=<JWT>` ab (Query-Parameter-Authentifizierung statt Header)
5. **Token-Erneuerung-Test:** Refresh-Token wird verwendet, um ein neues JWT zu erhalten; alte Sitzung läuft noch bis Ablauf (nicht sofort invalidiert)
6. **Geräte-Widerruf-Test:** Nach Widerruf schlägt Refresh mit bestehendem Refresh-Token fehl (401), aktive Sitzung mit altem JWT arbeitet aber noch bis zu 12h
7. **Mehrbenutzer-Isolation:** Zweites Gerät mit zweitem Benutzer: sieht nur eigene + öffentliche Playlists, kann fremde nicht ändern

Die Tests nutzen die bestehenden Testhilfen aus `Helpers/Pairing*` und `PlaylistsE2ETestBase`, statt neue zu bauen. Sie verwenden `VideoWebPlayer.Client`-Methoden (mit A5 implementiert), nicht manuelle HTTP-Aufrufe.

### Konfiguration

Keine zusätzliche Konfiguration erforderlich. Tests verwenden Standard-TestDB (`PairingTestDb`) und In-Memory-`WebApplicationFactory`.

### Akzeptanzkriterien

- [ ] Ein Test koppelt ein Gerät per QR-Bootstrap und ruft danach die Playlists des Anwenders ab.
- [ ] Ein Test startet aus einer Playlist eine Wiedergabe, holt den nächsten Titel und meldet Fortschritt mit Playlist-Bezug; der Eintrag erscheint anschließend in der Weiterschauen-Liste mit Playlist-Namen.
- [ ] Ein Test ruft das Titelbild einer Playlist mit dem Anmeldenachweis als Abfrageparameter ab.
- [ ] Ein Test belegt, dass nach dem Widerruf des Geräts die Sitzungserneuerung abgelehnt wird, die bereits laufende Sitzung aber bis zu ihrem Ablauf weiterarbeitet — so, wie es dokumentiert ist.
- [ ] Ein Test belegt, dass ein zweiter Anwender über ein eigenes Gerät nur seine eigenen und die öffentlichen Playlists sieht und fremde nicht ändern kann.
- [ ] Die vorhandenen Testhilfen für die Kopplung werden wiederverwendet, statt neue zu bauen.

---

## A7 — Automatische Prüfungen für Playlists mit lokalen Verzeichnissen als Medienquelle

**Priorität:** Mittel  
**Eigener Lifecycle-Lauf:** Ja

### Fachliche Zusammenfassung

Die Funktionsfähigkeit von Playlists mit lokalen Verzeichnissen (statt SFTP-Servern) als Medienquelle soll empirisch belegt werden. Bisher gibt es Testkomplexe für lokale Verzeichnisse und separate Testkomplexe für Playlists; diese sind nie kombiniert worden. Code-Analyse deutet darauf hin, dass die beteiligten Bausteine den Quelltyp abstrahieren, aber empirischer Beleg fehlt noch.

### Betroffene Klassen und Komponenten

- **Test-Klassen (neu):** Kombinierte E2E-Test-Suite für lokale Verzeichnisse × Playlists
- **Test-Hilfsklassen (wiederverwendet):**
  - `LocalMediaSourcePipelineE2ETests` (echte Verzeichnis-Aufbauten)
  - `PlaylistsE2ETestBase` (Playlist-Operationen)
- **Services (existierend):**
  - `MediaSourceReaderDispatcher` (abstrahiert Quelltyp)
  - `LocalMediaSourceReader`
  - `PlaylistService`, `PlaylistBackfillService`
  - `PlaylistCoverService`
  - `ContinueWatchingService`
- **Enums (existierend):** `MediaSourceType` (Werte: `Sftp = 0`, `LocalDirectory = 1`)
- **Models (existierend):** `MediaSource`, `LocalMediaSource`

### Implementierungsansatz

1. **Verzeichnis-Setup:** Nutze `LocalMediaSourcePipelineE2ETests`-Patterns zum Aufbau echter Dateisystem-Verzeichnisse mit Testvideo-/Bilddateien
2. **Medienquelle vom Typ LocalDirectory:** Erstelle `MediaSource` mit `SourceType = LocalDirectory` und echtem lokalen Pfad
3. **Titel-Hinzufügen zur Playlist:** Scanne Verzeichnis, füge erkannten Titel zur Playlist hinzu
4. **Wiedergabe:** Starte Playlist-Wiedergabe und belege erfolgreiches Streaming
5. **Backfill nach Scan:** Neue Dateien zum Verzeichnis hinzufügen, „Neu erfassen" (Media-Scan) durchführen, Backfill-Marker prüfen, automatische Playlist-Nachlieferung belegen
6. **Cover-Collage:** Erzeuge Playlist-Cover aus Bildern lokaler Inhalte (nicht nur SFTP)
7. **Weiterschauen-Ersatz:** Lösche lokale Medienquelle, prüfe dass entsprechender Weiterschauen-Eintrag mit Playlist-Bezug durch nächsten verfügbaren Titel derselben Playlist ersetzt wird

Der `MediaSourceReaderDispatcher` sollte bereits den Quelltyp abstrahieren; diese Tests belegen die Vollständigkeit der Abstraktion.

### Konfiguration

Keine zusätzliche Konfiguration erforderlich. Tests verwenden Standard-TestDB und echte Dateisystem-Verzeichnisse in Temp-Verzeichnissen.

### Akzeptanzkriterien

- [ ] Ein Test legt eine Medienquelle vom Typ „lokales Verzeichnis" mit echten Dateien an, fügt einen Titel daraus einer Playlist hinzu und spielt ihn aus der Playlist heraus ab.
- [ ] Ein Test belegt, dass ein neu hinzugekommener Titel in einem lokalen Verzeichnis nach dem „Neu erfassen" selbsttätig in eine Playlist nachgeliefert wird, die die zugehörige Serie, Staffel oder Filmsammlung enthält.
- [ ] Ein Test belegt, dass das Titelbild einer Playlist aus Bildern lokaler Inhalte erzeugt wird.
- [ ] Ein Test belegt, dass beim Löschen einer lokalen Medienquelle ein Weiterschauen-Eintrag mit Playlist-Bezug durch den nächsten verfügbaren Titel derselben Playlist ersetzt wird.
- [ ] Die vorhandenen Testhilfen für lokale Verzeichnisse und für Playlists werden wiederverwendet.

---

## A8 — Gerätedokumentation auf den Stand der QR-Kopplung bringen und den Playlist-Bezug ergänzen

**Priorität:** Mittel  
**Eigener Lifecycle-Lauf:** Ja

### Fachliche Zusammenfassung

Die Gerätedokumentation (`docs/help/geraete/*`) beschreibt derzeit nur den Stand vor QR-Bootstrap und Sitzungs-Erneuerung. Sie behauptet, es gebe nur einen öffentlichen Endpunkt; dies stimmt nicht mehr. Ferner fehlen die neuen Konzepte völlig: QR-Bootstrap, einmalige Tickets, Sitzungs-Erneuerung, Refresh-Token-Rotation, Widerruf-Verhalten. Der Zusammenhang mit Playlists wird überhaupt nicht erwähnt (welche Playlists ein Gerät sieht, öffentliche Playlists, Auswirkungen von Widerruf). Zusätzlich ist die Aussage zu Admin-only-Verwaltung falsch: Benutzer können sich selbst über ihre Profilseite ein Gerät koppeln (abhängig von Setting `Pairing:BootstrapAdminOnly`).

### Betroffene Klassen und Komponenten

**Dokumentationsdateien (zu aktualisieren):**
- `docs/help/geraete/beschreibung.md` — Übersicht der Funktionen; Korrektur: Self-Service neben Admin-Bereich; Playlist-Zugriff erklären
- `docs/help/geraete/api.md` — Alle öffentlichen Endpunkte korrekt aufzählen:
  - `POST /api/pairing/exchange` (Code-Exchange, alt)
  - `POST /api/pairing/bootstrap` (QR-Bootstrap, neu)
  - `POST /api/auth/refresh` (Sitzungs-Erneuerung, neu)
  - `POST /api/auth/logout` (Abmelden, neu)
  - Alle mit Scope `MauiOnly` (oder vergleichbar)
- `docs/help/geraete/datenmodell.md` — Neue Tabellen und Spalten:
  - `PairedDevices` (Tabelle)
  - `PairingCodes.Kind` (Spalte: `CodeExchange` oder `Bootstrap`)
  - `PairingCodes.TicketHash` (Spalte: Hash des einmaligen Tickets)
  - `RefreshTokens` (neue Tabelle)
- `docs/help/geraete/ablauf-technisch.md` — Detaillierte Beschreibung:
  - QR-Bootstrap-Flow (Ticket-Generierung, Hash-Speicherung, Einlösung)
  - JWT-Generierung mit `sub` = Benutzer
  - Refresh-Token-Rotation mit Reuse-Detection
  - Widerruf-Effekt (Refresh-Token ungültig, aktive JWT bis zu 12h)
- `docs/help/geraete/architektur.md` — Prinzipien und Aufteilerung:
  - Code-Exchange vs. Bootstrap (einmalig vs. wiederverwendbar)
  - Refresh-Token-Verwaltung und Rotation
  - Unterschied zwischen Geräteverwaltung (Admin/Selbstservice) und Pairing (User-spezifisch)
- `docs/help/geraete/qr-bootstrap-anwender.md` — bereits vorhanden; Link aus beschreibung.md ergänzen
- `docs/help/playlists.md` oder `docs/help/playlists-api.md` — Verweis auf Geräte-Zugriff ergänzen:
  - Gekoppelte Geräte sehen Playlists des JWT-Benutzers
  - Öffentliche Playlists sichtbar auf allen Geräten
  - Bei QR-Bootstrap: zunächst Playlists des Ticket-Erstellers; nach Benutzerwechsel des neuen Benutzers
  - Widerruf stoppt Refresh nicht sofort, aber aktive Playlist-Wiedergabe läuft bis JWT-Ablauf

**Link-Validierung:** Alle Querverweise zu anderen Seiten müssen gültig sein.

### Implementierungsansatz

Reine Dokumentation, keine Code-Änderung. Zu ergänzen:
1. API-Abschnitt: Alle 5 Endpunkte auflisten (statt „nur ein öffentlicher")
2. Datenmodell-Abschnitt: Neue Spalten und Tabellen mit Zweck erklären
3. Technischer Ablauf: QR-Bootstrap, Token-Rotation, Reuse-Detection, Widerruf-Logik
4. Geschäftsregeln: Wer darf was (Admin-Bereich vs. Self-Service, Playlist-Sichtbarkeit)
5. Playlist-Bezug: In Playlist-Dokumentation Geräte-Kapitel verlinken

### Konfiguration

Keine — reine Dokumentation.

### Akzeptanzkriterien

- [ ] Beschreibung, technischer Ablauf, API, Datenmodell, Architektur und Geschäftsregeln decken den QR-Bootstrap, die Sitzungs-Erneuerung und das Abmelden ab.
- [ ] Die Aussage „nur ein öffentlicher Endpunkt" und die Aufzählung der Endpunkte mit Zugangsschlüssel sind korrigiert.
- [ ] Es ist beschrieben, dass sich auch ein Anwender ohne Administratorrechte über seine Profilseite ein Gerät koppeln kann und wie sich die Einstellung „nur Administratoren" darauf auswirkt.
- [ ] Es ist beschrieben, wessen Playlists ein gekoppeltes Gerät sieht (die des angemeldeten Anwenders, bei QR-Kopplung zunächst die des Erstellers des Tickets), dass öffentliche Playlists dort ebenfalls erscheinen und wie ein Benutzerwechsel auf dem Gerät möglich ist.
- [ ] Es ist beschrieben, dass ein Widerruf die Erneuerung sofort sperrt, eine laufende Sitzung aber bis zu zwölf Stunden weiterarbeitet — auch bei laufender Playlist-Wiedergabe.
- [ ] Alle Querverweise sind gültig (Link-Prüfung läuft durch).

---

## A9 — Flackernde Playlist-Prüfungen im Browser stabilisieren

**Priorität:** Niedrig bis mittel  
**Eigener Lifecycle-Lauf:** Ja (klein)

### Fachliche Zusammenfassung

Zwei automatische Browser-Prüfungen zu Playlist-Titelsuche schlagen unregelmäßig fehl (ca. 25 % der Durchläufe). Beide scheitern in derselben Hilfsroutine `SelectSearchResultAsync`. Das Problem: fest kodierte Timeouts (5s für Suchergebnis, 1s für Serverantwort) reichen unter Last nicht aus. Lösung: Feste Wartezeiten durch explizites Warten auf beobachtbare Zustände (DOM-Element-Präsenz, Button-Verfügbarkeit) ersetzen; Playwright-Standard-Timeouts (30s) verwenden statt manueller kurzer Timeouts.

### Betroffene Klassen und Komponenten

- **Test-Hilfsklasse (zu ändern):** 
  - `VideoWebPlayer.Tests/Helpers/PlaylistsE2ETestBase.cs` (Methode `SelectSearchResultAsync`, Zeilen ~135–144)
- **Test-Klassen (betroffen von Stabilisierung):**
  - `PlaylistMediaSearchE2ETests` (Test `SelectSeason_CascadesOnlyThatSeasonsEpisodes`)
  - `PlaylistPlaybackE2ETests` (Test `PlaylistPreviousAtBeginningDoesNotShowEndReachedE2ETest`)
- **Browser-Automation (existierend):** Playwright (bereits in Nutzung)

### Implementierungsansatz

**Aktueller Code (fehlerhaft):**
```csharp
page.Locator(searchResultSelector).First.ClickAsync(new() { Timeout = 5000 });
await Task.Delay(1000); // feste Wartezeit
```

**Neuer Code (state-basiert):**
1. Nutze `Locator.WaitForAsync()` mit Playwright-Standard-Timeout (30s)
2. Ersetze `Task.Delay(1000)` durch explizites Warten auf beobachtbaren Zustand:
   - Button `#playlist-mode-add-button` ist clickable/enabled
   - oder: erwartete UI-Element ist sichtbar/präsent
3. Optional: Retry-Logik für Element-Suche, wenn mehrere Versuche nötig

Playwright-Methoden verwenden:
- `Locator.WaitForAsync()` — warte bis Element präsent
- `Locator.IsVisibleAsync()` — prüfe Sichtbarkeit statt Delay
- `Page.WaitForURLAsync()` — warte auf Navigation
- `Page.WaitForLoadStateAsync()` — warte auf Netzwerk-Idle

Keine Prüfung soll deaktiviert oder in ihrer Aussage abgeschwächt werden.

### Konfiguration

Keine zusätzliche Konfiguration erforderlich. Tests verwenden Standard-Playwright-Konfiguration.

### Akzeptanzkriterien

- [ ] Zwanzig aufeinanderfolgende Durchläufe der beiden betroffenen Prüfungen sind fehlerfrei.
- [ ] Die vollständige Testsuite läuft dreimal hintereinander fehlerfrei durch.
- [ ] Keine Prüfung wurde deaktiviert, übersprungen oder in ihrer Aussage abgeschwächt.
- [ ] Feste Wartezeiten sind durch Warten auf einen beobachtbaren Zustand ersetzt.

---

## Abhängigkeiten und Kontext

- **A4** (Fehlerantworten 403/404) ist bereits implementiert: ermöglicht korrekte Fehlerbehandlung in Tests
- **A5** (Client-Bibliothek mit Pairing, Bootstrap, Refresh) ist bereits implementiert: Tests in A6 können echte Client-Bibliotheks-Methoden nutzen
- Alle Tests nutzen existierende Testhilfen statt neue zu bauen
- Keine Programmänderungen am produktiven Code außer Dokumentation (A8 ist reine Dokumentation)

---

## Verbindliche Quellen (Ergänzung)

Diese Übersetzung fasst zusammen. Verbindlich für Ziel, **Akzeptanzkriterien** und Abgrenzung je Punkt ist der Originaltext:

- GitHub-Issue #233 (`gh issue view 233`), Entwürfe A6 bis A9 mit „Ausgangslage", „Ziel / gewünschtes Verhalten", „Akzeptanzkriterien", „Betroffene Bereiche", „Abgrenzung".
- Analysebericht `docs/projects/task/issue-207-fd729906880143ee8539fa3e046c88f4-playlists-fuer-serien-staffeln/analyse-staging-und-playlists.md`: Abschnitt 4 (Befunde F6 bis F9 mit Belegen und Fundstellen) und Abschnitt 5 (Entwürfe).
- Umfang dieses Laufs: A6, A7, A8, A9. A1 bis A5 sind bereits gemergt (PR #235, #236).
- Reihenfolge: A9 zuerst (Testinfrastruktur-Grundlage), dann A6 und A7 (reine Testarbeit, ähnliche Testhelfer), zuletzt A8 (reine Dokumentation).
- A6/A7/A9 sind ausdrücklich reine Testarbeit ohne Programmänderung. Fällt dabei echtes Fehlverhalten der Anwendung auf, wird es getrennt gemeldet, nicht in diesem Lauf "repariert".
