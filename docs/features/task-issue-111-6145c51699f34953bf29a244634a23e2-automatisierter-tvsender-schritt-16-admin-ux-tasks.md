# Tasks: TV-Sender-Adminseite restrukturieren + Probe-Detail-Listen (N13+N14)

| # | Bereich | Aufgabe | Status | Testnachweis |
|---|---------|---------|--------|--------------|
| 1 | Datenmodell | Record `MediaDurationProbeDetailItem` (Name, SourceName, LastAttemptAt, Attempts) anlegen | Erledigt | Suite: 2098 grün / 0 Fehler |
| 2 | Datenmodell | `MediaDurationProbeProgress` um `PendingItems`/`FailedItems` erweitern | Erledigt | Suite: 2098 grün / 0 Fehler |
| 3 | Logik | `MediaDurationProbeService.GetProgressAsync` um `detailItemCount`-Parameter + Pending-/Failed-Abfragen erweitern | Erledigt | Suite: 2098 grün / 0 Fehler |
| 4 | UI/CSS | `app.css`: `.admin-tabs`, `.admin-tab`, `.tab-pane`, `.rule-editor-card`, `.admin-header-side`, `.admin-card-header-left`, `.admin-sub-card`, `.actions-cell`, `.tvsender-slot-displaced`, `.probe-counts`, `.chip-detail-btn`, `.probe-detail` ergänzen | Erledigt | Suite: 2098 grün / 0 Fehler |
| 5 | UI | `TVSenderAdmin.razor`: Tab-Navigation + vier Panes (Regeln/Sendeplan/Einstellungen/Laufzeitermittlung) mit `activeTab`-State | Erledigt | Suite: 2098 grün / 0 Fehler |
| 6 | UI | `TVSenderAdmin.razor`: Regel-Editor als einklappbare `#rule-editor`-Card im Regeln-Tab (statt Overlay) | Erledigt | Suite: 2098 grün / 0 Fehler |
| 7 | UI | `TVSenderAdmin.razor`: Header-Chips (Sendername, Generierungsintervall) + `admin-header-side` | Erledigt | Suite: 2098 grün / 0 Fehler |
| 8 | UI | `TVSenderAdmin.razor`: Einstellungen in drei `admin-sub-card`-Gruppen gliedern | Erledigt | Suite: 2098 grün / 0 Fehler |
| 9 | UI | `TVSenderAdmin.razor`: „▾ Details"-Buttons an ausstehend/fehlgeschlagen + `<details class="probe-detail">`-Tabellen mit Kappungshinweis | Erledigt | Suite: 2098 grün / 0 Fehler |
| 10 | Tests | `MediaDurationProbeServiceTests_Progress`: Pending-Listen-Test | Erledigt | Suite: 2098 grün / 0 Fehler |
| 11 | Tests | `MediaDurationProbeServiceTests_Progress`: Failed-Listen-Test | Erledigt | Suite: 2098 grün / 0 Fehler |
| 12 | Tests | `MediaDurationProbeServiceTests_Progress`: Limit-Test | Erledigt | Suite: 2098 grün / 0 Fehler |
| 13 | Tests | bUnit `TVSenderAdminTests` anlegen (Tab-Struktur, Tab-Wechsel, Detail-Tabellen, Editor-Card) | Erledigt | Suite: 2098 grün / 0 Fehler |
| 14 | E2E-Tests | `TVSenderAdminE2ETests.Admin_CreateEditDeactivateDeleteRule` an Tabs anpassen + Tab-/Detail-Assertions ergänzen | Erledigt | Suite: 2098 grün / 0 Fehler |
| 15 | Dokumentation | `docs/help/tv-sender.md`: Tab-Struktur + Detail-Listen beschreiben | Erledigt | Suite: 2098 grün / 0 Fehler |
