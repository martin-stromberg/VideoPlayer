# Umsetzungsplan: Sendeplan — Bündelung scope-freier Commit-Weckrufe (Issue #337)

## Übersicht

`IProgramScheduleSignal.NotifyScheduleChanged()` weckt den `ProgramScheduleWorker` (und über `_liveChannel` den `LiveStreamWorker`) außerhalb eines `BeginScan`-Scopes heute bei jedem einzelnen sendeplan-relevanten Commit sofort. Umgesetzt wird eine zentrale Bündelung im Singleton `ProgramScheduleSignal`: scope-freie Meldungen werden über ein kurzes Bündelfenster zu genau einem Weckruf auf beiden Kanälen zusammengefasst. Für die explizit nutzergetriggerte Aktion „Jetzt generieren" in `TVSenderAdmin.razor` wird ein sofortiger, ungebündelter Meldeweg (`NotifyScheduleChangedNow`) ergänzt; das Scope-Verhalten (`BeginScan`/`EndScan`) und der materialisierte Pfad `NotifyLiveScheduleChanged` bleiben unverändert.

## Scope-Klärung

Verbindliche fachliche Abgrenzung vor der Umsetzung — übernommen bzw. konkretisiert aus dem Abschnitt `Fachlicher Scope` der `requirement.md`:

| Feld | Festlegung |
|------|------------|
| Verarbeitungseinheit | Der „Burst" scope-freier `NotifyScheduleChanged()`-Meldungen innerhalb des Bündelfensters — fachlich eine zusammenhängende Bearbeitungssequenz sendeplan-relevanter Eingaben, die den Sendeplan genau einmal neu bewerten lässt. |
| Auslösende Ereignisse | Unverändert alle bisherigen Aufrufer: `SaveChanges`/`SaveChangesAsync`-Hook in `ApplicationDbContext.PlaylistBackfillMarking.cs`, `DeleteMediaSourceAsync` (`ApplicationDbContext.cs`), `ProgramRuleService` (4 Stellen), `ProgramClipGroupService` (5 Stellen), `MediaDurationProbeService.ProbePendingAsync`, `TVSenderAdmin.razor` (3 Stellen), `ProgramSettings.razor` (1 Stelle). Neu: `NotifyScheduleChangedNow()` für den expliziten Sofort-Pfad. |
| Erwartete Wirkung | Eine Folge schnell aufeinanderfolgender Einzel-Commits führt zu genau einem `ExecutePassAsync`-Lauf (und einem Live-Weckruf) statt zu einem je Commit; der materialisierte Sendeplan spiegelt weiterhin sämtliche Änderungen — gebündelt und um höchstens das Bündelfenster verzögert. |
| Umgang mit fehlenden Objekten | Keine fehlenden fachlichen Objekte: `ProgramScheduleSignal` (Singleton, 1-Slot-Channels, `_activeScans`/`_suppressedNotifications`) wird wiederverwendet. Der Debounce-/Batch-Mechanismus ist ein neuer technischer Baustein innerhalb dieser Klasse; `ProgramScheduleOptions` (bereits an `ProgramSchedule` gebunden) nimmt die Fensterdauer auf. |

### Scope-Ausschlüsse

Jede Einschränkung gegenüber einer naheliegenden Nutzererwartung ist hier als bewusster Ausschluss mit Begründung und Status zu führen. Der Status `offen` wechselt erst auf `bestätigt`, wenn der Anwender den Ausschluss in Schritt 5a bestätigt hat (mit Datum vermerken).

