# Übersetzte Anforderung: Gültigkeitszeitraum für Programmregeln

## Fachliche Zusammenfassung

Die Programmregeln (`ProgramRule`) des automatischen TV-Senders (`TVChannel`) erhalten eine vom Sende-Rhythmus (`ProgramRule.Recurrence`: täglich, wöchentlich, einmalig, Feiertage, Werktags, Wochenende) unabhängige Gültigkeitseinstellung. Je Regel ist wählbar: uneingeschränkte Gültigkeit (Standard, entspricht dem bisherigen Verhalten), ein einmaliger Gültigkeitszeitraum mit Von-Datum und Bis-Datum oder ein jährlich wiederkehrendes Gültigkeitsfenster (nur Monat und Tag relevant, z. B. 01.12 bis 31.12). Außerhalb ihrer Gültigkeit erzeugt eine Regel keine Sendeplätze im Sendeplan. Eine Regel mit abgelaufenem einmaligem Zeitraum wird automatisch gelöscht; eine wiederkehrende Regel, die aktuell nicht gültig ist, bleibt erhalten und wird in der Regelauflistung der Verwaltung ans Ende sortiert.

## Betroffene Klassen und Komponenten

### Datenmodell

- `VideoWebPlayer/Data/ProgramRule.cs` — neue Eigenschaften (Namensvorschläge): ein Gültigkeitsmodus (z. B. `ValidityMode`) sowie die Fenstergrenzen `ValidFrom`/`ValidTo` (`DateOnly?`, beim jährlichen Fenster werden nur Monat und Tag ausgewertet).
- Neuer Enum (z. B. `VideoWebPlayer/Data/ProgramRuleValidityMode.cs`) mit den Werten uneingeschränkt / einmaliger Zeitraum / jährlich wiederkehrend.
- `VideoWebPlayer/Data/Configurations/ProgramRuleConfiguration.cs` — Pflicht/Optionalität und Defaults der neuen Spalten (Default: uneingeschränkt gültig, damit Bestandsdaten unverändert gültig bleiben).
- Neue EF-Migration (`VideoWebPlayer/Migrations/`).

### Logikklassen / Services

- `VideoWebPlayer/Services/ProgramRuleInput.cs` — der Create/Update-Record muss die neuen Gültigkeitsfelder aufnehmen.
- `VideoWebPlayer/Services/ProgramRuleService.cs` — `ValidateRuleInputAsync` (Von-/Bis-Pflicht und Reihenfolge je Modus), `CreateRuleAsync`, `UpdateRuleAsync` sowie die Sortierung in `GetRulesAsync` (aktuell ungültige wiederkehrende Regeln ans Ende).
- `VideoWebPlayer/Services/ProgramScheduleService.cs` — Gültigkeitsprüfung beim Ausbau des Sendeplans: `BuildOccurrences` (Zeile ~1615) bzw. die Regelfilterung in `EnsureCoverageAsync` (~Zeile 83), `ReconcileAsync` (~Zeile 169) und `GetRuleDiagnosesAsync` (~Zeile 447) berücksichtigen das Gültigkeitsfenster zusätzlich zu `IsActive`.
- Automatische Löschung abgelaufener Regeln: natürlicher Ausführungsort ist der regelmäßige Worker-Lauf (`ProgramScheduleWorker` ruft `ReconcileAsync`/`EnsureCoverageAsync` intervall- und signalgesteuert auf); alternativ eine eigene Bereinigungsmethode am `IProgramScheduleService`/`IProgramRuleService`. `ProgramSlot.ProgramRuleId` ist bewusst eine Soft-Referenz ohne FK — bereits eingeplante Sendeplätze bleiben als Historie erhalten und werden vom nächsten Reconcile-Lauf wie bei manueller Löschung auf `Displaced` gesetzt (`ProgramScheduleService`, ~Zeilen 289 und 1117). `ProgramRuleCursor`-Zeilen kaskadieren per FK mit.
- Wiederverwendbarer Mechanismus: die jährlich wiederkehrende Datumsfenster-Logik existiert bereits für saisonale Genres (`VideoWebPlayer/Services/SeasonalGenreWindow.cs`, `GenreCanonicalResolver.IsVisible`, `Genre.StartDate`/`EndDate`) — inklusives Datumsfenster, das per `AddYears` ins laufende Jahr verschoben wird. Die Regel-Gültigkeit sollte dieselbe Semantik nutzen, ggf. durch Extraktion/Verallgemeinerung der Fenster-Logik.

