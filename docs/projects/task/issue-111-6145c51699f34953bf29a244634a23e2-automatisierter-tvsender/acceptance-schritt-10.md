# Abnahme Schritt 10: Planungsdiagnose + manueller Generierungsanstoß (N5)

Prüfer: Devin (unabhängiger Lauf, nicht der Implementierer)
Datum: 2026-10-21
Geprüfter Stand: `ed196a4` auf `task-issue-111-...-schritt-10-diagnose`

## Ergebnis

**Status:** Erfüllt

Die Anforderung aus `nachbesserungen.md` §N5 ist fachlich und technisch korrekt umgesetzt;
der Implementierer-Bericht wurde anhand des Codes, eigener Builds/Testläufe und zweier
Gegenproben verifiziert.

### Geprüfte Kernpunkte

- **Admin-only serverseitig:** `GetRuleDiagnosesAsync` (`ProgramScheduleService.cs` Z. 335–364)
  beginnt mit `RequireAdminAsync` (Z. 366–375): unbekannter User → `UnauthorizedAccessException`,
  Nicht-Admin → `ForbiddenAccessException`. Erzwingung liegt im Service, nicht nur an der
  ausgeblendeten Schaltfläche. Durch `Diagnosis_NonAdmin_Throws` belegt — und durch Gegenprobe
  verifiziert (s. u.).
- **Dieselbe Expansion wie der Planlauf:** Die Diagnose iteriert alle Regeln des Kanals
  (inkl. inaktiver, `.Include(Genres)/.Include(MediaSourceFilters)`, `AsNoTracking`) und ruft
  pro Regel `ExpandMaterialAsync` mit frischen `SummaryCounters` auf — identischer Codepfad wie
  `EnsureCoverageAsync`/`ReconcileAsync`; `ToSummary()` und die öffentlichen Run-Summaries sind
  unverändert (neue Counter nur intern konsumiert).
- **Gründe fachlich korrekt:** `CountNoDuration` (Z. 987–1003) unterscheidet
  `NoMediaItem` (kein `MediaItemRef`) / `ProbeExhausted`
  (`DurationProbedAt != null && DurationProbeAttempts >= _maxProbeAttempts`) / `ProbePending`
  (sonst). Semantik stimmt mit `MediaDurationProbeService` überein: permanentes Scheitern setzt
  `attempts = Max(attempts+1, maxAttempts)` (Z. 216), Retry-Eligibility gilt nur unterhalb
  `max` (Z. 97–98), `DurationProbedAt` wird bei jedem Versuch gesetzt (Z. 220). `MaterialMissing`
  wird an allen relevanten Early-Returns gesetzt (Show/Movie/Collection/Playlist gelöscht;
  Genre: keine Stored-Zeilen, keine kanonischen Namen, leere effektive Genre-Ids nach
  Quell-Filter). `NoAccessCount` = Kandidaten mit Laufzeit ohne Freigabe fürs Gesamtpublikum.
- **MaterialId-Lookup-Verhalten unverändert:** `MovieMediaItemsAsync`/`EpisodeMediaItemsAsync`
  filtern nicht mehr auf `Duration != null`, wählen aber laufzeitbehaftete Items bevorzugt,
  Tie-Break = kleinste `MediaItemId` — für den Planlauf identisches Ergebnis wie zuvor.
