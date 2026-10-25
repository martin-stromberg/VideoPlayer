# Offene Aufgaben

Erstellt am: 2026-10-25
Abbruchgrund: Maximale Iterationsanzahl erreicht

Die folgenden Aufgaben konnten im automatisierten Zyklus nicht abgeschlossen werden
und müssen manuell oder in einem erneuten Lauf bearbeitet werden.

## Offene Planelemente

Keine — `review.md` trägt den Status `Vollständig umgesetzt` (alle 54 Tasks `Erledigt`).

## Code-Review-Befunde

- [ ] `VideoWebPlayer/Services/ProgramScheduleService.cs` — Doppelter Code: Karenzzeit-Ableitung `state.ClipGroups.Count > 0 && rule.SlotDuration is not null ? rule.PlaceholderGrace : (TimeSpan?)null` steht identisch an drei Stellen (`FlatAiringTailReproducible` Z. 598–600, `InterleavedAiringTailReproducible` Z. 675–677, `SelectItems` Z. 1453–1455). Empfehlung: als `RuleState.ClipGrace`-Property oder Helper `ClipGraceFor(RuleState)` kapseln.
- [ ] `VideoWebPlayer.Tests/TVSenderAdminClipGroupsE2ETests.cs` — Doppelter Code: „MediaSource holen oder `E2E`-Quelle anlegen" wortgleich dreimal (`SeedClipItemsAsync` Z. 76–78, `SeedMovieWithItemAsync` Z. 106–108, `GeneratedSchedule_ContainsVisibleClipSlots` Z. 302–304); Basisklasse enthält `EnsureMediaSourceIdAsync` (`PlaylistsE2ETestBase.cs:951`, `private`). Empfehlung: lokale Hilfsmethode oder Methode auf `protected` heben.

## Usability-Befunde

- [ ] `VideoWebPlayer/Components/Pages/Admin/TVSenderAdminClipGroups.razor` — Abweichendes Muster: Kandidaten-Suche bindet per `oninput` (Z. 88), wendet Suchtext/Quellenfilter aber erst auf „Suchen"-Klick (Z. 101) an; Quellen-Dropdown (Z. 92–98) reagiert nicht bei Auswahl. Etabliertes Muster: `MediaSearchSelector` sucht live mit 400-ms-Debounce. Empfehlung: `ReloadCandidatesAsync` gedebounced per `@bind:after` an Suchfeld und Quellen-`<select>`.
- [ ] `VideoWebPlayer/Components/Pages/Admin/TVSenderAdmin.razor` — Erreichbarkeit: Hinweis „Clip-Gruppen und Karenzzeit wirken nur bei gesetzter Sendeplatz-Länge" (Z. 359) verwendet einen Begriff, der als Feldbeschriftung nicht existiert — das Feld heißt „Länge (Minuten)" (`rule-duration`, Z. 243). Empfehlung: Wortlaut angleichen oder Feld umbenennen.

## Fehlgeschlagene Tests

- [ ] `PlaylistMediaSearchE2ETests.Search_NoMatches_ShowsEmptyResultsMessage` — `.media-search-empty` innerhalb 5 s nicht sichtbar (Aria-Snapshot zeigte fertige Suchseite; Timing/Last). In beiden Einzelwiederholungen bestanden → Flake außerhalb des Features, bisher nicht dokumentiert. Kein Feature-Bezug; ggf. Test-Härtung als Bestandspflege prüfen.
