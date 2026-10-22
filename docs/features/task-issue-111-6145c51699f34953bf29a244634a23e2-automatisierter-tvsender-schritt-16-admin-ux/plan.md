# Umsetzungsplan: TV-Sender-Adminseite restrukturieren + Probe-Detail-Listen (N13+N14)

## Übersicht

`/admin/tv-sender` wird in vier Tabs gegliedert (Regeln | Sendeplan | Einstellungen |
Laufzeitermittlung), der Regel-Editor wandert vom Overlay-Dialog in eine einklappbare
Card im Regeln-Tab, und `MediaDurationProbeService.GetProgressAsync` liefert zusätzlich
limitierte Listen der ausstehenden und fehlgeschlagenen Dateien, die in der
Laufzeitermittlung über „▾ Details" aufklappbar sind. Umsetzung folgt dem abgenommenen
Mockup `preview/admin-ux-vorschau.html`; sämtliche bestehenden IDs, Bindings und
Service-Aufrufe bleiben erhalten.

## Designentscheidungen

| Komponente / Bereich | Gewählter Ansatz | Begründung |
|----------------------|-----------------|------------|
| Tab-Umschaltung | Alle Panes bleiben gerendert, Umschalten über `.tab-pane`/`.active` + Razor-Feld `activeTab` | Mockup-Ansatz; Formular- und Scroll-Zustand sowie `@bind`-Werte bleiben über Tab-Wechsel erhalten, kein erneutes Laden — der Auftrag verlangt „kein Re-Loading-Stress" und „kein JS-Zwang" |
| Regel-Editor | `@if (editRule != null)` rendert `#rule-editor` als `.admin-card.rule-editor-card` im Regeln-Tab statt `admin-dialog-overlay` | Mockup zeigt einklappbare Card; alle Editor-IDs/Bindings bleiben unverändert, E2E-Selektoren stabil |
| Lösch-Bestätigung | Bleibt `admin-dialog-overlay`-Dialog | Tatsächlich modale Rückfrage; Mockup ändert daran nichts |
| Detail-Listen | `<details class="probe-detail" open="@bool">` + „▾ Details"-Button im Zähler-Chip (`chip-detail-btn`) | `<details>`/`summary` ist nativ aufklappbar ohne JS; der Chip-Button steuert zusätzlich den `open`-Zustand über Razor — deckt Mockup-Darstellung ab |
| Detail-Elementtyp | Neuer Record `MediaDurationProbeDetailItem` (Name, SourceName, LastAttemptAt `DateTime?`, Attempts) für beide Listen | `MediaDurationProbeRecentItem` hat nicht-nullbaren `ProbedAt` und ein `Duration`-Feld, das die Detail-Tabellen nicht zeigen — ein eigener Typ vermeidet Semantik-Bruch |
| Listenumfang | Neuer optionaler Parameter `detailItemCount = 25` an `GetProgressAsync`; Abschneide-Hinweiszeile „… und N weitere" aus `PendingCount`/`FailedCount` minus Zeilen | Mockup zeigt „… und 32 weitere"; Serverseitiges Limit verhindert lange Admin-Seiten |

## Programmabläufe

### Tab-Wechsel

1. `activeTab` (string, Startwert `"regeln"`) steuert `class="admin-tab @(active)"` der vier
   Buttons (`#tvsender-tab-regeln`, `#tvsender-tab-sendeplan`, `#tvsender-tab-einstellungen`,
   `#tvsender-tab-laufzeit`) mit `role="tab"`, `aria-selected`, `aria-controls`.
2. `@onclick` setzt `activeTab`; die vier Panes (`#tab-regeln`, `#tab-sendeplan`,
   `#tab-einstellungen`, `#tab-laufzeit`, `role="tabpanel"`) tragen `active`, wenn
   `activeTab` passt — CSS `display:none`/`display:block`.

Beteiligte Komponenten: `TVSenderAdmin.razor`, `app.css`.

### Regel-Editor auf-/zuklappen

1. `StartCreate`/`StartEdit` setzen `editRule` → Editor-Card `#rule-editor` erscheint
   unterhalb der Regel-Tabelle im Regeln-Tab.
2. „Schließen"-Button im Kartenkopf, „Abbrechen" unten und `CancelEdit` leeren
   `editRule` → Card verschwindet.

Beteiligte Komponenten: `TVSenderAdmin.razor`, `app.css` (`.rule-editor-card`).

