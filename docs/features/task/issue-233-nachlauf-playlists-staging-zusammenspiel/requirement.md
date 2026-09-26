# GitHub-Issue #233: Nachlauf Playlists × Staging-Änderungen

**Kontext:** Nachlauf zu #207 (Playlists) aus der Analyse „Zusammenspiel der Staging-Änderungen (Geräte-Pairing, QR-Bootstrap, lokale Verzeichnisse, Backup-Upload) mit dem Playlist-Feature".

**Status:** Geräte-API × Playlists funktioniert vollständig (empirisch belegt). Kritisch: Backup/Restore (A1) und API-Dokumentation (A2). Weitere Lücken in Tests und Dokumentation.

---

## A1 — Alte Datensicherungen müssen wiederherstellbar bleiben
**Priorität:** Hoch | **Eigener Lifecycle-Lauf:** Ja

### Fachliche Zusammenfassung
Das Backup-Restore-Verfahren lehnt Sicherungen ab, die die Tabellen `PairedDevices`, `PairingCodes` oder `RefreshTokens` nicht enthalten — Tabellen, die erst mit der Gerätekopplung hinzugekommen sind. Diese Tabellen müssen in der Restore-Logik als optional gekennzeichnet werden, damit alte Sicherungen wiederhergestellt werden können. Nach dem Restore bleiben die Tabellen leer, gekoppelte Geräte sind abgemeldet und müssen neu gekoppelt werden.

### Betroffene Klassen und Komponenten
- Restore-Logik / Backup-Handler: Prüfung erforderlicher vs. optionaler Tabellen
- Schema-Migration: Neue Tabellen als `OPTIONAL_RESTORE_TABLES` registrieren
- Test-Fixture: Regressionstest für Backup ohne `PairedDevices`, ohne `PairingCodes`, ohne `RefreshTokens`, ohne alle drei
- Release Notes und Help: Dokumentation der Geräte-Abmeldung nach Restore

### Implementierungsansatz
1. Tabellen `PairedDevices`, `PairingCodes`, `RefreshTokens` als optional im Restore-Verfahren kennzeichnen
2. Vier neue Regressionstests: je einer für fehlende `PairedDevices`, `PairingCodes`, `RefreshTokens` und alle drei zusammen
3. Bestätigung, dass aktuelle Backups die Tabellen vollständig wiederherstellen
4. Release Notes und Hilfedokumentation aktualisieren

### Konfiguration
Keine Konfiguration nötig. Die Markierung optionaler Tabellen erfolgt im Code.

### Offene Fragen
- Wo genau in der Migration werden optionale Tabellen registriert?
- Welches Restore-Verfahren (Datenbank-Export, Cloud-Restore, lokal) ist betroffen?

---

## A2 — Die API-Dokumentation muss die Playlist-Endpunkte vollständig beschreiben
**Priorität:** Hoch | **Eigener Lifecycle-Lauf:** Ja

### Fachliche Zusammenfassung
`docs/API.md` dokumentiert Playlist-Verwaltung (CRUD), fehlen aber 17 Endpunkte für Wiedergabe (Start, Forward, Backward, Auto-Play), Titelbilder (Get, Upload, Generate, Preview, Delete), Sortierung, Genres und manuelles Reordering. Der API-Vertragstest prüft keine Playlist-Routen und kennt die neuen Session-Endpunkte nicht (`POST /api/pairing/bootstrap`, `POST /api/auth/refresh`, `POST /api/auth/logout`). Dokumentiert wird auch, dass `access_token` als Query-Parameter an alle Endpunkte mitgegeben werden kann.

### Betroffene Klassen und Komponenten
- `docs/API.md`: Alle Playlist-Endpunkte dokumentieren
- `docs/help/playlists-api.md`: Konsistenz mit API.md prüfen
- API-Vertragstest: Playlist-Routen und Session-Endpunkte in Vertragstest einbinden; Durchlauf testen (Create Playlist → Add Item → Start Playback → Get Next Item)
- Playlist Controller / Endpoints: (keine Änderung, nur Dokumentation)