| # | Einschränkung / Ausschluss | Begründung | Status |
|---|----------------------------|------------|--------|
| 1 | „Jetzt generieren" (`GenerateNowAsync` in `TVSenderAdmin.razor`) wird **nicht** gebündelt, sondern ruft den neuen Sofort-Pfad `NotifyScheduleChangedNow()` — die Reaktion bleibt unverzögert. | Die Komponente wartet fest ~4 s und lädt dann Plan und Diagnosen neu; eine Bündelungsverzögerung würde die erwartete unmittelbare Rückmeldung der expliziten Nutzeraktion unterlaufen. | bestätigt (2026-11-05) |
| 2 | Die Live-Leitung (`_liveChannel`/`WaitLiveAsync`) wird bei Eingabe-Commits **mit**gebündelt — der `LiveStreamWorker` reagiert auf scope-freie `NotifyScheduleChanged` erst am Fensterende statt sofort. | Der Weckruf aus einer Eingabeänderung ist für den Stream spekulativ (die materialisierten Slots ändern sich erst nach dem Plan-Lauf); der verbindliche Live-Pfad `NotifyLiveScheduleChanged` nach einem Lauf bleibt unmittelbar. Die Mitbündelung vermeidet zusätzlich je Commit einen `SynchronizeAsync`-Tick. | bestätigt (2026-11-05) |
| 3 | Bündelung innerhalb von `BeginScan`-Scopes bleibt unverändert (`MediaSourceScanCoordinator`, `MediaDurationProbeWorker`-Backlog); die Scope-Nutzung der Scan-Aufrufer selbst ist Issue #333. | Bestehendes, separat abgenommenes Verhalten (Issues #290, #333); nicht Teil dieser Anforderung. | bestätigt (2026-11-05) |
| 4 | Die Menge und Regeln der sendeplan-relevanten Auslöser (Aufrufstellen, `SaveChanges`-Hook-Regeln, Roh-SQL-/Bulk-Ausnahmen) bleiben unverändert. | Fachlich nicht Teil der Anforderung; es wird nur gebündelt, nicht neu bewertet, was meldet. | bestätigt (2026-11-05) |
| 5 | `IPlaylistBackfillSignal`/`PlaylistBackfillWorker` bleiben unberührt. | Dessen Bündelung läuft vollständig über `BeginScan`; ausdrücklich nicht Gegenstand. | bestätigt (2026-11-05) |

## Designentscheidungen

| Komponente / Bereich | Gewählter Ansatz | Begründung |
|----------------------|-----------------|------------|
| Grundentscheidung: bündeln statt Ist-Verhalten | Scope-freie `NotifyScheduleChanged()`-Meldungen werden gebündelt. | Issue-Ziel; die Kosten vieler `ExecutePassAsync`-Läufe je Edit-Sequenz überwiegen die maximal `Fenster`-Sekunden Verzögerung — die Aktualität des Sendeplans leidet nicht messbar, weil das Generierungsintervall ohnehin im Minutenbereich liegt und der live-kritische Pfad (`NotifyLiveScheduleChanged` nach materialisierten Änderungen) unverändert sofort bleibt. |
| Eingriffsort | Zentral in `ProgramScheduleSignal` (Singleton). | Alle ~16 Aufrufstellen (Commit-Hook, Services, Razor-Komponenten) profitieren ohne eigene Änderung; der `SaveChanges`-Hook ist selbst eine Burst-Quelle und ließe sich an einzelnen Stellen gar nicht abdecken. Das Interface bleibt die fachliche Naht, nicht die Aufrufer. |
| Bündelmechanismus | Festes Bündelfenster (Leading-Edge-Koaleszierung): die erste scope-freie Meldung öffnet das Fenster; weitere Meldungen darin markieren nur „ausstehend"; am Fensterende wird genau einmal in `_channel` **und** `_liveChannel` geschrieben. Kein Trailing-Debounce, keine Max-Deferral-Logik. | Einfachste korrekte Umsetzung des im Issue genannten „impliziten kurzlebigen Scopes": begrenzte Verzögerung (≤ Fenster) ohne Starvation-Risiko, deterministic testbar über `TimeProvider`. Ein Trailing-Debounce (jede Meldung verlängert) brächte einen komplexeren Flush-Zyklus und könnte bei Dauerströmen beliebig verzögern. Die 1-Slot-Kanäle bleiben die Zustellinfrastruktur — das Muster der `BeginScan`-Koaleszierung (Zähler + einmalige Zustellung) wird auf die Zeitachse übertragen. |
| Sofort-Pfad für „Jetzt generieren" | Neues Interface-Mitglied `NotifyScheduleChangedNow()` statt `NotifyScheduleChanged(bool)`. | Benannte Methode ist am Aufrufort selbstdokumentierend (`ScheduleSignal.NotifyScheduleChangedNow()`); der gebündelte Pfad bleibt der Default, sodass kein bestehender Aufrufer eine Entscheidung treffen muss. Die Methode respektiert weiterhin die Scope-Suppression (kein Scan-Bypass) und verbraucht eine ausstehende Bündelung. |
| Verzögerungswert | `ProgramScheduleOptions.ScheduleChangedDebounceSeconds`, `int`, Standard **5 s**, normalisiert über `EffectiveScheduleChangedDebounce`. | 5 s bündeln UI-typische Folgeedits (z. B. Clip-Gruppe anlegen → Clips zufügen, mehrere Regel-Saves, maschinelle Commit-Sequenzen des Hooks) und bleiben klein gegenüber `LiveStreamWorker.PollCap` (30 s) und Generierungsintervall (15 min, `ProgramScheduleIntervalMinutes`). Konfigurierbar nach dem bestehenden `Effective*`-Muster (`MediaProbeOptions.IntervalSeconds` u. a.): `< 0` → Standard 5 s; `0` → sofortige Zustellung (bewusster Aus-Schalter, stellt das bisherige Verhalten wieder her); `> 0` → Wert in Sekunden. Nicht datenbankbasiert (`ProgramSettingsService`), weil es ein technischer Laufzeitparameter ist, kein fachlicher Sender-Parameter. |
| Flush-vs-Scan-Race | Schreibt der Bündelflush, während ein Scan-Scope offen ist, wird die ausstehende Meldung in `_suppressedNotifications` überführt (mit derselben Doppelcheck-Race-Behandlung wie `NotifyScheduleChanged` Z. 90–95). | Sonst würde ein während des Fensters begonnener Scan vom verzögerten Flush unterlaufen — die Suppression ist genau für diesen Fall erfunden. |
| Verbrauch ausstehender Bündelung | Jede andere Zustellung (`NotifyScheduleChangedNow`, `EndScan`-Wecken) löscht eine ausstehende Bündelung; ein später ablaufender Flush ohne ausstehende Markierung schreibt nichts. | Verhindert eine „doppelte" Weckung: die gebündelten Änderungen sind durch die sofortige Zustellung bereits abgedeckt. |

