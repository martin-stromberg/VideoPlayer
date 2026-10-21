# Abnahme Schritt 14 — Remote-Laufzeit-Probes über internen HTTP-Range-Endpunkt (N9 + N8-Nachtrag)

Prüfer: Devin (Sub-Agent, nicht der Implementierer). Branch:
`task-issue-111-6145c51699f34953bf29a244634a23e2-automatisierter-tvsender-schritt-14-http-probe`,
HEAD `92b21bb`, Arbeitsstand sauber committed.

## Ergebnis

**Status:** Abweichungen gefunden

Die Kernfunktion ist korrekt implementiert und nachgewiesen: eigener Range-fähiger
Endpunkt `GET /api/internal/media-stream/{token}` (`InternalMediaStreamController.cs`),
256-Bit-In-Memory-Token mit TTL (`InternalMediaStreamTokenService.cs`), Loopback-Auflösung
über `IServerAddressesFeature`, Fallback-Kette HTTP → `sftp://` → `pipe:0`, echte
Kestrel+ffprobe-Integrationstests. Drei inhaltliche Abweichungen (s. u.) betreffen
Log-Sicherheit und Robustheit der Fallback-Logik.

## Verifiziert

- **Build:** `dotnet build VideoPlayer.sln` Debug **und** Release — jeweils 0 Fehler,
  142 Warnungen (identischer Bestand wie Schritt 12/13, keine Warnung aus den neuen Dateien).
- **Tests:** `dotnet test VideoWebPlayer.Tests/VideoWebPlayer.Tests.csproj` komplett synchron:
  **2092 bestanden / 0 fehlgeschlagen / 1 übersprungen** (2093 gesamt, bekannter Alt-Skip
  `PairedDevice_ReportsProgressForMovieWithoutCollection_ContinueWatchingListStillLoads`).
  Dauer ~4:45 min. `ffprobe`/`ffmpeg` 9.0.2 lagen auf `PATH` — `MediaProbeHttpIntegrationTests`
  lief also wirklich mit echtem Kestrel + echtem ffprobe (nicht still übersprungen).
- **Format:** `dotnet format VideoPlayer.sln --verify-no-changes --severity error` sauber.
- **Gegenprobe:** `enableRangeProcessing: true` → `false` in
  `InternalMediaStreamController.cs:84` gesetzt → `Stream_ValidToken_RangeRequest_Returns206PartialContent`
  **und** `InternalStream_RangeRequest_ReturnsPartialContent` (echter Kestrel) schlagen rot
  (Expected 206, Actual 200). Revertiert via `git checkout --`, Arbeitsbaum wieder sauber.
- **Diff-Review** der Kerndateien: `InternalMediaStreamController`,
  `InternalMediaStreamTokenService`, `FfprobeMediaDurationProbe`, `MediaDurationProbeService`,
  `MediaProbeOptions`, `SftpProbeUriBuilder`, `MediaFileContentTypes`, `ItemsController`,
  `ServiceCollectionExtensions`, `appsettings.json` + alle fünf Testdateien.

**Fachlich geprüft und ok:**

- Token aus `RandomNumberGenerator.GetBytes(32)` (CSPRNG), 64-Hex, In-Memory-`ConcurrentDictionary`,
  TTL `HttpProbeTokenLifetimeSeconds` (Default 600 s, getestet mit `ManualTimeProvider`),
  je Token genau ein `MediaItemId` — pro-Datei-gescoped.
- Ungültiger/unbekannter/abgelaufener Token → 404 (verrät keine Existenz, getestet).
- Range-Request → 206 mit korrekten Bytes (`File(..., enableRangeProcessing: true)`; getestet
  auf Controller- und Kestrel-Ebene).
- Kein Path-Traversal: Token ist nur ein Dictionary-Schlüssel; der Dateiname kommt aus der DB
  (`Path.GetFileName(mediaItem.Path)`) und läuft durch dieselbe Root-gebundene Auflösung wie
  die Wiedergabe (`LocalMediaSourceReader.ResolveFilePath` lehnt Pfade außerhalb des Roots ab;
  SFTP kombiniert `collection.Path` + Dateiname — identisch zu `ItemsController.OpenMediaFileAsync`).
