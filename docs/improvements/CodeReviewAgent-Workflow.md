# Workflow: Automatisiertes Codereview & Architekturvorschläge

**Agent:** CodeReviewAgent
**Gültig ab:** 14.03.2026

---

## Ziel

Automatisiere Codereviews und die Erstellung von Architekturvorschlägen während der Weiterentwicklung, um kontinuierlich Codequalität und Wartbarkeit sicherzustellen.

---

## Workflow-Beschreibung

1. **Trigger**
   - Bei jedem Commit, Pull Request oder Merge in den Hauptentwicklungszweig.
   - Optional: Bei jeder Änderung an Dateien in den Verzeichnissen `Services/`, `ViewModels/`, `Controllers/` oder `Models/`.

2. **Analyse-Schritte**
   1. **Codeänderungen erkennen**
      - Identifiziere alle geänderten Dateien und Codeabschnitte.
   2. **Automatisiertes Codereview**
      - Führe für jede geänderte Datei ein Review durch:
        - Prüfe auf Lesbarkeit, Wartbarkeit, Fehlerquellen, Einhaltung von Best Practices.
        - Erstelle konkrete Verbesserungsvorschläge und bewerte sie.
        - Wende sinnvolle Korrekturen direkt an (sofern keine Rückfrage nötig).
   3. **Architektur-Check**
      - Erkenne grundlegende Architekturprobleme (z.B. zu große Klassen, fehlende Trennung von Verantwortlichkeiten).
      - Falls erkannt: Erstelle/aktualisiere eine Markdown-Vorschlagsdatei im Verzeichnis `docs/improvements/`.
   4. **Dokumentation**
      - Füge Review-Kommentare und Architekturvorschläge als Teil des Commit- oder PR-Kommentars hinzu.
      - Verweise auf relevante Verbesserungsdateien.

3. **Feedback & Iteration**
   - Entwickler erhalten automatisiertes Feedback und können Korrekturen übernehmen oder diskutieren.
   - Bei Annahme von Architekturvorschlägen: Schrittweise Umsetzung nach Migrationsvorgehen.

---

## Erweiterungsmöglichkeiten
- Integration in CI/CD-Pipeline (z.B. GitHub Actions, Azure DevOps)
- Automatisches Erstellen von Issues für größere Refactorings
- Optional: Schwellenwerte für Review-Intensität (z.B. nur bei >50 Zeilen Änderung)

---

## Beispiel-Trigger
- "git commit" oder "git push"
- "Pull Request opened/updated"
- "Datei in Services/ geändert"

---

## Verantwortlich
- CodeReviewAgent (automatisiert)
- Entwicklerteam (Review, Umsetzung, Diskussion)

---

**Hinweis:** Dieser Workflow kann als Grundlage für eine automatisierte Review- und Verbesserungs-Pipeline dienen und sollte regelmäßig evaluiert und angepasst werden.
