# Bestandsaufnahme: InternalMediaStreamController an das TV-Sender-Feature-Flag binden oder Ausnahme begründen

Untersucht wurde der token-gesicherte interne Media-Stream-Endpunkt
`GET /api/internal/media-stream/{token}` (`InternalMediaStreamController.Stream`) und sein
Verhältnis zum globalen Feature-Flag `Setup.TvSenderEnabled` — bezogen auf die Anforderung,
die fehlende Flag-Prüfung entweder nachzurüsten (Variante A) oder als bewusste Ausnahme im
Code zu begründen (Variante B, Entscheidung offen beim Anwender).

## Zusammenfassung

- **Vorhanden:** `InternalMediaStreamController.Stream` (`VideoWebPlayer/Controllers/InternalMediaStreamController.cs` Z. 54–85) — token-auflösender Endpunkt mit Range-Verarbeitung, ohne `[BearerTokenCheck]`, ohne Benutzerkontext und **ohne jede Flag-Prüfung**. `ProgramSettingsService` ist derzeit nicht injiziert, müsste für Variante A per Konstruktor ergänzt werden (DI scoped → `ServiceCollectionExtensions.cs` Z. 267; Controller sind scoped, Vorbild `LiveStreamController` Z. 33).
- **Vorhanden (Flag-Infrastruktur):** `Setup.TvSenderEnabled` (`Data/Setup.cs` Z. 80, Migration `AddSetupTvSenderEnabled`, Backup-Optionalspalte) mit `ProgramSettingsService.GetTvSenderEnabledAsync`/`UpdateTvSenderEnabledAsync`. Das Flag wird an allen Gates **pro Aufruf/Tick frisch aus der DB** gelesen.
- **Vorhanden (Referenzmuster):** `LiveStreamController` Z. 55–57 (Flag → 404, vor jeder Fachlogik) und `ProgramGuideController` Z. 37–40 (`FeatureReleasedAsync`, mit begründendem Kommentar „indistinguishable from does not exist") — beide prüfen vor der eigentlichen Arbeit. Die drei Worker `ProgramScheduleWorker` (Z. 63), `MediaDurationProbeWorker` (Z. 65) und `LiveStreamWorker` (Z. 73–79, stoppt aktiv den Segmenter) prüfen das Flag pro Zyklus/Tick.
- **Zwei Token-Arten laufen über denselben Endpunkt** (relevant für beide Varianten):
  - Probe-Tokens via `TryCreateProbeUri` — TTL `MediaProbe:HttpProbeTokenLifetimeSeconds` (Default 600 s), an `HttpProbeEnabled` gekoppelt; Verbraucher `MediaDurationProbeService.ProbeRemoteItemAsync` (Z. 319) für `ffprobe`.
  - Stream-Tokens via `TryCreateStreamUri` — TTL = restliche Sendeplatz-Laufzeit + 15 min (Fallback 45 min, Minimum 60 s), **nicht** an `HttpProbeEnabled` gekoppelt; Verbraucher `LiveStreamMediaResolver.ResolveAsync` (Z. 124–127) → `LiveStreamInput.ForHttp` → `ffmpeg -i` des Segmentierers. Tokens leben nur im Speicher (`ConcurrentDictionary`), Prozessneustart invalidiert sie.
- **Für Variante A bereits greifbare Nebenwirkungen:** Ein flag-bedingtes 404 während eines laufenden ffmpeg-Reads wird über `FfmpegErrorMarkers.ContainsHttpTransportError` als HTTP-Transportfehler klassifiziert → `LiveStreamExitInfo.HttpTransportFailure` → derselbe Slot würde beim nächsten Startversuch auf `pipe:0` zurückfallen (`LiveStreamCoordinator` Z. 111–129). Analog gilt ein 404 bei einer laufenden Probe in `FfprobeMediaDurationProbe.ProbeAsync` (Z. 192–198) als `UriUnsupported` → derselbe Probeversuch fällt sofort auf `sftp://`/`pipe:0` zurück — der Seek bricht zwar hart ab, der Versuch selbst geht aber nicht verloren.
- **Begründungsanker für Variante B vorhanden:** Der Klassen-Kommentar (`InternalMediaStreamController.cs` Z. 8–17) begründet bereits die Token-only-Berechtigung, erwähnt aber weder den Segmentierer-Pfad noch das Feature-Flag — die Ausnahme ist also noch **nicht** dokumentiert.
- **Fehlend:** Jeglicher Test, der den Endpunkt mit `Setup.TvSenderEnabled` verbindet. Die bestehenden `InternalMediaStreamControllerTests` laufen mit implizit ausgeschaltetem Flag (`Setup`-Zeile wird nie gesetzt) und belegen damit das heutige Verhalten „gültiges Token → 200 trotz Flag aus". Für Variante A fehlt der geforderte Regressionstest; bei Variante B wären die Bestandstests unverändert korrekt.

**Test-Ausgangszustand:** Baseline-Lauf der vollständigen Suite `dotnet test VideoWebPlayer.Tests/VideoWebPlayer.Tests.csproj` auf Commit `7f9023d`: **2246 erfolgreich, 0 fehlgeschlagen, 1 übersprungen** (bekannter, absichtlich deaktivierter Alt-Befund-Test) — siehe [inventory/tests.md](inventory/tests.md) samt Nachweisen in `inventory/test-results/`.

## Details

- [Datenmodell](inventory/models.md) — `Setup.TvSenderEnabled` samt Backup-Registrierung, vom Endpunkt gelesene Medien-Entitäten, interne Records (`TokenEntry`, `LiveStreamInput`, `LiveStreamExitInfo`)
- [Logik](inventory/logic.md) — `InternalMediaStreamController`, `InternalMediaStreamTokenService` (beide Token-Arten), `ProgramSettingsService`, Verbraucher (`MediaDurationProbeService`, `LiveStreamMediaResolver`, `LiveStreamCoordinator`, `FfmpegLiveStreamSegmenter`), Flag-Gates der Worker und Referenzmuster
- [Enums](inventory/enums.md) — `MediaSourceType`, `LiveStreamSyncAction`
- [Interfaces](inventory/interfaces.md) — `IInternalMediaStreamTokenService`, `ILiveStreamSegmenter`, `IMediaSourceReader`, `IMediaDurationProbe`
- [Tests](inventory/tests.md) — Test-Ausgangszustand, Endpunkt-/Token-Tests, Flag-Gate-Testmuster der übrigen Komponenten, Hilfsmethoden