## Programmabläufe

### Gebündelte scope-freie Eingabeänderung

1. Ein Aufrufer (z. B. `ProgramRuleService.UpdateRuleAsync`, `ApplicationDbContext.SaveChangesAsync`-Hook) ruft `NotifyScheduleChanged()`.
2. `ProgramScheduleSignal` prüft `_activeScans`: offen → bisheriger Suppression-Pfad unverändert (`_suppressedNotifications++`, Doppelcheck bei Race mit `EndScan`).
3. Kein Scope offen und `EffectiveScheduleChangedDebounce == 0` → sofortige Zustellung in `_channel` und `_liveChannel` (bisheriges Verhalten).
4. Kein Scope offen und Fenster > 0 → „ausstehend"-Markierung setzen; wenn noch kein Fenster läuft, Bündelfenster über `Task.Delay(fenster, _timeProvider)` starten (ein Flush je Fenster).
5. Weitere `NotifyScheduleChanged()`-Meldungen im laufenden Fenster setzen nur die Markierung erneut — kein neuer Flush, keine Zustellung.
6. Am Fensterende: offener Scan → Markierung in `_suppressedNotifications` überführen (Scan-Ende weckt einmal); kein Scan → einmal `_channel` + `_liveChannel` schreiben. Danach Fenster geschlossen, Markierung gelöscht. **Teardown-Reihenfolge:** Das Fenster-Läuft-Flag wird vor dem Löschen der Ausstehend-Markierung zurückgesetzt (oder die Markierung danach erneut geprüft) — umgekehrt würde eine Meldung, die zwischen „Markierung gelöscht" und „Fenster geschlossen" eintrifft, die Markierung setzen, ohne dass ein neuer Flush startet: verlorenes Wecken (Analogie zur Doppelcheck-Behandlung Z. 90–95).
7. `ProgramScheduleWorker` wacht aus `WaitAsync`, läuft `ExecutePassAsync` hinter `IBackgroundProcessingGate`, feuert bei materialisierten Änderungen sofort `NotifyLiveScheduleChanged()`; `LiveStreamWorker` wacht aus `WaitLiveAsync` und synchronisiert.

Beteiligte Klassen/Komponenten: `ProgramScheduleSignal`, `ProgramScheduleOptions`, `TimeProvider`, `ProgramScheduleWorker`, `LiveStreamWorker`, sämtliche `NotifyScheduleChanged`-Aufrufer (unverändert).

### Sofort-Meldung für explizite Nutzeraktion („Jetzt generieren")

1. `GenerateNowAsync` in `TVSenderAdmin.razor` ruft `NotifyScheduleChangedNow()` statt `NotifyScheduleChanged()`.
2. `ProgramScheduleSignal` schreibt bei geschlossenem Scan sofort in `_channel` und `_liveChannel` und löscht eine evtl. ausstehende Bündelungs-Markierung (bereits abgedeckt); bei offenem Scan gilt wie bisher der Suppression-Pfad (Scan-Ende weckt).
3. Die Seite wartet wie bisher ~4 s und lädt Plan + Diagnosen neu — die UX ist unverändert.

