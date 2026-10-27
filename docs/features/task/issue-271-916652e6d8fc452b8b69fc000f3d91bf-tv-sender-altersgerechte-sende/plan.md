# Umsetzungsplan: TV-Sender — altersgerechte Sendezeiten (Jugendschutzfenster, Issue #271)

## Übersicht

Die Sendeplan-Erzeugung (`ProgramScheduleService.EnsureCoverageAsync`/`ReconcileAsync`) wird um eine konfigurierbare Jugendschutzfenster-Regelung erweitert: Ein Titel darf nur materialisiert werden, wenn sein `AgeRatingMinAge` (Quelle: `Movie`/`TVShow` via `IAgeRated`, normalisiert durch `AgeRatingNormalizer`) zur lokalen Sendezeit des geplanten Sendeplatzes passt. Das Fensterschema folgt der deutschen Jugendschutz-Konvention (ab 20:00 FSK 12, ab 22:00 FSK 16, FSK 18 nur 23:00–06:00), wird senderweit über `Setup`-Spalten und `ProgramSettingsService` konfiguriert und erhält einen eigenen Diagnosegrund „kein altersgerechtes Material im Fenster" in `TVSenderAdmin.razor`. Betroffen sind beide Materialisierungspfade (`MaterializeOccurrence`, `MaterializeOccurrenceEvicting`), beide Auswahlvarianten (flat/interleaved), der Füller- und Clip-Fill-Pfad sowie die Airing-Reproduzierbarkeitsprüfung des Reconcile.

## Designentscheidungen

