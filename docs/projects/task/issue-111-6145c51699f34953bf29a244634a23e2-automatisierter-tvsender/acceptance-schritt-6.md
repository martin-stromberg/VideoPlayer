# Abnahme Schritt 6: Live-Stream ausliefern

Prüfer: Devin (frischer Lauf, nicht der Implementierer). Branch:
`task-issue-111-6145c51699f34953bf29a244634a23e2-automatisierter-tvsender-schritt-6-live-stream`
(HEAD `2015b20`).

## Ergebnis

**Status:** Abweichungen gefunden

Fachlich ist der Schritt weitgehend vollständig und sauber umgesetzt (echte ffmpeg-Segmentierung
mit `-re -ss -t`, rollierendes HLS-Fenster mit `delete_segments`, Singleton-Prozess,
zeitbasierte Resynchronisierung ohne Prozesszustand, Umschaltung bei Slot-Wechsel/Drift/Lücke,
konfigurierbare Segmentlänge/Speicherort/Retention samt UI, authentifizierte Auslieferung mit
`access_token`-Rewrite in der Playlist, Traversal-Whitelist, Migration + optionale
Backup-Spalten + Alt-Backup-Regressionstest, ffmpeg-Lokalisierung analog ffprobe,
GPL/Lizenz-Hinweis dokumentiert). Zwei Befunde sprechen aber gegen „Erfüllt": acht neue
Analyzer-Warnungen, die den CI-Gate „static analysis" nachweislich brechen, und ein
nie ausgelesener stderr-Puffer des dauerhaft laufenden ffmpeg-Prozesses (latenter
Pipe-Deadlock in Produktion).

## Prüfung selbst ausgeführt

- `git diff <basis>...HEAD --stat` und Kern-Diffs aller neuen/geänderten Dateien gelesen
  (Segmenter, Coordinator, Worker, Controller, SegmentStore, Resolver, Locator, Setup-Spalten,
  Migration `AddSetupLiveStreamSettings`, `VideoWebPlayerBackupData`, DI-Registrierung,
  Admin-UI, Doku).
- `dotnet build VideoPlayer.sln -c Debug` → 0 Fehler; `-c Release` → 0 Fehler.
- `dotnet test VideoWebPlayer.Tests/VideoWebPlayer.Tests.csproj` (vollständig, synchron,
  ~4,2 min) → **1953 bestanden, 0 fehlgeschlagen, 1 übersprungen**; der Skip ist der bekannte
  Baseline-Skip `DevicePlaylistE2ETests_Playback.PairedDevice_ReportsProgressForMovieWithoutCollection_ContinueWatchingListStillLoads`. Kein Retry nötig.
- Gegenprobe durchgeführt: Token-Rewrite in `LiveStreamController.RewritePlaylistAsync`
  sabotiert (`access_token=` → `token=`) → `LiveStreamControllerTests.GetFile_M3u8_InjectsAccessTokenIntoSegmentUris`
  schlägt wie erwartet fehl. Anschließend per `git checkout --` revertiert, Arbeitsbaum sauber.
- CI-Gate verifiziert: `dotnet build … -p:TreatWarningsAsErrors=true` mit exakt der
  `WarningsNotAsErrors`-Liste aus `.github/workflows/pr-staging-ci.yml` (Zeile 111-112)
  → siehe Abweichung 1.

## Abweichungen

- [ ] **Neue xUnit1051-Warnungen brechen den CI-Gate „static analysis".**
  `VideoWebPlayer.Tests/Services/FfmpegLiveStreamSegmenterTests.cs` Zeilen 51, 74, 89, 100,
  101, 112, 113 und 127 rufen Methoden mit `CancellationToken` ohne
  `TestContext.Current.CancellationToken` auf. Der CI-Build mit
  `-p:TreatWarningsAsErrors=true` (WarningsNotAsErrors enthält xUnit1051 **nicht**) liefert
  dafür 8 Fehler — lokal mit den exakten CI-Flags reproduziert. Dieselbe Warnungskategorie
  musste bereits in Schritt 4 nachgebessert werden. Zusätzlich neu, aber von der
  CI-Ausnahmeliste abgedeckt: CS8602 in `LiveStreamE2ETests.cs:78` (nicht CI-brechend,
  trotzdem neu).
