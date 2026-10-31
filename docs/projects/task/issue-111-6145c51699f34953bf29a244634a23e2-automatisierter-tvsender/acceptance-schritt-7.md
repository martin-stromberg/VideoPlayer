# Abnahme Schritt 7: Live-TV in der Benutzeroberfläche

Prüfer: Devin (unabhängig vom Implementierer, frische Prüfung gegen den Code)
Geprüfter Stand: `63d46a8 feat: Live-TV in der Benutzeroberflaeche (Schritt 7 TV-Sender)` auf
`task-issue-111-6145c51699f34953bf29a244634a23e2-automatisierter-tvsender-schritt-7-live-tv-ui`.

## Ergebnis

**Status:** Abweichungen gefunden

Die fachliche Kernanforderung (`project-plan.md`, „### Schritt 7") ist umgesetzt: Live-Ansicht
`/live-tv` mit Player (natives HLS per `canPlayType` zuerst, hls.js nur als MSE-Fallback mit
Lazy-Load), „Jetzt läuft"/„Gleich" plus begrenzter Vorschau (6 h clientseitig, Server-Cap 12 h),
Nav-Eintrag im `<Authorized>`-Block, verständliche Fehler-/Leerzustände statt technischer
Fehlerbilder, Zugriff serverseitig erzwungen (`[BearerTokenCheck]` + `CheckLoggedIn`, zusätzlich
Login-Formular statt Fehler für Anonyme). hls.js 1.7.3 vendored mit Apache-2.0-Lizenztext;
kein Eingriff in Build-/CI-Pipeline nötig (`wwwroot` wird publiziert). `access_token` wird an
`live.m3u8` angehängt und vom Server in jede Segment-URI eingeschrieben
(`LiveStreamController.RewritePlaylistAsync`). Dispose räumt `PeriodicTimer`,
`CancellationTokenSource`, Poll-Task, JS-Attach und `DotNetObjectReference` sauber auf.

Gefundene Abweichungen sind zwei sichtbare/kleine Verhaltensdefizite (Nav-Icon, Auto-Retry auf
dem nativen Pfad) sowie Hygiene-Mängel in den neuen Testdateien — siehe unten.

## Verifizierung (selbst ausgeführt)

- `git diff <projekt-branch>...HEAD --stat` + Kern-Diffs gelesen: `LiveTv.razor` (299 Z.),
  `liveTv.js` (137 Z.), `IProgramGuideApiClient` + `VideoWebPlayerClient.ProgramGuide.cs`,
  `NavMenu.razor`, `App.razor`, `ServiceCollectionExtensions`, `LiveTvErrorMessages`, Vendoring.
- `dotnet build VideoPlayer.sln -c Debug`: **0 Fehler** (147 Warnungen, siehe Hinweise).
- `dotnet build VideoPlayer.sln -c Release`: **0 Fehler**.
- `dotnet test VideoWebPlayer.Tests/VideoWebPlayer.Tests.csproj` (vollständig, synchron, nach
  erfolgreichem Build): **1964/1966 grün, 1 Skip** (bekannter Alt-Skip
  `DevicePlaylistE2ETests_Playback...ContinueWatchingListStillLoads`), **1 Fehler**:
  `PlaylistPlaybackE2ETests.PlaylistSkipLockedEntriesE2ETest` (Playwright-Timeout auf
  `#playlist-detail-selected-entry`, nicht schrittbezogen). Einzelwiederholung: **grün** —
  klassischer Flake, im Bericht ehrlich genannt.
- **Gegenprobe:** `?access_token=` → `?token=` in `LiveTv.razor` (`StreamUrl`) manipuliert →
  `LiveTvTests.Now_LoadsAndRendersCurrentNextAndUpcoming_AndAttachesStream` schlägt wie
  erwartet fehl (Assertion auf `access_token=test-token` im `liveTv.attach`-Aufruf).
  `git checkout --` — Arbeitsbaum wieder sauber.
- `dotnet format VideoPlayer.sln --verify-no-changes --no-restore --severity error`: schlägt
  fehl, aber **nur in Dateien außerhalb des Schritt-7-Diffs** (ältere Commits; siehe Hinweise).

## Abweichungen

- [ ] **Nav-Icon „Live-TV" rendert als ausgefülltes Quadrat.** `NavMenu.razor:42` nutzt
  `bi bi-tv`, aber `NavMenu.razor.css` definiert für `.bi-tv` **keine** `mask-image`-Maske —
  die `.bi`-Basis (dort Zeilen ~90–101) setzt `background-color: currentColor`, sodass jedes
  Icon ohne eigene Maske als gefülltes Quadrat erscheint. Genau dieser Fehlermodus ist in der
  Datei selbst dokumentiert (Kommentar Zeile 115–116 zum früheren `bi-list-ul`-Problem). Es
  fehlt eine `.bi-tv`-Regel mit TV-SVG-Maske in `NavMenu.razor.css`. Sichtbarer kosmetischer
  Defekt direkt im Navigationspunkt des Features.
- [ ] **Kein Auto-Retry auf dem nativen HLS-Pfad, obwohl die Meldung ihn verspricht.** In
  `liveTv.js` (Zeilen 100–106) meldet der `error`-Handler des nativen Pfads nur
  `NotifyStreamError` — ein `scheduleRetry`/erneutes `src`-Setzen gibt es nur im hls.js-Pfad
  (Zeilen 48–55). `LiveTvErrorMessages.StreamUnavailable` sagt „Es wird automatisch erneut
  versucht", und der Umsetzungsplan sah explizit „natives erneutes `src`-Setzen" vor. Auf
  Browsern mit nativem HLS (Safari) bleibt der Hinweis nach einem Ladefehler (z. B. 404 in der
  Anlaufphase des Segmenters) dauerhaft stehen, bis der nächste Slot-Wechsel ein Re-Attach
  auslöst. hls.js-Pfad (Chrome/Firefox/Edge) retryt korrekt alle 5 s.
- [ ] **Neue Analyzer-Warnungen im Schritt-7-Testcode** (Build-Log):
  `LiveTvTests.cs:20` CS9113 (Ctor-Parameter `output` ungelesen — Parameter ohne Nutzung),
  `LiveTvTests.cs:96` xUnit2031 (`Where` vor `Assert.Single`), `LiveTvTests.cs:120` xUnit2029
  (`Assert.Empty` statt `Assert.DoesNotContain`), `LiveTvE2ETests.cs:124/127/132` xUnit1051
  (`CancellationToken` nicht durchgereicht). Schritt 6 hatte xUnit1051/CS-Warnungen in
  Abnahmefixes explizit beseitigt; die neuen Tests führen dieselbe Kategorie wieder ein.
  Keine Build-Fehler, aber inkonsistente Hygiene.
- [ ] **Leeres Icon-Markup in der Sendelücken-Box.** `LiveTv.razor:60` setzt
  `<span class="bi bi-tv">`; `LiveTv.razor.css:50` ergänzt nur `font-size`. Außerhalb des
  NavMenu existiert kein `.bi`-Basisklassen-/Masken-Styling — das Span rendert unsichtbar
  (leer). Entweder Maske definieren oder Markup entfernen. Kosmetisch.

## Hinweise

- **Vorbelastung Format-Check (nicht Schritt 7):** `dotnet format --verify-no-changes` meldet
  WHITESPACE-Fehler in `VideoWebPlayer/Data/ApplicationDbContext.PlaylistBackfillMarking.cs`
  (Z. 94, 142) und `VideoWebPlayer.Tests/LiveStreamCoordinatorTests.cs` (Z. 106–107) — aus
  älteren Commits (Schritt 4/6 bzw. Backfill-Commit `b625d05`). Beim Merge des Projekt-Branches
  wird der PR-CI-Formatcheck (`pr-staging-ci.yml:88`) darauf anspringen; Behebung gehört nicht
  in diesen Schritt, sollte aber vor dem Gesamt-Merge erledigt werden.
- **E2E-Recovery-Nachweis plausibel begründet:** „Wiederanlauf bei Slot-Beginn ohne Reload" ist
  nicht browserseitig testbar, weil `ReconcileAsync` regellose Test-Slots als `Displaced`
  verwirft — im Code (`LiveTvE2ETests.cs:150-154`) und Review dokumentiert. bUnit deckt die
  Zustandsübergänge ab; nachvollziehbar.
- **`upcoming` enthält die laufende Sendung als erste Zeile** (API-Semantik aus Schritt 5):
  unter „Die nächsten Stunden" erscheint die gerade laufende Sendung erneut. Kein
  Anforderungsbruch, nur redundante Darstellung.
- **Token-Lebensdauer:** `StreamUrl` wird einmalig beim Attach mit dem dann aktuellen
  `Client.AuthorizationToken` gebaut; hls.js-Retries und der 5-s-Re-Attach nutzen dieselbe URL.
  Läuft der Bearer-Token während einer sehr langen Sitzung ab, bleiben Stream-Requests bei 401
  hängen (Re-Attach erst beim nächsten Slot-Wechsel). Gleiches Muster wie bei der bestehenden
  Film-Wiedergabe (`access_token`-Query) — als bekannte Grenze des App-Musters akzeptabel.
- **`_hlsScriptPromise` bleibt nach Ladefehler rejected:** scheitert das Nachladen von
  `hls.min.js` einmal (transient), schlagen spätere `attach`-Aufrufe sofort fehl, ohne das
  Skript erneut zu versuchen (die Fehler-Meldung + JS-Retry greift nur bei
  Manifest-/Medienfehlern). Kleiner Edge-Case.
- **bUnit verifiziert kein JS:** `liveTv.js`-Logik (nativ/hls.js-Verzweigung, Retry, detach)
  ist nicht durch Unit-Tests abgesichert; der Playwright-E2E belegt den hls.js-Pfad
  (Chromium hat kein natives HLS) inkl. `access_token`-Request. Der native `canPlayType`-Zweig
  bleibt uninstrumentiert — angesichts der einfachen Verzweigung vertretbar, aber mit dem oben
  genannten Retry-Defizit genau auf diesem Pfad relevant.
- **Flake-Protokoll:** `PlaylistSkipLockedEntriesE2ETest` 1× Timeout unter Volllast,
  Einzelretry grün; `TVSenderAdminE2ETests`-Flake aus dem Handover trat in diesem Lauf nicht
  auf.
- 12 neue Tests (bUnit 7 + NavMenu 1 + Client 2 + Playwright 2) decken Attach/Token,
  Sendelücke, Login, Fehlerzustände, Vorschau und Nav-Eintrag ab; Gegenprobe belegt, dass der
  Token-Test real greift.
