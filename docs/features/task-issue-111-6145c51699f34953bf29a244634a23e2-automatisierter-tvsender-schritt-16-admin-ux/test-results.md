# Testergebnisse

## Ergebnis

**Status:** Grün

## Ausgeführte Tests

| Test | Zweck | Ergebnis |
|------|-------|----------|
| `MediaDurationProbeServiceTests_Progress.GetProgressAsync_PendingItems_ListsUnprobedAndRetrying` | N13: Pending-Liste (nie probiert + Retry) | Bestanden |
| `MediaDurationProbeServiceTests_Progress.GetProgressAsync_FailedItems_ListsExhaustedAttemptsNewestFirst` | N13: Failed-Liste, Sortierung | Bestanden |
| `MediaDurationProbeServiceTests_Progress.GetProgressAsync_DetailItems_RespectLimit_CountersStayFull` | N13: Listen-Kappung, Zähler voll | Bestanden |
| `Components.TVSenderAdminTests.RendersFourTabs_WithRulesActive` | N14: Vier Tabs, Regeln aktiv, Panes im DOM | Bestanden |
| `Components.TVSenderAdminTests.SwitchesTabs_AndShowsProbeDetailTables` | N13+N14: Tabwechsel, ▾ Details, `open`-Attribut | Bestanden |
| `Components.TVSenderAdminTests.RuleEditor_RendersAsCard_InsideRulesTab` | N14: Editor als Card im Regeln-Tab, kein Overlay | Bestanden |
| `TVSenderAdminE2ETests.Admin_CreateEditDeactivateDeleteRule` | End-to-End: Tabs, Detail-Tabellen, Regel-CRUD, Einstellungen, Sendeplan | Bestanden |
| `TVSenderAdminE2ETests.Admin_GenreRule_MultiSelectAndSourceFilter` | Regression: Genre-/Quell-Auswahl im Editor-Card | Bestanden |
| `TVSenderAdminE2ETests.NonAdmin_SeesNotAuthorized` | Regression: Admin-Gate | Bestanden |

## Gesamtlauf

`dotnet test VideoWebPlayer.Tests/VideoWebPlayer.Tests.csproj --no-build` (synchron, nach Debug-Build)

- **Gesamt:** 2099
- **Bestanden:** 2098
- **Übersprungen:** 1 — `DevicePlaylistE2ETests_Playback.PairedDevice_ReportsProgressForMovieWithoutCollection_ContinueWatchingListStillLoads` (identischer Skip wie Baseline)
- **Fehlgeschlagen:** 0

Protokoll: `inventory/test-results/final-dotnet-test.log`.
Builds: `dotnet build VideoPlayer.sln` Debug **und** Release jeweils 0 Fehler.