### Implementierungsansatz
1. Alle 17 fehlenden Endpunkte in `docs/API.md` dokumentieren mit Pfad, HTTP-Methode, Parametern, Response-Format, Fehlercodes
2. Query-Parameter `access_token` als Alternative zu Bearer-JWT dokumentieren
3. Vertragstest erweitern: Zentrale Playlist-Routen (`POST /api/playlist`, `POST /api/playlist/{id}/item`, `POST /api/playlist/{id}/play`, `GET /api/playlist/{id}/item/next`) und Session-Endpunkte (`POST /api/pairing/bootstrap`, `POST /api/auth/refresh`, `POST /api/auth/logout`) prüfen
4. Widersprüche mit `docs/help/playlists-api.md` auflösen
5. Aktualisierungsdatum in `docs/API.md` aktualisieren

### Konfiguration
Keine Konfiguration nötig. Dokumentation ist statisch.

### Offene Fragen
- Liste der 17 fehlenden Endpunkte: Sind alle aufgezählt (5 für Titelbild, 4 für Wiedergabe, 3 für Sortierung, 5 weitere)?
- Welche exakten HTTP-Statuscodes sollen dokumentiert werden (basierend auf A4)?

---

## A3 — Deutsche Release Notes für die Gerätekopplung per QR-Code nachziehen
**Priorität:** Hoch | **Eigener Lifecycle-Lauf:** Nein (Teil von A1)

### Fachliche Zusammenfassung
Die Release Notes enthalten englische und deutsche Einträge, aber für die QR-Gerätekopplung fehlen im deutschen Teil alle Einträge. Besonders kritisch: Die Datenbankänderungen (neue Tabelle `RefreshTokens`, Spalten-Erweiterungen bei `PairingCodes`) und vier neue Konfigurationsschlüssel sind im deutschen Abschnitt „Wichtige Hinweise vor dem Update" nicht erwähnt.

### Betroffene Klassen und Komponenten
- `docs/RELEASE_NOTES.md`: Deutsche Übersetzung nachtragen

### Implementierungsansatz
1. Zu jedem englischen Punkt der QR-Kopplung einen deutschen Pendants hinzufügen
2. Im Abschnitt „Wichtige Hinweise vor dem Update" (deutsch) die Datenbankänderung und die vier neuen Config-Keys eintragen
3. Wiederherstellbarkeit alter Backups erwähnen (abhängig von A1)

### Konfiguration
Keine Konfiguration nötig.

### Offene Fragen
- Welche sind die vier neuen Konfigurationsschlüssel?

---

## A4 — Verständliche Fehlerantworten der Medien-API
**Priorität:** Mittel | **Eigener Lifecycle-Lauf:** Ja

### Fachliche Zusammenfassung
Die Medien-Endpunkte (Details, Videostrom, Download) geben falsche HTTP-Statuscodes zurück: 401 („Nicht angemeldet") statt 403 („Kein Zugriff") bei fehlender Berechtigung für angemeldete Nutzer; 500 („Interner Fehler") statt 404 („Nicht gefunden") bei unbekannten Kennungen oder fehlenden Videodateien. Client-Apps interpretieren 401 als abgelaufene Sitzung und versuchen eine Erneuerung, was hier scheitert. Mit öffentlichen Playlists ist das ein häufiger Fall: Ein fremder Nutzer sieht Titel, für die er nicht freigeschaltet ist.

### Betroffene Klassen und Komponenten
- Medien-Controller / Endpunkte (Details, Stream, Download): Fehlerbehandlung korrigieren
- Autorisierungs-Middleware: 403 bei fehlender Berechtigung
- Fehlerbehandlung / Exception Handler: 404 bei unbekannter ID oder fehlender Videodatei
- Client-Bibliothek `VideoWebPlayer.Client`: Fehlerbehandlung 403 nicht als Sitzungsablauf interpretieren
- Playlist-Detail-UI: Fehlerbehandlung anpassen
- Tests: Alle Tests, die 401/500 erwarten, anpassen; neue Tests für 403/404

