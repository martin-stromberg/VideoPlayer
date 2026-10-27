# Bestandsaufnahme: TV-Sender — Einstellung „Inhaltsspektrum"

Analysiert wurde der Sendeplan-/Einstellungsbereich des automatisierten TV-Senders bezogen
auf die Anforderung, die Zuschauer-Freigabeprüfung der Planung per senderweiter
Admin-Einstellung optional aufzuheben (siehe `requirement.md`).

## Zusammenfassung

- **Der Freigabe-Mechanismus existiert zentral:** `ProgramScheduleService.LoadAccessContextAsync`
  (`VideoWebPlayer/Services/ProgramScheduleService.cs` Z. 1298) baut einen `AccessContext`
  (Z. 1284), dessen `Allows(sourceId, showId, collectionId)` (Z. 1291) an genau vier Stellen
  geprüft wird: `TryMovieItem` (Z. 1877), `ToItem` (Z. 1921), `ExpandTrailersAsync`
  (Z. 2132) und `FilterClipCandidatesAsync` (Z. 2438). Alle drei Einstiegspunkte
  (`EnsureCoverageAsync` Z. 89, `ReconcileAsync` Z. 167, `GetRuleDiagnosesAsync` Z. 412)
  laden den Kontext über dieselbe Methode — eine Umschaltung würde alle Pfade gleichermaßen
  treffen. Der `Everything`-Zweig (aktiv, wenn keine Nicht-Admin-Benutzer existieren,
  Z. 1301–1310) ist bereits heute der Mechanismus „alles erlaubt".
- **Keine Einstellung vorhanden:** `Setup` (`VideoWebPlayer/Data/Setup.cs`) enthält alle
  bisherigen senderweiten Einstellungen (`TvSenderEnabled`, `HolidayFederalState`,
  `ProgramScheduleHorizonHours`, `ProgramScheduleIntervalMinutes`,
  `LiveStreamStandbyMediaItemId` u. a.), aber keine Inhaltsspektrum-Spalte. Auch `TVChannel`
  (kanalfähiges Modell) trägt keine Einstellung. Es gibt weder ein Enum für die zwei
  Optionen noch Getter/Updater im `ProgramSettingsService`.
- **UI-Ankerpunkt vorhanden:** `TVSenderAdmin.razor` Tab „Einstellungen", Unterkarte
  „Planung" (Z. 510–520) mit `#tvsender-horizon-hours`/`#tvsender-interval-minutes`;
  `select`-Muster `#tvsender-federal-state` (Z. 500–508). `SaveChannelSettingsAsync`
  (Z. 1611) bündelt die Updater, `OnInitializedAsync` (Z. 1020) lädt die Werte.
  `ScheduleSignal.NotifyScheduleChanged()` wird in der Seite bislang nur nach
  Generierungsanstoß/Plan-Reset gefeuert (Z. 1301, 1350); die Konvention „nach planrelevantem
  Speichern feuern" zeigt `ProgramSettings.razor` Z. 234.
- **Backup-Konvention vorhanden:** `VideoWebPlayerBackupData.OptionalRestoreColumns`
  (Z. 51–110) enthält alle `Setups`-Sender-Spalten; `OptionalRestoreBoolDefaults`
  (Z. 120–136) mit dem Muster `(Setups, TvSenderEnabled, false)` (Z. 134) sowie
  `OptionalRestoreIntDefaults` (Z. 143–155) für Alt-Backups.
- **Sender-Freigabe (Stream) ist ein anderer Pfad:** `TvSenderUserAccess` +
  `ProgramSettingsService.IsTvSenderAccessGrantedAsync` steuern Navigation, `/live-tv`,
  Program-Guide- und Live-Stream-Endpunkte — unabhängig vom Planungs-`AccessContext`.
- **Admin-Absicherung:** Serverseitig via `RequireAdminAsync` in `ProgramScheduleService`
  (Z. 464) und `ProgramRuleService` (Z. 243); die Seite `/admin/tv-sender` ist selbst
  Admin-only und zusätzlich an `TvSenderEnabled` gekoppelt.

Test-Ausgangszustand: Voller Suite-Lauf `dotnet test VideoWebPlayer.Tests/VideoWebPlayer.Tests.csproj`
(Debug) — **2494 bestanden, 0 fehlgeschlagen, 2 übersprungen** (1 plattformbedingt,
1 bewusst deaktivierter E2E-Befund). Nachweis in [inventory/tests.md](inventory/tests.md).

## Details

- [Datenmodell](inventory/models.md)
- [Logik](inventory/logic.md)
- [Enums](inventory/enums.md)
- [Interfaces](inventory/interfaces.md)
- [UI](inventory/ui.md)
- [Dokumentation](inventory/docs.md)
- [Tests](inventory/tests.md)
