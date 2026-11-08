# Plan-Gegenprüfung

Geprüft: `plan.md` gegen `requirement.md`, `inventory.md` und die Detaildokumente
(`inventory/models.md`, `inventory/logic.md`, `inventory/enums.md`, `inventory/interfaces.md`,
`inventory/tests.md`). Basisbranch: `staging` (HEAD `1be452b`, identisch mit Task-Branch).

## Ergebnis

**Status:** Plan vollständig

## Abgleich Akzeptanzkriterien

Die Anforderung hat keinen expliziten Akzeptanzkriterien-Abschnitt; die Kriterien wurden aus
„Fachliche Zusammenfassung“, „Fachlicher Scope“ und „Betroffene Klassen“ abgeleitet.

| Akzeptanzkriterium | Umsetzung im Plan | Testnachweis im Plan | Status |
|--------------------|-------------------|----------------------|--------|
| Zweiter Admin-Schalter „On Air" (`Setup.TvSenderOnAir`), unabhängig von `Setup.TvSenderEnabled`, steuert nur den HLS-Live-Stream | Umsetzungsschritt 1 (Entity + Migration `AddSetupTvSenderOnAir`, Default `true`), Schritt 3 (`Get/UpdateTvSenderOnAirAsync`); Verarbeitungseinheit und Ausschlüsse 1–6 in Scope-Klärung | `GetTvSenderOnAirAsync_DefaultsToTrue`, `UpdateTvSenderOnAirAsync_Roundtrip` (SQLite) | Abgedeckt |
| Off-Air: keine ffmpeg-Segmentierung, laufender Lauf wird gestoppt | Schritt 4: Gate-Erweiterung in `LiveStreamWorker.ExecuteAsync` (vorhandene Stopp-Verzweigung `IsRunning` → `StopAsync`) | `ExecuteAsync_OffAir_StopsSegmenterAndNeverStarts` (FakeSegmenter: `StopCalls >= 1`, nie gestartet) | Abgedeckt |
| Off-Air: `/api/live-stream/*` liefert keinen Stream (auch kein Standby) | Schritt 5: On-Air-Gate in `LiveStreamController.GetFile` nach Benutzerfreigabe, vor `TryResolveFileAsync`/`ServeStandbyAsync` → 404; Ausschluss 8 | `GetFile_OffAir_Returns404` (Theory: Playlist, Standby-Segment, echtes Segment); E2E `LiveStreamE2ETests` | Abgedeckt |
| Sendeplan-Generierung, Programmauskunft, Laufzeitermittlung, Verwaltungsseiten laufen Off-Air unverändert weiter | Ausschluss 3 (kein Eingriff in `ProgramScheduleWorker`, `ProgramGuideController`, Programmdaten); Scope-Klärung führt `MediaDurationProbeWorker`, `TvSenderUserAccess`, `InternalMediaStreamController` explizit als nicht zur Einheit gehörend | Bestehende Tests bleiben unverändert gültig (Default `true`); optionaler E2E-Gegenprobe-Test `/api/program-guide/now` → 200 Off-Air | Abgedeckt |
| Funktionsfreischaltung bleibt äußere Hürde (On-Air kann nicht teilweise freischalten) | Ausschluss 2; Gate-Reihenfolge `TvSenderEnabled` → Freigabe → On-Air; Akzeptanzszenario 3 | Bestehender Test `GetFile_FeatureDisabled_Returns404` + Admin-UI-Test (Top-Gate) | Abgedeckt |
| Admin stellt Schalter in der Verwaltung um; serverseitig persistiert, wirkt ohne Neustart, vorzeitig geweckt über `IProgramScheduleSignal` | Schritt 7: Sofort-Umschalter `#tvsender-on-air` in `TVSenderAdmin.razor` (Sub-Karte „Sendebetrieb", Tab „Einstellungen") → `UpdateTvSenderOnAirAsync` → `NotifyScheduleChangedNow()`; Signal wirkt über `_liveChannel`/`WaitLiveAsync` | `SettingsTab_OnAirToggle_PersistsAndNotifies`, `SettingsTab_OnAirToggle_SaveFails_RevertsCheckbox`; E2E Happy Path `TvSenderOnAirE2ETests` | Abgedeckt |
| `/live-tv` zeigt verständlichen „nicht auf Sendung"-Zustand | Schritt 6: `tvSenderOnAir`-Zustand (Init + 15-s-Poll), Off-Air-Bild `#live-tv-off-air` im Player-Bereich, `LiveTvErrorMessages.NotOnAir` als alt-Text, `.livetv-off-air`-CSS; Attach-Bedingung in `OnAfterRenderAsync` stoppt hls.js-Re-Attach | `Page_OffAir_ShowsOffAirImage_ProgramGuideStillRenders`, `PollTick_OffAirSwitch_DetachesPlayerAndShowsOffAirImage` (bUnit); E2E `LiveTvE2ETests` (Bild mit `naturalWidth > 0`) | Abgedeckt |
| On-Air: Segmentierung setzt am nächsten Takt an aktueller Programmposition fort (ohne Neustart) | Schritt 4 + Akzeptanzszenario 2; zeitbasierte Resynchronisation des Coordinators bestätigt (inventory/logic.md `LiveStreamCoordinator`) | `ExecuteAsync_OffAirToOnAir_ResumesSegmentation`; E2E „Off-Air → On-Air"-Szenario | Abgedeckt |
| Fehlende Objekte: `Setups`-Zeile existiert (Single-Row), Spalte per EF-Migration mit Default; Alt-Backups via `OptionalRestoreColumns`/`OptionalRestoreBoolDefaults` | Schritt 2 (Backup-Listen, Default `true`), Schritt 1 (Migration `defaultValue: true`); Scope-Klärung „Umgang mit fehlenden Objekten" vollständig gefüllt | `ReadFromAsync_LegacyBackupWithoutTvSenderOnAirColumn_RestoresDefaultTrue` (`LegacyBackupArchiveBuilder.RemoveColumnsAsync`, Seed auf Nicht-Default `false`) | Abgedeckt |
| Berechtigung serverseitig erzwungen: Schalter nur Administratoren | Kein eigener Endpunkt — Schalter liegt auf admin-only Seite (`isAdmin`-Gate), Entscheidung dokumentiert (Designentscheidung „Platzierung", Abschnitt „Validierungsregeln") | Endpunkt-Gate: `GetFile_OffAir_Returns404` + E2E (berechtigter Zuschauer erhält Off-Air 404); Seiten-Gate unverändert durch vorhandene `isAdmin`-Tests (`StandbyCard_NonAdmin_Blocked`-Muster) abgesichert | Abgedeckt |
| Scope-Ausschlüsse der Anforderung (kein Fahrplan, kein Eingriff in Sendeplan/Programmauskunft, `TvSenderUserAccess` unverändert, `InternalMediaStreamController` unberührt, keine Off-Air-Markierung im Sendeplan) | Alle sechs Ausschlüsse der Anforderung sind als Ausschlüsse 1–6 mit Begründung und Status `bestätigt (2026-11-08)` übernommen; zwei weitere entschiedene offene Fragen als Ausschlüsse 7 (kein Leeren des Segment-Verzeichnisses) und 8 (kein Standby-/Hinweis-Stream Off-Air) ebenfalls mit Begründung + Status | Optionaler E2E `/api/program-guide/now` → 200 als Gegenprobe für Ausschluss 3 | Abgedeckt |
| Dokumentation (`docs/help/tv-sender.md`, `tv-sender-ablauf-technisch.md`, `README.md`, `RELEASE_NOTES.md`) | Umsetzungsschritt 9 | — (Doku-Schritt, kein Test erforderlich) | Abgedeckt |

