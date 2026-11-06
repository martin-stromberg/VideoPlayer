# Abnahme Schritt 3: Sendeplan aus Regeln erzeugen

Prüfer: Devin (unabhängiger Lauf, nicht der Implementierer)
Branch: `task-issue-111-6145c51699f34953bf29a244634a23e2-automatisierter-tvsender-schritt-3-sendeplan-erzeugen` (HEAD `17ccc63`)

## Ergebnis

**Status:** Erfüllt

## Abweichungen

Keine.

## Hinweise

- **Downtime-Lücke in der Vergangenheit:** `coverageStart = max(now, lastEnd)` (`ProgramScheduleService.cs:63`). Nach Server-Ausfall bleibt die Periode `[lastEnd, now)` unbeplant — ab `now` ist der Plan wieder lückenlos. Sendungen können nicht rückwirkend ausgestrahlt werden, die Extend-only-Entscheidung ist im Handover dokumentiert und fachlich vertretbar. Für Schritt 5 (Programmauskunft) relevant nur bei historischen Abfragen in die Ausfallzeit.
- **Toter Code:** `ProgramScheduleService.TryEpisodeItem` (Zeile 516–529) wird nirgends aufgerufen — die Episode-Pfade nutzen `ToItem` mit `ProxyEpisode`. Harmlos, kann in einem Folgeschritt entfernt werden.
- **Harmlose Redundanz:** Der Overlap-Check in `MaterializeOccurrence` prüft `blocked` **und** `newSlots` getrennt, obwohl platzierte Ranges bereits via `blocked.AddRange(ranges)` enthalten sind; ebenso konkateniert `FillGaps` `newSlots` nochmals zu `blocked`. Fachlich korrekt, nur doppelte Arbeit.
- **Playwright-Flake (ehrlich berichtet):** Im Vollauf lief `PlaylistPlaybackE2ETests.PlaylistSkipLockedEntriesE2ETest` einmal in einen 30-s-Timeout (`#playlist-detail-selected-entry`). Der Test ist fachlich nicht Teil dieses Diffs (kein Playlist-/Playback-Code geändert); Einzelwiederholung lief grün. Entspricht der in `AGENTS.md` beschriebenen gelegentlichen Flake-Anfälligkeit unter Last.

## Durchgeführte Prüfungen

- **Diff:** 46 Dateien (+5756/−5). Kerndateien gelesen: `ProgramScheduleService` (komplett), `ProgramSlot`, `ProgramRuleCursor`, `ProgramCursorContentType`, `GermanHolidayCalendar`, `ProgramScheduleWorker`, `ProgramSettingsService`, beide EF-Konfigurationen, Migration `AddProgramSchedule`, `VideoWebPlayerBackupData`, `TVSenderAdmin.razor`, `ServiceCollectionExtensions`, alle neuen Testdateien, `docs/help/tv-sender.md`, `docs/RELEASE_NOTES.md`.
- **Build:** `dotnet build VideoPlayer.sln` Debug **und** Release — jeweils 0 Fehler (141 Warnungen, sämtlich pre-existing, v. a. BL0008 in Account-Pages).
- **Volle Testsuite:** `dotnet test VideoWebPlayer.Tests/VideoWebPlayer.Tests.csproj` synchron: 1850 erfolgreich, 1 übersprungen (Alt-Skip), 1 Fehler = der oben genannte Playwright-Flake; Einzelwiederholung grün → faktisch 1851 grün + 1 Skip wie erwartet.
- **Gegenprobe (Mutation):** `OrderByDescending(o => o.Rule.Priority)` → `OrderBy` invertiert → `ProgramScheduleServiceTests_Conflicts.EnsureCoverage_HigherPriorityWins_Overlap` schlug wie erwartet fehl (unterlegene Serie wurde eingeplant). Anschließend per `git checkout --` revertiert; Working Tree wieder sauber.

## Fachliche Kontrollen (aus dem Code verifiziert)

