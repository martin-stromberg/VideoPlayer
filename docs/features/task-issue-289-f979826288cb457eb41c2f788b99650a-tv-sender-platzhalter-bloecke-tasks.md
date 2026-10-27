# Tasks: Platzhalter-Blöcke im Sendeplan kompakt darstellen

| # | Bereich | Aufgabe | Status | Testnachweis |
|---|---------|---------|--------|--------------|
| 1 | Datenmodell | `ProgramSlot.IsClipFill` (`bool`, Default `false`) anlegen | Offen | — |
| 2 | Datenmodell | `ProgramSlotConfiguration`: `IsClipFill` als `IsRequired()` konfigurieren | Offen | — |
| 3 | Datenmodell | EF-Migration `AddProgramSlotIsClipFill` erzeugen (`defaultValue: false`, kein Backfill) + `ApplicationDbContextModelSnapshot` aktualisieren | Offen | — |
| 4 | Datenmodell | `VideoWebPlayerBackupData`: `ProgramSlots.IsClipFill` in `OptionalRestoreColumns` + `OptionalRestoreBoolDefaults` (Default `false`) eintragen | Offen | — |
| 5 | Logik | `ProgramScheduleService.FillAiringSegments`: `IsClipFill = true` am erzeugten `ProgramSlot` setzen | Offen | — |
| 6 | Testinfrastruktur | `ProgramScheduleTestDb.CreateSlotAsync`: optionaler Parameter `bool isClipFill = false` | Offen | — |
| 7 | UI | `ScheduleDisplayItem`/`ScheduleSlotItem`/`ScheduleClipBlockItem`-Records + `internal static BuildScheduleDisplayItems` im `@code` von `TVSenderAdmin.razor` (Blockgrenzen: Tag, `ProgramRuleId`-Wechsel, Zeitlücke, Nicht-Scheduled-Clip-Eintrag; Block ab ≥ 2 Clips) | Offen | — |
| 8 | UI | Sendeplan-Render-Loop auf `BuildScheduleDisplayItems` umstellen; Regel-Trennlinie über `ProgramRuleId` des ersten Slots je Eintrag | Offen | — |
| 9 | UI | Block-Karte `.program-card.tvsender-clip-block` (`data-first-slot-id`, `data-clip-count`): Zeitraum, Gesamtdauer, „N Clips", „Regel: …", Status-Chip, Live-Badge + Fortschritt über Blockspanne | Offen | — |
| 10 | UI | Detail-Button `.tvsender-clipblock-toggle` mit `aria-label="Platzhalter-Clips anzeigen"` + `clipBlockDetail`-State | Offen | — |
| 11 | UI | Overlay-Dialog `#tvsender-clipblock-dialog` im `admin-dialog-overlay`-Muster: Header „Platzhalter-Clips (N)", `.tvsender-clipblock-row` je Clip (`data-slot-id`, Start lokal, Dauer, Titel via `SlotCard`), Schließen per Button + Overlay-Klick | Offen | — |
| 12 | UI | `app.css`: Styles für `.tvsender-clip-block` (soweit über `.program-card` hinaus nötig) und `.tvsender-clipblock-row`-Liste | Offen | — |
| 13 | Tests | `ScheduleDisplayItemsTests` (neu): Blockbildung, Einzelclip ohne Block, Trennung durch Nicht-Clip-Slot/Regelwechsel/Zeitlücke, `Displaced` als Einzelkarte + Trenner, Reihenfolge | Offen | — |
| 14 | Tests | `ProgramScheduleServiceTests_ClipGroups`: Test „generierte Clip-Füll-Slots tragen `IsClipFill`, Titel nicht" (echtes SQLite) | Offen | — |
| 15 | Tests | `VideoWebPlayerBackupDataTests_ProgramSlotIsClipFill` (neu): Alt-Backup ohne Spalte via `LegacyBackupArchiveBuilder` + aktuelles Backup erhält `true` | Offen | — |
| 16 | Tests | `TVSenderAdminTests_ClipBlocks` (neu, bUnit): Block-Karte statt Einzelkarten; Detail-Dialog listet alle Clips in Reihenfolge; getrennte Läufe → getrennte Blöcke; `Displaced`-Clip nicht gruppiert | Offen | — |
| 17 | E2E-Tests | `TVSenderAdminClipBlocksE2ETests` (neu, Playwright, Basis `TVSenderAdminE2ETestBase`): Block mit vielen geseedeten `IsClipFill`-Slots → eine Block-Karte → `aria-label`-Button → Detail-Overlay mit allen Clips in Reihenfolge + Schließen | Offen | — |
| 18 | E2E-Tests | `TVSenderAdminClipGroupsE2ETests.GeneratedSchedule_ContainsVisibleClipSlots` anpassen: statt `.tvsender-slot-row` „funki-clip.mkv" Block-Karte + Detail-Dialog asserten | Offen | — |
| 19 | Dokumentation | `docs/help/tv-sender.md`: Abschnitt „Sendeplan" um Block-Darstellung + Detail-Button ergänzen; Abschnitt „Wirkung im Sendeplan" entsprechend verweisen | Offen | — |
| 20 | Dokumentation | `docs/RELEASE_NOTES.md` Eintrag; `README.md` auf nötige Ergänzung prüfen | Offen | — |
