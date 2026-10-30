# Umsetzungsplan: TV-Sender — Live-Stream von Serien/Filmen scheitert auf `pipe:0` (Issue #311)

## Übersicht

Die produktive Ursache ist inzwischen bewiesen: `InternalMediaStreamTokenService.ResolveBaseUrl` (`InternalMediaStreamTokenService.cs:148-195`) liest ausschließlich `IServerAddressesFeature.Addresses` — die enthält nur Adressen aus `UseUrls`/`ASPNETCORE_URLS`/`--urls`, **nicht** die über `Kestrel:Endpoints` konfigurierten Bindungen. Produktiv bindet Kestrel über `appsettings.Production.json` (`Kestrel:Endpoints:Http:Url = http://*:5002`), die Adressliste bleibt leer → `TryCreateStreamUri` liefert `null` → **alle** entfernten Quellen laufen auf `pipe:0` und scheitern an moov-am-Ende-MP4s exakt wie im Issue beschrieben. Der **Primärfix (Phase 1)** erweitert `ResolveBaseUrl` um eine dritte Erkennungsstufe über `Kestrel:Endpoints:*:Url` aus der Konfiguration (Vorbild `MdnsServiceProfileBuilder`) — davon profitieren `TryCreateStreamUri` (Segmenter) und `TryCreateProbeUri` (ffprobe-HTTP-Probes) gleichermaßen. Zusätzlich korrigiert Phase 1 die Cache-Semantik von `ResolveBaseUrl`: Der Ist-Code setzt `_baseUrlResolved = true` unbedingt (`InternalMediaStreamTokenService.cs:153`) und cacht damit ein `null`-Ergebnis prozessweit — entgegen den eigenen Code-Kommentaren (Z. 57-59, 184-186); das vergiftet die Erkennung bei der Startup-Race auf `Addresses` in `UseUrls`/`ASPNETCORE_URLS`-Installationen. Phase 1 enthält außerdem das Diagnose-/Robustheitspaket: `ResolutionNote` am `LiveStreamInput`, Erkennung nicht demuxbarer Eingänge (`UndecodableInput`) mit Sendeplatz-Skip statt Restart-Schleife und einen Byte-zählenden Eingabe-Pump. **Phase 2** ergänzt die `sftp://`-Direkt-Eingangsstufe für Installationen ohne jede http-Bindung (HTTPS-only-Betrieb) — für dieses Deployment wirkungslos, weil das produktive ffmpeg (statischer johnvansickle-Build 7.0.2) kein libssh enthält; sie ist dennoch fester Bestandteil des Plans (Scope-Entscheidung: beide Phasen werden umgesetzt).

## Designentscheidungen

