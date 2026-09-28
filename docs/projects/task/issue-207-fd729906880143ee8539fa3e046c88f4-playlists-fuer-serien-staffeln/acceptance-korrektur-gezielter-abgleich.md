# Abnahmeprüfung – Korrektur gezielter Playlist-Abgleich

Unabhängige Prüfung (Claude, ohne Kontext des Implementierers) der Commits `b625d05`, `365b76d`, `cb99c84` auf dem
Branch `task/issue-207-...-korrektur-gezielter-abgleich`. Geprüft wurde der tatsächliche Code, ein Durchlauf mit dem
echten Scan-/Klassifizierungscode (temporäres Verzeichnis, echter `MediaSourceScanner`, `MediaSourceClassifier`,
`MediaSourceScanService`, `PlaylistBackfillWorker`, echtes SQLite), die echte Anwendung gegen eine ältere Datenbank
sowie Mutationstests. Die Probe-Tests wurden nach der Prüfung gelöscht und nicht committet.

## Ergebnis

**Status:** Erfüllt (die vier niedrigen Abweichungen wurden nach der Prüfung vom Orchestrator behoben, siehe „Behebung der Abweichungen“ am Ende; diese Behebungen wurden nicht mehr von einem separaten Prüf-Agenten gegengeprüft)
zwei Kleinigkeiten am Verhalten; keine der fünf fachlichen Anforderungen ist verfehlt)

Der Kern hält: Markierung beim Erfassen (Staffel UND Serie, Staffel → Serie, Film → Sammlung) atomar mit dem
Medium, nur betroffene Playlists werden verarbeitet, kein periodischer Lauf, ein persistent gemerkter Sicherheitslauf
pro Tag, `BackfillIntervalMinutes` ist entfernt. Ein echter Scan mit anschließender Nachlieferung funktioniert Ende zu
Ende. Volle Suite 1187/1187 (zweiter Lauf), Debug und Release bauen fehlerfrei.

## Abweichungen

- [ ] **Niedrig – Dokumentation: Erst-Scan „gebündelt, ein Upsert“ stimmt nur je Speichervorgang.**
  `playlists-business-rules.md` (BR-18, Punkt 1) sagt, bei einem großen Erst-Scan würden die Markierungen
  „gebündelt geschrieben (ein `INSERT ... ON CONFLICT DO UPDATE`, höchstens eine zusätzliche Abfrage)“. Das gilt für
  einen einzelnen `SaveChanges`-Aufruf mit vielen Episoden. Der echte Klassifizierer speichert aber je Staffel und je
  Episode einzeln (`MediaSourceClassifier.ProcessEpisodesForTVShowAsync`). Gemessen (500 Episoden, 10 Staffeln, echte
  SQLite-Datei, echter Scan): 510 Upsert-Befehle, dedupliziert auf 11 Markierungszeilen (1 Serie, 10 Staffeln);
  insgesamt 11 717 statt 11 207 SQL-Befehle (+4,6 %), Laufzeit im Mittel 24,0 s statt 23,0 s (+4 %, drei
  Wiederholungen, Streuung etwa ±1 s). Das Verhalten ist vertretbar, die Formulierung soll es aber richtig
  beschreiben (Deduplizierung greift über die Zeilen, nicht über die Befehle).
- [ ] **Niedrig – Dokumentation: Beispiel „per Metadaten-Bearbeitung umgehängter Film“.**
  `playlists-business-rules.md` (Punkt 1 und Punkt 3) und das Diagramm in `playlists-ablauf-technisch.md` nennen die
  Metadaten-Bearbeitung als Weg, der Medien außerhalb eines Scans anlegt bzw. umhängt. `MediaMetadataEditorService` ändert nur Eigenschaften,
  es gibt keinen Pfad, der `MovieCollectionId`/`TVShowSeasonId` setzt (Suche im gesamten Code). Tatsächliche Wege
  außerhalb der zwei Scan-Bereiche sind die „Neu erfassen“-Aktion im Explorer (`MediaSourceExplorer.razor`) und die
  Datenupgrades (`DataUpgradeManager`).
