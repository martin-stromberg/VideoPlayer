# Anforderung — Issue #348: TV-Sender: On-Air

GitHub-Issue: martin-stromberg/VideoPlayer#348 (Kommentar „@softwarewerkstatt Bau dies bitte ein.").

Original-Anforderung: „Für den TV-Sender wird ein „On-Air"-Schalter benötigt, mit welchem der
Stream aktiviert und wieder deaktiviert werden kann, unabhängig von der allgemeinen
Funktionsaktivierung."

## Fachliche Zusammenfassung

Der automatisierte TV-Sender erhält einen zweiten, von der globalen Funktionsfreischaltung
(`Setup.TvSenderEnabled`) unabhängigen Admin-Schalter „On Air". Er steuert ausschließlich den
HLS-Live-Stream: Im ausgeschalteten Zustand läuft keine ffmpeg-Segmentierung und der
Stream-Endpunkt liefert keinen Stream aus, während Sendeplan-Generierung, Programmauskunft,
Laufzeitermittlung und die Verwaltungsseiten unverändert weiterlaufen. Die bisherige
Funktionsaktivierung bleibt die äußere Hürde — „unabhängig" bedeutet: Der Stream lässt sich
abschalten, ohne das ganze Feature samt Sendeplan und Programmauskunft mit abzuschalten.

## Fachlicher Scope

- **Verarbeitungseinheit:** Der Live-Stream des TV-Senders — die HLS-Auslieferung über
  `LiveStreamController` (`/api/live-stream/*`) samt der sie erzeugenden ffmpeg-Segmentierung
  (`LiveStreamWorker` → `LiveStreamCoordinator` → `ILiveStreamSegmenter`). Nicht zur Einheit
  gehören: Sendeplan-Generierung (`ProgramScheduleWorker`), Programmauskunft
  (`/api/program-guide/*`), Laufzeitermittlung (`MediaDurationProbeWorker`), die
  Benutzer-Freigabeliste (`TvSenderUserAccess`) und die Verwaltungsseiten
  (`/admin/tv-sender`, `Einrichtung → Allgemein`).
- **Auslösende Ereignisse:** Ein Administrator stellt den „On-Air"-Schalter in der
  Verwaltung um; der neue Stand wird serverseitig persistiert und wirkt ohne Neustart —
  eine laufende Segmentierung stoppt bzw. eine erlaubte startet beim nächsten
  Arbeitstakt, vorzeitig geweckt über `IProgramScheduleSignal.NotifyScheduleChanged()`
  (gleiches Muster wie bei `Setup.TvSenderEnabled`).
- **Erwartete Wirkung:** Off-Air → ein laufender ffmpeg-Lauf wird beendet, keine neue
  Segmentierung wird gestartet, `/api/live-stream/*` liefert keinen Stream aus; auf der
  Zuschauer-Seite `/live-tv` ist ein verständlicher „nicht auf Sendung"-Zustand sichtbar.
  On-Air → der `LiveStreamWorker` setzt die Segmentierung am nächsten Takt an der dann
  aktuellen Programmposition fort (die zeitbasierte Resynchronisation existiert bereits).
  Sendeplan, Programmauskunft und alle übrigen TV-Sender-Funktionen bleiben in beiden
  Stellungen unverändert betriebsbereit.
- **Umgang mit fehlenden Objekten:** Die `Setups`-Zeile existiert bereits (Single-Row-Tabelle,
  `ProgramSettingsService.GetOrCreateSetupAsync`); die neue Schalter-Spalte kommt per
  EF-Migration mit Default-Wert in die bestehende Tabelle — keine neue Entität nötig.
  Alt-Backups ohne die Spalte werden über `VideoWebPlayerBackupData`
  (`OptionalRestoreColumns`, `OptionalRestoreBoolDefaults`) auf denselben Default
  zurückgeführt.
