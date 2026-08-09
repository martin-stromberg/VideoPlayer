# Offene Aufgaben

Erstellt am: 2026-08-09
Abbruchgrund: Maximale Iterationsanzahl erreicht

Die folgenden Aufgaben konnten im automatisierten Zyklus nicht abgeschlossen werden
und muessen manuell oder in einem erneuten Lauf bearbeitet werden.

## Offene Planelemente

Keine.

## Code-Review-Befunde

- [ ] Manuelle Installation laeuft nach abgebrochenem/uebersprungenem Download weiter: `UpdateAdminService.InstallAsync` soll nach `DownloadAsync` nur bei `AutoUpdateOutcome.Success` mit `InstallAsync(true, ...)` fortfahren; bei `Skipped` oder `Canceled` soll das Download-Ergebnis zurueckgegeben werden. Tests fuer beide Faelle ergaenzen.
- [ ] Update-Backup-Pfad kann Installation unnoetig blockieren, obwohl der reale `msTools.Backup`-Adapter ihn nicht nutzt: Zielverzeichnis-Erstellung aus dem Coordinator entfernen oder provider-spezifisch machen, damit der Standardadapter ueber die bestehende Backup-Konfiguration nicht durch einen ungenutzten `UpdateBackupPath` blockiert wird. Regressionstest ergaenzen.

## Fehlgeschlagene Tests

Keine.
