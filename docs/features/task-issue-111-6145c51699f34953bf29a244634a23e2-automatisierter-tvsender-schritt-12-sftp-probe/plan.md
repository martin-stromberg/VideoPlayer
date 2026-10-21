# Umsetzungsplan: Seekbare Remote-Probes via `sftp://` statt `pipe:0` (N8)

## Übersicht

Die Laufzeitermittlung probt SFTP-Medien derzeit über `pipe:0`-Streaming — ffprobe muss bei `moov`-am-Ende-MP4s die ganze Datei lesen und läuft in `StreamTimeoutSeconds` (600 s). Stattdessen wird für `MediaSourceType.Sftp`-Items mit Credentials eine `sftp://user:pass@host:port/pfad`-URL direkt an ffprobe übergeben (libssh, seekfähig — nur Header/moov werden gelesen). Gleichzeitig wird die Fehlerklassifikation verschärft: endgültige Decoder-Fehler (`moov atom not found`, `Invalid data found`) bei **seekfähiger** Eingabe werden als permanent eingestuft. Betroffen: `FfprobeMediaDurationProbe`, `IMediaDurationProbe`, `MediaDurationProbeResult`, `MediaDurationProbeService`, plus neue interne Hilfsklasse für den URL-Aufbau und Tests.

## Designentscheidungen

| Komponente / Bereich | Gewählter Ansatz | Begründung |
|----------------------|-----------------|------------|
| URL-Format | `sftp://{EscapeDataString(user)}:{EscapeDataString(pass)}@{host}:{port}/{encodeEachSegment(path)}` | ffprobe/libssh nimmt Credentials nur über URL-Userinfo entgegen (verifiziert: `-h protocol=sftp` zeigt nur `timeout`, `truncate`, `private_key` — keine `user`/`password`-Option, keine Env-Variable). `UriBuilder` maskiert Userinfo nicht zuverlässig → manueller Aufbau mit `Uri.EscapeDataString` je Komponente; Pfad-Segmente einzeln encodieren, `/` bleibt Separator. |
| Passwort in Prozess-Aufrufzeile | **Akzeptiert, dokumentiert** | Die Aufrufzeile ist für denselben Benutzer auf demselben System sichtbar (Process Explorer/`Win32_Process`). Es gibt keine native Alternative — libssh unterstützt Credentials nur via URL. Das Passwort liegt bereits im Klartext in der lokalen SQLite-DB der Anwendung; der typische Einsatz ist ein Single-Admin-Server/-Desktop. In `docs/help/videolaufzeit.md` wird das offen benannt. Gegenmaßnahme: **nie** ins Log (siehe Maskierung). |
| Credential-Maskierung | Regex-Maskierung der Userinfo (`scheme://***@`) an zwei Stellen: Log-Anzeigename des Inputs **und** stderr vor dem Loggen | ffprobe echot die Input-URL in Fehlermeldungen (`sftp://user:pass@host/pfad: moov atom not found`). Eine Funktion `MaskUriCredentials` ersetzt jede `scheme://userinfo@`-Stelle — robust auch gegen Normalisierungen/Encodings, weil nicht das Literal der URL gesucht wird. |
| Eingabeart-Unterscheidung in `ProbeAsync` | `ProbeAsync` erhält die rohe Input-Angabe + einen maskierten Anzeigenamen + ein Flag `seekable` | Nur seekfähige Eingaben (Datei, URI) dürfen permanente stderr-Marker auswerten; bei `pipe:0` ist dieselbe stderr-Signatur auch durch einen abgebrochenen Stream erklärbar → bleibt transient (behutsam, keine Fehl-Permanentmarkierung bei Netzausfall). |
| Permanente Marker | `moov atom not found`, `Invalid data found when processing input` | Beide sind von ffprobe beobachtete/definierte Endgültig-Fehler, die bei vollständig gelesener seekbarer Datei eine kaputte/nicht-auswertbare Datei bedeuten. Andere Exit≠0-Fälle bleiben transient. |
| Fallback bei fehlendem `sftp`-Protokoll | `MediaDurationProbeResult` bekommt `UriUnsupported` (init-Property); Probe erkennt `Protocol not found` in stderr, cached das pro Instanz und gibt `UriUnsupportedFailure` zurück; Service fällt **im selben Versuch** auf `pipe:0` zurück | Ohne das würde jede nicht-libssh-ffprobe-Installation pro Item einen Versuch verbrennen. Cached-Flag vermeidet pro Item einen toten Prozessstart. |
| Pfad für die URL | `item.Path` wenn absolut (`/`-Präfix), sonst `collection.Path` + `item.Path`/`item.Name`; kein absoluter Pfad → `null` → Pipe-Fallback | `MediaItem.Path` trägt bei SFTP-Items bereits `entry.FullName` (voller Remote-Pfad). Relative Restfälle sind in `sftp://` nicht abbildbar (URL-Pfad ist immer absolut) → behutsamer Fallback auf die bisherige Pipe. |
| Timeout der URI-Probe | `EffectiveTimeoutSeconds` (120 s) | Seekbarer Zugriff liest nur Header/moov → Sekunden; 120 s ist großzügige Obergrenze, ein hängender Netz-Probe blockiert den Slot nicht 10 min. |
| Ort des URL-Aufbaus | Neue `internal static`-Hilfsklasse `SftpProbeUriBuilder` (`TryBuild(host, port, user, password, remotePath)` + `MaskCredentialsInUri`/`MaskCredentials(text)`) | Reine Funktion, ohne Entity-Abhängigkeit einfach testbar; `FfprobeMediaDurationProbe` und `MediaDurationProbeService` teilen sich die Maskierungslogik dort. |

