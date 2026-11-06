# Abnahme Schritt 13 — Neue Genre-Zeilen erben die globale Konfiguration (N7)

Prüfer: Devin (unabhängige Abnahme, frischer Lauf ohne Implementierungskontext —
Implementierung ebenfalls Devin, aber eigener Lauf; AGENTS.md-Fall-back-Klausel)
Geprüfter Stand: `eaae020` (`step: Schritt 13 Bearbeiter eingetragen, Umsetzungsstand
dokumentiert`), Arbeitsbaum sauber.

## Ergebnis

**Status:** Erfüllt

Die Anforderung aus `nachbesserungen.md` §N7 ist vollständig und korrekt umgesetzt:
neu angelegte `Genre`-Zeilen erben `IsHidden`/`StartDate`/`EndDate`/`AlternateNames`
der ältesten konfigurierten gleichnamigen Zeile anderer Quellen (case-insensitiv,
getrimmt), unveränderte Default-Zeilen werden beim Zugriff geheilt, konfigurierte
Zeilen werden nie überschrieben, geerbte Alternativnamen lenken auf das kanonische
Genre derselben Quelle um, Zyklen terminieren, `SaveGenre`-Case-Mismatch ist
behoben, `DataUpgradeManager` v9 heilt den Altbestand beim Start. Gegenprobe
erfolgreich.

### Verifiziert

- **`GenreGlobalConfig` (neu, `VideoWebPlayer/Services/GenreGlobalConfig.cs`):**
  `HasCustomConfig` = `IsHidden` || Saisonfenster || effektive Alternativnamen
  (via `GenreCanonicalResolver.HasEffectiveAlternates` — ohne Redirects auf den
  Eigennamen, ci). `LoadConfiguredTemplatesAsync` liefert pro `Name.Trim()`
  (OrdinalIgnoreCase) die älteste konfigurierte Zeile (`MinBy(Id)`) **anderer**
  Quellen — konsistent, da `SaveGenre` ohnehin alle ci-gleichnamigen Zeilen
  synchron schreibt. `ApplyConfig` kopiert Hidden+Fenster+Alternativnamen und
  funktioniert für neue (ungetrackte, `Id == 0` → FK über Navigation) wie für
  getrackte Zeilen (`GenreNames.RemoveRange` + Clear + Neuaufbau).
- **`MediaSourceClassifier.ResolveGenresAsync` (`:1407-1467`):** Quell-Genres
  einmalig mit `Include(AlternateNames)` in `GenreResolutionState` gecacht (statt
  bisher pro Name ein Full-Table-Load — Performance-Nebenverbesserung). Neue Zeile
  → `FindConfiguredTemplateAsync` (lazy geladenes Template-Dict) + `ApplyConfig`
  vor `Add`/`SaveChanges`. Bestehende Zeile ohne Custom-Config → Heilung
  (`ApplyConfig` + Save); konfigurierte Zeilen werden übersprungen. Schritt 3
  lenkt über `EffectiveAlternateNames` rekursiv auf das kanonische Genre
  **derselben Quelle** um — gilt auch für geerbte Alternativnamen neu angelegter
  Zeilen (Semantik unverändert: eine Zeile mit Alternativnamen trägt selbst
  nichts bei, nur ihre aufgelösten Ziele). Zyklenschutz `Visiting`-Set pro
  Rekursionspfad; A↔B-Zyklus terminiert und trägt nichts bei (statt bisher
  potenzieller Endlosrekursion — reale Robustheitsverbesserung).
- **`MediaMetadataEditorService.NormalizeGenresAsync` (`:196-242`):** `Include(
  AlternateNames)` ergänzt (nötig für `HasCustomConfig`/`ApplyConfig`), gleiche
  Vererbung bei Neuanlage und gleiche Heilung von Default-Zeilen; Templates lazy
  geteilt über die Namen des Requests.
- **`GenreAdmin.SaveGenre` (`GenreAdmin.razor:223-226`):** Filter jetzt
  `string.Equals(g.Name?.Trim(), editedName, OrdinalIgnoreCase)` über alle Zeilen —
  deckungsgleich mit der Anzeigegruppierung `Name.Trim().ToLower()` in
  `ReloadGenres`. Der dokumentierte Case-Mismatch („family" vs. „Family" blieb
  unkonfiguriert) ist behoben; bUnit-Test `SaveGenre_AppliesConfigToCaseVariants`
  beweist, dass beide Case-Varianten `IsHidden` erhalten.
- **`DataUpgradeManager` v9:** `CurrentVersion = 9`, `Upgrade_9` →
  `_classifier.ReloadGenres`. `EnsureUpToDateAsync` läuft scoped beim
  Scan-Service-Start (`MediaSourceScanService.cs:66-72`); `ReloadGenres`
  (`MediaSourceClassifier.cs:1334-1379`) re-resolviert alle nicht manuell
  editierten Movies/TVShows → heilt Default-Zeilen **und** baut die
  `MovieGenres`/`TVShowGenres`-Verknüpfungen auf kanonische Zeilen um —
  plausibel und ausreichend für den Altbestand aus dem Live-Befund. Keine
  EF-Migration nötig (reines Daten-Upgrade, korrekt über `DataVersion`).
- **Builds:** `dotnet build VideoPlayer.sln` Debug **und** `-c Release`: je
  0 Fehler, 142 Warnungen = dokumentierter Bestand, keine neue Warnung. Kein
  `VideoWebPlayer.exe`-Lock (keine Instanz lief).
