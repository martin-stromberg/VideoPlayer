# Plan

## Vorgehen

1. Lokalen Git- und Dokumentationsstand erfassen.
2. `core.hooksPath` setzen und Hook-Metadaten pruefen.
3. Build, Tests, API-Vertragstest, Markdown-Linkcheck und Vulnerability-Scan ausfuehren.
4. Secret-Scan fuer Arbeitsbaum und Historie ausfuehren und Treffer klassifizieren.
5. Falls ein lokales MAUI-Repository vorhanden ist, dessen Status, Remote und vorbereitende Commits pruefen; andernfalls konkrete manuelle Schritte dokumentieren.
6. `docs/PUBLICATION_CHECKLIST.md` in eine nachweisfaehige Checkliste mit Status, Ergebnis und Blockern ueberfuehren.
7. Ein Audit-Dokument mit Kommandos, Ergebnissen, offenen Freigaben und finaler Handlungsanleitung erstellen.
8. README, API-Dokumentation, Installationsdokumentation, Secrets-Dokumentation und Release Notes auf widerspruchsfreie Freigabehinweise pruefen und bei Bedarf korrigieren.
9. Review- und Testergebnisse dokumentieren.

## Offene Punkte

Keine fuer die lokale Umsetzung. Die manuell zu erledigenden Release-Gates werden als Blocker im Audit-Dokument dokumentiert.
