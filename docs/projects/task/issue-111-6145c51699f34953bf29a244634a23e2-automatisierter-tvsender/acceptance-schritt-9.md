# Abnahme Schritt 9: Genre-Material — Mehrfachauswahl, Quell-Filter, Status/Alternativnamen (N1+N2+N3)

Prüfer: Devin (unabhängiger Lauf, nicht der Implementierer)
Datum: 2026-10-21
Geprüfter Stand: `4947b2c` auf `task-issue-111-...-schritt-9-genre-auswahl`

## Ergebnis

**Status:** Erfüllt

Alle drei Nachbesserungen sind fachlich und technisch korrekt umgesetzt; der Implementierer-Bericht
(Join-Tabellen, `GenreCanonicalResolver`, `GetGenreMaterialOptionsAsync`, deduplizierte Union in
`ExpandGenreAsync`, Checkbox-Mehrfachauswahl, Migration inkl. Backfill, neue Tests) wurde anhand des
Codes und eigener Gegenproben verifiziert.

### Geprüfte Kernpunkte

- **N1 Mehrfachauswahl (n:m):** `ProgramRuleGenre` (`VideoWebPlayer/Data/ProgramRuleGenre.cs`) als
  echte Join-Tabelle mit Composite-PK `(ProgramRuleId, GenreId)` und Cascade-FKs beidseitig
  (`Data/Configurations/ProgramRuleGenreConfiguration.cs`); `MaterialId` bleibt als
  Kompat-Fallback (`ProgramRuleService.MaterialIdFor`, `ExpandGenreAsync` Z. 1026–1028;
  `TVSenderAdmin` Label-/Edit-Fallback). Serverseitig persistiert via
  `ReplaceGenreLinksAsync` (ProgramRuleService Z. 175–189) — kein UI-Versteck.
- **N2 Quell-Filter:** `ProgramRuleMediaSource`-Join (Composite-PK, Cascade) schränkt in
  `ExpandGenreAsync` sowohl die berücksichtigten Genre-Zeilen (Z. 1030–1034) als auch die
  Inhalte zusätzlich ein (`Movie.MediaSourceId` Z. 1070, `TVShowEpisode.MediaSourceId` Z. 1084);
  leerer Filter = alle Quellen. Filter wirkt zusätzlich zur bestehenden Freigabe-Prüfung
  (`access.Allows` unverändert).
- **N3 kanonische Auflösung:** `GenreCanonicalResolver` (`Services/GenreCanonicalResolver.cs`)
  löst Alternativnamen quellbezogen (Gruppierung nach `MediaSourceId`) rekursiv auf; Sichtbarkeit
  = `!IsHidden` + saisonales Fenster — identische Semantik zu
  `GenreService.GetSeasonalGenresAsync` (Z. 50–63, gleiche Jahres-Rollierung). `BuildOptions`
  liefert nur sichtbare, nicht-weiterleitende Zeilen, dedupliziert nach Name (deterministische
  min-Id). `api/items/genres`/`MediaMetadataEditorService.GetGenreOptionsAsync` bewusst
  unverändert (separate Sender-Optionsquelle `GetGenreMaterialOptionsAsync`, Admin-gated).
- **Alt-Zuordnungen:** Inhalte auf versteckten Genre-Zeilen, deren Alternativname auf ein
  gewähltes kanonisches Genre zeigt, fließen über `effectiveGenreIds` (Z. 1055–1058) in die
  Union ein — durch `EnsureCoverage_HiddenGenreWithAlternate_ItemsRollUpToCanonical` belegt.
- **Migration:** `20261021131012_AddProgramRuleGenreAndSourceLinks` — Composite-PKs, beidseitige
  Cascade, Index auf der jeweiligen Zweitspalte, Backfill `MaterialType = 3 (= Genre)` → Join mit
  Existenzfilter auf `Genres`; `Down` sauber. Model-Snapshot konsistent.
- **Backup:** Beide Tabellen in `OptionalRestoreTables` (`VideoWebPlayerBackupData.cs`);
  Alt-Backup-Regressionstest (Backup ohne beide Tabellen restauriert fehlerfrei, `MaterialId`
  bleibt tragend) + Roundtrip-Test vorhanden.
- **Validierung:** Genre-Material erfordert ≥1 Genre-Id; Genre-/Quell-Listen bei anderem Material
  → `ArgumentException`; unbekannte Ids → `KeyNotFoundException`; `RequireAdminAsync` auf allen
  Service-Pfaden inkl. Options-API.
