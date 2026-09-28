← [Zurück zur Übersicht](index.md)

# Geräte — Beschreibung

## Zweck

Das Geräte-Pairing koppelt Client-Apps (z. B. die TV-App) individuell an den VideoWebPlayer. Statt eines gemeinsamen Zugangsschlüssels für alle Geräte erhält jedes gekoppelte Gerät ein eigenes Geräte-Token, das sich gezielt widerrufen lässt — etwa wenn ein Gerät verloren geht oder ausgemustert wird.

## Kopplung: zwei Wege

Es gibt zwei Wege, ein Gerät zu koppeln. Beide führen zu einem eigenen, widerrufbaren Geräte-Token.

### 1. Einmal-Pairing-Code (Administrator)

Die Geräteverwaltung liegt im Einrichtungsbereich unter `Einrichtung` > `Geräte` (`/admin/devices`) und ist nur für Administratoren sichtbar.

- Über `Pairing-Code erzeugen` wird ein kurzlebiger Einmal-Code erstellt. Er wird genau einmal im Klartext angezeigt — zusammen mit der Angabe, bis wann er gültig ist (standardmäßig 5 Minuten). Danach ist der Code nicht mehr einsehbar.
- Der Code wird in der App eingegeben. Die App tauscht ihn selbstständig gegen ein Geräte-Token; die Übertragung ist dabei auf Anwendungsebene verschlüsselt.
- Das Gerät erhält auf diesem Weg **nur** ein Geräte-Token. Die Anmeldung eines Anwenders erfolgt danach getrennt über `POST /api/auth/login`.

### 2. QR-Bootstrap (Self-Service, auch ohne Administratorrechte)

Jeder angemeldete Anwender kann sich unter `Profil` > `Geräte` (`/Account/Manage/Devices`) selbst ein Gerät koppeln — dafür ist **kein** Administrator nötig. Die Seite erzeugt auf Knopfdruck ein Einmal-Ticket und zeigt es als QR-Code sowie als 8-stelligen Kurzcode an. Der QR-Code enthält die Serveradresse samt Ticket (`https://<server>/pairing?t=<ticket>`), sodass die App beides zugleich übernimmt.

- Das Ticket ist einmal verwendbar und läuft standardmäßig nach 5 Minuten ab (`Pairing:BootstrapTicketTtlMinutes`).
- Pro Anwender sind standardmäßig 10 Tickets je Stunde möglich (`Pairing:BootstrapMaxTicketsPerHour`); darüber meldet die Seite „Es wurden zu viele Kopplungs-Tickets erzeugt".
- Ist die Einstellung `Pairing:BootstrapAdminOnly` gesetzt (Standard: `false`), dürfen **nur Administratoren** Tickets erzeugen; alle anderen erhalten den Hinweis „Das Koppeln von Geräten ist auf diesem Server nur Administratoren erlaubt."
- Beim Einlösen bekommt das Gerät in einem Schritt drei Dinge: sein Geräte-Token, eine fertige Benutzersitzung (für den Anwender, der das Ticket erzeugt hat) und einen Erneuerungsnachweis. Eine Passworteingabe auf dem Gerät entfällt.

Die Schritt-für-Schritt-Anleitung steht in [Gerät per QR-Code koppeln (Self-Service)](qr-bootstrap-anwender.md).

## Sitzung, Erneuerung und Abmelden

- Die Benutzersitzung eines Geräts ist ein Anmeldetoken mit **12 Stunden** Gültigkeit.
- Läuft sie ab, erneuert das Gerät sie über `POST /api/auth/refresh` mit seinem Erneuerungsnachweis. Dabei wird der Nachweis ausgetauscht (Rotation): Der alte verfällt, das Gerät merkt sich den neuen. Der Nachweis selbst gilt standardmäßig 30 Tage (`Auth:RefreshTokenTtlDays`).
- Wird ein bereits ausgetauschter Nachweis noch einmal vorgelegt, wertet der Server das als Hinweis auf einen entwendeten Nachweis und sperrt **alle** Nachweise dieses Anwenders auf diesem Gerät.
- Beim Abmelden (`POST /api/auth/logout`) wird der vorgelegte Nachweis serverseitig gesperrt; die App verwirft Sitzung, Nachweis und Geräte-Token.
- Ein Anwenderwechsel auf dem Gerät ist möglich: Die App meldet sich mit `POST /api/auth/login` unter einem anderen Konto an und arbeitet danach mit dessen Sitzung — das Geräte-Token bleibt dabei dasselbe.

## Verwaltung und Widerruf

