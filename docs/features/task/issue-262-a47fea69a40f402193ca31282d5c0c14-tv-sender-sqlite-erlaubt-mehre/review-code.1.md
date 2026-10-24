# Code-Review

## Ergebnis

**Status:** Befunde vorhanden

Zwei Befunde, beide niedriger Schweregrad — keiner beeinträchtigt die Korrektheit der
Implementierung selbst. Migrations-SQL, `SelectStrandCursor`-Ordnung, Restore-Dedup und
Index-Konfiguration wurden zudem direkt verifiziert (Details siehe Abschnitt
„Verifikationsnachweise" am Ende).

## Befunde

### VideoWebPlayerBackupDataTests_StrandKey.cs / DeduplicateNullStrandKeyCursorRows (VideoWebPlayerBackupData)

- **Testqualität (fehlende Abdeckung)** — Der Pass-Through-Zweig für Zeilen mit nicht-leerem
  `StrandKey` in `DeduplicateNullStrandKeyCursorRows` (`VideoWebPlayerBackupData.cs` Z. 477–478)
  ist ungetestet. Sabotageprobe bestätigt das: Wird die Bedingung
  `GetString(row, nameof(ProgramRuleCursor.StrandKey)) is not null` entfernt — die Dedup
  gruppiert dann **alle** Zeilen je `ProgramRuleId` und würde `show:`/`other`-Strangzeilen
  einer Regel beim Restore zerstören — bleiben alle 109 Backup-Tests grün, weil kein Test
  ein Backup mit mehreren unterschiedlichen `StrandKey`-Zeilen einer Regel restauriert.
  Der Kommentar an Z. 463–465 dokumentiert das Pass-Through-Verhalten (inkl. „Composite-
  Index-Verstoß → Rollback wie bisher") ausdrücklich als beabsichtigt, ohne dass ein Test
  es absichert.

  Empfehlung: Test ergänzen, der ein Backup restauriert, in dem eine Regel sowohl eine
  `NULL`- als auch Nicht-`NULL`-`StrandKey`-Zeilen besitzt (z. B. `show:1` und `other`),
  und alle Zeilen überleben — etwa in `VideoWebPlayerBackupDataTests_StrandKey` über
  `SeedRuleWithNullStrandCursorAsync` plus zusätzlicher `show:1`-Zeile. Optional zusätzlich:
  Backup mit zwei Zeilen gleicher `(ProgramRuleId, StrandKey = "show:1")` → Restore schlägt
  weiterhin fehl (das dokumentierte „Rollback wie bisher").

### LegacyBackupArchiveBuilder.cs (LegacyBackupArchiveBuilder)

- **Doppelter Code** — `RemoveColumnsAsync(MemoryStream, …)` (Z. 165–221) und
  `AppendRowsAsync(MemoryStream, …)` (Z. 254–299) sind zu ~90 % identisch: beide lesen
  `index.json`, suchen Tabelle/`entryName`, bauen ein neues Zip-Archiv mit geschriebenem
  Index, kopieren nicht betroffene Einträge und schreiben den mutierten Tabellen-Payload.
  Unterschiede sind nur die Index-Mutation (Spaltenliste kürzen) und die Zeilen-Mutation
  (`row.AsObject().Remove` vs. `payloadRows.Add`).

  Empfehlung: Gemeinsames Archiv-Umbau-Gerüst in einen privaten Helfer auslagern, dem die
  abweichenden Mutationen als Callbacks übergeben werden, z. B.
  `RewriteTablePayloadAsync(MemoryStream archiveStream, string tableName,
  Action<JsonObject>? mutateTableIndex, Action<JsonArray> mutateRows, CancellationToken ct)`
  — beide öffentlichen Methoden reduzieren sich dann auf ~5 Zeilen.

## Verifikationsnachweise

Zusätzlich zur Kriterienprüfung wurden die zentralen Korrektheitsfragen direkt verifiziert:

- **Dedup-SQL der Migration** (`20261024072426_EnforceProgramRuleCursorNullStrandKeyUnique.cs`
  Z. 17–32): `ROW_NUMBER() OVER (PARTITION BY "ProgramRuleId" ORDER BY "UpdatedAt" DESC,
  "Id" DESC)`, Löschung `rn > 1` nur unter `WHERE "StrandKey" IS NULL` — korrekt (jüngste
  Zeile gewinnt, `Id` als stabiler Tiebreak; `Id` ist PK, daher keine Gleichstände offen).
  Window Functions stehen in SQLite seit 3.25 zur Verfügung. Sabotageprobe (`DESC`→`ASC`)
  schlägt `Migration_MergesDuplicateNullStrandKeyRows_YoungestWins` fehl — Ordnung ist
  abgesichert.
- **`SelectStrandCursor`** (`ProgramScheduleService.cs` Z. 957–962): `UpdatedAt` DESC,
  `Id` DESC — deckungsgleich mit Migration und Restore-Dedup. Alle drei Lesepfade
  (`RuleState.FindCursor`, `RewindCursor`, `PersistCursors`) nutzen den Helfer. Sabotageprobe
  (Ordnung entfernt) lässt alle 3 CursorSelection-Tests fehlschlagen.
- **Restore-Dedup**: Edge Cases geprüft — Backup ohne `StrandKey`-Spalte wird als NULL
  gewertet (durch `ReadFromAsync_LegacyBackupWithoutStrandKeyAndDuplicates_KeepsYoungest`
  abgedeckt), fehlende/ungültige `UpdatedAt`/`Id` fallen auf `MinValue` zurück (sichere,
  deterministische Richtung; stabiles `OrderBy`), fehlende `ProgramRuleId` passiert
  unverändert und scheitert wie bisher am Insert. Sabotageprobe (Dedup-Aufruf entfernt)
  lässt beide Dedup-Tests am UNIQUE-Verstoß des gefilterten Index fehlschlagen — beweist
  zugleich, dass der Index auf der `EnsureCreated`-Test-DB existiert und wirkt.
- **Kleinigkeit (kein Befund):** Gruppierung der Restore-Dedup erfolgt über den
  JSON-Rohtext von `ProgramRuleId` — abweichende Schreibweisen (`5.0` vs. `5`) kämen nur
  in handeditierten Backups vor; die Fehlerrichtung bleibt sicher (Unique-Verstoß → Rollback
  wie bisher).
- **Definition of Done (AGENTS.md §7):** `dotnet build VideoPlayer.sln` Debug **und** Release
  fehlerfrei; `dotnet ef migrations has-pending-model-changes` ohne Befund; Unique-Index-/
  FK-Tests laufen gegen echtes SQLite (`Microsoft.Data.Sqlite`, `IMigrator`,
  `ProgramScheduleTestDb`), nicht InMemory; keine neue Tabelle/Spalte, daher keine neuen
  `OptionalRestore*`-Einträge nötig (`ProgramRuleCursors.StrandKey` war bereits
  registriert, Z. 94); Release Notes in beiden Sprachen ergänzt; Hilfe-Doku-Update im Plan
  begründet nicht erforderlich (kein anwendersichtbarer Funktionswandel). Die vollständige
  Testsuite ist Bestandteil des noch offenen todo-Schritts 10 — in diesem Review wurden die
  11 neuen Tests sowie alle 109 Backup-Tests ausgeführt (grün) und drei Sabotageproben
  durchgeführt (jeweils erwartete Fehlschläge, anschließend zurückgebaut und erneut grün
  verifiziert).

## Geprüfte Dateien

Liste aller geprüften Dateien:
- `VideoWebPlayer/Data/Configurations/ProgramRuleCursorConfiguration.cs`
- `VideoWebPlayer/Data/ProgramRuleCursor.cs`
- `VideoWebPlayer/Migrations/20261024072426_EnforceProgramRuleCursorNullStrandKeyUnique.cs`
- `VideoWebPlayer/Migrations/20261024072426_EnforceProgramRuleCursorNullStrandKeyUnique.Designer.cs`
- `VideoWebPlayer/Migrations/ApplicationDbContextModelSnapshot.cs`
- `VideoWebPlayer/Services/Backups/VideoWebPlayerBackupData.cs`
- `VideoWebPlayer/Services/ProgramScheduleService.cs`
- `VideoWebPlayer.Tests/ProgramRuleCursorNullStrandKeyMigrationTests.cs`
- `VideoWebPlayer.Tests/ProgramRuleCursorUniquenessTests.cs`
- `VideoWebPlayer.Tests/ProgramScheduleServiceTests_CursorSelection.cs`
- `VideoWebPlayer.Tests/Services/Backups/LegacyBackupArchiveBuilder.cs`
- `VideoWebPlayer.Tests/Services/Backups/VideoWebPlayerBackupDataTests_StrandKey.cs`
- `docs/RELEASE_NOTES.md`
