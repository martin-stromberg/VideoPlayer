# Abnahme Schritt 12 — Remote-Probes seekbar via `sftp://` statt `pipe:0` (N8)

Prüfer: Devin (unabhängige Abnahme, frischer Lauf ohne Implementierungskontext)
Geprüfter Stand: `c386318` (`feat: SFTP-Laufzeitprobes seekfähig über sftp://-URL statt pipe:0`), Arbeitsbaum sauber.

## Ergebnis

**Status:** Erfüllt

Die Anforderung aus `nachbesserungen.md` §N8 ist vollständig und korrekt umgesetzt:
seekbare `sftp://`-Probes für SFTP-Quellen mit Credentials, Pipe-Fallback im selben
Versuch, Passwort in keiner Log-Stelle, permanente Fehlerklassifikation nur bei
seekbarer Eingabe. Alle kritischen Prüfpunkte verifiziert, Gegenprobe erfolgreich.

### Verifiziert

- **URI-Aufbau korrekt (`SftpProbeUriBuilder.cs`):** `Uri.EscapeDataString` je
  Komponente (User, Passwort, jedes Pfadsegment) — `:`/`@`/`/`/`%`/Leerzeichen/`&`/`?`
  können die URI-Struktur nicht brechen (Test: `p@ss:w/rd%` → `p%40ss%3Aw%2Frd%25`).
  IPv6-Literale werden gebracketed, Port ≤ 0 entfällt, fehlende Credentials/relativer
  Pfad → `null` → Pipe-Fallback. `CombinePath` spiegelt `SftpMediaSourceReader.
  CombineSftpPath` exakt (`SftpMediaSourceReader.cs:304-309`).
- **Seekbare URI-Probe (`FfprobeMediaDurationProbe.cs:81-93`):** `ProbeUriAsync`
  nutzt `EffectiveTimeoutSeconds` (120 s, File-Timeout — korrekt, da seekbar nur
  Header gelesen werden), `seekable: true`, maskiertes `displayInput`; die URI landet
  nur gequotet im Prozess-Argument, nie im Log.
- **Credential-Maskierung an allen Log-Stellen:** `displayInput` (maskiert via
  `MaskCredentials`) in allen sechs Log-Aufrufen — Exception (`:138`), Timeout
  (`:144`), Protocol-not-found (`:157-160`), permanenter Fehler (`:165-168`),
  generischer Exit-Code (`:171-173`), keine Laufzeit (`:180-182`). stderr wird vor
  Klassifikation UND Logging via `MaskCredentialsInText` gesäubert (`:152`), da
  ffprobe die Eingabe-URL in Fehlermeldungen echot. Service-Seite loggt nur
  `item.Path` (`MediaDurationProbeService.cs:253`, `:282-285`) — remote Pfad ohne
  Credentials. **Gegenprobe:** `MaskCredentials` auf unmaskiert gesetzt →
  `MaskCredentials_MasksUserInfo` und `ProbeUriAsync_ErrorLogs_MaskCredentials`
  schlagen fehl (2/48 rot) → `git checkout --` revertiert, Baum sauber.
- **`Protocol not found`-Erkennung:** Case-insensitive Contains auf maskiertem
  stderr bei Exit≠0 → `UriUnsupportedFailure` (`Transient: true`, `UriUnsupported`)
  + `_uriProtocolUnsupported`-Flag. `IMediaDurationProbe` ist **Singleton**
  (`ServiceCollectionExtensions.cs:241`) — das Flag ist tatsächlich prozessweit,
  keine weiteren toten Prozessaufrufe (Test zählt Aufrufe: 1).
- **Pipe-Fallback ohne verbrannten Attempt:** Bei `UriUnsupported` oder nicht
  baubarer URL fällt `ProbeRemoteItemAsync` (`MediaDurationProbeService.cs:266-292`)
  im selben Versuch auf `OpenFileStream`/`pipe:0` zurück; nur dessen Ergebnis zählt
  via `ApplyOutcome` als Attempt. Tests: `UriUnsupported_FallsBackToStream`,
  `RelativePaths_FallBackToStream`, fehlende Credentials → Stream.
- **Permanente Klassifikation nur bei seekbarer Eingabe:** `moov atom not found` /
  `Invalid data found when processing input` → `PermanentFailure` nur bei
  `seekable` (File/URI); bei `pipe:0` bleibt dieselbe stderr transient, weil sie auch
  durch früh abgebrochenen Stream entsteht (`:163`, Tests beidseitig).
  `ApplyOutcome` setzt `DurationProbeAttempts` auf `maxAttempts` → kein Retry-Burn.
- **Lokale Quellen:** unverändert `ProbeFileAsync(item.Path)` (Dateipfad, keine URI).
- **Pfadauflösung:** `MediaItem.Path` ist beim Scan der volle `entry.FullName`
  (absolut) → direkt verwendet; sonst `collection.Path`+Name kombiniert; nicht
  absolut → Fallback statt kaputter URI (`ResolveRemotePath`, `:301-310`).
