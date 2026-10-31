# Abnahme Schritt 20 — Nahtloser Slot-Wechsel via EXT-X-DISCONTINUITY (N17)

Prüfer: Devin (unabhängige Abnahme, nicht der Implementierer)
Geprüfter Stand: `049b21e` (`feat: Nahtloser Sendeplatz-Wechsel im Live-Stream —
EXT-X-DISCONTINUITY via append_list (N17)`), Arbeitsbaum nach der Gegenprobe wieder
sauber (`git diff HEAD` leer; einzig untracked ist `VideoWebPlayer/Data/LiveStream/`
— produktive Live-Ausgabe des Anwenders vom 22.10., nicht vom Agenten erzeugt,
ungefährlich liegen gelassen).

## Ergebnis

**Status:** Erfüllt

Die N17-Anforderung ist im Code verifiziert umgesetzt und durch einen echten
ffmpeg-E2E-Test abgesichert, den ich selbst kaputtgemacht und als wirksam bewiesen
habe: (1) Der HLS-Muxer hängt an die bestehende `live.m3u8` an
(`append_list` in `FfmpegLiveStreamSegmenter.BuildArguments:394`) — Segmente der
Vorgänger-Generation bleiben im Fenster gelistet und die Media-Sequence läuft fort
statt zurückzuspringen; (2) die Generationsgrenze trägt `#EXT-X-DISCONTINUITY`
(`discont_start`, empirisch vom neuen E2E bewiesen) und `EXT-X-PROGRAM-DATE-TIME`
verankert die Wanduhr (`program_date_time`); (3) `liveTv.js` behandelt fatale
hls.js-Fehler jetzt differenziert nach Typ statt pauschal `startLoad()`; (4) der
vom Implementierer als Nebenfund gemeldete Sharing-Fix im `LiveStreamController`
ist korrekt und zwingend (s. u.).

### Verifiziert (Code-Review)

- **ffmpeg-Flags** (`FfmpegLiveStreamSegmenter.cs:394`): `-hls_flags
  delete_segments+independent_segments+append_list+discont_start+program_date_time`.
  `append_list` ist die tragende Änderung — der Muxer liest die vorhandene
  Playlist ein, führt `EXT-X-MEDIA-SEQUENCE` UND den `%05d`-Zähler fort und
  markiert den Übergang selbst. `discont_start` erzwingt die DISCONTINUITY am
  Generationswechsel (beim Allererstlauf ohne Alt-Playlist ein legales,
  kosmetisches Head-Tag — RFC 8216 erlaubt das). `program_date_time` gibt hls.js
  die Wanduhr-Anker für `liveSyncPosition`. Alle drei Flags sind uralt
  (ffmpeg ≥ 3.x); auf einem exotischen Build ohne sie würde ffmpeg am Start
  scheitern → `LastUnexpectedExit`-Diagnose (N15-Mechanismus), nichts Schlimmeres
  als der bisherige Zustand.
- **E2E beweist den echten Slot-Wechsel-Pfad** (`FfmpegLiveStreamSegmenterE2ETests.cs:74-139`):
  `SecondJob_PlaylistContinuesAndMarksDiscontinuity` startet Job 2 über
  `segmenter.StartAsync(Job(2, video2))` **während Job 1 läuft** — exakt der
  Produktionspfad (`StartAsync` → `_gate` → `StopCoreAsync` killt Altprozess →
  neuer ffmpeg im selben `SegmentDirectory`). Assertiert: `_1_`-URI bleibt
  gelistet (Alt-Generation nicht geresettet), `_2_` gelistet, eine
  `#EXT-X-DISCONTINUITY`-Zeile, deren nächste Media-URI `_2_` enthält (Grenze,
  nicht irgendein Marker), `#EXT-X-PROGRAM-DATE-TIME` vorhanden,
  `LastUnexpectedExit == null`. Zwei echte lavfi-Testvideos (440/880 Hz) mit
  echtem ffmpeg 9.0.2 — lief hier wirklich (kein Skip, ffmpeg auf PATH).
