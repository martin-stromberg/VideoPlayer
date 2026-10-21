# Übersetzte Anforderung: Genre-Material — Mehrfachauswahl, Quell-Filter, Status/Alternativnamen (N1+N2+N3)

Quelle: `docs/projects/task/issue-111-6145c51699f34953bf29a244634a23e2-automatisierter-tvsender/nachbesserungen.md` (N1, N2, N3 — gemeinsam, gleiches Datenmodell/Formularfeld).
Projektschritt: Schritt 9 des Projekts „Automatisierter TV-Sender" (`steps.md`).

## Fachliche Zusammenfassung

Die Programmmaterial-Auswahl vom Typ `ProgramRuleMaterialType.Genre` wird von einer Einfachauswahl (`ProgramRule.MaterialId` = genau eine `Genre`-Zeile) zu einer **Mehrfachauswahl kanonischer Genres** erweitert. Zusätzlich kann die Regel per Mehrfachauswahl auf bestimmte `MediaSource`s eingeschränkt werden (leer = alle Quellen, abwärtskompatibel). Die wählbaren Genres folgen denselben Sichtbarkeits- und Alternativnamen-Regeln wie der Rest der Anwendung: `IsHidden`-Genres und Genres außerhalb ihres saisonalen `StartDate`/`EndDate`-Fensters sind nicht wählbar; versteckte Genres mit `GenreName`-Alternativnamen lösen auf das kanonische Ziel-Genre auf. Die Sendeplan-Expansion (`ProgramScheduleService.ExpandGenreAsync`) liefert die deduplizierte Vereinigungsmenge über alle gewählten Genres, eingeschränkt auf die gewählten Quellen, inklusive Alt-Zuordnungen über Alternativnamen.

## Einzelanforderungen

### N1 — Genre-Mehrfachauswahl

- Regelarten mit Genre-Material (`ProgramRuleType.GenreRotation`, `Repeat`, `HolidaySpecial`, `Filler` — siehe `ProgramRuleService.GetAllowedMaterialTypes`) erlauben die Auswahl **mehrerer** Genres pro Regel.
- Datenmodell: n:m-Beziehung `ProgramRuleGenre` (Join-Tabelle `ProgramRuleId` ↔ `GenreId`), EF-Konfiguration, Migration mit Befüllung aus dem bisherigen `MaterialId` (eine Zeile pro bestehender Genre-Regel), Registrierung in `OptionalRestoreTables` (`VideoWebPlayerBackupData`) + Alt-Backup-Regressionstest.
- `ProgramRuleInput` und `ProgramRuleService`-Validierung/CRUD werden auf die Genre-Menge umgestellt (mindestens ein Genre bei `MaterialType.Genre`, serverseitig).
- `TVSenderAdmin.razor`: Mehrfachauswahl statt `rule-material-genre`-Einfach-Dropdown.
- `ExpandGenreAsync` expandiert die **Vereinigungsmenge** aller gewählten Genres: Duplikate (Film/Episode in mehreren Genres) werden dedupliziert; die Cursor-/Rotationssemantik (`ProgramRuleCursor` über `ContentType`/`ContentId`) läuft über die deduplizierte Union — bestehende Cursor bleiben gültig, da `Movie`/`TVShowEpisode`-Content-Ids unverändert bleiben.

### N2 — Quell-Filter bei Genre-Material

- Bei Genre-Material kann die Regel per Mehrfachauswahl auf gewählte `MediaSource`s eingeschränkt werden; **keine Auswahl = alle Quellen** (Default, Abwärtskompatibilität für Bestandsregeln).
- Datenmodell: Join-Tabelle `ProgramRuleMediaSource` (`ProgramRuleId` ↔ `MediaSourceId`), Filter **pro Regel** (nicht pro Genre-Eintrag — siehe Designentscheidung im Plan).
- Wirkt nur bei `MaterialType.Genre`; zusätzlich zum bestehenden Freigabe-Filter (`AccessContext`), nicht ersetzend.
- `ExpandGenreAsync` schränkt Filme (`Movie.MediaSourceId`) und Episoden (`TVShowEpisode.MediaSourceId`) auf die gewählten `MediaSourceId`s ein — einschließlich der Auflösung der Genre-Zeilen (ein kanonisches Genre auf Quelle A darf keine Inhalte aus Quelle B hereinholen, wenn B nicht gewählt ist).

### N3 — Status/Alternativnamen bei der Genre-Auswahl

