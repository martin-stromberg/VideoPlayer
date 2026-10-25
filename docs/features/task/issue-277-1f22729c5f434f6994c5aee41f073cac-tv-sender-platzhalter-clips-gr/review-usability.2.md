# Usability-Review

## Ergebnis

**Status:** Befunde vorhanden

## Befunde

### TVSenderAdminClipGroups.razor (Tab „Clip-Gruppen" — Clip-Zuordnung)

- **Erreichbarkeit** — Clips ohne ermittelte Laufzeit werden in der Mitglieder- und in der Kandidatenliste zwar als „keine Laufzeit" markiert, lassen sich aber per „Hinzufügen" ganz normal einer Gruppe zuordnen — ohne dass irgendwo erklärt wird, dass solche Clips bei der Restzeit-Auffüllung nicht verwendet werden (Anforderung: „nur Clips mit bekannter Abspiellänge werden verwendet — Clips ohne bekannte Länge werden … in der Verwaltung als solche kenntlich gemacht"). Eine Anwenderin ordnet den Clip zu, erwartet ihn im Sendeplan — er erscheint dort nie, und die Oberfläche erklärt den Grund an keiner Stelle.

  Empfehlung: Die Konsequenz sichtbar machen, z. B. Spaltenwert „keine Laufzeit — wird nicht verwendet" oder einen erklärenden Satz über der Kandidaten-/Mitgliederliste („Clips ohne ermittelte Laufzeit werden bei der Auffüllung nicht berücksichtigt — Laufzeitermittlung siehe Tab „Laufzeitermittlung""). Alternativ „Hinzufügen" für solche Kandidaten deaktivieren und den Grund als Hinweistext ausgeben.

- **Erreichbarkeit** — Die Kandidatenliste „Clips hinzufügen" zeigt bei leerem Ergebnis eine leere Tabelle ohne jede Erklärung. Welche Clips überhaupt angeboten werden (nur Videodateien ohne Film-/Serien-Zuordnung, also unklassifizierte Clips), steht nirgends in der Oberfläche: Eine Anwenderin, die einen bestimmten Clip sucht und nicht findet, erfährt nicht, warum er fehlt; und bei einer Suche ohne Treffer ist „keine Treffer" von „Liste lädt noch / es gibt keine Clips" nicht zu unterscheiden. Die anderen Listen der Seite (Gruppen, Regeln, Sendeplan) haben durchweg ausformulierte Leerstände — hier fehlt er.

  Empfehlung: Leerstand-Text ergänzen, der die Auswahlregel erklärt, z. B. „Keine zuordenbaren Clips gefunden — angeboten werden nur Videodateien ohne Film-/Serien-Zuordnung. Suche oder Quellenfilter ggf. anpassen."

## Geprüfte Interaktionen

Liste der aus der Anforderung geprüften Benutzerinteraktionen:
- Clip-Gruppe anlegen („Neue Gruppe"-Dialog mit Klartext-Namensfeld) → unauffällig
- Clip-Gruppe umbenennen (eigener Dialog, verständliche Fehlermeldungen bei leerem/zu langem/doppeltem Namen) → unauffällig
- Clip-Gruppe löschen (Bestätigungsdialog erklärt, dass Regel-Zuordnungen mit entfernt werden) → unauffällig
- Reservierter Name „Trailer" (wird serverseitig abgelehnt, verständliche Fehlermeldung „Der Name „Trailer" ist für die reservierte System-Gruppe vorbehalten." erscheint im Dialog) → unauffällig
- Clips einer Gruppe zuordnen (Kandidatenliste mit Klartext Name/Quelle/Laufzeit statt Kennungen, Suchfeld, Quellenfilter, „Weitere laden"-Blättern à 100) → Befund vorhanden (leere Treffermenge ohne Erklärung der Auswahlregel)
- Clips aus Gruppe entfernen („Entfernen"-Schaltfläche je Zeile) → unauffällig
- Clips ohne bekannte Laufzeit kenntlich machen (Markierung „keine Laufzeit" in beiden Tabellen) → Befund vorhanden (Markierung vorhanden, aber ohne Hinweis auf die Konsequenz „wird nicht verwendet")
- System-Gruppe „Trailer" verstehen (Suffix „(System — automatisch befüllt)", Schaltfläche „Ansehen" statt „Clips verwalten", erklärender Hinweistext ohne Mitgliederliste) → unauffällig
- Gruppen-Mehrfachauswahl an Sendeplatz-Regel (Checkbox-Liste „Clip-Gruppen (Restzeit-Auffüllung)" mit Klartext-Namen inkl. „Trailer (automatisch befüllt)", gleiches Muster wie Genre-/Quellen-Mehrfachauswahl; Leerstand verweist auf den Tab „Clip-Gruppen") → unauffällig
- Karenzzeit einstellen (Zahlenfeld „Karenzzeit (Minuten)" mit Vorauswahl 15, direkt unter der Gruppenauswahl; Hinweis, dass Clip-Gruppen nur bei gesetzter Sendeplatz-Länge wirken) → unauffällig
- Zuordnung in der Regel-Liste wiedererkennen („— Clips: …" in der Material-Spalte) → unauffällig

## Geprüfte Dateien

Liste aller geprüften UI-Dateien:
- `VideoWebPlayer/Components/Pages/Admin/TVSenderAdmin.razor`
- `VideoWebPlayer/Components/Pages/Admin/TVSenderAdminClipGroups.razor`
