# Einrichtung im VideoWebPlayer

Diese Hilfe richtet sich an Administratoren. Der Einrichtungsbereich bündelt die administrativen Seiten der Webanwendung unter dem Menüpunkt `Einrichtung`.

## Bereiche

Die Startseite der Einrichtung zeigt Kacheln für die wichtigsten Verwaltungsaufgaben:

- `Quellen` für SFTP- und lokale Medienquellen
- `Backups` für Backup, Upload, Restore und Aufbewahrung
- `Updates` für Programmupdates und Update-Einstellungen
- `Sicherheit` für blockierte IP-Adressen
- `Geräte` für Pairing-Codes und gekoppelte Geräte
- `Genres` für Genre-Metadaten, Synonyme und Icons
- `Allgemein` für Anwendungstitel und Scan-Intervalle
- `Anwender` für die Registrierung neuer Benutzer

Nur Administratoren können den Einrichtungsbereich öffnen.

## Allgemein

Unter `Allgemein` werden globale Anzeige- und Scan-Einstellungen gepflegt. Der Anwendungstitel steuert die Bezeichnung in der Navigation und auf der Startseite. Die Scan-Intervalle bestimmen, wie oft die Anwendung nach Prozess- und Medienänderungen sucht.

## Quellen

Unter `Quellen` werden die Medienquellen der Anwendung verwaltet. Beim Anlegen über `Neu` wählt der Administrator im Feld `Quelltyp` zwischen `SFTP-Server` (entfernte Quelle mit Host, Port, Pfad und Zugangsdaten) und `Lokales Verzeichnis` (Verzeichnis auf dem Server, auch UNC-Pfad). Die Übersicht zeigt in der Spalte `Typ` die Kennzeichnung `SFTP` oder `Lokal`. Details stehen in der Hilfe zu den [Medienquellen](medienquellen/index.md).

## Quellen löschen

Über `Quellen` kann eine Medienquelle dauerhaft entfernt werden. Der Löschvorgang läuft asynchron ab, damit die Seite nicht blockiert:

- Klick auf `Löschen` blendet die Aktionsbuttons der betroffenen Quelle aus und zeigt einen Fortschrittsbalken.
- Im Hintergrund werden alle zugehörigen Medien-Items, Collections, Verknüpfungen, Filme, Serien, Staffeln, Episoden und Genres sowie die Berechtigungen gelöscht.
- Nach erfolgreichem Löschen verschwindet die Zeile aus der Übersicht bzw. man wird auf die Übersicht zurückgeleitet.
- Bei einem Fehler wird dieser auf der Seite angezeigt und die Löschung wird nicht ausgeführt.

## Geräte

Unter `Geräte` wird die Kopplung von Client-Apps (z. B. der TV-App) verwaltet.

- Über `Pairing-Code erzeugen` wird ein kurzlebiger Einmal-Code erstellt, der einmalig im Klartext angezeigt wird und standardmäßig 5 Minuten gültig ist. Der Code wird in der App eingegeben; die App tauscht ihn gegen ein individuelles Geräte-Token.
- Die Tabelle der aktiven Codes zeigt nur Metadaten (Erstellt, Gültig bis, Ersteller); der Klartext-Code ist danach nicht mehr einsehbar.
- Die Liste `Gekoppelte Geräte` zeigt Name, Ausstellungszeitpunkt, letzte Verwendung und Status jedes Geräts.
- `Umbenennen` ändert den Anzeigenamen eines Geräts; `Widerrufen` sperrt das Geräte-Token sofort: Folgeanfragen mit diesem Token werden abgelehnt. Bereits angemeldete App-Sitzungen bleiben bis zum Ablauf ihres Anmeldetokens (12 Stunden) gültig.
- Zu viele Fehlversuche beim Einlösen sperren die Client-IP; die Sperre ist unter `Sicherheit` sichtbar und kann dort aufgehoben werden.

Details stehen in der Hilfe zu den [Geräten](geraete/index.md).

## Migration und Backups

Bestehende Installationen erhalten beim Datenbankupdate einen Anwendungstitel mit dem Standardwert `Martins Videosammlung`. Backups aus älteren Versionen können auch dann wiederhergestellt werden, wenn sie noch keine `UpdateSettings`-Tabelle oder keinen Anwendungstitel enthalten.
