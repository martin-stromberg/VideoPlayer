# Tasks: Gleichverteilte Materialauswahl im Sendeplan (Issue #297)

| # | Bereich | Aufgabe | Status | Testnachweis |
|---|---------|---------|--------|--------------|
| 1 | Logik | FNV-1a-Kern aus `StrandOrderKey` in `private static int Fnv1aHash(string)` extrahieren (Hash-Werte unverändert) | Offen | — |
| 2 | Logik | `internal static int ItemOrderKey(long ruleId, string? strandKey, ProgramCursorContentType contentType, long contentId)` auf `ProgramScheduleService` einführen | Offen | — |
| 3 | Logik | `IsUnorderedRing`-Prädikat (`items.All(ContentType is Movie or MediaItem)`) und `MixUnorderedRing` (`OrderBy(ItemOrderKey).ThenBy(ContentId)`) einführen | Offen | — |
| 4 | Logik | `RuleState.SetItems` Flachpfad: `Items` und `StrandState` aus derselben gemischten Listeninstanz aufbauen | Offen | — |
| 5 | Logik | `RuleState.SetItems` Interleaved-Pfad: je Strang `MixUnorderedRing(Rule.Id, key, …)` vor `StrandState`-Bau anwenden | Offen | — |
| 6 | Logik | `ClipGroupState` um `RuleId` (Ctor-Parameter) erweitern und `SetItems` um `MixUnorderedRing` ergänzen | Offen | — |
| 7 | Logik | `BuildClipGroupStates`: `rule.Id` an `ClipGroupState` durchreichen | Offen | — |
| 8 | Tests | Neue Testklasse `ProgramScheduleServiceTests_RingOrder` anlegen (Fixture `ProgramScheduleTestDb`, echtes SQLite) | Offen | — |
| 9 | Tests | `ItemOrderKey_IsDeterministic_AndRuleDependent` (reiner Unit-Test, Muster `StrandOrderKey_IsDeterministic_AndNotAlphabetic`) | Offen | — |
| 10 | Tests | `EnsureCoverage_MovieCollectionRing_FirstPick_FollowsItemOrderKey` (SQLite) | Offen | — |
| 11 | Tests | `EnsureCoverage_UnorderedRing_HeadPicks_DistributeAcrossRules` (SQLite-Verteilungstest: N = 6 Filme, R = 24 Regeln — Akzeptanzkriterium) | Offen | — |
| 12 | Tests | `EnsureCoverage_GenreRule_OtherStrand_MoviesMixed_ShowsKeepAirOrder` (SQLite) | Offen | — |
| 13 | Tests | `EnsureCoverage_PlaylistRing_ManualSortOrder_IsNotMixed` (SQLite, Negativtest `PlaylistSortMode.Manual` — Slots folgen `SortOrder`, ungemischt; `Playlist`/`PlaylistEntry` direkt seeden, `MediaTypeValues.Movie`, Hash-Gegenordnung über `PlaylistEntry.Id`) | Offen | — |
| 14 | Tests | `EnsureCoverage_PlaylistRing_ByReleaseDate_KeepsReleaseOrder` (SQLite, `PlaylistSortMode.ByReleaseDate` bleibt ungemischt; Ordnung über `Movie.ReleaseDate ?? PremieredAt`, `MediaTypeValues.Movie`, Hash-Gegenordnung über `PlaylistEntry.Id`) | Offen | — |
| 15 | Tests | `EnsureCoverage_ClipSourceRing_FirstPick_FollowsItemOrderKey` (SQLite) | Offen | — |
| 16 | Tests | `EnsureCoverage_ClipGroupRing_FirstPick_FollowsItemOrderKey` (SQLite) | Offen | — |
| 17 | Tests | `EnsureCoverage_RingOrder_StableAcrossIndependentFixtures` (SQLite, Laufinvarianz) | Offen | — |
| 18 | Tests | `Reconcile_MixedRing_StaysReproducible` (SQLite, `AssertNoDisplacedAsync`) | Offen | — |
| 19 | Tests | `EnsureCoverage_TrailerRing_KeepsAiringsOrder` (SQLite, Trailer-Ausnahme) | Offen | — |
| 20 | Tests | Positionsabhängige Bestandstests verifizieren/anpassen (`_Boundary`: OversizedHeadItem, HardBound; `_AgeWindow`: Seeds + positionsnennende Kommentare aktualisieren; `_ClipSource`: CursorContinues; `_ClipGroups`: RemovedClipGroupMember — Ring-Verbrauch; `_ClipFillPeriodBounds` — Filmdauern permutationsfest gleichlang auf ≥ 106 min setzen, z. B. 4×106, Seed-Kommentar anpassen) — Erwartungen über `ItemOrderKey` ableiten, nichts abschwächen | Offen | — |
| 21 | Dokumentation | `docs/help/tv-sender-ablauf-technisch.md` um die deterministische Ringmischung ergänzen | Offen | — |
| 22 | Abschluss | `dotnet build VideoPlayer.sln` Debug + Release fehlerfrei; volle Suite `dotnet test VideoWebPlayer.Tests/VideoWebPlayer.Tests.csproj` grün | Offen | — |
