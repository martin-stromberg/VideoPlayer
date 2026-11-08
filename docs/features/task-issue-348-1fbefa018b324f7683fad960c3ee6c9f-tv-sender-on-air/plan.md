# Umsetzungsplan: TV-Sender: On-Air (Issue #348)

## Übersicht

Der automatisierte TV-Sender erhält einen zweiten Admin-Schalter „On Air" (`Setup.TvSenderOnAir`),
der von der globalen Funktionsfreischaltung (`Setup.TvSenderEnabled`) unabhängig nur den
HLS-Live-Stream steuert: Im Off-Air-Zustand läuft keine ffmpeg-Segmentierung und
`/api/live-stream/*` antwortet 404, während Sendeplan, Programmauskunft und alle übrigen
TV-Sender-Funktionen unverändert weiterlaufen. Zuschauern zeigt `/live-tv` im Player-Bereich
ein Off-Air-Bild statt des Videos. Betroffen sind das Datenmodell (`Setup` + EF-Migration),
`ProgramSettingsService`, `LiveStreamWorker`, `LiveStreamController`, die Zuschauer-Seite
`/live-tv` samt neuem Bild-Asset, die Verwaltungsseite `/admin/tv-sender`, die
Backup-Restore-Listen und die Dokumentation.

## Scope-Klärung

| Feld | Festlegung |
|------|------------|
| Verarbeitungseinheit | Der Live-Stream des TV-Senders: HLS-Auslieferung über `LiveStreamController` (`/api/live-stream/*`) samt der sie erzeugenden ffmpeg-Segmentierung (`LiveStreamWorker` → `LiveStreamCoordinator` → `ILiveStreamSegmenter`). Nicht dazu: `ProgramScheduleWorker`, `ProgramGuideController`, `MediaDurationProbeWorker`, `TvSenderUserAccess`, `InternalMediaStreamController`, Verwaltungsseiten. |
| Auslösende Ereignisse | Administrator stellt den On-Air-Umschalter in `/admin/tv-sender` um → `ProgramSettingsService.UpdateTvSenderOnAirAsync` persistiert → `IProgramScheduleSignal.NotifyScheduleChangedNow()` weckt den `LiveStreamWorker` sofort; spätestens der nächste Poll-Takt (`PollCap` = 30 s) greift sonst. |
| Erwartete Wirkung | Off-Air: laufender ffmpeg-Lauf wird idempotent gestoppt (`ILiveStreamSegmenter.StopAsync`), `/api/live-stream/*` antwortet 404, `/live-tv` zeigt im Player-Bereich ein Off-Air-Bild (`#live-tv-off-air`). On-Air: `LiveStreamCoordinator.SynchronizeAsync` setzt die Segmentierung am nächsten Takt an der aktuellen Programmposition fort (zeitbasierte Resynchronisation existiert). Sendeplan und Programmauskunft bleiben in beiden Stellungen betriebsbereit. |
| Umgang mit fehlenden Objekten | `Setups`-Zeile: wiederverwenden — existiert bereits bzw. wird von `ProgramSettingsService.GetOrCreateSetupAsync` angelegt (Single-Row-Tabelle). Neue Spalte: per EF-Migration mit Default-Wert `true` in die bestehende Tabelle. Alt-Backups ohne die Spalte: über `VideoWebPlayerBackupData` (`OptionalRestoreColumns` + `OptionalRestoreBoolDefaults`) auf denselben Default `true` zurückführen. Laufender `ILiveStreamSegmenter`: beim Off-Air-Schalten stoppen, beim On-Air-Schalten vom Coordinator neu starten lassen — kein persistierter Stream-Zustand nötig. Off-Air-Bild: statisches Asset `wwwroot/images/live-tv-off-air.png`, einmalig erzeugt und ins Repo committet. |

### Scope-Ausschlüsse

