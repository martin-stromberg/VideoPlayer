# Übersetzte Anforderung: Eindeutigkeit der `ProgramRuleCursor`-Zeilen mit `StrandKey = NULL` absichern (Issue #262)

Quelle: `issue.md` im Repo-Root (Aufgaben-ID `a47fea69-a40f-4021-93ca-31282d5c0c14`, Issue #262 — Nachbesserung aus Issue #111 / PR #257, Serien-Interleaving N19).

## Fachliche Zusammenfassung

Der Unique-Index `IX_ProgramRuleCursors_ProgramRuleId_StrandKey` auf `(ProgramRuleId, StrandKey)` soll garantieren, dass pro `ProgramRule` höchstens eine Fortsetzungsstand-Zeile (`ProgramRuleCursor`) je Sendestrang existiert. Diese Garantie greift für den Gesamt-Cursor nicht: SQLite wertet `NULL` in Unique-Indizes als voneinander verschieden, sodass mehrere Zeilen mit derselben `ProgramRuleId` und `StrandKey = NULL` zulässig sind. Alle Lesepfade des `ProgramScheduleService` (`FindCursor`, `PersistCursors`, `RewindCursor`) greifen per `FirstOrDefault(c => c.StrandKey == …)` nur eine Zeile — bei Duplikaten gewinnt eine beliebige, die anderen bleiben veraltet zurück, was potenziell nichtdeterministische Fortsetzungs-Episoden erzeugt. Zu leisten: Bestandsaufnahme, ob reale Datenbanken betroffen sind; Bereinigung vorhandener Duplikate sowie ein Schutz, der neue Duplikate verhindert oder die Auswahl deterministisch macht.

## Akzeptanzkriterien (aus der Anforderung abgeleitet)

1. **Bestandsaufnahme:** Es ist geprüft und dokumentiert, ob in realen Datenbanken Duplikate existieren — Abfrage laut Issue: `SELECT ProgramRuleId, COUNT(*) FROM ProgramRuleCursors WHERE StrandKey IS NULL GROUP BY ProgramRuleId HAVING COUNT(*) > 1`.
2. **Duplikat-Bereinigung:** Eine Datenmigration führt vorhandene `NULL`-`StrandKey`-Duplikate je `ProgramRuleId` zusammen; dabei gewinnt die jüngste Zeile (vom Issue so vorgegeben — „jüngste Zeile gewinnt"; ob das Kriterium `UpdatedAt` oder `Id` ist, ist unter „Offene Fragen" geklärt bzw. im Plan festzulegen).
3. **Schutz vor neuen Duplikaten:** Danach ist auf Datenbankebene sichergestellt, dass je `ProgramRuleId` höchstens eine Zeile mit `StrandKey = NULL` existiert — z. B. über einen gefilterten Unique-Index `WHERE StrandKey IS NULL` nur auf `ProgramRuleId`, oder über Migration von `NULL` auf einen festen Marker (z. B. `'main'`). Wird bewusst die Toleranz-Variante gewählt, entfällt der DB-Schutz — dann muss die Entscheidung samt Begründung dokumentiert sein.
4. **Deterministische Auswahl:** Unabhängig von der gewählten Variante treffen alle `FirstOrDefault`-Zugriffe auf `ProgramRuleCursor`-Zeilen (`FindCursor`, `PersistCursors`, `RewindCursor`) bei Mehrfachtreffern eine deterministische Wahl (z. B. `OrderBy(c => c.Id)`), statt eine undefinierte Zeile zu lesen.
5. **Dokumentation im Code:** Der Kommentar am Index in `ProgramRuleCursorConfiguration` sowie die XML-Doku von `ProgramRuleCursor.StrandKey` beschreiben das tatsächlich geltende Verhalten (bisheriger Kommentar erwähnt die NULL-Problematik bereits).
6. **Datenhaltung:** Die Änderung erhält eine EF-Migration. `ProgramRuleCursors` ist bereits in `VideoWebPlayerBackupData.OptionalRestoreTables` und `StrandKey` in `OptionalRestoreColumns` registriert — diese Registrierung muss wirksam bleiben; der Alt-Backup-Regressionstest (`VideoWebPlayerBackupDataTests_StrandKey`: Backup ohne `StrandKey`-Spalte stellt Zeilen mit `NULL` wieder her) bleibt grün bzw. wird der gewählten Variante angepasst.
7. **Tests gegen echtes SQLite:** Tests zu Unique-Index und Löschverhalten laufen gegen echtes SQLite (Projektvorgabe; vorhandenes Muster: `ProgramScheduleTestDb` mit `SqliteConnection` + `EnsureCreated`), nicht gegen EF InMemory. Kern-Nachweis: Eine zweite `NULL`-`StrandKey`-Zeile je `ProgramRuleId` wird vom Schema abgelehnt (bei Schutz-Variante) bzw. der Cursor-Lesepfad wählt deterministisch (bei Toleranz-Variante).

## Betroffene Klassen und Komponenten

- **Datenmodell:** `VideoWebPlayer/Data/ProgramRuleCursor.cs` (Eigenschaft `StrandKey`, ggf. Nullable-Status bei Marker-Variante), `VideoWebPlayer/Data/Configurations/ProgramRuleCursorConfiguration.cs` Z. 30 (`HasIndex(c => new { c.ProgramRuleId, c.StrandKey }).IsUnique()` samt Kommentar), neue EF-Migration, `ApplicationDbContextModelSnapshot`. Bezugsmigration: `20261022190000_AddProgramRuleCursorStrandKey`.
- **Logik/Services:** `VideoWebPlayer/Services/ProgramScheduleService.cs` — Cursor-Ladung (Z. 88–92 und 156–160, Gruppierung je `ProgramRuleId`), `RuleState.SetItems`/`FindCursor` (Z. 883–921), `RewindCursor` (Z. 712–741, Zugriff Z. 729), `PersistCursors` (Z. 1462–1491, Zugriff Z. 1471). Strang-Konventionen: `OtherStrandKey = "other"` (Z. 834), `show:{TVShowId}` (Z. 1777), `null` = Gesamt-Cursor nicht-interleaver Regeln.
- **Backup:** `VideoWebPlayer/Services/Backups/VideoWebPlayerBackupData.cs` — `OptionalRestoreTables` (Z. 42), `OptionalRestoreColumns` (Z. 94); bei Marker-Variante ggf. Default-Mechanismus für fehlende `StrandKey`-Spalte in Alt-Backups (vorhanden sind nur `OptionalRestoreBoolDefaults`/`OptionalRestoreLongDefaults`/`OptionalRestoreIntDefaults` — kein String-Default).
- **Tests:** `VideoWebPlayer.Tests/ProgramScheduleServiceTests_*.cs` (insbesondere `_Interleaving`), `VideoWebPlayer.Tests/Services/Backups/VideoWebPlayerBackupDataTests_StrandKey.cs` und `VideoWebPlayerBackupDataTests.cs`, `Helpers/ProgramScheduleTestDb.cs`, `Helpers/LegacyBackupArchiveBuilder`; neue Tests für Index-Erzwingung und deterministische Cursor-Auswahl.
- **UI/Controller:** keine — rein datenbank-/service-seitige Änderung.

## Implementierungsansatz

- **Bestandsaufnahme zuerst:** Duplikat-Abfrage aus dem Issue gegen verfügbare Datenbanken ausführen und Ergebnis festhalten.
- **Schutz-Variante A — gefilterter Unique-Index (vom Issue nahegelegt):** EF-Migration, die (a) per Datenmigration Duplikate je `ProgramRuleId` mit `StrandKey IS NULL` zusammenführt (jüngste Zeile gewinnt; Datenmigrationen per `migrationBuilder.Sql` sind etabliert, vgl. `20260905220309_NormalizePlaylistEntryMediaTypes`) und (b) einen zusätzlichen gefilterten Unique-Index auf `ProgramRuleId` mit `HasFilter("[StrandKey] IS NULL")` anlegt (Präzedenz im Modell: gefilterte Indizes per `HasFilter("[MovieId] IS NOT NULL")` u. a.). Der bestehende Composite-Index `(ProgramRuleId, StrandKey)` bleibt für die Nicht-NULL-Stränge wirksam. `StrandKey` bleibt nullable — Backup-/Restore-Semantik unverändert.
- **Schutz-Variante B — Marker statt `NULL`:** `NULL`-Werte auf festen Marker (z. B. `'main'`) migrieren, `StrandKey` NOT NULL setzen; der bestehende Composite-Index greift dann vollständig. Zu beachten (Annahme, im Plan zu verifizieren): Alt-Backups ohne `StrandKey`-Spalte stellen heute `NULL` wieder her — bei NOT NULL wäre ein String-Default-Mechanismus in `VideoWebPlayerBackupData` nötig oder die Spalte bliebe nullable mit Anwendungskonvention; außerdem müsste der Marker mit den bestehenden Schlüsseln `show:{id}`/`other` kollisionsfrei sein.
- **Toleranz-Variante C:** Kein Schema-Schutz; stattdessen deterministische `FirstOrDefault`-Wahl (z. B. `OrderBy(c => c.Id)`) und präzisierter Kommentar. Vom Issue als akzeptable Alternative genannt.
- **Unabhängig von der Variante:** Lesepfade deterministisch machen (Verteidigung in der Tiefe); in `PersistCursors` bei Mehrfachtreffern definiert verhalten (eine Zeile aktualisieren statt beliebiger).
- **Restore-Pfad beachten:** `VideoWebPlayerBackupData.ReadFromAsync` löscht alle Zeilen und insertet sie anschließend zeilenweise per `InsertRowAsync` direkt (ohne EF-Validierung). Ein Alt-Backup, das Duplikat-`NULL`-Zeilen enthält, würde bei Variante A/B am neuen Unique-Index scheitern — Deduplikation beim Restore oder dokumentierte Entscheidung im Plan klären.

## Konfiguration

Keine — die Änderung ist rein datenbank- und service-seitig; weder `Setup`/`ProgramSettingsService` noch `appsettings` sind betroffen.

## Offene Fragen

1. **Welche Variante wird umgesetzt?** Das Issue nennt gefilterten Unique-Index, `'main'`-Marker und bewusste Toleranz gleichrangig. Entscheidung im Umsetzungsplan treffen und begründen (Schutz-Varianten erfordern Duplikat-Migration; Toleranz erfordert nur deterministische Auswahl).
2. **Gewinner-Kriterium beim Zusammenführen:** „Jüngste Zeile gewinnt" — ist das `MAX(UpdatedAt)` oder `MAX(Id)`? (Bei Gleichstand von `UpdatedAt` ist ohnehin ein Tiebreaker über `Id` nötig.)
3. **Restore-Verhalten bei Alt-Backups mit Duplikaten:** Soll `ReadFromAsync` `NULL`-Duplikate deduplizieren (gleiche Gewinner-Regel wie die Migration), oder gilt der Index-Verstoß als bewusst abgelehnter inkonsistenter Bestand?
4. **Marker-Variante nur falls gewählt:** Wie wird `StrandKey` aus Alt-Backups ohne die Spalte behandelt (`OptionalRestoreColumns` liefert heute `NULL`; bei NOT NULL fehlt ein String-Default-Mechanismus)? Ist der Marker-Wert kollisionsfrei zu `show:{TVShowId}` und `other`?
5. **Bestandsaufnahme realer Datenbanken:** Gegen welche Bestände wird die Duplikat-Abfrage aus dem Issue tatsächlich ausgeführt (Entwicklungs-/Testdatenbanken vs. produktive Installationen), und wo wird das Ergebnis dokumentiert?
6. **Migrationstestbarkeit:** Test-DBs entstehen per `EnsureCreated` (Schema aus dem Modell, nicht aus Migrationshistorie) — wie wird die Datenmigration selbst getestet (z. B. Alt-Schema per SQL aufbauen, Migration via `Migrate()` laufen lassen), oder beschränkt sich der Test auf Index-Erzwingung und Lesepfad?