- [ ] **Niedrig – „Neu erfassen“ (Explorer) meldet sich nicht als Scan an.**
  `MediaSourceExplorer.razor` ruft `ScanCollectionTreeAsync` und `ClassifyCollectionTreeAsync` ohne
  `IPlaylistBackfillSignal.BeginScan()`. Es geht nichts verloren (jeder Commit signalisiert, die Markierungen sind
  versioniert), aber der Worker wird bei der ersten Markierung geweckt und läuft nach 10 s Beruhigungszeit
  möglicherweise mehrfach mit Zwischenständen, statt einmal am Ende. Kleine Änderung: derselbe `using`-Bereich wie im
  manuellen Komplettscan.
- [ ] **Niedrig – Uhr vorgestellt und zurückgestellt setzt den Sicherheitslauf aus.**
  Simuliert (`ManualTimeProvider`): Nach einem Sprung um +365 Tage und einem Lauf steht
  `PlaylistBackfillLastSweepAt` in der Zukunft; wird die Uhr zurückgestellt, ist der nächste Lauf erst nach dem
  Vorstellungszeitraum fällig (im Test 2027-09-18). Dauerläufe entstehen dabei nicht (geprüft), aber der Lauf kann
  „faktisch nie“ kommen (Rechner ohne Hardware-Uhr, falsche Zeit beim Booten, später per NTP korrigiert). Vorschlag:
  in `GetSafetySweepDueAtAsync` einen Zeitpunkt in der Zukunft als „fällig“ behandeln. Ein Rückstellen um 3 Tage
  verschiebt den Lauf nur um 3 Tage (geprüft).

## Beleg je Prüfpunkt

1. **Diff/zentrale Dateien gelesen.** Hook (`ApplicationDbContext.PlaylistBackfillMarking.cs`), Entität und
   Konfiguration, Migration `AddPlaylistBackfillMarkers` (Spalte `Setups.PlaylistBackfillLastSweepAt`, Tabelle,
   Unique-Index, Index `IX_PlaylistEntries_MediaType_MediaId`), Worker, Koordinator, Signal, Service, Scan-Ende-Signale,
   `PlaylistSettings`, `appsettings.json`, DI-Registrierung. `dotnet ef migrations has-pending-model-changes`: „No
   changes“, Snapshot passt zur Migration.

2. **Ende-zu-Ende mit echtem Scan – Erfüllt.**
   - Aufbau: temporäres Verzeichnis mit ShowA (Staffel 1, 2 Episoden), ShowB (Staffel 1, 2 Episoden), Filmordner
     „Sammlung“ (1 Film); Reader liest das echte Dateisystem. Erster echter Scan (Sequenz wie im manuellen
     Komplettscan): 2 Serien, 4 Episoden, 1 Film. Fünf Playlists über die echte `PlaylistService`-API: P1 Serie A, P2
     Serie B, P3 Staffel 1 von A, P4 Filmsammlung, P5 nur Einzelepisode.
   - Dann im Verzeichnis: neue Episode in A/Staffel 1, neue Staffel 2 (mit 1 Episode) in A, neuer Film in der
     Sammlung; zweiter echter Scan. Markierungen danach: Staffel 1 von A UND Serie A, die neue Staffel, die
     Filmsammlung; Serie B nicht. Playlists vor dem Worker unverändert (4, 4, 3, 2, 1 Einträge).
   - `PlanPendingAsync` liefert genau P1, P3, P4 (nicht P2, nicht P5). Nach dem Scan-Ende weckt das Signal den Worker:
     P1 4 → 7 (2 neue Episoden, 1 Staffel), P3 3 → 4 (nur die neue Episode von Staffel 1, keine aus Staffel 2), P4
     2 → 3, P2 und P5 unverändert; alle Markierungen danach entfernt. Signalfolge: `begin, notify, notify, notify,
     notify, end, wake` – der Worker wird erst nach dem Scan-Ende geweckt, nicht je Commit.
   - Zweiter Durchlauf mit dem echten `MediaSourceScanService` (Schleife, `BeginScan` im Dienst) plus laufendem
     `PlaylistBackfillWorker`: Nach dem Zeitsprung (Scan-Intervall) erkennt der Dienst die neuen Episoden, P1 4 → 7, P2
     unverändert bei 4, keine Markierung bleibt übrig, Signalfolge `begin, notify ×3, end, wake` (kein Wecken
     innerhalb eines Scans).
   - Erst-Scan einer großen Quelle: siehe erste Abweichung (11 Markierungszeilen für 500 Episoden, +4,6 % SQL-Befehle,
     +4 % Laufzeit). „Nicht spürbar langsamer“: bestätigt. Nebenbefund: Weil der Klassifizierer Serie und Staffeln in
     eigenen Speichervorgängen anlegt, entstehen beim Erst-Scan auch Markierungen für neu angelegte Serien/Staffeln
     (in keiner Playlist enthalten); die erste Verarbeitung entfernt sie ohne Playlist-Zugriff.

