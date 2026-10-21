# Umsetzungsplan: Genre-Konfiguration vererben (N7)

## Übersicht

Neue `Genre`-Zeilen erben beim Anlegen (Scan und Metadaten-Edit) die globale
Namenskonfiguration (`IsHidden`, `StartDate`, `EndDate`, `AlternateNames`) von einer
gleichnamigen, case-insensitiv gefundenen konfigurierten Zeile anderer Quellen.
Zeilen im unkonfigurierten Default-Zustand werden beim Scan-/Reload-Zugriff nachträglich
geheilt; ein einmaliges `DataUpgradeManager`-Upgrade richtet den Live-Bestand. Zusätzlich
wird der Case-Mismatch in `GenreAdmin.SaveGenre` behoben.

## Designentscheidungen

| Komponente / Bereich | Gewählter Ansatz | Begründung |
|----------------------|-----------------|------------|
| Vererbungslogik | Neue interne statische Hilfsklasse `GenreGlobalConfig` (`VideoWebPlayer/Services/`), genutzt von `MediaSourceClassifier` und `MediaMetadataEditorService` | Keine DI-Änderung an beiden Services nötig; ein einziger Ort für „Was ist eine konfigurierte Zeile" + Vorlagensuche + Anwendung. Spiegelt `GenreCanonicalResolver`-Muster (interne statische Fachlogik). |
| Konfigurationsvorlage | Älteste **konfigurierte** gleichnamige (ci, getrimmt) Zeile anderer Quellen (`MinBy(Id)`); keine konfigurierte Vorlage → kein Erbe | `SaveGenre` schreibt immer auf alle Zeilen — konfigurierte Zeilen sind daher untereinander konsistent; die älteste ist der stabilste Proxy für die Admin-Intention. Nicht-konfigurierte Vorlagen sind ohnehin wirkungslos. |
| Heilung | Nur Zeilen im **Default-Zustand** (`!IsHidden`, `StartDate`/`EndDate` null, keine effektiven Alternativnamen) werden synchronisiert; explizit konfigurierte Zeilen werden nie überschrieben | Divergente Default-Zeilen sind ausschließlich Bug-Artefakte (jede Zeile ohne Admin-Berührung). Konfigurierte Zeilen sind per `SaveGenre`-Semantik bereits global einheitlich; Rest-Artefakte aus dem alten Case-Filter richtet die nächste Admin-Speicherung (dann ci). Kein Risiko, Admin-Konfiguration zu überschreiben. |
| Umlenkung nach Erbe | Neu angelegte Zeilen mit geerbten Alternativnamen durchlaufen dieselbe rekursive Auflösung wie gefundene Zeilen | „Family" auf neuer Quelle → Inhalt landet beim kanonischen Genre derselben Quelle (Anforderung 2). Erreicht durch Umstrukturierung: Erzeugen/Heilen → dann einheitlich Alternativnamen-Prüfung → ggf. Rekursion. |
| Zyklenschutz | `visiting`-Set (Ids) auf dem Rekursionspfad, analog `GenreCanonicalResolver` | Bestehender Code hat keinen Schutz (A↔B-Alternativnamen = Endlosrekursion); geerbte Alternativnamen machen Zyklen wahrscheinlicher. |
| Bestandskorrektur | `DataUpgradeManager.CurrentVersion` 8→9 mit `Upgrade_9` → `ReloadGenres` (Muster von Upgrades 5/6) | `ReloadGenres` ruft `GetOrCreateGenresAsync` pro Movie/Show auf — die Heilungslogik repariert dabei Default-Zeilen aller Quellen **und** baut die Verknüpfungen auf kanonische Genres um. Kein Nutzereingriff, keine EF-Migration nötig. Zusätzlich kann der Admin jederzeit global neu speichern (`GenresChanged` → `CheckReloadGenres`). |
| `SaveGenre`-Case-Fix | Alle Zeilen laden (Include `AlternateNames`) und in-memory ci+Trim vergleichen — analog zur `ReloadGenres`-Gruppierung | `Where(g => g.Name == editGenre.Name)` ist auf SQLite BINARY-Kollation case-sensitiv; die Anzeige gruppiert ci. Konsistente Semantik ohne Collation-Tricks. |

