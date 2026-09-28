# Dokumentationsstatus — Bestandsaufnahme A8

## Übersicht der Gerätedokumentation

Die Gerätedokumentation befindet sich unter `docs/help/geraete/` und behandelt Geräte-Pairing, QR-Bootstrap, Sitzungs-Erneuerung und Abmelden.

| Datei | Größe (Zeilen ca.) | Stand | Lücken |
|-------|-------------------|-------|--------|
| `beschreibung.md` | ~50 | vor #232 | Self-Service erwähnt, QR-Bootstrap nicht; Playlist-Bezug fehlt |
| `api.md` | ~150 | vor #232 | Behauptet "nur ein öffentlicher Endpunkt" (false); Refresh/Logout fehlen |
| `datenmodell.md` | ~80 | vor #232 | `RefreshTokens`, `PairingCodes.Kind`/`TicketHash` fehlen |
| `ablauf-technisch.md` | ~120 | vor #232 | Bootstrap-Flow, Token-Rotation, Widerruf-Verhalten nicht beschrieben |
| `architektur.md` | ~100 | vor #232 | Code-Exchange vs. Bootstrap, Refresh-Token-Management nicht beschrieben |
| `business-rules.md` | ~80 | vor #232 | Widerruf nur für "Benutzer-JWTs" allgemein, nicht mit Playlist-Bezug |
| `qr-bootstrap-anwender.md` | ~60 | aktuell ✓ | Vollständig; aus PR #232 |
| `client-bibliothek.md` | ~100 | aus A5 ✓ | Dokumentiert API-Methoden; Pairing/Bootstrap/Refresh/Logout vorhanden |
| `index.md` | ~50 | gemischt | Enthält Links; `client-bibliothek.md` noch nicht verlinkt |

## Detaillierte Lücken (nach Analyse und Requirement A8)

### `beschreibung.md`
**Aktueller Zustand:** Beschreibt die Funktion auf Oberflächenebene, erwähnt nur Code-Exchange.

**Fehlende Abschnitte:**
- QR-Bootstrap als Alternative zu Code-Exchange
- Self-Service-Kopplung über Profilseite (nicht nur Admin-Bereich)
- Sitzungs-Erneuerung (Refresh-Token)
- Geräte-Widerruf
- Playlist-Zugriff durch Geräte (welche Playlists sieht ein Gerät)
- öffentliche Playlists auf Geräten

**Falsche Aussagen:**
- Zeile ~30: "Die Verwaltung liegt im Einrichtungsbereich unter `Einrichtung` > `Geräte` und ist nur für Administratoren sichtbar" — Benutzer können sich selbst über `Profil` > `Geräte` Geräte koppeln (Setting `Pairing:BootstrapAdminOnly`, Standard: false)

### `api.md`
**Aktueller Zustand:** Listet nur `POST /api/pairing/exchange`.

**Fehlende Endpunkte:**
- `POST /api/pairing/bootstrap` (QR-Bootstrap)
- `POST /api/auth/refresh` (Sitzungs-Erneuerung)
- `POST /api/auth/logout` (Abmelden)
- Spielweis auf `GET /api/playlists/{id}/cover?access_token=JWT` (Query-Parameter-Auth für Bilder)

**Falsche Aussagen:**
- Zeile ~5: "Das Feature exponiert einen **einzigen** öffentlichen Endpunkt" — False seit #232
- Zeile ~8: "Im Scope `MauiOnly` (**derzeit nur** `POST /api/auth/login`)" — False, Refresh/Logout auch im Scope

### `datenmodell.md`
**Aktueller Zustand:** Beschreibt nur `PairedDevices` (alt).

**Fehlende Tabellen und Spalten:**
- `PairingCodes.Kind` (Spalte: `CodeExchange` vs. `Bootstrap`)
- `PairingCodes.TicketHash` (Hash des einmaligen Tickets für Reuse-Detection)
- `RefreshTokens` (komplette neue Tabelle: `Id`, `PairedDeviceId`, `TokenHash`, `JwtId`, `ReusedTokenHash`, `IsRevoked`, `ExpiresAt`, `CreatedAt`)
- `ContinueWatchingEntries.PlaylistId` (Spalte für Playlist-Zugehörigkeit beim Fortschritt)

### `ablauf-technisch.md`
**Aktueller Zustand:** Behandelt alte Logik (Code-Exchange).

**Fehlende Abschnitte:**
1. **QR-Bootstrap-Flow:**
   - Ticket-Generierung (15-Min-Gültig, Hash speichern)
   - Client sendet Ticket + ECDH-PublicKey
   - Server erzeugt JWT + Refresh-Token, verschlüsselt mit Client-Key
   - Client dekryptiert Payload

2. **JWT-Generierung:**
   - Claim `sub` = Benutzer-ID
   - Gültigkeitsdauer: 12 Stunden
   - Unterschied zu "web" JWTs

3. **Refresh-Token-Rotation:**
   - Neuer Refresh bei erfolgreichem `POST /api/auth/refresh`
   - Reuse-Detection: doppelter Refresh desselben Tokens triggert Sicherheitsmaßnahme
   - Rotation mit `RefreshTokenService.RotateAsync`

4. **Geräte-Widerruf:**
   - `POST /api/auth/logout`: invalidiert Refresh-Token sofort
   - `RevokeAsync(deviceId)`: markiert Gerät als widerrufen
   - Effekt: zukünftige Refresh-Versuche schlagen fehl (401)
   - Alte JWTs: bleiben bis zu 12h gültig
   - Playlist-Wiedergabe: läuft bis JWT-Ablauf weiter