## Programmabläufe

### SFTP-Probe über `sftp://`-URL

1. `MediaDurationProbeService.ProbeItemAsync`: `MediaSource.SourceType == MediaSourceType.Sftp` → Remote-Pfad auflösen (`item.Path` absolut, sonst `collection.Path` + `item.Path`/`item.Name` kombinieren) → `SftpProbeUriBuilder.TryBuild(...)`.
2. `TryBuild` liefert `null` (fehlende Credentials/Host oder nicht-absoluter Pfad) → direkt in den bisherigen `pipe:0`-Zweig (`OpenFileStream` + `ProbeStreamAsync`).
3. `TryBuild` liefert URI → `_probe.ProbeUriAsync(uri, ct)`:
   - Ergebnis `UriUnsupported` → Fallback in denselben Versuch auf den `pipe:0`-Zweig.
   - Ergebnis Success/Transient/Permanent → wie bisher mappen.
4. `FfprobeMediaDurationProbe.ProbeUriAsync` → `ProbeAsync(uriArg, stdin: null, EffectiveTimeoutSeconds, seekable: true, ct)`; alle Logs nutzen den maskierten Anzeigenamen; stderr wird vor dem Loggen maskiert.
5. ffprobe/libssh öffnet die Datei seekfähig, liest Header + moov → Exit 0 + Dauer in Sekunden.

### Verschärfte Fehlerklassifikation

1. `ProbeAsync` bei `ExitCode != 0` und `seekable == true`: stderr gegen die permanenten Marker prüfen → Treffer = `PermanentFailure` (+ Warn-Log), sonst `TransientFailure` wie bisher.
2. `pipe:0`-Eingaben sind nie `seekable` → Verhalten unverändert transient.
3. `Protocol not found` bei URI-Probes → `UriUnsupportedFailure` (greift vor der permanent/transient-Einordnung).

## Neue Klassen

| Klasse | Typ | Zweck |
|--------|-----|-------|
| `SftpProbeUriBuilder` | internal static Klasse (`VideoWebPlayer/Services/SftpProbeUriBuilder.cs`) | `TryBuildUri(string host, int port, string? username, string? password, string remotePath)` → `string?`; `MaskCredentials(string uri)` für Log-Anzeige; `MaskCredentialsInText(string? text)` für stderr-Sanitizing. |

## Änderungen an bestehenden Klassen

### `IMediaDurationProbe` (Interface)

- **Neue Methoden:** `ProbeUriAsync(string uri, CancellationToken)` — Probt eine Remote-URI (z. B. `sftp://`), die ffprobe direkt öffnet. Rückgabewert `Task<MediaDurationProbeResult>`.

### `FfprobeMediaDurationProbe`

- **Neue Methoden:** `ProbeUriAsync(string uri, CancellationToken)` — ruft `ProbeAsync` mit dem quotierten URI-Argument, `stdin: null`, `EffectiveTimeoutSeconds`, `seekable: true`, maskiertem Anzeigenamen.
- **Geänderte Methoden:** `ProbeAsync` — neue Parameter `displayInput` (maskierter Name für Logs) und `seekable`; Exit≠0-Zweig: stderr vor Log maskieren (`MaskCredentialsInText`), permanente Marker bei `seekable` → `PermanentFailure`; `Protocol not found` in stderr → `UriUnsupportedFailure` + Instanz-Flag `_uriProtocolUnsupported`.
- **Neues Feld:** `private bool _uriProtocolUnsupported` — gesetzt nach erstem `Protocol not found`; `ProbeUriAsync` liefert dann sofort `UriUnsupportedFailure` ohne Prozessstart.

