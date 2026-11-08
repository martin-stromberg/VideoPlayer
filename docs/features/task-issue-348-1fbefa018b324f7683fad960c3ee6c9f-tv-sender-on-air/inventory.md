# Bestandsaufnahme: TV-Sender: On-Air (Issue #348)

Analysiert wurde der Live-Stream-Pfad des automatisierten TV-Senders (Setup-Modell,
`LiveStreamWorker` → `LiveStreamCoordinator` → `ILiveStreamSegmenter`,
`LiveStreamController`-Auslieferung, Standby-Mechanik, Admin-/Zuschauer-UI) bezogen auf die
Anforderung eines von `Setup.TvSenderEnabled` unabhängigen „On-Air"-Schalters
(`requirement.md`). Basisstand: `staging`-HEAD `1be452b`, der Task-Branch hat noch keine
eigenen Commits.

## Zusammenfassung

- **Der Schalter existiert noch nicht.** `Setup` trägt die Feature-Freischaltung
  `TvSenderEnabled` (Default `false`, Single-Row-Tabelle mit `UX_Setups_SingleRow`), aber
  keine On-Air-Eigenschaft; `GeneralSettingsUpdate` enthält nur `TvSenderEnabled`. Das
  etablierte Muster (bool-Spalte + `Get…`/`Update…` im `ProgramSettingsService` +
  `OptionalRestoreColumns`/`OptionalRestoreBoolDefaults` im Backup + EF-Migration +
  Alt-Backup-Regressionstest) ist komplett vorhanden — `TvSenderEnabled` ist das
  Referenzbeispiel.
- **Genau ein Gate steuert den Stream heute:** `LiveStreamWorker.ExecuteAsync` Zeile 96
  (`GetTvSenderEnabledAsync` → Segmenter idempotent stoppen vs. `SynchronizeAsync`) und
  `LiveStreamController.GetFile` Zeilen 77/81 (404 Feature-Gate → 403 Benutzerfreigabe).
  Ein On-Air-Gate hätte in beiden Fällen eine vorhandene Einfügestelle; Sofortwirken ist
  über `IProgramScheduleSignal.NotifyScheduleChanged()` machbar (Live-Channel `WaitLiveAsync`
  weckt den Worker — derselbe Aufruf, mit dem `ProgramSettings.razor:234` und
  `TVSenderAdmin.razor:2028` heute wirken).
- **Kein persistierter Stream-Zustand:** Der Coordinator rechnet jeden Takt die Wahrheit
  aus der Uhr neu — ein Off-Air-Stopp braucht keinen Abbau, ein On-Air-Start
  resynchronisiert am nächsten Takt an der Programmposition (Anforderungsannahme durch
  den Code bestätigt).
- **Standby-Pfad entscheidet mit:** Fehlt eine echte Playlist, antwortet der Controller aus
  dem `LiveStreamStandbyProvider` (eingebetteter oder konfigurierter Clip) statt 404 — ob
  das Off-Air so bleibt, ist eine offene Anforderungsfrage, kein Code-Befund.
- **Unbeteiligte Nachbarn sind sauber getrennt:** `ProgramGuideController`,
  `InternalMediaStreamController`, `ProgramScheduleWorker`, `MediaDurationProbeWorker` und
  die Benutzer-Freigabeliste `TvSenderUserAccess` folgen jeweils eigenen, expliziten
  `TvSenderEnabled`-Gates — der On-Air-Schalter muss sie nicht berühren, damit Sendeplan
  und Programmauskunft Off-Air weiterlaufen.
- **UI-Einfügestellen vorhanden:** `TVSenderAdmin.razor` Tab „Einstellungen" (Sub-Karte
  „Segmentierung" + ein Save-Pfad mit `NotifyScheduleChanged`), `ProgramSettings.razor`
  Karte „TV-Sender" (Freischaltung + Allowlist + Signal), `LiveTv.razor` Zustandskaskade
  (disabled/no-access/error → Player/Gap-Hinweis) mit `LiveTvErrorMessages` —
  kein Off-Air-Text/-Zustand vorhanden.
- **Backup-Kompatibilität:** `VideoWebPlayerBackupData` führt `Setups.TvSenderEnabled`
  in `OptionalRestoreColumns` (Zeile 100) und `OptionalRestoreBoolDefaults` (Zeile 150,
  Default `false`) — für die neue Spalte sind beide Listen plus Regressionstest
  (`LegacyBackupArchiveBuilder.RemoveColumnsAsync`-Muster) nachzuziehen.

Test-Ausgangszustand: **Alles grün** — Unit/Integration-Lauf (`Category!=E2E`): 2681 von
2682 bestanden, 0 Fehlschläge, 1 absichtsvoller Unix-Plattform-Skip; E2E-Lauf
(`Category=E2E`, Playwright + echtes ffmpeg): 252 von 253 bestanden, 0 Fehlschläge, 1
absichtsvoller `[Fact(Skip)]` (unbeteiligter Continue-Watching-Bestanddefekt). Kein
On-Air-bezogener Test existiert; Vorbild-Tests für alle nötigen Neuschreibungen sind
vorhanden (siehe [inventory/tests.md](inventory/tests.md)).

## Details

- [Datenmodell](inventory/models.md) — `Setup` (alle Spalten, Zieltabelle der On-Air-Spalte), `TvSenderUserAccess`, `TVChannel`, Stream-Records (`LiveStreamJob`, `LiveStreamInput`, `LiveStreamExitInfo`, `LiveStreamSyncResult`), `GeneralSettingsUpdate`, Options-/Standby-Records.
- [Logik](inventory/logic.md) — `LiveStreamWorker`, `LiveStreamCoordinator`, `FfmpegLiveStreamSegmenter`, `LiveStreamSegmentStore`, `LiveStreamStandbyProvider`, `ProgramScheduleSignal`, `ProgramSettingsService`, `LiveStreamController`, unbeteiligte Nachbarn (`ProgramGuideController`, `InternalMediaStreamController`, `ProgramScheduleWorker`, `MediaDurationProbeWorker`, `LiveStreamMediaResolver`, `LiveStreamStandbySegmenter`), UI (`LiveTv.razor`, `LiveTvErrorMessages`, `liveTv.js`, `TVSenderAdmin.razor`, `ProgramSettings.razor`, `NavMenu.razor`), DI-Registrierungen.
- [Enums](inventory/enums.md) — `LiveStreamSyncAction` (kein On-Air-Wert vorhanden) + Kontext-Enums.
- [Interfaces](inventory/interfaces.md) — `ILiveStreamSegmenter`, `IProgramScheduleSignal`, `IProgramGuideService`, `IProgramGuideApiClient`, `IInternalMediaStreamTokenService`, `IBackgroundProcessingGate`.
- [Tests](inventory/tests.md) — Ausgangslauf-Nachweis (TRX + Konsolenlogs unter `inventory/test-results/`), Testklassen je Bereich, Hilfsmethoden (`ProgramScheduleTestDb`, `LegacyBackupArchiveBuilder`, `TVSenderAdminE2ETestBase`).
