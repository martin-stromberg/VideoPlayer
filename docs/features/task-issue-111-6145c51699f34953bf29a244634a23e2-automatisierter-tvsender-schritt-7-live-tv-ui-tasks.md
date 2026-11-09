# Tasks: Live-TV in der Benutzeroberfläche (Schritt 7)

| # | Bereich | Aufgabe | Status | Testnachweis |
|---|---------|---------|--------|--------------|
| 1 | Assets | `wwwroot/js/vendor/hls.min.js` vendoren (hls.js 1.7.3, Apache-2.0) | Offen | — |
| 2 | Assets | `wwwroot/js/vendor/hls.js.LICENSE.txt` anlegen (Lizenztext aus npm-Paket) | Offen | — |
| 3 | Client-API | `IProgramGuideApiClient` in `VideoWebPlayer.Client` anlegen (`RequestProgramGuideNowAsync`, `RequestProgramGuideUpcomingAsync`) | Offen | — |
| 4 | Client-API | `VideoWebPlayerClient.ProgramGuide.cs` Partial mit Implementierung anlegen | Offen | — |
| 5 | Client-API | `VideoWebPlayerClient.cs`: `IProgramGuideApiClient` zur Interface-Liste hinzufügen | Offen | — |
| 6 | DI | `IProgramGuideApiClient`-Registrierung in `ServiceCollectionExtensions` | Offen | — |
| 7 | JS | `wwwroot/js/liveTv.js` (`window.liveTv`: attach/detach, native HLS/hls.js-Fallback, Callbacks, Re-Attach-Timer, Lazy-Load) | Offen | — |
| 8 | JS | `liveTv.js` in `App.razor` via `AssetVersions.Url` einbinden | Offen | — |
| 9 | UI | `LiveTvErrorMessages` in `ViewModels/` anlegen | Offen | — |
| 10 | UI | `Components/Pages/LiveTv.razor` anlegen (Route `/live-tv`, Zustandsmaschine, Player, Jetzt/Gleich, Vorschau, Timer, JS-Interop, Dispose) | Offen | — |
| 11 | UI | `Components/Pages/LiveTv.razor.css` Scoped-Styles anlegen | Offen | — |
| 12 | Navigation | `NavMenu.razor`: Eintrag „Live-TV" (`/live-tv`, `bi-tv`) im Authorized-Bereich | Offen | — |
| 13 | Tests | `LiveTvTests` (bUnit): Jetzt/Gleich/Vorschau, Sendelücke, Login-Form, Ladefehler, Unsupported, StreamError, attach-Interop | Offen | — |
| 14 | Tests | NavMenu-Test für `/live-tv`-Eintrag | Offen | — |
| 15 | Tests | `VideoWebPlayerClientTests_ProgramGuide` (`DeviceClientTestBase`): now/upcoming über Client | Offen | — |
| 16 | Tests | `LiveTvE2ETests` (Playwright): Nav→Seite→Player-m3u8-Request+Anzeige; Sendelücke→Hinweis→Erholung | Offen | — |
| 17 | Tests | `NoOpVideoWebPlayerClient.HttpGetAsync<T>` um Program-Guide-DTOs erweitern | Offen | — |
| 18 | Doku | `docs/help/tv-sender.md`: Live-TV-Ansicht + hls.js-Abhängigkeit (Lizenz/Bezug) | Offen | — |
| 19 | Doku | `docs/API.md`: Hinweis UI-Nutzung der Endpunkte (falls passend) | Offen | — |
| 20 | Doku | `README.md`, `docs/RELEASE_NOTES.md`, `changes.log` aktualisieren | Offen | — |
