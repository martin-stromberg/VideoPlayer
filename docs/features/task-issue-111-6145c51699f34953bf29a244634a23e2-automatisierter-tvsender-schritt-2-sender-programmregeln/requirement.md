# Technische Anforderungsübersetzung: Sender und Programmregeln verwalten (Schritt 2)

**Quelle:** `docs/projects/task/issue-111-6145c51699f34953bf29a244634a23e2-automatisierter-tvsender/project-plan.md`, Abschnitt „### Schritt 2: Sender und Programmregeln verwalten"
**Branch:** `task-issue-111-6145c51699f34953bf29a244634a23e2-automatisierter-tvsender-schritt-2-sender-programmregeln`
**Basisbranch:** `task/issue-111-6145c51699f34953bf29a244634a23e2-automatisierter-tvsender`
**Erstellt:** 2026-10-21

---

## Fachliche Zusammenfassung

Ein Administrator verwaltet den automatischen TV-Sender: Er legt **Programmregeln** an, bearbeitet sie, aktiviert/deaktiviert sie und löscht sie. Der **Sender** wird als eigenes, benanntes Datenobjekt geführt (kanalfähig, ausgeliefert wird zunächst genau einer). Eine Programmregel beschreibt eine wiederkehrende Sendung mit Regelart, Wiederholungsmuster, Sendezeit und Länge bzw. Folgenanzahl, zugeordnetem Programmmaterial und Priorität. Die Auswahl des Programmmaterials erfolgt über Such-/Auswahldialoge auf dem sichtbaren Medienbestand — nicht über interne IDs. Die Verwaltung ist Administratoren vorbehalten und serverseitig abgesichert; neue Datenbestände erhalten Migration und Backup-Registrierung mit Alt-Backup-Regressionstest.

