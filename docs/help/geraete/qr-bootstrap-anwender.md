← [Zurück zur Übersicht](index.md)

# Gerät per QR-Code koppeln (Self-Service)

Mit dem QR-Bootstrap koppeln Sie die App auf einem neuen Gerät selbst, ohne dort E-Mail-Adresse und Passwort eingeben zu müssen. Ein Administrator ist dafür nicht erforderlich.

## Voraussetzungen

- Sie sind in der Weboberfläche angemeldet.
- Die Client-App ist auf dem zu koppelnden Gerät installiert und erreicht den Server im Netzwerk.

## Schritt-für-Schritt-Anleitung

### 1. Geräte-Seite im Profil öffnen

Öffnen Sie `Profil` und wählen Sie den Menüpunkt `Geräte` (`/Account/Manage/Devices`).

### 2. Ticket erzeugen

Klicken Sie auf `Gerät koppeln`. Die Seite zeigt daraufhin:

- einen QR-Code,
- einen 8-stelligen Kurzcode als Alternative,
- eine kurze Anleitung sowie die verbleibende Gültigkeit (`Gültig bis <Uhrzeit>`).

> **Hinweis:** Das Ticket ist nur einmal verwendbar und läuft standardmäßig nach 5 Minuten ab. Erzeugen Sie bei Ablauf einfach ein neues Ticket.

### 3. Code in der App einlösen

Öffnen Sie die App auf dem Gerät und wählen Sie die Koppelung per QR-Code:

- Scannen Sie den QR-Code — die App übernimmt Adresse und Ticket automatisch, oder
- geben Sie den angezeigten 8-stelligen Kurzcode ein.

Das Gerät erhält dabei automatisch sein eigenes Geräte-Token, eine Benutzersitzung und ein Refresh-Token — eine Passworteingabe ist nicht nötig.

### 4. Hinweis bei localhost-Adresse

Läuft der Server unter `localhost` bzw. `127.0.0.1`, zeigt die Seite einen Warnhinweis: Der QR-Code funktioniert dann nur, wenn das Gerät denselben Rechner erreichen kann. Verwenden Sie in diesem Fall die Netzwerk-Adresse des Servers.

## Ergebnis

Das Gerät ist gekoppelt und direkt einsatzbereit. Gekoppelte Geräte kann ein Administrator unter `Einrichtung` > `Geräte` einsehen, umbenennen und bei Bedarf widerrufen — ein Widerruf sperrt sofort auch alle Refresh-Tokens des Geräts.

## Häufige Fragen

- **„Ticket ungültig oder abgelaufen"**: Das Ticket wurde bereits verwendet oder ist älter als 5 Minuten — erzeugen Sie ein neues.
- **„Zu viele Tickets"**: Pro Benutzer ist die Zahl der Tickets pro Stunde begrenzt; warten Sie, bis das Kontingent wieder frei ist.
- **Kein `Gerät koppeln`-Button sichtbar**: Auf diesem Server ist die Koppelung ggf. auf Administratoren beschränkt (`Pairing:BootstrapAdminOnly`).
