# Playlists

Angemeldete Anwender können eigene Playlists anlegen, öffnen, bearbeiten und löschen. Playlists sind
benutzerbezogen und privat: Jeder Anwender sieht und verwaltet ausschließlich seine eigenen
Playlists, unabhängig davon, welche anderen Anwender ebenfalls Playlists angelegt haben.

Dieser Abschnitt umfasst die Verwaltung der Playlists selbst (anlegen, öffnen, bearbeiten, löschen,
Übersicht), das Befüllen einer Playlist mit konkreten Medieninhalten (Filme, Episoden, Staffeln,
Serien, Filmsammlungen), die fortlaufend nachladende Anzeige der Inhalte auf der Detailseite sowie
die manuelle Umsortierung der Einträge per Drag & Drop oder Schnellaktion.

## Übersicht

Unter „Playlists" sieht ein Anwender seine eigenen Playlists als Kacheln (eine Kachel je Playlist)
mit Titel, einer Grafik, einem Symbol für den Sortiermodus sowie dezent dargestelltem Erstellungs-
und Aktualisierungszeitpunkt. Da Playlists noch kein eigenes Coverbild besitzen, zeigt die Kachel
an dieser Stelle einen generischen, farblich je Playlist unterschiedlichen Platzhalter.

Ein Klick (bzw. Tipp) auf eine Kachel öffnet die Detailseite der Playlist (siehe Abschnitt
„Detailseite"). Neue Playlists werden über die Schaltfläche „Neue Playlist erstellen" oberhalb der
Kacheln angelegt. Bearbeiten und Löschen einer Playlist stehen ausschließlich auf der Detailseite
zur Verfügung (siehe dort).

Ist noch keine Playlist vorhanden, zeigt die Übersicht einen Hinweis anstelle der Kacheln an.
Schlägt das Laden der Playlists fehl (z. B. weil der Anwender nicht angemeldet ist), erscheint
eine Fehlermeldung anstelle der Übersicht.

## Anlegen und Bearbeiten

Beim Anlegen oder Bearbeiten einer Playlist werden folgende Angaben erfasst:

- **Name** (Pflichtfeld): maximal 255 Zeichen, darf nicht leer oder nur aus Leerzeichen bestehen.
  Der Name muss pro Anwender eindeutig sein (Groß-/Kleinschreibung wird dabei nicht
  unterschieden); ein doppelter Name führt zu einer Fehlermeldung.
- **Beschreibung** (optional): maximal 2000 Zeichen.
- **Sortiermodus**: entweder „Nach Erscheinungsdatum" (automatisch, Standard) oder „Manuell". Diese
  Angabe lässt sich nur beim **Anlegen** einer neuen Playlist wählen. Beim Bearbeiten einer
  bestehenden Playlist wird der aktuelle Sortiermodus nur noch angezeigt (nicht änderbar); eine
  Änderung des Sortiermodus einer bestehenden Playlist erfolgt ausschließlich über die Aktion
  „Sortiermodus ändern" auf der Detailseite (siehe Abschnitt „Sortiermodus ändern").

Ungültige Eingaben werden sowohl im Formular als auch serverseitig abgelehnt und mit einer
aussagekräftigen Fehlermeldung angezeigt.

## Detailseite

