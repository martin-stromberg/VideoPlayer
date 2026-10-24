# Logik – Bestandsaufnahme

## `ProgramRuleService`
Datei: `VideoWebPlayer/Services/ProgramRuleService.cs` (scoped, `IProgramRuleService`)

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `GetAllowedMaterialTypes` | public | Whitelist der Materialarten je Regelart |
| `GetOrCreateChannelAsync` | public | Liefert den einzigen Sender, legt ihn („TV-Sender") bei Bedarf an |
| `RenameChannelAsync` / `GetRulesAsync` / `CreateRuleAsync` / `UpdateRuleAsync` / `SetRuleActiveAsync` / `DeleteRuleAsync` | public | Sender-/Regel-Operationen, alle mit `RequireAdminAsync` (DB-Check `IsAdmin` am Datensatz) |
| `ValidateRuleInputAsync` | private | Serverseitige Validierung inkl. Material-Whitelist und Material-Existenz je Tabelle |

## `ProgramSettingsService`
Datei: `VideoWebPlayer/Services/ProgramSettingsService.cs` (scoped)

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `GetOrCreateSetupAsync` | public | Einzel-`Setup`-Zeile lesen/anlegen; Defaults bei defekten Werten reparieren |
| `GetHolidayFederalStateAsync` / `UpdateHolidayFederalStateAsync` | public | Bundesland für Feiertagskalender (`HolidayFederalStates`: alle 16 `DE-*`-Codes, Whitelist-Validierung) |
| `GetScanIntervalsAsync` / `UpdateScanIntervalsAsync` u. a. | public | Muster für persistierte Einstellungen (Min-Clamp, Whitelist) |

## `MediaDurationProbeWorker`
Datei: `VideoWebPlayer/Services/MediaDurationProbeWorker.cs` (`BackgroundService`)

Einfaches Worker-Muster: `IServiceScopeFactory` → scoped Service + optionaler `IBackgroundProcessingGate`-Lease (`EnterOperationAsync`); `IOptions<MediaProbeOptions>` für Intervall/Batch; `TimeProvider` injizierbar; Start-Verzögerung `initialDelay`; Fehler je Durchlauf geloggt, ohne den Worker zu beenden.

## `PlaylistBackfillWorker` / `PlaylistBackfillCoordinator` / `PlaylistBackfillSignal`
Dateien: `VideoWebPlayer/Services/{PlaylistBackfillWorker,PlaylistBackfillCoordinator,PlaylistBackfillSignal}.cs`

Signal-getriebenes Muster: Worker schläft auf `_signal.WaitAsync` ODER Timer (Sicherheitslauf-Fälligkeit); eigentliche Arbeit in `PlaylistBackfillCoordinator` (Singleton, Scope-Factory, Gate-Lease `EnterOperationAsync("PlaylistBackfill", ct)` je Arbeitseinheit, Block-Pausen). Persistenter Laufzeitpunkt über `Setup.PlaylistBackfillLastSweepAt` + `ProgramSettingsService`.

## `PlaylistEntryAccessResolver` / `UnlockedMediaService`
Dateien: `VideoWebPlayer/Services/{PlaylistEntryAccessResolver,UnlockedMediaService}.cs`

| Methode | Sichtbarkeit | Kurzbeschreibung |
|---------|-------------|------------------|
| `GetMediaSourceIdsForUserAsync` | public | Quellen mit regulärem Zugriff für einen Benutzer |
| `GetUnlockedMovieCollectionIdsForUserAsync` / `GetUnlockedTVShowIdsForUserAsync` | public | Freischaltungen je Benutzer |
| `IsAccessible` | public | Kanonische Regel: Quellenzugriff ODER Einzelfreischaltung |

## `MediaHierarchyRegistry`
Datei: `VideoWebPlayer/Services/MediaHierarchyRegistry.cs` (internal static)

Bulk-Loader je `MediaType` (Titel, Existenz, Kaskaden-Kinder, Release-Daten, Hierarchie-Sequenz). Relevant: Staffel-Reihenfolge = `TVShowSeason.Id`-Reihenfolge; Episoden-Sequenz = `TVShowEpisode.Number`; `LoadCascadeChildrenAsync` für `TVShow`/`TVShowSeason`/`MovieCollection`.

## `TVSenderAdmin.razor`
Datei: `VideoWebPlayer/Components/Pages/Admin/TVSenderAdmin.razor`

Admin-Seite `/admin/tv-sender`: Sender-Name, Bundesland-Auswahl (`tvsender-federal-state`), Speichern (`tvsender-save-settings` → `SaveChannelSettingsAsync`), Regel-CRUD mit Auswahldialogen (`MediaSearchSelector` mit `AllowedMediaTypes`, Genre-/Playlist-Dropdowns). Admin-Gate über Claim `IsAdmin`.

## `ServiceCollectionExtensions`
Datei: `VideoWebPlayer/Extensions/ServiceCollectionExtensions.cs`

DI-Registrierungen: `ProgramSettingsService` scoped, `IProgramRuleService` scoped, `IBackgroundProcessingGate` Singleton, `TimeProvider.System` Singleton, `AddHostedService<MediaDurationProbeWorker>`/`PlaylistBackfillWorker` u. a.

## `VideoWebPlayerBackupData`
Datei: `VideoWebPlayer/Services/Backups/VideoWebPlayerBackupData.cs`

`OptionalRestoreTables` (u. a. `TVChannels`, `ProgramRules`), `OptionalRestoreColumns` (u. a. `Setups.HolidayFederalState`, `MediaItems.Duration*`), `OptionalRestoreIntDefaults`/`OptionalRestoreBoolDefaults`/`OptionalRestoreLongDefaults` für Spalten mit Default.
