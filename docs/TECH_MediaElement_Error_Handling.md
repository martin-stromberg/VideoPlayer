# MediaElement Error Handling - Technical Documentation

> **Dokumenttyp**: Technische Dokumentation  
> **Zielgruppe**: Entwickler  
> **Version**: 1.0  
> **Letzte Aktualisierung**: 2024

## Problem

Beim Abspielen von Videos über das `CommunityToolkit.Maui.MediaElement` kann es zu verschiedenen Fehlern kommen:
- Format nicht unterstützt
- Netzwerkfehler beim Streaming
- Codec-Probleme
- Unerwartete Dekodierungsfehler

Ohne Error-Handling würde die App in einem inkonsistenten Zustand bleiben (Video-Player sichtbar aber nicht abspielbar, Play-Button unsichtbar).

## Lösung

Vollständiges Error-Handling wurde implementiert in:
- `TVShowDetailsPage.xaml` / `.xaml.cs`
- `MovieCollectionDetailsPage.xaml` / `.xaml.cs`

## Implementierte Features

### 1. OnMediaFailed Event-Handler

```csharp
private void OnMediaFailed(object? sender, MediaFailedEventArgs e)
{
    MainThread.BeginInvokeOnMainThread(() =>
    {
        // Verstecke Video Player
        VideoPlayer.IsVisible = false;
        VideoPlayer.Source = null;
        
        // Zeige Play Button und Banner
        PlayButton.IsVisible = true;
        BannerImage.IsVisible = true;
        
        // Zeige Fehlermeldung
        if (ErrorLabel != null)
        {
            ErrorLabel.Text = $"⚠ Wiedergabe fehlgeschlagen: {e.ErrorMessage}";
            ErrorLabel.IsVisible = true;
        }
        
        _positionUpdateTimer?.Stop();
        
        Debug.WriteLine($"[MediaElement] Playback failed: {e.ErrorMessage}");
    });
}
```

**Funktionen:**
- ✅ Video Player wird ausgeblendet
- ✅ Source wird auf null gesetzt (stoppt Hintergrund-Prozesse)
- ✅ Play-Button wird wieder angezeigt
- ✅ Banner wird wieder angezeigt
- ✅ Fehlermeldung wird im Error-Label angezeigt
- ✅ Position-Update-Timer wird gestoppt

### 2. Fehlermeldungs-Banner

```xaml
<Label x:Name="ErrorLabel"
       IsVisible="False"
       BackgroundColor="#D32F2F"
       TextColor="White"
       Padding="10"
       FontSize="12"
       LineBreakMode="WordWrap"
       VerticalOptions="End"
       HorizontalOptions="Fill" />
```

**Design:**
- Rotes Banner am unteren Rand des Banner-Bereichs
- Weiße Schrift für gute Lesbarkeit
- WordWrap für lange Fehlermeldungen
- Initial unsichtbar

### 3. State-Reset bei erfolgreicher Wiedergabe

```csharp
private void OnMediaOpened(object? sender, EventArgs e)
{
    MainThread.BeginInvokeOnMainThread(() =>
    {
        // Verstecke Play Button und Banner
        PlayButton.IsVisible = false;
        BannerImage.IsVisible = false;
        
        // Verstecke Fehlermeldung
        if (ErrorLabel != null)
        {
            ErrorLabel.IsVisible = false;
        }
        
        // Zeige Video Player
        VideoPlayer.IsVisible = true;
        
        // Starte Position-Update-Timer
        StartPositionUpdateTimer();
    });
}
```

## Error-Flow

### Fehlerfall:

```
User klickt Play
        ↓
Video-Quelle wird geladen (Stream-URL oder lokale Datei)
        ↓
MediaElement versucht zu laden
        ↓
⚠ Fehler tritt auf (z.B. Format nicht unterstützt)
        ↓
OnMediaFailed Event wird ausgelöst
        ↓
VideoPlayer wird ausgeblendet
Source wird auf null gesetzt
        ↓
Play-Button + Banner werden wieder angezeigt
        ↓
ErrorLabel zeigt Fehlermeldung
        ↓
User kann erneut Play klicken oder zurücknavigieren
```

### Erfolgsfall:

```
User klickt Play
        ↓
Video-Quelle wird geladen
        ↓
MediaElement lädt erfolgreich
        ↓
OnMediaOpened Event wird ausgelöst
        ↓
Play-Button + Banner werden ausgeblendet
ErrorLabel wird ausgeblendet
        ↓
VideoPlayer wird angezeigt
        ↓
Timer startet für Position-Updates
        ↓
Video spielt ab
```

## Testing

### Manuelles Testing:

1. **Format-Error simulieren:**
   - Ungültige Video-URL verwenden
   - Erwartung: Error-Banner erscheint

2. **Netzwerk-Error simulieren:**
   - Internet während Streaming trennen
   - Erwartung: Error-Banner nach Timeout