Beteiligte Klassen/Komponenten: `IProgramScheduleSignal`/`ProgramScheduleSignal`, `TVSenderAdmin.razor`, `ProgramScheduleWorker`, `LiveStreamWorker`.

### Scan-Scope (unverändert)

1. `MediaSourceScanCoordinator`/`MediaDurationProbeWorker` öffnen `BeginScan()`; Meldungen werden über `_suppressedNotifications` gezählt.
2. Dispose des letzten Scopes weckt beide Kanäle einmal, wenn mindestens eine Meldung unterdrückt wurde — und löscht dabei eine evtl. ausstehende Bündelungs-Markierung. Ein danach erst ablaufender Fenster-Flush findet keine Markierung mehr vor und schreibt nichts (kein doppeltes Wecken durch das Interleaving „Meldung vor `BeginScan` + unterdrückte Meldung im Scan + `EndScan`-Wecken + späterer Flush").

Beteiligte Klassen/Komponenten: `ProgramScheduleSignal`, `MediaSourceScanCoordinator`, `MediaDurationProbeWorker`.

## Neue Klassen

Keine neuen öffentlichen Typen. Neu ist lediglich interner Zustand in `ProgramScheduleSignal` (Bündelfenster-/Ausstehend-Flags, Flush-Steuerung) sowie je eine neue Eigenschaft bzw. Methode an bestehenden Typen.

## Änderungen an bestehenden Klassen

### `IProgramScheduleSignal` (Interface)

- **Neue Methoden:** `NotifyScheduleChangedNow()` — meldet eine sendeplan-relevante Änderung ohne Bündelung sofort (für explizit nutzergetriggerte Aktionen); respektiert weiterhin die `BeginScan`-Suppression.

### `ProgramScheduleSignal` (Singleton)

- **Neuer Konstruktor:** `(IOptions<ProgramScheduleOptions>? options = null, TimeProvider? timeProvider = null)` — optionale Parameter nach dem Muster von `ProgramScheduleService` (`scheduleOptions?.Value ?? new ProgramScheduleOptions()`) bzw. den Workern (`timeProvider ?? TimeProvider.System`); DI (`AddSingleton<ProgramScheduleSignal>`) befüllt beides automatisch.
- **Neuer interner Zustand:** Ausstehend-Markierung und Fenster-Läuft-Flag (interlocked), für die Bündelung scope-freier Meldungen. Fenster-Teardown: das Läuft-Flag wird **vor** dem Löschen der Ausstehend-Markierung zurückgesetzt (oder die Markierung danach erneut geprüft) — sonst verlorenes Wecken bei einer Meldung im Teardown-Spalt (Implementierungsrisiko, per Kommentar absichern).
- **Geänderte Methoden:** `NotifyScheduleChanged()` — Scope-Pfad unverändert; Scope-frei neu: bei `EffectiveScheduleChangedDebounce == 0` sofortige Zustellung wie bisher, sonst Fenster öffnen/Markierung setzen statt sofort schreiben.
- **Neue Methoden:** `NotifyScheduleChangedNow()` — wie oben; private Flush-Ablauf-Logik (Fensterablauf über `_timeProvider`, Zustellung oder Überführung in `_suppressedNotifications` bei offenem Scan, Löschen der Markierung).
- **Geänderte Methoden:** `EndScan()` — löscht zusätzlich eine ausstehende Bündelungs-Markierung, wenn das Scan-Ende weckt (unveränderte Weckbedingung).

### `ProgramScheduleOptions` (Options-Klasse)

- **Neue Eigenschaften:** `ScheduleChangedDebounceSeconds` (`int`, Standard 5) — Bündelfenster für scope-freie `NotifyScheduleChanged`-Meldungen.
- **Neue Eigenschaften:** `EffectiveScheduleChangedDebounce` (`TimeSpan`) — Normalisierung: `< 0` → 5 s; `0` → `TimeSpan.Zero` (sofortige Zustellung); `> 0` → Wert.

### `TVSenderAdmin.razor` (Razor-Komponente)

- **Geänderte Methoden:** `GenerateNowAsync` — ruft `ScheduleSignal.NotifyScheduleChangedNow()` statt `NotifyScheduleChanged()` (Z. 1541); die 4-s-Warte und das Neu laden bleiben unverändert. `ResetScheduleAsync` (Z. 1590) und `SaveChannelSettingsAsync` (Z. 2028) bleiben bewusst auf dem gebündelten Pfad.

### `GatedScheduleSignal` (privates Test-Double in `MediaDurationProbeWorkerTests`)

- **Neue Methoden:** `NotifyScheduleChangedNow()` — delegiert an `_inner` (und kann bei Bedarf mitgezählt werden); Pflicht wegen Interface-Erweiterung.
- **Geänderter interner Zustand:** `_inner` wird mit `ProgramScheduleOptions { ScheduleChangedDebounceSeconds = 0 }` konstruiert — sonst bündelt das innere echte Signal scope-freie Meldungen 5 s in Echtzeit (Default-Fenster), was den Double langsamer und zeitabhängig macht.

### `FakeScheduleSignal` (privates Test-Double in `MediaDurationProbeServiceTests`)

- **Neue Methoden:** `NotifyScheduleChangedNow()` — No-Op bzw. Zähler analog `NotifyScheduleChanged`; Pflicht wegen Interface-Erweiterung.

## Datenbankmigrationen

Keine.

## Validierungsregeln

| Feld / Objekt | Regel | Fehlerfall |
|---------------|-------|------------|
| `ProgramScheduleOptions.ScheduleChangedDebounceSeconds` | `< 0` → Standard 5 s; `0` → sofortige Zustellung; `> 0` → Wert in Sekunden (Normalisierung in `EffectiveScheduleChangedDebounce`, Muster der bestehenden `Effective*`-Eigenschaften) | Ungültige/negative Werte fallen auf den Default zurück statt zu fehlschlagen |

## Konfigurationsänderungen

| Eintrag | Typ | Standardwert | Zweck |
|---------|-----|--------------|-------|
| `ProgramSchedule:ScheduleChangedDebounceSeconds` (`ProgramScheduleOptions`, `appsettings.json`-Sektion `ProgramSchedule`) | `int` (Sekunden) | `5` | Bündelfenster für scope-freie `NotifyScheduleChanged`-Meldungen; `0` deaktiviert die Bündelung (bisheriges Sofortverhalten) |

## Seiteneffekte und Risiken

- **Alle `NotifyScheduleChanged`-Aufrufer:** erhalten ohne eigene Änderung eine um höchstens das Bündelfenster (Standard 5 s) verzögerte Worker-Weckung — beabsichtigt; Endzustand des Sendeplans unverändert.
- **`LiveStreamWorker`:** wacht auf Eingabe-Commits erst am Fensterende (Scope-Ausschluss 2); materialisierte Änderungen (`NotifyLiveScheduleChanged`) bleiben sofort.
- **Race Bündelflush ↔ `BeginScan`:** ein Flush, der in einen gerade geöffneten Scan hineinläuft, würde die Suppression unterlaufen — abgesichert durch Überführung in `_suppressedNotifications` mit der bestehenden Doppelcheck-Behandlung.
- **Interface-Erweiterung:** `GatedScheduleSignal` und `FakeScheduleSignal` kompilieren erst nach Ergänzung von `NotifyScheduleChangedNow()`; es existieren genau diese zwei Test-Implementierungen (`grep ": IProgramScheduleSignal"`).
- **Bestehende Signal-Tests:** `ProgramScheduleSignalTests.Notify_WakesWaiter`/`Notify_IsCoalesced` erwarten Sofort-Zustellung und brechen mit dem neuen Default — werden auf den Sofort-Modus (`ScheduleChangedDebounceSeconds = 0`) umgestellt.
- **Tests mit echtem `ProgramScheduleSignal`:** `TVSenderAdminTests` (bUnit), `MediaSourceScanServiceTests`, `MediaSourceScanCoordinatorScopeTests`, `MediaDurationProbeWorkerTests` nutzen überwiegend den Scope-Pfad (unverändert) bzw. tolerante DB-Polls — zu verifizieren, nicht voraussichtlich anzupassen.
- **E2E-Tests:** `TVSenderAdminE2ETests` u. a. warten materialisierte Planergebnisse per DB-Polling mit großzügigen Timeouts (60–90 s) ab; „Jetzt generieren" bleibt per Sofort-Pfad unverzögert — die +5 s auf gebündelten Pfaden werden absorbiert.
- **`MediaDurationProbeService.ProbePendingAsync`:** einziger Produktiv-Aufrufer ist der Worker im `BeginScan`-Scope — der direkte Notify ist faktisch immer unterdrückt; unverändert.
- **Singleton-Lebenszeit:** ein beim Herunterfahren ausstehender Flush schreibt in Kanäle ohne Leser — harmlos (DropWrite, kein Handle-Leak).

## Umsetzungsreihenfolge

1. **`ProgramScheduleOptions` erweitern**
   - Voraussetzungen: Keine — Options-Klasse und `services.Configure<ProgramScheduleOptions>(…"ProgramSchedule")`-Bindung (`ServiceCollectionExtensions.cs` Z. 245) existieren.
   - Beschreibung: `ScheduleChangedDebounceSeconds` (Standard 5) und `EffectiveScheduleChangedDebounce` mit der festgelegten Normalisierung hinzufügen; Schlüssel in `appsettings.json` unter `ProgramSchedule` dokumentarisch ergänzen.

2. **`IProgramScheduleSignal` + `ProgramScheduleSignal` um Sofort-Pfad und Bündelung erweitern**
   - Voraussetzungen: Schritt 1 (Options-Eigenschaft); `TimeProvider.System` ist bereits als Singleton registriert (`ServiceCollectionExtensions.cs` Z. 286).
   - Beschreibung: Interface-Mitglied `NotifyScheduleChangedNow()` deklarieren; in `ProgramScheduleSignal` optionalen Ctor, Bündelzustand, geändertes `NotifyScheduleChanged()`, `NotifyScheduleChangedNow()`, Flush-Logik und `EndScan`-Markierungsverbrauch implementieren; Scope-Pfade unverändert lassen.

3. **Test-Doubles nachziehen**
   - Voraussetzungen: Schritt 2 (Interface-Signatur), sonst kompiliert das Testprojekt nicht.
   - Beschreibung: `GatedScheduleSignal` (`MediaDurationProbeWorkerTests`) und `FakeScheduleSignal` (`MediaDurationProbeServiceTests`) um `NotifyScheduleChangedNow()` ergänzen; `GatedScheduleSignal._inner` mit `ProgramScheduleOptions { ScheduleChangedDebounceSeconds = 0 }` konstruieren (Sofort-Modus, sonst bündelt das innere Signal 5 s in Echtzeit).

4. **`TVSenderAdmin.razor` auf den Sofort-Pfad umstellen**
   - Voraussetzungen: Schritt 2.
   - Beschreibung: `GenerateNowAsync` ruft `NotifyScheduleChangedNow()`; `ResetScheduleAsync` und `SaveChannelSettingsAsync` bleiben gebündelt.

5. **Neue Bündel-Tests schreiben und bestehende Signal-Tests anpassen**
   - Voraussetzungen: Schritte 2–3; `ControllableTimeProvider` (Hilfsklasse, feuert `TimeProvider`-Timer via `TickTimers`) ist im Testprojekt vorhanden; `Microsoft.Extensions.Options` für `Options.Create` ist über das Hauptprojekt verfügbar.
   - Beschreibung: `ProgramScheduleSignalTests` um die neuen Fälle erweitern und die zwei Sofort-Semantik-Tests auf `ScheduleChangedDebounceSeconds = 0` umstellen.

6. **Dokumentation aktualisieren**
   - Voraussetzungen: Schritte 1–4 (das beschriebene Verhalten muss dem Code entsprechen).
   - Beschreibung: `docs/help/tv-sender-ablauf-technisch.md`, Abschnitt „Gebündelte Auslöse-Signale" (Z. 937–960), anpassen — beschreibt bislang das sofortige Wecken außerhalb von Scopes und muss das Bündelfenster (`ProgramSchedule:ScheduleChangedDebounceSeconds`, Default 5 s, `0` = Sofortverhalten), den Sofort-Pfad `NotifyScheduleChangedNow` und die Mitbündelung der Live-Leitung aufnehmen. Eintrag in `docs/RELEASE_NOTES.md` ergänzen (nach dem Muster der Signal-Änderungen zu Issue #290/#315: Verhaltensänderung der Sendeplan-Nachführung — Einzel-Commits werden über ein kurzes Fenster zu einem Lauf gebündelt, „Jetzt generieren" bleibt sofort).

