# UI — Bestandsaufnahme

## `TVSenderAdmin.razor`

Datei: `VideoWebPlayer/Components/Pages/Admin/TVSenderAdmin.razor` (2050 Zeilen),
Route `@page "/admin/tv-sender"`, `@rendermode InteractiveServer`.

### Zugriff

- Nicht-Admin: „Nicht autorisiert." (Z. 27–30, `isAdmin` aus Claim `IsAdmin` in
  `OnInitializedAsync`, Z. 1022–1025)
- Sender nicht freigeschaltet: Hinweis-Panel `#tvsender-disabled` (Z. 31–39,
  `GetTvSenderEnabledAsync` Z. 1027)

### Tab „Einstellungen" (`#tab-einstellungen`, Z. 486–638)

Eine `admin-card-wide`-Karte „Sender-Einstellungen" mit `admin-settings-grid` und vier
`admin-sub-card`-Gruppen:

| Unterkarte | Zeile | Felder (Ids) |
|------------|-------|--------------|
| „Sender" | Z. 493–509 | `#tvsender-name` (Text), `#tvsender-federal-state` (`select`, Optionen aus `federalStates`) |
| „Planung" | Z. 510–520 | `#tvsender-horizon-hours` (number), `#tvsender-interval-minutes` (number) |
| „Segmentierung" | Z. 521–544 | `#tvsender-segment-length-seconds`, `#tvsender-segment-directory`, `#tvsender-segment-retention-minutes` |
| „Standby-Clip (Überbrückung)" | Z. 545–627 | `#tvsender-standby-current`, `#tvsender-standby-change`, Picker `#tvsender-standby-picker` |

Gemeinsame Speichern-Schaltfläche `#tvsender-save-settings` (Z. 630) →
`SaveChannelSettingsAsync`; Statusmeldung `#tvsender-channel-status` (Z. 632–635).
Die „Inhaltsspektrum"-Auswahl der Anforderung gehört laut Vorgabe in die Unterkarte
„Planung" (Z. 510–520); Muster für ein `select` ist `#tvsender-federal-state` (Z. 500–508).

### Code-Behind (relevante Stellen)

| Element | Zeile | Beschreibung |
|---------|-------|--------------|
| `@inject ProgramSettingsService ProgramSettings` | Z. 16 | Dienst der neuen Einstellung (bereits injiziert) |
| `@inject IProgramScheduleSignal ScheduleSignal` | Z. 13 | Signal für planrelevante Änderungen (bereits injiziert) |
| Felder `federalState`, `horizonHours`, `intervalMinutes`, … | Z. 925–944 | gebundene Formularfelder; `federalStates` aus `ProgramSettingsService.HolidayFederalStates` (Z. 944) |
| `OnInitializedAsync` | Z. 1020–1069 | lädt alle Einstellungen: `federalState` (Z. 1035), `horizonHours` (Z. 1036), `intervalMinutes` (Z. 1037), Segment-/Standby-Werte (Z. 1038–1045) — hier läge das initiale Laden der neuen Einstellung |
| `SaveChannelSettingsAsync` | Z. 1611–1628 | ruft `RenameChannelAsync`, `UpdateHolidayFederalStateAsync`, `UpdateProgramScheduleSettingsAsync`, `UpdateLiveStreamSettingsAsync`, `UpdateLiveStreamStandbyClipAsync`; Status „Einstellungen wurden gespeichert." — hier würde der neue Updater ergänzt |
| `ScheduleSignal.NotifyScheduleChanged()` | Z. 1301, 1350 | heute nur nach „Jetzt generieren" und Plan-Reset — **nicht** nach `SaveChannelSettingsAsync` |

Kein UI-Element für ein Inhaltsspektrum vorhanden.

## `ProgramSettings.razor` (Konventionsreferenz)

Datei: `VideoWebPlayer/Components/Pages/Admin/ProgramSettings.razor`

Seite „Einrichtung → Allgemein" (`/admin/program-settings`), pflegt `TvSenderEnabled` und die
Benutzer-Allowlist. `SaveAsync` (Z. 210–245) speichert über `UpdateGeneralSettingsAsync` +
`UpdateTvSenderAllowedUserIdsAsync` und feuert danach
`ScheduleSignal.NotifyScheduleChanged()` (Z. 234) — Kommentar: planrelevante Änderungen
sollen nicht bis zum nächsten Generierungslauf warten. Dasselbe Muster gilt für die neue
Einstellung in `SaveChannelSettingsAsync`.

## Weitere Zugriffspfade (unverändert)

- `LiveTv.razor` (Z. 212), `NavMenu.razor` (Z. 136), `ProgramGuideController` (Z. 46),
  `LiveStreamController` (Z. 81): Sender-Sichtbarkeit/Stream-Zugriff über
  `IsTvSenderAccessGrantedAsync` — **nicht** an den `AccessContext` der Planung gekoppelt.
