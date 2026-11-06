# Review (Planumsetzung)

Geprüft wurde die tatsächliche Implementierung gegen `plan.md` und `requirement.md`, nicht nur die
Plandokumente.

## Ergebnis

**Status:** Plan erfüllt (mit dokumentierter Einschränkung bei E2E-Recovery, siehe unten)

## Abgleich Umsetzung ↔ Plan

| Planpunkt | Umsetzung | Status |
|-----------|-----------|--------|
| Seite `/live-tv` mit Player, Jetzt/Gleich, begrenzter Vorschau | `VideoWebPlayer/Components/Pages/LiveTv.razor` (+ `.razor.css`): Video nur bei laufender Sendung, Jetzt-Karte mit Fortschritt, Gleich-Karte, Vorschau-Liste ≤ 6 h | Erfüllt |
| `IProgramGuideApiClient` nach `IPlaylistApiClient`-Muster | `VideoWebPlayer.Client/IProgramGuideApiClient.cs` + `VideoWebPlayerClient.ProgramGuide.cs` (partial, nutzt `HttpGetAsync`), Registrierung in `ServiceCollectionExtensions.cs` | Erfüllt |
| Authentifizierter HLS-Abruf mit `access_token` | `StreamUrl` aus `Client.AuthorizationToken` nach `EnsureAuthorizationTokenSilentlyAsync`; URL `/api/live-stream/live.m3u8?access_token=` | Erfüllt (E2E: Request mit `access_token` gefangen) |
| Native HLS zuerst, hls.js-Fallback | `wwwroot/js/liveTv.js`: `canPlayType('application/vnd.apple.mpegurl')` → natives `src`; sonst Lazy-Load von `hls.min.js` + `Hls.isSupported()`; `NotifyStreamUnsupported` ohne beides | Erfüllt |
| hls.js vendored + Lizenz | `wwwroot/js/vendor/hls.min.js` (1.7.3, Apache-2.0) + `hls.js.LICENSE.txt`; kein Pipeline-Eingriff | Erfüllt |
| Verständliche Zustände | `ViewModels/LiveTvErrorMessages.cs`; `LoginForm` bei Unauthentifiziert, `Ladefehler`/`403`-Text, Sendelücken-Hinweis, Stream-Hinweis mit Retry-Text, Browser-Hinweis | Erfüllt |
| Aktualität ohne Reload | `PeriodicTimer` 15 s: Datenabruf im Hintergrund, `InvokeAsync(StateHasChanged)` zum Rendern, Fehler pro Tick isoliert; Re-Attach bei Slot-Wechsel in `OnAfterRenderAsync`; JS-seitiger Re-Attach bei fatalen Fehlern | Erfüllt |
| Navigation | `NavMenu.razor`: Eintrag „Live-TV" im `<Authorized>`-Block | Erfüllt |
| Serverseitige Berechtigung | unverändert `[BearerTokenCheck]`/`CheckLoggedIn` | Erfüllt |
| Keine Migration | keine | Erfüllt |

## Tests

- bUnit `LiveTvTests` (7): Jetzt/Gleich/Vorschau + Attach-Aufruf mit `access_token`, Sendelücke ohne Attach, LoginForm unauthentifiziert ohne API-Aufruf, Ladefehler, 403-Text, Unsupported-/Stream-Hinweise.
- `NavMenuLiveTvTests` (1), `VideoWebPlayerClientTests_ProgramGuide` (2, gepaarter Client gegen gehostete App).
- Playwright `LiveTvE2ETests` (2): Nav-Pfad → Player + Programmanzeige + `live.m3u8?access_token`-Request; Sendelücke mit freundlichem Hinweis + Vorschau + kein Video.
- **Einschränkung:** Der „Wiederanlauf bei Slot-Beginn ohne Reload" ist E2E nicht nachweisbar, weil `ProgramScheduleService.ReconcileAsync` regellose, manuell geschriebene `ProgramSlot`s als `Displaced` markiert (im Lauf verifiziert: `st=Displaced` im DB-Dump; die Poll-Ticks liefen korrekt alle 15 s). Kein Produkt-Bug — die Reconciliation ist gewollt. Abgedeckt bleibt der Mechanismus durch die bUnit-Zustandsübergänge und den Attach-Nachweis. Kommentar steht in `LiveTvE2ETests`.

## Befunde der Iteration

1. `TVSenderAdminE2ETests`-Baseline-Fehlschlag: Einzelwiederholung grün → Flake (dokumentiert in `inventory/tests.md`).
2. bUnit-Selektor-Tippfehler (`.live-tv-upcoming-row` vs. `.livetv-upcoming-row`) — korrigiert.
3. E2E Nav-Klick von eingeklapptem Sidebar abgefangen → Test klappt Sidebar vorher auf (realer Bedienweg).
4. Recovery-E2E: `ReconcileAsync` verwirft manuelle Slots → Test auf Gap-Szenario reduziert (s. o.).

**Status:** Plan erfüllt
