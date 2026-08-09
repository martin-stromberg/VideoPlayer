# Strukturierte Anforderung: UI fuer Updates

## Metadaten

- Aufgaben-ID: `ca263640-5ef4-43cc-8ccd-94b833a37368`
- Branch: `task/issue-103-ca2636405ef443cc8ccd94b833a37368-ui-fuer-updates`
- Erstellt: 2026-08-09
- Thema: Administrationsoberflaeche und Ablaufsteuerung fuer automatische Updates mit `msTools.Updater`

## Ausgangslage

Die Anwendung nutzt bereits `msTools.Updater` fuer automatisierte Updates. Die vorhandene Update-Funktion ist jedoch noch nicht funktionsfaehig oder noch nicht vollstaendig integriert.

Es existiert inzwischen eine Backup-Funktion. Diese soll vor der Installation einer neuen Version genutzt werden koennen, damit vor einem Update ein Sicherungsstand erzeugt wird.

## Ziel

Administratoren sollen Updates der Anwendung ueber einen neuen Einstellungsbereich konfigurieren, den aktuellen Updatestatus einsehen und Update-Aktionen manuell ausloesen koennen.

Die Update-Installation soll optional automatisiert erfolgen koennen. Vor der Installation soll optional automatisch ein Backup erstellt werden. Fuer den Neustart nach der Installation soll ein Dienstname konfigurierbar sein.

## Zielgruppe und Berechtigung

- Der neue Einstellungsbereich ist fuer Administratoren vorgesehen.
- Nicht-administrative Benutzer sollen keinen Zugriff auf die Update-Einstellungen und Update-Aktionen erhalten.

## Funktionale Anforderungen

### Update-Einstellungsbereich

- Es soll einen neuen Einstellungsbereich fuer Updates geben.
- Der Bereich soll nur Administratoren zur Verfuegung stehen.
- Der Bereich soll alle relevanten Update-Einstellungen anzeigen und bearbeitbar machen.
- Der Bereich soll den aktuellen Updatestatus anzeigen.
- Der Bereich soll Aktionsbuttons fuer manuelle Update-Aktionen enthalten.

### Automatische Pruefung auf neue Versionen

- Administratoren sollen konfigurieren koennen, ob automatisch auf neue Versionen geprueft wird.
- Administratoren sollen ein Pruefintervall fuer die automatische Versionspruefung festlegen koennen.
- Das Pruefintervall soll gespeichert und fuer zukuenftige automatische Pruefungen verwendet werden.

### Prerelease-Versionen

- Administratoren sollen konfigurieren koennen, ob Prerelease-Versionen bei der Suche nach Updates akzeptiert werden.
- Beim Aktivieren der Prerelease-Akzeptanz soll eine Sicherheitsabfrage angezeigt werden.
- Die Sicherheitsabfrage soll darauf hinweisen, dass Prerelease-Versionen experimentell sein koennen.
- Die Einstellung soll erst nach bestaetigter Sicherheitsabfrage aktiviert werden.

### Automatisierte Installation

- Administratoren sollen konfigurieren koennen, ob eine neue Version nach dem Erkennen automatisch installiert wird.
- Wenn die automatisierte Installation deaktiviert ist, soll eine gefundene Version nur angezeigt werden und manuell installierbar sein.
- Wenn die automatisierte Installation aktiviert ist, soll die Anwendung die erkannte neue Version ohne weitere manuelle Installationsaktion installieren koennen.

### Dienstname fuer Neustart

- Administratoren sollen den Dienstnamen angeben koennen, der nach der Installation fuer den Neustart verwendet wird.
- Der konfigurierte Dienstname soll beim Installations- bzw. Neustartablauf beruecksichtigt werden.

### Automatisches Backup vor Installation

- Administratoren sollen konfigurieren koennen, ob vor der Installation automatisch ein Backup erstellt wird.
- Wenn die Einstellung aktiviert ist, soll vor der Installation einer neuen Version die vorhandene Backup-Funktion ausgefuehrt werden.
- Die Installation soll erst nach erfolgreichem Backup fortgesetzt werden.
- Wenn das Backup fehlschlaegt, soll die Installation nicht gestartet werden und der Fehler im Updatestatus sichtbar sein.

