# Abnahme Schritt 11 — Fortschrittsanzeige Laufzeitermittlung (N6)

Prüfer: Devin (unabhängige Abnahme, nicht der Implementierer)
Geprüfter Stand: `b075be8` (`feat: Fortschrittsanzeige Laufzeitermittlung …`), Arbeitsbaum sauber.

## Ergebnis

**Status:** Abweichungen gefunden

Die fachliche Anforderung ist vollständig und korrekt umgesetzt und durch Tests abgesichert.
Eine technische Abweichung (DbContext-Concurrency im Auto-Refresh, s. u.) sollte vor dem
Merge behoben werden — der implementierende Review verweist dafür auf `LiveTv.razor` als
Präzedenz, dieser Vergleich trifft aber nicht zu (Details unten).

### Verifiziert

- **Admin-only, serverseitig:** `MediaDurationProbeService.GetProgressAsync` ruft zuerst
  `RequireAdminAsync` (`MediaDurationProbeService.cs:141`, `:179-186`) — `IsAdmin` aus der DB,
  `UnauthorizedAccessException` bei unbekannter ID, `ForbiddenAccessException` bei Nicht-Admin.
  Die UI prüft zusätzlich das `IsAdmin`-Claim (`TVSenderAdmin.razor:489-491`). Gegenprobe:
  Admin-Gate aufgeweicht → `GetProgressAsync_NonAdmin_Throws` schlägt sofort fehl. Revertiert.
- **Zähler konsistent mit Worker-Teilmenge:** `GetProgressAsync` nutzt dieselbe
  `VideoExtensionPredicate()` wie `ProbePendingAsync` (`.mp4/.avi/.mkv/.mpeg` auf `Name`).
  `Failed = Duration == null && DurationProbeAttempts >= EffectiveMaxAttempts` spiegelt
  `ApplyOutcome` (`:261-281`); `Pending = Total − Probed − Failed` schließt Items im
  Retry-Delay korrekt als ausstehend ein. Gegenprobe: `>=` → `>` →
  `GetProgressAsync_CountsStatesSeparately` schlägt fehl (pending 3 statt 2). Revertiert.
- **„hängt"-Erkennung plausibel:** `ProbeStatusLabel` (`TVSenderAdmin.razor:558-570`) —
  Pending>0 + `ProbedLastHour`>0 → „läuft"; Pending>0 + keine Aktivität >1 h → „hängt
  möglicherweise (ffprobe verfügbar?)"; Pending=0 → „abgeschlossen/ermittelt".
  `DurationProbedAt` wird bei jedem Versuch (auch Fehlversuch) aktualisiert, sodass die
  Stundenrate echte Aktivität misst; Worker-Intervall (60 s) ≪ 1 h Fenster.
- **Rolling-Liste:** `DurationProbedAt != null`, DESC, `Take(recentCount)`, Name/Quelle/
  Zeitpunkt/Ergebnis — Anforderung erfüllt (`MediaDurationProbeService.cs:162-172`).
- **Auto-Refresh ohne Task-/Timer-Leak:** `PeriodicTimer` im `using`, `refreshCts.Cancel()`
  in `Dispose` (`TVSenderAdmin.razor:526-553`, `:717-721`), `disposed`-Guard vor
  `StateHasChanged`, Einzelfehler pro Tick abgefangen.
- **DI:** `MediaDurationProbeService` scoped registriert (`ServiceCollectionExtensions.cs:242`).
- **Tests:** 5 neue Service-Tests gegen echtes (In-Memory-)SQLite; E2E-Erweiterung ist in
  `Admin_CreateEditDeactivateDeleteRule` konsolidiert (kein eigener App-Start) — Seeds sind
  deterministisch stabil (beide Items nicht ausstehend; Episoden sind `TVShowEpisode`, keine
  `MediaItem`s; `attempts=10 ≥ EffectiveMaxAttempts=10`).
- **Suite:** `dotnet test VideoWebPlayer.Tests/VideoWebPlayer.Tests.csproj -c Release`,
  synchron: **2012 grün, 1 Skip (bekannter Alt-Skip
  `PairedDevice_ReportsProgressForMovieWithoutCollection_…`), 1 FAIL** =
  `PlaylistSkipLockedEntriesE2ETest` (Playwright-Timeout in `SelectEntryAsync`, ohne Bezug
  zur Änderung — identisch zum Implementierer-Lauf). **Einzelwiederholung: grün (20 s).**