- Die Tabelle `Pairing-Codes` zeigt aktive Codes nur mit ihren Metadaten (`Erstellt`, `Gueltig bis`, `Ersteller`); lässt sich das Ersteller-Konto nicht mehr auflösen (z. B. gelöschter Benutzer), steht dort `Unbekannt`.
- Die Tabelle `Gekoppelte Geräte` listet jedes Gerät mit `Name`, `Ausgestellt`, `Zuletzt verwendet` und `Status` (`Aktiv` oder `Zugriff entzogen`). Über `Umbenennen` lässt sich der Anzeigename anpassen, über `Widerrufen` wird der Zugriff des Geräts gesperrt.
- Der Widerruf muss über eine Bestätigungs-Checkbox (`Widerruf von <Name> serverseitig bestaetigen`) und die Schaltfläche `Widerruf bestaetigen` explizit bestätigt werden.
- Ein Widerruf sperrt sofort das Geräte-Token **und** alle Erneuerungsnachweise des Geräts: Die Sitzungserneuerung wird ab diesem Moment abgelehnt. Die bereits ausgestellte Sitzung arbeitet dagegen bis zu ihrem Ablauf (12 Stunden) weiter.
- Zwei Statistik-Karten oben auf der Seite zeigen die Anzahl gekoppelter Geräte (mit der Zahl der aktiven) und die Anzahl aktiver Codes.

## Geräte und Playlists

- Ein gekoppeltes Gerät sieht über `GET /api/playlists` genau die Playlists **des angemeldeten Anwenders** — bei der QR-Kopplung also zunächst die des Anwenders, der das Ticket erzeugt hat.
- Öffentliche Playlists anderer Anwender erscheinen zusätzlich über `GET /api/playlists/public` und lassen sich dort ansehen und abspielen.
- Fremde **private** Playlists sind auf dem Gerät weder sichtbar noch abrufbar (`403`), und ändern darf ein Gerät ausschließlich Playlists seines eigenen Anwenders — auch eine fremde öffentliche Playlist nicht.
- Meldet das Gerät Wiedergabefortschritt mit Playlist-Bezug, erscheint der Titel in der Weiterschauen-Liste des Anwenders zusammen mit dem Namen der Playlist.
- Nach einem Anwenderwechsel auf dem Gerät gelten dieselben Regeln für den neuen Anwender: Es sieht dann dessen Playlists, nicht mehr die des vorherigen.
- Wird das Gerät widerrufen, läuft eine gerade laufende Playlist-Wiedergabe bis zum Ablauf der Sitzung (höchstens 12 Stunden) weiter; neue Erneuerungsversuche schlagen sofort fehl.

Einzelheiten zu Sichtbarkeit und Berechtigungen stehen in [Playlists — API](../playlists-api.md).

## Beispiele

- Eine neue TV-App soll Zugriff erhalten: Administrator erzeugt einen Pairing-Code, gibt ihn in der App ein — das Gerät erscheint anschließend in der Liste `Gekoppelte Geräte`.
- Ein Anwender möchte sein Tablet ohne Administrator koppeln: Er öffnet `Profil` > `Geräte`, klickt `Gerät koppeln` und scannt den QR-Code mit der App.
- Ein Tablet geht verloren: Der Administrator widerruft das Gerät in der Liste; die Sitzung kann danach nicht mehr erneuert werden.
- Mehrere namenlose Geräte sind schwer zu unterscheiden: Über `Umbenennen` erhält jedes Gerät einen sprechenden Namen.

## Einschränkungen

- Ein Pairing-Code und ein Bootstrap-Ticket können jeweils nur einmal eingelöst werden und laufen standardmäßig nach 5 Minuten ab. Danach muss ein neuer erzeugt werden.
- Der Klartext-Code ist nur direkt nach dem Erzeugen sichtbar; ein verlorener Code kann nicht erneut angezeigt werden.
- Der Widerruf sperrt das Geräte-Token und die Erneuerungsnachweise sofort. Bereits angemeldete App-Sitzungen bleiben jedoch bis zum Ablauf ihres Anmeldetokens (12 Stunden) gültig.
- Die Self-Service-Seite im Profil erzeugt nur Tickets; das Auflisten, Umbenennen und Widerrufen von Geräten bleibt dem Administrationsbereich vorbehalten.
- Zu viele Fehlversuche beim Einlösen sperren die IP-Adresse des Geräts; die Sperre ist unter `Einrichtung` > `Sicherheit` sichtbar und kann dort aufgehoben werden.
- Geräte-Tokens gelten ausschließlich als Zugangsschlüssel der App; sie ersetzen keine Benutzeranmeldung.