- Wildcard-Adressen `*`/`+`/`0.0.0.0`/`::`/`localhost`/`::1` → `127.0.0.1` (Theorie mit 7 Fällen),
  HTTPS-only → `null` → automatischer Fallback; `HttpProbeEnabled=false` → `null` → saubere
  Kaskade zu sftp/pipe (getestet).
- Fallback-Kette in `ProbeRemoteItemAsync` (MediaDurationProbeService.cs:274-309): HTTP zuerst,
  bei `UriUnsupported` weiter zu `sftp://` (nur Sftp-Quellen, nur bei bau barer URL), dann
  `pipe:0`. Transienter HTTP-Fehler kaskadiert **nicht** innerhalb desselben Versuchs
  (bewusst — verbrennt kein zusätzliches 600-s-pipe-Fenster; Test
  `HttpProbeTransient_DoesNotCascadeToPipe`). Permanente Strukturfehler über HTTP (`moov atom
  not found`) bleiben permanent — korrekt, kein Fehl-Fallback.
- HTTP-Transportfehler-Marker (`HTTP error`, `Server returned`, `Connection refused/timed out`,
  `No route to host`, `Failed to connect`) nur für `http(s)`-Eingaben als `UriUnsupported`
  klassifiziert; `Connection refused` auf `sftp://` bleibt transient (Gegenprobe-Test).
- Kein Benutzerkonto/langlebiges Geheimnis in der ffprobe-Kommandozeile — nur das kurzlebige
  Token (dessen Prozesslisten-Sichtbarkeit ist die dokumentierte Designentscheidung analog N8).
- Keine DB-Schemaänderung → keine Migration/Backup-Registrierung nötig. Hilfe
  (`docs/help/videolaufzeit.md`) + `RELEASE_NOTES.md` (EN+DE) aktualisiert.

## Abweichungen

- [ ] **N9-Prüfpunkt „Token nie in Logs unmaskiert" verletzt — HTTP-Probe-Token erscheint
  im Klartext in Logdateien.** `FfprobeMediaDurationProbe.ProbeUriAsync` nutzt für
  `displayInput` und die stderr-Sanitisierung `SftpProbeUriBuilder.MaskCredentials(InText)`,
  das nur den Userinfo-Teil (`scheme://user:pass@`) maskiert (SftpProbeUriBuilder.cs:20-21,89-90).
  Das Stream-Token steht aber im URI-**Pfad** (`/api/internal/media-stream/{64-hex}`) und wird
  nicht maskiert: Jede Fehlermeldung in `ProbeAsync` (FfprobeMediaDurationProbe.cs:163,169,183,
  190,198,204,213) loggt Timeout/Transportfehler/Exit≠0/fehlende Dauer inkl. vollständigem Token,
  zusätzlich echot ffprobe die URI in stderr (dort ebenfalls nur Userinfo maskiert). Tragweite
  begrenzt (TTL ≤ 600 s, genau eine Datei, unratbar), aber Logdateien persistieren und der
  Endpunkt lauscht auf **allen** gebundenen Adressen (nicht auf Loopback beschränkt) — ein
  mitgeliefertes Log + noch gültiges Token gibt die eine Datei her. Der bestehende
  Maskierungs-Test (`ProbeUriAsync_ErrorLogs_MaskCredentials`) deckt nur `sftp://` ab; ein
  Test, der das HTTP-Token in Logausgaben verbietet, fehlt.
