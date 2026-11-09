# Übersetzte Anforderung: Live-TV in der Benutzeroberfläche (Schritt 7)

Quelle: `docs/projects/task/issue-111-6145c51699f34953bf29a244634a23e2-automatisierter-tvsender/project-plan.md`, Abschnitt „### Schritt 7: Live-TV in der Benutzeroberfläche" + Grobe Vorgehensentscheidungen (Begriffswelt, Stream-Charakter, Zugriffsregelung).

## Fachliche Zusammenfassung

Benutzer können den automatisierten TV-Sender direkt in der Anwendung ansehen. Eine neue Live-Ansicht enthält einen Videoplayer, der den in Schritt 6 ausgelieferten HLS-Live-Stream (`GET /api/live-stream/live.m3u8?access_token={token}`) wiedergibt — in Browsern mit nativer HLS-Unterstützung direkt über das `<video>`-Element, in übrigen Browsern (Chrome, Firefox, Edge) über die Ergänzungsbibliothek hls.js, die als neue Client-Abhängigkeit eingebunden wird (inkl. Lizenz-, Release-Build- und CI-Prüfung). Die Ansicht zeigt zusätzlich an, welche Sendung gerade läuft und welche als Nächstes kommt (`GET /api/program-guide/now`), und stellt eine begrenzte Programmvorschau der nächsten Stunden dar (`GET /api/program-guide/upcoming?hours=`); der vollständige Sendeplan bleibt der Administration vorbehalten. Die Ansicht ist über die Hauptnavigation erreichbar. Benutzer ohne Zugriff bzw. bei nicht verfügbarem Stream (Sendelücke, kein Sender eingerichtet) erhalten eine verständliche Rückmeldung statt eines technischen Fehlerbilds.

## Akzeptanzkriterien

