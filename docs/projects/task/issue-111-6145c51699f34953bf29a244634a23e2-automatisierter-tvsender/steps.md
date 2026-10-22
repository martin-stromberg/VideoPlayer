# Entwicklungsschritte – Projekt Automatisierter TV-Sender

Branch: `task/issue-111-6145c51699f34953bf29a244634a23e2-automatisierter-tvsender`

| # | Titel | Abhängigkeiten | Branch | Status |
|---|-------|-----------------|--------|--------|
| 1 | Videolaufzeit erfassen | Keine | `task-issue-111-6145c51699f34953bf29a244634a23e2-automatisierter-tvsender-schritt-1-videolaufzeit` | Fertig |
| 2 | Sender und Programmregeln verwalten | Keine | `task-issue-111-6145c51699f34953bf29a244634a23e2-automatisierter-tvsender-schritt-2-sender-programmregeln` | Fertig |
| 3 | Sendeplan aus Regeln erzeugen | 1, 2 | `task-issue-111-6145c51699f34953bf29a244634a23e2-automatisierter-tvsender-schritt-3-sendeplan-erzeugen` | Fertig |
| 4 | Sendeplan bei Änderungen nachführen | 3 | `task-issue-111-6145c51699f34953bf29a244634a23e2-automatisierter-tvsender-schritt-4-sendeplan-nachfuehren` | Fertig |
| 5 | Laufendes Programm zeitbasiert auskunftsfähig machen | 3 | `task-issue-111-6145c51699f34953bf29a244634a23e2-automatisierter-tvsender-schritt-5-programmauskunft` | Fertig |
| 6 | Live-Stream ausliefern | 5 | `task-issue-111-6145c51699f34953bf29a244634a23e2-automatisierter-tvsender-schritt-6-live-stream` | Fertig |
| 7 | Live-TV in der Benutzeroberfläche | 6 | `task-issue-111-6145c51699f34953bf29a244634a23e2-automatisierter-tvsender-schritt-7-live-tv-ui` | Fertig |
| 8 | Laufzeit-Ermittlung robust für Remote-Quellen + Retry (N4) | 1 | `task-issue-111-6145c51699f34953bf29a244634a23e2-automatisierter-tvsender-schritt-8-probe-robust` | Fertig |
| 9 | Genre-Material: Mehrfachauswahl, Quell-Filter, Status/Alternativnamen (N1+N2+N3) | 8 | `task-issue-111-6145c51699f34953bf29a244634a23e2-automatisierter-tvsender-schritt-9-genre-auswahl` | Fertig |
| 10 | Planungsdiagnose + manueller Generierungsanstoß (N5) | 9 | `task-issue-111-6145c51699f34953bf29a244634a23e2-automatisierter-tvsender-schritt-10-diagnose` | Fertig |
| 11 | Fortschrittsanzeige Laufzeitermittlung (N6) | 8 | `task-issue-111-6145c51699f34953bf29a244634a23e2-automatisierter-tvsender-schritt-11-probe-fortschritt` | Fertig |
| 12 | Remote-Probes seekbar via sftp:// statt pipe:0 (N8) | 8 | `task-issue-111-6145c51699f34953bf29a244634a23e2-automatisierter-tvsender-schritt-12-sftp-probe` | Fertig |
| 13 | Neue Genre-Zeilen erben globale Sichtbarkeit/Alternativnamen (N7) | 8 | `task-issue-111-6145c51699f34953bf29a244634a23e2-automatisierter-tvsender-schritt-13-genre-vererbung` | Fertig |
| 14 | Remote-Probes über eigenen Range-Endpunkt (N9) | 12 | `task-issue-111-6145c51699f34953bf29a244634a23e2-automatisierter-tvsender-schritt-14-http-probe` | Fertig |
| 15 | Saisonale Genres in Regel-Auswahl immer wählbar (N10) | 9 | `task-issue-111-6145c51699f34953bf29a244634a23e2-automatisierter-tvsender-schritt-15-saison-genre` | Fertig |
| 16 | Adminseite restrukturieren + Fehlschlag-/Ausstehend-Liste (N13+N14, Variante 2: HTML-Vorschau) | 10+11 | `task-issue-111-6145c51699f34953bf29a244634a23e2-automatisierter-tvsender-schritt-16-admin-ux` | Fertig |
| 17 | Trailer-Verknuepfung + Vorlauf-Fuellmaterial (N11) | 10 | `task-issue-111-6145c51699f34953bf29a244634a23e2-automatisierter-tvsender-schritt-17-trailer` | Fertig |
| 18 | Maskottchen-Interstitials: Drehbuecher + Clip-Produktion + Fuehler-Einbindung (N12) | 10 | `task-issue-111-6145c51699f34953bf29a244634a23e2-automatisierter-tvsender-schritt-18-maskottchen` | Fertig |
| 19 | Segmenter: stderr-Logging + Range-Input statt pipe:0 (N15) | 6 | `task-issue-111-6145c51699f34953bf29a244634a23e2-automatisierter-tvsender-schritt-19-stream-input` | Fertig |
| 20 | Nahtloser Slot-Wechsel: EXT-X-DISCONTINUITY (N17) | 6+7 | `task-issue-111-6145c51699f34953bf29a244634a23e2-automatisierter-tvsender-schritt-20-slot-wechsel` | Fertig |
| 21 | Planer-Eviction: keine massenhafte Verdrängung, stabiler Plan (N16) | 3+4 | `task-issue-111-6145c51699f34953bf29a244634a23e2-automatisierter-tvsender-schritt-21-planer-eviction` | Fertig |
| 22 | Regel 4 erzeugt keine Slots (N16-Rest) + Bereinigen-Button (N18) | 3+4 | `task-issue-111-6145c51699f34953bf29a244634a23e2-automatisierter-tvsender-schritt-22-plan-rest` | Fertig |
| 23 | Serien-Interleaving + Werktag/Wochenende + Regel-Overlay (N19-N21) | 3+4 | `task-issue-111-6145c51699f34953bf29a244634a23e2-automatisierter-tvsender-schritt-23-regel-features` | Fertig |
| 24 | Sendeplan-Panels mit Bild/Titel/Beschreibung (Admin + Live-TV) | 6+7 | `task-issue-111-6145c51699f34953bf29a244634a23e2-automatisierter-tvsender-schritt-24-plan-panels` | Fertig |