7. **Betroffene Bestandstests verifizieren und Suite laufen lassen**
   - Voraussetzungen: Schritte 1–6.
   - Beschreibung: `TVSenderAdminTests`, `MediaSourceScanServiceTests`, `MediaSourceScanCoordinatorScopeTests`, `MediaDurationProbeWorkerTests`, `MediaDurationProbeServiceTests` und die E2E-Klassen prüfen/laufen lassen; `dotnet build` Debug + Release sowie volle Suite `dotnet test VideoWebPlayer.Tests/VideoWebPlayer.Tests.csproj` (Definition of Done).

## Tests

### Neue Tests

| Test / Hilfsmethode | Testklasse | Was wird geprüft / bereitgestellt? |
|--------------------|------------|-------------------------------------|
| `Notify_OutsideScan_WakesAfterDebounceWindow` | `ProgramScheduleSignalTests` | Meldung ohne Scope weckt `WaitAsync` nicht sofort, sondern erst nach Ablauf des Fensters (getrieben über `ControllableTimeProvider.TickTimers`). |
| `Notify_BurstWithinWindow_WakesOnce` | `ProgramScheduleSignalTests` | Mehrere Meldungen im selben Fenster → genau eine Weckung; ein zweites `WaitAsync` bleibt ausstehend. |
| `Notify_AfterWindowDelivery_StartsNewWindow` | `ProgramScheduleSignalTests` | Meldung nach erfolgter Zustellung öffnet ein neues Fenster → zweite Weckung nach erneutem Fensterablauf. |
| `Notify_BatchedWake_AlsoFeedsLiveChannel` | `ProgramScheduleSignalTests` | Die gebündelte Zustellung weckt `WaitLiveAsync` ebenfalls genau einmal (Mitbündelung der Live-Leitung). |
| `NotifyScheduleChangedNow_WakesImmediately` | `ProgramScheduleSignalTests` | Sofort-Pfad weckt `WaitAsync` ohne Timer-Tick. |
| `NotifyScheduleChangedNow_DuringScan_IsSuppressed` | `ProgramScheduleSignalTests` | Sofort-Pfad respektiert offenen Scan-Scope: keine Weckung während des Scans, genau eine am Scope-Ende. |
| `NotifyScheduleChangedNow_ConsumesPendingBatch` | `ProgramScheduleSignalTests` | Ausstehende Bündelung + `NotifyScheduleChangedNow` → eine sofortige Weckung, kein späterer Flush-Weckruf mehr. |
| `PendingBatch_FlushedDuringScan_IsSuppressedUntilScanEnds` | `ProgramScheduleSignalTests` | Meldung vor `BeginScan`, Flush läuft in den offenen Scope → keine Weckung währenddessen, genau eine am Scope-Ende. |
| `EndScan_Wake_ConsumesPendingBatch_LateFlushDoesNotWake` | `ProgramScheduleSignalTests` | Interleaving: Meldung vor `BeginScan` (Fenster offen, Flush noch nicht abgelaufen) + unterdrückte Meldung **im** Scan → `EndScan` weckt genau einmal und verbraucht die Bündelungs-Markierung → der nachträglich ablaufende Flush schreibt keine zweite Weckung. |
| `DebounceDisabled_DeliversImmediately` | `ProgramScheduleSignalTests` | `ScheduleChangedDebounceSeconds = 0` → `NotifyScheduleChanged` liefert sofort (Rückfall auf das bisherige Verhalten). |

