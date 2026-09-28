# Projektplan-Gegenprüfung

## Ergebnis

**Status:** Projektplan vollständig

## Abgleich Anforderung ↔ Entwicklungsschritte

| Anforderungspunkt | Abgedeckt durch Schritt(e) | Status |
|--------------------|-----------------------------|--------|
| Playlist anlegen, benennen, optional beschreiben, bearbeiten, löschen | 1 | Abgedeckt |
| Playlist gehört einem Anwender (`Playlist.UserId`, `ApplicationUser.Playlists`), Übersicht eigener Playlists | 1 | Abgedeckt |
| Sortiermodus `Automatic` / `Manual` als Eigenschaft der Playlist, Auswahl bei Erstellung | 1 (Auswahl/Wechsel), 3 (Automatic), 4 (Manual) | Abgedeckt |
| Kein Kopieren/Duplizieren, keine Versionierung/Historie (explizite Nicht-Anforderung) | 1, Vorgehensentscheidung „Kein Kopieren, keine Historie" | Abgedeckt |
| Hinzufügen einzelner Filme | 2 | Abgedeckt |
| Hinzufügen einzelner Episoden | 2 | Abgedeckt |
| Hinzufügen einzelner Staffeln inkl. automatischer Ergänzung aller Episoden | 2 | Abgedeckt |
| Hinzufügen kompletter Serien inkl. automatischer Ergänzung aller Staffeln/Episoden | 2 | Abgedeckt |
| Hinzufügen von Filmsammlungen inkl. automatischer Ergänzung aller Filme | 2 | Abgedeckt |
| Beliebige Kombination der fünf Inhaltstypen in einer Playlist | 2 | Abgedeckt |
| Keine weiteren Inhaltstypen (Klärungspunkt 3 der Anforderung) | Vorgehensentscheidung „Inhaltstypen", 2 | Abgedeckt |
| Herkunft aus Sammel-Eintrag festhalten (Voraussetzung für Nachlieferung) | 2 (Festhalten), 8 (Nutzung) | Abgedeckt |
| Duplikat-Vermeidung: jeder Titel nur einmal je Playlist, Hinweis an den Anwender | 2, ergänzend 8 | Abgedeckt |
| Einzelne Titel wieder entfernen, ohne Auswirkung auf Titel selbst oder andere Playlists | 2 | Abgedeckt |
| Entfernter Titel aus dem Medienbestand → Playlist-Eintrag bereinigen (Klärungspunkt 2) | 2, 7 (Weiterschauen-Folgeverhalten), Vorgehensentscheidung „Entfallene Titel" | Abgedeckt |
| Anzeige der Playlist-Inhalte mit Titelbild, Bezeichnung, Zugehörigkeit | 3 | Abgedeckt |
| Infinity-List / seitenweises Nachladen ohne feste Obergrenze (Klärungspunkt 7) | 3, Vorgehensentscheidung „Mengengrenzen" | Abgedeckt |
| Automatische Sortierung nach Erscheinungsdatum, in diesem Modus nicht manuell änderbar | 3 | Abgedeckt |
| Fallback-Sortierung bei fehlendem Erscheinungsdatum (Annahme 2 der Anforderung) | 3, Vorgehensentscheidung „Sortierung ohne Datum" | Abgedeckt |
| Ausgrauen nicht freigeschalteter Titel (`UnlockedMediaEntry`), nicht startbar | 3, 11 (für Betrachter öffentlicher Playlists) | Abgedeckt |
| Manuelle Sortierung per Drag & Drop | 4 | Abgedeckt |
| Aktionen „An den Anfang" / „An das Ende" | 4 | Abgedeckt |
| Neue Titel im Manual-Mode am Ende einfügen | 4, 8 | Abgedeckt |
| Dauerhafte Speicherung der manuellen Reihenfolge (`PlaylistItem.Order`) | 4 | Abgedeckt |
| Wiedergabe aus der Playlist heraus ab beliebigem Titel | 5 | Abgedeckt |
| Nächstes Video in der Playlist ermitteln, automatisches Weiterschalten, vor/zurück (Annahme 5) | 5 | Abgedeckt |
| Playlist-Kontext während der Wiedergabe erkennbar | 5 | Abgedeckt |
| Wiedergabeverhalten ohne Playlist bleibt unverändert | 5, 6 | Abgedeckt |
| `ContinueWatchingEntry` um Playlist-Bezug erweitern | 6 | Abgedeckt |
| Dasselbe Video mehrfach in der Weiterschauen-Liste (je Playlist bzw. ohne Playlist), getrennt fortgeschrieben | 6 | Abgedeckt |
| Playlist-Zugehörigkeit in der Weiterschauen-Liste sichtbar | 6 | Abgedeckt |
| Globale Gesehen-Markierung (`WatchedEntry`) bleibt playlist-unabhängig | 6, Vorgehensentscheidung „Gesehen-Markierung" | Abgedeckt |
| Sicherheitsabfrage beim Entfernen eines Titels mit Weiterschauen-Bezug, inkl. Wortlaut (Klärungspunkt 8) | 7 | Abgedeckt |
| Ersetzen des Weiterschauen-Eintrags durch den nächsten verfügbaren Titel, sonst Löschen | 7 | Abgedeckt |
| Weiterschauen-Einträge ohne bzw. mit anderem Playlist-Bezug bleiben unangetastet | 7 | Abgedeckt |
| Aufräumen der Weiterschauen-Einträge beim Löschen einer ganzen Playlist | 7 | Abgedeckt |
| Automatische Nachlieferung neuer Staffeln/Episoden/Filme eines Sammel-Eintrags (Hintergrund-Job) | 8 | Abgedeckt |
| Einsortierung nachgelieferter Titel je nach Sortiermodus | 8 | Abgedeckt |
| Konfiguration: Scan-Intervall und Batch-Größe des Hintergrund-Abgleichs | 8 | Abgedeckt |
| Konfiguration: optionales Maximum an Playlists je Anwender | 1 | Abgedeckt |
| Konfiguration: optionales Maximum an Titeln je Playlist (auch für die Nachlieferung wirksam) | 2, 8 | Abgedeckt |
| Automatische Genre-Ableitung aus den enthaltenen Titeln, Aktualisierung bei Änderungen | 9 | Abgedeckt |
| Manuelles Überschreiben der Genres durch den Besitzer, Rücksetzen auf Automatik | 9 | Abgedeckt |
| Anzeige der Playlist-Genres, Nutzung für Suche/Filterung | 9 | Abgedeckt |
| Playlist-Abbildung: eigenes Bild hochladen | 10 | Abgedeckt |
| Playlist-Abbildung: automatische Erzeugung als Collage, max. 5 Bilder, Quellen-Priorität Serie > Episode > Filmsammlung > Film (Annahme 4) | 10 | Abgedeckt |
| Bildkonventionen für Auflösung/Format/Ablage (Klärungspunkt 6) | 10 | Abgedeckt |
| Anzeige der Abbildung in Übersicht und Detailansicht, neutrale Ersatzdarstellung | 10 | Abgedeckt |
| Konfiguration/Validierung des Bild-Uploads (Formate, maximale Dateigröße) | 10 | Abgedeckt |
| Öffentliche Playlists, nur durch Administratoren schaltbar (Annahme 6) | 11, Vorgehensentscheidung „Sichtbarkeit" | Abgedeckt |
| Öffentliche Playlists rein lesend: sichtbar und abspielbar, keine Bearbeitung durch Fremde | 11 | Abgedeckt |
| Eigene Übersicht öffentlicher Playlists, getrennt von den eigenen | 11 | Abgedeckt |
| Fremde Wiedergabe einer öffentlichen Playlist schreibt in die eigene Weiterschauen-Liste (Klärungspunkt 4) | 11 | Abgedeckt |
| Entzug der Öffentlich-Kennzeichnung entzieht sofort den Zugriff | 11 | Abgedeckt |
| Ownership-Prüfung bei allen Schreibzugriffen | 1, 2, 4, 9, 10, 11 | Abgedeckt |
| Wiederverwendung bestehender Zugriffskontrolle für Videos (Freischaltung/MediaSource) | 3, 5, 11 | Abgedeckt |
| Lokalisierbare Oberflächentexte inkl. Sicherheitsabfragen | 1, 7, Vorgehensentscheidung „Texte" | Abgedeckt |
| Bestehende Installationen: kein Vorbefüllen, Feature startet leer | Vorgehensentscheidung „Bestandsdaten" | Abgedeckt |

