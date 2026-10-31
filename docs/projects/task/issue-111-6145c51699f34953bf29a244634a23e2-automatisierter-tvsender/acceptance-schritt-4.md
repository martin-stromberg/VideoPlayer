# Abnahme Schritt 4: Sendeplan bei Änderungen nachführen

Prüfer: Devin (unabhängig, ohne Implementierungskontext). Geprüft wurde der Branch
`task-issue-111-6145c51699f34953bf29a244634a23e2-automatisierter-tvsender-schritt-4-sendeplan-nachfuehren`
(HEAD `e5ba78d`) gegen `task/issue-111-6145c51699f34953bf29a244634a23e2-automatisierter-tvsender`.

## Ergebnis

**Status:** Abweichungen gefunden

Die fachliche Umsetzung ist vollständig und korrekt: `ReconcileAsync` validiert den Bestand per
Ausstrahlungs-Simulation (`ProgramScheduleService.cs:109-312`), vergangene Slots (`End <= now`,
`Displace()` Zeile 165) bleiben unangetastet, ungültige werden als `Displaced` markiert statt
gelöscht, Suffix-Verdrängung + Cursor-Rücksetzung (`RewindCursor`, Zeile 406) sichern die
Episodenreihenfolge ohne Sprung, Neu-Episoden werden berücksichtigt, gelöschte Medien verdrängen
nur den eigenen Slot (weiche Referenzen), Lücken werden regelkonform/Füller-gefüllt,
Prioritäts-Eviction funktioniert. Automatisierung: koaleszierendes `ProgramScheduleSignal`
(Bounded-Channel + Scan-Suppression), Trigger an Regel-CRUD, Marker-Hook (neue Episoden/Filme),
`DeleteMediaSourceAsync` und Scan-Ende; Worker läuft hinter `IBackgroundProcessingGate` und fällt
auf das Intervall zurück. Sichtbarkeit: Admin-Karte „Sendeplan" mit Status-Chips (E2E-nachgewiesen).
Backup: `ProgramSlots.Status` in `OptionalRestoreColumns` samt Alt-Backup-Regressionstest;
Migration `AddProgramSlotStatus` vorhanden. Dokumentation (`docs/help/tv-sender.md`,
`docs/RELEASE_NOTES.md`) aktualisiert.

Aber: Zwei neu eingeführte Warnungsgruppen brechen das CI-Gate „Static analysis"
(`pr-staging-ci.yml`: `dotnet build VideoPlayer.sln -c Debug -p:TreatWarningsAsErrors=true` mit
`WarningsNotAsErrors`-Whitelist, die beide Codes nicht enthält). Der forcierte Rebuild belegt
`error CS1573` — lokal inkrementell sieht man das nicht, in der CI (frischer Checkout) schlägt es fehl.

Gegenprobe durchgeführt: Suffix-Verdrängung sabotiert (`ordered.Skip(firstInvalid).Take(1)`) →
5 von 10 Reconcile-Tests schlagen fehl; nach `git checkout --` wieder alle grün. Die Tests
sichern das Verhalten also tatsächlich ab.

Verifikation: `dotnet build VideoPlayer.sln` Debug **und** Release fehlerfrei;
`dotnet test VideoWebPlayer.Tests/VideoWebPlayer.Tests.csproj` komplett:
**1865 erfolgreich, 1 übersprungen, 0 fehlgeschlagen** (4m01s; Skip =
`PairedDevice_ReportsProgressForMovieWithoutCollection_...`, bekannter Alt-Skip; kein Flake).

## Abweichungen

- [ ] `VideoWebPlayer/Data/ApplicationDbContext.cs:27` — der neue Konstruktor-Parameter
  `programScheduleSignal` hat kein `<param>`-Tag im XML-Kommentar → Warnung **CS1573**, die unter
  dem CI-Gate `TreatWarningsAsErrors` als Fehler durchschlägt (CS1573 nicht in
  `WarningsNotAsErrors`). Belegt per Rebuild: `error CS1573 ... kein entsprechendes param-Tag`.
- [ ] `VideoWebPlayer.Tests/ProgramScheduleSignalTests.cs` (Zeilen 18, 29, 41, 43) und
  `VideoWebPlayer.Tests/TVSenderAdminE2ETests.cs` (Zeilen 98, 118) — sechs neue
  **xUnit1051**-Warnungen (`Task.Delay`/`SaveChangesAsync` ohne
  `TestContext.Current.CancellationToken`). Baseline ist für diesen Code sauber, xUnit1051 steht
  nicht in `WarningsNotAsErrors` → schlägt dasselbe CI-Gate fehl. (Direkter Compile-Beweis für
  das Testprojekt ist wegen Abweichung 1 nicht erreichbar — VideoWebPlayer.csproj bricht vorher ab —
  Mechanismus identisch.)

## Hinweise

- **CS1574 `VideoWebPlayer/Data/Setup.cs:74`** (`<see cref="ProgramRuleRecurrence.Holiday"/>` —
  `Setup` liegt im globalen Namespace, der cref löst daher nicht auf) bricht dasselbe CI-Gate,
  stammt aber **nicht** aus Schritt 4 (aus Schritt 2/3, dortige Abnahme hat das Gate offenbar
  nicht geprüft). Sollte zeitnah mitbehoben werden, sonst scheitert der Projekt-Merge ebenfalls.
- **Testlücke (nicht blockierend):** Der Cursor-Rückschreib-Pfad in `RewindCursor`
  (`ProgramScheduleService.cs:421-427`) wirkt nur für **inaktive** Regeln (bei aktiven
  überschreibt `PersistCursors` den Wert anschließend ohnehin korrekt). Das Szenario
  „Regel deaktiviert → Slots verdrängt → reaktiviert → Folge wird ohne Sprung fortgesetzt" ist
  ungetestet — ein Sabotageversuch an genau dieser Stelle blieb von allen 10 Tests unentdeckt.
  Funktional nach Code-Review korrekt, aber ungeschützt.
- `ApplicationDbContext.PlaylistBackfillMarking.cs:94,142` — die neuen
  `_programScheduleSignal?.NotifyScheduleChanged();`-Zeilen sind tiefer eingerückt als der
  umgebende Block (kosmetisch; ohne `.editorconfig` nicht CI-relevant).
- `TVSenderAdmin.razor`: `ReloadScheduleAsync()` läuft nur beim initialen Laden, nicht nach
  Regel-CRUD — die Sendeplan-Karte zeigt bis zum Reload den alten Stand. Vertretbar, da die
  Verdrängung ohnehin asynchron durch den Worker passiert (sofortiges Neuladen würde noch nichts
  zeigen). Die CSS-Klasse `tvsender-slot-displaced` hat keine Style-Regel in `app.css`; die
  Kennzeichnung erfolgt nur über den Chip — für „sichtbar" ausreichend.
- Designentscheidung (deckt sich mit Doku/Release-Notes): Ein **laufender** Slot, der durch die
  Änderung ungültig wird, wird ebenfalls „Verdrängt" markiert (`Displace()` schont nur
  `End <= now`). Die Anforderung verlangt Schutz nur für weiterhin gültige Plätze — konsistent.
- Bulk-Deletes außer `DeleteMediaSourceAsync` (z. B. direkte `ExecuteDelete`-Pfade) feuern kein
  Signal und werden nur über den Intervall-Sweep erkannt — im Handover dokumentiert, durch den
  zustandslosen Sweep fachlich abgesichert.
