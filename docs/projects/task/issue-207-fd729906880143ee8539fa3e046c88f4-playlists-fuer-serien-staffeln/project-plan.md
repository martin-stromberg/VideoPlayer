# Projektplan: Playlists für Serien, Staffeln, Episoden, Filme und Filmsammlungen

## Übersicht

Das Projekt führt benutzergesteuerte Playlists ein, in denen Serien, einzelne Staffeln, einzelne
Episoden, Filmsammlungen und einzelne Filme frei kombiniert werden können. Playlists lassen sich
wahlweise automatisch nach Erscheinungsdatum oder manuell per Drag & Drop sortieren, aus ihnen
heraus abspielen und in die Weiterschauen-Liste einbinden, wobei dasselbe Video mehrfach – einmal
je Playlist-Kontext – dort erscheinen darf. Ergänzend entstehen automatische Nachlieferung neuer
Inhalte, abgeleitete Genres, Playlist-Abbildungen und öffentliche, nur lesbare Playlists.
Betroffen sind die Bereiche Playlist-Verwaltung, Medienauswahl, Wiedergabe, Weiterschauen-Liste,
Genres, Bildverwaltung und Zugriffskontrolle.

## Grobe Vorgehensentscheidungen

| Bereich | Entscheidung | Begründung |
|---------|-------------|------------|
| Lieferreihenfolge | Zuerst die tragende Grundfunktion (Playlist anlegen, füllen, anzeigen, abspielen), danach die Komfort- und Zusatzfunktionen (automatische Nachlieferung, Genres, Abbildungen, öffentliche Playlists) | Nach wenigen Schritten steht ein für Anwender nutzbares Feature; die Zusatzfunktionen sind unabhängig voneinander nachlieferbar |
| Inhaltstypen | Playlists nehmen ausschließlich die fünf Inhaltstypen Serie, Staffel, Episode, Film und Filmsammlung auf; weitere Inhaltstypen sind nicht vorgesehen | Der Medienbestand kennt keine weiteren Inhaltstypen; eine Offenhaltung für hypothetische Typen würde nur unnötigen Aufwand erzeugen |
| Sammel-Einträge | Beim Hinzufügen einer Serie, einer Staffel oder einer Filmsammlung werden die enthaltenen Videos einzeln in die Playlist übernommen; zusätzlich wird gemerkt, aus welchem Sammel-Eintrag sie stammen | Nur so lassen sich einzelne Episoden manuell umsortieren und gezielt entfernen, während später neu erscheinende Episoden oder Filme automatisch nachgeliefert werden können |
| Sortiermodus | Der Sortiermodus wird beim Anlegen der Playlist gewählt und ist später änderbar; „automatisch nach Erscheinungsdatum" ist die Vorbelegung | Der weit überwiegende Anwendungsfall ist die chronologische Wiedergabe; manuelle Sortierung ist der Sonderfall |
| Sortierung ohne Datum | Fehlt einem Titel ein Erscheinungsdatum, wird bei automatischer Sortierung ersatzweise nach Serien-/Staffel-/Episodenreihenfolge und, wenn auch das fehlt, nach Aufnahmezeitpunkt in die Playlist sortiert | Verhindert willkürliche Reihenfolgen bei unvollständigen Metadaten, wie sie im Bestand vorkommen |
| Mengengrenzen | Es gibt keine feste fachliche Obergrenze für Titel je Playlist oder Playlists je Anwender; Listen laden seitenweise nach. Ergänzend gibt es je ein optionales, über die Anwendungskonfiguration setzbares Maximum, das standardmäßig deaktiviert ist | Anwender sollen fachlich nicht künstlich eingeschränkt werden; Betreiber großer Installationen können bei Bedarf dennoch begrenzen |
| Sichtbarkeit | Öffentlich schalten dürfen ausschließlich Administratoren; reguläre Anwender legen ausschließlich private Playlists an | Die Sichtbarkeit von Inhalten für andere Anwender bleibt kontrollierbar |
| Entfallene Titel | Verschwindet ein Titel aus dem Medienbestand, wird der zugehörige Playlist-Eintrag still entfernt; ein betroffener Weiterschauen-Eintrag wird nach derselben Logik behandelt wie beim manuellen Entfernen | Es entstehen keine toten Playlist-Einträge, und der Anwender wird nicht mit Meldungen zu Vorgängen behelligt, die er nicht ausgelöst hat |
| Weiterschauen | Der Playlist-Bezug ergänzt die bestehende Weiterschauen-Logik, ersetzt sie nicht; Einträge ohne Playlist-Bezug bleiben unverändert bestehen | Bestehendes Verhalten bleibt für alle Anwender erhalten, auch wenn sie keine Playlists nutzen |
| Gesehen-Markierung | Die globale Gesehen-Markierung eines Videos bleibt playlist-unabhängig | Ein Video gilt fachlich als gesehen, unabhängig davon, über welchen Weg es abgespielt wurde |
| Kein Kopieren, keine Historie | Playlists lassen sich weder duplizieren noch klonen, und es wird keine Historie früherer Stände geführt | Ausdrückliche Vorgabe der Anforderung; hält Datenmodell und Bedienung schlank |
| Texte | Alle für Anwender sichtbaren Texte, einschließlich der Sicherheitsabfragen, werden lokalisierbar hinterlegt | Entspricht dem bestehenden Umgang mit Oberflächentexten im System |
| Bestandsdaten | Bestehende Installationen erhalten keine vorbefüllten Playlists; das Feature startet leer | Playlists sind rein benutzergesteuert, es gibt keine sinnvolle automatische Ableitung aus dem Bestand |