## Fehlende oder unvollständige Testanforderungen

Keine — Status: Plan vollständig.

## E2E-Abdeckung

| Benutzerfluss / Akzeptanzkriterium | Geplanter E2E-Test | Status |
|------------------------------------|--------------------|--------|
| Happy Path: Admin schaltet Off-Air → Worker stoppt ffmpeg → `/api/live-stream/*` → 404 → `/live-tv` zeigt Off-Air-Bild | Neue `TvSenderOnAirE2ETests` auf `TVSenderAdminE2ETestBase` (Admin-Eingriff bis Zuschauer-Sicht, inkl. Bild-Auslieferung `naturalWidth > 0`) | Abgedeckt |
| Wiederaufnahme: Off-Air → On-Air, Stream und Player-Attach ohne Neustart | `TvSenderOnAirE2ETests` | Abgedeckt |
| Stream-Endpunkt blockiert Off-Air auch für berechtigte Zuschauer und den Standby-Pfad (Gate-Reihenfolge) | `LiveStreamE2ETests` (Erweiterung, Vorbild Feature-Gate-E2E) | Abgedeckt |
| Off-Air-Bild auf `/live-tv` bei laufender Sendung, Programmauskunft bleibt sichtbar, Player-Detach | `LiveTvE2ETests` (Erweiterung via `UpdateSetupAsync`) | Abgedeckt |
| Sendeplan/Programmauskunft bleiben Off-Air abfragbar (Gegenprobe Ausschluss 3) | `ProgramGuideE2ETests` oder `TvSenderOnAirE2ETests` (`/api/program-guide/now` → 200) | Abgedeckt (Optional markiert, fachlich sinnvoll) |
| Fehlerfall Umschalten (Persistenz schlägt fehl → Checkbox-Rollback) | bUnit `SettingsTab_OnAirToggle_SaveFails_RevertsCheckbox` — Fehler beim Update ist im echten Kestrel-Lauf nicht zuverlässig auslösbar; Komponententest akzeptabel | Abgedeckt (bUnit, E2E nicht sinnvoll erzwingbar) |
| Off-Air während laufender Wiedergabe (hls.js-Re-Attach-Schleife) | bUnit `PollTick_OffAirSwitch_DetachesPlayerAndShowsOffAirImage` + E2E-Bildsichtbarkeitsnachweis | Abgedeckt |

