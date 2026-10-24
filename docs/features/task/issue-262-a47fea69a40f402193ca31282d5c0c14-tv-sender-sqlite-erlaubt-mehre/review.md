# Plan-Review

## Ergebnis

**Status:** Vollständig umgesetzt

Geprüft gegen Commit `561476b` (`feat: NULL-StrandKey-Cursor-Eindeutigkeit absichern (Issue #262)`)
durch Lesen der Diffs und der relevanten Quelldateien, nicht nur des Selbstberichts. Die 11 neuen
Tests wurden selbst ausgeführt (11/11 grün); `dotnet ef migrations has-pending-model-changes` meldet
„No changes have been made to the model since the last migration."

## Umgesetzte Planelemente

- [x] Datenbestand-Aufnahme (Akzeptanzkriterium 1) — `datenbestand.md` im Feature-Ordner dokumentiert die Issue-Abfrage und das Ergebnis „kein realer Bestand prüfbar" (keine `WebVideoPlayer.db` im Repo-/Worktree-Bereich; produktive Bestände außerhalb des Rechtsraums)
- [x] Feld/Index `IX_ProgramRuleCursors_ProgramRuleId_NullStrandKey` in `ProgramRuleCursorConfiguration` (`VideoWebPlayer/Data/Configurations/ProgramRuleCursorConfiguration.cs` Z. 33–36) — `HasIndex(c => c.ProgramRuleId).IsUnique().HasDatabaseName(...).HasFilter("[StrandKey] IS NULL")` exakt wie geplant; Composite-Index `(ProgramRuleId, StrandKey)` unverändert erhalten
- [x] Geänderter Kommentar in `ProgramRuleCursorConfiguration` (Z. 27–32) — beschreibt jetzt die DB-erzwungene Ein-Zeilen-Garantie für `NULL` und die NULL-in-Unique-Index-Problematik
- [x] XML-Doku `ProgramRuleCursor.StrandKey` (`VideoWebPlayer/Data/ProgramRuleCursor.cs` Z. 30–39) — ergänzt um den gefilterten Unique-Index
- [x] EF-Migration `20261024072426_EnforceProgramRuleCursorNullStrandKeyUnique` — `Up`: `migrationBuilder.Sql`-Dedup **vor** `CreateIndex`, `ROW_NUMBER() OVER (PARTITION BY "ProgramRuleId" ORDER BY "UpdatedAt" DESC, "Id" DESC)` mit `rn > 1`-Löschung (Gewinner-Regel wie geplant); `Down`: nur `DropIndex`
- [x] `ApplicationDbContextModelSnapshot` — enthält den neuen gefilterten Index (Z. 1403–1406); `has-pending-model-changes` ohne Befund
- [x] Methode `SelectStrandCursor` in `ProgramScheduleService` (`VideoWebPlayer/Services/ProgramScheduleService.cs` Z. 957–962) — `internal static` auf Klassenebene (nicht in der `private` Nested Class `RuleState`), `Where(StrandKey == key)` → `OrderByDescending(UpdatedAt)` → `ThenByDescending(Id)` → `FirstOrDefault()`; `InternalsVisibleTo` in `VideoWebPlayer.csproj` Z. 15 vorhanden
- [x] `RuleState.FindCursor` (Z. 920–921) — delegiert an `SelectStrandCursor`
- [x] `RewindCursor` (Z. 729) — `FirstOrDefault` ersetzt durch `SelectStrandCursor`
- [x] `PersistCursors` (Z. 1481) — `FirstOrDefault` ersetzt durch `SelectStrandCursor` (null-sicher via `rows is null ? null : …`)
- [x] Methode `DeduplicateNullStrandKeyCursorRows` in `VideoWebPlayerBackupData` (`VideoWebPlayer/Services/Backups/VideoWebPlayerBackupData.cs` Z. 466–512) — privat, gruppiert Zeilen ohne/mit `null`-`StrandKey` je `ProgramRuleId`, Gewinner `UpdatedAt` DESC/`Id` DESC, gibt für andere Tabellen die Eingabe unverändert zurück; Aufruf in `ReadFromAsync` Z. 309 vor der Insert-Schleife; verworfene Zeilen per `_logger.LogInformation` gezählt
- [x] `OptionalRestoreTables`/`OptionalRestoreColumns` unverändert — `ProgramRuleCursors` (Z. 42) und `ProgramRuleCursors.StrandKey` (Z. 94) weiterhin registriert, kein String-Default-Mechanismus nötig (Variante A hält `StrandKey` nullable)
- [x] Testklasse `ProgramRuleCursorUniquenessTests` (`VideoWebPlayer.Tests/ProgramRuleCursorUniquenessTests.cs`, echtes SQLite via `ProgramScheduleTestDb`) — `ProgramRuleCursors_SecondNullStrandKeyRow_RejectedByUniqueIndex`, `ProgramRuleCursors_DistinctStrandKeys_SameRule_Allowed`, `ProgramRuleCursors_SameNonNullStrandKey_SameRule_StillRejected`; alle 3 grün
- [x] Testklasse `ProgramRuleCursorNullStrandKeyMigrationTests` (`VideoWebPlayer.Tests/ProgramRuleCursorNullStrandKeyMigrationTests.cs`, `IMigrator` auf echtem SQLite) — `Migration_MergesDuplicateNullStrandKeyRows_YoungestWins` und `Migration_NullStrandKeyUniqueIndex_RejectsDuplicateAfterUpgrade` gegen Vorgängermigration `20261023055517_AddTvSenderUserAccess`; beide grün, kein Fallback (`GenerateScript`/`__EFMigrationsHistory`-Befüllung) nötig
- [x] Testklasse `ProgramScheduleServiceTests_CursorSelection` (`VideoWebPlayer.Tests/ProgramScheduleServiceTests_CursorSelection.cs`) — `SelectStrandCursor_NullStrandKeyDuplicates_ReturnsYoungest`, `SelectStrandCursor_TiedUpdatedAt_ReturnsHigherId` (In-Memory-`IReadOnlyList`, direkter `internal`-Aufruf) sowie `EnsureCoverage_NullStrandKeyDuplicates_PersistCursorsUpdatesWinner` mit `ExecuteSqlRaw("DROP INDEX [IX_ProgramRuleCursors_ProgramRuleId_NullStrandKey]")` auf der `EnsureCreated`-Test-DB; alle 3 grün
- [x] Hilfsmethode `AppendRowsAsync` in `LegacyBackupArchiveBuilder` (`VideoWebPlayer.Tests/Services/Backups/LegacyBackupArchiveBuilder.cs`) — vorhanden, plus `MemoryStream`-Überladungen für `AppendRowsAsync`/`RemoveColumnsAsync` (Kombination der Archive-Rewrites, vom Plan implizit gefordert)
- [x] Restore-Test `ReadFromAsync_BackupWithDuplicateNullStrandKeyRows_KeepsYoungest` in `VideoWebPlayerBackupDataTests_StrandKey` — Duplikat-Archiv via `AppendRowsAsync`, Restore erfolgreich, nur die jüngere Zeile bleibt; grün
- [x] Restore-Test `ReadFromAsync_LegacyBackupWithoutStrandKeyAndDuplicates_KeepsYoungest` — `AppendRowsAsync` + `RemoveColumnsAsync` (Alt-Backup ohne `StrandKey`-Spalte mit Duplikaten); grün
- [x] Bestehender Regressionstest `ReadFromAsync_LegacyBackupWithoutStrandKeyColumn_RestoresSuccessfully` — unverändert erhalten und im selben Lauf grün
- [x] Release-Notes-Eintrag — `docs/RELEASE_NOTES.md` je ein Eintrag unter „What's New" und „Neuerungen" (Dedup, gefilterter Index, deterministische Lesepfade, Restore-Dedup, Migrationsname)
- [x] Keine neuen Produktivklassen, keine UI/API/Konfigurationsänderungen, keine Validierungsregeln — diff-bestätigt

## Hinweise

- `DeduplicateNullStrandKeyCursorRows` nutzt zwei kleine private statische Helfer `GetRowUpdatedAt`/`GetRowId` (Z. 504–512) zum Parsen der JSON-Werte — sinnvolle Zerlegung, vom Plan nicht ausdrücklich benannt, aber im Rahmen.
- Gruppierung der Restore-Zeilen erfolgt über den String des `ProgramRuleId`-Werts (`GetString` liefert für JSON-Zahlen `GetRawText()`) — konsistent innerhalb eines Payloads; Funktion geprüft, kein Plan-Verstoß.
- Sabotageproben (Task 15) sind im Tasks-File als durchgeführt vermerkt, wurden in diesem Review nicht wiederholt; die Testnachweise selbst wurden durch eigenen Testlauf (11/11 grün, Filter auf die vier betroffenen Klassen) verifiziert.
- `ExpandTrailerStatesAsync` benötigte keine gesonderte Änderung: der Pfad läuft über `SetItems`/`FindCursor`, das nun an `SelectStrandCursor` delegiert — wie im Plan vorgesehen („indirekt").
