# Abnahme Schritt 16 — Adminseite restrukturieren + Probe-Detail-Listen (N13+N14)

Prüfer: Devin (unabhängige Abnahme, nicht der Implementierer)
Geprüfter Stand: `bed4393` (`feat: Adminseite in vier Tabs gegliedert und Probe-Detail-Listen
für ausstehende/fehlgeschlagene Dateien (N13+N14)`), Arbeitsbaum sauber.

## Ergebnis

**Status:** Erfüllt

Die Umsetzung folgt dem abgenommenen Mockup
(`docs/projects/.../preview/admin-ux-vorschau.html`) in Struktur, Bezeichnern und Inhalten;
kein Funktionsverlust, der InvokeAsync-Dispatcher-Fix aus Schritt 11 ist unverändert
wirksam, und die neuen Detail-Listen sind filterdeckungsgleich zu den Zählern.

### Verifiziert

- **Vier Tabs wie im Mockup:** `nav.admin-tabs` mit `role="tablist"`, vier Buttons
  `role="tab"` in Mockup-Reihenfolge und -Beschriftung (Regeln / Sendeplan / Einstellungen /
  Laufzeitermittlung), `aria-selected` + `aria-controls`; vier `.tab-pane` mit
  `role="tabpanel"`/`aria-labelledby` bleiben vollständig im DOM und werden nur per
  `.active`-Klasse über CSS ein-/ausgeblendet (`TVSenderAdmin.razor:45-58`, Panes
  `:61/:285/:342/:405`, `app.css:544-587`). `activeTab`-State, initial `„regeln"` —
  entspricht dem Mockup (`admin-ux-vorschau.html:502-510`). Formularzustand und der
  15-s-Refresh-Loop überleben Tab-Wechsel ohne Reload (Kommentar `:662-664`).
- **Regel-Editor als einklappbare Card:** `#rule-editor`/`rule-editor-card` innerhalb der
  Regeln-Pane statt bisherigem `admin-dialog-overlay` (`TVSenderAdmin.razor:129-280`);
  „Schließen"/„Abbrechen" setzen `editRule = null` (`:933`). Der Lösch-Bestätigungsdialog
  bleibt bewusst Overlay (`:567-581`) — wie im Mockup.
- **Header-Chips + Unterkarten:** Sendername + Generierungsintervall als Chips im Header
  (`:35-42`), Einstellungen in drei `admin-sub-card`-Gruppen Sender/Planung/Segmentierung
  (`:344-400`) — mockuptreu (`admin-ux-vorschau.html:819-876`).
- **N13-Detail-Listen:** „▾ Details"-Buttons an den Chips „ausstehend"/„fehlgeschlagen"
  (nur gerendert, wenn Einträge existieren), `<details class="probe-detail">` mit
  `open` an `showPendingProbeDetails`/`showFailedProbeDetails` gebunden; Tabellen mit
  exakt den geforderten Spalten Name / Quelle / Versuche / Letzter Versuch, `LastAttemptAt`
  null → „—"; Kappungshinweis „… und N weitere" sobald `Count > Items.Count`
  (`TVSenderAdmin.razor:435-531`). Summary-Texte deckungsgleich zum Mockup.
- **Service-Filter deckungsgleich zu Zählern:** pending = `Duration == null &&
  DurationProbeAttempts < EffectiveMaxAttempts`, failed = `>= max` — `Pending =
  Total − Probed − Failed` ist damit exakt die Pending-List-Menge; Sortierung
  `DurationProbedAt` DESC (nie geprüfte Items mit null zuletzt), `Take(detailItemCount)`,
  Default 25 (`MediaDurationProbeService.cs:147-225`, Record
  `MediaDurationProbeProgress.cs:31-67`). Einziges Konsument ist die Seite; Admin-Gate
  `RequireAdminAsync` unverändert am Anfang.
- **Kein Rückbau des Schritt-11-Fixes:** `ProbeProgressLoopAsync` führt den Tick weiterhin
  über `await InvokeAsync(async () => { … LoadProbeProgressAsync … StateHasChanged() })`
  auf dem Dispatcher aus — Kommentar benennt explizit den DbContext-Concurrency-Grund
  (`TVSenderAdmin.razor:673-709`). `disposed`-Guard, `refreshCts.Cancel()` in `Dispose`,
  Einzelfehler-Toleranz unverändert.
- **Kein Funktionsverlust:** Alle Handler und Felder des Alt-Stands vorhanden:
  `SaveChannelSettingsAsync` (`:879`), `GenerateNowAsync` inkl. `ScheduleSignal` +
  `generatePending`-Guard (`:779`), kompletter Regel-Editor (Regelart-/Wiederholungs-
  Wechsel, Wochentag/Datum, Material-Picker `MediaSearchSelector` + Playlist, Genre-/
  Quellen-Mehrfachauswahl mit `ReloadGenreOptionsAsync`), `SaveRuleAsync` (`:1028`),
  `ToggleActiveAsync` (`:1073`), `AskDelete`/`DeleteRuleAsync` (`:1087-1106`),
  Planbar-Diagnose (`:846-871`), Sendeplan-Tabelle inkl. `tvsender-slot-displaced`.
  `GetProgressAsync`-Aufruf nutzt `cancellationToken:`-Named-Arg; Default `detailItemCount=25`.