## Programmabläufe

### Genre-Anlage beim Scan mit Vererbung und Umlenkung

1. `GetOrCreateGenresAsync` lädt einmalig alle `Genre`-Zeilen der Zielquelle inkl.
   `AlternateNames` (statt bisher pro Name).
2. Pro gefordertem Namen: Treffer in der Quelle → ggf. Heilung (Default-Zeile +
   konfigurierte Vorlage → `GenreGlobalConfig.ApplyConfig` + `SaveChanges`).
   Kein Treffer → `new Genre`; wenn Vorlage vorhanden → `ApplyConfig` vor dem ersten
   `SaveChanges`; Zeile dem quellbezogenen Cache hinzufügen.
3. Danach einheitlich: effektive Alternativnamen (ci, ohne Eigenname, getrimmt) — keine →
   Zeile ins Ergebnis; vorhanden → Rekursion mit den Alternativnamen in derselben Quelle
   (Pfad-`visiting`-Set; Zeile selbst nicht im Ergebnis).
4. Ergebnis-Deduplizierung nach `Genre.Id` (bestehend).

Beteiligte Klassen: `MediaSourceClassifier`, `GenreGlobalConfig`, `Genre`, `GenreName`.

### Genre-Anlage im Metadaten-Edit mit Vererbung

1. `NormalizeGenresAsync` ermittelt fehlende Namen wie bisher.
2. Für jede neue Zeile: konfigurierte Vorlage anderer Quellen → `ApplyConfig`.
3. Gefundene Default-Zeilen: Heilung wie beim Scan (Konsistenz; die Zuordnung selbst
   bleibt zeilenbezogen — keine Alternativnamen-Umleitung im Editor, dokumentierter
   Unterschied zum Scan).

Beteiligte Klassen: `MediaMetadataEditorService`, `GenreGlobalConfig`.

### Globale Speicherung ohne Case-Mismatch

1. `SaveGenre` lädt alle `Genre` inkl. `AlternateNames`, filtert in-memory mit
   `StringComparison.OrdinalIgnoreCase` auf `Name.Trim()` und wendet die Konfiguration auf
   alle Treffer an — deckungsgleich mit der Anzeigegruppierung.

Beteiligte Komponente: `GenreAdmin`.

### Einmalige Bestandskorrektur beim App-Start

1. `EnsureUpToDateAsync` führt `Upgrade_9` aus → `MediaSourceClassifier.ReloadGenres`.
2. `ReloadGenres` verarbeitet alle `!IsManuallyEdited`-Movies/TVShows → `GetOrCreateGenresAsync`
   heilt Default-Zeilen und lenkt Verknüpfungen auf kanonische Zeilen um;
   `GenreNames` wird auf die kanonischen Namen zurückgeschrieben.

Beteiligte Klassen: `DataUpgradeManager`, `MediaSourceClassifier`, `GenreGlobalConfig`.

## Neue Klassen

| Klasse | Typ | Zweck |
|--------|-----|-------|
| `GenreGlobalConfig` | interne statische Hilfsklasse (`Services/`) | `HasCustomConfig(Genre)` (erkennt Admin-Konfiguration: `IsHidden` oder saisonale Daten oder effektive Alternativnamen — letztere via `GenreCanonicalResolver.HasEffectiveAlternates`), `LoadConfiguredTemplatesAsync(ApplicationDbContext, long mediaSourceId, CancellationToken)` → `Dictionary<string, Genre>` (ci-Key `Name.Trim()`, älteste konfigurierte Zeile anderer Quellen), `ApplyConfig(ApplicationDbContext, Genre target, Genre template)` (kopiert `IsHidden`/`StartDate`/`EndDate`, ersetzt `AlternateNames` — EF-konform für neue und getrackte Zeilen) |

## Änderungen an bestehenden Klassen

### `MediaSourceClassifier` (Service)

- **Geänderte Methoden:**
  - `GetOrCreateGenresAsync` — Strukturierung als öffentlich-private Hülle + rekursive
    Arbeitsmethode mit Zustand (`sourceGenres`-Cache, lazy `templates`-Dictionary,
    `visiting`-Zyklenschutz); Vererbung beim Erzeugen; Heilung von Default-Zeilen;
    Alternativnamen aus der geladenen `AlternateNames`-Navigation statt erneuter
    `GenreNames`-Abfrage; Eigenname ci-ausgeschlossen.
