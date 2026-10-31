# Umsetzungsplan: Live-TV in der Benutzeroberfläche (Schritt 7)

## Übersicht

Neue Seite `LiveTv` (`/live-tv`) mit HLS-Player (nativ bzw. hls.js-Fallback), „Jetzt/Gleich"-Anzeige und begrenzter Programmvorschau; erreichbar über die `NavMenu`-Navigation für angemeldete Benutzer. Daten kommen über einen neuen `IProgramGuideApiClient` (serverseitig abgesicherte API), der Stream über die `access_token`-URL aus Schritt 6. hls.js wird als vendored wwwroot-Asset versionsfest eingebunden (Muster `wwwroot/bootstrap/`).

## Designentscheidungen

| Komponente / Bereich | Gewählter Ansatz | Begründung |
|----------------------|-----------------|------------|
| Datenabruf Programmanzeige | Neuer `IProgramGuideApiClient` (Interface in `VideoWebPlayer.Client`, implementiert durch `VideoWebPlayerClient.ProgramGuide.cs`-Partial), DI wie `IPlaylistApiClient` | Die Seite nutzt denselben serverseitig abgesicherten HTTP-Pfad wie Geräte (Berechtigung bleibt serverseitig); `InternalVideoWebPlayerClient` liefert Impersonation und 401-Erneuerung; `IPlaylistApiClient`-Muster ist die Vorgabe |
| Player-Anbindung | Eigenes JS-Modul `wwwroot/js/liveTv.js` (`window.liveTv`): native HLS per `canPlayType`, sonst hls.js über MSE; `VideoPlayer.razor` bleibt unverändert | Der Bestandsplayer ist ein Overlay für progressive Dateistreams mit Playlist-/Weiterschauen-Kontext — für Live-TV nicht passend (kein HLS, kein Vorspulen-Konzept); eine eigene Live-TV-Ansicht ist ein anderer Anwendungsfall |
| hls.js-Bereitstellung | Vendored `hls.min.js` (npm `hls.js@1.7.3`, Apache-2.0) unter `wwwroot/js/vendor/` + LICENSE-Datei; **Lazy-Load** per dynamischem `<script>`-Tag nur wenn der Browser kein natives HLS kann | `dotnet publish` packt `wwwroot` automatisch — keine Build-/CI-Pipeline-Änderung; Lazy-Load vermeidet ~620 kB Download auf jeder Seite (Vorteil ggü. globalem `<script>` in `App.razor`); `StaticAssetVersioner.Url` liefert die fingerabdruckte URL, die Blazor an `attach` übergibt; vendored-Präzedenzfall `bootstrap.min.css` |
| Fehler-/Leerzustände | Zustandsmaschine in der Seite: `Laden`, `NichtAngemeldet`, `Ladefehler`, `KeinProgramm` (Sendelücke), `SpieltAb`, `StreamFehler` (mit Selbstheilungs-Hinweis), `BrowserNichtUnterstuetzt`; senderbezogene Texte in neuer `LiveTvErrorMessages`-Klasse (Muster `MediaAccessErrorMessages`) | Verständliche Rückmeldung statt technischem Fehlerbild ist Akzeptanzkriterium; die Fehlerursache ist hier bekannt (Guide-Antwort/JS-Callback), also senderbezogene statt generischer Texte |
| Aktualisierung Programmanzeige | `PeriodicTimer` (~30 s) in `LiveTv.razor` (Component-Scope, `Dispose` räumt auf): pollt `now` + `upcoming`; bei Slot-Wechsel (`Now.SlotId`-Änderung oder Übergang `null`↔Wert) Player-Zustand neu bewerten und `liveTv.attach` erneut aufrufen | Rein zeitbasierte Zustandslosigkeit des Backends; 30 s Versatz bei „Jetzt/Gleich" vertretbar; Slot-Übergang erfordert Neu-Attach (neue m3u8-Session) |
| Stream-Fehler-Wiederholung | hls.js-Fatal-Error → .NET-Callback `NotifyStreamError` → Status `StreamFehler` + JS-seitiger Re-Attach-Timer (alle ~5 s `hls.recoverMediaError()`/`loadSource` bzw. natives erneutes `src`-Setzen); `playing`-Event → Status `SpieltAb` | Selbstheilend bei kurzen m3u8-Lücken (Slot-Übergang, Segmenter-Neustart) — kein manuelles Neuladen nötig |
| Autoplay | `autoplay muted playsinline controls` — stumm gestartet (Browser-Autoplay-Policy), Ton per Regler | Zuverlässiges Sofort-Loslaufen ohne Policy-Block |
| Sendelücke vs. fehlender Stream | `Now == null` → kein Attach, `KeinProgramm`-Hinweis + nächste Sendung/Vorschau bleiben sichtbar; `Now != null` aber m3u8 nicht ladbar (404: kein ffmpeg/Sender-Neustart) → `StreamFehler`-Hinweis mit Re-Attach | Trennt zwei verschiedene, dem Anwender erklärbare Zustände |