- **Suite:** `dotnet test VideoWebPlayer.Tests/VideoWebPlayer.Tests.csproj`
  synchron (Debug): **2054 grün / 1 Skip / 1 FAIL** (2056 gesamt). FAIL =
  `PlaylistSkipLockedEntriesE2ETest` (Playwright-Timeout in `SelectEntryAsync`,
  kein Bezug zur Änderung) — Einzelwiederholung **grün (10 s)**, bekannter
  Last-Flake. Skip = bekannter Alt-Skip `PairedDevice_ReportsProgressForMovie
  WithoutCollection_ContinueWatchingListStillLoads`. Effektiv 2055 grün +
  1 Alt-Skip — deckt sich mit dem Implementierer-Bericht.
- **Gegenprobe:** Vererbung im Erzeugungszweig sabotiert (`template is not null
  && false` vor `ApplyConfig`) → exakt die 3 Neu-Zeilen-Tests schlagen fehl
  (`..._InheritsHiddenAndAlternates_RedirectsToCanonical`,
  `..._InheritsSeasonalWindow`, `..._CaseVariant_...`); Heilungs-/Zyklus-/
  Editor-Tests blieben grün (separate Pfade — korrekte Zielgenauigkeit).
  `git checkout --` revertiert, Arbeitsbaum sauber.
- **Format:** `dotnet format VideoPlayer.sln --verify-no-changes --severity
  error` sauber (Exit 0).
- **Tests neu (+9):** 7× `MediaSourceClassifierGenreInheritanceTests` (Erbe
  hidden+alt+Umlenkung auf kanonische Zeile derselben Quelle inkl.
  `GenreNames`-Rewrite auf „Familie", Saisonfenster, ci-Match, Heilung
  Bestands-Default, konfigurierte Zeile unangetastet, ohne Vorlage bleibt
  Default, Zyklus terminiert), 1× `MediaMetadataEditorServiceGenreInheritance
  Tests`, 1× `GenreAdminTests` (bUnit) — alle gegen echtes In-Memory-SQLite.
- **Doku:** `docs/help/einrichtung.md` (globale Genre-Einstellungen + Vererbung
  + ci-Zusammenführung) und `docs/RELEASE_NOTES.md` (EN-Eintrag mit
  Upgrade-9-Hinweis) sind konsistent zur Umsetzung.

## Abweichungen

- [ ] Keine.

## Hinweise

- **Editor-Pfad verknüpft die (umlenkende) Zeile selbst, nicht das kanonische
  Ziel:** `NormalizeGenresAsync` liefert die angefragte Zeile zurück; trägt sie
  Alternativnamen, landet die `MovieGenres`-Verknüpfung auf der Redirect-Zeile
  (Test assertiert das explizit). Vorbestehende Semantik — der Editor verknüpfte
  schon immer die benannte Zeile und `ReloadGenres` überspringt
  `IsManuallyEdited`-Inhalte grundsätzlich. Unschädlich, weil die
  Sendeplan-Expansion (`ProgramScheduleService.ExpandGenreAsync`, `:1138-1164`)
  über `ResolveCanonicalNames` auch Redirect-Zeilen auf ihr kanonisches Ziel
  abbildet; für die reine `GenreId`-Filterliste gilt dasselbe wie für jede
  manuell getaggte Redirect-Zeile vorher. Kein N7-Regressionspunkt.
- **Ersttestlauf scheiterte umgebungsbedingt massiv:** Laufwerk C: hatte nur
  noch ~0,58 GB frei → `SQLite Error 13: 'database or disk is full'` +
  Playwright-Timeouts in 218 E2E-Tests (Test-DBs liegen unter
  `Path.GetTempPath()` = C:). Wiederholung mit `TEMP`/`TMP` auf D: umgeleitet
  → Suite wie oben grün. **Anwender-Hinweis:** C: ist faktisch voll — das ist
  unabhängig von diesem Schritt ein reales Betriebsproblem auf dieser Maschine.
- **`LoadConfiguredTemplatesAsync` materialisiert alle Genre-Zeilen anderer
  Quellen** (einmal pro `ResolveGenresAsync`-/`NormalizeGenresAsync`-Aufruf, nur
  lazy wenn tatsächlich angelegt/geheilt wird). Genre-Tabellen sind klein;
  vertretbar.
- **Heilung berührt nur Zeilen, deren Name gerade aufgelöst wird:** eine
  Default-Zeile ohne referenzierenden Inhalt bleibt bis zur nächsten Berührung
  ungeheilt — harmlos, da sie ohne Bezug nichts auslöst; `Upgrade_9` erfasst
  den praxisrelevanten Altbestand über die Inhalte.
- **`MarkGenresAsChangedAsync` wird in `SaveGenre` je Zeile in der Schleife
  gesetzt** — redundant, aber idempotent; vorbestehend.
- **Abnahme durch Devin statt fremdem Agenten:** Implementierung und Prüfung
  tragen beide „Devin"; geprüft wurde als frischer Sub-Agent-Lauf ohne
  Implementierungskontext (AGENTS.md-§4-Fall-back). Beleg: Gegenprobe oben.
- `steps.md` zeigt Schritt 13 noch „In Arbeit" — Status-Aktualisierung auf
  „Fertig" steht nach dieser Abnahme an.