## Fehlende oder unvollständige Planbestandteile

Keine — Status: Plan vollständig.

Vollständigkeitsfeststellungen im Einzelnen:

- `Scope-Klärung` ist vollständig gefüllt (Verarbeitungseinheit, auslösende Ereignisse,
  erwartete Wirkung, Umgang mit fehlenden Objekten).
- `Akzeptanzszenarien` enthält sechs Szenarien mit Ausgangszuständen: vollständiger Happy
  Path (1), Wiederaufnahme (2), fehlende Voraussetzung Feature-Freischaltung (3), Alt-Backup
  ohne Spalte (4), Off-Air während laufender Wiedergabe (5), Wartesituation Signal-/Scan-
  Unterdrückung mit garantiertem `PollCap`-Wiederaufnahmeweg (6).
- Alle Scope-Ausschlüsse (1–8) tragen Begründung und Status `bestätigt (2026-11-08)`; alle
  sechs „Offene Fragen" der Anforderung sind als Designentscheidung bzw. Ausschluss
  aufgelöst (Default `true`, 404-Antwort, Off-Air-Bild, Platzierung/Sofortwirkung,
  Segment-Aufräumen, „deaktiviert" = Ressourcen-Freigabe).
- Umsetzungsreihenfolge (Schritte 1–9) enthält alle Änderungen mit Voraussetzungen;
  Seiteneffekte/Risiken (Test-Fixtures, hls.js-Re-Attach, Standby-Provider, Segment-Reste,
  Navigation, Notify-Race) sind benannt.
- Betroffene bestehende Tests und Hilfsmittel sind genannt (`LiveTvTests.CreateContext`,
  `TVSenderAdminE2ETestBase.SetOnAirAsync`, `ProgramScheduleTestDb`-Seed-Verträglichkeit über
  Entity-Initializer `= true`, `LegacyBackupArchiveBuilder`, `ControllableTimeProvider`,
  `FakeSegmenter`). Verifiziert: `StaticAssetVersioner` ist in `LiveTv.razor:13` bereits
  injiziert — die Planbehauptung trifft zu.

## Hinweise

- Bei der Umsetzung von Schritt 7 kann ein bUnit-Test nach dem Muster
  `StandbyCard_NonAdmin_Blocked` erwogen werden, der den Nicht-Admin-Fall für die neue
  Sub-Karte explizit benennt — nicht erforderlich, da kein neuer Endpunkt entsteht und das
  `isAdmin`-Gate unverändert bestehen bleibt.
- `NotifyScheduleChangedNow` (ungebatcht) statt `NotifyScheduleChanged` (gebatcht) ist eine
  bewusste, begründete Abweichung vom `TvSenderEnabled`-Muster — bei der Abnahme kurz
  verifizieren, dass das Signal den `_liveChannel` tatsächlich sofort weckt
  (`ProgramScheduleSignal.WakeWorkers`, inventory/interfaces.md).
- Sollte sich beim Migrationsschritt zeigen, dass ein bestehender Backup-Test die
  vollständige Spaltenliste der `Setups`-Tabelle abgleicht (Golden-Schema-Charakter), wäre
  er unter „Betroffene bestehende Tests" nachzutragen — in `VideoWebPlayer.Tests` wurde kein
  solcher Test gefunden.
