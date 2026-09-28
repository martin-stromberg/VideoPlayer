# Vorlage: handover.md

Kopiere diese Datei nach `docs/projects/{projekt}/handover.md`, sobald ein zweiter Agent beteiligt ist
oder ein Schritt über eine mögliche Unterbrechung hinaus läuft. Regeln: siehe `AGENTS.md` im Repository-Root.
Die Datei ist bewusst kurz. Sie hält nur fest, was sich nicht aus Code und `git log` ablesen lässt.

## Bearbeiter

Ein Schritt hat zu jedem Zeitpunkt genau einen Bearbeiter. Status: `aktiv`, `pausiert`, `fertig`.
Diese Tabelle steht bewusst NICHT in `steps.md` (das Statusskript liest dessen erste Tabelle).

| Schritt | Bearbeiter | Branch | Status | Seit |
|---------|------------|--------|--------|------|
| 1 | Claude | `{projekt-branch}-schritt-1-{slug}` | fertig | 2026-01-01 |
| 2 | Devin | `{projekt-branch}-schritt-2-{slug}` | aktiv | 2026-01-02 |

## Übergabestand je Schritt

Pro Schritt, der nicht „fertig“ ist, ein Abschnitt in dieser Form. Vor jeder Pause aktualisieren, mit einem
Commit des Arbeitsstands (Präfix `step:`) abschließen.

### Schritt {n}: {Titel}

- **Stand vom:** {Datum, Uhrzeit}, letzter Commit `{hash}`
- **Bearbeiter / Übernehmer:** {Agent} / {Agent oder „offen“}

**Erledigt**
- {was im Code tatsächlich vorhanden ist, mit Dateien}

**Offen**
- {was noch fehlt, in sinnvoller Reihenfolge}

**Entscheidungen**
- {Auslegungsfragen, die schon entschieden wurden, jeweils mit Begründung}

**Außerhalb des Auftrags**
- {Commits/Änderungen, die nicht zum Schritt gehören, z. B. unterwegs gefundener Fehler}

**Tests**
- Letzter Lauf: {Anzahl grün/gesamt}, {Debug/Release}
- Bekannt flackernd: {Test, Beobachtung}
- Besonderheiten beim Ausführen: {z. B. Playwright-Browser, Dauer}

**Achtung**
- {Fallen, in die der Nächste tappen könnte}