## Entwicklungsschritte

### Schritt 1: Playlists anlegen und verwalten

**Beschreibung:** Angemeldete Anwender sollen eigene Playlists anlegen, umbenennen und löschen
können. Eine Playlist besitzt einen Namen, optional eine Beschreibung und einen Sortiermodus, der
entweder „automatisch nach Erscheinungsdatum" oder „manuell" lautet und beim Anlegen gewählt sowie
später geändert werden kann. Vorbelegt ist die automatische Sortierung. Es soll eine Übersicht
geben, in der ein Anwender alle seine Playlists sieht und von dort aus eine Playlist öffnen,
bearbeiten oder löschen kann. Beim Löschen ist eine Bestätigung einzuholen. Playlists sind
grundsätzlich privat: Nur der Besitzer darf seine Playlists sehen, ändern und löschen. Ein
Kopieren oder Duplizieren von Playlists ist nicht vorgesehen, ebenso wenig eine Historie
vorheriger Änderungsstände. Der Name einer Playlist ist eine Pflichtangabe; leere Namen werden
mit einer verständlichen Meldung abgelehnt. Für die Anzahl der Playlists je Anwender gibt es
fachlich keine feste Obergrenze; zusätzlich soll sich über die Anwendungskonfiguration ein
Maximum festlegen lassen, das standardmäßig deaktiviert ist und beim Überschreiten mit einer
verständlichen Meldung zum Abbruch der Neuanlage führt. Alle für Anwender sichtbaren Texte sind
lokalisierbar zu hinterlegen.

**Abhängigkeiten:** Keine

**Betroffene Bereiche:** Playlist-Verwaltung, Benutzerkonto, Zugriffskontrolle, Konfiguration

---

### Schritt 2: Inhalte zu einer Playlist hinzufügen und daraus entfernen

