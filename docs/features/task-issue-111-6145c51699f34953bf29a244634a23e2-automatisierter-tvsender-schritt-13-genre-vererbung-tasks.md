# Tasks: Genre-Konfiguration vererben (N7)

| # | Bereich | Aufgabe | Status | Testnachweis |
|---|---------|---------|--------|--------------|
| 1 | Logik | `GenreGlobalConfig` anlegen (`HasCustomConfig`, `LoadConfiguredTemplatesAsync`, `ApplyConfig`) | Offen | — |
| 2 | Logik | `MediaSourceClassifier.GetOrCreateGenresAsync`: Vererbung beim Erzeugen + Heilung von Default-Zeilen | Offen | — |
| 3 | Logik | `MediaSourceClassifier.GetOrCreateGenresAsync`: geerbte Alternativnamen → rekursive Umlenkung auf kanonisches Genre derselben Quelle | Offen | — |
| 4 | Logik | `MediaSourceClassifier.GetOrCreateGenresAsync`: Zyklenschutz (`visiting`-Set) + Eigenname ci-ausschließen + Quell-Cache einmalig laden | Offen | — |
| 5 | Logik | `MediaMetadataEditorService.NormalizeGenresAsync`: Vererbung bei Neuanlage + Heilung von Default-Zeilen | Offen | — |
| 6 | UI | `GenreAdmin.SaveGenre`: case-insensitiver (getrimmter) Namensfilter | Offen | — |
| 7 | Logik | `DataUpgradeManager`: `CurrentVersion` 9 + `Upgrade_9` → `ReloadGenres` | Offen | — |
| 8 | Tests | `MediaSourceClassifierGenreInheritanceTests`: Vererbung hidden+alt + Umlenkung auf kanonisches Genre | Offen | — |
| 9 | Tests | Test: saisonales Fenster-Vererbung | Offen | — |
| 10 | Tests | Test: Case-Variante (ci-Match) | Offen | — |
| 11 | Tests | Test: Default-Bestandszeile wird geheilt; konfigurierte Zeile bleibt unangetastet; ohne Vorlage bleibt Default | Offen | — |
| 12 | Tests | Test: Alternativnamen-Zyklus terminiert | Offen | — |
| 13 | Tests | `MediaMetadataEditorServiceGenreInheritanceTests`: Erbe bei Metadaten-Edit | Offen | — |
| 14 | Tests | `GenreAdminTests` (bUnit): `SaveGenre` wirkt auf Case-Varianten | Offen | — |