### `architektur.md`
**Aktueller Zustand:** Allgemeine Architektur ohne Bootstrap/Refresh.

**Fehlende Prinzipien:**
1. **Code-Exchange vs. Bootstrap:**
   - Code-Exchange: wiederverwendbar (paart Geräte mit Benutzer-Code)
   - Bootstrap: einmalig (QR-Code mit Ticket, direkt zur Playlist)

2. **Refresh-Token-Management:**
   - Token-Rotation nach Refresh
   - Reuse-Detection als Sicherheitsmerkmal
   - Speicherung als Hash (Plaintext wird nicht gespeichert)

3. **Unterschied Geräteverwaltung vs. Pairing:**
   - Geräte-Admin: wer darf Geräte widerrufen (Admin vs. Self-Service)
   - Pairing: technischer Prozess (Code-Exchange oder QR-Bootstrap)

4. **Self-Service-Kopplung:**
   - Setting `Pairing:BootstrapAdminOnly` (Standard: false)
   - Benutzer können über Profilseite Tickets erzeugen
   - Admin sieht und widerruft in Geräte-Verwaltung

### `business-rules.md`
**Aktueller Zustand:** Behandelt Widerruf allgemein (nicht Geräte-spezifisch).

**Fehlende Aussagen:**
- Widerruf eines Geräts: Refresh-Token sofort ungültig, alte JWTs bis 12h
- Playlist-Konsequenzen: laufende Wiedergabe läuft bis JWT-Ablauf, neue Refresh-Versuche schlagen fehl
- Mehrbenutzer-Isolation: Geräte sehen Playlists des JWT-Benutzers (nicht des Kopplers)

## Dokumentation `docs/help/playlists.md` / `docs/help/playlists-api.md`

**Status:** Playlist-Dokumentation existiert, erwähnt Geräte nicht.

**Fehlende Abschnitte:**
- Geräte-Zugriff auf Playlists (welche Playlists sieht ein Gerät)
- Öffentliche Playlists auf Geräten
- Playlist-Fortschritt mit `playlistId`
- Cover-Zugriff über `?access_token=JWT` Query-Parameter

## Verlinkung und Navigation

### `index.md` (Gerätedokumentation)
**Status:** Existiert, listet Übersichts-Links.

**Zu ergänzen:**
- Link zu `client-bibliothek.md` (ist aus A5 vorhanden, aber noch nicht im Index)
- Querverweise von anderen Seiten zurück zum Geräte-Index

### `docs/help/index.md` (Hauptnavigation)
**Status:** Existiert, enthält Playlists, Geräte und Medienquellen.

**Zu prüfen:**
- Verlinkung `Geräte` → `docs/help/geraete/index.md` (sollte funktionieren)
- Verlinkung `Playlists` → Geräte-Kapitel ergänzen (neuer Link)

## Link-Validierung

**Bisher nicht geprüft:**
- Alle `*.md` Dateien in `docs/help/geraete/` auf gültige interne Links
- Querverweise auf andere Dokumentations-Seiten
- Das Tool `MarkdownLinkCheck` (vorhanden, sollte genutzt werden)

**Anforderung A8:** Akzeptanzkriterium 5 verlangt Link-Prüfung ("Link-Prüfung läuft durch").

## Zusammenfassung der Arbeit für A8

| Bereich | Status | Aufwand |
|---------|--------|--------|
| `beschreibung.md` (Falschaussagen korrigieren) | ✗ Fehlt | Mittel |
| `beschreibung.md` (QR-Bootstrap ergänzen) | ✗ Fehlt | Mittel |
| `beschreibung.md` (Self-Service ergänzen) | ✗ Fehlt | Klein |
| `beschreibung.md` (Playlist-Bezug ergänzen) | ✗ Fehlt | Klein |
| `api.md` (Endpunkte aktualisieren) | ✗ Fehlt | Klein |
| `api.md` (Scopes korrigieren) | ✗ Fehlt | Klein |
| `datenmodell.md` (neue Tabellen/Spalten) | ✗ Fehlt | Klein |
| `ablauf-technisch.md` (QR-Bootstrap-Flow) | ✗ Fehlt | Groß |
| `ablauf-technisch.md` (JWT-Generierung) | ✗ Fehlt | Mittel |
| `ablauf-technisch.md` (Token-Rotation) | ✗ Fehlt | Mittel |
| `ablauf-technisch.md` (Widerruf) | ✗ Fehlt | Mittel |
| `architektur.md` (Code-Exchange vs. Bootstrap) | ✗ Fehlt | Mittel |
| `architektur.md` (Token-Management) | ✗ Fehlt | Mittel |
| `architektur.md` (Self-Service) | ✗ Fehlt | Klein |
| `business-rules.md` (Widerruf + Playlists) | ✗ Fehlt | Klein |
| `business-rules.md` (Mehrbenutzer-Isolation) | ✗ Fehlt | Klein |
| `playlists.md` (Geräte-Zugriff) | ✗ Fehlt | Klein |
| `playlists.md` (Cover-Query-Auth) | ✗ Fehlt | Klein |
| `index.md` (client-bibliothek.md verlinken) | ✗ Fehlt | Trivial |
| Link-Validierung (MarkdownLinkCheck) | ✗ Nicht geprüft | Klein |

