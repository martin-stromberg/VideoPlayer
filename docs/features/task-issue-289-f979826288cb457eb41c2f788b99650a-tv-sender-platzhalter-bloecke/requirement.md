# Übersetzte Anforderung: Platzhalter-Blöcke im Sendeplan kompakt darstellen

Quelle: Issue #289 (Folge-Issue zu #277 „Platzhalter-Clip-Gruppen mit karenzzeitgesteuerter Lückenfüllung im Sendeplatz").
Branch: `task-issue-289-f979826288cb457eb41c2f788b99650a-tv-sender-platzhalter-bloecke`.

## Fachliche Zusammenfassung

Die Sendeplan-Darstellung wird um eine rein darstellerische Gruppierung erweitert: Jeder zusammenhängende Lauf von `ProgramSlot`-Zeilen, die von `ProgramScheduleService.InsertClipFill`/`FillAiringSegments` als Clip-Füllung einer Sendeplatzregel erzeugt wurden (Restzeit unterhalb von `ProgramRule.PlaceholderGrace`), kollabiert zu **einem** Listeneintrag mit Zeitraum (`Start` des ersten bis `End` des letzten Clips), Gesamtdauer und Clip-Anzahl. Ein Symbol-Button mit zugänglichem Namen (z. B. `aria-label="Platzhalter-Clips anzeigen"`) öffnet eine Detailansicht — Overlay-Panel nach dem `admin-dialog-overlay`-Muster oder Inline-Aufklappen — mit den Einzelclips in Abspielreihenfolge (Clip-Name, Startzeit, Dauer). Einplanung und Abspielreihenfolge (`ProgramSlot`-Bestand, `ProgramRuleCursor`) bleiben unverändert; durch reguläre Sendungen unterbrochene Clip-Läufe bleiben als getrennte Blöcke sichtbar.

## Einzelanforderungen

- **Blockbildung:** Aufeinanderfolgende Clip-Füll-`ProgramSlot`s bilden in der Anzeige einen Block. Der Block endet am ersten Nicht-Clip-Slot (regulärer Titel, Füller- oder andere Sendung) — getrennte Blöcke werden nicht über andere Sendungen hinweg zusammengezogen.
- **Blockeintrag:** Zeigt Zeitraum, Gesamtdauer (`End - Start` des Blocks) und nach Möglichkeit die Clip-Anzahl; keine fehlende Beschreibung als leeres Feld.
- **Detailansicht:** Symbol-Button am Block; alle zugehörigen Clips in Abspielreihenfolge (Reihenfolge = `ProgramSlot.Start`), mindestens Clip-Name sowie Startzeit und Dauer, soweit im Sendeplan vorhanden. Der Button erhält einen zugänglichen Namen (`aria-label`).
- **Keine Planänderung:** Die Gruppierung erfolgt ausschließlich beim Aufbau der Anzeige — `ProgramScheduleService`, `ProgramSlot`-Persistenz, `Reconcile` und die Auslieferung (`LiveStreamCoordinator`) bleiben unangetastet.
- **UI-Test:** Ein UI-Test deckt einen Block mit vielen Clips und das Öffnen der Detailansicht ab (Muster: bUnit `TVSenderAdminTests*` bzw. Playwright `TVSenderAdminClipGroupsE2ETests`).

## Betroffene Klassen und Komponenten

- Datenmodell: `ProgramSlot` (`VideoWebPlayer/Data/ProgramSlot.cs`) — Clip-Füll-Slots sind heute **nicht** explizit markiert: `FillAiringSegments` setzt `RuleType = rule.RuleType` der erzeugenden Regel (nicht `ProgramRuleType.Filler`) und `ProgramRuleId = rule.Id`; Trailer-Clips tragen zusätzlich `MovieId`. Die einzige heutige Identifikation ist die `clipIds`-Klassifikation in `ProgramScheduleService` (`MediaItemId` ∈ Clip-Gruppen-Expansion der Regel, vgl. `ProgramScheduleService` ~Z. 521–560, 841–853). Ggf. neues denormalisiertes Kennzeichen (siehe Implementierungsansatz), `ApplicationDbContext`, EF-Konfiguration `ProgramSlotConfiguration`, ggf. Migration.
- Logik / Services: `IProgramScheduleService.GetScheduleAsync` (liefert die `schedule`-Liste des Admin-Tabs, `maxCount` 200 — viele Kurzclips begrenzen heute den sichtbaren Horizont zusätzlich); bei Ausweitung auf die öffentliche Ansicht `IProgramGuideService`/`ProgramGuideService` (`GetUpcomingAsync`, `LoadDetailsAsync`, `ToEntry`) und `DtoProgramGuideEntry`.
- UI: `VideoWebPlayer/Components/Pages/Admin/TVSenderAdmin.razor` — Tab „Sendeplan" (`#tvsender-schedule-cards`, `program-grid`): Render-Loop über `schedule` mit `tvsender-day-header` (Tagesgruppierung) und `tvsender-rule-divider` (bei `ProgramRuleId`-Wechsel), `SlotCard`/`ScheduleCardContent`, `IsRunning`/`ProgressPercent` (LIVE-Badge/Fortschritt — ggf. auf Blockebene zu übertragen). Overlay-Muster `admin-dialog-overlay` ist auf derselben Seite vorhanden.
- UI (öffentlich, scope-abhängig): `VideoWebPlayer/Components/Pages/LiveTv.razor` (`#live-tv-upcoming`, `DtoProgramGuideEntry`-Liste über `IProgramGuideApiClient` → `ProgramGuideController` `/api/program-guide`). **Randbedingung:** `GetNowAsync` speist auch `LiveStreamCoordinator` — dort darf nicht gruppiert werden.
- Neues Anzeige-View-Model (Annahme): Block-Darstellung (Zeitraum, Gesamtdauer, Clip-Anzahl, geordnete Clip-Liste) als Erweiterung um `ScheduleCardContent` bzw. eigenen Record im `TVSenderAdmin`-Code-Behind.
- Tests: `TVSenderAdminTests*` (bUnit, `VideoWebPlayer.Tests`), E2E nach Muster `TVSenderAdminClipGroupsE2ETests` (Playwright), Fixture-Hilfen `ProgramScheduleTestDb` (`CreateClipGroupAsync`, `AddClipToGroupAsync`, `SeedTrailerClipGroupAsync`); bei neuer `ProgramSlot`-Spalte `VideoWebPlayerBackupData` (`OptionalRestoreColumns`) + Alt-Backup-Regressionstest.
- Dokumentation: `docs/help/tv-sender.md` (Abschnitte „Sendeplan" und „Clip-Gruppen mit Karenzzeit im Sendeplatz"), `README.md`, `docs/RELEASE_NOTES.md`.

