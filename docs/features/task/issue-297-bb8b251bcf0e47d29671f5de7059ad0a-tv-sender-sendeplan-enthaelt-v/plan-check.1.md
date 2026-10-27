# Plan-Gegenprüfung

Geprüft: `plan.md` gegen `requirement.md`, `inventory.md` (inkl. `inventory/logic.md`,
`enums.md`, `tests.md`) und den tatsächlichen Code in
`VideoWebPlayer/Services/ProgramScheduleService.cs` sowie den betroffenen Testklassen.

## Ergebnis

**Status:** Plan lückenhaft

## Abgleich Akzeptanzkriterien

| Akzeptanzkriterium | Umsetzung im Plan | Testnachweis im Plan | Status |
|--------------------|-------------------|----------------------|--------|
| 1. Annähernde Gleichverteilung: SQLite-Verteilungstest (`ProgramScheduleTestDb`) über Generierungsläufe, kein Alphabet-/Id-Bias | Zentrale deterministische Mischung beim Ringaufbau: `ItemOrderKey` (FNV-1a über `{ruleId}:{strandKey}:{contentType}:{contentId}`, Muster `StrandOrderKey` Z. 1362-1374) in `RuleState.SetItems` (Z. 1306-1341) und `ClipGroupState.SetItems` (Z. 1266-1267). Deckt alle ungeordneten Ringe ab: MovieCollection und `MediaSourceClips` über den Flachpfad, Genre-`other`-Strang über den Interleaved-Pfad, Nutzer-Clip-Gruppen über `ClipGroupState` (neuer `RuleId`-Parameter, durchgereicht in `BuildClipGroupStates` Z. 2635). Der einzige unabgedeckte ungeordnete Ring — der Genre-Füller-Flachring mit gemischtem Inhalt — ist eine vom Anwender entschiedene, dokumentierte Einschränkung. | `EnsureCoverage_UnorderedRing_HeadPicks_DistributeAcrossRules` (24 Regeln × 6 Filme, `MovieId`-Ebene, deterministisch — kein Flaky-Risiko) plus `EnsureCoverage_MovieCollectionRing_FirstPick_FollowsItemOrderKey`, `EnsureCoverage_ClipSourceRing_FirstPick_FollowsItemOrderKey`, `EnsureCoverage_ClipGroupRing_FirstPick_FollowsItemOrderKey`. | Abgedeckt |
| 2. Geordnete Reihenfolgen bleiben: Serienepisoden in Sendedreihenfolge; bestehende Interleaving-/Fortsetzungstests grün | `IsUnorderedRing` = `items.Count > 0 && items.All(i => i.ContentType is Movie or MediaItem)` — komplementär zu `PreservesOrder` (Z. 1223-1224: `TVShowEpisode or PlaylistEntry`). `show:{id}`-Stränge, `ExpandShowAsync`-Ringe und alle Playlist-Ringe bleiben damit strukturell ungemischt; Trailer-Ringe behalten die fachliche Airings-Ordnung aus `ExpandTrailersAsync` (Z. 2401-2404). Verifiziert: `SelectAiringItemsInterleaved` liest `strands[s].Items` (Z. 1756), `FillGaps`/`NextClipItem` ziehen `PeekAt(0)` (Z. 1950, 2764) — alles positionsbasiert über die jeweils konsistente Liste. | `EnsureCoverage_GenreRule_OtherStrand_MoviesMixed_ShowsKeepAirOrder` (Episoden-Sendedreihenfolge je `show:{id}`-Strang) und `EnsureCoverage_TrailerRing_KeepsAiringsOrder`. Bestehende `ProgramScheduleServiceTests_Interleaving`-Tests sind show-only und bleiben betroffensfrei. **Playlist-Ringe (`PlaylistEntry`, `Manual` und `ByReleaseDate`): kein Testnachweis — weder bestehend (kein Sendeplan-Test verwendet `ProgramRuleMaterialType.Playlist`) noch neu geplant.** | Lücke |
| 3. Reproduzierbarkeit: Mischung prozess- und laufinvariant; `NextIndexFor`-Cursor-Fortsetzung und Reconcile-/Reproduzierbarkeitsprüfung funktionieren unverändert | FNV-1a über stabile Schlüssel — keine `string.GetHashCode`/`Random`-Abhängigkeit. `EnsureCoverageAsync` und `ReconcileAsync` bauen Ringe über denselben `RuleState`-/`SetItems`-Pfad (Z. 115, 195) → identische Ordnung in Generation und Re-Simulation. Verifiziert: `FlatAiringTailReproducible` vergleicht `expected.Positions[k] == idx` über `state.Items` (Z. 632-714) — der Plan erzwingt korrekt, dass `Items` und `Strands[0]` im Flachpfad dieselbe gemischte Listeninstanz teilen; `InterleavedAiringTailReproducible` (Z. 782-816, 868-902) und `RewindRing` (Z. 988-1016) arbeiten positions-/inhaltsbasiert über `strands[s].Items` bzw. `ring.Items` und bleiben konsistent. Der Einmaleffekt auf Bestandspläne (erste `ReconcileAsync` nach Update kann verdrängen) ist benannt und für die Release Notes vorgesehen. | `ItemOrderKey_IsDeterministic_AndRuleDependent` (reiner Unit-Test via `InternalsVisibleTo`, csproj Z. 15), `EnsureCoverage_RingOrder_StableAcrossIndependentFixtures`, `Reconcile_MixedRing_StaysReproducible`. | Abgedeckt |

## Fehlende oder unvollständige Testanforderungen

