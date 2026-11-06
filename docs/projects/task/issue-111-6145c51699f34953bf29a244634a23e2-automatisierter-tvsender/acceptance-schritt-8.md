# Abnahme Schritt 8: Laufzeit-Ermittlung robust für Remote-Quellen + Retry (N4)

Prüfer: Devin (unabhängig vom Implementierer, frische Prüfung gegen den Code)
Geprüfter Stand: `39c943b feat: Laufzeitermittlung robust für Remote-Quellen — Retry,
Stream-Timeout, Parallelität (N4, Schritt 8)` auf
`task-issue-111-6145c51699f34953bf29a244634a23e2-automatisierter-tvsender-schritt-8-probe-robust`.

## Ergebnis

**Status:** Erfüllt

Die Anforderung aus `nachbesserungen.md` (N4) ist vollständig und korrekt umgesetzt:

- **Getrenntes, konfigurierbares Timeout:** `FfprobeMediaDurationProbe.ProbeStreamAsync`
  (pipe:0/SFTP) nutzt `MediaProbeOptions.EffectiveStreamTimeoutSeconds` (Default 600 s),
  `ProbeFileAsync` (lokale Pfade) weiter `EffectiveTimeoutSeconds` (Default 120 s)
  (`FfprobeMediaDurationProbe.cs:56-61`). Beide Schlüssel plus `RetryDelaySeconds` (300 s),
  `MaxAttempts` (10), `ParallelProbes` (4) in `appsettings.json` und
  `docs/help/videolaufzeit.md` dokumentiert.
- **Retry mit Verzögerung statt Dauer-Brandmarkung:** neue Spalte
  `MediaItem.DurationProbeAttempts` (Migration `AddMediaItemDurationProbeAttempts`,
  Default 0); Selektion in `MediaDurationProbeService.cs:96-98`:
  `Duration == null && (DurationProbedAt == null || (Attempts < MaxAttempts &&
  DurationProbedAt <= jetzt - RetryDelay))`. Transiente Fehler inkrementieren den Zähler,
  Erfolg setzt ihn zurück, permanente Fehler setzen ihn auf `MaxAttempts`
  (`ApplyOutcome`, Zeilen 201-221).
- **Altbestand wird wieder aufgegriffen:** nach der Migration stehen Alt-Faileds bei
  `Attempts = 0 < 10` und werden nach Ablauf der Retry-Frist erneut geprobt — durch Test
  `ProbePendingAsync_LegacyFailedItem_IsPickedUpAgain` abgesichert und im Code
  nachvollzogen. `MediaSourceScanner` resettet den Zähler bei geändertem `CreatedAt`
  mit (Zeile ~276).
- **Klassifikation transient/permanent korrekt:** Timeout, Prozessstart-Fehler,
  `ExitCode != 0`, Ausnahmen und `null`-Stream ⇒ transient; `ExitCode == 0` ohne
  parsebare Dauer ⇒ permanent (`FfprobeMediaDurationProbe.cs:93-116`,
  `MediaDurationProbeService.cs:178-188`). stderr wird für Diagnose-Logs erfasst
  (200 Zeichen gekürzt).
- **Durchsatz:** `ParallelProbes` über `SemaphoreSlim` + `Task.WhenAll`
  (`RunProbesAsync`, Zeilen 133-152); der Worker-Loop (bereits vorhanden) zieht Batches
  ohne Intervall-Pause nach, bis nichts mehr ansteht.
- **Planbarkeit:** `IProgramScheduleSignal` (Singleton, DI-auflösbar) wird injiziert und
  `NotifyScheduleChanged()` feuert, sobald mindestens eine Duration geschrieben wurde
  (Zeilen 106-123) — fertig geprobte Items werden ohne Intervall-Wartezeit wieder
  planbar.
- **Parallelität sicher:** in den Probe-Tasks läuft nur ffprobe/Stream — kein DbContext;
  Ergebnisse werden seriell je Item via `SaveChangesAsync` angewandt.
  `SftpMediaSourceReader.OpenFileStream` baut pro Aufruf einen eigenen `SftpClient`
  (Zeilen 277-301), `FfprobeMediaDurationProbe` ist pro Aufruf zustandslos.
- **Backup/Migration:** `DurationProbeAttempts` steht in `OptionalRestoreColumns` und
  `OptionalRestoreIntDefaults` (Default 0); Regressionstest
  `ReadFromAsync_LegacyBackupWithoutDurationProbeAttemptsColumn_RestoresSuccessfully`
  stellt ein Alt-Backup ohne die Spalte wieder her und verifiziert Default 0.
