using System.Collections.Concurrent;
using System.Threading.Channels;

namespace VideoWebPlayer.Services
{
    /// <summary>
    /// Buffers continue-watching progress updates for background processing.
    /// </summary>
    public sealed class ContinueWatchingBuffer
    {
        /// <summary>
        /// Represents a snapshot of playback progress for a user and media entry.
        /// </summary>
        public sealed class ProgressEntry
        {
            /// <summary>
            /// Gets the user identifier associated with the progress entry.
            /// </summary>
            public required string UserId { get; init; }
            /// <summary>
            /// Gets the movie identifier, when the entry refers to a movie.
            /// </summary>
            public long? MovieId { get; init; }
            /// <summary>
            /// Gets the episode identifier, when the entry refers to a TV show episode.
            /// </summary>
            public long? EpisodeId { get; init; }
            /// <summary>
            /// Gets the current playback position.
            /// </summary>
            public TimeSpan Position { get; init; }
            /// <summary>
            /// Gets the total duration of the media item.
            /// </summary>
            public TimeSpan Duration { get; init; }
            /// <summary>
            /// Gets the timestamp when the entry was last updated.
            /// </summary>
            public DateTime UpdatedAt { get; init; } = DateTime.UtcNow;
        }

        private readonly ConcurrentDictionary<string, ProgressEntry> _latestByKey = new();
        private readonly Channel<string> _keysChannel = Channel.CreateUnbounded<string>(new UnboundedChannelOptions
        {
            SingleReader = true, SingleWriter = false
        });

        private static string MakeKey(string userId, long? movieId, long? episodeId)
            => $"{userId}|m:{movieId?.ToString() ?? "-"}|e:{episodeId?.ToString() ?? "-"}";

        /// <summary>
        /// Enqueues or updates a progress entry and schedules it for processing.
        /// </summary>
        /// <param name="userId">The user identifier.</param>
        /// <param name="movieId">The movie identifier.</param>
        /// <param name="episodeId">The episode identifier.</param>
        /// <param name="position">The playback position.</param>
        /// <param name="duration">The total duration.</param>
        public void EnqueueOrUpdate(string userId, long? movieId, long? episodeId, TimeSpan position, TimeSpan duration)
        {
            var key = MakeKey(userId, movieId, episodeId);
            var entry = new ProgressEntry
            {
                UserId = userId,
                MovieId = movieId,
                EpisodeId = episodeId,
                Position = position,
                Duration = duration,
                UpdatedAt = DateTime.UtcNow
            };

            if (_latestByKey.TryAdd(key, entry))
            {
                _ = _keysChannel.Writer.TryWrite(key);
            }
            else
            {
                _latestByKey[key] = entry; // nur aktualisieren, nicht erneut enqueuen
            }
        }

        /// <summary>
        /// Reads the next progress entry snapshot and removes it from the buffer.
        /// </summary>
        /// <param name="ct">A cancellation token.</param>
        /// <returns>The next progress entry or <c>null</c> if none is available.</returns>
        public async Task<ProgressEntry?> ReadNextAsync(CancellationToken ct)
        {
            var key = await _keysChannel.Reader.ReadAsync(ct);
            return _latestByKey.TryRemove(key, out var entry) ? entry : null;
        }
    }
}