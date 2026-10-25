# Code-Review

Basisbranch: `staging` (`git merge-base HEAD staging` = `3bbbd490`). Geprüft wurden alle im Branch geänderten und neuen Quelldateien inklusive der uncommitteten/untracked Dateien; Dokumentation (docs/, README.md, RELEASE_NOTES) wurde ausgenommen. `dotnet build` für `VideoWebPlayer` und `VideoWebPlayer.Tests` läuft fehlerfrei (0 Warnungen, 0 Fehler).

## Ergebnis

**Status:** Befunde vorhanden

## Befunde

### VideoWebPlayer/Services/ProgramScheduleService.cs

- **God-Methode** — `InsertClipFill` (Zeilen 2502–2608, ~107 Zeilen) erledigt mehrere konzeptuell getrennte Aufgaben hintereinander: Airing-Gruppierung, Rest-Budget-Verteilung über n+1 Segmente, Tail-Limit-Bestimmung (blocked + boundStarts), Clip-Platzierung, Neuverlegung der Titel-Slots und Wiederaufbau der `blocked`-Liste.

  Empfehlung: Auslagern in private Methoden, z. B. `TailLimit(...)` für die Grenzberechnung (2539–2546) und `FillAiringSegments(...)` für die Segment-Schleife (2555–2599).

- **Doppelter Code** — `RewindCursor` (Zeilen 866–892): Der neue Clip-Ring-Block ist eine nahezu wortgleiche Kopie der Strang-Schleife direkt darüber (838–864). Einzige Unterschiede: Indexauflösung (`FindItemIndex` vs. `FindIndex` auf `MediaItemId`) und Cursor-Key (`strand.Key` vs. `clipGroup.StrandKey`).

  Empfehlung: Gemeinsame private Hilfsmethode, z. B. `RewindRing(StrandState ring, string? key, List<ProgramSlot> slots, Func<ProgramSlot, int> indexOf, IReadOnlyList<ProgramRuleCursor> cursorRows)`, die beide Schleifen aufrufen.

- **Doppelter Code** — `ExpandClipGroupItemsAsync` (Zeilen 2393–2445) dupliziert die komplette Clip-Filterpipeline aus `ExpandMediaSourceClipsAsync` (Zeilen 2176–2225): `IsVideoFileName`-Check, Exklusion film-/episodenverlinkter Items, `summary.Candidates`/`CountNoDuration`/`SkippedNoAccess`-Zählung und `SchedulableItem`-Konstruktion sind identisch; nur die Kandidatenquelle (Join-Tabelle statt Quell-Filter) und die Access-SourceId unterscheiden sich.

  Empfehlung: Die Filterpipeline in eine gemeinsame Methode auslagern (Kandidatenliste + `AccessContext` + `SummaryCounters` als Parameter), die beide Expansionen aufrufen.

- **Long Parameter List** — Die neuen privaten Hilfsmethoden `BuildClipGroupStates` (7 Parameter, Zeile 2347), `ExpandClipGroupStatesAsync` (7 Parameter, Zeile 2376) und `InsertClipFill` (7 Parameter, Zeile 2502) tragen jeweils `(states/cursors, access, airingsByMovie, coverageStart/boundStarts, horizonEnd, summary)` weiter. Die Datei nutzt für genau dieses Problem bereits das Bündel-Muster (`ReproContext`, Zeile 484).

  Empfehlung: Parameterobjekt (analog `ReproContext`) für den Clip-Fill-Kontext einführen oder an vorhandene Bündel anhängen.

### VideoWebPlayer/Services/ProgramClipGroupService.cs

