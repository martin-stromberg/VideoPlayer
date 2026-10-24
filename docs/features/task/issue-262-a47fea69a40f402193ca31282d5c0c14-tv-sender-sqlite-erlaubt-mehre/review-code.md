# Code-Review

## Ergebnis

**Status:** Keine Befunde

Runde 2: Beide Befunde aus Runde 1 (siehe `review-code.1.md`) sind im Code verifiziert
behoben — der neue Pass-Through-Test fängt die Sabotageprobe zuverlässig, und das
`LegacyBackupArchiveBuilder`-Refactoring ist verhaltensgleich. Der erneut geprüfte
Gesamt-Diff gegen `origin/staging` ergab keine neuen Befunde (Details siehe Abschnitt
„Verifikationsnachweise").

## Befunde

Keine.

## Verifikationsnachweise

Über die Kriterienprüfung hinaus wurden die Runde-1-Befunde und die zentralen
Korrektheitsfragen direkt verifiziert:

- **Befund 1 (fehlende Pass-Through-Abdeckung) — behoben.** Neuer Test
  `ReadFromAsync_BackupWithNullAndStrandKeyRows_KeepsAllStrandRows`
  (`VideoWebPlayer.Tests/Services/Backups/VideoWebPlayerBackupDataTests_StrandKey.cs`
  Z. 140–192): Eine Regel besitzt im Backup zwei `NULL`-Duplikate plus `show:1`- und
  `other`-Strangzeilen; der Restore muss alle drei Stränge erhalten und nur die
  `NULL`-Duplikate auf die jüngste Zeile (`LastContentId = 42`) reduzieren. Teststruktur,
  Seeding (`SeedRuleWithNullStrandCursorAsync` wiederverwendet) und die `ChangeTracker.
  Clear()`-Falle sind korrekt gehandhabt.
- **Sabotageprobe zum neuen Test erfolgreich.** Bedingung
  `GetString(row, nameof(ProgramRuleCursor.StrandKey)) is not null` aus
  `DeduplicateNullStrandKeyCursorRows` (`VideoWebPlayerBackupData.cs` Z. 477) entfernt →
  `ReadFromAsync_BackupWithNullAndStrandKeyRows_KeepsAllStrandRows` schlägt fehl
  (`Expected: 3, Actual: 1` — die Dedup gruppiert alle vier Zeilen der Regel und zerstört
  die Strangzeilen, exakt das dokumentierte Fehlerbild). Die übrigen drei StrandKey-Tests
  bleiben erwartungsgemäß grün (sie enthalten nur `NULL`-Zeilen). Anschließend
  zurückgebaut und erneut 4/4 grün verifiziert.
- **Befund 2 (doppelter Code) — behoben und verhaltensgleich.** Alter und neuer Code von
  `RemoveColumnsAsync(MemoryStream, …)`/`AppendRowsAsync(MemoryStream, …)` gegen den
  extrahierten Helfer `RewriteTablePayloadAsync`
  (`LegacyBackupArchiveBuilder.cs` Z. 286–332) zeilenweise verglichen: gleiche
  Ablaufreihenfolge (`Position = 0` → Index lesen → `First` auf Tabellenname →
  `entryName` → Index-Mutation **vor** dem Schreiben von `index.json` → alle anderen
  Einträge kopieren → Ziel-Payload parsen, Zeilen mutieren, neu schreiben → Stream auf 0).
  `AppendRowsAsync` übergibt `mutateTableIndex: null` wie zuvor ohne Index-Mutation;
  die Zeilen-Serialisierung bleibt `JsonSerializer.SerializeToNode(row, JsonOptions)`.
  Alle 78 Tests unter `Services.Backups` (die Konsumenten beider Methoden) laufen grün.
- **Gesamt-Diff erneut geprüft** (alle 12 Quelldateien des Diff gegen `origin/staging`
  vollständig gelesen): Keine neuen Befunde. Insbesondere: `SelectStrandCursor`
  (`ProgramScheduleService.cs` Z. 953–960) ist an allen drei früheren
  `FirstOrDefault`-Stellen eingesetzt (`RuleState.FindCursor`, `RewindCursor`,
  `PersistCursors`); `internal` ist für die direkten Unittests nötig und durch
  `InternalsVisibleTo` (`VideoWebPlayer.csproj` Z. 15) abgedeckt. Die in Runde 1
  dokumentierte Zeilen-Umsortierung in `DeduplicateNullStrandKeyCursorRows` (Gewinner
  werden am Ende angehängt) ist weiterhin harmlos — einzelne Tabelle, keine
  Selbstreferenz, FK-Pragma ohnehin aus.
- **Definition of Done (AGENTS.md §7):** `dotnet build VideoPlayer.sln` in Debug
  (implizit über den Testlauf) **und** Release fehlerfrei (0 Fehler, nur
  vorhersehbare, branch-fremde Warnungen). Keine neue Tabelle/Spalte —
  `ProgramRuleCursors` steht bereits in `OptionalRestoreTables` (Z. 42) und
  `StrandKey` in `OptionalRestoreColumns` (Z. 94); Alt-Backup-Regression ist durch die
  StrandKey-Testklasse abgedeckt; die EF-Migration existiert. Unique-Index-/FK-Tests
  laufen gegen echtes SQLite (`Microsoft.Data.Sqlite`, `IMigrator`,
  `ProgramScheduleTestDb`), nicht InMemory. Release Notes in beiden Sprachen ergänzt;
  kein anwendersichtbarer Funktionswandel, daher kein Hilfe-Doku-Update erforderlich
  (Begründung aus Runde 1 weiterhin zutreffend). Keine neuen NuGet-Abhängigkeiten.
  Ausgeführte Tests: alle 78 Tests unter `Services.Backups` sowie
  `ProgramRuleCursorNullStrandKeyMigrationTests` (2), `ProgramRuleCursorUniquenessTests`
  (3) und `ProgramScheduleServiceTests_CursorSelection` (3) — grün. Die volle
  Testsuite bleibt Bestandteil des offenen todo-Schritts 10.

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