- **Sharing-Fix zwingend korrekt** (`LiveStreamController.cs:88-116`):
  `RewritePlaylistAsync` liest jetzt per `FileStream` mit
  `FileShare.ReadWrite | FileShare.Delete`. Das ist der einzige Playlist-Lesepfad
  im Server (grep belegt: `live.m3u8`/`PlaylistFileName`/`ReadAll*` kommt sonst
  nur in `LiveTv.razor` als URL-String und in `LocalMediaSourceReader` für
  fremde Zwecke vor). Der Fix ist nicht defensiv, sondern notwendig: ffmpegs
  HLS-Muxer schreibt die Playlist über tmp-Datei + Rename-over — gegen ein
  `FileShare.Read`-Handle des ausliefernden Requests scheitert der Rename unter
  Windows mit Access-Denied und der Muxer bricht ab. `ReadWrite|Delete` deckt
  beide Schreibstrategien ab (In-Place und Rename-over). Gleicher Share-Mode im
  Test-Poller (`ReadPlaylistShared:164-181`) — konsistent.
- **Token-Rewrite unberührt korrekt**: `DISCONTINUITY`/`PROGRAM-DATE-TIME`-Zeilen
  beginnen mit `#` → passieren den Rewriter unverändert (Zeile 105); nur
  Media-URIs bekommen `access_token`. `TrimEnd('\r')` nach `Split('\n')` —
  sauberer Ersatz für `ReadAllLines`.
- **liveTv.js Recovery** (`liveTv.js:48-69`): `MEDIA_ERROR` →
  `recoverMediaError()` (hls.js-Referenzmuster, u. a. für DISCONTINUITY-Restfälle),
  `NETWORK_ERROR` → `startLoad()`, sonstige → `destroy()` + `attachHls(state, …)`
  Neuaufbau im bisherigen 5-s-`scheduleRetry`-Takt. Vendor-hls.js ist 1.7.3 und
  enthält `ErrorTypes` + `recoverMediaError` (geprüft). Der Native-HLS-Pfad
  (Safari, Zeilen 114-126) ist komplett unangetastet — kein Bruch dort.
  `attachHls` im Rebuild-Zweig wiederverwendet `state` korrekt
  (WeakMap-Eintrag bleibt, `state.hls` zeigt auf neue Instanz, Detach-Wächter
  `states.get(videoEl) !== state` greift weiter).
- **Kill-Race sauber**: Beim Slot-Wechsel endet Job-1-stderr via Kill;
  `ReportProcessExit` trifft `!ReferenceEquals(_process, job1Process)` →
  Debug statt Fehl-`LastUnexpectedExit` — die Assertion `LastUnexpectedExit ==
  null` im E2E würde eine hier geloggte Falschwarnung fangen.
- **Unit-Spiegel**: `FfmpegLiveStreamSegmenterTests` assertet die drei neuen
  Flag-Strings im Argument (`:148-150`).
- **Doku konsistent**: `docs/help/tv-sender.md` beschreibt die fortgeführte
  Playlist + DISCONTINUITY statt „beginnt je Sendung sauber" (alter Text war
  faktisch die Bugbeschreibung); `docs/RELEASE_NOTES.md` EN+DE vollständig und
  deckungsgleich mit dem Code.

### Gegenprobe (selbst kaputtgemacht, Test rot, revertiert)

- `append_list+discont_start+program_date_time` aus den `-hls_flags` entfernt →
  `SecondJob_PlaylistContinuesAndMarksDiscontinuity` **schlägt fehl** an Zeile
  106 (`Assert.Contains(_1_)`): die produzierte Playlist zeigt
  `#EXT-X-MEDIA-SEQUENCE:0` und enthält ausschließlich `_2_`-Einträge — exakt das
  N17-Fehlerbild (Playlist-Reset, Alt-Generation weg, Sequenz auf 0). →
  `git checkout --` revertiert, Nachlauf auf restauriertem Stand: beide
  Segmenter-E2E grün (2/2, 16 s).

### Build & Tests (selbst ausgeführt, synchron, ffmpeg 9.0.2 auf PATH)

- `dotnet build VideoPlayer.sln -c Debug`: **0 Fehler**, 144 Warnungen —
  identischer Warnungsstand wie berichtet, alle Altlasten (BL0008/CS8602/…),
  keine neue Warnung in den geänderten Dateien.
