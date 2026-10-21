# Plan-Gegenprüfung

## Ergebnis

**Status:** Plan vollständig

## Abgleich Akzeptanzkriterien

| Akzeptanzkriterium | Umsetzung im Plan | Testnachweis im Plan | Status |
|--------------------|-------------------|----------------------|--------|
| Neue `Genre`-Zeile erbt `IsHidden`/`StartDate`/`EndDate`/`AlternateNames` von gleichnamigem Genre (ci) anderer Quellen | `GenreGlobalConfig` + Umbau `GetOrCreateGenresAsync` (Schritte 1–2) + `NormalizeGenresAsync` (Schritt 3) | `ReloadGenres_NewGenreOnOtherSource_InheritsHiddenAndAlternates_RedirectsToCanonical`, `ReloadGenres_NewGenre_InheritsSeasonalWindow` | Abgedeckt |
| Alternativnamen-Vererbung zieht Scan-Umlenkung mit (alternativer Name → kanonisches Genre derselben Quelle) | Umstrukturierung: Erzeugen → einheitliche Rekursionsbehandlung (Programmablauf 1, Schritt 3) | `ReloadGenres_NewGenreOnOtherSource_InheritsHiddenAndAlternates_RedirectsToCanonical` prüft `MovieGenres`→„Familie" + `GenreNames`-Rückschreibung | Abgedeckt |
| `SaveGenre`-Filter case-insensitiv wie Gruppierung | Schritt 4 (in-memory ci+Trim) | `GenreAdminTests.SaveGenre_AppliesConfigToCaseVariants` (bUnit, echte UI-Interaktion + SQLite) | Abgedeckt |
| Bestandskorrektur: Entscheidung + Begründung | Designentscheidung: `Upgrade_9` → `ReloadGenres` heilt Default-Zeilen aller Quellen einmalig; Admin-Neuspeicherung als zusätzlicher Pfad; konfigurierte divergente Zeilen (Case-Bug-Artefakte) heilt die nächste Speicherung (ci) | `ReloadGenres_ExistingDefaultRow_HealsFromTemplate` | Abgedeckt |
| Case-Varianten | ci-Suche in `LoadConfiguredTemplatesAsync` + ci-Filter `SaveGenre` | `ReloadGenres_CaseVariant_InheritsFromCaseInsensitiveMatch`, `GenreAdminTests.SaveGenre_AppliesConfigToCaseVariants` | Abgedeckt |
| Sichtbarkeitsfenster-Vererbung | `ApplyConfig` kopiert `StartDate`/`EndDate` | `ReloadGenres_NewGenre_InheritsSeasonalWindow` | Abgedeckt |

## E2E-Abdeckung

| Benutzerfluss / Akzeptanzkriterium | Geplanter E2E-Test | Status |
|------------------------------------|--------------------|--------|
| Genre in Admin bearbeiten/speichern (Case-Varianten) | `GenreAdminTests` (bUnit: gerenderte Seite, Admin-Auth, SQLite, Klickpfad Bearbeiten→Speichern) | Abgedeckt — bUnit ist das im Projekt etablierte Komponenten-Testmuster; kein neuer UI-Flow, Playwright ohne Mehrwert |
| Scan-Vererbung (Backend-Pfad) | `ReloadGenres`-Integrationstests fahren exakt den produktiven Pfad (`CheckReloadGenres`/`Upgrade_9` → `ReloadGenres` → `GetOrCreateGenresAsync`) | Nicht erforderlich — kein UI-Fluss; der produktive Codepfad ist abgedeckt |

## Hinweise

- `Upgrade_9` hat keinen dedizierten Test (benötigt `UserManager`-Infra); geprüfter
  Kern (`ReloadGenres`) ist abgedeckt — im Implementierungsreview als „Kein direkter
  Test" markieren.
- `ApplyConfig` muss für neue (Id=0, Navigation-Collection) und getrackte Zeilen
  (Delete+Insert der `GenreName`-Zeilen) korrekt arbeiten — beim Implementieren auf
  EF-Tracking achten.
- Performance: lazy Vorlagen-Ladung einmal pro `GetOrCreateGenresAsync`-Aufruf;
  quellbezogene Zeilen werden künftig einmal statt pro Name geladen.
