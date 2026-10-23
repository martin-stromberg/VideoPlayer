# Plan-Gegenprüfung

## Ergebnis

**Status:** Plan vollständig

## Abgleich Akzeptanzkriterien

| Akzeptanzkriterium | Umsetzung im Plan | Testnachweis im Plan | Status |
|--------------------|-------------------|----------------------|--------|
| Aktion „Probe zurücksetzen" pro Eintrag in `tvsender-probe-failed-detail` (neue Aktions-Spalte, Muster `btn btn-sm`/`actions-cell`) | Ablauf 1 + Schritt 4: `actions-cell`-Spalte, Zeilen-Button, Handler `ResetProbeItemAsync` | bUnit `ProbeFailedRow_ResetButton_ResetsItemAndRefreshes`; E2E-Szenario Einzel-Reset | Erfüllt |
| Massen-Reset über alle fehlgeschlagenen Items (ggf.-Anforderung, als Designentscheidung mit umgesetzt) | Ablauf 2 + Schritt 4: `#tvsender-probe-reset-all`, `ResetAllFailedProbesAsync` | bUnit `ProbeResetAll_ConfirmDialog_ResetsAllAndShowsStatus`; Service `ResetFailedProbesAsync_ResetsAllFailedItemsBeyondDetailCap`; E2E-Szenario Massen-Reset | Erfüllt |
| Bestätigungsdialog für Massen-Reset (`admin-dialog-overlay`/`admin-dialog`-Muster) | Ablauf 2: `confirmProbeReset`-Flag + Dialog nach `confirmReset`-Muster inkl. Abbrechen/Overlay-Klick | bUnit `ProbeResetAll_ConfirmDialog_…` (Abbrechen + Bestätigen); E2E prüft Dialog-Erscheinen | Erfüllt |
| Reset-Semantik `DurationProbedAt = null`, `DurationProbeAttempts = 0` (analog `MediaSourceScanner`) | Beide Service-Methoden via `ExecuteUpdateAsync` (Abläufe 1/2) | `ResetFailedProbeAsync_FailedItem_ClearsProbeStateAndBecomesPending` | Erfüllt |
| `MediaDurationProbeDetailItem` um `MediaItem.Id` erweitern (eindeutige Identifikation); `FailedItems` bleibt Datenquelle | Schritt 1: Parameter `long MediaItemId`, `DetailItemsAsync` projiziert `mi.Id` | `GetProgressAsync_DetailItems_CarryMediaItemId` | Erfüllt |
| Admin-only-Einzel-Reset pro `MediaItem.Id` mit Erfolg/Fundstatus-Rückgabe | `ResetFailedProbeAsync` → `Task<bool>` mit `RequireAdminAsync` + Failed-Guard | `…_ProbedItem_ReturnsFalseAndKeepsDuration`, `…_PendingOrUnknownItem_ReturnsFalse`, `…_NonAdmin_Throws` | Erfüllt |
| Admin-only-Massen-Reset über volle Fehlermenge jenseits `detailItemCount`-Kappung (Rückgabe = Anzahl) | `ResetFailedProbesAsync` → `Task<int>` mit `VideoExtensionPredicate()` + Failed-Prädikat | `…_ResetsAllFailedItemsBeyondDetailCap` (> 25 Items), `…_NonAdmin_Throws` | Erfüllt |
| `GetProgressAsync`/`DetailItemsAsync` um ID-Projektion erweitern | Änderung an `DetailItemsAsync` (Schritt 1); `GetProgressAsync` ruft sie | siehe `MediaItemId`-Test | Erfüllt |
| Wiederaufnahme durch `ProbePendingAsync`/Worker ohne Codeänderung (nächster Tick) | Ablauf 3 + Designentscheidung (kein Sofort-Probe, kein Signal) | `ProbePendingAsync_PicksUpResetItem` | Erfüllt |
| Nach Reset `probeProgress` neu laden + Statusmeldung (Muster `scheduleStatus`/`rulesStatus`) | Neues `probeStatus`-Feld + `#tvsender-probe-action-status`, `LoadProbeProgressAsync` in beiden Handlern | bUnit-Tests prüfen Statusmeldung; E2E-Szenario 1 „Statusmeldung erscheint" | Erfüllt |
| Serverseitige Berechtigung (ausgeblendete Schaltfläche reicht nicht) | `RequireAdminAsync` in beiden Methoden; Validierungsregeln-Tabelle | `ResetFailedProbeAsync_NonAdmin_Throws`, `ResetFailedProbesAsync_NonAdmin_Throws` (`ForbiddenAccessException`/`UnauthorizedAccessException`) | Erfüllt |
| Erfolgreich geprobte Items bleiben unberührt; Nicht-Fehlgeschlagene beim Einzel-Reset nicht zurückgesetzt | Failed-Guard im Prädikat beider Methoden | `…_ProbedItem_ReturnsFalseAndKeepsDuration`, `…_LeavesProbedAndPendingUntouched`, `…_PendingOrUnknownItem_ReturnsFalse` | Erfüllt |
| Keine Reset-Aktion in `tvsender-probe-pending-detail` (Nicht-Anforderung, offene Frage 2) | Designentscheidung: Pending-Tabelle unverändert | bUnit-Test prüft explizit Fehlen des Buttons in `tvsender-probe-pending-detail` | Erfüllt |
| Kein neuer Worker, keine neue Seite, kein Interface, keine Konfiguration, keine EF-Migration/Backup-Änderung | Designentscheidungen + Abschnitte „Neue Klassen: Keine", „Datenbankmigrationen: Keine", „Konfigurationsänderungen: Keine" | — | Erfüllt |
| Kein `NotifyScheduleChanged()` beim Reset | Designentscheidung begründet (Probe-Worker hat kein Signal; Reset macht nichts planbar) | — | Erfüllt |
| Dokumentation `docs/help/videolaufzeit.md` + `docs/RELEASE_NOTES.md` | Schritt 7 | — | Erfüllt |

