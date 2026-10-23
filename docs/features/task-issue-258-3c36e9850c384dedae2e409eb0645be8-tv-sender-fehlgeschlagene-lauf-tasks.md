# Tasks: TV-Sender: Fehlgeschlagene Laufzeit-Probes zurücksetzen (Issue #258)

| # | Bereich | Aufgabe | Status | Testnachweis |
|---|---------|---------|--------|--------------|
| 1 | Datenmodell | Record `MediaDurationProbeDetailItem` um `MediaItemId` erweitern und `DetailItemsAsync` in `MediaDurationProbeService` um die `mi.Id`-Projektion ergänzen | Offen | — |
| 2 | Logik | `MediaDurationProbeService.ResetFailedProbeAsync(userId, mediaItemId)` anlegen: `RequireAdminAsync`, `ExecuteUpdateAsync` mit Failed-Guard (`Duration == null && Attempts >= maxAttempts`), Set `DurationProbedAt = null`/`DurationProbeAttempts = 0`, Rückgabe `bool`, Logging | Offen | — |
| 3 | Logik | `MediaDurationProbeService.ResetFailedProbesAsync(userId)` anlegen: `RequireAdminAsync`, `ExecuteUpdateAsync` über die volle Failed-Menge inkl. `VideoExtensionPredicate()`, Rückgabe `int`, Logging | Offen | — |
| 4 | UI | `TVSenderAdmin.razor`: Aktionsspalte „Aktionen" mit `actions-cell` + `btn btn-sm`-Button „Probe zurücksetzen" je Zeile in `tvsender-probe-failed-detail`; Kappungszeile auf `colspan="4"` anpassen | Offen | — |
| 5 | UI | `TVSenderAdmin.razor`: Massen-Button „Alle zurücksetzen" im Failed-Detail-Block + `confirmProbeReset`-Flag + Bestätigungsdialog nach `admin-dialog-overlay`/`admin-dialog`-Muster | Offen | — |
| 6 | UI | `TVSenderAdmin.razor`: `probeStatus`-Feld + Anzeige `#tvsender-probe-action-status`, Handler `ResetProbeItemAsync`/`ResetAllFailedProbesAsync` mit `LoadProbeProgressAsync`-Refresh und Fehlerausgabe | Offen | — |
| 7 | Tests | `MediaDurationProbeServiceTests_Progress`: Assertion auf `MediaItemId` in `FailedItems`/`PendingItems` ergänzen | Offen | — |
| 8 | Tests | Neue Datei `MediaDurationProbeServiceTests_Reset.cs`: Einzel-Reset (Erfolg, Probed-Guard, Pending/unbekannt → `false`, Admin-Erzwingung), Massen-Reset (>25 Items jenseits der Kappung, Probed/Pending unberührt, Admin-Erzwingung), `ProbePendingAsync`-Wiederaufnahme | Offen | — |
| 9 | Tests | `TVSenderAdminTests` (bUnit): Zeilen-Reset-Button (vorhanden in Failed-, fehlend in Pending-Tabelle) mit Refresh/Status; Massen-Reset-Dialog (öffnen, abbrechen, bestätigen, Anzahl-Meldung) | Offen | — |
| 10 | E2E-Tests | `TVSenderAdminE2ETests.Admin_CreateEditDeactivateDeleteRule`: zweites Failed-Item seeden, Chip-Erwartungen anpassen, Einzel-Reset per Zeilen-Button und Massen-Reset inkl. Bestätigungsdialog durchspielen | Offen | — |
| 11 | Dokumentation | `docs/help/videolaufzeit.md` (Abschnitt „Fortschritt beobachten") und `docs/RELEASE_NOTES.md` ergänzen | Offen | — |
| 12 | Abschluss | `dotnet build VideoPlayer.sln` in Debug und Release fehlerfrei; volle Suite `dotnet test VideoWebPlayer.Tests/VideoWebPlayer.Tests.csproj` grün | Offen | — |
