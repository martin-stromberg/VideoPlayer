# Plan-Review

## Ergebnis

**Status:** Vollständig umgesetzt

## Umgesetzte Planelemente

- [x] `GenreGlobalConfig` (interne statische Hilfsklasse, `VideoWebPlayer/Services/GenreGlobalConfig.cs`) — angelegt
- [x] Methode `HasCustomConfig` in `GenreGlobalConfig` — vorhanden (IsHidden / Saisonfenster / effektive Alternativnamen via `GenreCanonicalResolver`)
- [x] Methode `LoadConfiguredTemplatesAsync` in `GenreGlobalConfig` — vorhanden (ci-Key, `MinBy(Id)` der konfigurierten Zeilen anderer Quellen)
- [x] Methode `ApplyConfig` in `GenreGlobalConfig` — vorhanden (kopiert IsHidden/Daten, ersetzt `AlternateNames` EF-konform für neue und getrackte Zeilen)
- [x] `MediaSourceClassifier.GetOrCreateGenresAsync` — umgebaut auf `GenreResolutionState` + `ResolveGenresAsync` + `FindConfiguredTemplateAsync`; Quell-Ladung einmalig, Vorlagen lazy, Vererbung bei Erzeugung (Schritt 2a), Heilung von Default-Zeilen (Schritt 2b), einheitliche Alternativnamen-Umlenkung incl. geerbter Namen, `visiting`-Zyklenschutz, Dedupe nach Id erhalten
- [x] `MediaMetadataEditorService.NormalizeGenresAsync` — `Include(AlternateNames)`, Vererbung bei Neuanlage, Heilung von Default-Zeilen (lazy Vorlagen-Dictionary)
- [x] `GenreAdmin.SaveGenre` — ci+Trim-Filter auf alle Zeilen (deckungsgleich mit `ReloadGenres`-Gruppierung)
- [x] `DataUpgradeManager.CurrentVersion` 8→9, `case 9` → `Upgrade_9` → `ReloadGenres`
- [x] `GenreCanonicalResolver.EffectiveAlternateNames` — von `private` auf `internal` gehoben (Wiederverwendung im Classifier)
- [x] Test `MediaSourceClassifierGenreInheritanceTests` (7 Fälle: Vererbung+Umlenkung, Saisonfenster, Case-Variante, Heilung, Nicht-Überschreiben, ohne Vorlage, Zyklus)
- [x] Test `MediaMetadataEditorServiceGenreInheritanceTests.UpdateAsync_NewGenre_InheritsGlobalConfig`
- [x] Test `GenreAdminTests.SaveGenre_AppliesConfigToCaseVariants` (bUnit, SQLite, Admin-Claims, Bearbeiten→Checkbox→Speichern-Klickpfad)

## Hinweise

- Gegenprobe durchgeführt: Vererbung im Erzeugungspfad kurzzeitig deaktiviert → 3 der
  Classifier-Tests schlugen fehl; danach wiederhergestellt (verifiziert).
- `Upgrade_9` ohne dedizierten Test (benötigt `UserManager`-Infra) — der geprüfte Kern
  (`ReloadGenres`) ist durch die neuen Tests abgedeckt.
