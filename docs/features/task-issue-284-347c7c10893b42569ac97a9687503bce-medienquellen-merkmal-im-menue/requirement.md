# Übersetzte Anforderung: Medienquellen — Merkmal „Im Menü ausblenden“ (Issue #284)

## Fachliche Zusammenfassung

Medienquellen, die ausschließlich Platzhalter-/Maskottchen-Clips (Interstitials) für den TV-Sender
bereitstellen — d. h. Quellen, die als Material einer `ProgramRule` vom Materialtyp
`ProgramRuleMaterialType.MediaSourceClips` („Clip-Quelle“) oder als Lieferant von
`ProgramClipGroup`-Kandidaten dienen —, müssen für alle Anwender über `MediaSourceUsers` bzw.
`IUnlockedMediaService` freigegeben sein. Diese Freigabe erzeugt heute zwangsläufig einen Eintrag in
der Quellen-Liste des `NavMenu.razor`, der auf `/mediasource/{id}` verlinkt, obwohl die
Browsing-Ansicht für eine reine Clip-Quelle keinen eingeordneten Inhalt zeigt.

Gewünscht ist ein neues, optionales boolesches Merkmal an `MediaSource` (Arbeitsname `HiddenInMenu`,
Default `false`), das den Menüeintrag für Admins und Nicht-Admins gleichermaßen unterdrückt. Das
Merkmal ist rein kosmetisch: Zugriffssteuerung, Scanner, `MediaItem`-Bestand, Planungslogik
(`ProgramScheduleService`), Füller-Regeln und Clip-Gruppen sowie der Direktaufruf von
`/mediasource/{id}` bleiben unverändert. Das Flag wird in `DtoMediaSource` ausgeliefert, damit auch
externe Clients (z. B. die MAUI-App über `VideoWebPlayerClient`) es perspektivisch auswerten können.

## Betroffene Klassen und Komponenten

### Datenmodell / Persistenz

- `VideoWebPlayer/Data/MediaSource.cs` — neue Eigenschaft `public bool HiddenInMenu { get; set; }`
  (Default `false` über CLR-Initialisierer und Migrations-`defaultValue`).
- `VideoWebPlayer/Data/MediaSourceExtensions.cs` — `Update(this MediaSource target, MediaSource source)`
  muss die neue Eigenschaft kopieren; sie wird von
  `ApplicationDbContext.UpdateMediaSourceAsync` aufgerufen. Ohne diese Ergänzung geht das Flag beim
  Speichern einer bestehenden Quelle still verloren.
- `VideoWebPlayer/Migrations/` — neue EF-Migration (Namensvorbild z. B. `AddSetupTvSenderEnabled`):
  `AddColumn<bool>` auf Tabelle `MediaSources`, `nullable: false`, `defaultValue: false`, plus
  aktualisiertem `ApplicationDbContextModelSnapshot.cs`. Bestandsdaten werden dadurch sichtbar
  (`false`).

### API / DTO

- `VideoWebPlayer.Client/Models/DtoSource.cs` — `DtoMediaSource` um `HiddenInMenu` erweitern.
  `ApiBaseController.Create<T>` kopiert namensgleiche Properties per Reflection, sodass das Flag in
  `SourcesController.GetSources` (`GET /api/Sources`) und `SourcesController.GetSource`
  (`GET /api/Sources/{id}`) ohne weiteren Mapping-Code ausgeliefert wird.
- `VideoWebPlayer/Controllers/SourcesController.cs` — abhängig von der Design-Entscheidung
  (s. „Offene Fragen“): entweder unverändert (Filterung clientseitig im `NavMenu`) oder die
  `GetSources`-Abfrage blendet `HiddenInMenu`-Quellen zusätzlich serverseitig aus. Die
  Freischaltungslogik (`MediaSourceUsers`, `IUnlockedMediaService.GetUnlockedSourceIdsForUserAsync`)
  darf in keinem Fall geändert werden.

### UI

- `VideoWebPlayer/Components/Layout/NavMenu.razor` — Filterung der Quellen-Liste: `HiddenInMenu`-Quellen
  aus `mediaSources` (Zeile ~131, `Client.RequestSourcesAsync()`) beim Rendern ausblenden. Zu
  beachten: Die Sektions-Überschrift „Quellen“ hängt an `mediaSources.Any()` (Zeile ~50); die
  Filterung muss vor dieser Prüfung greifen, damit die Überschrift verschwindet, wenn alle Quellen
  versteckt sind.
- `VideoWebPlayer/Components/Pages/Admin/MediaSources/MediaSourceAdminDetails.razor` — neue
  `InputCheckbox` (oder Checkbox im Stil der vorhandenen `form-check`-Elemente) mit erklärendem
  Hilfetext („für reine Clip-/Füller-Quellen — die Quelle bleibt für Scanner, Füller und Clip-Gruppen
  nutzbar“). Zwei Stolperstellen:
  - Die `editSource`-Kopie in `OnInitializedAsync` (Zeilen ~209–223) überträgt Felder einzeln von
    Hand — `HiddenInMenu` muss dort explizit übernommen werden, sonst zeigt die Checkbox beim
    Bearbeiten immer `false`.
  - Neu-Anlage (`editSource = new MediaSource { Port = 22 }`) benötigt nichts weiter; `false` ist
    CLR-Default.