### Updatestatus

- Der Einstellungsbereich soll den aktuellen Updatestatus anzeigen.
- Der Status soll mindestens erkennbar machen, ob gerade eine Pruefung oder Installation laeuft, ob eine neue Version verfuegbar ist, ob die Anwendung aktuell ist oder ob ein Fehler aufgetreten ist.
- Relevante Informationen zur gefundenen Version sollen angezeigt werden, soweit sie durch `msTools.Updater` verfuegbar sind.
- Fehler aus Pruefung, Backup, Installation oder Neustart sollen fuer Administratoren nachvollziehbar angezeigt werden.

### Manuelle Aktionen

- Es soll einen Aktionsbutton geben, der eine sofortige Pruefung auf neue Versionen ausloest.
- Es soll einen Aktionsbutton geben, der die Installation der neuen Version ausloest.
- Die Installation per Aktionsbutton soll nur moeglich sein, wenn eine installierbare neue Version bekannt ist.
- Die Buttons sollen waehrend laufender Aktionen gegen parallele Mehrfachausfuehrung abgesichert sein.

## Nicht-funktionale Anforderungen

- Update-Einstellungen muessen persistent gespeichert werden.
- Update-Aktionen duerfen keine inkonsistenten Zustaende erzeugen, insbesondere nicht bei gleichzeitigen manuellen und automatischen Ausloesern.
- Fehler muessen fuer Administratoren sichtbar und technisch ausreichend nachvollziehbar sein.
- Sicherheitsrelevante Einstellungen, insbesondere Prerelease-Akzeptanz und automatische Installation, sollen bewusst durch Administratoren gesetzt werden.

## Akzeptanzkriterien

- Administratoren sehen einen neuen Update-Einstellungsbereich.
- Nicht-Administratoren koennen den Update-Einstellungsbereich nicht nutzen.
- Die automatische Versionspruefung kann aktiviert oder deaktiviert werden.
- Ein Pruefintervall kann konfiguriert und gespeichert werden.
- Prerelease-Versionen koennen nur nach einer Sicherheitsabfrage akzeptiert werden.
- Die automatische Installation bei erkannter neuer Version kann aktiviert oder deaktiviert werden.
- Ein Dienstname fuer den Neustart nach der Installation kann gespeichert werden.
- Automatisches Backup vor der Installation kann aktiviert oder deaktiviert werden.
- Bei aktiviertem automatischem Backup wird vor der Installation ein Backup ausgefuehrt.
- Bei fehlgeschlagenem Backup wird die Installation abgebrochen und der Fehler im Updatestatus angezeigt.
- Der Updatestatus zeigt Pruef-, Installations-, Erfolgs- und Fehlerzustaende an.
- Eine manuelle Pruefung auf neue Versionen kann per Button gestartet werden.
- Eine neue Version kann per Button installiert werden, sofern eine installierbare Version verfuegbar ist.

## Offene Punkte

- Welche konkrete Administrations- bzw. Berechtigungslogik im bestehenden System fuer den Zugriff auf den neuen Einstellungsbereich verwendet werden soll, ist aus der Anforderung nicht ersichtlich.
- Welche Einheit und erlaubten Werte fuer das Pruefintervall verwendet werden sollen, ist nicht festgelegt.
- Welche konkreten Statusinformationen `msTools.Updater` bereitstellt und wie diese gemappt werden sollen, muss in der Bestandsaufnahme geklaert werden.
- Ob der Begriff "Datenupdate" in der Anforderung ein Backup vor der Installation oder eine zusaetzliche Datenmigration meint, muss technisch/fachlich geklaert werden.
- Welche Backup-Funktion konkret aufzurufen ist und welche Parameter sie benoetigt, muss in der Bestandsaufnahme geklaert werden.
- Wie der konfigurierte Dienstname technisch fuer den Neustart verwendet werden soll, muss anhand der bestehenden Update- und Hosting-Architektur geklaert werden.