| # | Einschränkung / Ausschluss | Begründung | Status |
|---|----------------------------|------------|--------|
| 1 | Kein zeit- oder regelgesteuerter Sendebetrieb — es gibt nur den manuellen Schalter, keinen automatischen On-Air-Fahrplan | Die Anforderung verlangt ausschließlich den manuellen Schalter; ein Fahrplan wäre eine neue Funktion | bestätigt (2026-11-08) |
| 2 | `Setup.TvSenderEnabled` bleibt die äußere Hürde — der On-Air-Schalter gilt nur innerhalb der Freischaltung und kann das Feature nicht teilweise „an" schalten | Anforderung: „unabhängig von der allgemeinen Funktionsaktivierung" bedeutet abschaltbar ohne das ganze Feature, nicht anstelle der Freischaltung | bestätigt (2026-11-08) |
| 3 | Sendeplan-Generierung und Programmauskunft laufen Off-Air unverändert weiter — kein Eingriff in `ProgramScheduleWorker`, `ProgramGuideController` oder die Programmdaten | Anforderung grenzt den Schalter bewusst auf den Stream ein; der Plan wird auch Off-Air weiter erzeugt und bleibt abfragbar | bestätigt (2026-11-08) |
| 4 | `TvSenderUserAccess` bleibt unverändert — der On-Air-Schalter ersetzt oder erweitert keine Benutzerberechtigung | Anforderung | bestätigt (2026-11-08) |
| 5 | `InternalMediaStreamController` (`/api/internal/media-stream/*`) folgt weiter allein `TvSenderEnabled` — kein On-Air-Gate | Annahme der Anforderung; der Endpunkt wird auch von der Laufzeitermittlung genutzt, die Off-Air weiterlaufen soll | bestätigt (2026-11-08) |
| 6 | Keine Off-Air-Markierung im Sendeplan und kein eigener „Off-Air"-Beitrag — ausgestrahlte Sendeplätze werden weder markiert noch umgeplant | Anforderung | bestätigt (2026-11-08) |
| 7 | Kein Leeren des Segment-Verzeichnisses beim Off-Air-Schalten — Restsegmente bleiben liegen und werden erst beim nächsten On-Air-Takt (Retention-Sweep in `SynchronizeAsync`) bzw. beim Worker-Start (`ClearAsync`) aufgeräumt | Durch das Endpunkt-Gate (404) sind Restsegmente unerreichbar; sofortiges Leeren bringt keinen sichtbaren Vorteil und würde Reste laufender Downloads unnötig abbauen | bestätigt (2026-11-08) |
| 8 | Der Stream-Endpunkt liefert Off-Air keinerlei Stream-Daten — weder echte Segmente noch Standby-Clip noch ein Hinweis-Asset (Antwort 404); die Off-Air-Erklärung erfolgt ausschließlich als Bild auf `/live-tv` | Anforderung „liefert keinen Stream aus"; ein Standby-/Hinweis-Video wäre ebenfalls eine Auslieferung. Die Zuschauer-Erklärung liegt im UI-Bild, nicht im Stream | bestätigt (2026-11-08) |

## Designentscheidungen