- **Doku:** `docs/help/videolaufzeit.md` (Retry-Semantik, neue Schlüssel-Tabelle) und
  `docs/RELEASE_NOTES.md` aktualisiert.

## Verifizierung (selbst ausgeführt)

- `git diff <projekt-branch>...HEAD --stat` + Kern-Diffs gelesen: `MediaProbeOptions`,
  `MediaItem`, `FfprobeMediaDurationProbe`, `IMediaDurationProbe`,
  `MediaDurationProbeResult` (neu), `MediaDurationProbeService`, `MediaSourceScanner`,
  `VideoWebPlayerBackupData`, Migration + Snapshot, `appsettings.json`, Handover.
- `dotnet build VideoPlayer.sln -c Debug`: **0 Fehler** (142 Warnungen, alle
  vorbestehend — die einzige in einer Schritt-Datei, `VideoWebPlayerBackupData.cs:647`
  CS8601, liegt in einer von diesem Schritt unberührten Methode; Diff-Hunks nur ~Z.79/127).
- `dotnet build VideoPlayer.sln -c Release`: **0 Fehler**.
- `dotnet test VideoWebPlayer.Tests/VideoWebPlayer.Tests.csproj` (vollständig, synchron,
  nach erfolgreichem Build): **1975/1976 grün, 0 Fehler, 1 Skip** — der bekannte
  Alt-Skip `DevicePlaylistE2ETests_Playback.PairedDevice_...ContinueWatchingListStillLoads`.
- **Gegenprobe 1 (Retry-Selektion):** `DurationProbeAttempts < maxAttempts` aus der
  Where-Klausel entfernt → `ProbePendingAsync_MaxAttemptsReached_NotRetried` schlägt
  fehl (erwartet 0, tatsächlich 1). `git checkout --` — sauber.
- **Gegenprobe 2 (Klassifikation):** Timeout-Pfad auf `PermanentFailure` umgestellt →
  `ProbeFileAsync_TimedOut_ReturnsTransientFailure` schlägt fehl. `git checkout --` —
  sauber.
- `dotnet format VideoPlayer.sln --verify-no-changes --no-restore --severity error`:
  **sauber** (Exit 0, keine Ausgabe).
- DI-Verdrahtung geprüft: `AddScoped<MediaDurationProbeService>`,
  `IProgramScheduleSignal` als Singleton registriert → optionale Ctor-Parameter werden
  vom Container befüllt (`ServiceCollectionExtensions.cs:242,253-254`).

## Abweichungen

- [x] Keine.

## Hinweise

- `ExitCode != 0` wird bewusst als transient klassifiziert (Stream-Abriss vs. korrupte
  Datei nicht unterscheidbar); eine dauerhaft korrupte Datei kostet maximal
  `MaxAttempts` Versuche à bis zu `StreamTimeoutSeconds` — mit Defaults bis zu ~100 min
  Probe-Zeit über den Zähler verteilt, akzeptabel und im Handover dokumentiert.
- Permanente Fehler sind als `Attempts >= MaxAttempts` codiert: eine spätere Erhöhung
  von `MediaProbe:MaxAttempts` reaktiviert sie nach der Retry-Frist — vom Implementierer
  als bewusste, harmlose Semantik deklariert; plausibel.
- `DurationProbedAt` wird nach `RunProbesAsync` auf das Batch-Ende gesetzt (nicht pro
  Item auf den Versuchsbeginn) — verlängert die Retry-Frist minimal, konservativ.
- Bei Cancellation mitten im Batch verwirft `Task.WhenAll` die bereits ermittelten
  Ergebnisse komplett (kein Teilspeichern) — die Items bleiben unprobed und werden
  nächste Runde erneut behandelt; idempotent, kein State-Verlust.
- `ProbePendingAsync` gibt `items.Count` zurück; bei OCE propagiert die Ausnahme zum
  Worker-Catch — Rückgabewert-Semantik unverändert und konsistent.
- DoD erfüllt: neue Spalte in `OptionalRestoreColumns`/`OptionalRestoreIntDefaults`
  samt Alt-Backup-Regressionstest, EF-Migration + Designer/Snapshot konsistent,
  Hilfe/Release Notes aktualisiert, Debug+Release fehlerfrei, volle Suite grün.
