← [Zurück zur Übersicht](index.md)

# Geräte — Client-Bibliothek

Die Bibliothek `VideoWebPlayer.Client` (`VideoWebPlayerClient`) deckt den vollständigen Weg eines gekoppelten Geräts ab: Kopplung, Sitzung, Wiedergabe und Abmeldung. Eine App muss dafür weder HTTP-Aufrufe noch die Sitzungsverwaltung selbst bauen.

## Kopplung

| Methode | Endpunkt | Zweck |
|---------|----------|-------|
| `PairingExchangeAsync(PairingExchangeRequest)` | `POST api/pairing/exchange` | Einmal-Pairing-Code gegen ein Geräte-Token einlösen |
| `PairingBootstrapAsync(PairingBootstrapRequest)` | `POST api/pairing/bootstrap` | QR-Ticket (oder dessen 8-stelligen Kurzcode) gegen Geräte-Token, Benutzersitzung und Erneuerungsnachweis einlösen |

Beide Aufrufe liefern die Antwort verschlüsselt (ECDH `nistP256` + AES-256-GCM). Die Entschlüsselung bleibt bei der aufrufenden App, weil nur sie den privaten Schlüssel des Geräts besitzt. Aus der entschlüsselten Antwort setzt die App:

- `DeviceToken` — das Geräte-Token,
- `DeviceRefreshToken` — den Erneuerungsnachweis,
- `SetAuthorizationToken(...)` — die Benutzersitzung.

Alle drei Werte gehören zu genau einer Client-Instanz und werden nicht zwischen Instanzen geteilt. Mehrere Geräte oder mehrere Anwender am selben Server beeinflussen sich damit nicht.

## Geräte-Token bei jedem Aufruf

Solange `DeviceToken` gesetzt ist, sendet die Bibliothek es bei jedem Aufruf als Kopfzeile `X-API-Key` mit — unabhängig davon, ob der Aufruf `GET`, `POST`, `PUT`, `PATCH` oder `DELETE` verwendet. Die Kopfzeile wird dabei je Anfrage gesetzt und nicht auf der gemeinsamen Verbindung hinterlegt. Das hat zwei Folgen: Ein Gate-Key, den eine andere Komponente auf derselben Verbindung eingetragen hat (die Weboberfläche tut das), bleibt erhalten und wird wieder verwendet, sobald kein Geräte-Token mehr gesetzt ist; und laufende Anfragen werden nicht beeinflusst, wenn das Geräte-Token wechselt. Ohne gesetztes Geräte-Token ergänzt die Bibliothek nichts.

## Sitzung

| Methode | Endpunkt | Zweck |
|---------|----------|-------|
| `RefreshAsync()` | `POST api/auth/refresh` | Sitzung erneuern; die Bibliothek übernimmt die neue Sitzung und den rotierten Erneuerungsnachweis selbst |
| `LogoutAsync()` | `POST api/auth/logout` | Sitzung beenden; danach sind Sitzung, Erneuerungsnachweis und Geräte-Token der Instanz geleert — auch dann, wenn der Server den Aufruf ablehnt (der Serverfehler wird anschließend gemeldet) |

### Selbsttätige Erneuerung

Antwortet der Server auf einen beliebigen Aufruf mit `401`, erneuert die Bibliothek die Sitzung einmal selbst und wiederholt den Aufruf anschließend genau einmal (mit kurzer Wartezeit). Das gilt für alle Aufrufe, also auch für die Wiedergabesteuerung (`nächster Titel`, `vorheriger Titel`, `automatisch weiterschalten`), für Abrufe mit Abbruchmarke sowie für das Umbenennen, Umsortieren, Löschen und die Bild-Aufrufe.

Laufen mehrere Aufrufe gleichzeitig in ein `401`, wird die Sitzung trotzdem nur einmal erneuert; die übrigen Aufrufe werden danach mit der neuen Sitzung wiederholt. Maßgeblich ist dabei der Stand der Sitzung zu dem Zeitpunkt, zu dem der jeweilige Aufruf abgeschickt wurde: Ein `401`, das erst bearbeitet wird, nachdem eine andere Erneuerung bereits fertig ist, betrifft eine längst ersetzte Sitzung und führt deshalb nur zur Wiederholung, nicht zu einer zweiten Erneuerung. Das gilt unabhängig davon, in welcher Reihenfolge und mit welchem zeitlichen Abstand die Antworten eintreffen.

`403` und `404` sind dagegen endgültige Antworten: Sie lösen keine Erneuerung und keine Wiederholung aus (siehe [API.md](../../API.md), Abschnitt „Standardstatuscodes“).

### Nach einem Widerruf

Wurde das Gerät widerrufen oder ist der Erneuerungsnachweis abgelaufen bzw. bereits verbraucht, scheitert die Erneuerung endgültig. `RefreshAsync()` meldet das mit einer eindeutigen Meldung („Das Gerät wurde widerrufen oder der Erneuerungsnachweis ist abgelaufen. Bitte das Gerät neu koppeln."), und der ursprüngliche Aufruf wird nicht weiter wiederholt.

Auf dem selbsttätigen Weg bleibt diese Ursache erhalten: Der Aufruf scheitert wie bisher mit dem Status `401`, die Meldung hängt aber als Ursache (`InnerException`) daran. Eine App unterscheidet daran den Widerruf von einer gewöhnlich abgelaufenen Sitzung und kann zur Neukopplung auffordern. Ein Aufruf ohne Gerätesitzung — etwa aus der Weboberfläche — meldet weiterhin ein `401` ohne Ursache.

## Weboberfläche

Die Weboberfläche verwendet dieselbe Bibliothek über `InternalVideoWebPlayerClient`, dort allerdings ohne Geräte-Token: Sie holt sich die Sitzung des angemeldeten Anwenders selbst. Das geschieht seit dieser Fassung für alle HTTP-Verben, nicht nur für `GET` und `POST` — also auch beim Umbenennen, Umsortieren, Löschen, beim Sortiermodus, bei Abrufen mit Abbruchmarke und bei der Wiedergabesteuerung.

## Siehe auch

- [API](api.md)
- [Gerät per QR-Code koppeln (Self-Service)](qr-bootstrap-anwender.md)
- [Fehlerbehebung](troubleshooting.md)
