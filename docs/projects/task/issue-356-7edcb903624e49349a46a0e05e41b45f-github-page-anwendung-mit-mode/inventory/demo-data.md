# Demodaten-Mechanismus und Erstregistrierung

## `IDemoDataSetService`

Datei: `VideoWebPlayer/Services/DemoData/IDemoDataSetService.cs`

| Member | Signatur | Zweck |
|--------|----------|-------|
| `DemoDataSetInfo` | `record(string Id, string Name, string? Description)` | Leichtgewichtige Info über einen gefundenen Bestand (Zeile 9) |
| `GetAvailableAsync` | `(CancellationToken) → Task<IReadOnlyList<DemoDataSetInfo>>` | Listet verfügbare Bestände (Zeile 19) |
| `ApplyAsync` | `(string demoDataSetId, ApplicationUser user, CancellationToken) → Task` | Wendet einen Bestand auf die DB an (Zeile 23) |

DI-Registrierung: `services.AddScoped<IDemoDataSetService, FileSystemDemoDataSetService>()`
in `VideoWebPlayer/Extensions/ServiceCollectionExtensions.cs:344`.

## `FileSystemDemoDataSetService`

Datei: `VideoWebPlayer/Services/DemoData/FileSystemDemoDataSetService.cs`

| Element | Ort | Beschreibung |
|---------|-----|--------------|
| `DemoDataFolderName` | Zeile 13 | Konstante `"DemoDataSets"` |
| `GetAvailableAsync` | Zeilen 38–66 | Enumeriert `*.json` im Demo-Ordner (nur Top-Level), deserialisiert `DemoDataSetDefinition`, Id = Dateiname ohne Endung, sortiert nach `Name` |
| `ApplyAsync` | Zeilen 69–133 | Sucht `<id>.json`, öffnet DB-Transaktion, legt pro `MediaSources`-Eintrag eine `MediaSource` an und verknüpft sie per `MediaSourceUser` mit dem übergebenen Benutzer |
| `GetDemoDataFolderPath` | Zeilen 135–148 | Kandidaten: `ContentRootPath/DemoDataSets` → `AppContext.BaseDirectory/DemoDataSets` → Fallback ContentRoot-Pfad |
| `DemoDataSetDefinition` | Zeilen 150–155 | `{ Name, Description?, MediaSources? }` |
| `DemoMediaSourceDefinition` | Zeilen 157–166 | `{ Name, Icon?, Host?, Port?, Path?, Username?, Password? }` — **kein `SourceType`-Feld** |

Was `ApplyAsync` heute tut (Zeilen 96–124):

- Überspringt Einträge ohne `Name` und Quellen, deren `Name` bereits existiert.
- Legt `MediaSource` mit `Host`/`Port` (Default 22)/`Path`/`Username`/`Password` an —
  **`SourceType` wird nicht gesetzt** und bleibt beim Entity-Default `Sftp`.
  `Icon` aus der Definition wird ignoriert (`MediaSource.IconPictureId` bleibt null).
- Ruft `_db.AddMediaSourceAsync(source)` auf → publiziert `MediaSourceCreatedEvent`
  (ohne bekannte Subscriber) und setzt den Id.
- Legt `MediaSourceUser { MediaSourceId, UserId }` an.
- Erzeugt **keine Dateien/Verzeichnisse und keine Inhalte** — ein `Path` aus der
  Definition muss bereits existieren, sonst liefert der Root-Scan später nichts.

Fehlerverhalten: fehlendes Verzeichnis → `DirectoryNotFoundException`; fehlende
Datei → `FileNotFoundException`; unlesbares JSON → `InvalidOperationException`.
DB-Fehler → Rollback der Transaktion.

## `Register.razor` — Erstregistrierung

Datei: `VideoWebPlayer/Components/Account/Pages/Register.razor`

| Stelle | Code |
|--------|------|
| Zeile 18 | `@inject IDemoDataSetService DemoDataSetService` |
| Zeilen 47–59 | `InputSelect id="demo-data-set"` wird nur gerendert, wenn `isFirstUser && demoDataSets.Count > 0`; Option `(Keine Demodaten anlegen)` mit leerem Wert |
| Zeilen 80–85 | `OnInitializedAsync`: `isFirstUser` = kein Benutzer außer `NormalizedUserName != "SYSTEM"`; nur dann `GetAvailableAsync()` |
| Zeilen 95–100 | Bei `isFirst` wird `user.IsAdmin = true` gesetzt |
| Zeilen 111–123 | Bei Auswahl: `ApplyAsync(Input.DemoDataSetId, user)`; Fehler → `identityErrors` = „Die Demodaten konnten nicht angelegt werden." — der Benutzer ist zu diesem Zeitpunkt **bereits angelegt** (`UserManager.CreateAsync` in Zeile 102), kein Rollback |
| Zeilen 188–189 | `InputModel.DemoDataSetId` (`string?`, Display „Demodatenbestand") |

Weiterer Kontext: `FirstUserRedirectMiddleware`
(`VideoWebPlayer/Services/FirstUserRedirectMiddleware.cs`) leitet HTML-Anfragen
auf `/Account/Register` weiter, solange noch kein Benutzer existiert (cached über
`Volatile _hasApplicationUser`).

## Auslieferung (`VideoWebPlayer.csproj`)

| Zeile | Regel |
|-------|-------|
| 87–92 | `<Content Update="DemoDataSets\*.json">` mit `CopyToOutputDirectory`/`CopyToPublishDirectory` = `PreserveNewest` — nur flache JSONs, **keine Unterverzeichnisse** (`media/`-Assets würden beim Publish nicht mitkommen) |
| 93–106 | Target `CopyStandardSourcesDemoData` (AfterTargets `Build;Publish`): kopiert `StandardSources.json` aus Repo-Root (Kandidaten `../StandardSources.json`, `../../StandardSources.json`) nach `$(PublishDir/OutDir)DemoDataSets/` — die Datei existiert im Repo **nicht**, das Target meldet „nicht gefunden" und überspringt |

Release-Verpackung: `.github/actions/build-and-package/action.yml` führt
`dotnet publish … -o publish/{win-x64,linux-x64}` aus und zippt den Ordner —
alles, was im Publish-Output liegt, wird ausgeliefert.

## Lücken gegenüber der Anforderung (F-C1–F-C5)

- `SourceType` kann per JSON nicht gesetzt werden — Definition und `ApplyAsync`
  müssten `LocalDirectory` unterstützen, sonst liest der `SftpMediaSourceReader`
  das `Path`-Feld als SFTP-Pfad.
- Es gibt keinen Mechanismus, ein Demo-Medienverzeichnis auf die Platte zu
  schreiben oder `MediaSource.Path` relativ zum ContentRoot aufzulösen —
  `LocalDirectory` verlangt einen absoluten Pfad (`TryValidateLocalDirectory`,
  siehe media-sources.md).
- Kein Scan-Trigger: nach `ApplyAsync` bleiben Bibliothek und Menü leer, bis der
  Hintergrund-Loop läuft (Default-Intervall 60 min, siehe scan-pipeline.md).
- Nicht atomar mit der Registrierung: schlägt `ApplyAsync` fehl, existiert der
  Erstbenutzer trotzdem (offene Frage O-9 in requirement.md).
