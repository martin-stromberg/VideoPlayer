# Notification Ticker Component - Technical Documentation

> **Dokumenttyp**: Technische Dokumentation  
> **Zielgruppe**: Entwickler  
> **Version**: 1.0  
> **Letzte Aktualisierung**: 2024

## Übersicht

Die `NotificationTicker`-Komponente ist ein animierter Lauftext (Marquee/Ticker), der Event-basierte Benachrichtigungen im Footer-Bereich der MAUI-Anwendung anzeigt.

## Repository-Kontext

Diese Datei beschreibt die ausgelagerte MAUI-Komponente. Pfade mit `VideoWebPlayer.Maui/...` beziehen sich auf das separate MAUI-Repository, in dieser Arbeitskopie auf den Klon unter `Sub-Repository/`.

## Features

- ✅ **Event-basiert**: Empfängt automatisch Notifications über das Event-System
- ✅ **Animiert**: Scrollt von rechts nach links wie ein Nachrichtenticker
- ✅ **Queue-System**: Mehrere Nachrichten werden nacheinander angezeigt
- ✅ **Responsive**: Passt die Animationsgeschwindigkeit an die Textlänge an
- ✅ **Icon-Support**: Zeigt passende Emoji-Icons für verschiedene Event-Typen

## Architektur

```
┌─────────────────────────────────────────────────┐
│         NotificationTicker Component            │
│                                                 │
│  ┌──────────────┐         ┌─────────────────┐  │
│  │ Event        │────────►│ Message Queue   │  │
│  │ Subscriptions│         │ (Concurrent)    │  │
│  └──────────────┘         └─────────────────┘  │
│         │                         │             │
│         │                         ↓             │
│         │                  ┌─────────────────┐  │
│         │                  │ Animation       │  │
│         │                  │ Engine          │  │
│         │                  └─────────────────┘  │
│         │                         │             │
│         └─────────┐               ↓             │
│                   ↓        ┌─────────────────┐  │
│            ┌─────────────┐ │ Ticker Label    │  │
│            │ Placeholder │ │ (Scrolling)     │  │
│            └─────────────┘ └─────────────────┘  │
└─────────────────────────────────────────────────┘
```

## Komponenten

### 1. XAML Layout

**Datei im MAUI-Repository:** `VideoWebPlayer.Maui/Components/NotificationTicker.xaml`

```xaml
<Border StrokeThickness="0" 
        BackgroundColor="Transparent">
    <Grid x:Name="ClipContainer">
        <!-- Scrolling Label -->
        <Label x:Name="TickerLabel"
               Text="{Binding Message, Source={x:Reference ThisControl}}"
               FontSize="16"
               TextColor="#FFDDAA"
               TranslationX="0" />
        
        <!-- Placeholder -->
        <Label Text="💾 Downloads bereit"
               IsVisible="{Binding HasMessage, Converter={StaticResource InvertedBoolConverter}}" />
    </Grid>
</Border>
```

**Wichtige Elemente:**
- **Border**: Sorgt für Clipping (Text bleibt im Container)
- **TickerLabel**: Das animierte Label mit der Nachricht
- **Placeholder**: Wird angezeigt wenn keine Nachrichten aktiv sind

### 2. Code-Behind

**Datei im MAUI-Repository:** `VideoWebPlayer.Maui/Components/NotificationTicker.xaml.cs`

#### Event Subscriptions

```csharp
public NotificationTicker()
{
    InitializeComponent();

    _eventSubscriber = App.ServiceProvider?.GetService<ISubscribeNotificationEvent>();
    if (_eventSubscriber != null)
    {
        _eventSubscriber.Subscribe<DownloadCompletedEvent>(OnDownloadCompleted);
        _eventSubscriber.Subscribe<DownloadDeletedEvent>(OnDownloadDeleted);
        _eventSubscriber.Subscribe<ContinueWatchingUpdatedEvent>(OnContinueWatchingUpdated);
        _eventSubscriber.Subscribe<NewVideosScannedEvent>(OnNewVideosScanned);
    }
}
```

#### Event Handlers

```csharp
private void OnDownloadCompleted(DownloadCompletedEvent e)
{
    var message = $"✅ Download abgeschlossen: {e.Download.Title}";
    QueueMessage(message);
}

private void OnDownloadDeleted(DownloadDeletedEvent e)
{
    var message = $"🗑️ Download gelöscht: {e.Title}";
    QueueMessage(message);
}

private void OnNewVideosScanned(NewVideosScannedEvent e)
{
    if (e.Count > 0)
    {
        var message = $"🎬 {e.Count} neue Video(s) gefunden";
        QueueMessage(message);
    }
}
```

