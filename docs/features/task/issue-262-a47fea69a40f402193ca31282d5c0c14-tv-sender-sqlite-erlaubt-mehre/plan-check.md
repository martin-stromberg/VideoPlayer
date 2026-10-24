# Plan-Gegenprüfung

## Ergebnis

**Status:** Plan vollständig

Zweite Prüfrunde. Beide Lücken aus `plan-check.1.md` sind im überarbeiteten Plan korrekt
geschlossen: (1) Der Testnachweis zur deterministischen Cursor-Auswahl ist eingearbeitet —
`SelectStrandCursor` als `internal static` Helfer (im Code verifiziert: `InternalsVisibleTo` in
`VideoWebPlayer.csproj` Z. 15, Präzedenz `StrandOrderKey` `internal static` Z. 939, `RuleState`
`private` nested Z. 860 → Helfer auf Klassenebene zwingend), zwei reine Unit-Tests inkl.
`Id`-Tiebreak sowie ein `DROP INDEX`-Integrationstest, der das geforderte
`PersistCursors`-Update-Verhalten am realen Service-Pfad nachweist; die Sabotageprobe wurde um
die Entfernung der Sortierkette erweitert. (2) Der `Migrate()`-Fallback ist jetzt ausführbar —
der Plan benennt explizit, dass `MigrateAsync(<Ziel>)` auf handgebautem Schema ohne befüllte
`__EFMigrationsHistory` nicht gangbar ist (alle Migrationen würden an `CREATE TABLE` scheitern),
und beschreibt zwei korrekte Alternativen: `IMigrator.GenerateScript(<Vorgänger>, <neu>)` +
`ExecuteSqlRaw` oder manuelles Befüllen der History-Tabelle (Liste via
`context.Database.GetMigrations()`).

Stichprobenhafte Verifikation der Plan-Fakten gegen den Code: genau drei
`FirstOrDefault(c => c.StrandKey == …)`-Stellen existieren (Z. 729 `RewindCursor`, Z. 921
`RuleState.FindCursor`, Z. 1471 `PersistCursors`) — alle im Plan adressiert; Signaturen
`IReadOnlyList<ProgramRuleCursor>` passend. `AddTvSenderUserAccess`
(`20261023055517`) ist aktuell die letzte Migration (Vorgänger-Annahme des Migrations-Tests
korrekt). `ProgramRuleCursor.Id` trägt `Sqlite:Autoincrement` (Migration Z. 33) — `Id`-Tiebreak
belastbar. `OptionalRestoreTables` Z. 42 / `OptionalRestoreColumns` Z. 94 verifiziert.
`ReadFromAsync`-Zeilenstruktur (Z. 303–316: `table.Name`, `List<Dictionary<string, JsonElement?>>
rows` vor `InsertRowAsync`) passt zum geplanten Dedup-Eingriff; `_logger` existiert (Z. 145).
`Program.cs:47 app.MigrateDatabase()` → `WebApplicationExtensions` Z. 23 verifiziert.
`LegacyBackupArchiveBuilder.RemoveColumnsAsync` existiert (Muster für `AppendRowsAsync`);
`ProgramScheduleTestDb.Db` ist `public` → `ExecuteSqlRaw("DROP INDEX …")` auf der
`EnsureCreated`-Test-DB ausführbar (Index-Name per `HasDatabaseName` fixiert).

## Abgleich Akzeptanzkriterien

