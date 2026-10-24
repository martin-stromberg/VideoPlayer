# Bestandsaufnahme: Logik

## `ProgramScheduleService`

Datei: `VideoWebPlayer/Services/ProgramScheduleService.cs` (2039 Zeilen), `sealed`, implementiert
`IProgramScheduleService`. Zugriff auf `ProgramRuleCursors` über `_db` (`ApplicationDbContext`).

### Cursor-Ladung

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `EnsureCoverageAsync` | `public` | Lädt in Z. 88–92 alle `ProgramRuleCursors` der aktiven Regeln, gruppiert per `GroupBy(c => c.ProgramRuleId)` in `Dictionary<long, IReadOnlyList<ProgramRuleCursor>>`; übergibt die Liste je Regel an `RuleState` (Z. 102), `ExpandTrailerStatesAsync` (Z. 119) und `PersistCursors` (Z. 122) |
| `ReconcileAsync` | `public` | Identische Ladung in Z. 156–160 (hier für **alle** Regeln inkl. inaktiver, weil verdrängte Slots inaktiver Regeln einen Cursor-Rewind brauchen, Z. 152–154); `RewindCursor`-Aufrufe Z. 262, 295, 335; `PersistCursors` Z. 344 |
| `ResetScheduleAsync` | `public` | Z. 425–427: `ExecuteDeleteAsync` auf `ProgramRuleCursors` der Kanal-Regeln — löscht **alle** Cursor-Zeilen (jedes Duplikat mit) |

Die Gruppierung lädt **alle** Zeilen je `ProgramRuleId` — Duplikate bleiben als Listeneinträge
erhalten und werden erst bei den `FirstOrDefault`-Zugriffen unten auf eine Zeile reduziert.

### Cursor-Lese- und Schreibzugriffe (die vom Issue betroffenen Stellen)

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `RuleState.SetItems` | `public` (Member der privaten Nested Class) | Z. 883–918: baut `Strands` neu. Bei Genre-Regeln mit >1 Strang: je Strang `NextIndexFor(items, FindCursor(cursors, key))` (Z. 911); sonst flacher Strang `Key == null` mit `FindCursor(cursors, null)` (Z. 917) |
| `RuleState.FindCursor` | `private static` | Z. 920–921: `cursors.FirstOrDefault(c => c.StrandKey == key)` — **keine** Sortierung; bei Duplikaten gewinnt eine beliebige Zeile |
| `RewindCursor` | `private static` | Z. 712–741: pro Strang der früheste noch expandierbare verdrängte Inhalt; Z. 729 `cursorRows.FirstOrDefault(c => c.StrandKey == strand.Key)` — wieder `FirstOrDefault` ohne Ordnung; setzt `LastContentType`/`LastContentId`/`UpdatedAt` auf den Vorgänger (Z. 730–735) |
| `PersistCursors` | `private` | Z. 1462–1491: pro Strang mit `LastPlaced` — Z. 1471 `rows?.FirstOrDefault(c => c.StrandKey == strand.Key)`: Treffer → UPDATE der einen Zeile (Z. 1472–1477), sonst `ProgramRuleCursors.Add(...)` (Z. 1479–1488). Bei Duplikaten wird eine beliebige Zeile aktualisiert, die übrigen bleiben veraltet; ein Insert erfolgt nur bei gar keiner Zeile |
| `NextIndexFor` | `private static` | Z. 1073–1080: Ringposition hinter dem Cursor-Inhalt; `pos + 1`, bei unauffindbarem Inhalt oder fehlendem Cursor `0` |
| `ExpandGenreAsync` | `private` | Vergibt Strang-Schlüssel: Filme `StrandKey = OtherStrandKey` (`"other"`, Z. 1773), Episoden `StrandKey = $"show:{ShowId}"` (Z. 1777) |
| `ExpandTrailerStatesAsync` | `private` | Z. 1783–1800: Trailer-Expansion nach der Materialisierung; ruft `state.SetItems(items, cursors...)` erneut auf (Z. 1798) — zweiter `FindCursor`-Pfad |

### Interne Typen / Konstanten

| Name | Art | Beschreibung |
|------|-----|--------------|
| `OtherStrandKey` | `private const string` | Z. 834: `"other"` — gemeinsamer Strang für Nicht-Episoden-Inhalt der Genre-Expansion |
| `StrandState` | `private sealed class` | Z. 840–858: Ring je Strang (`Key` `string?`, `Items`, `NextIndex`, `LastPlaced`, `PeekAt`, `Commit`, `RewindTo`) |
| `RuleState` | `private sealed class` | Z. 860–922: Regel-Zustand; `IsInterleaved` = `Strands.Count > 1`; Flat-Mode-Oberfläche `NextIndex`/`Commit`/`RewindTo` greifen auf `Strands[0]` |
| `SchedulableItem` | `private sealed record` | Z. 823–830: expandiertes Element inkl. `StrandKey` (Default `""`) |
| `StrandOrderKey` | `internal static` | Z. 939: stabiler FNV-1a-Hash-Mix je (Regel, Strang) für die Strang-Reihenfolge; einziges `internal`-Member, aus Tests direkt aufrufbar |