## Abhängigkeitsprüfung

| Prüfpunkt | Befund |
|-----------|--------|
| Zyklen in Abhängigkeiten | Keine gefunden; alle Verweise (2→1, 3→2, 4→3, 5→3, 6→5, 7→2,6, 8→2,3,4, 9→2, 10→1,2, 11→1,3,5,6) zeigen ausschließlich auf niedrigere, existierende Schrittnummern |
| Reihenfolge konsistent zu inhaltlichen Abhängigkeiten | Ja; jeder Schritt steht hinter allen Schritten, deren Ergebnisse er laut Beschreibung voraussetzt |

## Fehlende oder unvollständige Punkte

Keine — der Status lautet `Projektplan vollständig`.

## Hinweise

- Alle acht in `requirement.md` als „Zu klärende Punkte" aufgeführten Fragen sind im Projektplan
  entschieden (Kopieren/Historie, Verhalten bei entfallenen Titeln, Beschränkung auf fünf
  Inhaltstypen, Ausgrauen in öffentlichen Playlists, optionale Beschreibung, Bildkonventionen,
  Mengengrenzen, Wortlaut der Sicherheitsabfrage). Der Abschnitt „Offene Punkte" des Plans ist
  daher zu Recht leer.
- Schritt 11 nennt in seiner Beschreibung auch das Ändern von Bild und Genres als besitzergebundene
  Aktion, führt die Schritte 9 und 10 aber nicht als Abhängigkeit auf. Da beide Schritte in der
  Nummernfolge davorliegen, entsteht daraus keine Reihenfolgeninkonsistenz; eine Ergänzung der
  Abhängigkeitsliste wäre nur eine Präzisierung.
- Schritt 7 behandelt auch das Löschen einer ganzen Playlist, dessen Bedienung in Schritt 1
  entsteht. Auch hier ist die Reihenfolge stimmig, die Abhängigkeit „1" ist über 2 und 6 transitiv
  gegeben.
- Der Plan bündelt die in der Anforderung einzeln aufgeführten API-Endpunkte, Service-Interfaces
  und Entities fachlich in die elf Schritte. Die technische Ausgestaltung (Entity-Felder,
  Fluent-API-Konfiguration, Cascade-Regeln, Migrationen, Testabdeckung) prüft `/plan-check` im
  jeweiligen Lifecycle-Durchlauf.
