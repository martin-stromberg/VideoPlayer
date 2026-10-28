# Tests — Bestandsaufnahme

## Testklassen

### `TVSenderAdminTests` (bUnit)
Datei: `VideoWebPlayer.Tests/Components/TVSenderAdminTests.cs`

| Testmethode | Was wird getestet? |
|-------------|--------------------|
| `RendersAllTabs_WithRulesActive` | Fünf Tabs inkl. Texte, `#tab-regeln` initial aktiv, `#tvsender-new-rule` und Kopf-Chips vorhanden |
| `RuleEditor_RendersAsOverlayDialog` | `#tvsender-new-rule` öffnet `#rule-editor` als `.admin-dialog` in `.admin-dialog-overlay`; Overlay-Klick und „Schließen" beenden die Bearbeitung |
| `RuleEditor_TrailerCheckbox_OnlyForFillerRuleType` | `#rule-material-trailer` nur bei Regelart `Filler` |
| `RuleEditor_ClipSourceSelect_OnlyForFillerRuleType_AndSetsMaterial` | `#rule-material-clipsource` nur bei `Filler`; Auswahl befüllt `#rule-material-selected` mit „Clip-Quelle: …" |
| `SwitchesTabs_AndShowsProbeDetailTables` | Tab-Wechsel + Probe-Detailtabellen (kein Regel-Bezug) |
| `Sendeplan_RendersProgramCards_WithBadgesPosterAndProgress` | `.program-card.tvsender-slot-row` mit Badges/Poster/Fortschritt (Sendeplan-Karten, Vorlage) |
| `Sendeplan_GroupsSlotsByDay_AndMarksRuleChanges` | `.tvsender-day-header` und `.tvsender-rule-divider` — prüft `data-rule-id` **am Divider** (Z. 349), nicht an der Regelzeile |
| `ProbeFailedRow_ResetButton_ResetsItemAndRefreshes` | Kein Regel-Bezug |

### `TVSenderAdminTests_AgeWindow` (bUnit)
Datei: `VideoWebPlayer.Tests/Components/TVSenderAdminTests_AgeWindow.cs`

| Testmethode | Was wird getestet? |
|-------------|--------------------|
| `RuleDiagnosis_ShowsAgeWindowReason` (Z. 99) | Seeded Film-Regel → `.tvsender-rule-diagnosis` enthält „…nicht altersgerecht im Sendefenster" bzw. „kein altersgerechtes Material im Fenster" (Z. 116–120) — **betroffen vom Umbau** |
| `AgeWindows_RenderFieldsAndSave` / `AgeWindows_InvalidInput_ShowsError` / `AgeWindows_InvalidTimeInput_NamesFieldAndSavesNothing` | Einstellungen-Tab, kein Regel-Bezug |

### `TVSenderAdminTests_ClipGroups` (bUnit)
Datei: `VideoWebPlayer.Tests/Components/TVSenderAdminTests_ClipGroups.cs`

| Testmethode | Was wird getestet? |
|-------------|--------------------|
| `RuleEditor_ShowsClipGroupSelection_GraceField_AndMaterialLabelSuffix` (Z. 167) | Prüft `Assert.Contains("Clips: Maskottchen", cut.Markup)` — das `ClipGroupSuffix` von `MaterialLabel` **im gerenderten Seiten-Markup** (Z. 181), danach Editor-Felder (`#rule-clip-groups`, `#rule-grace-minutes(-hint)`) — **betroffen vom Umbau** |
| `ClipGroupsTab_*`, `MemberList_*`, `CandidateList_*`, `CandidateSearch_*` | Clip-Gruppen-Tab (`TVSenderAdminClipGroups`), kein Regel-Bezug |

### `TVSenderAdminTests_ClipBlocks` (bUnit)
Datei: `VideoWebPlayer.Tests/Components/TVSenderAdminTests_ClipBlocks.cs` — Sendeplan-Clip-Blöcke
(`BuildScheduleDisplayItems`-Rendering), kein Regel-Übersichts-Bezug.

### `TVSenderAdminE2ETests` (Playwright)
Datei: `VideoWebPlayer.Tests/TVSenderAdminE2ETests.cs`