**Beschreibung:** Anwender sollen einer eigenen Playlist Inhalte hinzufügen können, und zwar
einzelne Filme, einzelne Episoden, einzelne Staffeln, komplette Serien und komplette
Filmsammlungen. Andere Inhaltstypen sind nicht vorgesehen. Wird eine Serie hinzugefügt, gelangen
alle zugehörigen Staffeln und deren Episoden in die Playlist; wird eine Staffel hinzugefügt, alle
ihre Episoden; wird eine Filmsammlung hinzugefügt, alle darin enthaltenen Filme. Dabei ist
festzuhalten, aus welchem Sammel-Eintrag ein Titel stammt, damit später erscheinende Inhalte
desselben Sammel-Eintrags zugeordnet werden können. Ein und derselbe Titel darf innerhalb einer
Playlist nur einmal vorkommen: Beim Hinzufügen bereits enthaltener Titel werden diese übersprungen
und der Anwender erhält einen entsprechenden Hinweis. Einzelne Titel sollen sich wieder aus der
Playlist entfernen lassen, ohne dass dies Auswirkungen auf den Titel selbst oder auf andere
Playlists hat. Hinzufügen und Entfernen darf nur der Besitzer der Playlist. Beliebige
Kombinationen aus Serien, Staffeln, Episoden, Filmen und Filmsammlungen innerhalb einer Playlist
sind ausdrücklich erlaubt. Verschwindet ein Titel aus dem Medienbestand, wird der zugehörige
Playlist-Eintrag ohne Rückfrage und ohne Meldung entfernt, so dass keine toten Einträge
zurückbleiben. Für die Anzahl der Titel je Playlist gibt es fachlich keine feste Obergrenze; zusätzlich
soll sich über die Anwendungskonfiguration ein Maximum festlegen lassen, das standardmäßig
deaktiviert ist und beim Überschreiten das Hinzufügen mit einer verständlichen Meldung
verhindert.

**Abhängigkeiten:** 1

**Betroffene Bereiche:** Playlist-Verwaltung, Medienauswahl, Zugriffskontrolle, Konfiguration

---

### Schritt 3: Playlist-Inhalte anzeigen und automatisch nach Erscheinungsdatum sortieren

**Beschreibung:** Beim Öffnen einer Playlist sollen deren Titel in einer Liste angezeigt werden,
die auch bei sehr vielen Einträgen flüssig bedienbar bleibt und weitere Einträge seitenweise beim
Scrollen nachlädt, so dass die Anzeige ohne feste Obergrenze für die Anzahl der Titel auskommt.
Steht die Playlist auf automatischer Sortierung, werden alle Titel chronologisch nach
Erscheinungsdatum angeordnet; die Reihenfolge ist in diesem Modus nicht von Hand veränderbar.
Fehlt einem Titel das Erscheinungsdatum, wird ersatzweise nach Serien-, Staffel- und
Episodenreihenfolge und, falls auch diese nicht bestimmbar ist, nach dem Zeitpunkt der Aufnahme
in die Playlist sortiert. Zu jedem Titel sind die üblichen Angaben wie Titelbild, Bezeichnung und
Zugehörigkeit zu Serie, Staffel oder Filmsammlung sichtbar. Titel, für die der Anwender keine
Freischaltung besitzt, werden ausgegraut dargestellt und lassen sich nicht starten, bleiben aber
als Bestandteil der Playlist sichtbar.

**Abhängigkeiten:** 2

**Betroffene Bereiche:** Playlist-Anzeige, Sortierung, Freischaltung/Zugriffskontrolle

---

### Schritt 4: Manuelle Sortierung einer Playlist

**Beschreibung:** Steht eine Playlist auf manueller Sortierung, sollen Anwender die Reihenfolge
der enthaltenen Titel selbst bestimmen können. Titel lassen sich per Drag & Drop an eine andere
Position ziehen; zusätzlich stehen je Titel die Aktionen „An den Anfang" und „An das Ende" zur
Verfügung, damit auch bei sehr langen Listen große Sprünge ohne langes Ziehen möglich sind. Die
geänderte Reihenfolge wird dauerhaft gespeichert und gilt für Anzeige und Wiedergabe der
Playlist. Neu hinzugefügte Titel werden im manuellen Modus grundsätzlich am Ende der Playlist
angehängt. Wechselt eine Playlist von automatischer auf manuelle Sortierung, wird die zuletzt
gültige chronologische Reihenfolge als Ausgangsreihenfolge übernommen; beim Wechsel von manueller
auf automatische Sortierung wird wieder chronologisch sortiert, und der Anwender wird vorab
darauf hingewiesen, dass seine manuelle Reihenfolge dadurch verloren geht. Umsortieren darf nur
der Besitzer der Playlist.

**Abhängigkeiten:** 3

**Betroffene Bereiche:** Playlist-Anzeige, Sortierung

