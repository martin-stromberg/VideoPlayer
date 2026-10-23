# Umsetzungsplan: TV-Sender: Fehlgeschlagene Laufzeit-Probes zurücksetzen (Issue #258)

## Übersicht

Auf der Adminseite `/admin/tv-sender` (Tab „Laufzeitermittlung") wird die rein informative
Fehlerliste `tvsender-probe-failed-detail` um einen Einzel-Reset pro Zeile und einen
Massen-Reset über alle dauerhaft fehlgeschlagenen Video-Items ergänzt. Fachlich setzt der
Reset `DurationProbedAt = null` und `DurationProbeAttempts = 0` (dieselbe Semantik wie der
automatische Reset in `MediaSourceScanner`), sodass `ProbePendingAsync` die Items beim
nächsten Worker-Tick wieder aufnimmt. Dafür tragen `MediaDurationProbeDetailItem` künftig die
`MediaItem.Id`, und `MediaDurationProbeService` erhält zwei Admin-Methoden nach dem
`RequireAdminAsync`-Muster.

## Designentscheidungen

| Komponente/Bereich | Gewählter Ansatz | Begründung |
|---|---|---|
| Item-Identifikation | `MediaDurationProbeDetailItem` wird um einen führenden Parameter `long MediaItemId` erweitert; `DetailItemsAsync` projiziert `mi.Id` | `Name` ist nicht eindeutig (Anforderung). Die ID fällt für `PendingItems` automatisch mit an — kein Mehraufwand, kein Nachteil. |
| Einzel-Reset-Implementierung | `ResetFailedProbeAsync(userId, mediaItemId)` als `Task<bool>` über `ExecuteUpdateAsync` mit kombiniertem Prädikat `Id == mediaItemId && Duration == null && DurationProbeAttempts >= EffectiveMaxAttempts`; Rückgabe = betroffene Zeilen > 0 | Atomar in einer SQL-Anweisung: der Failed-Guard schützt erfolgreich geprobte Items ohne Lade-Roundtrip (ein falscher Reset würde sonst `Duration` verlieren). Entspricht dem `ExecuteDeleteAsync`-Muster der Vorbilder `DeleteDisplacedSlotsAsync`/`ResetScheduleAsync`. |
| Massen-Reset-Implementierung | `ResetFailedProbesAsync(userId)` als `Task<int>` über `ExecuteUpdateAsync` auf die volle Failed-Menge inkl. `VideoExtensionPredicate()` | `FailedItems` ist auf `detailItemCount` (Default 25) gekappt, `FailedCount` kann größer sein — clientseitiges Iterieren über die Zeilen würde den Rest nicht erfassen. Serverseitige Mengenoperation ist einmalig statt N Updates. |
| Massen-Reset-Umfang (offene Frage 1) | Wird mit umgesetzt; Umfang = **alle** dauerhaft fehlgeschlagenen Video-Items (`Duration == null && DurationProbeAttempts >= maxAttempts`), nicht nur die angezeigten | „ggf. Massen-Reset" der Anforderung: geringer Mehraufwand, und nur ein serverseitiger Gesamt-Reset erfasst die Kappungs-Lücke konsistent. |
| Reset in Pending-Liste (offene Frage 2) | Nein — `tvsender-probe-pending-detail` bleibt ohne Aktions-Spalte | Die Anforderung nennt nur fehlgeschlagene Items; der Retry-Wartezyklus (`RetryDelaySeconds`) ist reguläres Verhalten, kein Fehlerzustand. |
| Zeitverhalten nach Reset (offene Frage 3) | Kein Sofort-Probe — der `MediaDurationProbeWorker` greift zurückgesetzte Items im `MediaProbe:IntervalSeconds`-Takt (Default 60 s) auf | Ein Aufweck-Signal für den Probe-Worker existiert nicht; `IProgramScheduleSignal.NotifyScheduleChanged()` weckt den Planungs-Worker, nicht den Probe-Worker, und ist hier fachlich falsch (ein Reset allein macht nichts planbar). 60 s Verzögerung ist für eine manuelle Admin-Aktion akzeptabel. |
| Bestätigungsdialog (offene Frage 4) | Einzel-Reset ohne Dialog direkt ausführen; Massen-Reset über das bestehende Dialogmuster `admin-dialog-overlay`/`admin-dialog` (Vorbild `confirmReset`) | Der Einzel-Reset ist gefahrlos und reversibel (schlimmstenfalls ein erneuter Probe-Versuch); destruktiv wirkende Sammelaktionen haben im Projekt durchgehend Dialoge. |
| Statusmeldung | Neues Feld `probeStatus` + Anzeige-Element `#tvsender-probe-action-status` im Bereich der Probe-Karte | Die ID `tvsender-probe-status` ist bereits durch den `ProbeStatusLabel`-Absatz belegt; die bestehenden Statusmeldungen (`scheduleStatus`, `rulesStatus`) folgen demselben `form-text`-Muster. |
| Kein eigenes Interface | Die Reset-Methoden kommen direkt auf die konkrete Klasse `MediaDurationProbeService` | Die Klasse hat bewusst kein Interface und wird bereits direkt in `TVSenderAdmin` injiziert — kein neuer Baustein. |

## Programmabläufe

### Ablauf 1: Einzel-Reset einer fehlgeschlagenen Datei

1. Der Administrator öffnet den Tab „Laufzeitermittlung" und klappt
   `tvsender-probe-failed-detail` auf (bestehendes `chip-detail-btn`-Toggle).
2. Jede Zeile `tvsender-probe-detail-row` der Fehlerliste trägt in der neuen Aktionsspalte
   (`actions-cell`, Muster der Regel-Tabelle `TVSenderAdmin.razor:121-125`) eine
   `btn btn-sm`-Schaltfläche „Probe zurücksetzen".
3. Klick ruft den neuen Handler `ResetProbeItemAsync(MediaDurationProbeDetailItem item)`
   auf, der `ProbeService.ResetFailedProbeAsync(userId, item.MediaItemId, …)` aufruft.
4. `MediaDurationProbeService.ResetFailedProbeAsync`:
   - `RequireAdminAsync(userId)` — unbekannter Benutzer → `UnauthorizedAccessException`,
     Nicht-Admin → `ForbiddenAccessException`.
   - `ExecuteUpdateAsync` auf `MediaItems` mit Prädikat
     `mi.Id == mediaItemId && mi.Duration == null && mi.DurationProbeAttempts >= EffectiveMaxAttempts`;
     gesetzt werden `DurationProbedAt = null` und `DurationProbeAttempts = 0`
     (`Duration` ist in der Failed-Menge per Definition bereits `null`).
   - Rückgabe `true`, wenn genau eine Zeile geändert wurde, sonst `false` (Item nicht
     gefunden, nicht (mehr) fehlgeschlagen oder inzwischen gelöscht); `LogInformation` analog
     `DeleteDisplacedSlotsAsync`.
5. Der Handler setzt `probeStatus` („… wurde zurückgesetzt und wird beim nächsten
   Ermittlungslauf erneut geprüft." bzw. „… ist nicht (mehr) fehlgeschlagen."), lädt
   `probeProgress` über `LoadProbeProgressAsync` neu und fängt Fehler in `probeStatus`
   (`ex.Message`, Muster `CleanupDisplacedAsync`/`ResetScheduleAsync`).
6. Der `MediaDurationProbeWorker` nimmt das Item beim nächsten Tick über
   `ProbePendingAsync` wieder auf — `DurationProbedAt == null` ist dort immer auswählbar
   (`MediaDurationProbeService.cs:103`).

Beteiligte Klassen: `TVSenderAdmin`, `MediaDurationProbeService`,
`MediaDurationProbeDetailItem`, `MediaItem`, `MediaDurationProbeWorker` (indirekt).

### Ablauf 2: Massen-Reset aller fehlgeschlagenen Dateien

1. Im Bereich `tvsender-probe-failed-detail` steht eine Schaltfläche „Alle zurücksetzen"
   (`btn btn-sm btn-danger`), die das Flag `confirmProbeReset` setzt.
2. Daraufhin rendert die Seite einen Bestätigungsdialog `admin-dialog-overlay` +
   `admin-dialog` mit `@onclick:stopPropagation` (Muster `confirmReset`,
   `TVSenderAdmin.razor:651-665`): erklärender Text, dass **alle** `FailedCount`
   fehlgeschlagenen Dateien erneut in die Ermittlung aufgenommen werden, Bestätigungs-Button
   (z. B. `#probe-reset-all-confirm`) und „Abbrechen"; Overlay-Klick schließt den Dialog.
3. Bestätigen ruft den neuen Handler `ResetAllFailedProbesAsync()` auf: Flag zurücksetzen,
   `ProbeService.ResetFailedProbesAsync(userId, …)`.
4. `MediaDurationProbeService.ResetFailedProbesAsync`:
   - `RequireAdminAsync(userId)`.
   - `ExecuteUpdateAsync` auf `MediaItems` mit `VideoExtensionPredicate()` und
     `mi.Duration == null && mi.DurationProbeAttempts >= EffectiveMaxAttempts`; gesetzt
     werden `DurationProbedAt = null`, `DurationProbeAttempts = 0`.
   - Rückgabe: Anzahl geänderter Zeilen (`int`); `LogWarning` analog `ResetScheduleAsync`.
5. Der Handler meldet die Anzahl in `probeStatus` („N Datei(en) wurden zurückgesetzt…" bzw.
   „Es waren keine fehlgeschlagenen Dateien vorhanden.") und lädt `probeProgress` neu; die
   15-s-`ProbeProgressLoopAsync` zieht den weiteren Fortschritt ohnehin nach.

Beteiligte Klassen: `TVSenderAdmin`, `MediaDurationProbeService`, `MediaItem`,
`MediaDurationProbeWorker` (indirekt).

### Ablauf 3: Wiederaufnahme durch den Probe-Worker (unverändert, nur Kontext)

`MediaDurationProbeWorker.ExecuteAsync` ruft im `EffectiveIntervalSeconds`-Takt
`ProbePendingAsync(EffectiveBatchSize)`; die Selektion
(`Duration == null && (DurationProbedAt == null || (Attempts < max && ProbedAt <= retryThreshold))`)
schließt zurückgesetzte Items sofort wieder ein. Keine Codeänderung.

## Neue Klassen

Keine.

## Änderungen an bestehenden Klassen

### `VideoWebPlayer/Services/MediaDurationProbeProgress.cs`

- Record `MediaDurationProbeDetailItem` erhält einen führenden Parameter
  `long MediaItemId` (Identität des `MediaItem`, Schlüssel für den Einzel-Reset).
  XML-Doc entsprechend ergänzen. `MediaDurationProbeRecentItem` bleibt unverändert
  (die Rolling-Liste hat keine Aktionen).

### `VideoWebPlayer/Services/MediaDurationProbeService.cs`

- Neu: `public Task<bool> ResetFailedProbeAsync(string userId, long mediaItemId, CancellationToken cancellationToken)` —
  Admin-Prüfung + `ExecuteUpdateAsync` mit Failed-Guard (siehe Ablauf 1); `LogInformation`.
- Neu: `public Task<int> ResetFailedProbesAsync(string userId, CancellationToken cancellationToken)` —
  Admin-Prüfung + `ExecuteUpdateAsync` über die volle Failed-Menge mit
  `VideoExtensionPredicate()` (siehe Ablauf 2); `LogWarning`.
- Geändert: `DetailItemsAsync` projiziert zusätzlich `mi.Id` in den neuen
  `MediaItemId`-Parameter des Records.
- Unverändert: `ProbePendingAsync` (Selektion greift zurückgesetzte Items bereits),
  `RequireAdminAsync` (wird wiederverwendet).

### `VideoWebPlayer/Components/Pages/Admin/TVSenderAdmin.razor`

- Tabelle in `tvsender-probe-failed-detail` (Z. 567-598): zusätzliche Spalte „Aktionen"
  im `<thead>` und `<td class="actions-cell">` mit `btn btn-sm`-Schaltfläche
  „Probe zurücksetzen" (z. B. Klasse `tvsender-probe-reset`) je Zeile; der
  Kappungs-Hinweis „… und N weitere" bekommt `colspan="4"` statt `3`.
- Massen-Reset-Schaltfläche „Alle zurücksetzen" (`btn btn-sm btn-danger`, z. B.
  `#tvsender-probe-reset-all`) im `tvsender-probe-failed-detail`-Block; öffnet per
  neuem Flag `confirmProbeReset` einen Dialog nach dem `confirmReset`-Muster.
- Neuer Bestätigungsdialog `admin-dialog-overlay`/`admin-dialog` (Markup-Block neben
  `confirmCleanup`/`confirmReset`, Z. 635-665).
- Neuer Zustand: `bool confirmProbeReset`, `string? probeStatus` (Z. 700-718).
- Neue Anzeige `probeStatus` als `form-text`-Element `#tvsender-probe-action-status`
  am Ende der Probe-Karte (Muster `tvsender-rules-status`, Z. 132-135).
- Neue Handler: `ResetProbeItemAsync(MediaDurationProbeDetailItem)` und
  `ResetAllFailedProbesAsync()` — Service-Aufruf mit `userId`, Statusmeldung,
  `LoadProbeProgressAsync`, Fehler in `probeStatus` (Muster `CleanupDisplacedAsync`,
  Z. 914-932).
- `tvsender-probe-pending-detail` und `tvsender-probe-recent` bleiben unverändert.

### `VideoWebPlayer/Services/MediaDurationProbeWorker.cs`

Keine Änderung (nächster Tick reicht, siehe Designentscheidung).

### Dokumentation

- `docs/help/videolaufzeit.md`: Abschnitt „Fortschritt beobachten" um das Zurücksetzen
  fehlgeschlagener Ermittlungen (Einzel + alle, Wirkung = neuer Versuch beim nächsten
  Lauf) ergänzen.
- `docs/RELEASE_NOTES.md`: Eintrag für die neue Admin-Aktion.

## Datenbankmigrationen

Keine. Es werden nur Werte bestehender Spalten verändert; `DurationProbedAt` und
`DurationProbeAttempts` stehen bereits in `VideoWebPlayerBackupData.OptionalRestoreColumns`.

## Validierungsregeln

| Regel | Durchsetzung |
|---|---|
| Nur Administratoren dürfen zurücksetzen | Serverseitig über `RequireAdminAsync` in beiden Reset-Methoden (`UnauthorizedAccessException`/`ForbiddenAccessException`) — die Schaltflächen allein gelten nicht als Absicherung |
| Einzel-Reset nur auf Items der Failed-Menge | Failed-Guard im `ExecuteUpdateAsync`-Prädikat (`Duration == null && DurationProbeAttempts >= EffectiveMaxAttempts`) — schützt erfolgreich geprobte Items davor, `Duration` zu verlieren |
| Massen-Reset nur auf Video-Items der Failed-Menge | `VideoExtensionPredicate()` + Failed-Prädikat im `ExecuteUpdateAsync` |

## Konfigurationsänderungen

Keine. `MediaProbe:MaxAttempts` und `MediaProbe:IntervalSeconds` bleiben die
maßgeblichen Schwellen.

## Seiteneffekte und Risiken

- **Probe-Last:** Ein Massen-Reset stellt alle fehlgeschlagenen Items wieder an; der
  Worker verarbeitet pro Tick nur `EffectiveBatchSize` (Default 10) — die Wiederaufnahme
  verteilt sich über viele Ticks, kein Lastsprung, aber auch kein sofortiger
  Abschluss.
- **Erneutes Scheitern:** Items, deren Datei weiterhin defekt ist, sammeln nach dem
  Reset wieder Versuche und landen nach `MaxAttempts` erneut in der Fehlerliste —
  gewolltes Verhalten, in der Hilfe zu erwähnen.
- **Change-Tracker:** `ExecuteUpdateAsync` umgeht den EF-Change-Tracker; `TVSenderAdmin`
  und die Tests tracken keine `MediaItem`-Entitäten, die stale würden — unkritisch,
  aber im Test ist ggf. `Entry(item).ReloadAsync()`/neuer Fixture-Kontext nötig.
- **Race auf dem Datensatz:** Zwischen Listenanzeige und Klick kann ein Item per
  Rescan gelöscht oder geändert worden sein → `ExecuteUpdateAsync` trifft 0 Zeilen,
  Rückgabe `false`, UI meldet „nicht (mehr) fehlgeschlagen" — abgedeckt.
- **E2E-Zeitverhalten:** Nach dem Reset wird das Item ausstehend; läuft im E2E-Server
  ein Probe-Worker-Tick (initiale Verzögerung ~10 s, dann 60 s) und ist ffprobe
  verfügbar, wird das Item erneut geprobt und bleibt bei Transient-Fehlschlag
  ausstehend. Chip-Assertions nach dem Reset müssen daher auf den `Failed`-Chip
  („0 fehlgeschlagen") zielen; die Pending-Zahl nur über die Seed-Menge begründen
  (Retry-Delay schützt `e2e-wartet.mkv`, das frisch zurückgesetzte Item bleibt pending).
- **Record-Erweiterung:** `MediaDurationProbeDetailItem` wird nur an einer Stelle
  (`DetailItemsAsync`) konstruiert; kein Test instanziiert den Record direkt — keine
  Kompilierbrüche zu erwarten.

## Umsetzungsreihenfolge

1. **`MediaDurationProbeDetailItem` um `MediaItemId` erweitern** und `mi.Id` in
   `DetailItemsAsync` projizieren.
   Voraussetzung: keine (Bestandscode).
2. **Reset-Methoden auf `MediaDurationProbeService`**: `ResetFailedProbeAsync`,
   `ResetFailedProbesAsync` (beide mit `RequireAdminAsync`, `ExecuteUpdateAsync`,
   Logging, XML-Doc).
   Voraussetzung: `RequireAdminAsync` und `VideoExtensionPredicate()` (beide vorhanden);
   fachlich unabhängig von Schritt 1.
3. **Service-Tests**: neue Datei
   `VideoWebPlayer.Tests/Services/MediaDurationProbeServiceTests_Reset.cs` (siehe
   Tests) plus Id-Assertions in `MediaDurationProbeServiceTests_Progress`.
   Voraussetzung: Schritte 1+2; `ProgramScheduleTestDb`-Fixture (vorhanden).
4. **UI in `TVSenderAdmin.razor`**: Aktionsspalte + Zeilen-Button, Massen-Button,
   `confirmProbeReset`-Dialog, `probeStatus`-Feld/Anzeige, beide Handler, colspan-Fix.
   Voraussetzung: Schritte 1+2 (Record-ID und Service-Methoden vorhanden); Muster
   `actions-cell`/`admin-dialog`/`form-text`-Status (alle vorhanden).
5. **bUnit-Tests** in `TVSenderAdminTests.cs` (siehe Tests).
   Voraussetzung: Schritt 4; `CreateContextAsync`/`SeedProbeItemAsync` (vorhanden).
6. **E2E-Erweiterung** von
   `TVSenderAdminE2ETests.Admin_CreateEditDeactivateDeleteRule` (zweites
   Failed-Item seeden, Einzel- und Massen-Reset durchspielen).
   Voraussetzung: Schritt 4.
7. **Dokumentation**: `docs/help/videolaufzeit.md`, `docs/RELEASE_NOTES.md`.
   Voraussetzung: Schritt 4 (finale UI-Bezeichnungen).
8. **Abnahme-Lauf**: `dotnet build VideoPlayer.sln` Debug + Release, volle Suite
   `dotnet test VideoWebPlayer.Tests/VideoWebPlayer.Tests.csproj`.
   Voraussetzung: Schritte 1-7.

## Tests

### Neue Tests

| Test/Hilfsmethode | Testklasse | Was wird geprüft |
|---|---|---|
| `GetProgressAsync_DetailItems_CarryMediaItemId` (oder Erweiterung des bestehenden Failed-Tests) | `MediaDurationProbeServiceTests_Progress` | `FailedItems`/`PendingItems` liefern die `MediaItem.Id` des Items (`MediaItemId`) |
| `ResetFailedProbeAsync_FailedItem_ClearsProbeStateAndBecomesPending` | `MediaDurationProbeServiceTests_Reset` (neue Datei) | Rückgabe `true`; `DurationProbedAt == null`, `DurationProbeAttempts == 0`; `FailedCount` sinkt, `PendingCount` steigt (Item wandert von `FailedItems` in `PendingItems`) |
| `ResetFailedProbeAsync_ProbedItem_ReturnsFalseAndKeepsDuration` | `MediaDurationProbeServiceTests_Reset` | Erfolgreich geprobtes Item (`Duration != null`) wird nicht zurückgesetzt — Failed-Guard greift, `Duration` bleibt |
| `ResetFailedProbeAsync_PendingOrUnknownItem_ReturnsFalse` | `MediaDurationProbeServiceTests_Reset` | Ausstehendes Item (Attempts < max) und unbekannte Id → `false`, keine Änderung |
| `ResetFailedProbeAsync_NonAdmin_Throws` | `MediaDurationProbeServiceTests_Reset` | `ForbiddenAccessException` für Nicht-Admin, `UnauthorizedAccessException` für unbekannten Benutzer |
| `ResetFailedProbesAsync_ResetsAllFailedItemsBeyondDetailCap` | `MediaDurationProbeServiceTests_Reset` | > 25 fehlgeschlagene Items (Kappung von `FailedItems`) werden vollständig zurückgesetzt; Rückgabe = volle Anzahl; `FailedCount` danach 0 |
| `ResetFailedProbesAsync_LeavesProbedAndPendingUntouched` | `MediaDurationProbeServiceTests_Reset` | Probed- (`Duration` gesetzt) und Pending-Items (Attempts < max) bleiben unverändert |
| `ResetFailedProbesAsync_NonAdmin_Throws` | `MediaDurationProbeServiceTests_Reset` | Admin-Erzwingung analog Einzel-Reset |
| `ProbePendingAsync_PicksUpResetItem` | `MediaDurationProbeServiceTests_Reset` | Nach dem Reset liefert `ProbePendingAsync` (mit Fake-/Stub-Probe) für das Item wieder ein Ergebnis — Nachweis der Wiederaufnahme-Selektion |
| `ProbeFailedRow_ResetButton_ResetsItemAndRefreshes` | `TVSenderAdminTests` (bUnit) | Zeilen-Button sichtbar in `tvsender-probe-failed-detail`, fehlt in `tvsender-probe-pending-detail`; Klick → Zeile verschwindet aus Failed-Liste, Chips/Status aktualisieren sich, `#tvsender-probe-action-status` zeigt Meldung |
| `ProbeResetAll_ConfirmDialog_ResetsAllAndShowsStatus` | `TVSenderAdminTests` (bUnit) | „Alle zurücksetzen" öffnet `admin-dialog-overlay`; Abbrechen ändert nichts; Bestätigen setzt alle Failed-Items zurück, `#tvsender-probe-action-status` meldet die Anzahl |

### Betroffene bestehende Tests

| Test | Anpassung |
|---|---|
| `MediaDurationProbeServiceTests_Progress`-Tests | Kein Bruch (kein Test konstruiert `MediaDurationProbeDetailItem` direkt); optional Assertions auf `MediaItemId` ergänzen |
| `TVSenderAdminE2ETests.Admin_CreateEditDeactivateDeleteRule` | Seed um ein zweites Failed-Item erweitern; bestehende Chip-Erwartung „1 fehlgeschlagen" auf „2 fehlgeschlagen" anpassen; Reset-Abschnitt anhängen |

### E2E-Tests (primärer Funktionsnachweis)

| Priorität | Szenario | Testdatei/Testklasse | Abgedecktes Akzeptanzkriterium | Warum E2E nötig |
|---|---|---|---|---|
| Hoch | Einzel-Reset aus der geöffneten Fehlerliste: Zeilen-Button klicken → Item verlässt `tvsender-probe-failed-detail`, `tvsender-probe-failed`-Chip zählt herunter, `tvsender-probe-pending`-Chip zählt hoch, Statusmeldung erscheint | `TVSenderAdminE2ETests.Admin_CreateEditDeactivateDeleteRule` (Erweiterung) | „Probe zurücksetzen" pro Eintrag wirkt im echten Browserfluss inkl. Neu-Laden der Fortschrittsdaten | Benutzerfluss: Interaktion auf echter Seite, Server-Roundtrip über `InteractiveServer`, DB-Persistenz — bUnit kann die Circuit-/Datenaktualisierung nicht realitätsnah nachweisen |
| Hoch | Massen-Reset mit Bestätigungsdialog: „Alle zurücksetzen" → `admin-dialog` erscheint → Bestätigen → `Failed`-Chip „0 fehlgeschlagen", Fehlerliste entfällt | `TVSenderAdminE2ETests.Admin_CreateEditDeactivateDeleteRule` (Erweiterung, zweites Failed-Seed `e2e-defekt-2.mkv`) | Massen-Reset über die gesamte Fehlermenge inkl. Dialog-UX | Gleicher Benutzerfluss; der Dialog ist DOM-/Event-Verhalten, das nur im Browser glaubwürdig geprüft wird; Konsolidierung in einem Testlauf folgt der Projekt-Konvention |

| Betroffene bestehende E2E-Tests | Anpassung |
|---|---|
| `TVSenderAdminE2ETests.Admin_CreateEditDeactivateDeleteRule` | Wird selbst erweitert (siehe oben); Seed + Chip-Erwartungen anpassen |
| `TVSenderAdminE2ETests.Admin_GenreRule_MultiSelectAndSourceFilter` | Keine — nutzt die Probe-Karte nicht |

## Offene Punkte

Keine — die vier offenen Fragen der Anforderung (Massen-Reset-Umfang, Reset in der
Pending-Liste, Sofort-Probe vs. nächster Tick, Bestätigungsdialog nur beim Massen-Reset)
sind oben als Designentscheidungen festgelegt.
