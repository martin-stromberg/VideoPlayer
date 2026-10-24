# Umsetzungsplan: Issue #267 — ffmpeg/ffprobe per Download-Button in den TV-Sender-Einstellungen nachinstallieren

## Übersicht

`/admin/tv-sender` erhält eine serverseitig abgesicherte Admin-Aktion, die ein statisches
ffmpeg-Paket (mit `ffmpeg` + `ffprobe`) für die Laufzeitplattform herunterlädt, die beiden
Binaries ins Konventionsverzeichnis `tools/ffmpeg/` neben der Anwendung entpackt und unter
Linux das Ausführungsbit setzt. Die bestehenden „Binary fehlt"-Zustände bekommen Hinweisflächen
mit Download-Button, Fortschritts- und Fehleranzeige sowie einen GPL-/Lizenzhinweis vor dem
Download. Die Suchreihenfolge der Locators bleibt unverändert — nach der Installation greift
`Resolve()` ohne Neustart.

## Designentscheidungen

| Komponente / Bereich | Gewählter Ansatz | Begründung |
|---|---|---|
| Download-Quelle win-x64 | `https://www.gyan.dev/ffmpeg/builds/ffmpeg-release-essentials.zip` (stabile „latest release"-URL, .zip, enthält ffmpeg+ffprobe+ffplay) | Von ffmpeg.org offiziell verlinkte Windows-Quelle; zip entpackbar mit `System.IO.Compression` (BCL, vorhanden); Essentials-Variante ist GPL-Build mit `libx264` — der Segmenter ruft `-c:v libx264` (`FfmpegLiveStreamSegmenter.cs:391`), ein LGPL-Build reicht also nicht. |
| Download-Quelle linux-x64 | `https://johnvansickle.com/ffmpeg/releases/ffmpeg-release-amd64-static.tar.xz` (stabile „release"-URL) | Vollständig statisch gelinkt — inklusive glibc — läuft daher auf beliebigen Kerneln ≥ 3.2 und ist die sicherste Wahl für das Primärziel Synology (BtbN-Linux-Builds setzen glibc ≥ 2.28 voraus, was ältere DSM-Versionen unterschreiten können). Von ffmpeg.org verlinkt („64-bit static builds"). Abgewogen und verworfen: ffbinaries (zip auch für Linux, aber ffmpeg 6.1, Stand Dez 2023, Projekt ungepflegt); BtbN (nur .tar.xz für Linux + glibc-Anforderung). |
| tar.xz-Entpackung | Neue NuGet-Abhängigkeit **SharpCompress** (MIT-Lizenz, .NET-10-kompatibel, `TarReader` unterstützt Tar.XZ) | Es gibt keine gepflegte zip-basierte Linux-Quelle aktueller Builds (Recherche: BtbN/johnvansickle nur .tar.xz; einzige zip-Quelle ffbinaries ist veraltet). BCL kann xz nicht. SharpCompress ist MIT-lizenziert und rein managed — Lizenz ok; Version wird bei Umsetzung auf „älter als 7 Tage veröffentlicht" geprüft (aktuell 0.50.x-Reihe), Release-Build + CI-Skripte gemäß Projektregeln verifizieren. |
| Job-/Fortschrittsmodell | Singleton `FfmpegToolDownloadService` + `FfmpegToolDownloadJobSnapshot`/`FfmpegToolDownloadJobStatus` — analog `ManualBackupJobService`/`ManualBackupJobSnapshot` (`Services/Backups/ManualBackupJobService.cs`) | Bewährtes Muster im Repo: lock-geschützter immutabler Snapshot, `Idle`-Singleton, `IsActive` verhindert Parallel-Jobs; Enum-Wertemenge per `EnumContractTests` pinnbar. Snapshot wird um `BytesReceived`/`TotalBytes` (→ Prozent; `TotalBytes = null` bei fehlendem Content-Length) erweitert. Ein Job installiert beide Binaries — die Pakete enthalten beide; getrennte Downloads unnötig (auch bei Teil-Fehlzustand werden beide ins Konventionsverzeichnis geschrieben). |
| Admin-Erzwingung | `StartDownloadAsync(string userId)` prüft serverseitig im Dienst selbst: `IServiceScopeFactory` → Scope → `ApplicationDbContext` → `user.IsAdmin`, wirft `UnauthorizedAccessException`/`ForbiddenAccessException` — Muster `MediaDurationProbeService.RequireAdminAsync` | Die Aktion wird aus der Blazor-Server-Komponente aufgerufen (kein Form-POST wie beim Backup-Controller); der Dienst ist Singleton und muss die scoped `ApplicationDbContext` per Scope-Factory holen. Die `isAdmin`-UI-Ausblendung bleibt zusätzlich, ist aber nicht die Absicherung. |
| HTTP-Zugriff | Neuer benannter `HttpClient` `"FfmpegTools"` via `AddHttpClient` in `ServiceCollectionExtensions`; `HttpCompletionOption.ResponseHeadersRead`, gestreamtes Kopieren in eine Temp-Datei mit gedrosselten Fortschritts-Updates (Snapshot) | Der einzige vorhandene Client `"Internal"` trägt API-Key/User-Agent für Self-Calls und ist für externe Downloads ungeeignet; `IHttpClientFactory` steht bereit. |
| Zielablage & Atomicität | `{AppContext.BaseDirectory}/tools/ffmpeg/` — **beide** Binaries zuerst vollständig als `*.tmp`-Staging-Dateien im selben Verzeichnis ablegen, **dann** beide `File.Move(overwrite: true)` als letzter Schritt (halb installierter Zustand nur bei IO-Fehler exakt zwischen den zwei Moves möglich — Restrisiko in §Seiteneffekte); unter Unix `File.SetUnixFileMode` (Lese-/Schreib- + Ausführungsbit für Owner/Group/Other). Testbarer internal Ctor mit injizierbarem `baseDirectory` **und injizierbarer Plattform-Kennung** (RID-Überschreibung für Tests; Muster interner Ctor der Locators) | Identisch mit den `ConventionCandidates()` beider Locators → `Resolve()` greift ohne Neustart. Verbindliche Semantik: Der Download schreibt **immer ausschließlich** nach `{baseDir}/tools/ffmpeg/` — Config-Pfad (`MediaProbe:FfprobePath`/`FfmpegPath`) und PATH werden nie angefasst, weder als Ziel noch verändert (AK 5). Ein vorhandenes Binary im Konventionsverzeichnis wird überschrieben — es ist „unser" Installationsverzeichnis. |
| GPL-/Lizenzhinweis | Bestätigungsdialog im bestehenden `admin-dialog-overlay`/`admin-dialog`-Muster (wie `confirmProbeReset`) vor dem Start; Text konsistent zu `docs/help/tv-sender.md` § „ffmpeg-Bereitstellung und Lizenz" | Vorhandenes Dialog-Muster; Hinweis muss laut AK 4 *vor* dem Download erscheinen — Dialog statt Inline-Text erzwingt bewusste Bestätigung. |
| Architekturabdeckung | Nur `win-x64` und `linux-x64` (Plattformwahl über `OperatingSystem.IsWindows()` + `RuntimeInformation.OSArchitecture == X64`) | Entspricht den Release-RIDs (`release-win-x64.zip`/`release-linux-x64.zip`); andere Plattformen erhalten eine verständliche Fehlermeldung im Snapshot („Plattform wird nicht unterstützt"), kein stiller Fehler. |
| Quellen-Konfiguration | `MediaProbeOptions.DownloadSources` (`Dictionary<string,string>`, Schlüssel `win-x64`/`linux-x64`) mit eingebauten Defaults; überschreibbar via `MediaProbe:DownloadSources:*` | Ermöglicht Umleitung in abgeschotteten Umgebungen und — entscheidend — das Verdrahten eines Loopback-Testservers in E2E/Integrationstests ohne Netzwerkzugriff. |
| Integritätsprüfung | HTTPS-Download + Archiv-Validierung (Struktur, erwartete Einträge, nur Allowlist-Dateinamen werden extrahiert, kein Pfad-Traversal); kein Hash-Pinning und kein SHA256-Sidecar in dieser Version | „latest"-URLs liefern rotierende Inhalte — ein gepinnter Hash würde bei jedem Upstream-Release brechen; zur Laufzeit abrufbare `.sha256`-Sidecars bleiben eine mögliche spätere Erweiterung. |
| E2E-Testbarkeit der Locators | E2E registriert die **echten** `FfprobeLocator`/`FfmpegLocator` über deren bestehenden internal Ctor `(IOptions<MediaProbeOptions>, string baseDirectory, Func<string?> pathEnvironment)` neu (dank `InternalsVisibleTo("VideoWebPlayer.Tests")` direkt nutzbar): Temp-`baseDirectory`, `pathEnvironment = () => null`, frische `MediaProbeOptions` ohne `FfprobePath`/`FfmpegPath` — damit sind **alle drei** Kandidatenquellen neutralisiert (Config-Pfad, Konventionsverzeichnis, PATH), obwohl ffmpeg auf dem Testsystem im PATH liegt. Der Download-Service wird im selben Override auf dasselbe Temp-`baseDirectory` gebogen → nach `Succeeded` findet `Resolve()` die Binaries real (deckt AK 3 e2e-seitig ab) | Auf dem Testsystem ist ffmpeg/ffprobe vorhanden (`inventory/tests.md`) — ein reiner `baseDirectory`-Override würde die „fehlt"-Hinweise nie zeigen, weil PATH weiterhin liefert. Fake-`IFfmpegLocator`/`IFfprobeLocator` stattdessen würden nur die UI verdrahten; die echten Locators mit neutralisierten Quellen testen zusätzlich das tatsächliche Wiederfinden nach Installation. Für bUnit-Komponententests genügen schlichte Fake-Locators (`PresentLocator`-Muster aus `MediaProbeTestDoubles`). |
| Platzierung des ffmpeg-Hinweises | Tab „Einstellungen", innerhalb der „Segmentierung"-`admin-sub-card` | Der `FfmpegUnavailable`-Pausier-Zustand existiert heute nur im Log; die Segmentierungs-Karte ist die fachlich passende Stelle (dort liegen die Live-Stream-Einstellungen). |
| Update-Bestand von `tools/ffmpeg/` | Kein Eintrag in `AutoUpdate:ProtectedFiles`; `tools/ffmpeg/` bleibt ein ungeschütztes, deployment-seitig angelegtes Verzeichnis | `docs/TECH_Auto_Update.md` („Dateierhalt bei Updates") dokumentiert, dass deployment-seitig angelegte Dateien, die nicht im Paket enthalten sind, bei Updates unverändert erhalten bleiben — die installierten Binaries überstehen Updates damit ohne Schutzeintrag. |

## Programmabläufe

### Download-Job (Start bis Abschluss)

1. Admin klickt „ffmpeg/ffprobe herunterladen" → `TVSenderAdmin.razor` öffnet GPL-Dialog →
   Bestätigung ruft `IFfmpegToolDownloadService.StartDownloadAsync(userId)` mit der
   `NameIdentifier`-Claim-ID.
2. `FfmpegToolDownloadService` legt Scope an (`IServiceScopeFactory`), prüft `user.IsAdmin`
   (sonst `UnauthorizedAccessException`/`ForbiddenAccessException`), sperrt weitere Starts über
   `IsActive` und setzt Snapshot auf `Queued`; Job läuft via `Task.Run` außerhalb der
   Circuit-Lifetime.
3. Plattformwahl: `OperatingSystem.IsWindows()` + `OSArchitecture.X64` → RID-Schlüssel → URL aus
   `MediaProbeOptions.DownloadSources` (Default gyan.dev/johnvansickle). Unbekannte Plattform →
   Snapshot `Failed` mit verständlicher Meldung.
4. Download: `"FfmpegTools"`-`HttpClient`, `ResponseHeadersRead`, gestreamt in Temp-Datei;
   `BytesReceived`/`TotalBytes` regelmäßig in den Snapshot geschrieben; Status `Running`.
5. Entpacken: `.zip` via `System.IO.Compression.ZipArchive`, `.tar.xz` via SharpCompress
   `TarReader` — Allowlist-Abgleich auf `Path.GetFileName(eintrag)` (`ffmpeg`, `ffprobe`,
   `ffmpeg.exe`, `ffprobe.exe`); legitime Unterverzeichnis-Präfixe der echten Pakete
   (`bin/ffmpeg.exe`, `ffmpeg-…-amd64-static/ffprobe`) sind erlaubt, aber der Präfix wird
   **verworfen** — die Ablage ist immer flach unter dem reinen Dateinamen. Einträge mit
   `..`-Segmenten oder absoluten/rooted Pfaden werden **immer** verworfen (auch bei
   allowlistetem Dateinamen), nicht-allowlistete Dateinamen ebenfalls; als Defense-in-depth
   wird der normalisierte Zielpfad gegen `tools/ffmpeg/` geprüft. Fehlt ein erwartetes
   Binary → `Failed`.
6. Installation: `tools/ffmpeg/` anlegen (falls nötig); **zuerst beide** Binaries vollständig
   als `*.tmp`-Staging-Dateien im selben Verzeichnis schreiben, **dann beide**
   `File.Move(overwrite: true)` — die zwei Moves sind der letzte, weitestgehend atomare
   Schritt (Restrisiko: IO-Fehler genau zwischen den Moves → halb installierter Zustand;
   akzeptiert, weil der „fehlt"-Hinweis dann korrekt weiter angezeigt wird — siehe
   §Seiteneffekte); unter Unix `File.SetUnixFileMode` mit Ausführungsbits; Download-Temp-Datei
   und Staging-Reste in allen Pfaden aufräumen (auch im Fehlerfall).
7. Snapshot `Succeeded` — die UI erkennt beim nächsten Rendern/Pollen, dass `Resolve()` nun
   Pfade liefert, und blendet die Hinweise aus; `MediaDurationProbeWorker` und `LiveStreamWorker`
   laufen im nächsten Takt automatisch an.
8. Bei jeder Exception: `Failed` mit verständlicher `Error`-Meldung (HTTP-Status, Archiv-Fehler,
   Schreibrechte).

### UI-Fluss `TVSenderAdmin.razor`

- `OnInitializedAsync`/Refresh: `ffprobePath = IFfprobeLocator.Resolve()`,
  `ffmpegPath = IFfmpegLocator.Resolve()` (billige `File.Exists`-Checks, kein Caching nötig).
- Tab „Laufzeitermittlung": `ffprobePath == null` → Warnzeile mit Hinweis „ffprobe wurde nicht
  gefunden …" + Download-Button direkt daneben (ersetzt/ergänzt den indirekten Text in
  `ProbeStatusLabel`).
- Tab „Einstellungen": `ffmpegPath == null` → Hinweis „ffmpeg wurde nicht gefunden — der
  Live-Stream pausiert" + Download-Button in der Segmentierungs-`admin-sub-card`.
- Download-Button → GPL-Dialog (`admin-dialog-overlay`) → `StartDownloadAsync`; während
  `IsActive` sind beide Buttons deaktiviert.
- Fortschritt: `.progress`/`.progress-bar` (vorhandenes CSS) mit Prozent aus
  `BytesReceived`/`TotalBytes` (oder Byte-Anzeige bei unbekannter Länge); Fehler als
  Alert-Text; `Succeeded` → Hinweise ausblenden (Locator-Flags neu abfragen).
- Polling: neuer `DownloadProgressLoopAsync` mit `PeriodicTimer` (~1 s), läuft nur solange
  `IsActive` (Start beim Download-Beginn und bei `OnInitializedAsync`, falls ein Job bereits
  läuft — Muster `ProbeProgressLoopAsync`/`PollBackupStatusAsync`).

## Neue Klassen

| Klasse | Typ | Zweck |
|---|---|---|
| `IFfmpegToolDownloadService` | Interface (`VideoWebPlayer/Services/`) | `GetSnapshot()` + `StartDownloadAsync(string userId, CancellationToken)` |
| `FfmpegToolDownloadService` | sealed Singleton (`VideoWebPlayer/Services/`) | Download, Entpackung, Installation, Fortschritts-Snapshot, Admin-Prüfung; public Ctor (DI) + internal Ctor mit `baseDirectory` und Plattform-Override (RID-Selektor, z. B. `Func<string>`/`Func<(string rid, string archiveKind)>`) für Tests |
| `FfmpegToolDownloadJobStatus` | enum (in der Service-Datei, Muster `ManualBackupJobStatus`) | `Idle`, `Queued`, `Running`, `Succeeded`, `Failed` |
| `FfmpegToolDownloadJobSnapshot` | sealed record | `Id`, `Status`, `StartedAtUtc`, `CompletedAtUtc`, `Message`, `Error`, `BytesReceived`, `TotalBytes`, `IsActive`, `Idle`-Singleton |
| `FfmpegToolDownloadStartResult` | sealed record | `Started`, `Snapshot` (Muster `ManualBackupStartResult`) |

## Änderungen an bestehenden Klassen

### `MediaProbeOptions` (`VideoWebPlayer/Configuration/MediaProbeOptions.cs`)
- Neue Eigenschaft `DownloadSources` (`Dictionary<string,string>`) mit Defaults für `win-x64`
  und `linux-x64`.

### `ServiceCollectionExtensions` (`VideoWebPlayer/Extensions/ServiceCollectionExtensions.cs`)
- `AddHttpClient("FfmpegTools", …)` (kein API-Key-Header, angemessenes Timeout-Verhalten für
  große Downloads — Timeout über den Job-`CancellationToken`/Request-Timeout statt
  Client-Default).
- `AddSingleton<IFfmpegToolDownloadService, FfmpegToolDownloadService>()` neben den
  Locator-Registrierungen.

### `TVSenderAdmin` (`VideoWebPlayer/Components/Pages/Admin/TVSenderAdmin.razor`)
- Neue Injektionen: `IFfprobeLocator`, `IFfmpegLocator`, `IFfmpegToolDownloadService`.
- Neue Felder: `ffprobePath`, `ffmpegPath`, `downloadSnapshot`, `confirmFfmpegDownload`,
  `downloadError` (o. ä.).
- Neue Markup-Abschnitte: ffprobe-Warnzeile + Button im Tab „laufzeit"; ffmpeg-Warnzeile +
  Button in der Segmentierungs-Karte (Tab „einstellungen"); Fortschrittsbalken + Status-/
  Fehlertexte; GPL-Bestätigungsdialog (`admin-dialog-overlay`/`admin-dialog`).
- Neue Methoden: `StartFfmpegDownloadAsync` (Dialog-Bestätigung → Service-Aufruf),
  `DownloadProgressLoopAsync` (`PeriodicTimer`-Polling, Muster `ProbeProgressLoopAsync`),
  Refresh der Locator-Flags nach `Succeeded`; `Dispose` erweitert um die Timer-Ressourcen.

## Datenbankmigrationen

Keine. — Der Installationszustand ergibt sich aus dem Dateisystem (Locators), der
Download-Status ist flüchtig im Job-Singleton.

## Validierungsregeln

| Feld / Objekt | Regel | Fehlerfall |
|---|---|---|
| Archiveinträge | Zielname = `Path.GetFileName(eintrag)`; Allowlist `ffmpeg`, `ffprobe`, `ffmpeg.exe`, `ffprobe.exe`. Verworfen wird: (a) jeder Eintrag mit `..`-Segmenten oder absolutem/rooted Pfad — **auch bei allowlistetem Dateinamen** (`../ffmpeg.exe` wird nicht extrahiert); (b) jeder Eintrag mit nicht-allowlistetem Dateinamen (mit oder ohne Verzeichnispräfix, z. B. `subdir/evil.exe`); (c) jeder Eintrag, dessen normalisierter Zielpfad außerhalb `tools/ffmpeg/` läge (Defense-in-depth via `Path.GetFullPath`). Legitime Unterverzeichnis-Präfixe allowlisteter Dateinamen (`bin/ffmpeg.exe`, `ffmpeg-…-amd64-static/ffprobe`) sind erlaubt — echte Pakete sind so aufgebaut —, Ablage aber immer flach unter dem reinen Dateinamen | Erwartetes Binary fehlt im Archiv → Job `Failed` mit Meldung „Paket enthält kein …" |
| `userId` | Benutzer existiert und `IsAdmin == true` | `UnauthorizedAccessException` (nicht angemeldet) / `ForbiddenAccessException` (kein Admin) |
| Plattform | Nur Windows-x64 / Linux-x64 | Job `Failed`: „Diese Plattform wird nicht unterstützt" |
| Parallel-Start | `IsActive` → `Started = false` | UI hält Button deaktiviert; Service lehnt zweiten Job ab |
| Zielverzeichnis | Schreibrechte auf `{baseDir}/tools/ffmpeg/` | Job `Failed` mit Schreibrechte-Hinweis |
| Download-URL | Absolute `http(s)`-URL aus `DownloadSources`; HTTP-Fehlerstatus wird abgefangen | Job `Failed` mit Status-Code |

## Konfigurationsänderungen

| Eintrag | Typ | Standardwert | Zweck |
|---|---|---|---|
| `MediaProbe:DownloadSources:win-x64` | string (optional) | `https://www.gyan.dev/ffmpeg/builds/ffmpeg-release-essentials.zip` | Paket-URL Windows x64 (überschreibbar für abgeschottete Umgebungen/Tests) |
| `MediaProbe:DownloadSources:linux-x64` | string (optional) | `https://johnvansickle.com/ffmpeg/releases/ffmpeg-release-amd64-static.tar.xz` | Paket-URL Linux x64 |

(`appsettings.json` muss die Schlüssel nicht enthalten — Defaults liegen im Code; Dokumentation
in `docs/help/tv-sender.md`.)

## Seiteneffekte und Risiken

- **Neue NuGet-Abhängigkeit SharpCompress:** MIT-lizenziert; bei Umsetzung Version älter als 7
  Tage wählen, Release-Build und CI-Skripte prüfen (Projektregel). Betrifft Deployment-Größe
  beider Release-RIDs.
- **`tools/ffmpeg/` beim Update:** laut `docs/TECH_Auto_Update.md` bleiben deployment-seitig
  angelegte Dateien, die nicht im Paket enthalten sind, bei Updates erhalten — die Annahme ist
  damit als verbindlich übernommen; `AutoUpdate:ProtectedFiles` wird bewusst nicht ergänzt.
- **Schreibrechte Produktivsysteme:** Installation unter `AppContext.BaseDirectory` kann auf
  manchen Systemen (z. B. Windows-Programmverzeichnis) scheitern → Fehlermeldung im Snapshot
  muss auf die Alternative `MediaProbe:*Path`/PATH hinweisen.
- **Teil-Fehlzustand:** fehlt nur eines der Binaries, installiert der Download dennoch beide
  und überschreibt ein evtl. vorhandenes Binary im Konventionsverzeichnis — verbindliche
  Entscheidung; das Überschreibverhalten ist in `docs/help/tv-sender.md` zu dokumentieren.
- **Externe Netzwerkabhängigkeit:** Download scheitert ohne Internet/proxy — Fehlerpfad und
  Doku-Hinweis (manueller Download + `tools/ffmpeg/`-Ablage) nötig.
- **Halb installierter Zustand:** Staging-Strategie ist „beide Binaries komplett als `*.tmp`
  schreiben, dann beide `File.Move`"; ein IO-Fehler exakt zwischen den zwei Moves könnte nur
  eines der Binaries aktualisieren. Akzeptiertes Restrisiko: Der „fehlt"-Hinweis wird dann
  korrekt weiter angezeigt bzw. ein veraltetes Binary bleibt konsistent; ein erneuter
  Download-Lauf heilt den Zustand (Moves sind `overwrite: true`, Staging-Reste werden
  aufgeräumt).
- **Loopback-Paketserver im E2E:** kein `HttpListener` (http.sys-URL-Reservierung unter
  Windows, `netsh urlacl`-Probleme im CI-/Entwicklerkontext) — stattdessen ein minimaler
  `WebApplication`/Kestrel-Mini-Host auf `http://127.0.0.1:0` (freier Port vom OS), exakt
  das im Projekt etablierte Muster (`PlaylistsE2ETestBase`: `WebApplicationFactory` +
  `UseKestrel()` + `UseUrls("http://127.0.0.1:0")`).
- **Download-Größe (~30–145 MiB):** Temp-Datei liegt im System-Temp-Verzeichnis — knapper
  Speicherplatz → `Failed`; kein Resume-Support (einfach wiederholbar).
- **bUnit-Registrierung:** `TVSenderAdminTests.CreateContextAsync` muss die drei neuen
  Abhängigkeiten (Locators, Download-Service) stubben, sonst schlagen alle bestehenden
  Komponenten-Tests fehl.
- **Blazor-Server-Komponente ohne `isAdmin`:** Hinweisflächen und Button nur im Admin-Zweig
  rendern; Absicherung bleibt der Service-Wurf.

## Umsetzungsreihenfolge

1. **NuGet-Abhängigkeit SharpCompress aufnehmen**
   - Voraussetzungen: Designentscheidung Quelle/Paketformat getroffen.
   - Beschreibung: `PackageReference` in `VideoWebPlayer.csproj` (Version > 7 Tage alt, Lizenz
     MIT verifiziert); anschließend Debug- und Release-Restore/-Build prüfen.
2. **`MediaProbeOptions.DownloadSources` + Defaults**
   - Voraussetzungen: keine.
   - Beschreibung: Dictionary-Eigenschaft mit den beiden Default-URLs; Doku-Kommentar.
3. **Job-Typen: `FfmpegToolDownloadJobStatus`, `FfmpegToolDownloadJobSnapshot`, `FfmpegToolDownloadStartResult`, `IFfmpegToolDownloadService`**
   - Voraussetzungen: keine.
   - Beschreibung: in/neben der Service-Datei, Muster `ManualBackupJobService.cs`.
4. **`FfmpegToolDownloadService` implementieren**
   - Voraussetzungen: 1, 2, 3.
   - Beschreibung: Admin-Check per `IServiceScopeFactory`+`ApplicationDbContext`; Plattformwahl;
     gestreamter Download mit Snapshot-Fortschritt; zip-/tar.xz-Entpackung mit Allowlist/
     Traversal-Verwurf (§Validierungsregeln); „beide stagen, dann beide moven"-Installation +
     `SetUnixFileMode`; Fehler→`Failed`; Parallel-Sperre; internal Ctor mit `baseDirectory`
     und Plattform-Override (damit die tar.xz- und Unsupported-Plattform-Pfade
     plattformunabhängig testbar sind — SharpCompress ist rein managed, tar.xz läuft auch
     unter Windows im Test).
5. **DI-Registrierung + HttpClient `"FfmpegTools"`**
   - Voraussetzungen: 3, 4.
   - Beschreibung: `ServiceCollectionExtensions` erweitern.
6. **UI in `TVSenderAdmin.razor`**
   - Voraussetzungen: 3, 5.
   - Beschreibung: Locator-Injektionen, fehlt-Flags, Hinweisflächen + Buttons, GPL-Dialog,
     Fortschritts-/Fehleranzeige, `DownloadProgressLoopAsync`.
7. **Unit-/Komponententests**
   - Voraussetzungen: 4, 5, 6.
   - Beschreibung: `FfmpegToolDownloadServiceTests`, `EnumContractTests`-Pin, Erweiterung
     `TVSenderAdminTests` (Test-Doubles in `CreateContextAsync`).
8. **E2E-Test-Infrastruktur + E2E-Test**
   - Voraussetzungen: 6, 7.
   - Beschreibung: `PlaylistsE2ETestBase` um optionalen `ConfigureServices`/`UseSetting`-Hook
     erweitern (falls nötig); Loopback-Paketserver als minimaler `WebApplication`/Kestrel-Mini-Host
     auf `http://127.0.0.1:0` (etabliertes Projekt-Muster statt `HttpListener`), der ein kleines
     zip mit Dummy-Binaries ausliefert (konfigurierbar auf 404 für den Fehlerfall);
     `MediaProbe:DownloadSources:*` auf den Mini-Host zeigen lassen; **echte** Locators via
     internal Ctor neu registrieren (Temp-`baseDirectory`, `pathEnvironment = () => null`,
     frische `MediaProbeOptions` ohne `*Path` — alle drei Kandidatenquellen neutralisiert);
     `IFfmpegToolDownloadService` mit demselben Temp-`baseDirectory` registrieren;
     Playwright-Szenario Hinweis→Dialog→Download→Erfolg→Hinweis weg sowie Fehlerfall
     (404 → Alert sichtbar, Hinweis bleibt).
9. **Dokumentation**
   - Voraussetzungen: 6.
   - Beschreibung: `docs/help/videolaufzeit.md` (ffprobe-Button), `docs/help/tv-sender.md`
     (Download-Aktion, Lizenzabschnitt aktualisieren, `DownloadSources`-Schlüssel,
     Teil-Fehlzustand: ein Download installiert immer beide Binaries und überschreibt
     vorhandene Dateien im Konventionsverzeichnis), `README.md` und `docs/RELEASE_NOTES.md`.
10. **Definition of Done**
    - Voraussetzungen: alle vorherigen.
    - Beschreibung: `dotnet build VideoPlayer.sln` Debug + Release fehlerfrei;
      `dotnet test VideoWebPlayer.Tests/VideoWebPlayer.Tests.csproj` grün.

## Tests

### Neue Tests

| Test / Hilfsmethode | Testklasse | Was wird geprüft / bereitgestellt? |
|---|---|---|
| `StartDownloadAsync_NonAdmin_Throws` / `_AnonymousUser_Throws` | `FfmpegToolDownloadServiceTests` | `ForbiddenAccessException`/`UnauthorizedAccessException` vor Job-Start |
| `StartDownloadAsync_WhileRunning_ReturnsNotStarted` | dto. | Parallel-Sperre (`Started == false`, Snapshot unverändert) |
| `Run_ZipArchive_InstallsBothBinariesToToolsFfmpeg` | dto. | Fake-`HttpMessageHandler` serviert zip; `tools/ffmpeg/ffmpeg(.exe)` + `ffprobe(.exe)` vorhanden; Snapshot `Succeeded` |
| `Run_TarXzArchive_InstallsBothBinaries` | dto. | tar.xz-Pfad via SharpCompress (Test-Archiv im Test erzeugen oder Fixture-Datei) — **plattformunabhängig** über den Plattform-Override (`linux-x64`) ausgeführt, da SharpCompress rein managed ist; nur `SetUnixFileMode` bleibt OS-gebunden |
| `Run_OnUnix_SetsExecutableBit` | dto. | `File.GetUnixFileMode` enthält Execute-Bits (Skip/bedingt auf Windows) |
| `Run_ReportsProgress` | dto. | `BytesReceived` wächst, `TotalBytes` aus Content-Length |
| `Run_HttpError_Fails` / `Run_CorruptArchive_Fails` / `Run_MissingBinaryInArchive_Fails` | dto. | `Failed`-Status mit verständlicher `Error`-Meldung; Temp-Datei aufgeräumt |
| `Run_ZipWithMaliciousEntries_ExtractsOnlyAllowlistBinaries` / `Run_TarXzWithMaliciousEntries_ExtractsOnlyAllowlistBinaries` | dto. | **Traversal-/Allowlist-Nachweis (zip + tar.xz):** manipuliertes Archiv mit `../evil`, `../ffmpeg.exe` (Traversal + allowlisteter Name → verworfen), absoluten Pfaden (`/abs/evil`, `C:\evil.exe`), `subdir/evil.exe` (Präfix + nicht allowlistet → verworfen) neben legitimen `bin/ffmpeg(.exe)`/`bin/ffprobe(.exe)` → Assert: genau die zwei Allowlist-Binaries landen flach in `tools/ffmpeg/`, **keine** Datei außerhalb des Zielverzeichnisses, keine Staging-Reste |
| `Run_ConfiguredPaths_RemainUntouched` | dto. | **AK-5-Nachweis:** `MediaProbeOptions.FfprobePath`/`FfmpegPath` zeigen auf Marker-Dateien in einem separaten Verzeichnis, `{baseDir}/tools/ffmpeg/` existiert nicht; nach erfolgreichem Download sind die Marker-Dateien byte-identisch, `Resolve()` eines Locators mit denselben Options liefert weiterhin den Config-Pfad, und die einzigen geschriebenen Dateien liegen unter `{baseDir}/tools/ffmpeg/` — Download rührt Config-Pfade/PATH nie an |
| `Run_TargetDirectoryNotWritable_Fails` | dto. | Deterministischer Schreibrechte-Fehlerpfad: `{baseDir}/tools` liegt als **Datei** vor → `tools/ffmpeg/` kann nicht angelegt werden → `Failed` mit Schreibrechte-Hinweis (plattformstabil, keine ACL-Manipulation nötig) |
| `Run_UnsupportedPlatform_Fails` | dto. | Fehlermeldung bei nicht unterstütztem RID (injizierbare Plattform-Info) |
| `AfterInstall_LocatorResolveFindsBinaries` | dto. | Locator-Regression: `FfprobeLocator`/`FfmpegLocator` mit internem Ctor auf selbes `baseDirectory` finden die installierten Binaries |
| `FfmpegToolDownloadJobStatus_HasTheDocumentedStatesInOrder` | `EnumContractTests` | Wertemenge/Reihenfolge gepinnt (Muster `ManualBackupJobStatus`-Test) |
| `FfmpegMissing_ShowsNoticeAndDownloadButton` / `FfprobeMissing_ShowsNoticeAndDownloadButton` / `BinariesPresent_NoNotice` | `TVSenderAdminTests` (bUnit) | Hinweis + Button nur bei fehlendem Binary; Fake-Locators in `CreateContextAsync` |
| `DownloadButton_OpensGplDialog` / `GplDialog_Confirm_StartsDownload` | dto. | Dialog-Overlay erscheint; Stub-`IFfmpegToolDownloadService` erhält Aufruf mit User-ID |
| `DownloadFailed_ShowsErrorAlert_NoticeRemains` | dto. | **UI-Negativtest:** Stub-`IFfmpegToolDownloadService` liefert Snapshot `Failed` mit Fehlertext → sichtbarer Alert/Fehlertext im DOM, der „fehlt"-Hinweis bleibt bestehen |
| `DownloadActive_DisablesButtons` | dto. | Stub-Snapshot `IsActive` (Queued/Running) → beide Download-Buttons `disabled` |
| `FakeFfmpegToolDownloadService` (konfigurierbarer Snapshot: Idle/Running/Failed/Succeeded) + `AbsentFfprobeLocator`/`AbsentFfmpegLocator`/`FakeFfmpegLocator` + `FfmpegPackageServer` (minimaler `WebApplication`/Kestrel-Mini-Host auf `127.0.0.1:0` mit zip-Payload bzw. 404) | `VideoWebPlayer.Tests/Helpers/` | Test-Doubles für bUnit/E2E |

### Betroffene bestehende Tests

| Test / Testklasse | Grund der Anpassung |
|---|---|
| `TVSenderAdminTests.CreateContextAsync` (alle Tests der Klasse) | Neue injizierte Abhängigkeiten (`IFfprobeLocator`, `IFfmpegLocator`, `IFfmpegToolDownloadService`) müssen registriert/gestubbt werden |
| `EnumContractTests` | Neuer Contract-Test für `FfmpegToolDownloadJobStatus` |

### E2E-Tests (primärer Funktionsnachweis)

| Priorität | Szenario | Testdatei / Testklasse | Abgedecktes Akzeptanzkriterium | Warum E2E nötig ist |
|---|---|---|---|---|
| 1 | Admin sieht „ffmpeg fehlt"-Hinweis (Einstellungen) + „ffprobe fehlt"-Hinweis (Laufzeitermittlung), öffnet GPL-Dialog, startet Download, Fortschritt wird sichtbar, **explizite Asserts: Buttons sind während `IsActive` deaktiviert, Hinweise sind nach `Succeeded` aus dem DOM verschwunden** | `TVSenderAdminE2ETests` — neuer Test `Admin_DownloadsFfmpegTools_WhenMissing` | AK 1, 2, 3, 4 | Gesamter Benutzerfluss inkl. echtem Download über Loopback-HTTP, Installation in `tools/ffmpeg/` (Temp-`baseDirectory`-Override) und Wiederfinden durch die **echten** Locators — nur E2E prüfbar |
| 2 | Download schlägt fehl (Loopback-Server antwortet 404 oder kaputtes Archiv) → verständlicher Fehler-Alert sichtbar, „fehlt"-Hinweis bleibt bestehen | `TVSenderAdminE2ETests` — neuer Test `Admin_DownloadFails_ShowsErrorAlert` | AK 2 (Fehleranzeige) | End-to-end-Fehlerpfad über echten HTTP-Fehler; komplementiert den bUnit-Negativtest `DownloadFailed_ShowsErrorAlert_NoticeRemains` |
| 3 | Nicht-Admin erhält keinen Zugriff | bestehender `NonAdmin_SeesNotAuthorized` (evtl. unverändert ausreichend) | AK 6 (UI-Seite) | Seite ist bereits Admin-only; serverseitige Absicherung des Dienstes wird per Unit-Test belegt |

Angepasste E2E-Tests/Infrastruktur: `PlaylistsE2ETestBase` benötigt einen Erweiterungspunkt
(`WithWebHostBuilder`-Callback o. ä.), damit der Test-Host umkonfiguriert werden kann —
Umsetzung als Testinfrastruktur-Aufgabe (Schritt 8). Konkret:

- **Loopback-Paketserver:** minimaler `WebApplication`/Kestrel-Mini-Host auf
  `http://127.0.0.1:0` (freier Port vom OS; etabliertes Muster aus `PlaylistsE2ETestBase`:
  `WebApplicationFactory` + `UseKestrel()` + `UseUrls("http://127.0.0.1:0")`) — **kein**
  `HttpListener`, weil dessen `localhost`-Prefix unter Windows http.sys-URL-Reservierungen
  (`netsh urlacl`) brauchen kann. Antwortet wahlweise mit einem kleinen zip (Dummy-Binaries
  `ffmpeg.exe`/`ffprobe.exe` unter `bin/`) oder mit 404 (Fehlerszenario).
- **Quellen-Umleitung:** `MediaProbe:DownloadSources:win-x64` (bzw. `linux-x64`) per
  `UseSetting` auf die Mini-Host-URL.
- **„ffmpeg-freies" Umfeld:** Neuregistrierung der **echten** `FfprobeLocator`/`FfmpegLocator`
  über deren internal Ctor (`InternalsVisibleTo` vorhanden) mit (a) Temp-`baseDirectory`
  ohne `tools/ffmpeg/`, (b) `pathEnvironment = () => null` (neutralisiert das auf dem
  Testsystem vorhandene ffmpeg im PATH), (c) frischer `MediaProbeOptions`-Instanz ohne
  `FfprobePath`/`FfmpegPath` (neutralisiert evtl. gesetzte Config-Pfade des Hosts).
  `IFfmpegToolDownloadService` wird mit demselben Temp-`baseDirectory` registriert, damit
  die Installation ins Test-Verzeichnis geht und `Resolve()` danach real greift.

## Offene Punkte

Keine.
