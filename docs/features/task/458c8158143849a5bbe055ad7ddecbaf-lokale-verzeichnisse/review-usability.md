# Usability-Review

## Ergebnis

**Status:** Befunde vorhanden

## Befunde

### MediaSourceAdminDetails.razor (Quelle anlegen/bearbeiten)

- **Erreichbarkeit** — Bei der Interaktion „lokales Verzeichnis als Quelle angeben" ist aus der Oberfläche nicht erkennbar, dass sich der einzugebende Pfad auf das Dateisystem des Servers bezieht, auf dem der VideoWebPlayer läuft. Das Feld heißt nur „Lokales Verzeichnis" (Platzhalter: „Pfad"); für eine nicht-technische Administratorin, die die Weboberfläche von einem anderen Rechner aus aufruft, ist „lokal" mehrdeutig — sie kann einen Pfad auf ihrem eigenen PC (z. B. `C:\Filme`) eintragen und erhält dann nur „Verzeichnis existiert nicht.", ohne zu verstehen warum. Ebenso verlangt die Eingabe implizites Wissen darüber, welche Verzeichnisse auf dem Server überhaupt existieren.

  Empfehlung: Hinweistext unter dem Pfad-Feld ergänzen, der den Server-Bezug klarmacht und ein Beispiel nennt, z. B. „Absoluter Pfad zu einem Verzeichnis auf dem Server, auf dem der VideoWebPlayer läuft (z. B. `D:\Medien` oder `/mnt/medien`)". Die Fehlermeldung „Verzeichnis existiert nicht." sollte analog zu „Das Verzeichnis existiert nicht auf dem Server." präzisiert werden; bei „Auf das Verzeichnis kann nicht zugegriffen werden." hilft der Zusatz, dass der Serverprozess keine Leseberechtigung hat.

- **Erreichbarkeit** — Die Quelltyp-Auswahl („SFTP-Server" / „Lokales Verzeichnis") ist beim Bearbeiten einer bestehenden Quelle deaktiviert (`disabled="@(!IsNew)"`, Zeile 77), ohne dass die Oberfläche erklärt warum oder was die Alternative ist. Hat die Administratorin beim Anlegen versehentlich den falschen Typ gewählt, bleibt ihr nur, die Quelle komplett zu löschen (inkl. aller gescannten Daten) und neu anzulegen — das ist aus der UI nicht ersichtlich.

  Empfehlung: Kurzen Hinweistext am deaktivierten Feld ergänzen, z. B. „Der Quelltyp kann nach dem Anlegen nicht geändert werden." Alternativ den Typwechsel erlauben und dabei auf den erforderlichen Neu-Scan hinweisen (siehe Offene Frage 2 der Anforderung).

## Geprüfte Interaktionen

Liste der aus der Anforderung geprüften Benutzerinteraktionen:
- Quelle anlegen/bearbeiten und Quelltyp explizit zwischen „SFTP-Server" und „Lokales Verzeichnis" wählen → Dropdown mit Klartext-Optionen vorhanden; nachträgliche Änderung deaktiviert ohne Erklärung (Befund vorhanden)
- Bei „Lokales Verzeichnis" nur relevante Felder (Name, Pfad, Icon, Benutzerfreigaben) sehen; Host/Port/Benutzername/Passwort ausgeblendet → umgesetzt, Felder werden typabhängig ein-/ausgeblendet; Server-Bezug des Pfads nicht erklärt (Befund vorhanden)
- Lokalen Verzeichnispfad eingeben und validieren lassen (leer/ungültig/nicht existierend/kein Zugriff) → differenzierte, verständliche Fehlermeldungen vorhanden („Bitte einen absoluten Verzeichnispfad angeben.", „Verzeichnis existiert nicht.", „Auf das Verzeichnis kann nicht zugegriffen werden."); Meldungen nennen den Server-Bezug nicht (Befund vorhanden)
- Quelltyp in der Quellen-Übersicht erkennen → neue Spalte „Typ" mit „Lokal"/"SFTP", lokale Quellen zeigen „—" bei Host/Port/Benutzername → unauffällig
- Quelle löschen, Scan zurücksetzen, Komplettscan, Benutzerfreigaben per Checkbox, Icon-Upload → bestehende, unveränderte Bedienmuster → unauffällig

## Geprüfte Dateien

Liste aller geprüften UI-Dateien:
- `VideoWebPlayer/Components/Pages/Admin/MediaSources/MediaSourceAdminDetails.razor`
- `VideoWebPlayer/Components/Pages/Admin/MediaSources/MediaSourceAdmin.razor`
- `VideoWebPlayer/Components/Layout/NavMenu.razor` (Änderung betrifft nur Auth-Guard beim Laden der Quellen, keine neue Interaktion)
