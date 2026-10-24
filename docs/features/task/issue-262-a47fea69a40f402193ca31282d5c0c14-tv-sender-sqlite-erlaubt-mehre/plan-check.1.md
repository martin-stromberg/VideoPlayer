# Plan-Gegenprüfung

## Ergebnis

**Status:** Plan lückenhaft

## Abgleich Akzeptanzkriterien

| Akzeptanzkriterium | Umsetzung im Plan | Testnachweis im Plan | Status |
|--------------------|-------------------|----------------------|--------|
| 1. Bestandsaufnahme realer Datenbanken (Duplikat-Abfrage aus dem Issue) | Umsetzungsschritt 1: `*.db`-Suche im Repo-/Datenverzeichnis, Issue-Abfrage ausführen, Ergebnis in `datenbestand.md` dokumentieren — inkl. dokumentiertem Ergebnis „kein realer Bestand prüfbar" | Dokumentationsartefakt statt Test; angemessen für eine Untersuchung (per Inventory liegt keine DB im Arbeitsverzeichnis) | Abgedeckt |
| 2. Duplikat-Bereinigung („jüngste Zeile gewinnt") | Datenmigration per `migrationBuilder.Sql` vor `CreateIndex`; Gewinner = `UpdatedAt` DESC, Tiebreak `Id` DESC (Offene Frage 2 entschieden) | `Migration_MergesDuplicateNullStrandKeyRows_YoungestWins` (echtes SQLite, `IMigrator.MigrateAsync` bis Vorgängermigration `AddTvSenderUserAccess` — im Migrationsstand verifiziert als aktuell letzte Migration — dann auf latest) | Abgedeckt |
| 3. Schutz vor neuen Duplikaten auf DB-Ebene | Gefilterter Unique-Index `HasIndex(c => c.ProgramRuleId).IsUnique().HasFilter("[StrandKey] IS NULL")` mit explizitem Namen `IX_ProgramRuleCursors_ProgramRuleId_NullStrandKey`; Composite-Index bleibt für Nicht-NULL-Stränge | `ProgramRuleCursors_SecondNullStrandKeyRow_RejectedByUniqueIndex`, `..._DistinctStrandKeys_SameRule_Allowed`, `..._SameNonNullStrandKey_SameRule_StillRejected` (alle echtes SQLite via `ProgramScheduleTestDb`/`EnsureCreated`), plus `Migration_NullStrandKeyUniqueIndex_RejectsDuplicateAfterUpgrade` | Abgedeckt |
| 4. Deterministische Auswahl in allen `FirstOrDefault`-Zugriffen | Umsetzung vorhanden: gemeinsamer Helfer `SelectStrandCursor` (`UpdatedAt` DESC, `Id` DESC) für `RuleState.FindCursor`, `RewindCursor`, `PersistCursors` — alle drei Stellen im Code verifiziert (Z. 920–921, 729, 1471) | **Kein Test** — weder Unit- noch Integrationstest prüft die deterministische Gewinner-Wahl im Lesepfad; die Inventory nennt diese Lücke explizit („Kein Test für deterministische `FirstOrDefault`-Wahl") | **Lücke** |
| 5. Dokumentation im Code | Kommentar an `ProgramRuleCursorConfiguration` Z. 27–29 und XML-Doku von `ProgramRuleCursor.StrandKey` werden angepasst | Kein Test nötig (Doku) | Abgedeckt |
| 6. Datenhaltung (EF-Migration, Backup-Registrierung bleibt wirksam, Alt-Backup-Test grün) | Neue Migration `{Timestamp}_EnforceProgramRuleCursorNullStrandKeyUnique`; `OptionalRestoreTables`/`OptionalRestoreColumns` unverändert (verifiziert: `StrandKey` in `OptionalRestoreColumns` Z. 94); `ReadFromAsync_LegacyBackupWithoutStrandKeyColumn_RestoresSuccessfully` muss unverändert grün bleiben; Restore dedupliziert `NULL`-Zeilen vor dem Insert | `ReadFromAsync_BackupWithDuplicateNullStrandKeyRows_KeepsYoungest` (neue Hilfsmethode `AppendRowsAsync` am `LegacyBackupArchiveBuilder` ist eingeplant); bestehender Alt-Backup-Test als unverändert-grüner Regressionstest benannt | Abgedeckt |
| 7. Tests gegen echtes SQLite | Index-Tests via `ProgramScheduleTestDb` (`SqliteConnection` + `EnsureCreated` → Index wird von SQLite erzwungen); Migrations-Dedup via `IMigrator` auf echter SQLite-DB | Konkrete Testfälle benannt; Kern-Nachweis (zweite `NULL`-Zeile wird abgelehnt) vorhanden | Abgedeckt |

## Fehlende oder unvollständige Testanforderungen

- [ ] **Test zur deterministischen Cursor-Auswahl (Akzeptanzkriterium 4):** Es fehlt jeder Testnachweis, dass `SelectStrandCursor` bzw. die drei umgestellten Zugriffe bei Mehrfachtreffern die jüngste Zeile (`UpdatedAt` DESC, `Id` DESC) wählen. Da der gefilterte Index Duplikate auf Schemaebene verbietet, muss der Plan benennen, wie Duplikate im Test überhaupt hergestellt werden — z. B. Helfer `internal` machen und direkt unit-testen (Präzedenz: `StrandOrderKey` ist `internal static` und aus Tests aufrufbar) oder den Index per `DROP INDEX` (rohes SQL) auf der `EnsureCreated`-DB entfernen, Duplikate einfügen und `EnsureCoverageAsync`/`ReconcileAsync` bzw. `PersistCursors` ausführen. Auch das in Akzeptanzkriterium 4 geforderte definierte `PersistCursors`-Update-Verhalten (Gewinner-Zeile wird aktualisiert, nicht eine beliebige) ist ungetestet.

## E2E-Abdeckung

| Benutzerfluss / Akzeptanzkriterium | Geplanter E2E-Test | Status |
|------------------------------------|--------------------|--------|
| Kein über UI/Nutzeraktion erreichbarer Fluss — rein datenbank-/service-seitige Änderung (Requirement: „UI/Controller: keine") | Keine | Nicht erforderlich mit Begründung — der Plan begründet nachvollziehbar: Duplikate lassen sich über keinen Benutzerfluss erzeugen oder beobachten; Funktionsnachweis liegt auf echter-SQLite-Ebene |

## Fehlende oder unvollständige Planbestandteile

- [ ] **Fallback für den Migrationstest ist so nicht ausführbar:** Der Plan nennt als Fallback „Alt-Schema per SQL aufbauen und `IMigrator` nur für die neue Migration nutzen". `IMigrator.MigrateAsync(target)` wendet jedoch **alle** ausstehenden Migrationen bis zum Ziel an, ermittelt über `__EFMigrationsHistory`. Auf einem handgebauten Alt-Schema ohne befüllte History-Tabelle würde der Aufruf sämtliche Migrationen anwenden und an `CREATE TABLE` bereits vorhandener Tabellen scheitern. Der Plan muss ergänzen, wie die Beschränkung auf nur die neue Migration erfolgt — entweder `__EFMigrationsHistory` mit den IDs aller Vorgängermigrationen befüllen oder `IMigrator.GenerateScript(fromMigration, toMigration)` nur für die neue Migration erzeugen und als SQL ausführen.

## Hinweise

- **Testbarkeit des Dedup-Edge-Cases Restore:** Der geplante Restore-Dedup-Test verwendet ein Backup **mit** `StrandKey`-Spalte und zwei `NULL`-Zeilen. Der Helfer soll laut Design auch Zeilen **ohne** die Spalte (Alt-Backup) deduplizieren („fehlendem oder `null`-`StrandKey`"). Ein Alt-Backup ohne `StrandKey`-Spalte mit zwei Zeilen derselben `ProgramRuleId` ist mit `AppendRowsAsync` + `RemoveColumnsAsync` kombinierbar und wäre ein lohnender zweiter Fall — keine Pflichtlücke, aber nahe an der beanstandeten Stelle.
- **Sabotageprobe erweitern:** Schritt 7 sieht die Sabotageprobe für Index und Restore-Dedup vor; sobald ein Test für Akzeptanzkriterium 4 existiert, sollte die Probe auch das Entfernen der `OrderByDescending`/`ThenByDescending`-Kette umfassen.
- **Bestandsaufnahme produktiver Bestände:** Akzeptanzkriterium 1 kann lokal nur dokumentiert werden („kein realer Bestand prüfbar") — der Plan trägt dem korrekt Rechnung; die generische Datenmigration deckt produktive Duplikate unabhängig vom Untersuchungsergebnis ab.
- **Positiv festgehalten:** Umsetzungsreihenfolge erzwingt das gemeinsame Ausliefern von Migration (Schritt 3) und Restore-Dedup (Schritt 5) — sonst scheitern Alt-Backups mit Duplikaten am neuen Index; das ist im Plan explizit vermerkt. `has-pending-model-changes`, Debug- **und** Release-Build, volle Testsuite und Release-Notes-Eintrag sind enthalten (AGENTS.md §7 erfüllt); Hilfe-Doku-Verzicht ist begründet. AGENTS.md §3 (Snapshot-Kollision → Migration nach Rebase neu erzeugen) ist als Risiko benannt.
