# Anforderung — Issue #337

**Titel:** Sendeplan: Jeder Commit mit sendeplan-relevanten Entitäten weckt sofort — Bündelung greift nur im Scan-Scope
**Quelle:** https://github.com/martin-stromberg/VideoPlayer/issues/337

## Fachliche Zusammenfassung

`IProgramScheduleSignal.NotifyScheduleChanged()` weckt den `ProgramScheduleWorker` (und über die Live-Leitung `WaitLiveAsync` den `LiveStreamWorker`) außerhalb eines `BeginScan`-Scopes bei jedem einzelnen Commit sendeplan-relevanter Änderungen sofort. Schnell aufeinanderfolgende Einzel-Commits — etwa mehrere Edits an Programmregeln oder Clip-Gruppen über die Admin-UI — lösen dadurch jeweils einen eigenen `ExecutePassAsync`-Lauf aus, statt gebündelt zu werden. Zu klären und umzusetzen ist, ob solche Einzel-Commit-Folgen zu einem Plan-Lauf zusammengefasst werden (Debounce/Batch bzw. impliziter kurzlebiger Scope) oder das sofortige Wecken bei Einzel-Edits bewusst bestehen bleibt und nur Scan-Läufe bündeln.

## Fachlicher Scope

- **Verarbeitungseinheit:** Die Folge (der „Burst") von `NotifyScheduleChanged()`-Meldungen, die außerhalb eines `IProgramScheduleSignal.BeginScan()`-Scopes in kurzem zeitlichem Abstand aufeinanderfolgen. Fachlich entspricht sie einer zusammenhängenden Bearbeitungssequenz sendeplan-relevanter Eingaben (z. B. mehrere schnelle Regel-Edits), die den Sendeplan genau einmal neu bewerten lassen soll — nicht einmal je Commit.
- **Auslösende Ereignisse:** Jeder Commit, der `NotifyScheduleChanged()` ohne offenen Scan-Scope auslöst:
  - der zentrale `SaveChanges`/`SaveChangesAsync`-Hook in `ApplicationDbContext` (`ApplicationDbContext.PlaylistBackfillMarking.cs`), der feuert, wenn Backfill-Markierungen geschrieben werden — d. h. bei neuen oder umgehängten `Movie`-, `TVShowSeason`- und `TVShowEpisode`-Entitäten;
  - `ApplicationDbContext` nach dem committeten Löschen einer `MediaSource` (`DeleteMediaSourceAsync`, ca. Z. 448);
  - direkte Aufrufe in `ProgramRuleService` (Regel-CRUD), `ProgramClipGroupService` (Clip-Gruppen-CRUD), `MediaDurationProbeService.ProbePendingAsync` (außerhalb der Backlog-Schleife des `MediaDurationProbeWorker`);
  - direkte Aufrufe in den Admin-Komponenten `TVSenderAdmin.razor` („Jetzt generieren", Sendeplan-Reset, Kanaleinstellungen/Inhaltsumfang) und `ProgramSettings.razor`.
- **Erwartete Wirkung:** Eine Sequenz schnell aufeinanderfolgender sendeplan-relevanter Einzel-Commits führt zu genau einem `ExecutePassAsync`-Lauf des `ProgramScheduleWorker` statt zu einem Lauf je Commit. Der materialisierte Sendeplan (`ProgramSlot`s) spiegelt weiterhin sämtliche Änderungen — nur gebündelt und ggf. um eine kurze Bündelungszeit verzögert. Sichtbar ist eine deutlich geringere Zahl von Nachführungsläufen bei unverändertem Endzustand. (Fällt die Abwägung zugunsten des Ist-Verhaltens aus, bleibt die Wirkung unverändert; die Entscheidung ist dann zu dokumentieren.)
- **Umgang mit fehlenden Objekten:** Es fehlen keine fachlichen Objekte. `IProgramScheduleSignal`/`ProgramScheduleSignal` existiert als Singleton mit koaleszierendem 1-Slot-Channel und den `BeginScan`-Zählern (`_activeScans`, `_suppressedNotifications`) und wird wiederverwendet. Ein für die Bündelung nötiger Debounce-/Batch-Mechanismus existiert noch nicht und wäre neu zu schaffen (technischer Baustein, kein fachliches Objekt).
- **Scope-Ausschlüsse:**
  - Das Bündelverhalten innerhalb von `BeginScan`-Scopes bleibt unverändert: Scan-Läufe über `MediaSourceScanCoordinator.RunFullScanAsync`/`RescanCollectionTreeAsync` (Worker `MediaSourceScanService`, Komplettscan in `MediaSourceAdmin.razor`, „Neu erfassen" in `MediaSourceExplorer.razor`) und die Backlog-Schleife des `MediaDurationProbeWorker` koaleszieren Weckrufe bereits über das Scope-Ende.
  - Die Scope-Nutzung der drei Scan-Aufrufer selbst ist Gegenstand von Issue #333 und hier ausdrücklich nicht Teil.
  - `NotifyLiveScheduleChanged()` (meldet materialisierte Planänderungen nach einem `ExecutePassAsync` an den `LiveStreamWorker`, Issue #315) ist kein Eingabe-Commit-Signal und bleibt unmittelbar.
  - Es bleibt unverändert, welche Änderungen als sendeplan-relevant gelten — weder die Menge der Aufrufstellen noch die Regeln des `SaveChanges`-Hooks werden fachlich angepasst.
  - `IPlaylistBackfillSignal`/`PlaylistBackfillWorker` ist nicht betroffen; dessen Bündelung läuft vollständig über `BeginScan`.
  - Roh-SQL- und Bulk-Pfade (`ExecuteSql`, `ExecuteUpdate`, Backup-Restore), die den `SaveChanges`-Hook ohnehin umgehen, bleiben wie bisher außen vor.

## Betroffene Klassen und Komponenten

- `VideoWebPlayer/Services/ProgramScheduleSignal.cs` — `IProgramScheduleSignal`/`ProgramScheduleSignal` (`NotifyScheduleChanged`, `BeginScan`, `WaitAsync`, `WaitLiveAsync`, `_channel`, `_liveChannel`, `_activeScans`, `_suppressedNotifications`): zentraler Erweiterungspunkt für eine Bündelung außerhalb von Scopes.
- `VideoWebPlayer/Services/ProgramScheduleWorker.cs` — Konsument der Weckleitung über `WaitAsync`; dessen Lauf (`ExecutePassAsync` via `IProgramScheduleService`) wird gebündelt.
- `VideoWebPlayer/Services/LiveStreamWorker.cs` — Konsument über `WaitLiveAsync`; wird ebenfalls von `NotifyScheduleChanged` gespeist (Bezug zu den offenen Fragen).
- `VideoWebPlayer/Data/ApplicationDbContext.PlaylistBackfillMarking.cs` — `SaveChanges`/`SaveChangesAsync`-Overrides als zentraler Commit-Hook.
- `VideoWebPlayer/Data/ApplicationDbContext.cs` — `DeleteMediaSourceAsync` (Notify nach Commit).
- Direkte Aufrufer (voraussichtlich unverändert, sofern die Bündelung zentral im Signal greift): `ProgramRuleService`, `ProgramClipGroupService`, `MediaDurationProbeService`, `TVSenderAdmin.razor`, `ProgramSettings.razor`.
- `VideoWebPlayer/Services/MediaSourceScanCoordinator.cs` — Referenzimplementierung des bestehenden Bündelungsmusters (`BeginScan`), voraussichtlich unverändert.
- Tests: `ProgramScheduleSignalTests`, `ProgramScheduleWorkerTests`, `PlaylistBackfillMarkerHookTests`, `MediaDurationProbeWorkerTests`, `MediaDurationProbeServiceTests` sowie bestehende Service-/Komponenten-Tests, die `NotifyScheduleChanged`-Aufrufe verifizieren; neue Tests für das Bündelverhalten außerhalb von Scopes.

## Implementierungsansatz

Der Eingriffspunkt hängt von der offenen Grundentscheidung ab:

1. **Bündelung außerhalb von Scopes (Debounce/Batch):** `ProgramScheduleSignal` fast `NotifyScheduleChanged()`-Meldungen ohne offenen Scan-Scope über ein kurzes Zeitfenster zu einem Weckruf zusammen (Debounce mit kurzer Verzögerung, oder der im Issue genannte implizite kurzlebige Scope je Commit). Das vorhandene Muster — koaleszierender 1-Slot-Channel plus Zähler wie bei `BeginScan`/`EndScan` — ist die naheliegende Vorlage. Liegt die Bündelung zentral im Singleton, profitieren alle Aufrufer (Commit-Hook, Services, Admin-Komponenten) ohne eigene Änderung. Zu beachten: `NotifyScheduleChanged` schreibt sowohl `_channel` als auch `_liveChannel` — ob die Live-Leitung mitgebündelt wird, ist zu klären.
2. **Bewusstes Beibehalten des Ist-Verhaltens:** Einzel-Edits wecken weiterhin sofort; nur Scan-Läufe bündeln. Dann ist die Entscheidung zu dokumentieren, die Wecklogik bleibt unverändert.

Unabhängig davon bleiben relevante Erweiterungspunkte die Overrides von `SaveChanges`/`SaveChangesAsync` sowie die direkten Aufrufstellen; das `IBackgroundProcessingGate` (Restore-Schutz im `ProgramScheduleWorker`) ist unberührt.

## Konfiguration

Die Anforderung verlangt keine Konfigurierbarkeit. Wird die Debounce-Variante umgesetzt, wäre die Bündelungs-Verzögerung ein Kandidat für eine Anwendungseinstellung — das Projekt hält Sender-Laufzeitparameter in `ProgramSettingsService` (z. B. `GetProgramScheduleIntervalAsync`, datenbankbasiert je Installation) bzw. Options-Klassen (vgl. `MediaDurationProbeWorker` mit `EffectiveBatchSize`/`EffectiveIntervalSeconds`). Ob die Verzögerung konfigurierbar sein muss oder ein fester kleiner Wert genügt, ist offen.

## Offene Fragen

1. **Grundentscheidung (im Issue ausdrücklich offen):** Sollen Einzel-Commits außerhalb von Scan-Scopes gebündelt werden (Debounce/impliziter Scope), oder soll das sofortige Wecken bei einzelnen Admin-Edits bewusst bestehen bleiben, sodass nur Scan-Läufe bündeln? Abzuwägen ist die Aktualität des Sendeplans (ein verdrängter laufender Slot soll zeitnah umschalten) gegen die Zahl der `ExecutePassAsync`-Läufe.
2. **Live-Leitung:** Falls gebündelt wird — gilt die Verzögerung auch für `_liveChannel`/`WaitLiveAsync`? `NotifyScheduleChanged` speist beide Kanäle; eine Mitbündelung würde die Reaktion des `LiveStreamWorker` auf Eingabeänderungen verzögern, ein getrenntes Verhalten (Plan-Leitung gebündelt, Live-Leitung sofort) erhöht die Komplexität.
3. **Griffe der Bündelung:** Soll sie zentral in `ProgramScheduleSignal` greifen und damit alle Aufrufer einschließen — auch „Jetzt generieren" und Sendeplan-Reset in `TVSenderAdmin.razor`, wo der Anwender eine unmittelbare Reaktion erwartet (die Komponente wartet aktuell 4 s und lädt den Plan neu)? Oder nur an ausgewählten Aufrufstellen?
4. **Verzögerungsdauer:** Falls gebündelt wird — welcher Wert gilt als „kurz", und soll er konfigurierbar sein (Anwendungseinstellung) oder fest verdrahtet?