### Implementierungsansatz
1. Endpunkte durchsuchen, die 401 für fehlende Berechtigung zurückgeben → 403
2. Endpunkte durchsuchen, die 500 für unbekannte ID/fehlende Videodatei zurückgeben → 404
3. Autorisierungs-Middleware und Exception Handler entsprechend ändern
4. Alle bestehenden Tests anpassen (401 → 403, 500 → 404 je nach Fall)
5. Neue Tests für öffentliche Playlists mit gesperrten Titeln
6. `docs/API.md` mit korrekten Statuscodes aktualisieren

### Konfiguration
Keine Konfiguration nötig.

### Offene Fragen
- Welche Endpunkte geben heute 401/500 zurück?

---

## A5 — Client-Bibliothek um Gerätekopplung, QR-Bootstrap und Sitzungserneuerung ergänzen
**Priorität:** Mittel | **Eigener Lifecycle-Lauf:** Ja

### Fachliche Zusammenfassung
`VideoWebPlayer.Client` enthält Datenstrukturen für Gerätkopplung (Pairing, Bootstrap, Refresh), aber keine API-Methoden. Fehlend sind: Pairing-Code einlösen, QR-Bootstrap durchführen, Sitzung erneuern, abmelden. Das Device-Token kann nicht als Zugangsschlüssel mitgegeben werden. Der Wiederholungsmechanismus bei Session-Ablauf ist nicht implementiert. Jede Client-App muss diese Funktionen doppelt bauen.

### Betroffene Klassen und Komponenten
- `VideoWebPlayer.Client`: Neue öffentliche Methoden für Pairing/Bootstrap/Refresh/Logout
- Session-Management in `VideoWebPlayer.Client`: Device-Token speichern und bei Bedarf mitschicken
- Retry-Logik bei Session-Ablauf: Bei `VideoWebPlayer.Client`-Aufrufen automatisch erneuern und wiederholen
- Playlist-Aufrufe in `VideoWebPlayer.Client`: Mit automatischem Retry bei Session-Ablauf
- Server-seitiger Ableger der Bibliothek
- Tests: Durchlauf Pairing → Playlists → Playback → Progress; Session-Erneuerung zwischen zwei Playlist-Aufrufen; Fehler nach Geräte-Widerruf

### Implementierungsansatz
1. Neue öffentliche Methoden hinzufügen:
   - `RedeemPairingCodeAsync(code: string): Task<PairingResponse>`
   - `BootstrapQrAsync(ticket: string): Task<BootstrapResponse>`
   - `RefreshSessionAsync(): Task<AuthResponse>`
   - `LogoutAsync(): Task`
   - `SetDeviceToken(token: string): void`
2. Alle API-Aufrufe, die ein Device-Token haben, damit automatisch mitschicken
3. Retry-Logik implementieren: Bei 401 nach ersten Refresh versuchen und wiederholen (max. 1 Versuch, dann fehlschlagen mit eindeutigem Fehler)
4. PUT/PATCH/DELETE-Aufrufe und Aufrufe mit Abbruchmarke einbinden
5. Tests: End-to-End Pairing → Playlist → Playback → Progress; Session-Renewal; Post-Revocation-Error

### Konfiguration
Keine Konfiguration nötig.

### Offene Fragen
- Wie wird Device-Token in der Bibliothek gespeichert (statisch, pro Instance)?
- Welcher Fehler bei Post-Revocation (Exception-Type)?

---

## A6 — Automatische Prüfungen für Playlists auf gekoppelten Geräten
**Priorität:** Mittel | **Eigener Lifecycle-Lauf:** Ja

### Fachliche Zusammenfassung
Playlists und Gerätekopplung sind separat getestet, aber nie zusammen. Kein Test belegt, dass ein gekoppeltes Gerät (per QR-Bootstrap) Playlists sehen, abspielen und Fortschritt melden kann, und dass ein Geräte-Widerruf das Verhalten auswirkt. Eine Testsuite soll einen vollständigen Geräte-Lebenszyklus abdecken: Kopplung, Abruf Playlists, Wiedergabe starten, nächster Titel, Fortschritt mit Playlist-Bezug, Titelbild, Session-Erneuerung, Geräte-Widerruf.