- [ ] **Playlist-Ordnungserhalt ohne Testnachweis (Akzeptanzkriterium 2 / explizite Nicht-Anforderung).**
  Die Anforderung nennt „manuell sortierte Playlists" ausdrücklich als unangetastete fachliche
  Reihenfolge; der Plan übernimmt das korrekt als Design (`PlaylistEntry` → `IsUnorderedRing`
  falsch, `ExpandPlaylistAsync` Z. 2528-2549 bleibt geordnet), plant aber keinen Test dafür.
  Verifiziert: Kein Sendeplan-Test verwendet `ProgramRuleMaterialType.Playlist` — der Playlist-
  Pfad hat aktuell keinerlei Abdeckung im Sendeplan. Ein negativer Test nach dem Muster von
  `EnsureCoverage_GenreRule_OtherStrand_MoviesMixed_ShowsKeepAirOrder` (Playlist-Regel mit
  `SortMode.Manual`, Slots folgen `SortOrder`; optional `ByReleaseDate`) ist zu ergänzen. Er
  sichert auch strukturell ab, dass `IsUnorderedRing` und `PreservesOrder` komplementär bleiben
  — heute sind beide Prädikate nur per Konvention konsistent, ein künftiger geordneter
  ContentType oder ein Prädikatfehler würde Playlist-Ringe unbemerkt mischen.

## E2E-Abdeckung

| Benutzerfluss / Akzeptanzkriterium | Geplanter E2E-Test | Status |
|------------------------------------|--------------------|--------|
| Verteilung/Ringreihenfolge im Sendeplan | Keiner — Begründung im Plan: kein neuer oder geänderter UI-Fluss; die Änderung ist ausschließlich über den generierten Planinhalt beobachtbar und wird über SQLite-Integrationstests nachgewiesen. | Nicht erforderlich mit Begründung — plausibel und im Code verifiziert (kein UI-/API-Oberflächen-Bestandteil betroffen). |
| Bestehende senderbezogene E2E-Tests (`TVSenderAdminE2ETests`, `ProgramGuideE2ETests`, `LiveTvE2ETests`) | Keine Anpassung — assertieren Regel-CRUD und Anzeige, nicht die Ringreihenfolge. | Abgedeckt |

## Fehlende oder unvollständige Planbestandteile

- [ ] Testnachweis für Playlist-Ordnungserhalt (s. oben) — fehlt sowohl in „Neue Tests" als
  auch implizit in „Betroffene bestehende Tests", da kein Bestandstest existiert.

## Hinweise

- **Lookback-Nuance bei „erster Slot = Hash-Kopf":** `BuildOccurrences` erzeugt
  Occurrences ab `coverageStart - lookback` (Z. 1541); bei `Recurrence.Daily` konsumiert die
  gestrige Lookback-Occurrence den Ringkopf bereits ohne Slot (`end > coverageStart`-Filter in
  `MaterializeOccurrence`, Z. 1596). „Der erste Slot der Regel trägt das `ItemOrderKey`-Minimum"
  (`EnsureCoverage_MovieCollectionRing_FirstPick_...`, Verteilungstest) gilt daher nur, wenn die
  Testdaten so gewählt werden, dass keine Lookback-Occurrence vorab konsumiert (z. B.
  `Once`/`Weekly` außerhalb des Lookback-Fensters) — oder die Erwartung geht auf die Position
  hinter den Lookback-Konsum. Bei der Umsetzung zu beachten; der Mechanismus selbst ist davon
  unberührt.
- **Betroffene Bestandstests — Stichprobe verifiziert:** Die beiden im Plan genannten
  Boundary-Tests sind tatsächlich positionsabhängig (`EnsureCoverage_OversizedHeadItem_...`
  erwartet `End == 16:00` aus der Release-Reihenfolge `[Riesenfilm, Kurzfilm]`, Z. 145-164;
  `EnsureCoverage_HardBound_...` erwartet `Lang` um 19:00 und keinerlei Slots am Sonntag,
  Z. 243-262 — unter umgedrehter Hash-Ordnung würde `Kurz` am Sonntag durchaus passen). Ebenso
  `Reconcile_RemovedClipGroupMember_DisplacesAndReplans` (`ProgramScheduleServiceTests_ClipGroups`
  Z. 427-443): entfernt `clips[0]` — die Prämisse „dieser Clip war verplant" bleibt nur bestehen,
  weil 12 Clips/Tag den gesamten Ring verbrauchen; unter der „Allgemein"-Klausel abgedeckt, aber
  bei Seed-Änderungen zu verifizieren.
- **Prädikatspaar `IsUnorderedRing`/`PreservesOrder`:** komplementär, aber nicht verknüpft —
  `Trailer` und `Movie`/`MediaItem` und `TVShowEpisode`/`PlaylistEntry` decken den Enum nicht
  vollständig abseits der Konvention ab; ein künftiger ContentType landete ungemischt, ohne dass
  ein Test alarmierte. Für die Nachplanung erwägenswert: das Playlist-Testdefizit (oben) schließt
  die Lücke für den zweiten geordneten Typ.
- **Verifizierte Nebenbehauptungen des Plans:** `ClipGroupState` wird ausschließlich in
  `BuildClipGroupStates` (Z. 2635) konstruiert — der neue `ruleId`-Parameter reicht; die
  erneute `SetItems`-Ausführung für die Trailer-Gruppe in `ExpandClipGroupStatesAsync`
  (Z. 2661-2662) erfordert keine Zusatzanpassung, und `MixUnorderedRing` ist dort wegen
  `ContentType Trailer` ohnehin ein No-Op. `StrandOrderKey`-Extraktion nach `Fnv1aHash` ist
  wertidentisch machbar (Z. 1362-1374). `Items` wird im Interleaved-Pfad tatsächlich nur noch
  für Count-/Membership-Prüfungen gelesen (Z. 276, 1129, 1575) — die Entscheidung, dort die
  Expansionsliste zu belassen, ist korrekt.