- **Builds:** `dotnet build VideoPlayer.sln` Debug **und** `-c Release`: je
  0 Fehler, 142 Warnungen = Bestand; keine neue Warnung in geänderten Dateien
  (gegreppt). Kein `VideoWebPlayer.exe`-Lock — Benutzer-Instanz lief nicht.
- **Suite:** `dotnet test VideoWebPlayer.Tests/VideoWebPlayer.Tests.csproj`
  synchron (Debug): **2045 grün / 1 Skip (bekannter Alt-Skip
  `PairedDevice_ReportsProgressForMovieWithoutCollection_…`) / 1 FAIL** =
  `PlaylistSkipLockedEntriesE2ETest` (Playwright-Timeout `SelectEntryAsync`,
  kein Bezug zur Änderung). Einzelwiederholung: **grün (10 s)** — bekannter
  Last-Flake laut AGENTS.md. Effektiv 2046 grün + 1 Alt-Skip wie berichtet.
- **Tests neu:** ~33 Fälle (12 Builder-Fälle inkl. Encoding/IPv6/Masking, 8
  Probe-Tests inkl. Maskierungs-Log-Test und Flag-Caching, 4 Service-Tests);
  alle drei `IMediaDurationProbe`-Fakes erweitert.
- **Doku:** `docs/help/videolaufzeit.md` + `docs/RELEASE_NOTES.md` (EN+DE)
  dokumentieren sftp://-Probe, Fallback, `StreamTimeoutSeconds`-Semantik, die
  Klassifikationsänderung **und** die Sicherheitsentscheidung (Passwort in der
  Prozess-Kommandozeile für denselben Benutzer sichtbar — libssh bietet keine
  Alternative, Passwort liegt bereits in der App-DB). `handover.md` aktualisiert.

## Abweichungen

- [ ] Keine.

## Hinweise

- **Restrisiko Prozesszeile:** Das Passwort steht während des (sekundenkurzen)
  ffprobe-Aufrufs in dessen Kommandozeile — für Prozesse desselben Benutzers
  sichtbar. Bewusste, dokumentierte Entscheidung (keine libssh-Alternative);
  Passwort liegt bereits lesbar in der App-DB. In Logs wird es nirgends
  ausgegeben.
- **Theoretischer Leak-Pfad über Exceptions:** `catch (Exception ex)` in
  `FfprobeMediaDurationProbe.cs:136-139` und `MediaDurationProbeService.cs:253`
  loggt die Exception mit — trüge eine Exception-Message das Argument (Passwort),
  würde es nicht maskiert. Produktiv praktisch nicht erreichbar:
  `ExecuteProcessAsync` fängt alle Prozessstart-Fehler selbst (`:262-265`) und
  die dort möglichen Rest-Exceptions (IO beim Stream-Pumpen u. ä.) enthalten die
  Kommandozeile nicht. Kein Test deckt diesen Pfad ab — als bekanntes Restrisiko
  vermerkt, kein Blocker.
- **`_uriProtocolUnsupported` wird auch bei Nicht-URI-Probes gesetzt**, falls deren
  stderr „Protocol not found" enthielte (Check nicht auf URI-Eingabe begrenzt).
  Bei `pipe:0`/DOS-Pfaden praktisch unerreichbar; ein False-Positive würde
  URI-Probes prozessweit deaktivieren — nur Hinweis.
- **Verhaltensänderung auf dem lokalen Pfad:** `moov atom not found` /
  `Invalid data found` bei `ProbeFileAsync` gilt nun sofort als permanent
  (bisher: transient bis `MaxAttempts`). Fachlich korrekt (seekbare Eingabe) und
  N8-intentkonform, getestet und in den Release Notes beschrieben — erwähnt, weil
  es das Klassifikationsverhalten auch für Bestandsdaten lokaler Quellen ändert.
- **Konservative Pipe-Klassifikation:** Ein echt kaputtes MP4 ohne baubare
  sftp://-URL bleibt über `pipe:0` transient und kann bis zu `MaxAttempts` × 600 s
  kosten — bewusster Trade-off, da Pipe-Abbruch und Dateidefekt an stderr nicht
  unterscheidbar sind. Dokumentiert in `videolaufzeit.md`.
- **Bool-Flag ohne Lock:** `_uriProtocolUnsupported` wird aus parallelen
  Probe-Tasks gelesen/geschrieben — idempotenter Write, benign race.
- **Kein echter SFTP-Integrationstest** (kein Server in der Testumgebung) —
  Nachweis über `_executeAsync`-Seam; sachlich angemessen, im Handover benannt.
- `steps.md` zeigt Schritt 12 noch „In Arbeit" — Status-Aktualisierung auf
  „Fertig" steht nach dieser Abnahme an.
