# Übersetzte Anforderung: Genre-Konfiguration vererben (N7)

Quelle: `docs/projects/task/issue-111-6145c51699f34953bf29a244634a23e2-automatisierter-tvsender/nachbesserungen.md` §N7 (Priorität: hoch, BUG)

## Fachliche Zusammenfassung

`Genre`-Zeilen werden quellbezogen (`Genre.MediaSourceId`) angelegt, aber in `GenreAdmin.razor`
**global pro Name** verwaltet (Gruppierung `GroupBy(g.Name.ToLower())`, `SaveGenre` schreibt
`IsHidden`/`StartDate`/`EndDate`/`AlternateNames` auf alle gleichnamigen Zeilen). Beim Scan legt
`MediaSourceClassifier.GetOrCreateGenresAsync` neue Zeilen jedoch immer mit `IsHidden = false`
und ohne `AlternateNames` an — neu hinzukommende Quellen oder Rescans erben die vom Admin
konfigurierte globale Sichtbarkeit und Umlenkung nicht. Live-Befund: „Family" auf Quelle 1+2
ausgeblendet → „Familie", auf Quelle 3/5/6 sichtbar ohne Alternativnamen.

Zweiter Fehler: `SaveGenre` filtert `g.Name == editGenre.Name` exakt, während die Anzeige
case-insensitiv gruppiert — bei abweichender Groß-/Kleinschreibung („family" vs. „Family")
würde die Konfiguration nicht auf alle Zeilen der Gruppe angewendet.

## Betroffene Klassen und Komponenten

- Datenmodell: `Genre` (`Name`, `MediaSourceId`, `IsHidden`, `StartDate`, `EndDate`,
  `AlternateNames`), `GenreName` (keine Schemaänderung erwartet)
- Logik: `MediaSourceClassifier.GetOrCreateGenresAsync` (Scan-Erzeugung + Alternativnamen-
  Umlenkung, rekursiv pro Quelle), `MediaMetadataEditorService.NormalizeGenresAsync`
  (zweiter Genre-Erzeugungspfad über Metadaten-Edit), ggf. neue interne Hilfsklasse für die
  quellübergreifende Konfigurationsvererbung
- UI: `GenreAdmin.razor` (`SaveGenre` Filter case-insensitiv)
- Bestandskorrektur: bestehende falsch sichtbare Zeilen (Live-DB „Family" auf Quellen 3/5/6)
  — Entscheidung über Heilungspfad (`ReloadGenres`/`CheckReloadGenres`/`DataUpgradeManager`
  vs. Admin-Neuspeicherung)
- Tests: neue Tests gegen echtes SQLite (Bestand: `MediaSourceClassifierCollection`,
  SQLite-In-Memory-Pattern aus `MediaSourceClassifierActorBackfillTests`)

## Implementierungsansatz (aus Anforderung ableitbar)

- Beim Anlegen einer neuen `Genre`-Zeile in `GetOrCreateGenresAsync` (und anderen
  Erzeugungspfaden wie `MediaMetadataEditorService.NormalizeGenresAsync`) `IsHidden`,
  `StartDate`, `EndDate` und `AlternateNames` von einem gleichnamigen Genre einer anderen
  Quelle übernehmen (Name-Vergleich case-insensitiv).
- Die Alternativnamen-Vererbung muss die Scan-Umlenkung mitziehen: geerbte `GenreName`-Einträge
  führen zur bestehenden rekursiven Auflösung auf das kanonische Genre derselben Quelle.
- `SaveGenre` in `GenreAdmin.razor`: Zeilenfilter analog zur Gruppierung case-insensitiv.
- Bestandskorrektur prüfen: falsch sichtbare Altdaten werden beim Scan-Erzeugungspfad nicht
  erneut angelegt — zu entscheiden, ob einmalige Korrektur (z. B. über
  `DataUpgradeManager`/`ReloadGenres`) nötig ist oder Admin-Neuspeicherung genügt.

## Akzeptanzkriterien (aus Anforderung)

1. Neue `Genre`-Zeile auf einer neuen Quelle für „Family"-getaggten Inhalt ist sofort
   ausgeblendet und trägt die Alternativnamen, wenn ein gleichnamiges Genre (ci) auf einer
   anderen Quelle entsprechend konfiguriert ist.
2. Inhalt landet beim kanonischen Genre derselben Quelle (Alternativname → kanonisch,
   `MovieGenres`/`TVShowGenres` zeigen auf die kanonische Zeile, `GenreNames` aktualisiert).
3. `SaveGenre` wendet die Konfiguration auf alle Zeilen der case-insensitiven Namensgruppe an.
4. Sichtbarkeitsfenster (`StartDate`/`EndDate`) wird mitvererbt.
5. Case-Varianten („family"/„FAMILY") werden als gleiches Genre behandelt.
6. Tests: verstecktes Genre + Alternativname auf Quelle 1 → Genre-Anlage auf Quelle 2 →
   neue Zeile versteckt + Alternativnamen + Inhalt beim kanonischen Genre; Case-Varianten;
   Sichtbarkeitsfenster-Vererbung.

## Konfiguration

Keine neue Konfiguration erforderlich — Verhalten folgt der bestehenden globalen
Genre-Verwaltungssemantik von `GenreAdmin`.

## Offene Fragen

- Welche Zeile gilt als Konfigurationsvorlage, wenn gleichnamige Zeilen divergieren?
  (Vorschlag: älteste konfigurierte Zeile pro Name.)
- Bestandskorrektur: einmalige Heilung vorhandener divergierter Zeilen über
  `ReloadGenres`/`DataUpgradeManager` oder manuelle Admin-Neuspeicherung?