- **Scope-Ausschlüsse:**
  - Kein zeit- oder regelgesteuerter Sendebetrieb: Es gibt nur den manuellen Schalter, keinen
    automatischen On-Air-Fahrplan.
  - Die Funktionsfreischaltung `Setup.TvSenderEnabled` bleibt unverändert die äußere Hürde:
    Ist das Feature nicht freigeschaltet, erreicht kein Weg den Stream — der On-Air-Schalter
    gilt innerhalb der Freischaltung und kann das Feature nicht teilweise „an" schalten.
  - Kein Eingriff in Sendeplan oder Programmauskunft: Der Plan wird auch Off-Air weiter
    erzeugt und bleibt abfragbar (anders als bei `TvSenderEnabled`, das alles stoppt).
  - Die Benutzer-Freigabeliste (`TvSenderUserAccess`) bleibt unverändert; der On-Air-Schalter
    ersetzt oder erweitert keine Benutzerberechtigung.
  - `InternalMediaStreamController` (`/api/internal/media-stream/*`) bleibt vom On-Air-Schalter
    unberührt — der Endpunkt wird auch von der Laufzeitermittlung genutzt und folgt weiter
    allein `TvSenderEnabled` (Annahme).
  - Kein eigener „Off-Air"-Beitrag im Programm und keine Sendeplan-Änderung: Ausgestrahlte
    Sendeplätze werden Off-Air nicht markiert oder umgeplant.

## Betroffene Klassen und Komponenten

- `VideoWebPlayer/Data/Setup.cs` — neue bool-Eigenschaft (Arbeitsname `TvSenderOnAir`);
  dazu eine EF-Migration und der Eintrag in `ApplicationDbContextModelSnapshot`.
- `VideoWebPlayer/Services/ProgramSettingsService.cs` — Lesen/Schreiben des Schalters
  (analog `GetTvSenderEnabledAsync` / `UpdateTvSenderEnabledAsync`); ggf. Erweiterung von
  `GeneralSettingsUpdate`, falls der Schalter dort landet.
- `VideoWebPlayer/Services/LiveStreamWorker.cs` — zusätzliches Gate neben
  `GetTvSenderEnabledAsync` (Zeile ~96): Bei Off-Air einen laufenden `ILiveStreamSegmenter`
  stoppen und den Poll-Takt beibehalten.
- `VideoWebPlayer/Controllers/LiveStreamController.cs` — Gate vor der
  Segment-/Standby-Auslieferung (Reihenfolge: `TvSenderEnabled` → Benutzerfreigabe → On-Air).
- UI-Schalter: `VideoWebPlayer/Components/Pages/Admin/TVSenderAdmin.razor` (Tab
  „Einstellungen", nahe der Karte „Segmentierung") oder
  `VideoWebPlayer/Components/Pages/Admin/ProgramSettings.razor` (unterhalb der
  Funktionsfreischaltung) — Platzierung siehe Offene Fragen.
- `VideoWebPlayer/Components/Pages/LiveTv.razor` sowie `LiveTvErrorMessages` und ggf.
  `wwwroot/js/liveTv.js` — Off-Air-Anzeige statt Player.
- `VideoWebPlayer/Services/Backups/VideoWebPlayerBackupData.cs` — neue Spalte in
  `OptionalRestoreColumns` und `OptionalRestoreBoolDefaults` (Backup-Kompatibilität, DoD).
- `docs/help/tv-sender.md`, `docs/help/tv-sender-ablauf-technisch.md`, `README.md`,
  `docs/RELEASE_NOTES.md` — Dokumentation.
- Tests (`VideoWebPlayer.Tests`): `LiveStreamWorkerTests`, `Controllers/LiveStreamControllerTests`,
  `Components/LiveTvTests`, `ProgramSettingsServiceTests`,
  `Services/Backups/VideoWebPlayerBackupDataTests_*` (Regressionstest: Alt-Backup ohne die
  Spalte), E2E über `LiveStreamE2ETests` / `LiveTvE2ETests` / `TVSenderAdminE2ETestBase`.

## Implementierungsansatz

- Gleiches etabliertes Muster wie `Setup.TvSenderEnabled`: bool-Spalte in der
  Single-Row-Tabelle `Setups`, Zugriff über `ProgramSettingsService`; die Auswertung erfolgt
  je Arbeitstakt bzw. je Request und wirkt damit ohne Neustart.
