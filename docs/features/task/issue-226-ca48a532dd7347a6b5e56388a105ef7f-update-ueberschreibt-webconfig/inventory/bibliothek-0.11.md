# Bibliothek `msTools.Updater` 0.11.0-rc.1 — neue API und Skriptmechanik

Delta-Analyse zu Runde 1 (dort `0.10.4-rc.1` decompiliert). Quellenlage: `lib/packages/msTools.Updater.0.11.0-rc.1.nupkg` (liegt seit dieser Runde untracked im Repo), entpackt und mit `ilspycmd` 11.0.0.9375 decompiliert; XML-Doku `lib/net10.0/msTools.Updater.xml`; nuspec.

## Paketdaten (nuspec)

| Merkmal | Wert |
|---------|------|
| Version | `0.11.0-rc.1` |
| Lizenz | `MIT` (license expression) — keine Änderung zu 0.10.4 (kein PolyForm) |
| Repository-Commit | `61e3b0e2238a6b3c5aef3debaeff464d00b2be98` (`github.com/martin-stromberg/msTools.Updater`) |
| TFM | `net10.0` (einzige Lib-Variante) |
| Abhängigkeiten | `Microsoft.Extensions.*` je `10.0.7` (Configuration.Binder, DI.Abstractions, Hosting.Abstractions, Http, Logging.Abstractions, Options) |
| Inhalt | `lib/net10.0/msTools.Updater.dll` + `.xml` + `README.md` |

## Neue öffentliche API

### `AutoUpdateProtectedFile` (sealed class)

| Eigenschaft | Typ | Semantik |
|-------------|-----|----------|
| `Path` | `string` | Relativ zum Anwendungsverzeichnis, Wildcards `*`/`?` erlaubt; darf nicht rooted sein und keine `..`-Segmente enthalten (Validator, s. u.) |
| `Strategy` | `AutoUpdateProtectedFileStrategy` | `Preserve` (Datei vollständig behalten) oder `Merge` (nur konfigurierte Bereiche übernehmen) |
| `XmlElements` | `List<string>` | XPath-Elementpfade, z. B. `//aspNetCore/environmentVariables`; nur bei `Merge` |
| `XmlAttributes` | `List<string>` | Attribut-Übernahmen im Format `{element-xpath}@{attribut}`, z. B. `//aspNetCore@requestTimeout`; nur bei `Merge` |
| `JsonKeys` | `List<string>` | `:`-getrennte JSON-Schlüsselpfade, z. B. `Jwt:Key`; nur bei `Merge` |

### `AutoUpdateProtectedFileStrategy` (enum)

| Wert | Bedeutung |
|------|-----------|
| `Preserve` | Bestandsdatei wird vor dem Paketkopiervorgang gesichert und danach zurückkopiert — Paketänderungen an der Datei gehen verloren |
| `Merge` | Nur die konfigurierten XML-Elemente/-Attribute/JSON-Schlüssel werden aus der Bestandsdatei in die neue Paketdatei übernommen — Paketdatei bleibt sonst maßgeblich |

### `AutoUpdateOptions.ProtectedFiles`

- `public List<AutoUpdateProtectedFile> ProtectedFiles { get; set; } = new()` — leere Liste = bisheriges Verhalten (Vollüberschreibung).
- Per Konfigurationsbindung setzbar: `AutoUpdate:ProtectedFiles` (Standard-Sektion `AutoUpdate`, änderbar über `BindConfiguration`). `Strategy` bindet als String `"Preserve"`/`"Merge"`.

### `AutoUpdateBuilder` — neue Fluent-Methoden

| Methode | Verhalten |
|---------|-----------|
| `ProtectFile(AutoUpdateProtectedFile)` | fügt Eintrag hinzu (`ArgumentNullException` bei null) |
| `PreserveFile(string path)` | Kurzform `Preserve` (`ArgumentException` bei leerem Pfad) |
| `MergeFile(string path, Action<AutoUpdateProtectedFile>)` | `Merge`-Eintrag; wirft `ArgumentException` „A merged file requires at least one XML element, XML attribute or JSON key rule.", wenn `configure` keine Regel hinzufügt |
| `ExplicitProtectedFiles` (internal) | Snapshot der explizit konfigurierten Einträge |

## Vorrang Fluent vs. Konfigurationsbindung (verifiziert im decompilierten `UseAutoUpdate`/`BuildOptions`)

Reihenfolge in `AutoUpdateHostBuilderExtensions.UseAutoUpdate` → `BuildOptions`:

1. `configure`-Delegat läuft zuerst (Fluent-Aufrufe landen in `builder.Options` **und** in den `Explicit*`-Snapshots).
2. `builder.Configuration.GetSection("AutoUpdate").Bind(options)` — füllt bindbare Werte.
3. `ReapplyExplicitValues`: explizit gesetzte Werte werden **nach** der Bindung erneut auf die Optionen geschrieben — darunter `options.ProtectedFiles = explicitProtectedFiles.ToList()`, wenn `ExplicitProtectedFiles != null`.
4. `HealthTimeoutSeconds`-Clamp, Default-Quelle (`<DownloadPath>/source`), `AutoUpdateOptionsValidator` — bei `Failed` `OptionsValidationException` beim Start.

**Konsequenz:** Sobald mindestens ein `ProtectFile`/`PreserveFile`/`MergeFile`-Aufruf erfolgt, **ersetzt** die explizite Liste die gesamte gebundene Liste (kein Pfad-weises Mischen). Ohne Fluent-Einträge gilt allein die Konfigurationsbindung. Gleiches „explicit wins"-Muster wie bei `EnableAutomaticDownload`/`SourceCheck.Interval`/`UpdateUnitName` u. a.

## Startup-Validierung (`AutoUpdateOptionsValidator`, neu für `ProtectedFiles`)

Fehler → `OptionsValidationException` beim Start (kein Laufzeitfallback):

