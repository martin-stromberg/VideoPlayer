# Strukturierte Anforderung

## Ziel

Vor dem Umschalten des Web-Repositorys auf `public` sollen die verbleibenden Release-Gates explizit abgearbeitet, nachgewiesen und fuer nicht lokal ausfuehrbare Punkte als konkrete Handlungsanleitung dokumentiert werden.

## Muss-Kriterien

- Die Punkte aus `docs/PUBLICATION_CHECKLIST.md` werden lokal abgearbeitet, soweit sie in dieser Arbeitsumgebung pruefbar sind.
- Web-Build, Web-Tests, Markdown-Linkcheck, Hook-Pruefung und Vulnerability-Scan werden unmittelbar vor der Freigabe ausgefuehrt und dokumentiert.
- Secret-Scan fuer Arbeitsbaum und vereinbarten Historienumfang wird fuer das Web-Repository ausgefuehrt.
- Nicht lokal pruefbare Gates, insbesondere MAUI-Remote, Linux-Frischclone, produktive Secret-Rotation und GitHub-Repository-Einstellungen, werden als konkrete Anleitung und Blocker fuer die finale manuelle Freigabe festgehalten.
- Oeffentlich sichtbare Dokumente werden auf den tatsaechlichen Veroeffentlichungsstand korrigiert.

## Nicht-Ziele

- Repository-Sichtbarkeit nicht automatisiert auf `public` setzen.
- Produktive Secrets oder API-Tokens nicht innerhalb dieser Arbeitskopie erzeugen.
- Keine Historienbereinigung ohne explizite Freigabe.

## Abnahmekriterien

- Es gibt ein nachvollziehbares Audit-Dokument mit Datum, Commit-Stand, ausgefuehrten Kommandos, Ergebnissen und offenen manuellen Freigaben.
- Die Veroeffentlichungscheckliste unterscheidet erledigte, blockierte und manuell ausstehende Punkte eindeutig.
- Keine lokalen Befunde bleiben unerklaert.