- **Gegenproben (Mutanten getötet, beide revertiert):**
  1. Pending-Filter `< maxAttempts` → `<= maxAttempts`:
     `GetProgressAsync_PendingItems_ListsUnprobedAndRetrying` schlägt fehl (3 statt 2
     Einträge — defektes Item rutscht in die Pending-Liste). Revertiert.
  2. „▾ Details"-Button am Pending-Chip entfernt:
     `TVSenderAdminTests.SwitchesTabs_AndShowsProbeDetailTables` schlägt fehl
     (`ElementNotFoundException #tvsender-probe-pending-details`), zusätzlich E2E
     `Admin_CreateEditDeactivateDeleteRule` (Timeout auf denselben Selektor). Revertiert.
- **Tests:** 3 neue Service-Tests (Pending-Liste inkl. Sortierung/null-Timestamp,
  Failed-Liste newest-first, Kappung bei vollem Zähler) gegen echtes SQLite; 3 neue
  bUnit-Tests (`Components/TVSenderAdminTests.cs`: Tab-Struktur + aria + Panes im DOM,
  Tab-Wechsel + Detail-Tabellen inkl. open-Toggle und Zeileninhalten, Editor-Card ohne
  Overlay); E2E `Admin_CreateEditDeactivateDeleteRule` auf Tab-Bedienung umgebaut
  (`SwitchTabAsync` über `DriveUntilStateAsync`), mit neuen Assertions zu Tabs,
  Detail-Listen und Editor-Card — Deckung nicht reduziert (alle Alt-Stationen:
  Einstellungen speichern, Regel anlegen/diagnostizieren/de-/reaktivieren/löschen,
  „Jetzt generieren", Probe-Karte).
- **Builds:** `dotnet build VideoPlayer.sln` Debug **und** Release: jeweils 0 Fehler,
  142 Warnungen — identischer Bestand, keine neue Warnung in geänderten Dateien
  (geprüft per Grep auf TVSender/MediaDurationProbe/app.css). Kein Datei-Lock aufgetreten.
- **Suite:** `dotnet test VideoWebPlayer.Tests/VideoWebPlayer.Tests.csproj` (Debug),
  synchron: **2097 grün, 1 Skip** (bekannter Alt-Skip
  `DevicePlaylistE2ETests_Playback.PairedDevice_…ContinueWatchingListStillLoads`),
  **1 FAIL** = `PlaylistDetailE2ETests.Detail_Page_Delete_Shows_Confirmation_And_Deletes`
  (Playwright-Timeout 30 s auf `#confirm-delete-playlist-button`, ohne jeden Bezug zur
  Änderung — Playlist-Detailseite). **Einzelwiederholung: grün (8 s).** Effektiv 2098
  grün + 1 Alt-Skip, wie vom Implementierer berichtet. Nach den Mutationen die 14
  betroffenen Tests (8 Service + 3 bUnit + 3 E2E) einzeln erneut verifiziert: alle grün.
- **Doku:** `docs/help/tv-sender.md` (Tab-Struktur, Detail-Listen, „Jetzt generieren"-/
  Planung-Verweise korrigiert) und `docs/RELEASE_NOTES.md` (zwei Einträge, EN) aktualisiert;
  `handover.md` + Tasks-Datei gepflegt. Keine Migration nötig (reine Abfrage-/UI-Änderung).

## Abweichungen

- [ ] Keine.

## Hinweise

- **`<details>`-Zustand vs. gebundenes Flag:** Ein Klick direkt auf `<summary>` toggelt
  `open` nativ im DOM, ohne `showPendingProbeDetails`/`showFailedProbeDetails` zu
  aktualisieren; der „▾ Details"-Chip-Button bleibt die konsistente Steuerung (nach nativem
  Umschalten kann ein Button-Klick zur Resynchronisation wirkungslos erscheinen). Kosmetik,
  dem Mockup-Verhalten entsprechend, kein Defekt — die Listen bleiben über beide Wege
  vollständig bedienbar.
- **Optionales „Probe zurücksetzen"** (N13, als optional markiert) ist bewusst nicht
  umgesetzt — im Handover dokumentiert, im Mockup nicht vorgesehen.
- Editor wird nur bei `editRule != null` gemountet (Zustand geht beim Schließen verloren) —
  identisch zum bisherigen Overlay-Verhalten, mockupkonform.
- Inaktive Panes tragen kein `aria-hidden`; durch `display:none` für AT ohnehin
  ausgeblendet — hinreichend.
- `detailItemCount` liegt in der Signatur vor `cancellationToken`; sämtliche Aufrufstellen
  nutzen das benannte `cancellationToken:`-Argument, einziger Produktiv-Konsument ist die
  Seite selbst.
