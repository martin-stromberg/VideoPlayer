# Umsetzungsplan: Trailer als Vorlauf-Füllmaterial zu eingeplanten Filmen (N11)

## Übersicht

Trailer-Dateien werden beim Scan anhand der Plex/Jellyfin-Konvention (`-trailer`-Suffix neben der
Filmdatei, `trailers/`-Unterordner) erkannt und über `MovieMediaItem.IsTrailer` dem Film zugeordnet.
Eine neue Materialart `ProgramRuleMaterialType.Trailer` („Trailer anstehender Filme") wird für
Füller-Regeln angeboten; `ProgramScheduleService` bevorzugt bei der Lückenfüllung Trailer, deren
verknüpfter Film innerhalb `ProgramSchedule:TrailerLeadDays` (Default 7) nach der Lückenposition
ausgestrahlt wird. Trailer-Items erzeugen keine `Movie`-Datensätze und werden nirgends als
Hauptdatei eines Films gewählt.

## Designentscheidungen

| Komponente / Bereich | Gewählter Ansatz | Begründung |
|----------------------|-----------------|------------|
| Trailer↔Film-Beziehung | `MovieMediaItem.IsTrailer` (bool, Default false) | Vorhandenes Join, mehrere Trailer pro Film, Trailer bleibt `MediaItem` (Duration-Probe automatisch), eine Spalte statt neuer Tabelle/Entity |
| Trailer-Materialart | `ProgramRuleMaterialType.Trailer = 5`, kein `MaterialId`-Bezug (0) | Deckt „alle Trailer anstehender Filme" ab; konsistent zum polymorphen Materialmuster; Füller-Regel wählt es wie andere Materialarten |
| Vorlauf-Logik | Pro Lückenposition: erster Trailer, dessen Film in `(fillAt, fillAt+LeadDays]` startet; Items sortiert nach nächster Ausstrahlung | Echter Vorlauf-Effekt (Trailer vor, nicht nach dem Film); Fallback = andere Füller-Regeln füllen weiter |
| Expansion der Trailer-Regel | Verzögert: `EnsureCoverageAsync` expandiert Trailer-Regeln erst nach `MaterializeOccurrence`; `ReconcileAsync`/Diagnose nutzen persistierte Slots | Erstlauf: erst dann stehen die neu geplanten Film-Ausstrahlungen fest („Film im Plan") |
| `ProgramSlot.MovieId` bei Trailer | = verknüpfter Film | Programmführer zeigt den Filmtitel; Trailer-Slots werden über die `IsTrailer`-Link-Menge aus der Vorlauf-Ausstrahlungskarte ausgenommen |
| `TrailerLeadDays` | `ProgramScheduleOptions` (appsettings `ProgramSchedule:TrailerLeadDays`, Default 7) | Anforderung nennt ausdrücklich diesen Schlüssel; Betreiber-Konfiguration, kein Admin-DB-Setting |

## Programmabläufe

### Scan/Klassifizierung mit Trailer-Erkennung

1. `ProcessCollectionAsMovieAsync(collection)`: Video-`MediaItem`s in einer Collection namens
   `trailers` (OrdinalIgnoreCase) sowie Dateien mit Stem-Endung `-trailer` werden nicht als
   Film kandidiert (kein NFO-/Movie-Fluss), sondern als Trailer-Kandidaten gesammelt.
2. `-trailer`-Kandidat: Host-`MediaItem` mit gleichem Stem ohne Suffix in derselben Collection →
   dessen `Movie` → `MovieMediaItem{IsTrailer=true}` (Link anlegen bzw. `IsTrailer` nachträglich
   setzen bei Alt-Link).
3. `trailers/`-Kind-Collection: alle Video-Items → Links zu **allen** `Movies` der
   Eltern-`CollectionId` (Jellyfin: Movie-Ordner); die Eltern-Collection wird durch
   `ParentMediaCollection.Changed=true` ohnehin erneut verarbeitet → Reihenfolge egal.
4. `ProcessEpisodesForTVShowAsync`: dieselben zwei Ausnahmen (`-trailer`-Stem, Collection-Name
   `trailers`) → Trailer nie als Episode.
5. `MovieMediaItemsAsync`, `FindMediaItemAsync("movie")`, `BackfillMissingActorsAsync`,
   `AssignPicturesToMovieAsync` filtern `!IsTrailer`.

### Plangenerierung mit Trailer-Füller

1. `EnsureCoverageAsync`: Regel-Zustände für `MaterialType.Trailer` werden zunächst leer
   angelegt; nach `MaterializeOccurrence` `airingsByMovie` aus persistierten `Scheduled`-Slots
   (`MovieId != null`, `Start > coverageStart`, ohne Trailer-MediaItems) ∪ `newSlots` aufbauen,
   dann `ExpandTrailersAsync` je Trailer-Regel (`RuleState.Items` + Cursor-Index setzen).
2. `ReconcileAsync`: `airingsByMovie` aus persistierten Slots vor der Zustandsschleife;
   Trailer-Regeln regulär expandiert (Validierung bestehender Trailer-Slots braucht `Items`).
3. `ExpandTrailersAsync`: `MovieMediaItems` mit `IsTrailer` + Duration + Access (`Allows` über
   `MediaSourceId`/`MovieCollectionId` des Films), Dedup nach `MediaItemId`, Filter auf Filme mit
   Start > `coverageStart` in `airingsByMovie`, Sortierung nach nächster Ausstrahlung aufsteigend;
   `SchedulableItem(ProgramCursorContentType.Trailer, MediaItemId, MediaItemId, MovieId, null, Duration)`.
4. `FillGaps` mit `airingsByMovie` + `trailerLead`: bei Trailer-Regeln Scan der Ring-Items nach
   erstem Element mit Film-Start in `(fillAt, fillAt+Lead]` → `Commit(offset+1)`; kein Treffer →
   Regel trägt bei dieser Position nichts bei (andere Füller greifen = Fallback).
5. `GetRuleDiagnosesAsync`: `airingsByMovie` aus persistierten Slots (`Start > now`).

## Neue Klassen

| Klasse | Typ | Zweck |
|--------|-----|-------|
| `ProgramScheduleOptions` | Optionsklasse (`VideoWebPlayer/Configuration`) | `TrailerLeadDays` (int, Default 7), `EffectiveTrailerLeadDays` |

## Änderungen an bestehenden Klassen

### `MovieMediaItem` (Datenmodell)
- **Neue Eigenschaft:** `IsTrailer` (bool) — Link ist ein Trailer statt Hauptdatei.

### `ProgramRuleMaterialType` / `ProgramCursorContentType` (Enums)
- Neue Werte `Trailer = 5` bzw. `Trailer = 3`.

### `MediaSourceClassifier` (Service)
- `IsTrailerFileName`-Hilfsmethode; Trailer-Sammlung + Link-Erzeugung in
  `ProcessCollectionAsMovieAsync` (Suffix- + Ordner-Konvention); gleiche Ausnahmen in
  `ProcessEpisodesForTVShowAsync`; `!IsTrailer`-Filter in `BackfillMissingActorsAsync`,
  `AssignPicturesToMovieAsync`.

### `ProgramScheduleService` (Service)
- Neuer optionaler `IOptions<ProgramScheduleOptions>?`-Parameter; `_trailerLead`.
- `ExpandMaterialAsync` um `Trailer`-Fall (verzögert/aufrufend mit `airingsByMovie`).
- `ExpandTrailersAsync` + `LoadMovieAiringsAsync`/`BuildMovieAiringsAsync` (Map ohne
  Trailer-MediaItems).
- `FillGaps`-Signatur um `airingsByMovie` (Vorlauf-Prüfung nur bei Trailer-Regeln).
- `RuleState.Items` setzbar bzw. Re-Init für verzögerte Expansion.

### `ProgramRuleService` (Service)
- `GetAllowedMaterialTypes(Filler)` += `Trailer`; `exists`-Switch: `Trailer => true`.

### `ItemsController` (Controller)
- `FindMediaItemAsync`: `!mi.IsTrailer`-Filter bei `"movie"`.

### `TVSenderAdmin.razor` (UI)
- Trailer-Option im Material-Picker (Checkbox „Trailer anstehender Filme",
  `id="rule-material-trailer"`, sichtbar wenn erlaubt); `MaterialLabel`-Sonderfall.

### `ServiceCollectionExtensions` / `appsettings.json`
- `services.Configure<ProgramScheduleOptions>(configuration.GetSection("ProgramSchedule"))`;
  `"ProgramSchedule": { "TrailerLeadDays": 7 }`.

### `VideoWebPlayerBackupData` (Backup)
- `OptionalRestoreColumns` += `MovieMediaItems.IsTrailer`;
  `OptionalRestoreBoolDefaults` += `(MovieMediaItems, IsTrailer, false)`.

## Datenbankmigrationen

| Migrationsname | Betroffene Tabellen/Spalten | Beschreibung der Änderung |
|----------------|----------------------------|---------------------------|
| `AddMovieMediaItemIsTrailer` | `MovieMediaItems.IsTrailer` | bool NOT NULL DEFAULT 0 |

## Validierungsregeln

| Feld / Objekt | Regel | Fehlerfall |
|---------------|-------|------------|
| `ProgramRule.MaterialType = Trailer` | Nur bei `Filler`/`Repeat`/`HolidaySpecial` via `GetAllowedMaterialTypes` | `ArgumentException` „nicht zulässig" |
| `TrailerLeadDays` | <= 0 → Default 7 | — (normalize) |

## Konfigurationsänderungen

| Eintrag | Typ | Standardwert | Zweck |
|---------|-----|--------------|-------|
| `ProgramSchedule:TrailerLeadDays` | int | 7 | Vorlauffenster für Trailer-Füller |

## Seiteneffekte und Risiken

- **Movie-Hauptdatei:** Alle `MovieMediaItem`-Konsumenten müssen `!IsTrailer` filtern (benannt).
- **`Movie.MediaItems` (Convenience):** enthält Trailer — betroffene UI-/API-Flächen wurden
  geprüft; Hauptdatei-Auflösung geht über die gefilterten Pfade.
- **Cursor:** `ProgramCursorContentType.Trailer = 3` ist additiv → Alt-Daten kompatibel.
- **EnumContractTests:** erfassen die geänderten Enums nicht → kein Testbruch.

## Umsetzungsreihenfolge

1. **Datenmodell + Migration:** `MovieMediaItem.IsTrailer`, `dotnet ef migrations add`,
   `OptionalRestore*` in `VideoWebPlayerBackupData`.
   - Voraussetzungen: keine (dotnet-ef 10.0.10 vorhanden).
2. **Enums + Optionsklasse + DI:** `Trailer`-Werte, `ProgramScheduleOptions`, Registrierung,
   `appsettings.json`.
   - Voraussetzungen: keine.
3. **Classifier:** Erkennung + Verknüpfung + `!IsTrailer`-Filter (inkl. `ItemsController`).
   - Voraussetzungen: Schritt 1.
4. **Schedule:** `ExpandTrailersAsync`, Vorlauf in `FillGaps`, verzögerte Expansion.
   - Voraussetzungen: Schritte 1–2.
5. **Regel-Service + UI:** `GetAllowedMaterialTypes`, `exists`-Switch, `TVSenderAdmin.razor`.
   - Voraussetzungen: Schritt 2.
6. **Tests** (siehe unten).
   - Voraussetzungen: Schritte 1–5.
7. **Doku:** `docs/help/tv-sender.md`, `README.md`, `RELEASE_NOTES.md` (EN+DE), `changes.log`.

## Tests

### Neue Tests

| Test / Hilfsmethode | Testklasse | Was wird geprüft / bereitgestellt? |
|--------------------|------------|-------------------------------------|
| `ClassifyAllAsync_TrailerSuffix_LinksToMovie_NotAsMovie` | `LocalMediaSourceClassifierTests_Trailer` (neu) | `Film.mkv`+NFO+`Film-trailer.mkv` → 1 Movie, 2 Links (`IsTrailer` korrekt), kein zweiter Film |
| `ClassifyAllAsync_TrailersFolder_LinksToParentMovie` | dto. | `trailers/` Kind-Collection → `IsTrailer`-Links zum Film des Elternordners |
| `ClassifyAllAsync_TrailerSuffix_NotLinkedAsMainItem` | dto. | Trailer-Suffix-Datei wird nie Hauptdatei (`!IsTrailer`-Filter belegt) |
| `EnsureCoverage_TrailerFiller_AirsBeforeScheduledMovie` | `ProgramScheduleServiceTests_Trailer` (neu) | Füller-Regel Trailer → Trailer-Slot in Lücke vor Filmstart, `MovieId` = Film |
| `EnsureCoverage_TrailerForUnscheduledMovie_NotAired` | dto. | Trailer zu ungeplantem Film taucht nicht auf |
| `EnsureCoverage_NoLeadMatch_FallsBackToNormalFiller` | dto. | Filmstart > LeadDays → normale Füller-Materialart füllt |
| `EnsureCoverage_TrailerWithoutDuration_NotAired` | dto. | Unprobed Trailer → übersprungen |
| `Reconcile_TrailerSlot_RemainsValidWhenStillExpandable` | dto. (optional) | Bestehender Trailer-Füller-Slot überlebt Reconcile |
| `GetAllowedMaterialTypes_Filler_IncludesTrailer` + `ValidateRuleInput_Trailer_ForFiller_OK` / `…_ForSeries_Rejected` | `ProgramRuleServiceTests_Validation` | Materialart zulässig/nicht zulässig je Regelart |
| `ReadFromAsync_LegacyBackupWithoutIsTrailerColumn_RestoresSuccessfully` | `VideoWebPlayerBackupDataTests` | Alt-Backup ohne Spalte restaurierbar (Default false) |
| bUnit: Trailer-Option im Editor wählbar + Label | `TVSenderAdminTests` | Materialart in Füller-Regel auswählbar |

### Betroffene bestehende Tests

| Test / Testklasse | Grund der Anpassung |
|-------------------|---------------------|
| Keine | Signaturänderungen sind intern (private Methoden); `GetAllowedMaterialTypes(Filler)` erweitert die Liste — kein bestehender Test pinnt die exakte Menge (Prüfung im Implementierungslauf, ggf. Assertion-Anpassung) |

### E2E-Tests (primärer Funktionsnachweis)

| Priorität | Szenario | Testdatei / Testklasse | Abgedecktes Akzeptanzkriterium | Warum E2E nötig ist |
|-----------|----------|------------------------|-------------------------------|-------------------|
| Pflicht | Füller-Regel mit Material „Trailer anstehender Filme" im Editor anlegen und speichern | `TVSenderAdminE2ETests` (neuer Fall bzw. bestehender Regel-Editor-Fall erweitert) | AK 5 | Einziger anwendersichtbarer Fluss; serverseitige Validierung über `ProgramRuleService` mitgeprüft |

Bestehende E2E-Tests: „Keine." — bestehende `TVSenderAdminE2ETests` prüfen Regel-Editor-Fluss
generisch und laufen weiter.

## Offene Punkte

Keine.
