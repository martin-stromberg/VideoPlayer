# Plan-Gegenprüfung

## Ergebnis

**Status:** Plan vollständig

## Abgleich Akzeptanzkriterien

| Akzeptanzkriterium | Umsetzung im Plan | Testnachweis im Plan | Status |
|--------------------|-------------------|----------------------|--------|
| N1: Mehrfachauswahl Genres (GenreRotation, Repeat, HolidaySpecial, Filler) | `ProgramRuleGenre`-Join, `ProgramRuleInput.GenreIds`, Validierung, UI-Checkboxen | Validierungs-, CRUD-, E2E-Tests | Abgedeckt |
| N1: n:m-Beziehung + Migration + `OptionalRestoreTables` | Migration `AddProgramRuleGenreAndSourceLinks` mit Backfill-SQL; Backup-Registrierung | Alt-Backup-Regressionstest, Kaskade-Tests | Abgedeckt |
| N1: `ExpandGenreAsync` auf Vereinigungsmenge, Dedupe, Cursor-Semantik | Union-Expansion, Dedupe `ContentType`+`ContentId`, Cursor unverändert | Union-Dedupe-Test, Cursor-Test | Abgedeckt |
| N2: Quell-Mehrfachauswahl, leer = alle Quellen | `ProgramRuleMediaSource` pro Regel; leerer Filter = uneingeschränkt | Quell-Filter-Tests (leer/gesetzt) | Abgedeckt |
| N2: Filter zusätzlich zum Freigabe-Filter | Expansion: Quell-Filter vor `AccessContext.Allows` | Access-Tests bestehen + neue Filter-Tests | Abgedeckt |
| N3: `IsHidden` nicht wählbar | Optionsquelle filtert unsichtbare kanonische Zeilen | Options-Tests, E2E | Abgedeckt |
| N3: Alternativnamen → nur Ziel-Genre wählbar | Kanonische Auflösung wie `GetOrCreateGenresAsync` (quellbezogen, rekursiv) | Options-Test Hidden+Alt | Abgedeckt |
| N3: `StartDate`/`EndDate`-Fenster | Saisonale Sichtbarkeit analog `GetSeasonalGenresAsync` | Options-/Expansion-Test inaktives Fenster | Abgedeckt |
| N3: Expansion inkl. Alt-Zuordnungen | Rows, deren Auflösung auf gewählte kanonische Namen zeigt, liefern ihre Inhalte | Hidden+Alt-Rollup-Test | Abgedeckt |
| N3: `api/items/genres`-Entscheidung | Eigene Optionsquelle, begründet in Designentscheidungen | — | Abgedeckt |
| Live-Befund Quell-Differenzierung („Family" 1/2 versteckt, 5/6 sichtbar) | Quellabhängige Optionsliste | Quell-Differenzierungs-Test | Abgedeckt |
| Abwärtskompatibilität Alt-Regeln/Alt-Backups | Backfill-Migration + `MaterialId`-Fallback in Expansion + UI-Fallback | Alt-Backup-Test, Fallback-Test | Abgedeckt |
| Serverseitige Validierung | `ValidateRuleInputAsync`-Erweiterung, Admin-Gate | Validierungs-/Authorization-Tests | Abgedeckt |

## E2E-Abdeckung

| Benutzerfluss / Akzeptanzkriterium | Geplanter E2E-Test | Status |
|------------------------------------|--------------------|--------|
| Admin erstellt Regel mit mehreren Genres + Quell-Einschränkung, editiert/löscht (N1+N2+N3-Auswahl) | `TVSenderAdminE2ETests` — konsolidierter Genre-Test | Abgedeckt |
| Verstecktes Genre nicht in Auswahlliste (N3, UI-sichtbar) | Im selben konsolidierten Genre-E2E | Abgedeckt |

## Hinweise

- Saisonales Fenster: nur wenn **beide** Daten gesetzt sind (Semantik von `GetSeasonalGenresAsync`); einzeln gesetzte Grenze = unbegrenzt — im Plan verankert.
- `ProgramRule.MaterialId` bleibt required und wird bei Genre-Regeln weiterhin mit der ersten Genre-Id befüllt; der `MaterialId`-Fallback deckt Alt-Backups und direkt geseedete Regeln ab.
- `Genre.cs` ist Windows-1252-codiert — Datei wird nicht angefasst, kein Risiko.
