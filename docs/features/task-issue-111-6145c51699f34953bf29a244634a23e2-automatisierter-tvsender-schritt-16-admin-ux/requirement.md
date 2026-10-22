# Übersetzte Anforderung: TV-Sender-Adminseite restrukturieren + Probe-Detail-Listen (N13+N14)

Quelle: `docs/projects/task/issue-111-6145c51699f34953bf29a244634a23e2-automatisierter-tvsender/nachbesserungen.md` §N13, §N14.
Maßgebliches, vom Anwender abgenommenes Layout: `docs/projects/task/issue-111-6145c51699f34953bf29a244634a23e2-automatisierter-tvsender/preview/admin-ux-vorschau.html` (statisches Mockup, „1:1 sinngemäß" umzusetzen; verbleibt als Referenz im Repo).

## Fachliche Zusammenfassung

Die Administrationsseite `/admin/tv-sender` (`TVSenderAdmin.razor`) wird von einer
linearen Vier-Karten-Seite in eine Tab-Struktur mit vier Bereichen überführt:
**Regeln** (Regel-Tabelle + einklappbarer Regel-Editor), **Sendeplan** (Sendeplan-Tabelle
+ „Jetzt generieren"), **Einstellungen** (Sender-/Planungs-/Segmentierungsparameter) und
**Laufzeitermittlung** (bisherige Fortschrittskarte). Zusätzlich zeigt die
Laufzeitermittlung aufklappbare Detail-Tabellen der **ausstehenden** und der
**dauerhaft fehlgeschlagenen** Dateien (Name, Quelle, Anzahl Versuche, Zeitpunkt des
letzten Versuchs), damit Administratoren beurteilen können, welche Dateien scheitern.

## Betroffene Klassen und Komponenten

- `VideoWebPlayer/Components/Pages/Admin/TVSenderAdmin.razor` — Umstrukturierung des
  Markups in vier Tab-Panes; Regel-Editor aus dem Overlay-Dialog in eine einklappbare
  Card im Regeln-Tab; „▾ Details"-Schaltflächen und Detail-Tabellen in der
  Laufzeitermittlung. Tab-Zustand rein per Razor-State (kein JavaScript).
- `VideoWebPlayer/Services/MediaDurationProbeProgress.cs` — `MediaDurationProbeProgress`
  um zwei Listen (`PendingItems`, `FailedItems`) erweitern; neuer Eintragstyp mit
  Name, Quelle, Versuchen und letztem Versuchszeitpunkt (kann `null` sein, wenn noch
  nie probiert).
- `VideoWebPlayer/Services/MediaDurationProbeService.cs` — `GetProgressAsync` um die
  beiden limitierten Listenabfragen erweitern (analog der `RecentItems`-Abfrage):
  ausstehend = `Duration == null` und `DurationProbeAttempts < EffectiveMaxAttempts`
  (deckt sich mit `PendingCount`); fehlgeschlagen = `Duration == null` und
  `DurationProbeAttempts >= EffectiveMaxAttempts` (deckt sich mit `FailedCount`).
- `VideoWebPlayer/wwwroot/app.css` — neue Klassen für Tabs (`.admin-tabs`, `.admin-tab`,
  `.tab-pane`), einklappbare Editor-Card, Probe-Detail-Bereiche und „▾ Details"-Buttons
  im Stil der bestehenden `.admin-*`-Farbpalette.
- `VideoWebPlayer.Tests/TVSenderAdminE2ETests.cs` — bestehender Playwright-E2E-Test muss
  an die Tab-Struktur angepasst und um Tab-/Detail-Assertions erweitert werden.
- `VideoWebPlayer.Tests/Components/` — neuer bUnit-Test für Tab-Struktur und
  Detail-Listen der Laufzeitermittlung.
- `VideoWebPlayer.Tests/Services/MediaDurationProbeServiceTests_Progress.cs` — Tests für
  die neuen Listen in `GetProgressAsync`.
- `docs/help/tv-sender.md` — Hilfetext an die Tab-Struktur und die neuen
  Detail-Tabellen anpassen.

## Implementierungsansatz

- Tab-Umschaltung über Razor-Feld (`activeTab`), Panes bleiben im DOM und werden per
  CSS-Klasse `active` ein-/ausgeblendet (`display:none` auf inaktiven Panes) — so bleiben
  Formularzustände beim Tab-Wechsel erhalten und es gibt keinen Re-Load.
- Regel-Editor: bestehender `@if (editRule != null)`-Block wird als
  `admin-card rule-editor-card` im Regeln-Tab gerendert statt als
  `admin-dialog-overlay`; alle Bindings, IDs und Handler bleiben unverändert
  (`#rule-name`, `#rule-save`, `#rule-delete-confirm` usw.). Der Lösch-Bestätigungsdialog
  bleibt ein Overlay-Dialog.
- Detail-Listen: „▾ Details"-Button in den Chips „ausstehend"/„fehlgeschlagen" toggelt
  einen Razor-Bool, der das `open`-Attribut eines `<details class="probe-detail">`-Blocks
  steuert (nativ aufklappbar, kein JS). Tabellen nur rendern, wenn Einträge vorhanden;
  Kappung der Listen mit Hinweiszeile „… und N weitere".
- Fortschritts-Refresh (15 s `PeriodicTimer` über `InvokeAsync`-Dispatcher, Fix aus
  Schritt 11) bleibt unverändert.
- Serverseitige Absicherung bleibt bestehen: `RequireAdminAsync` in
  `GetProgressAsync` deckt die neuen Listen automatisch ab; die Seite rendert ohne
  Admin-Claim weiterhin „Nicht autorisiert".

## Akzeptanzkriterien

1. Die Seite zeigt vier Tabs „Regeln", „Sendeplan", „Einstellungen",
   „Laufzeitermittlung"; „Regeln" ist initial aktiv; jeder Tab zeigt genau seinen
   bisherigen Inhalt — kein Funktionsverlust (alle bisherigen IDs, Buttons,
   Statusmeldungen, Diagnose-Spalte, Auto-Refresh, Signal-Pushes bleiben).
2. Der Regel-Editor ist eine einklappbare Card im Regeln-Tab (öffnet über „Neue Regel"
   bzw. „Bearbeiten", schließt über „Schließen"/„Abbrechen"); das Mockup-Layout
   (Gruppierung der Felder in `admin-settings-grid`-Blöcken) wird sinngemäß
   übernommen.
3. Die Laufzeitermittlung zeigt „▾ Details"-Schaltflächen an den Chips „ausstehend"
   und „fehlgeschlagen"; aufklappende `<details>`-Bereiche listen die betroffenen
   Dateien mit Name, Quelle, Versuchen und letztem Versuchszeitpunkt; Listen sind
   serverseitig limitiert mit Hinweis auf weitere Einträge.
4. `GetProgressAsync` liefert `PendingItems` und `FailedItems` konsistent zu
   `PendingCount`/`FailedCount`, weiterhin nur für Administratoren.
5. Kein Funktionsverlust: E2E-Lifecycle-Test (Regel anlegen/bearbeiten/deaktivieren/
   löschen, Einstellungen speichern, Sendeplan generieren, Probe-Karte) läuft über
   die Tab-Struktur hinweg; neue Struktur und Detail-Listen sind durch
   bUnit-/E2E-Tests abgedeckt.
6. `dotnet build VideoPlayer.sln` in Debug und Release fehlerfrei;
   `dotnet test VideoWebPlayer.Tests` grün.

## Konfiguration

Keine neuen Konfigurationseinträge. Das Listen-Limit der Detail-Tabellen wird als
Parameter mit Standardwert an `GetProgressAsync` übergeben (kein
`appsettings`-Eintrag).

## Offene Fragen

Keine — das Layout ist per abgenommenem Mockup festgelegt; N13s optionale
„Probe zurücksetzen"-Schaltfläche ist nicht Teil des Mockups und wird nicht
umgesetzt (in `nachbesserungen.md` explizit als optional markiert).
