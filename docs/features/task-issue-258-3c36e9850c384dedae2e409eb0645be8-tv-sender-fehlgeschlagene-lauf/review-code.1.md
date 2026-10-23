# Code-Review

## Ergebnis

**Status:** Befunde vorhanden

## Befunde

### MediaDurationProbeService.cs (MediaDurationProbeService)

- **Doppelter Code / inkonsistente Prädikatsdefinition** — Die Failed-Menge `mi.Duration == null && mi.DurationProbeAttempts >= maxAttempts` steht viermal wörtlich im Code: `GetProgressAsync` (Zeilen 157–158 und 199–200), `ResetFailedProbeAsync` (Zeilen 428–430) und `ResetFailedProbesAsync` (Zeilen 456–458). Zusätzlich driften die Definitionen auseinander: `ResetFailedProbeAsync` filtert nicht über `VideoExtensionPredicate()`, obwohl die XML-Doku „guarded by the failed-set predicate“ behauptet und die in `GetProgressAsync`/`ResetFailedProbesAsync` verwendete Failed-Menge nur Videoitems umfasst. Ein Nicht-Video-`MediaItem` mit `DurationProbeAttempts >= maxAttempts` ließe sich über den Einzel-Reset zurücksetzen, obwohl es in keiner Failed-Anzeige auftaucht.
  Empfehlung: Eine gemeinsame Ausdrucksfabrik `FailedProbePredicate(int maxAttempts)` analog zu `VideoExtensionPredicate()` extrahieren und an allen vier Stellen verwenden; in `ResetFailedProbeAsync` zusätzlich `VideoExtensionPredicate()` anwenden, damit Einzel- und Massen-Reset dieselbe Menge adressieren.
- **Fehlerbehandlung / Logging-Konsistenz** — `ResetFailedProbesAsync` protokolliert einen erfolgreichen, bewusst ausgelösten Admin-Eingriff mit `LogWarning` (Zeile 464), während `ResetFailedProbeAsync` dieselbe fachliche Aktion mit `LogInformation` meldet (Zeile 436). Warning signalisiert ein Problem und erzeugt Alarmrauschen im Log, obwohl hier nichts fehlschlug.
  Empfehlung: Beide Reset-Methoden einheitlich auf `LogInformation` stellen (oder die höhere Stufe bewusst kommentiert begründen).

### TVSenderAdmin.razor (TVSenderAdmin)

- **Namenskonventionen / Terminologie** — Der Zeilen-Button trägt den Text „Probe zurücksetzen“ (Zeile 590) und mischt das englische „Probe“ in die sonst durchgehend deutsche Oberfläche, die den Vorgang konsequent „Laufzeitermittlung“/„Ermittlungslauf“ nennt (Tab-Titel, Chips, Dialogüberschrift „Fehlgeschlagene Dateien zurücksetzen“, Statusmeldungen in `ResetProbeItemAsync`/`ResetAllFailedProbesAsync`). Außerdem hat der Button als einzige Aktionsschaltfläche der Seite keine Varianten-Klasse (`btn btn-sm tvsender-probe-reset` statt z. B. `btn-outline-light`/`btn-danger` wie alle übrigen Buttons).
  Empfehlung: Buttontext vereinheitlichen, z. B. „Erneut ermitteln“ oder „Zurücksetzen“, und eine zur Aktion passende `btn-*`-Variantenklasse ergänzen.

### MediaDurationProbeServiceTests_Reset.cs (MediaDurationProbeServiceTests_Reset)

- **Doppelter Code** — `SeedItemAsync` (Zeilen 30–41) ist wortidentisch zu `MediaDurationProbeServiceTests_Progress.cs` (Zeilen 27–38); `PresentLocator` (Zeilen 224–227) ist wortidentisch in drei Testdateien (`_Progress`, `_Reset`, `TVSenderAdminTests`), `StubMediaSourceReader` (Zeilen 230–239) unterscheidet sich von den beiden anderen Kopien nur im Rückgabewert von `OpenFileStream`. Damit existieren vier nahezu gleiche Blöcke, die bei einer Interface-Erweiterung von `IMediaSourceReader`/`IFfprobeLocator` an drei bzw. vier Stellen nachgezogen werden müssen.
  Empfehlung: `SeedItemAsync` und die Stubs in einen gemeinsamen Test-Helper unter `VideoWebPlayer.Tests/Helpers` (oder als Methode auf `ProgramScheduleTestDb`) verschieben; den Reader-Stub so gestalten, dass das `OpenFileStream`-Verhalten je Aufrufstelle gesetzt werden kann.

### TVSenderAdminE2ETests.cs (TVSenderAdminE2ETests)

- **Kopplung / hardcodierter Wert** — `DurationProbeAttempts = 10` (Zeilen 99 und 110) dupliziert den Standard `MediaProbeOptions.MaxAttempts = 10`. Ändert sich der Default, seedet der Test still „pending“ statt „failed“ Items und die Assertions auf „2 fehlgeschlagen“ brechen an einer fachlich unzusammenhängenden Stelle.
  Empfehlung: Eine benannte Konstante mit Kommentar („entspricht MediaProbeOptions-Default“) einführen oder den Wert aus `new MediaProbeOptions().EffectiveMaxAttempts` beziehen.
- **Testqualität** — Der neue Reset-Abschnitt (Einzel-Reset plus Massen-Reset mit Dialog, Zeilen 169–188) hängt an der ohnehin mehrere fachliche Fälle prüfenden Methode `Admin_CreateEditDeactivateDeleteRule`; jeder Fehler in den vorgelagerten Szenarien (Regel-Editor, Sendeplan, Einstellungen) verhindert, dass die Reset-Prüfung überhaupt erreicht wird.
  Empfehlung: Die Reset-Strecke als eigenen `[Fact]` auslagern (die Seeding-Blöcke lassen sich in eine kleine Hilfsmethode extrahieren); die Wiederverwendung der App-Instanz bleibt über die Testbasis erhalten.

## Geprüfte Dateien

- `VideoWebPlayer/Services/MediaDurationProbeProgress.cs`
- `VideoWebPlayer/Services/MediaDurationProbeService.cs`
- `VideoWebPlayer/Components/Pages/Admin/TVSenderAdmin.razor`
- `VideoWebPlayer.Tests/Services/MediaDurationProbeServiceTests_Reset.cs`
- `VideoWebPlayer.Tests/Services/MediaDurationProbeServiceTests_Progress.cs`
- `VideoWebPlayer.Tests/Components/TVSenderAdminTests.cs`
- `VideoWebPlayer.Tests/TVSenderAdminE2ETests.cs`
