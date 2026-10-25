# Übersetzte Anforderung: Platzhalter-Clip-Gruppen mit karenzzeitgesteuerter Lückenfüllung im Sendeplatz (Issue #277, Nachbesserung zu Issue #111)

## Fachliche Zusammenfassung

Der automatisierte TV-Sender soll Platzhalter-Clips („Maskottchen"/Interstitials) zusätzlich zu ihrer bisherigen Verwendung als eigenes `ProgramRuleType.Filler`-Material (`ProgramRuleMaterialType.MediaSourceClips`) als begleitende Füllclips **innerhalb** einer `SlotDuration`-Ausstrahlung einsetzen: Clip-`MediaItem`s werden in benannte Gruppen eingeteilt, Gruppen werden an `ProgramRule`s zugeordnet, und eine neue je-Regel-Karenzzeit (Vorauswahl 15 Minuten) entscheidet am Periodenende, ob noch ein weiterer Titel mit Überlauf eingefügt wird oder die verbleibende Restzeit durch verteilt eingefügte Clip-Slots möglichst exakt auf das Periodenende aufgefüllt wird. Zusätzlich zu den frei anlegbaren Gruppen existiert die reservierte, nicht manuell befüllbare Gruppe **„Trailer"**, die — anknüpfend an den bestehenden Vorlauf-Mechanismus `ProgramScheduleOptions.TrailerLeadDays` (Default 7 Tage) — automatisch die Trailer der zukünftig im Sendeplan ausgestrahlten Filme liefert und an jeder Regel wie eine normale Gruppe auswählbar ist. Die bisherige Einplanung von Clips als eigenes Sendematerial über `ProgramRuleMaterialType.MediaSourceClips` in `FillGaps` bleibt davon unberührt nutzbar.

## Auswahl- und Zuordnungsanforderungen

- **Clip → Gruppe:** Der Anwender teilt gefundene Platzhalter-Clips aus der Menge der vorhandenen Clip-Kandidaten benannten Gruppen zu (z. B. „Tagesbeginn", „Tagesabschluss", „Zwischensequenzen") — eine explizite Zuordnungsinteraktion, keine Eingabe interner Kennungen.
- **Gruppe → Regel:** Der Anwender wählt bei Anlage/Bearbeitung einer Sendeplatz-Regel eine oder mehrere Platzhalter-Gruppen aus der Menge der angelegten Gruppen aus (Mehrfachauswahl), einschließlich der reservierten Gruppe „Trailer".
- **Reservierter Name:** Der Gruppenname „Trailer" kann nicht für frei anlegbare Gruppen vergeben werden (serverseitig zu prüfen).

## Akzeptanzkriterien

1. **Gruppenverwaltung:** Platzhalter-Clips lassen sich in benannte Gruppen einteilen; nur Clips mit bekannter Abspiellänge (`MediaItem.Duration`) werden verwendet — Clips ohne bekannte Länge werden aus der Verwendung ausgenommen bzw. in der Verwaltung als solche kenntlich gemacht.
2. **Reservierte Gruppe „Trailer":** Steht an jeder Regel zur Auswahl und liefert Trailer der Filme, die innerhalb des Vorlauffensters `TrailerLeadDays` zukünftig ausgestrahlt werden; nicht manuell befüllbar; Name für eigene Gruppen reserviert.
3. **Regel-Zuordnung:** An einer Sendeplatz-Regel lassen sich ≥ 1 Platzhalter-Gruppen zuordnen und die Karenzzeit einstellen (Default 15 Minuten).
4. **Lückenfüllung im Periodenende:** Beispielszenario Regel 20:00–24:00, Titel enden 23:45, Karenzzeit 15 min → kein dritter Titel; Clips aus den zugeordneten Gruppen werden vor und nach den Titeln innerhalb der Periode so verteilt, dass die Ausstrahlung möglichst genau um 24:00 endet. Restzeit wird voll ausgeschöpft; der letzte Clip darf über das Periodenende hinausragen.
5. **Verträglichkeit:** Ohne zugeordnete Gruppe bzw. bei Restzeit ≥ Karenzzeit bleibt das bisherige Verhalten unverändert (weiterer Titel, einmal begonnen läuft er über die weiche Periodengrenze hinaus zu Ende).
6. **Persistenz/Tests:** Neue Tabellen/Spalten stehen in `VideoWebPlayerBackupData` (`OptionalRestoreTables`/`OptionalRestoreColumns` samt Defaults) mit Alt-Backup-Regressionstest und EF-Migration; Tests für Mehrfach-Clip-Füllung, Trailer-Gruppe im Vorlauffenster, Karenzzeit-Grenzfälle und fehlende Gruppen-/Clip-Abdeckung (verbleibender Rest bleibt dokumentierte Lücke).

## Betroffene Klassen und Komponenten

- **Datenmodell:**
  - Neu: Gruppen-Entity für Platzhalter-Gruppen (Namensvorschlag `ProgramClipGroup`, analog `ProgramRule*`-Konvention: `Id`, `Name`, `CreatedAt`/`UpdatedAt`) — Annahme.
  - Neu: Join-Entity Clip ↔ Gruppe auf `MediaItem` (Namensvorschlag `ProgramClipGroupItem`) — Annahme.
  - Neu: Join-Entity Gruppe ↔ Regel auf `ProgramRule` (Namensvorschlag `ProgramRuleClipGroup`, Muster `ProgramRuleGenre`/`ProgramRuleMediaSource`); die reservierte Trailer-Gruppe benötigt eine Kennzeichnung (Flag/Enum oder reservierter Datensatz — Annahme).
  - `ProgramRule`: neue Eigenschaft für die Karenzzeit (Namensvorschlag `PlaceholderGrace`, `TimeSpan`, Default 15 min) plus Navigations-Collection auf die Gruppenzuordnung.
  - `MediaItem.Duration` / `DurationProbedAt` / `DurationProbeAttempts` — bereits vorhanden, wird als „bekannte Abspiellänge" wiederverwendet.
  - `ProgramRuleCursor` / `ProgramCursorContentType` — ggf. erweitert, falls der Clip-Fortschritt je Gruppe/Regel persistiert rotieren soll (offen).
  - `ApplicationDbContext`: neue `DbSet`s; `Data/Configurations/` für die neuen Entities.
- **Logik/Services:**
  - `ProgramScheduleService`: `SelectAiringItems`/`SelectAiringItemsInterleaved` (Startschwelle: bisher `_minStartRemain` aus `MinStartRemainSeconds`, neu je-Regel-Karenzzeit), `MaterializeOccurrence`/`MaterializeOccurrenceEvicting` (Einfügen der Clip-Slots vor/nach den Titeln), `AiringReproducible`/`FlatAiringTailReproducible`/`InterleavedAiringTailReproducible` (Reproduzierbarkeit der Ausstrahlung mit eingestreuten Clip-Slots), `FillGaps` (Zusammenspiel mit der In-Perioden-Füllung), Wiederverwendung von `ExpandTrailersAsync`, `FindLeadMatchOffset`, `LoadMovieAiringsAsync`/`BuildMovieAiringsFromSlotsAsync`, `TrailerMediaItemIdsAsync` und `ExpandMediaSourceClipsAsync` (Clip-Kandidatenmenge: unklassifizierte `MediaItem`s ohne `MovieMediaItem`-/`TVShowEpisodeMediaItem`-Verknüpfung).
  - `ProgramRuleService` / `IProgramRuleService` / `ProgramRuleInput`: Gruppen-Ids und Karenzzeit im Input-Record, `ValidateRuleInputAsync` (Existenz der Gruppen, Reservierung „Trailer"), `GetRulesAsync`/`CreateRuleAsync`/`UpdateRuleAsync` (Laden/Ersetzen der Gruppenlinks, Muster `ReplaceGenreLinksAsync`), `ProgramScheduleSignal.NotifyScheduleChanged` bei Änderungen.
  - Neu (Vorschlag): Service für Gruppen-CRUD und Clip-Zuordnung (z. B. `IProgramClipGroupService`), serverseitige Admin-Absicherung analog `RequireAdminAsync`.
  - `ProgramGuideService` (`LoadTitlesAsync`/`ToEntry`) und `SlotTitle`/`MaterialLabel` in `TVSenderAdmin.razor`: vorhandener `MediaItem.Name`-Fallback für Clip-Titel wiederverwendbar.
- **Enums:** `ProgramRuleMaterialType` voraussichtlich unverändert (Gruppen sind Zusatz-Zuordnung an der Regel, keine neue Materialart — Annahme); `ProgramCursorContentType` wiederverwendbar (`MediaItem`, `Trailer`), sofern Clip-Fortschritt persistiert wird; für die reservierte Gruppe ggf. neuer Kennzeichnungs-Enum — Annahme.
- **UI-Komponenten/Controller:**
  - `TVSenderAdmin.razor` Regel-Dialog: Mehrfachauswahl der Gruppen (Muster `rule-material-genre`/`rule-material-sources` Checkbox-Listen inkl. reservierter „Trailer"-Gruppe) und Karenzzeit-Eingabefeld (Muster `rule-duration`, Minuten); Darstellung der Zuordnung in der Regel-Liste/`MaterialLabel`.
  - Neue Verwaltungsoberfläche für Gruppen und ihre Clip-Zuordnung (Ort offen: eigene Admin-Seite oder Bereich in `TVSenderAdmin`), inkl. Kennzeichnung von Clips ohne bekannte Laufzeit.
- **Persistenz/Backup:** EF-Migration (neue Tabellen + `ProgramRule`-Spalte); `VideoWebPlayerBackupData` um `OptionalRestoreTables` für die neuen Tabellen und `OptionalRestoreColumns` + Default-Eintrag für die Karenzzeit-Spalte ergänzen (Default 15 min; ggf. Erweiterung des Default-Mechanismus um `TimeSpan`, da bisher nur Bool/Long/Int-Defaults existieren).
- **Tests:** neue `ProgramScheduleServiceTests_*`-Klasse (Verteilung mehrerer Clips vor/nach Titeln, Karenz-Grenzfälle, Trailer-Gruppe im Vorlauffenster, fehlende Clip-Abdeckung → dokumentierte Restlücke via `RemainingGapSeconds`, `ReconcileAsync`-Reproduzierbarkeit mit Clip-Slots), `ProgramRuleServiceTests_Validation` (Reservierung „Trailer", Gruppen-Existenz, Karenzzeit), `VideoWebPlayerBackupDataTests`/`LegacyBackupArchiveBuilder` (Alt-Backup), bUnit `TVSenderAdminTests` und E2E `TVSenderAdminE2ETests` (Muster `Admin_CreateEditDeactivateDeleteRule`). Tests zu Constraints/Löschverhalten der neuen Tabellen gegen echtes SQLite (nicht EF InMemory).

## Implementierungsansatz

- **Karenz-Schwelle:** In `SelectAiringItems`/`SelectAiringItemsInterleaved` (ProgramScheduleService.cs, Z. 1365 ff./1430 ff., Kommentar „a started item always completes") entscheidet heute `_minStartRemain` (`ProgramSchedule:MinStartRemainSeconds`, Default 30 s), ob ein weiterer Titel noch starten darf. Mit ≥ 1 zugeordneter Gruppe gilt stattdessen die je-Regel-Karenzzeit: Restzeit bis zum Periodenende (`SlotDuration`-Ziel) ≥ Karenzzeit → weiterer Titel wie bisher (Overshoot über die weiche Grenze erlaubt); Restzeit < Karenzzeit → kein Titel mehr, stattdessen Clip-Auffüllung. Ohne zugeordnete Gruppe bleibt das bisherige Verhalten unverändert.
- **Clip-Verteilung:** Auswahl aus den Clip-Mengen der zugeordneten Gruppen (voraussichtlich eigene `RuleState`-/Ring-Strukturen je Gruppe — Annahme), verteilt vor und nach den Titel-Slots innerhalb der Periode; nur Clips mit gesetztem `MediaItem.Duration` (Filter analog `ExpandMediaSourceClipsAsync` mit `CountNoDuration`-Diagnose). Die Restzeit ist voll auszuschöpfen; der letzte Clip darf das Periodenende überragen. Fehlt passendes Material, bleibt der Rest dokumentierte Lücke (`SummaryCounters.RemainingGapSeconds`).
- **Reproduzierbarkeit beachten:** `AiringReproducible` spielt `SelectAiringItems` erneut und vergleicht die `ProgramSlot`-Folge slotweise mit der `SchedulableItem`-Folge; eingestreute Clip-Slots müssen dort entweder reproduzierbar mit abgebildet oder als fremde Slots erkannt werden (z. B. über `ProgramSlot.ProgramRuleId`/`RuleType`-Kennzeichnung — Designentscheidung offen).
- **Trailer-Gruppe:** Wiederverwendung der bestehenden Vorlauf-Logik: Kandidatenmenge wie `ExpandTrailersAsync` (`MovieMediaItem.IsTrailer`-Links der Filme mit Ausstrahlung im bedeckten Fenster), Vorlaufprüfung wie `FindLeadMatchOffset` gegen `airingsByMovie` mit `_trailerLead` (`TrailerLeadDays`). Da die Ausstrahlungen erst im selben Lauf materialisiert werden, muss die Trailer-Gruppen-Expansion verzögert erfolgen — analog zum bestehenden `ExpandTrailerStatesAsync`-Mechanismus nach `MaterializeOccurrence`.
- **Zusammenspiel mit `FillGaps`:** `FillGaps` füllt weiterhin Lücken **zwischen** Ausstrahlungen über `ProgramRuleType.Filler`-Regeln; die neue Füllung liegt **innerhalb** der Periode der Ausstrahlung — beide Mechanismen koexistieren, die In-Perioden-Füllung muss vor/mit dem Blockieren der Restzeit durch `blocked`-Intervalle/`newSlots` abgestimmt werden.
- **Validierung serverseitig:** Im `ProgramRuleService` (bzw. neuem Gruppen-Service) wird die Reservierung des Namens „Trailer" und die Existenz zugeordneter Gruppen durchgesetzt; ausgeblendete UI-Elemente gelten nicht als Absicherung.

## Konfiguration

- **Karenzzeit:** pro Datensatz an `ProgramRule` (neue Spalte, Vorauswahl 15 Minuten) — Regel-Ebene, nicht globale Option.
- **Trailer-Vorlauf:** bestehender `IOptions`-Schlüssel `ProgramSchedule:TrailerLeadDays` (Default 7 Tage) wird wiederverwendet — keine neue Einstellung.
- **Reservierter Gruppenname „Trailer":** fachliche Festlegung (Validierung), nicht konfigurierbar.

## Offene Fragen

1. **Geltungsbereich der Lückenfüllung:** Gilt die In-Perioden-Füllung nur für `SlotDuration`-Regeln („Sendeplatz") oder auch für `EpisodeCount`-Regeln, bei denen das Ziel folgen- statt zeitbasiert ist (Annahme: nur `SlotDuration`, da „Periodenende" zeitlich definiert ist)?
2. **Clip-Kandidatenmenge:** Sind für die Gruppenzuordnung nur unklassifizierte `MediaItem`s vorgesehen (dieselbe Menge wie `ProgramRuleMaterialType.MediaSourceClips`, also Videodateien ohne NFO/ohne Film-/Episoden-Verknüpfung), oder sollen beliebige `MediaItem`s in Gruppen einteilbar sein (Annahme: nur unklassifizierte Clips)?
3. **Clip-Rotation:** Soll die Clip-Auswahl über Ausstrahlungen hinweg rotieren (persistierter Fortschritt je Gruppe/Regel über `ProgramRuleCursor`) oder wird pro Restzeit jeweils optimal aus der Gruppe kombiniert, ohne Fortschrittsgedächtnis?
4. **Modell der reservierten Gruppe:** Wird „Trailer" als Sonderfall im Gruppen-Entity (Flag/Enum, nicht manuell befüllbar) oder als vorbelegter Datensatz abgebildet — und was liefert die Gruppe, wenn aktuell kein Film im Vorlauffenster ausgestrahlt wird (leere Gruppe → andere Gruppen/Lücke)?
5. **Verhältnis Karenzzeit ↔ `MinStartRemainSeconds`:** Ersetzt die je-Regel-Karenzzeit bei zugeordneten Gruppen die globale 30-Sekunden-Startschwelle vollständig, oder gilt zusätzlich weiterhin die Mindest-Restlücke?
6. **Harte Grenzen und Konflikte:** Gelten eingefügte Clip-Slots als untrennbarer Teil der Ausstrahlung (atomares Verwerfen bei Kollision wie in `MaterializeOccurrence`) und wie verhalten sie sich gegenüber `Hard`-Bounds höherpriorer Regeln, gegen die kein Overshoot erlaubt ist?
7. **Optimierungskriterium:** Bei nicht exakt passender Clip-Auswahl — soll die Überschreitung des Periodenendes minimiert, die Restlücke minimiert oder eine feste Verteilungsstrategie (z. B. gleichmäßig vor/nach den Titeln) gelten („möglichst genau am Periodenende" vs. „Restzeit voll ausschöpfen")?
