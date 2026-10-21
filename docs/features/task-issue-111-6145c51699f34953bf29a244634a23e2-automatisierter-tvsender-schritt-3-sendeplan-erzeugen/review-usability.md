# Usability-Review (Schritt 8)

## Geprüfter Nutzerfluss

Einzige neue UI-Interaktion: zwei Zahlenfelder „Planungshorizont (Stunden)" und „Generierungsintervall (Minuten)" in der bestehenden Sender-Karte auf `/admin/tv-sender` — sie werden mit den übrigen Sender-Einstellungen (Name, Bundesland) über einen gemeinsamen „Speichern"-Button persistiert.

## Befunde

| Prüfpunkt | Ergebnis |
|-----------|----------|
| Beschriftung verständlich, Einheiten im Label | Ja („(Stunden)"/„(Minuten)") |
| Sinnvolle Defaults beim Öffnen | Ja — werden aus `ProgramSettingsService` geladen (48 h / 15 min) |
| Fehlerfall sichtbar | Ja — `ArgumentException` aus `UpdateProgramScheduleSettingsAsync` landet wie die übrigen Fehler in `channelStatus` |
| Validierung | `min="1"` im Input + serverseitige Min-Prüfung (≥1) — zweistufig |
| Berechtigung | Seite ist Admin-only; `SaveChannelSettingsAsync` läuft serverseitig über `ProgramRuleService.RenameChannelAsync(userId, …)` mit Admin-Prüfung — unverändert erzwungen (`NonAdmin_SeesNotAuthorized` bleibt) |
| Konsistenz zur bestehenden Seite | Gleiche Karte, gleiche `form-control`-Optik, gemeinsamer Speichern-Button — keine neue Bedienlogik |

## Ergebnis

**Status:** Erfüllt — kein zusätzlicher Usability-Bedarf. Der eigentliche Sendeplan hat bewusst keine eigene Oberfläche (Programmauskunft/Live-View folgen in Schritt 5/6); dass nur Admins die Planungsparameter ändern können, ist über die bestehende Seiten-Autorisierung serverseitig gesichert.
