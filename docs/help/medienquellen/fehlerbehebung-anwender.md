← [Zurück zur Übersicht](index.md)

# Medienquellen — Fehlerbehebung

## „Ungültiger Pfad: Bitte einen absoluten Verzeichnispfad angeben."

**Symptom:** Beim Anlegen oder Speichern einer Quelle vom Typ `Lokales Verzeichnis` erscheint diese Meldung.

**Ursache:** Der eingetragene Pfad ist leer oder kein vollständiger Verzeichnispfad.

**Lösung:**
1. Einen vollständigen Pfad eintragen, z. B. `D:\Videos` oder `\\server\share`.
2. Relative Angaben wie `videos` oder `.\videos` sind nicht zulässig.

## „Verzeichnis existiert nicht."

**Symptom:** Beim Speichern einer lokalen Quelle erscheint diese Meldung.

**Ursache:** Das angegebene Verzeichnis existiert auf dem Server nicht. Wichtig: Der Pfad gilt aus Sicht des Servers, nicht des eigenen Rechners.

**Lösung:**
1. Schreibweise und Laufwerksbuchstaben prüfen.
2. Sicherstellen, dass das Verzeichnis auf dem Server bzw. die Freigabe tatsächlich vorhanden ist.

## „Auf das Verzeichnis kann nicht zugegriffen werden."

**Symptom:** Beim Speichern einer lokalen Quelle erscheint diese Meldung.

**Ursache:** Das Verzeichnis existiert, aber die Anwendung darf es nicht lesen — meist fehlende Berechtigungen auf dem Verzeichnis oder der Netzwerkfreigabe.

**Lösung:**
1. Dem Benutzerkonto, unter dem die Anwendung läuft, Lesezugriff auf das Verzeichnis geben.
2. Bei einer Netzwerkfreigabe die Freigabe- und Dateisystem-Berechtigungen prüfen.

## Inhalte einer lokalen Quelle erscheinen nicht

**Symptom:** Die Quelle ist angelegt, aber in der Medienbibliothek tauchen keine Inhalte auf.

**Ursache:** Das Einlesen läuft automatisch im Scan-Intervall — oder das Verzeichnis ist aktuell nicht erreichbar (z. B. Freigabe offline, Laufwerk nicht verbunden, fehlende Rechte). Nicht erreichbare Quellen werden später automatisch erneut versucht.

**Lösung:**
1. In der Quellenübersicht `Komplettscan aller Quellen` klicken und kurz warten.
2. Prüfen, ob das Verzeichnis auf dem Server erreichbar ist.
3. Beachten: Einträge, deren Name mit einem Punkt beginnt, sowie Verknüpfungen werden beim Einlesen übersprungen.

## Wann Hilfe nötig ist

Wenden Sie sich an den Betreiber oder Administrator des Servers, wenn

- das Verzeichnis für das Dienstkonto der Anwendung freigegeben werden muss,
- eine Netzwerkfreigabe dauerhaft nicht erreichbar ist,
- ein wiederhergestelltes Backup Fehler meldet,
- oder der Quelltyp einer bestehenden Quelle gewechselt werden soll (dazu muss die Quelle gelöscht und neu angelegt werden).
