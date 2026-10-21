# Umsetzungsplan – Robustheit der Laufzeitermittlung (N4)

## Ziel

ffprobe-Probes für Remote-Quellen (`pipe:0`) bekommen ein eigenes, größeres Timeout; Probe-Fehlschläge werden in transient (wiederholbar mit Zähler + Wartezeit) und permanent (kein Retry) klassifiziert; Probes laufen konfigurierbar parallel; neu ermittelte Laufzeiten wecken die Sendeplanung sofort.

## Umsetzungsschritte

### 1. `MediaProbeOptions` erweitern (`VideoWebPlayer/Configuration/MediaProbeOptions.cs`)

Neue Eigenschaften + `Effective*`-Getter (Muster wie Bestandscode, Fallback bei `<= 0`):

- `StreamTimeoutSeconds` = 600 → `EffectiveStreamTimeoutSeconds`
- `RetryDelaySeconds` = 300 → `EffectiveRetryDelaySeconds`
- `MaxAttempts` = 10 → `EffectiveMaxAttempts`
- `ParallelProbes` = 4 → `EffectiveParallelProbes`

### 2. `appsettings.json`

Abschnitt `MediaProbe` um `StreamTimeoutSeconds: 600`, `RetryDelaySeconds: 300`, `MaxAttempts: 10`, `ParallelProbes: 4` ergänzen.

### 3. `MediaItem.DurationProbeAttempts` + EF-Migration

- `VideoWebPlayer/Data/MediaItem.cs`: `public int DurationProbeAttempts { get; set; }`; XML-Doku von `DurationProbedAt`/`Duration`/`DurationProbeAttempts` auf die neue Semantik anpassen (`DurationProbedAt` = letzter Versuch; `Duration == null && DurationProbeAttempts >= MaxAttempts` = dauerhaft fehlgeschlagen).
- Migration `AddMediaItemDurationProbeAttempts` erzeugen (`dotnet ef migrations add` im Projekt `VideoWebPlayer`, Spalte `int NOT NULL DEFAULT 0`).
- `VideoWebPlayer/Services/Backups/VideoWebPlayerBackupData.cs`: `OptionalRestoreColumns` += `MediaItems.DurationProbeAttempts`; `OptionalRestoreIntDefaults` += (`MediaItems`, `DurationProbeAttempts`, 0).

### 4. Ergebnistyp + Interface (`IMediaDurationProbe`)

Neue Datei `VideoWebPlayer/Services/MediaDurationProbeResult.cs`:

```csharp
public sealed record MediaDurationProbeResult(TimeSpan? Duration, bool Transient)
{
    public static MediaDurationProbeResult Success(TimeSpan duration);
    public static MediaDurationProbeResult TransientFailure { get; }
    public static MediaDurationProbeResult PermanentFailure { get; }
}
```

`IMediaDurationProbe.ProbeFileAsync`/`ProbeStreamAsync` → `Task<MediaDurationProbeResult>`.

### 5. `FfprobeMediaDurationProbe`

- `ProbeStreamAsync` nutzt `EffectiveStreamTimeoutSeconds`, `ProbeFileAsync` weiter `EffectiveTimeoutSeconds`.
- `ProbeAsync` liefert `MediaDurationProbeResult`:
  - Timeout / `ExitCode == -1` / `ExitCode != 0` / unerwartete Exception → `TransientFailure` (Warnung loggen wie bisher).
  - `ExitCode == 0` und `TryParseDurationOutput` = null → `PermanentFailure` (Warnung: keine bestimmbare Dauer).
  - parsebar → `Success(duration)`.
- `FfprobeInvocationResult` um `StandardError` erweitern; Warnungen loggen stderr (abgeschnitten) für Diagnose. Signatur der internen `_executeAsync`-Seam bleibt identisch (Record-Feld), Tests passen Record-Konstruktion an.

### 6. `MediaDurationProbeService`

