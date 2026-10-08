← [Zurück zur Übersicht](index.md)

# Netzwerk-Erkennung — Einrichtung

## Zweck

Administratoren legen fest, welche Adresse der Server bei der Broadcast-Erkennung an Apps meldet. In der Regel bleibt das Feld leer und die automatische Ableitung genügt; eine explizite Adresse ist nötig, wenn der Server hinter IIS, einem Reverse-Proxy oder einem externen TLS-Abschluss läuft.

## Einstellungen

Die Einstellung liegt auf der Seite `Einrichtung` → `Allgemein` (nur für Administratoren).

| Einstellung | Bedeutung |
|-------------|-----------|
| `Öffentliche Basis-URL` | Die Adresse, die der Server bei der Broadcast-Erkennung an Apps meldet — vollständig mit Protokoll, Host, Port und Pfad, z. B. `https://videos.example.com/videoplayer/`. Muss eine absolute `http`- oder `https`-Adresse sein. Leer lassen für die automatische Ableitung. |

## Vorgehen

1. Im Menü `Einrichtung` öffnen und die Kachel `Allgemein` wählen.
2. In der Karte `Öffentliche Basis-URL` die Adresse eintragen, unter der Clients den Server erreichen — zum Beispiel die externe Adresse der IIS-Site inklusive Unterpfad.
3. `Speichern` klicken. Ist die Eingabe keine gültige absolute `http`-/`https`-Adresse, erscheint eine Fehlermeldung und es wird nichts gespeichert.
4. Die neue Adresse gilt sofort bei der nächsten Suchanfrage — ein Neustart ist nicht nötig.

Um wieder zur automatischen Ableitung zurückzukehren, das Feld leeren und speichern.

## Hinweise

- Das Admin-Feld hat Vorrang vor der Basis-URL, die der Betreiber in der Serverkonfiguration hinterlegt hat. Steht im Admin-Feld eine Adresse, gilt ausschließlich diese.
- Die Einstellung wird in Backups mitgesichert. Wird ein Backup auf einem anderen Server wiederhergestellt, kann die eingetragene Adresse noch auf den alten Server zeigen — sie dann anpassen oder leeren, damit die automatische Ableitung greift.
- Der Schalter `Server per mDNS im Netzwerk ankündigen` auf derselben Seite steuert die zweite Erkennungsart (mDNS); er ist unabhängig von der gemeldeten Adresse.
