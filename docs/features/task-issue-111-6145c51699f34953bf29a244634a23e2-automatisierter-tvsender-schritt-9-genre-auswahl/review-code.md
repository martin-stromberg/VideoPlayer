# Code-Review — Schritt 9

*Selbstreview des Implementierers; fachliche Abnahme durch zweiten Agenten gemäß AGENTS.md §4.*

## Architektur

- n:m-Joins `ProgramRuleGenres` / `ProgramRuleMediaSources` mit Composite-Key,
  `HasPrincipalKey`-FKs und `Cascade`-Löschen; Migration backfillt `MaterialId` → Join.
- `MaterialId` bleibt Kompatibilitätsspalte (erstes Genre, Fallback für Altbestand/Alt-Backup).
- `GenreCanonicalResolver` (intern, `Services`) kapselt: Sichtbarkeit (`IsHidden` + saisonales
  Fenster analog `GenreService`), `HasEffectiveAlternates` (nur wenn auflösbarer Name),
  `ResolveCanonicalNames` (rekursiv, zyklussicher, quellbezogen), `BuildOptions` (Dedupe nach
  kanonischem Namen, Repräsentant = kleinste sichtbare kanonische Id).
- Sender-eigene Optionsquelle `GetGenreMaterialOptionsAsync` in `IProgramRuleService`;
  `api/items/genres` bleibt bewusst unverändert (Metadaten-/Playlist-Editoren nutzen rohe Namen).
- `ExpandGenreAsync` (ProgramScheduleService): Stored-Genres → kanonische Namen → alle
  In-Scope-Genre-Rows (Quell-Filter) mit Überschneidung → vereinigte Movie-/Show-Links →
  Quell-Filter zusätzlich auf Inhalts-`MediaSourceId` → Dedupe pro Inhalt → bestehende
  Access-/Dauer-Filter + Cursor-Semantik über der Union unverändert.
- Serverseitige Validierung: Genre verlangt ≥1 GenreId; Genre-/Quell-Listen bei anderem
  Material werden abgewiesen; unbekannte Ids → `KeyNotFoundException`; Admin-Pflicht auf
  allen neuen/veränderten Pfaden.

## Geprüfte Risiken / Befunde

- **Alt-Backup**: Join-Fallback auf `MaterialId` sowohl in Expansion als auch UI/Label —
  regressionstestiert (Backup ohne beide Tabellen restauriert, Regel funktioniert).
- **Zyklus-Schutz**: `ResolveCanonicalNames` bricht bei Selbst-/Ring-Referenzen ab (visited-Set).
- **Quell-Differenzierung**: gleiche kanonische Namen auf unterschiedlichen Quellen bleiben
  unterscheidbar (Live-Befund „Familie"/„Family" wird durch Options- und Expansionstests
  abgedeckt).
- **Dedupe**: Inhalt in mehreren gewählten Genres erscheint genau einmal pro Union
  (getestet inkl. Film in zwei Genres).
- **Performanz**: Expansion lädt Genre-Rows/Links über `IN`-Filter statt N+1; Optionsliste
  lädt Genres einmal inkl. `AlternateNames`.
- Keine bekannten Warnungen neu eingeführt (xUnit1051-Stellen behoben); XML-Dok vollständig.

## Hinweise an den Prüfer

- Bewusste Entscheidung: leere Genre-Auswahl wird serverseitig abgelehnt (siehe requirement.md,
  Auflösung der Begriffskollision „MaterialId für Genre-Regeln").
- `ProgramRuleCursor` läuft über den deduplizierten Union — unverändert, getestet über
  bestehende Cursor-/Wrap-Tests plus neue Union-Reihenfolgetests.
