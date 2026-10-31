# Plan-Prüfung: Schritt 26 — N23–N25

**Status:** Plan vollständig

## Geprüfte Punkte

- **N23:** Fix greift an der richtigen Stelle an (Strang-Reihenfolge, nicht Item-Reihenfolge —
  Episodenreihenfolge und Cursor bleiben intakt). Hash ist prozess-stabil (FNV-1a, nicht
  `string.GetHashCode`) und regelabhängig → „stabile, nicht alphabetische" Mischung. Tagesrotation
  (`wanted < m`) und stabile Positionen (`wanted >= m`) bleiben N19-konform.
  Testbedarf: Positionskonsistenz + Nicht-Alphabetik über Regeln — abgedeckt.
- **N24:** Beide Diagnose-Punkte der Anforderung adressiert (Poll-Start nur interaktiv;
  `upcoming` wird auch bei `now`-Fehler/Slot-Wechsel neu befüllt). bUnit-Test via
  `TimeProvider`-Injection machbar (`TimeProvider.System` bereits registriert).
- **N25:** Persistenz über `Setup`+`ProgramSettingsService` (Projektmuster), Admin-Toggle in
  „Allgemein", NavMenu-Kondition, serverseitige 404 an beiden Controllern, Blazor-Seiten
  gesperrt, alle drei Worker definiert (idle/stop). Backup-Spaltenregistrierung + Migration
  gemäß AGENTS.md. Testabdeckung: Endpoint-Schutz, Toggle-Sichtbarkeit, Worker-Idle — vorhanden.
- **Risiken benannt:** `GeneralSettingsUpdate`-Record-Änderung (alle Aufrufer), Alttest-Fixtures
  (Flag-Seed), E2E-Seeds — im Plan enthalten.

## Hinweise

- Kein E2E-Pflichtszenario neu: der Toggle ist ein Admin-Formular (bUnit ausreichend);
  vorhandene E2E-Suiten werden nur mit Flag-Seed weiterlaufen gelassen.