- [ ] **`_uriProtocolUnsupported` wirkt scheme-übergreifend und kann die HTTP-Stufe
  prozessweit ausschalten.** Das Flag (FfprobeMediaDurationProbe.cs:65, gesetzt in :181,
  geprüft in :100) wird bei **jeder** URI mit `Protocol not found` gesetzt — typischerweise
  `sftp://` auf einem ffprobe-Build ohne libssh (genau der N8-Nachtrag-Befund). Danach
  liefert `ProbeUriAsync` für **alle** Schemes sofort `UriUnsupportedFailure`, ohne ffprobe
  zu starten — inklusive `http://`-Loopback. Folge-Szenario: Ein Remote-Item trifft einen
  einmalig fehlerhaften HTTP-Endpunkt (`Server returned 500`, Verbindungsabbruch im
  Startfenster) → `UriUnsupported` → `sftp://` → `Protocol not found` → Flag gesetzt; alle
  folgenden Remote-Items überspringen die HTTP-Stufe dauerhaft und landen wieder auf
  `pipe:0` mit `StreamTimeoutSeconds` = 600 s — exakt das N8-Timeout-Verhalten, bis zum
  Prozess-Neustart. Das Flag müsste pro Scheme (oder nur für nicht-http) greifen; der Test
  `ProbeUriAsync_ProtocolNotFound_ReturnsUriUnsupported_AndSkipsLaterInvocations`
  verifiziert nur den gleich-Scheme-Fall.
- [ ] **`ResolveBaseUrl` cached auch das Negativ-Ergebnis dauerhaft.**
  `InternalMediaStreamTokenService.ResolveBaseUrl` (Zeilen 110-149) setzt `_baseUrlResolved`
  vor der Auflösung; waren `IServerAddressesFeature.Addresses` zum ersten Aufruf noch leer
  (Worker startet als `IHostedService`, erste Probe nach 10-s-Initialdelay — Rennbedingung
  bei langsamem Kaltstart/Reload) oder war die konfigurierte `HttpProbeBaseUrl` kurzzeitig
  ungültig, bleibt die HTTP-Probe für den gesamten Prozess deaktiviert, ohne dass ein
  erneuter Versuch stattfindet. Positive Caching ist sinnvoll (Adressen ändern sich nicht);
  negative Auflösung sollte erneut versucht werden. Eintrittswahrscheinlichkeit niedrig,
  aber ohne Neustart nicht behebbar.

## Hinweise

- `HttpProbeBaseUrl` akzeptiert auch `https://`-URLs (`TryNormalizeBaseUrl`, Zeilen 155-159).
  Doku sagt „HTTPS-only → automatischer Fallback" — eine explizit gesetzte https-Basis-URL
  erzeugt aber eine Probe-URI, deren TLS-Fehler (self-signed-Zertifikat) keiner der
  `HttpTransportErrorMarkers` trifft → transienter Retry-Loop statt sauberem Fallback.
  Konfigurations-Edge-Case, geringe Schärfen.
- Ein HTTP-Probe-Timeout (`EffectiveTimeoutSeconds` = 120 s) zählt als transienter Versuch
  ohne Kaskade — konsistent und im Test festgelegt; für ffprobe über HTTP könnte zusätzlich
  `timeout`/`rw_timeout`-Option in Betracht kommen (war N9-Offenpunkt, nicht umgesetzt).
- Der Endpunkt ist bewusst ohne `BearerTokenCheck`/Benutzer-Auth — Token ist alleinige
  Credential; erreichbar auf allen gebundenen Interfaces, nicht nur Loopback. Das ist die
  N9-Designentscheidung und durch Unguessability/TTL/Single-File-Scope abgesichert; die
  Log-Abweichung oben erhöht dessen Relevanz.
- Prozess-Hinweis: In `handover.md` fehlt die Bearbeiter-Tabellenzeile **und** der
  Übergabestand-Abschnitt für Schritt 14 (Tabelle endet bei Schritt 13); `steps.md` steht
  auf „In Arbeit". Nachzutragen gemäß AGENTS.md §2/§5.
- Testanzahl: Implementierer-Bericht „37 neue Tests" ≈ Deckung mit gezählten
  ~31-38 Ausführungsfällen (4 Controller + 2 Kestrel-Integration + 8 ffprobe-Fälle +
  ~19 Token-Service-Fälle inkl. Theory-Zeilen + 5 Service-Fälle). Aussage plausibel.
