# Plan-Review

## Ergebnis

**Status:** Offene Aufgaben vorhanden

## Umgesetzte Planelemente

### Neue Klassen

- [x] `ProgramClipGroup` (Datenmodellklasse, `VideoWebPlayer/Data/ProgramClipGroup.cs`) — angelegt: `Id`, `Name` (required, max 255, NOCASE), `IsSystem`, `CreatedAt`, `UpdatedAt`; Konstanten `TrailerGroupId = 1`, `TrailerGroupName = "Trailer"`, `NameMaxLength = 255`
- [x] `ProgramClipGroupItem` (Datenmodellklasse) — angelegt: `ProgramClipGroupId` + `MediaItemId` (Composite-PK), beide Navigationen
- [x] `ProgramRuleClipGroup` (Datenmodellklasse) — angelegt: `ProgramRuleId` + `ProgramClipGroupId` (Composite-PK), beide Navigationen (Navigation heißt `ClipGroup` statt `ProgramClipGroup` — dokumentierte Abweichung, fachlich korrekt: `ThenInclude(c => c.ClipGroup)`)
- [x] `ProgramClipGroupConfiguration` (EF-Konfiguration) — vorhanden: PK, `Name` required/NOCASE-`UseCollation` + Unique-Index, `IsSystem`-Default `false`
- [x] `ProgramClipGroupItemConfiguration` (EF-Konfiguration) — vorhanden: Composite-PK, beide FKs `DeleteBehavior.Cascade`
- [x] `ProgramRuleClipGroupConfiguration` (EF-Konfiguration) — vorhanden: Composite-PK, beide FKs `DeleteBehavior.Cascade`
- [x] `IProgramClipGroupService` (Interface, `VideoWebPlayer/Services/IProgramClipGroupService.cs`) — alle acht Signaturen vorhanden (`GetGroupsAsync`, `CreateGroupAsync`, `RenameGroupAsync`, `DeleteGroupAsync`, `GetGroupClipsAsync`, `GetClipCandidatesAsync`, `AddClipsToGroupAsync`, `RemoveClipFromGroupAsync`) plus Ergebnis-Records `ProgramClipGroupClip`/`ProgramClipGroupCandidate`
- [x] `ProgramClipGroupService` (Service) — implementiert mit `RequireAdminAsync`/`ValidateGroupNameAsync` (leer/≤255/Dublette/„Trailer"-Reservierung über beide Schreibpfade), `IsSystem`-Sperren, Clip-Prüfung (existiert/unklassifiziert/Video), `NotifyScheduleChanged` bei allen Mutationen
- [x] `ClipGroupState` (interner Hilfstyp, `ProgramScheduleService.cs:1106`) — vorhanden: `Group`, `Ring` (`StrandState`), `IsTrailer`, `StrandKey = $"clipgroup:{groupId}"`, Cursor-Initialisierung via `SetItems`/`SelectStrandCursor`

### Änderungen an bestehenden Klassen

- [x] `ProgramRule`: `PlaceholderGrace` (`TimeSpan`, NOT NULL, `ProgramRuleConfiguration` Default `DefaultPlaceholderGrace`), `ClipGroups` (`ICollection<ProgramRuleClipGroup>`), Konstante `DefaultPlaceholderGrace` = 15 min — vorhanden
- [x] `ApplicationDbContext`: DbSets `ProgramClipGroups`, `ProgramClipGroupItems`, `ProgramRuleClipGroups` — vorhanden (Zeilen 234/239/244)
- [x] `ProgramRuleInput`: `ClipGroupIds` (`IReadOnlyList<long>?`), `PlaceholderGrace` (`TimeSpan?`) als optionale Endparameter — vorhanden
- [x] `ProgramRuleService.ValidateClipGroupInputAsync` — vorhanden: `RuleType != Filler`, `SlotDuration != null`, Existenz aller Gruppen-Ids (`KeyNotFoundException`), `PlaceholderGrace > 0 && < SlotDuration`; in `ValidateRuleInputAsync` eingehängt
- [x] `ProgramRuleService.ReplaceClipGroupLinksAsync` — vorhanden (materialart-unabhängig, Aufruf in `CreateRuleAsync`/`UpdateRuleAsync`)
- [x] `ProgramRuleService.GetRulesAsync` — `Include(r => r.ClipGroups).ThenInclude(c => c.ClipGroup)` vorhanden; `PlaceholderGrace = input.PlaceholderGrace ?? DefaultPlaceholderGrace` in Create/Update
- [x] `ProgramScheduleService.ExpandClipGroupItemsAsync` — vorhanden: Video-Prüfung, Klassifizierungs-Filter (Movie-/Episode-Links), `Duration != null` mit `CountNoDuration`, `access.Allows(sourceId)` quellenübergreifend
- [x] `ProgramScheduleService.ExpandClipGroupStatesAsync` — vorhanden: verzögerte Nachexpansion der Trailer-Gruppen über `ExpandTrailersAsync`
- [x] `ProgramScheduleService.BuildClipGroupStates` — vorhanden: Gruppen-Reihenfolge `ProgramClipGroupId` aufsteigend, fehlende Trailer-Seed-Zeile toleriert (`link.ClipGroup is null → continue`)
- [x] `ProgramScheduleService.InsertClipFill` — vorhanden: `n+1`-Segmente inkl. Kopfsegment, gleichmäßige Restverteilung mit Carry, Round-Robin über Gruppen-Ringe (`NextClipItem`, keine Optimierungs-Suche), `FindLeadMatchOffset`-Semantik für Trailer-Positionen, Freiraum-/Bound-Wächter (`tailLimit` aus belegten Intervallen **und** höherprioren `BoundStarts`), Overshoot-Regel (erster Tail-Clip muss in `rest` passen, Überschuss danach bis `tailLimit` — dokumentierte Abweichung, fachlich korrekt), `blocked`-Nachpflege; kein eigenes `RemainingGapSeconds` (Doppelzählung mit `FillGaps` vermieden — dokumentierte Abweichung, fachlich korrekt)
- [x] `ProgramScheduleService.SelectItems`/`SelectAiringItems`/`SelectAiringItemsInterleaved` — `clipGrace`-Parameter, Abbruch `target - accumulated < clipGrace`; Übergabe nur bei Gruppen + `SlotDuration`
- [x] `ProgramScheduleService.EnsureCoverageAsync` — `Include(r => r.ClipGroups).ThenInclude(...)`, `BuildClipGroupStates`, `LoadMovieAiringsAsync`, `ExpandTrailerStatesAsync` + `ExpandClipGroupStatesAsync`, `InsertClipFill` vor `FillGaps`, `PersistCursors`
- [x] `ProgramScheduleService.ReconcileAsync` — gleiche Includes/Expansion (Trailer-Gruppe sofort mit persistiertem `airingsByMovie`), clip-bewusste `ReconcileWindow`-Validierung, `InsertClipFill` über `newSlots` nach dem Materialisierungs-Loop mit `BuildMovieAiringsFromSlotsAsync(kept ∪ new)`, `window.BoundStarts`
- [x] `AiringReproducible`/`FlatAiringTailReproducible`/`InterleavedAiringTailReproducible`/`ReconcileWindow` — Clip-zuerst-Klassifikation (`ClipMediaItemIds` ∈ Clip-Expansion), Clip-Slots auf Mitgliedschaft + lückenlose Verkettung geprüft, Titel-Teilfolge auf clip-freie Zeitachse projiziert (dokumentierte Abweichung, fachlich korrekt), `clipGrace` in den Tail-Checks
- [x] `PersistCursors` — schreibt Clip-Strang-Zeilen (`clipgroup:{groupId}`, `MediaItem`/`Trailer`); `RewindCursor` — Clip-Stränge mit berücksichtigt (clip-first-Klassifikation verhindert Fehlzuordnung von Trailer-Clips)
- [x] `TVSenderAdmin.razor`: Tab `tvsender-tab-clipgruppen` — vorhanden: Gruppen-Tabelle (Name, Clip-Anzahl, Aktionen; System-Zeile „Trailer" ohne Bearbeiten/Löschen, „Ansehen"), CRUD-Dialoge (`admin-dialog-overlay`), Fehleranzeige `#clipgroup-edit-error`
- [x] `TVSenderAdmin.razor`: Clip-Mitgliederverwaltung — Mitgliederliste mit „keine Laufzeit"-Kennzeichnung + Entfernen-Schaltfläche, Kandidaten-Auswahl (Suchfeld `#clip-candidate-search`, Quell-Filter `#clip-candidate-source`, Hinzufügen-Liste, keine Id-Eingabe), System-Gruppe schreibgeschützt mit Hinweistext
- [x] `TVSenderAdmin.razor` Regel-Dialog: `#rule-clip-groups`-Checkbox-Liste inkl. „Trailer" („(automatisch befüllt)") + `#rule-grace-minutes` (Minuten, Default 15) — nur bei `RuleType != Filler`, Hinweis bei fehlender `SlotDuration`
- [x] `RuleEditModel.SelectedClipGroupIds`/`GraceMinutes` (Default 15), `StartEdit`/`SaveRuleAsync`-Übertrag auf `ProgramRuleInput` (Filler-Guard), `MaterialLabel`-Suffix „— Clips: {Namen}" — vorhanden
- [x] `ServiceCollectionExtensions` — `services.AddScoped<IProgramClipGroupService, ProgramClipGroupService>()` (Zeile 278)
- [x] `VideoWebPlayerBackupData` — `OptionalRestoreTables` += 3 Tabellen, `OptionalRestoreColumns` += `ProgramRules.PlaceholderGrace`, neue `OptionalRestoreTimeSpanDefaults`-Liste + vierter Schleifen-Block in `AddOptionalRestoreDefaults`
- [x] Migration `AddProgramClipGroups` — drei Tabellen (Composite-PKs, FK-Kaskaden, NOCASE-Unique `Name`), `PlaceholderGrace` TEXT NOT NULL `DEFAULT '00:15:00'`, `migrationBuilder.InsertData` Seed `Id = 1, "Trailer", IsSystem = true`; Model-Snapshot aktualisiert
- [x] Doku: `docs/help/tv-sender.md` (Abschnitt „Clip-Gruppen mit Karenzzeit im Sendeplatz"), `README.md`, `docs/RELEASE_NOTES.md`

### Testinfrastruktur und Tests (umgesetzt)

- [x] `ProgramScheduleTestDb.CreateClipGroupAsync`/`AddClipToGroupAsync` — vorhanden
- [x] `ProgramScheduleTestDb.SeedTrailerClipGroupAsync` — vorhanden, Aufruf im Fixture-Setup `CreateAsync`
- [x] `ProgramScheduleTestDb.CreateRuleAsync` um `clipGroupIds`/`placeholderGrace` — vorhanden (optionale Endparameter, quellkompatibel)
- [x] `ProgramScheduleServiceTests_ClipGroups` — alle 19 geplanten Fälle vorhanden (`EnsureCoverage_GraceRemainder_FillsWithDistributedClips`, `…_RemainderAboveGrace_StartsNextTitle_Overshoots`, `…_RemainderExactlyGrace_StartsNextTitle`, `…_NoGroups_UnchangedOvershootBehavior`, `…_LastClip_MayOvershootPeriodEnd`, `…_ClipTooLargeForFreeRoom_LeavesDocumentedGap`, `…_ClipsSkipItemsWithoutDuration`, `…_ClipGroup_NotFullySharedSource_YieldsNoClipSlots`, `…_TwoClipGroups_RoundRobinDistributed`, `…_ClipOvershoot_ReservedBoundWindow_ClipDropped`, `…_ClipRing_RotatesAcrossRuns`, `…_TrailerGroup_AirsTrailerBeforeScheduledMovie`, `…_TrailerGroup_NoLeadMatch_OtherGroupsFill`, `…_TrailerClip_ExcludesItemsWithoutDuration`, `…_EpisodeCountAndSlotDuration_FillsTailWithClips`, `…_InterleavedRule_ClipFillDistributed`, `Reconcile_ClipFillSlots_StayScheduled`, `Reconcile_RemovedClipGroupMember_DisplacesAndReplans`, `Reconcile_WorkerLoop_WithClipFill_NoDisplacedAccumulation`)
- [x] `ProgramClipGroupTests` (echtes SQLite, Klasse in `ProgramClipGroupServiceTests.cs`) — Kaskaden (Regel→Links, Gruppe→Items+Links, MediaItem→Items), NOCASE-Unique `Name`, Composite-PK-Dublette — abweichende Testnamen, gleiche Abdeckung
- [x] `ProgramRuleServiceTests_ClipGroups` — Filler-/NoSlot-/UnknownGroup-Abweisung, Trailer-Gruppe zuweisbar, Grace-Bereich, Link-Roundtrip Create+Update, Default-Grace
- [x] `ProgramClipGroupServiceTests` — CRUD-Roundtrip, „Trailer"-Reservierung über **beide** Schreibpfade (Create + Rename), `IsSystem`-Sperren, Kandidatenmenge, klassifiziertes Item abgewiesen, NonAdmin-Verbot
- [x] `Guide_TrailerClipSlot_ShowsMovieDetails` (`ProgramGuideServiceTests_ClipGroups`) — Trailer-Clip-Slot mit `MovieId` zeigt Film-Details
- [x] `VideoWebPlayerBackupDataTests_ClipGroups` — Alt-Backup ohne neue Tabellen/Spalte restauriert mit Default 15 min; Roundtrip `ProgramClipGroups`
- [x] `TVSenderAdminTests_ClipGroups` (bUnit, 4 Tests) — Tab sichtbar + Trailer-Systemzeile geschützt, Gruppen-Anlage, „keine Laufzeit"-Kennzeichnung, Regel-Dialog zeigt `rule-clip-groups`/`rule-grace-minutes` + `MaterialLabel`-Suffix
- [x] E2E `TVSenderAdminClipGroupsE2ETests.ClipGroupsTab_CreateRenameDeleteGroup` + `ClipGroupsTab_TrailerGroupVisible_ButProtected` — Gruppen-CRUD, Trailer-Gruppe sichtbar/geschützt, Namensreservierung mit sichtbarem Fehler
- [x] Bestandsanpassungen — `TVSenderAdminE2ETests` (5 Tabs), `TVSenderAdminTests`-Kontext (DI + 5 Tabs), `ProgramRuleCursorNullStrandKeyMigrationTests` (Raw-SQL-Seed wegen neuer NOT-NULL-Spalte)

## Offene Aufgaben

- [ ] E2E-Testfall „Clips über die Kandidaten-Auswahl einer Gruppe zuordnen und entfernen" (`TVSenderAdminE2ETests`, Pflicht laut Plan/E2E-Tabelle) — fehlt vollständig: kein E2E-Test klickt „Hinzufügen"/„Entfernen" in der Mitgliederverwaltung; bUnit `MemberList_MarksItemWithoutDuration` deckt nur die Kennzeichnung ab
- [ ] E2E-Testfall „Regel mit Gruppen-Mehrfachauswahl + Karenzzeit anlegen/bearbeiten" — teilweise umgesetzt: `RuleEditor_ShowsClipGroupSelectionAndGraceField` prüft nur Feld-Anzeige, Auswahl und Default; kein Speichern einer Regel mit Gruppen-Auswahl über die UI und keine Sichtbarkeitsprüfung der Zuordnung in der Regel-Liste (MaterialLabel-Suffix ist nur per bUnit belegt)
- [ ] E2E-Testfall „Sendeplan zeigt Clip-Slots nach „Jetzt generieren" mit `MediaItem.Name`-Fallback" — teilweise umgesetzt: `GeneratedSchedule_ContainsVisibleClipSlots` ruft `EnsureCoverageAsync` direkt auf und prüft den Slot auf DB-Ebene; der UI-Pfad (Schaltfläche „Jetzt generieren" → sichtbare Slot-Karte mit Clip-`MediaItem.Name`-Fallback) wird nicht ausgeübt
- [ ] `MissingTrailerSeedRow_Tolerated` — teilweise umgesetzt: implementierter Test `MissingTrailerSeed_Tolerated` prüft nur `GetGroupsAsync` (leer) und `CreateGroupAsync`; die geplanten Teilbehauptungen „`CreateRuleAsync` mit `TrailerGroupId` → `KeyNotFoundException`" und „`BuildClipGroupStates` ohne Zeile → kein Gruppen-Zustand" fehlen (das Verhalten selbst ist im Code tolerant implementiert — nur der Test fehlt)
- [ ] E2E-Seed-Hilfen — teilweise umgesetzt: nur `SeedTrailerClipGroupAsync` existiert (privat in `TVSenderAdminClipGroupsE2ETests` — zulässig als „TV-Sender-spezifisch"); `SeedClipItemsAsync`, `SeedClipGroupAsync`/`SeedClipGroupItemAsync`, `SeedRuleClipGroupLinkAsync` fehlen — Seed erfolgt inline im Test `GeneratedSchedule_ContainsVisibleClipSlots`
- [ ] Verifikation (Schritt 13) — teilweise umgesetzt: Debug-Build `dotnet build VideoPlayer.sln` fehlerfrei und gefilterter Testlauf (49 Clip-Gruppen-Tests) grün im Review-Lauf verifiziert; Release-Build, volle Testsuite, `dotnet ef migrations has-pending-model-changes` und Sabotageproben ohne Repo-Nachweis (kein `test-results.md`)

## Hinweise

- Die Implementierung liegt vollständig als uncommittete Arbeitsänderungen auf dem Branch `task/issue-277-…` vor (kein Commit).
- Dokumentierte Abweichungen verifiziert und als umgesetzt bewertet: Navigation `ClipGroup` statt `ProgramClipGroup`; `InsertClipFill` ohne eigene `RemainingGapSeconds`-Buchung (Lücken fallen `FillGaps`/`GapSeconds` zu); Tail-Clip-Regel (erster Tail-Clip `pos + duration <= periodEnd`, danach bis `tailLimit`); Clip-Reproduktion über Projektion auf clip-freie Zeitachse.
- Zusätzliche, nicht geplante aber konsistente Abweichungen: `InsertClipFill`-Signatur ohne separaten `clipStatesByRule`-Parameter (Clip-Zustände hängen an `RuleState.ClipGroups`); `GetGroupsAsync`/Lesepfade sind ebenfalls Admin-erzwungen (plan-check Hinweis erlaubt das); Testnamen weichen vom Plan ab, Abdeckung ist gleichwertig.
- E2E-Klasse heißt `TVSenderAdminClipGroupsE2ETests` (separate Datei) statt neuer Fälle in `TVSenderAdminE2ETests` — trägt ebenfalls kein `Category=E2E`-Trait (bestehende Dokumentationslücke, im Plan als Bestand vermerkt).
- Vorhandenes `review-usability.md` meldet 3 UI-Befunde (100er-Kappung der Kandidatenliste ohne Hinweis, bereits zugeordnete Clips weiter „Hinzufügen"-aktiv, leere Mitgliedertabelle bei der Trailer-Gruppe) — für die Nacharbeit relevant, kein Plan-Verstoß.
- Reihenfolge-Empfehlung: Tasks 43–45 und 52/54 (Test-/Helper-Lücken) sowie Task 48 (Verifikationslauf inkl. Sabotageproben) vor dem Code-Review schließen.
