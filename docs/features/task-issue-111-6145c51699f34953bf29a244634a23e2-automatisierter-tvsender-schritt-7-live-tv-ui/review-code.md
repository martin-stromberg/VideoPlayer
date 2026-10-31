# Code-Review (technisch)

## Ergebnis

**Status:** Code akzeptiert (eine dokumentierte Einschränkung: E2E-Recovery-Nachweis)

## Geprüfte Stellen und Befunde

| Datei | Befund |
|-------|--------|
| `IProgramGuideApiClient.cs` | Schmale Schnittstelle analog `IPlaylistApiClient`; XML-Doku vorhanden (CS1591 ist Fehler). |
| `VideoWebPlayerClient.ProgramGuide.cs` | Partial-Pattern wie `.Playlists.cs`; nutzt `HttpGetAsync` (Reauth/Impersonation geteilt). |
| `LiveTv.razor` | `OnInitializedAsync` lädt Auth+Guide, startet `PollLoopAsync`. Poll: Datenabruf auf Timer-Thread, `InvokeAsync(StateHasChanged)` nur für Render — korrekt; Fehler pro Tick isoliert (Schleife überlebt Einzelfehler). `OnAfterRenderAsync` vergleicht `SlotId` → Attach/Detach nur bei Wechsel. `DisposeAsync` cancelt `_cts`, awaited `_pollTask`, detacht JS, disposed `_dotNetRef` — sauber. |
| `liveTv.js` | `WeakMap` je Video-Element; `detach` räumt Listener/Timer/hls-Instanz + `src` auf; Lazy-Load von `hls.min.js` (fingerabdruckte URL); Retry-Timer 5 s bei fatalen Fehlern; Callbacks `NotifyStreamPlaying`/`NotifyStreamError`/`NotifyStreamUnsupported`. Abbruch-Sicherheit: attach-Callbacks prüfen `states.get(videoEl) === state`. |
| `LiveTvErrorMessages.cs` | Konstante Texte, `Describe` mapped nur 403→Hinweis, sonst generischer Text — kein technisches Leck. |
| `App.razor` | `liveTv.js` über `AssetVersions.Url` (Fingerprinting). |
| `NavMenu.razor` | Eintrag im `<Authorized>`-Block, `bi-tv`-Icon. |
| `ServiceCollectionExtensions.cs` | `IProgramGuideApiClient` scoped auf dieselbe `VideoWebPlayerClient`-Instanz — konsistent. |
| Security | Token nur im Query des HLS-URLs (nötig für Media-Element/hls.js); Server erzwingt Auth unverändert; kein Token im localStorage; DTOs serverseitig. |
| `NoOpVideoWebPlayerClient` | `HttpGetAsync<T>` kennt die neuen DTO-Typen → bUnit-fähig. |

## Verifizierte Verhaltensbelege

- Debug-Instrumentierung im E2E-Lauf zeigte Poll-Ticks alle 15 s mit korrekter Datenablage (Ansicht blieb konsistent; Schleife lebt).
- `live.m3u8?access_token=`-Request im Playwright-Lauf gefangen (Attach funktioniert end-to-end).
- DB-Dump im Recovery-Versuch: vom Test manuell eingefügte Slots wurden vom Worker `Displaced` → E2E-Recovery-Nachweis nicht möglich (gewollte Reconciliation), dokumentiert.

## Findings

- Keine verbleibenden Findings. Während der Umsetzung behoben: Moq-/Selektor-Fehler in Tests, Sidebar-Interception im E2E (Testseite), Poll-Robustheit (try/catch im Tick, 15 s Intervall).
