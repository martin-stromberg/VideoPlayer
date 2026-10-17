# Antworten zu den offenen Punkten der Updater-Entwicklung (IIS-Updatepfad)

Bewertung der empfohlenen Vorschläge aus Sicht des beobachteten IIS-Update-Fehlschlags
(`D:\Dashboard\VideoPlayer`, 04.10.2026, msTools.Updater 0.10.4-rc.1). Bezugs-Issues:
martin-stromberg/msTools.Updater#69 (Windows-`update.log`), martin-stromberg/msTools.Updater#70
(IIS-Pfad ohne erhöhte Rechte).

## Punkt 1 — Vorrang der Auflösung

**Empfohlener Vorschlag:** `AppPoolName` → IIS-Erkennung → `ServiceName` → `ExecutablePath` → Probe.

**Bewertung: Zustimmung.** Die Reihenfolge ist richtig: Unter IIS sind `ServiceName` und
`ExecutablePath` funktionslos bzw. kontraproduktiv (Incident: `webplayer.service` → Skript für
Windows-Dienste → spurloser Abbruch, weil `Stop-Service` ins Leere lief). Die IIS-Erkennung über
die ANCM-Umgebungsvariablen (`ASPNETCORE_TOKEN`/`ASPNETCORE_PORT` für Out-of-Process,
`ASPNETCORE_IIS_*` für In-Process) ist falschpositiv-sicher — kein anderes Hosting-System setzt
diese Variablen; die Gefahr, einen funktionierenden Dienst-Deployment versehentlich als IIS zu
erkennen, ist praktisch null.

**Ergänzungen:**

- `ExecutablePath` unter IIS ist nicht nur wirkungslos, sondern aktiv schädlich: Das Skript würde
  die App als eigenständigen Prozess außerhalb des Site-Kontexts starten. Die Warnmeldung für
  ignorierte Optionen sollte das benennen, nicht nur „wirkungslos".
