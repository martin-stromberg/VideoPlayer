using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using VideoWebPlayer.Data;
using VideoWebPlayer.Utils;

namespace VideoWebPlayer.Services;

/// <summary>
/// The shared eligibility rule for selectable clips — enforced identically for clip-group
/// members (<see cref="ProgramClipGroupService"/>) and the live stream's standby clip
/// (<see cref="ProgramSettingsService"/>): a video file (extension per
/// <see cref="VideoFileExtensions"/>) without movie/episode classification (no entry in
/// <c>MovieMediaItems</c>/<c>TVShowEpisodeMediaItems</c>).
/// </summary>
internal static class MediaItemClipEligibility
{
    /// <summary>
    /// Returns the subset of <paramref name="mediaItemIds"/> that is classified, i.e. linked
    /// to a movie or a TV-show episode.
    /// </summary>
    internal static async Task<HashSet<long>> GetClassifiedIdsAsync(
        ApplicationDbContext db, IReadOnlyCollection<long> mediaItemIds, CancellationToken cancellationToken)
    {
        var ids = mediaItemIds as List<long> ?? mediaItemIds.ToList();
        var classified = (await db.MovieMediaItems
                .Where(l => ids.Contains(l.MediaItemId))
                .Select(l => l.MediaItemId)
                .ToListAsync(cancellationToken))
            .ToHashSet();
        classified.UnionWith(await db.TVShowEpisodeMediaItems
            .Where(l => ids.Contains(l.MediaItemId))
            .Select(l => l.MediaItemId)
            .ToListAsync(cancellationToken));
        return classified;
    }

    /// <summary>
    /// Enforces the eligibility rule on one media item: throws <see cref="ArgumentException"/>
    /// when the path is no video file or the item is classified.
    /// </summary>
    /// <param name="db">Database context.</param>
    /// <param name="mediaItemId">The media item to check.</param>
    /// <param name="path">The media item's file path (extension check).</param>
    /// <param name="usagePhrase">Completes the messages
    /// („Nur Videodateien können {usagePhrase}."), e.g. „als Standby-Clip ausgewählt werden".</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    internal static async Task EnsureUnclassifiedVideoClipAsync(
        ApplicationDbContext db, long mediaItemId, string path, string usagePhrase, CancellationToken cancellationToken)
    {
        if (!VideoFileExtensions.IsVideoFileName(path))
            throw new ArgumentException($"Nur Videodateien können {usagePhrase}.");
        if ((await GetClassifiedIdsAsync(db, [mediaItemId], cancellationToken)).Count != 0)
            throw new ArgumentException($"Nur nicht klassifizierte Clips können {usagePhrase}.");
    }
}
