# Bestandsaufnahme: Medienquellen — Merkmal „Im Menü ausblenden“ (Issue #284)

Die Anforderung sieht ein neues boolesches Merkmal `HiddenInMenu` an `MediaSource` vor, das den Menüeintrag im `NavMenu` unterdrückt, ohne Zugriff, Scanner, Planungslogik oder Direktaufruf zu ändern. Diese Bestandsaufnahme hält fest, was dafür bereits existiert und wie der Test-Ausgangszustand aussieht.

## Zusammenfassung

- `MediaSource` (`VideoWebPlayer/Data/MediaSource.cs`) hat **kein** `HiddenInMenu`-Feld; `MediaSourceExtensions.Update` kopiert heute neun Felder einzeln und müsste ergänzt werden.
- `DtoMediaSource` (`VideoWebPlayer.Client/Models/DtoSource.cs`) enthält das Feld noch nicht; die Auslieferung über `ApiBaseController.Create<T>` würde nach Ergänzung automatisch greifen (namensgleiche Properties, Reflection).
- `NavMenu.razor` rendert die Quellen-Liste **ungefiltert** aus `Client.RequestSourcesAsync()`; die Sektionsüberschrift „Quellen" hängt an `mediaSources.Any()` — die Filterung muss vor dieser Prüfung greifen.
- `SourcesController.GetSources`/`GetSource` filtern nur nach Freigabe (`MediaSourceUsers` ∪ `IUnlockedMediaService`), nicht nach Sichtbarkeit; `AdminSourcesController` hat nur `DeleteSource` — kein API-Schreibpfad für das Flag.
- `MediaSourceAdminDetails.razor` kopiert `editSource` beim Bearbeiten **manuell feldweise** — ein neues Feld muss dort explizit übernommen werden; `MediaSourceAdmin.razor` (`ResetScan`) läuft ebenfalls über `UpdateMediaSourceAsync`.
- `VideoWebPlayerBackupData` benötigt Einträge in `OptionalRestoreColumns` und `OptionalRestoreBoolDefaults`; das exakte Muster existiert bereits (`MediaSources.SourceType` int-Default, `Setups.TvSenderEnabled` bool-Default, Migration `AddSetupTvSenderEnabled`).
- Für alle benötigten Testarten existieren Vorbilder und Infrastruktur: `VideoWebPlayerBackupDataTests_TvSenderEnabled` + `LegacyBackupArchiveBuilder.RemoveColumnsAsync` (Backup-Regression), `ApplicationDbContextTests.Update_CopiesSourceType` (Update-Kopie), `SourceVisibilityControllerTests` (Controller/DTO), bUnit-Tests `NavMenuLiveTvTests`/`NavMenuPlaylistsTests` (Menü-Rendering — entgegen der Vermutung in der Anforderung **ist** bUnit-Infrastruktur vorhanden), E2E-Tests um `MediaSourceSwitchE2ETests`.
- Der `ApiDocumentationContractTests`-Vertrag prüft nur Routen-Überschriften, **keine** DTO-Felder — die `docs/API.md`-Pflicht zur Dokumentation des neuen Felds kommt aus der Anforderung/DoD, nicht aus einem automatischen Check.
- Verifikationspunkte verifiziert: `TVSenderAdmin.razor` (Zeile 940), `TVSenderAdminClipGroups.razor` (Zeilen 104–112, 382), `ProgramClipGroupService.GetClipCandidatesAsync` (Zeile 116), `MediaSourceScanner` (Zeile 57), `MediaSourceDetails.razor`/`MediaSourceDetailsViewModel` und `MediaSourceAdmin.razor` lesen `MediaSource` direkt über den `DbContext` — ein `HiddenInMenu`-Flag ist dort unsichtbar und verändert nichts.

Test-Ausgangszustand: Zwei Läufe (Release, `--no-build`) decken die volle Suite ab — `Category!=E2E`: 2192 von 2193 bestanden, 0 Fehler, 1 übersprungen (Unix-only); `Category=E2E`: 230 von 231 bestanden, 0 Fehler, 1 übersprungen (dokumentierter bekannter Defekt, `[Fact(Skip)]`). **Keine bekannten Fehlschläge.** Details: [inventory/tests.md](inventory/tests.md), Nachweise unter [inventory/test-results/](inventory/test-results/).

## Details

- [Datenmodell](inventory/models.md)
- [Logik](inventory/logic.md)
- [Enums](inventory/enums.md)
- [Interfaces](inventory/interfaces.md)
- [Tests](inventory/tests.md)
