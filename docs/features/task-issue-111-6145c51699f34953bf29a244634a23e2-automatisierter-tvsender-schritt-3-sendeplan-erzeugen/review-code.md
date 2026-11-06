# Code-Review (Schritt 9)

## Ergebnis

**Status:** Erfüllt (Befunde unten alle behoben oder als akzeptiert dokumentiert)

## Geprüfte Stellen

### Korrektheit
- **`ProgramScheduleService.EnsureCoverageAsync`**: Extend-only (`coverageStart = max(now, lastEnd)`); Lade-Fenster für persistierte Slots `End > coverageStart && Start < horizonEnd` — reicht, weil jeder neue Slot, der belegte Vergangenheit überlappen könnte, vor `coverageStart` endet und verworfen wird.
- **Lookback-Handling:** Ausstrahlungen im 24-h-Rückblick werden materialisiert; Slots, die vor `coverageStart` enden, entfallen (keine Doppel-Persistierung historischer Inhalte), konsumieren aber den Cursor — eine über Mitternacht laufende Sendung behält ihren echten `Start`. Mit dem Fix `SlotsCreated += ranges.Count` werden nur persistierte Slots gezählt.
- **Konfliktauflösung:** Sortierung `(Priority desc, RuleId asc, Start asc)`; atomarer Entfall über `blocked`-Intervalle (persistiert + bereits gewonnen). Cursor wird nicht fortgeschrieben, weil `Commit` erst nach erfolgreicher Platzierung läuft — korrekt.
- **Füller:** Round-Robin über `(Priority desc, Id asc)`; `PeekAt(0)`/`Commit(1)`; Items müssen in die Restlücke passen; `fillAt >= gap.End`-Break verhindert Mehrfach-Overshoot; am Horizont-Ende ist Overshoot erlaubt (echte Abdeckung statt Restlücke). Deterministisch.
- **Freigabe:** `LoadAccessContextAsync` bildet Schnittmengen über **alle** Nicht-Admin-Benutzer (Quellenzugriff bzw. `UnlockedMediaEntry` pro Serie/Filmsammlung) — konsistent zur `IsAccessible`-Semantik; ohne Nicht-Admins alles freigegeben. Verwaltung bleibt serverseitig (Service-Layer).
- **Feiertage:** Computus korrekt (verifiziert gegen 2026-Referenzdaten); Bundesland-Filter per `DE-*`-Listen; `null` = bundesweit.
- **Migration:** `20261021045013_AddProgramSchedule` erzeugt `ProgramSlots` (FK→TVChannels kaskadierend, Index `(TVChannelId, Start)`), `ProgramRuleCursors` (FK→ProgramRules kaskadierend, unique), zwei Setup-Int-Spalten mit Defaults. Snapshot regeneriert.

### Risiken / Design-Beobachtungen
- **`MaterializeOccurrence` Overlap-Check** ist O(Slots × Blocked) — für ~48 h × Minutenslots ausreichend; kein N+1-DB-Zugriff (alle Reads sind Sammel-Queries).
- **Expand-only:** Änderungen an Regeln wirken erst nach freiem Planende — bewusste Schritt-4-Abgrenzung.
- **`ProxyEpisode`-Hilfsrecord** trägt Id+MediaSourceId für die einheitliche `ToItem`-Prüfung — bewusst minimal.
- **`ProgramSlot`-Referenzen** (`MediaItemId`, `MovieId`, `TVShowEpisodeId`, `ProgramRuleId`) sind bewusst weich (kein FK) — Vorgabe aus der Schritt-2-Abnahme für das „verdrängt"-Konzept in Schritt 4; Verwaister-Verweis-Test vorhanden.

### Tests gegengeprüft
- Echtes SQLite (`mode=memory&cache=shared`), keine InMemory-Fakes → FK/Unique-Verhalten real.
- Der Freigabe-Test beweist beide Richtungen (ausschließen → planbar nach Freigabe bzw. Freischaltung für alle).
- Der Cursor-Test beweist Fortsetzung über zwei Läufe inkl. Wrap-around.
- E2E: `Admin_CreateEditDeactivateDeleteRule` setzt die neuen Felder und prüft die Statusmeldung; `NonAdmin_SeesNotAuthorized` unverändert.

## Offene Punkte

Keine.
