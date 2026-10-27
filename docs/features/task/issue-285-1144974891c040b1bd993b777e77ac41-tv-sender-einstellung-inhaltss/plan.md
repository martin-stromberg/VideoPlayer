# Umsetzungsplan: TV-Sender — Einstellung „Inhaltsspektrum" (Freigabe-Prüfung der Planung optional aufheben)

## Übersicht

Eine neue senderweite Admin-Einstellung „Inhaltsspektrum" in `TVSenderAdmin.razor` (Tab „Einstellungen", Unterkarte „Planung") schaltet die Zuschauer-Freigabeprüfung der Sendeplanung zwischen „Nur für alle Benutzer freigegebene Inhalte" (Default, bisheriges Verhalten) und „Alle Inhalte, die sich aus den Regeln ergeben" um. Persistiert wird sie als neue `Setups`-Spalte `ProgramScheduleContentScope` (Enum-Int) über `ProgramSettingsService`; ausgewertet wird sie zentral in `ProgramScheduleService.LoadAccessContextAsync`, wo der „Alle Inhalte"-Modus den bereits vorhandenen `Everything`-Zweig des `AccessContext` liefert — damit greifen alle vier `Allows`-Prüfstellen (Titel, Clip-Quelle, Clip-Gruppen, Trailer) und alle drei Einstiegspunkte (`EnsureCoverageAsync`, `ReconcileAsync`, `GetRuleDiagnosesAsync`) ohne Einzeländerung.

## Designentscheidungen

