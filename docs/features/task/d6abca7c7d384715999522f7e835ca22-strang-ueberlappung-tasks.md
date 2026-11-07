# Tasks: Überlappungsfreie Strang-Rotation bei Interleaved-Ausstrahlungen (Issue #343)

| # | Bereich | Aufgabe | Status | Testnachweis |
|---|---------|---------|--------|--------------|
| 1 | Tests | Pflichttest `EnsureCoverage_ManyCollectionStrands_ConsecutiveAiringsServeDisjointStrands` schreiben (Bedient-Mengen aufeinanderfolgender Abende disjunkt bei `strandCount > picksPerDay`; rot auf Ist-Stand) | Offen | — |
| 2 | Tests | Regressionstest `EnsureCoverage_SecondRun_ContinuesDisjoint` schreiben (Zeiger-Fortsetzung über zweiten `EnsureCoverage`-Lauf hinweg; rot auf Ist-Stand) | Offen | — |
| 3 | Logik | `OccupiedSlot`-Record anlegen und `persistedOccupancy` in `EnsureCoverageAsync` um `MediaItemId`/`MovieId`/`TVShowEpisodeId`/`IsClipFill` erweitern; `MaterializeOccurrence`-Signatur anpassen | Offen | — |
| 4 | Logik | `FindItemIndex`-Überladung (ids statt `ProgramSlot`) einführen; bestehende Überladung delegiert | Offen | — |
| 5 | Logik | `TitleStrandIndex` implementieren (Slot → Strang-Index, Clip-Klassifikation via `ClipMediaItemIds`/`IsClipFill`) | Offen | — |
| 6 | Logik | `StrandRotationOffset` implementieren (letzter auflösbarer Titel-Slot der Regel vor `airingStartUtc` → `strandIndex + 1 mod m`, sonst 0) | Offen | — |
| 7 | Logik | `SelectItems` + `SelectAiringItemsInterleaved` um `strandRotationOffset`-Parameter erweitern; Rotationspfad (`wanted >= m`) nutzt den Zeiger statt `StrandStartOffset` | Offen | — |
| 8 | Logik | `MaterializeOccurrence` und `MaterializeOccurrenceEvicting`: Zeiger aus Regel-Slots (`persistedOccupancy`/`keptSlots` ∪ `newSlots`) berechnen und an `SelectItems` übergeben | Offen | — |
| 9 | Dokumentation | `docs/help/tv-sender-ablauf-technisch.md`: Abschnitt Strang-Rotation auf fortlaufenden Zeiger aktualisieren | Offen | — |
| 10 | Dokumentation | `docs/RELEASE_NOTES.md` + `changes.log` Eintrag; `docs/help/tv-sender.md` bei Bedarf | Offen | — |
| 11 | DoD | `dotnet build VideoPlayer.sln` Debug + Release fehlerfrei; volle Testsuite `dotnet test VideoWebPlayer.Tests` grün; Rot-Nachweis der neuen Tests gegen Altstand | Offen | — |