---

### Schritt 5: Wiedergabe aus einer Playlist heraus

**Beschreibung:** Anwender sollen eine Playlist ab einem beliebigen enthaltenen Titel abspielen
können. Während der Wiedergabe ist erkennbar, aus welcher Playlist der laufende Titel stammt und
an welcher Stelle er sich darin befindet. Ist ein Titel zu Ende, wird automatisch der in der
Playlist folgende Titel gestartet; ebenso soll manuell zum nächsten oder vorherigen Titel der
Playlist gewechselt werden können. Maßgeblich ist dabei immer die aktuell gültige
Sortierreihenfolge der Playlist. Titel, für die der Anwender keine Freischaltung besitzt, werden
beim Weiterschalten übersprungen. Ist das Ende der Playlist erreicht, endet die Wiedergabe ohne
Fehlermeldung. Wird dasselbe Video außerhalb einer Playlist gestartet, bleibt das bisherige
Wiedergabeverhalten unverändert.

**Abhängigkeiten:** 3

**Betroffene Bereiche:** Wiedergabe, Playlist-Anzeige, Freischaltung/Zugriffskontrolle

---

### Schritt 6: Weiterschauen-Einträge mit Playlist-Bezug

**Beschreibung:** Wird ein Video aus einer Playlist heraus abgespielt und nicht zu Ende gesehen,
soll der zugehörige Weiterschauen-Eintrag den Bezug zu dieser Playlist behalten und beim
Fortsetzen die Wiedergabe wieder im Kontext dieser Playlist starten. In der Weiterschauen-Liste
ist erkennbar, zu welcher Playlist ein Eintrag gehört. Dasselbe Video darf mehrfach in der
Weiterschauen-Liste erscheinen, sofern die Einträge zu unterschiedlichen Playlists oder zur
Wiedergabe ohne Playlist gehören; diese Einträge werden getrennt voneinander fortgeschrieben,
ausgeblendet und gelöscht. Die globale Markierung „gesehen" eines Videos bleibt davon unberührt
und gilt weiterhin playlist-übergreifend. Für Wiedergaben ohne Playlist-Bezug bleibt das
bisherige Verhalten der Weiterschauen-Liste unverändert.

**Abhängigkeiten:** 5

**Betroffene Bereiche:** Weiterschauen-Liste, Wiedergabe, Playlist-Verwaltung

---

### Schritt 7: Sicherheitsabfrage beim Entfernen von Titeln mit Weiterschauen-Bezug

**Beschreibung:** Wird ein Titel aus einer Playlist entfernt, für den in der Weiterschauen-Liste
noch ein Eintrag mit Bezug zu genau dieser Playlist existiert, soll der Anwender vor dem
Entfernen darauf hingewiesen werden und das Entfernen ausdrücklich bestätigen müssen. Die
Sicherheitsabfrage lautet „Dieser Eintrag befindet sich in deiner Weiterschauen-Liste. Entfernen?"
und ist wie alle anderen Oberflächentexte lokalisierbar zu hinterlegen. Bestätigt der Anwender,
wird der betroffene Weiterschauen-Eintrag durch den nächsten in dieser Playlist verfügbaren Titel
ersetzt; gibt es keinen weiteren verfügbaren Titel, wird der Weiterschauen-Eintrag entfernt.
Weiterschauen-Einträge desselben Videos ohne Playlist-Bezug oder mit Bezug zu einer anderen
Playlist bleiben in jedem Fall unangetastet. Dasselbe Ersetzen- bzw. Entfernen-Verhalten gilt
sinngemäß, wenn ein Titel nicht vom Anwender entfernt wird, sondern aus dem Medienbestand
verschwindet und sein Playlist-Eintrag daraufhin still entfernt wird; in diesem Fall entfällt die
Sicherheitsabfrage, weil der Anwender den Vorgang nicht ausgelöst hat. Wird eine ganze Playlist
gelöscht, werden alle Weiterschauen-Einträge mit Bezug zu dieser Playlist entfernt, während
Einträge ohne oder mit anderem Playlist-Bezug erhalten bleiben.