### Interfaces

- `IProgramRuleService` — ggf. neue Methode für das Bereinigen abgelaufener Regeln oder erweiterte Doku der geänderten `GetRulesAsync`-Sortierung.
- `IProgramScheduleService` — nur falls die Löschung dort als eigener Schritt angeboten wird.

### UI-Komponenten

- `VideoWebPlayer/Components/Pages/Admin/TVSenderAdmin.razor` — Regel-Editor-Dialog (`RuleEditModel`, ~Zeile 2325): Auswahl der Gültigkeitsart plus Datumsfelder (Von/Bis) bzw. Monat-Tag-Eingaben für das Jahresfenster; `SaveRuleAsync` (~Zeile 2053) füllt das erweiterte `ProgramRuleInput`; `StartEdit`/`StartCreate` (~Zeilen 1884–1921) befüllen die neuen Felder; Regelliste zeigt den Gültigkeitsstatus an (`RuleMetaLabel`/`RecurrenceLabel`, ~Zeilen 2245–2278) — ungültige wiederkehrende Regeln erscheinen am Ende der Liste.

### Tests

- `VideoWebPlayer.Tests/ProgramRuleServiceTests_Validation` — Validierung der neuen Eingaben.
- Neue Tests der Fenster-Logik: Tagesgrenzen (inklusive/exklusive), Jahreswechsel, einmalig vs. wiederkehrend, Sortierung von `GetRulesAsync` (ungültige wiederkehrende Regeln hinten), automatisches Löschen abgelaufener Regeln (incl. Verhalten der verbleibenden Sendeplätze).
- `VideoWebPlayerBackupData`-Regressionstest: Alt-Backup ohne die neuen Spalten stellt Regeln als uneingeschränkt gültig wieder her (`OptionalRestoreColumns` + Default-Eintrag in `VideoWebPlayerBackupData.cs`, ~Zeilen 51/101/169).
- Ggf. `TVSenderAdminE2ETests` um den neuen Dialogabschnitt und die End-Sortierung erweitern.
- Hilfe-Dokumentation unter `docs/help/` für den TV-Sender ergänzen.

## Implementierungsansatz

- Die Gültigkeit wird als zusätzliche Dimension modelliert, nicht als neuer Wert von `ProgramRuleRecurrence`: `Recurrence` bestimmt weiterhin die Sendetage innerhalb der Gültigkeit, die Gültigkeit begrenzt den Gesamtzeitraum. `BuildOccurrences` prüft pro Tag zusätzlich, ob dieser innerhalb des Gültigkeitsfensters liegt.
- Einmaliger Zeitraum: Von- und Bis-Datum (`DateOnly`, inklusive — Annahme, analog zum Genre-Fenster); nach Überschreiten des Bis-Datums gilt die Regel als abgelaufen und wird beim nächsten Bereinigungslauf gelöscht.
- Jährlich wiederkehrend: Fenster aus Monat/Tag zu Monat/Tag; die Auswertung erfolgt taggenau gegen das laufende Jahr (Mechanik von `SeasonalGenreWindow.GetExpiry`/`GenreCanonicalResolver.IsVisible`). Aktuell außerhalb des Fensters liegende Regeln erzeugen keine Vorkommen, bleiben aber bestehen.
- Zeitbasis: Tagesvergleiche auf lokale Datumswerte (konsistent zu `BuildOccurrences`, das lokale Tage über `TimeZoneInfo.Local` bildet) — Annahme.
- Sortierung: `GetRulesAsync` liefert weiterhin die komplette Liste, gruppiert gültige (und uneingeschränkte) Regeln zuerst und aktuell ungültige wiederkehrende Regeln ans Ende, innerhalb der Gruppen wie bisher nach Name — Annahme, serverseitig umzusetzen.
- Löschung: im Zuge des Worker-Laufs (hinter dem `TvSenderEnabled`-Flag) oder als eigener Purge-Schritt; nach dem Löschen ist der Sendeplan neu zu bewerten (`IProgramScheduleSignal.NotifyScheduleChanged()` bzw. der ohnehin folgende `ReconcileAsync`-Lauf), damit geplante Sendeplätze der entfernten Regel konsistent verdrängt werden. Log-Ausgabe analog `DeleteDisplacedSlotsAsync`.
- Backup/DoD: neue Spalten in `VideoWebPlayerBackupData.OptionalRestoreColumns` samt Default-Eintrag und Alt-Backup-Regressionstest; EF-Migration nach dem üblichen Muster.