| Komponente / Bereich | Gewählter Ansatz | Begründung |
|----------------------|-----------------|------------|
| Basis-URL-Erkennung (Primärfix) | `IConfiguration` in `InternalMediaStreamTokenService` injizieren (optionaler letzter Konstruktorparameter, Default: leere `ConfigurationBuilder`-Konfiguration); `ResolveBaseUrl` enumeriert `Kestrel:Endpoints`–Children und normalisiert jede `Url` über das vorhandene `TryNormalizeServerAddress` (nur `http`, Wildcard/`localhost` → `127.0.0.1`). Reihenfolge bleibt: `MediaProbe:HttpProbeBaseUrl` → `IServerAddressesFeature.Addresses` → `Kestrel:Endpoints:*:Url` | `MdnsServiceProfileBuilder.ResolvePort` (`MdnsServiceProfileBuilder.cs:61`) liest `Kestrel:Endpoints:Http:Url` bereits direkt aus `IConfiguration` — gleiche Quelle, etabliertes Muster. `IOptions<KestrelServerOptions>` verworfen: die Optionsbindung hält die benannten Endpunkte nicht als rohe URLs vor (sie werden beim Kestrel-Setup in `Listen*`-Aufrufe übersetzt) und zöge eine Kestrel-Abhängigkeit in den Token-Service. Enumerieren **aller** Children statt nur des Namens `Http`: Endpunktnamen sind frei wählbar (z. B. `Api`, `PublicHttp`); erster http-fähiger Treffer gewinnt, `https` wird wie bisher abgelehnt (Loopback-TLS scheitert an der Zertifikatsprüfung). |
| Cache-Semantik von `ResolveBaseUrl` | **Korrektur zum dokumentierten Intent:** `_baseUrlResolved` wird nur noch bei positivem Ergebnis gesetzt; ein `null`-Ergebnis wird beim nächsten Aufruf erneut versucht. Die „ungültige `HttpProbeBaseUrl`"-Warnung erhält einen eigenen Einmal-Latch (`_invalidConfigWarned`), da sie ohne den unbedingten Latch bei jedem Retry erneut loggen würde | Der Ist-Code setzt `_baseUrlResolved = true` am Methodenanfang unbedingt (`InternalMediaStreamTokenService.cs:153`) — ein `null`-Ergebnis wird damit prozessweit gecacht, obwohl die Kommentare (Z. 57-59, 184-186) genau das Gegenteil beschreiben. In Installationen, die per `UseUrls`/`ASPNETCORE_URLS` binden, kann `Addresses` beim ersten Aufruf noch leer sein (Startup-Race); der Ist-Code deaktiviert dann den HTTP-Eingang für die gesamte Prozesslaufzeit. Die Korrektur schließt diese Vergiftung; sie ist eine bewusste Verhaltensänderung (betrifft AK 5) und wird als solche im Abnahmebericht benannt. Kosten: ein Feature-/Konfig-Lesevorgang je Aufruf — vernachlässigbar gegen die folgenden Prozessstarts. |
| `Kestrel:Endpoints`-Filterung | Je Endpunkt `Url` normalisieren; zusätzlich `Port <= 0` und `Protocols`-Einschränkung beachten: `http://*:0`/`http://127.0.0.1:0` (dynamischer Port, vgl. `appsettings.Testing.json`) ist als Basis-URL unbrauchbar → `TryNormalizeServerAddress` lehnt Port ≤ 0 ab (gilt damit auch für die `Addresses`-Stufe — Verbesserung). Ein Endpunkt mit `Protocols`, das weder `Http1` noch `Http2` enthält (z. B. reines `Http3` = nur UDP), wird übersprungen | Ein `:0`-Treffer würde die nie erreichbare Basis-URL `http://127.0.0.1:0` liefern — der HTTP-Transport-Fallback finge das zwar auf, der Endpunkt ist aber von vornherein nutzlos. Reines `Http3` lauscht nur auf UDP; TCP-Loopback scheitert — der Filter vermeidet einen garantiert falschen Treffer. Substring-Vergleich (`OrdinalIgnoreCase`) auf `Http1`/`Http2` im `Protocols`-Wert — deckt `Http1AndHttp2`/`Http1AndHttp2AndHttp3` ab, ohne Kestrel-Enum-Abhängigkeit. (`HttpProtocols` hat kein `All`-Member — nur `None`/`Http1`/`Http2`/`Http1AndHttp2`/`Http3`/`Http1AndHttp2AndHttp3`; der Vergleich muss case-insensitiv sein, weil Kestrel kleingeschriebene Konfigurationswerte wie `http3` akzeptiert.) |
| Repo-Betriebskonfiguration | **Keine** Änderung an `appsettings.Production.json` — die `Kestrel:Endpoints:Http:Url = http://*:5002`-Bindung ist dort bereits vorhanden und wird vom Fix gelesen | Eine zusätzliche Loopback-Bindung oder `MediaProbe:HttpProbeBaseUrl` im Repo wäre redundant und änderte die produktive Listener-Topologie; die Code-Erkennung deckt auch Installationen ab, die ihre Bindung nur in der Produktiv-Konfiguration tragen. |
| sftp-Eingangsstufe | Als eigenständige **Phase 2** im Plan, nachgelagert hinter Phase 1 umgesetzt | Für dieses Deployment wirkungslos (ffmpeg ohne `--enable-libssh`, `sftp://` → `Protocol not found`). Sie bleibt aber die einzige seekbare Stufe für HTTPS-only-Installationen ohne jede http-Bindung — dort greifen weder der Kestrel-Fix (kein http-Endpunkt) noch `HttpProbeBaseUrl` sinnvoll (Loopback-TLS-Problem). Umsetzungskosten sind überschaubar, weil URI-Bau (`SftpProbeUriBuilder.TryBuildUri`) und Credential-Maskierung bereits für die Probe-Seite existieren und libssh-fähige ffmpeg-Builds (gyan.dev-Full, Distro-Pakete) verbreitet sind. |
| Fallback-Steuerung im Coordinator (Phase 2) | Zwei Flags `useHttpInput`/`useSftpInput` als Parameter am `ResolveAsync`, gesteuert über Fehlerflags in `LiveStreamExitInfo` (`SftpInputFailure`, `UndecodableInput`) — kein neues Statusobjekt | Verlängert das vorhandene Muster (`useHttpInput` + `HttpTransportFailure`) um eine Stufe; der Coordinator bleibt zustandslos (scoped, je Tick neu), der Singleton-Segmenter liefert die Diagnose. |
| Nicht demuxbarer Eingang | Erkennen (`UndecodableInput` über Demux-stderr-Marker) + Sendeplatz überspringen (`LiveStreamSyncAction.InputUnplayable`, WakeUp am Slotende) — **keine** `probesize`/`analyzeduration`-Erhöhung, kein Remux | Bei `moov`-am-Ende unter `-re` kommt der Header erst nach dem vollständigen Durchlauf in Echtzeit — kein endliches Probe-Fenster hilft; ein Remux-Vorsatz kann Pakete vor dem `moov`-Atom ebenfalls nicht liefern. Die Marker decken korrupte Dateien auf **jedem** Eingang ab (auch `ForFile`), sodass keine Restart-Schleife mehr entsteht — das ist zugleich die Absicherung für Betreiber, die `LiveStream:UseHttpInput` bewusst deaktivieren. |
| Skip-Log ohne Wiederholungs-Spam | Der Skip-Zweig folgt dem `MediaUnavailable`-Muster: `SynchronizeStreamAsync` gibt nur das `LiveStreamSyncResult(InputUnplayable, now.EndUtc, Detail)` zurück — **keine** eigene Coordinator-Warnung je Tick (die einmalige Diagnose steht bereits in der `ReportProcessExit`-Warnung des Segmenters inkl. stderr-Tail und neu auch Pump-Statistik). `LiveStreamWorker` dedupliziert identische `(Action, Detail)`-Wiederholungen von `MediaUnavailable`/`InputUnplayable`: erstes Auftreten → `Information`, unveränderte Folgeergebnisse → `Debug`. **Zusätzlich entfällt der bisherige Per-Tick-Fallback-Infolog** (`LiveStreamCoordinator.cs:124-129`): er loggt denselben HTTP-Fallback-Hinweis alle ~30 s, solange `useHttpInput == false` — dieselbe Spam-Klasse; die Information steht künftig einmalig in der `ResolutionNote` des `Started`-`Detail`s („HTTP-Transportfehler im vorherigen Lauf") | Der `LiveStreamCoordinator` ist scoped — er kann keinen „schon gemeldet"-Zustand halten; `WakeAtUtc` begrenzt die Schlafzeit nur nach unten (`PollCap` = 30 s bleibt die Obergrenze), also läuft der Skip-Zweig bei langen Sendeplätzen alle ~30 s erneut. Eine per-Tick-Warnung würde eine halbstündige Sendung mit ~60 Warnzeilen fluten. Der Worker ist Singleton und darf den letzten gemeldeten Schlüssel halten (Muster wie `_lastFfmpegWarning`). Für den Fallback-Hinweis ist die einmalige `ResolutionNote` je Job-Start die passendere Stelle als ein Per-Tick-Log — der Coordinator kann selbst nicht deduplizieren, und der `Started`-Eintrag erscheint ohnehin genau einmal je neuem Job. |
| Vollständigkeitsdiagnose des Pumps | Manuelle Kopierschleife mit Bytezählung in `SegmenterProcessSupport.PumpInputAsync`, `ExpectedBytes` aus `Stream.Length` wenn `CanSeek` (bei `SftpStreamWrapper`/`FileStream` gegeben); Ergebnis als neuer `SegmenterPumpResult`-Record statt nacktem `SegmenterPumpOutcome` | `CopyToAsync` liefert keine Mengenangabe — nur so wird „Quelle brach mittendrin ab" von „vollständig geliefert, Container-Layout ist die Ursache" im Log unterscheidbar (Akzeptanzkriterium 4). |
| Ursachen-Diagnose des Pipe-Wegs | `ResolutionNote` (Klartext-Grund) am `LiveStreamInput`-Record, vom `LiveStreamCoordinator` in die `Started`-Logzeile aufgenommen | `ResolveAsync` läuft jeden ~30-s-Tick — eigenes Logging dort wäre Spam; die Note erscheint genau bei Job-Start und nennt die tatsächlich zutreffende Ursache (Option aus, HTTP-Transport-Vorschaden, keine http-Basis-URL — inkl. geprüfter Kestrel-Endpoints —, in Phase 2 zusätzlich: sftp-URI nicht bildbar, `UseSftpInput` deaktiviert) — Akzeptanzkriterium 2/5. |
| ffmpeg ohne `sftp`-Protokoll (Phase 2) | `ILiveStreamSegmenter.SftpInputSupported`-Latch (startet `true`, wird bei Schema-Markern im stderr permanent `false`) statt `ffmpeg -protocols`-Vorabprüfung | Kein zusätzlicher Prozessaufruf; ein fehlendes libssh äußert sich ohnehin beim ersten Lauf und wird dann prozessweit gemerkt — ffmpeg-Build-Wechsel erfordert ohnehin einen App-Neustart. |

## Programmabläufe

### Basis-URL-Erkennung für Loopback-Eingänge (Phase 1 — der eigentliche Fix)

1. `TryCreateStreamUri` (Segmenter) und `TryCreateProbeUri` (ffprobe) rufen `ResolveBaseUrl`. **Korrigierte Cache-Semantik:** `_baseUrlResolved` wird nur noch bei positivem Ergebnis gesetzt; ein `null`-Ergebnis löst beim nächsten Aufruf einen erneuten Versuch aus — ein Startup-Race-Fehlschlag deaktiviert die Erkennung nicht mehr prozessweit (bisherige Ist-Semantik cachte `null` unbedingt, entgegen dem dokumentierten Intent).
2. Stufe 1: `MediaProbe:HttpProbeBaseUrl` — gültige absolute http(s)-URL gewinnt (unverändert); eine ungültige URL wird einmalig gewarnt (neuer Latch `_invalidConfigWarned`, weil der früher unbedingte `_baseUrlResolved`-Latch diese Einmaligkeit mitlieferte) und die Erkennung fährt fort.
3. Stufe 2: `IServerAddressesFeature.Addresses` — erste normalisierbare `http://`-Adresse mit Port > 0 gewinnt (unverändert bis auf die neue Port-≤-0-Ablehnung in `TryNormalizeServerAddress`).
4. **Stufe 3 (neu):** `_configuration.GetSection("Kestrel:Endpoints").GetChildren()` — je benanntem Endpunkt `Url` lesen und über `TryNormalizeServerAddress` normalisieren; ein `Protocols`-Wert, der (`OrdinalIgnoreCase`-Vergleich) weder `Http1` noch `Http2` enthält, disqualifiziert den Endpunkt; der erste http-fähige Treffer gewinnt (Wildcard/`localhost` → `127.0.0.1`; `https`-, Port-0- und ungültige Einträge werden übersprungen — ein `https`-Eintrag vor einem `http`-Eintrag blockiert den Treffer nicht).
5. Kein Treffer in allen Stufen → die einmalige Warnung (`_noAddressWarned`, unverändert) nennt jetzt beide betroffenen Verbraucher (HTTP-Laufzeitermittlung **und** Live-Stream-HTTP-Eingang) sowie die ausgeschöpften Erkennungswege; `null`.

Beteiligte Klassen/Komponenten: `InternalMediaStreamTokenService`, `MediaProbeOptions`, `IConfiguration` (DI-injiziert), `IServerAddressesFeature`.

### Eingangsauflösung entfernter Quellen — Phase 1 (HTTP → Pipe mit Diagnose)

1. `LiveStreamCoordinator.SynchronizeStreamAsync` leitet `useHttpInput` wie bisher aus `LastUnexpectedExit.HttpTransportFailure` am selben Slot ab; neu: `UndecodableInput` am selben Slot → Skip-Verzweigung (siehe nächster Ablauf), die **vor** der Auflösung greift.
2. `LiveStreamMediaResolver.ResolveAsync(mediaItemId, runDuration, useHttpInput, ct)` lädt `MediaItem`/`MediaCollection`/`MediaSource` wie bisher; `LocalDirectory` → `ForFile` (unverändert).
3. Entfernte Quelle: `useHttpInput && LiveStreamOptions.UseHttpInput` → `TryCreateStreamUri` → `ForHttp` (keine `ResolutionNote` — bevorzugter Eingang; nach dem Kestrel-Fix der produktiv wirksame Pfad).
4. Sonst → `ForStream` mit `ResolutionNote`, die die zutreffenden Gründe nennt („HTTP-Eingang deaktiviert (`LiveStream:UseHttpInput`)", „HTTP-Transportfehler im vorherigen Lauf", „keine http-Basis-URL auflösbar — `MediaProbe:HttpProbeBaseUrl`, gebundene Adressen und `Kestrel:Endpoints` geprüft").
5. Der Coordinator hängt Eingangsart (Datei/HTTP/pipe:0 — interner `InputKindLabel` am `LiveStreamInput`) und ggf. `ResolutionNote` an das `Started`-`Detail`; `LiveStreamWorker` loggt es auf Information.

Beteiligte Klassen/Komponenten: `LiveStreamCoordinator`, `LiveStreamMediaResolver`, `LiveStreamInput`, `InternalMediaStreamTokenService`, `LiveStreamOptions`, `LiveStreamWorker`.

### Eingangsauflösung entfernter Quellen — Phase 2 (HTTP → sftp → Pipe)

1. `LiveStreamCoordinator.SynchronizeStreamAsync` liest `ILiveStreamSegmenter.LastUnexpectedExit` und `ILiveStreamSegmenter.SftpInputSupported` und leitet daraus ab: `useHttpInput` (false, wenn `HttpTransportFailure` am selben Slot), `useSftpInput` (false, wenn `SftpInputFailure` am selben Slot oder `SftpInputSupported == false`).
2. `LiveStreamMediaResolver.ResolveAsync(mediaItemId, runDuration, useHttpInput, useSftpInput, ct)` — Stufen 1–3 wie Phase 1.
3. Neue Mittelstufe: `SourceType == Sftp && useSftpInput && LiveStreamOptions.UseSftpInput` → Remote-Pfad über `SftpProbeUriBuilder.ResolveRemotePath(collection, item)`, URI über `SftpProbeUriBuilder.TryBuildUri(source.Host, source.Port, source.Username, source.Password, remotePath)` → `ForSftp` mit `ResolutionNote` (warum HTTP nicht griff).
4. Sonst → `ForStream` mit `ResolutionNote`, die zusätzlich die sftp-Gründe nennen kann („keine sftp://-URI bildbar (fehlende Credentials oder kein absoluter Remote-Pfad)", „`LiveStream:UseSftpInput` deaktiviert", „ffmpeg ohne sftp-Protokoll erkannt").
5. `Started`-`Detail` nennt die Eingangsart nun Datei/HTTP/**sftp**/pipe:0.
6. `LiveStreamStandbyProvider.ResolveMediaInputDefaultAsync` ruft `ResolveAsync` mit `useSftpInput: true` — die Standby-Vorbereitung nutzt die seekbare sftp-Stufe ebenfalls; ein Build ohne libssh scheitert dort toleriert (bestehende Fehlerbehandlung: `_failedMediaItemId` gegen Retry-Sturm, bisheriger/Standard-Clip bleibt aktiv).

Beteiligte Klassen/Komponenten: `LiveStreamCoordinator`, `LiveStreamMediaResolver`, `LiveStreamInput`, `SftpProbeUriBuilder`, `InternalMediaStreamTokenService`, `LiveStreamOptions`, `LiveStreamWorker`, `LiveStreamStandbyProvider`.

### Exit-Klassifikation mit Eingangs-Fehlerflags

1. `FfmpegLiveStreamSegmenter.DrainStandardErrorAsync` endet mit stderr-EOF → `ReportProcessExit` prüft wie bisher geplanten Stop und planmäßiges Slotende.
2. Bei unerwartetem Exit wird klassifiziert: `HttpTransportFailure` (nur `HttpUrl`-Jobs, unveränderte `ContainsHttpTransportError`-Marker); `UndecodableInput` (Demux-Marker via `ContainsUndecodableInputError` auf **jedem** Eingang — `partial file`, `Invalid data found when processing input`, `Could not find codec parameters`, `Cannot determine format`, `moov atom not found`; Transport-Flags haben Vorrang, weil bei Transportversagen der Inhalt gar nicht erreicht wurde). **Phase 2:** `SftpInputFailure` (nur `SftpUrl`-Jobs: Transport-, Auth- und Schema-Marker über `ContainsSftpInputError`/`ContainsUnsupportedSchemeError`); Schema-Marker bei einem `SftpUrl`-Job setzen zusätzlich den prozessweiten Latch `SftpInputSupported = false`.
3. `LastUnexpectedExit` erhält die neuen Flags; die Warnzeile wird um die Pump-Statistik ergänzt, sofern der Pump-Task bereits beendet ist (`BytesPumped`/`ExpectedBytes` — nicht blockierend eingeholt; der Pump schreibt seine Bytezahl ohnehin in seine eigene Abschlusszeile). Token- und Credential-Maskierung bleiben unverändert.

Beteiligte Klassen/Komponenten: `FfmpegLiveStreamSegmenter`, `SegmenterProcessSupport`, `FfmpegErrorMarkers`, `LiveStreamExitInfo`, `ILiveStreamSegmenter` (Phase 2).

### Coordinator-Fallback-Progression und Slot-Skip

1. `LastUnexpectedExit` am selben `SlotId` mit `UndecodableInput` → `_segmenter.StopAsync`, `LiveStreamSyncResult(LiveStreamSyncAction.InputUnplayable, now.EndUtc, Detail)` — kein erneuter Job bis zum nächsten Sendeplatz. **Keine** eigene Coordinator-Warnung in diesem Zweig: die einmalige Diagnose (stderr-Tail + Pump-Statistik) steht bereits in der `ReportProcessExit`-Warnung; das `Detail` trägt Slot und Grund für die Worker-Zeile.
2. `HttpTransportFailure` am selben Slot → `useHttpInput = false` (der nächste Versuch nutzt **Phase 1:** Pipe / **Phase 2:** sftp oder Pipe statt HTTP). Der bisherige Per-Tick-Infolog an dieser Stelle (`LiveStreamCoordinator.cs:124-129`) entfällt — die Fallback-Ursache steht einmalig in der `ResolutionNote` des `Started`-`Detail`s.
3. **Phase 2:** `SftpInputFailure` am selben Slot oder `SftpInputSupported == false` → `useSftpInput = false` (nur noch Pipe).
4. `UndecodableInput`/Fehler an einem **anderen** Slot oder gar kein Vorschaden → volle verfügbare Kette.
5. `LiveStreamWorker` loggt die neue Aktion `InputUnplayable` mit Detail auf Information; identische `(Action, Detail)`-Wiederholungen von `MediaUnavailable`/`InputUnplayable` werden auf `Debug` heruntergestuft (Dedup über den zuletzt geloggten Schlüssel im Singleton-Worker — der Poll-Takt läuft bis Slotende weiter, der Dauerzustand erzeugt aber nur eine Information-Zeile).

Beteiligte Klassen/Komponenten: `LiveStreamCoordinator`, `ILiveStreamSegmenter`, `LiveStreamMediaResolver`, `LiveStreamWorker`, `LiveStreamSyncAction`.

### Vollständigkeitsdiagnose des Eingabe-Pumps (Phase 1)

1. `SegmenterProcessSupport.PumpInputAsync` öffnet die Quelle wie bisher (`null` → `SourceUnavailable`).
2. Statt `CopyToAsync` läuft eine manuelle Pufferschleife, die `BytesPumped` mitzählt; `ExpectedBytes` wird einmalig aus `source.Length` gelesen, wenn `source.CanSeek` (Fehler tolerant → `null`).
3. Quellende: `ExpectedBytes` bekannt und `BytesPumped < ExpectedBytes` → Outcome `Truncated` + Warnung „Eingabe-Pump unvollständig: {n} von {m} Bytes — Quelle brach vorzeitig ab"; sonst `Completed` + Debug „Eingabe-Pump vollständig: {n} Bytes".
4. Erwartete Pipe-Brüche/Fehler bleiben wie bisher (`Completed`-Debug bzw. `Failed`-Warnung), liefern aber die bis dahin gepumpte Bytezahl im Ergebnis mit.
5. `FfmpegLiveStreamSegmenter.PumpInputAsync` wertet das Ergebnis für die `SourceUnavailable`-Warnung und die Exit-Diagnose aus; `LiveStreamStandbySegmenter.PumpInputAsync` behandelt `Truncated` wie `Failed` → Prozess-Kill (Teilmaterial würde sonst als Standby-Clip übernommen). `FfprobeMediaDurationProbe` nutzt `PumpInputAsync` nicht (eigener `_executeAsync`-Pfad) — die beiden Aufrufer sind damit vollständig.

Beteiligte Klassen/Komponenten: `SegmenterProcessSupport`, `SegmenterPumpOutcome`, `SegmenterPumpResult`, `FfmpegLiveStreamSegmenter`, `LiveStreamStandbySegmenter`.

## Neue Klassen

| Klasse | Typ | Zweck |
|--------|-----|-------|
| `SegmenterPumpResult` | interner Record (in `SegmenterProcessSupport.cs`) | Ergebnis des Eingabe-Pumps: `Outcome` (`SegmenterPumpOutcome`), `BytesPumped` (`long`), `ExpectedBytes` (`long?`). |
| `TruncatingReadStream` | `internal` Test-Hilfsklasse unter `VideoWebPlayer.Tests/Helpers/` (eigene Datei, Muster wie `ThrottledStream`/`StalledStream` in `FfmpegToolDownloadTestDoubles.cs`) | Seekbarer Fake-Stream, dessen `Length` größer ist als die tatsächlich gelieferten Bytes — provoziert den `Truncated`-Pfad. Wird von `SegmenterProcessSupportTests` **und** `LiveStreamStandbySegmenterTests` genutzt — als private nested class wäre sie nicht dateiübergreifend teilbar. |

## Änderungen an bestehenden Klassen

### Phase 1 (Primärfix + Diagnose/Robustheit)

### `InternalMediaStreamTokenService`

- **Geänderter Konstruktor:** neuer optionaler Parameter `IConfiguration? configuration = null` (Default: leere Konfiguration via `ConfigurationBuilder`); DI löst `IConfiguration` automatisch auf, bestehende Test-Konstruktionen kompilieren unverändert.
- **Geänderte Methoden:** `ResolveBaseUrl` — neue Stufe 3: `Kestrel:Endpoints`-Children enumerieren, jede `Url` über `TryNormalizeServerAddress` normalisieren (Endpunkte mit `Protocols` ohne `Http1`/`Http2` werden übersprungen), erster http-fähiger Treffer gewinnt; **Cache-Korrektur:** `_baseUrlResolved` nur bei positivem Ergebnis setzen (Ist setzt es unbedingt → `null` wurde prozessweit gecacht); die einmalige Warnung benennt neben der HTTP-Laufzeitermittlung auch den Live-Stream-HTTP-Eingang und die geprüften Erkennungswege. `TryNormalizeServerAddress` — lehnt zusätzlich Port ≤ 0 ab (dynamischer Port ist als Loopback-Basis unbrauchbar).
- **Neuer Zustand:** `_invalidConfigWarned` (`bool`) — Einmal-Latch für die „ungültige `HttpProbeBaseUrl`"-Warnung, die zuvor implizit durch den unbedingten `_baseUrlResolved`-Latch einmalig war.

### `LiveStreamInput` (Record)

- **Neue Eigenschaften:** `ResolutionNote` (`string?`, optionaler Positionsparameter am Ende) — Klartext-Grund, warum kein bevorzugter Eingang gewählt wurde; `InputKindLabel` (interner Getter) — „Datei"/„HTTP"/(Phase 2: „sftp")/„pipe:0" für `Started`-Detail und Logzeilen.
- **Geänderte Methoden:** `ForStream` erhält optionalen `resolutionNote`-Parameter (Default `null`, bestehende Aufrufe unverändert).
- **Neue Methoden:** `ToString()`-Override — maskiert die URL-Eigenschaften (`HttpUrl` mit dem Stream-Token via `MaskStreamTokenInText`; Phase 2 zusätzlich `SftpUrl` mit Credentials via `SftpProbeUriBuilder.MaskCredentialsInText`). Als positionaler Record würde `LiveStreamInput` bei direktem Loggen (`{Input}`-Platzhalter, `ToString`) alle Parameter im Klartext ausgeben — heute passiert das nirgends, der Override ist eine billige Absicherung für die Zukunft.

### `LiveStreamMediaResolver`

- **Geänderte Methoden:** `ResolveAsync` — `ForStream`-Ergebnisse tragen eine `ResolutionNote` mit der konkreten Fallback-Ursache.

### `SegmenterProcessSupport` (interne Hilfsklasse)

- **Geänderte Methoden:** `PumpInputAsync` — manuelle Kopierschleife mit Bytezählung und `ExpectedBytes`-Ermittlung, Rückgabetyp `SegmenterPumpResult`, Bytezahlen in den Abschluss-Logs, neues Outcome `Truncated`.
- **Enum `SegmenterPumpOutcome`:** neuer Wert `Truncated` — Quelle endete vor der bekannten Dateigröße.

### `FfmpegErrorMarkers` (interne Hilfsklasse)

- **Neue Methoden:** `ContainsUndecodableInputError(string? stderr)` — Demux-Marker („partial file", „Invalid data found when processing input", „Could not find codec parameters", „Cannot determine format", „moov atom not found").

### `LiveStreamExitInfo` (Record)

- **Neue Eigenschaften:** `UndecodableInput` (`bool`, Default `false`) — als optionaler Positionsparameter am Ende, bestehende Konstruktoraufrufe kompilieren unverändert.

### `FfmpegLiveStreamSegmenter`

- **Geänderte Methoden:** `StartAsync` — der Pump-Task wird als Referenz an `DrainStandardErrorAsync` übergeben (statt nur ins Feld), damit die Klassifikation bei Jobwechsel nicht den falschen Pump liest; `DrainStandardErrorAsync` — reicht das Pump-Ergebnis (falls bereits abgeschlossen) an `ReportProcessExit`; `ReportProcessExit` — wertet Demux-Marker aus, setzt `UndecodableInput` und ergänzt Pump-Bytezahlen in der Warnzeile; `PumpInputAsync` — verarbeitet den neuen `SegmenterPumpResult`-Rückgabetyp (`SourceUnavailable`-Warnung unverändert).

### `LiveStreamCoordinator`

- **Geänderte Methoden:** `SynchronizeStreamAsync` — Skip-Verzweigung bei `UndecodableInput` am selben Slot (vor der Auflösung: `StopAsync`, `InputUnplayable` mit `WakeAtUtc = now.EndUtc` und Grund im `Detail` — **keine** eigene Warnung je Tick, die einmalige Diagnose bleibt in der `ReportProcessExit`-Warnung); `Started`-`Detail` um Eingangsart (`InputKindLabel`) und `ResolutionNote` ergänzt.
- **Entfernter Log:** der Per-Tick-`LogInformation`-Fallbackhinweis (`LiveStreamCoordinator.cs:124-129`, feuert jeden ~30-s-Tick, solange `useHttpInput == false`) entfällt — dieselbe Information steht einmalig in der `ResolutionNote` des `Started`-`Detail`s; der scoped Coordinator kann nicht selbst deduplizieren.
- **Enum `LiveStreamSyncAction`:** neuer Wert `InputUnplayable` — Sendeplatz auflösbar, aber mit keinem verfügbaren Eingang lesbar; Restlaufzeit wird übersprungen.

### `LiveStreamWorker`

- **Geänderte Methoden:** `ExecuteAsync` — `InputUnplayable` wird wie `MediaUnavailable` auf Information geloggt; zusätzlich Dedup: das zuletzt auf Information geloggte `(Action, Detail)`-Paar wird gehalten, identische Wiederholungen von `MediaUnavailable`/`InputUnplayable` gehen auf `Debug` (Dauerzustand erzeugt eine Informationszeile statt einer alle ~30 s).

### `LiveStreamStandbySegmenter`

- **Geänderte Methoden:** `PumpInputAsync` — neuer Rückgabetyp; Outcomes `SourceUnavailable`/`Failed`/`Truncated` führen wie bisher zum Prozess-Kill.

### Phase 2 (sftp-Eingangsstufe)

### `LiveStreamInput` (Record)

- **Neue Eigenschaften:** `SftpUrl` (`string?`, optionaler Positionsparameter am Ende) — direkte `sftp://`-URI für ffmpeg/libssh (seekbar); `InputKindLabel` liefert zusätzlich „sftp".
- **Neue Methoden:** `ForSftp(string url, string? resolutionNote = null)` — Fabrik für den sftp-Eingang.
- **Geänderte Methoden:** `ToString()` (aus Phase 1) maskiert zusätzlich `SftpUrl` via `SftpProbeUriBuilder.MaskCredentialsInText` — die URI trägt Credentials im Klartext.

### `LiveStreamOptions` (Konfigurationsklasse)

- **Neue Eigenschaften:** `UseSftpInput` (`bool`, Default `true`) — schaltet den `sftp://`-Direkt-Eingang entfernter SFTP-Quellen ab (z. B. auf Mehrbenutzersystemen, weil die URI Zugangsdaten in der Prozess-Kommandozeile trägt).

### `SftpProbeUriBuilder` (interne Hilfsklasse)

- **Neue Methoden:** `ResolveRemotePath(MediaCollection collection, MediaItem item)` (`internal static`, `string?`) — hierhin verlagerte Logik des privaten `MediaDurationProbeService.ResolveRemotePath` (`item.Path` wenn absolut, sonst `CombinePath(collection.Path, …)`), damit Probe- und Segmenter-Pfad denselben Remote-Pfad bilden. Signatur mit expliziter `collection`, weil der Resolver `item` ohne `MediaCollection`-Navigation lädt (`AsNoTracking`, kein `Include`).

### `MediaDurationProbeService`

- **Geänderte Methoden:** private `ResolveRemotePath(MediaItem)` entfällt; `ProbeRemoteItemAsync` ruft `SftpProbeUriBuilder.ResolveRemotePath(item.MediaCollection, item)` — Verhalten unverändert.

### `LiveStreamMediaResolver`

- **Geänderte Methoden:** `ResolveAsync` — neuer Parameter `bool useSftpInput = true` (vor `cancellationToken`); zwischen HTTP- und Pipe-Zweig die sftp-Stufe (`SourceType == Sftp`, `useSftpInput`, `LiveStreamOptions.UseSftpInput`, `TryBuildUri`); `ForSftp`-Ergebnisse tragen eine `ResolutionNote` mit der HTTP-Fallback-Ursache.

### `SegmenterProcessSupport` (interne Hilfsklasse)

- **Geänderte Methoden:** `InputSpec` — `SftpUrl` wird wie `HttpUrl` als quotiertes `-i`-Argument geliefert (Reihenfolge `FilePath` → `HttpUrl` → `SftpUrl` → `pipe:0`).

### `FfmpegErrorMarkers` (interne Hilfsklasse)

- **Neue Methoden:** `ContainsSftpInputError(string? stderr)` — Transport-/Auth-Marker für `sftp://`-Eingänge (Verbindungsfehler, „Permission denied", „Authentication"); `ContainsUnsupportedSchemeError(string? stderr)` — Schema-Marker („Protocol not found", „Unknown protocol", „not on whitelist").

### `LiveStreamExitInfo` (Record)

- **Neue Eigenschaften:** `SftpInputFailure` (`bool`, Default `false`) — optionaler Positionsparameter am Ende.

### `ILiveStreamSegmenter` (Interface)

- **Neue Eigenschaften:** `SftpInputSupported` (`bool`) — `false`, sobald ein `sftp://`-Lauf am fehlenden Protokoll scheiterte; der Coordinator lässt die sftp-Stufe dann prozessweit aus. **Betroffene Implementierungen:** `FfmpegLiveStreamSegmenter` sowie die Test-Fakes `LiveStreamCoordinatorTests.FakeSegmenter` und `LiveStreamWorkerTests.FakeSegmenter` (`LiveStreamWorkerTests.cs:31-42`) — letzterer war im bisherigen Plan nicht eingeplant und muss das Member ebenfalls implementieren, sonst bricht der Build.

### `FfmpegLiveStreamSegmenter`

- **Neue Eigenschaften:** `SftpInputSupported` (volatile Latch, initial `true`).
- **Geänderte Methoden:** `ReportProcessExit` — wertet zusätzlich `SftpUrl`-Transport-/Schema-Marker aus und setzt `SftpInputFailure` bzw. den Schema-Latch.

### `LiveStreamCoordinator`

- **Geänderte Methoden:** `SynchronizeStreamAsync` — `useSftpInput` aus `LastUnexpectedExit`/`SftpInputSupported` abgeleitet und an `ResolveAsync` gereicht; `Started`-Detail nennt die sftp-Eingangsart. (Der früher hier erwähnte Per-Tick-Fallback-Infolog wurde bereits in Phase 1 entfernt — nichts anzupassen.)

### `LiveStreamStandbySegmenter` / `LiveStreamStandbyProvider`

- `BuildArguments` benötigt keine Anpassung — geht über `InputSpec`/`NoStdinArg` automatisch mit `sftp://` um (Credential-Maskierung im stderr-Log bereits vorhanden).
- **Geänderte Methoden:** `LiveStreamStandbyProvider.SourceDescriptor` — `SftpUrl`-Eingänge erhalten den Deskriptor `"sftp"` (einmalige Cache-Erneuerung, kein Datenverlust); die Methode wird `internal` statt `private`, damit ein kleiner Unit-Test die Abbildung absichert (die Testassembly sieht Internals bereits, vgl. direkte `SegmenterProcessSupport`-Aufrufe in den Tests).
- **Geänderte Methoden:** `LiveStreamStandbyProvider.ResolveMediaInputDefaultAsync` (`LiveStreamStandbyProvider.cs:311`) — der Aufruf `ResolveAsync(mediaItemId, runDuration: null, useHttpInput: true, cancellationToken)` bricht mit dem neuen `useSftpInput`-Parameter vor `cancellationToken` (positionales `CancellationToken` → CS1503); er wird auf `useSftpInput: true` mit benanntem `cancellationToken` umgestellt — die Standby-Auflösung darf die seekbare sftp-Stufe nutzen (`LiveStream:UseSftpInput` greift im Resolver; ein ffmpeg ohne libssh scheitert toleriert über die bestehende Fehlerbehandlung).

## Datenbankmigrationen

Keine.

## Validierungsregeln

Keine — keine neuen Eingaben; `UseSftpInput` (Phase 2) ist ein Boolescher Konfigurationsschalter.

## Konfigurationsänderungen

| Eintrag | Typ | Standardwert | Zweck |
|---------|-----|--------------|-------|
| `LiveStream:UseSftpInput` (`LiveStreamOptions`, `appsettings.json`) — **Phase 2** | `bool` | `true` | Schaltet den `sftp://`-Direkt-Eingang entfernter SFTP-Quellen ab; `false` = nach HTTP nur noch `pipe:0` (z. B. wegen Credentials in der Prozess-Kommandozeile). |
| `AutoUpdate:ProtectedFiles[].JsonKeys` — Eintrag `LiveStream:UseSftpInput` (`appsettings.json`, **Phase 2**) | Whitelist-Eintrag | — | Sonst überlebt ein Operator-Override auf `UseSftpInput` das Paket-Update nicht (Json-Merge-Whitelist). Im selben Schritt wird die **Bestandslücke** geschlossen: `LiveStream:UseHttpInput`, `MediaProbe:HttpProbeBaseUrl` und `MediaProbe:HttpProbeEnabled` fehlen dort bereits heute und werden ebenfalls ergänzt (Bestandskorrektur in eigenem Commit, im Abnahmebericht zu nennen). |

**Keine Änderung an `appsettings.Production.json`:** `Kestrel:Endpoints:Http:Url = http://*:5002` ist dort bereits konfiguriert und wird von der neuen Erkennungsstufe gelesen. `MediaProbe:HttpProbeBaseUrl` bleibt ein reiner Operator-Override, kein Pflicht-Eintrag.

## Seiteneffekte und Risiken

- **Kestrel-Endpoints-Auswahl (Phase 1):** Bei mehreren konfigurierten http-Endpunkten gewinnt der erste — Loopback-Erreichbarkeit ist für jeden gebundenen http-Endpunkt gegeben (Wildcards → `127.0.0.1`); ein https-Endpunkt wird wie bisher nie gewählt. Risiko: ein Endpunkt, der an eine nicht-loopback-erreichbare Adresse bindet (z. B. nur externe IP), würde eine theoretisch unerreichbare Basis-URL liefern — der Fehler äußert sich dann als HTTP-Transport-Fehlschlag mit bereits vorhandenem Fallback.
- **Cache-Semantik-Korrektur (Phase 1, bewusste Verhaltensänderung — betrifft AK 5):** `ResolveBaseUrl` cachte bisher auch das `null`-Ergebnis prozessweit; jetzt wird nur ein positives Ergebnis gecacht. Praktische Folge: in fehlkonfigurierten Installationen ohne jede http-Bindung läuft die dreistufige Erkennung je Aufruf erneut (billig); in `UseUrls`/`ASPNETCORE_URLS`-Installationen wird die Startup-Race nicht mehr prozessweit vergiftet. Die Korrektur folgt dem dokumentierten Intent der Code-Kommentare (`InternalMediaStreamTokenService.cs:57-59, 184-186`) und ist im Abnahmebericht ausdrücklich zu benennen.
- **Port-≤-0-Ablehnung (Phase 1):** `TryNormalizeServerAddress` liefert für `http://*:0`/`http://127.0.0.1:0` jetzt `null` statt einer unbrauchbaren Basis-URL — betrifft auch die `Addresses`-Stufe (bisheriger Randfall, z. B. `appsettings.Testing.json` `http://127.0.0.1:0`; in `WebApplicationFactory`-Hosts ungefährlich, da dort `Addresses` den echten zufälligen Port enthält und Stufe 2 vor Stufe 3 greift).
- **Reihenfolge-Konsistenz (Phase 1):** Kestrel-Endpoints werden erst **nach** den tatsächlich gebundenen Adressen geprüft — eine konfigurierte, aber nicht mehr gebundene Url gewinnt also nie gegen die Laufzeitwahrheit.
- **Prozess-Kommandozeile (Phase 2):** Die `sftp://`-URI enthält Benutzername und Passwort und ist damit für lokale Benutzer über `/proc/<pid>/cmdline` bzw. `ps` lesbar — in Logtexten bleibt sie maskiert (`SftpProbeUriBuilder.MaskCredentialsInText` wird bereits auf Aufruf- und stderr-Logs angewendet). Als Opt-out dient `LiveStream:UseSftpInput`; Hinweis kommt in die Hilfe-Doku.
- **Standby-Pfad:** `LiveStreamStandbySegmenter`/`LiveStreamStandbyProvider` profitieren (Phase 2) automatisch vom seekbaren sftp-Eingang (Offline-Transcode entfernter Clips); der geänderte `SourceDescriptor` (`"sftp"` statt `"stream"`) invalidiert den Disk-Cache einmalig → einmaliger Neu-Transcode. Ein ffmpeg ohne libssh scheitert im Standby-Pfad toleriert (`_failedMediaItemId`-Schutz, bisheriger/Standard-Clip bleibt).
- **Standby-Teilmaterial (Phase 1):** Ein `Truncated`-Pump führt jetzt zum Prozess-Kill — vorher hätte ein vorzeitig endender Quell-Stream unbemerkt einen kürzeren Standby-Clip erzeugt; das neue Verhalten ist die Korrektur, nicht die Regression.
- **Zusätzliche SFTP-Verbindung (Phase 2):** ffmpeg/libssh öffnet eine eigene SFTP-Sitzung parallel zu den `SftpMediaSourceReader`-Verbindungen — der SFTP-Server muss parallele Sessions desselben Kontos erlauben (üblich, aber abhängig vom Server-Setup).
- **Skip-Semantik (Phase 1):** Ein als nicht lesbar klassifizierter Sendeplatz bleibt für seine Restlaufzeit stumm (Playlist liefert weiter Standby aus) — bewusste Degradierung statt Restart-Sturm; betrifft auch korrupte **lokale** Dateien neu. Log-Aufkommen: eine `ReportProcessExit`-Warnung + eine `InputUnplayable`-Informationszeile je betroffenem Sendeplatz (Dedup), nicht alle ~30 s.
- **Restrisiko ohne seekbaren Eingang:** In Installationen ohne http-Bindung **und** ohne sftp-Fähigkeit/-Credentials (bzw. bei deaktiviertem `LiveStream:UseSftpInput`) läuft jeder betroffene Sendeplatz genau einmal in den Pipe-Fehlschlag und wird dann übersprungen — deutlich besser als die heutige Restart-Schleife, aber weiterhin schwarz; Betreiberhinweis in der Doku (`MediaProbe:HttpProbeBaseUrl` bzw. http-Bindung).
- **`-ss` auf Pipe:** bleibt unverändert ein Echtzeit-Discard bei nicht seekbarem Eingang — die seekbaren Stufen sind der eigentliche Fix; wird in der Doku festgehalten.
- **`ILiveStreamSegmenter`-Erweiterung (Phase 2):** `FakeSegmenter` in `LiveStreamCoordinatorTests` **und** `LiveStreamWorkerTests` (`VideoWebPlayer.Tests/LiveStreamWorkerTests.cs:31-42`) muss das neue Member `SftpInputSupported` implementieren — beide Stellen brechen sonst den Build. Weitere `ILiveStreamSegmenter`-Implementierungen existieren nicht (E2E-/Testing-Umgebung hostet den `LiveStreamWorker` nicht).
- **`ResolveAsync`-Signatur (Phase 2):** Der vor `cancellationToken` eingefügte Parameter `useSftpInput` bricht zwei positionale Aufrufe — `LiveStreamStandbyProvider.ResolveMediaInputDefaultAsync` (`LiveStreamStandbyProvider.cs:311`, Umstellung im selben Schritt eingeplant) und den Coordinator-Aufruf (`LiveStreamCoordinator.cs:116`, Umstellung in Schritt 16). **Zwischen Schritt 13 und Schritt 16 kompiliert der Stand nicht** (CS1503) — bei schrittweisen Commits ein bewusster, benannter nicht-buildbarer Zwischencommit; alternativ die Schritte 13–16 in einem Commit zusammenfassen. Die Test-Aufrufe nutzen durchgängig benannte `cancellationToken:`-Argumente und bleiben kompilierfähig.
- **Per-Tick-Fallback-Infolog entfernt (Phase 1, bewusste Verhaltensänderung):** der `LogInformation`-Hinweis bei `useHttpInput == false` (`LiveStreamCoordinator.cs:124-129`) erschien jeden ~30-s-Tick bis Slotende — er wird durch die einmalige `ResolutionNote` im `Started`-`Detail` ersetzt (kein Informationsverlust, weniger Log-Spam; betrifft AK 5 und ist im Abnahmebericht zu benennen).
- **`LiveStreamInput`-`ToString()` (Phase 1/2):** der positionale Record erhält einen maskierenden `ToString()`-Override — ohne ihn würde ein versehentliches direktes Loggen des Records `HttpUrl` (Stream-Token) bzw. `SftpUrl` (Credentials) im Klartext ausgeben. Heute wird der Record nirgends direkt geloggt; der Override ist eine billige Absicherung für künftige Log-Aufrufe.
- **`AutoUpdate`-Whitelist-Bestandslücke:** `LiveStream:UseHttpInput` und `MediaProbe:HttpProbeBaseUrl`/`HttpProbeEnabled` sind heute schon nicht in `AutoUpdate:ProtectedFiles/JsonKeys` — Operator-Overrides überleben Paket-Updates nicht. Wird im Konfigurationsschritt als Bestandskorrektur mitgeschlossen (eigener Commit).
- **Bekannter Flaky-Test im selben Bereich:** `FfmpegLiveStreamSegmenterE2ETests.SecondJob_PlaylistContinuesAndMarksDiscontinuity` (dokumentiert in `inventory/tests.md`) — nicht Teil dieses Schritts, im Bericht ehrlich zu behandeln.

## Umsetzungsreihenfolge

### Phase 1 — Primärfix (Kestrel-Erkennung + Cache-Korrektur) + Diagnose/Robustheit

1. **Basis-URL-Erkennung um Kestrel-Endpoints erweitern + Cache-Semantik korrigieren**
   - Voraussetzungen: Keine (`Microsoft.Extensions.Configuration` ist im Hosting-Bundle enthalten; `IConfiguration` steht in DI standardmäßig bereit).
   - Beschreibung: `InternalMediaStreamTokenService` — optionaler `IConfiguration`-Konstruktorparameter; `ResolveBaseUrl` enumeriert `Kestrel:Endpoints`-Children und normalisiert deren `Url` über `TryNormalizeServerAddress` (nach den gebundenen Adressen; `Protocols` ohne `Http1`/`Http2` — `OrdinalIgnoreCase`-Vergleich — überspringt den Endpunkt); `TryNormalizeServerAddress` lehnt Port ≤ 0 ab; `_baseUrlResolved` nur bei positivem Ergebnis setzen (Cache-Korrektur zum dokumentierten Intent) + `_invalidConfigWarned`-Latch für die ungültige-Basis-URL-Warnung; Warnungstext um Live-Stream-Eingang und geprüfte Wege ergänzen; `InternalMediaStreamTokenServiceTests.CreateService` um optionale Konfigurations- und Logger-Parameter erweitern. **Dieser Schritt allein behebt den produktiven Fehler.**

2. **`ResolutionNote` an `LiveStreamInput` + Resolver-Begründung**
   - Voraussetzungen: Keine.
   - Beschreibung: `LiveStreamInput.ResolutionNote` + interner `InputKindLabel`-Getter; optionaler Parameter an `ForStream`; maskierender `ToString()`-Override (HttpUrl-Token); `LiveStreamMediaResolver.ResolveAsync` befüllt die Note am Pipe-Ergebnis (Option aus, HTTP-Transport-Vorschaden, keine http-Basis-URL inkl. Kestrel-Endpoints); `LiveStreamCoordinator` hängt Eingangsart + Note an das `Started`-`Detail`.

3. **Demux-Fehlermarker**
   - Voraussetzungen: Keine.
   - Beschreibung: `FfmpegErrorMarkers.ContainsUndecodableInputError` samt Markerliste anlegen.

4. **Pump mit Bytezählung**
   - Voraussetzungen: Keine.
   - Beschreibung: `SegmenterPumpOutcome.Truncated` und `SegmenterPumpResult` einführen; `PumpInputAsync` auf zählende Kopierschleife mit `ExpectedBytes` und Byte-Logs umstellen; Aufrufer `FfmpegLiveStreamSegmenter` und `LiveStreamStandbySegmenter` an den neuen Rückgabetyp anpassen (`Truncated` → Kill im Standby-Pfad).

5. **Exit-Diagnose: `UndecodableInput`**
   - Voraussetzungen: Schritte 3 und 4.
   - Beschreibung: `LiveStreamExitInfo.UndecodableInput` neu; `FfmpegLiveStreamSegmenter`: Pump-Referenz in die Drain-Klassifikation reichen, `UndecodableInput`-Klassifikation in `ReportProcessExit`, Pump-Statistik in der Warnzeile.

6. **Coordinator-Slot-Skip + Worker-Logging/-Dedup**
   - Voraussetzungen: Schritte 2 und 5.
   - Beschreibung: `LiveStreamSyncAction.InputUnplayable` neu; `LiveStreamCoordinator`: Skip-Verzweigung bei `UndecodableInput` am selben Slot (`StopAsync`, `InputUnplayable` mit `WakeAtUtc = now.EndUtc` und Grund im `Detail` — keine eigene Warnung je Tick), `Started`-Detail mit `InputKindLabel`/`ResolutionNote`; der bisherige Per-Tick-Fallback-Infolog (`LiveStreamCoordinator.cs:124-129`) entfällt zugunsten der `ResolutionNote` im `Started`-`Detail`; `LiveStreamWorker` loggt die neue Aktion und dedupliziert identische `(Action, Detail)`-Wiederholungen von `MediaUnavailable`/`InputUnplayable` auf `Debug`.

7. **Unit-/Integrationstests Phase 1**
   - Voraussetzungen: Schritte 1–6.
   - Beschreibung: Neue Tests nach Tabelle „Neue Tests" (Phase-1-Zeilen) schreiben und betroffene bestehende Tests anpassen.

8. **ffmpeg-E2E-Tests Phase 1**
   - Voraussetzungen: Schritte 5 und 6.
   - Beschreibung: Zwei neue Szenarien in `FfmpegLiveStreamSegmenterE2ETests` (s. E2E-Tabelle); laufen nur mit echtem ffmpeg (vorhanden laut Bestandsaufnahme).

9. **Dokumentation Phase 1**
   - Voraussetzungen: Implementierung Phase 1 steht (Schritte 1–6).
   - Beschreibung: `docs/help/tv-sender.md` — Abschnitt „entfernte Quellen" (~Z. 559-570) um die automatische Basis-URL-Erkennung inkl. `Kestrel:Endpoints` und den Slot-Skip bei nicht lesbarem Eingang ergänzen; moov-Hinweis (~Z. 747-752) entsprechend präzisieren. `docs/help/tv-sender-ablauf-technisch.md` — den Abschnitt „Geordneter Stop der Live-Stream-Segmentierung (Eingabe-Pumpe)" (Z. 357-439) auf `SegmenterPumpResult`/`Truncated` und Bytezählung aktualisieren (`PumpInputAsync` liefert dort noch `SegmenterPumpOutcome`-Rückgaben — teilweise falsch) und die Eingangsauflösung (`ResolveAsync`-Aufruf, ~Z. 328) um die neuen Diagnose-Elemente ergänzen. `docs/help/videolaufzeit.md` — die dort beschriebene Basis-URL-Erkennung aktualisieren: Z. 8 („Die Loopback-Adresse wird aus den gebundenen Adressen der laufenden Instanz ermittelt") und die `MediaProbe:HttpProbeBaseUrl`-Zeile (~Z. 55, „automatische Erkennung aus den gebundenen Server-Adressen") beschreiben jetzt die dreistufige Erkennung Konfiguration → gebundene Adressen → `Kestrel:Endpoints` (beide Verbraucher — ffprobe-Probe und Live-Stream-Eingang — profitieren vom selben Fix). Eintrag in `docs/RELEASE_NOTES.md` (Stil der vorhandenen „What's New"-Einträge).

### Phase 2 — `sftp://`-Direkt-Eingangsstufe (für HTTPS-only-Installationen)

10. **`SftpUrl`-Eingangsart + Eingangs-Spezifikation**
    - Voraussetzungen: Schritt 2 (`ResolutionNote`/`InputKindLabel` existiert).
    - Beschreibung: `LiveStreamInput.SftpUrl`, Fabrik `ForSftp`, `ToString()`-Maskierung auf `SftpUrl` erweitern; `SegmenterProcessSupport.InputSpec` um die `SftpUrl`-Stufe ergänzen; `LiveStreamStandbyProvider.SourceDescriptor` um `"sftp"`-Deskriptor ergänzen (wird `internal`).

11. **Konfiguration `LiveStream:UseSftpInput` + `AutoUpdate`-Whitelist**
    - Voraussetzungen: Keine.
    - Beschreibung: `LiveStreamOptions.UseSftpInput` (Default `true`) und Eintrag in `VideoWebPlayer/appsettings.json` (Section `LiveStream`); `AutoUpdate:ProtectedFiles[].JsonKeys` um `LiveStream:UseSftpInput` ergänzen — im selben Schritt die Bestandsschlüssel `LiveStream:UseHttpInput`, `MediaProbe:HttpProbeBaseUrl`, `MediaProbe:HttpProbeEnabled` nachziehen (Bestandslücke, eigener Commit).

12. **Gemeinsame Remote-Pfad-Auflösung**
    - Voraussetzungen: Keine.
    - Beschreibung: `SftpProbeUriBuilder.ResolveRemotePath(collection, item)` neu; `MediaDurationProbeService` auf die gemeinsame Hilfe umstellen.

13. **Resolver: sftp-Stufe + betroffene Aufrufstellen**
    - Voraussetzungen: Schritte 10–12.
    - Beschreibung: `LiveStreamMediaResolver.ResolveAsync` um `useSftpInput`-Parameter und die `ForSftp`-Stufe (`ResolveRemotePath` + `TryBuildUri`) erweitern; `ResolutionNote` um die sftp-Gründe ergänzen; **`LiveStreamStandbyProvider.ResolveMediaInputDefaultAsync` (Z. 311) auf `useSftpInput: true` + benanntes `cancellationToken` umstellen** (sonst CS1503); der `LiveStreamCoordinator`-Aufruf (`LiveStreamCoordinator.cs:116`, positionales `cancellationToken` → ebenfalls CS1503) folgt in Schritt 16 — **der Zwischenstand kompiliert daher nicht**: Schritte 13–16 in einem Commit zusammenfassen oder den Zwischencommit bewusst als nicht-buildbar kennzeichnen.

14. **sftp-Fehlermarker**
    - Voraussetzungen: Keine.
    - Beschreibung: `FfmpegErrorMarkers` um `ContainsSftpInputError` und `ContainsUnsupportedSchemeError` samt Markerlisten erweitern.

15. **Exit-Diagnose: `SftpInputFailure` + `SftpInputSupported`-Latch**
    - Voraussetzungen: Schritte 5, 10 und 14.
    - Beschreibung: `LiveStreamExitInfo.SftpInputFailure` neu; `ILiveStreamSegmenter.SftpInputSupported` neu (beide Test-Fakes `LiveStreamCoordinatorTests.FakeSegmenter` und `LiveStreamWorkerTests.FakeSegmenter` implementieren das Member); `FfmpegLiveStreamSegmenter`: Schema-Latch und `SftpInputFailure`-Klassifikation in `ReportProcessExit`.

16. **Coordinator: sftp-Fallback-Progression**
    - Voraussetzungen: Schritte 6, 13 und 15.
    - Beschreibung: `LiveStreamCoordinator`: `useSftpInput`-Ableitung (`SftpInputFailure` am selben Slot oder `SftpInputSupported == false` → Pipe), Übergabe an `ResolveAsync` (stellt den positionalen `cancellationToken`-Aufruf an `LiveStreamCoordinator.cs:116` um — erst danach kompiliert der Stand wieder); der Per-Tick-Fallback-Infolog wurde in Schritt 6 entfernt, nichts anzupassen.

17. **Unit-/Integrationstests Phase 2**
    - Voraussetzungen: Schritte 10–16.
    - Beschreibung: Neue Tests nach Tabelle „Neue Tests" (Phase-2-Zeilen); `FakeSegmenter`-Member in beiden Testklassen; Seeds um SFTP-Credentials ergänzen.

18. **ffmpeg-E2E-Test Phase 2**
    - Voraussetzungen: Schritt 15.
    - Beschreibung: `RealFfmpeg_SftpInputUnreachable_SetsSftpInputFailure` in `FfmpegLiveStreamSegmenterE2ETests` (s. E2E-Tabelle).

19. **Dokumentation Phase 2**
    - Voraussetzungen: Implementierung Phase 2 steht.
    - Beschreibung: `docs/help/tv-sender.md` — dreistufige Kette HTTP → `sftp://` → `pipe:0`, `LiveStream:UseSftpInput`, Credentials-Hinweis und `ffmpeg -protocols`-Prüfung dokumentieren; `docs/help/tv-sender-ablauf-technisch.md` — Eingangsauflösung auf die dreistufige Kette aktualisieren; `docs/RELEASE_NOTES.md` ergänzen.

20. **Regressionsnachweis**
    - Voraussetzungen: Phase 1 und Phase 2 vollständig.
    - Beschreibung: `dotnet build VideoPlayer.sln` in Debug **und** Release fehlerfrei; volle Suite `dotnet test VideoWebPlayer.Tests/VideoWebPlayer.Tests.csproj` grün (bekannte Skipps/Flaky ehrlich benennen).

## Tests

### Neue Tests

| Test / Hilfsmethode | Testklasse | Was wird geprüft / bereitgestellt? |
|--------------------|------------|-------------------------------------|
| `TryCreateStreamUri_OnlyKestrelEndpoint_ReturnsLoopbackUri` (Phase 1) | `InternalMediaStreamTokenServiceTests` | Leere `IServerAddressesFeature` + `Kestrel:Endpoints:Http:Url = http://*:5002` (In-Memory-Config) ⇒ Stream-URI auf `http://127.0.0.1:5002`. |
| `TryCreateProbeUri_OnlyKestrelEndpoint_ReturnsLoopbackUri` (Phase 1) | `InternalMediaStreamTokenServiceTests` | Dasselbe für die ffprobe-Probe-URI (beide Pfade profitieren vom Fix). |
| `TryCreateStreamUri_KestrelCustomEndpointName_ReturnsLoopbackUri` (Phase 1) | `InternalMediaStreamTokenServiceTests` | Endpunkt mit frei gewähltem Namen (z. B. `Kestrel:Endpoints:Api:Url`) wird gefunden — nicht nur `Http`. |
| `TryCreateStreamUri_KestrelHttpsBeforeHttp_SkipsHttpsAndUsesHttp` (Phase 1) | `InternalMediaStreamTokenServiceTests` | Gemischte Endpunkte (`Endpoints: { Https: https://*:443, Http: http://*:5002 }`): der `https`-Eintrag vor dem `http`-Eintrag wird übersprungen ⇒ `http://127.0.0.1:5002` (Skip-und-Weitersuchen-Semantik, Akzeptanzkriterium 1). |
| `TryCreateStreamUri_KestrelHttp3OnlyEndpoint_SkipsEndpoint` (Phase 1) | `InternalMediaStreamTokenServiceTests` | `http://`-Endpunkt mit `Protocols: Http3` (nur UDP) wird übersprungen ⇒ `null` bzw. späterer `Http1`-Endpunkt gewinnt. |
| `TryNormalizeServerAddress_PortZero_ReturnsNull` (Phase 1) | `InternalMediaStreamTokenServiceTests` | `http://*:0`/`http://127.0.0.1:0` (dynamischer Port, vgl. `appsettings.Testing.json`) liefert `null` statt einer unbrauchbaren Basis-URL. |
| `TryCreateProbeUri_AddressesAppearAfterFirstMiss_RetriesAndResolves` (Phase 1) | `InternalMediaStreamTokenServiceTests` | Startup-Race/Cache-Korrektur: leere `Addresses` ⇒ `null`; nach dem Befüllen der Feature-Liste liefert der nächste Aufruf die URI — ein Fehlschlag wird nicht mehr prozessweit gecacht. |
| `TryCreateProbeUri_PositiveResult_StaysCached` (Phase 1) | `InternalMediaStreamTokenServiceTests` | Positive Auflösung bleibt gecacht: nach dem ersten Treffer liefert der zweite Aufruf die URI auch dann, wenn `Addresses` inzwischen leer ist. |
| `TryCreateStreamUri_BoundAddressWins_OverKestrelEndpoint` (Phase 1) | `InternalMediaStreamTokenServiceTests` | Gebundene Adresse hat Vorrang vor dem Kestrel-Konfig-Fallback. |
| `TryCreateStreamUri_KestrelHttpsOnly_ReturnsNull` (Phase 1) | `InternalMediaStreamTokenServiceTests` | Nur-`https`-Endpunkt wird abgelehnt (Loopback-TLS ungeeignet) ⇒ `null`. |
| `TryCreateStreamUri_NoAddressNoKestrel_ReturnsNull` (Phase 1) | `InternalMediaStreamTokenServiceTests` | Weder gebundene Adresse noch Kestrel-Eintrag ⇒ `null` (Absicherung des bisherigen Verhaltens). |
| `TryCreateProbeUri_InvalidBaseUrl_WarnsOnlyOnceAcrossRetries` (Phase 1) | `InternalMediaStreamTokenServiceTests` | Ungültige `MediaProbe:HttpProbeBaseUrl` + zwei `TryCreateProbeUri`-Aufrufe ⇒ genau **eine** „keine gültige Basis-URL"-Warnung (`ListLogger`) — Regressionsschutz für den `_invalidConfigWarned`-Latch: ohne ihn würde die Warnung nach der Cache-Korrektur bei jedem Retry erneut erscheinen (`InternalMediaStreamTokenService.cs:165-168`). |
| `ContainsUndecodableInputError_DemuxMarkers_Match` (Phase 1) | `FfmpegErrorMarkersTests` (neu) | Demux-Marker aus dem Produktiv-Log (`partial file`, `Invalid data found`, `Could not find codec parameters`, `Cannot determine format`, `moov atom not found`) werden erkannt; Text ohne Marker → `false`. |
| `ResolveAsync_PipeFallback_ResolutionNoteNamesCause` (Phase 1) | `LiveStreamMediaResolverTests` | `ResolutionNote` nennt die zutreffende Ursache (z. B. fehlende http-Basis-URL / deaktivierte Option). |
| `PumpInputAsync_Success_ReportsPumpedAndExpectedBytes` (Phase 1) | `SegmenterProcessSupportTests` | `SegmenterPumpResult.BytesPumped`/`ExpectedBytes` korrekt bei `MemoryStream`. |
| `PumpInputAsync_NonSeekableSource_CompletedWithoutExpectedBytes` (Phase 1) | `SegmenterProcessSupportTests` | Nicht seekbare Quelle (`CanSeek == false`) ⇒ `Completed`, `ExpectedBytes == null` — der tolerante Pfad ohne Größenvergleich. |
| `PumpInputAsync_ShortStream_ReturnsTruncated` (Phase 1) | `SegmenterProcessSupportTests` + interne Hilfsklasse `Helpers/TruncatingReadStream` | Quelle kürzer als `Length` ⇒ Outcome `Truncated`, Warnung mit Bytevergleich. |
| `UnexpectedExit_PipeDemuxMarkers_SetsUndecodableInput` (Phase 1) | `FfmpegLiveStreamSegmenterTests` | Pipe-Job + Produktiv-stderr ⇒ `LastUnexpectedExit.UndecodableInput == true`. |
| `UnexpectedExit_FileInputDemuxMarkers_SetsUndecodableInput` (Phase 1) | `FfmpegLiveStreamSegmenterTests` | Korrupte lokale Datei wird ebenfalls als nicht lesbar klassifiziert. |
| `UnexpectedExit_PipeInput_IncludesPumpStatsInWarning` (Phase 1) | `FfmpegLiveStreamSegmenterTests` | Warnzeile enthält Pump-Bytezahlen, wenn der Pump bereits beendet ist. |
| `SynchronizeAsync_UndecodableInputSameSlot_SkipsSlotUntilEnd` (Phase 1) | `LiveStreamCoordinatorTests` | `InputUnplayable`, kein neuer Job, `WakeAtUtc == now.EndUtc`, `StopCalls` ≥ 1. |
| `SynchronizeAsync_UndecodableInputOtherSlot_IgnoresFailure` (Phase 1) | `LiveStreamCoordinatorTests` | Undecodable-Flag an fremdem Slot ⇒ normale Auflösung. |
| `SynchronizeAsync_RemotePipeFallback_StartedDetailNamesInputKindAndNote` (Phase 1) | `LiveStreamCoordinatorTests` | `Started`-`Detail` nennt die Eingangsart (`pipe:0`) und die `ResolutionNote`-Ursache, wenn `TryCreateStreamUri` `null` liefert — Transport der Diagnose bis ins Log (Akzeptanzkriterium 2). |
| `SynchronizeAsync_RemoteMedia_StartedDetailNamesHttpInput` (Phase 1) | `LiveStreamCoordinatorTests` | `Started`-`Detail` nennt die Eingangsart `HTTP` beim bevorzugten Eingang. |
| `TranscodeAsync_TruncatedPumpInput_KillsProcessAndReturnsNull` (Phase 1) | `LiveStreamStandbySegmenterTests` | Pipe-Quelle über die gemeinsame `Helpers/TruncatingReadStream` (seekbar, `Length` > gelieferte Bytes — über `ForStream` eingespeist) ⇒ `Truncated` ⇒ Prozess-Kill + `null`-Ergebnis (kein Teilmaterial im Standby-Clip). |
| `ExecuteAsync_UndecodableSlotSkips_LogsInputUnplayableOnce` (Phase 1) | `LiveStreamWorkerTests` | Wiederholte `InputUnplayable`-Ticks desselben Sendeplatzes erzeugen nur eine `Information`-Zeile (Folgeergebnisse `Debug`); benötigt `ListLogger<LiveStreamWorker>`, `ControllableTimeProvider` und eine `FakeGuide`-Variante mit laufendem Sendeplatz; `FakeSegmenter.LastUnexpectedExit` wird dafür setzbar (aktuell `=> null`, `LiveStreamWorkerTests.cs:37`). |
| `ContainsSftpInputError_Markers_Match` / `..._NoMarkers_NoMatch` (Phase 2) | `FfmpegErrorMarkersTests` | sftp-Transport-/Auth-Marker werden erkannt; Demux-Text ohne Marker → `false`. |
| `ContainsUnsupportedSchemeError_ProtocolMarkers_Match` (Phase 2) | `FfmpegErrorMarkersTests` | „Protocol not found"/Whitelist-Marker werden erkannt. |
| `ResolveAsync_RemoteSourceCredentialsNoHttpUri_ReturnsSftpUrl` (Phase 2) | `LiveStreamMediaResolverTests` | Seed mit `Username`/`Password`/absolutem `Path`, `TryCreateStreamUri` → `null` ⇒ `input.SftpUrl` gesetzt (`sftp://user:pass@host:port/…`), `IsPipe == false`. |
| `ResolveAsync_UseHttpInputFalseWithCredentials_ReturnsSftpUrl` (Phase 2) | `LiveStreamMediaResolverTests` | Nach-HTTP-Transport-Fallback geht zu `ForSftp`, nicht direkt zur Pipe. |
| `ResolveAsync_UseSftpInputFalse_SkipsSftp` (Phase 2) | `LiveStreamMediaResolverTests` | Parameter `useSftpInput: false` ⇒ Pipe trotz Credentials. |
| `ResolveAsync_SftpOptionDisabled_SkipsSftp` (Phase 2) | `LiveStreamMediaResolverTests` | `LiveStreamOptions.UseSftpInput = false` ⇒ Pipe. |
| `ResolveAsync_SftpMissingCredentials_FallsBackToPipe` (Phase 2) | `LiveStreamMediaResolverTests` | Ohne Credentials bleibt es bei `ForStream` (Absicherung des bisherigen Verhaltens). |
| `InputSpec_SftpUrl_ReturnsQuotedUrl` / `NoStdinArg_SftpInput_ReturnsNostdin` (Phase 2) | `SegmenterProcessSupportTests` | `sftp://`-Eingang erscheint quotiert als `-i`, mit `-nostdin`. |
| `StartAsync_SftpInput_UsesUrlArgumentAndNoStdin` (Phase 2) | `FfmpegLiveStreamSegmenterTests` | Argumentaufbau mit `sftp://`-Eingang. |
| `UnexpectedExit_SftpInputTransportMarkers_SetsSftpInputFailure` (Phase 2) | `FfmpegLiveStreamSegmenterTests` | `sftp://`-Job + Transportmarker ⇒ `SftpInputFailure == true`. |
| `UnexpectedExit_SftpSchemeMarkers_LatchesSftpInputSupportedFalse` (Phase 2) | `FfmpegLiveStreamSegmenterTests` | „Protocol not found" ⇒ `SftpInputSupported == false` (Latch) + `SftpInputFailure`. |
| `UnexpectedExit_SftpInput_MasksCredentialsInStderr` (Phase 2) | `FfmpegLiveStreamSegmenterTests` | `sftp://user:pass@…` aus stderr erscheint maskiert im Log (Regression). |
| `SynchronizeAsync_HttpTransportFailureSameSlotWithCredentials_FallsBackToSftp` (Phase 2) | `LiveStreamCoordinatorTests` | HTTP-Schaden + Credentials ⇒ `job.Input.SftpUrl` gesetzt. |
| `SynchronizeAsync_SftpFailureSameSlot_FallsBackToPipe` (Phase 2) | `LiveStreamCoordinatorTests` | `SftpInputFailure` am selben Slot ⇒ Pipe-Job. |
| `SynchronizeAsync_SftpUnsupported_SkipsSftpInput` (Phase 2) | `LiveStreamCoordinatorTests` | `SftpInputSupported == false` ⇒ kein `SftpUrl`-Job. |
| Seed-Helfer mit Credentials (Phase 2) | `LiveStreamCoordinatorTests` / `LiveStreamMediaResolverTests` | `MediaSource` mit `Username`/`Password` und absoluten POSIX-Pfaden (`/remote/…`) für die sftp-Varianten. |
| `FakeGuide`-Variante mit laufendem Sendeplatz (Phase 1) | `LiveStreamWorkerTests` | `GetNowAsync` liefert `Now`-Eintrag mit `SlotId`/`MediaItemId`/`EndUtc` — treibt den Worker-Dedup-Test. |
| `SourceDescriptor_SftpInput_ReturnsSftpDescriptor` (Phase 2) | `LiveStreamStandbyProviderTests` | `LiveStreamInput.ForSftp(...)` ⇒ Deskriptor `"sftp"` — sichert die bewusst einmalige Cache-Invalidierung ab; setzt `SourceDescriptor` als `internal` voraus. |

### Betroffene bestehende Tests

| Test / Testklasse | Grund der Anpassung |
|-------------------|---------------------|
| `InternalMediaStreamTokenServiceTests.CreateService` | Optionaler `IConfiguration`-Parameter am Service-Konstruktor — Helfer um `IConfiguration?`-Übergabe erweitern; zusätzlich optionale `ILogger<InternalMediaStreamTokenService>`-Übergabe für den `_invalidConfigWarned`-Test (`ListLogger`). Bestehende Aufrufe bleiben unverändert (leere Konfiguration, `NullLogger`). |
| `LiveStreamWorkerTests.FakeSegmenter.LastUnexpectedExit` (Phase 1) | Aktuell `=> null` (read-only, `LiveStreamWorkerTests.cs:37`) — für den Worker-Dedup-Test setzbar machen (Auto-Property mit Setter); unabhängig davon kommt in Phase 2 noch `SftpInputSupported` dazu. |
| `SegmenterProcessSupportTests` — `PumpInputAsync_BrokenPipe_…`, `…_ExpectedStopExceptions_…`, `…_UnexpectedError_…`, `…_SourceUnavailable_…`, `…_Success_PumpsAndClosesStdin` | `PumpInputAsync` liefert `SegmenterPumpResult` statt `SegmenterPumpOutcome` → Assertions auf `result.Outcome` umstellen. |
| `LiveStreamCoordinatorTests.FakeSegmenter` (Phase 2) | Neues Interface-Member `SftpInputSupported` (Auto-Property, Default `true`). |
| `LiveStreamWorkerTests.FakeSegmenter` (Phase 2, `LiveStreamWorkerTests.cs:31-42`) | Dasselbe Interface-Member — bisher im Plan nicht eingeplant; ohne Implementierung bricht die Testklasse zu kompilieren. |
| `LiveStreamCoordinatorTests.SynchronizeAsync_HttpTransportFailureSameSlot_FallsBackToPipe` (Phase 2) | Semantik erweitert: ohne Credentials bleibt der Seed-Fall Pipe (Test besteht), Doku/Kommentar an die neue Kette anpassen. |
| `FfmpegLiveStreamSegmenterTests.StopAsync_PipeInputPumpBreak_LogsCompactDebugBeforeStopReturns` und Pump-nahe Tests | Abschluss-Logs des Pumps enthalten künftig Bytezahlen — Assertions auf Logtexte ggf. angleichen. |
| `LiveStreamWorkerTests` | Neue Aktion `InputUnplayable` + Log-Dedup — kein bestehender Test bricht, aber das Log-Verhalten für die neue Aktion und die Deduplizierung abdecken (s. neue Tests). |
| `FfmpegLiveStreamSegmenterE2ETests.SecondJob_PlaylistContinuesAndMarksDiscontinuity` | Bereits dokumentiertes Flackern (Race mit `LastUnexpectedExit`) — kein Umbau nötig; bei erneutem Ausfall einzeln wiederholen und ehrlich berichten. |

### E2E-Tests (primärer Funktionsnachweis)

Die Anforderung betrifft keinen UI-Benutzerfluss — keine Razor-/Playwright-Änderung. Der benutzerwirksame Nachweis ist die Prozess-Ebene des Live-Streams: Die bestehende `FfmpegLiveStreamSegmenterE2ETests`-Infrastruktur (echtes ffmpeg, `E2EBinaryLocator`, `Category = E2E`) bildet genau die produktive Fehlersituation ab, die Unit-Tests mit `FakeSegmenterProcess` nicht beweisen können; die Coordinator-Kette wird durch die SQLite-echten `LiveStreamCoordinatorTests` abgesichert. Die Basis-URL-Erkennung (Kestrel-Fix) ist konfigurationsgetrieben und durch die neuen `InternalMediaStreamTokenServiceTests` mit In-Memory-Konfiguration ausreichend gedeckt — ein echter Kestrel-Bindungstest würde einen vollständigen Webhost erfordern und ist für den reinen Konfig-Lesepfad nicht nötig.

| Priorität | Szenario | Testdatei / Testklasse | Abgedecktes Akzeptanzkriterium | Warum E2E nötig ist |
|-----------|----------|------------------------|-------------------------------|-------------------|
| Pflicht (Phase 1) | `RealFfmpeg_MoovAtEndPipeInput_FlagsUndecodableInput` — generierte MP4 (moov am Ende, wie `GenerateVideoAsync` sie erzeugt) via `ForStream`/`pipe:0` ⇒ ffmpeg bricht ab, `LastUnexpectedExit.UndecodableInput == true` | `FfmpegLiveStreamSegmenterE2ETests` | Kriterien 3 + 4: der Pipe-Fehler wird als Eingangsproblem erkannt statt unklassifiziert zu bleiben | Reproduziert den echten Produktiv-Fehlschlag (Exit 183, „partial file"/„Cannot determine format") — nur mit echter ffmpeg-Binary nachweisbar. |
| Pflicht (Phase 1) | `RealFfmpeg_MoovAtEndFileInputWithOffset_ProducesSegments` — dieselbe MP4 via `ForFile` mit `PositionSeconds > 0` ⇒ Playlist + Segmente, `LastUnexpectedExit == null` | `FfmpegLiveStreamSegmenterE2ETests` | Kriterium 1: seekbarer Eingang segmentiert moov-am-Ende-MP4 inkl. `-ss`-Sprung | Beweist, dass der gewählte Fix (seekbarer Eingang) die Container-Falle tatsächlich löst; deckt `ForSftp`/`ForHttp` argumentgleich mit ab (identische `-i`-Behandlung). |
| Pflicht (Phase 2) | `RealFfmpeg_SftpInputUnreachable_SetsSftpInputFailure` — `ForSftp("sftp://u:p@127.0.0.1:1/x")` ⇒ Verbindungsfehler/Schema-Fehler ⇒ `SftpInputFailure == true` (bzw. `SftpInputSupported == false` bei Build ohne libssh) | `FfmpegLiveStreamSegmenterE2ETests` | Kriterien 3 + 5: sftp-Fehlschlag fällt gestuft auf Pipe zurück statt zu loopen; beweist zugleich, ob das Test-ffmpeg `sftp` spricht | Echter ffmpeg entscheidet zwischen „Protokoll fehlt" und „Verbindung verweigert" — beide Pfade müssen sauber klassifiziert werden; ein SFTP-Server steht nicht zur Verfügung, daher bewusst der Unerreichbarkeits-Fall. |

Welche bestehenden E2E-Tests müssen angepasst werden?

| Test / Testklasse | Grund der Anpassung |
|-------------------|---------------------|
| Keine | Die neuen Szenarien sind additive Tests; `RealFfmpeg_PipeInputStop_LogsNoExceptionStacktrace` bleibt unverändert gültig (dessen Kommentar bestätigt bereits die moov-Falle). |

## Offene Punkte

Keine.
