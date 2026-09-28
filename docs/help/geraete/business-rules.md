← [Zurück zur Übersicht](index.md)

# Geräte — Business Rules

## Einmal-Pairing-Code

**Beschreibung:** Ein Pairing-Code darf nur einmal und nur innerhalb seiner Gültigkeitsdauer eingelöst werden.

**Bedingungen:**
- Der eingegebene Code muss per `SHA-256`-Hash einem Datensatz in `PairingCodes` entsprechen.
- `ExpiresAtUtc` darf nicht überschritten sein, `ConsumedAtUtc` muss `null` sein.

**Verhalten:**
- Wenn alle Bedingungen erfüllt sind: Der Code wird atomar als verbraucht markiert und der Exchange läuft weiter.
- Sonst: generische `401`-Antwort und die Client-IP erhält einen Fehlversuchszähler.

**Umsetzung:** `PairingService.ExchangeAsync` — der Verbrauch erfolgt per `ExecuteUpdateAsync` mit `WHERE ConsumedAtUtc IS NULL AND ExpiresAtUtc > now`, sodass parallele Einlöseversuche und ein zwischen Lese- und Schreibzugriff abgelaufener Code sicher abgefangen werden.

## Einmal-Bootstrap-Ticket

**Beschreibung:** Ein Bootstrap-Ticket ist ebenfalls einmalig; das lange Ticket aus dem QR-Code und der 8-stellige Kurzcode sind zwei Zugänge zu **demselben** Datensatz.

**Bedingungen:**
- Der vorgelegte Wert muss per `SHA-256` entweder `TicketHash` oder `CodeHash` eines Datensatzes mit `Kind = BootstrapTicket` treffen.
- `ExpiresAtUtc` darf nicht überschritten sein (`Pairing:BootstrapTicketTtlMinutes`, Standard 5 Minuten), `ConsumedAtUtc` muss `null` sein.
- Das Konto, das das Ticket erzeugt hat (`CreatedByUserId`), muss noch existieren.

**Verhalten:**
- Erfüllt: Das Ticket wird atomar verbraucht; das Gerät erhält Geräte-Token, Anmeldenachweis **des Ticket-Erstellers** und Erneuerungsnachweis in einem verschlüsselten Paket.
- Sonst: generische `401`-Antwort, Fehlversuchszähler für die Client-IP. Ein Einlösen über den Kurzcode verbraucht damit auch das QR-Ticket und umgekehrt.

**Umsetzung:** `PairingBootstrapService.BootstrapAsync` (atomarer Verbrauch per `ExecuteUpdateAsync`).

## Wer darf koppeln

**Beschreibung:** Das Erzeugen von Bootstrap-Tickets ist Selbstbedienung, kann aber auf Administratoren beschränkt werden.

**Bedingungen:**
- `Pairing:BootstrapAdminOnly` (Standard `false`) entscheidet, ob ein Nicht-Administrator ein Ticket erzeugen darf.
- Pro Anwender dürfen höchstens `Pairing:BootstrapMaxTicketsPerHour` (Standard `10`) Tickets je Stunde entstehen.

**Verhalten:**
- Standard: Jeder angemeldete Anwender erzeugt sich unter `Profil` > `Geräte` selbst ein Ticket.
- `Pairing:BootstrapAdminOnly = true`: Nicht-Administratoren erhalten „Das Koppeln von Geräten ist auf diesem Server nur Administratoren erlaubt." und kein Ticket.
- Quote erreicht: „Es wurden zu viele Kopplungs-Tickets erzeugt." und kein Ticket.
- Das Auflisten, Umbenennen und Widerrufen von Geräten bleibt unabhängig davon dem Administrationsbereich vorbehalten.

**Umsetzung:** `PairingBootstrapService.CreateBootstrapTicketAsync`, `Components/Account/Pages/Manage/Devices.razor`.

## Fehlversuchszählung und IP-Sperre

**Beschreibung:** Bruteforce auf die öffentlichen Pairing-Endpunkte wird über die geteilte IP-Sperrliste gebremst.

**Bedingungen:**
- Nur inhaltlich ungültige Codes bzw. Tickets (`401`) zählen als Fehlversuch; Formatfehler (`400`) zählen nicht, damit z. B. ein leerer Code den Zähler nicht erhöht.
- Ab 5 Fehlversuchen (`LoginIpBlockService.Threshold`) wird die IP gesperrt; ein erfolgreiches Einlösen setzt den Zähler zurück.