- **Builds:** Release `dotnet build VideoPlayer.sln -c Release`: 0 Fehler, 142 Warnungen
  (= Bestand, keine neue Warnung in geänderten Dateien). Debug ist durch laufende
  Benutzer-Instanz `VideoWebPlayer.exe` (PID 55720) gesperrt — **nicht beendet**;
  Debug-Kompilierung über `-p:OutDir=<scratch>` vollständig verifiziert: 0 Fehler.
- **Doku:** `docs/help/tv-sender.md`, `docs/help/videolaufzeit.md`, `docs/RELEASE_NOTES.md`
  (EN+DE) und `handover.md` aktualisiert; keine Migration nötig (keine neue Persistenz).

## Abweichungen

- [ ] **DbContext-Concurrency im Auto-Refresh-Tick** (`TVSenderAdmin.razor:526-553`):
  `ProbeProgressLoopAsync` führt `LoadProbeProgressAsync` — d. h. EF-Core-Queries — auf einem
  Threadpool-Thread aus, **nicht** über `InvokeAsync` auf dem Blazor-Dispatcher. Der von
  `ProbeService` verwendete `ApplicationDbContext` ist dieselbe scoped Instanz wie das
  injizierte `DbContext`-Feld der Seite (`:10`, benutzt u. a. `:503`) und die Contexts in
  `ProgramRuleService`/`ProgramScheduleService`/`ProgramSettingsService`. Ein 15-s-Tick
  (5 Queries) kann daher mit einer Nutzeraktion (Regel speichern, Diagnosen laden, Sender-
  Einstellungen) kollidieren → `InvalidOperationException` („A second operation started on
  this context instance"). Trifft es den Tick, wird er geschluckt (veraltete Anzeige, harmlos);
  trifft es die Nutzeraktion, scheitert diese sporadisch und schwer diagnostizierbar.
  Der Implementierer-Review begründet das mit „gleichem Risikoprofil wie `LiveTv.razor`" —
  **das trifft nicht zu**: `LiveTv.razor` ruft im Tick nur `ProgramGuideClient` (HTTP-Client)
  auf und berührt den scoped `DbContext` nie (`LiveTv.razor:188-229`). Das korrekte
  etablierte Muster ist `Backups.razor:517-549`: dort steht die Datenbeschaffung **innerhalb**
  `await InvokeAsync(async () => { … })`, was Tick-Arbeit mit UI-Handlern serialisiert.
  Vorschlag: `await InvokeAsync(async () => { await LoadProbeProgressAsync(ct); StateHasChanged(); })`
  in der Schleife; der `disposed`-Check kann entfallen bzw. bleiben.

## Hinweise

- Kleinigkeit, kein Befund: `refreshCts` wird in `Dispose` nur gecancelt, nicht disposed —
  vom Implementierer begründet (ObjectDisposed-Race mit in-flight `WaitForNextTickAsync`),
  nachvollziehbar.
- `ProbedLastHour` zählt Versuche (inkl. Fehlversuche) — fachlich richtig für „Aktivität".
- Edge-Case ohne Befundwert: `RetryDelaySeconds` ≫ 1 h konfiguriert → Karte meldet „hängt",
  obwohl der Worker nur wartet; Text sagt „möglicherweise" — akzeptabel.
- Wiederkehrender E2E-Flake `PlaylistSkipLockedEntriesE2ETest` (Timeout beim Eintrags-
  Dialog) trat im Voll-Lauf bereits beim Implementierer auf; Einzelretry grün. Kein Bezug
  zu diesem Schritt, aber wiederholtes Flackern — Beobachtung für spätere Stabilisierung.
- Benutzer-Instanz `VideoWebPlayer.exe` (PID 55720) sperrt weiterhin `bin/Debug`; normaler
  Debug-Build und Debug-Testlauf erst nach dem Schließen dieser Instanz möglich. Hier
  verifiziert über Release-Build + Release-Suite + Scratch-OutDir-Debug-Compile.