- `dotnet build VideoPlayer.sln -c Release`: **0 Fehler**, 144 Warnungen.
- `dotnet format VideoPlayer.sln --verify-no-changes --no-restore --severity error`
  (CI-Spiegel): **sauber**, Exit 0.
- `dotnet test VideoWebPlayer.Tests/VideoWebPlayer.Tests.csproj` vollständig:
  **2148 gesamt — 2146 bestanden, 1 Fehler, 1 übersprungen** (4 m 01 s) —
  deckt sich exakt mit dem Implementierer-Bericht.
  - Der 1 Fehler: `PlaylistPlaybackE2ETests.PlaylistSkipLockedEntriesE2ETest`
    (Playwright-Timeout 30 s auf `#playlist-detail-selected-entry`) — fachlich
    unbeteiligt (Playlist-UI, kein Live-Stream), bekannter Last-Flake-Typus.
    **Einzel-Retry: grün (10 s).**
  - Der 1 Skip: `PairedDevice_ReportsProgressForMovieWithoutCollection_…` —
    der bekannte Alt-Skip, unverändert.

## Abweichungen

- [ ] Keine fachlichen Abweichungen festgestellt.

## Hinweise

- **E2E assertet Media-Sequence nicht explizit** (`#EXT-X-MEDIA-SEQUENCE > 0`
  nach dem Übergang). Faktisch abgedeckt: `_1_`-Erhalt + Grenz-DISCONTINUITY
  implizieren Fortführung, und die Gegenprobe zeigte im Fehlerbild `SEQUENCE:0`
  — ein Reset ohne Flag-Kombination würde am `_1_`-Assert scheitern. Eine
  explizite Sequence-Assert wäre margenale Schärfe, kein Mangel.
- **`.ts`-Segmente behalten `FileShare.Read`** (`PhysicalFile` im selben
  Controller): löscht ffmpeg (`delete_segments`) ein Segment, während ein Client
  es lädt, schlägt ffmpegs Delete fehl — für den Muxer eine unkritische Warnung
  (Leiche bis zum Fenster-Ende), kein Abbruch. Umgekehrt-Richtung
  (Leser blockiert ffmpeg-Schreiben) existiert bei `.ts` nicht — Dateien werden
  nie in-place überschrieben (eindeutige Namen). Hinnehmbar, aber der gleiche
  Sharing-Gedanke wäre der nächste logische Schritt.
- **Untracked `VideoWebPlayer/Data/LiveStream/`** mit produktiven Segmenten vom
  22.10. liegt im Repo-Wurzelbereich — nicht vom Agenten erzeugt, per AGENTS §8
  unangetastet. Die Alt-`live.m3u8` darin zeigt übrigens genau das N17-Bild vor
  dem Fix: ausschließlich `_7_`-URIs nach dem Wechsel, `#EXT-X-ENDLIST`. Ggf.
  lohnt sich ein `.gitignore`-Eintrag oder das Auslagern des Segment-Verzeichnisses
  aus dem Checkout — außerhalb dieses Schritts.
- **`steps.md` markierte Schritt 20 bereits „Fertig"** vor dieser Abnahme
  (handover.md sagt korrekt „Abnahme durch anderen Agenten ausstehend") —
  kosmetische Prozessfußnote, kein Code-Befund.
- **Handover-Selbstauskunft stichprobenartig verifiziert**: „2148 gesamt,
  2147 grün, 1 Alt-Skip" → hier 2146+1 Flake, Retry grün → effektiv identisch;
  „Mutationscheck schlägt fehl" → selbst reproduziert; „144 Warnungen
  unverändert" → bestätigt; „format clean" → bestätigt. Die behaupteten
  `docs/features/…`-Probendateien zum `append_list`-Verhalten auf ffmpeg 9.0.2
  sind untracked/lokal und hier nicht einsehbar — die Empirie ist aber durch den
  eigenen E2E-Lauf (beide Generationen + DISCONTINUITY + PDT auf ffmpeg 9.0.2)
  unabhängig belegt.
- **`GetFile`-Catch-Reihenfolge** (`LiveStreamController.cs:68-77`): der generische
  `Exception`-Filter mit `IOException or UnauthorizedAccessException` nach dem
  spezifischen `UnauthorizedAccessException`-Catch ist doppelt gemoppelt —
  Alt-Code, unverändert, kosmetisch.
