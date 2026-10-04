# Bestandsaufnahme: Dokumentation (Discovery-Bezug)

## `docs/INSTALL_AVAHI.md`
- Beschreibt den bisher einzigen mDNS-Weg: manueller Avahi-Daemon unter Linux mit statischer Dienstdatei `/etc/avahi/services/videowebplayer.service` (Zeilen 30–41).
- Vorlage announcet `_http._tcp` (Z. 36), Instanzname `VideoWebPlayer` (Z. 34), Port `5000` (Z. 37), TXT-Record `path=/` (Z. 38).
- Hinweise: UDP-Port 5353 muss offen sein (Z. 64); eigener Diensttyp `_videowebplayer._tcp` wird als Option genannt (Z. 65); „Nächste Schritte" verlangen noch die Client-Implementierung (Z. 69–71).
- Bei In-App-Advertisement besteht die Gefahr des Doppel-Advertisements parallel zu einer solchen statischen Avahi-Konfiguration (offene Frage 3 in `requirement.md`).

## `docs/API.md`
- Zeile 12: dokumentiert die lokale Basis-URL `http://localhost:5000` über `Host:Address`/`Host:Port` — kein Discovery-Abschnitt vorhanden.
- `ApiDocumentationContractTests` prüfen die Konsistenz dieser Datei mit dem Code (siehe `tests.md`).

## `docs/GUIDE_Installation.md`
- Zeile 53: erwähnt Launch-Profile (`http://localhost:57331`, `http://localhost:5039`) und dass die „Discovery-Adresse" ohne Konfiguration auf `http://localhost:5000` zurückfällt.

## `docs/help/einrichtung.md`, `README.md`, `docs/RELEASE_NOTES.md`
- Volltextsuche nach `discovery`, `mDNS`, `avahi`, `5001`, `5353`: **keine Treffer** — kein bestehender Discovery-Abschnitt in der Benutzerdoku.

## `issue.md` (Repo-Root, untracked Bezugsdokument)
- Enthält die Original-Anforderung (Issue #223): MAUI-App `VideoPlayer.Maui` sucht per mDNS (`_http._tcp.local.`) und per UDP-Broadcast Port 5001; UDP funktioniert, mDNS fehlt serverseitig.

## Sonstiges
- `docs/INDEX.md`: Index der Doku — würde bei neuen Discovery-Dokumenten ggf. einen Eintrag brauchen (nicht geprüft auf Vollständigkeit, kein Discovery-Eintrag vorhanden).
- CI-Workflows: `staging-ci.yml` führt die Testsuiten aus; ein neues NuGet-Paket würde durch den `security-scan`-Step (`.github/actions/security-scan`) geprüft.