3. **Produktivpfad vs. Testpfad – Erfüllt.**
   - Die Hook-, Worker- und Koordinator-Tests laufen gegen echtes SQLite (`PlaylistServiceTestBase`, Shared-Memory-
     SQLite). Nur `InMemoryProvider_StillMarks_ThroughTheNonRelationalFallback` und die 19 Alt-Klassen nutzen den
     InMemory-Fallback; im Produktivbetrieb ist `Database.IsRelational()` immer wahr.
   - Upsert-SQL: Datenwerte (`MediaType`, `MediaId`, Zeitstempel) ausschließlich als `SqliteParameter`, der Tabellenname
     stammt aus dem EF-Modell und ist gequotet; keine Verkettung mit Daten.
   - Selbst gebaut, gegen echte SQLite-Datei: drei Kontexte parallel, je 30 Episoden in dieselbe Staffel: keine
     Ausnahme, beide Markierungen Version 90, 90 Episoden. `SaveChanges` innerhalb einer äußeren Transaktion:
     Rollback → weder Episode noch Markierung; Commit (auch synchroner `SaveChanges`) → Markierungen; ohne äußere
     Transaktion synchron: Version + 1. Episode in eine andere Staffel umgehängt → neue Staffel und Serie markiert
     (bisher ungetestet, jetzt belegt).
   - Start gegen eine ältere Datenbank: Datenbank per Migrator bis `AddPlaylistIsPublic` (Vorgänger) erzeugt, `Setups`
     mit `DataVersion = 0`. Die echte Anwendung (`dotnet VideoWebPlayer.dll`, Production) gestartet: Migration läuft
     (`Program.cs` ruft `MigrateDatabase` vor `app.Run`, also vor allen Hosted Services), Datenupgrades 1–8 laufen
     durch (inklusive `SaveChanges`), Scan-Dienst und Worker starten fehlerfrei, nach 30 s läuft der Sicherheitslauf
     einmal („0 Playlists geprüft“) und schreibt `PlaylistBackfillLastSweepAt`. Kein „no such table“, einzige Fehler
     sind der GitHub-Rate-Limit des Auto-Updates (unabhängig). Es gibt keinen `SaveChanges`-Aufruf vor der Migration
     (Suche in `Program.cs`, `Extensions`, Hosted Services). Ein alter Eintrag `Playlists__BackfillIntervalMinutes=1`
     wird ohne Fehler ignoriert.