- **Zyklen:** `visiting`-Set in `ResolveNames` (branch-lokale Kopien `[.. visiting]`) — durch
  ad-hoc-Gegenbeispiel-Test verifiziert: A→B→A, Selbstzyklus und Dreierzyklus (A→B→C→A) terminieren
  sauber, liefern leere Auswahl/kein Material (Probe-Test temporär ausgeführt, anschließend
  entfernt; nicht committed).
- **UI:** Checkbox-Listen `rule-material-genre`/`rule-material-sources` nur bei Genre-Material bzw.
  freiem Materialfeld; Optionsliste folgt dem Quell-Filter; bereits gewählte, außerhalb des
  Filters liegende Genres bleiben sichtbar/abwählbar (`ReloadGenreOptionsAsync`);
  Material-Chip „Genre: A, B — Quellen: …" (E2E-nachgewiesen).

### Builds & Tests (selbst ausgeführt)

- `dotnet build VideoPlayer.sln` (Debug): **0 Fehler**, 142 Warnungen (Baseline; keine Warnungen
  in geänderten/neuen Dateien, kein xUnit1051/CS15xx).
- `dotnet build VideoPlayer.sln -c Release`: **0 Fehler**, identische Warnungslage.
- `dotnet test VideoWebPlayer.Tests/VideoWebPlayer.Tests.csproj` (synchron, vollständig):
  **1997/1999 grün, 1 Fehler, 1 bekannter Alt-Skip**
  (`DevicePlaylistE2ETests_Playback.PairedDevice_ReportsProgress...`, SKIP-Bedingung).
  - Einzelfehler: `TVSenderAdminE2ETests.Admin_CreateEditDeactivateDeleteRule` (Zeile 49,
    `LiveStreamSegmentLengthSeconds` erwartet 10 / tatsächlich 6 = Default). Betrifft einen
    Schritt-6-Bereich (Live-Stream-Settings), den dieser Schritt nicht verändert; im Handover
    seit Schritt 7 als Baseline-Flake (FillAsync-Timing) dokumentiert. **Einzelwiederholung
    (`--filter TVSenderAdminE2ETests`): 3/3 grün** inkl. des neuen Genre-E2E.
- **Gegenprobe 1:** `GenreCanonicalResolver.IsVisible` für `IsHidden` gezielt gebrochen
  (`return true`) → `GetGenreMaterialOptions_HiddenWithoutAlternate_NotListed` schlägt fehl
  (`["Sichtbar","Versteckt"]`). → `git checkout --` zurückgesetzt.
- **Gegenprobe 2 (Erkenntnis):** Entfernen *einer einzelnen* Quell-Filter-Ebene (nur Genre-Zeilen
  oder nur Inhalte) lässt `EnsureCoverage_GenreSourceFilter_RestrictsSources` grün — die beiden
  Ebenen decken sich bei gleichquelliger Verlinkung gegenseitig. Der Filter ist fachlich
  einschränkend und das zusammengesetzte Verhalten getestet; die Inhalts-Ebene sichert
  zusätzlich den Cross-Source-Link-Fall ab.

## Abweichungen

- [ ] Keine sperrenden Abweichungen.

## Hinweise

- **Datum-abhängige Saison-Tests:** `EnsureCoverage_InSeasonGenre_IsScheduled` (Oktober-Fenster),
  `EnsureCoverage_OutOfSeasonGenre_ProducesNothing` und
  `GetGenreMaterialOptions_OutOfSeasonGenre_NotListed` (Dezember-Fenster) hängen am realen
  Laufzeitdatum (`DateTime.UtcNow` im Resolver), nicht am Test-`now` des Schedule-Laufs. Heute
  (2026-10-21) grün; Läufe im November bzw. 1.–26. Dezember würden diese Tests rot machen.
  Implementierung selbst ist korrekt — ggf. später Fenster relativ zu „heute" konstruieren.
- **Doppelte Quell-Filter-Ebenen:** s. Gegenprobe 2 — Redundanz ist Robustheit (Cross-Source-
  MovieGenre/TVShowGenre-Zeilen), kein Fehler; der Test detektiert nur vollständiges Fehlen.
- **Zyklensicherheit ohne eingecheckten Test:** A→B→A-Absicherung ist per Code-Inspektion und
  Ad-hoc-Probe nachgewiesen, aber nicht durch einen persistenten Test abgesichert.
- **Testzahl:** Implementierer-Doku (`test-results.md`, Handover) nennt „+24"; tatsächlich
  **23 neue Testmethoden** (10 Schedule + 10 Service + 2 Backup + 1 E2E; Suite 1976 → 1999).
- **Doku:** `docs/help/tv-sender.md` und `docs/RELEASE_NOTES.md` aktualisiert; README-Verweis auf
  die Hilfe-Seite ausreichend (kein Feature-Detail dort gepflegt).
