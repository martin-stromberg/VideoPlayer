# Abnahme Schritt 27 — N26+N27+N28

Prüfer: Devin (frischer Lauf, ohne Implementierungskontext)
Stand: HEAD `810b633` auf `task-issue-111-...-schritt-27-ui-freigabe`
Geprüft: N26 (Fortschrittsbalken invariant), N27 (Tagesgliederung + Regelwechsel-Divider),
N28 (TV-Sender pro Benutzer freigeben)

## Ergebnis

**Status:** Erfüllt

Verifikation im Detail:

- **Diff gegen Projekt-Branch geprüft** (34 Dateien, +3428/-25): alle berichteten Änderungen
  vorhanden — `CssFormat.Percent` (InvariantCulture) in `ViewModels/CssFormat.cs:20`,
  verwendet in `LiveTv.razor:114` und `VideoPlayer.razor:39`; Tages-Header +
  `.tvsender-rule-divider` bei `ProgramRuleId`-Wechsel in `TVSenderAdmin.razor:348-364`;
  `TvSenderUserAccess`-Entity + Konfiguration (PK UserId, FK → AspNetUsers Cascade) +
  Migration `20261023055517_AddTvSenderUserAccess` (Up/Down korrekt, Model-Snapshot
  konsistent); `IsTvSenderAccessGrantedAsync` in `ProgramSettingsService.cs:195` (Flag UND
  (Admin ODER Allowlist-Zeile)); Gating in `NavMenu.razor:136`, `LiveTv.razor:204-211/219`
  (Notice `#live-tv-no-access`, kein Guide-Aufruf, keine Poll-Schleife),
  `ProgramGuideController` (both `now` + `upcoming` → `Forbid()`) und
  `LiveStreamController.GetFile` → `Forbid()`; Admin-Checkbox-Liste in
  `ProgramSettings.razor` (Admin-Häkchen disabled, "immer Zugriff"); explizites Cleanup in
  `UserManagement.razor` DeleteUser + FK-Cascade; `OptionalRestoreTables` erweitert;
  Hilfe + Release Notes aktualisiert.
- **Build:** `dotnet build VideoPlayer.sln` Debug **und** Release: 0 Fehler, je 144
  Warnungen — alle altbekannt (BL0008-Flotte, CS9113 `LiveTvTests` ctor, CS8602
  `LiveStreamE2ETests:79` — beide Stellen auf Base identisch, nur Zeilen verschoben).
  Kein Datei-Lock aufgetreten.
- **Volle Testsuite** (`dotnet test VideoWebPlayer.Tests/VideoWebPlayer.Tests.csproj`,
  synchron): **2210 grün + 1 Alt-Skip** (`DevicePlaylistE2ETests_Playback...` — bekannter
  Umgebungs-Skip). Einziger Ausfall: `MediaBoxContextMenuInteractionE2ETests.
  PointerReleaseBeforeDelay_DoesNotOpenMenu_AndNavigatesNormally` — Playwright-Timing-Flake
  im Kontextmenü, fachlich unabhängig von N26-N28, bei Einzelwiederholung grün.
- **Gegenproben (abgesicherte Stellen absichtlich gebrochen):**
  - `CssFormat.Percent` → `CultureInfo.CurrentCulture`: `CssFormatTests.
    Percent_UnderGermanCulture_WritesDotSeparator` und `LiveTvTests.
    Now_ProgressFill_WritesInvariantPercent_UnderGermanCulture` schlagen fehl
    (`"width:12,5%"` statt `"width:12.5%"`) — der Komma-Bug wäre exakt reproduziert.
    Zurückgesetzt.
  - `IsTvSenderAccessGrantedAsync` → Allowlist-Check entfernt (`return false` nach
    Admin-Check): `TvSenderUserAccessTests.IsGranted_RequiresFlagAndAllowlistOrAdmin` und
    `ProgramGuideControllerTests.GetNow_UserWithTvAccess_Returns200` schlagen fehl.
    Zurückgesetzt (`git checkout --`), Arbeitsverzeichnis wieder sauber.
