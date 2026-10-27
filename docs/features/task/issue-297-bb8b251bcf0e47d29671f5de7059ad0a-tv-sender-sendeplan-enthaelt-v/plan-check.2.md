# Plan-Gegenprüfung

Zweite Prüfrunde. Geprüft: `plan.md` gegen `requirement.md`, `inventory.md` (inkl.
`inventory/logic.md`, `enums.md`, `tests.md`) und den tatsächlichen Code in
`VideoWebPlayer/Services/ProgramScheduleService.cs` sowie sämtlichen sendeplanbezogenen
Testklassen. Runde 1 ist als `plan-check.1.md` archiviert.

## Ergebnis

**Status:** Plan lückenhaft

Die Runde-1-Lücke (fehlender Test für Playlist-Ordnungserhalt) ist geschlossen, die beiden
Runde-1-Hinweise sind eingearbeitet (Lookback-Hinweis, Z. 210-218;
`Reconcile_RemovedClipGroupMember_DisplacesAndReplans` in der Betroffenen-Tabelle, Z. 228).
Die Nachbesserung selbst hat keine neue fachliche Lücke erzeugt — die Playlist-Testansätze
sind im Code verifiziert (s. Hinweise). Verblieben ist eine unvollständige Stelle in der
Aufzählung betroffener Bestandstests (s. unten).

## Abgleich Akzeptanzkriterien