- Das Warn-Log für überstimmte Optionen sollte den konkreten Grund nennen („unter IIS nicht
  wirksam/schädlich"), damit die Fehlkonfiguration für den Betreiber korrigierbar bleibt.

## Punkt 2 — Entkopplungsmechanismus und Rechte der Pool-Identität

**Empfohlener Vorschlag:** WMI `Win32_Process.Create` via `System.Management`; Fehlschlag →
`InstallationFailed` mit klarer Meldung; Vorab-Verifikation auf der Zielmaschine.

**Bewertung: Zustimmung — mit Präzisierung der Verifikation.** Direkt auf der betroffenen
Maschine verifiziert: `Invoke-CimMethod Win32_Process -MethodName Create` liefert als
**nicht-administrativer** Benutzer `ReturnValue 0`, der Prozess läuft (Testdatei wurde
geschrieben). Der Mechanismus ist grundsätzlich ohne Adminrechte möglich und erzeugt den Prozess
außerhalb des IIS-Job-Objects — er überlebt damit Host-Stop und Pool-Recycle.

**Ergänzungen:**

- Die Vorab-Verifikation muss gezielt `Win32_Process.Create` ausführen — *Abfragen* der Klasse
  (`Get-CimInstance Win32_Process`) ist für Nicht-Admins erlaubt und sagt nichts über `Create`
  aus; `Create` ist die kritische Operation.
- Der Verifikationstest sollte unter der echten Pool-Identität laufen (Test-App-Pool oder
  `psexec`-Variante), nicht als interaktiver Benutzer: `IIS AppPool\<name>`-Identitäten laufen in
  Session 0 ohne Benutzerprofil, DCOM-Whitelisting kann anders greifen. Der am 04.10. geloggte
  Befund (Skriptprozess ohne ein einziges Engine-Event gestorben) spricht dafür, dass der
  Kindprozess im Job des Worker-Prozesses mitgerissen wurde — die Entkopplung ist keine
  Optimierung, sondern Voraussetzung.
- Implementierung in .NET benötigt das Zusatzpaket `System.Management` (Windows-only) —
  Abhängigkeitskosten bewusst einpreisen; alternativ P/Invoke/WMI-COM (deutlich hässlicher).
- Fallback, falls `Create` in einer Umgebung doch verweigert wird: Task-Scheduler-Einmaltask unter
  der eigenen Identität — aber erst als zweiter Pfad, nicht parallel implementieren.

## Punkt 3 — Umfang des #69-Nachzugs

**Empfohlener Vorschlag:** `update.log` für alle drei Windows-Skripte, gemeinsames Fragment.

**Bewertung: Zustimmung.** Sobald die Entkopplung das stdout/stderr-Capture für alle `.ps1`-Starts
entfernt, wären alle Windows-Pfade diagnostisch blind. Die Log-Hilfsfunktion als gemeinsames
Fragment hält den Aufwand klein; das Linux-`write_log`/`capture`-Muster ist die Vorlage.

**Ergänzung:** Punkt L3 aus #69 nicht vergessen — die Workspace-Recovery räumt
`pending\update.ps1` beim nächsten Start weg; Skript (oder Archivkopie) und `update.log` müssen
nach einem Fehlschlag zur Inspektion erhalten bleiben.

## Punkt 4 — Restart-Race

**Empfohlener Vorschlag:** `app_offline.htm` vor dem Terminieren schreiben, nach dem Tausch
entfernen.

**Bewertung: Zustimmung — der Vorschlag ist sogar besser als er dasteht.** `app_offline.htm` ist
der dokumentierte IIS-Mechanismus für genau diesen Zweck und löst zusätzlich einen Teil der
Stopp-Logik mit:

- **Out-of-Process:** ANCM terminiert den Backend-Prozess (`VideoWebPlayer.exe`) beim Erscheinen
  von `app_offline.htm` selbstständig — der Kill aus dem Skript entfällt komplett, Locks sind nach
  dem Backend-Exit frei. Requests bekommen den Offline-Content statt Verbindungsfehler.
- **In-Process:** `app_offline.htm` fährt die App herunter und verhindert Neustarts, aber die
  geladenen Assemblies bleiben im `w3wp` gelocked (der Default-`AssemblyLoadContext` kann nicht
  entladen) — dort ist das Terminieren des `w3wp` weiterhin nötig (Pool-Identität darf den eigenen
  Prozess terminieren, kein Admin nötig).

**Pflichtdetails:**

- `app_offline.htm` gehört zwingend in einen `try/finally`- bzw. `trap`-gesicherten Abschnitt:
  Bleibt die Datei nach einem Skriptabbruch liegen, ist die Site **dauerhaft** offline — ein
  schlimmerer Fehlermodus als der jetzige Zustand.
- Nach dem Schreiben auf den tatsächlichen Backend-Exit warten (PID-Poll mit Timeout), nicht auf
  `Start-Sleep`-Werte verlassen.
- Zielpfad: Site-Root = `ApplicationDirectory` für Out-of-Process; für In-Process ist
  `ASPNETCORE_IIS_PHYSICAL_PATH` die sichere Quelle.

## Punkt 5 — In-Process-Nebenwirkung (geteilter App-Pool)

**Empfohlener Vorschlag:** Dokumentierte Voraussetzung (dedizierter Pool je Anwendung), Hinweis in
`update.log` und Doku, kein Sonderverhalten im Code.

**Bewertung: Zustimmung, mit Eingrenzung.** Die Nebenwirkung betrifft nur In-Process — unter
Out-of-Process entfällt der `w3wp`-Kill durch `app_offline.htm` vollständig (siehe Punkt 4), der
Pool und seine anderen Sites bleiben unangetastet. Als dokumentierte Voraussetzung für den
In-Process-Pfad reicht das aus; ein `update.log`-Hinweis („Pool kann mehrere Sites enthalten")
hilft bei der Nachanalyse.

## Punkt 6 — Echte IIS-Verifikation

**Empfohlener Vorschlag:** Manueller Verifikationstest auf lokalem IIS (beide Hosting-Modelle,
nicht-administrative Pool-Identität), automatisiert bleiben Content-Assertions.

**Bewertung: Zustimmung.** Ein echter IIS-Durchlauf ist in der automatisierten Suite nicht
sinnvoll reproduzierbar; die Trennung ist richtig.

**Ergänzungen:**

- Das manuelle Protokoll als Checkliste/Skript ablegen, damit es reproduzierbar bleibt (Site mit
  Testpool anlegen → Update auslösen → Dateiversionen, `update.log`, Offline-Verhalten und
  Wiederanlauf prüfen).
- Automatisiert bleiben zusätzlich sinnvoll: Content-Assertions auf das generierte Skript
  (Reihenfolge `app_offline` → Stopp → Tausch → Aufräumen → `app_offline`-Entfernung) und eine
  reale Skriptausführung gegen eine „tote" PID — analog dem `GeneratedScript_...`-Testmuster im
  VideoPlayer-Repo.
- Mindestens einmal die Fehlpfade manuell durchspielen (Abbruch mitten im Tausch): `app_offline.htm`
  muss entfernt und `update.lock` freigegeben werden bzw. der Zustand muss aus `update.log`
  rekonstruierbar sein.

## Zusammenfassung

| # | Punkt | Bewertung |
|---|-------|-----------|
| 1 | Vorrang IIS-Erkennung vor `ServiceName`/`ExecutablePath` | Übernehmen; Warngrund konkretisieren |
| 2 | WMI-Entkopplung | Übernehmen; `Create` (nicht Query) unter echter Pool-Identität verifizieren; `System.Management`-Paket einpreisen |
| 3 | `update.log` für alle Windows-Pfade | Übernehmen; Skript-Erhalt aus #69 L3 mitziehen |
| 4 | `app_offline.htm` gegen Restart-Race | Übernehmen; macht Backend-Kill für Out-of-Process überflüssig; `try/finally` + Exit-Poll zwingend |
| 5 | Geteilter Pool (In-Process) | Übernehmen; betrifft nur In-Process |
| 6 | Manuelle IIS-Verifikation | Übernehmen; Protokoll als Checkliste ablegen, Fehlpfade einmal manuell durchspielen |
