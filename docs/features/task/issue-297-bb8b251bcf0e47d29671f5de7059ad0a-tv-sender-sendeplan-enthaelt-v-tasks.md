# Tasks: Gleichverteilte Materialauswahl im Sendeplan (Issue #297)

| # | Bereich | Aufgabe | Status | Testnachweis |
|---|---------|---------|--------|--------------|
| 1 | Logik | FNV-1a-Kern aus `StrandOrderKey` in `private static int Fnv1aHash(string)` extrahieren (Hash-Werte unverändert) | Fertig | `StrandOrderKey_IsDeterministic_AndNotAlphabetic` u. a. Interleaving-Tests grün |
| 2 | Logik | `internal static int ItemOrderKey(long ruleId, string? strandKey, ProgramCursorContentType contentType, long contentId)` auf `ProgramScheduleService` einführen | Fertig | `ItemOrderKey_IsDeterministic_AndRuleDependent` |
| 3 | Logik | `IsUnorderedRing`-Prädikat (`items.All(ContentType is Movie or MediaItem)`) und `MixUnorderedRing` (`OrderBy(ItemOrderKey).ThenBy(ContentId)`) einführen | Fertig | Kopf-/Ordnungstests der neuen Klasse |
| 4 | Logik | `RuleState.SetItems` Flachpfad: `Items` und `StrandState` aus derselben gemischten Listeninstanz aufbauen | Fertig | `EnsureCoverage_MovieCollectionRing_FirstPick_FollowsItemOrderKey`, `Reconcile_MixedRing_StaysReproducible` |
| 5 | Logik | `RuleState.SetItems` Interleaved-Pfad: je Strang `MixUnorderedRing(Rule.Id, key, …)` vor `StrandState`-Bau anwenden | Fertig | `EnsureCoverage_GenreRule_OtherStrand_MoviesMixed_ShowsKeepAirOrder` |
| 6 | Logik | `ClipGroupState` um `RuleId` (Ctor-Parameter) erweitern und `SetItems` um `MixUnorderedRing` ergänzen | Fertig | `EnsureCoverage_ClipGroupRing_FirstPick_FollowsItemOrderKey` |
| 7 | Logik | `BuildClipGroupStates`: `rule.Id` an `ClipGroupState` durchreichen | Fertig | dto. |
| 8 | Tests | Neue Testklasse `ProgramScheduleServiceTests_RingOrder` anlegen (Fixture `ProgramScheduleTestDb`, echtes SQLite) | Fertig | 11 Tests grün |
| 9 | Tests | `ItemOrderKey_IsDeterministic_AndRuleDependent` (reiner Unit-Test, Muster `StrandOrderKey_IsDeterministic_AndNotAlphabetic`) | Fertig | grün |
| 10 | Tests | `EnsureCoverage_MovieCollectionRing_FirstPick_FollowsItemOrderKey` (SQLite) | Fertig | grün (Hash-Kopf ≠ Release-Erster verifiziert) |
| 11 | Tests | `EnsureCoverage_UnorderedRing_HeadPicks_DistributeAcrossRules` (SQLite-Verteilungstest: N = 6 Filme, R = 24 Regeln — Akzeptanzkriterium) | Fertig | grün; Verteilung 5/5/2/3/4/5, jeder Film mind. 1× Kopf, Release-Erster 5× ≤ 8 |
| 12 | Tests | `EnsureCoverage_GenreRule_OtherStrand_MoviesMixed_ShowsKeepAirOrder` (SQLite) | Fertig | grün |
| 13 | Tests | `EnsureCoverage_PlaylistRing_ManualSortOrder_IsNotMixed` (SQLite, Negativtest `PlaylistSortMode.Manual` — Slots folgen `SortOrder`, ungemischt; `Playlist`/`PlaylistEntry` direkt seeden, `MediaTypeValues.Movie`, Hash-Gegenordnung über `PlaylistEntry.Id`) | Fertig | grün (Zusatz: `GrantSourceAccessAsync` für Playlist-Besitzer nötig) |
| 14 | Tests | `EnsureCoverage_PlaylistRing_ByReleaseDate_KeepsReleaseOrder` (SQLite, `PlaylistSortMode.ByReleaseDate` bleibt ungemischt; Ordnung über `Movie.ReleaseDate ?? PremieredAt`, `MediaTypeValues.Movie`, Hash-Gegenordnung über `PlaylistEntry.Id`) | Fertig | grün |
| 15 | Tests | `EnsureCoverage_ClipSourceRing_FirstPick_FollowsItemOrderKey` (SQLite) | Fertig | grün (Hash-Kopf = Clip-Id 3 ≠ kleinste Id) |
| 16 | Tests | `EnsureCoverage_ClipGroupRing_FirstPick_FollowsItemOrderKey` (SQLite) | Fertig | grün (Hash-Kopf = Clip-Id 13 ≠ kleinste Id) |
| 17 | Tests | `EnsureCoverage_RingOrder_StableAcrossIndependentFixtures` (SQLite, Laufinvarianz) | Fertig | grün |
| 18 | Tests | `Reconcile_MixedRing_StaysReproducible` (SQLite, `AssertNoDisplacedAsync`) | Fertig | grün |
| 19 | Tests | `EnsureCoverage_TrailerRing_KeepsAiringsOrder` (SQLite, Trailer-Ausnahme) | Fertig | grün (Airings-Kopf ≠ ItemOrderKey-Kopf — diskriminierend) |
| 20 | Tests | Positionsabhängige Bestandstests verifizieren/anpassen (`_Boundary`: OversizedHeadItem, HardBound; `_AgeWindow`: Seeds + positionsnennende Kommentare aktualisieren; `_ClipSource`: CursorContinues; `_ClipGroups`: RemovedClipGroupMember — Ring-Verbrauch; `_ClipFillPeriodBounds` — Filmdauern permutationsfest gleichlang auf ≥ 106 min setzen, z. B. 4×106, Seed-Kommentar anpassen) — Erwartungen über `ItemOrderKey` ableiten, nichts abschwächen | Fertig | Alle 5 Klassen grün (61 Tests); `_Boundary.HardBound` Seed getauscht (Lang = Id 2 = Hash-Kopf), `_ClipFillPeriodBounds` 4×106, `_AgeWindow` Kommentare an `ItemOrderKey`-Positionen angepasst, Rest verifiziert unverändert grün |
| 21 | Dokumentation | `docs/help/tv-sender-ablauf-technisch.md` um die deterministische Ringmischung ergänzen | Fertig | Abschnitt „Plangenerierung" Punkt 1 ergänzt; zusätzlich Release-Notes-Eintrag (EN+DE) |
| 22 | Abschluss | `dotnet build VideoPlayer.sln` Debug + Release fehlerfrei; volle Suite `dotnet test VideoWebPlayer.Tests/VideoWebPlayer.Tests.csproj` grün | Fertig | Debug + Release fehlerfrei (0 Fehler); volle Suite in Schritt 10: 2604 Tests, 2602 bestanden, 0 Fehlschläge, 2 Skips = Baseline (`test-results.md`) |