- **Neue Methoden:** private Hilfsklasse/State für den Auflösungskontext; ggf.
  `EffectiveAlternateNames`-Helper (Semantik wie `GenreCanonicalResolver`).

### `MediaMetadataEditorService` (Service)

- **Geänderte Methoden:** `NormalizeGenresAsync` — neue Zeilen erben via
  `GenreGlobalConfig`; gefundene Default-Zeilen werden geheilt.

### `GenreAdmin` (Razor-Seite)

- **Geänderte Methoden:** `SaveGenre` — ci+Trim-Filter auf alle Zeilen (in-memory),
  analog `ReloadGenres`-Gruppierung.

### `DataUpgradeManager` (Service)

- **Geänderte Member:** `CurrentVersion` 8 → 9; `switch` um `case 9` → `Upgrade_9`.
- **Neue Methoden:** `Upgrade_9` — ruft `_classifier.ReloadGenres(cancellationToken)`
  (Muster `Upgrade_5`/`Upgrade_6`).

## Datenbankmigrationen

Keine (kein Schemaänderungsbedarf; `Setup.DataVersion` existiert bereits).

## Validierungsregeln

Keine.

## Konfigurationsänderungen

Keine.

## Seiteneffekte und Risiken

- **Scan-Laufzeit:** `GetOrCreateGenresAsync` lädt quellbezogene Zeilen künftig einmal pro
  Aufruf statt pro Name (schneller); zusätzlich einmal pro Aufruf lazy alle konfigurierten
  Vorlagen anderer Quellen (eine kleine Abfrage über die `Genres`-Tabelle). Netto neutral
  bis schneller.
- **Heilung schreibt beim Scannen:** Default-Zeilen mit konfigurierter Vorlage werden beim
  Zugriff persistiert — einmalig pro Zeile (konvergiert). `GenreCanonicalResolver`,
  `SourceGenresController` und `GenreAdmin` sehen danach konsistente Daten.
- **`ReloadGenres` bei Upgrade 9** läuft einmalig über den gesamten Bestand (gleiches
  Verhalten wie Upgrades 5/6, akzeptierte Startverzögerung).
- **Manuell editierte Inhalte** (`IsManuallyEdited`) werden von `ReloadGenres` nicht
  angefasst — ihre `MovieGenres`-Verknüpfungen bleiben, lösen aber über
  `GenreCanonicalResolver` weiter korrekt auf.
- **Metadaten-Edit** weist weiterhin die Zeile selbst zu (keine Alternativnamen-Umleitung
  beim Edit) — geheilte/neue versteckte Zeilen können dadurch direkt verknüpft werden;
  Expansion/Auswahl lösen kanonisch korrekt auf.

## Umsetzungsreihenfolge

1. **`GenreGlobalConfig` anlegen**
   - Voraussetzungen: keine (`GenreCanonicalResolver.HasEffectiveAlternates` existiert)
   - Beschreibung: Hilfsklasse mit `HasCustomConfig`, `LoadConfiguredTemplatesAsync`, `ApplyConfig`
2. **`MediaSourceClassifier.GetOrCreateGenresAsync` umbauen**
   - Voraussetzungen: Schritt 1
   - Beschreibung: State-Objekt, einmalige Quell-Ladung, lazy Vorlagen, Vererbung + Heilung + Zyklenschutz + Umlenkung
3. **`MediaMetadataEditorService.NormalizeGenresAsync` erweitern**
   - Voraussetzungen: Schritt 1
   - Beschreibung: Vererbung bei Neuanlage + Heilung von Default-Zeilen (lazy Vorlagen einmal je Aufruf)
4. **`GenreAdmin.SaveGenre` ci-Filter**
   - Voraussetzungen: keine
   - Beschreibung: In-Memory-Vergleich ci+Trim
5. **`DataUpgradeManager.Upgrade_9`**
   - Voraussetzungen: Schritt 2
   - Beschreibung: `CurrentVersion = 9`, `case 9` → `Upgrade_9` → `ReloadGenres`