| Komponente / Bereich | Gewählter Ansatz | Begründung |
|----------------------|-----------------|------------|
| Speicherort des Schalters | Neue bool-Spalte `Setup.TvSenderOnAir` in der Single-Row-Tabelle `Setups` (nicht an `TVChannel`) | Es wird aktuell genau ein `TVChannel` ausgeliefert; das `TvSenderEnabled`-Muster (Entity-Initializer, `Get…`/`Update…`-Methoden, Backup-Listen, Migration, Alt-Backup-Test) ist komplett vorhanden und direkt übertragbar. Kanalfähigkeit kann später als `TVChannel`-Spalte nachgezogen werden. |
| Default-Wert des Schalters | `true` (On Air) — Entity-Initializer `= true`, Migration `defaultValue: true`, Restore-Default `true` | Bestandsinstallationen und Alt-Backups behalten ihr bisheriges Verhalten — ein Upgrade darf laufende Sender nicht stumm abschalten; die Funktionsfreischaltung bleibt ohnehin ab Werk aus. Alle Test-Fixtures bleiben ohne Anpassung On-Air. |
| Gate-Ebene der Segmentierung | Zusätzliche Bedingung im `LiveStreamWorker` vor `SynchronizeAsync` — nicht im `LiveStreamCoordinator`; Off-Air stoppt den ffmpeg-Lauf samt Ressourcen über die vorhandene Stopp-Verzweigung | Der Coordinator rechnet rein zeitbasiert und kennt keinen Schalter; der Worker ist der einzige Taktgeber und besitzt bereits die Stopp-Verzweigung des Feature-Flags (LiveStreamWorker.cs:96–103). „Deaktiviert" bedeutet Ressourcen-Freigabe, nicht nur Auslieferungs-Stopp; die Wiederaufnahme ist über die zeitbasierte Resynchronisation kostenlos. |
| Endpunkt-Antwort Off-Air | `404 NotFound`, geprüft **vor** `TryResolveFileAsync` und damit auch vor `ServeStandbyAsync` | Konsistent zum Feature-Gate und zur Anforderung „keinen Stream ausliefern"; der warme Standby-Stand bleibt im Speicher und beschleunigt den On-Air-Übergang, wird aber Off-Air nicht ausgeliefert. |
| Service-Zugriff | Eigene Methoden `GetTvSenderOnAirAsync`/`UpdateTvSenderOnAirAsync` statt Erweiterung von `GeneralSettingsUpdate` | Der Schalter soll mit dem Umschalten sofort wirken (eigene Sub-Karte, eigener Handler) und nicht gebündelt mit „Speichern" der Allgemein-Einstellungen; `UpdateTvSenderEnabledAsync` (ProgramSettingsService.cs:206) ist das direkte Vorbild. `GeneralSettingsUpdate` bleibt unverändert. |
| Aufwecken beim Umschalten | `IProgramScheduleSignal.NotifyScheduleChangedNow()` statt `NotifyScheduleChanged()` | Ein Sendeschalter soll unverzüglich greifen und nicht das Debounce-Fenster (`ScheduleChangedDebounceSeconds`) abwarten; das ungebatchte Signal weckt über `WakeWorkers` denselben `_liveChannel` (ProgramScheduleSignal.cs:137–150, 182–186). Mechanismus identisch zum etablierten Muster, nur ohne Batching. |
| Platzierung des Schalters | Sofort wirkender `form-check`-Umschalter `#tvsender-on-air` in eigener Sub-Karte „Sendebetrieb" im Tab „Einstellungen" von `TVSenderAdmin.razor` (vor/neben der Sub-Karte „Segmentierung") | Ein Sendeschalter gehört zur Sender-Verwaltung und darf nicht an einen gebündelten „Speichern"-Button gekoppelt sein; `NotifyScheduleChangedNow` weckt den Worker unverzüglich. Die Seite ist admin-only (`isAdmin`-Gate) — die Berechtigung ist damit serverseitig erzwungen, ein eigener Endpunkt entsteht nicht. |
| Off-Air-Zustand auf `/live-tv` | Eigenes Off-Air-Bild (`<img id="live-tv-off-air">`) im Player-Bereich (`livetv-player-wrap`) als primäre Darstellung; `LiveTvErrorMessages.NotOnAir` dient als `alt`-Text (zugängliche Beschreibung, keine sichtbare Textmeldung). Programmauskunft-Karten laufen weiter | Der Anwender hat entschieden: Ein Bild signalisiert den eingestellten Sendebetrieb eindeutiger als Text. Die Programmauskunft bleibt Off-Air bewusst abfragbar — sie komplett auszublenden (wie `#live-tv-disabled`) würde „nicht freigeschaltet" und „nicht auf Sendung" verwischen. |
| Off-Air-Bild-Asset | Neues statisches Asset `VideoWebPlayer/wwwroot/images/live-tv-off-air.png` (16:9, z. B. 1280×720, Testbild-/Standby-Stil), einmalig per ffmpeg erzeugt und ins Repo committet; csproj-`Content Update`-Eintrag mit `CopyToOutputDirectory=Always` analog `placeholder.png`; Referenz über `AssetVersions.Url("images/live-tv-off-air.png")` | Kein vorhandenes Asset passt: `wwwroot/images/placeholder.png` ist ein hochformatiges Film-Poster (801×1200), `Images/` im Repo-Root enthält nur Icons/Poster außerhalb des Webroots (u. a. `tv.png` 512×512) und `Assets/LiveStreamStandby/standby_*.ts` sind Video-Segmente, keine Bilder. Ein per ffmpeg generiertes 16:9-Testbild (`testsrc2`/`smptehdbars`) ist lizenzfrei, selbsterklärend und reproduzierbar. |

## Programmabläufe

### Admin schaltet Off-Air

1. Admin öffnet `/admin/tv-sender`, Tab „Einstellungen"; `OnInitializedAsync` hat `tvSenderOnAir`
   über `ProgramSettingsService.GetTvSenderOnAirAsync` geladen (nur bei `isAdmin` und
   `tvSenderEnabled` — die Seite ist ohnehin hinter beiden Gates).
2. Admin betätigt den On-Air-Umschalter → Handler `ToggleOnAirAsync` ruft
   `ProgramSettingsService.UpdateTvSenderOnAirAsync(false)` → `Setup.TvSenderOnAir` persistiert;
   danach `ScheduleSignal.NotifyScheduleChangedNow()` → `WakeWorkers` schreibt in den
   `_liveChannel`. Status-Text bestätigt; schlägt das Update fehl, wird die Checkbox
   zurückgesetzt und die Fehlermeldung angezeigt.
3. `LiveStreamWorker.ExecuteAsync` wird aus `WaitLiveAsync` geweckt →
   `GetTvSenderEnabledAsync` = `true`, `GetTvSenderOnAirAsync` = `false` → die bestehende
   Stopp-Verzweigung greift: `ILiveStreamSegmenter.IsRunning` → `StopAsync` (ffmpeg-Prozessbaum
   beendet, `StopTimeout` 10 s). Der Poll-Takt läuft weiter.
4. Nächste Anfrage an `/api/live-stream/{fileName}`: `LiveStreamController.GetFile` →
   `CheckLoggedIn` → Feature-Gate (passiert) → Benutzerfreigabe (passiert) →
   **On-Air-Gate → `NotFound()`** — weder echte Playlist/Segmente noch Standby werden
   ausgeliefert.
5. `/live-tv`: Der 15-s-Poll-Tick (`PollLoopAsync`) liest `tvSenderOnAir = false` →
   `OnAfterRenderAsync` detacht den laufenden Player (`DetachPlayerAsync`, hls.js-Re-Attach
   stoppt) → der Player-Bereich rendert `#live-tv-off-air` (Off-Air-Bild) statt `<video>`;
   die Programmauskunft-Karten aktualisieren weiter über `/api/program-guide/*`.

Beteiligte Klassen/Komponenten: `TVSenderAdmin.razor`, `ProgramSettingsService`,
`IProgramScheduleSignal`/`ProgramScheduleSignal`, `LiveStreamWorker`, `ILiveStreamSegmenter`,
`LiveStreamController`, `LiveTv.razor`, `liveTv.js` (nur via Detach).

### Admin schaltet wieder On-Air

1.–2. Wie Off-Air, mit `UpdateTvSenderOnAirAsync(true)` und `NotifyScheduleChangedNow()`.
3. `LiveStreamWorker`-Takt: beide Gates `true` → `IBackgroundProcessingGate`-Lease →
   `LiveStreamCoordinator.SynchronizeAsync` → `IProgramGuideService.GetNowAsync` →
   Eingangskaskade (`LiveStreamMediaResolver`) → `ILiveStreamSegmenter.StartAsync` an der
   aktuellen Programmposition (Resynchronisation ist eingebaut, kein Neustart-Stand nötig).
4. `/api/live-stream/*` liefert wieder die echte `live.m3u8`/Segmente; bis die erste echte
   Playlist steht, greift der bestehende Standby-Pfad (`ServeStandbyAsync`) — unverändertes
   Warm-up-Verhalten.
5. `/live-tv`: Poll-Tick liest `tvSenderOnAir = true` → `<video>` wird wieder gerendert →
   `OnAfterRenderAsync` attacht den Player; die Standby→echt-Discontinuity-Markierung greift
   wie gehabt.

Beteiligte Klassen/Komponenten: wie oben plus `LiveStreamCoordinator`,
`LiveStreamMediaResolver`, `LiveStreamStandbyProvider`.

### Alt-Backup-Wiederherstellung (Backup ohne die neue Spalte)

1. `VideoWebPlayerBackupData.ReadFromAsync` liest ein Archiv, dessen `Setups`-Tabelle
   `TvSenderOnAir` nicht enthält.
2. `OptionalRestoreColumns` deklariert die Spalte als optional → kein Fehler;
   `OptionalRestoreBoolDefaults` liefert den Default `true`.
3. Die wiederhergestellte `Setups`-Zeile trägt den Default — eine Alt-Installation verhält
   sich exakt wie bisher (Stream läuft, sobald das Feature freigeschaltet ist).

Beteiligte Klassen/Komponenten: `VideoWebPlayerBackupData`, `Setup`.

## Neue Klassen

Keine — der Schalter folgt vollständig dem `TvSenderEnabled`-Muster (Entity-Eigenschaft +
Service-Methoden + Migration). Die generierte Migrationsklasse ist unter
„Datenbankmigrationen" geführt; das Off-Air-Bild ist ein Asset, keine Klasse.

## Änderungen an bestehenden Klassen

### `Setup` (Datenmodellklasse, `VideoWebPlayer/Data/Setup.cs`)

- **Neue Eigenschaften:** `TvSenderOnAir` (`bool`, Initializer `= true`) — On-Air-Schalter;
  wirkt nur innerhalb von `TvSenderEnabled`. Platzierung nahe `TvSenderEnabled` (Zeile 80)
  mit gleichem Kommentarstil.

### `ProgramSettingsService` (`VideoWebPlayer/Services/ProgramSettingsService.cs`)

- **Neue Methoden:**
  - `GetTvSenderOnAirAsync(CancellationToken)` → `Task<bool>` — liest
    `Setup.TvSenderOnAir` über `GetOrCreateSetupAsync` (Vorbild `GetTvSenderEnabledAsync`,
    Zeile 195).
  - `UpdateTvSenderOnAirAsync(bool onAir, CancellationToken)` → `Task` — persistiert den
    Schalter (Vorbild `UpdateTvSenderEnabledAsync`, Zeile 206). Das Aufwecken der Worker
    bleibt — wie überall — Aufgabe des Aufrufers (`IProgramScheduleSignal`).

### `LiveStreamWorker` (`VideoWebPlayer/Services/LiveStreamWorker.cs`)

- **Geänderte Methoden:** `ExecuteAsync` — die Gate-Bedingung in der Poll-Schleife
  (Zeilen 96–104) wird erweitert: `GetTvSenderEnabledAsync` und, nur wenn das Feature an ist,
  `GetTvSenderOnAirAsync` lesen; `enabled && onAir` → bisheriger `SynchronizeAsync`-Pfad,
  andernfalls die vorhandene Stopp-Verzweigung (`ILiveStreamSegmenter.IsRunning` →
  `StopAsync`). Der `IBackgroundProcessingGate`-Lease bleibt wie heute nur auf der
  Arbeitspfad-Seite; der Poll-Takt und `WakeAtUtc`-Handling bleiben unverändert.

### `LiveStreamController` (`VideoWebPlayer/Controllers/LiveStreamController.cs`)

- **Geänderte Methoden:** `GetFile` — neues Gate nach der Benutzerfreigabe
  (Reihenfolge `CheckLoggedIn` → `TvSenderEnabled` → `IsTvSenderAccessGrantedAsync` →
  **`GetTvSenderOnAirAsync` → `NotFound()`**), vor `TryResolveFileAsync` und damit auch vor
  `ServeStandbyAsync`.

### `LiveTv.razor` (`VideoWebPlayer/Components/Pages/LiveTv.razor`)

- **Neuer Zustand:** `tvSenderOnAir` (`bool`) — in `OnInitializedAsync` nach
  `IsTvSenderAccessGrantedAsync` über `ProgramSettings.GetTvSenderOnAirAsync` lesen; in
  `PollLoopAsync` je Tick erneut lesen (ein Setup-Lesezugriff je 15 s — dieselbe
  Kostklasse wie die Guide-Aufrufe), damit der Zustand ohne Seiten-Reload umspringt.
- **Anzeige:** Im Player-Bereich (`livetv-player-wrap`, Zeilen 64–87) hat der Off-Air-Zustand
  Vorrang vor `<video>` und Gap-Hinweis: `tvSenderOnAir == false` →
  `<img class="livetv-off-air" id="live-tv-off-air" src="@OffAirImageUrl" alt="@LiveTvErrorMessages.NotOnAir" />`
  als primäre Darstellung (kein sichtbarer Hinweistext; der `alt`-Text trägt die zugängliche
  Beschreibung). `OffAirImageUrl` wird über den bereits injizierten `StaticAssetVersioner`
  aufgelöst (`AssetVersions.Url("images/live-tv-off-air.png")`). Die
  Programmauskunft-Karten (`livetv-side`) und `RefreshGuideAsync` laufen unverändert weiter.
- **Geänderte Methode:** `OnAfterRenderAsync` — die effektive Attach-Bedingung wird um
  `tvSenderOnAir` erweitert (Off-Air verhält sich attach-seitig wie „kein laufender Slot":
  bestehender Attach → `DetachPlayerAsync`, kein Re-Attach). Damit stoppt die
  hls.js-Fehlerschleife (`NotifyStreamError`/5-s-Timer) beim Off-Air-Schalten.

### `LiveTv.razor.css` (`VideoWebPlayer/Components/Pages/LiveTv.razor.css`)

- **Neue Regel:** `.livetv-off-air` — `width: 100%`, `display: block`, `aspect-ratio: 16 / 9`,
  `object-fit: cover`, `background: #000` (füllt den Player-Bereich exakt wie
  `.livetv-video`, Zeilen 42–47).

### `LiveTvErrorMessages` (`VideoWebPlayer/ViewModels/LiveTvErrorMessages.cs`)

- **Neue Konstante:** `NotOnAir` — „Der Sender ist zurzeit nicht auf Sendung." (gleicher
  Konstanten-Stil wie `NoProgramRunning`). Verwendung als `alt`-Text des Off-Air-Bildes.

