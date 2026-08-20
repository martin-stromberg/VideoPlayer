# Übersetzte Anforderung

## Metadaten

- **Aufgaben-ID:** 0cb2008a-7cd4-4113-ae7a-55c3bdfa4f62
- **Branch:** `task/issue-126-0cb2008a7cd44113ae7a55c3bdfa4f62-editiermodus-fuer-serien-staff`
- **Titel:** Editiermodus für Serien, Staffeln, Episoden, Filme und Filmsammlungen
- **Erstellt:** 2026-08-20

## Ziel

Auf den Detailseiten von Serien, Staffeln, Episoden, Filmen und Filmsammlungen soll ein kontextabhängiger Editiermodus verfügbar sein. Nutzer sollen die zentralen Metadaten des aktuell angezeigten Objekts bearbeiten, speichern oder verwerfen können. Über die Oberfläche geänderte Daten müssen dauerhaft vor dem Überschreiben durch spätere Scanvorgänge geschützt werden.

## Funktionale Anforderungen

1. Auf der Detailseite jedes unterstützten Objekttyps befindet sich neben dem Favoriten-Stern ein Stift-Symbol zum Aktivieren des Editiermodus.
2. Im Editiermodus werden die bisherigen Anzeigen für Titel, Jahr und Genres, Plot sowie der Abspiel-Button ausgeblendet.
3. Im Editiermodus werden stattdessen Eingabefelder für Titel und Datum, eine horizontale Genre-Auswahlleiste sowie ein mehrzeiliges Plot-Eingabefeld angezeigt.
4. Der Editiermodus bearbeitet immer die Daten des aktuell angezeigten Objekts:
   - Serienansicht: Seriendaten
   - Staffeldetail: Staffeldaten
   - Episodenansicht: Episodendaten
   - Filmansicht: Filmdaten
   - Filmsammlung: Sammlungsdaten
5. Das Stift-Symbol wird im Editiermodus durch ein Speichern-Symbol ersetzt. Zusätzlich wird ein Abbrechen-Button zum Verwerfen der laufenden Änderungen angezeigt.
6. In der Serienansicht kann im Editiermodus über ein Stift-Symbol neben der Staffel-Auswahl in die Bearbeitung der Staffeldaten gewechselt werden.
7. Die Auswahl einer anderen Staffel wechselt automatisch zur Bearbeitung der ausgewählten Staffel.
8. Die Auswahl einer Episode wechselt zur Bearbeitung der Episodendaten.
9. Über ein Symbol links oben im Kopfbereich kann aus der Staffel- oder Episodenbearbeitung in die Seriendateneingabe gewechselt werden, sofern diese nicht bereits aktiv ist.
10. Für Filme und Filmsammlungen gilt ein entsprechender Wechselmechanismus:
    - Ein Symbol links oben im Kopfbereich wechselt zur Sammlungsdateneingabe.
    - Die Auswahl eines Films wechselt zur Filmdateneingabe.
11. Vor jedem Wechsel des Bearbeitungskontexts oder dem Verwerfen von Änderungen wird geprüft, ob ungespeicherte Änderungen vorhanden sind. In diesem Fall muss der Nutzer bestätigen, dass die Änderungen verworfen werden sollen.
12. Beim Speichern werden die Änderungen dem aktuell bearbeiteten Objekt zugeordnet und dauerhaft gespeichert.
13. Über die Programmoberfläche geänderte Objekte werden als manuell bearbeitet beziehungsweise geschützt gekennzeichnet, sodass nachfolgende Scanvorgänge ihre Daten nicht überschreiben.
14. Der Schutz vor Überschreiben gilt sowohl für den initialen Scan als auch für Scanvorgänge nach einer Änderung der zugrunde liegenden Informationsdatei.
15. Für alle genannten Editier-, Wechsel-, Speicher-, Abbruch- und Überschreibschutzfunktionen werden automatisierte Tests erstellt.

## Nichtfunktionale Anforderungen

- Die Bearbeitung muss unabhängig vom Objekttyp konsistent bedienbar sein.
- Ungespeicherte Änderungen dürfen bei Kontextwechseln nicht ohne ausdrückliche Bestätigung verloren gehen.
- Die bestehende Anzeige- und Auswahlfunktionalität für Serien, Staffeln, Episoden, Filme und Filmsammlungen darf außerhalb des Editiermodus nicht beeinträchtigt werden.
- Die gespeicherten manuellen Änderungen müssen über nachfolgende Scanvorgänge hinweg persistent bleiben.
- Die Umsetzung muss sich in die vorhandenen UI-, Datenmodell- und Testkonventionen einfügen.

## Akzeptanzkriterien

- [ ] Auf den Detailseiten von Serie, Staffel, Episode, Film und Filmsammlung kann der Editiermodus über ein Stift-Symbol aktiviert werden.
- [ ] Im Editiermodus werden Titel, Jahr und Genres, Plot sowie Abspiel-Button durch die vorgesehenen Eingabeelemente ersetzt.
- [ ] Die Eingabefelder werden mit den Daten des aktuell angezeigten Objekts vorbelegt und speichern Änderungen dem korrekten Objekttyp zu.
- [ ] Das Stift-Symbol wird während der Bearbeitung durch ein Speichern-Symbol ersetzt und ein Abbrechen-Button wird angezeigt.
- [ ] Änderungen können gespeichert oder verworfen werden.
- [ ] Wechsel zwischen Serie, Staffel und Episode sowie zwischen Filmsammlung und Film funktioniert über die vorgesehenen Symbole und Auswahlen.
- [ ] Bei ungespeicherten Änderungen wird vor einem Kontextwechsel oder Verwerfen eine Bestätigung verlangt.
- [ ] Manuell geänderte Objekte werden durch spätere initiale oder aktualisierte Scanvorgänge nicht überschrieben.
- [ ] Automatisierte Tests decken alle genannten Bearbeitungs-, Wechsel-, Speicher-, Abbruch- und Scan-Schutzfälle ab.

## Abgrenzung

- Die Anforderung umfasst die Bearbeitung der genannten Metadaten und den Schutz dieser Änderungen vor Scanvorgängen.
- Eine Änderung der externen Informationsquellen oder des Scanverfahrens außerhalb des erforderlichen Überschreibschutzes ist nicht Bestandteil der Anforderung.
- Neue Metadatenfelder außerhalb von Titel, Datum, Genres und Plot sind nicht Bestandteil der Anforderung.

## Offene Punkte

- Welche konkrete Datumsbedeutung und welches Eingabeformat gelten für die einzelnen Objekttypen?
- Welche Genres stehen in der horizontalen Genre-Auswahl zur Verfügung und wie werden Mehrfachauswahlen gespeichert?
- Wie lautet der genaue Bestätigungsdialog für das Verwerfen ungespeicherter Änderungen und welche Aktionen stehen darin zur Verfügung?
- Wie wird der manuelle Überschreibschutz im bestehenden Datenmodell persistiert und kann ein Nutzer ihn später wieder aufheben?
- Welche Rechte oder Rollen dürfen die Metadaten bearbeiten?