6. **Tests**
   - Voraussetzungen: Schritte 1–5
   - Beschreibung: neue SQLite-Testklasse + Editor-Test + bUnit-Test für `SaveGenre`

## Tests

### Neue Tests

| Test / Hilfsmethode | Testklasse | Was wird geprüft / bereitgestellt? |
|--------------------|------------|-------------------------------------|
| `ReloadGenres_NewGenreOnOtherSource_InheritsHiddenAndAlternates_RedirectsToCanonical` | `MediaSourceClassifierGenreInheritanceTests` (neu, `[Collection(MediaSourceClassifierCollection.Name)]`, SQLite shared-memory) | „Family" hidden+alt „Familie" auf Quelle 1; Movie `GenreNames="Family"` auf Quelle 2 → neue Quelle-2-„Family"-Zeile `IsHidden`, `GenreName "Familie"`; `MovieGenres` → Quelle-2-„Familie"-Zeile; `GenreNames` = „Familie" |
| `ReloadGenres_NewGenre_InheritsSeasonalWindow` | dto. | Vorlage mit `StartDate`/`EndDate` (sichtbar, keine Alternativen) → neue Zeile erbt Daten, bleibt sichtbar, Film wird zugeordnet |
| `ReloadGenres_CaseVariant_InheritsFromCaseInsensitiveMatch` | dto. | Vorlage „FAMILY" konfiguriert; Movie `GenreNames="family"` → neue Zeile geheilt/konfiguriert |
| `ReloadGenres_ExistingDefaultRow_HealsFromTemplate` | dto. | Quelle-2-„Family"-Zeile existiert schon im Default-Zustand → nach `ReloadGenres` hidden+Alternativen; Verknüpfung auf kanonisches Genre |
| `ReloadGenres_ExistingConfiguredRow_IsNotOverwritten` | dto. | Konfigurierte Zeile wird nicht von der Heilung angefasst |
| `ReloadGenres_NoConfiguredTemplate_CreatesDefaultGenre` | dto. | Ohne Vorlage bleibt neue Zeile sichtbar ohne Alternativen (Regressionsschutz) |
| `ReloadGenres_AlternateNameCycle_Terminates` | dto. | A↔B-Alternativnamen in einer Quelle → kein Stack-Overflow, Ergebnis leer/terminiert |
| `UpdateAsync_NewGenre_InheritsGlobalConfig` | `MediaMetadataEditorServiceGenreInheritanceTests` (neu, SQLite) | Metadaten-Edit legt Genre auf Quelle 2 an → erbt `IsHidden`+`AlternateNames` von Quelle 1 |
| `SaveGenre_AppliesConfigToCaseVariants` | `GenreAdminTests` (neu, bUnit, SQLite) | „Family"/„family"-Zeilen → Bearbeiten+Speichern über die gerenderte Seite → beide `IsHidden` |

### Betroffene bestehende Tests

| Test / Testklasse | Grund der Anpassung |
|-------------------|---------------------|
| `MediaMetadataEditorServiceTests.UpdateMovieAsync_SavesMetadataAndCreatesNewGenres` | Verhalten unverändert (keine Vorlage vorhanden → Default-Anlage) — Erwartung bleibt, kein Eingriff nötig |

### E2E-Tests (primärer Funktionsnachweis)

Kein neuer Benutzerfluss entsteht: Die UI-Oberfläche (`GenreAdmin`) bleibt unverändert,
lediglich der Zeilenfilter des Speichern-Pfads wird korrigiert — abgedeckt durch den
bUnit-Test `GenreAdminTests.SaveGenre_AppliesConfigToCaseVariants`, der die Seite mit
echtem `DbContext` (SQLite), echtem `GenreService` und Admin-Authentifizierung rendert und
den Speichern-Klick über die UI auslöst. Scan-/Vererbungslogik ist Backend und wird durch
SQLite-Integrationstests gegen den realen `MediaSourceClassifier`/`ReloadGenres`-Pfad
(= dem Pfad, den Scan und `CheckReloadGenres` tatsächlich fahren) nachgewiesen; ein
Playwright-E2E hätte keinen zusätzlichen Prüfgegenstand (keine neue UI-Interaktion).

## Offene Punkte

Keine.