## Implementierungsansatz

- **Clip-Erkennung (zentrale Designentscheidung der Planung):**
  - Variante A: denormalisiertes Flag/Kind auf `ProgramSlot` (z. B. `IsClipFill`/`ProgramSlotKind`), gesetzt in `FillAiringSegments` — robust gegenüber späteren Gruppen-/Regeländerungen und Regel-Löschung (`ProgramRuleId` kann `null` werden), erfordert EF-Migration + `OptionalRestoreColumns`-Eintrag + Alt-Backup-Test.
  - Variante B: Laufzeit-Erkennung über `ProgramClipGroupItem`-Mitgliedschaft (`MediaItemId` in den über `ProgramRuleClipGroup` an `ProgramRuleId` hängenden Gruppen) — ohne Schemaänderung, aber abhängig vom aktuellen Gruppenbestand und der noch vorhandenen Regel.
- **Gruppierung beim Anzeigeaufbau:** Aus `schedule` eine Anzeige-Sequenz mit Block-Elementen bauen; ein neuer Block beginnt am ersten Nicht-Clip-Slot. Der Render-Loop erhält einen Zweig für Block-Karten (Zeitraum, Dauer, Anzahl, Detail-Button); Tages-Header und Regel-Trennlinie bleiben bestehen (Blockgrenzen an Tag/Regel: siehe Offene Fragen).
- **Detailansicht:** Overlay-Dialog (`admin-dialog-overlay`-Muster wie die vorhandenen Bestätigungsdialoge) oder `details`-artiges Aufklappen in der Karte; Liste der Block-`ProgramSlot`s mit `Start.ToLocalTime()`, Dauer `End - Start` und Titel (`MediaItem.Name`-Fallback wie in `LoadDetailsAsync`/`ScheduleCardContent`).
- **Abhängigkeiten:** kein Eingriff in `ProgramScheduleService` außer ggf. dem Setzen des Clip-Kennzeichens; `IsRunning`/`ProgressPercent` sinnvoll auf Blockebene abbilden, wenn ein laufender Slot im Block liegt (Annahme).
- **Live-TV-Seite (nur falls im Scope):** `DtoProgramGuideEntry` um Block-Information erweitern oder gruppierte Einträge liefern; `GetNowAsync`/`LiveStreamCoordinator` unverändert lassen.

## Konfiguration

Die Anforderung verlangt keine Konfigurierbarkeit — keine neue Anwendungseinstellung. (Optional denkbar, nicht gefordert: Mindest-Clip-Anzahl, ab der gruppiert wird; Entscheidung im Plan.)

## Offene Fragen

- **Scope:** Gilt die kompakte Darstellung nur für den Admin-Tab „Sendeplan" (`TVSenderAdmin.razor`) oder auch für die öffentliche Live-TV-Vorschau (`LiveTv.razor`, `/api/program-guide`)? Das Issue sagt „Sendeplan"; Programmauskunft und Live-TV zeigen dieselben Clip-Slots.
- **Clip-Quelle-Füller:** Sollen auch kurze Füller-Slots einer `ProgramRuleType.Filler`-Regel mit Material „Clip-Quelle" (`ProgramRuleMaterialType.MediaSourceClips`) zu Blöcken zusammengefasst werden? Fachlich gleichartige Platzhalter-Clips, aber außerhalb des #277-Mechanismus.
- **Ein-Clip-Block:** Bleibt ein einzelner Clip-Slot eine normale Karte oder wird auch er als Block mit Detail-Button dargestellt?
- **Verdrängte Slots:** Sollen `ProgramSlotStatus.Displaced`-Clip-Slots im Admin-Plan ebenfalls gruppiert werden oder unverändert einzeln (Traceability)?
- **Blockgrenzen:** Zählt ein Regelwechsel (`ProgramRuleId`) oder die Tagesgrenze (`tvsender-day-header`) als Trenner — also je Regel/Tag ein eigener Block — oder gruppiert strikt nach zeitlicher Zusammenhängigkeit der Clip-Slots?
- **Detail-Mechanik:** Overlay-Dialog oder Inline-Aufklappen — das Issue lässt beide Varianten zu; Entscheidung im Plan nach dem vorhandenen `admin-dialog-overlay`-Muster.