### `MediaDurationProbeResult`

- **Neue Eigenschaften:** `UriUnsupported` (`bool`, `init`) — kennzeichnet „Eingabe-Protokoll nicht unterstützt".
- **Neue Fabrik:** `static readonly MediaDurationProbeResult UriUnsupportedFailure` (`Duration: null, Transient: true, UriUnsupported = true`).

### `MediaDurationProbeService`

- **Geänderte Methoden:** `ProbeItemAsync` — `Sftp`-Zweig zuerst `ProbeUriAsync` versuchen (bei baubarer URL), `UriUnsupported`/nicht baubar → bisheriger `OpenFileStream`+`ProbeStreamAsync`-Fallback; `LocalDirectory` unverändert.
- **Neue private Hilfsmethode:** `ResolveRemotePath(MediaItem item)` — `item.Path` absolut → verwenden; sonst `collection.Path` + `item.Path`/`item.Name` kombinieren (Konvention `CombineSftpPath`).

## Datenbankmigrationen

Keine.

## Validierungsregeln

| Feld / Objekt | Regel | Fehlerfall |
|---------------|-------|------------|
| `sftp://`-URL-Bestandteile | `Host`, `Username`, `Password` nicht leer; Remote-Pfad absolut (`/`-Präfix); `Port` > 0 sonst weggelassen; Host mit `:` → `[…]`-Klammern | `TryBuildUri` → `null` → Pipe-Fallback |
| stderr-Klassifikation | permanente Marker nur bei `seekable` auswerten | pipe:0 bleibt transient |
| Log-Ausgaben | `displayInput` und stderr maskiert | Passwort gelangt nicht ins Log |

## Konfigurationsänderungen

Keine neuen Schlüssel. `MediaProbe:StreamTimeoutSeconds` gilt weiterhin — nur noch für den `pipe:0`-Fallback.

## Seiteneffekte und Risiken

- **ffprobe-Build ohne libssh:** URI-Probe schlägt mit `Protocol not found` fehl → automatischer Pipe-Fallback, kein Funktionsverlust, kein dauerhafter Schaden.
- **Passwort in der Prozesszeile:** dokumentiert in `docs/help/videolaufzeit.md` (bewusst akzeptiert; libssh hat keine Alternativ-Übergabe; Passwort liegt ohnehin lokal in der SQLite-DB).
- **Permanente Klassifikation:** betrifft jetzt auch `ProbeFileAsync` (lokale Dateien) — ein Exit≠0 mit `moov atom not found` bei echter lokaler Datei ist korrekt permanent; Risiko von Fehlklassifikation bei lokalen Quoten/Fehlern ist gering, da nur die zwei konkreten Decoder-Marker greifen.
- **Live-Stream (`LiveStreamMediaResolver`/`FfmpegLiveStreamSegmenter`):** unverändert — gleiche Pipe-Einschränkung bleibt dort bestehen (laut Auftrag nur Probe-Seite fixen).
- **Interface-Erweiterung `IMediaDurationProbe`:** `FakeDurationProbe` in den Tests muss die neue Methode implementieren.

## Umsetzungsreihenfolge

1. **`SftpProbeUriBuilder` anlegen + Unit-Tests für URL-Aufbau/Encoding/Maskierung**
   - Voraussetzungen: keine (reine statische Hilfsklasse)
   - Beschreibung: `TryBuildUri` mit `Uri.EscapeDataString` für User/Pass/Pfad-Segmente, IPv6-Klammerung, Port-Bedingung; `MaskCredentials`/`MaskCredentialsInText` per Regex `scheme://userinfo@` → `scheme://***@`.
2. **`MediaDurationProbeResult` um `UriUnsupported` + `UriUnsupportedFailure` erweitern**
   - Voraussetzungen: keine
3. **`IMediaDurationProbe.ProbeUriAsync` + `FfprobeMediaDurationProbe`-Implementierung**
   - Voraussetzungen: Schritte 1–2
   - Beschreibung: `ProbeAsync`-Signatur um `displayInput`/`seekable` erweitern; stderr-Maskierung; permanente Marker; `Protocol not found`-Erkennung + `_uriProtocolUnsupported`-Flag.
