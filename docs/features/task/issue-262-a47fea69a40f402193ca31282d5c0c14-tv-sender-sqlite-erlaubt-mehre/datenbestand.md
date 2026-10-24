# Datenbestand: Duplikat-Abfrage aus Issue #262 (Akzeptanzkriterium 1)

Datum: 2026-10-24; Branch `task/issue-262-a47fea69a40f402193ca31282d5c0c14-tv-sender-sqlite-erlaubt-mehre`.

## Ergebnis: kein realer Bestand prüfbar

Die Issue-Abfrage

```sql
SELECT ProgramRuleId, COUNT(*) FROM ProgramRuleCursors
WHERE StrandKey IS NULL
GROUP BY ProgramRuleId
HAVING COUNT(*) > 1;
```

konnte gegen **keine** reale Datenbank ausgeführt werden:

- `appsettings.json` (`VideoWebPlayer/appsettings.json` Z. 3) verweist auf
  `Data/WebVideoPlayer.db` relativ zum Inhaltsverzeichnis — diese Datei existiert in diesem
  Arbeitsverzeichnis nicht (bereits in der Bestandsaufnahme festgestellt).
- Eine Suche nach `*.db`/`*.sqlite*` im gesamten Repository-Baum
  (`D:\Repositories\softwareschmiede\a47fea69-a40f-4021-93ca-31282d5c0c14`) sowie in den
  übrigen Worktrees unter `D:\Repositories\softwareschmiede` fand keine `WebVideoPlayer.db`;
  die einzige gefundene Datenbank (`financemanager.db`) gehört zu einem anderen Projekt.
- Produktive Installationen liegen außerhalb des Repository-Rechtsraums und sind von hier
  nicht erreichbar.

## Konsequenz

Ob reale Bestände `NULL`-`StrandKey`-Duplikate enthalten, bleibt offen — die generische
Datenmigration in der neuen EF-Migration `EnforceProgramRuleCursorNullStrandKeyUnique`
(`ROW_NUMBER() OVER (PARTITION BY ProgramRuleId ORDER BY UpdatedAt DESC, Id DESC)`, jüngste
Zeile gewinnt) bereinigt sie beim Update automatisch, und der gefilterte Unique-Index
verhindert neue Duplikate. Ein Betreiber kann die Abfrage oben bei Bedarf vor oder nach dem
Update gegen seine `Data/WebVideoPlayer.db` ausführen; ein „Ergebnis 0 Zeilen" ist der
Normalfall, da `PersistCursors` je Strang nur eine Zeile schreibt und `NULL`-Duplikate nur
über parallele Inserts oder Restores von Alt-Backups entstehen konnten.
