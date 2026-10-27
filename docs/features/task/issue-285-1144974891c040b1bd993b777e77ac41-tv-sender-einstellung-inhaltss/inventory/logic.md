# Logik — Bestandsaufnahme

## `ProgramScheduleService`

Datei: `VideoWebPlayer/Services/ProgramScheduleService.cs` (2681 Zeilen),
implementiert `IProgramScheduleService`.

### Öffentliche Einstiegspunkte

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `EnsureCoverageAsync(utcNow, ct)` (Z. 68) | public | Extend-only-Ausbau des Sendeplans bis `now + horizon`; lädt den Zugriffskontext über `LoadAccessContextAsync` (Z. 89) |
| `ReconcileAsync(utcNow, ct)` (Z. 150) | public | Neubewertung bestehender Slots gegen die aktuelle Expansion; lädt denselben Kontext (Z. 167) — nicht reproduzierbare Slots werden `Displaced` |
| `GetScheduleAsync(channelId, fromUtc, maxCount, ct)` (Z. 391) | public | Lesezugriff Sendeplan-Anzeige |
| `GetRuleDiagnosesAsync(userId, channelId, ct)` (Z. 400) | public | Regel-Diagnose (Admin-only via `RequireAdminAsync`); expandiert jede Regel mit demselben `AccessContext` (Z. 412) |
| `DeleteDisplacedSlotsAsync(userId, channelId, ct)` (Z. 436) | public | Admin-only; löscht verdrängte Slots |
| `ResetScheduleAsync(userId, channelId, ct)` (Z. 447) | public | Admin-only; löscht Plan + Cursors |
| `RequireAdminAsync(userId, ct)` (Z. 464) | private | Admin-Prüfung über `ApplicationUser.IsAdmin`, wirft `UnauthorizedAccessException`/`ForbiddenAccessException` |

### Zentraler Freigabe-Mechanismus

| Element | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `AccessContext` (Z. 1284) | private class | Schnappschuss der Zuschauer-Freigabe: `Everything` (bool), `FullySharedSourceIds`, `UnlockedShowIdsForAll`, `UnlockedCollectionIdsForAll` (je `HashSet<long>`) |
| `AccessContext.Allows(sourceId, showId, collectionId)` (Z. 1291) | public (im Typ) | `true` wenn `Everything`, Quelle für alle Nicht-Admins geteilt oder Show/Collection für alle einzeln freigeschaltet |
| `LoadAccessContextAsync(ct)` (Z. 1298) | private | Liest Nicht-Admin-Benutzer; **bei 0 Nicht-Admins `Everything = true`** (Z. 1301–1310) — exakt der Mechanismus, den der „Alle Inhalte"-Modus der Anforderung wiederverwenden kann; sonst `MediaSourceUsers`-/`UnlockedMediaEntries`-Schnitt über alle Benutzer |

Der `AccessContext` wird an genau vier Stellen ausgewertet:

| Prüfstelle | Zeile | Pfad |
|------------|-------|------|
| `TryMovieItem` | Z. 1877 | Filme (Materialarten `Movie`, `MovieCollection`, `Genre`, `Playlist`) — `Allows(movie.MediaSourceId, null, movie.MovieCollectionId)` |
| `ToItem` | Z. 1921 | Episoden (`TVShow`, `Genre`, `Playlist`) — `Allows(episode.MediaSourceId, showId, null)` |
| `ExpandTrailersAsync` | Z. 2132 | Trailer-Material und System-Clip-Gruppe „Trailer" — `Allows(link.MediaSourceId, null, link.MovieCollectionId)` |
| `FilterClipCandidatesAsync` | Z. 2438 | Füller „Clip-Quelle" und Benutzer-Clip-Gruppen — `Allows(item.MediaSourceId, null, null)` |

Jeder Ausschluss erhöht `SummaryCounters.SkippedNoAccess` (Z. 1264) — propagiert in
`ProgramScheduleRunSummary`/`ProgramScheduleReconcileSummary`/`ProgramRuleDiagnosis.NoAccessCount`.

