# Usability-Review

## Ergebnis

**Status:** Keine Befunde

## Befunde

Nur ausfüllen, wenn Status „Befunde vorhanden":

## Geprüfte Interaktionen

- Administrator öffnet über Einrichtung → Kachel „Allgemein“ die Seite `/admin/program-settings` und pflegt das Feld „Öffentliche Basis-URL“ (Karte „Öffentliche Basis-URL“) → unauffällig
- Administrator trägt eine vollständige absolute URL mit Schema/Host/Port/Pfad ein und speichert → unauffällig
- Administrator leert das Feld, damit die Adresse automatisch abgeleitet wird → unauffällig
- Administrator gibt eine ungültige Adresse ein und erhält eine verständliche Validierungsmeldung (clientseitig „Der Wert muss eine absolute http- oder https-URL sein (z. B. …)“, serverseitig „Die öffentliche Basis-URL muss …“) → unauffällig

## Geprüfte Dateien

- `VideoWebPlayer/Components/Pages/Admin/ProgramSettings.razor`
- `VideoWebPlayer/Components/Shared/AbsoluteHttpUrlAttribute.cs`
- `VideoWebPlayer/Configuration/DiscoveryUrlRules.cs` (Wortlaut der Validierungsmeldung)
- `VideoWebPlayer/Services/ProgramSettingsService.cs` (serverseitige Fehlermeldung)
- `docs/help/einrichtung.md` (Abschnitt „Öffentliche Basis-URL“)