| Komponente / Bereich | Gewählter Ansatz | Begründung |
|----------------------|-----------------|------------|
| Regelungsumfang | Senderweit einheitlich — kein neues `ProgramRule`-Feld, keine Regel-Editor-Anpassung | Jugendschutz knüpft an die Sendezeit, nicht an die Regel; Freigabe-Ausnahmen je Regel würden die Schutzwirkung aushebeln. Eine „Abend-Regel ohne Begrenzung" ist bereits über `TimeOfDay`/Priorität abbildbar. Spart zudem eine weitere Migration und Editor-Komplexität. |
| Fensterschema | Value Object `ProgramScheduleAgeWindowSettings` (Record mit vier `TimeOnly`-Werten: früheste lokale Sendezeit FSK 12/16/18 + gemeinsames Nachtende) plus statischer Auswertehilfsklasse `ProgramScheduleAgeWindow` | Das Schema ist ein identitätsloser Wert; die Auswahl braucht eine reine O(1)-Prüfung pro Kandidat ohne DB-Zugriff. `AgeRatingNormalizer` bleibt die einzige Stufenquelle — das Fensterschema arbeitet ausschließlich auf dem bereits normalisierten `AgeRatingMinAge`. |
| Zuordnung `AgeRatingMinAge` → Fenster | Größte konfigurierte Stufe ≤ `MinAge`; `MinAge < 12` ⇒ jederzeit erlaubt; `null` ⇒ Unbekannt-Modus | Schwellen semantisch am Mindestalter festgemacht: `R`→17 fällt ins FSK-16-Fenster (22:00), `PG-13`→13 ins FSK-12-Fenster (20:00), `NC-17`→18 ins FSK-18-Fenster (23:00) — deckt sich mit den Beispielen der Anforderung. |
| Standard bei unbekannter Freigabe | `RestrictToNightWindow` als Default des `ProgramScheduleUnknownRatingMode` | Konservativ: unklassifiziert heißt nicht als kindtauglich nachgewiesen; entspricht der Projekt-Konvention restriktiver Defaults (`ProgramScheduleContentScope.SharedOnly`). Der Admin kann auf `Unrestricted` umstellen oder Material klassifizieren. Gilt auch für Material ohne `IAgeRated` (Clips, Trailer, Füller — ausgenommen `LiveStreamStandbyMediaItemId`, siehe eigene Entscheidungszeile) — diese Konsequenz wird in der Hilfe dokumentiert. |
| Prüfzeitpunkt | Zulässigkeit wird gegen die lokale Startzeit des jeweiligen Sendeplatzes (`slotStart` im Scan, via `TimeZoneInfo.Local`) geprüft — nicht gegen den Occurrence-Start | Identisch zur Boundary-Regel „a started item always completes": ein Item, das seine Sendezeit überschreitet, läuft zu Ende; ein späteres Item derselben Airing darf in ein später aufgehendes Fenster hineinwachsen. `TimeZoneInfo.Local` ist konsistent zu `BuildOccurrences`/`ProgramRule.TimeOfDay`. |
| Skip- vs. Defer-Semantik | Differenziert nach Strang-Ordnung: geordnete Stränge (`ContentType` `TVShowEpisode`, `PlaylistEntry`) ⇒ blockierter Kopf lässt den Strang/das Occurrence ruhen (Defer, wie die Bound-Regel `done[s]`); ungeordnete Ringe (Movies, Collections, Genre-„other"-Strang, Clips) ⇒ blockierter Titel wird übersprungen (Ring-Offset ohne `selected++`) | „Ausstrahlungsreihenfolge bleibt gewahrt" (Episoden/Playlists dürfen nie umgeordnet werden) und gleichzeitig „kein dauerhaftes Verklemmen" (eine Filmrotation mit einem unzulässigen Kopf darf das Occurrence nicht blockieren). Bei ungeordneten Ringen werden übersprungene Positionen mit-konsumiert (`NextIndex` hinter die letzte *gescannte* Position) — der Titel wird beim nächsten Ringumlauf gegen die dann aktuelle lokale Sendezeit erneut angeboten. **Wichtig:** `LastPlaced` muss dabei immer das letzte *genommene* Item bleiben, nie eine übersprungene Position — sonst persistiert `PersistCursors` einen nie gesendeten Titel und `NextIndexFor` (= `pos(LastPlaced) + 1`) würde ihn beim nächsten Lauf dauerhaft hinter den Cursor setzen; das Wiederangebot entfiele. |
| Material ohne `IAgeRated` (Clips, Trailer, `MediaSourceClips`) | Gelten als „unbekannte Freigabe" und folgen dem konfigurierten `ProgramScheduleUnknownRatingMode` | Annahme der Anforderung wird übernommen; Trailer erben bewusst nicht die Freigabe ihres Films — die Zuordnung `MovieId` → Rating wäre spekulativ und ist fachlich nicht angefordert. **Eingrenzung:** `LiveStreamStandbyMediaItemId` ist hiervon ausgenommen — Standby greift im Live-TV-Ausfallpfad und wird nicht über die Sendeplan-Materialisierung ausgewählt; die Fensterregel gilt ausschließlich für materialisierte Sendeplätze (in der Hilfe so zu formulieren). |
| Reproduzierbarkeit (`AiringReproducible*`) | Dieselbe Fensterprüfung wird in die Re-Simulation der Auswahl eingebaut (Parameter über `ReproContext`); zusätzlich wird die positionsbasierte Verifikation lückentolerant gemacht (siehe eigene Entscheidungszeile unten) | Ohne konsistente Fensterlogik in der Validierung würden beim Reconcile bestehende, regelkonform geplante Airings massenhaft als nicht reproduzierbar `Displaced`. |
| Lücken in der Airing-Reproduzierbarkeit (Folge des Skips) | Genommene Positionen eines ungeordneten Rings dürfen in der Prüfung **nicht-konsekutiv** sein: eine Zwischenposition gilt als übersprungen, wenn sie zur Sendezeit des folgenden Picks unzulässig war — übersprungene Positionen werden nämlich zum selben `slotStart` geprüft wie der nächste genommene Titel (der Skip rückt `slotStart` nicht vor), die Entscheidung ist also exakt nachprüfbar. Geordnete Stränge behalten strikte Kontiguität | Zwingende Folge des Mit-Konsumierens: `InterleavedAiringTailReproducible` verlangt heute pro Strang konsekutive Ring-Indizes (`idx == firstIndex + countInAiring`) und `FlatAiringTailReproducible` setzt Rückwärtsschritte (Positionen) mit genommenen Items gleich (`consumed = expected.Count - k`, `headIndex = idx - k`). Mit Skip-Positionen in einem Airing liefern beide falsch-negativ ⇒ massenhafte Fehl-Displacements. Die Alternative, Skips nicht zu konsumieren, löst das Kontiguitätsproblem nicht (genommene Positionen bleiben nicht-konsekutiv) und verletzt zusätzlich die „kein erneutes Anbieten in derselben Rotation"-Semantik. Unklare Hypothesen bleiben konservativ: verwirft ⇒ displaced (kostet Rematerialisierung, niemals ein falsch behaltenes Airing). |
| Cursor-Rewind (`RewindCursor`/`RewindRing`) unter Scan-Commit | **Unverändert** — mit definierter Semantik: `RewindRing` setzt `NextIndex` auf den Index des frühesten displaced Inhalts zurück; mit-konsumierte Skip-Positionen *hinter* diesem Index werden dadurch erneut angeboten, Skip-Positionen *davor* bleiben konsumiert | Da Skip-Positionen nicht protokolliert werden, kennt der Rewind nur platzierte Inhalte — ein „nur genommene Positionen zurückspulen" ist nicht implementierbar. Das Wiederangebot übersprungener Titel nach einem Displacement ist fachlich zulässig (nie gesendet ⇒ erneutes Anbieten entspricht dem Akzeptanzkriterium) und ungefährlich: jede erneute Prüfung läuft wieder gegen die aktuelle Sendezeit, ein Out-of-Window-Placement ist nicht möglich; die Ausstrahlungsreihenfolge genommener Titel bleibt gewahrt, weil displaced Titel in Ring-Reihenfolge wieder drankommen. Die Bedingung `ring.NextIndex > idx` bleibt korrekt (ein displaced Index war genommen, also gescannt). |
| Einstellungsmodell | Senderweit über `Setups`-Spalten + `ProgramSettingsService` (Get/Update mit `Enum.IsDefined`-Normalisierung) — exakt das `ProgramScheduleContentScope`-Muster | Gleiche Konfigurationsebene wie `ProgramScheduleHorizonHours`/`HolidayFederalState`/`ProgramScheduleContentScope`; kein neues Konfigurationsparadigma. |

## Programmabläufe

### Fenster-Zulässigkeit eines Titels bestimmen

1. `ProgramScheduleAgeWindow.IsAllowedAt(item, utcStart, settings, mode)` wird je Auswahl-Kandidat aufgerufen.
2. `utcStart` wird via `TimeZoneInfo.Local` in lokale Zeit umgerechnet.
3. `item.AgeRatingMinAge is null` ⇒ Modus entscheidet: `Unrestricted` ⇒ erlaubt; `RestrictToNightWindow` ⇒ nur im FSK-18-Fenster (`lokale Zeit ≥ Fsk18` oder `< NightEnd`).
4. `MinAge` wird der größten konfigurierten Stufe ≤ `MinAge` zugeordnet; keine Stufe ≤ `MinAge` ⇒ jederzeit erlaubt.
5. Erlaubt ist der Titel, wenn die lokale Zeit im Intervall `[früheste Sendezeit der Stufe, NightEnd)` über den Tagewechsel liegt (`t ≥ earliest || t < NightEnd`).

Beteiligte Klassen: `ProgramScheduleAgeWindow`, `ProgramScheduleAgeWindowSettings`, `ProgramScheduleUnknownRatingMode`, `SchedulableItem`

### Flache Auswahl mit Fensterprüfung (`SelectAiringItems`)

1. `SelectItems` reicht Fenster-Einstellungen und Modus an `SelectAiringItems` durch.
2. Pro Scan-Position (`items[(headIndex + ring) % count]`) wird die Zulässigkeit gegen den laufenden `slotStart` geprüft.
3. Zulässig ⇒ bisherige Logik (Bound-/Grace-Prüfung, aufnehmen, `slotStart` vorrücken).
4. Unzulässig + ungeordneter Ring ⇒ überspringen (`ring++` ohne `selected++`), der Titel zählt nicht als genommen.
5. Unzulässig + geordneter Ring ⇒ `break` (das Occurrence bzw. die Rest-Airing endet — der Kopf wartet, Reihenfolge bleibt strikt).
6. `SelectItems` liefert neben `Taken`/`TakenCounts` zusätzlich den Scan-Umfang je Strang (Anzahl gescannter Positionen inkl. Übersprungener) sowie je genommenem Item seine Scan-/Ringposition (Scan-Trace) zurück — der Trace wird für die lückentolerante Reproduzierbarkeitsprüfung gebraucht (siehe Reconcile-Ablauf).
7. `CommitTaken` setzt `NextIndex` je Strang auf „Position hinter der letzten gescannten Position" und `LastPlaced` auf das letzte tatsächlich genommene Item — übersprungene Positionen werden mit-konsumiert.
8. Liefert das Fenster kein einziges zulässiges Item ⇒ `Taken` leer, `MaterializeOccurrence*` schreibt nichts und committet auch nichts (Strang-Kopf bleibt stehen und wird beim nächsten Occurrence erneut gescannt); `FillGaps`/Füller decken das Fenster, der Diagnosezähler wird später ausgewiesen.

Beteiligte Klassen: `SelectItems`, `SelectAiringItems`, `CommitTaken`, `StrandState`, `RuleState`, `MaterializeOccurrence`, `MaterializeOccurrenceEvicting`

### Interleaved-Auswahl mit Fensterprüfung (`SelectAiringItemsInterleaved`)

1. Pro Strang-Zug wird der Kopf `items[(NextIndex + taken[s] + skip[s]) % count]` gegen den aktuellen `slotStart` geprüft.
2. Geordneter Strang + blockierter Kopf ⇒ `done[s] = true` (Strang ruht für dieses Occurrence — analog zur Bound-Behandlung).
3. Ungeordneter Strang („other") + blockierter Kandidat ⇒ `skip[s]++`, nächste Ringposition prüfen; ein voller Ringumlauf ohne Zulässiges ⇒ Strang ruht.
4. `takenCounts`/`skip`-Umfang je Strang (genommen + gescannt, je Pick seine Ringposition) gehen in die Scan-Commit-Semantik von `CommitTaken` ein und liefern den Scan-Trace für die lückentolerante Reproduzierbarkeitsprüfung.

Beteiligte Klassen: `SelectAiringItemsInterleaved`, `StrandState`, `RuleState`, `CommitTaken`

### Füller- und Clip-Fill-Pfad mit Fensterprüfung

1. `FillGaps`: `PeekAt(0)`-Kandidat eines Füller-Rings wird gegen `fillAt` geprüft; unzulässige Füller-Items werden per Ring-Offset weitergesucht (`PeekAt(offset)`); findet kein Ring ein zulässiges Item, bleibt die Restlücke (`RemainingGapSeconds`).
2. `InsertClipFill`/`FillAiringSegments`: das `fits`-Prädikat von `NextClipItem` wird um die Fensterprüfung gegen `pos` ergänzt; Clip-Items tragen `AgeRatingMinAge = null` ⇒ Unbekannt-Modus.
3. Trailer-Ring (`group.IsTrailer`): der Vorlauf-`FindLeadMatchOffset` bleibt unverändert; die Fensterprüfung kommt zusätzlich ins `fits`-Prädikat.

Beteiligte Klassen: `FillGaps`, `InsertClipFill`, `FillAiringSegments`, `NextClipItem`, `ClipFillContext`

### Reconcile / Airing-Reproduzierbarkeit

1. `ReconcileAsync` lädt die Fenster-Einstellungen einmal und reicht sie in `ReproContext` durch.
2. `FlatAiringTailReproducible` und `InterleavedAiringTailReproducible` wenden bei der Re-Simulation der Auswahl dieselbe Fensterprüfung an (identischer Skip-/Defer-Pfad wie `SelectAiringItems*`).
3. **Lückentoleranz (Folge des Skips):** die positionsbasierte Verifikation beider Tail-Checks wird für ungeordnete Ringe lückentolerant gemacht —
   - `InterleavedAiringTailReproducible`: die Strang-Kontiguität (`idx == firstIndex + countInAiring`) gilt weiter für geordnete Stränge; für ungeordnete Stränge („other") sind Lücken erlaubt, wenn jede Zwischenposition zur Sendezeit des folgenden Picks unzulässig war (der Skip wird zum selben `slotStart` geprüft wie der folgende Pick ⇒ exakt nachprüfbar). Die Kopf-Rekonstruktion `firstIndex - prefixCount` zählt den Scan-Umfang (genommen + übersprungen) statt nur der Picks — die Präfix-Übersprungenen ergeben sich aus derselben Lücken-Regel an den rekonstruierten Präfix-Pick-Zeiten.
   - `FlatAiringTailReproducible`: der Rückwärtslauf trennt „genommene Items zurück" (treiben `skipped`-Dauer und `consumed = expected.Count - k`) von „Ringpositionen zurück" (treiben `headIndex`): Positionen, die zur Sendezeit des nächsten genommenen Items unzulässig waren, werden als übersprungen gewertet — sie zählen als konsumierte Position (gehen in `headIndex` ein), aber nicht als genommenes Item und nicht in die Sendezeit-Rekonstruktion. Grundlage ist der Scan-Trace der Auswahl (welcher Ringposition entspricht ein genommenes Item).
   - Unklare Hypothesen (z. B. zulässige, aber nicht genommene Zwischenposition aus einem früheren, gebund-abgebrochenen Scan) werden wie bisher konservativ verworfen ⇒ `Displaced` + Rematerialisierung statt falsch behaltenem Airing.
4. **Rewind (`RewindCursor`/`RewindRing`)** bleibt unverändert: `RewindTo(idx)` setzt `NextIndex` auf den Index des frühesten displaced Inhalts — mit-konsumierte Skip-Positionen hinter `idx` werden bei der anschließenden Rematerialisierung erneut angeboten (gewollt, siehe Designentscheidung), dahinter bleibt der Ringverbrauch unverändert.
5. Ein Airing, das zum Zeitpunkt seiner Planung noch zulässig war, jetzt aber gegen die (ggf. geänderte) Fensterkonfiguration verstößt, gilt als nicht reproduzierbar und wird `Displaced` — die Rematerialisierung erfolgt über `MaterializeOccurrenceEvicting` mit derselben Fensterprüfung.

Beteiligte Klassen: `ReconcileAsync`, `ReconcileWindow`, `ReproContext`, `AiringReproducible`, `FlatAiringTailReproducible`, `InterleavedAiringTailReproducible`, `RewindCursor`, `RewindRing`, `MaterializeOccurrenceEvicting`

### Regel-Diagnose „kein altersgerechtes Material im Fenster"

1. `GetRuleDiagnosesAsync` lädt die Fenster-Einstellungen einmal.
2. Je Regel wird die Expansion wie bisher mit `SummaryCounters` gefahren; zusätzlich wird die Fenster-Zulässigkeit der expandierten Items gegen die lokale Startzeit der Regel-Auswertung (`rule.TimeOfDay` in lokaler Zeit, nächste anstehende Occurrence) geprüft: die Anzahl der zum Regel-Zeitfenster unzulässigen Items geht in das neue `ProgramRuleDiagnosis.AgeWindowBlockedCount`.
3. `DiagnosisLabel` in `TVSenderAdmin.razor` weist bei `AgeWindowBlockedCount > 0` den Grund „{n} nicht altersgerecht im Sendefenster" aus (bzw. „kein altersgerechtes Material im Fenster", wenn alle Kandidaten betroffen sind).

Beteiligte Klassen: `GetRuleDiagnosesAsync`, `ProgramRuleDiagnosis`, `SummaryCounters`, `TVSenderAdmin.DiagnosisLabel`

### Einstellungen laden und ändern

1. `EnsureCoverageAsync`, `ReconcileAsync` und `GetRuleDiagnosesAsync` laden `GetProgramScheduleAgeWindowsAsync` + `GetProgramScheduleUnknownRatingModeAsync` jeweils einmal zu Laufbeginn (analog `LoadAccessContextAsync`/`GetProgramScheduleContentScopeAsync`).
2. Admin speichert in `SaveChannelSettingsAsync` (`TVSenderAdmin.razor`, Planungs-Karte): `UpdateProgramScheduleAgeWindowsAsync` + `UpdateProgramScheduleUnknownRatingModeAsync`, danach `ScheduleSignal.NotifyScheduleChanged()` → der nächste Lauf plant mit neuen Fenstern.
3. Get-Methoden normalisieren ungültige gespeicherte Werte auf das Standard-Schema (20:00/22:00/23:00, Nachtende 06:00; Modus-Default `RestrictToNightWindow`).

Beteiligte Klassen: `ProgramSettingsService`, `Setup`, `ProgramScheduleService`, `TVSenderAdmin`, `ScheduleSignal`

## Neue Klassen

| Klasse | Typ | Zweck |
|--------|-----|-------|
| `ProgramScheduleUnknownRatingMode` | Enum (`VideoWebPlayer/Data/`) | Verhalten bei `AgeRatingMinAge == null`: `Unrestricted` (jederzeit erlaubt) / `RestrictToNightWindow` (nur FSK-18-Fenster). Muster `ProgramScheduleContentScope`. |
| `ProgramScheduleAgeWindowSettings` | Record (`VideoWebPlayer/Services/`) | Value Object mit den vier `TimeOnly`-Werten (früheste lokale Sendezeit FSK 12/16/18 + Nachtende) inkl. `Default`-Instanz. |
| `ProgramScheduleAgeWindow` | Statische Hilfsklasse (`VideoWebPlayer/Services/`) | Auswertung: `IsAllowedAt(minAge, utcStart, settings, mode)`, Stufen-Zuordnung `MinAge` → Fenster, Zählung blockierter Kandidaten für die Diagnose. |

## Änderungen an bestehenden Klassen

### `Setup` (Datenmodellklasse, `VideoWebPlayer/Data/Setup.cs`)

- **Neue Eigenschaften:** `ProgramScheduleAgeWindow12Local` (`TimeOnly`, Default 20:00) — früheste Sendezeit FSK-12-Fenster
- **Neue Eigenschaften:** `ProgramScheduleAgeWindow16Local` (`TimeOnly`, Default 22:00) — früheste Sendezeit FSK-16-Fenster
- **Neue Eigenschaften:** `ProgramScheduleAgeWindow18Local` (`TimeOnly`, Default 23:00) — früheste Sendezeit FSK-18-Fenster
- **Neue Eigenschaften:** `ProgramScheduleAgeWindowNightEndLocal` (`TimeOnly`, Default 06:00) — gemeinsames Ende aller Nachtfenster
- **Neue Eigenschaften:** `ProgramScheduleUnknownRatingMode` (`ProgramScheduleUnknownRatingMode`) — Verhalten bei unbekannter Freigabe

### `ProgramSettingsService` (Service, `VideoWebPlayer/Services/ProgramSettingsService.cs`)

- **Neue Methoden:** `GetProgramScheduleAgeWindowsAsync` — liest die vier `Setups`-Spalten, normalisiert ungültige/unvollständige Werte auf `ProgramScheduleAgeWindowSettings.Default`; Rückgabe `ProgramScheduleAgeWindowSettings`
- **Neue Methoden:** `UpdateProgramScheduleAgeWindowsAsync` — validiert die Stufen-Ordnung und das Nachtende (siehe Validierungsregeln), schreibt die vier Spalten
- **Neue Methoden:** `GetProgramScheduleUnknownRatingModeAsync` — `Enum.IsDefined`-Normalisierung auf den Default
- **Neue Methoden:** `UpdateProgramScheduleUnknownRatingModeAsync` — `Enum.IsDefined`-Validierung, `ArgumentException` bei unbekanntem Wert

### `ProgramScheduleService` (Service, `VideoWebPlayer/Services/ProgramScheduleService.cs`)

- **`SchedulableItem`:** neues Feld `AgeRatingMinAge` (`int?`) — Fenster-Eingang ohne Nachabfragen
- **`SummaryCounters`:** neuer Zähler `BlockedByAgeWindow` (`int`) — Diagnose-Grundlage; wird zusätzlich in `ProgramScheduleRunSummary`/`ProgramScheduleReconcileSummary` übernommen (Sichtbarkeit in Logs/Zusammenfassungen — beide sind `readonly record struct`s, Konstruktionsstellen liegen nur im Service: `SummaryCounters.ToSummary()` und der `ReconcileAsync`-Aufruf)
- **`StrandState`/`RuleState`:** Ableitung `PreservesOrder` aus den `ContentType`s des Strangs (`TVShowEpisode`/`PlaylistEntry` ⇒ geordnet); neue Commit-Variante mit zwei Eingängen — Scan-Umfang (treibt `NextIndex`) und letztes *genommenes* Item (setzt `LastPlaced` explizit; `LastPlaced` darf nie auf eine übersprungene Position zeigen, sonst konsumiert `NextIndexFor` einen nie gesendeten Titel dauerhaft). `RewindTo` bleibt unverändert.
- **`SelectItems` / `SelectAiringItems` / `SelectAiringItemsInterleaved`:** neue Parameter `ProgramScheduleAgeWindowSettings` + `ProgramScheduleUnknownRatingMode`; Fensterprüfung pro `slotStart`; Rückgabe des Scan-Umfangs je Strang plus Scan-Trace (Ringposition je genommenem Item) für die Repro-Prüfung
- **`CommitTaken`:** Umstellung auf Scan-basiertes Commit (siehe oben)
- **`MaterializeOccurrence` / `MaterializeOccurrenceEvicting`:** Fenster-Einstellungen in `SelectItems` durchreichen (kein Verhaltensunterschied zwischen Extend-only- und Reconcile-Pfad)
- **`RewindCursor` / `RewindRing`:** **keine Codeänderung** — Semantik unter Scan-Commit geprüft und in den Designentscheidungen festgelegt (mit-konsumierte Skip-Positionen hinter dem Rewind-Punkt werden erneut angeboten); Absicherung über den Rewind-Test
- **`ExpandShowAsync` / `ExpandMovieAsync` / `ExpandMovieCollectionAsync` / `ExpandGenreAsync` / `ExpandPlaylistAsync`:** `AgeRatingMinAge` in die `SchedulableItem`-Projektion aufnehmen; bei Episoden-Quellen die Freigabe der `TVShow` mitladen (Join über `TVShowSeason` bzw. vorhandene `TVShowSeasonId`/`ShowId`-Zuordnung); `TryMovieItem`/`ToItem` setzen das Feld
- **`FillGaps`:** Fensterprüfung je `fillAt`; unzulässige Füller-Items per `PeekAt(offset)` weitersuchen
- **`InsertClipFill` / `FillAiringSegments` / `NextClipItem`:** Fensterprüfung im `fits`-Prädikat gegen `pos`; `ClipFillContext` trägt Fenster-Einstellungen + Modus
- **`ReproContext` / `AiringReproducible` / `FlatAiringTailReproducible` / `InterleavedAiringTailReproducible`:** Fenster-Einstellungen + Modus in die Re-Simulation aufnehmen, identische Skip-/Defer-Semantik; positionsbasierte Verifikation lückentolerant machen (Unzulässigkeits-Beweis der Zwischenpositionen zum Pick-Zeitpunkt; Scan-Umfang statt Pick-Anzahl in der Kopf-Rekonstruktion — siehe Programmablauf „Reconcile")
- **`EnsureCoverageAsync` / `ReconcileAsync` / `GetRuleDiagnosesAsync`:** Fenster-Einstellungen einmal pro Lauf via `_settings` laden und durchreichen
- **`GetRuleDiagnosesAsync`:** `AgeWindowBlockedCount` pro Regel befüllen (Fenster-Zulässigkeit der expandierten Items gegen die lokale `rule.TimeOfDay`-Startzeit)

### `ProgramRuleDiagnosis` (Record, `VideoWebPlayer/Services/IProgramScheduleService.cs`)

- **Neuer Parameter:** `AgeWindowBlockedCount` (`int`) — Kandidaten, die zum Regel-Zeitfenster nicht altersgerecht planbar sind (XML-Doc ergänzen)

### `TVSenderAdmin.razor` (Komponente)

- **Neue UI-Elemente (Planungs-Karte, Reiter „Einstellungen"):** vier Zeit-Eingabefelder (Muster `tvsender-horizon-hours`-`input`, IDs `tvsender-agewindow-12`/`16`/`18`/`-nightend`) + ein `select` für den Unbekannt-Modus (Muster `tvsender-content-scope` mit Erklärtext) — Bind-Felder, Laden beim Komponenten-Init, Speichern in `SaveChannelSettingsAsync`
- **Geänderte Methoden:** `SaveChannelSettingsAsync` — ruft zusätzlich `UpdateProgramScheduleAgeWindowsAsync` + `UpdateProgramScheduleUnknownRatingModeAsync`
- **Geänderte Methoden:** `DiagnosisLabel` — zusätzlicher Teil „{n} nicht altersgerecht im Sendefenster" bei `AgeWindowBlockedCount > 0`

### `VideoWebPlayerBackupData` (Service, `VideoWebPlayer/Services/Backups/`)

- **`OptionalRestoreColumns`:** fünf neue `Setups.*`-Einträge für die neuen `Setup`-Spalten

## Datenbankmigrationen

| Migrationsname | Betroffene Tabellen/Spalten | Beschreibung der Änderung |
|----------------|----------------------------|---------------------------|
| `AddProgramScheduleAgeWindows` | `Setups`: `ProgramScheduleAgeWindow12Local`, `ProgramScheduleAgeWindow16Local`, `ProgramScheduleAgeWindow18Local`, `ProgramScheduleAgeWindowNightEndLocal` (je `TimeOnly`), `ProgramScheduleUnknownRatingMode` (`int`) | Jugendschutzfenster-Einstellungen und Unbekannt-Modus senderweit |

## Validierungsregeln

| Feld / Objekt | Regel | Fehlerfall |
|---------------|-------|------------|
| `UpdateProgramScheduleAgeWindowsAsync` | Früheste Sendezeiten müssen monoton aufsteigend sein: `Fsk12 ≤ Fsk16 ≤ Fsk18` | `ArgumentException` bei Verletzung |
| `UpdateProgramScheduleAgeWindowsAsync` | `NightEnd` muss vor der frühesten Fenster-Startzeit liegen (`NightEnd < Fsk12`), damit das Nachtfenster eindeutig ist | `ArgumentException` bei Verletzung |
| `UpdateProgramScheduleUnknownRatingModeAsync` | Wert muss in `ProgramScheduleUnknownRatingMode` definiert sein (`Enum.IsDefined`) | `ArgumentException` bei unbekanntem Wert |
| UI-Zeitfelder (`tvsender-agewindow-*`) | Pflichtfelder, `TimeOnly`-Eingabe; Fehlermeldung der Service-Validierung wird wie bisherige Einstellungsfehler in der Seite angezeigt | Speichern schlägt mit Fehlermeldung fehl |
| `Get…`-Methoden | Ungültig gespeicherte Kombinationen (korrumpierte Zeile) ⇒ Rückfall auf `Default` | Keine — Normalisierung wie bei `ProgramScheduleContentScope` |

## Konfigurationsänderungen

| Eintrag | Typ | Standardwert | Zweck |
|---------|-----|--------------|-------|
| `Setups.ProgramScheduleAgeWindow12Local` | `TimeOnly` | 20:00 | Früheste lokale Sendezeit für Titel ab `MinAge` 12–15 |
| `Setups.ProgramScheduleAgeWindow16Local` | `TimeOnly` | 22:00 | Früheste lokale Sendezeit für Titel ab `MinAge` 16–17 |
| `Setups.ProgramScheduleAgeWindow18Local` | `TimeOnly` | 23:00 | Früheste lokale Sendezeit für Titel ab `MinAge` ≥ 18 |
| `Setups.ProgramScheduleAgeWindowNightEndLocal` | `TimeOnly` | 06:00 | Gemeinsames Ende der Nachtfenster |
| `Setups.ProgramScheduleUnknownRatingMode` | `ProgramScheduleUnknownRatingMode` | `RestrictToNightWindow` | Verhalten bei `AgeRatingMinAge == null` (gilt auch für Material ohne `IAgeRated`: Clips, Trailer, Füller — nicht für `LiveStreamStandbyMediaItemId`, das nicht über die Sendeplan-Materialisierung läuft) |

Keine `appsettings`-Änderungen erforderlich — Einstellungen laufen über `Setups`-Zeile/`ProgramSettingsService`.

## Seiteneffekte und Risiken

- **Rollout/Upgrade:** bereits geplante Slots, die gegen das neue Fenster verstoßen, werden beim nächsten `ReconcileAsync` als nicht reproduzierbar `Displaced` — gewollt, muss in Hilfe/Release Notes erwähnt werden. Voraussetzung: Fensterprüfung in Repro-Pfad und Auswahl absolut identisch, sonst massenhafte Fehl-Displacements.
- **Geordnete Stränge (Episoden/Playlists):** ein dauerhaft blockierter Kopf (z. B. tägliche 15:00-Regel, deren nächste Episode FSK 18 ist) verklemmt den Strang fachlich korrekt — sichtbar wird das über den neuen Diagnosegrund, nicht über stilles Überspringen.
- **Ungeordnete Ringe:** übersprungene Titel werden mit-konsumiert und erst beim nächsten Ringumlauf erneut angeboten; für Regeln, die nur zu Tageszeiten senden, kann ein hoch eingestufter Titel faktisch nie drankommen — über die Diagnose sichtbar. **Persistenz-Einschränkung:** „mit-konsumiert" gilt nur lauf-intern — der persistierte Cursor ist `LastPlaced`-basiert, am Laufende mit-gescannte aber nicht genommene Positionen hinter dem letzten Taken werden beim nächsten Lauf ohnehin erneut gescannt (gewollt: verhindert, dass ein persistierter Zustand einen nie gesendeten Titel dauerhaft überspringt).
- **Rewind unter Scan-Commit:** `RewindRing` setzt `NextIndex` auf den frühesten displaced Index zurück; mit-konsumierte Skip-Positionen *hinter* diesem Index werden damit früher als „nächster Ringumlauf" erneut angeboten — definiert und fachlich zulässig (nie gesendete Titel werden erneut geprüft, die Prüfung läuft wieder gegen die jeweils aktuelle Sendezeit; Ausstrahlungsreihenfolge genommener Titel bleibt gewahrt). Skip-Positionen *vor* dem Rewind-Punkt bleiben konsumiert. Abgesichert über den Rewind-Test.
- **Repro-Lückentoleranz:** die gelockerte Kontiguität verlangt den Unzulässigkeits-Beweis je Zwischenposition zum Pick-Zeitpunkt; weicht die dortige Auswertung von der Auswahl ab (fehlende/kopierte statt geteilte Logik), entstehen systematisch Fehl-Displacements oder falsch behaltene Airings — die gemeinsame `ProgramScheduleAgeWindow`-Auswertung ist Pflicht, kein Duplikat.
- **Clip-Fill/Trailer/Füller ohne `IAgeRated`:** folgen dem Unbekannt-Modus; im ausgelieferten Default `RestrictToNightWindow` füllen Clips/Trailer/Füller nur noch im Nachtfenster — Restlücken tagsüber werden als `RemainingGapSeconds` ausgewiesen (Hilfe dokumentiert das; Admin kann Modus lockern oder Material klassifizieren).
- **`ProgramRuleDiagnosis` ist ein positionsbasierter Record:** neuer Parameter ändert alle Konstruktionsstellen und ggf. Komponententests (`TVSenderAdminTests*`).
- **Signaturänderungen an `SelectAiringItems*`/`ReproContext`/`ClipFillContext`:** interne/private Signaturen — alle Aufrufer liegen in derselben Datei; bestehende Tests laufen über `EnsureCoverageAsync`/`ReconcileAsync` und bleiben unberührt, sofern sich das Plan-Ergebnis für freigabe-lose Testdaten nicht ändert (alle Test-Seeds ohne `AgeRatingMinAge` ⇒ Unbekannt-Modus entscheidet — siehe Risiko „Test-Seeds": mit konservativem Default würden **alle** bisherigen Schedule-Tests plötzlich nur nachts planen!).

- **Kritisch — Test-Seeds / Bestandsverhalten:** sämtliche bestehenden `ProgramScheduleServiceTests_*`-Seeds setzen keine `AgeRatingMinAge`-Werte, und der produktive Default ist fest `RestrictToNightWindow` — damit die 2526 grünen Tests nicht kippen, muss `ProgramScheduleTestDb` den `Setup`-Seed explizit auf `ProgramScheduleUnknownRatingMode = Unrestricted` setzen (oder `CreateMovieAsync`/`CreateShowAsync` standardmäßig `MinAge = 0` vergeben). Die Fenster-Tests setzen dann gezielt Ratings bzw. den Modus. **In jedem Fall** muss die Unbekannt-Regelung in Tests explizit gewählt werden.

## Umsetzungsreihenfolge

1. **`ProgramScheduleUnknownRatingMode`-Enum, `ProgramScheduleAgeWindowSettings`-Record und `ProgramScheduleAgeWindow`-Auswertehilfe anlegen**
   - Voraussetzungen: Keine (nur `TimeZoneInfo.Local`, analog `BuildOccurrences`)
   - Beschreibung: Neue Dateien unter `VideoWebPlayer/Data/` bzw. `VideoWebPlayer/Services/`; Auswertelogik inkl. Stufen-Zuordnung und Unit-testbarer `IsAllowedAt`-Prüfung.

2. **`Setup`-Spalten, `ProgramSettingsService`-Get/Update, EF-Migration und `OptionalRestoreColumns`**
   - Voraussetzungen: Schritt 1 (Enum- und Record-Typen für die Spalten/Signaturen)
   - Beschreibung: Fünf `Setups`-Spalten, Migration `AddProgramScheduleAgeWindows`, `OptionalRestoreColumns`-Einträge, Get/Update-Methoden mit Normalisierung/Validierung nach `ProgramScheduleContentScope`-Muster.

3. **`SchedulableItem.AgeRatingMinAge` + Freigabe-Laden in allen `Expand…`-Methoden**
   - Voraussetzungen: Keine (Felder existieren auf `Movie`/`TVShow`)
   - Beschreibung: `TryMovieItem`/`ToItem` übernehmen `Movie.AgeRatingMinAge`; Episoden-Quellen (`ExpandShowAsync`, `ExpandGenreAsync`, `ExpandPlaylistAsync`) laden `TVShow.AgeRatingMinAge` über den Season-/Show-Join mit; Clips/Trailer bleiben `null`.

4. **Fensterprüfung in der Auswahl + Commit-Umbau**
   - Voraussetzungen: Schritte 1–3 (`ProgramScheduleAgeWindow`, `AgeRatingMinAge` am Item, Einstellungen ladbar)
   - Beschreibung: `SelectItems`/`SelectAiringItems`/`SelectAiringItemsInterleaved` mit Fensterparametern; Skip-/Defer-Semantik; `StrandState`/`RuleState` `PreservesOrder` + Scan-Commit; `CommitTaken` umstellen; `MaterializeOccurrence`/`MaterializeOccurrenceEvicting` durchreichen; Laden der Einstellungen in `EnsureCoverageAsync`/`ReconcileAsync`.

5. **Fensterprüfung in `FillGaps` und Clip-Fill**
   - Voraussetzungen: Schritte 1, 3, 4
   - Beschreibung: `FillGaps` (Füller-Ringe mit `PeekAt(offset)`), `NextClipItem`-`fits`-Prädikat, `ClipFillContext`-Erweiterung.

6. **Fensterprüfung + Lückentoleranz im Reproduzierbarkeits-Pfad; Rewind-Verifikation**
   - Voraussetzungen: Schritt 4 (Scan-Trace/Scan-Umfang aus der Auswahl)
   - Beschreibung: `ReproContext` + `FlatAiringTailReproducible`/`InterleavedAiringTailReproducible` um die identische Fenster-Re-Simulation erweitern; positionsbasierte Verifikation lückentolerant machen (Unzulässigkeits-Beweis der Zwischenpositionen zum Pick-Zeitpunkt; Scan-Umfang statt Pick-Anzahl in der Kopf-Rekonstruktion). `RewindCursor`/`RewindRing` gegen die definierte Semantik verifizieren (keine Codeänderung erwartet — siehe Designentscheidung).

7. **Diagnose-Grund „kein altersgerechtes Material im Fenster"**
   - Voraussetzungen: Schritte 1–3
   - Beschreibung: `SummaryCounters.BlockedByAgeWindow` inkl. Übernahme in `ProgramScheduleRunSummary`/`ProgramScheduleReconcileSummary`, `ProgramRuleDiagnosis.AgeWindowBlockedCount`, Befüllung in `GetRuleDiagnosesAsync`.

8. **Admin-UI: Einstellungen + Diagnose-Anzeige**
   - Voraussetzungen: Schritte 2 (Settings-Service), 7 (Diagnose-Feld)
   - Beschreibung: Planungs-Karte um vier Zeitfelder + Modus-Select erweitern (Muster `tvsender-content-scope`), `SaveChannelSettingsAsync`, `DiagnosisLabel`.

9. **Unit-/Integrationstests**
   - Voraussetzungen: jeweils die zugehörigen Logik-Schritte; `ProgramScheduleTestDb` um `ageRatingMinAge`-Seed-Parameter erweitert (Teil dieses Schritts)
   - Beschreibung: Neue Testklasse `ProgramScheduleServiceTests_AgeWindow` gegen echtes SQLite, `ProgramSettingsServiceTests_AgeWindow`, Diagnose-Test, Backup-Regressionstest; Seed-Verhalten der bestehenden Suite umsetzen (Setup-Seed `Unrestricted` — siehe Seiteneffekte) und alle bestehenden Tests grün halten.

10. **E2E-Tests**
    - Voraussetzungen: Schritt 8
    - Beschreibung: `TVSenderAdminE2ETests`-Szenarien für Einstellungs-UI (Zeitfelder + Modus persistieren) und Diagnose-Anzeige.

11. **Dokumentation**
    - Voraussetzungen: Schritte 1–8
    - Beschreibung: `docs/help/tv-sender.md` (Fensterschema, Konfiguration, Unbekannt-Modus inkl. Eingrenzung auf materialisierte Sendeplätze — kein Standby —, Restlücken-Hinweis, Diagnose), `docs/help/tv-sender-datenmodell.md`, `docs/help/tv-sender-ablauf-technisch.md`, `README.md` und `docs/RELEASE_NOTES.md` bei Bedarf.

12. **Abschluss-Validierung (DoD)**
    - Voraussetzungen: Schritte 1–11
    - Beschreibung: `dotnet build VideoPlayer.sln` in Debug **und** `-c Release` fehlerfrei; volle Testsuite `dotnet test VideoWebPlayer.Tests/VideoWebPlayer.Tests.csproj` grün (einmalig geflackerter Playwright-Test wird einzeln wiederholt und im Bericht benannt, nicht deaktiviert).

## Tests

### Neue Tests

| Test / Hilfsmethode | Testklasse | Was wird geprüft / bereitgestellt? |
|--------------------|------------|-------------------------------------|
| `ageRatingMinAge`-Parameter in `CreateMovieAsync`/`CreateShowAsync` | `ProgramScheduleTestDb` (Helper) | Seeds mit gesetzter Freigabe; zusätzlich explizites Setzen von `ProgramScheduleUnknownRatingMode`/`AgeWindow`-Defaults im `Setup`-Seed |
| `ProgramScheduleAgeWindowTests` (Unit) | `ProgramScheduleAgeWindowTests` | `IsAllowedAt`: Stufen-Zuordnung (`MinAge` 0/6/12/16/18, Zwischenwerte 8/13/17), Nachtfenster über Tageswechsel (23:00–06:00), `null` je Modus |
| Gemischte Freigaben über Fenstergrenzen | `ProgramScheduleServiceTests_AgeWindow` | Akzeptanzkriterium: Tages-Regel plant nur ≤ FSK-6-Titel; FSK-16-Titel erst ab 22:00; echtes SQLite |
| MPAA-Mapping auf `AgeRatingMinAge` | `ProgramScheduleServiceTests_AgeWindow` | `R`→17 ≙ 22:00-Fenster, `PG-13`→13 ≙ 20:00-Fenster, `NC-17`→18 ≙ 23:00-Fenster |
| Unbekannte Freigabe, beide Modi | `ProgramScheduleServiceTests_AgeWindow` | `RestrictToNightWindow`: unklassifizierter Film nur 23:00–06:00; `Unrestricted`: jederzeit |
| Cursor/Rotation | `ProgramScheduleServiceTests_AgeWindow` | Übersprungener Film wird beim nächsten (nächtlichen) Occurrence erneut angeboten und dort geplant; Rotation klemmt nicht (Lückenlosigkeit mit passendem Restmaterial/Füller) |
| Episoden-Defer | `ProgramScheduleServiceTests_AgeWindow` | Geordneter Serienstrang mit blockiertem Kopf verändert die Episodenreihenfolge nicht; Kopf wird am nächsten Occurrence erneut versucht |
| Interleaved — Skip im „other"-Strang | `ProgramScheduleServiceTests_AgeWindow` | Genre-Regel (Show-Stränge + Movies im `other`-Strang) zu Tageszeit: Kopf-Film des `other`-Strangs FSK 18 ⇒ `skip[s]`-Pfad — der Film wird nicht am Tag platziert, das Round-Robin-Fenster füllt sich mit den Show-Strängen bzw. zulässigem `other`-Material (kein Klemmen, kein Out-of-Window-Slot); der übersprungene Film erscheint frühestens im Nachtfenster bzw. beim nächsten Ringumlauf |
| Interleaved — Defer im geordneten Strang | `ProgramScheduleServiceTests_AgeWindow` | Genre-Regel mit zwei Shows, Kopf-Episode von Show A FSK 18, tägliche Occurrence zu Tageszeit ⇒ `done[s]`-Pfad: Strang A ruht (kein A-Slot, Strang-Cursor nicht fortgeschrieben, Episodenreihenfolge unverändert), Strang B/`other` füllen das Fenster; nach Fenster-Öffnung (zweite Regel im Nachtfenster oder verschobene Occurrence) wird die Kopf-Episode genommen |
| Interleaved — Reproduzierbarkeit | `ProgramScheduleServiceTests_AgeWindow` | Interleaved-Airing mit Fenster-Skip im `other`-Strang bleibt bei unveränderten Einstellungen reproduzierbar (`ReconcileAsync` ⇒ `AssertNoDisplacedAsync` — deckt `InterleavedAiringTailReproducible` inkl. Lückentoleranz ab); nach Verschärfung der Fenster-Einstellung wird das verstoßende Interleaved-Airing `Displaced` und regelkonform rematerialisiert |
| Rewind — Wiederangebot nach Displacement | `ProgramScheduleServiceTests_AgeWindow` | Ungeordneter Ring mit mit-konsumierter Skip-Position hinter dem displaced Index: nach `ReconcileAsync` (Displacement z. B. über deaktivierte/verschobene Regel, Muster `Reconcile_DisplacedSeries_ReschedulesAtNextOccurrenceWithoutSkip`) werden die displaced Titel in Ring-Reihenfolge am nächsten Occurrence wieder ausgestrahlt und die übersprungene Position erneut geprüft — sie erscheint nur im zulässigen Fenster, nie außerhalb |
| Tages-/Wochenübergang | `ProgramScheduleServiceTests_AgeWindow` | Begonnener Beitrag läuft über die Fenstergrenze hinaus zu Ende; Titel um 05:30 erlaubt, um 06:30 nicht; **explizit** auch eine `Weekly`-Regel über der Grenze (Occurrence am Wochentag der Regel über 05:30→06:30 hinaus bzw. FSK-16-Beitrag am Wochenende-Tag über 22:00) |
| Füller/Clip-Fenster | `ProgramScheduleServiceTests_AgeWindow` | `FillGaps` überspringt unzulässige Füller-Items; Clip-Fill respektiert den Unbekannt-Modus |
| Reconcile | `ProgramScheduleServiceTests_AgeWindow` | Geänderte Fenster-Einstellung ⇒ verstoßende Slots `Displaced` + Rematerialisierung; unverändert ⇒ `AssertNoDisplacedAsync` bleibt grün |
| Diagnose | `ProgramScheduleServiceTests_Diagnosis` (Erweiterung) | Regel zu Tageszeit mit ausschließlich FSK-18-Material ⇒ `AgeWindowBlockedCount > 0`; `Diagnosis_NonAdmin_Throws` unverändert |
| Settings | `ProgramSettingsServiceTests_AgeWindow` | Get/Update-Roundtrip, Default-Normalisierung korrumpierter Werte, Validierungsfehler (Reihenfolge, `NightEnd`, `Enum.IsDefined`) |
| Backup-Regression | `VideoWebPlayerBackupDataTests` (Erweiterung) | Alt-Backup ohne die fünf neuen `Setups`-Spalten restauriert fehlerfrei (`LegacyBackupArchiveBuilder.RemoveColumnsAsync`) |
| DiagnosisLabel | `TVSenderAdminTests*` (Komponententest) | Neuer Grund „nicht altersgerecht im Sendefenster" erscheint bei `AgeWindowBlockedCount > 0` |

### Betroffene bestehende Tests

| Test / Testklasse | Grund der Anpassung |
|-------------------|---------------------|
| Sämtliche `ProgramScheduleServiceTests_*` (indirekt) | Seeds ohne `AgeRatingMinAge` ⇒ Verhalten hängt vom Unbekannt-Modus ab; `ProgramScheduleTestDb` muss die bisherige Planbarkeit bewahren (siehe Seiteneffekte) — Anpassung im Helper, nicht in den Tests selbst |
| `TVSenderAdminTests*` (Diagnose-bezogene Fälle) | `ProgramRuleDiagnosis` erhält neuen positionalen Parameter — Konstruktionsstellen in Testdoubles anpassen |

### E2E-Tests (primärer Funktionsnachweis)

| Priorität | Szenario | Testdatei / Testklasse | Abgedecktes Akzeptanzkriterium | Warum E2E nötig ist |
|-----------|----------|------------------------|-------------------------------|-------------------|
| Pflicht | Admin öffnet Reiter „Einstellungen", setzt Fensterzeiten (z. B. FSK 16 ab 21:00) und speichert; nach Reload stehen die Werte | `TVSenderAdminE2ETests` (`Admin_AgeWindows_SetAndPersist`) | Konfigurierbare Fenstertabelle | Benutzerfluss Einstellungs-UI — Muster `Admin_ContentScope_SelectAndPersist` |
| Pflicht | Admin wählt Unbekannt-Modus im Select und speichert; Wert persistiert | `TVSenderAdminE2ETests` (`Admin_UnknownRatingMode_SelectAndPersist`) | Dokumentiertes, konfigurierbares Standardverhalten bei unbekannter Freigabe | Benutzerfluss Einstellungs-UI |
| Pflicht | Regel-Diagnose zeigt „nicht altersgerecht im Sendefenster" für eine Tages-Regel mit ausschließlich hoch eingestuftem Material | `TVSenderAdminE2ETests` (`Admin_RuleDiagnosis_ShowsAgeWindowReason`) | Diagnose-Grund ausweisbar | Sichtbarer Diagnose-Fluss über die Admin-Seite |
| Pflicht | Validierungsfehler (z. B. nicht monotone Fensterzeiten) wird beim Speichern in der UI angezeigt | `TVSenderAdminE2ETests` (`Admin_AgeWindows_InvalidInput_ShowsError`) | Validierte Eingaben | Fehlerfall ist über die UI auslösbar — Muster bestehender Einstellungs-Fehlermeldungen |
| Pflicht | Durchspielen: FSK-18-Film wird nach Regel+Seed nicht am Tag, sondern im Nachtfenster im Sendeplan gelistet (Schedule-Ansicht der Admin-Seite) | `TVSenderAdminE2ETests` (`Admin_AgeWindowedContent_ScheduledAtNight`) | End-to-End-Nachweis „Titel wird in zulässiges Fenster verschoben" (Akzeptanzkriterium 1 — einziger UI-End-to-End-Nachweis) | Nachweis des Gesamtflusses Einstellung → Planung → Anzeige |

### Betroffene bestehende E2E-Tests

Keine — `TVSenderAdminE2ETests`-Szenarien zu ContentScope, Regeln und Clip-Gruppen bleiben unverändert (neue UI-Elemente sind additiv; Standardwerte verändern bestehende Flüsse nicht, sofern der Seed-Modus die bisherige Planbarkeit bewahrt).

## Offene Punkte

Keine.
