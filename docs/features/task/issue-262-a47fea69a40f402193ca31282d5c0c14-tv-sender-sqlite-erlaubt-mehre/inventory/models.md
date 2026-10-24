# Bestandsaufnahme: Datenmodell

Betroffene Entität: `ProgramRuleCursor` samt EF-Konfiguration und zugehöriger Migration.

## `ProgramRuleCursor`

Datei: `VideoWebPlayer/Data/ProgramRuleCursor.cs`

Persistenter Fortsetzungsstand („Fortsetzungsstand") einer `ProgramRule`: welches Element der
expandierten Materialliste zuletzt eingeplant wurde, damit Serien und Rotationen über
Generierungsläufe und Neustarts hinweg in Ausstrahlungsreihenfolge fortgesetzt werden.

| Eigenschaft | Typ | Beschreibung / Zweck |
|-------------|-----|----------------------|
| `Id` | `long` | Primärschlüssel (`[Key]`, `Sqlite:Autoincrement`) |
| `ProgramRuleId` | `long` | Fremdschlüssel auf `ProgramRule` (`[ForeignKey]`); Löschweitergabe `Cascade` — beim Löschen der Regel gehen alle Cursor-Zeilen mit |
| `ProgramRule` | `ProgramRule` | Navigationseigenschaft zur besitzenden Regel |
| `StrandKey` | `string?` | Sendestrang-Schlüssel des Cursors: `show:{TVShowId}` je Serie bei Interleaving, `other` für den gemeinsamen Nicht-Episoden-Strang, `null` für den Gesamt-Cursor nicht-interleaver Regeln (und Zeilen von vor dem Strang-Modell). XML-Doku Z. 31–37 beschreibt `null` als „the rule's single overall cursor" |
| `LastContentType` | `ProgramCursorContentType` | Art des Inhalts, auf den `LastContentId` verweist |
| `LastContentId` | `long` | Id des zuletzt eingeplanten Elements (Movie-, Episode-, PlaylistEntry- oder MediaItem-Id je nach `LastContentType`) |
| `UpdatedAt` | `DateTime` | Zeitstempel der letzten Aktualisierung; Initialwert `DateTime.UtcNow` (Z. 55) |

## `ProgramRuleCursorConfiguration` (EF-Konfiguration)

Datei: `VideoWebPlayer/Data/Configurations/ProgramRuleCursorConfiguration.cs`

- Z. 14: `HasKey(c => c.Id)`
- Z. 16: `StrandKey` explizit nullable (`IsRequired(false)`); `LastContentType`, `LastContentId`,
  `UpdatedAt` required (Z. 17–19)
- Z. 21–25: `HasOne(c => c.ProgramRule).WithMany().HasForeignKey(c => c.ProgramRuleId)`
  mit `OnDelete(DeleteBehavior.Cascade)`
- Z. 27–30: Kommentar + Unique-Index:
  `builder.HasIndex(c => new { c.ProgramRuleId, c.StrandKey }).IsUnique();`
  Der Kommentar nennt die NULL-Problematik bereits: „SQLite treats NULLs as distinct, but only
  one such row is ever written per rule".

## Modell-Snapshot

Datei: `VideoWebPlayer/Migrations/ApplicationDbContextModelSnapshot.cs` Z. 1380–1407

- `StrandKey` als `TEXT` nullable, `UpdatedAt` als `TEXT` required, `LastContentType` `INTEGER`
- `b.HasIndex("ProgramRuleId", "StrandKey").IsUnique()` — kein `HasFilter`

## Migrationen

### `20261021045013_AddProgramSchedule`

Datei: `VideoWebPlayer/Migrations/20261021045013_AddProgramSchedule.cs`

Erstellt `ProgramRuleCursors` (Z. 28–48) mit Unique-Index `IX_ProgramRuleCursors_ProgramRuleId`
auf `ProgramRuleId` allein (Z. 77–81) — d. h. vor dem Strang-Modell war je Regel **garantiert
höchstens eine** Cursor-Zeile möglich. `StrandKey` existiert noch nicht.

### `20261022190000_AddProgramRuleCursorStrandKey` (Bezugsmigration)

Datei: `VideoWebPlayer/Migrations/20261022190000_AddProgramRuleCursorStrandKey.cs`

- `Up`: `DropIndex IX_ProgramRuleCursors_ProgramRuleId` (Z. 13–15), `AddColumn StrandKey TEXT
  nullable` (Z. 17–21), `CreateIndex IX_ProgramRuleCursors_ProgramRuleId_StrandKey` unique auf
  `(ProgramRuleId, StrandKey)` (Z. 23–27) — **keine** Duplikat-Bereinigung, kein Filter.
- `Down`: Index und Spalte zurückbauen, alter Unique-Index auf `ProgramRuleId` wiederherstellen.

Konsequenz: Zeilen, die vor dieser Migration existierten, können keine `NULL`-Duplikate sein
(der alte Unique-Index verhinderte mehr als eine Zeile je `ProgramRuleId`). `NULL`-Duplikate
sind erst nach der Migration entstehbar (z. B. parallele Inserts, Wiederherstellung eines
Backups mit Duplikaten).

### Weitere relevante Präzedenzen im Migrationsbestand

- Datenmigrationen per `migrationBuilder.Sql(...)` sind etabliert:
  `20260905220309_NormalizePlaylistEntryMediaTypes` (UPDATEs),
  `20260830193357_ResetActorsClassifiedAt` (UPDATEs),
  `20260822103156_AddContinueWatchingListOrder` (SQL-Block Z. 20),
  `20261021131012_AddProgramRuleGenreAndSourceLinks` (Backfill-`INSERT INTO ... SELECT`, Z. 73–76).
- Gefilterte Unique-Indizes per `HasFilter` sind etabliert:
  `VideoWebPlayer/Data/Configurations/ContinueWatchingEntryConfiguration.cs` Z. 15–35
  (`HasFilter("[MovieId] IS NOT NULL")` u. a.) und
  `VideoWebPlayer/Data/Configurations/WatchedEntryConfiguration.cs` Z. 20–25.

## `ApplicationDbContext`

Datei: `VideoWebPlayer/Data/ApplicationDbContext.cs`

- Z. 214: `public DbSet<ProgramRuleCursor> ProgramRuleCursors { get; set; }`
- Migrationen werden beim App-Start ausgeführt: `Program.cs` Z. 47 `app.MigrateDatabase()` →
  `VideoWebPlayer/Extensions/WebApplicationExtensions.cs` Z. 23–32 `db.Database.Migrate()`.
- Produktiv-Connection-String: `appsettings.json` Z. 3 `Data Source=Data/WebVideoPlayer.db`
  (SQLite-Datei relativ zum Inhaltsverzeichnis; im Repo-Verzeichnis liegt **keine** Datenbankdatei).