## E2E-Abdeckung

| Benutzerfluss / Akzeptanzkriterium | Geplanter E2E-Test | Status |
|------------------------------------|--------------------|--------|
| Einzel-Reset aus geöffneter Fehlerliste: Button-Klick → Item verlässt `tvsender-probe-failed-detail`, Failed-Chip zählt herunter, Pending-Chip zählt hoch, Statusmeldung erscheint | Erweiterung `TVSenderAdminE2ETests.Admin_CreateEditDeactivateDeleteRule` (Szenario „Hoch") | Erfüllt |
| Massen-Reset mit Bestätigungsdialog: „Alle zurücksetzen" → `admin-dialog` → Bestätigen → „0 fehlgeschlagen", Fehlerliste entfällt | Erweiterung desselben Tests mit zweitem Failed-Seed `e2e-defekt-2.mkv` (Szenario „Hoch") | Erfüllt |
| Dialog-Variante „Abbrechen" | Nur bUnit (`ProbeResetAll_ConfirmDialog_…` prüft Abbrechen), kein E2E | Vertretbar — bUnit deckt „Klick → kein Service-Aufruf" ausreichend ab; siehe Hinweise |

## Hinweise

- **Bestehender bUnit-Test nicht gelistet, aber unkritisch:** `TVSenderAdminTests` (Test bei
  `TVSenderAdminTests.cs:134-154`) assertet `TextContent.Contains(...)` auf
  `tvsender-probe-detail-row` — die neue Spalte bricht Contains-Assertions nicht. In der
  Tabelle „Betroffene bestehende Tests" fehlt er; bei Umsetzung kurz gegenprüfen, ob eine
  Assertion spalten- oder colspan-sensitiv ist.
- **E2E-Seiteneffekt auf `tvsender-probe-recent`:** Das zweite Failed-Seed
  (`e2e-defekt-2.mkv`) erscheint je nach `DurationProbedAt`-Zeitstempel auch in der
  Rolling-Liste `tvsender-probe-recent`; die bestehende Positions-Assertion
  `probeRows.Nth(2)` (`TVSenderAdminE2ETests.cs:136`) kann dadurch verschoben werden. Der
  Plan erwähnt nur die Chip-Erwartung — beim Seeden auf Reihenfolge/Zeitstempel achten.
- **Testdaten-Hinweis aus der Bestandsaufnahme:** `ProgramScheduleTestDb.CreateMediaItemAsync`
  setzt `DurationProbedAt = UtcNow` (inventory/tests.md:59) — für Failed-/Pending-Seeds
  sind `DurationProbedAt`/`DurationProbeAttempts` nachträglich zu korrigieren; für den
  Kappungs-Test (> 25 Items) ist ein `SeedItemAsync`-Analogon in der neuen Testdatei
  `MediaDurationProbeServiceTests_Reset.cs` anzulegen. Im Plan als Voraussetzung
  („`ProgramScheduleTestDb`-Fixture (vorhanden)") nur knapp benannt, aber ausreichend.
- **E2E-Zeitverhalten gut vorweggenommen:** Der Risiko-Abschnitt adressiert, dass ein
  Probe-Worker-Tick das zurückgesetzte Item im E2E-Lauf erneut proben kann, und legt die
  Assertions entsprechend auf den Failed-Chip — verhindert erwartbares Flackern.
- **Abbrechen-Pfad ohne E2E:** Der Plan begründet E2E-Bedarf über DOM-/Event-Verhalten;
  „Abbrechen" wird nur per bUnit geprüft. Fachlich akzeptabel, da die Abbrechen-Wirkung
  (kein Service-Aufruf, keine Datenänderung) in bUnit zuverlässig nachweisbar ist — bei
  strenger Lesart wäre eine Ergänzung des E2E-Abschnitts um eine Zeile „Abbrechen" denkbar.