### `TVSenderAdmin.razor` (`VideoWebPlayer/Components/Pages/Admin/TVSenderAdmin.razor`)

- **Neuer Zustand:** `tvSenderOnAir` (`bool`) — in `OnInitializedAsync` (nach dem
  `tvSenderEnabled`-Gate, ~Zeile 1260) über `GetTvSenderOnAirAsync` laden.
- **Neue UI:** Eigene Sub-Karte „Sendebetrieb" im Tab „Einstellungen" (vor bzw. neben der
  Sub-Karte „Segmentierung", Zeilen 717–740) mit `form-check`-Umschalter `#tvsender-on-air`
  und Erklärtext. Der Umschalter wirkt **sofort** (`@onchange`-Handler `ToggleOnAirAsync`),
  nicht erst über den gebündelten „Speichern"-Button.
- **Neuer Handler:** `ToggleOnAirAsync` → `UpdateTvSenderOnAirAsync` →
  `ScheduleSignal.NotifyScheduleChangedNow()` → `channelStatus`-Bestätigung; im Fehlerfall
  Checkbox-Stand zurücksetzen und Fehlermeldung anzeigen. Die Seite ist admin-only
  (`isAdmin`-Gate), ein separater Endpunkt entsteht nicht — die Berechtigung ist damit
  serverseitig erzwungen.

### `VideoWebPlayerBackupData` (`VideoWebPlayer/Services/Backups/VideoWebPlayerBackupData.cs`)

- `OptionalRestoreColumns` (Liste ab Zeile ~85): Eintrag `Setups.TvSenderOnAir` ergänzen.
- `OptionalRestoreBoolDefaults` (Zeile ~136): Eintrag `(Setups, TvSenderOnAir, true)`
  ergänzen.

### `VideoWebPlayer.csproj`

- **Neuer `Content Update`-Eintrag:** `wwwroot\images\live-tv-off-air.png` mit
  `CopyToOutputDirectory=Always` (analog `placeholder.png`, Zeile 74).

### `ApplicationDbContextModelSnapshot` (`VideoWebPlayer/Migrations/`)

- Wird durch die neue Migration aktualisiert (Eintrag analog `TvSenderEnabled`,
  `b.Property<bool>("TvSenderOnAir").HasColumnType("INTEGER")`).

## Datenbankmigrationen

| Migrationsname | Betroffene Tabellen/Spalten | Beschreibung der Änderung |
|----------------|----------------------------|---------------------------|
| `AddSetupTvSenderOnAir` | `Setups.TvSenderOnAir` (INTEGER, NOT NULL) | Neue bool-Spalte mit `defaultValue: true`. Kein Raw-SQL nötig; Bestandszeilen erhalten den Default. Vorbild: `AddSetupTvSenderEnabled`. |

## Validierungsregeln

Keine — der Schalter ist ein boolescher Wert ohne Wertebereich. Die Admin-Berechtigung wird
serverseitig durch das bestehende `isAdmin`-Gate der Verwaltungsseite erzwungen; ein
eigener Update-Endpunkt entsteht nicht.

## Konfigurationsänderungen

Keine — anwendungsweite Admin-Einstellung in der `Setups`-Tabelle (Single-Row), keine
`appsettings`-Ebene (`LiveStreamOptions` bleibt unverändert).

## Seiteneffekte und Risiken

- **Test-Fixtures (`ProgramScheduleTestDb`, `LiveTvTests.CreateContext`, E2E-Basen):**
  seeden `new Setup { TvSenderEnabled = true }`. Mit dem Entity-Initializer
  `TvSenderOnAir = true` bleiben alle bestehenden Stream-Tests unverändert On-Air — keine
  Fixture-Anpassung nötig.
- **hls.js-Re-Attach-Schleife:** Läuft der Player beim Off-Air-Schalten, antwortet der
  Endpunkt 404 — ohne serverseitiges Detach würde `liveTv.js` im 5-s-Takt re-attachen.
  Die Attach-Bedingung in `OnAfterRenderAsync` muss deshalb `tvSenderOnAir` einbeziehen
  (siehe `LiveTv.razor`-Abschnitt).
- **Standby-Provider:** Bleibt Off-Air vorbereitet im Speicher (Refresh läuft beim
  Worker-Start), wird bei Endpunkt-404 nur nicht mehr ausgeliefert; der warme Stand macht
  den On-Air-Übergang schneller.
- **Segment-Verzeichnis:** Off-Air läuft kein Retention-Sweep (liegt in
  `SynchronizeAsync`) — Restsegmente bleiben bis zum nächsten On-Air-Takt bzw.
  `ClearAsync` beim Worker-Start liegen, sind aber durch das Endpunkt-Gate unerreichbar
  (Scope-Ausschluss 7).
- **Navigation:** Der „Live-TV"-Eintrag bleibt Off-Air sichtbar
  (`IsTvSenderAccessGrantedAsync` hängt nur an `TvSenderEnabled` + Freigabe) — gewollt;
  die Seite selbst erklärt den Off-Air-Zustand über das Bild.
- **Notify-Race:** Das ungebatchte Signal kann während eines laufenden `SynchronizeAsync`
  ankommen; die Schleife ist sequenziell — der nächste Takt wertet den neuen Stand aus.
  Kein neuer Zustand nötig.

## Umsetzungsreihenfolge

1. **Datenmodell: `Setup.TvSenderOnAir` + EF-Migration `AddSetupTvSenderOnAir`**
   - Voraussetzungen: Keine.
   - Beschreibung: Eigenschaft mit Initializer `= true` nahe `TvSenderEnabled` anlegen;
     `dotnet ef migrations add` erzeugt Migration und Snapshot-Update.
2. **Backup-Kompatibilität: `VideoWebPlayerBackupData`-Listen + Alt-Backup-Regressionstest**
   - Voraussetzungen: Schritt 1 (Spalte existiert).
   - Beschreibung: `OptionalRestoreColumns` und `OptionalRestoreBoolDefaults` ergänzen;
     Testklasse `VideoWebPlayerBackupDataTests_TvSenderOnAir` nach dem
     `VideoWebPlayerBackupDataTests_TvSenderEnabled`-Muster mit
     `LegacyBackupArchiveBuilder.RemoveColumnsAsync` (Seed auf den Nicht-Default-Wert
     `false` stellen, damit der Restore-Default bewiesen wird).
3. **Service: `GetTvSenderOnAirAsync` / `UpdateTvSenderOnAirAsync`**
   - Voraussetzungen: Schritt 1.
   - Beschreibung: Beide Methoden nach dem `TvSenderEnabled`-Vorbild im
     `ProgramSettingsService`; Roundtrip-Tests in `ProgramSettingsServiceTests`.
4. **Worker-Gate: `LiveStreamWorker.ExecuteAsync`**
   - Voraussetzungen: Schritt 3.
   - Beschreibung: On-Air-Stand in der Gate-Verzweigung lesen; Off-Air → vorhandene
     Stopp-Verzweigung. Tests `LiveStreamWorkerTests` (FakeSegmenter beweist
     Stopp/Nicht-Start).
5. **Endpunkt-Gate: `LiveStreamController.GetFile`**
   - Voraussetzungen: Schritt 3.
   - Beschreibung: On-Air-Gate nach der Benutzerfreigabe → `NotFound()`; Tests
     `LiveStreamControllerTests` (Theory über Playlist, Standby-Segment, echtes Segment).
6. **Zuschauer-UI: Off-Air-Bild-Asset + `LiveTvErrorMessages.NotOnAir` + `LiveTv.razor`**
   - Voraussetzungen: Schritt 3.
   - Beschreibung: `live-tv-off-air.png` (16:9-Testbild, per ffmpeg erzeugt) unter
     `wwwroot/images/` committen + csproj-Eintrag; Konstante `NotOnAir` (alt-Text);
     `tvSenderOnAir`-Zustand (Init + Poll), Off-Air-Bild-Zweig im Player-Bereich,
     `.livetv-off-air`-CSS-Regel, Attach-Bedingung erweitern. bUnit-Tests `LiveTvTests`
     (`CreateContext` um `tvSenderOnAir`-Parameter erweitern).
7. **Admin-UI: `TVSenderAdmin.razor` Sub-Karte „Sendebetrieb" + `ToggleOnAirAsync`**
   - Voraussetzungen: Schritt 3.
   - Beschreibung: Sub-Karte im Tab „Einstellungen", Sofort-Umschalter mit
     `NotifyScheduleChangedNow`, Fehler-Rollback der Checkbox. bUnit-Tests
     `TVSenderAdminTests`.
8. **E2E-Tests**
   - Voraussetzungen: Schritte 4–7.
   - Beschreibung: Szenarien aus dem E2E-Abschnitt (`LiveStreamE2ETests`,
     `LiveTvE2ETests`, neue `TvSenderOnAirE2ETests` auf `TVSenderAdminE2ETestBase`).
9. **Dokumentation**
   - Voraussetzungen: Schritte 1–7 (fachlich korrekte Beschreibung des Endverhaltens).
   - Beschreibung: `docs/help/tv-sender.md` (Schalter erklären, Unterschied zur
     Freischaltung, Off-Air-Bild), `docs/help/tv-sender-ablauf-technisch.md` (Gate-Kette,
     Signal), `README.md`, `docs/RELEASE_NOTES.md`.

## Tests

### Neue Tests

| Test / Hilfsmethode | Testklasse | Was wird geprüft / bereitgestellt? |
|--------------------|------------|-------------------------------------|
| `GetTvSenderOnAirAsync_DefaultsToTrue` | `ProgramSettingsServiceTests` (oder neue Teilklasse) | Default `true` der Spalte beim `GetOrCreateSetupAsync`-Pfad. |
| `UpdateTvSenderOnAirAsync_Roundtrip` | `ProgramSettingsServiceTests` | Schreiben/Lesen des Schalters gegen echtes SQLite. |
| `ReadFromAsync_LegacyBackupWithoutTvSenderOnAirColumn_RestoresDefaultTrue` | `VideoWebPlayerBackupDataTests_TvSenderOnAir` (neu) | Alt-Backup ohne die Spalte; Seed auf `false` (Nicht-Default), Restore-Default `true` wird bewiesen — Vorbild `..._TvSenderEnabled`. |
| `ExecuteAsync_OffAir_StopsSegmenterAndNeverStarts` | `LiveStreamWorkerTests` | `TvSenderEnabled=true`, `TvSenderOnAir=false`: laufender `FakeSegmenter` wird gestoppt (`StopCalls >= 1`), nie gestartet — Vorbild `ExecuteAsync_FeatureNotReleased_StopsSegmenterAndNeverStarts`. |
| `ExecuteAsync_OffAirToOnAir_ResumesSegmentation` | `LiveStreamWorkerTests` | Umschalten zwischendurch: nach erneutem On-Air ruft der Worker wieder `SynchronizeAsync` (Coordinator-Fake zählt) — beweist Wirken ohne Neustart. |
| `GetFile_OffAir_Returns404` (Theory: `live.m3u8`, `standby_0_00.ts`, `seg_001.ts`) | `Controllers/LiveStreamControllerTests` | On-Air-Gate greift vor echter Datei und vor Standby — Vorbild `GetFile_FeatureDisabled_Returns404` (Zeile 346). |
| `Page_OffAir_ShowsOffAirImage_ProgramGuideStillRenders` | `Components/LiveTvTests` | Off-Air → `#live-tv-off-air` (`<img>` mit Off-Air-src und `alt` = `NotOnAir`) sichtbar, kein `<video>`, Guide-Karten rendern weiter — `CreateContext` um `tvSenderOnAir`-Parameter erweitern (Vorbild `Page_FeatureNotReleased_...`). |
| `PollTick_OffAirSwitch_DetachesPlayerAndShowsOffAirImage` | `Components/LiveTvTests` | `ControllableTimeProvider`-Tick nach Umschalten → Player-Detach, Off-Air-Bild erscheint ohne Reload. |
| `SettingsTab_OnAirToggle_PersistsAndNotifies` | `Components/TVSenderAdminTests` (neue Teilklasse oder vorhandene) | Umschalter toggelt → `UpdateTvSenderOnAirAsync` persistiert + Signal feuert — Vorbild `StandbyCard_*`-Tests. |
| `SettingsTab_OnAirToggle_SaveFails_RevertsCheckbox` | `Components/TVSenderAdminTests` | Fehler beim Update → Checkbox-Stand zurückgesetzt, Fehlermeldung sichtbar. |

### Betroffene bestehende Tests

| Test / Testklasse | Grund der Anpassung |
|-------------------|---------------------|
| `Components/LiveTvTests` (`CreateContext`-Hilfsmethode) | Neuer Parameter `tvSenderOnAir` (Default `true`) — bestehende Aufrufe bleiben unverändert, neue Tests reichen den Off-Air-Stand durch. |
| `Helpers/TVSenderAdminE2ETestBase` | Neue Hilfsmethode `SetOnAirAsync(bool)` nach dem `UpdateSetupAsync`-Muster für die E2E-Szenarien (keine Änderung an bestehenden Aufrufen). |

### E2E-Tests (primärer Funktionsnachweis)

| Priorität | Szenario | Testdatei / Testklasse | Abgedecktes Akzeptanzkriterium | Warum E2E nötig ist |
|-----------|----------|------------------------|-------------------------------|-------------------|
| Pflicht | Admin schaltet Off-Air über `/admin/tv-sender` → `/api/live-stream/live.m3u8` antwortet 404 → `/live-tv` zeigt das Off-Air-Bild (`#live-tv-off-air`-`img` sichtbar, `naturalWidth > 0`) | neue `TvSenderOnAirE2ETests` (`Helpers/TVSenderAdminE2ETestBase`) | Vollständiger Happy Path vom Admin-Eingriff bis zur Zuschauer-Sicht | Der Ablauf überspringt UI, Service, Signal, Worker-Gate, Endpoint und Zuschauerseite — nur ein echter Kestrel-Lauf mit echter `Setups`-Zeile beweist das Zusammenspiel; das Bild-Asset selbst wird nur im Browser-Nachweis als wirklich ausgeliefert geprüft. |
| Pflicht | Off-Air → On-Air: Umschalten zurück → Endpunkt liefert wieder (echte Playlist oder Standby), `/live-tv` attacht den Player erneut | `TvSenderOnAirE2ETests` | Wiederaufnahme ohne Neustart an aktueller Programmposition | Resynchronisation über Worker/Coordinator + Signal ist nur integriert prüfbar. |
| Pflicht | Off-Air blockiert den Stream-Endpunkt auch für berechtigte Zuschauer und auch den Standby-Pfad | `LiveStreamE2ETests` (bestehende Klasse erweitern) | `/api/live-stream/*` liefert Off-Air keinen Stream (Vorbild der Feature-Gate-E2E-Tests) | Die Gate-Reihenfolge (Freischaltung → Freigabe → On-Air → Standby) ist nur über echte HTTP-Anfragen beweisbar. |
| Pflicht | Off-Air-Bild auf `/live-tv` bei laufender Sendung, Programmauskunft bleibt sichtbar | `LiveTvE2ETests` (bestehende Klasse erweitern, `UpdateSetupAsync`) | Zuschauer sehen das Off-Air-Bild statt Player/Fehlertext | Browser-Sicht der Zustandskaskade inkl. Player-Detach und tatsächlich geladenem Bild. |
| Optional | Sendeplan/Programmauskunft bleiben Off-Air abfragbar (`/api/program-guide/now` → 200) | `ProgramGuideE2ETests` oder `TvSenderOnAirE2ETests` | Scope-Ausschluss 3 hält | Gegenprobe, dass das Gate nicht zu weit greift — als Testfall billig, als bUnit/Controller-Test schon abgedeckt. |

Anzupassende bestehende E2E-Tests: Keine — mit Default `true` bleiben alle bestehenden
Live-Stream-/Live-TV-E2E-Szenarien On-Air.

## Akzeptanzszenarien

| # | Szenario | Auslöser | Ausgangszustand | Schritte | Sichtbare Wirkung | Nachweis |
|---|----------|----------|------------------|----------|-------------------|----------|
| 1 | Happy Path: Admin schaltet Off-Air | Umschalter in `/admin/tv-sender` → „Einstellungen" | Feature freigeschaltet, `TvSenderOnAir=true`, ffmpeg-Segmentierung läuft, Zuschauer auf `/live-tv`, `Setups`-Zeile existiert | `UpdateTvSenderOnAirAsync(false)` → `NotifyScheduleChangedNow` → Worker weckt aus `WaitLiveAsync` → `StopAsync` → Endpoint 404 → `/live-tv`-Poll zeigt Off-Air-Bild | ffmpeg beendet; `/api/live-stream/*` → 404; `/live-tv` zeigt das Off-Air-Bild (`#live-tv-off-air`) mit weiterlaufender Programmauskunft; Sendeplan wird weiter erzeugt | E2E `TvSenderOnAirE2ETests` + `LiveStreamWorkerTests`/`LiveStreamControllerTests`/`LiveTvTests` |
| 2 | Wiederaufnahme: Admin schaltet On-Air | Umschalter zurück | Wie Szenario 1 danach: Off-Air, kein ffmpeg, `Setups`-Zeile trägt `false` | `UpdateTvSenderOnAirAsync(true)` → Signal → Worker-Takt → `SynchronizeAsync` → `StartAsync` an Programmposition | Stream erreichbar (Standby bis erste echte Playlist), Player attacht wieder | E2E `TvSenderOnAirE2ETests` + Worker-Test |
| 3 | Fehlende Voraussetzung: Feature nicht freigeschaltet | — | `TvSenderEnabled=false` (Auslieferungszustand) | Admin öffnet `/admin/tv-sender` → Top-Gate zeigt Freischaltungs-Hinweis, On-Air-Schalter nicht erreichbar; `/api/live-stream/*` bleibt 404 | Kein Weg erreicht den Stream — On-Air kann nicht teilweise freischalten | Bestehende Gates + `GetFile_FeatureDisabled_Returns404`; Admin-UI-Test |
| 4 | Alt-Backup ohne die neue Spalte | Wiederherstellung eines Backups vor der Migration | Frische DB, `Setups`-Tabelle im Archiv ohne `TvSenderOnAir` | `ReadFromAsync` → `OptionalRestoreBoolDefaults` liefert Default | Zeile mit Default `true` — Verhalten wie bisher | `VideoWebPlayerBackupDataTests_TvSenderOnAir` |
| 5 | Off-Air während laufender Wiedergabe | Umschalten während ein Zuschauer schaut | Player attached (`hls.js`), Sendung läuft | 15-s-Poll-Tick liest `tvSenderOnAir=false` → `DetachPlayerAsync` → Off-Air-Bild statt Video | Kein 404-Dauerfehler des Players, eindeutiges Off-Air-Bild, Guide-Karten weiter aktuell | bUnit `PollTick_OffAirSwitch_...` + E2E |
| 6 | Wartesituation: Umschalten ohne Signalverlust | Admin schaltet, Signal gebündelt/unterdrückt während Scan | `BeginScan`-Scope offen | Ohne Signal: nächster `PollCap`-Takt (≤30 s) wertet den neuen Stand aus | Wirksamkeit garantiert auch ohne sofortiges Wake — kein dauerhaft „hängender" Zustand | `LiveStreamWorkerTests` (Takt läuft weiter) |

## Offene Punkte

Keine.
