# Tasks: Platzhalter-Clips auf der Live-TV-Seite wie im Sendeplan zusammenfassen (Issue #328)

| # | Bereich | Aufgabe | Status | Testnachweis |
|---|---------|---------|--------|--------------|
| 1 | API/DTO | `DtoProgramGuideEntry` um `IsClipFill` (bool) und `ProgramRuleId` (long?) erweitern — die zwei Angaben, die die Sendeplan-Gruppierung braucht | Erledigt | ProgramGuideServiceTests_ClipGroups.Guide_ClipFillSlot_ExposesClipFlagAndRuleId (echtes SQLite) |
| 2 | Logik | `ProgramGuideService.ToEntry`: `IsClipFill`/`ProgramRuleId` aus dem `ProgramSlot` übernehmen (API-Vertrag bleibt: jeder Slot ein Eintrag — Gruppierung ist Anzeigesache) | Erledigt | dto. |
| 3 | UI | `LiveTv.razor`: `UpcomingDisplayItem`/`UpcomingEntryItem`/`UpcomingClipBlockItem` (≥2-Invariante wie `ScheduleClipBlockItem`) + `internal static BuildUpcomingDisplayItems` — lückenlose Clip-Fuell-Läufe derselben `ProgramRuleId` (`StartUtc == prev.EndUtc`) kollabieren zu einem Block; Grenzen: Regelwechsel, Zeitlücke, Nicht-Clip-Eintrag | Erledigt | LiveTvUpcomingDisplayItemsTests (6 Tests) |
| 4 | UI | `LiveTv.razor`: Block-Karte in „Die nächsten Stunden" — placeholder.png-Poster, Zeitraum, „Platzhalter-Clips", „{n} Clips · Gesamtdauer hh:mm:ss", Beschreibung, `data-first-slot-id`/`data-clip-count`; Icon-Button (`aria-expanded`) klappt die Einzelclips (Zeit/Dauer/Titel, Abspielreihenfolge) inline auf; `expandedClipBlockId` überlebt die 15-s-Pollings | Erledigt | LiveTvTests.Upcoming_ClipRun_RendersSingleBlockRow_WithAggregates / Upcoming_ClipBlock_Toggle_ExpandsClipListInOrder (bUnit); E2E LiveTvClipBlocksE2ETests.LiveTv_ClipRun_RendersAggregatedBlockRow_WithInlineDetail |
| 5 | UI | `LiveTv.razor.css`: Toggle-Button (`--vp-*`-Tokens, `.btn-icon` ist admin-scoped) + `.livetv-clip-block-detail`-Liste | Erledigt | E2E (sichtbar gerendert) |
| 6 | Dokumentation | `docs/help/tv-sender.md`: Clip-Füll-Abschnitt + Live-TV-Ansicht aktualisiert; `docs/API.md`: `isClipFill`/`programRuleId` im Guide-Schema; `docs/RELEASE_NOTES.md` EN+DE (alter Clip-Block-Eintrag von der überholten Einzelclip-Aussage bereinigt) | Erledigt | Kein direkter Test |
| 7 | Abschluss | `dotnet build VideoPlayer.sln` Debug + Release fehlerfrei; volle Suite `dotnet test VideoWebPlayer.Tests` grün | Erledigt | 0 Fehler Debug+Release; Suite grün (s. Commit-Beschreibung/PR) |

## Entscheidungen

- **Gruppierung clientseitig in der Seite** (nicht im `GetUpcomingAsync`): Der dokumentierte
  API-Vertrag („die laufende Sendung plus alle Sendungen … nach Start sortiert") bleibt
  unverändert; die Gruppierung ist reine Anzeige, wie im Sendeplan-Tab.
- **„Jetzt läuft"/„Gleich"-Karten unverändert**: Sie nennen die tatsächlich laufende bzw.
  nächste Sendung — die Gruppierung betrifft die Liste der kommenden Sendungen.
- **Detailansicht inline** statt Overlay-Dialog (Viewer-Seite hat kein Dialog-Pattern);
  Inhalt identisch zum Sendeplan-Dialog (Zeit, Dauer, Titel in Abspielreihenfolge).
