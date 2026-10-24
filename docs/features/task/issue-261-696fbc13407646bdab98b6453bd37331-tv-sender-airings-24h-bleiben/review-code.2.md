# Code-Review

## Ergebnis

**Status:** Befunde vorhanden

## Befunde

### ProgramScheduleService.cs (ReconcileWindow)

- **Fehlende Kapselung / Zustandskonsistenz** — `ReconcileWindow` wurde eingeführt, damit nach
  einem verbreiterten Reload keine Struktur des schmaleren Fensters versehentlich weiterverwendet
  wird. Der Vertrag bleibt aber halb offen: `Rebuild()` (Zeilen 821–842) rechnet alle
  ladeabhängigen Strukturen neu, lässt jedoch die `Validate()`-Produkte `InvalidGroups` und
  `HasRescuableInvalid` unangetastet — ein `Rebuild()` ohne anschließendes `Validate()` bedient
  die Aufrufer danach still mit dem Validierungsergebnis des alten Fensters (genau die
  Fehlerklasse, die die Klasse beseitigen sollte). Zusätzlich steht die Sequenz
  `LoadFrom`-Setzen + `Scheduled`-Nachladen + `Rebuild()` + `Validate()` an zwei Stellen nahezu
  identisch inline (Zeilen 201–205 und 214–220), realisiert über die öffentlichen Setter
  `LoadFrom`/`Scheduled` — ein dritter Reload-Punkt müsste die Viererfolge erneut fehlerfrei
  wiederholen.

  Empfehlung: Den Zustandsübergang in eine Methode des Fensters verlagern, z. B.
  `window.WidenToAsync(DateTime from)` bzw. `window.Reload(DateTime from, List<ProgramSlot>
  scheduled)`, die `LoadFrom`/`Scheduled` setzt und `Rebuild()` + `Validate()` atomar ausführt —
  beide Aufrufstellen schrumpfen auf je eine Zeile, die Setter können intern werden. Mindestens
  sollte `Rebuild()` `InvalidGroups`/`HasRescuableInvalid` zurücksetzen, damit ein vergessenes
  `Validate()` keine veralteten Ergebnisse ausliefert.

## Geprüfte Dateien

- `VideoWebPlayer/Services/ProgramScheduleService.cs`
- `VideoWebPlayer/Configuration/ProgramScheduleOptions.cs`
- `VideoWebPlayer.Tests/Helpers/ProgramScheduleTestDb.cs`
- `VideoWebPlayer.Tests/ProgramScheduleServiceTests_Reconcile.cs`
- `VideoWebPlayer.Tests/ProgramScheduleServiceTests_Interleaving.cs`
- `VideoWebPlayer.Tests/ProgramScheduleServiceTests_Boundary.cs`
- `docs/help/tv-sender.md` (stichprobenartig)
- `docs/RELEASE_NOTES.md` (stichprobenartig)

## Verifikation der Befunde aus Runde 1 (alle behoben)

Am tatsächlichen Code geprüft (`git diff HEAD` + vollständige Dateien):

1. **Closure-`null!`-Variablen:** Alle acht versteckten Out-Parameter sind in `ReconcileWindow`
   gewandert (`_invaderStarts`, `_occurrenceStarts`, `Occurrences`, `BoundStarts`, `Airings`,
   `FillerSlots`, `InvalidGroups`, `HasRescuableInvalid`, Zeilen 808–819). Kein `null!` mehr;
   `Rebuild()`/`Validate()` werden an beiden Reload-Stellen paarweise aufgerufen (207–208,
   218–219). Alle Aufrufer lesen ausschließlich über `window.*` (Zeilen 236, 247, 272, 278, 291,
   317–326).
2. **Long Parameter List:** `ReproContext` (Zeilen 474–480) bündelt `State`, `OccurrenceStarts`,
   `BoundStarts`, `InvaderStarts`, `MinStartRemain` und `Memo`. Beide Blattmethoden destrukturieren
   vollständig (Zeilen 523, 604) und reichen `context` in allen Rekursionsstufen unverändert
   weiter (Zeilen 585, 738) — das Memo wird korrekt über die gesamte Rekursion geteilt.
3. **Namenskonventionen:** `FlatAiringTailReproducible`/`InterleavedAiringTailReproducible`
   symmetrisch; Memo-Typ mit Tupelelementnamen `(int Offset, DateTime AfterStart)` an Record-
   Deklaration und Konstruktion einheitlich.