1. **Live-Ansicht mit Videoplayer:** Eine neue Seite (eigene Route) zeigt einen Videoplayer, der den Live-Stream wiedergibt. Alle Zuschauer sehen zeitgleich denselben Programmpunkt — kein Timeshift, keine Positionssteuerung im Sinne von Vorspulen auf ältere Inhalte (natives HLS-Verhalten: Wiedergabe am Live-Rand).
2. **Native HLS-Wiedergabe wo möglich:** In Browsern mit nativer HLS-Unterstützung (`canPlayType("application/vnd.apple.mpegurl")`, z. B. Safari) wird die `.m3u8`-URL direkt als `src` des `<video>`-Elements gesetzt — ohne hls.js.
3. **hls.js als Fallback:** In Browsern ohne native HLS-Unterstützung aber mit Media Source Extensions (Chrome, Firefox, Edge) wird hls.js eingesetzt (`Hls.isSupported()`). Ist weder native Wiedergabe noch MSE verfügbar, erhält der Anwender eine verständliche Rückmeldung statt eines leeren Players.
4. **hls.js-Abhängigkeit sauber eingebunden:** Die neue Client-Abhängigkeit hls.js (Apache-2.0) ist versionsfest eingebunden; die Einbindung folgt den Projekt-Asset-Mustern (statische Assets unter `wwwroot`, Auslieferung über `dotnet publish`), die Lizenz ist geprüft und dokumentiert, und Auswirkungen auf Release-Build und CI sind dokumentiert bzw. angepasst, soweit erforderlich.
5. **Authentifizierter Stream-Abruf:** Die Wiedergabeliste wird mit dem `access_token`-Abfrageparameter aufgerufen (`/api/live-stream/live.m3u8?access_token={Client.AuthorizationToken}`) — das in Schritt 6 etablierte Muster; der Token kommt aus `VideoWebPlayerClient.AuthorizationToken` nach `EnsureAuthorizationToken(Silently)Async`. Der Server schreibt die Segment-URIs bereits tokenbehaftet um.
6. **„Was läuft" + „Was kommt gleich":** Die Ansicht zeigt die aktuell laufende Sendung (Titel, Sendezeit, ggf. Fortschritt/Restdauer aus `PositionSeconds`/`RemainingSeconds`) und die nächste Sendung — Datenquelle `GET /api/program-guide/now` über das `IPlaylistApiClient`-Muster (neuer Program-Guide-API-Client).
7. **Begrenzte Programmvorschau:** Die Ansicht zeigt eine Liste der kommenden Sendungen der nächsten Stunden (`GET /api/program-guide/upcoming`, Stundenfenster innerhalb `ProgramGuideService.MaxPreviewHours`); der vollständige Sendeplan bleibt `/admin/tv-sender` vorbehalten.
8. **Navigation:** Die Live-Ansicht ist über die Hauptnavigation (`NavMenu`) für angemeldete Benutzer erreichbar — ein eigener, verständlich beschrifteter Eintrag (Sender-Vokabular, z. B. „Live-TV").
9. **Verständliche Rückmeldungen statt technischem Fehlerbild:**
   - Nicht angemeldete/ohne Zugriff: verständliche Meldung bzw. Weiterleitung zum Login, kein unbehandelter 401/403-Text.
   - Sendelücke bzw. kein laufender Stream (m3u8 404): verständliche Meldung wie „Aktuell läuft keine Sendung" statt leerem Player/Fehlerkonsole; die Anzeige des nächsten Programmpunkts bleibt sichtbar.
   - Stream-Fehler während der Wiedergabe: verständliche Meldung (Muster `MediaAccessErrorMessages`-Ansatz), ggf. mit erneutem Ladeversuch.
10. **Aktualität der Anzeige:** Die Programmanzeige aktualisiert sich ohne manuelles Neuladen in sinnvollen Abständen (zeitgesteuertes Polling der Programmauskunft bzw. Re-Laden am Sendeplatz-Übergang), sodass „Jetzt/Gleich"-Angaben nicht veralten.
11. **Berechtigungen serverseitig:** Die Ansicht selbst prüft die Anmeldung (Muster bestehender Seiten); alle Daten kommen über die bereits serverseitig abgesicherten Endpunkte — keine neuen Berechtigungsannahmen nur in der UI.
12. **Kein Datenbankschema-Zwang:** Der Schritt ist reine Oberfläche/Client-Integration — neue Tabellen/Spalten nur, falls fachlich nötig (nicht erwartet); dann aber Migration + Backup-Registrierung wie üblich.
13. **Namensabgrenzung:** Sender-Vokabular bleibt konsistent (Sender/Live-TV/Sendung/Sendeplatz) — kein „Playlist"-Naming für die Live-Ansicht.

## Betroffene Klassen und Komponenten

- **Neu:** Live-TV-Seite (`Components/Pages/...`, eigene Route z. B. `/live-tv` oder `/tv`) mit `@rendermode InteractiveServer`: Videobereich, „Jetzt/Gleich"-Anzeige, Programmvorschau, Fehlerzustände.
- **Neu:** JS-Modul unter `wwwroot/js/` für die Player-Anbindung: native HLS-Erkennung, hls.js-Initialisierung/-Abbau, Fehler-Callback an Blazor (Muster `continueWatching.js`/`window.*`-Interop).
- **Neu:** hls.js als vendored Asset unter `wwwroot` (Muster `wwwroot/bootstrap/`), inkl. Lizenz-/Versionsvermerk; Einbindung in `App.razor` über `StaticAssetVersioner.Url(...)` wie die übrigen Skripte.
- **Neu:** `IProgramGuideApiClient`-Interface + `VideoWebPlayerClient.ProgramGuide.cs`-Partial (`api/program-guide/now`, `api/program-guide/upcoming?hours=`) nach `IPlaylistApiClient`/`VideoWebPlayerClient.Playlists.cs`-Muster; DI-Registrierung analog `IPlaylistApiClient`.
- **Geändert:** `Components/Layout/NavMenu.razor` — neuer Eintrag im `<Authorized>`-Bereich.
- **Bestehend (genutzt):** `VideoWebPlayerClient.AuthorizationToken` + `EnsureAuthorizationTokenSilentlyAsync` (Token für `access_token`), `DtoProgramGuideNow`/`DtoProgramGuideEntry`, `MediaAccessErrorMessages`-Ansatz für Fehlertexte, `AuthenticationStateProvider` für Anmeldeprüfung, `StaticAssetVersioner` für versionierte Script-URLs.
- **Tests:** bUnit-Komponententests der neuen Seite (Anzeige Jetzt/Gleich/Vorschau, Fehlerzustände, Navigationspunkt), Client-Tests für den Program-Guide-API-Client, Playwright-E2E (`PlaylistsE2ETestBase`-Konvention, konsolidiert) für den Live-TV-Benutzerfluss inkl. Fehlerfällen.

## Implementierungsansatz

- Neue Seite nach dem Muster bestehender Seiten (`@page`, `@rendermode InteractiveServer`, `AuthenticationStateProvider` für Anmeldezustand). Datenabruf über den neuen `IProgramGuideApiClient` (serverseitig abgesicherte HTTP-Endpunkte, Token-Erneuerung durch `InternalVideoWebPlayerClient`-Infrastruktur) — kein direkter Service-Zugriff, damit die UI denselben abgesicherten Pfad nutzt wie Geräte.
- Videobereich: `<video>`-Element ohne `src`-Direktbindung; ein JS-Modul (Interop wie `window.continueWatching`) entscheidet `video.canPlayType(...)` → nativ, sonst `Hls.isSupported()` → `new Hls()` + `loadSource(url)` + `attachMedia(video)`; sonst verständliche Meldung. hls.js-Fehler-Events werden an Blazor zurückgemeldet (DotNetObjectReference-Callback-Muster aus `videoPlayer.registerTimeUpdate`), damit die Seite die verständliche Rückmeldung zeigt.
- Stream-URL: `/api/live-stream/live.m3u8?access_token={Client.AuthorizationToken}` (Token zuvor per `EnsureAuthorizationTokenSilentlyAsync` sichergestellt — Muster `TVSenderAdmin`/`NavMenu`).
- Programmanzeige: initiales Laden von `now` + `upcoming` beim Seitenaufruf; Aktualisierung per `System.Threading.Timer`/`PeriodicTimer` im Component-Scope (Dispose-Muster) — kurzes Intervall für „Jetzt/Gleich", Vorschau zusammen mit.
- Sendelücke/kein Stream: `now.Now == null` bzw. Ladefehler der Wiedergabeliste (404) → Player-Bereich zeigt verständliche Hinweismeldung; „Was kommt gleich"/Vorschau bleibt sichtbar.
- hls.js-Bereitstellung: vendored `hls.min.js` (versionsfest, aus npm-Paket `hls.js` dist) unter `wwwroot` — analog `wwwroot/bootstrap/`; Lizenz (Apache-2.0) und Bezugsweg in Hilfe/README dokumentieren; `dotnet publish` nimmt `wwwroot` automatisch mit, daher keine CI-/Build-Pipeline-Änderung nötig (Prüfung dokumentieren).

## Konfiguration

Keine neuen Konfigurationseinträge erforderlich — Vorschau-Stundenzahl als Konstante der Seite (innerhalb `MaxPreviewHours = 12`, Standard-Abruf 6 h wie API-Default); Aktualisierungsintervall als Konstante.

## Offene Fragen

Keine — Wiedergabeweg (nativ + hls.js-Fallback), Datenquellen (`api/program-guide`, `api/live-stream`), Token-Muster (`access_token`), Navigationsort, Berechtigungsprüfung und Fehlerrückmeldungen sind durch die Vorgehensentscheidungen, fertige Schritte 5/6 und bestehende UI-Muster festgelegt. Die Bereitstellungsform der hls.js-Abhängigkeit (vendored Asset statt npm-Buildschritt) folgt dem bestehenden `wwwroot/bootstrap`-Muster, da der Build keine npm-Phase für die Anwendung kennt.