3. **Erfolgreicher Retry:**
   - Nach Error erneut Play klicken
   - Erwartung: Error-Banner verschwindet bei erfolgreicher Wiedergabe

### Automatisierte Tests:

```csharp
[Fact]
public void OnMediaFailed_HidesPlayerAndShowsError()
{
    // Arrange
    var page = new MovieCollectionDetailsPage(mockViewModel);
    
    // Act
    page.OnMediaFailed(null, new MediaFailedEventArgs("Test error"));
    
    // Assert
    Assert.False(page.VideoPlayer.IsVisible);
    Assert.True(page.PlayButton.IsVisible);
    Assert.True(page.ErrorLabel.IsVisible);
    Assert.Contains("Test error", page.ErrorLabel.Text);
}
```

## Bekannte Einschränkungen

### .NET 10 Kompatibilität

**Problem:**
Das `CommunityToolkit.Maui.MediaElement` Package hat derzeit Kompatibilitätsprobleme mit .NET 10.

**Status:**
- Package-Version 4.1.0: Nicht kompatibel mit .NET 10
- Package-Version 4.2.0+: Angekündigt mit .NET 10 Support

**Workarounds:**

1. **Temporäres Downgrade auf .NET 8:**
   ```xml
   <TargetFramework>net8.0</TargetFramework>
   ```

2. **Alternative Packages:**
   - `MediaManager` (Cross-Platform Media Player)
   - Platform-spezifische Implementierung (AVPlayer für iOS)

3. **Warten auf Update:**
   - Das Error-Handling ist bereits implementiert
   - Funktioniert sofort nach Package-Update

## Error-Messages

### Häufige Fehler:

| Error | Ursache | Lösung |
|-------|---------|--------|
| "Format not supported" | Codec nicht verfügbar | Server-seitige Transcoding |
| "Network error" | Verbindung unterbrochen | Retry-Mechanismus |
| "Resource not found" | Ungültige URL | URL-Validierung |
| "Insufficient buffer" | Langsame Verbindung | Progressive Loading |

## Zukünftige Erweiterungen

### 1. Automatischer Retry

```csharp
private int _retryCount = 0;
private const int MaxRetries = 3;

private async void OnMediaFailed(object? sender, MediaFailedEventArgs e)
{
    if (_retryCount < MaxRetries)
    {
        _retryCount++;
        await Task.Delay(2000); // 2 Sekunden warten
        // Erneut laden
        VideoPlayer.Source = _currentVideoSource;
    }
    else
    {
        // Zeige Fehler nach Max-Retries
        ShowError(e.ErrorMessage);
    }
}
```

### 2. Fallback zu alternativer Qualität

```csharp
private async Task TryLowerQualityAsync()
{
    if (_currentQuality == "1080p")
    {
        _currentQuality = "720p";
        VideoPlayer.Source = GetStreamUrl(_videoId, "720p");
    }
    else if (_currentQuality == "720p")
    {
        _currentQuality = "480p";
        VideoPlayer.Source = GetStreamUrl(_videoId, "480p");
    }
}
```

### 3. Detaillierte Error-Telemetrie

```csharp
private void LogMediaError(MediaFailedEventArgs e)
{
    var errorDetails = new
    {
        ErrorMessage = e.ErrorMessage,
        VideoId = _currentVideoId,
        VideoType = _currentVideoType,
        Source = VideoPlayer.Source,
        Timestamp = DateTime.UtcNow,
        Platform = DeviceInfo.Platform.ToString(),
        DeviceModel = DeviceInfo.Model
    };
    
    // An Analytics-Service senden
    AnalyticsService.TrackError("MediaPlaybackFailed", errorDetails);
}
```

## Best Practices

### 1. Immer UI-Thread verwenden

```csharp
MainThread.BeginInvokeOnMainThread(() =>
{
    // UI-Updates hier
});
```

### 2. Resources freigeben

```csharp
VideoPlayer.Source = null; // Stoppt Hintergrund-Prozesse
Timer?.Stop(); // Stoppt Timer
```

### 3. State konsistent halten

```csharp
// Alle UI-Elemente müssen in konsistenten Zustand gebracht werden:
VideoPlayer.IsVisible = false;
PlayButton.IsVisible = true;
Banner.IsVisible = true;
ErrorLabel.IsVisible = true;
```

## Related Documentation

- [Episode Selection](./TECH_Episode_Selection.md) - Smart Play Button Implementation
- [Video Playback](./TECH_Video_Playback.md) - Vollständige Playback-Dokumentation

---

**Implementiert in**: v2.0  
**Betrifft**: iOS, Windows  
**Package**: CommunityToolkit.Maui.MediaElement 4.x

**Siehe auch:**
- [CommunityToolkit.Maui Documentation](https://learn.microsoft.com/dotnet/communitytoolkit/maui/views/mediaelement)
- [MAUI Platform Specifics](https://learn.microsoft.com/dotnet/maui/platform-integration/)