### Betroffene Klassen und Komponenten
- Test-Suite: Endpunkt- und Ende-zu-Ende-Tests für Geräte-Playlist-Szenarios
- Test-Fixture: Geräte-Kopplung (QR-Bootstrap) + Playlist-Zugriff
- Test-Helpers: Wiederverwendung bestehender Kopplung-Fixtures

### Implementierungsansatz
1. Test: Gerät per QR-Bootstrap koppeln → Playlists auflisten (nur eigene und öffentliche)
2. Test: Playlist-Wiedergabe starten → Nächster Titel holen → Fortschritt melden mit Playlist-Bezug → In Weiterschauen-Liste mit Playlist-Namen verifizieren
3. Test: Titelbild mit `access_token`-Query-Parameter abrufen
4. Test: Nach Geräte-Widerruf → Session-Erneuerung schlägt fehl, aber laufende Session läuft bis 12 Stunden weiter
5. Test: Zwei Nutzer, je ein Gerät → Geräte-1-Nutzer sieht nur seine und öffentliche Playlists, nicht die von Geräte-2-Nutzer
6. Bestehende Kopplung-Helpers wiederverwenden

### Konfiguration
Keine Konfiguration nötig.

### Offene Fragen
- Wo werden bestehende Kopplung-Fixtures definiert?

---

## A7 — Automatische Prüfungen für Playlists mit lokalen Verzeichnissen als Medienquelle
**Priorität:** Mittel | **Eigener Lifecycle-Lauf:** Ja

### Fachliche Zusammenfassung
Medienquellen können lokal oder SFTP sein, Playlists sind getestet, aber nie zusammen. Kein Test belegt, dass Playlists mit lokalen Verzeichnissen genauso funktionieren wie mit SFTP (besonders bei Auto-Scan und Cover-Generierung).

### Betroffene Klassen und Komponenten
- Test-Suite: Playlist + lokale Verzeichnisse
- Test-Fixture: Lokale Medienquelle mit echten Dateien + Playlist

### Implementierungsansatz
1. Test: Lokale Medienquelle anlegen → Titel hinzufügen zu Playlist → Aus Playlist abspielen
2. Test: Neu erfasster Titel in lokalem Verzeichnis wird selbsttätig in Serie/Staffel/Sammlung-Playlist nachgeliefert
3. Test: Playlist-Cover wird aus lokalen Bildern erzeugt
4. Test: Beim Löschen lokaler Medienquelle wird Weiterschauen-Eintrag mit Playlist-Bezug auf nächsten Titel in derselben Playlist aktualisiert
5. Bestehende Helpers für lokale Verzeichnisse und Playlists wiederverwenden

### Konfiguration
Keine Konfiguration nötig.

### Offene Fragen
- Wo sind bestehende Test-Helpers für lokale Verzeichnisse definiert?

---

## A8 — Gerätedokumentation auf den Stand der QR-Kopplung bringen und den Playlist-Bezug ergänzen
**Priorität:** Mittel | **Eigener Lifecycle-Lauf:** Ja

### Fachliche Zusammenfassung
Die Geräte-Hilfe unter `docs/help/geraete/*` ist veraltet: Sie beschreibt die Kopplung vor QR-Code-Einführung, behauptet genau einen öffentlichen Endpunkt (falsch) und dass nur Admins Geräte verwalten (falsch — jeder Nutzer kann über Profil-Seite). QR-Bootstrap, Session-Erneuerung und Playlist-Bezug fehlen komplett.

### Betroffene Klassen und Komponenten
- `docs/help/geraete/beschreibung.md`: Kopplung (Code + QR), Session-Erneuerung, Abmelden
- `docs/help/geraete/technischer-ablauf.md`: Bootstrap-Tickets, Session-Tokens
- `docs/help/geraete/api.md`: Alle Endpunkte (public + protected)
- `docs/help/geraete/datenmodell.md`: PairedDevices, PairingCodes, RefreshTokens
- `docs/help/geraete/architektur.md`: Device-Identität, Token-Verwaltung
- `docs/help/geraete/geschaeftsregeln.md`: Sichtbarkeit Playlists (nutzer-spezifisch + öffentliche)
- `docs/help/playlists-api.md`: Verweis auf Geräte-Dokumentation