| Komponente / Bereich | Gewählter Ansatz | Begründung |
|----------------------|-----------------|------------|
| Ablageort der Einstellung | Neue Spalte `Setup.ProgramScheduleContentScope` (nicht `TVChannel`) | Alle vergleichbaren senderweiten Admin-Einstellungen (`TvSenderEnabled`, `HolidayFederalState`, `ProgramScheduleHorizonHours`, `ProgramScheduleIntervalMinutes`, `LiveStreamStandbyMediaItemId`) liegen in der Ein-Zeilen-Tabelle `Setups` und werden über `ProgramSettingsService` gepflegt. `TVChannel` trägt heute keinerlei Einstellungsspalten — ein Bruch dieser Konvention ohne Mehrwert, da genau ein Sender ausgeliefert wird. |
| Datentyp | Enum `ProgramScheduleContentScope` (`SharedOnly = 0`, `AllContent = 1`), persistiert als `int` | Die UI zeigt zwei benannte Optionen; ein Enum bildet sie sprechend ab und bleibt erweiterbar (z. B. späterer Per-Regel- oder Teilspektrum-Modus). `SharedOnly = 0` als Default garantiert: Alt-Backups ohne Spalte und korrupte Werte fallen auf das restriktive Verhalten zurück. Restore-Default geht über die bestehende `OptionalRestoreIntDefaults`-Tabelle. |
| Auswertungspunkt | `LoadAccessContextAsync` liefert im `AllContent`-Modus `AccessContext { Everything = true }` | Minimal-invasiv: ein einziger Punkt deckt Titel-Expansion (`TryMovieItem` Z. 1877, `ToItem` Z. 1921), Trailer (`ExpandTrailersAsync` Z. 2132) und Clip-Pfade (`FilterClipCandidatesAsync` Z. 2438) sowie `EnsureCoverageAsync`, `ReconcileAsync` und `GetRuleDiagnosesAsync` ab. `SkippedNoAccess`/`NoAccessCount` werden im `AllContent`-Modus zwangsläufig 0, weil `Allows` stets `true` liefert. |
| Reconcile-Semantik beim Zurückschalten | Kein Sonderfall: der bestehende Mitgliedschafts-Check von `ReconcileAsync` verdrängt nicht mehr reproduzierbare Slots | Entspricht „wirkt auf die nächste Planerstellung" (Annahme der Anforderung, konsistent zum Verhalten bei Regel-/Freigabeänderungen). Kein zusätzlicher Aufräum-Code nötig. |
| Regel-Diagnose | Kein zusätzlicher Hinweis; `GetRuleDiagnosesAsync` nutzt denselben `AccessContext` | Konsistent: Im `AllContent`-Modus gibt es faktisch keine Zugriffs-Ausschlüsse mehr; die Diagnose spiegelt das wirksame Verhalten (Annahme der Anforderung). |
| UI-Steuerelement | `select` mit zwei Klartext-Optionen + Hinweistext, in der Unterkarte „Planung" | Direkte Wiederverwendung des bestehenden Musters `#tvsender-federal-state` (Z. 500–508, Klartext-Dropdown) und des `form-text`-Hinweismusters der Standby-Karte (Z. 547). Keine ID-Eingabe, benannte Optionen erfüllen die UI-Anforderung. |
| Worker-Wecksignal | `ScheduleSignal.NotifyScheduleChanged()` nach jedem erfolgreichen `SaveChannelSettingsAsync` | Konvention aus `ProgramSettings.razor` Z. 234: planrelevante Änderungen sollen nicht bis zum nächsten Intervall warten. Die Karte bündelt ohnehin nur plan-/streamrelevante Einstellungen; das Signal koalesziert Wake-ups, ein pauschales Feuern nach Erfolg ist einfacher und sicherer als ein Änderungsvergleich. |
| Persistenz der Einstellung vs. Betreiber-Konfiguration | `Setups`-Spalte, **nicht** `ProgramScheduleOptions`/appsettings | Die Anforderung verlangt eine in der Admin-Oberfläche änderbare Einstellung; `ProgramScheduleOptions` (`TrailerLeadDays` u. a.) ist reine Betreiber-Konfiguration ohne Admin-UI-Pfad. |
| Per-Regel-Override | Nicht implementiert | Von der Anforderung bewusst ausgenommen („erstmal global"). |

## Programmabläufe

### Einstellung anzeigen und speichern (Admin-UI)

1. `OnInitializedAsync` (`TVSenderAdmin.razor` Z. 1020) lädt nach den bestehenden Planungswerten (Z. 1035–1037) den Wert über `ProgramSettings.GetProgramScheduleContentScopeAsync()` in das neue Feld `contentScope`.
2. Das `select` `#tvsender-content-scope` in der Unterkarte „Planung" bindet an `contentScope`; Optionen: `SharedOnly` = „Nur für alle Benutzer freigegebene Inhalte", `AllContent` = „Alle Inhalte, die sich aus den Regeln ergeben". Ein `form-text`-Hinweis (`#tvsender-content-scope-hint`) erklärt die Konsequenz des `AllContent`-Modus: Zuschauer sehen im Stream Inhalte, die ihnen in der Mediathek nicht freigegeben sind.
3. `SaveChannelSettingsAsync` (Z. 1611) ruft nach `UpdateProgramScheduleSettingsAsync` zusätzlich `ProgramSettings.UpdateProgramScheduleContentScopeAsync(contentScope)`; der Updater validiert den Enum-Wert (`Enum.IsDefined`, sonst `ArgumentException`), setzt `setup.ProgramScheduleContentScope` über `GetOrCreateSetupAsync` und speichert via `SaveChangesAsync`.
4. Nach erfolgreichem Speichern feuert `SaveChannelSettingsAsync` `ScheduleSignal.NotifyScheduleChanged()` — der `ProgramScheduleWorker` wacht sofort auf und plant mit der neuen Einstellung. Danach Statusmeldung „Einstellungen wurden gespeichert."

Beteiligte Klassen/Komponenten: `TVSenderAdmin`, `ProgramSettingsService`, `Setup`, `ProgramScheduleContentScope`, `IProgramScheduleSignal`/`ProgramScheduleSignal`, `ProgramScheduleWorker`.

### Planungslauf mit Inhaltsspektrum (alle Expansionspfade)

1. `EnsureCoverageAsync` (Z. 89), `ReconcileAsync` (Z. 167) und `GetRuleDiagnosesAsync` (Z. 412) rufen wie bisher `LoadAccessContextAsync` auf.
2. `LoadAccessContextAsync` liest zuerst die Einstellung über `_settings.GetProgramScheduleContentScopeAsync(cancellationToken)`.
3. Bei `AllContent` liefert die Methode sofort `new AccessContext { Everything = true, FullySharedSourceIds = [], UnlockedShowIdsForAll = [], UnlockedCollectionIdsForAll = [] }` — vor den Benutzer-/Freigabe-Abfragen (spart drei Queries, analog zum bisherigen „keine Nicht-Admin-Benutzer"-Early-Return Z. 1301–1310).
4. Bei `SharedOnly` läuft der bisherige Pfad unverändert: Nicht-Admin-Benutzer laden, `FullySharedSourceIds`/`UnlockedShowIdsForAll`/`UnlockedCollectionIdsForAll` als Schnitt über alle Benutzer bilden.
5. Die vier `Allows`-Prüfstellen (`TryMovieItem` Z. 1877, `ToItem` Z. 1921, `ExpandTrailersAsync` Z. 2132, `FilterClipCandidatesAsync` Z. 2438) bleiben unverändert; im `AllContent`-Modus liefert `Allows` stets `true` — `SummaryCounters.SkippedNoAccess` (und damit `ProgramRuleDiagnosis.NoAccessCount`) bleibt 0.

Beteiligte Klassen/Komponenten: `ProgramScheduleService` (`LoadAccessContextAsync`, `AccessContext`, `SummaryCounters`), `ProgramSettingsService`, `ProgramScheduleWorker` (ruft `ReconcileAsync` + `EnsureCoverageAsync` je Intervall).

### Umschalten zurück auf „nur freigegebene Inhalte" (Reconcile)

1. Der Admin stellt die Einstellung auf `SharedOnly` zurück und speichert; `NotifyScheduleChanged` weckt den Worker.
2. Der nächste `ReconcileAsync`-Lauf expandiert die Regeln mit dem restriktiven `AccessContext`; zuvor im `AllContent`-Modus geplante Slots mit nicht freigegebenen Inhalten sind gegen die aktuelle Expansion nicht reproduzierbar und werden `Displaced` — der Plan wird lückenlos neu geplant.
3. `EnsureCoverageAsync` verlängert danach nur noch mit freigegebenen Inhalten.

Beteiligte Klassen/Komponenten: `ProgramScheduleService` (`ReconcileAsync`, `LoadAccessContextAsync`), `ProgramSlot` (`Status = Displaced`).

### Restore eines Alt-Backups ohne die neue Spalte

1. `VideoWebPlayerBackupData.ReadFromAsync` findet `Setups.ProgramScheduleContentScope` nicht im Backup-Index.
2. Der Eintrag in `OptionalRestoreColumns` toleriert die fehlende Spalte; `OptionalRestoreIntDefaults` liefert den Restore-Default `(Setups, ProgramScheduleContentScope, 0)` = `SharedOnly`.
3. Die restaurierte `Setups`-Zeile verhält sich restriktiv — identisch zum bisherigen Verhalten.

Beteiligte Klassen/Komponenten: `VideoWebPlayerBackupData`, `Setup`, `ProgramScheduleContentScope`.

## Neue Klassen

| Klasse | Typ | Zweck |
|--------|-----|-------|
| `ProgramScheduleContentScope` | Enum (`VideoWebPlayer/Data/ProgramScheduleContentScope.cs`) | Inhaltsspektrum der Sendeplanung: `SharedOnly = 0` (nur für alle Nicht-Admin-Benutzer freigegebene Inhalte — Default) und `AllContent = 1` (alle Inhalte, die sich aus den Regeln ergeben; Zuschauer-Freigabe wird ignoriert). |

## Änderungen an bestehenden Klassen

### `Setup` (Datenmodellklasse, `VideoWebPlayer/Data/Setup.cs`)

- **Neue Eigenschaften:** `ProgramScheduleContentScope` (`ProgramScheduleContentScope`) — Inhaltsspektrum der Sendeplanung; Property-Initialisierer `= ProgramScheduleContentScope.SharedOnly` (Konvention: Spalten-Defaults in den Initialisierern). XML-Doc im Stil der übrigen Sender-Eigenschaften.

### `ProgramSettingsService` (Klasse, `VideoWebPlayer/Services/ProgramSettingsService.cs`)

- **Neue Methoden:**
  - `GetProgramScheduleContentScopeAsync(CancellationToken)` — liest `setup.ProgramScheduleContentScope` über `GetOrCreateSetupAsync`; nicht definierte Werte (korrupte DB) werden auf `SharedOnly` normalisiert (Muster der `<= 0`-Normalisierung in `GetProgramScheduleHorizonAsync`). Rückgabe `Task<ProgramScheduleContentScope>`.
  - `UpdateProgramScheduleContentScopeAsync(ProgramScheduleContentScope scope, CancellationToken)` — validiert `Enum.IsDefined(scope)`, sonst `ArgumentException` (Muster `UpdateHolidayFederalStateAsync` Z. 329–338); setzt den Wert via `GetOrCreateSetupAsync` + `SaveChangesAsync`.

### `ProgramScheduleService` (Klasse, `VideoWebPlayer/Services/ProgramScheduleService.cs`)

- **Geänderte Methoden:** `LoadAccessContextAsync` (Z. 1298) — liest zu Beginn die persistierte Einstellung über `_settings.GetProgramScheduleContentScopeAsync` und liefert bei `AllContent` unmittelbar den `Everything`-`AccessContext` (derselbe Mechanismus wie der bisherige „keine Nicht-Admin-Benutzer"-Zweig). Bei `SharedOnly` unverändert.

### `TVSenderAdmin` (Razor-Komponente, `VideoWebPlayer/Components/Pages/Admin/TVSenderAdmin.razor`)

- **Neue Felder:** `contentScope` (`ProgramScheduleContentScope`, Default `SharedOnly`) im Feld-Block Z. 925–944.
- **Neues UI-Element:** `select` `#tvsender-content-scope` mit zwei Klartext-Optionen + Hinweistext `#tvsender-content-scope-hint` (`form-text`) in der Unterkarte „Planung" (Z. 510–520), Muster `#tvsender-federal-state` (Z. 500–508).
- **Geänderte Methoden:**
  - `OnInitializedAsync` (Z. 1020) — lädt `contentScope` via `GetProgramScheduleContentScopeAsync` nach Z. 1037.
  - `SaveChannelSettingsAsync` (Z. 1611) — ruft `UpdateProgramScheduleContentScopeAsync(contentScope)` und feuert danach `ScheduleSignal.NotifyScheduleChanged()` (Konvention `ProgramSettings.razor` Z. 234).

### `VideoWebPlayerBackupData` (Klasse, `VideoWebPlayer/Services/Backups/VideoWebPlayerBackupData.cs`)

- **Neue Listeneinträge:**
  - `OptionalRestoreColumns`: `$"{nameof(ApplicationDbContext.Setups)}.{nameof(Setup.ProgramScheduleContentScope)}"` (nach Z. 109).
  - `OptionalRestoreIntDefaults`: `(nameof(ApplicationDbContext.Setups), nameof(Setup.ProgramScheduleContentScope), (int)ProgramScheduleContentScope.SharedOnly)` (nach Z. 154; Muster `(Setups, ProgramScheduleHorizonHours, 48)` Z. 149 bzw. `(int)ProgramSlotStatus.Scheduled` Z. 151).

## Datenbankmigrationen

| Migrationsname | Betroffene Tabellen/Spalten | Beschreibung der Änderung |
|----------------|----------------------------|---------------------------|
| `AddSetupProgramScheduleContentScope` | `Setups.ProgramScheduleContentScope` (`INTEGER`, `NOT NULL`, `defaultValue: 0`) | Neue Spalte für das Inhaltsspektrum; Namensmuster `AddSetup<Property>` (vgl. `20261026154346_AddSetupLiveStreamStandbyMediaItem`). `Down` entfernt die Spalte. |

Hinweis (AGENTS.md §3): Bei parallelen Schritten mit eigenen Migrationen kollidiert `ApplicationDbContextModelSnapshot.cs` — wer später mergt, erzeugt seine Migration nach dem Rebase neu.

## Validierungsregeln

| Feld / Objekt | Regel | Fehlerfall |
|---------------|-------|------------|
| Parameter `scope` von `UpdateProgramScheduleContentScopeAsync` | `Enum.IsDefined` — nur `SharedOnly`/`AllContent` zulässig | `ArgumentException` (Muster `UpdateHolidayFederalStateAsync`) |
| Persistierter `Setups.ProgramScheduleContentScope`-Wert | Ungültige Int-Werte werden leseseitig auf `SharedOnly` normalisiert | Kein Fehler — restriktiver Fallback (robust gegen korrupte Werte) |
| UI-Auswahl | `select` bietet nur die zwei Enum-Werte an | Nicht zutreffend — keine freie Eingabe möglich |

## Konfigurationsänderungen

Keine. Die Einstellung ist eine persistierte Admin-Einstellung in `Setups` — bewusst kein Eintrag in `ProgramScheduleOptions`/`appsettings` (siehe Designentscheidungen).

## Seiteneffekte und Risiken

- **Regel-Diagnose (`GetRuleDiagnosesAsync`):** Im `AllContent`-Modus zeigt die Diagnose keine Zugriffs-Ausschlüsse mehr (`NoAccessCount = 0`) — gewünscht und konsistent, da dieselbe Expansion gilt; in `docs/help/tv-sender.md` mitzudokumentieren.
- **Bestehender Sendeplan beim Zurückschalten:** `ReconcileAsync` verdrängt nicht mehr zulässige Slots sofort beim nächsten Lauf — erwünscht (siehe Designentscheidung), aber als Verhaltensänderung im Hinweistext/Hilfe erwähnenswert.
- **Stream-Auslieferung:** `TvSenderUserAccess`/`IsTvSenderAccessGrantedAsync` (Navigation, `/live-tv`, Program-Guide-, Live-Stream-Endpunkte) bleiben unverändert — Zuschauer können im Stream Inhalte sehen, die in der Mediathek nicht für sie freigegeben sind. Genau diese Konsequenz trägt der UI-Hinweistext.
- **Backup/Restore:** Ohne die Einträge in `OptionalRestoreColumns`/`OptionalRestoreIntDefaults` bräche der Restore von Alt-Backups; beide Einträge sind Pflichtbestandteil (AGENTS.md DoD) samt Regressionstest.
- **Migration-Snapshot:** `ApplicationDbContextModelSnapshot.cs` kann mit parallelen Schritten kollidieren — Migration ggf. nach Rebase neu erzeugen (AGENTS.md §3).
- **Bestehende Tests:** Keine brechen — der Default `SharedOnly` entspricht exakt dem bisherigen Verhalten; der `ProgramScheduleTestDb`-Seed (`Setup { TvSenderEnabled = true }`) braucht keine Anpassung.

## Umsetzungsreihenfolge

1. **Enum `ProgramScheduleContentScope` anlegen**
   - Voraussetzungen: Keine.
   - Beschreibung: `VideoWebPlayer/Data/ProgramScheduleContentScope.cs` mit `SharedOnly = 0`, `AllContent = 1`, XML-Doc im Stil von `ProgramRuleMaterialType.cs`.

2. **`Setup`-Spalte + EF-Migration**
   - Voraussetzungen: Schritt 1 (Enum-Typ), `dotnet ef`-Werkzeug bzw. Migrations-Namensmuster vorhanden.
   - Beschreibung: `Setup.ProgramScheduleContentScope` mit Initialisierer `SharedOnly`; Migration `AddSetupProgramScheduleContentScope` (`AddColumn<int>`, `defaultValue: 0`); Model-Snapshot aktualisieren.

3. **`ProgramSettingsService`: Getter + Updater**
   - Voraussetzungen: Schritte 1–2.
   - Beschreibung: `GetProgramScheduleContentScopeAsync` (mit Normalisierung auf `SharedOnly`) und `UpdateProgramScheduleContentScopeAsync` (mit `Enum.IsDefined`-Validierung, `GetOrCreateSetupAsync`, `SaveChangesAsync`).

4. **`ProgramScheduleService.LoadAccessContextAsync`: Scope-Auswertung**
   - Voraussetzungen: Schritt 3 (`_settings`-Feld bereits injiziert, Z. 30).
   - Beschreibung: Einstellung lesen; bei `AllContent` früh `Everything`-`AccessContext` zurückgeben; `SharedOnly`-Pfad unverändert.

5. **Backup-Restore-Einträge**
   - Voraussetzungen: Schritt 2 (Spalte existiert).
   - Beschreibung: `OptionalRestoreColumns` + `OptionalRestoreIntDefaults` in `VideoWebPlayerBackupData` ergänzen.

6. **UI: `TVSenderAdmin.razor`**
   - Voraussetzungen: Schritt 3 (Updater aufrufbar); Muster `#tvsender-federal-state`/`form-text` vorhanden.
   - Beschreibung: Feld `contentScope`; `select` `#tvsender-content-scope` + Hinweistext in der Unterkarte „Planung"; Laden in `OnInitializedAsync`; `UpdateProgramScheduleContentScopeAsync` + `NotifyScheduleChanged()` in `SaveChannelSettingsAsync`.

7. **Unit-/Integrationstests (echtes SQLite)**
   - Voraussetzungen: Schritte 1–5; Testinfrastruktur `ProgramScheduleTestDb`, `PairingTestDb`, `LegacyBackupArchiveBuilder` bereits vorhanden.
   - Beschreibung: `ProgramSettingsServiceContentScopeTests`, `ProgramScheduleServiceTests_ContentScope` (siehe Tests), `VideoWebPlayerBackupDataTests_ProgramScheduleContentScope`.

8. **bUnit-Test der Auswahl**
   - Voraussetzungen: Schritt 6; `TVSenderAdminTests.CreateContextAsync` vorhanden.
   - Beschreibung: `TVSenderAdminTests_ContentScope` — Rendern, Optionen, Hinweistext, Persistenz über `#tvsender-save-settings`.

9. **E2E-Test (Playwright)**
   - Voraussetzungen: Schritte 4 + 6; `TVSenderAdminE2ETestBase` (`EnableTvSenderAsync`, `OpenAdminPageAsAdminAsync`, `SwitchTabAsync`) vorhanden.
   - Beschreibung: `Admin_ContentScope_…` — Auswahl persistieren, Hinweistext, Planungswirkung über „Jetzt generieren"/Worker (siehe E2E-Tests).

10. **Dokumentation + Release Notes**
    - Voraussetzungen: Schritte 1–9.
    - Beschreibung: `docs/help/tv-sender.md`, `docs/help/tv-sender-ablauf-technisch.md`, `docs/help/tv-sender-datenmodell.md`, `docs/RELEASE_NOTES.md` (EN+DE); `README.md` nur bei Bedarf.

11. **Abschlussnachweis**
    - Voraussetzungen: Schritte 1–10.
    - Beschreibung: `dotnet build VideoPlayer.sln` in Debug **und** Release fehlerfrei; volle Suite `dotnet test VideoWebPlayer.Tests/VideoWebPlayer.Tests.csproj` grün (Definition of Done, AGENTS.md §7).

## Tests

### Neue Tests

| Test / Hilfsmethode | Testklasse | Was wird geprüft / bereitgestellt? |
|--------------------|------------|-------------------------------------|
| `GetProgramScheduleContentScope_DefaultsToSharedOnly` | `ProgramSettingsServiceContentScopeTests` (neu, `VideoWebPlayer.Tests/ProgramSettingsServiceContentScopeTests.cs`, Muster `ProgramSettingsServiceLiveStreamTests.cs`) | Frische `Setups`-Zeile liefert `SharedOnly`. |
| `UpdateProgramScheduleContentScope_Roundtrip` | `ProgramSettingsServiceContentScopeTests` | `AllContent` speichern → Getter liefert `AllContent`; zurück auf `SharedOnly` persistiert. |
| `UpdateProgramScheduleContentScope_InvalidValue_Throws` | `ProgramSettingsServiceContentScopeTests` | Nicht definierter Enum-Wert → `ArgumentException` (Muster `UpdateHolidayFederalStateAsync_UnknownCode_Throws`). |
| `EnsureCoverage_AllContent_SchedulesUnreleasedMovieAndEpisode` | `ProgramScheduleServiceTests_ContentScope` (neu, `VideoWebPlayer.Tests/ProgramScheduleServiceTests_ContentScope.cs`) | Zwei Nicht-Admin-Benutzer, Quelle nur für einen freigegeben (Muster `EnsureCoverage_NotFullyReleased_IsExcluded`); Scope = `AllContent` → Film- und Episoden-Slots werden geplant, `SkippedNoAccess == 0`. |
| `EnsureCoverage_AllContent_SchedulesClipSourceItems` | `ProgramScheduleServiceTests_ContentScope` | Füller „Clip-Quelle" auf nicht vollständig geteilter Quelle liefert im `AllContent`-Modus Clip-Slots (Gegenstück zu `EnsureCoverage_ClipSource_NotFullySharedSource_YieldsNoSlots`). |
| `EnsureCoverage_AllContent_SchedulesClipGroupItems` | `ProgramScheduleServiceTests_ContentScope` | Benutzer-Clip-Gruppe mit Mitgliedern aus nicht geteilter Quelle liefert Clip-Slots (Gegenstück zu `EnsureCoverage_ClipGroup_NotFullySharedSource_YieldsNoClipSlots`). |
| `EnsureCoverage_AllContent_SchedulesTrailers` | `ProgramScheduleServiceTests_ContentScope` | Trailer-Expansion (`IsTrailer`-Link auf Film mit Ausstrahlung) plant Trailer aus nicht geteilter Quelle — deckt `ExpandTrailersAsync` und die System-Gruppe „Trailer" ab. |
| `EnsureCoverage_SharedOnlyDefault_StillFiltersUnreleased` | `ProgramScheduleServiceTests_ContentScope` | Scope explizit `SharedOnly` (und/oder unveränderte `Setups`-Zeile) → `SkippedNoAccess >= 1`, keine Slots — bewusster Regressionstest des Defaults neben den bestehenden Access-Tests. |
| `EnsureCoverage_ScopeSwitch_TakesEffectNextRun` | `ProgramScheduleServiceTests_ContentScope` | Erster Lauf restriktiv (keine Slots) → `UpdateProgramScheduleContentScopeAsync(AllContent)` → nächster `EnsureCoverageAsync`-Lauf plant den nicht freigegebenen Inhalt (AK 5, „wirkt auf die nächste Planerstellung"). |
| `Reconcile_SwitchBackToSharedOnly_DisplacesUnreleasedSlots` | `ProgramScheduleServiceTests_ContentScope` | `AllContent` plant nicht freigegebenen Slot → zurück auf `SharedOnly` → `ReconcileAsync` markiert den Slot `Displaced` und plant lückenlos neu (Muster `ProgramScheduleServiceTests_Reconcile`). |
| `GetRuleDiagnoses_AllContent_ReportsNoAccessExclusions` | `ProgramScheduleServiceTests_ContentScope` | `GetRuleDiagnosesAsync` liefert im `AllContent`-Modus `NoAccessCount == 0` für eine Regel mit nicht freigegebenem Material. |
| `ReadFromAsync_LegacyBackupWithoutContentScopeColumn_RestoresSharedOnly` | `VideoWebPlayerBackupDataTests_ProgramScheduleContentScope` (neu, `VideoWebPlayer.Tests/Services/Backups/`, Muster `VideoWebPlayerBackupDataTests_TvSenderEnabled.cs`) | Alt-Backup ohne die Spalte (`LegacyBackupArchiveBuilder.RemoveColumnsAsync`), Seed bewusst `AllContent` → nach Restore steht `SharedOnly` (0). |
| `ContentScope_RendersOptionsAndHint_DefaultsToSharedOnly` | `TVSenderAdminTests_ContentScope` (neu, `VideoWebPlayer.Tests/Components/`) | bUnit: `select` `#tvsender-content-scope` zeigt beide Klartext-Optionen, Default `SharedOnly` vorausgewählt, Hinweistext `#tvsender-content-scope-hint` sichtbar. |
| `ContentScope_SavePersistsSelection` | `TVSenderAdminTests_ContentScope` | bUnit: Auswahl `AllContent` + `#tvsender-save-settings` → `Setups.ProgramScheduleContentScope == AllContent` persistiert (Muster `StandbyCard_ShowsDefault_And_PersistsSelection`). |

Hilfsmethoden: Keine neuen nötig — `ProgramScheduleTestDb` (`AddUserAsync`, `GrantSourceAccessAsync`, `CreateMovieAsync`, `CreateShowAsync`, `CreateMediaItemAsync`, `CreateClipGroupAsync`, `AddClipToGroupAsync`, `CreateRuleAsync`, `SeedTrailerClipGroupAsync`), `PairingTestDb` und `LegacyBackupArchiveBuilder` decken alle Seeds ab. Der Scope-Wechsel erfolgt über `new ProgramSettingsService(fixture.Db, NullLogger<…>)` bzw. direktes Setzen der `Setups`-Zeile im Seed.

### Betroffene bestehende Tests

Keine — die Änderung ist additiv und der Default `SharedOnly` entspricht exakt dem bisherigen Verhalten; die bestehenden Access-/Clip-Source-/Clip-Group-/Trailer-Tests sichern den restriktiven Pfad unverändert weiter ab.

### E2E-Tests (primärer Funktionsnachweis)

| Priorität | Szenario | Testdatei / Testklasse | Abgedecktes Akzeptanzkriterium | Warum E2E nötig ist |
|-----------|----------|------------------------|-------------------------------|-------------------|
| Pflicht | Admin öffnet `/admin/tv-sender` → Tab „Einstellungen" → `select` `#tvsender-content-scope` zeigt „Nur für alle Benutzer freigegebene Inhalte" vorausgewählt, Hinweistext zur Stream-Konsequenz ist sichtbar; Wechsel auf „Alle Inhalte, die sich aus den Regeln ergeben" + Speichern → `Setups.ProgramScheduleContentScope == AllContent` in der DB | `TVSenderAdminE2ETests` — neue Methode `Admin_ContentScope_SelectAndPersist` | AK 1, 4, 6 | Der Benutzerfluss (Auswahl → Speichern → serverseitige Persistenz) ist nur über die echte Seite nachweisbar; der Hinweistext ist eine UI-Pflicht der Anforderung. |
| Pflicht | Kontrastlauf der Planung: Seed = Film in einer Quelle, die ein ge seedeter Nicht-Admin-Benutzer nicht sehen darf + tägliche Film-Regel → „Jetzt generieren" liefert **keinen** Slot; dann Inhaltsspektrum auf „Alle Inhalte" umschalten, speichern, erneut generieren → der Film erscheint als Slot im Sendeplan-Tab | `TVSenderAdminE2ETests` — neue Methode `Admin_ContentScope_AllContent_SchedulesUnreleased` (darf mit dem Persistenz-Szenario in einem Browser-Run kombiniert werden — Konvention „one browser run because Playwright tests are expensive", Z. 446–447) | AK 2, 3, 5 | Primärer Funktionsnachweis, dass die Einstellung die tatsächliche Planung über den Worker/`NotifyScheduleChanged`-Pfad wirkt — Unit-Tests decken `LoadAccessContextAsync`, aber nicht den Zusammenhang UI → Persistenz → Signal → Worker → sichtbarer Sendeplan. |

Für das zweite Szenario wird im Test über `Services.CreateScope()` ein Nicht-Admin-`ApplicationUser` ohne `MediaSourceUser`-Eintrag sowie ein Film mit probeter Laufzeit in einer nicht geteilten Quelle geseedet (Muster `SeedProbeItemsAsync`); die Plan-Generierung erfolgt über `#tvsender-generate-now` im Sendeplan-Tab (Status „aktualisiert", Muster Z. 213–216).

Anzupassende bestehende E2E-Tests: Keine — das neue Feld ist additiv und verändert keine bestehenden Selektoren oder Abläufe (`Admin_CreateEditDeactivateDeleteRule` speichert die Einstellungen weiterhin mit dem Default-Wert).

## Offene Punkte

Keine. Die in der Anforderung gelisteten offenen Fragen (Ablageort `Setup` vs. `TVChannel`, Datentyp Bool vs. Enum, Reconcile-Sofortverdrängung, Diagnose-Hinweis, Per-Regel-Override) sind aus den Projekt-Konventionen bzw. den in der Anforderung genannten Annahmen eindeutig ableitbar und wurden als Designentscheidungen festgelegt.
