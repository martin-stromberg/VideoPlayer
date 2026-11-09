# Übersetzte Anforderung: Programmauskunft (zeitbasierte Auskunft über das laufende Programm)

Quelle: `docs/projects/task/issue-111-6145c51699f34953bf29a244634a23e2-automatisierter-tvsender/project-plan.md`, Schritt 5 + Vorgehensentscheidungen.

## Fachliche Zusammenfassung

Das System bekommt eine neue, eigenständige fachliche Komponente „Programmauskunft": Sie beantwortet
zu einem beliebigen Zeitpunkt rein aus Uhrzeit und Sendeplan (`ProgramSlot`), welche Sendung gerade
läuft, wie weit diese fortgeschritten ist (Position im Video), wie lange sie noch läuft und welche
Sendung als Nächstes folgt. Die Auskunft ist zustandslos — sie hängt weder von Zuschauern noch von
Prozesszustand ab und ist nach einem Server-Neustart sofort korrekt. Angemeldete Benutzer erhalten
über eine abgesicherte HTTP-Schnittstelle den aktuellen Stand („Jetzt/Gleich") sowie eine begrenzte
Programmübersicht der kommenden Stunden (Vorschau). Der vollständige Sendeplan bleibt administrativ
(bestehende `TVSenderAdmin`-Ansicht über `IProgramScheduleService.GetScheduleAsync`). Der Dienst ist
die einheitliche fachliche Grundlage für Anzeige (Schritt 7) und Stream-Auslieferung (Schritt 6) und
trägt einen eigenen Namen — begrifflich klar von den Benutzer-Playlists abgegrenzt, kein
„Playlist"-Naming.

## Akzeptanzkriterien

1. **Zeitbasiert, zustandslos:** Die Auskunft ist eine reine Funktion von (`utcNow`, Sendeplan).
   Kein Prozesszustand, kein Zuschauer-Bezug; nach Server-Neustart ohne Wiederanlauf korrekt.
2. **Laufende Sendung:** Für `utcNow` wird der `ProgramSlot` mit `Start <= utcNow < End` und
   `Status == ProgramSlotStatus.Scheduled` als laufend ausgegeben — inkl. Position im Video
   (`utcNow - Start`), Restdauer (`End - utcNow`) und Fortschritt.
3. **Nächste Sendung:** Der früheste geplante Slot mit `Start >= End der laufenden Sendung`
   (bzw. `Start > utcNow` bei Sendelücke) wird als „als Nächstes" ausgegeben.
4. **Verdrängte Slots:** `ProgramSlotStatus.Displaced`-Slots gelten fachlich nicht als laufend/geplant
   und tauchen in der Auskunft nicht auf — weder als aktuell noch als Vorschau.
5. **Sendelücke:** Deckt kein `Scheduled`-Slot `utcNow` ab, meldet die Auskunft „keine laufende
   Sendung" (kein Fehler, kein erfundener Inhalt); die nächste Sendung kann dennoch benannt werden.
6. **Abgesicherte Schnittstelle:** HTTP-Endpunkte nur für angemeldete Benutzer (Bearer-Token über
   `Authorization`-Header oder `access_token`-Abfrageparameter, `BearerTokenCheckAttribute` +
   `CheckLoggedIn`-Muster). Anonyme Aufrufe → 401.
7. **Begrenzte Vorschau:** Die Programmübersicht liefert die laufende Sendung plus die kommenden
   Sendungen eines serverseitig begrenzten Zeitraums (Stunden-Parameter mit Maximum) — nicht den
   vollständigen Sendeplan. Der volle Plan bleibt der Administration vorbehalten.
8. **Titelauflösung:** Slots werden auf ihren Inhalt aufgelöst — Film (`Movie.Name`) bzw. Episode
   (`TVShow.Name` — Staffel, Folge: `TVShowEpisode.Name`), analog der Admin-Sendeplan-Ansicht.
   Aufgelöste Inhalte bleiben erkennbar, auch wenn die weiche Referenz (`MediaItemId`/`MovieId`/
   `TVShowEpisodeId`, keine DB-FKs) ins Leere läuft.
9. **Kein Schema-Zwang:** Reiner Service-/API-Schritt — keine neuen Tabellen/Spalten erforderlich,
   folglich keine Migration und keine Backup-Änderung.
10. **Namensabgrenzung:** Öffentliche API, DTOs und Doku verwenden „Programmauskunft"/`ProgramGuide`
    o. ä. — niemals „Playlist".

## Betroffene Klassen und Komponenten

- **Neu:** `IProgramGuideService` + `ProgramGuideService` (Services) — zeitbasierte Auskunftslogik.
- **Neu:** Ergebnis-/DTO-Typen, z. B. `ProgramGuideEntry` (Service-Seite) sowie
  `DtoProgramGuideEntry`, `DtoProgramGuideNow` (`VideoWebPlayer.Client/Models`) — Analogie zu
  `DtoPlaylist*`.
- **Neu:** `ProgramGuideController` (`api/program-guide`) — `ApiBaseController` + `BearerTokenCheck`.
- **Bestehend (gelesen):** `ProgramSlot`, `ProgramSlotStatus`, `TVChannel`, `Movie`,
  `TVShowEpisode`/`TVShowSeason`/`TVShow`, `IProgramRuleService` (`GetOrCreateChannelAsync`).
- **DI:** Registrierung in `ServiceCollectionExtensions` (scoped, wie `IProgramScheduleService`).
- **Tests:** neue Service-Tests (SQLite-InMemory über `ProgramScheduleTestDb`-Muster),
  Controller-Tests (FakeAuthService-Muster), HTTP-E2E-Test (`DeviceClientTestBase`/`PairingWebApplicationFactory`-Muster)
  für 401-anonym / 200-angemeldet / `access_token`-Query.

## Implementierungsansatz

- Neuer Service analog `IProgramScheduleService`: Methoden mit explizitem `utcNow`-Parameter
  (Zustandslosigkeit + Testbarkeit). Der Controller übergibt `DateTimeOffset.UtcNow`.
- Queries auf `ProgramSlots` mit `Status == Scheduled`; `AsNoTracking`. Titelauflösung per
  Batch-Lookup auf `Movies`/`TVShowEpisodes` (Muster aus `TVSenderAdmin.razor` übernehmen und in
  den Service verlagern).
- Controller folgt dem `PlaylistsController`-Muster (`ExecuteAsync`, `CheckLogedIn`, 401/400/500-Mapping).
- E2E-HTTP-Tests über `WebApplicationFactory` (paired-device Token bzw. `access_token`-Query).

## Konfiguration

- Maximale Vorschau-Stunden: serverseitige Obergrenze für den `hours`-Parameter (Konstante im
  Service/Controller, z. B. 12 h) — „begrenzte Übersicht" ist eine harte fachliche Grenze, kein
  Setup-Feld (keine Konfiguration verlangt).

## Offene Fragen

Keine — Vorgehensentscheidungen sind im Projektplan getroffen (eigener Begriff, zeitbasiert,
begrenzte Vorschau, voller Plan administrativ).
