# Code-Review

## Ergebnis

**Status:** Befunde vorhanden

## Befunde

### TVSenderAdmin.razor (TVSenderAdmin)

- **Doppelter Code** — `MaterialSummaryLabel` (Zeilen 2174–2194) dupliziert die
  Genre-Auflösung aus `MaterialLabel(ProgramRule)` (Zeilen 2160–2171): Beide Methoden
  implementieren unabhängig voneinander dieselbe Fachregel „Genre-Regel ohne
  `Genres`-Join-Zeilen → `MaterialId` in `DbContext.Genres` nachschlagen → sonst
  „Genre (nicht gefunden)"“. Die beiden Varianten (`FirstOrDefault` vs. `Any`,
  Namen vs. Zähler) können künftig auseinanderlaufen. Zusätzlich führt die neue
  Methode — wie schon die alte — eine synchrone EF-Abfrage pro gerenderter Zeile
  während des Renderings aus.

  Empfehlung: Die Genre-Auflösung (ermittelte Genre-Anzahl bzw. „nicht gefunden")
  in eine gemeinsame Hilfsmethode auslagern, die beide Labels nutzen; besser noch:
  die Auflösung einmal beim Laden der Regeln (analog `ruleDiagnoses`) vorbereiten,
  statt im Render-Pfad synchron auf `DbContext` zuzugreifen.

- **Inkonsistente Textbildung** — `MaterialSummaryLabel`, Zeile 2185:
  `$"Genre: {genreCount} Genres"` verwendet fest die Pluralform und erzeugt im
  Fallback-Zweig (`genreCount = 1`) „Genre: 1 Genres". Im selben Label wird für
  Quellen und Clip-Gruppen das „(n)"-Schema verwendet (`1 Quelle(n)`,
  `1 Clip-Gruppe(n)`), für Genres aber nicht; außerdem steht das Wort „Genre"
  doppelt im Text.

  Empfehlung: Einheitlich `$"Genre: {genreCount} Genre(n)"` (oder Label ohne
  doppelten Begriff, z. B. `"{genreCount} Genre(n)"`).

- **Markup/Robustheit** — Meta-Zeile Zeile 163: Die vier Bestandteile werden mit
  literalem `·` verkettet, `DurationLabel(rule)` kann jedoch einen Leerstring
  liefern (weder `EpisodeCount` noch `SlotDuration` gesetzt — zwar per
  Service-Validierung ausgeschlossen, aber für Alt-/korrupte Daten möglich). Die
  Zeile endet dann mit einem hängenden „· ". Die alte Tabellenzelle zeigte in dem
  Fall schlicht nichts an.

  Empfehlung: Die Bestandteile als Liste zusammensetzen und mit `string.Join`
  verbinden oder den letzten Trenner konditional nur bei nicht-leerem
  `DurationLabel` ausgeben.

- **Fehlende Testabdeckung** — Der neue Zweig „Genre (nicht gefunden)"
  (`MaterialSummaryLabel`, Zeilen 2181–2182) wird von keinem Test geprüft: Es gibt
  keinen Test mit einer Genre-Regel ohne `Genres`-Join-Zeilen, deren `MaterialId`
  nicht (mehr) existiert. Ebenso ungetestet ist der Legacy-Fall `genreCount = 1`
  über `MaterialId`.

  Empfehlung: bUnit-Test mit Genre-Regel ohne `Genres`-Einträge ergänzen — einmal
  mit existierendem `MaterialId`-Genre („Genre: 1 Genre(n)") und einmal mit
  verweistem Material („Genre (nicht gefunden)").

### TVSenderAdminTests.cs (TVSenderAdminTests)

- **Testqualität: Implementierungsdetail statt Verhalten** —
  `RulePanel_StatusBadge_BeforeName` (Zeile ~349) prüft die visuelle Reihenfolge
  per `InnerHtml.IndexOf("rule-panel-badges") < IndexOf("rule-panel-name")` auf dem
  serialisierten Markup-String — ohne Assert-Meldung und empfindlich gegenüber
  Vorkommen der Klassennamen außerhalb der Elemente (z. B. in Attributwerten).

  Empfehlung: Reihenfolge über das DOM prüfen, z. B.
  `row.QuerySelector(".rule-panel-head")!.Children` auf die Reihenfolge der
  Elemente mit `.rule-panel-badges` / `.rule-panel-name` untersuchen oder
  `CompareDocumentPosition` nutzen, und das `Assert.True` mit einer aussagekräftigen
  Meldung versehen.

- **Testqualität: zwei Fälle in einer Methode** —
  `RulePanel_PriorityBadge_HiddenWhenZero_ShownWhenSet` (Zeilen ~306–328) prüft
  zwei fachlich getrennte Fälle: „Badge ausgeblendet bei Priorität 0" und
  „Badge zeigt Wert bei Priorität > 0".

  Empfehlung: In zwei Testmethoden aufteilen
  (`..._HiddenWhenZero` / `..._ShownWhenSet`), damit ein Fehler dem jeweiligen
  Fall eindeutig zugeordnet werden kann.

## Geprüfte Dateien

- `VideoWebPlayer/Components/Pages/Admin/TVSenderAdmin.razor`
- `VideoWebPlayer/wwwroot/app.css`
- `VideoWebPlayer.Tests/Components/TVSenderAdminTests.cs`
- `VideoWebPlayer.Tests/Components/TVSenderAdminTests_ClipGroups.cs`
- `VideoWebPlayer.Tests/TVSenderAdminE2ETests.cs`
- `VideoWebPlayer.Tests/TVSenderAdminClipGroupsE2ETests.cs`
- `docs/help/tv-sender.md`
