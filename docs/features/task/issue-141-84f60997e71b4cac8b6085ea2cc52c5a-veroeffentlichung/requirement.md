# Übersetzte Anforderung

## Metadaten

- **Aufgaben-ID:** 84f60997-e71b-4cac-8b60-85ea2cc52c5a
- **Branch:** `task/issue-141-84f60997e71b4cac8b6085ea2cc52c5a-veroeffentlichung`
- **Titel:** Veröffentlichung

## Ziel

Das Repository soll öffentlich zugänglich und für private Nutzung frei verfügbar werden. Gleichzeitig sollen kommerzielle Nutzungen einer vorherigen Genehmigung unterliegen. Die Veröffentlichung muss durch nachvollziehbare Lizenz-, Qualitäts- und Installationsinformationen unterstützt werden.

## Funktionale Anforderungen

1. Das Repository erhält eine Lizenzregelung, die private Nutzung ohne vorherige Genehmigung erlaubt und kommerzielle Nutzung von einer vorherigen Genehmigung abhängig macht.
2. Die Lizenzregelung wird im Repository an den dafür vorgesehenen Stellen eindeutig und konsistent ausgewiesen, insbesondere in der Lizenzdatei und in der Projektbeschreibung.
3. Für Markdown-Dateien wird ein Git-Hook eingerichtet, der beim vorgesehenen Prüfzeitpunkt verwaiste beziehungsweise nicht erreichbare lokale und repositoryinterne Links erkennt und den Vorgang bei gefundenen Fehlern mit einer verständlichen Meldung fehlschlagen lässt.
4. Die Linkprüfung kann von Entwicklern reproduzierbar ausgeführt werden und ist in die für das Repository vorgesehene lokale Qualitätsprüfung integrierbar.
5. Eine detaillierte Schritt-für-Schritt-Installationsanleitung wird bereitgestellt. Sie beschreibt Voraussetzungen, Abruf des Repositorys, Konfiguration, Installation benötigter Abhängigkeiten, Start der Anwendung, Prüfung der erfolgreichen Einrichtung und die Behandlung häufiger Fehler.

## Nichtfunktionale Anforderungen

- Die Lizenztexte müssen eindeutig, widerspruchsfrei und für private sowie kommerzielle Nutzer verständlich sein.
- Der Git-Hook darf bestehende Entwicklungsabläufe nicht unnötig verlangsamen und muss bei fehlenden oder nicht installierten optionalen Werkzeugen eine nachvollziehbare Fehlermeldung ausgeben.
- Die Linkprüfung muss stabile, reproduzierbare Ergebnisse liefern und darf keine externen Netzwerkzugriffe voraussetzen, sofern dies für lokale beziehungsweise repositoryinterne Links nicht erforderlich ist.
- Die Installationsanleitung muss den tatsächlich unterstützten Entwicklungs- und Laufzeitumgebungen entsprechen und für neue Nutzer ohne zusätzliche interne Kenntnisse nachvollziehbar sein.
- Die Veröffentlichung darf keine Zugangsdaten, privaten Konfigurationen oder sonstigen vertraulichen Inhalte in öffentlich erreichbaren Repository-Bestandteilen offenlegen.

## Akzeptanzkriterien

- [ ] Die Lizenz ist auf freie private Nutzung und genehmigungspflichtige kommerzielle Nutzung umgestellt und im Repository eindeutig dokumentiert.
- [ ] Ein Git-Hook prüft Markdown-Dateien auf verwaiste beziehungsweise nicht erreichbare lokale oder repositoryinterne Links und meldet gefundene Fehler mit einem fehlgeschlagenen Prüfergebnis.
- [ ] Die Linkprüfung kann gemäß dokumentiertem Ablauf lokal eingerichtet und reproduzierbar ausgeführt werden.
- [ ] Eine detaillierte Schritt-für-Schritt-Installationsanleitung mit Voraussetzungen, Einrichtung, Konfiguration, Start, Verifikation und Fehlerbehebung ist im Repository vorhanden.
- [ ] Die Installationsanleitung wurde anhand einer sauberen Einrichtung nachvollzogen und enthält keine veralteten oder widersprüchlichen Schritte.
- [ ] Vor der öffentlichen Zugänglichmachung wurden öffentlich zugängliche Inhalte auf versehentlich enthaltene Geheimnisse und private Konfigurationen geprüft.

## Referenzen

- Kundenanforderung: Veröffentlichung, Aufgaben-ID `84f60997-e71b-4cac-8b60-85ea2cc52c5a`

## Abgrenzung

- Die Beantragung oder Erteilung kommerzieller Nutzungsgenehmigungen ist nicht Bestandteil dieser Anforderung.
- Eine vollständige automatisierte Prüfung externer Internetlinks ist nicht Bestandteil dieser Anforderung.
- Die Bereitstellung einer öffentlichen Hosting- oder Repository-Plattform sowie organisatorische Freigabeprozesse sind nicht Bestandteil dieser Anforderung, sofern sie nicht für die technische Umsetzung der genannten Kriterien erforderlich sind.

## Offene Punkte

- Welcher konkrete Lizenzname beziehungsweise Lizenztext soll für die Regelung „frei für private Zwecke, Genehmigungspflicht für kommerzielle Zwecke“ verwendet werden, und wer ist die zuständige Kontaktstelle für kommerzielle Genehmigungen?
- Soll der Git-Hook als versionskontrollierter Client-Hook, über ein bestehendes Hook-Framework oder zusätzlich als CI-Prüfung bereitgestellt werden, und welche Betriebssysteme müssen unterstützt werden?
- Welche Zielplattformen, .NET-/SDK-Versionen, Datenbank- und sonstigen Laufzeitvoraussetzungen gelten verbindlich für die Installationsanleitung?
