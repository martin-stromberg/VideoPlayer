# Plan-Gegenprüfung

## Ergebnis

**Status:** Plan vollständig

## Abgleich Akzeptanzkriterien

| Akzeptanzkriterium | Umsetzung im Plan | Testnachweis im Plan | Status |
|--------------------|-------------------|----------------------|--------|
| 1. Sender als benanntes, kanalfähiges Objekt (genau einer ausgeliefert) | `TVChannel`-Entity + `GetOrCreateChannelAsync` (Auto-Anlage „TV-Sender"), Umbenennung; `ProgramRule.TVChannelId`-FK | `ProgramRuleServiceTests_Channel` (Anlage, Idempotenz, Rename, Kaskade) | Abgedeckt |
| 2. Programmregel-CRUD inkl. Aktivieren/Deaktivieren/Löschen | `ProgramRuleService` CRUD + `SetRuleActiveAsync`/`DeleteRuleAsync`; UI-Liste mit Toggle + `ConfirmationDialog` | `ProgramRuleServiceTests_Crud`; E2E `Admin_CreateEditDeactivateDeleteRule` | Abgedeckt |
| 3. Regelinhalt (Regelart, Wiederholungsmuster, Sendezeit, Länge/Folgen, Material, Priorität) | `ProgramRule`-Felder + 3 Enums; Validierungsmatrix | `ProgramRuleServiceTests_Validation` | Abgedeckt |
| 4. Materialauswahl: genau eines je Regelart (Serie/Film/Sammlung/Genre/Playlist) über Such-/Auswahldialoge, keine IDs | `MaterialType`/`MaterialId` + Existenzprüfung; `MediaSearchSelector.AllowedMediaTypes`, Genre-/Playlist-Dropdowns | `ProgramRuleServiceTests_Validation` (Matrix, Existenz); `MediaSearchSelectorTests`; E2E-Auswahl per Suche | Abgedeckt |
| 5. Nur Administratoren, serverseitig erzwungen | Service prüft `ApplicationUser.IsAdmin` aus DB je Methode; UI-Gate zusätzlich | `ProgramRuleServiceTests_Authorization`; E2E `NonAdmin_SeesNotAuthorized` | Abgedeckt |
| 6. Migration + Backup-Registrierung + Alt-Backup-Regressionstest | Migration `AddTVChannelsAndProgramRules`; `OptionalRestoreTables`/`OptionalRestoreColumns` | `VideoWebPlayerBackupDataTests`-Regressionstest; Unique/FK-Tests auf echtem SQLite | Abgedeckt |
| 7. Bundesland-Konfiguration über etablierte Einstellungsmechanismen | `Setup.HolidayFederalState` + `ProgramSettingsService` Get/Update + Dropdown in Admin-Seite | `ProgramSettingsServiceTests`-Erweiterung | Abgedeckt |

## E2E-Abdeckung

| Benutzerfluss / Akzeptanzkriterium | Geplanter E2E-Test | Status |
|------------------------------------|--------------------|--------|
| Regel anlegen inkl. Materialauswahl per Suchdialog, bearbeiten, deaktivieren, löschen | `TVSenderAdminE2ETests.Admin_CreateEditDeactivateDeleteRule` | Abgedeckt |
| Nicht-Administrator → „Nicht autorisiert." | `TVSenderAdminE2ETests.NonAdmin_SeesNotAuthorized` | Abgedeckt |

## Hinweise

- Verwaiste `MaterialId`-Referenzen (kein DB-FK) sind bewusste Entscheidung; die Behandlung liegt bei Schritt 3 — im Code-Review als dokumentiert akzeptieren, nicht als Befund.
- Die Begriffswelt „Sender/Programmregel" ist im Code konsequent von `Playlist` getrennt zu halten (nur `MaterialType.Playlist` referenziert die Benutzer-Playlist als Material).