## Programmabläufe

### Seitenaufruf `/live-tv` (angemeldet, Programm läuft)

1. `OnInitializedAsync`: `AuthenticationStateProvider.GetAuthenticationStateAsync` → `isAuthenticated` (false → `LoginForm` rendern, Muster `Home.razor`); `Client.EnsureAuthorizationTokenSilentlyAsync(user)` (Token für `access_token`); `IProgramGuideApiClient.RequestProgramGuideNowAsync()` + `RequestProgramGuideUpcomingAsync(PreviewHours)`.
2. Render: Kopfbereich „Live-TV"; Player-Bereich mit `<video @ref>`; „Jetzt"-Karte (Titel, Sendezeit `HH:mm–HH:mm`, Fortschrittsbalken aus `PositionSeconds`/`DurationSeconds`, Restdauer); „Gleich"-Karte (Titel + Startzeit); Vorschau-Liste.
3. `OnAfterRenderAsync` (wenn `Now != null` && Video-Element gerendert && URL noch nicht attached): `JS.InvokeVoidAsync("liveTv.attach", _videoRef, streamUrl, hlsScriptUrl, _dotNetRef)`; `streamUrl = /api/live-stream/live.m3u8?access_token={Client.AuthorizationToken}`, `hlsScriptUrl = AssetVersions.Url("js/vendor/hls.min.js")`.
4. `liveTv.attach` (JS): `videoEl.canPlayType('application/vnd.apple.mpegurl')` → `src` setzen; sonst `Hls.isSupported()` → Script-Tag für `hlsScriptUrl` injizieren (einmalig), `new Hls()` + `loadSource` + `attachMedia`; sonst `dotNetRef.invokeMethodAsync('NotifyStreamUnsupported')`. Video-`error`/`playing`-Events → DotNet-Callbacks `NotifyStreamError`/`NotifyStreamPlaying`. hls.js `ERROR`-Event (fatal) → `NotifyStreamError` + JS-Re-Attach-Timer.
5. `PeriodicTimer` alle ~30 s: `now` + `upcoming` neu laden; `Now.SlotId`-Wechsel oder `null`↔Wert-Übergang → `liveTv.detach`/`attach` neu; Fehler beim Poll → geloggter, stiller Retry (letzter Stand bleibt sichtbar), nur initialer Ladefehler → `Ladefehler`-Zustand.
6. `Dispose`/`IAsyncDisposable`: Timer stoppen, `liveTv.detach` (JS-Aufruf in try/catch), `_dotNetRef.Dispose()`.

### Seitenaufruf ohne Anmeldung

`isAuthenticated == false` → `<LoginForm/>` wie `Home.razor` — verständlicher Einstieg statt technischem Fehler. API/Stream bleiben ohnehin serverseitig abgesichert.

### Sendelücke (kein laufender Slot)

