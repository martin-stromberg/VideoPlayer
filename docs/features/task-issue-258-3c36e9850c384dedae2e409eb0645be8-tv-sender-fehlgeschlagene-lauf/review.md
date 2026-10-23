# Plan-Review

## Ergebnis

**Status:** Vollständig umgesetzt

## Umgesetzte Planelemente

- [x] `MediaDurationProbeDetailItem` (Record) — um führenden Parameter `long MediaItemId` erweitert inkl. XML-Doc (`VideoWebPlayer/Services/MediaDurationProbeProgress.cs:53-70`); `MediaDurationProbeRecentItem` unverändert
- [x] `MediaDurationProbeService.ResetFailedProbeAsync(string userId, long mediaItemId, CancellationToken)` (public, `Task<bool>`) — `RequireAdminAsync` + `ExecuteUpdateAsync` mit Failed-Guard (`Id == mediaItemId && Duration == null && DurationProbeAttempts >= EffectiveMaxAttempts`), Set `DurationProbedAt = null`/`DurationProbeAttempts = 0`, Rückgabe `reset > 0`, `LogInformation` (`MediaDurationProbeService.cs:411-440`)
- [x] `MediaDurationProbeService.ResetFailedProbesAsync(string userId, CancellationToken)` (public, `Task<int>`) — `RequireAdminAsync` + `ExecuteUpdateAsync` über volle Failed-Menge inkl. `VideoExtensionPredicate()`, gleiche Sets, Rückgabe Zeilenzahl, `LogWarning` (`MediaDurationProbeService.cs:442-468`)
- [x] `DetailItemsAsync` (private Hilfsmethode) — projiziert `mi.Id` in den neuen `MediaItemId`-Parameter (`MediaDurationProbeService.cs:213-224`); `ProbePendingAsync` und `RequireAdminAsync` unverändert
- [x] Aktionsspalte „Aktionen" + `<td class="actions-cell">` mit `btn btn-sm tvsender-probe-reset` („Probe zurücksetzen") je Zeile in `tvsender-probe-failed-detail`; Kappungszeile auf `colspan="4"` angehoben (`TVSenderAdmin.razor:578, 589-591, 596`)
- [x] Massen-Button „Alle zurücksetzen" `#tvsender-probe-reset-all` (`btn btn-sm btn-danger`) im `tvsender-probe-failed-detail`-Block, setzt `confirmProbeReset` (`TVSenderAdmin.razor:601`)
- [x] Bestätigungsdialog `admin-dialog-overlay`/`admin-dialog` mit `@onclick:stopPropagation`, erklärendem Text mit `FailedCount`, `#probe-reset-all-confirm` und „Abbrechen"; Overlay-Klick schließt (`TVSenderAdmin.razor:676-690`)
- [x] Neuer Zustand `bool confirmProbeReset` + `string? probeStatus` (`TVSenderAdmin.razor:729-730`) und Anzeige `#tvsender-probe-action-status` als `form-text` am Ende der Probe-Karte (`TVSenderAdmin.razor:636-639`)
- [x] Handler `ResetProbeItemAsync(MediaDurationProbeDetailItem)` und `ResetAllFailedProbesAsync()` — Service-Aufruf mit `userId`, Statusmeldungen in `probeStatus` (Erfolg/„nicht (mehr) fehlgeschlagen"/Anzahl/„keine vorhanden"), `LoadProbeProgressAsync`-Refresh, Fehler → `probeStatus = ex.Message` (`TVSenderAdmin.razor:1470-1501`)
- [x] `tvsender-probe-pending-detail` und `tvsender-probe-recent` unverändert; `MediaDurationProbeWorker` unverändert (kein Eintrag in `git status`)
- [x] Test `GetProgressAsync_DetailItems_CarryMediaItemId` in `MediaDurationProbeServiceTests_Progress` (`MediaDurationProbeServiceTests_Progress.cs:179-195`)
- [x] Neue Testdatei `MediaDurationProbeServiceTests_Reset.cs` mit allen 8 geplanten Tests gegen echtes SQLite (`ProgramScheduleTestDb`): `ResetFailedProbeAsync_FailedItem_ClearsProbeStateAndBecomesPending`, `ResetFailedProbeAsync_ProbedItem_ReturnsFalseAndKeepsDuration`, `ResetFailedProbeAsync_PendingOrUnknownItem_ReturnsFalse`, `ResetFailedProbeAsync_NonAdmin_Throws`, `ResetFailedProbesAsync_ResetsAllFailedItemsBeyondDetailCap`, `ResetFailedProbesAsync_LeavesProbedAndPendingUntouched`, `ResetFailedProbesAsync_NonAdmin_Throws`, `ProbePendingAsync_PicksUpResetItem`
- [x] bUnit-Tests `ProbeFailedRow_ResetButton_ResetsItemAndRefreshes` (Button nur in Failed-Tabelle, Refresh, `#tvsender-probe-action-status`) und `ProbeResetAll_ConfirmDialog_ResetsAllAndShowsStatus` (öffnen/abbrechen/bestätigen/Anzahl) in `TVSenderAdminTests.cs:353-427`
- [x] E2E-Erweiterung `TVSenderAdminE2ETests.Admin_CreateEditDeactivateDeleteRule`: zweites Failed-Item `e2e-defekt-2.mkv` geseedet, Chip „2 fehlgeschlagen", Einzel-Reset per `.tvsender-probe-reset` in der `e2e-defekt.mkv`-Zeile (→ „1 fehlgeschlagen", „2 ausstehend", Statusmeldung), Massen-Reset über `#tvsender-probe-reset-all` + `#probe-reset-all-confirm` (→ „0 fehlgeschlagen", „1 Datei(en)") (`TVSenderAdminE2ETests.cs:101-188`)
- [x] `docs/help/videolaufzeit.md` — Abschnitt „Fortschritt beobachten" um Einzel-/Massen-Reset inkl. Wirkung und erneutem Scheitern ergänzt (`videolaufzeit.md:17`)
- [x] `docs/RELEASE_NOTES.md` — What's-New-Eintrag für die Reset-Aktionen (`RELEASE_NOTES.md:27`)

## Hinweise

- `dotnet build VideoPlayer.sln` wurde im Review in Debug **und** Release ausgeführt — beide fehlerfrei (0 Warnungen, 0 Fehler). Der volle Testlauf (`dotnet test`) wurde nicht erneut ausgeführt; er steht als eigener Lifecycle-Schritt (Tests ausführen) noch aus — Tasks-Zeile 12 daher „Offen" gelassen, obwohl alle Planelemente vorhanden sind. Bei grünem Testlauf dort auf „Erledigt" setzen.
- Der einzelne Zurücksetzen-Button trägt nur `btn btn-sm` ohne Farbklasse (`btn-primary` o. ä.) — der Plan schreibt bewusst nur `btn btn-sm` vor, daher kein Mangel, aber für das Usability-Review erwähnenswert.
- `ResetProbeItemAsync`/`ResetAllFailedProbesAsync` übergeben `CancellationToken.None` statt eines Circuit-/Dispose-Tokens — plankonform (der Plan nennt kein Token-Handling), ebenfalls nur als Beobachtung.
- Alle Änderungen liegen noch uncommittet im Arbeitsverzeichnis (`git status`: 9 modifizierte Dateien + die neue Testdatei untracked); der Planungscommit `b8dfc15` enthält nur `plan.md`.