### Weitere relevante Methoden

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `ExpandMaterialAsync` (Z. 1788/1791) | private | Dispatcher über `ProgramRuleMaterialType` → `ExpandShowAsync`, `ExpandMovieAsync`, `ExpandMovieCollectionAsync`, `ExpandGenreAsync`, `ExpandPlaylistAsync`, `ExpandTrailersAsync`, `ExpandMediaSourceClipsAsync` — alle mit `AccessContext`-Parameter |
| `ExpandShowAsync` (Z. 1885), `ExpandMovieAsync` (Z. 1929), `ExpandMovieCollectionAsync` (Z. 1943) | private | Material-Expansionen, rufen `ToItem`/`TryMovieItem` |
| `ExpandGenreAsync` (Z. 1974) | private | Genre-Vereinigung über kanonische Namen + Quellfilter, ruft `TryMovieItem`/`ToItem` |
| `ExpandPlaylistAsync` (Z. 2230) | private | Playlist-Einträge (Filme/Episoden), ruft `TryMovieItem`/`ToItem` |
| `ExpandTrailersAsync` (Z. 2099) | private | „Trailer anstehender Filme": `MovieMediaItems.IsTrailer`-Links, deren Film im Planungsfenster ausgestrahlt wird; Zugriffsprüfung Z. 2132 |
| `ExpandMediaSourceClipsAsync` (Z. 2174) | private | Füller „Clip-Quelle": unklassifizierte Videodateien einer Quelle → `FilterClipCandidatesAsync` |
| `ExpandClipGroupItemsAsync` (Z. 2386) | private | Mitglieder einer Benutzer-Clip-Gruppe → `FilterClipCandidatesAsync` |
| `FilterClipCandidatesAsync` (Z. 2407) | private | Geteilte Clip-Filterpipeline (Video-Dateiendung, nicht klassifiziert, Laufzeit, `Allows` Z. 2438) |
| `ExpandTrailerStatesAsync` (Z. 2076) / `ExpandClipGroupStatesAsync` (Z. 2369) / `BuildClipGroupStates` (Z. 2342) | private | Verzögerte/sofortige Trailer-Expansion je Laufart; Trailer-Gruppe = `ClipGroupState.IsTrailer` (`ProgramClipGroup.TrailerGroupId = 1`) |
| `SummaryCounters` (Z. 1259) | private class | Laufzähler inkl. `SkippedNoAccess`; `ToSummary()` → `ProgramScheduleRunSummary` |
| `ClipFillContext` (Z. 2310) | private record | Bündelt u. a. `Access` (Feld `AccessContext Access`, Z. 2311) für die Clip-Pfade |

Events: keine publiziert/abonniert — der Dienst wird vom `ProgramScheduleWorker` aufgerufen.

## `ProgramSettingsService`

Datei: `VideoWebPlayer/Services/ProgramSettingsService.cs` (522 Zeilen)

Persistenzdienst für `Setups`-Werte; `GetOrCreateSetupAsync` (Z. 32) legt die Ein-Zeilen-Tabelle
bei Bedarf an (Single-Row-Race per Unique-Index abgefangen, `CreateSetupAsync` Z. 80).

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `GetOrCreateSetupAsync` | public | Setup-Zeile lesen/erstellen inkl. Normalisierung ungültiger Werte |
| `GetScanIntervalsAsync` / `UpdateScanIntervalsAsync` | public | Scan-Intervalle |
| `GetApplicationTitleAsync` | public | Anwendungstitel |
| `GetContinueWatchingEndThresholdAsync` | public | Weiterschauen-Schwelle |
| `UpdateGeneralSettingsAsync(GeneralSettingsUpdate)` | public | Atomares Speichern der Allgemein-Seite (inkl. `TvSenderEnabled`); wirft `DiscoveryUrlValidationException` |
| `GetTvSenderEnabledAsync` / `UpdateTvSenderEnabledAsync` | public | Feature-Flag TV-Sender |
| `IsTvSenderAccessGrantedAsync(userId, isAdmin, ct)` | public | Sender-Sichtbarkeit: `TvSenderEnabled` UND (Admin ODER `TvSenderUserAccess`-Eintrag) — der Pfad der Stream-/Seiten-Freigabe, von der Planung unabhängig |
| `GetTvSenderAllowedUserIdsAsync` / `UpdateTvSenderAllowedUserIdsAsync` | public | Benutzer-Allowlist des Senders |
| `GetDiscoveryPublicBaseUrlAsync` | public | Discovery-Basis-URL |
| `GetMdnsAdvertisementEnabledAsync` / `UpdateMdnsAdvertisementEnabledAsync` | public | mDNS-Schalter |
| `GetHolidayFederalStateAsync` / `UpdateHolidayFederalStateAsync` | public | Bundesland; Updater validiert gegen `HolidayFederalStates` (static, Z. 305) — Muster für den neuen Getter/Updater |
| `GetProgramScheduleHorizonAsync` / `GetProgramScheduleIntervalAsync` / `UpdateProgramScheduleSettingsAsync` | public | Horizont + Generierungsintervall mit Validierung (`ArgumentException` < 1) |
| `GetLiveStreamSegmentLengthAsync` / `GetLiveStreamSegmentDirectoryAsync` / `GetLiveStreamSegmentRetentionAsync` / `UpdateLiveStreamSettingsAsync` | public | Segmentierung (Validierung 1–300 s, Pfad-Probe) |
| `GetLiveStreamStandbyMediaItemIdAsync` / `UpdateLiveStreamStandbyClipAsync` | public | Standby-Clip (Validierung: existiert, Video, unklassifiziert via `MediaItemClipEligibility`) |

