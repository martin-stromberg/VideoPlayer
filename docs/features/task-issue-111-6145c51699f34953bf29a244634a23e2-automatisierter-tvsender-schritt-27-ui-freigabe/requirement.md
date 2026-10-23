# Anforderung — Schritt 27 (N26–N28)

Quelle: `docs/projects/task/issue-111-6145c51699f34953bf29a244634a23e2-automatisierter-tvsender/nachbesserungen.md` §N26–§N28.
Basisbranch: `task/issue-111-6145c51699f34953bf29a244634a23e2-automatisierter-tvsender`.

## N26 — Fortschrittsbalken immer voll (Kultur-Bug Komma statt Punkt) (hoch, BUG)

Der Fortschrittsbalken der Live-TV-Seite rendert `style="width:59,381461147936385%"` —
der Prozentwert wird mit der aktuellen (deutschen) Kultur serialisiert (Komma statt
Punkt), CSS verwirft die Angabe und der Balken wirkt immer voll.

- Fix: Prozentwerte in `style="width:…%"`-Attributen werden mit
  `CultureInfo.InvariantCulture` formatiert.
- Betroffene Stellen: `LiveTv.razor` (`progress-fill`), `VideoPlayer.razor`
  (`player-progress-fill`, gleicher latent vorhandene Bug).
- Test: Format-Assertion — unter deutscher Kultur darf kein Komma im style-Attribut
  erscheinen.

## N27 — Sendeplan nach Tagen gliedern + Trennlinie bei Regelwechsel (mittel, UI)

Der Sendeplan-Tab der TV-Sender-Adminseite soll die Programm-Karten nach Kalendertag
gliedern (Tages-Header) und innerhalb eines Tages eine Trennlinie/Markierung zeigen,
sobald die zugehörige `ProgramRuleId` wechselt (Block-Abgrenzung je Programmregel).

- Umsetzung in `TVSenderAdmin.razor`: Gruppierung nach `slot.Start` (lokales Datum),
  Section-Header je Tag, Divider beim Regelwechsel.
- Test: bUnit-Markup-Prüfung (Tages-Header, Divider am Regelwechsel, kein Divider am
  Tagesanfang).

## N28 — TV-Sender pro Benutzer freigeben (hoch)

Erweiterung von N25 (`Setup.TvSenderEnabled`): Der Sender muss zusätzlich pro Benutzer
freigegeben werden können.

- Benutzer-Allowlist als neue Tabelle `TvSenderUserAccess` (sauberer als ein
  serialisiertes `Setup`-Feld: FK-Integrität, Muster `MediaSourceUser`).
- Admin wählt die freigegebenen Benutzer in der Admin-UI aus (neben dem globalen
  Schalter in `Einrichtung → Allgemein`).
- Serverseitige Prüfung: `NavMenu` (Eintrag nur bei Flag + Freigabe), `LiveTv.razor`
  (Hinweis statt Inhalt), `ProgramGuideController`/`LiveStreamController`
  (403 für nicht freigegebene Benutzer, weiterhin 404 bei gesetztem Globaflag-Aus).
- Administratoren haben implizit immer Zugriff.
- Flag UND Benutzer-Freigabe müssen gemeinsam erfüllt sein.
- EF-Migration; neue Tabelle in `OptionalRestoreTables` (Alt-Backups ohne sie
  wiederherstellbar); Löschen eines Benutzers räumt die Freigabezeilen mit ab.
- Tests: Benutzer ohne Freigabe → versteckt/verweigert (403/Hinweis); Benutzer mit
  Freigabe → sichtbar/erlaubt; Admin ohne Zeile → erlaubt.
