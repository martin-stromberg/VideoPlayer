# Anforderung

## Ziel

Die GitHub-Startseite des Projekts soll durch eine schlankere README besser fuer externe Besucher geeignet sein. Zusaetzlich soll die Webanwendung eine About-Seite erhalten, die kurz erklaert, was die Anwendung ist, wie Anwender ihre Videos sichtbar machen, und auf die GitHub-Projektseite verweist.

## Umfang

- README als GitHub-Startseite ueberarbeiten.
- Unnoetige interne Informationen aus der README entfernen oder auf bestehende Detaildokumente verlinken.
- About-Seite fuer die Webanwendung unter der bereits verlinkten Route `/about` erstellen.
- About-Seite mit kurzer Anwendungsbeschreibung, ersten Schritten zum Anzeigen eigener Videos und GitHub-Link ausstatten.
- Umsetzung in den bestehenden Entwicklungsworkflow aufnehmen und planbar machen.

## Nicht-Ziele

- Keine Aenderung am Release-Prozess selbst.
- Keine neuen externen Abhaengigkeiten.
- Keine Aenderung an Authentifizierung, Medien-Scanning oder Datenmodell, ausser sie ist fuer die About-Seite zwingend erforderlich.

## Akzeptanzkriterien

- README wirkt als oeffentliche GitHub-Startseite fokussiert und enthaelt keine internen Veroeffentlichungs-/Hook-Details im Hauptfluss.
- README verweist fuer Details auf die vorhandene Dokumentation.
- `/about` rendert eine eigenstaendige Seite ohne 404.
- About-Seite beschreibt die Anwendung knapp.
- About-Seite erklaert die ersten Schritte: Konto/Einrichtung, Medienquelle anlegen, Scan/Klassifizierung starten bzw. abwarten, Quelle in der Navigation oeffnen, Video abspielen.
- About-Seite enthaelt einen Link zu `https://github.com/martin-stromberg/VideoPlayer`.
- Build und relevante Tests laufen erfolgreich.