### Betroffene bestehende Tests

| Test / Testklasse | Grund der Anpassung |
|-------------------|---------------------|
| `ProgramScheduleSignalTests.Notify_WakesWaiter`, `Notify_IsCoalesced` | Erwarten Sofort-Zustellung; mit dem neuen Default-Fenster bliebe `WaitAsync` ausstehend — Signal wird mit `ScheduleChangedDebounceSeconds = 0` konstruiert (Sofort-Modus). |
| `GatedScheduleSignal` (`MediaDurationProbeWorkerTests`) | Muss `NotifyScheduleChangedNow()` implementieren (Interface-Erweiterung). |
| `FakeScheduleSignal` (`MediaDurationProbeServiceTests`) | Muss `NotifyScheduleChangedNow()` implementieren (Interface-Erweiterung). |
| `TVSenderAdminTests`, `MediaSourceScanServiceTests`, `MediaSourceScanCoordinatorScopeTests`, `MediaDurationProbeWorkerTests` | Keine Code-Anpassung erwartet (Scope-Pfade unverändert bzw. `NotifyLiveScheduleChanged` unberührt), aber im Lauf zu verifizieren, da das echte `ProgramScheduleSignal` nun standardmäßig bündelt. |

### E2E-Tests (primärer Funktionsnachweis)