### Probe-Detail-Listen laden und anzeigen

1. `GetProgressAsync(userId, recentCount, detailItemCount, ct)` fragt nach
   `RequireAdminAsync` zusätzlich ab:
   - `PendingItems`: `Duration == null && DurationProbeAttempts < maxAttempts`,
     `DurationProbedAt` absteigend (nie probierte Items zuletzt), `Take(detailItemCount)`.
   - `FailedItems`: `Duration == null && DurationProbeAttempts >= maxAttempts`,
     `DurationProbedAt` absteigend, `Take(detailItemCount)`.
   Projektion auf `MediaDurationProbeDetailItem(mi.Name, mi.MediaCollection.MediaSource.Name,
   mi.DurationProbedAt, mi.DurationProbeAttempts)` — analog `RecentItems`.
2. Die Fortschrittskarte zeigt an den Chips `#tvsender-probe-pending` /
   `#tvsender-probe-failed` je einen `▾ Details`-Button
   (`#tvsender-probe-pending-details`, `#tvsender-probe-failed-details`), der
   `showPendingProbeDetails`/`showFailedProbeDetails` umstellt.
3. `<details id="tvsender-probe-pending-detail" open="@showPendingProbeDetails">` und
   `<details id="tvsender-probe-failed-detail" ...>` werden nur gerendert, wenn die
   jeweilige Liste Einträge hat; `summary` nennt Anzahl und Bedeutung; darunter eine
   Tabelle (Name/Quelle/Versuche/Letzter Versuch) mit Zeilenklasse
   `.tvsender-probe-detail-row`; bei gekappter Liste eine Schlusszeile „… und N weitere".
4. Der 15-s-Refresh (`ProbeProgressLoopAsync` über `InvokeAsync`) aktualisiert die
   Listen mit — der `open`-Status der `<details>` bleibt durch den Razor-Bool erhalten.

Beteiligte Komponenten: `MediaDurationProbeService`, `MediaDurationProbeProgress`,
`MediaDurationProbeDetailItem` (neu), `TVSenderAdmin.razor`, `app.css`.

## Neue Klassen

| Klasse | Typ | Zweck |
|--------|-----|-------|
| `MediaDurationProbeDetailItem` | record (in `MediaDurationProbeProgress.cs`) | Eintrag der Pending-/Failed-Listen: `Name`, `SourceName`, `LastAttemptAt` (`DateTime?`), `Attempts` |

## Änderungen an bestehenden Klassen

### `MediaDurationProbeProgress` (record)

- **Neue Parameter:** `PendingItems` (`IReadOnlyList<MediaDurationProbeDetailItem>`),
  `FailedItems` (`IReadOnlyList<MediaDurationProbeDetailItem>`) — nach `RecentItems`
  ans Ende der Parameterliste.

### `MediaDurationProbeService`

- **Geänderte Methoden:** `GetProgressAsync` — neuer optionaler Parameter
  `int detailItemCount = 25`; zwei zusätzliche Projektionsabfragen analog
  `RecentItems` (Pending: `Duration == null && DurationProbeAttempts < maxAttempts`;
  Failed: `Duration == null && DurationProbeAttempts >= maxAttempts`, beide
  `DurationProbedAt` absteigend, `Take(detailItemCount)`); neuer Record-Konstruktoraufruf
  mit acht Argumenten.

### `TVSenderAdmin.razor` (Komponente)

- **Neue Felder:** `activeTab` (`string`, `"regeln"`), `showPendingProbeDetails` (`bool`),
  `showFailedProbeDetails` (`bool`).
- **Neue Methoden:** `SelectTab(string)` — setzt `activeTab`.
- **Markup:** Vier-Karten-Layout → `admin-tabs`-Navigation + vier `.tab-pane`-Blöcke;
  Seitenkopf ergänzt `admin-header-side` mit Status-Chips (Sendername aus
  `channel?.Name`, Intervall aus `intervalMinutes`) und dem bisherigen Zurück-Link;
  Regel-Editor von `admin-dialog-overlay` zu `#rule-editor`-Card im Regeln-Tab;
  Fortschrittskarte um `.probe-counts`-Wrapper, Detail-Buttons und zwei
  `<details class="probe-detail">`-Blöcke erweitert; Einstellungen in drei
  `admin-sub-card`-Gruppen (Sender/Planung/Segmentierung).
