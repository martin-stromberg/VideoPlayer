# Bestandsaufnahme: Sendeplan bei Änderungen nachführen (Schritt 4)

Analyse der Sendeplan-, Regel- und Medienbestands-Codepfade bezogen auf die Anforderung `requirement.md` (Sendeplan bei Regel- und Medienänderungen nachführen, ungültige Sendeplätze als „verdrängt" markieren und ersetzen).

## Zusammenfassung

- **Sendeplan-Erzeugung existiert vollständig** (Schritt 3): `ProgramScheduleService.EnsureCoverageAsync` erzeugt rollierend Sendeplätze — Occurrences je Regel, Prioritäts-Konfliktauflösung (atomar je Ausstrahlung), Füller-Lückenfüllung, `ProgramRuleCursor`-Fortschritt, Filter für `Duration` und Vollfreigabe. Der Lauf ist bewusst **extend-only**: `coverageStart = max(now, max(ProgramSlots.End))`; bestehende Sendeplätze werden nie angefasst — Reaktion auf Änderungen fehlt noch komplett.
- **`ProgramSlot` hat keinen Status** — die Aufgabenbeschreibung nannte einen Status, der existiert nicht. Es gibt nur angelegte Zeilen; eine neue Spalte/Enum `Status` (`Scheduled`/`Displaced`) ist erforderlich (Migration + `OptionalRestoreColumns` + Alt-Backup-Regressionstest). `MediaItemId`, `MovieId`, `TVShowEpisodeId`, `ProgramRuleId` sind bewusst **weiche Referenzen ohne FK** — Löschkaskaden entfallen von sich aus, verwaiste Referenzen sind abfragbar.
- **Trigger-Punkte existieren:** `ProgramRuleService` (Create/Update/SetRuleActive/Delete) für Regel-CRUD; Medien-Löschung läuft fast ausschließlich über `ApplicationDbContext.DeleteMediaSourceAsync`/`DeleteMediaCollectionsForSourceAsync` mit **Bulk-`ExecuteDeleteAsync`** (umgeht Change-Tracker-Hooks!) — ein `SaveChanges`-Hook allein sieht Medienlöschungen nicht; der periodische Worker-Sweep bzw. ein explizites Signal sind nötig. Neue Episoden/Medien entstehen über Scan (`MediaSourceScanService` → `MediaSourceScanner`/`MediaSourceClassifier`), der die `SaveChanges`-Hooks durchläuft (`PlaylistBackfillMarker`-Mechanismus dokumentiert denselben Pfad).
- **Wiederverwendbares Signal-Muster vorhanden:** `IPlaylistBackfillSignal`/`PlaylistBackfillSignal` (Singleton, 1-Slot-Channel, `BeginScan()`-Entprellung während Scans) + `PlaylistBackfillWorker` (wartet auf Signal ODER Timer via `Task.WhenAny`). `ApplicationDbContext` nimmt das Signal bereits als optionalen ctor-Parameter (`backfillSignal = null`).
- **Serialisierung vorhanden:** `IBackgroundProcessingGate` (`EnterOperationAsync`) wird im `ProgramScheduleWorker` bereits genutzt; `ProgramScheduleWorker` läuft als `AddHostedService` mit Scope-Factory, konfigurierbarem Intervall (`Setup.ProgramScheduleIntervalMinutes`) und `TimeProvider`.
- **Keine Sendeplan-Anzeige:** `TVSenderAdmin.razor` zeigt nur Sender-Einstellungen und Regeln — eine Sendeplan-Ansicht mit Status-Kennzeichnung („verdrängt") fehlt komplett (neue UI-Karte nötig).
- **Backup:** `ProgramSlots`/`ProgramRuleCursors` stehen bereits in `OptionalRestoreTables`; eine neue `Status`-Spalte braucht `OptionalRestoreColumns`-Eintrag `ProgramSlots.Status` + Alt-Backup-Regressionstest.

Test-Ausgangszustand: `dotnet test VideoWebPlayer.Tests/VideoWebPlayer.Tests.csproj` → **1850 bestanden, 0 dauerhaft fehlgeschlagen, 1 übersprungen**; einmaliger Playwright-Flake (`PlaylistPlaybackE2ETests.PlaylistSkipLockedEntriesE2ETest`, Timeout) bestand beim Einzelretry — Details: [tests.md](inventory/tests.md), [Log](inventory/test-results/baseline-run-1.log).

## Details

- [Datenmodell](inventory/models.md)
- [Logik / Services / Worker / Trigger](inventory/logic.md)
- [Enums](inventory/enums.md)
- [Interfaces](inventory/interfaces.md)
- [Tests](inventory/tests.md)