**Verhalten:**
- Gesperrte IP: `429`, unabhängig vom Request-Inhalt.
- Die Sperre gilt auch für das Web-Login und ist unter `/admin/security` sichtbar und entsperrbar.

**Umsetzung:** `PairingController.Exchange` und `PairingController.Bootstrap` in Verbindung mit `ILoginIpBlockService` (`IsBlocked`, `RegisterFailure`, `RegisterSuccess`).

## Klartext-Geheimnisse werden nie gespeichert

**Beschreibung:** Weder Pairing-Codes, Bootstrap-Tickets, Geräte-Tokens noch Erneuerungsnachweise liegen im Klartext in der Datenbank.

**Bedingungen:**
- `PairingCode.CodeHash`, `PairingCode.TicketHash`, `PairedDevice.TokenHash` und `RefreshToken.TokenHash` enthalten `SHA-256`-Hashes (hohe Entropie, daher ohne Salt).

**Verhalten:**
- Der Klartext-Code wird genau einmal in der Admin-UI angezeigt; `GetActiveCodesAsync` liefert nur Metadaten.
- Ticket und Kurzcode erscheinen nur einmal auf der Profilseite.
- Geräte-Token, Anmeldenachweis und Erneuerungsnachweis verlassen den Server nur AES-256-GCM-verschlüsselt (Einlösung) bzw. über HTTPS in der Antwort auf `POST api/auth/refresh`.

**Umsetzung:** `HashHelper.Sha256Hex`, `PairingService.CreatePairingCodeAsync`, `PairingBootstrapService.CreateBootstrapTicketAsync`, `DeviceTokenService.IssueAsync`, `RefreshTokenService.IssueAsync`.

## Rotation und Wiederverwendung des Erneuerungsnachweises

**Beschreibung:** Jede Sitzungserneuerung tauscht den Erneuerungsnachweis aus; ein zweiter Gebrauch desselben Nachweises gilt als Kompromittierung.

**Bedingungen:**
- Der vorgelegte Nachweis muss existieren, darf nicht gesperrt (`RevokedAtUtc`) und nicht abgelaufen sein, und das zugehörige Gerät darf nicht widerrufen sein.

**Verhalten:**
- Erfüllt: Der alte Nachweis wird in derselben Operation gesperrt (`ReplacedByHash` zeigt auf den Nachfolger), ein neuer Nachweis und ein neuer Anmeldenachweis werden ausgegeben.
- Bereits gesperrter Nachweis erneut vorgelegt: Es werden **alle** noch offenen Nachweise derselben Kombination aus `UserId` und `DeviceId` gesperrt. Das Gerät muss neu gekoppelt werden.
- Jeder Ablehnungsgrund führt zu derselben generischen `401`-Antwort.

**Umsetzung:** `RefreshTokenService.RotateAsync` (atomare Rotation per `ExecuteUpdateAsync`), `AuthController.Refresh`.

## Abmelden

**Beschreibung:** Das Abmelden auf dem Gerät sperrt den Erneuerungsnachweis, nicht die laufende Sitzung.

**Bedingungen:** keine — der Endpunkt ist idempotent.

**Verhalten:**
- Mit gültigem Nachweis: Der Nachweis wird gesperrt; eine Erneuerung ist danach nicht mehr möglich.
- Ohne Body oder mit unbekanntem Nachweis: ebenfalls `200 OK`, ohne Auskunft darüber, ob etwas gesperrt wurde.
- Der bereits ausgestellte Anmeldenachweis bleibt bis zu seinem Ablauf gültig; die Client-Bibliothek verwirft ihn deshalb lokal sofort mit.

**Umsetzung:** `AuthController.Logout`, `RefreshTokenService.RevokeAsync`, `VideoWebPlayerClient.LogoutAsync`.

## Widerruf

**Beschreibung:** Ein widerrufenes Gerät verliert sofort das Gate und die Möglichkeit, seine Sitzung zu erneuern.

**Bedingungen:**
- `RevokeAsync` setzt `PairedDevice.RevokedAtUtc`; `IsValidDeviceTokenAsync` akzeptiert nur Einträge mit `RevokedAtUtc == null`.
- Im selben Vorgang werden über `RevokeAllForDeviceAsync` alle offenen Erneuerungsnachweise des Geräts gesperrt.