- **Fachlich kritisch nachvollzogen:**
  - Invarianz ist bewiesen: die Tests setzen `CurrentCulture`/`CurrentUICulture` auf de-DE
    und fordern Punkt-Dezimaltrenner — der de-DE-Test existiert und greift (Gegenprobe).
  - Tagesgruppierung `s.Start.ToLocalTime().Date` verwendet dieselbe Konvertierung wie die
    angezeigten Sendezeiten (`slot.Start.ToLocalTime()` an `TVSenderAdmin.razor:383`) —
    Header und angezeigte Zeiten können sich nicht widersprechen. Divider erscheint korrekt
    nur bei Regelwechsel (erster Slot je Tagesgruppe ohne Divider); bUnit-Test deckt
    zwei Tage + einen Wechsel ab.
  - 403 ist serverseitig: beide ProgramGuide-Endpunkte und `live-stream/{fileName}` prüfen
    die Freigabe nach dem Feature-Flag (404 bleibt die Tarnung bei deaktiviertem Feature,
    403 bei aktivem Feature ohne Benutzerfreigabe — sinnvolle Semantik). `CheckLoggedIn()`
    läuft vorher; `ForbidResult` = HTTP 403. Die LiveTv-Seite startet ohne Freigabe weder
    Guide-Abruf noch Poll-Loop; das NavMenu blendet den Eintrag aus.
  - Allowlist-Logik `Flag AND (Admin OR Zeile)`: Admin ohne Zeile kommt rein — durch Tests
    auf Service-, Controller- und NavMenu-Ebene belegt
    (`NavMenu_ShowsLiveTvEntry_ForAdminWithoutAccessRow`,
    `GetNow_AdminWithoutTvAccessRow_Returns200`, `GetFile_AdminWithoutTvAccessRow_...`).
  - `UpdateTvSenderAllowedUserIdsAsync` arbeitet mit Replace-Semantik in einem
    `SaveChanges` — kein halbgeschriebener Zwischenstand; ungültige Ids werden gefiltert
    (Test belegt).
  - FK-Cascade in Konfiguration + Migration identisch; `DeleteUser` entfernt die Zeile
    zusätzlich explizit (Muster wie `MediaSourceUsers`) — funktioniert auch ohne
    erzwungene FKs.
  - `OptionalRestoreTables` enthält `TvSenderUserAccess`; Regressionstest restauriert ein
    Alt-Backup ohne die Tabelle fehlerfrei, Rundreise-Test stellt Zeilen wieder her.
    FK-Test läuft gegen echtes SQLite (`AccessRow_RequiresExistingUser_FkEnforced` wirft
    `DbUpdateException`).

## Abweichungen

Keine.

## Hinweise

- `TVSenderAdmin.razor:393` und `:521` nutzen weiterhin `width:@ProgressPercent%` bzw.
  `ProbePercent%` statt `CssFormat.Percent` — dort ist der Wert ein `int`, kann also kein
  Dezimal-Komma erzeugen. Kein Bug, nur inkonsistent zum neuen Helper; bei späterem
  Umbau auf `double` sofort auf `CssFormat.Percent` umstellen.
- `ProgramSlot.Start` ist `DateTime` und wird als UTC gespeichert, von EF/SQLite aber als
  `DateTimeKind.Unspecified` gelesen (`ProgramGuideService.cs:142` kompensiert das selbst
  via `SpecifyKind`). Damit ist `ToLocalTime()` an den Anzeige-/Gruppierungsstellen
  faktisch eine No-Op — die Sendeplan-Zeitanzeige zeigt streng genommen UTC-Werte. Das ist
  ein **Alt-Befund** (Zeile schon auf dem Projekt-Branch, nicht in diesem Schritt
  eingeführt); die N27-Gruppierung ist mit der Anzeige konsistent. Bei Bedarf als eigenes
  Nachbesserungs-Item aufnehmen.
- Untracked `VideoWebPlayer/Data/LiveStream/` (live.m3u8 + .ts-Segmente, Zeitstempel
  während des Testlaufs) ist Laufzeit-Artefakt einer laufenden App-Instanz — nicht
  angefasst, nicht committed.
- Testlauf dauerte ~4:22 min; der eine Playwright-Ausfall wurde wie vereinbart einzeln
  wiederholt und war grün.