**Begriffswelt (verbindlich, Projektplan „Grobe Vorgehensentscheidungen"):** Sender, Programmregel, Sendeplatz, Sendeplan, Programmauskunft. Die Begriffe „Playlist" und „Playlist-Engine" werden für diese Funktion **nicht** verwendet — `Playlist`/`IPlaylistService` bezeichnen die bestehende Benutzer-Playlist-Funktionalität (Issue #207).

## Akzeptanzkriterien

1. **Sender als benanntes Objekt:** Es existiert eine Sender-Entität mit eigenem Namen; das Datenmodell ist kanalfähig (Regeln hängen am Sender, nicht global). Ausgeliefert und bedienbar ist genau ein Sender.
2. **Programmregel-CRUD:** Der Administrator kann Programmregeln anlegen, bearbeiten, aktivieren/deaktivieren und löschen.
3. **Regelinhalt:** Eine Regel enthält Regelart (Serien-Sendezeit, Film zur Primetime, Genre-/Formatrotation, Wiederholung, Feiertags-Special, Platzhalter/Füller), Wiederholungsmuster (täglich, wöchentlich an einem Wochentag, einmalig, an Feiertagen), gewünschte Sendezeit und Länge bzw. Anzahl der Folgen, Programmmaterial und Priorität.
4. **Programmmaterial-Auswahl (eigener Fachpunkt):** Je Regelart ist genau ein Programmmaterial auszuwählen — eine Serie, ein Film, eine Filmsammlung, ein Genre oder eine bestehende Benutzer-Playlist. Die Auswahl erfolgt über Such-/Auswahldialoge auf dem sichtbaren Medienbestand; der Administrator gibt niemals interne IDs ein.
5. **Berechtigung:** Die Regelverwaltung steht ausschließlich Administratoren zur Verfügung und ist serverseitig erzwungen (keine reine UI-Ausblendung).
6. **Persistenz & Backup:** Neue Tabellen erhalten eine EF-Migration und eine Registrierung in `VideoWebPlayerBackupData` (`OptionalRestoreTables`/`OptionalRestoreColumns`) inklusive Regressionstest, der ein Alt-Backup ohne die neuen Tabellen/Spalten wiederherstellt.
7. **Konfiguration:** Sender-bezogene Konfiguration (Bundesland für den Feiertagskalender) wird über den etablierten Einstellungsmechanismus (`Setup`-Tabelle + `ProgramSettingsService`) geführt.

## Betroffene Klassen und Komponenten

### Neue Datenmodellklassen (Entities)

- **`TVChannel`** (Sender, neu)
  - `Id` (long, PK), `Name` (string, RQ), `CreatedAt`, `UpdatedAt`
  - Navigation: `ProgramRules`
- **`ProgramRule`** (Programmregel, neu)
  - `Id` (long, PK), `TVChannelId` (FK → `TVChannel`)
  - `Name` (string, RQ)
  - `RuleType` (`ProgramRuleType`, RQ)
  - `Recurrence` (`ProgramRuleRecurrence`, RQ)
  - `DayOfWeek` (`DayOfWeek`?, nur bei wöchentlicher Wiederholung)
  - `SingleDate` (`DateOnly`?, nur bei einmaliger Wiederholung)
  - `TimeOfDay` (`TimeOnly`/TimeSpan, geplante Sendezeit)
  - `EpisodeCount` (int?, Anzahl Folgen für serienbezogene Regeln)
  - `SlotDuration` (`TimeSpan`?, gewünschte Länge des Sendeplatzes)
  - `MaterialType` (`ProgramRuleMaterialType`, RQ) + `MaterialId` (long) — polymorphe Materialreferenz analog `PlaylistEntry.MediaType`/`MediaId` (kein harter FK auf die polymorphen Medienentitäten)
  - `Priority` (int) — Vorrang bei Regelkonflikten (Auswertung in Schritt 3)
  - `IsActive` (bool), `CreatedAt`, `UpdatedAt`

### Neue Enums

- **`ProgramRuleType`**: `SeriesFixedTime`, `MoviePrimeTime`, `GenreRotation`, `Repeat`, `HolidaySpecial`, `Filler`
- **`ProgramRuleRecurrence`**: `Daily`, `Weekly`, `Once`, `Holiday`
- **`ProgramRuleMaterialType`**: `TVShow`, `Movie`, `MovieCollection`, `Genre`, `Playlist`

### Änderungen an bestehenden Klassen

- **`ApplicationDbContext`** — neue DbSets `TVChannels`, `ProgramRules`; Konfigurationen unter `Data/Configurations/`
- **`Setup`** — neue Spalte `HolidayFederalState` (Bundesland-Konfiguration für den Feiertagskalender, Entscheidung „Konfiguration über etablierte Einstellungsmechanismen")
- **`ProgramSettingsService`** — Lese-/Schreibmethoden für das Bundesland
- **`VideoWebPlayerBackupData`** — `TVChannels` + `ProgramRules` in `OptionalRestoreTables`; `Setups.HolidayFederalState` in `OptionalRestoreColumns`

### Logikklassen / Services

- **`IProgramRuleService` / `ProgramRuleService`** (neu, scoped)
  - CRUD für `ProgramRule` sowie `TVChannel` (Anlage/Lesen/Umbenennen des einen ausgelieferten Senders)
  - Jede Methode verlangt die aufrufende Benutzer-Id und erzwingt `ApplicationUser.IsAdmin` serverseitig (Muster: `PlaylistService.SetPlaylistPublicAsync` liest den Admin-Status aus dem Benutzerdatensatz, nicht aus dem Token-Claim)
  - Validierung: Regelart ↔ zulässige Materialart, genau ein Material, wöchentlich ↔ Wochentag gesetzt, einmalig ↔ Datum gesetzt, Sendezeit gesetzt, Priorität ganzzahlig
- **Sender-Einstellung Bundesland** über `ProgramSettingsService` (s. o.)

### UI-Komponenten

- **Neue Admin-Seite** `Components/Pages/Admin/TVSenderAdmin.razor` (`/admin/tv-sender`, `@rendermode InteractiveServer`, Muster `GenreAdmin.razor`): Sender-Anzeige/-Umbenennung, Regel-Liste, Anlegen-/Bearbeiten-Dialog, Aktivieren/Deaktivieren, Löschen mit Bestätigung (`ConfirmationDialog.razor`)
- **Auswahldialoge:** Wiederverwendung von `Components/Playlists/MediaSearchSelector.razor` für Serie/Film/Filmsammlung; Genre-Auswahl über `Client.RequestGenreOptionsAsync` (`GET /api/items/genres` → `DtoGenreOption`); Playlist-Auswahl über die Playlist-API (`api/playlists`, eigene + öffentliche Playlists)
- **`AdminIndex.razor`** — neue Kachel „TV-Sender"

### Tests

- **Unit-/Integrationstests (echtes SQLite, `PairingTestDb`):** `ProgramRuleServiceTests_*` — CRUD, Aktivieren/Deaktivieren, Validierung (Materialart je Regelart, Pflichtfelder je Wiederholungsmuster), Admin-Erzwingung (Nicht-Admin → `ForbiddenAccessException`), Kaskade `TVChannel` → `ProgramRule`
- **Backup-Regressionstest** in `VideoWebPlayerBackupDataTests` via `LegacyBackupArchiveBuilder` (Tabellen entfernt → Restore erfolgreich)
- **E2E (Playwright, Muster `ProgramSettingsE2ETests`):** Administrator legt eine Regel über den Dialog inkl. Mediensuche an, bearbeitet/deaktiviert/löscht sie; Nicht-Administrator sieht „Nicht autorisiert"

## Implementierungsansatz

1. **Datenmodell:** Neue Entities + Enums + EF-Konfigurationen (Muster `Playlist`/`PlaylistEntry`); polymorphe Materialreferenz als `MaterialType` + `MaterialId` ohne EF-FK auf Medienentitäten (bewährtes Muster aus `PlaylistEntry`); Kaskade `TVChannel` → `ProgramRule`. EF-Migration, automatisch angewendet via `app.MigrateDatabase()` in `Program.cs`.
2. **Service:** `ProgramRuleService` mit DB-basierter Admin-Prüfung; DI-Registrierung in `ServiceCollectionExtensions.AddVideoWebPlayerServices`.
3. **UI:** Admin-Seite im `admin-console`-Stil mit Such-/Auswahldialogen statt ID-Eingabe; Eintrag in `AdminIndex`.
4. **Backup:** Registrierung in `OptionalRestoreTables`/`OptionalRestoreColumns` + Alt-Backup-Regressionstest.
5. **Zugriffsregelung (Kontext):** „Nur vollständig freigegebene Inhalte" wird in Schritt 3 bei der Planung serverseitig erzwungen; in Schritt 2 erfolgt die Materialauswahl auf dem für den Administrator sichtbaren Medienbestand (etablierter Endpunkt `GET /api/items`).

## Konfiguration

- **`Setup.HolidayFederalState`** (string?, Bundesland-Code z. B. `DE-NW`) — Feiertagskalender-Konfiguration, Pflege über `ProgramSettingsService` und die Sender-Admin-Seite.

## Offene Fragen

Keine — die Vorgehensentscheidungen des Projektplans (Begriffswelt, Bundesland-Konfiguration, Zugriffsregelung, Kanalfähigkeit mit einem ausgelieferten Sender, Konfiguration über `Setup`/`ProgramSettingsService`) decken die fachlichen Klärungsbedarfe ab. Verbleibende Detailentscheidungen (zulässige Materialart je Regelart, Prioritäts-Semantik) werden als Designentscheidungen im Umsetzungsplan festgelegt.
