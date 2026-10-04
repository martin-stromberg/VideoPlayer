# Offene Aufgaben

Erstellt am: 2026-10-04
Abbruchgrund: Maximale Iterationsanzahl erreicht

Die folgenden Aufgaben konnten im automatisierten Zyklus nicht abgeschlossen werden
und müssen manuell oder in einem erneuten Lauf bearbeitet werden.

Hinweis: Alle Code-Planelemente sind vollständig umgesetzt und alle Reviews/Tests grün.
Die offenen Punkte sind Dokumentationsaufgaben (Lifecycle-Schritte 12/12b/12c dieses
Laufs) und eine manuelle Verifikation, die nicht automatisierbar ist.

## Offene Planelemente

- [x] Task 23: `docs/INSTALL_AVAHI.md` aktualisieren (In-App-Advertisement als Standardweg, Avahi als Alternative, Doppel-Advertisement-Hinweis) — erledigt in Lifecycle-Schritt 12
- [x] Task 24: `docs/GUIDE_Installation.md`, `docs/help/einrichtung.md`, `docs/API.md`, `README.md`, `docs/INDEX.md`, `docs/RELEASE_NOTES.md` aktualisieren inkl. Hinweis auf MAUI-App-Umstellung — erledigt in Lifecycle-Schritten 12/12b/12c
- [x] Task 26: mDNS-Verifikation — am 2026-10-04 automatisiert nachgeholt (statt `avahi-browse`/`dns-sd`: eigener Browse-Client auf Basis `Makaretu.Dns.Multicast.New` 0.38.0, Server als Debug-Build unter `ASPNETCORE_ENVIRONMENT=Development`, `ASPNETCORE_URLS=http://127.0.0.1:5099`):
  - Instanz `VideoWebPlayer._videowebplayer._tcp.local` wurde per PTR/SRV/TXT/A/AAAA announced; SRV-Port 5099 entspricht der gebundenen Kestrel-Adresse; TXT: `txtvers=1; path=/; app=VideoWebPlayer`. Server-Log: `Server per mDNS angekündigt: Instanz 'VideoWebPlayer', Diensttyp '_videowebplayer._tcp.local.', Port 5099`.
  - Admin-Schalter aus (`Setups.MdnsAdvertisementEnabled=0` per SQLite-Update): nächster 60-s-Tick sandte Goodbye (alle Records TTL=0, `INSTANCE SHUTDOWN`), Dienst beantwortete danach keine Queries mehr — wirksam ≤60 s. Server-Log: `mDNS-Advertisement beendet (Goodbye-Pakete gesendet)`.
  - Admin-Schalter wieder an: Re-Announcement nach ~44 s.
  - Shutdown (CTRL_BREAK): `Application is shutting down` + `mDNS-Advertisement beendet (Goodbye-Pakete gesendet)`; Browse-Client empfing TTL=0-Goodbye.

## Code-Review-Befunde

Keine — `review-code.md` trägt den Status „Keine Befunde" (alle Befunde aus
`review-code.1.md` und `review-code.2.md` behoben).

## Usability-Befunde

Keine — `review-usability.md` trägt den Status „Keine Befunde".

## Fehlgeschlagene Tests

Keine — `test-results.md` trägt den Status „Keine Fehler" (1665/1666 grün,
1 dokumentierter Baseline-Skip).