4. **`MediaDurationProbeService.ProbeItemAsync` SFTP-Zweig**
   - Voraussetzungen: Schritte 1–3
   - Beschreibung: `ResolveRemotePath` + `TryBuildUri` → `ProbeUriAsync` → Fallback auf `OpenFileStream`/`ProbeStreamAsync`.
5. **Tests erweitern** (`FfprobeMediaDurationProbeTests`, `MediaDurationProbeServiceTests`, neue `SftpProbeUriBuilderTests`)
   - Voraussetzungen: Schritte 1–4
6. **Dokumentation** (`docs/help/videolaufzeit.md`, `docs/RELEASE_NOTES.md`, ggf. `README.md`)
   - Voraussetzungen: Schritte 1–5

## Tests

### Neue Tests

| Test / Hilfsmethode | Testklasse | Was wird geprüft? |
|--------------------|------------|---------------------|
| `TryBuildUri_BuildsExpectedUrl` | `SftpProbeUriBuilderTests` | Format `sftp://user:pass@host:port/pfad` |
| `TryBuildUri_EncodesCredentialsAndPath` | dto. | `:` `@` `/` `%` Leerzeichen in Credentials; Pfad-Segmente encodiert, `/` bleibt |
| `TryBuildUri_MissingInput_ReturnsNull` (Theory: kein Host/User/Pass, relativer Pfad) | dto. | `null` → Fallback |
| `TryBuildUri_PortOmitted_WhenNotPositive`, `TryBuildUri_Ipv6Host_Bracketed` | dto. | Port-Konditional, IPv6 |
| `MaskCredentials_*` | dto. | Userinfo in URI + in stderr-artigem Text maskiert |
| `ProbeUriAsync_ParsesStdoutDuration` / `_UsesFileTimeout` | `FfprobeMediaDurationProbeTests` | quotiertes URI-Argument, `EffectiveTimeoutSeconds` statt Stream-Timeout |
| `ProbeUriAsync_MoovAtomNotFound_ReturnsPermanent` / `_InvalidDataFound_ReturnsPermanent` | dto. | permanente Klassifikation seekbarer Inputs |
| `ProbeUriAsync_ProtocolNotFound_ReturnsUriUnsupported_AndSkipsProcessOnRetry` | dto. | `UriUnsupported`-Flag + Prozess-Cache-Flag |
| `ProbeFileAsync_MoovAtomNotFound_ReturnsPermanent` | dto. | gleiche Klassifikation für lokale Datei |
| `ProbeStreamAsync_MoovAtomNotFound_RemainsTransient` | dto. | pipe:0 unverändert transient |
| `ProbeUriAsync_LogsDoNotContainPassword` | dto. | ILogger-Fake: kein Passwort in Warn-Logs (Fehlerpfad) |
| `ProbePendingAsync_SftpSource_WithCredentials_UsesUriProbe` | `MediaDurationProbeServiceTests` | `ProbeUriAsync` statt Stream; Dauer persistiert |
| `ProbePendingAsync_SftpSource_WithoutCredentials_FallsBackToStream` (bestehender `SftpSource_StreamsViaOpenFileStream`-Test deckt ab — Quelle ohne Credentials) | dto. | Pipe-Fallback ohne Credentials |
| `ProbePendingAsync_SftpSource_UriUnsupported_FallsBackToStream` | dto. | `UriUnsupported` → Stream im selben Versuch |
| `ProbePendingAsync_SftpSource_RelativeItemPath_FallsBackToStream` | dto. | nicht-absoluter Pfad → Pipe |

### Betroffene bestehende Tests

| Test / Testklasse | Grund der Anpassung |
|-------------------|---------------------|
| `FakeDurationProbe` in `MediaDurationProbeServiceTests` | muss `ProbeUriAsync` implementieren (neue Interface-Methode) |
| `ProbePendingAsync_SftpSource_StreamsViaOpenFileStream` | bleibt bestehen als Fallback-Nachweis (Seed-Quelle hat keine Credentials) |

### E2E-Tests (primärer Funktionsnachweis)

Keine — die Anforderung ändert ausschließlich ein internes Background-Probe-Verhalten (Worker + Prozessaufruf), das über keine UI oder Nutzeraktion erreichbar ist; ein echter SFTP-Server steht in der Testumgebung nicht zur Verfügung. Der Funktionsnachweis liegt in den Seam-basierten Unit- und SQLite-Integrationstests; bestehende E2E-Tests sind nicht betroffen.

## Offene Punkte

Keine.
