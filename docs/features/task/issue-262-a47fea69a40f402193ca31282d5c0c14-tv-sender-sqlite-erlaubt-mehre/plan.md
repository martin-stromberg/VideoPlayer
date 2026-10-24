# Umsetzungsplan: Eindeutigkeit der `ProgramRuleCursor`-Zeilen mit `StrandKey = NULL` (Issue #262)

## Übersicht

`ProgramRuleCursors` erhält einen zusätzlichen gefilterten Unique-Index, der je `ProgramRuleId`
höchstens eine Zeile mit `StrandKey IS NULL` zulässt; eine Datenmigration führt vorhandene
`NULL`-Duplikate vor der Indexanlage zusammen („jüngste Zeile gewinnt"). Der Restore-Pfad
dedupliziert `NULL`-`StrandKey`-Zeilen in eingehenden Backups mit derselben Gewinner-Regel, und
die drei `FirstOrDefault`-Zugriffe in `ProgramScheduleService` (`FindCursor`, `RewindCursor`,
`PersistCursors`) wählen bei Mehrfachtreffern deterministisch dieselbe Gewinner-Zeile. Betroffen
sind ausschließlich Datenmodell, Migration, `ProgramScheduleService` und
`VideoWebPlayerBackupData` — keine UI, keine API, keine Konfiguration.

## Designentscheidungen

| Komponente / Bereich | Gewählter Ansatz | Begründung |
|----------------------|-----------------|------------|
| Schutz vor neuen `NULL`-Duplikaten | Gefilterter Unique-Index `HasIndex(c => c.ProgramRuleId).IsUnique().HasFilter("[StrandKey] IS NULL")` (Variante A aus dem Issue) | Einzige Variante mit DB-Garantie, die `StrandKey` nullable belässt — Backup-Registrierung (`OptionalRestoreColumns` → Wiederherstellung als `NULL`) und Alt-Backup-Semantik bleiben unverändert; `HasFilter`-Präzedenz besteht (`ContinueWatchingEntryConfiguration`, `WatchedEntryConfiguration`). Marker-Variante (B) bräuchte `NOT NULL`, einen neuen `OptionalRestoreStringDefaults`-Mechanismus für Alt-Backups und Kollisionssicherheit gegen `show:{id}`/`other`; Toleranz (C) ließe die vom Issue beanstandete Lücke ohne DB-Schutz |
| Gewinner-Kriterium beim Zusammenführen | `UpdatedAt` DESC, Tiebreak `Id` DESC | „Jüngste Zeile gewinnt" operationalisiert; `Id` (`Sqlite:Autoincrement`) ist der stabile Tiebreak bei gleichem `UpdatedAt`. Dieselbe Ordnung gilt in Datenmigration, Restore-Dedup und Lesepfaden, damit alle Stellen dieselbe Zeile als Gewinner bestimmen |
| Restore-Verhalten bei Alt-Backups mit Duplikaten | Deduplizieren statt ablehnen | Ein Backup mit `NULL`-Duplikaten (vor dem Fix erzeugt oder von einem Stand ohne Migration) würde am neuen Unique-Index den gesamten Restore per Rollback scheitern lassen — das Backup wäre unbenutzbar. Dedup mit derselben Gewinner-Regel ist konsistent zur Datenmigration und verwirft nur ohnehin inkonsistente Zeilen. Zeilen mit nicht-leerem `StrandKey` werden bewusst nicht angefasst (Composite-Index-Verstoß → Rollback wie bisher) |
| Deterministische Lesepfade | Gemeinsamer `internal static` Helfer `SelectStrandCursor` auf `ProgramScheduleService`: `Where(c => c.StrandKey == key)` → `OrderByDescending(c => c.UpdatedAt)` → `ThenByDescending(c => c.Id)` → `FirstOrDefault()` | Verteidigung in der Tiefe (vom Issue unabhängig von der Variante gefordert); identische Gewinner-Ordnung wie Migration/Restore → konsistente Wahl, sollten je wieder Duplikate in der geladenen Liste liegen. Ein Helfer statt drei abweichender Kopien. `internal` statt `private`, damit die Gewinner-Ordnung ohne Schema-Manipulation direkt aus Tests aufrufbar ist — Präzedenz `StrandOrderKey` (`internal static`, Z. 939), `InternalsVisibleTo` steht in `VideoWebPlayer.csproj` Z. 15. Der Helfer liegt als Member der äußeren Klasse, weil `RuleState` eine `private` Nested Class ist und ein `internal` Member darin nicht erreichbar wäre |
| Duplikate in Tests trotz Unique-Index | Zwei Ebenen: (a) `SelectStrandCursor` als `internal` direkt unit-testen (In-Memory-Liste, keine DB); (b) für den Service-Pfad den gefilterten Index auf der `EnsureCreated`-Test-DB per `ExecuteSqlRaw("DROP INDEX [IX_ProgramRuleCursors_ProgramRuleId_NullStrandKey]")` entfernen, Duplikate einfügen, dann `EnsureCoverageAsync` ausführen | Der gefilterte Index verbietet `NULL`-Duplikate auf Schemaebene — reine Service-Pfad-Tests könnten sie sonst gar nicht herstellen. Der direkte Helfer-Test prüft die Ordnung exakt inkl. `Id`-Tiebreak; der `DROP INDEX`-Test prüft das von Akzeptanzkriterium 4 geforderte `PersistCursors`-Verhalten am realen Pfad (die Gewinner-Zeile wird aktualisiert, nicht eine beliebige) |
| Migrationstestbarkeit | `IMigrator.MigrateAsync(<Vorgängermigration>)` → Duplikate einfügen → `MigrateAsync()` auf latest | Erster `Migrate()`-Einsatz im Testbestand, aber einziger Weg, die Datenmigration selbst zu prüfen — `EnsureCreated` baut das Schema aus dem Modell und überspringt `migrationBuilder.Sql`-Blöcke. Da die Vorgängermigration dem aktuellen Modell minus neuem Index entspricht, können die Duplikate nach dem Teilmigrationsstand normal über EF-Entitäten eingefügt werden. Korrigierter Fallback siehe „Seiteneffekte und Risiken" |
| Name des neuen Index | Explizit `IX_ProgramRuleCursors_ProgramRuleId_NullStrandKey` via `HasDatabaseName` | Der EF-Standardname `IX_ProgramRuleCursors_ProgramRuleId` wurde in `AddProgramRuleCursorStrandKey` für den alten, anders definierten Index gedroppt — ein neuer Name vermeidet Verwechslungen in der Migrationshistorie |

## Programmabläufe

### Schema-Schutz bei neuen Cursor-Schreibvorgängen

1. `PersistCursors` fügt bei fehlender Zeile wie bisher genau eine Zeile je
   `(ProgramRuleId, StrandKey)` ein; eine zweite `NULL`-Zeile je Regel wird jetzt von SQLite mit
   UNIQUE-Verletzung abgelehnt (sichtbare `DbUpdateException` statt stiller Inkonsistenz).
2. Nicht-`NULL`-Stränge bleiben wie bisher über den Composite-Index `(ProgramRuleId, StrandKey)`
   geschützt; der gefilterte Index greift ausschließlich bei `StrandKey IS NULL`.

Beteiligte Klassen/Komponenten: `ProgramRuleCursorConfiguration`, Tabelle `ProgramRuleCursors`,
`ProgramScheduleService.PersistCursors`.

### Datenmigration beim Update

1. `app.MigrateDatabase()` (Aufruf in `Program.cs` → `WebApplicationExtensions`) wendet beim
   Start die neue Migration an.
2. `Up`: zuerst `migrationBuilder.Sql` mit der Zusammenführung — je `ProgramRuleId` werden unter
   den `StrandKey IS NULL`-Zeilen alle bis auf die Gewinner-Zeile gelöscht (Gewinner = höchstes
   `UpdatedAt`, Tiebreak höchste `Id`; z. B. über `ROW_NUMBER() OVER (PARTITION BY ProgramRuleId
   ORDER BY UpdatedAt DESC, Id DESC)` — SQLite-Window-Functions stehen zur Verfügung).
3. Danach erst `CreateIndex` des gefilterten Unique-Index — kann nur erfolgreich anlegen, weil
   keine Duplikate mehr existieren.
4. `Down`: `DropIndex`; die Zusammenführung wird nicht rückgängig gemacht (irreversible
   Datenmigration, wie die bestehenden `migrationBuilder.Sql`-Präzedenzen).

Beteiligte Klassen/Komponenten: neue Migration, `ApplicationDbContextModelSnapshot`,
`WebApplicationExtensions.MigrateDatabase`.

### Restore mit Duplikat-Toleranz

1. `ReadFromAsync` liest je Tabelle die Backup-Zeilen (`ReadTablePayloadAsync`).
2. Für die Tabelle `ProgramRuleCursors` werden die eingelesenen Zeilen vor der Insert-Schleife
   reduziert: Zeilen mit fehlendem oder `null`-`StrandKey` (deckt sowohl Backups mit
   `StrandKey = null` als auch Alt-Backups ohne die Spalte ab) werden je `ProgramRuleId`
   gruppiert; pro Gruppe bleibt die Gewinner-Zeile (höchstes `UpdatedAt`, Tiebreak höchste
   `Id`), die übrigen werden verworfen und per `_logger` gezählt/protokolliert.
3. Die verbliebenen Zeilen laufen wie bisher durch `InsertRowAsync`/`DbCommand` — der neue
   Unique-Index kann nicht mehr verletzt werden.
4. Zeilen mit nicht-leerem `StrandKey` durchlaufen die Deduplizierung nicht; ein Verstoß gegen
   den Composite-Index führt weiterhin zum Rollback (unverändertes Verhalten).

Beteiligte Klassen/Komponenten: `VideoWebPlayerBackupData.ReadFromAsync`, neuer privater Helfer
(z. B. `DeduplicateNullStrandKeyCursorRows`).

### Deterministische Cursor-Auswahl

1. `EnsureCoverageAsync` (Z. 88–92) und `ReconcileAsync` (Z. 156–160) laden wie bisher alle
   Cursor-Zeilen gruppiert je `ProgramRuleId` — unverändert.
2. Neuer `internal static` Helfer `SelectStrandCursor(IReadOnlyList<ProgramRuleCursor>,
   string? key)` auf `ProgramScheduleService` (auf Klassenebene wie `StrandOrderKey`, da
   `RuleState` `private` nested ist): Treffermenge `Where(c => c.StrandKey == key)` →
   `OrderByDescending(c => c.UpdatedAt)` → `ThenByDescending(c => c.Id)` → `FirstOrDefault()`.
3. `RuleState.FindCursor` (Z. 920–921) delegiert an den Helfer; `RewindCursor` (Z. 729) und
   `PersistCursors` (Z. 1471) ersetzen ihr `FirstOrDefault` durch denselben Aufruf. In
   `PersistCursors` wird damit bei Mehrfachtreffern definiert die Gewinner-Zeile aktualisiert,
   nicht eine beliebige.

Beteiligte Klassen/Komponenten: `ProgramScheduleService` (`RuleState`, `RewindCursor`,
`PersistCursors`; indirekt `ExpandTrailerStatesAsync` über `SetItems`/`FindCursor`).

## Neue Klassen

Keine neuen Produktivklassen — die Änderungen sind eine Index-Konfiguration, interne/private
Helfer und eine Migration. Neue Testklassen siehe Abschnitt „Tests".

## Änderungen an bestehenden Klassen

### `ProgramRuleCursorConfiguration` (EF-Konfiguration)

- **Neuer Index:** `builder.HasIndex(c => c.ProgramRuleId).IsUnique().HasFilter("[StrandKey] IS
  NULL").HasDatabaseName("IX_ProgramRuleCursors_ProgramRuleId_NullStrandKey")` — erzwingt höchstens
  eine `NULL`-`StrandKey`-Zeile je `ProgramRuleId`.
- **Geänderter Kommentar** (Z. 27–29): beschreibt künftig, dass die Ein-Zeilen-Garantie für
  `NULL` per gefiltertem Index erzwungen wird (bisheriger Kommentar: „only one such row is ever
  written per rule" — traf nur auf den Schreibpfad, nicht auf die DB zu).

### `ProgramRuleCursor` (Datenmodellklasse)

- **Geänderte XML-Doku `StrandKey`:** ergänzt, dass die Datenbank je Regel höchstens eine
  `NULL`-Zeile erzwingt (gefilterter Unique-Index); `null` bleibt der Gesamt-Cursor
  nicht-interleaver Regeln und von Alt-Zeilen.

### `ProgramScheduleService` (Service)

- **Neue interne statische Methode:** `SelectStrandCursor(IReadOnlyList<ProgramRuleCursor>
  cursors, string? key)` — deterministische Trefferwahl (`UpdatedAt` DESC, `Id` DESC).
  `internal` statt `private`, damit Tests die Gewinner-Ordnung direkt prüfen können
  (`InternalsVisibleTo` in `VideoWebPlayer.csproj` Z. 15; Präzedenz `StrandOrderKey`,
  `internal static`, Z. 939).
- **Geänderte Methode `RuleState.FindCursor` (Z. 920–921):** delegiert an `SelectStrandCursor`.
- **Geänderte Methode `RewindCursor` (Z. 729):** `FirstOrDefault` → `SelectStrandCursor`.
- **Geänderte Methode `PersistCursors` (Z. 1471):** `FirstOrDefault` → `SelectStrandCursor`.

### `VideoWebPlayerBackupData` (Backup)

- **Neue private Methode:** `DeduplicateNullStrandKeyCursorRows(TableMetadata table,
  List<Dictionary<string, JsonElement?>> rows)` — gruppiert Zeilen ohne/mit `null`-`StrandKey`
  je `ProgramRuleId`, behält die Gewinner-Zeile (`UpdatedAt` DESC, `Id` DESC), liefert die
  reduzierte Liste (für andere Tabellen die Eingabe unverändert).
- **Geänderte Methode `ReadFromAsync` (Z. ~309):** wendet den Helfer auf `rows` an, wenn
  `table.Name` `ProgramRuleCursors` ist; die Anzahl verworfener Zeilen wird per
  `_logger.LogInformation` gemeldet.
- `OptionalRestoreTables`/`OptionalRestoreColumns` bleiben unverändert — `StrandKey` bleibt
  nullable, es ist kein String-Default-Mechanismus nötig.

### `ApplicationDbContextModelSnapshot`

- Durch `dotnet ef` regeneriert (neuer gefilterter Index).

## Datenbankmigrationen

| Migrationsname | Betroffene Tabellen/Spalten | Beschreibung der Änderung |
|----------------|----------------------------|---------------------------|
| `{Timestamp}_EnforceProgramRuleCursorNullStrandKeyUnique` (Arbeitstitel — endgültiger Name/Zeitstempel beim Scaffolden) | `ProgramRuleCursors` | `Up`: (a) `migrationBuilder.Sql`-Datenmigration — löscht je `ProgramRuleId` alle `StrandKey IS NULL`-Zeilen außer der jüngsten (`UpdatedAt` DESC, `Id` DESC); (b) `CreateIndex` `IX_ProgramRuleCursors_ProgramRuleId_NullStrandKey` auf `ProgramRuleId`, `unique: true`, `filter: "[StrandKey] IS NULL"`. `Down`: `DropIndex` (Zusammenführung nicht umkehrbar). **Wichtig:** Das Dedup-SQL ist nicht scaffold-bar und muss manuell vor den erzeugten `CreateIndex`-Aufruf gesetzt werden |

## Validierungsregeln

Keine — es gibt keine neuen oder geänderten Benutzereingaben.

## Konfigurationsänderungen

Keine.

## Seiteneffekte und Risiken

- **Restore-Verhalten ändert sich bewusst:** Backups mit `NULL`-Duplikaten werden künftig
  dedupliziert wiederhergestellt statt unverändert — gewollt und in den Release Notes zu nennen.
- **Fail-loud statt stiller Duplikate:** Ein theoretischer paralleler Insert zweier
  `NULL`-Zeilen schlägt jetzt mit `DbUpdateException` fehl statt unbemerkt Duplikate zu erzeugen.
  `ProgramScheduleWorker`-Läufe sind seriell, Restores laufen transaktional — das Risiko ist
  gering und das Verhalten gewünscht.
- **Migrationstest mit neuem Muster:** `IMigrator` ist das erste `Migrate()` im Testbestand.
  Falls historische Migrationen auf einer frischen SQLite-DB wider Erwarten nicht laufen, gilt
  als Fallback: Alt-Schema per SQL aufbauen und `IMigrator.GenerateScript(<Vorgänger-Id>,
  <neue-Migrations-Id>)` aufrufen — das erzeugte Skript enthält nur die neue Migration
  (Dedup-SQL + `CreateIndex`) und wird per `ExecuteSqlRaw` auf dem Alt-Schema ausgeführt.
  Nicht gangbar ist dagegen ein schlichtes `MigrateAsync(<Ziel>)` auf dem handgebauten
  Schema: `MigrateAsync` ermittelt über `__EFMigrationsHistory`, welche Migrationen
  ausstehen; ohne befüllte History-Tabelle würden **alle** Migrationen anlaufen und an
  `CREATE TABLE` bereits vorhandener Tabellen scheitern. Gleichwertige Alternative zum
  Skript: `__EFMigrationsHistory` manuell anlegen und mit den IDs aller
  Vorgängermigrationen befüllen (Liste via `context.Database.GetMigrations()`), sodass
  `MigrateAsync` nur noch die neue Migration anwendet.
- **Irreversibles `Down()`:** Gelöschte Duplikate werden beim Downgrade nicht wiederhergestellt
  — dokumentierter Datenverlust, entspricht den bisherigen Datenmigrationen.
- **Migrations-Kollisionen:** `ApplicationDbContextModelSnapshot.cs` kollidiert bei parallelen
  Schritten mit Migration — die Migration ist nach einem Rebase neu zu erzeugen statt
  Snapshot-Konflikte von Hand zu lösen (AGENTS.md §3).
- **Hilfe-Doku:** `docs/help/tv-sender.md` erwähnt Fortsetzungsstände nur als Anzeigeinhalt —
  es gibt keinen anwendersichtbaren Funktionswandel; Release-Notes-Eintrag ja, Hilfe-Update nein.

## Umsetzungsreihenfolge

1. **Bestandsaufnahme realer Bestände (Akzeptanzkriterium 1)**
   - Voraussetzungen: keine.
   - Beschreibung: `*.db`/`WebVideoPlayer.db` im Repo- und Datenverzeichnis suchen (laut
     Inventory liegt im Arbeitsverzeichnis keine Datenbank); gefundene Bestände mit der
     Issue-Abfrage `SELECT ProgramRuleId, COUNT(*) FROM ProgramRuleCursors WHERE StrandKey IS
     NULL GROUP BY ProgramRuleId HAVING COUNT(*) > 1` prüfen; das Ergebnis — auch „kein realer
     Bestand prüfbar" — im Feature-Ordner dokumentieren (z. B. `datenbestand.md`) und im
     Handover/Abnahmebericht nennen.

2. **Modell: gefilterter Index + Doku**
   - Voraussetzungen: keine.
   - Beschreibung: `ProgramRuleCursorConfiguration` — gefilterten Unique-Index mit explizitem
     Namen anlegen, Kommentar anpassen; `ProgramRuleCursor.StrandKey`-XML-Doku anpassen.

3. **EF-Migration**
   - Voraussetzungen: Schritt 2 (das Modell muss den Index enthalten); dotnet-ef vorhanden
     (Projektkonvention, zuletzt dotnet-ef 10.x).
   - Beschreibung: `dotnet ef migrations add EnforceProgramRuleCursorNullStrandKeyUnique
     --project VideoWebPlayer`; in `Up` das Dedup-`migrationBuilder.Sql` manuell **vor**
     `CreateIndex` setzen; `Down` = `DropIndex`; Snapshot-Diff und
     `has-pending-model-changes` prüfen.

4. **Deterministische Cursor-Auswahl**
   - Voraussetzungen: keine (fachlich unabhängig von Schritt 2–3, wird aber im selben
     Auslieferungsstand benötigt).
   - Beschreibung: `SelectStrandCursor` als `internal static` Member in
     `ProgramScheduleService` einführen (Klassenebene, nicht in `RuleState` — die Nested
     Class ist `private`); `FindCursor`, `RewindCursor`, `PersistCursors` umstellen.

5. **Restore-Deduplizierung**
   - Voraussetzungen: keine (muss aber zwingend gemeinsam mit Schritt 3 ausgeliefert werden —
     sonst scheitern Alt-Backups mit Duplikaten am neuen Index).
   - Beschreibung: `DeduplicateNullStrandKeyCursorRows` in `VideoWebPlayerBackupData` einführen,
     in `ReadFromAsync` für `ProgramRuleCursors` aufrufen, verworfene Zeilen loggen.

6. **Tests**
   - Voraussetzungen: Schritte 2–5; `ProgramScheduleTestDb` und `LegacyBackupArchiveBuilder`
     sind vorhanden; `SelectStrandCursor` muss `internal` sein (Schritt 4); für die
     Restore-Tests ist die neue Hilfsmethode `AppendRowsAsync` (Teil dieses Schritts) nötig.
   - Beschreibung: Index-Erzwingungstests gegen echtes SQLite (`ProgramScheduleTestDb`),
     Migrations-Dedup-Test via `IMigrator`, `SelectStrandCursor`-Unit-Tests
     (Gewinner-Ordnung inkl. `Id`-Tiebreak, In-Memory-Liste ohne DB), `DROP INDEX`-
     Integrationstest für das `PersistCursors`-Update-Verhalten sowie zwei
     Restore-Dedup-Tests über umgebaute Backup-Archive (mit `StrandKey`-Spalte und als
     Alt-Backup ohne Spalte).

7. **Dokumentation + Verifikation**
   - Voraussetzungen: Schritte 1–6.
   - Beschreibung: kurzer Eintrag in `docs/RELEASE_NOTES.md` (Robustheitsfix: Duplikat-
     Bereinigung, gefilterter Index, Restore dedupliziert Alt-Backups);
     `dotnet build VideoPlayer.sln` in Debug **und** Release; volle Testsuite
     `dotnet test VideoWebPlayer.Tests/VideoWebPlayer.Tests.csproj`;
     `dotnet ef migrations has-pending-model-changes` ohne Befund; Sabotageprobe der neuen
     Tests (Index-Definition entfernen → Index-Tests rot; Dedup-Aufruf im Restore entfernen →
     Restore-Tests rot; `OrderByDescending`/`ThenByDescending`-Kette in `SelectStrandCursor`
     entfernen → Auswahl-Tests rot).

## Tests

### Neue Tests

| Test / Hilfsmethode | Testklasse | Was wird geprüft / bereitgestellt? |
|--------------------|------------|-------------------------------------|
| `ProgramRuleCursors_SecondNullStrandKeyRow_RejectedByUniqueIndex` | `ProgramRuleCursorUniquenessTests` (neu; echtes SQLite via `ProgramScheduleTestDb`) | Regel + eine `NULL`-Zeile speicherbar; eine zweite `NULL`-Zeile derselben Regel schlägt beim Speichern mit `DbUpdateException` (UNIQUE) fehl |
| `ProgramRuleCursors_DistinctStrandKeys_SameRule_Allowed` | dto. | `NULL` + `show:{id}` + `other` koexistieren weiterhin je Regel |
| `ProgramRuleCursors_SameNonNullStrandKey_SameRule_StillRejected` | dto. | Regression: der Composite-Index lehnt einen doppelten Nicht-`NULL`-Strang weiterhin ab |
| `Migration_MergesDuplicateNullStrandKeyRows_YoungestWins` | `ProgramRuleCursorNullStrandKeyMigrationTests` (neu; echtes SQLite, `IMigrator`) | `MigrateAsync` bis zur Vorgängermigration der neuen Migration (aktuell `AddTvSenderUserAccess`) → Schema entspricht dem Modell ohne den neuen Index → Kanal, Regel und zwei `NULL`-Cursor-Zeilen mit unterschiedlichem `UpdatedAt`/`LastContentId` über EF einfügen → `MigrateAsync()` auf latest → genau eine Zeile bleibt, und zwar die jüngere (Assertion über `LastContentId`/`UpdatedAt`) |
| `Migration_NullStrandKeyUniqueIndex_RejectsDuplicateAfterUpgrade` | dto. (ggf. mit dem vorigen Test zusammenlegbar) | Nach `Migrate()` lehnt das Schema einen weiteren `NULL`-Insert derselben Regel ab |
| `AppendRowsAsync` (Hilfsmethode) | `LegacyBackupArchiveBuilder` | Erzeugt aus einem aktuellen Backup ein Archiv mit zusätzlichen Zeilen im Tabellen-Payload (Muster von `RemoveColumnsAsync`) — Voraussetzung für den Restore-Dedup-Test |
| `ReadFromAsync_BackupWithDuplicateNullStrandKeyRows_KeepsYoungest` | `VideoWebPlayerBackupDataTests_StrandKey` | Backup-Archiv mit zwei `NULL`-Zeilen derselben `ProgramRuleId` (ältere und jüngere `UpdatedAt`, unterscheidbare `LastContentId`) restauriert erfolgreich; danach existiert genau eine Zeile — die jüngere |
| `ReadFromAsync_LegacyBackupWithoutStrandKeyAndDuplicates_KeepsYoungest` | dto. | Zweiter Dedup-Fall — Alt-Backup **ohne** `StrandKey`-Spalte UND mit Duplikaten: `AppendRowsAsync` fügt eine zweite Zeile gleicher `ProgramRuleId` an (abweichendes `UpdatedAt`/`LastContentId`), `RemoveColumnsAsync` entfernt `StrandKey` aus allen Zeilen → beide Zeilen erhalten beim Restore `NULL`, der Helfer dedupliziert → genau die jüngere Zeile bleibt (deckt den „Spalte fehlt"-Zweig der Dedup-Bedingung ab) |
| `SelectStrandCursor_NullStrandKeyDuplicates_ReturnsYoungest` | `ProgramScheduleServiceTests_CursorSelection` (neu; reiner Unit-Test, keine DB) | `internal static` Helfer direkt aufrufen — Duplikate entstehen als In-Memory-`IReadOnlyList<ProgramRuleCursor>`: zwei `StrandKey = null`-Zeilen derselben Regel (älteres vs. jüngeres `UpdatedAt`, unterscheidbare `LastContentId`/`Id`) plus nicht-matchende `show:`-/`other`-Zeilen → Rückgabe ist die jüngere `NULL`-Zeile |
| `SelectStrandCursor_TiedUpdatedAt_ReturnsHigherId` | dto. | Zwei Duplikate mit identischem `UpdatedAt` → die Zeile mit der höheren `Id` gewinnt (Tiebreak-Ordnung deckungsgleich mit Migration und Restore-Dedup) |
| `EnsureCoverage_NullStrandKeyDuplicates_PersistCursorsUpdatesWinner` | dto. (echtes SQLite via `ProgramScheduleTestDb`) | Duplikate trotz Unique-Index herstellen: `ExecuteSqlRaw("DROP INDEX [IX_ProgramRuleCursors_ProgramRuleId_NullStrandKey]")` auf der `EnsureCreated`-Test-DB, dann zweite `NULL`-Zeile derselben Regel (älteres `UpdatedAt`, abweichende `LastContentId`) über EF einfügen → `EnsureCoverageAsync` ausführen → die jüngere Gewinner-Zeile wurde aktualisiert, die ältere Zeile bleibt unverändert — Nachweis des definierten `PersistCursors`-Update-Verhaltens am realen Service-Pfad (`FindCursor`/`RewindCursor` delegieren an denselben Helfer und sind durch die Unit-Tests plus die bestehenden Reconcile-/Interleaving-Tests abgedeckt) |

### Betroffene bestehende Tests

| Test / Testklasse | Grund der Anpassung |
|-------------------|---------------------|
| Keine | Alle geänderten Methoden sind privat; `ReadFromAsync` gewinnt nur Toleranz. `ReadFromAsync_LegacyBackupWithoutStrandKeyColumn_RestoresSuccessfully` muss **unverändert** grün bleiben — er ist der Regressionstest für die wirksame `OptionalRestoreColumns`-Registrierung und die `NULL`-Wiederherstellung |

### E2E-Tests (primärer Funktionsnachweis)

Keine — die Anforderung berührt keinen über UI oder Nutzeraktion erreichbaren Ablauf: Der Schutz
wirkt auf Datenbankebene, und Duplikate lassen sich über keinen Benutzerfluss erzeugen oder
beobachten (sie entstehen nur über Migrations-, Restore- oder parallele Insert-Pfade). Der
Funktionsnachweis liegt auf echter-SQLite-Ebene (Index-Erzwingung, tatsächlicher Migrationslauf,
Restore), was den betroffenen Mechanismus direkter prüft, als es ein UI-Test könnte.

Bestehende E2E-Tests müssen angepasst werden: „Keine."

## Offene Punkte

Keine — alle in der Anforderung offen gelassenen Fragen sind in den Designentscheidungen
getroffen: Variante A (gefilterter Unique-Index), Gewinner-Kriterium `UpdatedAt` DESC mit
Tiebreak `Id` DESC, Restore dedupliziert statt abzulehnen, Migrationstest via `IMigrator`. Die
Bestandsaufnahme realer Datenbanken (Akzeptanzkriterium 1) ist als Umsetzungsschritt 1
eingeplant; laut Inventory liegt im Arbeitsverzeichnis keine Datenbank — sollte kein Bestand
prüfbar sein, wird das dokumentiert und die generische Datenmigration trägt die Absicherung.
