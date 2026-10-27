# Umsetzungsplan: Gleichverteilte Materialauswahl im Sendeplan (Issue #297)

## Übersicht

Die ungeordneten `SchedulableItem`-Ringe des `ProgramScheduleService` erhalten beim
Ringaufbau eine deterministische, prozess- und laufinvariante Mischung (FNV-1a-Hash über
`Rule.Id` + Strang- und Inhaltsschlüssel, Muster `StrandOrderKey`), sodass der systematische
Alphabet-/Id-/Releasedate-Bias beim Start am Ringkopf entfällt. Geordnete Stränge
(`PreservesOrder`: Serienepisoden, Playlists) und die fachlich geordneten Trailer-Ringe
bleiben unverändert; Datenmodell, Konfiguration und UI sind nicht betroffen. Nachgewiesen
wird die annähernde Gleichverteilung durch einen neuen SQLite-Verteilungstest über mehrere
Generierungsregeln/-läufe (Fixture `ProgramScheduleTestDb`, echtes SQLite).

## Designentscheidungen

| Komponente / Bereich | Gewählter Ansatz | Begründung |
|----------------------|------------------|------------|
| Angriffspunkt der Mischung | Zentral beim Ringaufbau in `RuleState.SetItems` und `ClipGroupState.SetItems`, nicht in den Expand-Methoden | Ein einziger Durchsetzungspunkt für alle ungeordneten Ringe — einschließlich künftiger — statt vierfach duplizierter Sortierlogik in `ExpandMovieCollectionAsync`/`ExpandGenreAsync`/`FilterClipCandidatesAsync`/`ExpandClipGroupItemsAsync`. Die `OrderBy`-Klauseln der Expansionen bleiben als stabile Eingangsordnung, für Dedup-Auswahl (`GroupBy` in `ExpandTrailersAsync`) und Diagnosen erhalten. Die vorhandene geordnet/ungeordnet-Grenze (`StrandState.PreservesOrder`) liegt genau an dieser Stelle. |
| Mischschlüssel | Neuer `internal static int ItemOrderKey(long ruleId, string? strandKey, ProgramCursorContentType contentType, long contentId)` — FNV-1a über `"{ruleId}:{strandKey}:{contentType}:{contentId}"`; der FNV-1a-Kern wird aus `StrandOrderKey` in `Fnv1aHash` herausgelöst und geteilt | Gleiches Muster wie `StrandOrderKey`: prozess- und laufinvariant (kein `string.GetHashCode`/`Random` — Voraussetzung für `ReproContext`/Reconcile), aber regel- und strangabhängig, sodass sich die Ringköpfe über Regeln verteilen (genau das beseitigt den senderweiten „A"-Bias). `internal` erlaubt Direkttests (`InternalsVisibleTo` ist vorhanden, Muster `StrandOrderKey_IsDeterministic_AndNotAlphabetic`). Sortierung `OrderBy(ItemOrderKey…).ThenBy(ContentId)` ergibt eine totale, kollisionssichere Ordnung. |
| Welche Ringe gemischt werden | Nur Ringe, deren sämtliche Items `ContentType` `Movie` oder `MediaItem` tragen (Prädikat `IsUnorderedRing`) | Komplementär zu `PreservesOrder`: Stränge mit `TVShowEpisode` oder `PlaylistEntry` (Serienfenster, `show:{id}`-Stränge, Playlists — auch `PlaylistSortMode.Manual`/`ByReleaseDate`, gemäß Anforderungsannahme) behalten ihre fachliche Reihenfolge. Der Genre-Füller-Flachring (gemischter Film-/Episoden-Inhalt) bleibt in Expansionsordnung — dort hätte eine Vollmischung die Episoden-Sendedreihenfolge zerstört. `Trailer`-Ringe behalten die Airings-Ordnung aus `ExpandTrailersAsync`, weil diese fachlich geordnet ist (Vorlauf-Relevanz: `FindLeadMatchOffset` bevorzugt den frühesten anstehenden Film und findet Kopf-Treffer typischerweise bei Offset 0). |
| Bezugsgröße und Stichprobe des Verteilungstests | Titel-Ebene (`ProgramSlot.MovieId`); Test über R = 24 Regeln auf N = 6 Filme derselben Collection | Entspricht der Kundenbeobachtung (Titel-Ebene, nicht MediaItem-Ebene) und dem Mechanismus: `ruleId` im Mischschlüssel variiert den Ringkopf je Regel. Die Auswahl ist vollständig deterministisch — die Toleranzgrenzen werden einmal verifiziert und bleiben danach stabil (kein Flaky-Risiko). |

## Programmabläufe

### Ringaufbau mit deterministischer Mischung

1. Die Expansionen (`ExpandMaterialAsync` bzw. `ExpandMovieCollectionAsync`, `ExpandGenreAsync`,
   `ExpandPlaylistAsync`, `ExpandShowAsync`, `ExpandTrailersAsync`,
   `ExpandMediaSourceClipsAsync`/`ExpandClipGroupItemsAsync` via `FilterClipCandidatesAsync`)
   liefern `SchedulableItem`-Listen unverändert in ihrer bisherigen, fachlichen Ordnung.
2. `RuleState.SetItems` baut die Ringe:
   - **Interleaved-Pfad** (`MaterialType.Genre` und `RuleType != Filler`, >1 Strang): die
     Stranggruppierung und die `StrandOrderKey`-Mischung der Strang-Reihenfolge bleiben
     unverändert. Neu: jede Strang-Itemliste wird vor dem `StrandState`-Bau durch
     `MixUnorderedRing(Rule.Id, key, items)` ersetzt — greift nur im `other`-Strang
     (reine `Movie`-Items); `show:{id}`-Stränge bleiben geordnet. `Items` bleibt die
     Expansionsliste (dort nur Mitgliedschaftsprüfung, Z. ~276).
   - **Flachpfad** (alle übrigen Materialien inkl. Genre-Füller): `Items` und der einzige
     `StrandState(null, …)` werden aus derselben gemischten Listeninstanz
     `MixUnorderedRing(Rule.Id, null, items)` aufgebaut — zwingend, weil
     `SelectAiringItems` und `FlatAiringTailReproducible` positionsbasiert über
     `state.Items` lesen.
   - **Strand-Key-Nuance:** Eine Genre-Regel mit genau einem Strang (`order.Count == 1`,
     z. B. nur Filme ohne Serien) läuft über den Flachpfad und wird mit
     `strandKey = null` gemischt; dieselbe Filmmenge als `other`-Strang einer
     mehrsträngigen Expansion wird mit `strandKey = "other"` gemischt. Identischer
     Materialsatz kann so je nach Strangzahl eine andere Ringordnung erhalten — für
     Cursor-Anker und Re-Simulation innerhalb eines Laufs unschädlich (Expansion und
     Reproduzierbarkeits-Check treffen dieselbe Verzweigung), aber bei der Formulierung
     der `ItemOrderKey`-Erwartungen in Genre-Tests zu beachten.
3. `ClipGroupState.SetItems` mischt den `clipgroup:{id}`-Ring vor `NextIndexFor` über
   `MixUnorderedRing(RuleId, StrandKey, items)`. Die Regel-Id wird `ClipGroupState` als
   neuer Konstruktor-Parameter mitgegeben und von `BuildClipGroupStates` aus `rule.Id`
   durchgereicht, damit Regeln mit derselben Clip-Gruppe unterschiedliche Ringordnungen
   erhalten. Der Trailer-Clip-Ring bleibt ungemischt (`ContentType Trailer` →
   `IsUnorderedRing` falsch).
4. `NextIndexFor` bleibt unverändert: der Cursor-Anker (`LastContentType` +
   `LastContentId`) wird per `FindIndex` an beliebiger Ringposition gefunden; ohne Cursor
   oder ohne Treffer startet der Lauf bei Index 0 — jetzt dem Hash-Kopf statt des
   Sortier-Ersten.
5. Entnahme und Fortschreibung (`SelectAiringItems`, `SelectAiringItemsInterleaved`,
   `FillGaps`, `NextClipItem`, `Commit`/`CommitScan`, `PersistCursors`,
   `RewindCursor`/`RewindRing`) bleiben unverändert — sie arbeiten positions- bzw.
   inhaltsbasiert und funktionieren mit jeder stabilen Ordnung.

### Reproduzierbarkeit unter der neuen Ordnung

1. `AiringReproducible` → `FlatAiringTailReproducible`/`InterleavedAiringTailReproducible`
   re-simulieren die Auswahl über denselben `SetItems`-Aufbau; da `ItemOrderKey` eine
   reine Funktion der stabilen Eingaben ist, ergibt jede Expansion desselben
   Materialsatzes dieselbe Ordnung — der Positionsvergleich und die Skip-Rekonstruktion
   funktionieren unverändert, prozess- und laufinvariant.
2. Einmaleffekt: Bestands-Slots, die unter der alten Sortier-Ordnung erzeugt wurden,
   finden sich unter der neuen Ordnung an anderen Ringpositionen wieder — einzelne
   Ausstrahlungen können beim ersten `ReconcileAsync` nach dem Update als nicht
   reproduzierbar gelten, verdrängt und per Cursor-Anker (`RewindRing`/`NextIndexFor`)
   neu geplant werden (s. Seiteneffekte).

## Neue Klassen

Keine — die Umsetzung erfolgt über neue/private Hilfsmethoden und einen
Konstruktor-Parameter innerhalb von `ProgramScheduleService` und dessen nested classes.

## Änderungen an bestehenden Klassen

### `ProgramScheduleService` (Service, `VideoWebPlayer/Services/ProgramScheduleService.cs`)

- **Neue Methoden:**
  - `private static int Fnv1aHash(string input)` — gemeinsamer FNV-1a-Kern, aus
    `StrandOrderKey` extrahiert (Rückgabewerte unverändert).
  - `internal static int ItemOrderKey(long ruleId, string? strandKey, ProgramCursorContentType contentType, long contentId)`
    — Ordnungsschlüssel je Ringelement; `internal` für Direkttests (Muster `StrandOrderKey`).
  - `private static bool IsUnorderedRing(IReadOnlyList<SchedulableItem> items)` —
    `items.Count > 0 && items.All(i => i.ContentType is Movie or MediaItem)`.
  - `private static List<SchedulableItem> MixUnorderedRing(long ruleId, string? strandKey, List<SchedulableItem> items)`
    — liefert bei `IsUnorderedRing` die nach `ItemOrderKey`/`ContentId` sortierte Liste,
    sonst die übergebene Liste unverändert.
- **Geänderte Methoden:**
  - `StrandOrderKey` — delegiert auf `Fnv1aHash` (identische Hash-Werte; das Mischverhalten
    der Strang-Reihenfolge und bestehende Tests bleiben stabil).

### `RuleState` (private nested class in `ProgramScheduleService`)

- **Geänderte Methoden:**
  - `SetItems` — Interleaved-Pfad: jede Strang-Itemliste vor `new StrandState(…)` durch
    `MixUnorderedRing(Rule.Id, key, list)` ersetzen. Flachpfad: `Items` und die
    `StrandState`-Itemliste aus demselben `MixUnorderedRing(Rule.Id, null, items)`-Ergebnis
    aufbauen.

### `ClipGroupState` (private nested class in `ProgramScheduleService`)

- **Neue Eigenschaften:** `RuleId` (`long`) — Misch-Seed je Regel, damit Regeln mit
  derselben Clip-Gruppe unterschiedliche Ringordnungen bekommen.
- **Geänderte Methoden:**
  - Konstruktor — nimmt `ruleId` zusätzlich entgegen.
  - `SetItems` — wendet `MixUnorderedRing(RuleId, StrandKey, items)` an, bevor
    `NextIndexFor` den Cursor-Anker bestimmt.

### `ProgramScheduleService.BuildClipGroupStates`

- **Geänderte Methoden:** `new ClipGroupState(link.ClipGroup, rule.Id)` — Regel-Id
  durchreichen (`ExpandClipGroupStatesAsync` ruft `SetItems` am bereits korrekt
  initialisierten Zustand erneut auf, keine weitere Anpassung nötig).

## Datenbankmigrationen

Keine.

## Validierungsregeln

Keine — es werden keine neuen oder geänderten Eingaben verarbeitet.

## Konfigurationsänderungen

Keine — das korrigierte Auswahlverhalten gilt fachlich immer; eine Schaltbarkeit ist aus
der Anforderung nicht ableitbar.

## Seiteneffekte und Risiken

- **Bestandspläne:** Beim ersten `ReconcileAsync` nach dem Update kann die
  Reproduzierbarkeitsprüfung einzelne unter der alten Sortier-Ordnung erzeugte
  Ausstrahlungen als nicht reproduzierbar einstufen und neu planen — einmaliger,
  fachlich gewollter Effekt (Cursor-Anker greift weiter); in den Release Notes zu nennen.
- **Positionsabhängige Bestandstests:** Tests, deren Szenario eine konkrete Ringposition
  voraussetzt (s. „Betroffene bestehende Tests"), müssen Seed oder Erwartung an die
  Hash-Ordnung anpassen — Erwartungen stets über `ItemOrderKey` ableiten, niemals
  Assertions abschwächen.
- **Genre-Füller-Flachring (gemischter Inhalt):** bleibt bewusst in Expansionsordnung
  (Filme sortiert, Episoden in Sendedreihenfolge); der Film-Anteil behält dort seinen
  Sortier-Bias — akzeptierte Einschränkung zugunsten der Episodenreihenfolge (s.
  Offene Punkte).
- **Keine Auswirkung auf:** Datenmodell (`ProgramRuleCursor` ankert über
  `LastContentType`/`LastContentId`, keine Positionsabhängigkeit), Backup/Restore,
  `GetRuleDiagnosesAsync` (zählt nur Kandidaten, keine Ringe), `ProgramScheduleWorker`,
  UI und E2E-Tests.

## Umsetzungsreihenfolge

1. **FNV-1a-Kern und Ordnungsschlüssel einführen**
   - Voraussetzungen: Keine.
   - Beschreibung: `Fnv1aHash` aus `StrandOrderKey` extrahieren; `ItemOrderKey`,
     `IsUnorderedRing` und `MixUnorderedRing` in `ProgramScheduleService` anlegen
     (kein Aufrufer — Zwischenstand kompiliert).

2. **`RuleState.SetItems` um die Mischung erweitern**
   - Voraussetzungen: Schritt 1.
   - Beschreibung: Flachpfad (`Items` und `StrandState` aus derselben gemischten Liste)
     und Interleaved-Pfad (`MixUnorderedRing` je Strang vor `StrandState`-Bau).

3. **Clip-Gruppen-Ringe mischen**
   - Voraussetzungen: Schritt 1.
   - Beschreibung: `ClipGroupState` um `RuleId` (Ctor-Parameter) erweitern, `SetItems`
     mischt den Ring; `BuildClipGroupStates` reicht `rule.Id` durch.

4. **Neue Testklasse `ProgramScheduleServiceTests_RingOrder` anlegen**
   - Voraussetzungen: Schritte 1–3; Fixture `ProgramScheduleTestDb` (echtes SQLite)
     und `InternalsVisibleTo` sind vorhanden.
   - Beschreibung: Unit-Test für `ItemOrderKey` sowie SQLite-Tests für Ringkopf,
     Verteilung (Akzeptanzkriterium), Interleaving-Grenze, Playlist-Ordnungserhalt
     (`Manual` und `ByReleaseDate`), Clip-Ringe, Trailer-Ausnahme,
     Laufinvarianz und Reconcile-Reproduzierbarkeit — siehe „Neue Tests".

5. **Betroffene Bestandstests prüfen und ggf. anpassen**
   - Voraussetzungen: Schritte 2–3.
   - Beschreibung: Positionsabhängige Szenarien (`_Boundary`, `_AgeWindow`,
     `_ClipSource`) verifizieren; Seeds/Erwartungen über `ItemOrderKey` ausrichten —
     keine Assertions abschwächen.

6. **Dokumentation und Abschlusslauf**
   - Voraussetzungen: Schritte 1–5.
   - Beschreibung: `docs/help/tv-sender-ablauf-technisch.md` um die Ringmischung
     ergänzen (Abschnitt Ring-/Strang-Semantik); `dotnet build VideoPlayer.sln` in
     Debug und Release fehlerfrei; volle Suite
     `dotnet test VideoWebPlayer.Tests/VideoWebPlayer.Tests.csproj` grün.

## Tests

### Neue Tests

Neue thematisch fokussierte Testklasse `ProgramScheduleServiceTests_RingOrder`
(`VideoWebPlayer.Tests/ProgramScheduleServiceTests_RingOrder.cs`) — alle DB-Tests gegen
echtes SQLite über `ProgramScheduleTestDb` (in-memory shared, `EnsureCreated`).

| Test / Hilfsmethode | Testklasse | Was wird geprüft / bereitgestellt? |
|---------------------|------------|-------------------------------------|
| `ItemOrderKey_IsDeterministic_AndRuleDependent` | `ProgramScheduleServiceTests_RingOrder` | Reiner Unit-Test ohne DB (Muster `StrandOrderKey_IsDeterministic_AndNotAlphabetic`): identische Eingabe → identischer Schlüssel; das Kopfelement variiert über `ruleId`; die Ordnung ist weder Id- noch alphabetisch sortiert. |
| `EnsureCoverage_MovieCollectionRing_FirstPick_FollowsItemOrderKey` | `ProgramScheduleServiceTests_RingOrder` | Collection mit N Filmen, deren Release-Ordnung ≠ Hash-Ordnung ist; der erste Slot der Regel trägt den `ItemOrderKey`-Minimalfilm — nicht den `ReleaseDate`-/Alphabet-Ersten. Regel-Recurrence lookback-sicher wählen (s. Hinweis unter der Tabelle). |
| `EnsureCoverage_UnorderedRing_HeadPicks_DistributeAcrossRules` | `ProgramScheduleServiceTests_RingOrder` | **Akzeptanzkriterium (Verteilung):** N = 6 Filme in einer Collection, R = 24 Regeln (`MovieCollection`-Material, unterschiedliche `TimeOfDay`/`Rule.Id`), ein `EnsureCoverageAsync`-Lauf. Je Regel ist der erste Slot (`MovieId`) der Hash-Kopf. Aggregiert: jeder Film mindestens 1× Ringkopf, kein Film häufiger als ~2·(R/N)+2 ≈ 10; der Release-/Alphabet-Erste ist nicht überrepräsentiert. Grenzen einmal verifizieren und im Test kommentieren (deterministisch, nicht flaky). Regel-Recurrence lookback-sicher wählen (s. Hinweis unter der Tabelle). |
| `EnsureCoverage_GenreRule_OtherStrand_MoviesMixed_ShowsKeepAirOrder` | `ProgramScheduleServiceTests_RingOrder` | Genre-Regel mit Filmen + Serie: Episoden je `show:{id}`-Strang bleiben in Sendedreihenfolge (Positionsfolge +1 je Tag), der Führungsfilm des `other`-Strangs ist das `ItemOrderKey(rule.Id, "other", …)`-Minimum. Strand-Key-Nuance beachten: Die Erwartung nutzt `"other"`, weil die Regel mehrsträngig expandiert — bei einsträngiger Genre-Expansion (Flachpfad) wäre der Misch-Seed `null` (s. Programmablauf). Regel-Recurrence lookback-sicher wählen (s. Hinweis unter der Tabelle). |
| `EnsureCoverage_PlaylistRing_ManualSortOrder_IsNotMixed` | `ProgramScheduleServiceTests_RingOrder` | **Negativtest Playlist-Ordnungserhalt** (der Playlist-Pfad hat bisher keinerlei Sendeplan-Abdeckung — kein Bestandstest nutzt `ProgramRuleMaterialType.Playlist`): `Playlist` mit `SortMode.Manual` und Movie-`PlaylistEntry`s, deren `SortOrder` bewusst gegen die `ItemOrderKey`-Hash-Ordnung der Entry-Ids gesetzt ist; Playlist-Regel — die Slot-Folge entspricht `SortOrder`/`AddedAt`, nicht `ItemOrderKey`. Sichert strukturell ab, dass `IsUnorderedRing` und `PreservesOrder` komplementär bleiben (ein künftiger geordneter ContentType oder ein Prädikatfehler würde Playlist-Ringe sonst unbemerkt mischen). Seed: `Playlist`/`PlaylistEntry`-Zeilen direkt über `Db` (die Fixture hat keinen Playlist-Helfer — bei Bedarf kleinen `CreatePlaylistAsync`-Helfer ergänzen; `Playlist.UserId` ist Pflichtfeld, `AddUserAsync` vorhanden). Dabei `PlaylistEntry.MediaType = MediaTypeValues.Movie` setzen — sonst sortiert der `IsPlayable`-Filter den Eintrag aus; die `ItemOrderKey`-Gegenerwartung über `PlaylistEntry.Id` richten, nicht über `Movie.Id` (die `ContentId` der Items ist die Entry-Id, `ExpandPlaylistAsync` Z. 2573/2579). Regel-Recurrence lookback-sicher wählen (s. Hinweis unter der Tabelle). |
| `EnsureCoverage_PlaylistRing_ByReleaseDate_KeepsReleaseOrder` | `ProgramScheduleServiceTests_RingOrder` | Zweiter geordneter Playlist-Pfad: `Playlist` im `ByReleaseDate`-Modus, deren `ReleaseDate`-Reihenfolge bewusst gegen die `ItemOrderKey`-Ordnung gesetzt ist — die Slot-Folge entspricht der Release-Datums-Ordnung aus `ExpandPlaylistAsync`, ungemischt. Die Release-Ordnung steuert der Test über `Movie.ReleaseDate ?? PremieredAt` (per `MediaHierarchyRegistry` aufgelöst); `PlaylistEntry.MediaType = MediaTypeValues.Movie` und die Hash-Gegenordnung über `PlaylistEntry.Id` wie im `Manual`-Test. Regel-Recurrence lookback-sicher wählen (s. Hinweis unter der Tabelle). |
| `EnsureCoverage_ClipSourceRing_FirstPick_FollowsItemOrderKey` | `ProgramScheduleServiceTests_RingOrder` | `MediaSourceClips`-Füller: der erste gezogene Clip-Füll-Slot ist der Hash-Kopf, nicht die kleinste `MediaItemId`. Regel-Recurrence lookback-sicher wählen (s. Hinweis unter der Tabelle). |
| `EnsureCoverage_ClipGroupRing_FirstPick_FollowsItemOrderKey` | `ProgramScheduleServiceTests_RingOrder` | Nutzer-Clip-Gruppe (`clipgroup:{id}`-Strang): erster Clip-Slot = `ItemOrderKey(ruleId, "clipgroup:{id}", …)`-Minimum. Regel-Recurrence lookback-sicher wählen (s. Hinweis unter der Tabelle). |
| `EnsureCoverage_RingOrder_StableAcrossIndependentFixtures` | `ProgramScheduleServiceTests_RingOrder` | Zwei identisch geseedete Fixtures (gleiche Filme/Regel) erzeugen dieselbe Slot-Sequenz — Laufinvarianz; Prozessinvarianz zusätzlich durch den FNV-1a-Unit-Test abgesichert. |
| `Reconcile_MixedRing_StaysReproducible` | `ProgramScheduleServiceTests_RingOrder` | MovieCollection-Regel: `EnsureCoverageAsync`, dann `ReconcileAsync` ohne Änderung → `AssertNoDisplacedAsync` (Positionsvergleich der Repro-Prüfung funktioniert mit der neuen Ordnung). |
| `EnsureCoverage_TrailerRing_KeepsAiringsOrder` | `ProgramScheduleServiceTests_RingOrder` | Zwei Filme mit Ausstrahlungen zu unterschiedlichen Zeiten + je Trailer: im Lead-Fenster wird der Trailer des früher ausgestrahlten Films gezogen — die fachliche Vorlauf-Ordnung bleibt bewusst ungemischt. |

**Lookback-Hinweis zu den Kopf-/Positionserwartungen:** `BuildOccurrences` erzeugt
Occurrences ab `coverageStart − lookback`; bei `Recurrence.Daily` verbraucht die
gestrige Lookback-Occurrence den Ringkopf bereits, ohne einen Slot zu erzeugen
(`end > coverageStart`-Filter in `MaterializeOccurrence`). Alle obigen Tests mit
„erster Slot = Ringkopf"- bzw. Positionserwartung (`…_FirstPick_…`,
`…_HeadPicks_…`, `…_OtherStrand_…`, die Playlist-Ordnungstests) seeden ihre Regel
daher mit `ProgramRuleRecurrence.Once` oder `Weekly` außerhalb des
Lookback-Fensters — oder legen die Erwartung um den Lookback-Konsum verschoben
auf die Position hinter dem verbrauchten Kopf.

### Betroffene bestehende Tests

| Test / Testklasse | Grund der Anpassung |
|-------------------|---------------------|
| `ProgramScheduleServiceTests_Boundary.EnsureCoverage_OversizedHeadItem_AirsAndOvershootsSuccessor` | Szenario-Prämisse: die Lookback-Occurrence verbraucht den Ringkopf; welches Item heute führt, hängt künftig von `ItemOrderKey` statt `ReleaseDate` ab — Ringposition über `ItemOrderKey` verifizieren, Seed/Erwartung ggf. anpassen. |
| `ProgramScheduleServiceTests_Boundary.EnsureCoverage_HardBound_ItemCannotComplete_DefersToNextOccurrence` | Die „45-Minuten-Kopf"-Prämisse ist positionsabhängig — Ringposition des Langfilms über `ItemOrderKey` verifizieren/anpassen. |
| `ProgramScheduleServiceTests_AgeWindow` (`EnsureCoverage_UnorderedMovieRing_SkipsBlockedAndOffersItAgainAtNight`, `EnsureCoverage_MixedRatings_Fsk16AirsOnlyFrom22`, `Reconcile_DisplacedUnorderedRing_ReoffersSkippedPositionOnlyInWindow`, `Reconcile_InterleavedWindowSkip_StaysReproducible_TighterWindowDisplaces`) | Die Assertions sind ratingbasiert und bleiben unter jeder Permutation grün (verifiziert: gesperrte Position wird konsumiert-übersprungen, zulässige ausgestrahlt). Die positionsnennenden Kommentare veralten jedoch in jedem Fall („blocked title sits at ring position 0", „FSK-16 title heads the ring", „middle position is FSK-18-blocked") — an die tatsächlichen `ItemOrderKey`-Positionen anpassen oder positionsneutral formulieren; zusätzlich prüfen, ob die intendierte „gesperrt an Position k"-Prämisse unter der Hash-Ordnung noch erfüllt ist, sonst Seed über `ItemOrderKey` ausrichten. |
| `ProgramScheduleServiceTests_ClipSource.EnsureCoverage_ClipSource_CursorContinuesRingAcrossRuns` | Alternationsannahme am Ring-Anker — voraussichtlich unverändert grün, aber unter neuer Ordnung zu verifizieren. |
| `ProgramScheduleServiceTests_ClipGroups.Reconcile_RemovedClipGroupMember_DisplacesAndReplans` | Die Prämisse „der entfernte `clips[0]` war verplant" hängt vom Ring-Verbrauch ab (12 Clips/Tag ziehen den gesamten Ring): unter der neuen Hash-Ordnung verifizieren, dass `clips[0]` weiterhin verplant ist — bei Seed-Änderungen ist die Zuordnung über `ItemOrderKey` nachzuprüfen. |
| `ProgramScheduleServiceTests_ClipFillPeriodBounds` (`EnsureCoverage_FullPrimetimeScenario_ClipsDistributedInsidePeriod`, `EnsureCoverage_OccupiedInsidePeriod_ClipFillDoesNotOverlap`, `EnsureCoverage_StartedMidPeriod_NoClipsAfterNominalPeriodEnd`) | **Konkret gefährdet — Seed anpassen.** Die Grace-Verzweigung `t - accumulated < grace` (`SelectAiringItems`, Z. 1844) hängt an der Paarsumme der gezogenen Titel: Der `MovieCollection`-Ring aus vier Filmen (105/106/105/106 min, Seed Z. 34-35) erfüllt die im Kommentar (Z. 31-33) begründete Prämisse „alle Vierer-Paare gleich lang" nur über die bisherige sequenzielle Ringreihenfolge (jedes Folgepaar = 211 min → Rest 29 < Grace 30 → Clip-Füllung). Unter der `ItemOrderKey`-Permutation kann ein {105,105}-Paar gezogen werden: Rest exakt 30 → `30 < 30` falsch → ein dritter Titel startet und überschießt das Periodenende — `Assert.NotEmpty(clipSlots)` im FullPrimetime-Test schlägt fehl, im Occupied-Test kann der Überschuss mit dem reservierten 00:00-00:30-Fenster kollidieren. **Anpassung:** die vier Filmdauern wirklich gleichlang auf ≥ 106 min setzen (z. B. 4×106 → jede Paarsumme unter jeder Permutation 212, Rest 28 < 30 — permutationsinvariant, keine `ItemOrderKey`-Verifikation nötig; **nicht** 4×105: Paar 210, Rest 30 = Grace). Alternativ Paarsummen der konkreten `ItemOrderKey`-Ordnung verifizieren und Dauern/Grace danach wählen. Den Seed-Kommentar anpassen (die Ringreihenfolge-Begründung gilt unter Permutation nicht mehr). `…_StartedMidPeriod_…` (Grace 15) ist robust (Clip-Assertions vakuum-wahr), bleibt aber in der Verifikation. Keine Assertions abschwächen. |
| Allgemein | Nach Umsetzung volle Suite fahren; weitere Tests, die implizit die Sortierreihenfolge ungeordneter Ringe erwarten, ggf. anpassen — Erwartungen aus `ItemOrderKey` ableiten. |

### E2E-Tests (primärer Funktionsnachweis)

Keine neuen E2E-Tests erforderlich. Begründung: Die Anforderung ändert keinen über UI
oder Nutzeraktion erreichbaren Ablauf — kein neuer oder geänderter Benutzerfluss, keine
neue Interaktion. Die geänderte Auswahlreihenfolge ist ausschließlich über den Inhalt
des generierten Sendeplans beobachtbar und wird durch die SQLite-Integrationstests der
neuen Testklasse (inkl. Verteilungstest) nachgewiesen. Ein E2E-Test könnte die
Verteilung nicht zuverlässiger prüfen als die Service-Tests und wäre nur ein
indirekter, teurerer Umweg über `GetSchedule`/Anzeige.

Anzupassende bestehende E2E-Tests: Keine — die senderbezogenen E2E-Tests
(`TVSenderAdminE2ETests`, `ProgramGuideE2ETests`, `LiveTvE2ETests`) assertieren
Regel-CRUD, MaterialType-Persistenz und Anzeige, nicht die Ringreihenfolge.

## Offene Punkte

Keine — beide zuvor offenen Punkte wurden vom Anwender am 2026-10-27 entschieden,
jeweils wie im Plan empfohlen:

1. **Gleichverteilung über Regeln/Materialmengen:** Der konstante Ringkopf je Regel
   (stabile Hash-Ordnung bei unverändertem Materialsatz) genügt dem
   Akzeptanzkriterium „über mehrere Generierungsläufe" — die Gleichverteilung
   entsteht über die Regeln, nicht über Läufe einer einzelnen Regel. Der geplante
   Verteilungstest über 24 Regeln ist der Nachweis.
2. **Genre-Füller-Flachring:** Dass der flache Ring (Filme + Episoden) wegen der
   Episodenreihenfolge ungemischt bleibt und dort der Film-Sortier-Bias bestehen
   bleibt, ist als dokumentierte Einschränkung akzeptiert (s. „Seiteneffekte und
   Risiken").
