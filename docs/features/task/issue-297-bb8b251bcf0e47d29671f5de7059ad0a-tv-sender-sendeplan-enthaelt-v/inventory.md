# Bestandsaufnahme: Gleichverteilte Materialauswahl im Sendeplan (Issue #297)

Analysiert wurde die Material-Expansion und Ring-Entnahme des `ProgramScheduleService`
(`VideoWebPlayer/Services/ProgramScheduleService.cs`) des automatisierten TV-Senders —
bezogen auf den beobachteten Bias, dass überproportional Titel mit Anfangsbuchstabe „A"
eingeplant werden.

## Zusammenfassung

- Die Vermutung der Anforderung bestätigt sich im Code: **Alle ungeordneten Ringe werden in
  fester Sortierreihenfolge aufgebaut** — `ExpandMovieCollectionAsync` sortiert
  `ReleaseDate ?? PremieredAt` + `Id` (Z. 2230-2231), `ExpandGenreAsync` Filme
  `ReleaseDate ?? PremieredAt` + `Name` + `Id` (Z. 2312-2314), `FilterClipCandidatesAsync`
  `MediaItemId` (Z. 2710), `ExpandPlaylistAsync` je `PlaylistSortMode` (Z. 2528-2549).
- Die Entnahme beginnt systematisch am **Ringanfang**: `NextIndexFor` liefert Index 0, wenn
  kein Cursor existiert oder das Cursor-Element nicht mehr im Ring ist (`FindIndex < 0`,
  Z. 1529-1536); `FillGaps`/`NextClipItem` ziehen `PeekAt(0)` (Z. 1950, 2764). Kombination:
  frühe Listenelemente werden bevorzugt → Alphabet-/Id-/Releasedate-Bias.
- **`PreservesOrder` ist die vorhandene geordnet/ungeordnet-Grenze** (`StrandState`, Z.
  1223-1224): `TVShowEpisode`- und `PlaylistEntry`-Stränge sind geordnet (Serienepisoden,
  alle Playlists — auch `PlaylistSortMode.ByReleaseDate`); `Movie`-, `Trailer`- und
  `MediaItem`-Ringe sind ungeordnet und für eine Mischung frei.
- **Ein deterministisches Mischmuster existiert bereits:** `StrandOrderKey` (FNV-1a über
  `"{ruleId}:{strandKey}"`, `internal static`, Z. 1362-1374) mischt heute die
  *Strang*-Reihenfolge bei Genre-Interleaving — getestet durch
  `StrandOrderKey_IsDeterministic_AndNotAlphabetic` und
  `EnsureCoverage_GenreRule_LeadStrand_FollowsHashOrder_NotAlphabetical`. `string.GetHashCode`
  und `Random` kommen nicht infrage (prozesszufällig; Reproduzierbarkeitspflicht).
- **Ordnungskritische Abhängigkeiten:** Die Reproduzierbarkeitsprüfung (`AiringReproducible`/
  `FlatAiringTailReproducible`/`InterleavedAiringTailReproducible`) re-simuliert die Auswahl
  über dieselbe Ringordnung inkl. Positionsvergleich und Skip-Rekonstruktion — jede neue
  Ordnung muss prozess- und laufinvariant identisch sein. `NextIndexFor`, `FindItemIndex`
  und `RewindRing` arbeiten positionsbasiert über `FindIndex`/`MediaItemId` und funktionieren
  mit jeder stabilen Ordnung.
- Datenmodell: voraussichtlich unverändert — Cursor ankern über `LastContentType`/
  `LastContentId`, nicht über Positionen.
- Fachliche Testlücke: kein bestehender Test prüft die Verteilung über Generierungsläufe;
  das geforderte Akzeptanzkriterium (SQLite-Verteilungstest über `ProgramScheduleTestDb`)
  ist neu zu schreiben.

Test-Ausgangszustand: komplette Suite `dotnet test` (Debug, inkl. 75 E2E-Klassen) **grün** —
2591 bestanden, 0 fehlgeschlagen, 2 übersprungen (dokumentiert: 1× OS-bedingt, 1× bekannter
Produktiv-Befund). Debug- und Release-Build jeweils fehlerfrei (140 preexisting
BL0008-Warnungen). Nachweis: [inventory/tests.md](inventory/tests.md).

## Details

- [Datenmodell](inventory/models.md) — `ProgramRule`, `ProgramRuleCursor`, `ProgramSlot`,
  Clip-Gruppen-/Genre-/Quellen-Joins, `Playlist`/`PlaylistEntry`, Medien-Basisfelder
- [Logik](inventory/logic.md) — `ProgramScheduleService` komplett: Expansion, Ringaufbau
  (`RuleState`/`StrandState`/`ClipGroupState`), Entnahme (`NextIndexFor`, `PeekAt`,
  `SelectAiringItems*`, `FillGaps`, `NextClipItem`), Cursor (`PersistCursors`,
  `RewindCursor`), Reproduzierbarkeit, `StrandOrderKey`; `ProgramScheduleWorker`
- [Enums](inventory/enums.md) — `ProgramRuleMaterialType`, `ProgramRuleType`,
  `ProgramRuleRecurrence`, `ProgramSlotStatus`, `ProgramCursorContentType`,
  `PlaylistSortMode`, `ProgramScheduleContentScope`
- [Interfaces](inventory/interfaces.md) — `IProgramScheduleService` + Summary-/Diagnose-Records
- [Tests](inventory/tests.md) — Test-Ausgangszustand (TRX- + Konsolen-Nachweis), alle
  sendeplanbezogenen Testklassen, Fixture `ProgramScheduleTestDb`