### Implementierungsansatz
1. Beschreibung: QR-Code-Kopplung neben Code-Kopplung dokumentieren
2. Technischer Ablauf: Bootstrap-Tickets und Session-Erneuerung ergänzen
3. API: Alle Endpunkte auflisten und dokumentieren (public/private unterscheiden)
4. Datenmodell: Drei neuen Tabellen dokumentieren
5. Architektur: QR-Bootstrap-Fluss dokumentieren
6. Geschäftsregeln: Sichtbarkeit Playlists (nutzer-eigene + öffentliche), Widerruf-Verhalten (sofort Renewal sperren, Session läuft 12h)
7. Profil-Seite: Nutzer-Geräte-Verwaltung dokumentieren
8. Link-Check durchführen

### Konfiguration
Keine Konfiguration nötig.

### Offene Fragen
- Existiert bereits eine Struktur unter `docs/help/geraete/`?

---

## A9 — Flackernde Playlist-Prüfungen im Browser stabilisieren
**Priorität:** Niedrig | **Eigener Lifecycle-Lauf:** Ja

### Fachliche Zusammenfassung
Zwei automatische Browser-Tests für Playlist-Titelsuche schlagen unregelmäßig fehl (ca. 25% Fehlerquote). Die gemeinsame Hilfsfunktion wartet nur 5 Sekunden auf Suchergebnis und 1 Sekunde auf Server-Antwort — unter Last zu kurz. Tests sollen auf Zustand warten, nicht auf feste Zeiten.

### Betroffene Klassen und Komponenten
- Test-Helper: Gemeinsame Routine für Titelsuche in Playlists
- Browser-Test-Suite: Beide flackernden Tests

### Implementierungsansatz
1. Hilfsfunktion identifizieren, die feste Wartezeiten nutzt
2. Feste Zeiten durch Warten auf erwartete Zustandsänderung ersetzen (z. B. Element sichtbar, API-Antwort erhalten)
3. Beide flackernden Tests reparieren
4. 20 aufeinanderfolgende Durchläufe beider Tests durchführen (fehlerfrei)
5. Komplette Test-Suite 3x hintereinander durchführen (fehlerfrei)
6. Keine Tests deaktivieren oder abschwächen

### Konfiguration
Keine Konfiguration nötig.

### Offene Fragen
- Welche sind die beiden flackernden Tests?
- Welche Test-Helpers für Browser-Tests existieren?

---

## Verbindliche Quellen (Ergänzung)

Diese Übersetzung fasst zusammen. Verbindlich für Ziel, **Akzeptanzkriterien** und Abgrenzung je Punkt ist der Originaltext:

- GitHub-Issue #233 (`gh issue view 233`), Entwürfe A1 bis A9 mit „Ausgangslage", „Ziel / gewünschtes Verhalten", „Akzeptanzkriterien", „Betroffene Bereiche", „Abgrenzung".
- Analysebericht `docs/projects/task/issue-207-fd729906880143ee8539fa3e046c88f4-playlists-fuer-serien-staffeln/analyse-staging-und-playlists.md`: Abschnitt 4 (Befunde F1 bis F10 mit Belegen, Ursachen und Fundstellen, z. B. `VideoWebPlayer/Services/Backups/VideoWebPlayerBackupData.cs` OptionalRestoreTables) und Abschnitt 5 (Entwürfe).
- Die in den Abschnitten „Offene Fragen" oben genannten Punkte sind im Analysebericht überwiegend beantwortet (Fundstellen dort); der Plan soll sie daraus klären und nur echte Restfragen als offene Punkte führen.
- Reihenfolge: A1, A2, A3 (Priorität Hoch) zuerst; danach A4 bis A9.
