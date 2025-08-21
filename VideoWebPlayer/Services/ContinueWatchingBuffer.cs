using System.Collections.Concurrent;
using System.Threading.Channels;

namespace VideoWebPlayer.Services
{
    public sealed class ContinueWatchingBuffer
    {
        public sealed class ProgressEntry
        {
            public required string UserId { get; init; }
            public long? MovieId { get; init; }
            public long? EpisodeId { get; init; }
            public TimeSpan Position { get; init; }
            public TimeSpan Duration { get; init; }
            public DateTime UpdatedAt { get; init; } = DateTime.UtcNow;
        }

        private readonly ConcurrentDictionary<string, ProgressEntry> _latestByKey = new();
        private readonly Channel<string> _keysChannel = Channel.CreateUnbounded<string>(new UnboundedChannelOptions
        {
            SingleReader = true, SingleWriter = false
        });

        private static string MakeKey(string userId, long? movieId, long? episodeId)
            => $"{userId}|m:{movieId?.ToString() ?? "-"}|e:{episodeId?.ToString() ?? "-"}";

        // Erzeugt oder aktualisiert den Eintrag; bei neuer Key schreibt er den Key einmal in die Queue
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

        // Liefert den aktuellen Snapshot für einen Key und entfernt ihn
        public async Task<ProgressEntry?> ReadNextAsync(CancellationToken ct)
        {
            var key = await _keysChannel.Reader.ReadAsync(ct);
            return _latestByKey.TryRemove(key, out var entry) ? entry : null;
        }
    }
}