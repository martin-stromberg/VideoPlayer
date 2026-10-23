# Umsetzungsplan — Schritt 27 (N26–N28)

Drei getrennte Commits: `fix:` N26, `feat:`/`fix:` N27, `feat:` N28.

## N26 — Fortschrittsbalken-Kultur (Commit 1)

1. `LiveTv.razor`: `ProgressPercent` formatiert invariant — die Methode liefert den
   CSS-tauglichen String (`ToString(CultureInfo.InvariantCulture)`), Markup
   `style="width:@ProgressPercent(...)%"` bleibt lesbar. `@using System.Globalization`.
2. `VideoPlayer.razor`: `ProgressPercent`-Property analog (gleicher latenter Bug).
3. Test `LiveTvTests`: bUnit-Render unter `CultureInfo.CurrentCulture = de-DE`,
   `PositionSeconds`/`DurationSeconds` mit gebrochenem Prozent (z. B. 12,5 %) →
   `style` enthält `width:12.5%` mit Punkt, kein Komma.

## N27 — Tagesgliederung + Regelwechsel (Commit 2)

1. `TVSenderAdmin.razor` Sendeplan-Tab: `schedule` wird nach
   `slot.Start.ToLocalTime().Date` gruppiert; je Tag ein Header
   (`.tvsender-day-header`, `data-day="yyyy-MM-dd"`, Text „dddd, dd.MM.yyyy").
   Innerhalb eines Tages: wechselt `ProgramRuleId` gegenüber dem vorherigen Slot,
   wird ein `.tvsender-rule-divider` (`data-rule-id`, Label „Nächster Block: Regel X")
   vor der Karte eingefügt — nicht am Tagesanfang (Header trennt bereits).
2. `app.css`: Styles für `.tvsender-day-header` und `.tvsender-rule-divider` im
   TV-Sender-Abschnitt (Admin-Tokens).
3. bUnit-Test in `TVSenderAdminTests`: zwei Tage, Regelwechsel → Header je Tag,
   Divider nur beim Wechsel, kein Divider am Tagesanfang.

## N28 — Benutzer-Freigabe (Commit 3)

1. **Entity:** `Data/TvSenderUserAccess.cs` (`UserId` string PK, `User`-Navigation),
   `Data/Configurations/TvSenderUserAccessConfiguration.cs` (`HasKey(UserId)`),
   DbSet `TvSenderUserAccess` in `ApplicationDbContext` (Achtung: Datei enthält ein
   cp1252-Byte — editieren auf Byte-Ebene oder defekten Kommentar-Umlaut zu UTF-8
   reparieren).
2. **Migration:** `AddTvSenderUserAccess` (CreateTable, PK UserId, FK Cascade →
   AspNetUsers) + Designer + `ApplicationDbContextModelSnapshot`. `dotnet ef`
   versuchen; bei Build-Lock manuell nach Muster `UpdateMediaModels20`.
3. **Service (`ProgramSettingsService`):**
   - `IsTvSenderAccessGrantedAsync(string? userId, bool isAdmin, CancellationToken)`:
     `false` wenn Flag aus; `true` wenn `isAdmin`; sonst `AnyAsync` auf Allowlist.
   - `GetTvSenderAllowedUserIdsAsync` + `UpdateTvSenderAllowedUserIdsAsync`
     (Replace-Semantik: fehlende Zeilen löschen, neue einfügen — ein SaveChanges).
4. **Gating:**
   - `NavMenu.razor`: `tvSenderEnabled` → `IsTvSenderAccessGrantedAsync(userId,
     isAdmin)` aus Claims (NameIdentifier + IsAdmin-Claim).
   - `LiveTv.razor`: zwei Stufen — Flag aus → `#live-tv-disabled`; Flag an, kein
     Zugriff → `#live-tv-no-access` mit `LiveTvErrorMessages.Forbidden`.
   - `ProgramGuideController`: Flag aus → 404; nicht freigegeben → `Forbid()` (403).
   - `LiveStreamController`: analog (403 für nicht freigegebene Benutzer).
   - `TVSenderAdmin.razor`: unverändert (admin-only).
5. **Admin-UI:** `ProgramSettings.razor` TV-Sender-Karte: Checkbox-Liste aller
   Nicht-System-Benutzer (UserManager.Users, `NormalizedUserName != "SYSTEM"`),
   Hinweis „Administratoren haben immer Zugriff". SaveAsync schreibt Flag +
   Allowlist.
6. **UserManagement.razor:** `DeleteUser` räumt `TvSenderUserAccess`-Zeilen mit ab
   (Muster MediaSourceUsers).
7. **Backup:** `OptionalRestoreTables` += `TvSenderUserAccess` (UserId-Mapping beim
   Restore greift automatisch).
8. **Tests:**
   - `ProgramSettingsServiceTests` (oder neue Datei): Flag-aus → false; Admin → true;
     gelistet → true; nicht gelistet → false; Update-Roundtrip (echtes SQLite,
     User via `PairingTestDb`/ProgramScheduleTestDb wegen FK).
   - `ProgramGuideControllerTests`: nicht freigegeben → `ForbidResult`; freigegeben →
     OK; Admin ohne Zeile → OK.
   - `LiveStreamControllerTests`: nicht freigegeben → `ForbidResult`.
   - `NavMenuLiveTvTests`: verborgen ohne Freigabe; sichtbar mit Freigabe/als Admin.
   - `LiveTvTests`: kein Zugriff → `#live-tv-no-access`-Hinweis, kein Guide-Aufruf.
   - `VideoWebPlayerBackupDataTests_TvSenderUserAccess` (neu): Alt-Backup ohne
     Tabelle restauriert; Rundreise erhält die Zeilen.

## Offene Punkte

Keine.