| Akzeptanzkriterium | Umsetzung im Plan | Testnachweis im Plan | Status |
|--------------------|-------------------|----------------------|--------|
| 1. Bestandsaufnahme realer Datenbanken (Duplikat-Abfrage aus dem Issue) | Umsetzungsschritt 1: `*.db`-Suche, Issue-Abfrage ausführen, Ergebnis in `datenbestand.md` dokumentieren — inkl. dokumentiertem Ergebnis „kein realer Bestand prüfbar" | Dokumentationsartefakt statt Test; angemessen (Inventory: keine DB im Arbeitsverzeichnis) | Abgedeckt |
| 2. Duplikat-Bereinigung („jüngste Zeile gewinnt") | Datenmigration per `migrationBuilder.Sql` **vor** `CreateIndex`; Gewinner `UpdatedAt` DESC, Tiebreak `Id` DESC; `ROW_NUMBER() OVER (PARTITION BY …)` benannt | `Migration_MergesDuplicateNullStrandKeyRows_YoungestWins` (echtes SQLite, `IMigrator.MigrateAsync` bis Vorgänger `AddTvSenderUserAccess` → Duplikate per EF einfügen → `MigrateAsync()` auf latest → jüngste Zeile bleibt) | Abgedeckt |
| 3. Schutz vor neuen Duplikaten auf DB-Ebene | Gefilterter Unique-Index `HasIndex(c => c.ProgramRuleId).IsUnique().HasFilter("[StrandKey] IS NULL").HasDatabaseName("IX_ProgramRuleCursors_ProgramRuleId_NullStrandKey")`; Composite-Index bleibt für Nicht-NULL-Stränge | `ProgramRuleCursors_SecondNullStrandKeyRow_RejectedByUniqueIndex`, `…_DistinctStrandKeys_SameRule_Allowed`, `…_SameNonNullStrandKey_SameRule_StillRejected`, `Migration_NullStrandKeyUniqueIndex_RejectsDuplicateAfterUpgrade` (alle echtes SQLite) | Abgedeckt |
| 4. Deterministische Auswahl in allen `FirstOrDefault`-Zugriffen | Gemeinsamer `internal static` Helfer `SelectStrandCursor` (`UpdatedAt` DESC, `Id` DESC) für `FindCursor`, `RewindCursor`, `PersistCursors`; in `PersistCursors` wird definiert die Gewinner-Zeile aktualisiert | `SelectStrandCursor_NullStrandKeyDuplicates_ReturnsYoungest`, `SelectStrandCursor_TiedUpdatedAt_ReturnsHigherId` (Unit, In-Memory-Liste), `EnsureCoverage_NullStrandKeyDuplicates_PersistCursorsUpdatesWinner` (echtes SQLite, Duplikate via `DROP INDEX` hergestellt — Herstellbarkeit trotz Unique-Index gelöst) | Abgedeckt |
| 5. Dokumentation im Code | Kommentar an `ProgramRuleCursorConfiguration` Z. 27–29 und XML-Doku von `ProgramRuleCursor.StrandKey` (Z. 31–37) werden ans tatsächliche Verhalten angepasst | Kein Test nötig (Doku) | Abgedeckt |
| 6. Datenhaltung (EF-Migration, Backup-Registrierung wirksam, Alt-Backup-Test grün) | Migration `{Timestamp}_EnforceProgramRuleCursorNullStrandKeyUnique` (Dedup-SQL manuell vor `CreateIndex`; `Down` = `DropIndex`); `OptionalRestoreTables`/`OptionalRestoreColumns` unverändert (verifiziert Z. 42/94); Restore dedupliziert `NULL`-Zeilen vor dem Insert (offene Frage 3 entschieden: deduplizieren statt ablehnen — verhindert unbenutzbare Alt-Backups) | `ReadFromAsync_BackupWithDuplicateNullStrandKeyRows_KeepsYoungest`, `ReadFromAsync_LegacyBackupWithoutStrandKeyAndDuplicates_KeepsYoungest` (deckt den „Spalte fehlt"-Zweig ab), `AppendRowsAsync`-Hilfsmethode am `LegacyBackupArchiveBuilder` eingeplant; `ReadFromAsync_LegacyBackupWithoutStrandKeyColumn_RestoresSuccessfully` als unverändert-grüner Regressionstest benannt | Abgedeckt |
| 7. Tests gegen echtes SQLite | Index-Tests via `ProgramScheduleTestDb` (`SqliteConnection` + `EnsureCreated`), Migrations-Dedup via `IMigrator` auf echter SQLite-DB | Konkrete Testfälle benannt; Kern-Nachweis (zweite `NULL`-Zeile wird abgelehnt) vorhanden; Fallback-Strategie für `Migrate()`-Ersteinsatz dokumentiert | Abgedeckt |

## Fehlende oder unvollständige Testanforderungen

Keine.

## E2E-Abdeckung

| Benutzerfluss / Akzeptanzkriterium | Geplanter E2E-Test | Status |
|------------------------------------|--------------------|--------|
| Kein über UI/Nutzeraktion erreichbarer Fluss — rein datenbank-/service-seitige Änderung (Requirement: „UI/Controller: keine"; Duplikate entstehen nur über Migrations-, Restore- oder Parallel-Insert-Pfade) | Keine | Nicht erforderlich mit Begründung — der Plan begründet nachvollziehbar und verweist auf den direkteren Funktionsnachweis auf echter-SQLite-Ebene (Index-Erzwingung, tatsächlicher Migrationslauf, Restore) |

## Fehlende oder unvollständige Planbestandteile

Keine.

## Hinweise

- **`AppendRowsAsync`-Signatur:** `RemoveColumnsAsync` nimmt das `VideoWebPlayerBackupData`-Objekt
  (exportiert frisch aus der DB), keinen Stream. Für den kombinierten Alt-Backup-Fall
  (`ReadFromAsync_LegacyBackupWithoutStrandKeyAndDuplicates_KeepsYoungest`) sollte
  `AppendRowsAsync` einen `MemoryStream` annehmen/zurückgeben, damit sich beide Umbauten
  verketten lassen — z. B. `RemoveColumnsAsync(backup,…)` → Stream ohne `StrandKey`-Spalte →
  `AppendRowsAsync(stream,…)` fügt eine Zeile an, deren Row-Dict den `StrandKey`-Schlüssel gar
  nicht enthält. Alternativ umgekehrte Reihenfolge mit stream-basierter `RemoveColumns`-Variante.
  Reine Implementierungsentscheidung, kein Vollständigkeitsproblem.
- **`PersistCursors`-Aufrufstelle:** `rows` ist `IReadOnlyList<ProgramRuleCursor>?` (nullable via
  `GetValueOrDefault`, Z. 1467) — der Helfer-Aufruf braucht dort eine Null-Fallunterscheidung;
  trivial, Implementierungsdetail.
- **Vorgänger-Annahme:** `AddTvSenderUserAccess` ist zum Planungsstand die letzte Migration —
  kommen vor der Umsetzung weitere Migrationen auf den Branch, ist die Vorgänger-Id im
  Migrations-Test anzupassen (Plan formuliert korrekt „aktuell").
- **`GenerateScript`-Fallback:** das erzeugte Skript enthält `__EFMigrationsHistory`-DDL/Inserts
  (`CREATE TABLE IF NOT EXISTS`) und ist damit auf dem handgebauten Alt-Schema direkt
  ausführbar — der beschriebene Fallback ist praktikabel.
- **Positiv festgehalten:** Umsetzungsreihenfolge erzwingt das gemeinsame Ausliefern von
  Migration (Schritt 3) und Restore-Dedup (Schritt 5). AGENTS.md §7 ist vollständig abgedeckt:
  Debug- **und** Release-Build, volle Testsuite, `has-pending-model-changes`, Sabotageproben,
  Release-Notes-Eintrag, begründeter Hilfe-Doku-Verzicht, Snapshot-Kollisions-Regel (§3) als
  Risiko benannt. Alle fünf offenen Fragen der Anforderung sind entschieden und begründet
  (Variante A, `UpdatedAt`/`Id`-Gewinnerregel konsistent über Migration, Restore-Dedup und
  Lesepfade, Dedup statt Ablehnung, `IMigrator`-Ansatz, dokumentierte Bestandsaufnahme).
