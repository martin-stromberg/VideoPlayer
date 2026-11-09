# Abnahme Schritt 26 — N23–N25 (Serienmischung, Upcoming-Refresh, Sender-Freischaltung)

Geprüft auf `task-issue-111-6145c51699f34953bf29a244634a23e2-automatisierter-tvsender-schritt-26-diverse`
(HEAD `907e28a`), diff gegen `task/issue-111-6145c51699f34953bf29a244634a23e2-automatisierter-tvsender`.
Prüfer unbeteiligt an der Implementierung; alle Behauptungen des Implementierer-Berichts wurden am Code
und per Sabotage-Gegenprobe verifiziert.

## Ergebnis

**Status:** Abweichungen gefunden (Umsetzung fachlich korrekt, Lücken in Testabdeckung/DoD
sowie ein beobachteter Laufzeitfehler im eigenen E2E-Log des Implementierers).

Verifiziert als korrekt:

- **N23** `ProgramScheduleService.StrandOrderKey` (`ProgramScheduleService.cs:939`): FNV-1a über
  `"{ruleId}:{strandKey}"`, prozess-invariant und deterministisch (kein `string.GetHashCode`),
  in `SetItems` (`:910`) als Strang-`OrderBy` verdrahtet; Tages-/Cursor-Stabilität bleibt
  (`EnsureCoverage_GenreRule_Interleaved_StrandPositionsStableAcrossDays` angepasst und grün).
  Gegenprobe: `StrandOrderKey` auf konstant `0` gesetzt →
  `StrandOrderKey_IsDeterministic_AndNotAlphabetic` schlägt fehl (wie erwartet).
- **N24** `LiveTv.razor`: Poll-Schleife startet erst in `OnAfterRenderAsync(firstRender)`
  auf der interaktiven Instanz (kein Prerender-Timer-Leak; `DisposeAsync` räumt
  `_cts`/`_timer`/`_pollTask` sauber auf). `now`/`upcoming` werden entkoppelt geholt — ein
  dauerhafter `now`-Fehler friert die Vorschau nicht mehr ein. `PeriodicTimer` läuft über den
  injizierten `TimeProvider` (`AddSingleton(TimeProvider.System)` in
  `ServiceCollectionExtensions.cs:271`); bUnit-Tests `PollTick_RefreshesUpcomingList` und
  `PollTick_NowFails_UpcomingStillRefreshes` über `ControllableTimeProvider` — grün.
- **N25 Serverseitig:** `Setup.TvSenderEnabled` (Default `false`, Migration
  `AddSetupTvSenderEnabled` `nullable: false, defaultValue: false`), `ProgramSettingsService.Get-/
  UpdateTvSenderEnabledAsync`, Admin-Toggle in `ProgramSettings.razor` inkl.
  `ScheduleSignal.NotifyScheduleChanged()` (wirkt ohne Neustart). `ProgramGuideController`
  (`now` + `upcoming`) und `LiveStreamController.GetFile` liefern **404** ohne Freigabe.
  Gegenprobe: Flag-Check neutralisiert → beide 404-Tests schlagen fehl (wie erwartet).
  `ProgramScheduleWorker`/`MediaDurationProbeWorker` idlen im Intervall-Takt,
  `LiveStreamWorker` stoppt einen laufenden `ILiveStreamSegmenter` idempotent und wacht per
  Signal sofort auf. Backup: Spalte in `OptionalRestoreColumns` +
  `OptionalRestoreBoolDefaults` (Default `false` — Alt-Backups stellen „deaktiviert" wieder
  her, konsistent zum Ab-Werk-Default).
- Testsuite (`dotnet test VideoWebPlayer.Tests/VideoWebPlayer.Tests.csproj -c Release`,
  synchron): **2189 erfolgreich, 1 übersprungen (Alt-Skip), 1 Fehler** =
  `PlaylistPlaybackE2ETests.PlaylistSkipLockedEntriesE2ETest` (Playwright-Timeout auf
  `#playlist-detail-selected-entry`, themenfremd) — bei Einzelwiederholung **grün** (Flake).
- Builds: Release `dotnet build VideoPlayer.sln -c Release` **0 Fehler**; Debug kompiliert
  **0 Fehler**, die `bin/Debug`-Ausgabe ist durch die laufende Benutzer-App (VideoWebPlayer
  PID 50736 + VS) gesperrt — wie vereinbart mit `-p:OutDir` in `D:\Repos_scratch` gebaut und
  danach aufgeräumt. Keine neuen Warnungen durch diesen Schritt (CS9113/CS8602 sind älter).

## Abweichungen

- [ ] **DoD §7 verletzt — kein Alt-Backup-Regressionstest für `Setups.TvSenderEnabled`.**
  Die Spalte steht zwar in `OptionalRestoreColumns`/`OptionalRestoreBoolDefaults`, aber es
  gibt **keinen** Test, der ein Backup ohne die Spalte wiederherstellt. Das etablierte Muster
  (`LegacyBackupArchiveBuilder.RemoveColumnsAsync`, siehe
  `VideoWebPlayerBackupDataTests_StrandKey.cs` / `..._Trailer.cs`) wurde für diese Spalte nicht
  angewendet (`grep TvSenderEnabled VideoWebPlayer.Tests/Services/Backups/` → kein Treffer).
