← [Zurück zur Übersicht](index.md)

# Geräte — Installation und Konfiguration

## Voraussetzungen

- Laufender VideoWebPlayer mit Datenbank; die Migrationen `AddDevicePairing` (Tabellen `PairedDevices` und `PairingCodes`) und `AddPairingBootstrapAndRefreshTokens` (Spalten `PairingCodes.Kind`/`TicketHash`, Tabelle `RefreshTokens`) werden beim Start über `MigrateDatabase()` automatisch angewendet.
- In Produktion ist `Jwt:ApiToken:Maui` weiterhin ein Pflichtwert (Fallback für ältere App-Versionen); Deployments ohne diesen Wert starten nicht.

## Installationsschritte

1. Anwendung aktualisieren und starten — die Datenbankmigrationen laufen automatisch.
2. `Jwt:ApiToken:Maui` in Produktion gesetzt halten (User Secrets, Umgebungsvariable oder Secret Store).
3. Entweder als Administrator `Einrichtung` > `Geräte` öffnen und einen Pairing-Code erzeugen (die App ruft `POST /api/pairing/exchange` auf) …
4. … oder als beliebiger angemeldeter Anwender `Profil` > `Geräte` öffnen und `Gerät koppeln` klicken; die App scannt den QR-Code und ruft `POST /api/pairing/bootstrap` auf.

## Konfiguration

| Parameter | Typ | Standardwert | Beschreibung |
|-----------|-----|--------------|--------------|
| `Pairing:CodeLength` | int | `8` | Länge des Pairing-Codes (Alphabet ohne `0/O/1/I/L`, fest im Code). |
| `Pairing:CodeTtlMinutes` | int | `5` | Gültigkeitsdauer eines Pairing-Codes in Minuten. |
| `Pairing:BootstrapTicketTtlMinutes` | int | `5` | Gültigkeitsdauer eines Bootstrap-Tickets in Minuten; Werte kleiner `1` werden wie `1` behandelt. |
| `Pairing:BootstrapMaxTicketsPerHour` | int | `10` | Höchstzahl der Bootstrap-Tickets je Anwender und Stunde; Werte kleiner `1` werden wie `1` behandelt. |
| `Pairing:BootstrapAdminOnly` | bool | `false` | `true` beschränkt das Erzeugen von Bootstrap-Tickets auf Administratoren. |
| `Auth:RefreshTokenTtlDays` | int | `30` | Gültigkeitsdauer eines Erneuerungsnachweises in Tagen; Werte kleiner `1` werden wie `1` behandelt. Gilt auch für den bei jeder Rotation neu ausgestellten Nachweis. |

Die Länge des Bootstrap-Kurzcodes (8 Zeichen) und des Ticket-Geheimnisses (24 Byte) sind fest im Code; die Gültigkeit des Anmeldenachweises (12 Stunden) ebenfalls. Die Fehlversuchsschwelle für `exchange` und `bootstrap` ist keine eigene Konfiguration, sondern die geteilte Konstante `Threshold = 5` des `LoginIpBlockService`. Es gibt keinen Aufräumlauf für abgelaufene Codes und Tickets; sie werden bei der Validierung verworfen und in der UI ausgefiltert.

## Umgebungsvariablen

| Variable | Pflicht | Beispielwert | Beschreibung |
|----------|---------|--------------|--------------|
| `Jwt__ApiToken__Maui` | Ja (Produktion) | `<PRODUKTIVER_MAUI_API_TOKEN>` | Fallback-Gate-Token für ältere App-Versionen. |
| `Pairing__CodeLength` | Nein | `8` | Länge des Pairing-Codes. |
| `Pairing__CodeTtlMinutes` | Nein | `5` | Gültigkeitsdauer des Pairing-Codes in Minuten. |
| `Pairing__BootstrapTicketTtlMinutes` | Nein | `5` | Gültigkeitsdauer des Bootstrap-Tickets in Minuten. |
| `Pairing__BootstrapMaxTicketsPerHour` | Nein | `10` | Tickets je Anwender und Stunde. |
| `Pairing__BootstrapAdminOnly` | Nein | `false` | Kopplung nur für Administratoren. |
| `Auth__RefreshTokenTtlDays` | Nein | `30` | Gültigkeitsdauer des Erneuerungsnachweises in Tagen. |

## Überprüfung

- Die Kachel `Geräte` erscheint unter `Einrichtung` für Administratoren.
- `Pairing-Code erzeugen` zeigt einen Code mit Gültigkeitsdauer an; ein Test-Exchange gegen `POST /api/pairing/exchange` liefert `200` mit `serverPublicKey` und `encryptedToken`.
- Unter `Profil` > `Geräte` zeigt `Gerät koppeln` QR-Code, Kurzcode und Restlaufzeit; ein Test-Bootstrap gegen `POST /api/pairing/bootstrap` liefert `200` mit `serverPublicKey` und `encryptedPayload`.
- Ein ungültiger Code bzw. ein ungültiges Ticket liefert `401`; nach 5 Fehlversuchen `429`, die IP erscheint unter `Einrichtung` > `Sicherheit`.
- Ein zweiter Aufruf von `POST /api/auth/refresh` mit demselben Erneuerungsnachweis liefert `401` — die Rotation greift.