## Konfiguration

Konfiguration pro Datensatz (je `ProgramRule`) im Regel-Editor der `TVSenderAdmin`-Seite. Es ist keine anwendungsweite oder benutzerspezifische Einstellung erforderlich. Der Default „uneingeschränkt gültig" stellt Abwärtskompatibilität für Bestandsdaten und Alt-Backups sicher. Die Bereinigungsfrequenz ergibt sich implizit aus dem vorhandenen Sendeplan-Intervall (`GetProgramScheduleIntervalAsync`), eine separate Konfiguration wird nicht gefordert.

## Offene Fragen

1. **Grenzen inklusive?** Gelten Von-Datum und Bis-Datum jeweils einschließlich (Regel gültig am Bis-Datum)? Annahme: ja, analog zu den saisonalen Genre-Fenstern.
2. **Jahresfenster über den Jahreswechsel** (z. B. 20.12–06.01): Muss ein Fenster unterstützt werden, das Silvester überschreitet, oder muss das Fenster innerhalb eines Kalenderjahrs liegen? Die bestehende Genre-Mechanik verschiebt das komplette Fenster ins Folgejahr; ein über Silvester reichendes Fenster erfordert eine geteilte Auswertung.
3. **Beziehung zu `Recurrence.Once`/`SingleDate`:** Ist der neue „einmalige Gültigkeitszeitraum" unabhängig vom bisherigen einmaligen Sendedatum zu verstehen (z. B. tägliche Ausstrahlung nur im Zeitraum)? Annahme: ja — gilt ein `SingleDate` außerhalb des Gültigkeitsfensters, entsteht keine Ausstrahlung. Alternativ denkbar: Der einmalige Gültigkeitszeitraum ersetzt `Recurrence.Once` fachlich.
4. **Zeitpunkt der automatischen Löschung:** Nur beim Worker-Lauf (dann pausiert das Löschen bei deaktiviertem `TvSenderEnabled`-Flag) oder auch beim Laden der Regelliste/bei Service-Aufrufen, damit abgelaufene Regeln in der Admin-Ansicht sofort verschwinden?
5. **Offene Grenzen:** Erlaubt der einmalige Zeitraum auch nur ein Von- oder nur ein Bis-Datum, oder sind beide Pflicht? Annahme: beide Pflicht, wie in der Anforderung formuliert.
6. **Kennzeichnung:** Reicht die End-Sortierung aktuell ungültiger wiederkehrender Regeln, oder soll die Liste den Gültigkeitsstatus zusätzlich anzeigen (Chip/Label, z. B. „ungültig bis 30.11.")? Annahme: eine Kennzeichnung ist sinnvoll.
7. **Nur jährlich?** Ist „wiederkehrend" ausschließlich jährlich gemeint, oder sollen weitere Perioden (z. B. monatlich) vorgesehen werden? Annahme: nur jährlich.
8. **Geltungsbereich:** Gilt die Gültigkeit für alle `ProgramRuleType`s einschließlich `Filler`-Regeln? Annahme: ja.