| Akzeptanzkriterium | Umsetzung im Plan | Testnachweis im Plan | Status |
|--------------------|-------------------|----------------------|--------|
| 1. Annähernde Gleichverteilung: SQLite-Verteilungstest (`ProgramScheduleTestDb`) über Generierungsläufe, kein Alphabet-/Id-Bias | Zentrale deterministische Mischung beim Ringaufbau: `ItemOrderKey` (FNV-1a über `{ruleId}:{strandKey}:{contentType}:{contentId}`, Muster `StrandOrderKey` Z. 1362-1374) in `RuleState.SetItems` (Z. 1306-1341) und `ClipGroupState.SetItems` (Z. 1266-1267). Deckung aller ungeordneten Ringe verifiziert: `MovieCollection` und `MediaSourceClips` über den Flachpfad, Genre-`other`-Strang über den Interleaved-Pfad (`MaterialType == Genre && RuleType != Filler && order.Count > 1`, Z. 1310-1337 — auch ein einzelner `other`-Strang ohne Serien landet im Flachpfad und wird dort gemischt), Nutzer-Clip-Gruppen über `ClipGroupState` (neuer `RuleId`-Parameter, durchgereicht in `BuildClipGroupStates` Z. 2635), `MediaSourceClips`-/`MovieCollection`-Füller über denselben Flachpfad (gezogen am Ringkopf via `FillGaps`/`PeekAt(0)`, Z. 1950). Einzige ungemischte ungeordnete Ringe — Genre-Füller-Flachring (gemischter Inhalt) und Trailer-Ringe (fachliche Vorlauf-Ordnung, `ExpandTrailersAsync` Z. 2401-2404) — sind vom Anwender entschiedene, dokumentierte Einschränkungen. | `EnsureCoverage_UnorderedRing_HeadPicks_DistributeAcrossRules` (24 Regeln × 6 Filme, `MovieId`-Ebene, deterministisch — kein Flaky-Risiko; deckt die Anwender-Entscheidung „Verteilung über Regeln" ab) plus `EnsureCoverage_MovieCollectionRing_FirstPick_FollowsItemOrderKey`, `EnsureCoverage_ClipSourceRing_FirstPick_FollowsItemOrderKey`, `EnsureCoverage_ClipGroupRing_FirstPick_FollowsItemOrderKey`. | Abgedeckt |
| 2. Geordnete Reihenfolgen bleiben: Serienepisoden in Sendedreihenfolge; bestehende Interleaving-/Fortsetzungstests grün | `IsUnorderedRing` = `items.Count > 0 && items.All(i => i.ContentType is Movie or MediaItem)` — komplementär zu `PreservesOrder` (Z. 1223-1224: `TVShowEpisode or PlaylistEntry`). `show:{id}`-Stränge, `ExpandShowAsync`-Ringe und alle Playlist-Ringe bleiben strukturell ungemischt: `ExpandPlaylistAsync` erzeugt ausschließlich `PlaylistEntry`-Items (Z. 2573, 2579) — auch für gemischte Playlists. | `EnsureCoverage_GenreRule_OtherStrand_MoviesMixed_ShowsKeepAirOrder` (Episoden-Sendedreihenfolge je `show:{id}`-Strang), `EnsureCoverage_TrailerRing_KeepsAiringsOrder`, und **neu** die beiden Negativtests `EnsureCoverage_PlaylistRing_ManualSortOrder_IsNotMixed` und `EnsureCoverage_PlaylistRing_ByReleaseDate_KeepsReleaseOrder` — die Runde-1-Lücke ist geschlossen. Machbarkeit verifiziert: `SortMode.Manual → OrderBy(SortOrder).ThenBy(AddedAt)` (Z. 2530), `ByReleaseDate → ReleaseDate ?? PremieredAt` via `MediaHierarchyRegistry` (Z. 2544-2548, Registry Z. 78); die Fixture hat keinen Playlist-Helfer (im Plan korrekt benannt, `CreatePlaylistAsync` optional ergänzen; `Playlist.UserId` ist Pflichtfeld — `AddUserAsync` vorhanden). Bestehende `ProgramScheduleServiceTests_Interleaving`-Tests sind show-only (verifiziert) und bleiben betroffensfrei. | Abgedeckt |
| 3. Reproduzierbarkeit: Mischung prozess- und laufinvariant; `NextIndexFor`-Cursor-Fortsetzung und Reconcile-/Reproduzierbarkeitsprüfung funktionieren unverändert | FNV-1a über stabile Schlüssel — keine `string.GetHashCode`/`Random`-Abhängigkeit (Z. 1362-1374). Generation und Re-Simulation bauen Ringe über denselben `SetItems`-Pfad → identische Ordnung. Verifiziert: `SelectItems` liest flach über `state.Items`/`state.NextIndex` (Z. 1672) und `FlatAiringTailReproducible` vergleicht `expected.Positions[k] == idx` über `state.Items` (Z. 632-714) — die Plan-Forderung, dass `Items` und `Strands[0]` im Flachpfad dieselbe gemischte Listeninstanz teilen, ist notwendig und korrekt. `SelectAiringItemsInterleaved` liest `strands[s].Items` (Z. 1756); `NextIndexFor` ankert per `FindIndex` an beliebiger Position (Z. 1529-1536). Einmaleffekt auf Bestandspläne benannt und für Release Notes vorgesehen. | `ItemOrderKey_IsDeterministic_AndRuleDependent` (reiner Unit-Test via `InternalsVisibleTo`, `VideoWebPlayer.csproj` Z. 15), `EnsureCoverage_RingOrder_StableAcrossIndependentFixtures`, `Reconcile_MixedRing_StaysReproducible`. | Abgedeckt |

## Fehlende oder unvollständige Testanforderungen

Keine — alle Akzeptanzkriterien haben einen konkreten Testnachweis; die Runde-1-Lücke beim
Playlist-Ordnungserhalt ist geschlossen.

## E2E-Abdeckung

| Benutzerfluss / Akzeptanzkriterium | Geplanter E2E-Test | Status |
|------------------------------------|--------------------|--------|
| Verteilung/Ringreihenfolge im Sendeplan | Keiner — Begründung im Plan: kein neuer oder geänderter UI-Fluss; die Änderung ist ausschließlich über den generierten Planinhalt beobachtbar und wird über SQLite-Integrationstests nachgewiesen. | Nicht erforderlich mit Begründung — plausibel und im Code verifiziert. |
| Bestehende senderbezogene E2E-Tests (`TVSenderAdminE2ETests`, `ProgramGuideE2ETests`, `LiveTvE2ETests`, `TVSenderAdminClipGroupsE2ETests`) | Keine Anpassung. Stichprobe verifiziert: `TVSenderAdminClipGroupsE2ETests` (Z. 243-283) seedet genau einen Clip (`clipIds[0]`) — die Assertion `s.MediaItemId == clipIds[0]` ist ordnungsunabhängig; `TVSenderAdminE2ETests` assertiert `MovieId` eines Einzelfilms. | Abgedeckt |

## Fehlende oder unvollständige Planbestandteile

- [ ] **`ProgramScheduleServiceTests_ClipFillPeriodBounds` fehlt in „Betroffene bestehende
  Tests"** (Datei `VideoWebPlayer.Tests/ProgramScheduleServiceTests_ClipFillPeriodBounds.cs`,
  3 Tests). Die Klasse nutzt einen `MovieCollection`-Ring aus vier Filmen mit den Dauern
  105/106/105/106 min (Z. 34-35) und ist nachweislich ordnungsabhängig: Die
  Grace-Verzweigung `t - accumulated < grace` (`SelectAiringItems`, Z. 1844) entscheidet an
  der Paarsumme der gezogenen Titel, ob ein dritter Titel startet oder die Restzeit in die
  Clip-Füllung geht. Unter der bisherigen sequenziellen Ordnung sind alle Vierer-Paare
  gleich lang (211 min) — der Seed-Kommentar begründet das explizit über den Ringverbrauch
  (Z. 31-33: „die Lookback-Occurrence des Vortags verbraucht denselben Ring, also müssen
  alle Vierer-Paare gleich lang sein"). Unter der Hash-Permutation kann ein {105,105}-Paar
  an gezogener Position entstehen: Rest genau 30 → `30 < 30` falsch → ein dritter Titel
  startet und überschießt das Periodenende → keine Clip-Restzeit. Damit ist
  `Assert.NotEmpty(clipSlots)` in `EnsureCoverage_FullPrimetimeScenario_ClipsDistributedInsidePeriod`
  (Z. 127, Grace 30) konkret gefährdet; in `EnsureCoverage_OccupiedInsidePeriod_ClipFillDoesNotOverlap`
  (Grace 30) kann der überschießende dritte Titel mit dem reservierten 00:00-00:30-Fenster
  kollidieren und die gesamte Ausstrahlung verwerfen. `EnsureCoverage_StartedMidPeriod_NoClipsAfterNominalPeriodEnd`
  (Grace 15) ist dagegen robust (Clip-Assertions sind vakuum-wahr). Aufnahme in die
  Betroffenen-Tabelle mit der üblichen Maßgabe: Erwartung über `ItemOrderKey` verifizieren,
  Seed ggf. auf wirklich gleichlange Dauern ausrichten — keine Assertions abschwächen.

## Hinweise

- **Verifizierte Robustheit der explizit gelisteten Bestandstests:** Die vier genannten
  `_AgeWindow`-Tests dokumentieren in Kommentaren konkrete Ringpositionen („blocked title
  sits at ring position 0", Z. 40; „FSK-16 title heads the ring", Z. 344; „middle position
  is FSK-18-blocked", Z. 408, 464). Ihre Assertions sind ratingbasiert und dürften unter
  jeder Permutation grün bleiben — geprüfte Szenarien: `SkipsBlockedAndOffersItAgainAtNight`
  und `MixedRatings_Fsk16AirsOnlyFrom22` (2er-Ring: gesperrte Position wird stets
  konsumiert-übersprungen, zulässige Position ausgestrahlt), `Reconcile_DisplacedUnorderedRing_...`
  (3er-Ring mit einer gesperrten Position). Die Plan-Anweisung „Prämisse prüfen, Seed ggf.
  über `ItemOrderKey` ausrichten, Kommentare aktualisieren" trifft den Punkt genau.
- **`Reconcile_RemovedClipGroupMember_DisplacesAndReplans`** (Z. 427-443) ist korrekt
  gelistet: 12 Clips/Tag aus einem 12er-Ring verbrauchen jede Position, `clips[0]` bleibt
  unter jeder Ordnung verplant — die Assertion `SlotsDisplaced > 0` ist robust, die
  Verifikation gehört trotzdem in die Liste.
- **Stichproben weiterer Kandidaten — ordnungsunabhängig, korrekt nicht gelistet:**
  `EnsureCoverage_SlotDuration_FillsUntilTarget` (3×60 min, nur Count-/Dauer-Assertions),
  `EnsureCoverage_ClipRing_RotatesAcrossRuns` (`firstDayClips[0] != secondDayClips[0]`
  gilt unter jeder Permutation), `EnsureCoverage_TwoClipGroups_RoundRobinDistributed`
  (A/B-Alternanz über HashSets), `EnsureCoverage_ClipSource_CursorContinuesRingAcrossRuns`
  (2er-Ring alterniert zwangsläufig — gelistet, wird voraussichtlich grün bleiben),
  `_Genres`-Klasse (Mengen-Assertions via `ToHashSet`/`Contains`), `_CursorSelection`
  (relationale Assertion `updatedWinner.LastContentId == slots[^1].MediaItemId`),
  `ProgramGuideServiceTests_ClipGroups` (Slots direkt geseedet, kein Ringaufbau).
- **Mischschlüssel-Nuance (keine Lücke, Implementierungshinweis):** Eine Genre-Regel mit
  genau einem Strang (`order.Count == 1`, z. B. nur Filme ohne Serien) läuft über den
  Flachpfad und wird mit `strandKey = null` gemischt; dieselbe Filmmenge im `other`-Strang
  einer mehrsträngigen Expansion wird mit `strandKey = "other"` gemischt. Identischer
  Materialsatz kann so je nach Strangzahl eine andere Ringordnung erhalten — für den
  Cursor-Anker und die Re-Simulation innerhalb eines Laufs unschädlich (Expansion und
  Re-Check treffen dieselbe Verzweigung), aber bei der Formulierung der
  `ItemOrderKey`-Erwartungen in Genre-Tests zu beachten.
- **Playlist-Tests:** Der `Manual`-Test sollte die Erwartung an `ItemOrderKey` der
  `PlaylistEntry`-ContentIds (=`PlaylistEntry.Id`, nicht `Movie.Id`) ausrichten — das steht
  so im Plan („ItemOrderKey-Hash-Ordnung der Entry-Ids", korrekt). `PlaylistEntry.MediaType`
  muss `MediaTypeValues.Movie` tragen (spielbar, `IsPlayable`-Filter Z. 2523); die
  `ByReleaseDate`-Ordnung steuert der Test über `Movie.ReleaseDate ?? PremieredAt`.
- **Lookback-Hinweis verifiziert:** `BuildOccurrences` startet bei
  `coverageStartUtc - lookback` (Z. 1541, 1564); `MaterializeOccurrence` ruft `CommitTaken`
  auch dann, wenn alle Slot-Intervalle vor `coverageStart` enden und `ranges` leer bleibt
  (Z. 1596, 1627) — die Lookback-Occurrence konsumiert Ringpositionen ohne Slots, exakt wie
  der Plan-Hinweis beschreibt.