- [ ] **`FfmpegLiveStreamSegmenter` leitet stderr um, ohne ihn je zu lesen**
  (`FfmpegLiveStreamSegmenter.cs:139` `RedirectStandardError = true`; kein Reader).
  Zum Vergleich: `FfprobeMediaDurationProbe.cs:164` drained stderr explizit
  (`ReadToEndAsync`). Bei einem Prozess, der stundenlang läuft (`-loglevel warning` kann bei
  realen Quellen sehr gesprächig sein — DTS-/Mux-Warnungen), läuft der Pipe-Puffer voll und
  ffmpeg blockiert beim Schreiben → Segmentierung steht, bis der Drift-Neustart (>30 s)
  greift; je nach Eingabe wiederkehrend. Fix wäre trivial: stderr asynchron drainen (z. B.
  letzte Zeilen ins Log) oder Umleitung entfernen.

## Hinweise

- **Pipe-Eingang + `-ss` (Remote-Quellen):** Bei `pipe:0` ist kein Seek möglich; ffmpeg liest
  und verwirft den Eingabestrom bis zur Zielposition — gedrosselt durch `-re` also in Echtzeit.
  Ein (Neu-)Start mitten im Slot über eine SFTP-Quelle liefert daher ~`Position` Sekunden lang
  keine Segmente. Die Dokumentation nennt bisher nur die moov-Einschränkung
  (`docs/help/tv-sender.md`); dieses Aufwärmlag-Verhalten ist eine weitere dokumentationswürdige
  Einschränkung. `LocalDirectory`-Quellen (Dateipfad, echter Seek) sind nicht betroffen.
- **Ausgelieferte Dateien nicht auf Stream-Artefakte beschränkt:** `TryResolveFileAsync`
  akzeptiert jeden Dateinamen der sicheren Whitelist — nicht nur `seg_*.ts`/`*.m3u8`. Setzt ein
  Administrator das Segment-Verzeichnis auf einen Pfad mit anderen Dateien, sind diese für
  jeden angemeldeten Nutzer über den Endpunkt lesbar. Erfordert Admin-Fehlkonfiguration;
  härten ließe es sich über eine engere Whitelist (`seg_*.ts`, `*.m3u8`).
- **Bereits auf dem Basisbranch vorhanden (nicht aus Schritt 6):**
  `ApplicationDbContext.cs:27` enthält `&uuml;` (Commit fa0bebe) → CS1570 wird unter
  `TreatWarningsAsErrors` zum Fehler und bricht den CI-Gate unabhängig von diesem Schritt;
  ist nicht in `WarningsNotAsErrors` gelistet. Separat zu beheben/prüfen.
- `LiveStreamController.GetFile` hat eine redundante Catch-Klausel
  (`UnauthorizedAccessException` in Zeile 73ff. nach bereits behandeltem Catch) — vom
  Implementierer dokumentiert und bewusst belassen; unkritisch.
- Singleton-Garantie verifiziert: `ILiveStreamSegmenter` ist singleton, `StartAsync` stoppt
  den Vorgängerprozess unter `SemaphoreSlim`, `Kill(entireProcessTree)` vorhanden. Abgestürzter
  ffmpeg (`HasExited`) wird beim nächsten Tick neu gestartet (Poll-Cap 30 s).
- `LiveStreamWorker` läuft nur außerhalb der Testing-Umgebung (kein ffmpeg in
  E2E-/WebApplicationFactory-Hosts) und leert das Segment-Verzeichnis einmalig beim Start —
  konsistent mit der zeitbasierten Resynchronisierung ohne Prozesszustand.
- `steps.md` zeigt Schritt 6 noch „In Arbeit" / Bearbeiter-Tabelle „pausiert" — Statuspflege
  beim Abschluss beachten.
