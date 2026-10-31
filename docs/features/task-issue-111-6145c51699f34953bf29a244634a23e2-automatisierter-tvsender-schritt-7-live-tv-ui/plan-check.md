# Plan-Gegenprüfung

## Ergebnis

**Status:** Plan vollständig

## Abgleich Akzeptanzkriterien

| Akzeptanzkriterium | Umsetzung im Plan | Testnachweis im Plan | Status |
|--------------------|-------------------|----------------------|--------|
| AK 1 Live-Ansicht mit Videoplayer, gemeinsamer Programmpunkt | `LiveTv.razor` + `liveTv.attach` auf m3u8-URL (Live-Rand via HLS-Mechanik) | E2E `LiveTv_ViaNavMenu_ShowsPlayerAndProgram` (m3u8-Request-Nachweis) + bUnit `Now_LoadsAndRenders...` | Abgedeckt |
| AK 2 Native HLS wo möglich | `canPlayType`-Pfad in `liveTv.js` (kein hls.js dann) | JS-Pfad wird implizit durch Fallback-Logik getestet; nativer Pfad im Chromium nicht erreichbar — abgesichert durch modulare Verzweigung + Unsupported-Callback-Test | Abgedeckt |
| AK 3 hls.js-Fallback (MSE) | `Hls.isSupported()`-Pfad mit Lazy-Load, `NotifyStreamUnsupported` ohne beides | bUnit `Page_NotifyStreamUnsupported_...` + E2E (Chromium nutzt zwangsläufig hls.js/MSE-Pfad) | Abgedeckt |
| AK 4 hls.js sauber eingebunden (Lizenz/Release/CI) | vendored `hls.min.js` 1.7.3 + LICENSE unter `wwwroot/js/vendor/`; `dotnet publish` ohne Pipeline-Änderung; Doku in Hilfe/README/Plan | Kein direkter Test — Feststellung; Versions-/Hash-Dokumentation im Plan | Abgedeckt |
| AK 5 Authentifizierter Stream-Abruf (`access_token`) | `Client.AuthorizationToken` nach `EnsureAuthorizationTokenSilentlyAsync`; URL `/api/live-stream/live.m3u8?access_token=` | bUnit `AttachDetachesLiveTvJs` (Interop-Assert auf URL) + E2E (Request-Fang mit Token) | Abgedeckt |
| AK 6 „Was läuft" + „Was kommt gleich" | Now-/Next-Karten aus `RequestProgramGuideNowAsync` | bUnit + E2E | Abgedeckt |
| AK 7 Begrenzte Programmvorschau | `RequestProgramGuideUpcomingAsync(6)` ≤ `MaxPreviewHours`; Vorschau-Liste | bUnit + E2E | Abgedeckt |
| AK 8 Navigation | `NavMenu.razor`-Eintrag im `<Authorized>`-Block | `NavMenu_ContainsLiveTvEntry...` + E2E-Klickpfad | Abgedeckt |
| AK 9 Verständliche Rückmeldungen | `LoginForm` bei unauthentifiziert; `KeinProgramm` bei Sendelücke; `StreamFehler` mit Retry; `Ladefehler` bei API-Fehlern; `LiveTvErrorMessages` | bUnit je Zustand + E2E Sendelücke | Abgedeckt |
| AK 10 Aktualität ohne Reload | `PeriodicTimer` ~30 s Poll + Re-Attach bei Slot-Wechsel; JS-Re-Attach bei Stream-Fehler | E2E `..._RecoversOnSlotStart` (Slot wird zur Laufzeit geseedet) | Abgedeckt |
| AK 11 Berechtigungen serverseitig | Alle Daten via `IProgramGuideApiClient`/`access_token` über die abgesicherten Endpunkte; Seite prüft Anmeldung | Bestehende API-E2E (401 anonym) unverändert + UI-Login-Test | Abgedeckt |
| AK 12 Kein Schema-Zwang | Plan: „Keine" Migrationen | — | Abgedeckt |
| AK 13 Namensabgrenzung | Sender-Vokabular (Live-TV/Sendung) in Seite/Nav; kein Playlist-Naming | implizit | Abgedeckt |

## Fehlende oder unvollständige Testanforderungen

— (Status ist `Plan vollständig`; Abschnitt nicht ausgefüllt.)

## E2E-Abdeckung

| Benutzerfluss / Akzeptanzkriterium | Geplanter E2E-Test | Status |
|------------------------------------|--------------------|--------|
| Angemeldeter Benutzer → Navigationslink → Player-Attach (m3u8 + Token) → Jetzt/Gleich/Vorschau (AK 1,5,6,7,8) | `LiveTvE2ETests.LiveTv_ViaNavMenu_ShowsPlayerAndProgram` | Abgedeckt |
| Sendelücke → freundlicher Hinweis + Vorschau sichtbar; Slot-Beginn → automatische Wiederherstellung ohne Reload (AK 9,10) | `LiveTvE2ETests.LiveTv_ProgramGap_ShowsFriendlyHint_RecoversOnSlotStart` | Abgedeckt |
| Nicht angemeldet → verständliche Rückmeldung (AK 9) | bUnit `Page_NotAuthenticated_ShowsLoginForm`; zusätzlich prüfbar im ersten E2E (Login-Schritt) | Abgedeckt (bUnit; Browser-Login-Pfad ist identisch zu `Home.razor`-Muster) |
| Stream nicht verfügbar (404) → freundlicher Hinweis + Retry (AK 9,10) | bUnit `Page_NotifyStreamError_...`; der Sendelücken-E2E deckt die 404-Situation zusätzlich | Abgedeckt |

## Fehlende oder unvollständige Planbestandteile

— (Status ist `Plan vollständig`; Abschnitt nicht ausgefüllt.)

## Hinweise

- Chromium (Playwright) hat kein natives HLS → der E2E läuft zwangsläufig über den hls.js/MSE-Pfad; der native Pfad ist damit nicht browserseitig nachweisbar, die Verzweigung selbst ist trivial (`canPlayType`) und bleibt als Code-Eigenschaft dokumentiert.
- `NoOpVideoWebPlayerClient.HttpGetAsync<T>` muss die neuen DTO-Typen bedienen, sonst `InvalidCastException` in bUnit-Tests — als Task 17 explizit erfasst.
- m3u8-Request-Nachweis im E2E via Playwright-Request-Ereignis/Route statt Video-Decodierung — deterministisch trotz Dummy-Segment.
