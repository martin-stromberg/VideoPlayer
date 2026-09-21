# Playlists

Angemeldete Anwender können eigene Playlists anlegen, öffnen, bearbeiten und löschen. Playlists sind
benutzerbezogen und standardmäßig privat: Jeder Anwender sieht und verwaltet ausschließlich seine
eigenen Playlists, unabhängig davon, welche anderen Anwender ebenfalls Playlists angelegt haben.
Ausschließlich Administratoren können eigene Playlists als **öffentlich** kennzeichnen; eine
öffentliche Playlist ist für alle Anwender sichtbar und abspielbar, aber nur lesend (siehe Abschnitt
„Öffentliche Playlists").

Dieser Abschnitt umfasst die Verwaltung der Playlists selbst (anlegen, öffnen, bearbeiten, löschen,
Übersicht), das Befüllen einer Playlist mit konkreten Medieninhalten (Filme, Episoden, Staffeln,
Serien, Filmsammlungen), die fortlaufend nachladende Anzeige der Inhalte auf der Detailseite, die
manuelle Umsortierung der Einträge per Ziehen oder Schnellaktion sowie die Abbildung (das
Coverbild) einer Playlist — hochgeladen oder automatisch als Collage aus den Bildern der enthaltenen
Inhalte erzeugt (siehe Abschnitt „Abbildung (Cover)").

## Übersicht

Unter „Playlists" (ein einziger Menüpunkt) sieht ein Anwender seine eigenen Playlists als Kacheln
(eine Kachel je Playlist) — gefolgt von den Playlists, die andere Anwender öffentlich freigegeben haben
(siehe „Filterleiste" und „Fremde Playlists" unten). Die Kacheln sind wie die Film- und Serienkacheln in
deren Übersichten aufgebaut: Das Bild füllt die gesamte Kachelfläche, und der Titel steht als einzige
Beschriftung in einem Verlaufs-Overlay am unteren Rand. Er darf mehrzeilig werden und wird an
Silbengrenzen umgebrochen, damit auch ein sehr langer Name vollständig lesbar bleibt. Weitere Texte
(Öffentlichkeitsstatus als Wort, Erstellungs- und Aktualisierungszeitpunkt, Sortiermodus, Beschreibung,
Genres) zeigt die Kachel bewusst nicht — sie stehen auf der Detailseite. Als Bild zeigt die Kachel die
Abbildung (das Coverbild) der Playlist — entweder ein vom Besitzer hochgeladenes Bild oder eine
automatisch aus den Bildern der enthaltenen Inhalte erzeugte Collage (siehe Abschnitt
„Abbildung (Cover)"). Besitzt eine Playlist noch kein Coverbild, zeigt die Kachel stattdessen einen
generischen, farblich je Playlist unterschiedlichen Farbverlauf als Platzhalter (bewusst ohne Symbol).

Oberhalb der Kacheln steht, sobald mindestens ein Genre unter den eigenen Playlists vorkommt, ein
Auswahlfeld „Nach Genre filtern" zur Verfügung, mit dem sich die Übersicht auf Playlists
beschränken lässt, die ein bestimmtes Genre führen (siehe Abschnitt „Genres").

Ist eine der eigenen Playlists öffentlich, trägt ihre Kachel in der rechten oberen Ecke ein
Globus-Symbol (zugänglicher Name „Öffentlich — für alle Anwender sichtbar und abspielbar (nur lesend)",
siehe Abschnitt „Öffentliche Playlists") — an derselben Stelle wie das Fremd-Symbol fremder Playlists.

### Filterleiste

Rechts oberhalb der Kacheln steht die Filterleiste: **ein** Panel mit drei Schaltbereichen, die durch je
eine senkrechte Trennlinie voneinander abgesetzt sind. Der Rahmen und die Rundung gehören der Leiste als
Ganzes, die Schaltbereiche selbst haben keinen eigenen Rahmen. Der aktuell gewählte Filter ist deutlich
hervorgehoben (gefüllter Akzenthintergrund mit weißem Symbol); jede Schaltfläche hat einen Tooltip und
einen zugänglichen Namen („Alle Playlists", „Eigene Playlists", „Öffentliche Playlists"). Die
Schaltfläche „Neue Playlist" steht rechts daneben auf derselben horizontalen Ebene und hat dieselbe Höhe,
dieselbe Rundung und denselben Stil wie die Schaltbereiche der Filterleiste.

| Symbol | Filter | Zeigt |
|--------|--------|-------|
| vier Kacheln | **Alle** (Voreinstellung) | eigene Playlists (zuerst) und danach die öffentlichen Playlists anderer Anwender |
| Person | **Eigene** | nur die eigenen Playlists, auch die eigenen öffentlichen |
| Globus | **Öffentliche** | alle öffentlichen Playlists: die eigenen öffentlichen und die anderer Anwender |

Eine eigene öffentliche Playlist wird nie doppelt aufgeführt: Sie erscheint einmal als eigene Playlist
(mit dem Kennzeichen „Öffentlich"), nicht zusätzlich als fremde. Der Filter „Nach Genre filtern" wirkt
in allen drei Ansichten (er wird auf eigene und fremde Playlists angewendet) und lässt sich mit der
Filterleiste kombinieren. Gibt es im gewählten Filter keine Playlists, erscheint ein passender Hinweis
(z. B. „Keine eigenen Playlists vorhanden").

Die frühere eigene Adresse `/playlists/public` (öffentliche Übersicht) funktioniert weiter: Sie zeigt
dieselbe Übersicht mit vorgewähltem Filter „Öffentliche". Einen zweiten Menüpunkt „Öffentliche
Playlists" gibt es nicht mehr.

### Fremde Playlists

Kacheln von Playlists, die ein anderer Anwender öffentlich freigegeben hat, tragen in der rechten oberen
Ecke ein kleines Personengruppen-Symbol (Tooltip „Von einem anderen Benutzer freigegeben"). Sie sind
strikt nur lesend: Ein Klick öffnet die Detailseite im Lesemodus, in der Übersicht gibt es keine
Bearbeitungsmöglichkeit. Angaben zum Besitzer werden nicht angezeigt.

Ein Klick (bzw. Tipp) auf eine Kachel öffnet die Detailseite der Playlist (siehe Abschnitt
„Detailseite"). Neue Playlists werden über den Symbol-Button „Neue Playlist" (Plus-Symbol, Tooltip und
zugänglicher Name „Neue Playlist") oberhalb der Kacheln angelegt. Bearbeiten und Löschen einer Playlist stehen ausschließlich auf der Detailseite
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
Detailseite einer Playlist. Sie ist nach dem Muster der Detailseiten von Serien und Filmen aufgebaut:
oben ein **Kopfbereich** mit Hintergrundbild, darunter der Inhaltsbereich mit den Titeln der Playlist.

**Kopfbereich.** Er hat wie bei Serien und Filmen eine **vorgegebene Höhe**, die weder vom Bild noch vom
Textumfang bestimmt wird: Ob das Bild der Playlist sehr hoch, sehr breit oder sehr klein ist oder gar keines
vorhanden ist und wie lang Name und Beschreibung sind — der Kopfbereich bleibt gleich hoch, das Bild wird
passend eingepasst (zugeschnitten), und Name bzw. Beschreibung werden nach wenigen Zeilen mit „…" abgekürzt
(der vollständige Text steht im Bearbeitungsformular).
Das Bild wird wie bei Serien und Filmen **gedämpft** dargestellt (ein Abdunklungsverlauf liegt darüber),
sodass die Informationen der Playlist darüber gut lesbar bleiben. Als Hintergrundbild dient die Abbildung
(das Coverbild) der Playlist; besitzt die Playlist noch kein Coverbild, erscheint stattdessen ein
generischer Platzhalter (siehe Abschnitt „Abbildung (Cover)"). Die Symbol-Schaltflächen sitzen wie bei
Serien **auf dem Bild** (oben rechts im Kopfbereich), nicht darüber.

### Playlist-Informationen im Kopfbereich

Solange kein Titel ausgewählt ist, zeigt der Kopfbereich die Informationen zur Playlist:

- **Name** — Name der Playlist (Überschrift)
- **Sortierung** — Symbol und Text für den aktuellen Sortiermodus, daneben eine Schaltfläche zum
  Wechseln (siehe Abschnitt „Sortiermodus ändern")
- **Beschreibung** — Beschreibungstext (falls vorhanden)
- **Genres** — die Genres der Playlist (falls vorhanden), mit Schaltflächen zum Bearbeiten und
  ggf. Zurücksetzen (siehe Abschnitt „Genres")
- **Erstellt** / **Aktualisiert** — Zeitpunkt der Erstellung bzw. letzten Änderung als zwei klar getrennte,
  beschriftete Angaben im deutschen Format, z. B. „Erstellt: 07.09.2026, 06:23 Uhr" und „Aktualisiert:
  08.09.2026, 21:07 Uhr", dezent dargestellt

### Symbol-Schaltflächen im Kopfbereich

Alle Bearbeitungsmöglichkeiten der Detailseite (die nachfolgenden Symbol-Schaltflächen, der Sortiermodus-Wechsel,
die Genre-Bearbeitung sowie im Inhaltsbereich das Hinzufügen, Entfernen und Umsortieren) stehen ausschließlich
dem **Besitzer** zur Verfügung. Öffnet ein anderer Anwender eine öffentliche Playlist, werden sie nicht
angezeigt (siehe Abschnitt „Öffentliche Playlists"); stattdessen zeigt der Kopfbereich das Kennzeichen
„Öffentlich". Alle reinen Symbol-Schaltflächen haben einen Tooltip und einen zugänglichen Namen.

- **Öffentlich-Kennzeichnung** — nur für Administratoren, die Besitzer der Playlist sind: setzt bzw. entfernt
  die Kennzeichnung „öffentlich" (siehe Abschnitt „Öffentliche Playlists"). Das Symbol zeigt den Zustand:
  ein **Globus** auf farbigem Grund für eine öffentliche, ein **geschlossenes Schloss** für eine private
  Playlist. Der Tooltip nennt den Zustand und was ein Klick bewirkt, z. B. „Privat - nur für Sie sichtbar.
  Klicken, um für alle Anwender zu veröffentlichen" bzw. „Öffentlich - für alle Anwender sichtbar. Klicken, um
  die Veröffentlichung zurückzunehmen".
- **Bereich wechseln** — wechselt zwischen der Titelliste und dem Hinzufügen von Titeln (siehe Abschnitt
  „Zwei getrennte Bereiche"). Wie die Öffentlich-Kennzeichnung zeigt diese Schaltfläche immer nur **ein**
  Symbol, nämlich das des Bereichs, in den ein Klick wechselt: ein **Plus** für „Titel hinzufügen" bzw. eine
  **Liste** für „Titel der Playlist auflisten".
- **Playlist-Bild** (Bild-Symbol) — öffnet das Bild-Panel zum Hochladen, Erzeugen und Entfernen des Bildes
  (siehe Abschnitt „Abbildung (Cover)")
- **Bearbeiten** (Stiftsymbol) — Öffnet das Bearbeitungsformular für die Playlist
- **Löschen** (Papierkorbsymbol) — Löscht die Playlist nach Bestätigung
- **Zurück** (Pfeilsymbol, oben links) — Kehrt zur Playlist-Übersicht zurück (bei einem ausgewählten Titel
  hebt er stattdessen die Auswahl auf, siehe unten)

Das Bearbeitungsformular, die Bestätigung zum Löschen der Playlist, das Bild-Panel, der Genre-Editor und die
Sicherheitsabfragen sind Overlay-Fenster mit **sichtbarem Rahmen** und deckendem Hintergrund, damit sie sich
klar vom restlichen Seiteninhalt abheben.

### Titel auswählen

Ein Klick auf einen Titel in der Liste **wählt ihn aus** — wie die Auswahl einer Episode auf der Serienseite.
Die Kachel ist dann markiert, und der Kopfbereich zeigt statt der Playlist die Informationen des Titels:
Titel, Erscheinungsjahr (falls bekannt), Art (Film bzw. „Episode 3"), die Zugehörigkeit zu Serie, Staffel oder
Filmsammlung, die Handlung (falls vorhanden), das Bild des Titels und das Datum, an dem er hinzugefügt wurde.
Bei einer **Episode** wird – wie auf der Serienseite – deren Bild zum Hintergrund des Kopfbereichs; bei einem Film
erscheint sein Poster neben den Angaben. Für einen nicht freigeschalteten Titel wird kein Bild nachgeladen.
Die Auswahl ist auch per Tastatur bedienbar (mit der Tabulatortaste zur Kachel, **Eingabe** oder
**Leertaste** wählt aus, **Escape** hebt die Auswahl auf) und lässt sich mit dem Zurück-Pfeil im Kopfbereich
aufheben; dann zeigt der Kopfbereich wieder die Playlist. Die Auswahl hat nichts mit der Wiedergabe zu tun und
ändert die Adresse der Seite nicht.

Im Kopfbereich eines ausgewählten Titels stehen zur Verfügung:

- **Abspielen** (große Schaltfläche unten rechts, wie bei Episoden) — startet die Wiedergabe ab diesem Titel
  im Playlist-Kontext (siehe Abschnitt „Wiedergabe starten"). Für einen Titel, für den der Betrachter nicht
  freigeschaltet ist, gibt es sie nicht; stattdessen erklärt ein Hinweis, dass der Titel nicht abspielbar ist.
- **Aus der Playlist entfernen** (Papierkorbsymbol in der Schaltflächenleiste **auf dem Bild**) — nur für den
  Besitzer und nur, solange ein Titel ausgewählt ist: entfernt genau diesen Titel (siehe Abschnitt
  „Entfernen"). Die Schaltflächen der Playlist selbst (Bearbeiten, Löschen, Bild, ...) werden währenddessen
  nicht angezeigt, damit es keine zwei verwechselbaren „Löschen"-Schaltflächen gibt; der Wechsel des Bereichs
  bleibt daneben verfügbar.

### Zwei getrennte Bereiche: Titel der Playlist und Titel hinzufügen

Unterhalb des Kopfbereichs stehen die Bereiche **„Titel der Playlist"** (die Liste) und **„Titel hinzufügen"**
(die Suche) nicht gleichzeitig auf der Seite. Gewechselt wird über eine **Symbol-Schaltfläche im Kopfbereich
auf dem Bild**, bei den übrigen Aktionsschaltflächen — nicht mehr über Schaltflächen im Inhaltsbereich. Es
wird immer nur **eine** Schaltfläche gezeigt, nämlich die für den Wechsel in den jeweils anderen Bereich
(Plus-Symbol „Titel hinzufügen" bzw. Listensymbol „Titel der Playlist auflisten").
Ist die Playlist leer, wird sofort „Titel hinzufügen" angeboten, andernfalls hat die Titelliste Vorrang. Beim
Hinzufügen bleibt der Bereich „Titel hinzufügen" geöffnet, damit mehrere Titel nacheinander hinzugefügt werden
können; wird der letzte Titel entfernt, wechselt die Seite zum Hinzufügen. Beim Wechsel zum Hinzufügen wird
eine Titelauswahl aufgehoben. Anwender, die nicht Besitzer sind, sehen keinen Umschalter, sondern nur die Liste.

## Abbildung (Cover)

Jede Playlist kann eine Abbildung (ein Coverbild) besitzen, die in der Übersicht auf der Kachel und
auf der Detailseite als Hintergrundbild des Kopfbereichs angezeigt wird. Bedient wird sie ausschließlich vom
Besitzer über **eine** Symbol-Schaltfläche (Bild-Symbol) im Kopfbereich der Detailseite. Sie öffnet das
**Bild-Panel**, ein Overlay-Fenster mit Rahmen:

- **Vorschau** — zeigt das aktuelle Bild der Playlist (mit dem Hinweis, ob es hochgeladen oder automatisch
  erzeugt wurde) bzw. „Kein Bild vorhanden". Sobald eine Datei gewählt oder ein Bild erzeugt wurde, zeigt sie
  stattdessen dieses **neue Bild als Vorschau** mit dem Hinweis „noch nicht gespeichert". Die Vorschau wird
  immer auf die Breite und Höhe des Panels begrenzt — auch ein sehr großes, sehr breites oder sehr hohes Bild
  erzeugt keine Scrollbalken im Panel.
- **Bild hochladen** — Auswahl einer Bilddatei. Nach der Auswahl erscheinen die Vorschau sowie Dateiname und
  -größe; der Bestätigungsbutton heißt dann **„Hochladen"** und übernimmt das Bild als Cover.
- **Bild erzeugen** („Aus den Titeln erzeugen") — erzeugt eine Vorschau des automatisch aus den Bildern der
  Playlist-Inhalte zusammengesetzten Bildes. Erzeugen speichert **nichts**; der Bestätigungsbutton heißt dann
  **„Anwenden"** und übernimmt die angezeigte Vorschau erst als Cover. „Abbrechen" schließt das Panel ohne
  Änderung. Es ist immer nur ein Kandidat (Datei **oder** erzeugtes Bild) ausgewählt; ein neuer ersetzt den
  bisherigen.
- **Bild entfernen** — nur sichtbar, wenn die Playlist ein Bild hat; entfernt das bestehende Bild (die Playlist
  zeigt dann wieder den Platzhalter). Ein **hochgeladenes** Bild wird nur nach Rückfrage entfernt („Hochgeladenes
  Bild entfernen — kann nicht wiederhergestellt werden"); ein bloß **automatisch erzeugtes** ohne Rückfrage, da
  es sich jederzeit neu erzeugen lässt.

Akzeptiert werden die gängigen Bildformate JPEG, PNG und WebP bis zu einer Dateigröße von 5 MB (beides kann der
Administrator in der Konfiguration anpassen, siehe Abschnitt „Konfiguration"). Ungeeignete Dateien werden bereits
bei der Auswahl bzw. spätestens beim Hochladen mit einer verständlichen Fehlermeldung **im Panel** abgelehnt,
z. B. „Format BMP wird nicht unterstützt. Erlaubte Formate: JPEG, PNG, WebP.", „Datei zu groß, max. 5 MB erlaubt."
oder „Datei ist kein gültiges Bild.". Maßgeblich ist dabei das Format, das die Datei
tatsächlich enthält — nicht die Dateiendung oder die vom Browser gemeldete Typangabe: Eine GIF-Datei
wird also auch dann abgelehnt, wenn sie als „.png" umbenannt wurde. Zu große Bilder (Standard: mehr
als 4096 Bildpunkte in Breite oder Höhe bzw. mehr als rund 16,8 Megapixel insgesamt) werden mit
„Bild zu groß (… x … Pixel). Erlaubt sind höchstens … Bitte verkleinern Sie das Bild." abgelehnt,
und unvollständig übertragene oder beschädigte Dateien mit „Datei ist beschädigt oder unvollständig
und kann nicht als Bild gelesen werden.". Das hochgeladene Bild wird unverändert im tatsächlich
erkannten Originalformat gespeichert. Das automatisch erzeugte Bild ist eine Collage aus den Bildern der in der
Playlist enthaltenen Inhalte — höchstens fünf Bilder. Dabei werden zuerst die Bilder enthaltener Serien und
Staffeln herangezogen (eine Staffel besitzt kein eigenes Bild und verwendet daher das Bild ihrer Serie), danach
die der Episoden, dann die der Filmsammlungen und zuletzt die der Filme; innerhalb derselben Stufe zählt, welcher
Inhalt zuerst zur Playlist hinzugefügt wurde. Enthält die Playlist keine Inhalte mit Bildern, meldet das Panel
„Keine Bilder verfügbar." — die bisherige Abbildung bzw. der Platzhalter bleiben dabei unverändert bestehen.

**Vorrang des eigenen Bildes:** Ein hochgeladenes Bild wird nie im Hintergrund durch eine automatisch
erzeugte Collage ersetzt — nur eine ausdrückliche Aktion des Besitzers ändert das Cover. Damit ein hochgeladenes
Bild nicht versehentlich verloren geht, weist das Panel **vorher** deutlich darauf hin: Ist das aktuelle Bild ein
hochgeladenes und wurde ein Kandidat gewählt, steht unter der Vorschau „Das aktuell hochgeladene Bild wird ersetzt
und kann nicht wiederhergestellt werden." Erst der ausdrückliche Klick auf „Hochladen" bzw. „Anwenden" führt den
Austausch aus, „Abbrechen" lässt das Bild unverändert. (Der Server verlangt die Bestätigung für das Ersetzen
eines hochgeladenen Bildes durch ein erzeugtes weiterhin selbst — das Panel sendet sie mit dem Klick auf
„Anwenden" mit; ohne sie antwortet der Server mit einem Konflikt, siehe API-Referenz.) Ist das aktuelle Cover
schon automatisch erzeugt oder noch gar nicht vorhanden, entfällt der Hinweis.

**Keine automatische Aktualisierung:** Anders als die Genres einer Playlist (siehe Abschnitt
„Genres") wird das Coverbild bei Änderungen des Inhalts — beim Hinzufügen oder Entfernen von Titeln
ebenso wie bei der automatischen Nachlieferung neuer Inhalte — bewusst **nicht** von selbst neu
erzeugt. Damit das Cover den geänderten Inhalt widerspiegelt, erzeugt der Anwender es im Bild-Panel
selbst neu und wendet es an.

**Ersatzdarstellung:** Besitzt eine Playlist kein Coverbild (noch keines hochgeladen oder erzeugt,
oder das Bild ist nicht ladbar), wird an beiden Stellen ein neutrales, je Playlist farblich
unterschiedlicher Farbverlauf (ohne Symbol) angezeigt. Wird die Playlist gelöscht,
wird auch ihr Coverbild mit entfernt.

## Inhalte hinzufügen und entfernen

Unterhalb des Kopfbereichs zeigt die Detailseite im Bereich „Titel der Playlist" die Inhalte („Einträge")
dieser Playlist als Kacheln - im gleichen Stil wie die Episoden-Kacheln der Serien-Detailansicht - mit
Titelbild, Titel, zugehöriger Sammlung (falls vorhanden) und Hinzufügedatum. Die Kacheln tragen keine
Abspielen- oder Entfernen-Schaltflächen: ein Klick wählt den Titel aus, seine Informationen sowie
**Abspielen** und **Entfernen** stehen dann im Kopfbereich (siehe Abschnitt „Titel auswählen"). Nur Filme und
Episoden erhalten eine eigene Kachel (siehe Abschnitt „Kaskaden-Logik" weiter unten für den Hintergrund).

### Titelbild

Jeder Eintrag zeigt ein kleines Titelbild des referenzierten Inhalts (Film oder Episode). Ist für
den Inhalt kein eigenes Bild hinterlegt, erscheint stattdessen ein Platzhalterbild.

### Zugriffsstatus in der Liste

Einträge, auf deren Inhalt der angemeldete Anwender keinen Zugriff hat, werden in der Liste
optisch abgeblendet dargestellt, um anzuzeigen, dass die enthaltenen Inhalte derzeit nicht
angeschaut werden können. Zugriff besteht, wenn der Anwender entweder regulären Zugriff auf die
zugrunde liegende Mediaquelle hat oder der Inhalt individuell für ihn freigeschaltet wurde
(bei Filmen, Staffeln und Episoden über die übergeordnete Filmsammlung bzw. Serie); dies gilt für
alle Medientypen. Das **Entfernen** im Kopfbereich bleibt für solche Einträge weiterhin nutzbar — der
Anwender kann einen nicht zugänglichen Eintrag also jederzeit aus der Playlist entfernen. Die
**Abspielen**-Schaltfläche erscheint dagegen nicht, und ein Doppelklick auf die Kachel startet
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
die Seite auch bei sehr umfangreichen Playlists flüssig bedienbar bleibt. Schlägt das Nachladen
fehl (z. B. wegen eines Verbindungsproblems), erscheint eine Fehlermeldung mit der Schaltfläche
„Erneut versuchen"; die Anwendung wiederholt den Ladevorgang nicht von selbst, sondern erst nach
einem Klick darauf.

### Manuelle Sortierung

Steht eine Playlist im Sortiermodus „Manuell", kann der Anwender die Reihenfolge der Einträge
selbst festlegen. Neu hinzugefügte Einträge werden dabei stets ans Ende der bisherigen Reihenfolge
angehängt. Zum Umsortieren stehen zwei gleichwertige Wege zur Verfügung:

- **Ziehen einer Kachel**: Ein Eintrag lässt sich mit der Maus greifen und auf die Kachel der
  gewünschten Position ziehen; er nimmt dann deren Platz ein, die übrigen Einträge rücken
  entsprechend auf. Während des Ziehens wird die gezogene Kachel gedämpft dargestellt und die Kachel
  unter dem Zeiger als Ziel markiert (farbiger Rahmen und eine Einfügelinie an der Kante, an der der
  Titel landet), so dass die Zielposition jederzeit sichtbar ist. Die Kacheln stehen je nach
  Fensterbreite in mehreren Spalten mit Abstand dazwischen; wird in der Lücke zwischen Kacheln oder bis etwa eine Kachelhöhe neben einer Kachel losgelassen,
  gilt die nächstliegende als Ziel. Wird dagegen weit außerhalb der Liste losgelassen, bleibt die
  Reihenfolge unverändert und ein kurzer Hinweis am unteren Rand sagt das. Steht der Zeiger am oberen
  oder unteren Fensterrand, scrollt die Liste von selbst weiter, so dass auch Positionen außerhalb des
  Sichtbereichs erreichbar sind. Ein Abbruch ist jederzeit mit der Escape-Taste möglich (der Eintrag
  bleibt dann, wo er war, und wird auch nicht ausgewählt). Ein kurzer Hinweistext oberhalb der Liste
  erinnert im manuellen Modus an diese Möglichkeit.

  Mit Finger oder Stift funktioniert das Ziehen ebenfalls: die Kachel kurz gedrückt halten (etwa eine
  halbe Sekunde), bis sie sich löst, und dann ziehen — ein einfaches Wischen scrollt weiterhin die
  Seite. Auf schmalen Bildschirmen (Handy) füllt eine Kachel allerdings einen großen Teil der Anzeige,
  die Nachbarkachel liegt also außerhalb des Sichtbereichs: Man zieht den Finger dann an den unteren
  oder oberen Rand und wartet, bis die gewünschte Position herangescrollt ist. Für größere Sprünge
  sind dort die Schaltflächen "An Anfang"/"An Ende" der bequemere Weg.
- **Schnellaktionen**: Jeder Eintrag besitzt im manuellen Modus zusätzlich die Schaltflächen
  **„An Anfang"** und **„An Ende"**, mit denen sich der Eintrag ohne Ziehen sofort an den Anfang
  bzw. das Ende der Liste verschieben lässt — nützlich insbesondere bei vielen Einträgen oder wenn
  das Ziehen nicht bequem nutzbar ist. Für eine Zielposition mitten in der Liste bleibt das Ziehen
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

Zum Hinzufügen eines Medieninhalts wechselt der Besitzer mit dem Umschalter zum Bereich „Titel hinzufügen"
(bei einer leeren Playlist ist er von Anfang an geöffnet) und gibt einen Suchbegriff in das Suchfeld ein. Bereits während der Eingabe (mit einer kurzen Verzögerung, damit nicht bei
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

Zum Entfernen wählt der Besitzer den Titel in der Liste aus und klickt im Kopfbereich auf das
**Löschen-Symbol** („Titel aus der Playlist entfernen"). Das entfernt genau diesen Eintrag sofort aus der
Playlist. Die Liste wird unmittelbar aktualisiert, die Auswahl aufgehoben; war es der letzte Titel, wechselt die
Seite zum Bereich „Titel hinzufügen".

Befindet sich der zu entfernende Titel noch mit Bezug zu genau dieser Playlist in der
Weiterschauen-Liste, erscheint zuvor eine Sicherheitsabfrage: „Dieser Eintrag befindet sich in
deiner Weiterschauen-Liste. Entfernen?" Erst nach ausdrücklicher Bestätigung wird der Eintrag
tatsächlich entfernt. Der betroffene Weiterschauen-Eintrag wird dabei durch den nächsten in
dieser Playlist verfügbaren Titel ersetzt (die Wiedergabeposition beginnt dann wieder bei null);
gibt es keinen weiteren verfügbaren Titel, wird der Weiterschauen-Eintrag entfernt. Weiterschauen-
Einträge desselben Videos ohne Playlist-Bezug oder mit Bezug zu einer anderen Playlist bleiben in
jedem Fall unangetastet.

Ein so bewusst entfernter Titel wird sich gemerkt: Sollte dieser Titel später erneut Teil einer in
dieser Playlist enthaltenen Serie, Staffel oder Filmsammlung sein (siehe Abschnitt „Automatische
Nachlieferung neuer Inhalte" unten), nimmt die automatische Nachlieferung ihn **nicht** von selbst
wieder auf. Fügt der Anwender den Titel später jedoch selbst erneut hinzu — einzeln oder als Teil
einer erneut hinzugefügten Serie/Staffel/Sammlung —, gilt diese bewusste Entscheidung wieder und
der Titel wird ab dann auch von der automatischen Nachlieferung wieder berücksichtigt.

### Automatische Bereinigung

Wird ein Medieninhalt aus dem Bestand entfernt (z. B. eine Serie oder ein Film gelöscht),
verschwindet der zugehörige Playlist-Eintrag beim nächsten Laden der Playlist still
(ohne Fehlermeldung oder Hinweismeldung für den Anwender). Diese automatische Bereinigung
verhindert, dass die Playlist auf nicht mehr existierende Inhalte verweist. Dasselbe
Ersetzen-/Entfernen-Verhalten wie beim manuellen Entfernen gilt dabei sinngemäß auch für einen
betroffenen Weiterschauen-Eintrag — allerdings ohne Sicherheitsabfrage, da der Anwender diesen
Vorgang nicht selbst ausgelöst hat.

## Automatische Nachlieferung neuer Inhalte

Enthält eine Playlist eine komplette Serie, eine komplette Staffel oder eine komplette
Filmsammlung als Eintrag (siehe Abschnitt „Kaskaden-Logik" oben), werden später hinzukommende
Inhalte dieses Sammel-Eintrags automatisch in die Playlist aufgenommen — der Anwender muss die
Serie, Staffel oder Sammlung dafür nicht erneut hinzufügen:

- Wird einer bereits enthaltenen **Serie** eine neue Staffel hinzugefügt, erscheinen deren Episoden
  automatisch in der Playlist.
- Wird einer bereits enthaltenen **Staffel** eine neue Episode hinzugefügt, erscheint diese
  automatisch in der Playlist.
- Wird einer bereits enthaltenen **Filmsammlung** ein neuer Film hinzugefügt, erscheint dieser
  automatisch in der Playlist.

Sobald ein neuer Titel in den Medienbestand aufgenommen wird, merkt sich die Anwendung die betroffene
Serie, Staffel oder Filmsammlung. Nach dem Ende des Scans (oder kurz nach einer Änderung außerhalb eines
Scans) werden genau die Playlists aktualisiert, die diese Serie, Staffel oder Sammlung enthalten - alle
anderen Playlists werden dabei nicht angefasst, und ohne Änderung im Medienbestand findet gar keine
Prüfung statt. Die Aktualisierung läuft in kleinen Blöcken mit Pausen und wirkt sich nicht spürbar auf die
sonstige Nutzung der Anwendung aus; ein neuer Titel erscheint daher nicht sofort, sondern kurz nach dem
Scan-Ende. Zusätzlich gleicht die Anwendung einmal täglich alle Playlists mit Serien, Staffeln oder
Filmsammlungen ab, falls einmal eine Vormerkung verloren gegangen sein sollte.

Neu nachgelieferte Titel werden je nach Sortiermodus der Playlist einsortiert:

- **„Nach Erscheinungsdatum"**: Der neue Titel erscheint automatisch an der zu seinem
  Erscheinungsdatum passenden Stelle (siehe Abschnitt „Sortierung und Anzeige").
- **„Manuell"**: Der neue Titel wird ans Ende der bisherigen, vom Anwender festgelegten
  Reihenfolge angehängt, damit diese erhalten bleibt.

Ein Titel, den der Anwender zuvor bewusst einzeln aus der Playlist entfernt hat (siehe Abschnitt
„Entfernen"), wird von dieser automatischen Nachlieferung nicht erneut aufgenommen. Ist für die
Playlist eine maximale Anzahl an Einträgen konfiguriert (siehe Abschnitt „Konfiguration") und
bereits erreicht, werden für diese Playlist keine weiteren Titel nachgeliefert.

## Genres

Zu jeder Playlist werden Genres geführt: In der Übersicht (als Kachel-Zeile) und im Kopfbereich der
Detailseite werden sie angezeigt, nach Häufigkeit absteigend sortiert und auf die ersten fünf
begrenzt, damit die Darstellung übersichtlich bleibt. Diese Genres lassen sich — wie die Genres
anderer Inhalte — zum Filtern nutzen: Das Auswahlfeld „Nach Genre filtern" oberhalb der
Playlist-Kacheln (siehe Abschnitt „Übersicht") beschränkt die Übersicht auf Playlists, die das
gewählte Genre führen; dabei zählt jedes der Playlist zugeordnete Genre, nicht nur die tatsächlich
angezeigten fünf.

### Automatische Ableitung

Standardmäßig ergeben sich die Genres einer Playlist automatisch aus den Genres der enthaltenen
Titel und aktualisieren sich, sobald sich der Inhalt der Playlist ändert — beim manuellen
Hinzufügen oder Entfernen eines Titels ebenso wie bei der automatischen Nachlieferung neuer Inhalte
(siehe Abschnitt „Automatische Nachlieferung neuer Inhalte" oben). Dabei gilt:

- Ein **Film** trägt seine eigenen Genres bei.
- Eine **Serie** trägt ihre eigenen Genres bei.
- Eine **Staffel** oder eine **Episode** trägt die Genres ihrer übergeordneten Serie bei (Staffeln
  und Episoden haben selbst keine eigenen Genres).
- Eine **Filmsammlung** trägt die kombinierten Genres der darin enthaltenen Filme bei.

Übernommen werden dabei alle in den enthaltenen Titeln vorkommenden Genres — nicht nur die
angezeigten fünf; die Begrenzung betrifft ausschließlich die Anzeige, nicht die Filterbarkeit
(siehe oben) oder die zugrunde liegende Ableitung. Entfernt ein Anwender den letzten Titel mit
einem bestimmten Genre aus der Playlist, verschwindet dieses Genre entsprechend wieder.

### Manuelles Überschreiben und Zurücksetzen

Über die Stift-Schaltfläche neben den Genres im Kopfbereich der Detailseite kann der Besitzer einer
Playlist die automatisch abgeleiteten Genres von Hand überschreiben: Ein Dialog zeigt alle
verfügbaren Genres als antippbare Auswahl-Schaltflächen (dasselbe Bedienmuster wie bei der
Genre-Auswahl von Filmen und Serien), aus denen sich eine beliebige eigene Kombination
zusammenstellen und speichern lässt.

Ab dem Speichern bleibt diese manuelle Auswahl bestehen und wird **nicht mehr automatisch
verändert**, auch wenn anschließend Titel zur Playlist hinzugefügt oder daraus entfernt werden. Ein
Hinweis „Manuell festgelegt" neben den Genres zeigt diesen Zustand an, und eine zusätzliche
Schaltfläche (Zurücksetzen-Symbol) erscheint, mit der sich die überschriebene Auswahl jederzeit
wieder auf die automatische Ableitung aus dem aktuellen Playlist-Inhalt zurücksetzen lässt.

## Wiedergabe aus einer Playlist und Weiterschauen-Integration

Titel lassen sich direkt aus einer Playlist heraus abspielen, mit automatischem und manuellem
Weiterschalten sowie einer durchgängigen Anzeige, aus welcher Playlist und an welcher Position
gerade abgespielt wird. Die Wiedergabe wird dabei automatisch mit der Weiterschauen-Liste verknüpft:
Wird ein Video aus einer Playlist gestartet und später pausiert, speichert das System den Playlist-Bezug
für diesen Eintrag sowie die Wiedergabeposition. Beim Fortsetzen wird die Wiedergabe im gleichen 
Playlist-Kontext **mit der gespeicherten Position** rekonstruiert.

### Wiedergabe starten

Auf der Detailseite wählt der Anwender einen abspielbaren Eintrag (Film oder Episode) aus und klickt im
Kopfbereich auf **Abspielen**; alternativ startet ein Doppelklick auf die Kachel die Wiedergabe ab
genau diesem Eintrag. (Das Abspielen aus der Weiterschauen-Liste läuft unabhängig davon über die Adresse mit
dem Eintrag und wählt keinen Titel aus.) Der Video-Player öffnet sich daraufhin mit dem gewählten Titel und zeigt
oberhalb des Players einen Playlist-Badge mit Playlist-Name und Position an, z. B.
„[Meine Favoriten: 3/12]". 

Wurde ein Video aus dieser Playlist bereits früher pausiert und in der Weiterschauen-Liste gespeichert,
startet der Player automatisch an der gespeicherten Wiedergabeposition statt bei 0:00.

Sammel-Einträge (Serie, Staffel, Filmsammlung) erhalten wie beschrieben ohnehin keine eigene
Kachel und damit auch keine Abspielen-Schaltfläche, da sie nicht direkt abspielbar sind — nur
Filme und Episoden lassen sich starten. Nicht zugängliche (gesperrte) Einträge (siehe Abschnitt
„Zugriffsstatus in der Liste") erhalten zwar eine Kachel und lassen sich auswählen, zeigen aber im Kopfbereich
keine Abspielen-Schaltfläche, und ein Doppelklick auf ihre Kachel bleibt wirkungslos.

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

## Öffentliche Playlists

Ein Administrator kann seine **eigenen** Playlists als „öffentlich" kennzeichnen — und die Kennzeichnung
jederzeit wieder entfernen. Dafür gibt es auf der Detailseite (im Kopfbereich, in der Reihe der
Symbol-Schaltflächen) einen Symbol-Button, dessen Symbol den Zustand zeigt (Globus = öffentlich, Schloss =
privat, siehe Abschnitt „Detailseite"). Regulären Anwendern wird dieser
Button gar nicht erst angezeigt (nicht ausgegraut, sondern nicht vorhanden); ihre Playlists bleiben
stets privat. Auch der Server lehnt das Setzen oder Entfernen der Kennzeichnung durch Nicht-Administratoren
ab (HTTP 403).

**Eigene Playlists des Administrators.** Ein Administrator kann die Kennzeichnung nur bei Playlists setzen,
die ihm selbst gehören — nicht bei Playlists anderer Anwender. Die Anforderung verlangt, dass die Playlists
der Anwender stets privat bleiben und nur der Besitzer sie verändern darf; müsste ein Administrator fremde
Playlists sehen, um sie zu veröffentlichen, wäre das ein Widerspruch dazu.

### Was andere Anwender sehen und tun dürfen

Öffentliche Playlists erscheinen für alle angemeldeten Anwender in der **zusammengefassten Playlist-Übersicht**
(ein Menüpunkt „Playlists"): am Ende der Liste der eigenen Playlists, mit einem Symbol in der rechten
oberen Ecke der Kachel, das auf eine fremde Playlist hinweist; mit dem Filter „Öffentliche" der Filterleiste
(oder über die alte Adresse `/playlists/public`) werden nur die öffentlichen Playlists gezeigt. Kacheln wie
bei eigenen Playlists (mit Genre-Filter), Klick öffnet die Detailseite. Es werden keine Angaben zum Besitzer
angezeigt. Die eigenen öffentlichen Playlists eines Administrators erscheinen nur einmal (als eigene) in
der Übersicht, im Filter „Öffentliche" ebenfalls.

Die Detailseite einer öffentlichen Playlist ist für alle außer dem Besitzer **ausschließlich lesend**. Es
gibt weder Bearbeiten noch Löschen, kein Bild-Panel (Hochladen, Erzeugen, Entfernen), keine Genre-Bearbeitung
oder -Rücksetzung, keinen Wechsel des Sortiermodus, keinen Umschalter und keine Suche zum Hinzufügen, kein
Entfernen eines Titels im Kopfbereich und keine Umsortierung (weder „An Anfang"/„An Ende" noch Drag & Drop) —
diese Elemente werden nicht angezeigt. Das gilt auch für einen Administrator, der nicht der Besitzer ist.
Titel lassen sich auswählen (der Kopfbereich zeigt ihre Informationen), Abspielen bleibt möglich,
mit Weiterschalten, Playlist-Badge und Positionswiederherstellung wie bei eigenen Playlists.

**Freischaltung je Betrachter.** Ob ein Titel abspielbar ist, hängt immer vom Betrachter ab (regulärer
Quellenzugriff oder Einzelfreischaltung des *Betrachters*), nicht vom Besitzer: Titel, für die der
Betrachter keine Freischaltung besitzt, werden ihm abgeblendet dargestellt und lassen sich nicht abspielen
(das automatische und manuelle Weiterschalten überspringt sie). Eine Freischaltung, die nur der Besitzer
hat, gilt für den Betrachter nicht.

**Weiterschauen.** Spielt ein anderer Anwender eine öffentliche Playlist ab, wird sein Fortschritt wie
gewohnt in seiner **eigenen** Weiterschauen-Liste geführt, mit Bezug zu dieser Playlist („In Playlist:
…"). Weder die Playlist noch der Fortschritt des Besitzers oder anderer Anwender werden dadurch verändert.

**Kennzeichnung wird entfernt.** Entfernt der Besitzer die Kennzeichnung, verlieren alle anderen Anwender
sofort den Zugriff auf die Playlist (Detailseite, Einträge, Wiedergabe und Cover werden mit 403 abgelehnt;
sie verschwindet aus der Übersicht der anderen Anwender). Die Weiterschauen-Einträge der anderen Anwender mit
Bezug zu dieser Playlist verlieren dabei ihren Playlist-Bezug und werden zu normalen Einträgen ohne
Playlist-Zuordnung (Position bleibt erhalten; existiert für dasselbe Video bereits ein Eintrag ohne
Playlist-Bezug, bleibt dieser bestehen und der Duplikat-Eintrag entfällt). So gibt es keinen Link mehr in
die jetzt private Playlist und ihr Name wird nicht mehr angezeigt.

### Wenn der Besitzer eine öffentliche Playlist ändert

- **Titel entfernen:** Die Sicherheitsabfrage („Dieser Eintrag befindet sich in deiner Weiterschauen-Liste …")
  erscheint weiterhin nur, wenn der Besitzer selbst einen solchen Weiterschauen-Eintrag hat. Weiterschauen-Einträge
  anderer Anwender für den entfernten Titel werden ohne Nachfrage still aufgelöst — dieselbe Regel wie beim
  Besitzer: Ersetzen durch den nächsten Titel der Playlist, der für **diesen** Anwender abspielbar ist
  (Position zurückgesetzt), oder Entfernen, wenn es keinen gibt. Die Abfrage gibt nichts über andere Anwender preis.
- **Playlist löschen:** Die Weiterschauen-Einträge aller Anwender behalten ihre Position und verlieren den
  Playlist-Bezug (Duplikate zu bereits vorhandenen Einträgen ohne Playlist werden entfernt), wie bei eigenen Einträgen.
- **Verschwindet ein Titel aus der Bibliothek** (Medium oder Medienquelle gelöscht), gilt die Ersetzen-/Entfernen-Regel
  ebenfalls für die Einträge aller Anwender.
- **Lesender Zugriff verändert nichts:** Öffnet ein anderer Anwender die Playlist, werden Einträge, deren Medium nicht
  mehr existiert, für ihn lediglich nicht angezeigt; die automatische Bereinigung dieser verwaisten Einträge
  (siehe „Automatische Bereinigung") führt nur ein Zugriff des Besitzers durch.

## Zugriff und Berechtigungen

Alle Playlist-Funktionen erfordern eine Anmeldung. Der Zugriff folgt diesem Rollenmodell:

| Aktion | Besitzer | Anderer Anwender, Playlist privat | Anderer Anwender, Playlist öffentlich |
|--------|----------|-----------------------------------|---------------------------------------|
| Playlist ansehen (Detail, Einträge, Cover) | ja | nein (403) | ja |
| Abspielen, Weiterschalten | ja | nein (403) | ja — nur freigeschaltete Titel |
| Umbenennen, Inhalt ändern, umsortieren, Bild, Genres, Sortiermodus, löschen | ja | nein (403) | **nein (403)** |
| „öffentlich" setzen/entfernen | nur als Administrator | nein (403) | nein (403), auch nicht als Administrator |

Ein Administrator, der nicht der Besitzer ist, hat also keinerlei zusätzliche Rechte an fremden Playlists.
Der Versuch, eine fremde Playlist zu bearbeiten oder zu löschen, wird serverseitig abgelehnt — unabhängig davon,
ob die Oberfläche die Bedienelemente anbietet. Falls eine Playlist nicht existiert, wird ebenfalls eine
Fehlermeldung angezeigt.

Wird ein Benutzerkonto gelöscht, werden auch alle Playlists dieses Anwenders automatisch entfernt; die
Weiterschauen-Einträge anderer Anwender mit Bezug zu dessen öffentlichen Playlists verlieren dabei ihren
Playlist-Bezug (wie beim Löschen einer Playlist).

## Weiterschauen mit Playlist-Bezug

Jedes Mal, wenn Sie ein Video direkt aus einer Playlist heraus starten und später pausieren, wird
diese Information gespeichert. In Ihrer **Weiterschauen-Liste** erscheint dieser Titel dann mit einem
Hinweis wie „In Playlist: Meine Favoriten". Wenn Sie diesen Eintrag später anklicken, wird die
Wiedergabe genau dort fortgesetzt, wo Sie pausiert haben — **im gleichen Playlist-Kontext mit der 
gespeicherten Position**.

Dies hat mehrere Vorteile:

- Sie können dasselbe Video mehrfach in der Weiterschauen-Liste haben: einmal ohne Playlist (wenn Sie es
  einzeln angesehen haben) und mehrfach mit verschiedenen Playlists (je nachdem, aus welcher Playlist
  Sie es gestartet haben). Jede Variante hat ihren eigenen Fortschritt — je Playlist aber immer nur
  einen einzigen Eintrag (siehe unten).
- Die Funktionen „Ausblenden" und „Überspringen" wirken nur auf die jeweilige Playlist-Variante. Sie können
  z. B. ein Video in einer Playlist ausblenden, es aber weiterhin in einer anderen Playlist fortsetzen.
- Die globale Markierung „als gesehen" ist weiterhin playlist-übergreifend: Wenn Sie ein Video zu Ende
  schauen, werden alle Varianten (mit und ohne Playlist-Bezug) aus der Weiterschauen-Liste entfernt.

**Eine Playlist — ein Eintrag.** Je Playlist gibt es immer höchstens **einen** Eintrag in Ihrer
Weiterschauen-Liste. Rufen Sie aus derselben Playlist einen anderen Titel auf, ersetzt dieser den
bisherigen Eintrag — auch dann, wenn es sich um eine andere Serie oder um einen Film handelt. Eine
Playlist wird also als Ganzes fortgesetzt und nicht in mehrere Einträge aufgeteilt. Einträge anderer
Playlists und Einträge ohne Playlist-Bezug bleiben davon unberührt.

**Der Nachfolger ist der nächste Titel der Playlist.** Erreicht ein aus einer Playlist gestarteter
Titel seine Endsequenz (standardmäßig die letzten 30 Sekunden, konfigurierbar) oder wählen Sie
„Überspringen", rückt der nächste Titel **der Playlist** in deren aktueller Sortierung nach — nicht
die nächste Episode der Serie. Dadurch geht es am Ende einer Serie mit der nächsten Serie derselben
Playlist weiter; Titel, die Sie aus der Playlist entfernt haben, sowie für Sie gesperrte und nicht
direkt abspielbare Sammel-Einträge (Serie, Staffel, Filmsammlung) werden dabei übersprungen. Ist die
Playlist zu Ende, verschwindet der Eintrag ersatzlos aus der Weiterschauen-Liste — auch dann, wenn die
Serie selbst noch weitergehen würde.

Ohne Playlist-Bezug bleibt es beim gewohnten Verhalten: Nachfolger ist die nächste Episode der Serie
bzw. der nächste Film der Sammlung.

Wird eine Playlist gelöscht, bleiben die Weiterschauen-Einträge bestehen und verlieren ihren Playlist-Bezug —
sie werden zu normalen Einträgen ohne Playlist-Zuordnung. Falls bereits ein Eintrag ohne Playlist-Bezug für 
das gleiche Video existiert, wird das Duplikat automatisch entfernt, um Inkonsistenzen zu vermeiden.

Bei einer **öffentlichen Playlist eines anderen Anwenders** entsteht der Weiterschauen-Eintrag in Ihrer
eigenen Liste und verändert weder die Playlist noch den Fortschritt des Besitzers (siehe Abschnitt „Öffentliche
Playlists"). Wird die Kennzeichnung „öffentlich" entfernt, verliert Ihr Eintrag den Playlist-Bezug.

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
    "MaxPageSize": 100,
    "BackfillBatchSize": 25,
    "BackfillBlockPauseSeconds": 2,
    "BackfillSettleSeconds": 10,
    "BackfillSafetySweepIntervalHours": 24,
    "AllowedCoverImageFormats": "image/jpeg,image/png,image/webp",
    "MaxCoverImageSizeBytes": 5242880,
    "MaxCoverImageWidthPixels": 4096,
    "MaxCoverImageHeightPixels": 4096,
    "MaxCoverImageTotalPixels": 16777216,
    "GeneratedCoverWidthPixels": 1600,
    "GeneratedCoverHeightPixels": 520,
    "GeneratedCoverJpegQuality": 85
  }
}
```

`null` bedeutet keine Begrenzung (Standardeinstellung). Ein numerischer Wert begrenzt die Anzahl
der Playlists, die ein einzelner Anwender gleichzeitig anlegen kann; beim Überschreiten wird das
Anlegen weiterer Playlists mit einer Fehlermeldung abgelehnt.

`MaxPlaylistItemCount` begrenzt die Anzahl an Einträgen pro Playlist (Standard: `null`,
unbegrenzt). Ist die Grenze erreicht, lehnt sowohl das manuelle Hinzufügen weiterer Titel als
auch die automatische Nachlieferung (siehe Abschnitt „Automatische Nachlieferung neuer Inhalte")
weitere Titel für diese Playlist ab.

`DefaultPageSize` legt fest, wie viele Einträge auf der Detailseite pro Ladevorgang beim Scrollen
nachgeladen werden (Standard: 20). `MaxPageSize` begrenzt die höchstzulässige Anzahl an Einträgen
pro Ladevorgang (Standard: 100).

Die automatische Nachlieferung läuft nicht in einem festen Takt, sondern wird durch neu erfasste Titel
angestoßen (siehe Abschnitt „Automatische Nachlieferung neuer Inhalte"). `BackfillBatchSize` begrenzt,
wie viele Playlists in einer Arbeitseinheit (Block) aktualisiert werden (Standard: 25);
`BackfillBlockPauseSeconds` ist die Pause zwischen zwei Blöcken (Standard: 2 Sekunden), damit auch ein großer
Nachzug den laufenden Betrieb nicht spürbar beeinträchtigt. `BackfillSettleSeconds` (Standard: 10 Sekunden)
ist die kurze Wartezeit nach dem Scan-Ende, damit mehrere Änderungen gebündelt werden.
`BackfillSafetySweepIntervalHours` legt den Abstand des täglichen Sicherheitslaufs über alle betroffenen
Playlists fest (Standard: 24 Stunden, `0` schaltet ihn aus); der Zeitpunkt des letzten Laufs wird
gespeichert, so dass er auch bei häufigen Neustarts nicht öfter als einmal je Intervall läuft.

Die folgenden Werte steuern die Abbildung (das Coverbild) einer Playlist (siehe Abschnitt
„Abbildung (Cover)"):

- `AllowedCoverImageFormats` legt fest, welche Bildformate beim Hochladen akzeptiert werden —
  als kommagetrennte Liste der Format-Bezeichner (Standard: `image/jpeg,image/png,image/webp`).
- `MaxCoverImageSizeBytes` begrenzt die Dateigröße eines hochgeladenen Bildes in Bytes
  (Standard: 5242880 = 5 MB).
- `MaxCoverImageWidthPixels` und `MaxCoverImageHeightPixels` begrenzen die Breite bzw. Höhe eines
  hochgeladenen Bildes in Bildpunkten (Standard: je 4096), `MaxCoverImageTotalPixels` zusätzlich die
  Gesamtzahl der Bildpunkte (Breite × Höhe; Standard: 16777216, also 4096 × 4096). Die Prüfung erfolgt
  anhand der Bildkopfdaten, bevor das Bild dekodiert wird; ein Wert von 0 oder kleiner schaltet die
  jeweilige Prüfung ab. Beim vollständigen Prüfen eines Bildes wird kurzzeitig Arbeitsspeicher in
  der Größenordnung von vier Byte je Bildpunkt benötigt (bei 4096 × 4096 rund 64 MB) — die Grenzen
  sollten daher nicht ohne Not stark angehoben werden.
- `GeneratedCoverWidthPixels` und `GeneratedCoverHeightPixels` legen die Abmessungen der
  automatisch erzeugten Collage in Bildpunkten fest (Standard: 1600 × 520).
- `GeneratedCoverJpegQuality` steuert die Bildqualität der erzeugten Collage auf einer Skala
  von 0 bis 100 (Standard: 85).