- **Fehlerbehandlung / Eingabevalidierung** — `GetClipCandidatesAsync` (Zeile 119): `EF.Functions.Like(mi.Name, $"%{search.Trim()}%")` verwendet den Nutzer-Suchtext unmaskiert als LIKE-Pattern — `%`, `_` und `\` wirken als Wildcards. Die bestehende Codebasis escaped diese Zeichen (`ItemsController.ApplySearchFilter`, `ItemsController.cs:230–232`: `Replace("\\","\\\\").Replace("%","\\%").Replace("_","\\_")` mit Escape-Zeichen `"\\"`).

  Empfehlung: Gleiches Escaping wie in `ApplySearchFilter` anwenden (ggf. als gemeinsame Hilfsmethode).

### VideoWebPlayer/Services/Backups/VideoWebPlayerBackupData.cs

- **Doppelter Code** — Die neue Schleife über `OptionalRestoreTimeSpanDefaults` (Zeilen 1013–1024) ist die vierte identische Kopie des Default-Injektionsblocks in derselben Methode (gleiches `continue`-Kriterium, gleiches `insertColumns.Add` + `SerializeToElement`); davor existieren bereits drei wortgleiche Schleifen für andere Typen.

  Empfehlung: Eine typunspezifische Tabelle `(string Table, string Column, object DefaultValue)` oder eine lokale Funktion `InjectDefault(tableName, columnName, defaultValue)`, die alle vier Schleifen ersetzt.

### VideoWebPlayer/Components/Pages/Admin/TVSenderAdmin.razor

- **Hardcodierter Wert** — `RuleEditModel.GraceMinutes { get; set; } = 15` (Zeile 2090) dupliziert den Default aus `ProgramRule.DefaultPlaceholderGrace` (15 Minuten). Ändert sich die Konstante, läuft der UI-Default auseinander.

  Empfehlung: `= (int)ProgramRule.DefaultPlaceholderGrace.TotalMinutes` verwenden.

- **Fehlende Kapselung / ineffiziente Abfrage** — `ReloadClipGroupsAsync` (Zeilen 1801–1806) lädt pro Gruppe die komplette Member-Liste via `GetGroupClipsAsync` (inkl. Joins auf `MediaCollection`/`MediaSource`), nur um `.Count` daraus zu zählen — N+1-Abfragen mit voller Projektion.

  Empfehlung: Dedizierte Count-Abfrage im Service (z. B. `GetGroupCountsAsync` mit einer einzigen `GroupBy`-Query statt n Listenabfragen).

- **Überflüssige Abfrage** — `SelectGroupAsync` (Zeilen 1862–1869) ruft `ReloadCandidatesAsync` auch für `IsSystem`-Gruppen auf, obwohl der Kandidaten-Picker für Systemgruppen nicht gerendert wird; `GetClipCandidatesAsync` materialisiert dabei alle unklassifizierten MediaItems.

  Empfehlung: `ReloadCandidatesAsync` nur aufrufen, wenn `!group.IsSystem`, bzw. in `ReloadCandidatesAsync` auf `selectedGroup.IsSystem` prüfen.

- **God-Komponente** — Der neue „Clip-Gruppen"-Tab (Tabellen, Member-Verwaltung, Kandidaten-Picker, zwei Dialoge, ~10 Handler) fügt der ohnehin ~2100 Zeilen großen Seite eine fünfte klar abgrenzbare fachliche Verantwortlichkeit hinzu (Regeln, Sendeplan, Einstellungen, Laufzeitermittlung, Clip-Gruppen).

  Empfehlung: Den Clip-Gruppen-Bereich (Markup + Code-Behind-Logik) in eine Kind-Komponente (z. B. `TVSenderAdminClipGroups.razor`) auslagern und nur die Datenübergabe in der Seite belassen.

### VideoWebPlayer.Tests/ProgramScheduleServiceTests_ClipGroups.cs

- **Toter Code / falsche Nullbarkeit** — `SeedClipAiringAsync` (Zeilen 25, 49): Das Tupel-Element ist als `ProgramClipGroup group` (non-nullable) deklariert, liefert bei `withGroup: false` aber `group!` auf `null`. Die `!`-Unterdrückung versteckt einen tatsächlichen Null-Rückgabewert.

  Empfehlung: Rückgabetyp `ProgramClipGroup?` deklarieren (Aufrufer verwenden ohnehin `_` bzw. nicht-null-Kontexte).

- **Toter Code** — Ungenutzte Variablen: `movie` in `EnsureCoverage_GraceRemainder_FillsWithDistributedClips` (Zeile 62, destrukturiert, aber nie referenziert) und `user` in `EnsureCoverage_ClipGroup_NotFullySharedSource_YieldsNoClipSlots` (Zeile 192, nur der Seed-Nebeneffekt zählt).

  Empfehlung: `_`-Discard verwenden (`var (rule, _, _, clips)`, `await fixture.AddUserAsync(...)` ohne Zuweisung).

- **Einheitlichkeit** — Der Datei-lokale Helper `LocalUtc(int h, int mi = 0, int d = 24)` (Zeile 18) weicht in Name und Semantik von den gleichnamigen Helpers aller anderen `ProgramScheduleServiceTests_*`-Dateien ab (`LocalUtc(int y, int mo, int d, int h, int mi)` — andere Parameterreihenfolge). Beim Verschieben/Kopieren von Tests zwischen den Dateien entstehen vertauschte Argumente.

  Empfehlung: Signatur an die Schwestern-Dateien angleichen (Jahr/Monat/Tag explizit) oder den Helper in die Fixture `ProgramScheduleTestDb` heben.

### VideoWebPlayer.Tests/ProgramClipGroupServiceTests.cs

- **Testqualität** — `Candidates_OnlyUnclassifiedVideos` (Zeilen 169–184): Die Seeds `noDur` (Zeile 175, Item ohne Laufzeit) und `show` (Zeile 178, episodenverlinktes Item) werden angelegt, aber nie in Assertions geprüft — weder dass `noDur` als Kandidat gelistet wird (laut Interface-Doku sollen Items ohne Laufzeit gelistet sein), noch dass das Episoden-Item ausgeschlossen wird. Nur die Movie-Verlinkung wird verifiziert.

  Empfehlung: `Assert.Contains(candidates, c => c.MediaItemId == noDur.Id)` und ein `Assert.DoesNotContain` für das Episoden-MediaItem ergänzen — oder die Seeds entfernen.

- **Toter Code** — Ungenutzte Variablen `noDur` (Zeile 175) und `show` (Zeile 178), siehe vorigen Befund: ohne Assertions bleiben beide deklariert, aber unverwendet.

### VideoWebPlayer.Tests/Services/Backups/VideoWebPlayerBackupDataTests_ClipGroups.cs

- **Toter Code** — `userId` aus `LegacyBackupArchiveBuilder.CreateSeededBackupAsync` wird in beiden Tests destrukturiert (Zeilen 21, 45), aber nie verwendet.

  Empfehlung: `var (db, backup, _)` als Discard destrukturieren.

### VideoWebPlayer.Tests/TVSenderAdminClipGroupsE2ETests.cs

- **Toter Code** — Zeile 130: `Page.Locator("label[for='rule-clip-group-" + "']")` konkateniert ein leeres String-Literal; der entstehende Selektor `label[for='rule-clip-group-']` matcht kein Element (alle Labels tragen eine Gruppen-Id im `for`-Attribut), sodass die Assertion de facto nur `.rule-clip-group-option` prüft.

  Empfehlung: Auf `Page.Locator(".rule-clip-group-option")` vereinfachen.

## Geprüfte Dateien

Geändert:
- `VideoWebPlayer/Components/Pages/Admin/TVSenderAdmin.razor`
- `VideoWebPlayer/Data/ApplicationDbContext.cs`
- `VideoWebPlayer/Data/Configurations/ProgramRuleConfiguration.cs`
- `VideoWebPlayer/Data/ProgramRule.cs`
- `VideoWebPlayer/Extensions/ServiceCollectionExtensions.cs`
- `VideoWebPlayer/Migrations/ApplicationDbContextModelSnapshot.cs`
- `VideoWebPlayer/Services/Backups/VideoWebPlayerBackupData.cs`
- `VideoWebPlayer/Services/ProgramRuleInput.cs`
- `VideoWebPlayer/Services/ProgramRuleService.cs`
- `VideoWebPlayer/Services/ProgramScheduleService.cs`
- `VideoWebPlayer.Tests/Components/TVSenderAdminTests.cs`
- `VideoWebPlayer.Tests/Helpers/ProgramScheduleTestDb.cs`
- `VideoWebPlayer.Tests/ProgramRuleCursorNullStrandKeyMigrationTests.cs`
- `VideoWebPlayer.Tests/TVSenderAdminE2ETests.cs`

Neu:
- `VideoWebPlayer/Data/ProgramClipGroup.cs`
- `VideoWebPlayer/Data/ProgramClipGroupItem.cs`
- `VideoWebPlayer/Data/ProgramRuleClipGroup.cs`
- `VideoWebPlayer/Data/Configurations/ProgramClipGroupConfiguration.cs`
- `VideoWebPlayer/Data/Configurations/ProgramClipGroupItemConfiguration.cs`
- `VideoWebPlayer/Data/Configurations/ProgramRuleClipGroupConfiguration.cs`
- `VideoWebPlayer/Migrations/20261025112747_AddProgramClipGroups.cs`
- `VideoWebPlayer/Migrations/20261025112747_AddProgramClipGroups.Designer.cs`
- `VideoWebPlayer/Services/IProgramClipGroupService.cs`
- `VideoWebPlayer/Services/ProgramClipGroupService.cs`
- `VideoWebPlayer.Tests/Components/TVSenderAdminTests_ClipGroups.cs`
- `VideoWebPlayer.Tests/ProgramClipGroupServiceTests.cs`
- `VideoWebPlayer.Tests/ProgramGuideServiceTests_ClipGroups.cs`
- `VideoWebPlayer.Tests/ProgramRuleServiceTests_ClipGroups.cs`
- `VideoWebPlayer.Tests/ProgramScheduleServiceTests_ClipGroups.cs`
- `VideoWebPlayer.Tests/Services/Backups/VideoWebPlayerBackupDataTests_ClipGroups.cs`
- `VideoWebPlayer.Tests/TVSenderAdminClipGroupsE2ETests.cs`