- `VideoWebPlayer/Components/Pages/MediaSources/MediaSourceDetails.razor` /
  `ViewModels/MediaSourceDetailsViewModel.cs` — nur falls der optionale UX-Hinweis bei Direktaufruf
  einer versteckten Quelle gewünscht wird (Entscheidung offen). `MediaSourceDetailsViewModel` lädt
  die Quelle über `RequestSourceAsync` (Einzel-Endpunkt); das DTO-Feld steht damit bereits zur
  Verfügung.

### Backup / Restore

- `VideoWebPlayer/Services/Backups/VideoWebPlayerBackupData.cs`:
  - `OptionalRestoreColumns` um
    `$"{nameof(ApplicationDbContext.MediaSources)}.{nameof(MediaSource.HiddenInMenu)}"` erweitern.
  - `OptionalRestoreBoolDefaults` um
    `(nameof(ApplicationDbContext.MediaSources), nameof(MediaSource.HiddenInMenu), false)`
    erweitern (Default `false` = sichtbar), analog `Setup.TvSenderEnabled`.

### Explizit nicht betroffen (Verifikationspunkte für die Abnahme)

Das Merkmal darf die folgenden Stellen nicht verändern — sie lesen `MediaSource` direkt über
`ApplicationDbContext` und müssen versteckte Quellen weiterhin vollständig sehen:

- `TVSenderAdmin.razor` (Zeile ~940: `DbContext.MediaSources` für die Materialauswahl
  `ProgramRuleMaterialType.MediaSourceClips` und `ProgramRuleMediaSource`-Filter) — „Clip-Quelle“
  muss auswählbar bleiben.
- `TVSenderAdminClipGroups.razor` (Zeilen ~104–112: Quellenfilter der Clip-Kandidaten) und
  `ProgramClipGroupService.GetClipCandidatesAsync` (`mediaSourceId`-Parameter).
- `ProgramScheduleService` / `ProgramRuleService` / `ProgramRule` (`ClipGroups`,
  `MediaSourceFilters`) — Einplanung von Clips unverändert.
- `MediaSourceScanService` / `MediaSourceScanner` / `MediaSourceReaderDispatcher` — Scan unverändert.
- `SourcesController.GetSource` und `MediaSourceDetails.razor` — Direktaufruf `/mediasource/{id}`
  bleibt erreichbar (kein Zugriffsschutz).
- `MediaSourceAdmin.razor` (Admin-Quellenliste) — Admins sehen versteckte Quellen weiterhin in der
  Verwaltung.
- `AdminSourcesController` — enthält nur `DeleteSource`; kein Schreibpfad für das Flag über die API.

### Tests

- Backup-Regressionstest nach Vorbild
  `VideoWebPlayer.Tests/Services/Backups/VideoWebPlayerBackupDataTests_TvSenderEnabled.cs` (Helper
  `LegacyBackupArchiveBuilder.RemoveColumnsAsync`): Alt-Backup ohne `MediaSources.HiddenInMenu`
  restored fehlerfrei, fehlende Spalte wird mit `false` aufgefüllt — Seed bewusst mit `true`
  belegen, damit der Restore-Default bewiesen wird.
- Update-Kopiermodus: Test im Stil von `ApplicationDbContextTests.Update_CopiesSourceType`, der
  sicherstellt, dass `MediaSourceExtensions.Update` bzw. `UpdateMediaSourceAsync` `HiddenInMenu`
  überträgt.
- DTO-/Controller-Test (z. B. Erweiterung `SourceVisibilityControllerTests`): `GetSources` liefert
  `HiddenInMenu` im `DtoMediaSource` aus; bei serverseitiger Filterung zusätzlich: versteckte Quelle
  fehlt in der Liste, ist aber über `GetSource(id)` erreichbar.
- E2E-/Playwright-Test: versteckte Quelle erscheint nicht im `NavMenu` (für Admin- und
  Nicht-Admin-Kontext), Direktaufruf `/mediasource/{id}` funktioniert, Checkbox in
  `MediaSourceAdminDetails.razor` persistiert den Wert. Hinweis: Für Razor-Komponenten ohne
  bUnit-Infrastruktur ist ggf. zu prüfen, ob die Menüfilterung als reine Unit-Logik testbar gemacht
  werden soll.
- API-Dokumentationsvertrag beachten (`ApiDocumentationContractTests`): `docs/API.md` muss das neue
  Feld im `DtoMediaSource`-Beispiel dokumentieren.

### Dokumentation (per Definition of Done)