`Now == null` → kein `liveTv.attach`; `KeinProgramm`-Hinweisbox („Aktuell läuft keine Sendung"), `Next` + Vorschau-Liste weiterhin sichtbar; Timer pollt weiter → bei Slot-Beginn automatischer Übergang in `SpieltAb`.

### Stream nicht verfügbar (Slot läuft, m3u8 404 — z. B. ffmpeg fehlt/Startphase)

`liveTv.attach` → hls.js `manifestLoadError`/nativer `error` → `NotifyStreamError` → `StreamFehler`-Hinweisbox („Der Live-Stream ist momentan nicht verfügbar — es wird automatisch erneut versucht") + JS-Re-Attach-Timer; Programmanzeige bleibt sichtbar.

### Fehler beim API-Abruf (z. B. abgelaufene/verweigerte Session)

`HttpRequestException` aus `IProgramGuideApiClient` beim initialen Laden → `Ladefehler`-Zustand mit verständlicher Meldung (`MediaAccessErrorMessages.Describe`-Muster für 403; generisch sonst); beim Polling → stiller Retry.

## Neue Klassen

| Klasse | Typ | Zweck |
|--------|-----|-------|
| `IProgramGuideApiClient` | Interface (`VideoWebPlayer.Client`) | `RequestProgramGuideNowAsync(CancellationToken)` → `DtoProgramGuideNow`; `RequestProgramGuideUpcomingAsync(int hours, CancellationToken)` → `DtoProgramGuideEntry[]` |
| `VideoWebPlayerClient.ProgramGuide.cs` | Partial-Klasse | Implementiert `IProgramGuideApiClient` über `HttpGetAsync<T>` (Endpunkte `api/program-guide/now`, `api/program-guide/upcoming?hours={h}`) |
| `LiveTv.razor` | Razor-Seite (`Components/Pages/`) | Route `/live-tv`; Player + Jetzt/Gleich + Vorschau + Zustandsmaschine + Timer |
| `LiveTv.razor.css` | Scoped-CSS | Layout Player/Karten/Vorschau-Liste im `app.css`-Stil |
| `liveTv.js` | JS-Modul (`wwwroot/js/`) | `window.liveTv`: `attach`, `detach`; native HLS → hls.js (Lazy-Load) → Unsupported; `playing`/`error`/hls.js-Fatal-Events → DotNet-Callbacks; Re-Attach-Timer |
| `LiveTvErrorMessages` | `internal static` (`VideoWebPlayer/ViewModels/`) | Senderbezogene Texte: `NotAuthorized`-Hinweis, `NoProgramRunning`, `StreamUnavailable`, `UnsupportedBrowser`, `LoadFailed`, `Describe(Exception)` für 403/401 |
| `hls.min.js` | vendored Asset (`wwwroot/js/vendor/`) | hls.js 1.7.3 (npm dist), SHA-256 dokumentiert |
| `hls.js.LICENSE.txt` | Textdatei (`wwwroot/js/vendor/`) | Apache-2.0-Lizenztext aus dem npm-Paket |

## Änderungen an bestehenden Klassen

### `VideoWebPlayerClient` (Partial-Datei)

- **Neue Datei** `VideoWebPlayerClient.ProgramGuide.cs`; Klassendeklaration ändert sich zu `public partial class VideoWebPlayerClient : IPlaylistApiClient, IProgramGuideApiClient` in `VideoWebPlayerClient.cs` (Interface-Liste ergänzen).

### `ServiceCollectionExtensions` (DI)

- **Neue Registrierung:** `services.AddScoped<IProgramGuideApiClient>(sp => sp.GetRequiredService<VideoWebPlayerClient>());` — direkt hinter der `IPlaylistApiClient`-Zeile.

### `NavMenu.razor` (Layout)

- **Neuer Eintrag** im `<Authorized>`-Block zwischen „Playlists" und Quellen: `<NavLink class="nav-link" href="/live-tv">` mit `bi bi-tv`-Icon + „Live-TV".

### `docs`-Artefakte (kein Code)

- `docs/help/tv-sender.md` — Abschnitt Live-TV-Ansicht + hls.js-Lizenz/Bereitstellung.
- `docs/API.md` — Program-Guide-/Live-Stream-Endpunkte bereits aus Schritt 5/6 dokumentiert; ggf. ergänzen, dass die UI sie nutzt.
- `README.md`, `docs/RELEASE_NOTES.md`, `changes.log` — Feature-Eintrag + hls.js-Abhängigkeit.

## Datenbankmigrationen

Keine.

## Validierungsregeln

| Feld / Objekt | Regel | Fehlerfall |
|---------------|-------|------------|
| `hours`-Parameter `RequestProgramGuideUpcomingAsync` | `1 <= hours <= MaxPreviewHours` (Konstante `PreviewHours` der Seite ≤ 12) | Ungültige Werte gar nicht erst senden (Konstante fixiert, kein User-Input) |
| Stream-URL | nur gesetzt, wenn `AuthorizationToken` vorhanden und `Now != null` | ohne Token kein Attach — `Ladefehler`/Auth-Zustand |

## Konfigurationsänderungen

Keine — `PreviewHours` (= 6, API-Default) und `PollIntervalSeconds` (= 30) sind Konstanten der Seite.

## Seiteneffekte und Risiken

- **`NoOpVideoWebPlayerClient` (Tests):** `VideoWebPlayerClient` implementiert `IProgramGuideApiClient` — das NoOp-Double erbt die Methoden; sein `HttpGetAsync<T>` liefert für unbekannte `T` eine `List<MediaEntryDto>` → `InvalidCastException` bei `DtoProgramGuideNow`. Tests, die die neuen Methoden durchlaufen, brauchen Erweiterung des Doubles oder eigene Fakes (geplant).
- **`App.razor`:** `liveTv.js` wird wie die anderen Module global eingebunden (klein); `hls.min.js` bewusst NICHT global (Lazy-Load).
- **Playwright-E2E:** Chromium hat kein natives HLS → hls.js-Pfad aktiv; m3u8 kann mit Dummy-Segmenten serviert werden — die Assertions setzen auf DOM-Zustände + abgefangenen m3u8-Request, nicht auf echte Decodierung.
- **Video-Autoplay:** `muted`-Autoplay ist die zuverlässige Variante.
- **Token-Lebensdauer:** lange offene Seite → `access_token` kann altern; Re-Attach nutzt die beim Attach übergebene URL (gleiches Verhalten wie Bild-URLs im Bestand). Kein neues Risiko.

## Umsetzungsreihenfolge

1. **hls.js vendoren** — Voraussetzungen: keine (Dateien bereits heruntergeladen: `hls.min.js` 1.7.3 sha256 `a12e7ee1…`, LICENSE).
   - `wwwroot/js/vendor/hls.min.js` + `wwwroot/js/vendor/hls.js.LICENSE.txt` anlegen.
2. **`IProgramGuideApiClient` + Partial + DI** — Voraussetzungen: bestehende `HttpGetAsync`-Helfer, DTOs (vorhanden).
   - Interface, `VideoWebPlayerClient.ProgramGuide.cs`, Interface-Liste in `VideoWebPlayerClient.cs`, DI-Zeile.
3. **`liveTv.js` + `App.razor`-Einbindung** — Voraussetzungen: Schritt 1.
   - `window.liveTv` attach/detach, Lazy-Load hls.js, DotNet-Callbacks, Re-Attach-Timer; `<script src="@AssetVersions.Url("js/liveTv.js")">` in `App.razor`.
4. **`LiveTvErrorMessages`** — Voraussetzungen: keine. Statische Texte + `Describe`.
5. **`LiveTv.razor` + `LiveTv.razor.css`** — Voraussetzungen: Schritte 2–4.
   - Zustandsmaschine, Markup, Timer, JS-Interop, Dispose.
6. **`NavMenu.razor`-Eintrag** — Voraussetzungen: Schritt 5 (Route existiert).
7. **Tests** — Voraussetzungen: Schritte 1–6.
   - bUnit `LiveTvTests` + NavMenu-Test; Client-Test via `DeviceClientTestBase`; Playwright `LiveTvE2ETests`.
8. **Dokumentation** — `docs/help/tv-sender.md`, `docs/API.md` (UI-Nutzung), `README.md`, `docs/RELEASE_NOTES.md`, `changes.log`.

## Tests

### Neue Tests

| Test / Hilfsmethode | Testklasse | Was wird geprüft / bereitgestellt? |
|--------------------|------------|-------------------------------------|
| `Now_LoadsAndRendersCurrentNextAndUpcoming` | `LiveTvTests` (bUnit, `Components/`) | Authentifiziert + Guide-Daten → Now-Titel/-Zeit/-Fortschritt, Next-Titel, Vorschau-Zeilen, Video-Element |
| `Now_WithoutRunningSlot_ShowsNoProgramHintAndUpcoming` | `LiveTvTests` | `Now == null` → „keine Sendung"-Hinweis, kein `liveTv.attach`-Aufruf (JS-Interop-Assert), `Next`/Vorschau sichtbar |
| `Page_NotAuthenticated_ShowsLoginForm` | `LiveTvTests` | Nicht authentifiziert → `LoginForm` gerendert, kein API-Aufruf |
| `Page_GuideCallFails_ShowsFriendlyLoadError` | `LiveTvTests` | `HttpRequestException` → `Ladefehler`-Meldung statt Exception |
| `Page_NotifyStreamUnsupported_ShowsBrowserHint` | `LiveTvTests` | `NotifyStreamUnsupported`-Callback → „Browser wird nicht unterstützt"-Meldung |
| `Page_NotifyStreamError_ShowsRetryHint` | `LiveTvTests` | `NotifyStreamError`-Callback → „momentan nicht verfügbar"-Meldung mit Retry-Hinweis |
| `NavMenu_ContainsLiveTvEntry_ForAuthorizedUser` | `NavMenuLiveTvTests` (oder Erweiterung `NavMenuPlaylistsTests`) | Genau ein `/live-tv`-Link mit „Live-TV" im Authorized-Bereich |
| `PairedDevice_RequestNow_ReturnsGuide` | `VideoWebPlayerClientTests_ProgramGuide` (`ClientLibrary/`, `DeviceClientTestBase`) | `Client.RequestProgramGuideNowAsync()` gegen gehostete App mit geseedeten Slots |
| `PairedDevice_RequestUpcoming_ReturnsBoundedEntries` | `VideoWebPlayerClientTests_ProgramGuide` | `RequestProgramGuideUpcomingAsync(3)` → laufender+folgender Eintrag |
| `SeedChannelWithProgramAsync` | `LiveTvE2ETests` (privat) | `TVChannel` + laufender/folgender `ProgramSlot` + m3u8/ts im Temp-Segmentverzeichnis + `Setup.LiveStreamSegmentDirectory` |
| `AttachDetachesLiveTvJs` (JS-Interop-Verifikation) | `LiveTvTests` | bUnit-JSInterop verifiziert `liveTv.attach` mit m3u8-URL + `access_token` |

### Betroffene bestehende Tests

| Test / Testklasse | Grund der Anpassung |
|-------------------|---------------------|
| `NoOpVideoWebPlayerClient` (Hilfsklasse) | `HttpGetAsync<T>` um `DtoProgramGuideNow`/`DtoProgramGuideEntry[]` erweitern, sonst `InvalidCastException` bei bUnit-Tests, die den neuen Client-Pfad auslösen |

### E2E-Tests (primärer Funktionsnachweis)

| Priorität | Szenario | Testdatei / Testklasse | Abgedecktes Akzeptanzkriterium | Warum E2E nötig ist |
|-----------|----------|------------------------|-------------------------------|-------------------|
| Pflicht | Angemeldeter Benutzer erreicht `/live-tv` über die Navigation; Player wird angebunden (m3u8-Request mit `access_token` geht raus), Jetzt/Gleich/Vorschau sichtbar | `LiveTvE2ETests.LiveTv_ViaNavMenu_ShowsPlayerAndProgram` | AK 1, 5, 6, 7, 8 | Gesamter Benutzerfluss: Navigation → Seite → API → Player-JS (hls.js-Pfad im Chromium) → Stream-Request |
| Pflicht | Sendelücke: kein laufender Slot → verständliche Meldung, Vorschau/„Gleich" bleiben sichtbar; danach geseedeter laufender Slot → Seite zeigt Sendung ohne manuellen Reload | `LiveTvE2ETests.LiveTv_ProgramGap_ShowsFriendlyHint_RecoversOnSlotStart` | AK 9, 10 | Sichtbarer Fehler-/Leerzustand + zeitgesteuerte Aktualisierung über die echte UI |

Angepasste bestehende E2E-Tests: Keine.

## Offene Punkte

Keine.
