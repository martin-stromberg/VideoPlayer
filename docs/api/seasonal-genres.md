# Saisonale Startseitenlisten

`GET /api/home/seasonal-genres` benötigt einen gültigen Benutzer-Bearer-Token.
Web und mobile App verwenden denselben Vertrag; nicht angemeldete Aufrufe erhalten 401.

Die Antwort ist ein Array geordneter Sektionen:

```json
[
  {
    "genreId": 42,
    "name": "Halloween",
    "expiresAt": "2026-11-01T00:00:00Z",
    "entries": [
      { "$type": "movie", "id": 123, "name": "Beispielfilm", "mediaSourceId": 1, "posterPictureId": 456 }
    ]
  }
]
```

`entries` verwendet `DtoMediaEntry` einschließlich der vorhandenen Diskriminatoren
`movie`, `movieCollection` und `show`. Weitere Felder entsprechen den vorhandenen
Medien-DTOs, insbesondere Bildkennungen und benutzerspezifisches `watchedAt`.
Nur Medien aus Quellen, die dem Benutzer freigegeben sind, werden geliefert.
Sammlungen werden dedupliziert, eine Sammlung mit einem Film erscheint als Film.
Die Sortierung bleibt `EndedAt`, `ReleaseDate`, `PremieredAt`, `CreatedAt` absteigend,
danach Name aufsteigend. Die Sektionen folgen der Genre-ID-Reihenfolge.

Der Server berechnet aktive, jährlich wiederkehrende Saisonfenster anhand von
UTC-Tagen mit inklusivem Start und Ende. Fehlende oder ungültige Grenzen sowie
Genres ohne Medien liefern keine Sektion. `expiresAt` ist die **exklusive** Grenze
00:00 UTC des Folgetags des letzten Saisontags. Clients berechnen keine Saison,
sondern zeigen eine Sektion ausschließlich vor diesem gelieferten Zeitpunkt.
Eine erfolgreiche leere Antwort ist `200 []`; HTTP-/Transportfehler dürfen nicht
als leere Antwort behandelt werden. Ein Offline-Cache darf Sichtbarkeit nicht
über `expiresAt` hinaus verlängern.

Die Webkomponente `SeasonalGenreList` verwendet den authentifizierten internen
HTTP-Client und entfernt abgelaufene Sektionen per Timer, ohne Seitenwechsel.
Die Filmkartenroute `/movie/{id}` nutzt die vorhandene Filmdetailansicht; sie
unterstützt auch Filme ohne Sammlung.

## Nachweis

`SeasonalHomeServiceTests` prüft UTC-Grenzen, Wiederholung, Jahreswechsel,
SQLite-Quellenfilterung, Gruppierung, Sortierung, Polymorphie und Gesehen-Status.
`SeasonalGenresE2ETests` startet Kestrel und SQLite mit Testbenutzer und prüft mit
Chromium die Anmeldung, Inhalte, HTTP-Aufrufe, Mediennavigation und Ablauf.

```powershell
dotnet test VideoWebPlayer.Tests/VideoWebPlayer.Tests.csproj --filter "FullyQualifiedName~SeasonalHomeServiceTests|FullyQualifiedName~SeasonalGenresE2ETests"
```

Playwright-Browser müssen vorher mit dem generierten `playwright.ps1 install chromium`
installiert werden; ein fehlender Browser lässt die neuen Tests fehlschlagen.

## Bereitstellung

Den Server mit diesem Endpunkt zuerst bereitstellen, bevor eine mobile App
ausgerollt wird, die `GET /api/home/seasonal-genres` abruft. Die Webansicht
verwendet ebenfalls denselben authentifizierten Endpunkt.
