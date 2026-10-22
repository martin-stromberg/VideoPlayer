# Abnahme Schritt 15 — Saisonale Genres in Regel-Material-Auswahl wählbar (N10)

Prüfer: Devin (Sub-Agent, nicht der Implementierer). Branch:
`task-issue-111-6145c51699f34953bf29a244634a23e2-automatisierter-tvsender-schritt-15-saison-genre`,
HEAD `2580eaf`, Arbeitsstand sauber committed (Arbeitsbaum vor Prüfung clean).

## Ergebnis

**Status:** Erfüllt

N10-Anforderung vollständig umgesetzt: `GetGenreMaterialOptionsAsync` listet Genres
unabhängig vom saisonalen `StartDate`/`EndDate`-Fenster, Out-of-Season-Genres tragen ein
Fenster-Label („Weihnachten (01.12.–26.12.)"), `IsHidden`- und
Alternativnamen-Filter bleiben intakt, und die Expansion (`ExpandGenreAsync` /
`ResolveCanonicalNames` / `effectiveGenreIds`) ist unverändert — das Fenster begrenzt
weiterhin nur die Ausstrahlung.

## Verifiziert

- **Diff-Umfang:** `git diff task/issue-111-...-automatisierte...tv-sender...HEAD` —
  exakt 2 Produkt-Dateien (`VideoWebPlayer/Services/GenreCanonicalResolver.cs`,
  `VideoWebPlayer/Services/ProgramRuleService.cs`) + 1 Testdatei
  (`VideoWebPlayer.Tests/ProgramRuleServiceTests_GenreMaterial.cs`). Keine
  Folgeänderungen.
- **Build:** `dotnet build VideoPlayer.sln -c Release` — **0 Fehler**, 142 Warnungen
  (identischer Bestand wie Schritt 12–14, keine neue Warnung).
- **Tests synchron (Release):**
  `dotnet test VideoWebPlayer.Tests/VideoWebPlayer.Tests.csproj -c Release --filter "FullyQualifiedName~GenreMaterial|FullyQualifiedName~GenreCanonical|FullyQualifiedName~ProgramScheduleServiceTests_Genres"`
  → **20 bestanden / 0 Fehler / 0 übersprungen**, wie erwartet.
- **Fachlich (Code-Review `GenreCanonicalResolver.cs:92-110`):**
  - `!g.IsHidden` ist explizit im Filter (Zeile 94) — Hidden-Genres werden auch bei
    `includeOutOfSeason: true` nicht gelistet. `IsVisible` prüft `IsHidden`
    zusätzlich, der eigene Check ist nötig, weil `includeOutOfSeason` den
    `IsVisible`-Aufruf kurzschließt — korrekt gelöst.
  - `!HasEffectiveAlternates(g)` unverändert — Alternativnamen-Weiterleitungen tauchen
    weiterhin nicht als eigene Option auf (Test
    `GetGenreMaterialOptions_HiddenWithAlternate_OnlyCanonicalListed` grün).
  - Fenster-Label nur bei `includeWindow` = Fenster konfiguriert **und** `!IsVisible`
    (Zeilen 104-110): Out-of-Season mit Fenster → „Name (dd.MM.–dd.MM.)"; in-Season
    oder ohne Fenster → nackter Name. Durch Test
    `GetGenreMaterialOptions_OutOfSeasonGenre_ListedWithWindowLabel` belegt
    (`["Immer", "Weihnachten (01.12.–26.12.)"]`).
  - `BuildOptions`-Default `includeOutOfSeason = false` erhält das alte Verhalten für
    alle anderen Aufrufer; einziger Produktiv-Aufrufer ist
    `GetGenreMaterialOptionsAsync` (ProgramRuleService.cs:98, mit `true`) —
    `api/items/genres` und alle anderen Optionsquellen unberührt.
  - Expansion unverändert: `ProgramScheduleService.ExpandGenreAsync` (:1111-1185) und
    `ResolveCanonicalNames`/`ResolveNames` (:120-148) sind nicht im Diff;
    `ResolveNames` filtert weiterhin über `IsVisible` (Zeile 129) — außerhalb der
    Saison liefert das Material nichts, Regel kann dauerhaft existieren. Exakt die
    N10-Zielsetzung.
- **Gegenprobe:** `!g.IsHidden` aus Zeile 94 entfernt →
  `GetGenreMaterialOptions_HiddenWithoutAlternate_NotListed` schlägt **rot**
  (Expected `["Sichtbar"]`, Actual `["Sichtbar", "Versteckt"]`) — der Test schützt den
  Filter real. Revertiert via `git checkout --`, Arbeitsbaum wieder clean, HEAD
  unverändert.

## Abweichungen

- [ ] Keine.

## Hinweise

- Kein dedizierter Test für „in-Season-Genre mit Fenster erscheint **ohne** Label"
  — die Bedingung `!IsVisible` in `includeWindow` deckt das logisch ab, „Immer"
  (ohne Fenster) belegt den Label-freien Pfad. Niedrige Schärfe.
- `OptionName` wählt per `g.First()` die Zeile, deren Fenster das Label bestimmt.
  Bei ci-gleichnamigen Zeilen mit *unterschiedlichen* Fenstern (z. B. Quelle A
  in-Season, Quelle B out-of-Season) hängt das Label von der EF-Zeilenreihenfolge ab.
  Edge-Case ohne Praxis-Befund; die Option bleibt in jedem Fall wählbar.
- Stil-Miniatur: private Methode `includeWindow` (Zeile 109) in camelCase, während die
  übrigen privaten Methoden der Klasse PascalCase nutzen — kein Verhalten betroffen.
- Prozess (wie schon in `acceptance-schritt-14.md` vermerkt): In `handover.md` fehlen
  die Bearbeiter-Tabellenzeilen für Schritt 14 und 15 sowie der Übergabestand-Abschnitt
  für Schritt 15; `steps.md` steht noch auf „In Arbeit". Nachzutragen gemäß
  AGENTS.md §2/§5 durch den Anwender/Implementierer.
