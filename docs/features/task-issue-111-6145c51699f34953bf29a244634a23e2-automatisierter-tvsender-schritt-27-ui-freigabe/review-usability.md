# Usability-Review — Schritt 27 (N26–N28)

**Status: Keine Befunde**

- N26: Fortschrittsbalken zeigt jetzt die tatsächliche Position — der Komma-Bug ließ ihn immer
  voll aussehen.
- N27: Tages-Header („dddd, dd.MM.yyyy") und gestrichelte Trennlinie mit Regelname gliedern den
  Sendeplan lesbar; bestehende Karten, Badges und Buttons unverändert.
- N28: Die Benutzer-Auswahl sitzt direkt unter dem globalen Schalter auf der Einstellungsseite;
  Administratoren sind gesperrt markiert mit Hinweis „immer Zugriff". Benutzer ohne Freigabe
  sehen auf `/live-tv` einen verständlichen Hinweis statt einer Fehlerseite, die API liefert
  403 — im UI mit „Sie haben keinen Zugriff auf den TV-Sender." übersetzt.
- Hinweis (bewusst, kein Befund): Der Eintrag „Live-TV" verschwindet ohne Erklärung aus der
  Navigation, wenn die Freigabe entzogen wird — konsistent mit dem N25-Verhalten des Flags.
