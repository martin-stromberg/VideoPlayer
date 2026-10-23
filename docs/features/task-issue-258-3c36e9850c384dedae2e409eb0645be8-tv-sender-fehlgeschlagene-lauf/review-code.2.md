# Code-Review

## Ergebnis

**Status:** Befunde vorhanden

## Befunde

### TVSenderAdminE2ETests.cs (TVSenderAdminE2ETests)

- **Testqualität** — `await Expect(failedRows.First).ToContainTextAsync(FailedProbeAttempts.ToString());` (Zeile 172) prüft die Versuche-Spalte nur per Substring-Match auf „10" im gesamten Zeilentext. Der Zeitstempel der Zeile (Format `dd.MM. HH:mm:ss`) kann „10" ebenfalls enthalten (z. B. „30.10.", „14:10:32" oder Sekunde „…:10"), sodass die Assertion auch grün ist, wenn die Versuche-Zelle den Wert nicht oder falsch anzeigt. Dieselbe schwache Assertion steht — unverändert, gleiche Befundklasse — in `TVSenderAdminTests.cs` Zeile 143 (`Assert.Contains(MaxAttempts.ToString(), failedRows[0].TextContent)`).
  Empfehlung: Die „Versuche"-Zelle gezielt mit exaktem Vergleich prüfen, z. B. `await Expect(failedRows.First.Locator("td").Nth(2)).ToHaveTextAsync(FailedProbeAttempts.ToString());` bzw. im bUnit-Test `failedRows[0].QuerySelectorAll("td")[2].TextContent` auf Gleichheit asserten.

### MediaProbeTestDoubles.cs (StubMediaSourceReader)

- **XML-Dokumentation / Konsistenz** — `<paramref name="openFileStream"/>` (Zeilen 35–37) steht in der Typ-Zusammenfassung von `StubMediaSourceReader`. `paramref` ist nur in Member-Dokumentation auflösbar; der Parameter gehört zum Konstruktor, nicht zum Typ, sodass Doc-Generatoren den Tag nicht auflösen (alle übrigen `paramref`-Verwendungen in der Codebasis stehen korrekt in Methoden-/Konstruktor-Doku).
  Empfehlung: `<paramref name="openFileStream"/>` durch `<c>openFileStream</c>` ersetzen oder den Hinweis in eine `<param>`-Doku am Konstruktor verschieben.

## Geprüfte Dateien

- `VideoWebPlayer/Services/MediaDurationProbeProgress.cs`
- `VideoWebPlayer/Services/MediaDurationProbeService.cs`
- `VideoWebPlayer/Components/Pages/Admin/TVSenderAdmin.razor`
- `VideoWebPlayer.Tests/Helpers/MediaProbeTestDoubles.cs`
- `VideoWebPlayer.Tests/Helpers/ProgramScheduleTestDb.cs`
- `VideoWebPlayer.Tests/Services/MediaDurationProbeServiceTests_Progress.cs`
- `VideoWebPlayer.Tests/Services/MediaDurationProbeServiceTests_Reset.cs`
- `VideoWebPlayer.Tests/Services/MediaDurationProbeWorkerTests.cs`
- `VideoWebPlayer.Tests/Components/TVSenderAdminTests.cs`
- `VideoWebPlayer.Tests/TVSenderAdminE2ETests.cs`
