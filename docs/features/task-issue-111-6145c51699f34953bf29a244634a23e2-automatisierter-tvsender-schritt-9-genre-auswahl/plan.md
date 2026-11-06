# Umsetzungsplan: Genre-Material — Mehrfachauswahl, Quell-Filter, Status/Alternativnamen (N1+N2+N3)

## Übersicht

Genre-Material von Programmregeln wird von der Einfachauswahl (`ProgramRule.MaterialId`) auf eine n:m-Beziehung (`ProgramRuleGenre`) umgestellt, erhält einen optionalen Quell-Filter (`ProgramRuleMediaSource`, leer = alle Quellen) und eine kanonische Genre-Auflösung, die `IsHidden`, `GenreName`-Alternativnamen und das saisonale `StartDate`/`EndDate`-Fenster berücksichtigt — sowohl in der Auswahlliste als auch in der Sendeplan-Expansion. Betroffen: Datenmodell + Migration, `ProgramRuleService`, `ProgramScheduleService.ExpandGenreAsync`, `TVSenderAdmin.razor`, Backup-Registrierung, Tests.

## Designentscheidungen

| Komponente / Bereich | Gewählter Ansatz | Begründung |
|----------------------|-----------------|------------|
| Genre-Speicherung | `ProgramRuleGenre(ProgramRuleId, GenreId)` mit echten FKs + Kaskade; gespeichert wird die **repräsentative Genre-Zeile** der Auswahl | Konsistent mit `MovieGenre`/`TVShowGenre`-Muster; FK/Kaskade gibt Löschverhalten; die Auflösung Namen→alle gleichnamigen/umgeleiteten Zeilen erfolgt zur Laufzeit, deckt neue gleichnamige Quellen-Genres automatisch ab. `ProgramRule.MaterialId` bleibt (required) und wird bei Genre-Regeln weiterhin mit der ersten gewählten Genre-Id befüllt — Kompatibilität für Alt-Daten/Backups. |
| Quell-Filter | `ProgramRuleMediaSource(ProgramRuleId, MediaSourceId)` **pro Regel**, nur bei `MaterialType.Genre` wirksam; leer = alle Quellen | Anforderung spricht von einem Filter je Regel („Genre Familie, aber nur aus Quelle A und B"); ein Filter pro Genre-Eintrag wäre ein komplexeres UI ohne nachgewiesenen Bedarf. |
| Genre-Optionsquelle | **Eigene sender-spezifische Quelle**: `IProgramRuleService.GetGenreMaterialOptionsAsync(userId, IReadOnlyCollection<long>? mediaSourceIds, ct)` — direkter Service-Aufruf aus `TVSenderAdmin` (serverseitig), `api/items/genres` bleibt unverändert | Die Optionen hängen vom gewählten Quell-Filter ab (quellspezifische Sichtbarkeit: „Family" versteckt auf 1/2, sichtbar auf 5/6) — eine quellenunabhängige flache Liste kann das nicht ausdrücken. Metadaten-Editor/Playlist-Editor brauchen Roh-Namen als Textvorschläge (andere Semantik); eine gemeinsame Änderung würde vier weitere Konsumenten ohne Anforderung verändern und birgt Regressionsrisiko. |
| Kanonische Auflösung | Eigene wiederverwendbare Logik in `ProgramRuleService` bzw. einer internen Hilfsklasse `GenreCanonicalResolver` (privat in `ProgramScheduleService` bzw. gemeinsam genutzte statische Hilfsklasse), Semantik wie `MediaSourceClassifier.GetOrCreateGenresAsync`: pro `MediaSourceId`, Genre-Zeile mit `GenreName`-Alternativnamen → rekursive Auflösung auf die benannten Zeilen derselben Quelle (Zyklenschutz); Zeile ohne Alternativnamen → kanonisch unter eigenem Namen. Sichtbar = `!IsHidden` && saisonales Fenster aktiv (Semantik wie `GenreService.GetSeasonalGenresAsync`: Fenster nur bei beiden Daten, jährlich rollierend) | Spiegelt exakt die Scan-Zuordnung: Inhalte auf versteckten Alt-Zeilen laufen auf das kanonische Genre auf; Verstecktes ohne Alternativnamen ist nirgends erreichbar. |
| Auswahlliste | Optionen = deduplizierte kanonische Namen aller **sichtbaren, kanonischen** Genre-Zeilen in den gewählten Quellen (leer = alle); `Id` = Id einer sichtbaren kanonischen Zeile (deterministisch: kleinste Id); zuvor gewählte, nach Quell-Einschränkung verschwundene Genres bleiben als Auswahl-Chips sichtbar (kein stiller Datenverlust) | Live-Befund erfordert quellabhängige Differenzierung; Chips verhindern implizites Verlieren von Auswahlen. |
| Expansion | `ExpandGenreAsync` expandiert die Vereinigungsmenge: kanonische Namen der gespeicherten Genre-Zeilen → alle Genre-Zeilen in den gefilterten Quellen, deren Auflösung auf diese Namen zeigt → Union der `MovieGenre`/`TVShowGenre`-Inhalte, dedupliziert nach `ContentType`+`ContentId`, Quell-Filter zusätzlich auf `Movie.MediaSourceId`/`TVShowEpisode.MediaSourceId` | Dedupe über die Union wie gefordert; Cursor-Semantik (`ContentType`,`ContentId` = Movie-/Episode-Id) unverändert → bestehende Cursor bleiben gültig. |
| Legacy-Fallback Expansion | Hat eine Genre-Regel **keine** `ProgramRuleGenres`-Zeilen, fällt die Expansion auf `ProgramRule.MaterialId` als einzelnes Genre zurück | Alt-Backup-Restore: Restores schreiben nur Daten in die bereits migrierte DB — ein Alt-Backup enthält `ProgramRules` mit `MaterialId`, aber keine Join-Zeilen (der Migrations-Backfill läuft nur beim Schema-Upgrade). Ohne Fallback wären restaurierte Genre-Regeln wirkungslos. Außerdem bleiben direkt geseedete Regeln (Tests, Alt-Daten) funktionsfähig. |
| `ProgramRuleInput` | Positional record um optionale `IReadOnlyList<long>? GenreIds` und `IReadOnlyList<long>? MediaSourceIds` am Ende erweitert (Defaults `null`) | Bestehende Aufrufer (Tests, Razor) kompilieren unverändert; Genre-Regeln liefern die Liste, andere Materialarten müssen leer/`null` bleiben (serverseitig abgelehnt). |

## Programmabläufe

### Regel mit Genre-Mehrfachauswahl + Quell-Filter speichern

1. `TVSenderAdmin` lädt Quellen (`DbContext.MediaSources`) und ruft `IProgramRuleService.GetGenreMaterialOptionsAsync(userId, selectedSourceIds)` — Liste kanonischer, sichtbarer Genres für den aktuellen Quell-Filter.
2. Admin aktiviert Genre-Checkboxen (setzt `MaterialType = Genre`) und optional Quell-Checkboxen; Quell-Änderung lädt die Optionen neu.
3. `SaveRuleAsync` baut `ProgramRuleInput` mit `MaterialId = erster Genre-Id`, `GenreIds`, `MediaSourceIds`.
4. `ProgramRuleService.ValidateRuleInputAsync` prüft serverseitig: Genre ⇒ `GenreIds` nicht leer, alle ids existieren, `MediaSourceIds` existieren; Nicht-Genre ⇒ beide Listen leer.
5. `CreateRuleAsync`/`UpdateRuleAsync` schreibt `ProgramRule` (inkl. `MaterialId`) und ersetzt die Join-Zeilen (`ProgramRuleGenres`, `ProgramRuleMediaSources`), feuert `IProgramScheduleSignal`.

### Sendeplan-Expansion (Genre)

1. `ExpandMaterialAsync` lädt für `MaterialType.Genre` die `ProgramRuleGenres`-Ids und den `ProgramRuleMediaSources`-Filter der Regel.
2. `ExpandGenreAsync` lädt alle Genre-Zeilen (+`AlternateNames`) der in-scope Quellen, berechnet pro Zeile die kanonischen Namen (Alternativnamen-Resolution, Zyklenschutz; unsichtbare Zeilen ohne Alternativnamen tragen keinen Namen bei). Fallback: ohne `ProgramRuleGenres`-Zeilen gilt `MaterialId` als einzelnes gespeichertes Genre (Alt-Backup-/Alt-Daten-Pfad).
3. Gespeicherte Genre-Zeilen → deren kanonische Namen; effektive Genre-Zeilen = in-scope Zeilen mit Namen ∩ Auswahl ≠ ∅.
4. `MovieGenres`/`TVShowGenres` der effektiven Zeilen → Filme/Episoden; Quell-Filter zusätzlich auf `MediaSourceId` der Inhalte; Dedupe; bisherige Sortierung; `TryMovieItem`/`ToItem` (Dauer + `AccessContext`) unverändert.
5. `RuleState`+`ProgramRuleCursor` arbeiten unverändert auf der deduplizierten Union.

### Genre-Optionsliste (N3)

1. Für die gewählten Quellen (leer = alle) alle `Genres` + `AlternateNames` laden.
2. Pro Zeile Auflösung: Alternativnamen vorhanden → rekursiv (gleiche Quelle, Zyklenschutz) auf Zielzeilen; sonst die Zeile selbst.
3. Optionen = Namen der **sichtbaren** Auflösungs-Endpunkte, dedupliziert (OrdinalIgnoreCase), `Id` = kleinste sichtbare kanonische Zeilen-Id je Name.

## Neue Klassen

| Klasse | Typ | Zweck |
|--------|-----|-------|
| `ProgramRuleGenre` | Datenmodellklasse | n:m Regel↔Genre (`VideoWebPlayer/Data/`) |
| `ProgramRuleMediaSource` | Datenmodellklasse | n:m Regel↔MediaSource Quell-Filter (`VideoWebPlayer/Data/`) |
| `ProgramRuleGenreConfiguration` | EF-Config | Composite-PK, FKs mit Kaskade |
| `ProgramRuleMediaSourceConfiguration` | EF-Config | Composite-PK, FKs mit Kaskade |
| `GenreVisibility` (interne Hilfsklasse) | Logikklasse (intern) | Kanonische Auflösung + Sichtbarkeit (Alternativnamen rekursiv, `IsHidden`, saisonales Fenster) — geteilt von Optionsquelle und Expansion |

## Änderungen an bestehenden Klassen

### `ProgramRule` (Datenmodell)
- **Neue Eigenschaften:** `Genres` (`ICollection<ProgramRuleGenre>`), `MediaSourceFilters` (`ICollection<ProgramRuleMediaSource>`) — Navigations für Include/Label.

### `ApplicationDbContext`
- **Neue DbSets:** `ProgramRuleGenres`, `ProgramRuleMediaSources`.

### `ProgramRuleInput` (record)
- **Neue Felder (positional, Default `null`):** `IReadOnlyList<long>? GenreIds`, `IReadOnlyList<long>? MediaSourceIds`.

### `ProgramRuleService` / `IProgramRuleService`
- **Neue Methode:** `GetGenreMaterialOptionsAsync(string userId, IReadOnlyCollection<long>? mediaSourceIds, CancellationToken)` → `Task<List<DtoGenreOption>>` (Admin-Check wie üblich).
- **Geänderte Methoden:** `ValidateRuleInputAsync` (Genre-Listenvalidierung, Quell-Existenz, Listen nur bei Genre); `CreateRuleAsync`/`UpdateRuleAsync` (Join-Zeilen ersetzen, `MaterialId` = erste Genre-Id); `GetRulesAsync` (`Include` der Joins für die Materialanzeige).

### `ProgramScheduleService`
- **Geänderte Methoden:** `ExpandMaterialAsync` (lädt Genre-Ids + Quell-Filter); `ExpandGenreAsync` (Vereinigung + Auflösung + Quell-Filter + Dedupe); Regel-Ladequeries um `Include` bzw. separate Join-Loads ergänzt.

### `TVSenderAdmin.razor`
- Genre-Einfach-`<select>` (`rule-material-genre`) → Mehrfachauswahl (Checkbox-Liste `rule-material-genres` + Auswahl-Chips) nur wenn `Genre` in `AllowedMaterialTypes`; Quell-Mehrfachauswahl (`rule-material-sources`, Checkboxen) sichtbar bei Genre-Material; `RuleEditModel` um `SelectedGenreIds`/`SelectedMediaSourceIds` erweitert; `MaterialLabel` zeigt alle Genre-Namen; `StartEdit` lädt bestehende Auswahl aus den Joins mit `MaterialId`-Fallback für Alt-Regeln ohne Join-Zeilen.

### `VideoWebPlayerBackupData`
- `OptionalRestoreTables` += `ProgramRuleGenres`, `ProgramRuleMediaSources`.

## Datenbankmigrationen

| Migrationsname | Betroffene Tabellen/Spalten | Beschreibung der Änderung |
|----------------|----------------------------|---------------------------|
| `AddProgramRuleGenreAndSourceLinks` | `ProgramRuleGenres`, `ProgramRuleMediaSources` | Beide Join-Tabellen (Composite-PK, FK→`ProgramRules`/`Genres`/`MediaSources` mit Kaskade) + `migrationBuilder.Sql`-Backfill `INSERT INTO ProgramRuleGenres SELECT Id, MaterialId FROM ProgramRules WHERE MaterialType=3 AND MaterialId IN (SELECT Id FROM Genres)` |

## Validierungsregeln

| Feld / Objekt | Regel | Fehlerfall |
|---------------|-------|------------|
| `GenreIds` | Bei `MaterialType.Genre` ≥ 1 Eintrag, alle in `Genres` vorhanden | `ArgumentException` / `KeyNotFoundException` |
| `GenreIds`/`MediaSourceIds` | Bei `MaterialType != Genre` müssen beide leer sein | `ArgumentException` |
| `MediaSourceIds` | Alle ids in `MediaSources` vorhanden; leer = uneingeschränkt | `KeyNotFoundException` |
| Admin-Rechte | `RequireAdminAsync` auch für `GetGenreMaterialOptionsAsync` | `ForbiddenAccessException` |

## Konfigurationsänderungen

Keine.

## Seiteneffekte und Risiken

- **`api/items/genres` bleibt unverändert** — Metadaten-Editor/Playlist-Editor unberührt (bewusste Entscheidung, s. o.).
- Bestehende Genre-Regeln behalten über Backfill identisches Verhalten (eine Join-Zeile = bisheriges `MaterialId`).
- `ProgramRule.MaterialId` bleibt bei Genre befüllt (erste Genre-Id) — Alt-Leser bleiben kompatibel.
- `Genre.cs` ist **nicht UTF-8** — Datei wird nicht angefasst.
- Cursor-Semantik: schrumpft die Expansion (z. B. durch neuen Quell-Filter), springt `NextIndexFor` auf 0 — bekanntes Verhalten bei Materialänderung, unverändert.
- Regellöschung kaskadiert die Join-Zeilen; Genre-/Quellen-Löschung entfernt Verknüpfungen (regel wird ggf. „verwaist" — bekanntes Muster).

## Umsetzungsreihenfolge

1. **Datenmodell + EF-Config + DbSets**
   - Voraussetzungen: keine
   - `ProgramRuleGenre`, `ProgramRuleMediaSource`, Konfigurationen, `DbSet`s, Navigationen auf `ProgramRule`.
2. **Migration**
   - Voraussetzungen: Schritt 1
   - `dotnet ef migrations add AddProgramRuleGenreAndSourceLinks` + Backfill-SQL.
3. **Kanonische Genre-Auflösung (Hilfslogik)**
   - Voraussetzungen: keine
   - Auflösung Alternativnamen/Sichtbarkeit/saisonales Fenster, quellbezogen, Zyklenschutz.
4. **`ProgramRuleService` + `IProgramRuleService` + `ProgramRuleInput`**
   - Voraussetzungen: Schritte 1+3
   - Input-Felder, Validierung, CRUD-Join-Handling, `GetGenreMaterialOptionsAsync`, `GetRulesAsync`-Include.
5. **`ProgramScheduleService.ExpandGenreAsync`**
   - Voraussetzungen: Schritte 1+3
   - Union-Expansion mit Auflösung, Quell-Filter, Dedupe.
6. **`TVSenderAdmin.razor`**
   - Voraussetzungen: Schritt 4
   - Mehrfachauswahl Genres (abhängig vom Quell-Filter), Quell-Checkboxen, Chips, Labels, Edit-Laden.
7. **Backup-Registrierung**
   - Voraussetzungen: Schritt 1
   - `OptionalRestoreTables` ergänzen.
8. **Tests**
   - Voraussetzungen: Schritte 1–7
   - Unit/Integrations-/E2E-/Alt-Backup-Tests (s. u.).
9. **Dokumentation** (`docs/help/tv-sender.md`, README, `docs/RELEASE_NOTES.md`)
   - Voraussetzungen: Implementierung abgeschlossen

## Tests

### Neue Tests

| Test / Hilfsmethode | Testklasse | Was wird geprüft / bereitgestellt? |
|--------------------|------------|-------------------------------------|
| `ProgramScheduleTestDb.CreateGenreAsync` (Hilfsmethode) | Helpers | Genre-Zeile je Quelle, optional `IsHidden`/`AlternateNames`/`StartDate`/`EndDate`, Verknüpfung zu Movie/Show |
| Genre-Union dedupliziert | `ProgramScheduleServiceTests_Generation` (oder neue `..._Genres`) | Film in 2 gewählten Genres → genau ein Slot; Cursor läuft über Union |
| Quell-Filter leer = alle / gesetzt = eingeschränkt | Genre-Tests | Items nur aus gewählten Quellen; leerer Filter = bisheriges Verhalten |
| Verstecktes Genre mit Alternativname | Genre-Tests | Items der versteckten Zeile landen beim kanonischen Genre in der Expansion |
| Verstecktes Genre ohne Alternativname | Genre-Tests | dessen Inhalte unerreichbar |
| Saisonales Fenster inaktiv | Genre-Tests | außerhalb des Fensters = nicht wählbar/expansion-leer |
| Quell-Differenzierung (Live-Befund) | `GetGenreMaterialOptionsAsync`-Tests | „Family" sichtbar auf Quelle B, versteckt→„Familie" auf Quelle A → Optionen je Quell-Filter korrekt |
| Validierung Genre/Quellen | `ProgramRuleServiceTests_Validation` | Genre ohne `GenreIds` → Fehler; `MediaSourceIds` bei Nicht-Genre → Fehler; unbekannte ids → `KeyNotFoundException` |
| CRUD-Roundtrip Joins | `ProgramRuleServiceTests_Crud` | Create/Update ersetzt Join-Zeilen; Delete kaskadiert |
| FK/Kaskade gegen echtes SQLite | neue EF-Tests | Regel-/Genre-/Quellen-Löschung entfernt Join-Zeilen |
| Alt-Backup-Regression | `VideoWebPlayerBackupDataTests_*` | Backup ohne `ProgramRuleGenres`/`ProgramRuleMediaSources` restaurierbar |
| Options-Admin-Gate | `ProgramRuleServiceTests_Authorization` | Nicht-Admin → `ForbiddenAccessException` |

### Betroffene bestehende Tests

| Test / Testklasse | Grund der Anpassung |
|-------------------|---------------------|
| `ProgramRuleServiceTests_*` (`ProgramRuleInput`-Aufrufe) | Record-Erweiterung — kompilierbar dank Defaults; Genre-Fälle müssen `GenreIds` liefern (sonst schlägt die neue Genre-Pflicht) |
| `TVSenderAdminE2ETests` | Genre-`<select>` → Checkboxen (Selektoren) |
| `ProgramScheduleServiceTests_*` mit Genre-Regeln | `CreateRuleAsync`-Hilfsmethode muss Join-Zeilen schreiben bzw. Regelpfad anpassen |

### E2E-Tests (primärer Funktionsnachweis)

| Priorität | Szenario | Testdatei / Testklasse | Abgedecktes Akzeptanzkriterium | Warum E2E nötig ist |
|-----------|----------|------------------------|-------------------------------|-------------------|
| Pflicht | Admin legt Regel mit **mehreren Genres** + **Quell-Einschränkung** an, editiert und löscht sie; verstecktes Genre erscheint nicht in der Auswahl | `TVSenderAdminE2ETests` (Erweiterung des bestehenden konsolidierten Tests `Admin_CreateEditDeactivateDeleteRule` bzw. ein zweiter konsolidierter Test für Genre) | N1 Mehrfachauswahl, N2 Quell-Filter, N3 Auswahlliste | Benutzerfluss im Admin-Formular; Service-Tests decken UI-Bindung nicht ab |

Bestehende E2E-Anpassung: `rule-material-genre`-Selektor in `TVSenderAdminE2ETests` (falls referenziert) → Checkbox-Selektoren.

## Offene Punkte

Keine.
