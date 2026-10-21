# Tasks: Genre-Konfiguration vererben (N7)

| # | Bereich | Aufgabe | Status | Testnachweis |
|---|---------|---------|--------|--------------|
| 1 | Logik | `GenreGlobalConfig` anlegen (`HasCustomConfig`, `LoadConfiguredTemplatesAsync`, `ApplyConfig`) | Erledigt | `MediaSourceClassifierGenreInheritanceTests` (indirekt über alle Vererbungstests) |
| 2 | Logik | `MediaSourceClassifier.GetOrCreateGenresAsync`: Vererbung beim Erzeugen + Heilung von Default-Zeilen | Erledigt | `ReloadGenres_NewGenreOnOtherSource_InheritsHiddenAndAlternates_RedirectsToCanonical`, `ReloadGenres_ExistingDefaultRow_HealsFromTemplate` |
| 3 | Logik | `MediaSourceClassifier.GetOrCreateGenresAsync`: geerbte Alternativnamen → rekursive Umlenkung auf kanonisches Genre derselben Quelle | Erledigt | `ReloadGenres_NewGenreOnOtherSource_InheritsHiddenAndAlternates_RedirectsToCanonical` (MovieGenres → „Familie"-Zeile Quelle 2) |
| 4 | Logik | `MediaSourceClassifier.GetOrCreateGenresAsync`: Zyklenschutz (`visiting`-Set) + Eigenname ci-ausschließen + Quell-Cache einmalig laden | Erledigt | `ReloadGenres_AlternateNameCycle_Terminates` |
| 5 | Logik | `MediaMetadataEditorService.NormalizeGenresAsync`: Vererbung bei Neuanlage + Heilung von Default-Zeilen | Erledigt | `MediaMetadataEditorServiceGenreInheritanceTests.UpdateAsync_NewGenre_InheritsGlobalConfig` |
| 6 | UI | `GenreAdmin.SaveGenre`: case-insensitiver (getrimmter) Namensfilter | Erledigt | `GenreAdminTests.SaveGenre_AppliesConfigToCaseVariants` |
| 7 | Logik | `DataUpgradeManager`: `CurrentVersion` 9 + `Upgrade_9` → `ReloadGenres` | Erledigt | Kein direkter Test (ruft `ReloadGenres`, das vollständig getestet ist; `EnsureUpToDateAsync`-Mechanik unverändert) |
| 8 | Tests | `MediaSourceClassifierGenreInheritanceTests`: Vererbung hidden+alt + Umlenkung auf kanonisches Genre | Erledigt | selbst |
| 9 | Tests | Test: saisonales Fenster-Vererbung | Erledigt | `ReloadGenres_NewGenre_InheritsSeasonalWindow` |
| 10 | Tests | Test: Case-Variante (ci-Match) | Erledigt | `ReloadGenres_CaseVariant_InheritsFromCaseInsensitiveMatch` |
| 11 | Tests | Test: Default-Bestandszeile wird geheilt; konfigurierte Zeile bleibt unangetastet; ohne Vorlage bleibt Default | Erledigt | `ReloadGenres_ExistingDefaultRow_HealsFromTemplate`, `ReloadGenres_ExistingConfiguredRow_IsNotOverwritten`, `ReloadGenres_NoConfiguredTemplate_CreatesDefaultGenre` |
| 12 | Tests | Test: Alternativnamen-Zyklus terminiert | Erledigt | `ReloadGenres_AlternateNameCycle_Terminates` |
| 13 | Tests | `MediaMetadataEditorServiceGenreInheritanceTests`: Erbe bei Metadaten-Edit | Erledigt | selbst |
| 14 | Tests | `GenreAdminTests` (bUnit): `SaveGenre` wirkt auf Case-Varianten | Erledigt | selbst |