- **Lückenloser rollierender Plan:** `EnsureCoverageAsync` füllt `[coverageStart, now+Horizont]`; `FillGaps` kachelt Restlücken mit Füller-Material (nur passende Stücke; der letzte Füller am Horizont-Rand darf überschießen — dokumentierte Entscheidung). `AssertGapless`-Tests belegen lückenlose, überlappungsfreie Belegung; Zweitlauf verlängert ohne Duplikate (`EnsureCoverage_SecondRun_ExtendsNotDuplicates`).
- **Serien-Fortsetzung:** `ProgramRuleCursor` (LastContentType + LastContentId, Unique je Regel, echte FK-Kaskade zur Regel) persistiert den Fortschritt; `NextIndexFor` setzt pos+1 fort, Wrap-around über Modulo in `RuleState`. Episodenreihenfolge: `TVShowSeason.Id`, dann `Number` — konsistent mit `MediaHierarchyRegistry` (Season hat kein eigenes Nummernfeld). Getestet inkl. Wrap (`EnsureCoverage_SeriesExhausted_WrapsAround`) und Fortsetzung über Runs hinweg.
- **24-h-Lookback:** Vergangene Ausstrahlungen (bis 24 h) konsumieren den Cursor ohne Slot-Zeilen — bewusste, im Handover dokumentierte Entscheidung („gelaufen"); spannt ein Slot den coverageStart, behält er seinen echten Start (`EnsureCoverage_SpanningOccurrence_KeepsTrueStart`).
- **Priorität:** Ausstrahlungen werden absteigend nach `Priority`, dann Regel-Id, dann Start materialisiert; bei Kollision mit bereits belegtem Intervall wird die unterlegene Ausstrahlung **atomar** verworfen und ihr Cursor nicht fortgeschrieben. Durch Gegenprobe verifiziert abgesichert.
- **Nur Laufzeit-bekannte Inhalte:** `Duration != null`-Filter in `MovieMediaItemsAsync`/`EpisodeMediaItemsAsync`; Zählung über `SkippedNoDuration` (`EnsureCoverage_ItemWithoutDuration_IsSkipped`).
- **Nur vollständig freigegebene Inhalte:** `AccessContext` lässt Inhalte nur zu, wenn **jeder** Nicht-Admin-Benutzer sie sieht — Quellenzugriff für alle (`MediaSourceUsers` distinct-count == audienceCount) ODER Einzelfreischaltung der Serie/Filmsammlung für alle (`UnlockedMediaEntries`). Ohne Nicht-Admin-Benutzer ist alles freigegeben. Tests für beide Pfade vorhanden.
- **Verwaiste Materialien:** Alle fünf Expansions prüfen Existenz (`AnyAsync`/`FirstOrDefaultAsync`) → leere Liste → `OrphanedRules++` + Log, kein Abbruch. Verwaiste Playlist-Einträge werden still übersprungen. Test `EnsureCoverage_OrphanedMaterial_ProducesNothing`.
- **Feiertagskalender:** Anonymer gregorianischer Computus korrekt (Ostern 2026 = 05.04.); Oster-Offsets −2/+1/+39/+50/+60 stimmen; Landesfeiertage korrekt kodiert (Hl. Drei Könige BW/BY/ST, Frauentag BE/MV, Fronleichnam BW/BY/HE/NW/RP/SL, Mariä Himmelfahrt BY/SL, Weltkindertag TH, Reformationstag 9 Länder, Allerheiligen BW/BY/NW/RP/SL, Buß- und Bettag SN = Mittwoch vor dem 23.11.). Bundesland aus `Setup.HolidayFederalState`, `null` = nur bundesweit.
- **Worker:** Frischer DI-Scope je Lauf, `IBackgroundProcessingGate.EnterOperationAsync("Sendeplan")`-Lease, Intervall wird je Iteration frisch aus den Settings gelesen (Änderung ohne Neustart), Fehler werden geloggt und die Schleife läuft weiter; Registrierung via `AddHostedService` — unter „Testing" nicht ausgenommen (in Tests mit `initialDelay: TimeSpan.Zero` direkt instanziiert).
- **Datenhaltung/Backup:** Migration `AddProgramSchedule` (ProgramSlots + ProgramRuleCursors + zwei Setup-Spalten); `OptionalRestoreTables` enthält beide neuen Tabellen, `OptionalRestoreColumns` + `RestoreColumnDefaults` (48 h/15 min) die neuen Setup-Spalten; Alt-Backup-Regressionstest stellt Backup ohne neue Tabellen/Spalten wieder her.
- **Kanalfähig, ein Sender:** `ProgramSlot.TVChannelId` (FK + Kaskade, Index `(TVChannelId, Start)`); Service filtert Regeln/Slots/Cursor je Channel; Regel-/Material-Referenzen im Slot bewusst weich (kein FK) für Planhistorie — spiegelt `ProgramRule.MaterialId`.
- **Settings/UI/Doku:** `Setup.ProgramScheduleHorizonHours`/`ProgramScheduleIntervalMinutes` mit Defaults 48/15, Validierung ≥1; Admin-UI-Felder + E2E-Erweiterung; `docs/help/tv-sender.md` und `docs/RELEASE_NOTES.md` aktualisiert.