4. **Race / doppelte Verarbeitung – Erfüllt.** Ablauf gelesen: Plan (Id, Version) → Blöcke → bedingtes Löschen
   `WHERE Id IN (...) AND Version = <verarbeitet>`; nach dem Plan neu entstandene Markierungen haben eine neue Id bzw.
   höhere Version und bleiben. Markierungen eines noch nicht committeten Scan-Schritts sind wegen der gemeinsamen
   Transaktion nicht sichtbar. Marker-Lauf und Sicherheitslauf sind im selben Worker-Loop serialisiert (kein
   gleichzeitiger Lauf im Prozess). Zwei Instanzen gleichzeitig (`BackfillPlaylistsAsync` parallel, echte
   SQLite-Datei, 40 neue Episoden): keine Duplikate (Eindeutigkeits-Index), 40 Einträge, keine Markierung übrig; ein
   Unique-Konflikt würde je Playlist abgefangen und die Markierung stehen lassen.
   **Mutationstests:**
   - Versionsprüfung beim Löschen entfernt (`&& m.Version == version`): `MarkerSetWhileProcessing_IsNotLost` schlägt fehl
     (der einzige Test dafür; er prüft den Fall gezielt und korrekt).
   - Markieren im Hook abgeschaltet (Kandidatensammlung leer): 27 Tests aus Hook-, Verarbeitungs-, Koordinator-,
     Worker-, Backfill- und Genre-Tests schlagen fehl (zusätzlich mein Ende-zu-Ende-Probetest).
   - Registrierung in `VideoWebPlayerBackupData` entfernt (Tabelle und Spalte): die Alt-Backup-Tests
     `ReadFromAsync_LegacyBackupWithoutPlaylistBackfillMarkers_RestoresSuccessfully` und
     `..._WithoutPlaylistBackfillLastSweepAtColumn_RestoresWithNull` schlagen fehl.
   Alle Mutationen mit `git checkout -- <Datei>` zurückgenommen (Arbeitsbaum sauber).

5. **Auslöser und Last – Erfüllt.** Der Worker hat keine Zeitschleife: `Task.WhenAny(Signal, ein Zeitgeber auf den
   persistierten Fälligkeitszeitpunkt)`, sonst nur Startlauf nach 30 s (`WhenIdle_TheWorkerDoesNotPoll` belegt: im
   Leerlauf keine SQL-Befehle; bei „nichts markiert“ eine Abfrage). Kein Minuten-Polling. Signal an allen Wegen:
   automatischer `MediaSourceScanService` (`BeginScan` bis Ende der Klassifizierung, auch bei Fehlern), manueller
   Komplettscan (`MediaSourceAdmin.razor`, Scope umschließt Scan, Klassifizierung, Genre-Reload). Ein Weg, der Medien
   anlegt und gar nicht signalisiert, wurde nicht gefunden: alle Kontexte kommen aus dem DI mit Signal
   (`AddDbContext`, kein `new ApplicationDbContext` im Produktivcode, keine `IDbContextFactory`); Roh-SQL/Massenbefehle
   legen keine Medien an (`ExecuteUpdate`/`ExecuteDelete` nur für Löschen und Flags). Der Weg ohne Scan-Bereich ist das
   „Neu erfassen“ im Explorer (siehe Abweichung 3), er signalisiert je Commit. Serverstart: offene Markierungen nach
   30 s; `BackfillSettleSeconds` (10 s) und Blockpausen (2 s) wirken im Test wie beschrieben.

6. **Sicherheitslauf – Erfüllt (mit Hinweis Uhrsprung).** Zwei Koordinator-Instanzen hintereinander innerhalb des
   Intervalls: nur einmal (der Zeitpunkt steht in der Datenbank); nach Intervall wieder; `0` = aus; blockweise mit
   Pausen (Test mit 5 Playlists, Block 2, 2 Pausen). Infrastrukturfehler (Gate wirft): Ausnahme, `PlaylistBackfillLastSweepAt`
   bleibt leer, der Worker versucht es nach einer Stunde erneut. Einzelne fehlgeschlagene Playlists lassen den Lauf
   als erledigt gelten (Hinweis: sie werden erst wieder über eine Markierung oder den nächsten Lauf versucht).
   Nach dem Update „nie gelaufen“: genau ein Lauf über alle Playlists 30 s nach dem ersten Start, blockweise mit
   Pausen (in der echten Anwendung gemessen: läuft, schreibt den Zeitpunkt, kein zweiter Lauf im selben Prozess) –
   vertretbar, und erwünscht, weil er Nachzügler aus der Zeit vor dem Update erfasst. Ein langer Sicherheitslauf hält
   markierungsgetriebene Läufe zurück (gleicher Worker), bis er fertig ist; bei sehr vielen Playlists (viele 25er-
   Blöcke à 2 s Pause) sind das Minuten, keine Datenverluste.

