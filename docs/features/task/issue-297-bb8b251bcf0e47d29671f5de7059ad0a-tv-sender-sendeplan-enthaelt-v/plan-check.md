# Plan-Gegenprüfung

Dritte Prüfrunde. Geprüft: `plan.md` gegen `requirement.md`, `inventory.md` (inkl.
`inventory/logic.md`, `tests.md`) und den tatsächlichen Code in
`VideoWebPlayer/Services/ProgramScheduleService.cs` sowie sämtlichen sendeplanbezogenen
Testklassen (jede Klasse aus `inventory/tests.md` einzeln gegen den Code verifiziert).
Runden 1 und 2 sind als `plan-check.1.md` bzw. `plan-check.2.md` archiviert.

## Ergebnis

**Status:** Plan vollständig

Die Runde-2-Lücke (`ProgramScheduleServiceTests_ClipFillPeriodBounds` fehlte in
„Betroffene bestehende Tests") ist sauber geschlossen: Die neue Tabellenzeile (plan.md
Z. 237) nennt alle drei Tests der Klasse, analysiert den Gefährdungsmechanismus korrekt
und gibt eine permutationsinvariante Seed-Korrektur samt Gegenwarnung vor. Die
Paarsummen-Logik ist im Code verifiziert (Details unter „Hinweise"). Die Nachbesserung
erzeugt keine neue Lücke; die Runde-2-Einarbeitungen (Strand-Key-Nuance `null` vs.
`"other"`, Z. 43-50 und Z. 209; Playlist-Test-Details `PlaylistEntry.Id` als ContentId,
`MediaTypeValues.Movie` für den `IsPlayable`-Filter, `Playlist.UserId` als Pflichtfeld,
Z. 210-211; `_AgeWindow`-Kommentar-Anpassung, Z. 234) sind im Code verifiziert.

## Abgleich Akzeptanzkriterien

| Akzeptanzkriterium | Umsetzung im Plan | Testnachweis im Plan | Status |
|--------------------|-------------------|----------------------|--------|
| 1. Annähernde Gleichverteilung: SQLite-Verteilungstest (`ProgramScheduleTestDb`) über Generierungsläufe, kein Alphabet-/Id-Bias | Zentrale deterministische Mischung beim Ringaufbau: `ItemOrderKey` (FNV-1a über `{ruleId}:{strandKey}:{contentType}:{contentId}`, Muster `StrandOrderKey` Z. 1362-1374) in `RuleState.SetItems` (Z. 1306-1341) und `ClipGroupState.SetItems` (Z. 1266-1267). Deckung **aller** ungeordneten Ringe verifiziert: `MovieCollection` (Z. 2230-2231), `MediaSourceClips`/`FilterClipCandidatesAsync` (Z. 2710) und einsträngige Genre-Expansionen über den Flachpfad (`StrandState(null, …)`, Z. 1340), der Genre-`other`-Strang über den Interleaved-Pfad (`MaterialType == Genre && RuleType != Filler && order.Count > 1`, Z. 1310-1337; `OtherStrandKey = "other"` Z. 1205/2346), Nutzer-Clip-Gruppen über `ClipGroupState` mit neuem `RuleId`-Ctor-Parameter (`BuildClipGroupStates` Z. 2635; `ExpandClipGroupStatesAsync` Z. 2653-2664 benötigt keine weitere Anpassung, da die Regel-Id im Zustand liegt — verifiziert). Einzige ungemischte ungeordnete Ringe — Genre-Füller-Flachring (gemischter Inhalt) und `Trailer`-Ringe (fachliche Vorlauf-Ordnung, `ExpandTrailersAsync` Z. 2401-2404, `FindLeadMatchOffset` Z. 2426-2440) — sind vom Anwender entschiedene, dokumentierte Einschränkungen. | `EnsureCoverage_UnorderedRing_HeadPicks_DistributeAcrossRules` (24 Regeln × 6 Filme, `ProgramSlot.MovieId`-Ebene — entspricht der Anwender-Entscheidung „Verteilung über Regeln"; vollständig deterministisch, daher kein Flaky-Risiko und stabile Grenzen) plus `EnsureCoverage_MovieCollectionRing_FirstPick_FollowsItemOrderKey`, `EnsureCoverage_ClipSourceRing_FirstPick_FollowsItemOrderKey`, `EnsureCoverage_ClipGroupRing_FirstPick_FollowsItemOrderKey`. | Abgedeckt |
| 2. Geordnete Reihenfolgen bleiben: Serienepisoden in Sendedreihenfolge; bestehende Interleaving-/Fortsetzungstests grün | `IsUnorderedRing` = `items.Count > 0 && items.All(i => i.ContentType is Movie or MediaItem)` — komplementär zu `PreservesOrder` (Z. 1223-1224: `TVShowEpisode or PlaylistEntry`). Verifiziert: `ExpandPlaylistAsync` erzeugt ausschließlich `PlaylistEntry`-Items (Z. 2573, 2579 — auch für gemischte Playlists), `ExpandShowAsync`- und `show:{id}`-Stränge tragen `TVShowEpisode`; beide bleiben strukturell ungemischt. `Trailer` ist weder `PreservesOrder` noch `IsUnorderedRing` — behält die Expansionsordnung, wie geplant. | `EnsureCoverage_GenreRule_OtherStrand_MoviesMixed_ShowsKeepAirOrder` (Episoden-Sendedreihenfolge je `show:{id}`-Strang + `ItemOrderKey(rule.Id, "other", …)`-Erwartung — Strand-Key-Nuance korrekt berücksichtigt), `EnsureCoverage_TrailerRing_KeepsAiringsOrder` und die beiden Negativtests `EnsureCoverage_PlaylistRing_ManualSortOrder_IsNotMixed` / `EnsureCoverage_PlaylistRing_ByReleaseDate_KeepsReleaseOrder`. Machbarkeit verifiziert: `SortMode.Manual → OrderBy(SortOrder).ThenBy(AddedAt)` (Z. 2530), `ByReleaseDate → releaseDates` via `MediaHierarchyRegistry` (Z. 2534-2548), `IsPlayable`-Filter Z. 2523 (`PlaylistEntryMediaTypeResolver` akzeptiert `MediaTypeValues.Movie`), `ContentId = entry.Id` (Z. 2573/2579). Fixture: `CreatePlaylistAsync` fehlt tatsächlich (im Plan als optionalen Zusatz benannt), `Playlist.UserId` ist `required` (`Playlist.cs` Z. 38-40), `AddUserAsync` vorhanden (`ProgramScheduleTestDb` Z. 113). Bestehende `_Interleaving`-Tests sind show-only (verifiziert) und bleiben betroffensfrei. | Abgedeckt |
| 3. Reproduzierbarkeit: Mischung prozess- und laufinvariant; `NextIndexFor`-Cursor-Fortsetzung und Reconcile-/Reproduzierbarkeitsprüfung funktionieren unverändert | FNV-1a über stabile Schlüssel, kein `string.GetHashCode`/`Random` (Z. 1362-1374). Generation und Re-Simulation teilen denselben `SetItems`-Aufbau → identische Ordnung je Expansion. Verifiziert: Flachpfad liest positionsbasiert über `state.Items`/`state.NextIndex` (`SelectItems` Z. 1672, `FlatAiringTailReproducible` `expected.Positions[k] == idx` Z. 714) — die Plan-Forderung, dass `Items` und `Strands[0]` dieselbe gemischte Listeninstanz teilen, ist notwendig und korrekt; Interleaved-Pfad und Repro-Check lesen `strands[s].Items` (Z. 1756, 782); `NextIndexFor` ankert per `FindIndex` an beliebiger Position (Z. 1529-1536); `RewindRing`/`RewindCursor` arbeiten positions-/inhaltsbasiert (Z. 967-1016); `PersistCursors` schreibt `LastContentType`/`LastContentId` — keine Positionsabhängigkeit im Datenmodell. Einmaleffekt auf Bestandspläne benannt und für Release Notes vorgesehen. | `ItemOrderKey_IsDeterministic_AndRuleDependent` (reiner Unit-Test via `InternalsVisibleTo`, `VideoWebPlayer.csproj` Z. 15 — verifiziert), `EnsureCoverage_RingOrder_StableAcrossIndependentFixtures` (Laufinvarianz), `Reconcile_MixedRing_StaysReproducible` (`AssertNoDisplacedAsync` vorhanden, `ProgramScheduleTestDb` Z. 338). | Abgedeckt |

## Fehlende oder unvollständige Testanforderungen

Keine — alle Akzeptanzkriterien haben konkrete Testnachweise; die Runde-1-Lücke
(Playlist-Ordnungserhalt) und die Runde-2-Lücke (`_ClipFillPeriodBounds`-Zeile) sind
geschlossen. Die vollständige Durchprüfung aller sendeplanbezogenen Testklassen ergab:
Die in „Betroffene bestehende Tests" gelisteten Tests sind exakt die positionsabhängigen
(s. Hinweise); alle übrigen Klassen sind ordnungsunabhängig.

## E2E-Abdeckung

| Benutzerfluss / Akzeptanzkriterium | Geplanter E2E-Test | Status |
|------------------------------------|--------------------|--------|
| Verteilung/Ringreihenfolge im Sendeplan | Keiner — Begründung im Plan: kein neuer oder geänderter UI-Fluss; die Änderung ist ausschließlich über den generierten Planinhalt beobachtbar und wird über SQLite-Integrationstests nachgewiesen. | Nicht erforderlich mit Begründung — plausibel und im Code verifiziert. |
| Bestehende senderbezogene E2E-Tests (`TVSenderAdminE2ETests`, `ProgramGuideE2ETests`, `LiveTvE2ETests`, `TVSenderAdminClipGroupsE2ETests`) | Keine Anpassung — assertieren Regel-CRUD, MaterialType-Persistenz und Anzeige; die stichprobenartig geprüften Assertions (`clipIds[0]`-Einzelclip, Einzelfilm-`MovieId`) sind ordnungsunabhängig. | Abgedeckt |

## Fehlende oder unvollständige Planbestandteile

Keine.

## Hinweise

- **Runde-2-Lücke — Verifikation der Paarsummen-Logik (korrekt):** Der Seed verwendet
  105/106/105/106 min (`ProgramScheduleServiceTests_ClipFillPeriodBounds.cs` Z. 34-35,
  Kommentar Z. 31-33) bei `slotDuration` 240 min und Grace 30. Die Grace-Verzweigung
  `t - accumulated < grace` steht in `SelectAiringItems` Z. 1844 (Interleaved-Pendant
  Z. 1747) — die zitierte Zeilennummer stimmt exakt. Unter der bisherigen sequenziellen
  Ordnung sind beide gezogenen Paare (0,1) und (2,3) je 211 min → Rest 29 < 30 →
  Clip-Füllung. Von den 6 Permutationen der Multimenge {105,105,106,106} erzeugen 2 ein
  {105,105}-Paar (Rest exakt 30 → `30 < 30` falsch → dritter Titel startet und
  überschießt): konkret gefährdet, wie der Plan schreibt — `Assert.NotEmpty(clipSlots)`
  (Z. 127) scheitert dann, im Occupied-Test kollidiert der Überschuss zusätzlich mit dem
  reservierten 00:00-00:30-Fenster (atomarer Drop Z. 1604-1608).
- **Fix-Vorschlag korrekt, eine Präzisierung:** „vier Dauern gleichlang auf ≥ 106 min"
  ist permutationsinvariant richtig (jede Paarsumme 2·d ≥ 212 → Rest ≤ 28 < 30) und die
  Warnung vor 4×105 (Rest 30 = Grace, striktes `<`) ist exakt. Ergänzende Obergrenze für
  die Umsetzung: Bei d ≥ 120 min erreicht die Paarsumme das 240-min-Ziel —
  `accumulated >= target` (Z. 1862) beendet die Ausstrahlung vor der Grace-Prüfung, es
  bleibt kein Rest für Clips und `Assert.NotEmpty(clipSlots)` scheitert ebenfalls. Der
  sichere Band ist also 106–119 min; das konkret genannte Beispiel 4×106 liegt darin.
  `…_StartedMidPeriod_…` (Grace 15) ist korrekt als robust eingestuft: Rest 28–30 > 15
  zieht unter jeder Permutation einen dritten Titel → Clip-Assertions vakuum-wahr.
- **Betroffene Bestandstests — Vollständigkeit eigenständig verifiziert:** Alle Klassen
  aus `inventory/tests.md` durchsucht. Positionsabhängig sind exakt die gelisteten:
  `_Boundary.EnsureCoverage_OversizedHeadItem_AirsAndOvershootsSuccessor` (erwartet
  `End == 16:00` aus der Release-Reihenfolge `[Riesenfilm, Kurzfilm]`, Z. 145-164 —
  unter umgedrehter Hash-Ordnung endet der große Film 15:00 und der Test scheitert) und
  `_Boundary.EnsureCoverage_HardBound_ItemCannotComplete_DefersToNextOccurrence`
  (45-Minuten-Kopf-Prämisse, Z. 243-262 — unter umgedrehter Ordnung sendet der 2-Min-Film
  am Sonntag vor der harten Grenze), die vier `_AgeWindow`-Tests (Kommentare mit konkreten
  Ringpositionen Z. 40, 344, 408, 464 — Assertions ratingbasiert robust, Prämissen-
  Verifikation korrekt gefordert), `_ClipSource.EnsureCoverage_ClipSource_CursorContinuesRingAcrossRuns`
  (2er-Ring alterniert zwangsläufig — bleibt grün), `_ClipGroups.Reconcile_RemovedClipGroupMember_DisplacesAndReplans`
  (12 Entnahmen aus 12er-Ring verbrauchen jede Position — `clips[0]` bleibt verplant,
  Assertion robust), `_ClipFillPeriodBounds` (s. oben) und die Allgemein-Klausel.
- **Ordnungsunabhängig und korrekt nicht gelistet (Stichproben + Sichtprüfung):**
  `_Genres` komplett (Mengen-Assertions via `ToHashSet`/`Contains`/`Distinct`, Z. 50-52,
  102-104, 130-132), `_Generation` (nur Einzelfilm-Ringe bzw. `SlotDuration_FillsUntilTarget`
  mit 3×60 min — Z. 221-235), `_Interleaving` komplett (show-only; `StrandOrderKey`-Tests
  bleiben stabil, da die `Fnv1aHash`-Extraktion identische Werte liefert), `_Filler`,
  `_Reconcile` (Einzelfilme/Serien), `_Trailer` (Einzelfilme + Trailer-Ringe ungemischt),
  `_Conflicts`, `_Access`, `_Diagnosis` (Zähl-Assertions), `_ContentScope`,
  `_CursorSelection` (relationale Assertion `updatedWinner.LastContentId == slots[^1].MediaItemId`,
  Z. 97), `ProgramRuleCursorUniqueness*`, `ProgramScheduleWorkerTests`, `ProgramScheduleSignalTests`,
  `ProgramGuideServiceTests(_ClipGroups)` (Slots direkt geseedet), `ProgramRuleServiceTests_*`,
  `ProgramClipGroup*Tests`, `Components/TVSenderAdminTests*`.
- **Zwei `_AgeWindow`-Tests mit „OtherStrand" im Namen sind bewusst nicht gelistet und
  das ist korrekt:** `EnsureCoverage_Interleaved_OrderedStrandDefers_OtherStrandContinues`
  (Z. 147) enthält nur `show:`-Stränge (kein `other`-Ring), und
  `EnsureCoverage_Interleaved_UnorderedOtherStrand_SkipsBlockedMovie` (Z. 189) hat einen
  einelementigen `other`-Ring — Permutation trivial. Ebenso ordnungsunabhängig:
  `Diagnosis_CountsWindowBlockedAtRuleEvaluationTime` (Z. 304-331, Collection mit 2
  Filmen, aber reine Zähl-Assertion `AgeWindowBlockedCount`, keine Positionsannahme).
- **Verteilungstest-Tauglichkeit:** Der Test aggregiert über 24 Regeln (variierte
  `ruleId` im Hash) auf Titel-Ebene (`MovieId`) — entspricht der Anwender-Entscheidung
  und dem Mechanismus. Deterministisch, einmalige Grenzverifikation ausreichend;
  der „jeder Film mind. 1× Ringkopf"-Teil der Erwartung ist nicht strukturell garantiert
  und muss wie geplant an der konkreten Hash-Ausprägung verifiziert werden.
- **Strand-Key-Nuance verifiziert:** Flachpfad baut `StrandState(null, …)` (Z. 1340),
  Interleaved-`other`-Strang trägt `StrandKey = "other"` (Z. 1205, 1316) — identische
  Filmmenge erhält je nach Strangzahl unterschiedliche Seeds (`null` vs. `"other"`); der
  Plan dokumentiert das korrekt und weist die Genre-Test-Erwartung auf `"other"` hin.
- **Lookback-Hinweis verifiziert:** `BuildOccurrences` startet bei
  `coverageStartUtc - lookback` (Z. 1541, 1564); `MaterializeOccurrence` ruft
  `CommitTaken` auch bei leerem `ranges` auf (Z. 1596 Filter, Z. 1627 Commit) — der
  Lookback-Konsum ohne Slots ist real, und die Vorgabe (Recurrence `Once`/`Weekly`
  außerhalb des Lookback-Fensters oder verschobene Erwartung) ist korrekt.
- **Release Notes:** Der Einmaleffekt auf Bestandspläne ist in „Seiteneffekte" mit dem
  Vermerk „in den Release Notes zu nennen" erfasst; bei der Umsetzung in Schritt 6
  (`docs/RELEASE_NOTES.md`) mit abhaken — kein Mangel, da benannt.