Keine Events werden abonniert oder publiziert.

**Beobachtung zur Schreibrate:** `PersistCursors` schreibt pro `(ProgramRuleId, StrandKey)` immer
genau eine Zeile (Update oder Insert). `NULL`-Duplikate entstehen durch diesen Pfad nicht —
mögliche Quellen sind Alt-Bestände nach der `StrandKey`-Migration, Race-Conditions bei
parallelen Inserts ohne DB-Schutz oder Restores (s. `VideoWebPlayerBackupData`).

## `VideoWebPlayerBackupData`

Datei: `VideoWebPlayer/Services/Backups/VideoWebPlayerBackupData.cs` (1158 Zeilen), `sealed`,
implementiert `IBackupData` (aus Paket `msTools.Backup`, nicht im Repo definiert).

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `WriteToAsync` | `public` | Z. 192–233: exportiert alle Modell-Tabellen (`GetTables`) als JSON-Zeilen in ZIP-Einträge `entities/*.json` plus `index.json` (Provider-Id `VideoWebPlayer.ApplicationDbContext`, `SchemaVersion = 1`) |
| `ReadFromAsync` | `public` | Z. 236–352: validiert (`ValidatePayloadAsync`), `PRAGMA foreign_keys = OFF` bei SQLite (Z. 288), löscht **alle** Zeilen aller Tabellen (Z. 296–297), insertet zeilenweise per `InsertRowAsync` **direkt über `DbCommand`** — ohne EF (Z. 312–324); danach `PRAGMA foreign_key_check` (Z. 331) und Commit. Unique-Index-Verletzungen (z. B. durch `NULL`-Duplikate im Backup unter einem künftigen gefilterten Index) würden als SQLite-Constraint-Fehler zum Rollback führen — heute gibt es für `ProgramRuleCursors` keinen solchen Schutz, d. h. ein Backup mit `NULL`-Duplikaten würde sie 1:1 wiederherstellen |
| `GetTables` | `private` | Z. 1094–1116: Tabellen- und Spaltenliste aus dem EF-Modell (`GetEntityTypes`, `GetColumnName`) — treibt sowohl Export als auch Restore |
| `ValidatePayloadAsync` | `private` | Z. 459–583: fehlende Tabellen nur erlaubt, wenn in `OptionalRestoreTables`; fehlende Spalten nur, wenn in `OptionalRestoreColumns`; unbekannte Spalten (außer `IgnoredRestoreColumns`) sind Fehler |
| `InsertRowAsync` | `private static` | Z. 873–894: baut `INSERT` nur über die im Backup vorhandenen Spalten plus Defaults aus `AddOptionalRestoreDefaults`; fehlende optionale Spalten landen als `NULL` im Insert (kein Spalten-Default) |
| `AddOptionalRestoreDefaults` | `private static` | Z. 896–950: ergänzt fehlende Spalten mit Defaults — nur `OptionalRestoreBoolDefaults`, `OptionalRestoreLongDefaults`, `OptionalRestoreIntDefaults` sowie Sonderfall `Setups.ApplicationTitle`; **kein String-Default-Mechanismus** |
| `EnsureNoSqliteForeignKeyViolationsAsync` | `private static` | Z. 1018–1029: `PRAGMA foreign_key_check` — wirft `InvalidDataException` bei Verletzungen (nur FK, keine Unique-Prüfung) |
| `BuildColumnSelectExpression` / `BuildTableFilter` | `private static` | Z. 407–444: Export-Sonderfälle nur für `TVShowEpisodes` und `Playlists`/`Pictures`; für `ProgramRuleCursors` keine Sonderbehandlung |

### Relevante statische Registrierungen

| Member | Zeilen | Inhalt bezüglich `ProgramRuleCursors` |
|--------|--------|----------------------------------------|
| `OptionalRestoreTables` | 23–46 | `ProgramRuleCursors` registriert (Z. 42) — Alt-Backups ohne die Tabelle sind restore-fähig |
| `OptionalRestoreColumns` | 48–96 | `ProgramRuleCursors.StrandKey` registriert (Z. 94) — Alt-Backups ohne die Spalte sind restore-fähig; die Zeilen erhalten `StrandKey = NULL` |
| `OptionalRestoreBoolDefaults` / `…LongDefaults` / `…IntDefaults` | 106–140 | kein `StrandKey`-Eintrag; es existiert kein `OptionalRestoreStringDefaults` |

## Weitere Schreibpfade auf `ProgramRuleCursors`

- `ProgramScheduleWorker` (`VideoWebPlayer.Tests` bestätigt: `ProgramScheduleWorkerTests`):
  triggert `EnsureCoverageAsync`/`ReconcileAsync` zeitgesteuert — kein eigener Cursor-Zugriff.
- Keine anderen Services schreiben in `ProgramRuleCursors` (grep über `ProgramRuleCursors`
  im `VideoWebPlayer`-Projekt: nur `ProgramScheduleService`, `VideoWebPlayerBackupData`
  (Tabellenliste), `ApplicationDbContext`).
