# Abhängigkeiten und Referenzen

## Projektverweise

```text
VideoWebPlayer
`-- VideoWebPlayer.Client

VideoWebPlayer.Maui
`-- VideoWebPlayer.Client

VideoWebPlayer.Tests
`-- VideoWebPlayer
    `-- VideoWebPlayer.Client

VideoWebPlayer.Maui.Tests
`-- VideoWebPlayer
    `-- VideoWebPlayer.Client

WebPlayer
`-- WebPlayer.Client
    `-- WebPlayerApi.Common

WebPlayerApi
`-- WebPlayerApi.Common
```

Die vier alten Projekte (`WebPlayer`, `WebPlayer.Client`, `WebPlayerApi`, `WebPlayerApi.Common`) bilden einen eigenen, von den erhaltenen Projekten getrennten Abhängigkeitsgraphen. `Videos` hat keine Projektverweise.

## Belege

- `VideoWebPlayer/VideoWebPlayer.csproj`: Referenz auf `../VideoWebPlayer.Client/VideoWebPlayer.Client.csproj`
- `VideoWebPlayer.Maui/VideoWebPlayer.Maui.csproj`: Referenz auf `../VideoWebPlayer.Client/VideoWebPlayer.Client.csproj`
- `VideoWebPlayer.Tests/VideoWebPlayer.Tests.csproj`: Referenz auf `../VideoWebPlayer/VideoWebPlayer.csproj`
- `VideoWebPlayer.Maui.Tests/VideoWebPlayer.Maui.Tests.csproj`: Referenz auf `../VideoWebPlayer/VideoWebPlayer.csproj`
- `WebPlayer/WebPlayer.Client/WebPlayer.Client.csproj`: Referenz auf `../../WebPlayerApi.Common/WebPlayerApi.Common.csproj`
- `WebPlayerApi/WebPlayerApi.csproj`: Referenz auf `../WebPlayerApi.Common/WebPlayerApi.Common.csproj`

## Paket- und Dateiabhängigkeiten der Kernprojekte

`VideoWebPlayer` bindet zusätzlich die DLL `lib/msTools.Updater/msTools.Updater.dll` über einen `HintPath` ein und verwendet das Paket `msTools.Backup`. Der Ordner `lib` ist deshalb kein genereller Löschkandidat.

`VideoWebPlayer.Maui` nutzt MAUI, SignalR, SQLite, SkiaSharp und Zeroconf. Diese Abhängigkeiten liegen als Paketverweise und als Dateien innerhalb des Projekts vor.