- **Unverändert:** Sämtliche Feld-Bindings, Element-IDs, `OnInitializedAsync`,
  `ProbeProgressLoopAsync`, `GenerateNowAsync`, Diagnose-/Status-Logik,
  `RuleEditModel`, Lösch-Dialog, Nicht-Admin-Gate.

### `app.css`

- **Neue Klassen:** `.admin-tabs`, `.admin-tab` (+`:hover`, `.active`),
  `.tab-pane` (+`.active`), `.rule-editor-card`, `.admin-header-side`,
  `.admin-card-header-left`, `.admin-sub-card`, `.actions-cell`,
  `.tvsender-slot-displaced` (Durchstreich-Optik analog Mockup `.row-displaced`),
  `.probe-counts`, `.chip-detail-btn`, `.probe-detail` (+`summary`,
  `.table-wrap-inner`) — alles im Stil der `--admin-*`-Palette.

## Datenbankmigrationen

Keine.

## Validierungsregeln

Keine — keine neuen Eingaben; die Detail-Abfrage ist read-only und bleibt
admin-only (`RequireAdminAsync`).

## Konfigurationsänderungen

Keine (`detailItemCount` ist Methodenparameter mit Standardwert 25, keine
`appsettings`-Option).

## Seiteneffekte und Risiken

- **`TVSenderAdminE2ETests`:** `OpenAdminPageAsAdminAsync` wartet auf `#tvsender-name`
  und bedient Einstellungsfelder — diese liegen künftig im versteckten
  Einstellungen-Tab (`display:none`); der Test muss zuerst auf den jeweiligen Tab
  wechseln (Playwright `fill`/`click` verlangt Sichtbarkeit).
- **`<details open>`-Attribut:** Blazor behandelt `open` als Boolesches Attribut
  (`true` → Attribut gesetzt, `false` → weggelassen) — verifiziert durch bUnit-Test.
- **Refresh-Loop:** `GetProgressAsync` führt zwei weitere `Take`-Abfragen alle 15 s
  aus — begrenzt auf `detailItemCount` Zeilen, gegen indizierte Spalten; kein
  spürbarer Zusatzaufwand, die Abfragen laufen weiter auf dem Dispatcher.
- **Kopfzeilen-Chips:** `intervalMinutes`/`channel?.Name` sind zum Renderzeitpunkt
  gesetzt (nach `OnInitializedAsync`), kein Null-Risiko bei `channel` nach
  `GetOrCreateChannelAsync`.

## Umsetzungsreihenfolge

1. **`MediaDurationProbeDetailItem` + `MediaDurationProbeProgress` erweitern**
   - Voraussetzungen: keine
   - Neuen Record anlegen, Record-Parameter `PendingItems`/`FailedItems` ergänzen.

2. **`MediaDurationProbeService.GetProgressAsync` erweitern**
   - Voraussetzungen: Schritt 1
   - `detailItemCount`-Parameter, beide Listenabfragen, neuer Record-Aufruf.

3. **Service-Tests ergänzen** (`MediaDurationProbeServiceTests_Progress`)
   - Voraussetzungen: Schritt 2
   - Tests für Inhalt/Felder, Limit, Konsistenz mit Zählern, Nicht-Video-Ausschluss.

4. **`app.css` um Tab-/Detail-Klassen ergänzen**
   - Voraussetzungen: keine

5. **`TVSenderAdmin.razor` restrukturieren**
   - Voraussetzungen: Schritte 1–4
   - Tab-Navigation, vier Panes, Header-Chips, Editor-Card, Einstellungs-Subkarten,
     Probe-Detail-Sektionen, neue Razor-Felder.

6. **bUnit-Test `TVSenderAdminTests` anlegen**
   - Voraussetzungen: Schritt 5
   - SQLite-In-Memory-DB, Service-Registrierung wie bei `GenreAdminTests`,
     `ctx.AddAuthorization()` mit `IsAdmin`-Claim + `NameIdentifier` auf seedeten
     Admin-User; `VideoWebPlayerClient` mit Stub-`HttpMessageHandler` (`[]`-Antworten).

7. **`TVSenderAdminE2ETests` an Tab-Struktur anpassen + erweitern**
   - Voraussetzungen: Schritt 5
   - `OpenAdminPageAsAdminAsync` auf sichtbares Warte-Element umstellen, Tab-Klicks
     vor Feldzugriffen, Assertions für Tabs und Detail-Listen in den bestehenden
     konsolidierten Test.

