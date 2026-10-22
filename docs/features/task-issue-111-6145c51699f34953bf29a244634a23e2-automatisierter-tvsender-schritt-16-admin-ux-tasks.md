# Tasks: TV-Sender-Adminseite restrukturieren + Probe-Detail-Listen (N13+N14)

| # | Bereich | Aufgabe | Status | Testnachweis |
|---|---------|---------|--------|--------------|
| 1 | Datenmodell | Record `MediaDurationProbeDetailItem` (Name, SourceName, LastAttemptAt, Attempts) anlegen | Offen | — |
| 2 | Datenmodell | `MediaDurationProbeProgress` um `PendingItems`/`FailedItems` erweitern | Offen | — |
| 3 | Logik | `MediaDurationProbeService.GetProgressAsync` um `detailItemCount`-Parameter + Pending-/Failed-Abfragen erweitern | Offen | — |
| 4 | UI/CSS | `app.css`: `.admin-tabs`, `.admin-tab`, `.tab-pane`, `.rule-editor-card`, `.admin-header-side`, `.admin-card-header-left`, `.admin-sub-card`, `.actions-cell`, `.tvsender-slot-displaced`, `.probe-counts`, `.chip-detail-btn`, `.probe-detail` ergänzen | Offen | — |
| 5 | UI | `TVSenderAdmin.razor`: Tab-Navigation + vier Panes (Regeln/Sendeplan/Einstellungen/Laufzeitermittlung) mit `activeTab`-State | Offen | — |
| 6 | UI | `TVSenderAdmin.razor`: Regel-Editor als einklappbare `#rule-editor`-Card im Regeln-Tab (statt Overlay) | Offen | — |
| 7 | UI | `TVSenderAdmin.razor`: Header-Chips (Sendername, Generierungsintervall) + `admin-header-side` | Offen | — |
| 8 | UI | `TVSenderAdmin.razor`: Einstellungen in drei `admin-sub-card`-Gruppen gliedern | Offen | — |
| 9 | UI | `TVSenderAdmin.razor`: „▾ Details"-Buttons an ausstehend/fehlgeschlagen + `<details class="probe-detail">`-Tabellen mit Kappungshinweis | Offen | — |
| 10 | Tests | `MediaDurationProbeServiceTests_Progress`: Pending-Listen-Test | Offen | — |
| 11 | Tests | `MediaDurationProbeServiceTests_Progress`: Failed-Listen-Test | Offen | — |
| 12 | Tests | `MediaDurationProbeServiceTests_Progress`: Limit-Test | Offen | — |
| 13 | Tests | bUnit `TVSenderAdminTests` anlegen (Tab-Struktur, Tab-Wechsel, Detail-Tabellen, Editor-Card) | Offen | — |
| 14 | E2E-Tests | `TVSenderAdminE2ETests.Admin_CreateEditDeactivateDeleteRule` an Tabs anpassen + Tab-/Detail-Assertions ergänzen | Offen | — |
| 15 | Dokumentation | `docs/help/tv-sender.md`: Tab-Struktur + Detail-Listen beschreiben | Offen | — |
