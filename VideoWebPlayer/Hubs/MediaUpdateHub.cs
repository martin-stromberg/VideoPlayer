using Microsoft.AspNetCore.SignalR;

namespace VideoWebPlayer.Hubs;

/// <summary>
/// SignalR Hub für Echtzeit-Updates von Media-Listen.
/// </summary>
public class MediaUpdateHub : Hub
{
    private readonly ILogger<MediaUpdateHub> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="MediaUpdateHub"/> class.
    /// </summary>
    /// <param name="logger">Logger instance.</param>
    public MediaUpdateHub(ILogger<MediaUpdateHub> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Called when a client connects to the hub.
    /// </summary>
    public override async Task OnConnectedAsync()
    {
        _logger.LogInformation("Client connected: {ConnectionId}", Context.ConnectionId);
        await base.OnConnectedAsync();
    }

    /// <summary>
    /// Called when a client disconnects from the hub.
    /// </summary>
    /// <param name="exception">The exception that caused the disconnect, if any.</param>
    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        _logger.LogInformation("Client disconnected: {ConnectionId}", Context.ConnectionId);
        await base.OnDisconnectedAsync(exception);
    }

    /// <summary>
    /// Sendet Update-Benachrichtigung an alle Clients, dass neue Videos gescannt wurden.
    /// </summary>
    public async Task NotifyNewVideosScanned(long sourceId, int count)
    {
        await Clients.All.SendAsync("NewVideosScanned", sourceId, count);
    }

    /// <summary>
    /// Sendet Update-Benachrichtigung an einen spezifischen User, dass Continue-Watching aktualisiert wurde.
    /// </summary>
    public async Task NotifyContinueWatchingUpdated(string userId)
    {
        await Clients.User(userId).SendAsync("ContinueWatchingUpdated");
    }

    /// <summary>
    /// Sendet Update-Benachrichtigung an einen spezifischen User, dass Favoriten geändert wurden.
    /// </summary>
    public async Task NotifyFavoritesChanged(string userId)
    {
        await Clients.User(userId).SendAsync("FavoritesChanged");
    }
}
