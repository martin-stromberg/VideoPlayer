# Umsetzungsplan: Platzhalter-Clip-Gruppen mit karenzzeitgesteuerter Lückenfüllung im Sendeplatz (Issue #277, Nachbesserung zu Issue #111)

## Übersicht

Platzhalter-Clips („Maskottchen") werden in benannte Gruppen eingeteilt und an `ProgramRule`s zugeordnet. Eine neue je-Regel-Karenzzeit `ProgramRule.PlaceholderGrace` (Default 15 min) stoppt die Titel-Auswahl, sobald die Restzeit bis zum `SlotDuration`-Periodenende darunter fällt; die Restzeit wird stattdessen mit vor/nach den Titeln verteilten Clip-Slots aus den zugeordneten Gruppen aufgefüllt. Die reservierte System-Gruppe „Trailer" liefert automatisch Trailer der im `TrailerLeadDays`-Vorlauf anstehenden Filme. Betroffen sind Datenmodell (drei neue Entities + `ProgramRule`-Spalte), `ProgramScheduleService` (Grace-Schwelle, neuer Clip-Füll-Pass, clip-bewusste Reproduzierbarkeit), ein neuer `IProgramClipGroupService`, `ProgramRuleService`/`ProgramRuleInput`, `TVSenderAdmin.razor` (neuer Gruppen-Tab + Regel-Dialog-Erweiterung) und `VideoWebPlayerBackupData`. Die bisherige `ProgramRuleMaterialType.MediaSourceClips`-Füller-Nutzung in `FillGaps` bleibt unverändert.

## Designentscheidungen

| Komponente / Bereich | Gewählter Ansatz | Begründung |
|----------------------|-----------------|------------|
| Reservierte Gruppe „Trailer" | Migrierter Seed-Datensatz `ProgramClipGroup { Id = 1, Name = "Trailer", IsSystem = true }` per `migrationBuilder.InsertData`; Konstante `ProgramClipGroup.TrailerGroupId = 1`; in `EnsureCreatedAsync`-Test-/E2E-Fixtures (`ProgramScheduleTestDb.cs:36`, `PlaylistsE2ETestBase.cs:972` — `InsertData` läuft nur unter `Migrate()`, `WebApplicationExtensions.cs:29`) wird die Zeile über die Seed-Hilfe `SeedTrailerClipGroupAsync` gelegt | Einheitlicher `ProgramRuleClipGroup`-Join ohne Sonderwerte/Null-FKs; der Unique-Index auf `Name` (NOCASE) besetzt den reservierten Namen zusätzlich auf DB-Ebene; keine Enum-/Sentinel-Verbiegung nötig |
| Clip-Füllung | Eigenständiger Pass `InsertClipFill` **nach** der Materialisierung aller Occurrences: arbeitet auf den noch nicht persistierten `newSlots`, verteilt Clip-Slots in die `n+1`-Segmente (vor dem ersten Titel, zwischen den Titeln, nach dem letzten) und verschiebt Folgeslots | Die Trailer-Gruppe braucht `airingsByMovie` inkl. der frisch geplanten Film-Ausstrahlungen dieses Laufs — erst nach `MaterializeOccurrence` vollständig (gleiches Problem wie `ExpandTrailerStatesAsync`); `newSlots` ist eine Liste und lässt sich problemlos spleißen; `FillGaps` läuft danach auf der endgültigen Geometrie |
| Karenz-Schwelle | Zusätzliche Abbruchbedingung in `SelectAiringItems`/`SelectAiringItemsInterleaved`: `target - accumulated < clipGrace` → kein Titel mehr; `clipGrace` wird von `SelectItems` nur übergeben, wenn die Regel ≥ 1 Gruppe hat und `SlotDuration` gesetzt ist; die Fenster-Bound-Prüfung `bound.StartUtc - slotStart < _minStartRemain` bleibt unverändert bestehen | Trennt „Perioden-Rest" (je-Regel-Karenzzeit) von „Fenster-Rest" (globale Startschwelle) — beide Bedeutungen bleiben erhalten; die Karenzzeit ersetzt `_minStartRemain` nicht global |
| Clip-Slot-Markierung | Clip-Slots tragen `ProgramRuleId` und `RuleType` der erzeugenden Regel (kein neuer `ProgramRuleType`-Wert, kein `Filler`-Snapshot) | Bleiben Teil der Ausstrahlung: `GroupAirings` fasst sie mit den Titeln zu einer Gruppe zusammen, Verdrängen/Atomarität greifen automatisch; `RuleType = Filler` würde die Ausstrahlung beim `GroupAirings`-Rebuild in zwei Fragmente spalten und beide wären nicht reproduzierbar (verifiziert: das Tail-Check akzeptiert nur echte Suffixe/Prefixe über Cursor-Ring) |
| Reproduzierbarkeit | Clip-bewusster Vorfilter: Slots werden zuerst anhand der Clip-Kandidatenmenge der Regel klassifiziert (`MediaItemId` ∈ Clip-Expansion → Clip-Slot); die bestehenden Tail-Checks (`FlatAiringTailReproducible`/`InterleavedAiringTailReproducible`) laufen auf der titel-gefilterten Teilfolge; Clip-Slots werden separat auf Mitgliedschaft in der aktuellen Clip-Expansion und lückenlose Verkettung geprüft (Position/Anzahl wird **nicht** re-simuliert) | Clip-Füllung ist nicht sequenzkritisch (keine Folgeordnung wie Episoden); ein entfernter/veränderter Clip macht die Ausstrahlung über die Mitgliedschafts-Prüfung invalid → Verdrängen + Neuplanung — gleiches Verhalten wie bei geändertem Material. Re-Simulation der exakten Position wäre fragil ohne Mehrwert. Klassifikation Clip-zuerst verhindert Fehlzuordnung eines Trailer-Clips über `slot.MovieId` an den Titel-Ring |
| Clip-Fortschritt | Persistierter Ring je (Regel, Gruppe) über `ProgramRuleCursor` mit `StrandKey = $"clipgroup:{groupId}"`, `LastContentType` = `MediaItem` bzw. `Trailer`; sequenzielle Entnahme, keine Optimierungs-Suche | Rotation über Ausstrahlungen/Läufe wie bei den übrigen Ringen; `SelectStrandCursor`, `PersistCursors`, `RewindCursor` sind direkt wiederverwendbar |
| Gruppen-Zustand im Scheduler | `RuleState` erhält eine eigene `ClipGroups`-Liste (je Gruppe: Entity + `StrandState`-Ring + Trailer-Flag), getrennt von `Strands` | Clip-Ringe dürfen nicht in `Strands` landen — sonst kippt `IsInterleaved`, `CommitTaken` und die Tail-Checks |
| Clip-Kandidatenmenge | Wie `ExpandMediaSourceClipsAsync`: unklassifizierte Video-`MediaItem`s (kein `MovieMediaItem`-/`TVShowEpisodeMediaItem`-Link, `IsVideoFileName`), `Duration` Pflicht, `access.Allows(sourceId)` — allerdings quellenübergreifend (Gruppen enthalten Clips beliebiger Quellen); serverseitig sowohl bei der Zuordnung (`AddClipsToGroupAsync`) als auch bei der Expansion geprüft | Dieselbe fachliche „Clip"-Definition wie beim bewährten `MediaSourceClips`-Material; klassifizierte Inhalte können nicht in Gruppen sickern; doppelte Prüfung fängt nachträglich klassifizierte Items ab |
| Karenzzeit-Datentyp | `TimeSpan PlaceholderGrace` NOT NULL mit Default `ProgramRule.DefaultPlaceholderGrace = 15 min` (DB-Default `00:15:00`, TEXT wie `SlotDuration`) | Je-Regel-Einstellung laut Anforderung, kein Nullable-Sonderfall im Scheduler; TimeSpan-Backup-Roundtrip verifiziert (`JsonSerializerDefaults.Web` ↔ `"hh:mm:ss"`) |
| `TimeSpan`-Restore-Default | Neue Liste `OptionalRestoreTimeSpanDefaults` in `VideoWebPlayerBackupData`, dritter paralleler Block in `AddOptionalRestoreDefaults` | Der Default-Mechanismus kennt nur `bool`/`long`/`int`; `ToDbValue` deserialisiert `"00:15:00"` korrekt zu `TimeSpan` (verifiziert) |
| Clip-Mehrfachmitgliedschaft | Ein Clip darf mehreren Gruppen angehören (keine Exklusiv-Constraint) | „Einteilen" verlangt keine Partition; Exklusivität würde die Verwaltung verkomplizieren |
| Clip-Verteilung | Deterministisch-sequenziell, **keine** Rucksack-Optimierung: gleichmäßige Verteilung der Restzeit auf die `n+1`-Segmente (Kopfsegment vor dem ersten Titel eingeschlossen); Entnahme aus den Gruppen-Ringen per Round-Robin; letzter Clip darf `periodEnd` überschreiten, nicht platzierbarer Rest → `RemainingGapSeconds` | Reproduzierbar, einfach verifizierbar und deckt „Restzeit voll ausschöpfen / möglichst genau am Periodenende" ab; die Restzeit ist ohnehin klein (< Karenzzeit), Optimierungsgewinn wäre marginal bei deutlich fragilerer Reproduzierbarkeit |
| Clip-Platzierung | Clip-Slots werden nur in freien Raum gelegt; „frei" heißt: kein Überlapp mit belegten Intervallen (andere Ausstrahlungen, `blocked`) **und** kein Überlapp mit reservierten `Bound`-Fenstern höherpriorer Regeln (`BoundStarts`/`Hard`-Bounds — Occurrence-Grenzen, auch wenn dort noch keine Slots materialisiert sind); sie verdrängen/verschieben keine belegten Nachbar-Ausstrahlungen; ein über `periodEnd` hinausschießender Clip darf kein Bound-Fenster einer höherprioren Regel verletzen und wird bei belegtem oder gebundenem Folgeraum verworfen (Rest = `RemainingGapSeconds`) | Clips sind Füllmaterial ohne Prioritätsanspruch; nachträgliches Verschieben belegter Ausstrahlungen würde die bewährte Atomaritäts-/Verdrängungslogik aufbrechen; ein Füllclip darf kein höherpriores Fenster blockieren — konsistent zur Titel-Auswahl (`b.Hard`-Abbruch in `SelectAiringItems`) |
| Geltungsbereich | `ClipGroupIds` nur bei `RuleType != Filler` und `SlotDuration != null`, serverseitig abgelehnt; `PlaceholderGrace` muss `> 0` und `< SlotDuration` sein; Regeln mit beiden Feldern (`SlotDuration` + `EpisodeCount`) füllen den Rest nach den `EpisodeCount`-Titeln | „Periodenende" ist nur bei `SlotDuration` zeitlich definiert; `grace >= SlotDuration` würde nie einen Titel starten lassen; `EpisodeCount`-Regeln ohne `SlotDuration` haben kein zeitliches Restziel |

## Programmabläufe

### Gruppenverwaltung (Anlegen, Umbenennen, Löschen)

1. `TVSenderAdmin.razor` Tab „Clip-Gruppen" ruft `IProgramClipGroupService.GetGroupsAsync(userId)` auf → Liste aller Gruppen inkl. der System-Gruppe „Trailer" (`IsSystem`, als solche markiert, ohne Bearbeiten/Löschen-Schaltflächen).
2. `CreateGroupAsync(userId, name)` / `RenameGroupAsync(userId, groupId, name)` / `DeleteGroupAsync(userId, groupId)`: `RequireAdminAsync` → Validierung (Name nicht leer, ≤ `NameMaxLength`, keine Dublette ordinal-ignore-case, Name ≠ „Trailer" für freie Gruppen, `IsSystem`-Gruppen nicht änder-/löschbar) → Speichern → `IProgramScheduleSignal.NotifyScheduleChanged()`.

Beteiligte Klassen/Komponenten: `IProgramClipGroupService`, `ProgramClipGroupService`, `ProgramClipGroup`, `ApplicationDbContext`, `IProgramScheduleSignal`, `TVSenderAdmin.razor`.

### Clip-Zuordnung zu einer Gruppe

1. Der Dialog zeigt die Kandidatenliste aus `GetClipCandidatesAsync(userId, search, mediaSourceId, page)`: unklassifizierte Video-`MediaItem`s aller Quellen (kein `MovieMediaItem`-/`TVShowEpisodeMediaItem`-Link, `IsVideoFileName`), mit Name, Quelle, `Duration` und Probe-Status; Clips ohne `Duration` werden aufgelistet und gekennzeichnet („keine Laufzeit"). Auswahl erfolgt über Suchfeld + Quell-Filter + Checkbox-/Schaltflächen-Liste — keine Id-Eingabe.
2. `AddClipsToGroupAsync(userId, groupId, mediaItemIds)`: `RequireAdminAsync` → `IsSystem`-Gruppe abweisen → jedes `MediaItem` muss existieren, unklassifiziert und Video sein (`ArgumentException` sonst) → `ProgramClipGroupItem`-Zeilen (Composite-PK verhindert Dubletten) → `NotifyScheduleChanged`.
3. `GetGroupClipsAsync(userId, groupId)` liefert die Mitglieder mit `Duration`/Probe-Status zur Kennzeichnung; `RemoveClipFromGroupAsync` entfernt einzelne Mitglieder; bei der Trailer-Gruppe sind beide Schreibpfade gesperrt (Anzeige nur informativ).

Beteiligte Klassen/Komponenten: `IProgramClipGroupService`, `ProgramClipGroupItem`, `MediaItem`, `MovieMediaItem`, `TVShowEpisodeMediaItem`, `VideoFileExtensions`, `TVSenderAdmin.razor`.

### Regel mit Clip-Gruppen und Karenzzeit speichern

1. Regel-Dialog: `rule-clip-groups`-Checkbox-Liste (Muster `rule-material-genre`) listet alle Gruppen inkl. „Trailer" (mit Hinweis „automatisch befüllt"); `rule-grace-minutes`-Zahlfeld (Muster `rule-duration`, Minuten, Default 15).
2. `SaveRuleAsync` baut `ProgramRuleInput` um `ClipGroupIds` + `PlaceholderGrace` erweitert → `ProgramRuleService.CreateRuleAsync`/`UpdateRuleAsync` → `RequireAdminAsync` → `ValidateRuleInputAsync` ruft `ValidateClipGroupInputAsync` (`RuleType != Filler`, `SlotDuration != null`, alle Gruppen-Ids existieren — System-Gruppe eingeschlossen, `PlaceholderGrace > 0 && < SlotDuration`) → `PlaceholderGrace = input.PlaceholderGrace ?? DefaultPlaceholderGrace` → `ReplaceClipGroupLinksAsync` (ersetzt `ProgramRuleClipGroups` wie `ReplaceGenreLinksAsync`, aber unabhängig von der Materialart) → `NotifyScheduleChanged`.
3. `GetRulesAsync` lädt `Include(r => r.ClipGroups).ThenInclude(c => c.ClipGroup)`; `MaterialLabel` zeigt die Gruppennamen als Suffix („— Clips: Tagesbeginn, Trailer").

Beteiligte Klassen/Komponenten: `IProgramRuleService`, `ProgramRuleService`, `ProgramRuleInput`, `ProgramRuleClipGroup`, `TVSenderAdmin.razor` (`RuleEditModel`, `MaterialLabel`).

### Plangenerierung mit Clip-Füllung (`EnsureCoverageAsync`)

1. Regeln laden inkl. `Include(r => r.ClipGroups).ThenInclude(c => c.ClipGroup)`; je Regel mit `ClipGroups.Count > 0` werden `ClipGroupState`s gebaut: Benutzergruppen sofort über `ExpandClipGroupItemsAsync` (Mitglieder-`MediaItem`s: Video, unklassifiziert, `Duration != null`, `access.Allows(sourceId)` — Filter analog `ExpandMediaSourceClipsAsync`/`CountNoDuration`), die Trailer-Gruppe zunächst leer (verzögerte Expansion, s. u.); Ring-Startindex je Gruppe über `SelectStrandCursor` mit `StrandKey = $"clipgroup:{groupId}"`.
2. `MaterializeOccurrence` läuft wie bisher; `SelectItems` übergibt `clipGrace = rule.PlaceholderGrace` (nur bei Gruppen + `SlotDuration`): `SelectAiringItems`/`SelectAiringItemsInterleaved` brechen die Titel-Auswahl zusätzlich ab, sobald `target - accumulated < clipGrace` — kein weiterer Titel mit Overshoot.
3. Nach der Occurrence-Schleife: `LoadMovieAiringsAsync(channel.Id, coverageStart, newSlots)` (persistierte + frische Ausstrahlungen) → `ExpandTrailerStatesAsync` (bestehendes Trailer-Material) **und** Nachexpansion der Trailer-`ClipGroupState`s über `ExpandTrailersAsync`.
4. `InsertClipFill(newSlots, blocked, clipStates, airingsByMovie, boundStarts, horizonEnd, summary)`: gruppiert `newSlots` zu Ausstrahlungen (gleiche `ProgramRuleId`, lückenlos); je Ausstrahlung einer Regel mit Gruppen: `periodEnd = erstesSlot.Start + SlotDuration`, `rest = periodEnd - letztesSlot.End`; ist `rest > 0`, wird die Restzeit gleichmäßig auf die `n+1`-Segmente verteilt (Kopfsegment vor dem ersten Titel eingeschlossen) und jedes Segment sequenziell mit Clips aufgefüllt; Clips aus den Gruppen-Ringen per Round-Robin (Gruppen-Reihenfolge = `ProgramClipGroupId` aufsteigend, Entnahme sequenziell — keine Optimierungs-Suche); jeder Clip-`ProgramSlot` trägt `ProgramRuleId`/`RuleType` der Regel, `MovieId`/`TVShowEpisodeId` = null (Trailer-Clips: `MovieId` = Film wie beim Trailer-Material); Folgeslots der Ausstrahlung werden um die eingefügte Dauer verschoben; die Trailer-Gruppe liefert je Position nur Items mit Vorlauf-Treffer (`FindLeadMatchOffset`-Semantik, Filmstart in `(position, position + _trailerLead]`); der letzte Clip darf `periodEnd` überschreiten, sofern der Raum frei ist — „frei" heißt: kein Überlapp mit belegten Intervallen (andere Ausstrahlungen, `blocked`) **und** kein Überlapp mit reservierten `Bound`-Fenstern höherpriorer Regeln (`BoundStarts`/`Hard`-Bounds — Occurrence-Grenzen, auch wenn dort noch keine Slots materialisiert sind); belegte Nachbar-Ausstrahlungen werden nicht nachträglich verschoben und ein überschießender Clip, der in belegten oder gebundenen Folgeraum ragte, wird verworfen; nicht platzierbarer Rest → `summary.RemainingGapSeconds`; `blocked` wird auf die endgültige Geometrie aktualisiert.
5. `FillGaps` läuft unverändert über die dann verbleibenden Lücken **zwischen** Ausstrahlungen; `PersistCursors` schreibt zusätzlich Clip-Strang-Zeilen (`clipgroup:{groupId}`); `SaveChangesAsync` persistiert alles gemeinsam.

Beteiligte Klassen/Komponenten: `ProgramScheduleService` (`SelectItems`, `SelectAiringItems`, `SelectAiringItemsInterleaved`, `MaterializeOccurrence`, `InsertClipFill`, `ExpandClipGroupItemsAsync`, `ExpandTrailersAsync`, `LoadMovieAiringsAsync`, `FillGaps`, `PersistCursors`, `SummaryCounters`), `ProgramClipGroup`, `ProgramClipGroupItem`, `ProgramSlot`, `ProgramRuleCursor`.

### Neu-Bewertung (`ReconcileAsync`) mit Clip-Slots

1. `airingsByMovie` aus persistierten Slots (bestehend); Regel-Zustände inkl. Clip-Gruppen expandiert (Trailer-Gruppe hier sofort, da persistierte Ausstrahlungen bekannt sind — analog der bestehenden Trailer-Material-Behandlung in `ReconcileAsync`).
2. `ReconcileWindow.Validate` ruft die clip-bewusste `AiringReproducible`-Variante: Klassifikation Clip-zuerst (`MediaItemId` ∈ Clip-Expansion der Regel), titel-gefilterte Teilfolge an die bestehenden Tail-Checks, Clip-Slots auf Mitgliedschaft + Verkettung geprüft; invalide Ausstrahlungen werden wie bisher verdrängt (Clip-Slots verdrängen automatisch mit — gleiche `ProgramRuleId`, Teil der Gruppe).
3. `MaterializeOccurrenceEvicting` materialisiert nur Titel (Grace-Stop über `SelectItems`); Clip-Slots verdrängen nie (`Evicts` unverändert — Clips kommen erst im Füll-Pass und überspringen belegten Raum).
4. `BuildMovieAiringsFromSlotsAsync(keptSlots ∪ newSlots)` (bestehend, für `FillGaps`) → `InsertClipFill` nur über die neu materialisierten Ausstrahlungen in `newSlots`; `FillGaps`; `PersistCursors` inkl. Clip-Stränge; `RewindCursor` berücksichtigt verdrängte Clip-Slots (Clip-Strang zurück auf Vorgänger).

Beteiligte Klassen/Komponenten: `ProgramScheduleService` (`ReconcileWindow`, `AiringReproducible`, `FlatAiringTailReproducible`, `InterleavedAiringTailReproducible`, `MaterializeOccurrenceEvicting`, `InsertClipFill`, `RewindCursor`, `PersistCursors`).

### Alt-Backup-Restore

1. `ReadFromAsync` eines Archivs ohne `ProgramClipGroups`/`ProgramClipGroupItems`/`ProgramRuleClipGroups`/`ProgramRules.PlaceholderGrace`: `OptionalRestoreTables` überspringt fehlende Tabellen; `OptionalRestoreColumns` + `OptionalRestoreTimeSpanDefaults` injiziert `PlaceholderGrace = "00:15:00"` je wiederhergestellter `ProgramRules`-Zeile (`ToDbValue` → `TimeSpan`).

Beteiligte Klassen/Komponenten: `VideoWebPlayerBackupData`.

## Neue Klassen

| Klasse | Typ | Zweck |
|--------|-----|-------|
| `ProgramClipGroup` | Datenmodellklasse (`VideoWebPlayer/Data`) | Benannte Clip-Gruppe: `Id`, `Name` (required, max 255, NOCASE-Unique), `IsSystem` (bool, reservierte System-Gruppen — „Trailer"), `CreatedAt`, `UpdatedAt`; Konstanten `TrailerGroupId = 1`, `TrailerGroupName = "Trailer"` |
| `ProgramClipGroupItem` | Datenmodellklasse | Join Clip↔Gruppe: `ProgramClipGroupId` + `MediaItemId` (Composite-PK), beide mit Navigationen |
| `ProgramRuleClipGroup` | Datenmodellklasse | Join Gruppe↔Regel: `ProgramRuleId` + `ProgramClipGroupId` (Composite-PK), beide mit Navigationen (Muster `ProgramRuleGenre`) |
| `ProgramClipGroupConfiguration` | EF-Konfiguration (`Data/Configurations`) | PK, `Name` required/NOCASE + Unique-Index, `IsSystem`-Default |
| `ProgramClipGroupItemConfiguration` | EF-Konfiguration | Composite-PK, beide FKs kaskadieren |
| `ProgramRuleClipGroupConfiguration` | EF-Konfiguration | Composite-PK, beide FKs kaskadieren |
| `IProgramClipGroupService` | Interface (`VideoWebPlayer/Services`) | Gruppen-CRUD, Kandidatenliste, Mitgliederverwaltung (Signaturen s. `ProgramClipGroupService`) |
| `ProgramClipGroupService` | Service | Implementierung mit `RequireAdminAsync`/`ValidateGroupName`-Helfern und `NotifyScheduleChanged` |
| `ClipGroupState` | interner Hilfstyp (`ProgramScheduleService`) | Je zugeordnete Gruppe: `Group`, `StrandState`-Ring, `IsTrailer`-Flag, Cursor-Zuordnung |

## Änderungen an bestehenden Klassen

### `ProgramRule` (Datenmodell)
- **Neue Eigenschaften:** `PlaceholderGrace` (`TimeSpan`, NOT NULL, Default `DefaultPlaceholderGrace` = 15 min) — je-Regel-Karenzzeit für die Clip-Füllung; `ClipGroups` (`ICollection<ProgramRuleClipGroup>`) — Navigations-Collection der Gruppenzuordnung.
- **Neue Konstanten:** `DefaultPlaceholderGrace` (`TimeSpan.FromMinutes(15)`).

### `ApplicationDbContext` (Datenmodell)
- **Neue DbSets:** `ProgramClipGroups`, `ProgramClipGroupItems`, `ProgramRuleClipGroups`; `OnModelCreating`/`ApplyConfigurationsFromAssembly` greift die neuen `IEntityTypeConfiguration`s automatisch.

### `ProgramRuleInput` (Record)
- **Neue Parameter:** `IReadOnlyList<long>? ClipGroupIds = null`, `TimeSpan? PlaceholderGrace = null` — optionale Endparameter (quellkompatibel zu bestehenden Aufrufen).

### `ProgramRuleService` (Service)
- **Neue Methoden:** `ValidateClipGroupInputAsync(input, ct)` — `RuleType != Filler`, `SlotDuration != null`, Existenz aller `ClipGroupIds`, `PlaceholderGrace`-Bereich; `ReplaceClipGroupLinksAsync(ruleId, input, ct)` — ersetzt `ProgramRuleClipGroups` (Muster `ReplaceGenreLinksAsync`, aber materialart-unabhängig).
- **Geänderte Methoden:** `GetRulesAsync` — `Include(r => r.ClipGroups).ThenInclude(c => c.ClipGroup)`; `CreateRuleAsync`/`UpdateRuleAsync` — `PlaceholderGrace` setzen (`input.PlaceholderGrace ?? ProgramRule.DefaultPlaceholderGrace`), `ReplaceClipGroupLinksAsync` aufrufen.
- **Neuer Reservierungs-Check:** „Trailer" als Gruppenname wird im `ProgramClipGroupService` serverseitig abgewiesen (zusätzlich zur Unique-Index-Absicherung).

### `ProgramClipGroupService` (Service, neu — Vertrag)
- `GetGroupsAsync(userId, ct)` → `Task<List<ProgramClipGroup>>`; `CreateGroupAsync(userId, name, ct)`; `RenameGroupAsync(userId, groupId, name, ct)`; `DeleteGroupAsync(userId, groupId, ct)`; `GetGroupClipsAsync(userId, groupId, ct)` → Mitglieder inkl. `Duration`/Probe-Status/Quellname; `GetClipCandidatesAsync(userId, search, mediaSourceId, skip, take, ct)` → unklassifizierte Video-Items aller Quellen; `AddClipsToGroupAsync(userId, groupId, mediaItemIds, ct)`; `RemoveClipFromGroupAsync(userId, groupId, mediaItemId, ct)` — alle Mutationen mit `RequireAdminAsync` + `NotifyScheduleChanged`.

### `ProgramScheduleService` (Service)
- **Neue Methoden:**
  - `ExpandClipGroupItemsAsync(groupId, access, summary, ct)` — Mitglieder-Ring einer Benutzergruppe (`MediaItem`s mit `Duration`, Video, unklassifiziert, freigegeben; `CountNoDuration`-Buchhaltung).
  - `ExpandClipGroupStatesAsync(states, cursors, access, airingsByMovie, coverageStart, summary, ct)` — verzögerte Nachexpansion der Trailer-`ClipGroupState`s über `ExpandTrailersAsync` (Muster `ExpandTrailerStatesAsync`).
  - `InsertClipFill(newSlots, blocked, states, clipStatesByRule, airingsByMovie, boundStarts, horizonEnd, summary)` — Verteilpass: `n+1`-Segmente je Ausstrahlung, Round-Robin über die Gruppen-Ringe, Trailer-Vorlauf je Position (`FindLeadMatchOffset`-Semantik), Freiraum-/Kollisionswächter (belegte Intervalle **und** reservierte `Bound`-Fenster höherpriorer Regeln gelten als nicht frei), Overshoot-Regel, `blocked`-Nachpflege, `RemainingGapSeconds`-Buchung.
  - `BuildClipGroupStates(rule, cursors, ct)` — Ring-Initialisierung je Gruppe inkl. `SelectStrandCursor` (`clipgroup:{id}`).
- **Geänderte Methoden:**
  - `EnsureCoverageAsync` — `Include(r => r.ClipGroups).ThenInclude(...)`, Clip-Gruppen-Expansion, `InsertClipFill` vor `FillGaps`, `PersistCursors` mit Clip-Strängen.
  - `ReconcileAsync` — gleiche Includes/Expansion (Trailer-Gruppe sofort), clip-bewusste `ReconcileWindow`-Validierung, `InsertClipFill` über `newSlots` nach dem Materialisierungs-Loop, Clip-`RewindCursor`.
  - `SelectItems` — übergibt `clipGrace` (= `rule.PlaceholderGrace` bei Gruppen + `SlotDuration`, sonst `null`).
  - `SelectAiringItems`/`SelectAiringItemsInterleaved` — neuer Parameter `TimeSpan? clipGrace`, Abbruch `target - accumulated < clipGrace`.
  - `AiringReproducible`/`FlatAiringTailReproducible`/`InterleavedAiringTailReproducible`/`ReconcileWindow` — Clip-Slot-Klassifikation und -Verifikation (s. Designentscheidung).
  - `PersistCursors` — Clip-Strang-Zeilen; `RewindCursor` — Clip-Stränge.
- **Neuer interner Typ:** `ClipGroupState` (s. Neue Klassen).

### `TVSenderAdmin.razor` (UI)
- **Neuer Tab** `tvsender-tab-clipgruppen` („Clip-Gruppen"): Gruppen-Tabelle (Name, Clip-Anzahl, Aktionen; System-Zeile „Trailer" ohne Schaltflächen), Anlegen-/Umbenennen-/Lösch-Dialoge (Muster bestehender `admin-dialog-overlay`-Dialoge).
- **Clip-Mitgliederverwaltung** im Tab: Mitgliederliste (Name, Quelle, `Duration` bzw. Kennzeichnung „keine Laufzeit", Entfernen-Schaltfläche) + Kandidaten-Auswahl (Suchfeld über `MediaItem.Name`, Quell-Filter-Select, Trefferliste mit Hinzufügen — Muster `rule-multi-select`/Probe-Tabellen; **keine** Id-Eingabe); Trailer-Gruppe: Mitgliederliste schreibgeschützt (Hinweis auf automatische Befüllung).
- **Regel-Dialog:** `#rule-clip-groups`-Checkbox-Liste aller Gruppen inkl. „Trailer" (Muster `rule-material-genre`), `#rule-grace-minutes`-Zahlfeld (Muster `rule-duration`); Anzeige nur bei `RuleType != Filler` (Hinweistext bei fehlender `SlotDuration`).
- **`RuleEditModel`:** `SelectedClipGroupIds` (`HashSet<long>`), `GraceMinutes` (`int?`, Default 15); `StartEdit`/`SaveRuleAsync` befüllen/übertragen die Felder; `MaterialLabel(rule)` hängt „— Clips: {Namen}" an, wenn `rule.ClipGroups.Count > 0`.

### `ServiceCollectionExtensions` (DI)
- `services.AddScoped<IProgramClipGroupService, ProgramClipGroupService>()`.

### `VideoWebPlayerBackupData` (Backup)
- `OptionalRestoreTables` += `ProgramClipGroups`, `ProgramClipGroupItems`, `ProgramRuleClipGroups`; `OptionalRestoreColumns` += `ProgramRules.PlaceholderGrace`; neue `OptionalRestoreTimeSpanDefaults`-Liste inkl. drittem Schleifen-Block in `AddOptionalRestoreDefaults` (Eintrag `(ProgramRules, PlaceholderGrace, TimeSpan.FromMinutes(15))`).

## Datenbankmigrationen

| Migrationsname | Betroffene Tabellen/Spalten | Beschreibung der Änderung |
|----------------|----------------------------|---------------------------|
| `AddProgramClipGroups` | `ProgramClipGroups` (neu), `ProgramClipGroupItems` (neu), `ProgramRuleClipGroups` (neu), `ProgramRules.PlaceholderGrace` | Drei neue Tabellen (Composite-PKs, FK-Kaskaden, NOCASE-Unique auf `ProgramClipGroups.Name`); `PlaceholderGrace` `TimeSpan` NOT NULL `DEFAULT '00:15:00'`; `migrationBuilder.InsertData` Seed-Zeile `ProgramClipGroups { Id = 1, Name = "Trailer", IsSystem = true }` |

**Wichtig zum Trailer-Gruppen-Seed:** `migrationBuilder.InsertData` läuft ausschließlich über `db.Database.Migrate()` — also nur für die Produktiv-DB (`WebApplicationExtensions.cs:29`). Test- und E2E-Fixtures bauen das Schema per `EnsureCreatedAsync` auf (`ProgramScheduleTestDb.cs:36`, `PlaylistsE2ETestBase.cs:972`, `MediaBoxContextMenuE2ETestBase.cs:133`); Migrationen samt `InsertData` laufen dort **nicht**. Die Seed-Zeile der Trailer-Gruppe wird deshalb in diesen Fixtures zusätzlich über die Seed-Hilfe `SeedTrailerClipGroupAsync` gelegt (s. Tests/E2E-Tests).

## Validierungsregeln

| Feld / Objekt | Regel | Fehlerfall |
|---------------|-------|------------|
| `ProgramClipGroup.Name` | Nicht leer, ≤ 255 Zeichen (getrimmt); keine Dublette ordinal-ignore-case; ≠ „Trailer" (case-insens.) für freie Gruppen | `ArgumentException` (lesbare Meldung; Unique-Index fängt Dubletten zusätzlich ab → übersetzte `DbUpdateException`-Meldung analog vorhandener Praxis) |
| `ProgramClipGroup` mit `IsSystem` | Kein Umbenennen, Löschen, Hinzufügen/Entfernen von Clips | `InvalidOperationException`/`ArgumentException` |
| Clip-Zuordnung (`AddClipsToGroupAsync`) | `MediaItem` existiert, ist unklassifiziert (kein `MovieMediaItem`/`TVShowEpisodeMediaItem`-Link) und Video (`IsVideoFileName`); `Duration == null` erlaubt (gekennzeichnet, bei Expansion ausgeschlossen) | `ArgumentException`/`KeyNotFoundException` |
| `ProgramRuleInput.ClipGroupIds` | Nur `RuleType != ProgramRuleType.Filler`; nur mit `SlotDuration != null`; alle Ids existieren (inkl. `TrailerGroupId`) | `ArgumentException` bzw. `KeyNotFoundException` („gewählte Clip-Gruppe wurde nicht gefunden") |
| `ProgramRuleInput.PlaceholderGrace` | `> TimeSpan.Zero` und `< SlotDuration` (sofern gesetzt); `null` → Default 15 min | `ArgumentException` |

## Konfigurationsänderungen

Keine — `ProgramSchedule:TrailerLeadDays` (Default 7) wird wiederverwendet; die Karenzzeit ist eine Je-Regel-Einstellung (`ProgramRule.PlaceholderGrace`), keine Option.

## Seiteneffekte und Risiken

- **Reproduzierbarkeit (höchstes Risiko):** Die clip-bewusste Validierung entscheidet, ob jede bestehende Ausstrahlung mit Clip-Slots bei jeder `ReconcileAsync`-Runde überlebt; ein falsch-negatives Urteil verdrängt Pläne dauerhaft neu (Oszillation). Gegenmaßnahme: `Reconcile_ClipFillSlots_StayScheduled`-Test + `Reconcile_WorkerLoop`-Nachlauf mit Clip-Regeln.
- **`FillGaps`-Geometrie:** `blocked` enthält nach der Materialisierung die **unverschobenen** Titelintervalle; `InsertClipFill` muss `blocked` auf die endgültige Geometrie bringen, sonst sucht `FillGaps` in Phantom-Freiraum bzw. zählt `GapSeconds` falsch.
- **`GroupAirings`-Fragmentierung:** Clip-Slots müssen exakt lückenlos (`Start == Vorgänger.End`) in die Ausstrahlung eingefügt werden, sonst zerfallen Ausstrahlungen in invalide Fragmente.
- **Atomaritäts-Änderung:** `MaterializeOccurrence` bleibt für Titel unverändert; Clip-Slots entstehen im Nachpass ohne eigenen atomaren Check — der Kollisionswächter ersetzt ihn und behandelt dabei nicht nur belegte Intervalle, sondern auch **reservierte `Bound`-Fenster höherpriorer Regeln** (`BoundStarts`/`Hard`-Bounds — Occurrence-Grenzen, für die evtl. noch keine Slots materialisiert sind oder deren Occurrence verworfen wurde) als nicht frei. Konsequenz: ein Füllclip darf kein Bound-Fenster einer höherprioren Regel verletzen (er würde sonst dessen Fenster blockieren); ein überschießender letzter Clip wird bei belegtem **oder gebundenem** Folgeraum verworfen statt die Nachbar-Ausstrahlung zu verschieben (festgelegte Entscheidung, s. „Clip-Platzierung").
- **`MediaItem`-Löschung:** harte FK `ProgramClipGroupItems.MediaItemId` → Kaskade entfernt Mitgliedschaften; laufende Pläne mit dem Clip bleiben (weiche `MediaItemId`-Referenz) — gleiches Verhalten wie bei bestehendem Material.
- **Backup-Schema:** neue Tabellen/spalten erhöhen `TableIndex`; Alt-Backups ohne sie müssen restaurierbar bleiben (Regressionstests); `IsSystem`-Seedzeile in Alt-DBs entsteht nur über die Migration — ein Alt-Backup-Restore nach der Migration fügt keine Trailer-Gruppe ein → die `TrailerGroupId`-Auflösung muss eine fehlende Zeile an konkreten Stellen tolerieren: `BuildClipGroupStates` löst Gruppen über den `ProgramRuleClipGroup`-Join auf (fehlende Zeile → kein Gruppen-Zustand, keine Trailer-Clips — der FK entfernt verwaiste Link-Zeilen ohnehin kaskadierend), `GetGroupsAsync` liefert die Liste schlicht ohne „Trailer"-Zeile, und `ValidateClipGroupInputAsync` weist `ClipGroupIds` mit `TrailerGroupId` bei fehlender Zeile per `KeyNotFoundException` ab (dokumentiertes Verhalten — die Regel-Zuordnung verweist ohnehin auf eine `ProgramClipGroups`-Zeile, die im Backup enthalten sein muss; Toleranz-Test `MissingTrailerSeedRow_Tolerated` geplant).
- **Guide-/Karten-Anzeige der Trailer-Clips:** Trailer-Clip-Slots tragen `MovieId` (Film des Trailers) — `ProgramGuideService.LoadDetailsAsync`/`SlotCard` zeigen dann die Film-Details statt des `MediaItem.Name`-Fallbacks (der greift nur bei `MovieId == null`, also bei Clips aus Benutzergruppen). Das ist konsistent zur Anzeige bestehender `Trailer`-Material-Slots (gleiche Slot-Semantik) und wird per `ProgramGuideServiceTests`-Fall (`GetNowAsync_TrailerClipSlot_ShowsMovieDetails`) abgesichert.
- **`MaterialLabel`/bUnit-Aufhängungen:** bestehende bUnit-Tests können Label-Texte pinnen — Prüfung im Implementierungslauf.
- **E2E-Trait-Lücke:** `TVSenderAdminE2ETests` trägt kein `Category=E2E`-Trait — neue E2E-Fälle derselben Klasse erben das; die Klassen laufen im `Category!=E2E`-Lauf mit (Bestand, kein Fix in diesem Schritt).
- **Leistung:** `InsertClipFill` arbeitet auf In-Memory-Listen (`newSlots`), Clip-Expansionen sind einmalige DB-Leser pro Lauf — kein N+1; die Reproduzierbarkeits-Clip-Klassifikation braucht die Clip-Item-Menge je Regel ohnehin im Zustand.

## Umsetzungsreihenfolge

1. **Datenmodell:** `ProgramClipGroup`, `ProgramClipGroupItem`, `ProgramRuleClipGroup`, `ProgramRule.PlaceholderGrace`/`ClipGroups`, drei EF-Konfigurationen, `ApplicationDbContext`-DbSets.
   - Voraussetzungen: Keine (EF Core 10 vorhanden; Muster `ProgramRuleGenre`/`ProgramRuleGenreConfiguration` liegt vor).
   - Beschreibung: Entities + Konfigurationen (Composite-PKs, Kaskaden, NOCASE-Unique `Name`, `IsSystem`-Default), Navigation `ProgramRule.ClipGroups`, DbSets.
2. **EF-Migration `AddProgramClipGroups`** inkl. `InsertData`-Seed der Trailer-Gruppe (Id 1, `IsSystem = true`); Snapshot-Diff prüfen.
   - Voraussetzungen: Schritt 1; `dotnet-ef` vorhanden.
   - Hinweis: Der `InsertData`-Seed gilt nur für per `Migrate()` aufgebaute Datenbanken (Produktiv-DB); `EnsureCreatedAsync`-Fixtures (`ProgramScheduleTestDb`, E2E-Basen) erhalten die Trailer-Gruppe über Seed-Hilfen (Schritte 10–11).
3. **Backup-Registrierung:** `OptionalRestoreTables`/`OptionalRestoreColumns` + `OptionalRestoreTimeSpanDefaults`-Mechanismus in `VideoWebPlayerBackupData`.
   - Voraussetzungen: Schritte 1–2.
4. **`IProgramClipGroupService` + `ProgramClipGroupService` + DI-Registrierung** (CRUD, Kandidatenliste, Mitgliederverwaltung, `RequireAdminAsync`, `NotifyScheduleChanged`, „Trailer"-Reservierung/`IsSystem`-Sperren).
   - Voraussetzungen: Schritt 1 (Entities/DbSets).
5. **`ProgramRuleInput`/`ProgramRuleService`:** neue Record-Parameter, `ValidateClipGroupInputAsync`, `ReplaceClipGroupLinksAsync`, `GetRulesAsync`-Include, `PlaceholderGrace`-Setzung.
   - Voraussetzungen: Schritte 1–2.
6. **Scheduler-Grundgerüst:** `ClipGroupState`, `BuildClipGroupStates`, `ExpandClipGroupItemsAsync`, Includes in `EnsureCoverageAsync`/`ReconcileAsync`, `clipGrace`-Parameter in `SelectAiringItems`/`SelectAiringItemsInterleaved`/`SelectItems`.
   - Voraussetzungen: Schritte 1–2.
7. **`InsertClipFill`-Pass:** Verteil-Algorithmus (`n+1`-Segmente, Round-Robin, Freiraumwächter, Overshoot, `blocked`-Nachpflege, `RemainingGapSeconds`), Trailer-`ExpandClipGroupStatesAsync` (Nachexpansion), Aufruf in `EnsureCoverageAsync` vor `FillGaps`; `PersistCursors`-Clip-Stränge.
   - Voraussetzungen: Schritt 6.
8. **Reconcile:** clip-bewusste `AiringReproducible`-Kette + `ReconcileWindow`-Kontext, `InsertClipFill` über `newSlots` nach dem Materialisierungs-Loop, `RewindCursor` für Clip-Stränge.
   - Voraussetzungen: Schritt 7.
9. **UI `TVSenderAdmin.razor`:** neuer Tab „Clip-Gruppen" (Gruppenliste + CRUD-Dialoge + Mitgliederverwaltung inkl. Kandidaten-Auswahl und Laufzeit-Kennzeichnung), Regel-Dialog (`rule-clip-groups`, `rule-grace-minutes`), `RuleEditModel`/`StartEdit`/`SaveRuleAsync`/`MaterialLabel`.
   - Voraussetzungen: Schritte 4–5.
10. **Unit-/Integrationstests** (siehe Tests).
    - Voraussetzungen: Schritte 1–8; Testinfrastruktur `ProgramScheduleTestDb`/`LegacyBackupArchiveBuilder` vorhanden.
    - Beschreibung: Zuerst die Fixture-Hilfen anlegen — insbesondere `SeedTrailerClipGroupAsync` (System-Gruppen-Zeile, da `EnsureCreatedAsync` den Migrations-`InsertData`-Seed nicht ausführt; Aufruf im Fixture-Setup `CreateAsync`), dazu `CreateClipGroupAsync`/`AddClipToGroupAsync`/`CreateRuleAsync`-Erweiterung — dann die geplanten Tests.
11. **E2E-Tests** (siehe Tests).
    - Voraussetzungen: Schritt 9.
    - Beschreibung: E2E-Seed-Hilfen in `PlaylistsE2ETestBase` (bzw. TV-Sender-spezifisch): `SeedTrailerClipGroupAsync` (System-Gruppe — die E2E-Fixture-DB läuft per `EnsureCreatedAsync` ohne Migrations-Seed), Clip-`MediaItem`s mit `Duration`, `ProgramClipGroupItem`-/`ProgramRuleClipGroup`-Links; danach die neuen `TVSenderAdminE2ETests`-Fälle.
12. **Dokumentation:** `docs/help/tv-sender.md` (Gruppen, Karenzzeit, Trailer-Gruppe), `README.md`, `docs/RELEASE_NOTES.md`.
    - Voraussetzungen: Schritte 1–9.
13. **Verifikation:** `dotnet build VideoPlayer.sln` Debug + Release, volle Suite `dotnet test VideoWebPlayer.Tests/VideoWebPlayer.Tests.csproj`, `dotnet ef migrations has-pending-model-changes`, Sabotageproben der neuen Sicherungen.
    - Voraussetzungen: Schritte 1–12.

## Tests

### Neue Tests

| Test / Hilfsmethode | Testklasse | Was wird geprüft / bereitgestellt? |
|--------------------|------------|-------------------------------------|
| `CreateClipGroupAsync`, `AddClipToGroupAsync` | `ProgramScheduleTestDb` (Helper) | Gruppen-/Mitglieds-Seeds (direkter DB-Insert); die Trailer-Gruppe liegt **nicht** automatisch vor (Migrations-`InsertData` läuft unter `EnsureCreatedAsync` nicht) → `SeedTrailerClipGroupAsync` |
| `SeedTrailerClipGroupAsync` | `ProgramScheduleTestDb` (Helper, Aufruf im Fixture-Setup `CreateAsync`) | System-Gruppen-Zeile `ProgramClipGroup { Id = TrailerGroupId, Name = "Trailer", IsSystem = true }` als Ersatz des nur unter `Migrate()` laufenden `InsertData`-Seeds — Voraussetzung aller Trailer-Gruppen-Tests |
| `CreateRuleAsync` um `clipGroupIds`, `placeholderGrace` | `ProgramScheduleTestDb` (Helper) | Regel-Seed mit `ProgramRuleClipGroups`-Links und Karenzzeit |
| `EnsureCoverage_GraceRemainder_FillsWithDistributedClips` | `ProgramScheduleServiceTests_ClipGroups` (neu) | Kernszenario 20–24 Uhr, 2 Titel, Rest < 15 min → kein 3. Titel, Clip-Slots verteilt vor/zwischen/nach Titeln, Ende ≈ Periodenende |
| `EnsureCoverage_RemainderAboveGrace_StartsNextTitle_Overshoots` | dto. | Rest ≥ Karenzzeit → weiterer Titel wie bisher |
| `EnsureCoverage_RemainderExactlyGrace_StartsNextTitle` | dto. | Grenzfall Rest == Karenzzeit → Titel startet noch |
| `EnsureCoverage_NoGroups_UnchangedOvershootBehavior` | dto. | Regel ohne Gruppen: bisheriges Verhalten unverändert (Steuerbeleg zu AK 5) |
| `EnsureCoverage_LastClip_MayOvershootPeriodEnd` | dto. | Letzter Clip ragt über `SlotDuration`-Ende, wenn Raum frei |
| `EnsureCoverage_ClipTooLargeForFreeRoom_LeavesDocumentedGap` | dto. | Nichts Passendes → Rest in `RemainingGapSeconds` |
| `EnsureCoverage_ClipsSkipItemsWithoutDuration` | dto. | Gruppen-Mitglied ohne `Duration` wird nicht geplant |
| `EnsureCoverage_ClipGroup_NotFullySharedSource_YieldsNoClipSlots` | dto. | Freigabefilter `access.Allows` in `ExpandClipGroupItemsAsync`: Mitglied aus einer nicht für **alle** Nicht-Admins freigegebenen Quelle liefert keine Clip-Slots (Analogon `EnsureCoverage_ClipSource_NotFullySharedSource_YieldsNoSlots`, `ProgramScheduleServiceTests_ClipSource.cs:107`; Seed über `AddUserAsync` + `GrantSourceAccessAsync`) |
| `EnsureCoverage_TwoClipGroups_RoundRobinDistributed` | dto. | Regel mit ≥ 2 Benutzergruppen: die Clip-Entnahme verteilt sich per Round-Robin über beide Gruppen-Ringe (Clips aus beiden Gruppen erscheinen abwechselnd im Sendeplatz) |
| `EnsureCoverage_ClipOvershoot_ReservedBoundWindow_ClipDropped` | dto. | Letzter Clip über `periodEnd` ragt in ein reserviertes `Bound`-Fenster einer höherprioren Regel → Clip wird verworfen, Rest bleibt `RemainingGapSeconds` (festgelegte Hard-Bound-Entscheidung) |
| `EnsureCoverage_ClipRing_RotatesAcrossRuns` | dto. | Persistierter `clipgroup:{id}`-Cursor setzt Ring fort |
| `EnsureCoverage_TrailerGroup_AirsTrailerBeforeScheduledMovie` | dto. | Trailer-Clip innerhalb der Periode, dessen Film im Vorlauffenster startet |
| `EnsureCoverage_TrailerGroup_NoLeadMatch_OtherGroupsFill` | dto. | Ohne Vorlauf-Treffer füllen die anderen Gruppen |
| `EnsureCoverage_TrailerClip_ExcludesItemsWithoutDuration` | dto. | Trailer ohne `Duration` wird nicht geplant |
| `EnsureCoverage_EpisodeCountAndSlotDuration_FillsTailWithClips` | dto. | Regel mit beiden Feldern: nach `EpisodeCount`-Titeln Rest mit Clips |
| `EnsureCoverage_InterleavedRule_ClipFillDistributed` | dto. | Multi-Strang-Ausstrahlung bekommt Clip-Slots verteilt |
| `Reconcile_ClipFillSlots_StayScheduled` | dto. | Ausstrahlung mit Clip-Slots überlebt Reproduce-Check |
| `Reconcile_RemovedClipGroupMember_DisplacesAndReplans` | dto. | Entferntes Mitglied → Ausstrahlung invalid → verdrängt, neu geplant |
| `Reconcile_WorkerLoop_WithClipFill_NoDisplacedAccumulation` | dto. | Wiederholte Reconcile-Läufe ohne Displaced-Anhäufung/Overlap |
| `ClipGroupLinks_CascadeOnRuleAndGroupAndItemDelete`, `ClipGroupName_UniqueNoCase`, `ClipGroupItem_CompositeKey_DuplicateRejected` | `ProgramClipGroupTests` (neu, echtes SQLite) | Kaskaden, NOCASE-Unique, Composite-PK — gegen echtes SQLite |
| `CreateRule_ClipGroupsOnFiller_Throws`, `…_WithoutSlotDuration_Throws`, `…_UnknownGroup_ThrowsKeyNotFound`, `…_TrailerGroup_Allowed`, `…_GraceOutOfRange_Throws`, `CreateAndUpdateRule_ClipGroupLinksRoundTripped` | `ProgramRuleServiceTests_ClipGroups`/`_Validation` | Serverseitige Regel-Validierung + Link-Roundtrip (Muster `…_GenreMaterial`) |
| `CreateGroup_ReservedTrailerName_Throws`, `RenameGroup_ReservedTrailerName_Throws`, `CreateRenameDelete_RoundTrips`, `SystemGroup_NotModifiable`, `AddClips_ClassifiedItem_Rejected`, `GetClipCandidates_OnlyUnclassifiedVideos`, `*_NonAdmin_ThrowsForbidden` | `ProgramClipGroupServiceTests` (neu) | Service-Validierung, Reservierung über **beide** Schreibpfade (`CreateGroupAsync` und `RenameGroupAsync` — Umgehungspfad), Kandidatenmenge, Admin-Erzwingung |
| `MissingTrailerSeedRow_Tolerated` | `ProgramClipGroupServiceTests` | Nach gezieltem Entfernen der Seed-Zeile: `GetGroupsAsync` fehlerfrei ohne „Trailer"-Zeile, `CreateRuleAsync` mit `TrailerGroupId` → `KeyNotFoundException`; `BuildClipGroupStates` liefert ohne Zeile keinen Gruppen-Zustand (Alt-Backup-Toleranz, s. Risiken) |
| `GetNowAsync_TrailerClipSlot_ShowsMovieDetails` | `ProgramGuideServiceTests` | Trailer-Clip-Slot mit gesetztem `MovieId` zeigt die Film-Details — Anzeige konsistent zum bestehenden `Trailer`-Material (der `MediaItem.Name`-Fallback greift nur bei `MovieId == null`, d. h. bei Benutzergruppen-Clips) |
| `SeedTrailerClipGroupAsync`, `SeedClipItemsAsync`, `SeedClipGroupAsync`/`SeedClipGroupItemAsync`, `SeedRuleClipGroupLinkAsync` | `PlaylistsE2ETestBase` (E2E-Seed-Hilfen) | Clip-`MediaItem`s mit `Duration`, Trailer-System-Gruppen-Zeile (die E2E-Fixture-DB läuft per `EnsureCreatedAsync` ohne Migrations-`InsertData`-Seed), Gruppen-/Mitglieds-/Regel-Links für die neuen E2E-Fälle |
| `ReadFromAsync_LegacyBackupWithoutClipGroupTables_RestoresSuccessfully` | `VideoWebPlayerBackupDataTests_ClipGroups` (neu) | Alt-Backup ohne neue Tabellen + ohne `PlaceholderGrace` → erfolgreich, Default 15 min |
| `ReadFromAsync_ClipGroupTables_RoundTrips` | dto. | Neue Tabellen/Spalte überleben Backup→Restore |
| Tab sichtbar, Gruppen-CRUD, Kandidatenliste mit „keine Laufzeit"-Kennzeichnung, Regel-Dialog zeigt `rule-clip-groups` + `rule-grace-minutes`, Trailer-Gruppe nicht bearbeitbar, `MaterialLabel`-Suffix | `TVSenderAdminTests` (bUnit) | UI-Verhalten ohne Browser |

### Betroffene bestehende Tests

| Test / Testklasse | Grund der Anpassung |
|-------------------|---------------------|
| `ProgramScheduleTestDb.CreateRuleAsync`-Aufrufer | Erweiterte Signatur — neue optionale Parameter, bestehende Aufrufe kompilieren unverändert (prüfen) |
| `ProgramScheduleTestDb`-Fixtures insgesamt | `CreateAsync` seedet künftig zusätzlich die Trailer-System-Gruppen-Zeile (`SeedTrailerClipGroupAsync`) — bestehende Tests sehen eine zusätzliche `ProgramClipGroups`-Zeile (keine Fachlogik betroffen; bei eventuellen Tabellen-Assertions prüfen) |
| `ProgramRuleServiceTests_*` (`ProgramRuleInput`-Konstruktionen) | Record erweitert um zwei optionale Endparameter — quellkompatibel; bei benanntem Vollkonstruktor ggf. ergänzen |
| `TVSenderAdminTests` (bUnit) | Neue Tab-/Dialog-Elemente können bestehende Render-Assertions berühren — konkrete betroffene Assertions im Implementierungslauf identifizieren; `MaterialLabel`-Suffix ggf. Erwartungsanpassung |
| `ProgramScheduleServiceTests_Boundary`/`_Generation` | Grace-Schwelle greift nur bei zugeordneten Gruppen — bestehende Regeln ohne Gruppen müssen unverändert grün bleiben (Steuernachweis, keine Anpassung erwartet) |

### E2E-Tests (primärer Funktionsnachweis)

| Priorität | Szenario | Testdatei / Testklasse | Abgedecktes Akzeptanzkriterium | Warum E2E nötig ist |
|-----------|----------|------------------------|-------------------------------|-------------------|
| Pflicht | Clip-Gruppe anlegen, umbenennen, löschen; reservierte „Trailer"-Gruppe sichtbar und nicht bearbeitbar; Versuch „Trailer" als Name wird abgewiesen | `TVSenderAdminE2ETests` (neuer Fall, Muster `Admin_CreateEditDeactivateDeleteRule`) | AK 1, 2 | Einziger anwendersichtbarer CRUD-Fluss der Gruppenverwaltung |
| Pflicht | Clips über die Kandidaten-Auswahl einer Gruppe zuordnen; Eintrag ohne Laufzeit gekennzeichnet; Entfernen | `TVSenderAdminE2ETests` (neuer Fall) | AK 1 | Auswahl-/Zuordnungsinteraktion nur über UI erreichbar |
| Pflicht | Regel mit ≥ 1 Gruppe (inkl. „Trailer") und Karenzzeit anlegen/bearbeiten/speichern; Zuordnung in Regel-Liste sichtbar | `TVSenderAdminE2ETests` (neuer Fall, Muster `Admin_GenreRule_MultiSelectAndSourceFilter`) | AK 2, 3 | Mehrfachauswahl + Karenzfeld ist der zentrale Regel-Benutzerfluss |
| Pflicht | Sendeplan zeigt Clip-Slots: Seed-DB mit Clips + Regel (Grace greifend) über die neuen E2E-Seed-Hilfen (`SeedTrailerClipGroupAsync`, `SeedClipItemsAsync`, Gruppen-/Regel-Links — `EnsureCreatedAsync`-DB ohne Migrations-Seed), „Jetzt generieren" → Slot-Karten mit Clip-`MediaItem.Name`-Fallback | `TVSenderAdminE2ETests` (neuer Fall) | AK 4 | Nachweis, dass Füllung im realen UI-Fluss sichtbar wird (Funktionskern zusätzlich per Service-Tests abgesichert) |

Welche bestehenden E2E-Tests müssen angepasst werden?

| Test / Testklasse | Grund der Anpassung |
|-------------------|---------------------|
| Keine | Neue Tabs/Felder sind additiv; `Admin_CreateEditDeactivateDeleteRule`, `Admin_GenreRule_MultiSelectAndSourceFilter`, `NonAdmin_SeesNotAuthorized` bleiben gültig |

## Offene Punkte

Keine.