7. **Regression der Nachlieferungslogik – Erfüllt.** Per Diff: In `PlaylistBackfillServiceTests` und `_Genres` sind nur
   Methodennamen und der Aufruf (`RunBatchAsync` → `RunPendingBackfillAsync` über Plan, Block, Freigabe) geändert;
   alle Assertions sind unverändert (Ausschluss bewusst entfernter Titel, `MaxPlaylistItemCount` erreicht/teilweise,
   Manual → ans Ende, Genre-Neuberechnung, keine Duplikate, Playlist ohne Sammel-Eintrag). Die Arrangements erzeugen
   ihre Markierungen über den echten Hook (kein manuelles Setzen), die Tests laufen also tatsächlich über den
   Markierungspfad. Die zwei entfernten Cursor-Tests sind durch `ProcessPending_WorksInBlocksWithPausesBetweenThem`
   (5 Playlists, Block 2, 3 Blöcke, 2 Pausen) und `SafetySweep_NeverRan_IsDueAndRunsInBlocks_...` (Cursor über Blöcke)
   gleichwertig ersetzt; das „Wrap-around“ entfällt fachlich mit dem Cursor. Nebenwirkung ohne Belang: in zwei
   Testdateien wurde ein BOM ergänzt.

8. **Konfiguration/Doku/Backup – Erfüllt bis auf zwei Doku-Ungenauigkeiten (Abweichungen 1 und 2).**
   `BackfillIntervalMinutes` kommt in Code, `appsettings.json`, Tests und README nicht mehr vor; in Hilfe-Doku und
   Release Notes steht es nur noch als „entfällt“; das alte Abnahmeprotokoll `acceptance-schritt-8.md` ist ein
   historisches Archiv. Tabelle `PlaylistBackfillMarkers` (`OptionalRestoreTables`) und Spalte
   `Setups.PlaylistBackfillLastSweepAt` (`OptionalRestoreColumns`) registriert, Alt-Backup-Tests laufen (Mutation oben).
   Die Doku nennt die Grenzen ehrlich: Roh-SQL/Massenbefehle umgehen den Hook und werden erst vom täglichen Lauf
   gefunden (bei ausgeschaltetem Lauf gar nicht), Verzug bis Scan-Ende/Beruhigungszeit/Blockpause, Neustart mitten in
   der Verarbeitung.

9. **Testergebnis.** Erster Lauf der Suite vor meinen Probe-Tests: 1187/1187 grün (2 m 52 s). Nach dem Löschen der
   Probe-Tests: `dotnet build VideoPlayer.sln -c Release` und Debug ohne Fehler; erster Lauf 1186/1187, ein Ausfall:
   `Components.MediaSearchSelectorTests.EventCallback_OnMediaSelected_InvokedWithCorrectParameters` (Bunit-Komponente,
   7 s Laufzeit, nichts mit dem Abgleich zu tun); die Klasse einzeln sechsmal wiederholt: 6/6 grün; erneuter
   voller Lauf: 1187/1187 grün (3 m 2 s). Einordnung: einmaliges Flackern unter Last, kein echter Fehler.
   **Flackern der neuen zeitabhängigen Tests:** Worker-, Koordinator-, Signal- und Markierungstests elfmal
   nacheinander unter künstlicher Last (48 bzw. 100 Busy-Loops auf 24 Kernen): jedes Mal alle grün (45/45 bzw.
   20/20); keine Auffälligkeit. Sie arbeiten aber mit echten Wartezeiten (`Task.Delay`, „ruhig seit 300 ms“ als
   Leerlaufkriterium) – bei extremer Last theoretisch anfällig, in der Praxis nicht aufgefallen.

## Hinweise

