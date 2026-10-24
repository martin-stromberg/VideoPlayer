# Umsetzungsplan: Schritt 26 — N23–N25 (drei getrennte Commits)

## Commit 1 — N23: Deterministischer Hash-Mix der Stränge

**Diagnose:** `RuleState.SetItems` ordnet Stränge in Erstauftrittsreihenfolge an (`"other"` zuerst,
dann Serien alphabetisch); `StrandStartOffset` liefert bei `wanted >= m` immer 0 → alphabetisch frühe
Serien belegen zuverlässig die vorderen Positionen.

**Fix:** In `SetItems` die Strang-`order`-Liste nach einem stabilen, prozess-invarianten Hash aus
(`rule.Id`, `strandKey`) sortieren — private statische `internal` Hilfsmethode `StrandOrderKey`
(FNV-1a). Ergebnis: reproduzierbare, regelabhängige, nicht alphabetische Reihenfolge; stabile
Tagespositionen (N19) und `wanted < m`-Tagesrotation bleiben erhalten. Episodenreihenfolge im Strang
und Cursor-Semantik (`NextIndexFor`, `PersistCursors` pro `StrandKey`) unverändert.

**Tests** (`ProgramScheduleServiceTests_Interleaving.cs` + neue Datei):
- `StableSlotsAcrossDays` umschreiben: konsistente Position je Tag (gleiche Serie an gleicher
  Position — Identität nicht mehr hartkodiert).
- Neu: mehrere Genre-Regeln über denselben Serienbestand → nicht alle Regeln starten mit der
  alphabetisch ersten Serie (über `rule.Id` variierend); zusätzlich direkter Test der Hash-Ordnung
  über `InternalsVisibleTo`.

## Commit 2 — N24: Upcoming-Refresh robust + kein Prerender-Leak

**Fix in `LiveTv.razor`:**
- Poll-Start von `OnInitializedAsync` nach `OnAfterRenderAsync(firstRender)` — die Schleife lebt nur
  auf der interaktiven Instanz (kein weggeworfener Prerender-Timer).
- `RefreshGuideAsync`: `now` und `upcoming` unabhängig abrufen (je try) — ein dauerhaft
  fehlschlagender Endpoint friert den anderen nicht mehr ein.
- `TimeProvider` injizieren → `PeriodicTimer(PollInterval, _timeProvider)`.

**Tests** (`LiveTvTests.cs`):
- `ControllableTimeProvider`-Testhelfer (ITimer manuell auslösbar): Tick → Upcoming neu geladen +
  UI aktualisiert.
- `now` schlägt fehl, `upcoming` liefert → Liste aktualisiert trotzdem.

## Commit 3 — N25: `Setup.TvSenderEnabled`-Flag + Absicherung

**Datenmodell:** `Setup.TvSenderEnabled` (bool, Default `false` — Feature erst nach Freischaltung
sichtbar); EF-Migration `AddSetupTvSenderEnabled`; `OptionalRestoreColumns` +
`OptionalRestoreBoolDefaults` (false).

**Settings:** `ProgramSettingsService.GetTvSenderEnabledAsync`/`UpdateTvSenderEnabledAsync`;
`GeneralSettingsUpdate` + `UpdateGeneralSettingsAsync` um das Flag erweitern (positional call sites
anpassen); `ProgramSettings.razor`: Checkbox „TV-Sender freischalten" + Model-Feld; nach Speichern
`IProgramScheduleSignal.NotifyScheduleChanged()` damit Worker zügig reagieren.

**Absicherung:**
- `NavMenu.razor`: `ProgramSettingsService` injizieren, Link nur wenn Flag.
- `LiveTv.razor`: Flag-Check → Hinweis statt Inhalt (kein Poll).
- `TVSenderAdmin.razor`: Flag-Check → Hinweis + Verweis auf „Allgemein" (Kachel bleibt).
- `ProgramGuideController`, `LiveStreamController`: `ProgramSettingsService` injizieren → 404 ohne Flag.
- Worker: `ProgramScheduleWorker` (Reconcile+Ensure überspringen), `LiveStreamWorker`
  (`ILiveStreamSegmenter.StopAsync` + überspringen), `MediaDurationProbeWorker` (Probe überspringen,
  `ProgramSettingsService` im Scope).

**Tests:**
- `ProgramScheduleTestDb.CreateAsync` seedet `Setup { TvSenderEnabled = true }` → Fixture-Tests bleiben grün.
- `ProgramScheduleWorkerTests`/`MediaDurationProbeWorkerTests`: Flag-Seed ergänzen.
- Controller-Tests: Konstruktor anpassen; neu: ohne Flag → 404.
- `NavMenuLiveTvTests`: Flag-Seed + neuer Disabled-Fall.
- `LiveTvTests`: `ProgramSettingsService` in Test-DI registrieren (Flag an).
- `TVSenderAdminTests`: Flag-Seed.
- Neu: Worker idles (Schedule-Worker erzeugt keine Slots bei Flag aus; LiveStream-Worker stoppt
  Segmenter / ruft Coordinator nicht); Settings-Roundtrip; Backup-Spaltenregistrierung.
- E2E: `ProgramGuideE2ETests`, `LiveStreamE2ETests`, `LiveTvE2ETests`, `TVSenderAdminE2ETests` —
  Flag in Test-DB setzen (Hilfsmethode).

## Doku

`docs/help/tv-sender.md` (Freischaltung), `README.md`, `docs/RELEASE_NOTES.md`.

## Offene Punkte

Keine — Flag-Default `false` gemäß Anforderung (Anwender schaltet in „Allgemein" frei);
Toggle in „Allgemein" statt in `/admin/tv-sender`, damit die Admin-Seite selbst gesperrt werden kann.
