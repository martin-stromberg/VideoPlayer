using Microsoft.AspNetCore.SignalR;
using VideoWebPlayer.Hubs;

namespace VideoWebPlayer.Services
{
    /// <summary>
    /// Provides centralized access to SignalR media update notifications.
    /// </summary>
    public class MediaUpdateNotificationService
    {
        private readonly IHubContext<MediaUpdateHub> _hubContext;
        private readonly ILogger<MediaUpdateNotificationService> _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="MediaUpdateNotificationService"/> class.
        /// </summary>
        /// <param name="hubContext">SignalR hub context for push notifications.</param>
        /// <param name="logger">Logger instance.</param>
        public MediaUpdateNotificationService(
            IHubContext<MediaUpdateHub> hubContext,
            ILogger<MediaUpdateNotificationService> logger)
        {
            _hubContext = hubContext;
            _logger = logger;
        }

        /// <summary>
        /// Notifies all clients that new videos have been scanned.
        /// </summary>
        /// <param name="ct">A cancellation token.</param>
        public async Task NotifyStatusAsync(string message, CancellationToken ct = default)
        {
            try
            {
                await _hubContext.Clients.All.SendAsync("StatusChanged", message, cancellationToken: ct);
                _logger.LogInformation("SignalR: StatusChanged sent (message: {message})", message);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to send SignalR notification for StatusChanged");
            }
        }

        /// <summary>
        /// Notifies all clients that new videos have been scanned.
        /// </summary>
        /// <param name="sourceId">The media source identifier.</param>
        /// <param name="count">The number of new videos scanned.</param>
        /// <param name="ct">A cancellation token.</param>
        public async Task NotifyNewVideosScannedAsync(long sourceId, int count, CancellationToken ct = default)
        {
            // Nur senden, wenn tatsächlich neue Videos gefunden wurden
            if (count <= 0)
            {
                _logger.LogDebug("Skipping SignalR notification for NewVideosScanned (Count: {Count})", count);
                return;
            }

            try
            {
                await _hubContext.Clients.All.SendAsync("NewVideosScanned", sourceId, count, cancellationToken: ct);
                _logger.LogInformation("SignalR: NewVideosScanned sent (SourceId: {SourceId}, Count: {Count})", sourceId, count);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to send SignalR notification for NewVideosScanned");
            }
        }

        /// <summary>
        /// Notifies a specific user that their continue-watching list has been updated.
        /// </summary>
        /// <param name="userId">The user identifier.</param>
        /// <param name="ct">A cancellation token.</param>
        public async Task NotifyContinueWatchingUpdatedAsync(string userId, CancellationToken ct = default)
        {
            try
            {
                await _hubContext.Clients.User(userId)
                    .SendAsync("ContinueWatchingUpdated", cancellationToken: ct);
                _logger.LogInformation("SignalR: ContinueWatchingUpdated sent to user {UserId}", userId);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to send SignalR update for ContinueWatchingUpdated to user {UserId}", userId);
            }
        }

		/// <summary>
		/// Notifies a specific user that their favorites list has been updated.
		/// </summary>
		/// <param name="userId">The user identifier.</param>
		/// <param name="ct">A cancellation token.</param>
		public async Task NotifyFavoritesChangedAsync(string userId, CancellationToken ct = default)
		{
			try
			{
				await _hubContext.Clients.User(userId)
					.SendAsync("FavoritesChanged", cancellationToken: ct);
				_logger.LogInformation("SignalR: FavoritesChanged sent to user {UserId}", userId);
			}
			catch (Exception ex)
			{
				_logger.LogWarning(ex, "Failed to send SignalR update for FavoritesChanged to user {UserId}", userId);
			}
		}

        /// <summary>
        /// Notifies all clients that media content has been updated.
        /// </summary>
        /// <param name="ct">A cancellation token.</param>
        public async Task NotifyMediaUpdatedAsync(CancellationToken ct = default)
        {
            try
            {
                await _hubContext.Clients.All.SendAsync("MediaUpdated", cancellationToken: ct);
                _logger.LogInformation("SignalR: MediaUpdated sent to all clients");
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to send SignalR notification for MediaUpdated");
            }
        }
    }
}