**Abhängigkeiten:** 2, 6

**Betroffene Bereiche:** Playlist-Verwaltung, Weiterschauen-Liste

---

### Schritt 8: Automatische Nachlieferung neu erschienener Inhalte

**Beschreibung:** Enthält eine Playlist eine komplette Serie, eine komplette Staffel oder eine
komplette Filmsammlung, sollen später hinzukommende Inhalte dieses Sammel-Eintrags – etwa eine
neue Staffel einer Serie, eine neue Episode einer Staffel oder ein neuer Film einer Filmsammlung
– automatisch in die Playlist aufgenommen werden, ohne dass der Anwender die Serie oder Sammlung
erneut hinzufügen muss. Bei automatischer Sortierung werden die neuen Titel gemäß ihrem
Erscheinungsdatum an der passenden Stelle einsortiert; bei manueller Sortierung werden sie am
Ende angehängt, damit die vom Anwender festgelegte Reihenfolge erhalten bleibt. Titel, die der
Anwender zuvor bewusst einzeln aus der Playlist entfernt hat, werden nicht erneut aufgenommen.
Bereits enthaltene Titel werden nicht doppelt hinzugefügt. Ist für die Playlist ein
konfiguriertes Maximum an Titeln aktiv und erreicht, werden keine weiteren Titel nachgeliefert.
Der Abgleich läuft im Hintergrund und darf den laufenden Betrieb nicht spürbar beeinträchtigen;
sein zeitlicher Abstand und die je Durchlauf verarbeitete Menge sind über die
Anwendungskonfiguration einstellbar.

**Abhängigkeiten:** 2, 3, 4

**Betroffene Bereiche:** Playlist-Verwaltung, Medienbestand/Hintergrundverarbeitung, Sortierung,
Konfiguration

---

### Schritt 9: Genres einer Playlist

**Beschreibung:** Zu jeder Playlist sollen Genres geführt werden, die sich standardmäßig
automatisch aus den Genres der enthaltenen Titel ergeben und sich aktualisieren, sobald Titel
hinzugefügt oder entfernt werden. Übernommen werden dabei alle in den enthaltenen Titeln
vorkommenden Genres; angezeigt werden sie nach Häufigkeit absteigend sortiert, wobei die Anzeige
auf die ersten Einträge begrenzt wird, damit die Darstellung übersichtlich bleibt. Die Genres
einer Playlist werden in der Playlist-Übersicht und in der Detailansicht angezeigt und lassen
sich wie die Genres anderer Inhalte zum Filtern und Suchen nutzen. Der Besitzer einer Playlist
soll die automatisch abgeleiteten Genres von Hand überschreiben können; ab dann bleibt seine
Auswahl bestehen und wird nicht mehr automatisch verändert. Eine überschriebene Auswahl soll sich
wieder auf die automatische Ableitung zurücksetzen lassen.

**Abhängigkeiten:** 2

**Betroffene Bereiche:** Playlist-Verwaltung, Genres, Suche/Filterung

---

### Schritt 10: Abbildungen für Playlists

**Beschreibung:** Jede Playlist soll eine Abbildung erhalten, die in der Playlist-Übersicht und in
der Detailansicht angezeigt wird. Der Besitzer kann entweder ein eigenes Bild hochladen oder ein
Bild automatisch aus den Abbildungen der enthaltenen Titel erzeugen lassen. Das automatisch
erzeugte Bild ist eine Collage aus den Abbildungen der ersten enthaltenen Titel, wobei höchstens
fünf Bilder verwendet werden und die Auswahl der Bildquellen der Reihenfolge Serienbilder,
Episodenbilder, Filmsammlungsbilder, Filmbilder folgt. Für Auflösung, Format und Ablage gelten
dieselben Konventionen wie für die bereits im System automatisch erzeugten Bilder. Ein
hochgeladenes Bild hat immer Vorrang vor einem automatisch erzeugten. Die automatische Erzeugung
soll sich jederzeit erneut anstoßen lassen, etwa nachdem sich der Inhalt der Playlist geändert
hat. Ist weder ein eigenes noch ein erzeugtes Bild vorhanden, wird eine neutrale Ersatzdarstellung
angezeigt. Hochgeladene Bilder werden auf gängige, zulässige Bildformate und auf eine maximale
Dateigröße geprüft, die über die Anwendungskonfiguration festgelegt wird; unzulässige Uploads
werden mit einer verständlichen Meldung abgelehnt.

