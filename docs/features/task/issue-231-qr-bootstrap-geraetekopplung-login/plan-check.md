# Plan-Gegenprüfung

## Ergebnis

**Status:** Plan vollständig

## Abgleich Akzeptanzkriterien

| Akzeptanzkriterium (Issue #231) | Umsetzung im Plan | Testnachweis im Plan | Status |
|--------------------|-------------------|----------------------|--------|
| 1. Profilseite „Gerät koppeln": QR + 8-Zeichen-Text + Kurzanleitung; Rate-Limit je Benutzer | `Manage/Devices.razor` (SSR-EditForm-Muster), QR via QRCoder-Data-URL, Kurzcode + Anleitung + „Gültig bis"; DB-basiertes Rate-Limit `BootstrapMaxTicketsPerHour` | `ProfilePairingE2ETests` (Ticket-Erzeugung sichtbar; Rate-Limit-Meldung) + `PairingServiceTests_BootstrapTicket` | Abgedeckt |
| 2. `api/pairing/bootstrap` verbraucht Ticket atomar, liefert verschlüsselt Geräte-Token + JWT + Refresh-Token; öffentlich | `PairingBootstrapService.BootstrapAsync` (atomarer `ExecuteUpdateAsync`, Token-Trio, AES-GCM-Payload); `PairingController.Bootstrap` ohne Auth-Attribute | `PairingServiceTests_Bootstrap` (Happy Path, Alias, Atomarität, Race), `PairingBootstrapContractTests`, `ProfilePairingE2ETests.Bootstrap_Flow_*` | Abgedeckt |
| 3. Datenmodell trägt beide Pairing-Richtungen | `PairingCodeKind` (`AdminCode`/`BootstrapTicket`/`DeviceInitiated` reserviert) + Migration | Build + Service-Tests (`Kind`-Filter-Verhalten in `PairingServiceTests_Bootstrap`) | Abgedeckt |
| 4. Refresh-Endpunkt mit Rotation | `RefreshTokenService.RotateAsync` (atomar, `ReplacedByHash`, Reuse-Detection), `POST api/auth/refresh` | `RefreshTokenServiceTests`, `AuthRefreshContractTests`, E2E-Gesamtfluss | Abgedeckt |
| 5. Admin-Only-Option konfigurierbar; Vertrag in `docs/API.md` | `Pairing:BootstrapAdminOnly` (Default false); API.md-Abschnitt + `ApiDocumentationContractTests` | `PairingServiceTests_BootstrapTicket.*AdminOnly*`, Contract-Test Routenliste | Abgedeckt |

## Fehlende oder unvollständige Testanforderungen

— (Status `Plan vollständig`)

## E2E-Abdeckung

| Benutzerfluss / Akzeptanzkriterium | Geplanter E2E-Test | Status |
|------------------------------------|--------------------|--------|
| Ticket auf Profilseite erzeugen, QR/Kurzcode/Gültigkeit sehen (AK1) | `ProfilePairingE2ETests.User_Creates_Bootstrap_Ticket_And_Sees_Qr_ShortCode_Ttl` | Abgedeckt |
| Gesamtfluss Ticket → bootstrap → Trio → Login + Refresh (AK2/AK4) | `ProfilePairingE2ETests.Bootstrap_Flow_UiTicket_Exchange_Login_Refresh_Succeeds` | Abgedeckt |
| Rate-Limit-Meldung in der UI (AK1) | `ProfilePairingE2ETests.Rate_Limit_Shows_Message_On_Profile_Page` | Abgedeckt |
| Admin-Code-Flow `exchange` (Regression) | bestehende `DevicePairingE2ETests` | Abgedeckt |
| Loopback-Hinweis, Admin-Only-Verweigerung in UI | Service-Level-Tests; UI-Hinweis über Markup-Prüfung im ersten E2E-Szenario mitgeprüft (Test läuft unter localhost → Warnhinweis erwartbar sichtbar) | Abgedeckt |

## Fehlende oder unvollständige Planbestandteile

— (Status `Plan vollständig`)

## Hinweise

- Vertragsfestlegung (für App-Schritt 5b zu übernehmen): Request `ticket`/`clientPublicKey`/`deviceName`; Response `serverPublicKey`/`encryptedPayload`; entschlüsselter Payload `deviceToken`/`token`/`expires`/`refreshToken`; Refresh `refreshToken` → `token`/`expires`/`refreshToken`; Fehlercodes 400/401/429 wie `exchange`.
- Risiko „Header-Vertrauen" (`Scheme`/`Host` für QR-Adresse) ist als Entscheidung dokumentiert; Loopback-Fall bekommt UI-Hinweis.
- `IDeviceTokenService.IssueAsync`-Signaturänderung ist intern; alle Aufrufer sind benannt (`ExchangeAsync`, Tests, `PairingTestDb`).