- [ ] **Worker-Idle-Verhalten ohne jede Testabdeckung.** N25 verlangt explizit „Verhalten der
  Worker, wenn deaktiviert" — implementiert (s. o.), aber kein Test prüft, dass
  `ProgramScheduleWorker` ohne Flag nicht reconciled, `MediaDurationProbeWorker` nicht probt
  und `LiveStreamWorker` den Segmenter stoppt (`grep TvSenderEnabled` in den Worker-Tests:
  nur `= true`-Seeds, kein Disabled-Fall).
- [ ] **`ObjectDisposedException` in `NavMenu.OnInitializedAsync` — 5× „Unhandled exception
  in circuit" im eigenen E2E-Lauf** (`VideoWebPlayer.Tests/bin/Release/net10.0/Logs/
  web-20261023.log`, `NavMenu.razor:132` → `GetTvSenderEnabledAsync` → disposed
  `ApplicationDbContext`). Neu eingeführter EF-Zugriff *nach* awaits: reißt der Circuit
  während `RequestSourcesAsync` ab, ist der Scoped-Context bereits entsorgt und die Circuit-
  Exception landet unbehandelt im Log. Bisher einziges EF-Read im NavMenu — vorher konnte die
  Race nicht auftreten.
- [ ] **`EnsureCoverage_GenreRule_LeadStrand_VariesAcrossRules_NotAlphabetical` ist als
  Regressionsbeweis wirkungslos:** bei konstantem `StrandOrderKey` (Sabotage) blieb der Test
  **grün** (Leads `{A,B}` variieren schon alphabetisch, weil der erste Slot durch
  Vergangenheits-/Grenz-Trimming nicht zwingend der erste Strang-Pick ist). Der Hash-Mix wird
  faktisch nur durch den Unit-Test `StrandOrderKey_IsDeterministic_AndNotAlphabetic`
  abgesichert — der Integrationstest sollte präzisiert oder gestrichen werden.
- [ ] **Klärungsbedürftig:** N25 fordert „404/403" auch für `/admin/tv-sender` (und sinngemäß
  `/live-tv`); umgesetzt ist **200 + Hinweisseite** (`#tvsender-disabled`/`#live-tv-disabled`).
  Fachlich ist das Feature damit unerreichbar (keine Daten, keine Aktionen), die Wortlaut-
  Forderung 404/403 ist jedoch nicht erfüllt — bitte Anwender-Entscheid, ob die Hinweisseite
  als gleichwertig gilt.

## Hinweise

- `InternalMediaStreamController` (`/api/internal/media-stream/{token}`) ist **nicht**
  flag-gated. Vertretbar: Tokens sind kurzlebig + nicht ratbar und werden bei deaktiviertem
  Sender nicht mehr ausgestellt (Probe-/Segmenter-Worker idle); bereits ausgestellte Tokens
  streamen bis zum Ablauf weiter.
- `ApplicationDbContextModelSnapshot.cs:309-312` und die Designer-Datei enthalten
  vermurkste Einrückung (handeditiert statt regeneriert). `dotnet format --verify-no-changes`
  (CI-Gate) schlägt **nicht** fehl — die Datei trägt `// <auto-generated />` und wird vom
  Formatter übersprungen (mit Probe-Datei verifiziert); funktional ist das Modell korrekt.
- `ProgramSettingsService.UpdateTvSenderEnabledAsync` wird nirgends aufgerufen (Schreiben
  erfolgt ausschließlich über `UpdateGeneralSettingsAsync`) — ungenutzte öffentliche API.
- `ProgramGuideControllerTests` mutieren die Flag-Spalte im Test-Body — unbedenklich, da die
  Fixture pro Test frisch aufgebaut wird.
- Unstrukturierte, nicht zum Schritt gehörende untracked-Datei `VideoWebPlayer/Data/LiveStream/`
  liegt im Arbeitsverzeichnis (nicht von dieser Prüfung angelegt, unangetastet).

## Nachtrag — Abweichungen behoben

1. Alt-Backup-Regressionstest fuer Setups.TvSenderEnabled ergaenzt (RemoveColumnsAsync-Pfad).
2. Worker-Idle-Pfade getestet: ScheduleWorker (keine Slots), LiveStreamWorker (Segmenter stoppt), ProbeWorker (idles).
3. N23-Test pinnt die Hash-Strangreihenfolge konkret (StrandOrderKey-Permutation).
4. NavMenu: ObjectDisposedException bei Circuit-Teardown abgefangen.
5. Notice-Page statt 404 fuer /admin/tv-sender + /live-tv: als Design-Entscheidung akzeptiert
   (funktional unerreichbar + hilfreicher Hinweis statt nacktem Fehler).