Keine neuen E2E-Tests erforderlich: Die Anforderung ändert keinen Benutzerfluss — sie verändert ausschließlich internes Zeitverhalten (Weck-Bündelung ≤ 5 s) ohne neue oder geänderte UI-Interaktion. Der einzige Nutzerfluss mit Zeitvertrag, „Jetzt generieren", bleibt über `NotifyScheduleChangedNow` unverzögert und ist durch bestehende E2E-Tests abgedeckt, die das materialisierte Ergebnis per DB-Polling mit großzügigen Timeouts prüfen (`TVSenderAdminE2ETests` u. a. — „Jetzt generieren", Regel-Saves, Scope-Umstellung). Diese bestehenden E2E-Tests dienen als Regression und sind im Lauf zu verifizieren, nicht anzupassen. Das Bündelverhalten selbst ist über die UI nicht beobachtbar und wird durch die Unit-Tests am Signal nachgewiesen.

## Akzeptanzszenarien

| # | Szenario | Auslöser | Ausgangszustand | Schritte | Sichtbare Wirkung | Nachweis |
|---|----------|----------|------------------|----------|-------------------|----------|
| 1 | Burst-Bündelung (Happy Path) | Admin speichert in < 5 s drei Regel-/Clip-Gruppen-Änderungen über `TVSenderAdmin` | TV-Sender aktiv, kein Scan läuft | Jeder Save ruft `NotifyScheduleChanged()` → Fenster öffnet beim ersten, weitere markieren nur ausstehend → Fensterende liefert einmal | Genau ein `ExecutePassAsync`-Lauf nach Fensterende; Sendeplan enthält alle drei Änderungen | Unit-Tests `Notify_BurstWithinWindow_WakesOnce`, `Notify_AfterWindowDelivery_StartsNewWindow`; E2E-Regression `TVSenderAdminE2ETests` |
| 2 | „Jetzt generieren" bleibt sofort | Klick auf „Jetzt generieren" | TV-Sender aktiv, kein Scan | `NotifyScheduleChangedNow()` → sofortige Zustellung → Worker-Lauf; Seite wartet 4 s und lädt neu | Status „aktualisiert" wie bisher; Plan-Lauf startet ohne Fensterverzögerung | Unit-Test `NotifyScheduleChangedNow_WakesImmediately`; bestehender E2E-Pfad `TVSenderAdminE2ETests` |
| 3 | Scan-Suppression unverändert | `MediaSourceScanCoordinator`-Scan mit Commits | Scan-Scope offen | Meldungen → `_suppressedNotifications`; Scope-Ende weckt einmal | Genau ein Worker-Lauf am Scan-Ende | Bestehende Tests `Notify_DuringScan_IsSuppressed_UntilScanEnds`, `MediaDurationProbeWorker_BundlesBacklogSignals_SingleWake`, `RunFullScanAsync_DefersScheduleWake_UntilScanEnds` |
| 4 | Isolierter Commit | Einzelnes Speichern (z. B. eine Regel) | Kein Scan, kein Burst | Meldung öffnet Fenster → Zustellung am Fensterende | Ein Plan-Lauf, um ≤ 5 s verzögert; Endzustand unverändert | Unit-Test `Notify_OutsideScan_WakesAfterDebounceWindow` |
| 5 | Flush während Scan-Start | Meldung kurz vor Scan-Beginn, Flush läuft in den Scope | Fenster ausstehend, `BeginScan` öffnet | Flush sieht `_activeScans > 0` → Überführung in `_suppressedNotifications` | Kein Weckruf während des Scans; genau einer am Scope-Ende | Unit-Test `PendingBatch_FlushedDuringScan_IsSuppressedUntilScanEnds` |
| 6 | Live-Leitung | Materialisierte Planänderung nach dem gebündelten Lauf | Worker-Lauf erzeugt/verdrängt Slots | `NotifyLiveScheduleChanged()` schreibt sofort `_liveChannel` | `LiveStreamWorker` synchronisiert unverzüglich — keine Fensterverzögerung auf dem materialisierten Pfad | Unit-Test `Notify_BatchedWake_AlsoFeedsLiveChannel` + bestehende `LiveStreamWorkerTests` |
| 7 | `EndScan` verbraucht ausstehende Bündelung | Meldung kurz vor `BeginScan` (Fenster offen), weitere Meldung im Scan | Fenster ausstehend, Scan-Scope öffnet sich, Flush läuft erst nach Scope-Ende ab | `EndScan` weckt einmal und löscht die Bündelungs-Markierung → späterer Flush findet keine Markierung | Genau eine Weckung für das gesamte Interleaving — kein zweiter Weckruf durch den späten Flush | Unit-Test `EndScan_Wake_ConsumesPendingBatch_LateFlushDoesNotWake` |

## Offene Punkte

Keine. Die in der Anforderung offenen Fragen (Grundentscheidung, Live-Leitung, Griff der Bündelung, Verzögerungsdauer/Konfigurierbarkeit) sind als Designentscheidungen getroffen (siehe oben).
