# Plan-Check – Schritt 22

**Status: Plan vollständig**

Geprüft gegen `requirement.md` und `inventory.md`:

- Die Ursache ist anhand der Live-Datenbank verifiziert (nicht nur vermutet):
  Überstand der Vorgänger-Airings + atomarer Drop → Regeln 4/6/7 komplett leer.
- Der Fix adressiert beide nötigen Richtungen: (a) neue Airings invadieren nicht
  mehr (softEnd-Trim + Oversized-Head-Konsum), (b) vorhandene invadierende
  Bestandsslots werden suffix-genau entfernt (Invasor-Trim), damit Regel 4 sofort
  wieder materialisiert statt erst nach Auslaufen der überstehenden Slots.
- `AiringReproducible` wird mitgeführt — ohne den Grenz-Trim im Validierer würden
  gekappte Airings beim nächsten Lauf verdrängt (neue Oszillation).
- Repro-Test „heute 16:00, jetzt 16:30" ist als Pflicht-Test enthalten.
- N18: Service-Methode serverseitig abgesichert, Dialogmuster vorhanden, Tests
  inkl. Berechtigungsprüfung geplant. „Plan zurücksetzen" wird als separat
  markierte Aktion mit eigenem Dialog umgesetzt.
- Testbedarf abgedeckt: echtes SQLite via `ProgramScheduleTestDb`, Idempotenz,
  Cursor-Verhalten, Berechtigung. Kein E2E nötig (kein neuer Benutzerfluss jenseits
  Admin-Buttons; Komponententests vorhanden → `TVSenderAdminTests` ergänzen, falls
  dort Razor-Rendering getestet wird).

Risiken/Entscheidungen:

- Grenzmodell ist prioritaetsblind: Fenstergrenzen gelten unabhängig von Priorität
  (Kachel-Tagesplan-Semantik); Priorität löst weiterhin gleichzeitige
  Occurrence-Konflikte.
- Verdrängen eines laufenden (überstehenden) Slots ist beabsichtigt — er hätte
  an der Grenze enden müssen.