- Die Auswahlliste zeigt nur **sichtbare kanonische Genres**:
  - `IsHidden = true` → nicht unter eigenem Namen wählbar.
  - Verstecktes Genre mit `GenreName`-Alternativnamen → nur das/die Ziel-Genres der Umleitung sind wählbar (Praxisbeispiel: „Family" versteckt → Alternativname „Familie" → nur „Familie" wählbar).
  - Saisonales Sichtbarkeitsfenster (`StartDate`/`EndDate`, Semantik wie `GenreService.GetSeasonalGenresAsync` — jährlich wiederkehrendes Fenster; Fenster ohne aktuellen Tag = nicht sichtbar) wird mitbehandelt.
  - Quell-Differenzierung (Live-Befund): „Family" ist auf Quellen 1/2 versteckt→„Familie", auf 5/6 sichtbar → die Auswahl muss in Abhängigkeit der gewählten Quellen differenzieren können (sichtbare „Family"-Zeilen auf 5/6 bleiben wählbar, wenn 5/6 im Filter enthalten sind).
- Expansion: Alt-Zuordnungen werden auflösungskonform eingeschlossen — `MovieGenre`/`TVShowGenre`-Zeilen, die noch auf das versteckte Genre verlinken, dessen Alternativname auf ein gewähltes kanonisches Genre zeigt, liefern ihre Inhalte für dieses Genre (analog der Scan-Logik `MediaSourceClassifier.GetOrCreateGenresAsync`, die „Family" beim Einlesen auf „Familie" umlenkt; **pro Quelle** aufgelöst).
- `api/items/genres` (`ItemsController` → `MediaMetadataEditorService.GetGenreOptionsAsync`) wird auch vom Metadaten-Editor (`MovieCollectionDetails`, `TVShowDetails`), `PlaylistGenreEditor` und `PlaylistsList` genutzt — Entscheidung zwischen gemeinsamer Korrektur und eigener Optionsquelle für den TV-Sender ist zu treffen und zu begründen.

## Betroffene Klassen und Komponenten

- Datenmodell: `ProgramRule` (`MaterialType`/`MaterialId`), **neu** `ProgramRuleGenre`, **neu** `ProgramRuleMediaSource`, `Genre`/`GenreName` (nur lesend), `MovieGenre`/`TVShowGenre`, `MediaSource`, `ApplicationDbContext` (neue `DbSet`s), EF-Konfigurationen.
- Logik: `ProgramRuleService` (Validierung, `GetAllowedMaterialTypes`, CRUD), `ProgramRuleInput`, `ProgramScheduleService.ExpandGenreAsync` + `ExpandMaterialAsync`, ggf. neue Hilfslogik für kanonische Genre-Auflösung (Scan-Referenz: `MediaSourceClassifier.GetOrCreateGenresAsync`; Sichtbarkeit: `GenreService.GetSeasonalGenresAsync`, `SourceGenresController`-`IsHidden`-Filter, `GenreAdmin.razor`).
- UI: `VideoWebPlayer/Components/Pages/Admin/TVSenderAdmin.razor` (Mehrfachauswahl Genres + Quellen-Mehrfachauswahl, Materialanzeige).
- Client/DTO: `DtoGenreOption` (bzw. neue serverseitige Optionsquelle); `VideoWebPlayerClient.RequestGenreOptionsAsync` ggf. unverändert.
- Backup: `VideoWebPlayerBackupData` (`OptionalRestoreTables` + Alt-Backup-Regressionstest).
- Tests: `ProgramScheduleServiceTests_*`, `ProgramRuleServiceTests_*`, `TVSenderAdminE2ETests`, `VideoWebPlayerBackupDataTests_*`, `LegacyBackupArchiveBuilder`.

## Implementierungsansatz

- n:m über eigenständige Join-Entitäten mit echten FKs und Kaskadenlöschung (Muster: `MovieGenre`/`TVShowGenre`/`PlaylistGenre`), Migration per `dotnet ef migrations add`, Befüllung der Genre-Join-Tabelle per `migrationBuilder.Sql` aus `ProgramRules.MaterialId`.
- Kanonische Auflösung als wiederverwendbare serverseitige Logik (pro `MediaSource`: Genre-Zeile → Alternativnamen rekursiv mit Zyklenschutz → sichtbares Ziel; Sichtbarkeit = `!IsHidden` und innerhalb saisonalem Fenster).
- `ExpandGenreAsync` arbeitet über die aufgelöste Genre-Zeilen-Menge (Union über gewählte Genres + Alt-Verknüpfungen, eingeschränkt auf Quell-Filter).
- Serverseitige Validierung in `ProgramRuleService.ValidateRuleInputAsync` (Genre-Mehrfachauswahl, Quell-Ids nur bei Genre-Material).
- UI-Mehrfachauswahl nach bestehendem `admin-chip`/`form-check-input`-Muster in `TVSenderAdmin.razor`.

## Konfiguration

Keine neuen Anwendungseinstellungen; der Quell-Filter ist pro Datensatz (Join-Tabelle) konfiguriert.

## Offene Fragen

- Keine fachlichen Rückfragen — Anforderung ist aus `nachbesserungen.md` + Live-Befund ableitbar. Zu entscheidende Designfragen (im Plan zu beantworten): Speicherung der Genre-Auswahl als `GenreId` vs. kanonischer Name; gemeinsame vs. eigene Optionsquelle für den TV-Sender; Filter pro Regel vs. pro Genre-Eintrag.