- **UI:** `Planbar`-Spalte (`.tvsender-rule-diagnosis`) mit `DiagnosisLabel` deckt alle Gründe
  ab („… planbar", „ohne Mediendatei", „ohne Laufzeit (Ermittlung läuft/gescheitert)",
  „nicht für alle freigegeben", „Material nicht gefunden (verwaist)", „Keine Inhalte im
  Material"); Diagnose wird bei jedem `ReloadRulesAsync` neu berechnet.
- **„Jetzt generieren":** `#tvsender-generate-now` → `GenerateNowAsync` ruft
  `IProgramScheduleSignal.NotifyScheduleChanged()` (Z. 492), wartet 4 s, lädt Sendeplan +
  Diagnosen neu, meldet über `#tvsender-schedule-status`; `generatePending`/`disposed` sauber
  gehandhabt (`IDisposable`). Das Signal selbst ist durch `ProgramScheduleSignalTests`/
  Worker-Tests abgesichert; der E2E belegt den Klickpfad bis zur Status-Rückmeldung.
- **Tests:** 10 neue Integrationstests (`ProgramScheduleServiceTests_Diagnosis.cs`, echtes
  SQLite) — Schedulable/NoMediaItem/ProbePending (nie geprobt + unter Max)/ProbeExhausted/
  NoAccess/MaterialMissing (Entity gelöscht + Genre auflösungsleer)/inaktive Regel/Non-Admin.
  E2E `Admin_CreateEditDeactivateDeleteRule` um Diagnose-Zelle + Button erweitert — konsolidiert
  im bestehenden Test, kein neuer Browser-Start.
- **Doku:** `docs/help/tv-sender.md` (Abschnitt „Planungsdiagnose und manueller Anstoß") und
  `docs/RELEASE_NOTES.md` aktualisiert. Keine Migration nötig (rein berechnet, kein Schema).

### Builds & Tests (selbst ausgeführt)

- `dotnet build VideoPlayer.sln` (Debug): **0 Fehler**, 142 Warnungen — Baseline, keine
  Warnung in geänderten/neuen Dateien.
- `dotnet build VideoPlayer.sln -c Release`: **0 Fehler**, identische Warnungslage.
- `dotnet test VideoWebPlayer.Tests/VideoWebPlayer.Tests.csproj` (synchron, vollständig):
  **2008 grün, 0 Fehler, 1 bekannter Alt-Skip**
  (`DevicePlaylistE2ETests_Playback.PairedDevice_ReportsProgress...`, SKIP-Bedingung).
  Der vom Implementierer dokumentierte E2E-Flake
  (`Admin_CreateEditDeactivateDeleteRule`, Settings-Race Z. 49, Schritt-7-Code, unverändert)
  trat in diesem Lauf **nicht** auf — Einordnung als Baseline-Flake bleibt plausibel, die neuen
  Assertions des Tests (Diagnose-Zelle, Button, Statustext) liefen fehlerfrei.
- **Gegenprobe 1:** `RequireAdminAsync`-Aufruf aus `GetRuleDiagnosesAsync` entfernt →
  `Diagnosis_NonAdmin_Throws` schlägt fehl (`No exception was thrown`). → `git checkout --`.
- **Gegenprobe 2:** `DurationProbeAttempts >= _maxProbeAttempts` auf `>` geändert →
  `Diagnosis_ExhaustedProbe_CountsProbeExhausted` schlägt fehl (0 statt 1), die übrigen 9
  Diagnose-Tests bleiben grün — die Pending/Exhausted-Grenze ist exakt dort abgesichert.
  → `git checkout --`. Arbeitsstand danach wieder sauber (`git status` leer).

## Abweichungen

- [ ] Keine sperrenden Abweichungen.

## Hinweise

- **Log-Rauschen:** `ExpandMaterialAsync` loggt bei `items.Count == 0` weiterhin „liefert kein
  planbares Material" — jetzt auch bei jeder Diagnose-Berechnung (also bei jedem Laden der
  Admin-Seite pro leerer Regel). Fachlich harmlos, ggf. später dedizieren.
- **Playlist-Regel mit ausschließlich verwaisten Einträgen:** verwaiste `PlaylistEntries`
  (referenziertes Medium gelöscht) werden in `ExpandPlaylistAsync` (Z. 1273) still übersprungen
  und zählen nicht als Kandidat — die Zelle zeigt dann „Keine Inhalte im Material" statt
  „verwaist". Vertretbar (Material = Playlist existiert), deckt aber den Wortlaut
  „Material verwaist" für diesen Unterfall nicht ab.
- **Repräsentant-Heuristik:** Bei mehreren `MediaItem`-Links ohne `Duration` klassifiziert der
  Eintrag mit kleinster `MediaItemId` — ein Item mit erschöpftem Budget kann „gescheitert"
  melden, obwohl ein anderes Link desselben Inhalts noch pending ist. Im Code kommentiert;
  Edge-Case.
- **4-s-Warte im Button:** `GenerateNowAsync` wartet fest 4 s und lädt dann neu; die
  Statusmeldung erscheint auch, wenn der Worker-Lauf länger braucht (Hinweistext im UI deckt
  das ab). Kein Polling, kein Signal-Rückkanal — bewusst einfach gehalten.
- **Diagnose-Kosten:** Die Berechnung läuft synchron über alle Regeln pro Seitenaufruf
  (on-demand, nichts persistiert). Bei sehr großen Bibliotheken/vielen Regeln spürbar —
  entspricht aber bewusst dem Plan („on-demand über denselben Expansionscode").
- **Prozess:** Die Bearbeiter-Tabelle in `handover.md` enthält keine Schritt-10-Zeile (Schritt 9
  steht noch auf „aktiv"), einen `### Schritt 10`-Übergabeabschnitt gibt es nicht — die
  Regel „Bearbeiter eintragen + Handover pflegen" (AGENTS.md §2/§5) wurde nicht eingehalten.
  Fachlich ohne Auswirkung; im Projekt nachzupflegen.
- **Teststand vs. Bericht:** Implementierer meldet „2007 grün + 1 Flake"; bei der Prüfung lief
  die Suite mit **2008 grün** durch (Flake trat nicht auf). Testzahl konsistent (2009 gesamt,
  1 Alt-Skip).