**Verhalten:**
- Folgeanfragen mit dem Geräte-Token als `X-API-Key` werden mit `401` abgelehnt.
- `POST api/auth/refresh` antwortet mit `401` — die Sitzungserneuerung ist ab dem Widerruf gesperrt.
- Bereits ausgestellte Anmeldenachweise bleiben bis zu ihrem Ablauf (12 Stunden) gültig — bewusst akzeptierte Restlaufzeit. Die laufende Sitzung arbeitet in dieser Zeit normal weiter.
- **Playlist-Folge:** Eine laufende Playlist-Wiedergabe endet nicht mit dem Widerruf. Das Gerät kann Playlists weiter abrufen, weiterschalten und Fortschritt melden, bis der Anmeldenachweis abläuft; danach bleibt ihm nur die Neukopplung.
- Die UI erfordert eine explizite Checkbox-Bestätigung vor dem Widerruf.

**Umsetzung:** `DeviceTokenService.RevokeAsync`, `DeviceTokenService.IsValidDeviceTokenAsync`, `RefreshTokenService.RevokeAllForDeviceAsync`, `Devices.razor` (`ConfirmRevokeAsync`).

## Playlist-Sichtbarkeit auf Geräten

**Beschreibung:** Ein Gerät sieht ausschließlich die Playlists des Anwenders, der in seinem Anmeldenachweis steht — und zusätzlich die öffentlichen.

**Bedingungen:**
- Maßgeblich ist der Anwender aus dem Anmeldenachweis, nicht das Gerät: nach dem QR-Bootstrap der Ersteller des Tickets, nach einem `POST api/auth/login` auf dem Gerät der dort angemeldete Anwender.

**Verhalten:**
- `GET api/playlists` liefert nur die eigenen Playlists dieses Anwenders.
- `GET api/playlists/public` liefert zusätzlich alle als öffentlich gekennzeichneten Playlists — auch die anderer Anwender; dort ist Ansehen und Abspielen erlaubt.
- Fremde private Playlists sind weder in einer Liste enthalten noch einzeln abrufbar (`403`), einschließlich ihres Titelbilds.
- Ändern (Einträge hinzufügen oder entfernen, umbenennen, umsortieren, Cover setzen) darf nur der Besitzer — auch bei einer öffentlichen Playlist antwortet der Server anderen mit `403`.
- Ein Anwenderwechsel auf dem Gerät wechselt damit auch die sichtbaren Playlists; das Geräte-Token bleibt dasselbe.
- Fortschritt mit Playlist-Bezug wird nur angenommen, wenn der Anwender die Playlist lesen darf (`404` bei unbekannter, `403` bei fremder privater Playlist). Der entstehende Weiterschauen-Eintrag gehört immer dem meldenden Anwender.

**Umsetzung:** `PlaylistService.GetPlaylistsAsync` / `GetPublicPlaylistsAsync` / `EnsureReadable`, `PlaylistsController`, `ContinueWatchingService.ValidatePlaylistAccessAsync`. Einzelheiten in [Playlists — API](../playlists-api.md).

## Fallback-Konfigurationstoken

**Beschreibung:** `Jwt:ApiToken:Maui` bleibt neben den Geräte-Tokens als akzeptierter `X-API-Key` für `MauiOnly` bestehen, damit ältere App-Versionen weiter funktionieren.

**Bedingungen:**
- Nur im Scope `MauiOnly` wird nach einem Config-Nicht-Treffer die Datenbank geprüft; `AnyClient` löst nie einen DB-Zugriff aus.

**Verhalten:**
- Config-Treffer oder gültiges Geräte-Token: Request passiert; bei Geräte-Token wird zusätzlich `LastUsedAtUtc` aktualisiert.
- Weder noch: `401`, Log ohne Token-Wert.

**Umsetzung:** `ApiTokenCheckAttribute.OnActionExecutionAsync`; der Produktions-Pflichtcheck auf `Jwt:ApiToken:Maui` in `ServiceCollectionExtensions` bleibt unverändert.

## Gerätename

**Beschreibung:** Die App kann beim Koppeln einen Anzeigenamen liefern; Administratoren können ihn später ändern.

**Bedingungen:**
- `deviceName` ist optional, wird getrimmt und darf höchstens 200 Zeichen haben; Überschreitung → `400` (keine stille Kürzung). Das gilt für `exchange` und `bootstrap` gleichermaßen.
- `RenameAsync` akzeptiert denselben Wertebereich (1–200 Zeichen nach Trimmen).

**Verhalten:**
- Leerer/fehlender Name → Server-Default `Geraet vom <Ausstellungszeitpunkt>`, damit namenlose Geräte unterscheidbar bleiben.

**Umsetzung:** `PairingService.ExchangeAsync` und `PairingBootstrapService.BootstrapAsync` (Validierung), `DeviceTokenService.IssueAsync` (Default) und `DeviceTokenService.RenameAsync`.