Durch einen Klick auf eine Kachel in der Übersicht oder direkte Navigation gelangt ein Anwender zur
Detailseite einer Playlist. Die Detailseite ist - ähnlich wie die Detailansichten von Filmen und
Serien - mit einem Kopfbereich mit Hintergrundbild aufgebaut (mangels eigenem Coverbild aktuell
ebenfalls ein generischer Platzhalter, siehe Abschnitt „Übersicht"). Der Kopfbereich zeigt:

- **Name** — Name der Playlist (Überschrift)
- **Sortierung** — Symbol und Text für den aktuellen Sortiermodus, daneben eine Schaltfläche zum
  Wechseln (siehe Abschnitt „Sortiermodus ändern")
- **Beschreibung** — Beschreibungstext (falls vorhanden)
- **Erstellt** / **Aktualisiert** — Zeitpunkt der Erstellung bzw. letzten Änderung, dezent dargestellt

Im Kopfbereich stehen zusätzlich als Symbol-Schaltflächen zur Verfügung:

- **Bearbeiten** (Stiftsymbol) — Öffnet das Bearbeitungsformular für die Playlist
- **Löschen** (Papierkorbsymbol) — Löscht die Playlist nach Bestätigung
- **Zurück** (Pfeilsymbol, oben links) — Kehrt zur Playlist-Übersicht zurück

## Inhalte hinzufügen und entfernen

Unterhalb des Kopfbereichs zeigt die Detailseite die Inhalte („Einträge") dieser Playlist als
Kacheln - im gleichen Stil wie die Episoden-Kacheln der Serien-Detailansicht - mit Titelbild, Titel,
zugehöriger Sammlung (falls vorhanden) und Hinzufügedatum. Nur Filme und Episoden erhalten dabei
eine eigene Kachel (siehe Abschnitt „Kaskaden-Logik" weiter unten für den Hintergrund).

### Titelbild

Jeder Eintrag zeigt ein kleines Titelbild des referenzierten Inhalts (Film oder Episode). Ist für
den Inhalt kein eigenes Bild hinterlegt, erscheint stattdessen ein Platzhalterbild.

### Zugriffsstatus in der Liste

Einträge, auf deren Inhalt der angemeldete Anwender keinen Zugriff hat, werden in der Liste
optisch abgeblendet dargestellt, um anzuzeigen, dass die enthaltenen Inhalte derzeit nicht
angeschaut werden können. Zugriff besteht, wenn der Anwender entweder regulären Zugriff auf die
zugrunde liegende Mediaquelle hat oder der Inhalt individuell für ihn freigeschaltet wurde
(bei Filmen, Staffeln und Episoden über die übergeordnete Filmsammlung bzw. Serie); dies gilt für
alle Medientypen. Der **Entfernen**-Button bleibt für solche Einträge weiterhin nutzbar — der
Anwender kann einen nicht zugänglichen Eintrag also jederzeit aus der Playlist entfernen. Die
**Abspielen**-Schaltfläche erscheint dagegen nicht, und ein Doppelklick auf die Zeile startet
keine Wiedergabe (siehe Abschnitt „Wiedergabe starten").

### Sortierung und Anzeige

Steht die Playlist im Sortiermodus „Nach Erscheinungsdatum" (Standard), erscheinen die Einträge
automatisch chronologisch nach Erscheinungsdatum des jeweiligen Inhalts. Besitzt ein Eintrag kein
Erscheinungsdatum, wird er stattdessen anhand seiner Serien-/Staffel-/Episodenzugehörigkeit
eingeordnet; ist auch das nicht möglich, richtet sich die Reihenfolge danach, wann der Eintrag der
Playlist hinzugefügt wurde. Im Sortiermodus „Manuell" erscheinen die Einträge in der vom Anwender
selbst festgelegten Reihenfolge (siehe Abschnitt „Manuelle Sortierung").

Enthält eine Playlist viele Einträge, werden zunächst nur die ersten davon angezeigt. Beim
Herunterscrollen der Liste werden automatisch weitere Einträge nachgeladen und angehängt, sodass
die Seite auch bei sehr umfangreichen Playlists flüssig bedienbar bleibt.

### Manuelle Sortierung

Steht eine Playlist im Sortiermodus „Manuell", kann der Anwender die Reihenfolge der Einträge
selbst festlegen. Neu hinzugefügte Einträge werden dabei stets ans Ende der bisherigen Reihenfolge
angehängt. Zum Umsortieren stehen zwei gleichwertige Wege zur Verfügung:

- **Drag & Drop**: Ein Eintrag lässt sich per Maus greifen (Mauszeiger wechselt über einer
  ziehbaren Zeile zu einer Greifhand) und auf einen anderen Eintrag ziehen, um ihn dorthin zu
  verschieben. Ein kurzer Hinweistext oberhalb der Liste erinnert im manuellen Modus an diese
  Möglichkeit.
- **Schnellaktionen**: Jeder Eintrag besitzt im manuellen Modus zusätzlich die Schaltflächen
  **„An Anfang"** und **„An Ende"**, mit denen sich der Eintrag ohne Ziehen sofort an den Anfang
  bzw. das Ende der Liste verschieben lässt — nützlich insbesondere bei vielen Einträgen oder wenn
  Drag & Drop nicht bequem nutzbar ist. Für eine Zielposition mitten in der Liste bleibt Drag & Drop
  der einzige Weg.

Jede Umsortierung wird sofort gespeichert und bleibt auch nach einem Neuladen der Seite erhalten.

### Sortiermodus ändern

Der Sortiermodus einer bestehenden Playlist lässt sich im Kopfbereich der Detailseite über eine
Symbol-Schaltfläche (zeigt das Symbol des aktuellen Sortiermodus) jederzeit auf den jeweils anderen
Modus umstellen:

- **Wechsel zu „Manuell"**: Die Einträge übernehmen als Ausgangsreihenfolge die zuletzt
  angezeigte, automatisch nach Erscheinungsdatum sortierte Reihenfolge. Ab diesem Zeitpunkt lässt
  sich die Reihenfolge wie im Abschnitt „Manuelle Sortierung" beschrieben frei anpassen.
- **Wechsel zu „Nach Erscheinungsdatum"**: Da die bisherige manuelle Reihenfolge dabei
  unwiederbringlich verloren geht, erscheint zuvor ein Bestätigungsdialog mit einer entsprechenden
  Warnung. Erst nach ausdrücklicher Bestätigung wird der Sortiermodus tatsächlich umgestellt; ein
  Abbrechen belässt die Playlist im manuellen Modus mit der bisherigen Reihenfolge unverändert.

### Hinzufügen

Zum Hinzufügen eines Medieninhalts gibt der Anwender einen Suchbegriff in das Suchfeld oberhalb der
Einträge-Liste ein. Bereits während der Eingabe (mit einer kurzen Verzögerung, damit nicht bei
jedem Tastendruck eine eigene Suche ausgelöst wird) durchsucht das System alle fünf Medientypen —
**Film**, **Serie**, **Staffel**, **Episode** und **Filmsammlung** — nach passenden Namen und zeigt
die Treffer als Kacheln mit Titelbild, Titel und Medientyp an. Ein Klick auf eine Kachel fügt den
zugehörigen Medieninhalt sofort zur Playlist hinzu; eine manuelle Eingabe von internen IDs ist
nicht mehr nötig. Die Suche berücksichtigt dabei keine Groß-/Kleinschreibung — eine Suche nach
„breaking bad" findet also auch einen Eintrag mit dem Namen „Breaking Bad". Das gilt auch für
deutsche Umlaute und ß: Eine Suche nach „mörder" findet ebenso einen Eintrag mit dem Namen
„Mörder" wie „MÖRDER".

Nur Inhalte, auf die der Anwender bereits Zugriff hat (reguläre Mediaquelle oder individuelle
Freischaltung, siehe Abschnitt „Zugriffsstatus in der Liste"), erscheinen in den Suchergebnissen.
Liefert ein Suchbegriff keine Treffer, zeigt die Oberfläche eine entsprechende Hinweismeldung an.

**Kaskaden-Logik:** Wird eine Serie, Staffel oder Filmsammlung hinzugefügt, werden automatisch
auch alle zugehörigen Staffeln/Episoden bzw. Filme mit aufgenommen. Beispiele:
- Hinzufügen einer **Serie** → alle Staffeln und Episoden dieser Serie werden hinzugefügt
- Hinzufügen einer **Staffel** → alle Episoden dieser Staffel werden hinzugefügt
- Hinzufügen einer **Filmsammlung** → alle Filme dieser Sammlung werden hinzugefügt

Die hinzugefügte Serie/Staffel/Filmsammlung selbst wird dabei ebenfalls als Eintrag der Playlist
gespeichert (rein organisatorisch, u. a. damit sie beim Entfernen wiedererkannt wird), erscheint
auf der Detailseite aber bewusst **nicht** als eigene Kachel — sichtbar sind ausschließlich die
sich daraus ergebenden Filme und Episoden, da nur diese direkt abspielbar sind.

Falls Teile der Kaskade bereits in der Playlist vorhanden sind, werden diese übersprungen,
ohne dass dies dem Anwender als Fehler angezeigt wird. Nur neue Inhalte werden hinzugefügt.

**Duplikat-Prüfung:** Ein Medieninhalt darf nicht doppelt in derselben Playlist vorkommen.
Der Versuch, einen bereits vorhandenen Inhalt erneut hinzuzufügen, wird **nicht** mit einer
Fehlermeldung abgelehnt. Stattdessen werden die bereits vorhandenen Inhalte übersprungen
und der Anwender erhält eine aussagekräftige Rückmeldung, z. B. „3 Titel hinzugefügt, 2 bereits
vorhanden und übersprungen." oder „Alle 5 Titel waren bereits vorhanden." Die Operation wird
erfolgreich abgeschlossen (grüne Info-Meldung), auch wenn nichts Neues hinzugefügt wurde.

### Entfernen

Jeder Eintrag in der Liste besitzt einen **Entfernen**-Button. Ein Klick darauf entfernt
genau diesen Eintrag sofort aus der Playlist. Die Liste wird unmittelbar aktualisiert.

Befindet sich der zu entfernende Titel noch mit Bezug zu genau dieser Playlist in der
Weiterschauen-Liste, erscheint zuvor eine Sicherheitsabfrage: „Dieser Eintrag befindet sich in
deiner Weiterschauen-Liste. Entfernen?" Erst nach ausdrücklicher Bestätigung wird der Eintrag
tatsächlich entfernt. Der betroffene Weiterschauen-Eintrag wird dabei durch den nächsten in
dieser Playlist verfügbaren Titel ersetzt (die Wiedergabeposition beginnt dann wieder bei null);
gibt es keinen weiteren verfügbaren Titel, wird der Weiterschauen-Eintrag entfernt. Weiterschauen-
Einträge desselben Videos ohne Playlist-Bezug oder mit Bezug zu einer anderen Playlist bleiben in
jedem Fall unangetastet.

### Automatische Bereinigung

Wird ein Medieninhalt aus dem Bestand entfernt (z. B. eine Serie oder ein Film gelöscht),
verschwindet der zugehörige Playlist-Eintrag beim nächsten Laden der Playlist still
(ohne Fehlermeldung oder Hinweismeldung für den Anwender). Diese automatische Bereinigung
verhindert, dass die Playlist auf nicht mehr existierende Inhalte verweist. Dasselbe
Ersetzen-/Entfernen-Verhalten wie beim manuellen Entfernen gilt dabei sinngemäß auch für einen
betroffenen Weiterschauen-Eintrag — allerdings ohne Sicherheitsabfrage, da der Anwender diesen
Vorgang nicht selbst ausgelöst hat.

## Wiedergabe aus einer Playlist und Weiterschauen-Integration

Titel lassen sich direkt aus einer Playlist heraus abspielen, mit automatischem und manuellem
Weiterschalten sowie einer durchgängigen Anzeige, aus welcher Playlist und an welcher Position
gerade abgespielt wird. Die Wiedergabe wird dabei automatisch mit der Weiterschauen-Liste verknüpft:
Wird ein Video aus einer Playlist gestartet und später pausiert, speichert das System den Playlist-Bezug
für diesen Eintrag sowie die Wiedergabeposition. Beim Fortsetzen wird die Wiedergabe im gleichen 
Playlist-Kontext **mit der gespeicherten Position** rekonstruiert.

### Wiedergabe starten

Auf der Detailseite besitzt jeder abspielbare Eintrag (Film oder Episode) eine
**Abspielen**-Schaltfläche; alternativ startet ein Doppelklick auf die Kachel die Wiedergabe ab
genau diesem Eintrag. Der Video-Player öffnet sich daraufhin mit dem gewählten Titel und zeigt
oberhalb des Players einen Playlist-Badge mit Playlist-Name und Position an, z. B.
„[Meine Favoriten: 3/12]". 

Wurde ein Video aus dieser Playlist bereits früher pausiert und in der Weiterschauen-Liste gespeichert,
startet der Player automatisch an der gespeicherten Wiedergabeposition statt bei 0:00.

Sammel-Einträge (Serie, Staffel, Filmsammlung) erhalten wie beschrieben ohnehin keine eigene
Kachel und damit auch keine Abspielen-Schaltfläche, da sie nicht direkt abspielbar sind — nur
Filme und Episoden lassen sich starten. Nicht zugängliche (gesperrte) Einträge (siehe Abschnitt
„Zugriffsstatus in der Liste") erhalten zwar eine Kachel, zeigen aber ebenfalls keine
Abspielen-Schaltfläche, und ein Doppelklick auf ihre Kachel bleibt wirkungslos.

### Navigation innerhalb der Playlist

Solange aus einer Playlist heraus abgespielt wird, zeigt der Video-Player zusätzlich zu den
üblichen Video-Bedienelementen zwei Schaltflächen „Vorheriger (Playlist)" und „Nächster
(Playlist)" an. Ein Klick darauf lädt den vorherigen bzw. nächsten Titel in der aktuell gültigen
Sortierreihenfolge der Playlist (siehe Abschnitt „Sortierung und Anzeige") und aktualisiert den
Playlist-Badge entsprechend (z. B. „3/12" → „4/12").

Dabei werden automatisch übersprungen:

- **Sammel-Einträge** (Serie, Staffel, Filmsammlung), da sie nicht direkt abspielbar sind
- **Nicht freigeschaltete Einträge**, auf die der Anwender keinen Zugriff hat

Wird dabei über „Nächster (Playlist)" das Ende der Playlist erreicht, ohne dass ein weiterer
abspielbarer und zugänglicher Titel gefunden wird, erscheint der Hinweis „Ende der Playlist
erreicht." mit einer Schaltfläche **„Neu starten"**, über die sich die Playlist erneut von vorne
abspielen lässt (siehe auch Abschnitt „Automatisches Weiterschalten"). Wird dagegen über
„Vorheriger (Playlist)" der Anfang der Playlist erreicht, bleibt der Klick einfach wirkungslos —
es erscheint keine Meldung, da hier kein Fehlerzustand vorliegt.

### Automatisches Weiterschalten

Endet der aktuell abgespielte Titel, schaltet der Player automatisch zum nächsten abspielbaren
und zugänglichen Titel der Playlist weiter (dieselbe Übersprunglogik wie bei der manuellen
Navigation) und aktualisiert den Playlist-Badge. Ein Eingreifen des Anwenders ist dafür nicht
nötig.

Ist das Ende der Playlist erreicht, stoppt die Wiedergabe ohne Fehlermeldung; stattdessen
erscheint der Hinweis „Ende der Playlist erreicht." mit einer Schaltfläche **„Neu starten"**, über
die sich die Playlist erneut von vorne abspielen lässt.

### Wiedergabe außerhalb einer Playlist

Wird ein Titel nicht aus einer Playlist heraus gestartet (z. B. direkt aus der Medienbibliothek
oder den Suchergebnissen), zeigt der Video-Player weder den Playlist-Badge noch die
Playlist-Navigationsschaltflächen an. Die Wiedergabe verhält sich in diesem Fall unverändert wie
bisher.

### Browser-Neuladen während der Wiedergabe

Der aktuell abgespielte Eintrag wird in der Adresszeile als Abfrageparameter geführt
(`?entryId=…`). Lädt der Anwender die Seite während der Playlist-Wiedergabe neu, wird dieselbe
Position automatisch wiederhergestellt.

## Löschen

Playlists werden nach Bestätigung in einem Dialog endgültig gelöscht (kein Papierkorb).

## Zugriff und Berechtigungen

Alle Playlist-Funktionen erfordern eine Anmeldung. Jeder Anwender kann nur auf seine eigenen
Playlists zugreifen: Der Versuch, eine fremde Playlist zu bearbeiten oder zu löschen, wird
abgelehnt. Falls eine Playlist nicht existiert, wird ebenfalls eine Fehlermeldung angezeigt.

Wird ein Benutzerkonto gelöscht, werden auch alle Playlists dieses Anwenders automatisch entfernt.

## Weiterschauen mit Playlist-Bezug

Jedes Mal, wenn Sie ein Video direkt aus einer Playlist heraus starten und später pausieren, wird
diese Information gespeichert. In Ihrer **Weiterschauen-Liste** erscheint dieser Titel dann mit einem
Hinweis wie „In Playlist: Meine Favoriten". Wenn Sie diesen Eintrag später anklicken, wird die
Wiedergabe genau dort fortgesetzt, wo Sie pausiert haben — **im gleichen Playlist-Kontext mit der 
gespeicherten Position**.

Dies hat mehrere Vorteile:

- Sie können dasselbe Video mehrfach in der Weiterschauen-Liste haben: einmal ohne Playlist (wenn Sie es
  einzeln angesehen haben) und mehrfach mit verschiedenen Playlists (je nachdem, aus welcher Playlist
  Sie es gestartet haben). Jede Variante hat ihren eigenen Fortschritt.
- Die Funktionen „Ausblenden" und „Überspringen" wirken nur auf die jeweilige Playlist-Variante. Sie können
  z. B. ein Video in einer Playlist ausblenden, es aber weiterhin in einer anderen Playlist fortsetzen.
- Die globale Markierung „als gesehen" ist weiterhin playlist-übergreifend: Wenn Sie ein Video zu Ende
  schauen, werden alle Varianten (mit und ohne Playlist-Bezug) aus der Weiterschauen-Liste entfernt.

Wird eine Playlist gelöscht, bleiben die Weiterschauen-Einträge bestehen und verlieren ihren Playlist-Bezug —
sie werden zu normalen Einträgen ohne Playlist-Zuordnung. Falls bereits ein Eintrag ohne Playlist-Bezug für 
das gleiche Video existiert, wird das Duplikat automatisch entfernt, um Inkonsistenzen zu vermeiden.

Weitere Details siehe [Weiterschauen – Beschreibung](weiterschauen/beschreibung.md).

## Konfiguration (Administratoren)

Optional lässt sich die maximale Anzahl an Playlists pro Anwender begrenzen. Der Wert wird in der
Konfiguration festgelegt:

```json
{
  "Playlists": {
    "MaxPlaylistsPerUser": null,
    "MaxPlaylistItemCount": null,
    "DefaultPageSize": 20,
    "MaxPageSize": 100
  }
}
```

`null` bedeutet keine Begrenzung (Standardeinstellung). Ein numerischer Wert begrenzt die Anzahl
der Playlists, die ein einzelner Anwender gleichzeitig anlegen kann; beim Überschreiten wird das
Anlegen weiterer Playlists mit einer Fehlermeldung abgelehnt.

`MaxPlaylistItemCount` ist für eine spätere Begrenzung der Anzahl an Einträgen pro Playlist
vorbereitet. Die Konfiguration existiert bereits, wird aber aktuell noch nicht durchgesetzt
(`null`, unbegrenzt).

`DefaultPageSize` legt fest, wie viele Einträge auf der Detailseite pro Ladevorgang beim Scrollen
nachgeladen werden (Standard: 20). `MaxPageSize` begrenzt die höchstzulässige Anzahl an Einträgen
pro Ladevorgang (Standard: 100).