| Testmethode | Was wird getestet? |
|-------------|--------------------|
| `Admin_CreateEditDeactivateDeleteRule` (Z. 84) | Voller Regel-Lebenszyklus über die Oberfläche: Regel anlegen → Zeile `.tvsender-rule-row` (HasText „Abendserie") mit „Serien-Sendezeit" + „Serie: E2E Serie"; Edit über `.tvsender-rule-edit`; Diagnose `.tvsender-rule-diagnosis` enthält „ohne Mediendatei"; Toggle über `.tvsender-rule-toggle` → Zeile enthält „Inaktiv"; Löschen über `.tvsender-rule-delete` + `#rule-delete-confirm` → `.tvsender-rule-row` Count 0 + `#tvsender-rules-status` „gelöscht"; zweite Regel („Funki-Füller") analog — **alle Regel-Hooks betroffen** |
| `Admin_GenreRule_MultiSelectAndSourceFilter` (Z. 355) | Genre-Regel mit zwei Genres + Quellfilter; Zeile `.tvsender-rule-row` muss „Genre: E2E Action, E2E Komödie" und „Quellen: {sourceName}" enthalten (Z. 405–406) — **prüft die Listenform von `MaterialLabel` in der Übersicht** |
| `NonAdmin_SeesNotAuthorized` (Z. 429) | „Nicht autorisiert", kein `#tvsender-new-rule` |
| `Admin_ProbeReset_*`, `Admin_StandbyClip_*`, `Admin_ContentScope_*`, `TVSenderAdmin_RepeatGenerate_*` | Kein Regel-Übersichts-Bezug (Sendeplan/Einstellungen/Laufzeit) |

### `TVSenderAdminClipGroupsE2ETests` (Playwright)
Datei: `VideoWebPlayer.Tests/TVSenderAdminClipGroupsE2ETests.cs`

| Testmethode | Was wird getestet? |
|-------------|--------------------|
| `RuleEditor_ShowsClipGroupSelectionAndGraceField` (Z. 162) | Speichert Regel mit zwei Clip-Gruppen; Zeile `.tvsender-rule-row` (HasText „Primetime-Clips") muss „Clips: Maskottchen" und „Trailer" enthalten (Z. 217–220) — **betroffen** |

### `TVSenderAdminE2ETests_AgeWindow` (Playwright)
Datei: `VideoWebPlayer.Tests/TVSenderAdminE2ETests_AgeWindow.cs`

| Testmethode | Was wird getestet? |
|-------------|--------------------|
| `Admin_RuleDiagnosis_ShowsAgeWindowReason` (Z. 83) | `.tvsender-rule-diagnosis` wartet und enthält „altersgerecht" (Z. 93–94) — **betroffen** |

### Service-Tests (kein Markup-Bezug)
`VideoWebPlayer.Tests/ProgramScheduleServiceTests_Diagnosis.cs` — prüft `GetRuleDiagnosesAsync`
/`ProgramRuleDiagnosis` auf Service-Ebene (10 Tests, u. a. `Diagnosis_SchedulableMovie_CountsSchedulable`);
`ProgramRuleServiceTests_*` decken CRUD/Validierung/Genre-Material ab. Alle vom UI-Umbau
unberührt.

## Verwendete Test-Hooks (Selektoren) auf der Regel-Übersicht

| Hook | Element im Markup | Verwendet von |
|------|-------------------|---------------|
| `.tvsender-rule-row` | `<tr>` je Regel (Z. 147, mit `data-rule-id="@rule.Id"`) | `TVSenderAdminE2ETests` (Z. 196, 206, 221, 227, 254, 272, 402), `TVSenderAdminClipGroupsE2ETests` (Z. 217) |
| `.tvsender-rule-edit` | Bearbeiten-Button (Z. 160) | `TVSenderAdminE2ETests` (Z. 202, 419) |
| `.tvsender-rule-toggle` | (De)aktivieren-Button (Z. 161) | `TVSenderAdminE2ETests` (Z. 220) |
| `.tvsender-rule-delete` | Löschen-Button (Z. 162) | `TVSenderAdminE2ETests` (Z. 224, 269) |
| `.tvsender-rule-diagnosis` | `<td>` der Planbar-Spalte (Z. 154) | `TVSenderAdminE2ETests` (Z. 210), `TVSenderAdminE2ETests_AgeWindow` (Z. 93), `TVSenderAdminTests_AgeWindow` (Z. 116–118) |
| `#tvsender-new-rule` | „Neue Regel"-Button (Z. 116) | bUnit + E2E mehrfach |
| `#rule-delete-confirm` | Bestätigungsbutton im Löschdialog (Z. 967) | `TVSenderAdminE2ETests` (Z. 225–226, 270–271) |
| `#tvsender-rules-status` | Statuszeile (Z. 172) | `TVSenderAdminE2ETests` (Z. 228, 425) |
| `data-rule-id` | Attribut auf `.tvsender-rule-row` (Z. 147) — **wird aktuell von keinem Test an der Regelzeile gelesen** (die einzige `data-rule-id`-Assertion, `TVSenderAdminTests.cs` Z. 349, betrifft `.tvsender-rule-divider` im Sendeplan) | — |
| Texte „Aktiv"/„Inaktiv" | Status-Chip (Z. 157) | `TVSenderAdminE2ETests` (Z. 221) |

## Hilfsmethoden

### `ProgramScheduleTestDb` (bUnit-Fixture, echtes SQLite)
Datei: `VideoWebPlayer.Tests/Helpers/ProgramScheduleTestDb.cs`

- `CreateAsync(dbName, ct)` (Z. 27) — Fixture mit SQLite-DB anlegen.
- `CreateRuleAsync(channelId, name, type, recurrence, timeOfDay, materialType, materialId, priority, episodeCount, slotDuration, dayOfWeek, singleDate, isActive, genreIds, mediaSourceIds, clipGroupIds, placeholderGrace)` (Z. 263–311) — Regel samt Join-Zeilen (`ProgramRuleGenres`, `ProgramRuleMediaSources`, `ProgramRuleClipGroups`) direkt seeden.
- `GetChannelAsync` (Z. 313) — liefert/legt den Kanal an.
- `CreateClipGroupAsync` (Z. 64), `SeedTrailerClipGroupAsync` (Z. 52), `AddClipToGroupAsync` (Z. 73).
- `CreateGenreAsync` (Z. 232), `LinkMovieGenreAsync` (Z. 249), `LinkShowGenreAsync` (Z. 256).
- `CreateMovieAsync` (Z. 180), `CreateShowAsync` (Z. 206), `CreateMediaItemAsync` (Z. 140), `SeedProbeItemAsync` (Z. 163), `EnsureSourceAsync` (Z. 121), `AddUserAsync` (Z. 113), `CreateSlotAsync` (Z. 87), `SaveDiagAsync` (Z. 317).

### `TVSenderAdminTests.CreateContextAsync` (bUnit-DI-Aufbau)
Datei: `VideoWebPlayer.Tests/Components/TVSenderAdminTests.cs` (Z. 31–70)

Baut den `BunitContext` mit echten Services (`ProgramRuleService`, `ProgramScheduleService`,
`ProgramSettingsService`, `MediaDurationProbeService`, `ProgramClipGroupService`,
`ProgramScheduleSignal`), Test-Doubles (`FakeFfmpegLocator`, `FakeFfmpegToolDownloadService`,
`PresentLocator`, `EmptyListHandler`-Client) und Admin-Claims (`IsAdmin`, `NameIdentifier`).
Wird von allen `TVSenderAdminTests_*`-Klassen wiederverwendet.

### `TVSenderAdminE2ETestBase` (Playwright-Basisklasse)
Datei: `VideoWebPlayer.Tests/Helpers/TVSenderAdminE2ETestBase.cs`

- `OpenAdminPageAsAdminAsync(readyTabSelector)` (Z. 27) — Admin-Rechte, Quell-Zugriff, Login, Seite öffnen.
- `SwitchTabAsync(tabButtonSelector, paneSelector)` (Z. 195) — robuster Tab-Klick (Blazor-Circuit-Handover).
- `EnableTvSenderAsync` (Z. 51) / `UpdateSetupAsync(mutate)` (Z. 40) — Setup-Seeds.
- `SeedSharedCollectionAsync` (Z. 62), `SeedMovieWithItemAsync` (Z. 99), `SeedMovieRuleAsync` (Z. 141), `SeedClipItemsAsync` (Z. 172) — Testdaten.
