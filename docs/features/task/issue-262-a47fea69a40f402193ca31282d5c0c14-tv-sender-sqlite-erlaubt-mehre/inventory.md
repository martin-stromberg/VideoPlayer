# Bestandsaufnahme: Eindeutigkeit der `ProgramRuleCursor`-Zeilen mit `StrandKey = NULL` (Issue #262)

Analysiert wurden Datenmodell, Cursor-Logik, Backup-/Restore-Pfad und Testbestand rund um
`ProgramRuleCursors.StrandKey` — bezogen auf die Anforderung aus `requirement.md` (SQLite lässt
mehrere `NULL`-`StrandKey`-Zeilen je `ProgramRuleId` zu; Duplikat-Bereinigung, Schutz bzw.
deterministische Auswahl gefordert).

## Zusammenfassung

- **Der beanstandete Zustand ist bestätigt:** `ProgramRuleCursorConfiguration` (Z. 30) definiert
  nur den Composite-Unique-Index `(ProgramRuleId, StrandKey)` — ohne Filter. `StrandKey` ist
  `string?` und bleibt für den Gesamt-Cursor `NULL`. SQLite wertet `NULL` im Unique-Index als
  verschieden; es existiert **kein** DB-Schutz gegen mehrere `NULL`-Zeilen je Regel.
- **Alle drei Cursor-Lesepfade sind nichtdeterministisch bei Duplikaten:**
  `RuleState.FindCursor` (Z. 921), `RewindCursor` (Z. 729) und `PersistCursors` (Z. 1471) nutzen
  jeweils `FirstOrDefault(c => c.StrandKey == …)` ohne Sortierung.
- **Vor der `StrandKey`-Migration waren Duplikate ausgeschlossen:** `AddProgramSchedule` legte
  einen Unique-Index auf `ProgramRuleId` allein an; `AddProgramRuleCursorStrandKey` ersetzte ihn
  durch den Composite-Index. `NULL`-Duplikate können also nur aus Zeiten **nach** dieser
  Migration stammen (parallele Inserts, Restore), nicht als Altlast von vor dem Strang-Modell.
- **Restore-Pfad umgeht EF komplett:** `VideoWebPlayerBackupData.ReadFromAsync` löscht alle
  Zeilen und insertet zeilenweise per `DbCommand`. Ein Alt-Backup mit `NULL`-Duplikaten würde
  unter einem künftigen gefilterten Unique-Index am Restore scheitern — dafür gibt es aktuell
  keinen Deduplizierungs-Mechanismus und keinen String-Default (`OptionalRestoreBool/Long/
  IntDefaults` nur).
- **Backup-Registrierung vorhanden:** `ProgramRuleCursors` in `OptionalRestoreTables` (Z. 42),
  `StrandKey` in `OptionalRestoreColumns` (Z. 94); Regressionstest
  `VideoWebPlayerBackupDataTests_StrandKey` sichert das `NULL`-Wiederherstellen ab.
- **Präzedenzen vorhanden:** Datenmigrationen per `migrationBuilder.Sql` (z. B.
  `20260905220309_NormalizePlaylistEntryMediaTypes`, `20261021131012_AddProgramRuleGenreAndSourceLinks`)
  und gefilterte Unique-Indizes per `HasFilter` (`ContinueWatchingEntryConfiguration`,
  `WatchedEntryConfiguration`).
- **Testlücke:** Kein Test erzwingt den Unique-Index bei `NULL`-Duplikaten und keiner prüft die
  deterministische Cursor-Auswahl. Alle Schedule-Tests laufen bereits gegen echtes SQLite
  (`ProgramScheduleTestDb`, `EnsureCreatedAsync`); **kein** Test führt `Migrate()` aus — eine
  künftige Datenmigration ist mit dem vorhandenen Muster nicht direkt testbar.
- **Keine reale Datenbank im Repo-Verzeichnis:** `appsettings.json` verweist auf
  `Data/WebVideoPlayer.db`, die Datei existiert in diesem Arbeitsverzeichnis nicht — die
  Duplikat-Abfrage aus dem Issue (Akzeptanzkriterium 1) kann hier gegen keinen realen Bestand
  ausgeführt werden.

Test-Ausgangszustand: Volle Suite (`dotnet test VideoWebPlayer.Tests/VideoWebPlayer.Tests.csproj`,
Debug) — 2227 Tests: 2225 bestanden, 1 fehlgeschlagen (`PlaylistPlaybackE2ETests.
PlaylistSkipCollectionEntriesE2ETest`, Playwright-Timeout; in zwei Einzel-Wiederholungen
bestanden → Flake), 1 übersprungen (`[Fact(Skip)]` — dokumentierter, bekannter Defekt in
`api/continue-watching`, ohne Bezug zu dieser Anforderung). Details und Nachweise:
[Tests](inventory/tests.md).

## Details

- [Datenmodell](inventory/models.md) — `ProgramRuleCursor`, EF-Konfiguration, Snapshot, Migrationen
- [Logik](inventory/logic.md) — `ProgramScheduleService` (Cursor-Ladung, `FindCursor`, `RewindCursor`, `PersistCursors`, `ResetScheduleAsync`), `VideoWebPlayerBackupData` (Restore-Pfad, `OptionalRestore*`)
- [Enums](inventory/enums.md) — `ProgramCursorContentType`
- [Interfaces](inventory/interfaces.md) — `IProgramScheduleService`, `IBackupData` (extern)
- [Tests](inventory/tests.md) — Test-Ausgangszustand, bestehende Testklassen und Hilfsmethoden