- `Path` leer → „ProtectedFiles entries must have a non-empty Path."
- `Path` rooted (`Path.IsPathRooted`, führendes `/` oder `\\`) → Fehler.
- `..`-Segmente (auf `/` oder `\` gesplittet) → Fehler.
- Ungültige Pfadzeichen außer `*`/`?` → Fehler.
- `Strategy == Merge` ohne jede Regel (`XmlElements`, `XmlAttributes`, `JsonKeys` alle leer) → Fehler.
- `XmlAttributes`-Eintrag nicht im Format `{xpath}@{attr}` (genau ein `@`, nicht am Ende) → Fehler.

## Umsetzung im generierten Skript

`AutoUpdateScriptGenerator` hat eine neue ctor-Signatur `(..., AutoUpdateOptions? options = null, ILogger? = null)`; `HasProtectedFiles` = `_options?.ProtectedFiles?.Count > 0`. Ohne Einträge erzeugt das Skript **exakt** den bisherigen Inhalt. `BackupDirectory` = `Path.Combine(_packageStore.RootDirectory, "backup")` → bei `AutoUpdate:DownloadPath = "Updates"` ist das `Updates/backup/` (relativ zum Anwendungsverzeichnis). Die Backup-Dateien liegen darunter mit ihrer relativen Pfadstruktur (`Updates/backup/web.config`, `Updates/backup/appsettings.json`, …) und werden **nicht** automatisch aufgeräumt (manueller Restore-Pfad; keine Retention).

Strikte Reihenfolge in allen drei Skriptvarianten: **Backup → Paketkopiervorgang → Merge/Restore → Lock-/Descriptor-Entfernung → Dienst-/AppPool-/Exe-Neustart**. Backup- oder Merge-Fehler **brechen die Installation nicht ab**: `Write-Warning` (Windows) bzw. `update.log`-Eintrag (Linux), Neustart läuft trotzdem; Backups bleiben zur manuellen Wiederherstellung liegen.

### Windows `update.ps1` (beide Varianten: `GenerateWindowsAppPoolAsync` **und** `GenerateWindowsServiceOrExecutableAsync`)

- `$protectedFiles = @( @{ Path=…; Strategy='Preserve'|'Merge'; XmlElements=@(…); XmlAttributes=@(…); JsonKeys=@(…) }, … )`.
- `Get-AutoUpdateProtectedMatches` löst Wildcards per `Get-ChildItem -Path (Join-Path $AppRoot $Pattern)` auf (Literal-Fallback via `Test-Path -LiteralPath`) — Treffer sind Dateien im **Anwendungsverzeichnis**.
- `Backup-AutoUpdateProtectedFiles` (nach `Expand-Archive`, vor dem `Copy-Item`): kopiert jeden Treffer nach `Updates/backup/<relativer Pfad>` (Verzeichnisstruktur wird angelegt). In `try/catch` → `Write-Warning` bei Fehler.
- `Invoke-AutoUpdateProtectedFiles` (nach dem `Copy-Item`, vor Lock-Entfernung/Restart): je Treffer, `Preserve` → `Copy-Item` Backup über die neue Datei; `Merge` → `Merge-AutoUpdateXmlContent` (wenn XML-Regeln vorhanden) und/oder `Merge-AutoUpdateJsonContent`. Pro Datei `try/catch` → `Write-Warning`.
- **XML-Merge** (`[xml]` + `SelectSingleNode`, volle XPath-Unterstützung): Element — existiert der Knoten im Neuen, wird er ersetzt (`ReplaceChild`); sonst wird an den Elternknoten angehängt (Eltern-XPath durch Abstreifen des letzten Segments; fehlt der Elternknoten, wird übersprungen). Attribut — wird nur gesetzt, wenn das Element in **beiden** Dateien existiert und das Attribut im Alten vorhanden ist. Gespeichert wird nur bei Änderung.
- **JSON-Merge** (`ConvertFrom-Json` → `ConvertTo-Json -Depth 100`): Schlüsselpfad per `:`-Split; nur vorhandene Altwerte werden übernommen; fehlende Zwischenobjekte werden als `[pscustomobject]` neu erzeugt. Schreibt die **ganze Datei neu** (Formatierung kann sich ändern; JSON-Kommentare würden beim Parsen scheitern → per-Datei-Warnung). Repo-`appsettings*.json` enthalten keine Kommentare.
- Nur-in-der-Bestandsinstallation-vorhandene Treffer (z. B. eine nicht paketierte `appsettings.Local.json`): werden gesichert und bei `Preserve`/`Merge` praktisch auf sich selbst zurückgeschrieben — sie werden vom Paketkopiervorgang ohnehin nicht überschrieben (no-op).
- Fehlt das Backup zu einer neuen Paketdatei (Erstinstallation) → `continue`, kein Fehler.

### Linux `update.sh`

- `backup="$updates/backup"`, `protected_paths` (alle Einträge), `preserve_paths` (nur `Preserve`), `protected_files_json` (JSON-Array der `Merge`-Einträge mit `path`/`xmlElements`/`xmlAttributes`/`jsonKeys`). `NormalizePattern` wandelt `\` in `/`.
- `backup_protected_files` per `compgen -G "$app/$pattern"` + `cp -f` (Glob-Expansion, keine `**`-Rekursion nötig für Root-Dateien).
- `apply_preserved_files` per `cp -f` — **keine** python3-Abhängigkeit.
- `merge_protected_files` erfordert **`python3` auf dem Zielsystem**: `command -v python3` fehlt → `write_log "python3 is not available; protected file merges were skipped. Backups remain in $backup for manual restore."`, Returncode 0 (Update läuft normal weiter!). Eingebettetes Heredoc-Python (`xml.etree.ElementTree`, `json`, `glob`):
  - XML-`select` übersetzt führendes `/` zu `.//…` — **ElementTree-XPath-Teilmenge**: `//aspNetCore/environmentVariables` funktioniert; Attributprädikate wie `//add[@name='x']` **nicht** (`SyntaxError` → leere Treffermenge, still übersprungen). Auf Linux sind Merge-Regeln nur mit ElementTree-kompatiblen Pfaden wirksam.
  - JSON-Merge analog PowerShell (`:`-Split, fehlende Zwischenobjekte werden angelegt), Ausgabe `json.dump(indent=4)` — schreibt ebenfalls die ganze Datei neu.
  - Fehler je Datei → `stderr`-Meldung; Gesamtfehler → `write_log`, Update nicht abgebrochen.
- `Preserve` und Backups laufen komplett mit Bordmitteln (`cp`, `compgen`); nur `Merge` hängt an `python3`.

## Erzeugungszeitpunkt und Migrationsrelevanz

Das Installationsskript wird von der **laufenden (alten)** Version erzeugt (`AutoUpdateInstaller.PrepareAsync` → `IAutoUpdateScriptGenerator.GenerateAsync` zum Installationszeitpunkt). Der Schutz greift daher erst ab dem **ersten Update nach** der Version, die diese Konfiguration ausliefert — das Update auf diese Version selbst läuft noch mit dem alten, schutzlosen Skript. Ebenso liest der Generator die Optionen zur Laufzeit aus dem `AutoUpdateOptions`-Singleton: `ProtectedFiles` kann von der Anwendung zwischen Check und Install mutiert werden (niemand im Repo tut das).

## Unveränderte Bestandteile (relevante Gegenprobe zu 0.10.4)

- `IAutoUpdateScriptGenerator`, `AutoUpdateScriptGenerator` (`sealed`), `TryAddSingleton`-DI-Registrierung, `UseAutoUpdate`-Signatur, `WithIisApplicationPool`, `AutoUpdateServiceResolver`/`DefaultAutoUpdateServiceProbe` (nur Windows-Dienste, keine App-Pools) — unverändert.
- `AutoUpdateOptions.WorkspaceUser`/`WorkspaceGroup` existieren **bereits in 0.10.4-rc.1** (XML-Doku geprüft) — kein Delta dieses Pakets.