`GeneralSettingsUpdate` (Record, gleiche Datei ab Z. 515): Parameterobjekt der
Allgemein-Seite — enthält `TvSenderEnabled`; eine neue Sender-Einstellung gehört **nicht**
zwangsläufig hierhin (die Sender-Einstellungen der TVSenderAdmin-Seite nutzen eigene
Updater wie `UpdateProgramScheduleSettingsAsync`).

Keine Methode für ein Inhaltsspektrum vorhanden.

## `ProgramScheduleWorker`

Datei: `VideoWebPlayer/Services/ProgramScheduleWorker.cs`

Hintergrunddienst: ruft je Intervall `ReconcileAsync` + `EnsureCoverageAsync` (Z. 71/72),
nur wenn `GetTvSenderEnabledAsync` wahr; wacht über `IProgramScheduleSignal.WaitAsync`
zusätzlich auf (Z. 95). Liest das Intervall je Iteration neu — eine neue `Setup`-Einstellung
würde ebenfalls je Lauf gelesen, weil `LoadAccessContextAsync` pro Planungslauf aufgerufen wird.

## `ProgramScheduleSignal` / `IProgramScheduleSignal`

Datei: `VideoWebPlayer/Services/ProgramScheduleSignal.cs`

- `NotifyScheduleChanged()` — weckt den Worker sofort (Konvention für planrelevante
  Änderungen; wird in `TVSenderAdmin` nach „Jetzt generieren"/Plan-Reset und in
  `ProgramSettings.razor` Z. 234 nach dem Speichern gefeuert).
- `BeginScan()` — unterdrückt Wake-ups während Scans.
- `WaitAsync(ct)` — Worker-Wartepunkt.

## `ProgramRuleService` / `IProgramRuleService`

Datei: `VideoWebPlayer/Services/ProgramRuleService.cs`

Kanal- und Regel-CRUD; eigene `RequireAdminAsync` (Z. 243, wirft
`ForbiddenAccessException „Nur Administratoren dürfen den TV-Sender verwalten."`);
`RenameChannelAsync(userId, name)` wird von `SaveChannelSettingsAsync` genutzt.

## `ProgramScheduleOptions`

Datei: `VideoWebPlayer/Configuration/ProgramScheduleOptions.cs`

Betreiber-Konfiguration (appsettings-Sektion `ProgramSchedule`): `TrailerLeadDays`,
`MinStartRemainSeconds`, `MaxOccurrenceLookbackDays` — kein Admin-Oberflächen-Pfad;
die Anforderung verlangt bewusst eine persistierte Admin-Einstellung (nicht hier).

## `VideoWebPlayerBackupData`

Datei: `VideoWebPlayer/Services/Backups/VideoWebPlayerBackupData.cs`

| Element | Zeile | Beschreibung |
|---------|-------|--------------|
| `OptionalRestoreTables` | Z. 23–49 | Alt-Backup-tolerierte Tabellen (u. a. alle Sender-Tabellen) |
| `OptionalRestoreColumns` | Z. 51–110 | Alt-Backup-tolerierte Spalten; alle bisherigen `Setups.*`-Sender-Spalten stehen hier (`TvSenderEnabled` Z. 99, `HolidayFederalState` Z. 90, `ProgramScheduleHorizonHours` Z. 91, `LiveStreamStandbyMediaItemId` Z. 109, …) |
| `OptionalRestoreBoolDefaults` | Z. 120–136 | Restore-Defaults für fehlende Bool-Spalten — Muster `(Setups, TvSenderEnabled, false)` Z. 134 |
| `OptionalRestoreIntDefaults` | Z. 143–155 | Restore-Defaults für fehlende Int-Spalten (z. B. Horizont 48) |
| `OptionalRestoreLongDefaults` / `OptionalRestoreTimeSpanDefaults` | Z. 138–160 | weitere typisierte Defaults |

Eine neue `Setups`-Spalte muss in `OptionalRestoreColumns` und — falls nicht nullable —
in der passenden Defaults-Tabelle eingetragen werden, damit Alt-Backups restriktiv
restauriert werden.