- `LiveStreamWorker.ExecuteAsync` prüft den On-Air-Stand in derselben Verzweigung wie das
  Feature-Flag: `TvSenderEnabled && OnAir` → `LiveStreamCoordinator.SynchronizeAsync`;
  sonst laufenden Segmenter idempotent stoppen (`segmenter.IsRunning` → `StopAsync`). Das
  `IProgramScheduleSignal` weckt den Takt beim Umschalten sofort.
- `LiveStreamController.GetFile` verweigert Off-Air die Auslieferung — naheliegend mit 404
  wie das Feature-Gate (Alternativverhalten: siehe Offene Fragen). Der Standby-Pfad
  (`LiveStreamStandbyProvider`) bleibt dabei Teil der Off-Air-Antwort-Entscheidung.
- `LiveTv.razor` liest den On-Air-Stand serverseitig (über `ProgramSettingsService`, gleiche
  Stelle wie `GetTvSenderEnabledAsync` in `OnInitializedAsync`) und zeigt statt des Players
  einen verständlichen Hinweis; die Programmauskunft-Karten können unverändert weiterlaufen,
  da `/api/program-guide/*` nicht betroffen ist.
- Die Berechtigung wird serverseitig erzwungen: Der Schalter steht nur Administratoren offen
  (die Verwaltungsseiten sind ohnehin admin-only; für einen etwaigen separaten
  Update-Endpunkt gilt dieselbe Hürde wie bei `UpdateTvSenderEnabledAsync`).

## Konfiguration

Anwendungsweite Admin-Einstellung in der `Setups`-Tabelle (Single-Row), persistiert als
bool-Spalte — keine benutzerspezifische und keine `appsettings.json`-Ebene. Da aktuell genau
ein `TVChannel` ausgeliefert wird, genügt die globale Setup-Spalte; soll der Zustand später
kanalfähig sein, wäre stattdessen eine Spalte an `TVChannel` denkbar (aktuell nicht
erforderlich).

## Offene Fragen

- **Default-Wert des Schalters:** Empfehlung `true` (On Air), damit Bestandsinstallationen und
  Alt-Backups ihr bisheriges Verhalten behalten — die Funktionsfreischaltung bleibt ohnehin
  ab Werk aus. Alternativ `false` als bewusst konservativer Auslieferungszustand. Klären.
- **Antwort des Stream-Endpunkts Off-Air:** 404 (Feature ununterscheidbar von „kein Stream",
  konsistent zum Feature-Gate) oder eine dedizierte Off-Air-Auslieferung (z. B. Standby-Clip
  bzw. eigenes Hinweis-Asset)? Klären — beeinflusst auch, ob `LiveStreamStandbyProvider`
  Off-Air weiter vorgehalten wird.
- **Zuschauer-Darstellung Off-Air auf `/live-tv`:** Eigener Hinweis („Der Sender ist zurzeit
  nicht auf Sendung.") im Player-Bereich bei weiterhin sichtbarer Programmauskunft — oder
  soll die Seite weiterhin den Player mit Standby-/Fehlerverhalten zeigen? Klären.
- **Platzierung und Sofortwirkung des Schalters:** Tab „Einstellungen" in
  `TVSenderAdmin.razor` (gebündelt, wirkt mit „Speichern") oder prominenter, sofort wirkender
  Umschalter im Kopf der TV-Sender-Verwaltung bzw. unter `Einrichtung → Allgemein`? Klären.
- **Aufräumen beim Abschalten:** Soll das Segment-Verzeichnis beim Off-Air-Schalten geleert
  werden (analog `LiveStreamSegmentStore.ClearAsync` beim Worker-Start), oder genügt das
  Gate am Endpunkt? Klären.
- **Begrifflichkeit „aktiviert":** Die Anforderung sagt „Stream aktiviert/deaktiviert" —
  Annahme: Deaktivieren stoppt den ffmpeg-Lauf und gibt Ressourcen frei, nicht nur die
  Auslieferung. Falls „deaktiviert" nur die Zuschauer-Auslieferung bei weiterlaufender
  Segmentierung meinen sollte, wäre das ein anderer (teurerer) Zielzustand — klären.