#### Message Queue System

```csharp
private readonly ConcurrentQueue<string> _messageQueue = new();

private void QueueMessage(string message)
{
    _messageQueue.Enqueue(message);

    MainThread.BeginInvokeOnMainThread(() =>
    {
        if (!_isAnimating)
        {
            _ = ProcessMessageQueueAsync();
        }
    });
}

private async Task ProcessMessageQueueAsync()
{
    _isAnimating = true;

    while (_messageQueue.TryDequeue(out var message))
    {
        await ShowMessageAsync(message);
        await Task.Delay(1000); // Pause zwischen Nachrichten
    }

    _isAnimating = false;
}
```

#### Animation Logic

```csharp
private async Task ShowMessageAsync(string message)
{
    await MainThread.InvokeOnMainThreadAsync(async () =>
    {
        Message = message;
        HasMessage = true;

        // Berechne Animationsdauer basierend auf Text-Länge
        var containerWidth = ClipContainer.Width;
        var labelWidth = message.Length * 10; // Schätzung
        var distance = containerWidth + labelWidth;
        var duration = (uint)(distance / 200.0 * 1000); // ~200px/s
        duration = Math.Max(3000, Math.Min(duration, 15000)); // 3-15 Sekunden

        // Start-Position: rechts außerhalb
        TickerLabel.TranslationX = containerWidth;

        // Animiere nach links außerhalb
        await TickerLabel.TranslateTo(-labelWidth, 0, duration, Easing.Linear);

        // Reset
        TickerLabel.TranslationX = 0;
        HasMessage = false;
        Message = string.Empty;
    });
}
```

## Unterstützte Events

### 1. DownloadCompletedEvent

**Anzeige**: `✅ Download abgeschlossen: [Titel]`

Wird ausgelöst von:
- `DownloadManager.SaveCompletedDownloadAsync()`

### 2. DownloadDeletedEvent

**Anzeige**: `🗑️ Download gelöscht: [Titel]`

Wird ausgelöst von:
- `DownloadManager.DeleteDownloadAsync()`
- `DownloadManager.CleanupExpiredDownloadsAsync()`

### 3. ContinueWatchingUpdatedEvent

**Anzeige**: `📺 Weiterschauen aktualisiert`

Wird ausgelöst via SignalR vom Backend.

### 4. NewVideosScannedEvent

**Anzeige**: `🎬 [Anzahl] neue Video(s) gefunden`

Wird ausgelöst via SignalR vom Backend (nur wenn Count > 0).

## Animation-Details

### Geschwindigkeit
- **Standard**: ~200 Pixel pro Sekunde
- **Mindestdauer**: 3 Sekunden
- **Maximaldauer**: 15 Sekunden
- **Easing**: Linear (konstante Geschwindigkeit)

### Berechnung

```csharp
distance = containerWidth + labelWidth
duration = (distance / 200) * 1000 milliseconds
duration = clamp(duration, 3000, 15000)
```

**Beispiel:**
- Container: 1200px
- Label: 350px (Text: "✅ Download abgeschlossen: MyMovie.mp4")
- Distance: 1550px
- Duration: 7750ms (~7.8 Sekunden)

### Queue-Verhalten

1. Neue Nachricht wird zur Queue hinzugefügt
2. Wenn keine Animation läuft, startet Verarbeitung
3. Jede Nachricht wird komplett angezeigt (rechts → links)
4. 1 Sekunde Pause zwischen Nachrichten
5. Nächste Nachricht aus Queue wird angezeigt

## Integration in HomePage

### XAML

```xaml
<!-- Footer -->
<Image Grid.Row="2" Grid.Column="0" Source="background_footer_left.png" />
<Image Grid.Row="2" Grid.Column="1" Source="background_footer_middle.png" />
<Image Grid.Row="2" Grid.Column="2" Source="background_footer_right.png" />

<!-- Notification Ticker -->
<components:NotificationTicker Grid.Row="2" Grid.Column="1" 
                                Margin="20,0" />
```

Die Komponente liegt über den Hintergrundbildern im Footer-Bereich.

## Platzhalter

Wenn keine Nachricht angezeigt wird:

- **Text**: "💾 Downloads bereit"
- **Farbe**: Grau (#666666)
- **Opacity**: 0.5
- **Position**: Zentriert

## Debugging

### Debug-Ausgaben

```
[NotificationTicker] Subscribed to notification events
[NotificationTicker] Queuing message: ✅ Download abgeschlossen: Movie.mp4
[NotificationTicker] Container: 1200, Label: 350
[NotificationTicker] Animating from 1200 to -350 over 7750ms
```

### Troubleshooting

**Problem**: Nachrichten erscheinen nicht

**Diagnose:**
1. Prüfe Event-Subscriptions im Debug-Output
2. Prüfe ob NotificationEventService korrekt registriert ist
3. Prüfe ob Events tatsächlich publiziert werden

**Problem**: Animation ruckelt

**Ursache**: UI-Thread blockiert

**Lösung**: Async-Operationen in Event-Handlers verwenden:
```csharp
await MainThread.InvokeOnMainThreadAsync(async () => { /* ... */ });
```

## Best Practices

### 1. Kurze Nachrichten
Halten Sie Nachrichten prägnant (max. 50-70 Zeichen):
- ✅ "✅ Download abgeschlossen: Movie.mp4"
- ❌ "Der Download von 'The Very Long Movie Title With Many Words.mp4' wurde erfolgreich abgeschlossen und steht nun zur Verfügung"

### 2. Icons verwenden
Nutzen Sie Emojis für visuelle Unterscheidung:
- ✅ Download abgeschlossen
- 🗑️ Gelöscht
- 📺 Aktualisiert
- 🎬 Neue Videos

### 3. Relevante Infos
Zeigen Sie nur wichtige Benachrichtigungen:
- ✅ Downloads abgeschlossen
- ✅ Neue Videos verfügbar
- ❌ Jeder einzelne Fortschritt-Update

### 4. Performance
- Queue verhindert Überlastung bei vielen Events
- ConcurrentQueue ist thread-safe
- Animation läuft nur wenn Nachrichten vorhanden

## Customization

### Styling anpassen

```xaml
<components:NotificationTicker Grid.Row="2" Grid.Column="1">
    <!-- Styles können über BindableProperties angepasst werden -->
</components:NotificationTicker>
```

### Neue Event-Typen hinzufügen

```csharp
public NotificationTicker()
{
    // ...
    _eventSubscriber?.Subscribe<PlaybackStartedEvent>(OnPlaybackStarted);
}

private void OnPlaybackStarted(PlaybackStartedEvent e)
{
    var message = $"▶️ Wiedergabe gestartet: {e.Title}";
    QueueMessage(message);
}
```

## Bekannte Einschränkungen

1. **Lange Texte**: Werden auf 15 Sekunden Animationsdauer begrenzt
2. **Container-Breite**: Muss zur Layout-Zeit bekannt sein
3. **Queue-Größe**: Keine Begrenzung (könnte bei extremen Situationen problematisch sein)

## Future Enhancements

- [ ] Klickbare Nachrichten (Navigation zu Details)
- [ ] Farbcodierung nach Event-Typ
- [ ] Konfigurier bare Animationsgeschwindigkeit
- [ ] History der letzten N Nachrichten
- [ ] Pause-Button für Animation
- [ ] Priority-Queue für wichtige Nachrichten

## Performance-Metriken

### Memory
- Jede Nachricht in Queue: ~100 Bytes
- Typisch 0-5 Nachrichten in Queue
- Gesamtverbrauch: < 1 KB

### CPU
- Animation via MAUI-Framework (GPU-beschleunigt)
- Minimale CPU-Last während Animation
- Keine Auswirkung wenn keine Nachrichten

## Related Documentation

- [Event-System](./TECH_Event_System.md) - MAUI Event Infrastructure
- [SignalR-Implementation](./TECH_SignalR_Implementation.md) - Backend-Event-Integration
- [Download Management](./TECH_Download_Management.md) - Download-Event-Quelle

---

**Implementiert in**: v2.0  
**Plattformen**: iOS, Windows  
**Komponente**: `VideoWebPlayer.Maui/Components/NotificationTicker.xaml`

**Siehe auch:**
- [MAUI Animations](https://learn.microsoft.com/dotnet/maui/user-interface/animation/basic)
- [MAUI Bindable Properties](https://learn.microsoft.com/dotnet/maui/fundamentals/data-binding/binding-mode)
