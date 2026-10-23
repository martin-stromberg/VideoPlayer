# Bestandsaufnahme — Schritt 27 (N26–N28)

## N26: Fortschrittsbalken-Kultur

`style="width:…%"`-Stellen mit Gleitkommawerten (nur `double`/`float` ist betroffen,
`int` formatiert nie mit Dezimaltrennzeichen):

| Datei | Zeile | Typ | Befund |
|-------|-------|-----|--------|
| `VideoWebPlayer/Components/Pages/LiveTv.razor` | 108 | `double` via `ProgressPercent(position, duration)` (Z. 356) | BUG — Komma unter de-DE |
| `VideoWebPlayer/Components/Shared/Media/VideoPlayer.razor` | 37 | `double` via `ProgressPercent`-Property (Z. 102) | gleicher latenter BUG |
| `VideoWebPlayer/Components/Pages/Admin/TVSenderAdmin.razor` | 375 | `int` via `ProgressPercent(slot)` (Z. 1026) | nicht betroffen |
| `VideoWebPlayer/Components/Pages/Admin/TVSenderAdmin.razor` | 502 | `int` via `ProbePercent` (Z. 813) | nicht betroffen |

## N27: Sendeplan-Gliederung

- `TVSenderAdmin.razor` Z. 344–380: `program-grid` iteriert `schedule` (List<ProgramSlot>,
  `ProgramScheduleService.GetScheduleAsync` — nach `Start` sortiert) flach.
- `ProgramSlot.Start`/`End` = UTC-`DateTime`; Anzeige erfolgt via `ToLocalTime()` →
  Gruppierung konsistent nach lokalem Datum.
- `slot.ProgramRuleId` (`long?`, Soft-Reference) triggert den Regelwechsel-Divider;
  `RuleName(slot)` löst den Anzeigenamen auf (Fallback „(Regel gelöscht)").
- Styles: `VideoWebPlayer/wwwroot/app.css` — `.program-grid`, `.program-card`,
  Admin-Overrides `.admin-console .program-*`; neue Klassen für Tages-Header +
  Regel-Divider kommen dort in den TV-Sender-Abschnitt.

## N28: User-Freigabe

- Global-Flag: `Setup.TvSenderEnabled` (`Data/Setup.cs` Z. 80), gelesen/geschrieben von
  `ProgramSettingsService.GetTvSenderEnabledAsync`/`UpdateTvSenderEnabledAsync` sowie
  `UpdateGeneralSettingsAsync` (Batch aus `GeneralSettingsUpdate`).
- Gating heute:
  - `Components/Layout/NavMenu.razor` Z. 41–48 + Z. 134 (`tvSenderEnabled`, catch
    ObjectDisposed/OperationCanceled).
  - `Components/Pages/LiveTv.razor` Z. 33–38 (`#live-tv-disabled`) + Z. 196.
  - `Components/Pages/Admin/TVSenderAdmin.razor` Z. 26–34 (`#tvsender-disabled`) +
    Z. 720 — Seite ist ohnehin admin-only (`IsAdmin`-Claim).
  - `Controllers/ProgramGuideController.cs` `FeatureReleasedAsync` → 404.
  - `Controllers/LiveStreamController.cs` Z. 56 → 404.
  - Worker (`LiveStreamWorker`, `ProgramScheduleWorker`, `MediaDurationProbeWorker`)
    bleiben global — kein User-Bezug nötig.
- `ApiBaseController.CurrentUser` liefert `ApplicationUser` (mit `IsAdmin`-DB-Feld) —
  für Controller authoritative. Blazor-Komponenten nutzen ClaimsPrincipal-Claims
  (`ClaimTypes.NameIdentifier`, `IsAdmin`).
- Benutzerverwaltung: `Components/Pages/Admin/UserManagement.razor` (UserManager,
  `NormalizedUserName != "SYSTEM"`, explizites Löschen userbezogener Zeilen Z. 237–246
  — `TvSenderUserAccess` muss dort ergänzt werden, obwohl FK-Cascade existiert).
- Freigabe-UI: `Components/Pages/Admin/ProgramSettings.razor` Karte „TV-Sender"
  (Z. 90–97) — hier sitzt der globale Schalter; die Benutzer-Auswahl gehört
  dorthin (auch vor Aktivierung erreichbar).
- Muster für Allowlist-Tabelle: `Data/MediaSourceUser` + `MediaSourceUserConfiguration`
  (PK/FK Cascade auf `AspNetUsers`).
- Backup: `Services/Backups/VideoWebPlayerBackupData.cs` — neue Tabelle muss in
  `OptionalRestoreTables`; `UserId`-Spalten werden beim Restore automatisch über
  `ApplyRestoreUserIdMap`/`DeleteConflictingUsersAsync` behandelt.
- Migrations-Pattern: `Migrations/20250817090750_UpdateMediaModels20.cs` (CreateTable
  mit FK Cascade nach AspNetUsers). `dotnet ef` ist installiert (10.0.10); bei
  gesperrtem Debug-bin (laufende App) Migration ggf. manuell wie in Schritt 23.

## Test-Infrastruktur

- `VideoWebPlayer.Tests/Helpers/ProgramScheduleTestDb.cs`: shared-memory SQLite mit
  FKs, seedet `Setup { TvSenderEnabled = true }`, Helpers `AddUserAsync`,
  `CreateSlotAsync`, `CreateRuleAsync`, `GetChannelAsync`.
- `PairingTestDb`: echtes `UserManager<ApplicationUser>`-DI (AddIdentityCore) —
  für User-Verwaltungstests.
- bUnit: `Components/TVSenderAdminTests.cs`, `LiveTvTests.cs`, `NavMenuLiveTvTests.cs`
  (InMemory-DB ausreichend für NavMenu/LiveTv).
- Controller: `Controllers/ProgramGuideControllerTests.cs`,
  `LiveStreamControllerTests.cs` mit `Helpers/FakeAuthService`.
- Backup: `Services/Backups/LegacyBackupArchiveBuilder` (`RemoveTablesAsync`,
  `RemoveColumnsAsync`), Vorbild `VideoWebPlayerBackupDataTests_ProgramRuleLinks.cs`.
