# Anforderung: Diagnosefähigkeit des msTools.Updater-Installationsskripts

## Ziel

Das Verhalten beim Fehlschlag eines automatischen Updates muss so weit durch `msTools.Updater` protokolliert und aufbewahrt werden, dass die tatsaechliche Ursache im Nachhinein reproduzierbar identifiziert werden kann.

Aktuell beendet der Host sich unmittelbar nach Script-Start. Der Fehler, das heruntergeladene Paket und das generierte Script verschwinden zusammen mit dem beendeten Prozess. Es verbleibt keine ausreichende Beweislage, um die Ursache zweifelsfrei festzustellen.

## Festgestellte Symptome

- In der UI erscheint ein Hinweis, dass die installierte Version `x` nicht zur neuen Version `y` passt.
- Der Dienst stoppt und startet nicht wieder.
- Der Host ist durch `AutoUpdateOptions.StopHostAfterScriptStart` sofort beendet.
- Im Updater-Log steht, dass die Paketdatei entpackt werden soll, und anschliessend, dass sie nicht gefunden wurde.
- Die Paketdatei war zuvor vorhanden und ist anschliessend verschwunden.
- Die tatsaechlich generierte Skriptdatei und `update.log` sind nach dem Fehlschlag nicht verfuegbar.

Die eigentliche Ursache ist unbekannt. Der Paket-Dateiname wurde vom Anwender ausgeschlossen.

## Notwendige Änderungen an msTools.Updater

### A1. Korrekte Paketreferenz im Installationsskript

- `IAutoUpdateProcessRunner.StartScript` verifiziert die Paketdatei unmittelbar vor dem Start.
- Das generierte Script muss die Paketdatei unter einem absoluten, eindeutigen Pfad referenzieren, der aus `IAutoUpdatePackageStore.PendingAssetPath(assetName)` stammt.
- Der Prozessstart darf nicht von einem zufaelligen `WorkingDirectory` abhaengig sein, sondern muss den Pfad deterministisch aufloesen.
- `IAutoUpdatePackageStore.ScriptPath(...)`, `PendingAssetPath(fileName)`, `StagingDirectory` und `DescriptorPath` muessen vor dem Script-Start in das Anwendungslog geschrieben werden.

### A2. Jeder Installationsschritt im Script loggen

Das generierte Script muss alle relevanten Schritte in `update.log` schreiben, das es selbststaendig im `IAutoUpdatePackageStore.LogPath` erzeugt und befuellt. Mindestens erforderlich:

- Zeitstempel jedes Befehls.
- Vollstaendigen Pfad der verwendeten Paketdatei.
- Ergebnis und Exit-Code von `systemctl stop <ServiceName>`.
- Verzeichnis, in das entpackt wird.
- Ergebnis und Exit-Code der Entpackoperation.
- Ergebnis und Exit-Code von `systemctl start <ServiceName>`.
- Jede `release-metadata.json`-Pruefung inklusive der geprueften Werte.
- Jede Operation, die das Paket verschiebt, kopiert oder loescht.

### A3. Paket und Staging nicht vor Erfolg löschen

- Die heruntergeladene Paketdatei im `PendingDirectory` darf nur geloescht werden, wenn das Script mit Exit-Code 0 beendet und der Dienst als aktiv gemeldet wurde.
- Im Fehlerfall muss das Paket im `PendingDirectory` erhalten bleiben.
- Das `StagingDirectory` muss entweder leer oder mit dem entpackten Inhalt erhalten bleiben, damit geprueft werden kann, ob das Entpacken begonnen hat.

### A4. Statusmeldungen müssen nach Host-Stop lesbar sein

- `AutoUpdateStatusService` muss den Fehlerzustand und den Pfad zu `update.log` persistieren, bevor der Host beendet wird.
- Der Status-Snapshot muss nach dem naechsten Host-Start lesbar sein, damit die UI den Fehler ohne erneutes Update anzeigen kann.
- `AutoUpdateError.ProcessOutput` muss, sofern der Installationsprozess noch innerhalb desselben Host-Laufzeitraums endet, den Pfad zu `update.log` oder dessen Inhalt enthalten. Ist der Host bereits beendet, ist `update.log` auf dem Dateisystem die verlaessliche Quelle.

### A5. Sichere Beendigungsreihenfolge

- `DefaultAutoUpdateHostTerminator` darf den Host nicht stoppen, bevor `IAutoUpdateProcessRunner` den Script-Prozess gestartet hat.
- Da der Script-Prozess detached und selbst loggend ist, muss der Host nicht auf das Script-Ende warten.
- Das Script muss selbststaendig weiterlaufen und `update.log` befuellen, ohne von der Anwendung abhaengig zu sein.

### A6. Ereignisse für die Anwendung auslösbar

- `IAutoUpdateEventAggregator.BeforeStartUpdateScript` und `AfterStartUpdateScript` muessen vor dem Host-Stop ausgeloest werden.
- `ErrorOccurred` muss mit dem Pfad zu `update.log` und der Fehlerphase ausgeloest werden, bevor der Host verschwindet.

## Nicht durch VideoWebPlayer lösbar

- `VideoWebPlayer` kann das generierte Script und den Ablauf der Installation nicht selbst protokollieren oder beeinflussen, da dies vollstaendig in `msTools.Updater` stattfindet.
- `VideoWebPlayer` kann lediglich `release-metadata.json` korrekt befuellen und Update-Events loggen. Die eigentliche Diagnosefaehigkeit muss in der Bibliothek entstehen.

## Akzeptanzkriterien

1. Nach einem fehlgeschlagenen Update liegt auf dem Server das vollstaendige generierte Installationsscript und das dazugehoerige `update.log` vor.
2. Das heruntergeladene Paket ist im Fehlerfall im `PendingDirectory` noch vorhanden.
3. `update.log` enthaelt den vollstaendigen Pfad der Paketdatei, alle `systemctl` Exit-Codes und den Exit-Code der Entpackoperation.
4. Nach dem naechsten Host-Start zeigt `IAutoUpdateStatusProvider` den Fehler an und verweist auf `update.log`.
5. `DefaultAutoUpdateHostTerminator` stoppt den Host erst, nachdem der Script-Prozess gestartet wurde.

## Betroffene msTools.Updater-Komponenten

- `AutoUpdateScriptGenerator`
- `DefaultAutoUpdateProcessRunner`
- `IAutoUpdatePackageStore` / `FileSystemAutoUpdatePackageStore`
- `AutoUpdateWorkspaceInitializationService`
- `DefaultAutoUpdateHostTerminator`
- `AutoUpdateStatusService` / `IAutoUpdateStateStore`
- `AutoUpdateOrchestrator`

## Referenzen

- `VideoWebPlayer/appsettings.json`: `AutoUpdate:StopHostAfterScriptStart = true`
- `VideoWebPlayer/VideoWebPlayer.csproj`: `GenerateReleaseMetadata` Target
- `update.json` Manifest: `assetName` je Plattform
- `msTools.Updater` Doku: `IAutoUpdatePackageStore`, `IAutoUpdateProcessRunner.StartScript`, `IAutoUpdateEventAggregator`, `AutoUpdateOptions.StopHostAfterScriptStart`