4. **Obergrenze:** `EffectiveMaxOccurrenceLookback` klemmt `Math.Min(MaxOccurrenceLookbackDays, 30)`
   (ProgramScheduleOptions.cs Zeile 49); Normalisierung durch
   `ProgramScheduleOptions_MaxOccurrenceLookback_Normalizes` abgedeckt (inkl. `int.MaxValue`).
5. **Tautologische Assertion:** `Reconcile_LongAiring_BeyondTwoDays_StaysScheduled` prüft jetzt
   `Assert.All(await fixture.SlotsAsync(ct), s => Assert.Equal(Scheduled, s.Status))` — echte
   DB-weite Assertion über alle Slots einschließlich historischer.
6. **Doppelter Dump-Code:** `ProgramScheduleTestDb.AssertNoDisplacedAsync` (Zeile 277) baut den
   Dump nur im Fehlerfall; alle fünf bisherigen Inline-Blöcke (Reconcile.cs ×3, Boundary.cs,
   Interleaving.cs) und alle neuen Tests nutzen den Helper, Kontext-Strings (`"Lauf {i}"`,
   `"2. Lauf"`) gehen über den optionalen `context`-Parameter.

## Anmerkungen zur Korrektheit (keine Befunde)

- **`BuildOccurrences` static:** Die Methode nutzte zuvor ausschließlich statischen Zustand
  (`TimeZoneInfo.Local`, `OccurrenceLookback`); der neue `lookback`-Parameter ist an beiden
  Aufrufstellen korrekt (Zeile 115 mit `OccurrenceLookback`, Zeile 832 mit
  `coverageStart - LoadFrom`). Die `windowStartDay`-Umstellung von `Tag(coverageStart) - 1` auf
  `Tag(coverageStart - lookback)` ist für das 24-h-Fenster äquivalent (im DST-Grenzfall max. ein
  zusätzlicher Schleifentag, der `startUtc >= coverageStart - lookback`-Filter bleibt maßgeblich)
  und deckt für das breite Fenster exakt den gefilterten Bereich ab.
- **`ReproContext`-Vollständigkeit:** Memo-Key `(offset, afterStart)` bleibt ausreichend — alle
  übrigen Parameter sind rekursionsinvariant im Record; rekursive Aufrufe verwenden
  `afterStart = occurrenceStart` des jeweiligen Eltern-Ankers (streng aufsteigend, Z. 555, 649).
- **`states[rule.Id]` in `Validate()` (Z. 861):** sicher, da `rulesById` und `states` aus derselben
  `rules`-Liste befüllt werden (Zeilen 144, 165–170).
- **`HasRescuableInvalid` (Z. 865):** wird nur bei invalidem Airing einer aktiven Regel gesetzt —
  genau die Fälle, die ein breiteres Fenster retten kann; inaktive/gelöschte Regeln bleiben
  zurecht unrettbar.
- **Materialisierungsfenster (Z. 317):** `Where(o => o.StartUtc >= coverageStart -
  OccurrenceLookback)` hält die Neuplanung bewusst auf dem Basisfenster (Oszillations-Kommentar),
  während `window.Occurrences`/`BoundStarts` die Verbreiterung für Anker und Grenzen nutzen.
- **EF-Tracking beim Reload:** `LoadScheduledAsync` ist tracking-gebunden; überlappende Zeilen
  liefern dieselben Entitätsinstanzen, Referenzen in `Airings`/`InvalidGroups` bleiben konsistent,
  Status-Mutationen (`Displace`, `DisplaceRuleFuture`) greifen auf denselben Instanzen.
- **`AssertNoDisplacedAsync`:** Signatur und Nur-im-Fehlerfall-Dump korrekt; `Assert.Fail` statt
  `Assert.True(false, …)`; Kompilieren und Aufrufe verifiziert (Solution-Build Debug fehlerfrei,
  0 Warnungen — `dotnet build` beider Projekte).
- **Tests:** Neue Tests sind verhaltensbasiert (öffentliche `ReconcileAsync`/`EnsureCoverageAsync`
  gegen SQLite-Fixture), decken Trigger-Pfad, validierungsgetriebene Verbreiterung, verschobene
  Starts, Interleaved-Ketten, die Max-Lookback-Grenze (`MaxOccurrenceLookbackDays = 1`) und echte
  Staleness (geänderte `TimeOfDay`) ab.
- **Docs:** `tv-sender.md` dokumentiert das Verhalten und den neuen Konfigurationsschlüssel
  inkl. 30-Tage-Deckel; `RELEASE_NOTES.md` in beiden Sprachabschnitten ergänzt.