- `docs/API.md` — `DtoMediaSource`-Beispiel unter `GET /api/Sources` erweitern.
- `docs/help/medienquellen/` — insbesondere `datenmodell.md` (Feld, Migration, Restore-Eintrag,
  DTO-Grenze; beachte dort dokumentierte Konvention „DTO enthält bewusst nicht alles“),
  `einrichtung-anwender.md` bzw. `beschreibung.md` (Admin-Anleitung zum Merkmal).
- `README.md` und `docs/RELEASE_NOTES.md` — bei Bedarf aktualisieren.

## Implementierungsansatz

- Neues Feld `HiddenInMenu` (`bool`, Default `false`) auf `MediaSource` — keine neue Klasse, kein
  neues Interface, kein Enum nötig. Persistenz und DTO-Auslieferung laufen über vorhandene
  Mechanismen (EF Core Property, `ApiBaseController.Create<T>`-Reflection).
- Migration wie bei `AddSetupTvSenderEnabled`: non-nullable `INTEGER`-Spalte mit `defaultValue:
  false`; der Model-Snapshot wird von der Migration mitgeführt.
- Kopierpfade mitschleifen: `MediaSourceExtensions.Update` (Persistenz) und die manuelle
  `editSource`-Kopie in `MediaSourceAdminDetails.OnInitializedAsync` (UI-Bindung).
- Menüfilterung im `NavMenu.razor` beim Rendern der `mediaSources`-Liste; die Sektionsbedingung
  `mediaSources.Any()` muss auf der gefilterten Menge ausgewertet werden.
- Admin-Pflege ausschließlich über `MediaSourceAdminDetails.razor` (Blazor-Server-seitige
  `isAdmin`-Prüfung in `OnInitializedAsync` plus `AuthorizeView`); ein Schreiben des Flags über die
  REST-API existiert nicht und ist nicht erforderlich.
- Backup/Restore: `OptionalRestoreColumns` + `OptionalRestoreBoolDefaults` wie oben; neue Backups
  enthalten die Spalte automatisch durch die Entity-Iteration über `DbContext.Model`.
- Events: `UpdateMediaSourceAsync` publiziert bereits `MediaSourceUpdatedEvent`; das `NavMenu`
  befüllt `mediaSources` nur in `OnInitializedAsync` — ein geändertes Flag wird wie bisherige
  Quelländerungen erst beim nächsten Laden sichtbar (Beverhalten unverändert, ggf. als offene Frage
  führen).

## Konfiguration

Das Merkmal ist eine Eigenschaft je `MediaSource` und kein globaler Schalter; ein Eintrag in
`Setup`/`ProgramSettingsService` ist nicht erforderlich. Die Pflegeebene ist die Admin-
Quellenverwaltung (`/admin/mediasources/{id}`); die serverseitige Absicherung erfolgt über das
bestehende `isAdmin`-Gate der Razor-Seite (Nicht-Admins erhalten „Zugriff verweigert“).

## Offene Fragen

1. **Filterort serverseitig vs. clientseitig:** Soll `SourcesController.GetSources` versteckte
   Quellen bereits serverseitig aus der Liste entfernen, oder filtert nur `NavMenu.razor`?
   Clientseitige Filterung hält die API-Liste vollständig und macht das DTO-Flag für
   Listenkonsumenten (MAUI-App) nutzbar — das entspricht der Begründung des Akzeptanzkriteriums.
   Serverseitige Filterung wäre einfacher durchsetzbar, ließe das DTO-Flag in der Liste aber immer
   `false` sein (nutzbar nur über `GetSource`). Entscheidung in der Planung.
2. **Optionaler Hinweis bei Direktaufruf:** Soll `/mediasource/{id}` bei einer versteckten Quelle
   einen Hinweis anzeigen (reine UX, kein Zugriffsschutz)? Laut Anforderung „Entscheidung in
   Planung“.
3. **Sichtbarmachen des Merkmals in der Admin-Übersicht:** Soll `MediaSourceAdmin.razor` versteckte
   Quellen kenntlich machen (z. B. Badge/Spalte)? Nicht gefordert, verbessert aber die
   Nachvollziehbarkeit, warum eine Quelle nicht im Menü erscheint.
4. **Benennung:** `HiddenInMenu` ist der Vorschlag aus der Anforderung; die Codebasis nutzt für
   boolesche Flags gemischte Konventionen (`IsManuallyEdited`, `IsPublic`, `TvSenderEnabled`,
   `Changed`). Ggf. `HiddenInMenu` beibehalten oder an Konvention angleichen.
5. **Aktualisierung des laufenden Menüs:** Ändert ein Admin das Flag, sehen bereits angemeldete
   Clients die Änderung erst nach Neuladen des `NavMenu` (bisheriges Verhalten auch für
   Umbenennungen). Ist das akzeptabel oder soll ein Refresh-Mechanismus vorgesehen werden?
