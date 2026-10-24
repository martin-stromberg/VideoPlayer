# Umsetzungsplan: InternalMediaStreamController an das TV-Sender-Feature-Flag binden (Issue #259)

## Übersicht

Der interne, token-gesicherte Endpunkt `GET /api/internal/media-stream/{token}`
(`InternalMediaStreamController.Stream`) ist die einzige TV-Sender-Komponente ohne Prüfung
des globalen Feature-Flags `Setup.TvSenderEnabled`. Der Anwender hat **Variante A**
entschieden: Der Endpunkt wird an das Flag gebunden — `GetTvSenderEnabledAsync` wird als
erste Aktion in `Stream` (vor dem Token-Resolve) geprüft und liefert bei deaktiviertem
Flag 404, konsistent mit `LiveStreamController` und `ProgramGuideController`. Betroffen
sind `InternalMediaStreamController.cs`, der XML-Kommentar von `Setup.TvSenderEnabled`,
die Testklassen `InternalMediaStreamControllerTests` und `MediaProbeHttpIntegrationTests`
sowie `docs/RELEASE_NOTES.md` und `docs/help/tv-sender.md` (Prüfung).

## Designentscheidungen

| Komponente / Bereich | Gewählter Ansatz | Begründung |
|----------------------|-----------------|------------|
| Grundsatz: Flag-Bindung des internen Endpunkts | **Variante A** — `GetTvSenderEnabledAsync` → 404 bei deaktiviertem Flag (Anwender-Entscheidung) | Konsistenz: Beim Abschalten des Senders sind alle zugehörigen Endpunkte geschlossen. Die Nebenwirkung — ein Flag-Wechsel bricht laufende ffmpeg-Reads des Segmentierers und laufende Probes hart ab (siehe „Seiteneffekte") — ist bewusst in Kauf genommen. |
| Position des Flag-Checks | Check als erste Aktion in `Stream`, **vor** `_tokens.ResolveMediaItemId(token)` | Konsistenz mit den übrigen Gates: `LiveStreamController` prüft das Flag vor jeder Fachlogik (Z. 55–57), `ProgramGuideController` begründet das 404 explizit mit „the feature is indistinguishable from does not exist" (Z. 37–38). Ein Check vor dem Token-Resolve macht den Endpunkt bei deaktiviertem Feature für jeden Request identisch unsichtbar. Der Preis — eine DB-Abfrage (`GetOrCreateSetupAsync`) je Request inkl. ungültiger Tokens — entspricht dem Muster der anderen Gates. |
| Ort der Entscheidungs-Dokumentation | Klassen-Kommentar des `InternalMediaStreamController` (Z. 8–17) plus kurzer Begründungskommentar am Gate; ergänzend XML-Kommentar von `Setup.TvSenderEnabled` | Der Klassen-Kommentar begründet bereits die Token-only-Berechtigung und ist der natürliche Anker; er erwähnt bislang weder den Segmentierer-Pfad noch das Flag. `Setup.TvSenderEnabled` listet heute alle abgedeckten Komponenten, aber nicht den internen Endpunkt — der Kommentar muss den Endpunkt in diese Liste aufnehmen. |

## Programmabläufe

### Request-Behandlung mit Flag-Gate (neuer Ablauf)

1. `Stream(token, ct)` wird aufgerufen.
2. **Neu:** `await _settings.GetTvSenderEnabledAsync(HttpContext.RequestAborted)` →
   `false` ⇒ `NotFound()` (Kommentar analog „Feature unsichtbar" wie an den übrigen Gates;
   `GetTvSenderEnabledAsync` legt den `Setup`-Datensatz bei Bedarf an).
3. `_tokens.ResolveMediaItemId(token)` → `null` (unbekannt/abgelaufen) ⇒ `NotFound()`.
4. `MediaItems` inkl. `MediaCollection.MediaSource` laden → `null` ⇒ `NotFound()`.
5. `_reader.OpenFileStream(collection, fileName)` → Ausnahme ⇒ 500 mit Warn-Log;
   `null` ⇒ `NotFound()`.
6. `File(stream, MediaFileContentTypes.ForFileName(fileName), enableRangeProcessing: true)`.

Beteiligte Klassen/Komponenten: `InternalMediaStreamController` (neu mit injiziertem
`ProgramSettingsService`), `ProgramSettingsService`, `IInternalMediaStreamTokenService`,
`ApplicationDbContext`, `IMediaSourceReader`.

## Neue Klassen

Keine.

## Änderungen an bestehenden Klassen

### `InternalMediaStreamController` (Controller)

- **Neue Dependency:** `ProgramSettingsService` als Konstruktor-Parameter `settings` mit
  Feld `_settings` — scoped-Service in scoped Controller, Vorbild `LiveStreamController`
  Z. 33 bzw. `ProgramGuideController` Z. 30. Keine neue DI-Registrierung nötig
  (`ServiceCollectionExtensions` Z. 267).
- **Geänderte Methode:** `Stream` — `GetTvSenderEnabledAsync(HttpContext.RequestAborted)`
  als erste Aktion; `false` ⇒ `NotFound()` mit kurzem Begründungskommentar.
- **Klassen-Kommentar:** ergänzt um den Segmentierer-Pfad (`TryCreateStreamUri`, lange
  Token-Lebensdauer) und die Flag-Bindung (Endpunkt folgt jetzt dem globalen Schalter wie
  die übrigen TV-Sender-Endpunkte).

### `Setup` (Datenmodellklasse)

- **XML-Kommentar `TvSenderEnabled`:** Der interne Media-Stream-Endpunkt wird in die
  Liste der abgedeckten Komponenten aufgenommen.

## Datenbankmigrationen

Keine — `Setup.TvSenderEnabled` existiert bereits (Migration `AddSetupTvSenderEnabled`,
als optionale Spalte in `VideoWebPlayerBackupData` registriert). Die Änderung am
`Setup`-Kommentar ist keine Schemaänderung.

## Validierungsregeln

Keine.

## Konfigurationsänderungen

Keine — es wird ausschließlich der bestehende Schalter `Setup.TvSenderEnabled`
(`ProgramSettingsService.GetTvSenderEnabledAsync`/`UpdateTvSenderEnabledAsync`)
herangezogen.

## Seiteneffekte und Risiken

- **Harter Abbruch laufender ffmpeg-Reads:** Ein flag-bedingtes 404 während eines
  laufenden Segmentierer-Reads erscheint in ffmpeg-stderr als „Server returned 404…" und
  wird über `FfmpegErrorMarkers.ContainsHttpTransportError` als HTTP-Transportfehler
  klassifiziert → `LiveStreamExitInfo.HttpTransportFailure` → derselbe Slot fällt beim
  nächsten Startversuch auf `pipe:0` zurück (`LiveStreamCoordinator` Z. 111–129), statt
  regulär über `LiveStreamWorker` → `StopAsync` beendet zu werden. Die Fallback-Markierung
  bleibt bestehen, bis ein späterer Lauf `LastUnexpectedExit` überschreibt. Bewusst
  akzeptiert (Anwender-Entscheidung Variante A).
- **Laufende Probes:** Ein 404 mitten im Seek wird in
  `FfprobeMediaDurationProbe.ProbeAsync` (Z. 192–198) als `UriUnsupported` gewertet →
  Fallback auf `sftp://`/`pipe:0` innerhalb desselben Versuchs; der Seek bricht hart ab,
  der Versuch geht aber nicht verloren.
- **DB-Abfrage je Request:** `GetTvSenderEnabledAsync` läuft über
  `GetOrCreateSetupAsync` (beim allerersten Aufruf schreibend) für jeden Request, auch für
  ungültige Tokens — gleiches Muster wie an allen anderen Gates, aber zusätzliche Last auf
  einem Endpunkt, der bisher vollständig DB-leicht startete (Token-Resolve ist rein
  in-memory).
- **Bestehende Tests brechen ohne Flag-Seed:** `InternalMediaStreamControllerTests`
  (drei Tests mit 200/206-Erwartung) und `MediaProbeHttpIntegrationTests` (`Category=E2E`,
  zwei Tests über echten Kestrel-Host) laufen mit implizit ausgeschaltetem Flag und
  würden mit 404 statt 200/206 brechen — Flag-Seed `TvSenderEnabled = true` erforderlich.
- **Dokumentations-Genauigkeit:** `docs/help/tv-sender.md` Z. 15 sagt „Ohne Freigabe
  antworten die Endpunkte mit 404" — durch Variante A wird diese Aussage vollständig
  zutreffend; der Absatz ist bei der Umsetzung gegen den finalen Stand zu prüfen.

## Umsetzungsreihenfolge

1. **Flag-Gate im `InternalMediaStreamController`**
   - Voraussetzungen: `ProgramSettingsService` ist bereits scoped registriert
     (`ServiceCollectionExtensions` Z. 267) — keine neue Infrastruktur nötig.
   - Beschreibung: `ProgramSettingsService` injizieren, Flag-Check als erste Aktion in
     `Stream` (vor Token-Resolve) mit Begründungskommentar.

2. **Kommentare: Entscheidung im Code dokumentieren**
   - Voraussetzungen: Schritt 1.
   - Beschreibung: Klassen-Kommentar des `InternalMediaStreamController` (Segmentierer-
     Pfad via `TryCreateStreamUri` nennen, Flag-Bindung festhalten) und XML-Kommentar von
     `Setup.TvSenderEnabled` (internen Endpunkt in die Liste der abgedeckten Komponenten
     aufnehmen) ergänzen — siehe „Änderungen an bestehenden Klassen".

3. **Tests: Flag-Seed + Regressionstest**
   - Voraussetzungen: Schritt 1.
   - Beschreibung: Hilfsmethode `SetTvSenderEnabledAsync(bool)` in
     `InternalMediaStreamControllerTests`; `IssueProbeUriAsync` setzt das Flag auf `true`;
     neuer Test `Stream_FeatureDisabled_ValidToken_Returns404`; Flag-Seed `true` in
     `MediaProbeHttpIntegrationTests`.

4. **Dokumentation**
   - Voraussetzungen: Schritt 1.
   - Beschreibung: Eintrag in `docs/RELEASE_NOTES.md` (englischer + deutscher Abschnitt,
     Muster der bisherigen TV-Sender-Einträge); `docs/help/tv-sender.md` Z. 15 gegen den
     finalen Stand prüfen (Aussage wird durch das Gate zutreffend).

5. **Abschlussprüfung**
   - Voraussetzungen: Schritte 1–4.
   - Beschreibung: `dotnet build VideoPlayer.sln` in Debug **und** Release fehlerfrei;
     volle Suite `dotnet test VideoWebPlayer.Tests/VideoWebPlayer.Tests.csproj` grün
     (Baseline: 2246 erfolgreich, 1 übersprungen).

## Tests

### Neue Tests

| Test / Hilfsmethode | Testklasse | Was wird geprüft / bereitgestellt? |
|--------------------|------------|-------------------------------------|
| `SetTvSenderEnabledAsync(bool)` (Hilfsmethode) | `InternalMediaStreamControllerTests` | Setzt `Setup.TvSenderEnabled` in einem DI-Scope — über `ProgramSettingsService.UpdateTvSenderEnabledAsync` oder direkten `Setup`-Seed (Vorbild: `ProgramScheduleTestDb` Z. 40). Aufgerufen aus `IssueProbeUriAsync` mit `true`, damit alle Bestandstests mit aktiviertem Flag laufen. |
| `Stream_FeatureDisabled_ValidToken_Returns404` | `InternalMediaStreamControllerTests` | Flag aus (`SetTvSenderEnabledAsync(false)`) + gültiges Token → 404 — der geforderte Regressionstest des Gates. |
| `Stream_FeatureEnabled_ValidToken_ReturnsFile` | (kein neuer Test) | Bereits durch `Stream_ValidToken_ReturnsFile` und `Stream_ValidToken_RangeRequest_Returns206PartialContent` abgedeckt — sie belegen nach dem Flag-Seed „gültiges Token + Flag an → 200 inkl. Range". |

### Betroffene bestehende Tests

| Test / Testklasse | Grund der Anpassung |
|-------------------|---------------------|
| `Stream_ValidToken_ReturnsFile`, `Stream_ValidToken_RangeRequest_Returns206PartialContent`, `Stream_TokenIsBoundToIssuedItem_UnknownAfterInvalid`, `Stream_InvalidToken_Returns404` (`InternalMediaStreamControllerTests`) | Laufen mit implizit ausgeschaltetem Flag; die 200/206-Erwartungen würden mit 404 brechen. Anpassung über `SetTvSenderEnabledAsync(true)` in `IssueProbeUriAsync`; die reinen 404-Tests bleiben inhaltlich gleich, laufen aber einheitlich mit gesetztem Flag. |
| `HttpProbe_RangeEndpoint_ReturnsDuration`, `InternalStream_RangeRequest_ReturnsPartialContent` (`MediaProbeHttpIntegrationTests`, `Category=E2E`) | Echter Kestrel-Host ohne Flag-Seed — beide würden mit 404 statt ffprobe-Duration/206 brechen; `SeedMediaItemAsync` (oder `InitializeAsync`) setzt `TvSenderEnabled = true` in der Test-DB. |

### E2E-Tests (primärer Funktionsnachweis)

Keine neuen E2E-Tests: Die Anforderung berührt keinen über UI oder Nutzeraktion
erreichbaren Ablauf. Der Endpunkt ist eine interne Loopback-Schnittstelle, die
ausschließlich serverseitige Prozesse (ffprobe-Probe, ffmpeg-Segmentierer) mit nicht
erratbaren, nur vom Server ausgestellten Tokens aufrufen — es existiert kein
anwendersichtbarer Benutzerfluss, den ein E2E-Test nachweisen müsste. Die HTTP-Ebene des
Endpunkts ist bereits durch `MediaProbeHttpIntegrationTests` (`Category=E2E`, echter
Kestrel-Host) abgedeckt; diese werden wie unten mit Flag-Seed angepasst und dienen dann
zugleich als E2E-Regression „Flag an → Endpunkt liefert".

**Anzupassende bestehende E2E-Tests:**

| Test / Testklasse | Grund der Anpassung |
|-------------------|---------------------|
| `HttpProbe_RangeEndpoint_ReturnsDuration`, `InternalStream_RangeRequest_ReturnsPartialContent` (`MediaProbeHttpIntegrationTests`) | Flag-Seed `TvSenderEnabled = true` nötig, sonst 404 statt 200/206 (siehe Tabelle oben). |

## Offene Punkte

Keine.
