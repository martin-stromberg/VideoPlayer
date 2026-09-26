# Umsetzungsplan: Nachlauf Playlists × Staging-Änderungen (Issue #233) — Lauf 1 (A1–A3)

## Übersicht

Diese Planung befasst sich mit den drei Anforderungen höchster Priorität (A1–A3) zur Absicherung und Dokumentation des Zusammenspiels zwischen dem Playlist-Feature (#207) und den Staging-Änderungen (Geräte-Pairing #229, QR-Bootstrap #232, lokale Verzeichnisse #225). Kritische Lücken: Alte Backups lassen sich nicht mehr wiederherstellen (F1), 17 Playlist-API-Endpunkte und alle QR-Bootstrap-Endpunkte fehlen in `docs/API.md` (F2), deutsche Release Notes sind unvollständig (F10). Diese drei Anforderungen müssen vor dem Push/PR zu `staging` erfüllt sein. A4–A9 folgen in eigenen Lifecycle-Läufen und bleiben im Issue #233 offen.

---

## Designentscheidungen

| Komponente / Bereich | Gewählter Ansatz | Begründung |
|---|---|---|
| **A1: Backup-Kompatibilität** | Drei neue Einträge in `VideoWebPlayerBackupData.OptionalRestoreTables` (`PairedDevices`, `PairingCodes`, `RefreshTokens`) + ein Eintrag in `OptionalRestoreIntDefaults` für `PairingCodes.Kind` (default `0`) | `PairingCodes` ist eine Tabelle aus Migration #229 (vor #232); die neuen Spalten `Kind` und `TicketHash` werden in Migration `20260924103134_AddPairingBootstrapAndRefreshTokens` hinzugefügt. Ein Alt-Backup VOR dieser Migration hat `PairingCodes` ohne diese Spalten. Restore muss `Kind` mit default `0` füllen; `TicketHash` ist nullable und bleibt NULL. |
| **A2: API-Dokumentation** | Dokumentation aller 17 Playlist-Endpunkte in `docs/API.md` + Vertragstest mit echtem Durchlauf (Playlist erstellen → Titel hinzufügen → Wiedergabe starten → nächsten Titel holen) + Prüfung, dass Test scheitert, wenn eine Routen entfernt wird | Folgt etabliertem Muster; `docs/help/playlists-api.md` wird danach auf Konsistenz mit `API.md` geprüft. |
| **A3: Deutsche Release Notes** | Wort-für-Wort-Übersetzung der englischen QR-Bootstrap-Einträge + Ergänzung des Abschnitts „Wichtige Hinweise vor dem Update" mit Schema- und Config-Hinweisen | Stellengleiche Struktur; Umlaute korrekt (ü, ö, ä); jeder englische Punkt hat einen deutschen Equivalent. |

---

## Programmabläufe

### A1: Restore eines Alt-Backups (ohne PairedDevices, PairingCodes, RefreshTokens oder mit älteren Spalten)

1. Benutzer wählt eine `.bak`-Datei aus, die vor QR-Bootstrap-Einführung oder vor `20260924103134_AddPairingBootstrapAndRefreshTokens` erzeugt wurde.
2. `VideoWebPlayerBackupData.ReadFromAsync()` liest `index.json` und prüft verfügbare Tabellen und Spalten.
3. Für jede EF-Entität in `ApplicationDbContext` wird geprüft, ob die Tabelle in der `.bak`-Datei vorhanden ist.
4. Tabellen in `OptionalRestoreTables` (einschließlich der neuen drei `PairedDevices`, `PairingCodes`, `RefreshTokens`) → füllen mit leeren Datensätzen; fehlende Spalten aus `OptionalRestoreColumns`/`OptionalRestoreIntDefaults` mit Defaults.
5. Tabellen nicht in `OptionalRestoreTables` → wirft `InvalidDataException`.
6. Nach Restore: `PairedDevices`, `PairingCodes`, `RefreshTokens` sind leer oder mit Defaults gefüllt; gekoppelte Geräte sind abgemeldet (alte Refresh-Tokens ungültig).
7. In den Release Notes und `docs/help/backups.md` dokumentieren: Nach Restore aus Alt-Backup sind gekoppelte Geräte abgemeldet.

Beteiligte Klassen: `VideoWebPlayerBackupData`, `ApplicationDbContext`

### A2: API-Vertragstest für Playlist-Endpunkte und Session-Endpunkte

1. Test-Case erstellt eine Playlist (POST /api/playlists).
2. Fügt einen Eintrag hinzu (POST /api/playlists/{id}/entries).
3. Startet Wiedergabe (POST /api/playlists/{id}/play) → erhält 200.
4. Ruft nächsten Titel auf (POST /api/playlists/{id}/play/next) → erhält 200.
5. Bestätigt Statuscodes 200/201/204.
6. Test-Fall für öffentliche Playlists: fremder Benutzer sieht Playlist, versucht Wiedergabe, erhält 403 (fehlende Berechtigung) oder 404 (nicht freigeschaltet).
7. Vertragstest prüft auch Session-Endpunkte (POST /api/pairing/bootstrap, POST /api/auth/refresh, POST /api/auth/logout).
8. Vertragstest ist so konfiguriert, dass er scheitert, wenn eine geforderte Route aus `docs/API.md` aus dem Code entfernt wird.

Beteiligte Klassen: `ApiDocumentationContractTests`

### A3: Deutsche Release Notes ergänzen und Konsistenz sicherstellen

1. Für jeden englischen QR-Bootstrap-Eintrag (Zeilen 38–50 in `docs/RELEASE_NOTES.md`) einen deutschen Eintrag hinzufügen.
2. Im Abschnitt "Wichtige Hinweise vor dem Update" (deutsch, Zeile 68+) Hinweise auf Datenbankänderung (dotnet ef database update erforderlich) und die vier neuen Konfigurationsschlüssel `Pairing:CodeLength`, `Pairing:CodeTtlMinutes`, `Pairing:BootstrapTicketTtlMinutes`, `Pairing:BootstrapMaxTicketsPerHour`, `Pairing:BootstrapAdminOnly`, `Auth:RefreshTokenTtlDays` eintragen.
3. Wiederherstellbarkeit alter Backups erwähnen (nach A1 implementiert).
4. Struktur und Länge deutscher und englischer Release Notes sind gleichwertig (jeder englische Punkt hat einen deutschen Equivalent).

Beteiligte Klassen: `docs/RELEASE_NOTES.md`

---

## Neue Klassen

Keine neuen Klassen erforderlich. Alle A1–A3 Anforderungen werden durch:
- Erweiterung bestehender Klassen (`VideoWebPlayerBackupData`)
- Dokumentations-Updates (`docs/API.md`, `docs/RELEASE_NOTES.md`, `docs/help/backups.md`)
- Testmethoden-Erweiterung (`ApiDocumentationContractTests`)

umgesetzt.

---

## Änderungen an bestehenden Klassen

### `VideoWebPlayerBackupData` (Klasse)

**Datei:** `VideoWebPlayer/Services/Backups/VideoWebPlayerBackupData.cs`

- **Neue Einträge in `OptionalRestoreTables` (Zeile 23–36):**
  - `"PairedDevices"` — Geräte-Kopplung (optional, falls Alt-Backup vor #229)
  - `"PairingCodes"` — Pairing-Codes (optional, falls Alt-Backup vor #229)
  - `"RefreshTokens"` — Session-Renewal-Tokens (optional, falls Alt-Backup vor #232)

- **Neue Einträge in `OptionalRestoreColumns` (Zeile 38+):**
  - `"PairingCodes.Kind"` — Spalte aus Migration `20260924103134_AddPairingBootstrapAndRefreshTokens`
  - `"PairingCodes.TicketHash"` — Spalte aus Migration `20260924103134_AddPairingBootstrapAndRefreshTokens`

- **Neue Einträge in `OptionalRestoreIntDefaults` (Zeile 98+):**
  - `(nameof(ApplicationDbContext.PairingCodes), nameof(PairingCodes.Kind), 0)` — Default für fehlende `Kind`-Spalte in Alt-Backups
  - (`TicketHash` ist nullable und bleibt NULL; kein Default nötig)

### `docs/API.md` (Dokumentation)

**Datei:** `docs/API.md`

- **Neue Dokumentation der 17 fehlenden Playlist-Endpunkte:**

  **Abschnitt "Playlist Playback":**
  - `POST /api/playlists/{id}/play` — Wiedergabe starten
  - `POST /api/playlists/{id}/play/next` — nächster Titel (Parameter: `currentEntryId`)
  - `POST /api/playlists/{id}/play/previous` — vorheriger Titel (Parameter: `currentEntryId`)
  - `POST /api/playlists/{id}/play/advance` — vorspulen (Parameter: `seconds`)

  **Abschnitt "Playlist Cover Management":**
  - `GET /api/playlists/{id}/cover` — Cover abrufen
  - `POST /api/playlists/{id}/cover/upload` — Cover hochladen (Parameter: `file`, `cropLeft`, `cropTop`, `cropWidth`, `cropHeight`)
  - `POST /api/playlists/{id}/cover/regenerate` — Cover regenerieren (Parameter: ggf. `options`)
  - `POST /api/playlists/{id}/cover/preview` — Cover-Vorschau (Parameter: `cropLeft`, `cropTop`, `cropWidth`, `cropHeight`)
  - `DELETE /api/playlists/{id}/cover` — Cover löschen

  **Abschnitt "Playlist Sorting & Genres":**
  - `PATCH /api/playlists/{id}/sort-mode` — Sortiermodus ändern (Parameter: `mode`)
  - `PUT /api/playlists/{id}/genres` — Genres aktualisieren (Parameter: Genre-IDs)
  - `POST /api/playlists/{id}/genres/reset` — Genres zurücksetzen

  **Abschnitt "Playlist Entry Reordering":**
  - `PUT /api/playlists/{id}/entries/{entryId}/order` — Reihenfolge ändern
  - `POST /api/playlists/{id}/entries/batch-reorder` — Batch-Reordering
  - `GET /api/playlists/{id}/entries/max-sort-order` — max sort order abrufen
  - `POST /api/playlists/{id}/entries/{entryId}/move-to-beginning` — zum Anfang verschieben
  - `POST /api/playlists/{id}/entries/{entryId}/move-between` — zwischen Einträgen verschieben (Parameter: `previousEntryId`, `nextEntryId`)

- **Dokumentation der Session-Endpunkte:**
  - `POST /api/pairing/bootstrap` — QR-Bootstrap durchführen (Parameter: `ticket`; Response: JWT, Refresh-Token)
  - `POST /api/auth/refresh` — Token erneuern (Parameter: `refreshToken`; Response: neuer JWT, neuer Refresh-Token)
  - `POST /api/auth/logout` — Abmelden (Response: 204)

- **Query-Parameter `access_token` systematisch dokumentieren** — für jeden Endpunkt (Alternative zu Bearer-JWT)

- **Widerspruch mit `docs/help/playlists-api.md` auflösen** — Duplikation prüfen, ggf. nur Verweis auf API.md

- **Aktualisierungsdatum auf 2026-09-26 setzen**

### `docs/RELEASE_NOTES.md` (Dokumentation)

**Datei:** `docs/RELEASE_NOTES.md`

- **Neue deutsche Einträge unter "Neuerungen" (Zeile 76+), parallel zu englischen Zeilen 38–50:**
  - "QR-Kopplung vom Profil aus"
  - "Neue öffentliche API `POST /api/pairing/bootstrap`"
  - "Neue Session-API `POST /api/auth/refresh` und `POST /api/auth/logout`"
  - "Widerruf eines gekoppelten Geräts sperrt sofort alle Refresh-Tokens"
  - "Bootstrap-Tickets mit Ablaufzeit"
  - "Gerätekopplung für Client-Apps"
  - "Neue Admin-Seite `Geräte`"
  - "Neue öffentliche API `POST /api/pairing/exchange`"

- **Neue deutsche Einträge unter "Wichtige Hinweise vor dem Update" (Zeile 68+), parallel zu englischen Zeilen 12–13:**
  - "Datenbankschema: `dotnet ef database update` erforderlich"
  - "Neue Konfigurationsschlüssel: `Pairing:CodeLength`, `Pairing:CodeTtlMinutes`, `Pairing:BootstrapTicketTtlMinutes`, `Pairing:BootstrapMaxTicketsPerHour`, `Pairing:BootstrapAdminOnly`, `Auth:RefreshTokenTtlDays` (siehe `appsettings.json`)"
  - "Nach Restore aus einem älteren Backup sind gekoppelte Geräte abgemeldet"

### `docs/help/backups.md` (Dokumentation)

**Datei:** `docs/help/backups.md`

- **Neuer Hinweis im Abschnitt "Sicherungs- und Wiederherstellungsverfahren":**
  - Nach Restore aus einem Backup vor der Geräte-Kopplungs-Einführung (vor #229/#232) sind gekoppelte Geräte abgemeldet und müssen neu gekoppelt werden.

---

## Datenbankmigrationen

| Migrationsname | Betroffene Tabellen/Spalten | Beschreibung der Änderung |
|---|---|---|
| (Keine neuen erforderlich) | — | Die drei Tabellen `PairedDevices`, `PairingCodes`, `RefreshTokens` sind bereits durch Migration `20260924103134_AddPairingBootstrapAndRefreshTokens.cs` angelegt. Die zwei neuen Spalten `PairingCodes.Kind` und `PairingCodes.TicketHash` sind bereits durch diese Migration vorhanden. Keine neue Migration für A1–A3 nötig. |

---

## Validierungsregeln

| Feld / Objekt | Regel | Fehlerfall |
|---|---|---|
| (Keine neuen erforderlich) | — | Alle Validierungen bestehen bereits (z. B. Pairing-Code-Format, Bootstrap-Ticket-TTL, Playlist-Namen, Genreauswahl). |

---

## Konfigurationsänderungen

| Eintrag | Typ | Standardwert | Zweck |
|---|---|---|---|
| (Keine neuen erforderlich) | — | — | Alle Einträge (`Pairing:*`, `Auth:RefreshTokenTtlDays`, `Playlists:*`) sind bereits in `appsettings.json` vorhanden (aus #229, #232). A3 (Release Notes) dokumentiert diese Einträge für deutsche Benutzer. |

---

## Seiteneffekte und Risiken

- **A1/Backup-Restore:** Release Notes in aktuellem Stand behaupten, dass Alt-Backups wiederherstellbar sind — das stimmt derzeit nicht (F1). Nach Implementierung von A1 stimmt es wieder. Legacy-Backups (vor #229) sollen ohne Fehler wiederhergestellt werden.

- **A2/API-Dokumentation:** Vertragstest muss bei jedem Push/PR durchlaufen und scheitern, wenn eine geforderte Route aus `docs/API.md` aus dem Code entfernt wird (aktive Überwachung der Dokumentation).

- **A3/Release Notes:** Deutsche und englische Release Notes müssen struktur- und inhaltsgleich sein. Fehlende oder widersprüchliche Einträge werden bei manueller Review sichtbar.

---

## Umsetzungsreihenfolge

### Priorität Hoch (vor Push/PR zu staging) — A1, A2, A3

1. **A1a: Tabellen `PairedDevices`, `PairingCodes`, `RefreshTokens` in `OptionalRestoreTables` eintragen**
   - Voraussetzungen: `VideoWebPlayerBackupData.cs` existiert, Tabellen sind bereits in `ApplicationDbContext.cs` definiert
   - Beschreibung: Drei Einträge hinzufügen: `"PairedDevices"`, `"PairingCodes"`, `"RefreshTokens"` in der Liste `OptionalRestoreTables` (Zeile 23–36)

2. **A1b: Spalten `PairingCodes.Kind` und `PairingCodes.TicketHash` in `OptionalRestoreColumns` und Default in `OptionalRestoreIntDefaults` eintragen**
   - Voraussetzungen: Schritt A1a abgeschlossen
   - Beschreibung: 
     - `"PairingCodes.Kind"` in `OptionalRestoreColumns` hinzufügen
     - `"PairingCodes.TicketHash"` in `OptionalRestoreColumns` hinzufügen
     - `(nameof(ApplicationDbContext.PairingCodes), nameof(PairingCodes.Kind), 0)` in `OptionalRestoreIntDefaults` hinzufügen

3. **A1c: Vier Regressionstests für Alt-Backup-Restore schreiben**
   - Voraussetzungen: Schritte A1a und A1b abgeschlossen, Testklasse `VideoWebPlayerBackupDataTests.cs` existiert
   - Beschreibung: Vier neue Test-Methoden (nach Vorbild bestehender `ReadFromAsync_LegacyBackupWithout...`-Tests):
     - Test 1: Alt-Backup ohne `PairedDevices` → erfolgreich wiederhergestellt
     - Test 2: Alt-Backup ohne `PairingCodes` → erfolgreich wiederhergestellt
     - Test 3: Alt-Backup ohne `RefreshTokens` → erfolgreich wiederhergestellt
     - Test 4: Alt-Backup ohne alle drei → erfolgreich wiederhergestellt

4. **A2a: 17 fehlende Playlist-Endpunkte in `docs/API.md` dokumentieren**
   - Voraussetzungen: `docs/API.md` existiert, `PlaylistsController.cs` ist Quelle für Endpunkt-Details
   - Beschreibung: Vier neue Abschnitte mit Pfad, HTTP-Methode, Parametern, Response-Format, Fehlercodes:
     - "Playlist Playback" (4 Endpunkte: play, play/next, play/previous, play/advance)
     - "Playlist Cover Management" (5 Endpunkte: cover GET/POST/DELETE, preview, regenerate)
     - "Playlist Sorting & Genres" (3 Endpunkte: sort-mode, genres, genres/reset)
     - "Playlist Entry Reordering" (5 Endpunkte: order, batch-reorder, max-sort-order, move-to-beginning, move-between)

5. **A2b: Query-Parameter `access_token` in `docs/API.md` systematisch dokumentieren**
   - Voraussetzungen: Schritt A2a abgeschlossen
   - Beschreibung: Für jeden Endpunkt (oder in einer Einleitung) dokumentieren, dass `?access_token=<JWT>` als Alternative zu Bearer-JWT funktioniert

6. **A2c: Session-Endpunkte (`POST /api/pairing/bootstrap`, `POST /api/auth/refresh`, `POST /api/auth/logout`) dokumentieren**
   - Voraussetzungen: `docs/API.md` existiert, Endpunkte sind bereits im Code implementiert (aus #232)
   - Beschreibung: Drei neue Abschnitte mit Parametern, Response, Fehlercodes, Beispiele

7. **A2d: Vertragstest `ApiDocumentationContractTests` erweitern**
   - Voraussetzungen: Schritte A2a, A2b, A2c abgeschlossen, `VideoWebPlayer.Tests/ApiDocumentationContractTests.cs` existiert
   - Beschreibung: Neue Test-Methoden für Playlist-Routen und Session-Endpunkte
     - Test: Playlist erstellen → Eintrag hinzufügen → Wiedergabe starten → nächster Titel → Status 200/201/204
     - Test: Öffentliche Playlist, fremder Benutzer versucht Wiedergabe → 403 oder 404 (Akzeptanzkriterium von A2)
     - Test: Session-Endpunkte (bootstrap, refresh, logout) → Status 200/204
     - Test: Vertragstest schlägt fehl, wenn eine geforderte Route aus `docs/API.md` aus dem Code entfernt wird

8. **A2e: Widerspruch zwischen `docs/API.md` und `docs/help/playlists-api.md` auflösen**
   - Voraussetzungen: Schritte A2a–A2d abgeschlossen
   - Beschreibung: Prüfen, ob Duplikation notwendig ist oder ob nur Verweis auf API.md genügt; Konsistenz sicherstellen

9. **A3: Deutsche Release Notes ergänzen**
   - Voraussetzungen: `docs/RELEASE_NOTES.md` existiert, englische Einträge für QR-Bootstrap vorhanden (Zeilen 38–50)
   - Beschreibung: Acht neue deutsche Einträge unter "Neuerungen" + drei neue deutsche Einträge unter "Wichtige Hinweise vor dem Update"

10. **Dokumentation aktualisieren: `docs/help/backups.md` und `docs/API.md` Aktualisierungsdatum**
    - Voraussetzungen: Schritte A1c, A2d, A3 abgeschlossen
    - Beschreibung: Aktualisierungsdatum in allen aktualisierten Dateien auf 2026-09-26 setzen; Hinweis auf Geräte-Abmeldung nach Restore in `docs/help/backups.md` eintragen

---

## Tests

### Neue Tests

| Test / Hilfsmethode | Testklasse | Was wird geprüft / bereitgestellt? |
|---|---|---|
| `ReadFromAsync_LegacyBackupWithoutPairedDevices_RestoresSuccessfully` | `VideoWebPlayerBackupDataTests` | Alt-Backup ohne `PairedDevices` wird erfolgreich wiederhergestellt |
| `ReadFromAsync_LegacyBackupWithoutPairingCodes_RestoresSuccessfully` | `VideoWebPlayerBackupDataTests` | Alt-Backup ohne `PairingCodes` wird erfolgreich wiederhergestellt |
| `ReadFromAsync_LegacyBackupWithoutRefreshTokens_RestoresSuccessfully` | `VideoWebPlayerBackupDataTests` | Alt-Backup ohne `RefreshTokens` wird erfolgreich wiederhergestellt |
| `ReadFromAsync_LegacyBackupWithoutAllThreeDeviceTables_RestoresSuccessfully` | `VideoWebPlayerBackupDataTests` | Alt-Backup ohne alle drei Geräte-Tabellen wird erfolgreich wiederhergestellt |
| `PlaylistsAndSessionEndpoints_ContractTest` | `ApiDocumentationContractTests` | Playlist-Routen (Playback, Cover, Sorting, Reordering) und Session-Endpunkte sind dokumentiert und funktionsfähig; Test scheitert, wenn eine Route entfernt wird |
| `ApiDocumentationContractTest_MissingRoute_Fails` | `ApiDocumentationContractTests` | Vertragstest scheitert, wenn eine geforderte Route aus `docs/API.md` aus dem Code entfernt wird |

### Betroffene bestehende Tests

| Test / Testklasse | Grund der Anpassung |
|---|---|
| (Keine) | — |

### E2E-Tests (primärer Funktionsnachweis)

| Priorität | Szenario | Testdatei / Testklasse | Abgedecktes Akzeptanzkriterium | Warum E2E nötig ist |
|---|---|---|---|---|
| Pflicht | Alter Backup (vor #229/#232) wird erfolgreich wiederhergestellt | `VideoWebPlayerBackupDataTests` (Integrations- oder E2E-Test mit echtem Backup-Artefakt) | A1 "Alt-Backups ohne PairedDevices, PairingCodes, RefreshTokens sind wiederherstellbar" | Legacy-Backup-Kompatibilität ist ein Integrations-Testszenario; nur echte Backup-Datei und Wiederherstellung können den vollständigen Ablauf prüfen |
| Pflicht | Playlist-Endpunkte sind in `docs/API.md` dokumentiert und funktionsfähig | `ApiDocumentationContractTests.PlaylistsAndSessionEndpoints_ContractTest` | A2 "17 Playlist-Endpunkte + 3 Session-Endpunkte dokumentiert und funktioniert" | API-Vertragstest muss Dokumentation mit Live-Code synchron halten; nur echter Durchlauf (Create → Add → Play → Next) kann Endpunkt-Funktionalität und Dokumentation validieren |

Welche bestehenden E2E-Tests müssen angepasst werden?

| Test / Testklasse | Grund der Anpassung |
|---|---|
| (Keine) | — |

---

## Nicht Teil dieses Laufs

Die folgenden Anforderungen (A4–A9) sind **nicht** Bestandteil dieses Lifecycle-Laufs und bleiben im Issue #233 offen. Sie folgen in eigenen Läufen:

- **A4:** Verständliche Fehlerantworten der Medien-API (Statuscodes 403/404 statt 401/500)
- **A5:** Client-Bibliothek erweitern (Device-Token, Session-Erneuerung, neue Methoden)
- **A6:** E2E-Test: Gerät per QR-Bootstrap koppeln + Playlists nutzen
- **A7:** E2E-Test: Lokale Medienquelle + Playlist
- **A8:** Geräte-Dokumentation aktualisieren (6 Dateien unter `docs/help/geraete/`)
- **A9:** Flackernde Tests stabilisieren (Timeouts, Zustand-basiertes Warten)

---

## Zusammenfassung der Arbeitspakete für diesen Lauf

**Priorität Hoch (vor Push/PR zu staging):**
- A1: 3 Einträge in `OptionalRestoreTables` + 2 in `OptionalRestoreColumns`/`OptionalRestoreIntDefaults` + 4 Regressionstests = 3 Arbeitsschritte
- A2: 17 Endpunkte dokumentieren + Session-Endpunkte + Vertragstest = 5 Arbeitsschritte
- A3: Deutsche Release Notes ergänzen = 1 Arbeitsschritt
- Dokumentation aktualisieren = 1 Arbeitsschritt

**Gesamt für diesen Lauf: 10 Arbeitsschritte**

---

## Offene Punkte

Keine