- Die selbst gemeldeten Grenzen des Implementierers stimmen mit dem Code überein: Roh-SQL/`ExecuteSql`-Wege umgehen
  den Hook (im Produktivcode legt keiner Medien an), äußerer Rollback nach gesendetem Signal ist folgenlos (der
  Worker findet nichts), der InMemory-Fallback ist nur für Tests, eine markierte Playlist prüft alle ihre Sammel-
  Einträge. Auch umgehängte Episoden/Staffeln markieren (jetzt gezeigt).
- Der Hook löst je Speichervorgang mit relevanten Entitäten eine eigene Transaktion aus; für den echten Scan sind das
  rund 500 zusätzliche Upserts je 500 Episoden (siehe Abweichung 1), sonst ohne Belang.
- Einmalige, harmlose Nachwirkung: Beim Erst-Scan werden Markierungen für neu angelegte Serien/Staffeln gesetzt,
  die ohne Playlist-Bezug bei der ersten Verarbeitung wieder verschwinden.
- Nicht geprüft: zwei gleichzeitig laufende Serverinstanzen gegen dieselbe Datenbank (nicht vorgesehen; die parallele
  Verarbeitung im selben Prozess mit zwei Kontexten zeigt, dass Unique-Konflikte nicht zu Schaden führen).

## Behebung der Abweichungen

1. **Doku „ein Upsert“ beim Erst-Scan (BR-18):** korrigiert. Die Bündelung gilt je Speichervorgang; der Klassifizierer speichert je Episode einzeln, bei 500 Episoden entstehen rund 500 Upserts (deduplizierte 11 Zeilen, etwa +5 % SQL-Befehle, etwa +4 % Laufzeit, Messwerte der Prüfung).
2. **Doku „per Metadaten-Bearbeitung umgehängter Film“:** korrigiert. Es gibt keinen solchen Pfad über die Oberfläche; der Hook erfasst jeden Entity-Framework-Weg, der Medien anlegt oder umhängt. Auch die Überschrift im technischen Ablauf („Scan / Metadaten-Bearbeitung“) angepasst.
3. **„Neu erfassen“ im Quellen-Explorer meldete sich nicht als Scan an:** `MediaSourceExplorer.razor` ruft jetzt wie der Komplettscan `IPlaylistBackfillSignal.BeginScan()` auf; der Worker wird am Ende einmal geweckt und nicht je Commit mit Zwischenständen. Doku nennt den Weg. Kein eigener Test (Komponente mit Scanner/Classifier ohne bestehende Testabdeckung); `BeginScan` selbst ist durch die Signal-Tests abgedeckt.
4. **Uhrsprung des Sicherheitslaufs:** Ein gespeicherter Zeitpunkt des letzten Laufs, der in der Zukunft liegt, gilt jetzt als ungültig; der Lauf ist dann sofort fällig und speichert den richtigen Zeitpunkt (statt für die Dauer des Uhrsprungs auszusetzen). Neuer Test `SafetySweep_StoredTimeInTheFuture_CountsAsNeverRan_AndRunsAgain`, Doku (BR-18) ergänzt.

**Hinweis Architektur:** Ein Hook des Projekts meldet, dass Blazor-Komponenten keine Services direkt injizieren sollen. `MediaSourceExplorer.razor` injizierte `ApplicationDbContext`, `MediaSourceScanner` und `MediaSourceClassifier` schon vorher direkt (ebenso `MediaSourceAdmin.razor`); die Signal-Injektion folgt demselben Muster.

Testlauf danach: 1188 Tests. Im ersten Gesamtlauf fielen fünf zeitabhängige Tests aus (vier in `MediaSearchSelectorTests`, bUnit-Debounce, und `PlaylistPlaybackE2ETests.PlaylistBadgeDisplayE2ETest`, Playwright-Zeitüberschreitung); diese Klassen bestanden danach dreimal einzeln (28 von 28, darunter der neue Test). `dotnet build VideoPlayer.sln` in Debug und Release: 0 Fehler.