- Ctor: `IOptions<MediaProbeOptions>` und optional `IProgramScheduleSignal` hinzufügen.
- Auswahl-Query: `Duration == null` UND ( `DurationProbedAt == null` ODER ( `DurationProbeAttempts < EffectiveMaxAttempts` UND `DurationProbedAt <= now - EffectiveRetryDelaySeconds` ) ); `now` aus `_timeProvider`.
- Abarbeitung: Probes parallel per `SemaphoreSlim(EffectiveParallelProbes)` — jede Task liefert `(item, outcome)` ohne DbContext; danach seriell je Item anwenden + `SaveChangesAsync`. Outcome-Mapping:
  - `Success` → `Duration = d`, `DurationProbedAt = now`, `DurationProbeAttempts = 0`.
  - `Transient` (inkl. `OpenFileStream == null`, Exception) → `Duration = null`, `DurationProbedAt = now`, `DurationProbeAttempts++`.
  - `Permanent` → `Duration = null`, `DurationProbedAt = now`, `DurationProbeAttempts = EffectiveMaxAttempts`.
- Hat mindestens ein Item `Duration` neu erhalten → nach der Schleife `_signal?.NotifyScheduleChanged()`.

### 7. Tests anpassen / neu

`MediaDurationProbeServiceTests`:
- Fakes auf `MediaDurationProbeResult` umstellen; `IOptions<MediaProbeOptions>` + Fake-`IProgramScheduleSignal` registrieren.
- Bestandstests an neue Semantik anpassen (`FailedProbe_MarksProbedWithoutDuration` → transienter Fehler wird nach `RetryDelay` erneut geprüft; `SftpSource_NullStream` → transient, nicht final).
- Neu: Retry-Gate (Item mit `DurationProbedAt` gerade eben → übersprungen; älter als `RetryDelay` → erneut geprüft), `MaxAttempts`-Ausschluss, permanenter Fehler → Zähler = Max → kein Retry, Erfolg → Signal ausgelöst, Parallelität (`ParallelProbes = 1` seriell; `> 1` nachweisbar überlappend, z. B. über max. gleichzeitige Aufrufe im Fake).

`FfprobeMediaDurationProbeTests`: Rückgabetyp umstellen; neu: Stream-Probe verwendet `StreamTimeoutSeconds`; Timeout/`ExitCode != 0` → `Transient == true`; Exit 0 ohne Dauer → `Transient == false`.

`MediaDurationProbeWorkerTests`: `IMediaDurationProbe`-Fake auf neuen Rückgabetyp umstellen.

`VideoWebPlayerBackupDataTests`: neu `ReadFromAsync_LegacyBackupWithoutDurationProbeAttemptsColumn_RestoresSuccessfully` (Spalte entfernt → Restore ok, `DurationProbeAttempts == 0`).

Weitere IMediaDurationProbe-Implementierungen/Fakes im Repo suchen und anpassen.

### 8. Dokumentation

- `docs/help/videolaufzeit.md`: Abschnitt „Wie es funktioniert" (Retry-Semantik statt „nicht erneut geprüft") + Konfigurationstabelle um die vier neuen Schlüssel.
- `docs/RELEASE_NOTES.md`: Eintrag zu robusten Probes (längeres Stream-Timeout, Retry mit Versuchszähler/Verzögerung, Parallelität, Alt-Backup-kompatibel, neue Spalte + Migration).
- `README.md`: prüfen — der dortige Absatz verweist nur auf die Hilfeseite; Ergänzung nur bei Bedarf.

## Umsetzungsreihenfolge

1 → 2 → 4 → 5 → 3 (Entität + Migration + Backup) → 6 → 7 → Build Debug/Release → Tests → 8.

## Risiken / Seiteneffekte

- Schnittstellenänderung `IMediaDurationProbe`: internes Projekt-Interface, alle Implementierungen/Fakes im selben Repo — überschaubar.
- Parallelität: DbContext bleibt strikt single-threaded (Ergebnisanwendung seriell); `OpenFileStream` erzeugt je Aufruf neuen `SftpClient`.
- Größeres Stream-Timeout + Retries erhöhen die Last auf dem SFTP-Server — durch `ParallelProbes` (Default 4) und `RetryDelay` (Default 5 min) begrenzt.
- Alt-Verhalten gezielt geändert: bisher „fehlgeschlagen = nie wieder" ist jetzt „wiederholt bis MaxAttempts" — fachlich genau das Ziel von N4.

## Offene Punkte

Keine.