8. **Hilfe-Doku `docs/help/tv-sender.md` aktualisieren**
   - Voraussetzungen: Schritt 5

## Tests

### Neue Tests

| Test / Hilfsmethode | Testklasse | Was wird geprüft / bereitgestellt? |
|--------------------|------------|-------------------------------------|
| `GetProgressAsync_PendingItems_ListsUnprobedAndRetrying` | `MediaDurationProbeServiceTests_Progress` | Pending-Liste enthält nie probierte und retryende Items (nicht ermittelte/nicht gescheiterte/nicht-Nichtvideos), Felder Name/Quelle/Versuche/Letzter Versuch inkl. `null`-Zeitpunkt |
| `GetProgressAsync_FailedItems_ListsExhaustedAttempts` | `MediaDurationProbeServiceTests_Progress` | Failed-Liste enthält nur erschöpfte Items, sortiert nach letztem Versuch absteigend, mit allen Feldern |
| `GetProgressAsync_DetailItems_RespectLimit` | `MediaDurationProbeServiceTests_Progress` | `detailItemCount` kappt beide Listen; Zähler bleiben vollständig |
| `RendersFourTabs_WithRulesActive` | `TVSenderAdminTests` (bUnit, neu) | Vier `admin-tab`-Buttons mit den Mockup-Titeln, `aria-selected`, initial `Regeln` aktiv; Inhalte der anderen Tabs im DOM aber ohne `active` |
| `SwitchesTabs_AndShowsProbeDetailTables` | `TVSenderAdminTests` (bUnit, neu) | Klick auf „Laufzeitermittlung" aktiviert Pane; „▾ Details" öffnet `<details>` und zeigt Dateinamen aus Pending-/Failed-Listen |
| `RuleEditor_RendersAsCard_InsideRulesTab` | `TVSenderAdminTests` (bUnit, neu) | „Neue Regel" öffnet `#rule-editor` als Card (kein `admin-dialog-overlay`), „Schließen" klappt zu |

### Betroffene bestehende Tests

| Test / Testklasse | Grund der Anpassung |
|-------------------|---------------------|
| `TVSenderAdminE2ETests.Admin_CreateEditDeactivateDeleteRule` | Einstellungsfelder und Probe-Karte liegen in anderen Tabs; Selektoren brauchen vorherigen Tab-Wechsel; zusätzlich Assertions der Tab-Struktur und Detail-Listen |
| `TVSenderAdminE2ETests.Admin_GenreRule_MultiSelectAndSourceFilter` | kein inhaltlicher Eingriff erwartet (Editor bleibt im aktiven Regeln-Tab), Selektoren unverändert — Lauf verifizieren |
| `MediaDurationProbeServiceTests_Progress.*` | Record-Konstruktor wächst — Aufrufe nur im Service, Tests lesen Felder per Name; unverändert lauffähig |

### E2E-Tests (primärer Funktionsnachweis)

| Priorität | Szenario | Testdatei / Testklasse | Abgedecktes Akzeptanzkriterium | Warum E2E nötig ist |
|-----------|----------|------------------------|-------------------------------|-------------------|
| Pflicht | Tab-Struktur: vier Tabs sichtbar, Regeln aktiv, Wechsel zu Einstellungen/Sendeplan/Laufzeitermittlung zeigt jeweilige Inhalte | `TVSenderAdminE2ETests.Admin_CreateEditDeactivateDeleteRule` | AK 1, 5 | Sichtbarkeit/Wechsel ist echter Browserfluss |
| Pflicht | Detail-Listen: „▾ Details" an ausstehend/fehlgeschlagen zeigt Tabelle mit Dateiname, Quelle, Versuchen, letztem Versuch | `TVSenderAdminE2ETests.Admin_CreateEditDeactivateDeleteRule` | AK 3, 5 | Aufklapp-Interaktion über gerendertes UI |
| Pflicht | Regel-Lifecycle über neue Editor-Card + Einstellungen im neuen Tab (bestehender konsolidierter Test) | `TVSenderAdminE2ETests.Admin_CreateEditDeactivateDeleteRule` | AK 2, 5 | Regressionsschutz des gesamten Benutzerflusses |

Bestehende E2E-Anpassungen siehe Tabelle oben — keine neuen Testmethoden, die
Abdeckung wird in den vorhandenen konsolidierten Test integriert (E2E-Methodenzahl
bewusst minimal halten: jede Methode startet Browser + Server).

## Offene Punkte

Keine.
