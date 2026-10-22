# Plan-Review

## Ergebnis

**Status:** Vollständig umgesetzt

## Umgesetzte Planelemente

- [x] Record `MediaDurationProbeDetailItem` (Name, SourceName, LastAttemptAt `DateTime?`, Attempts) — in `MediaDurationProbeProgress.cs`
- [x] `MediaDurationProbeProgress` um `PendingItems`/`FailedItems` erweitert
- [x] `MediaDurationProbeService.GetProgressAsync` — `detailItemCount = 25`-Parameter, Pending-/Failed-Abfragen (identische Filter wie Zähler, `DurationProbedAt` absteigend, `Take`), über gemeinsamen Helfer `DetailItemsAsync`
- [x] `app.css`: `.admin-tabs`, `.admin-tab`(+.active), `.tab-pane`(+.active), `.rule-editor-card`, `.admin-header-side`, `.admin-card-header-left`, `.admin-sub-card`, `.actions-cell`, `.tvsender-slot-displaced`, `.probe-counts`, `.chip-detail-btn`, `.probe-detail`(summary/table-wrap-inner)
- [x] `TVSenderAdmin.razor`: `admin-tabs`-Navigation (4 Tabs, `role=tablist/tab`, `aria-selected`, `aria-controls`), `activeTab`-State, `SelectTab`, vier `.tab-pane` (`#tab-regeln/sendeplan/einstellungen/laufzeit`), Regeln initial aktiv
- [x] Regel-Editor als `#rule-editor` `.rule-editor-card` im Regeln-Tab (alle IDs/Bindings unverändert; kein `admin-dialog-overlay` mehr für den Editor; Lösch-Dialog bleibt Overlay)
- [x] Header-Chips `#tvsender-header-channel`/`#tvsender-header-interval` + `.admin-header-side`
- [x] Einstellungen in drei `.admin-sub-card`-Gruppen (Sender/Planung/Segmentierung)
- [x] „▾ Details"-Buttons `#tvsender-probe-pending-details`/`#tvsender-probe-failed-details`, `<details class="probe-detail">` mit `open`-Binding, Tabellen (Name/Quelle/Versuche/Letzter Versuch), `.tvsender-probe-detail-row`, „… und N weitere"-Schlusszeile
- [x] `ProbeProgressLoopAsync`/`InvokeAsync`-Dispatcher unverändert; `LoadProbeProgressAsync` nutzt benanntes `cancellationToken`-Argument
- [x] Service-Tests: `GetProgressAsync_PendingItems_ListsUnprobedAndRetrying`, `..._FailedItems_ListsExhaustedAttemptsNewestFirst`, `..._DetailItems_RespectLimit_CountersStayFull`
- [x] bUnit `TVSenderAdminTests` (3 Tests: Tabs/Wechsel+Details/Editor-Card)
- [x] `TVSenderAdminE2ETests.Admin_CreateEditDeactivateDeleteRule` an Tabs angepasst + Tab-/Detail-Assertions; `SwitchTabAsync`-Helfer
- [x] `docs/help/tv-sender.md` aktualisiert (Tab-Struktur, ▾ Details-Tabellen)

## Hinweise

Das optionale „Probe zurücksetzen" (N13) wurde bewusst nicht umgesetzt — im Mockup nicht enthalten und in `nachbesserungen.md` als optional markiert (in `requirement.md` dokumentiert).
