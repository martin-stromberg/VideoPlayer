# Bestandsaufnahme: Live-TV in der Benutzeroberfläche (Schritt 7)

Analyse des Bestands für die neue Live-TV-Ansicht: Player, Programmanzeige, Navigation, Client-API-Anbindung, Fehlertexte, Skript-/Asset-Muster und Testinfrastruktur.

## Zusammenfassung

- **Stream-Auslieferung fertig:** `LiveStreamController` liefert `GET /api/live-stream/{fileName}` — die `.m3u8`-Wiedergabeliste wird beim Abruf so umgeschrieben, dass jede Segment-URI den übergebenen `access_token` trägt (`no-store`); `[BearerTokenCheck]` + `CheckLoggedIn`, 404 inkl. Sendelücke.
- **Programmauskunft fertig:** `ProgramGuideController` (`api/program-guide/now` + `upcoming?hours=`) liefert `DtoProgramGuideNow`/`DtoProgramGuideEntry` (DTOs existieren bereits in `VideoWebPlayer.Client.Models`), serverseitig abgesichert, Vorschau-Limit `ProgramGuideService.MaxPreviewHours = 12`.
- **Fehlt:** Jede UI für Live-TV — keine Seite, kein Navigationspunkt, kein HLS-fähiger Player (`VideoPlayer.razor` ist ein Overlay-Player für progressive Dateistreams, kein HLS), kein Program-Guide-API-Client (`VideoWebPlayerClient` hat keine `program-guide`-Methoden), kein hls.js (keinerlei JS-Abhängigkeiten im Repo — `package.json` dient ausschließlich semantic-release).
- **Wiederverwendbare Muster:** `Client.AuthorizationToken` + `EnsureAuthorizationTokenSilentlyAsync` für `access_token`-URLs (NavMenu, TVSenderAdmin); `IPlaylistApiClient` + `VideoWebPlayerClient.Playlists.cs`-Partial als API-Client-Vorbild; `window.*`-JS-Interop-Module unter `wwwroot/js/` + `StaticAssetVersioner.Url()` in `App.razor`; vendored Third-Party-Assets unter `wwwroot/bootstrap/` (direkter Präzedenzfall für vendored hls.js); `MediaAccessErrorMessages` für verständliche Fehlertexte; Anmeldeprüfung je Seite via `AuthenticationStateProvider` (kein globales `[Authorize]`).
- **Auffälligkeit im Bestand (kein Befund dieses Schritts, nur Feststellung):** `VideoPlayer.razor` ruft `videoPlayer.*`-JS-Interop-Funktionen auf, die in keiner `wwwroot`-Datei definiert sind (`window.videoPlayer` existiert nicht im Repo).
- **Test-Ausgangszustand:** siehe [Tests](inventory/tests.md) — Baseline-Lauf dokumentiert.

## Details

- [Logik / Services / Controller / Client](inventory/logic.md)
- [Datenmodell](inventory/models.md)
- [UI-Struktur und Muster](inventory/ui.md)
- [Konfiguration, Assets und Externe Abhängigkeiten](inventory/config.md)
- [Tests](inventory/tests.md)