**Abhängigkeiten:** 1, 2

**Betroffene Bereiche:** Playlist-Verwaltung, Bildverwaltung, Konfiguration

---

### Schritt 11: Öffentliche Playlists mit Leseberechtigung

**Beschreibung:** Playlists sollen als öffentlich gekennzeichnet werden können. Diese
Kennzeichnung dürfen ausschließlich Administratoren setzen und wieder entfernen; regulären
Anwendern wird die Möglichkeit gar nicht erst angeboten, ihre Playlists bleiben stets privat. Eine
öffentliche Playlist ist für alle berechtigten Anwender sichtbar und abspielbar, aber
ausschließlich lesend: Nur der Besitzer darf sie umbenennen, ihren Inhalt ändern, umsortieren, ihr
Bild oder ihre Genres ändern oder sie löschen. Für andere Anwender werden
Bearbeitungsmöglichkeiten gar nicht erst angeboten. Titel, für die ein Betrachter keine Freischaltung besitzt, werden
ihm ausgegraut und nicht abspielbar dargestellt. Öffentliche Playlists erscheinen in einer eigenen
Übersicht, getrennt von den eigenen Playlists des Anwenders. Spielt ein anderer Anwender eine
öffentliche Playlist ab, werden seine Wiedergabefortschritte wie gewohnt in seiner eigenen
Weiterschauen-Liste mit Bezug zu dieser Playlist geführt, ohne die Playlist selbst oder die
Fortschritte anderer zu verändern. Wird die Kennzeichnung als öffentlich wieder entfernt,
verlieren andere Anwender sofort den Zugriff.

**Abhängigkeiten:** 1, 3, 5, 6

**Betroffene Bereiche:** Playlist-Verwaltung, Zugriffskontrolle, Playlist-Anzeige,
Weiterschauen-Liste

## Übersichtstabelle

| # | Titel | Abhängigkeiten | Betroffene Bereiche |
|---|-------|-----------------|----------------------|
| 1 | Playlists anlegen und verwalten | Keine | Playlist-Verwaltung, Benutzerkonto, Zugriffskontrolle, Konfiguration |
| 2 | Inhalte zu einer Playlist hinzufügen und daraus entfernen | 1 | Playlist-Verwaltung, Medienauswahl, Zugriffskontrolle, Konfiguration |
| 3 | Playlist-Inhalte anzeigen und automatisch nach Erscheinungsdatum sortieren | 2 | Playlist-Anzeige, Sortierung, Freischaltung |
| 4 | Manuelle Sortierung einer Playlist | 3 | Playlist-Anzeige, Sortierung |
| 5 | Wiedergabe aus einer Playlist heraus | 3 | Wiedergabe, Playlist-Anzeige, Freischaltung |
| 6 | Weiterschauen-Einträge mit Playlist-Bezug | 5 | Weiterschauen-Liste, Wiedergabe, Playlist-Verwaltung |
| 7 | Sicherheitsabfrage beim Entfernen von Titeln mit Weiterschauen-Bezug | 2, 6 | Playlist-Verwaltung, Weiterschauen-Liste |
| 8 | Automatische Nachlieferung neu erschienener Inhalte | 2, 3, 4 | Playlist-Verwaltung, Hintergrundverarbeitung, Sortierung, Konfiguration |
| 9 | Genres einer Playlist | 2 | Playlist-Verwaltung, Genres, Suche/Filterung |
| 10 | Abbildungen für Playlists | 1, 2 | Playlist-Verwaltung, Bildverwaltung, Konfiguration |
| 11 | Öffentliche Playlists mit Leseberechtigung | 1, 3, 5, 6 | Playlist-Verwaltung, Zugriffskontrolle, Playlist-Anzeige, Weiterschauen-Liste |

## Offene Punkte

Keine.
