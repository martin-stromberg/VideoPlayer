using System;
using System.Collections.Generic;

namespace VideoWebPlayer.Services;

/// <summary>
/// Aggregated progress of the media duration probe (<see cref="MediaDurationProbeService"/>) for
/// the administration view: counters over all video media items plus the most recent probe
/// results, so an administrator can tell whether the probe is advancing or stalled without
/// consulting logs or the database.
/// </summary>
/// <param name="TotalCount">How many video media items the probe considers at all.</param>
/// <param name="ProbedCount">Items with a determined <see cref="Data.MediaItem.Duration"/>.</param>
/// <param name="PendingCount">Items without a duration whose probe is still pending or retrying
/// (attempts below the configured maximum).</param>
/// <param name="FailedCount">Items without a duration whose attempt budget is exhausted
/// (permanently failed).</param>
/// <param name="LastActivityAt">Timestamp of the most recent probe attempt, or <c>null</c> when
/// nothing was ever probed.</param>
/// <param name="ProbedLastHour">How many probe attempts happened within the last hour.</param>
/// <param name="RecentItems">The most recently probed items, newest first.</param>
/// <param name="PendingItems">Up to a caller-chosen limit of the still pending or retrying
/// items (matching <see cref="PendingCount"/>), most recently attempted first; items never
/// probed come last and report a <see langword="null"/> attempt timestamp.</param>
/// <param name="FailedItems">Up to a caller-chosen limit of the permanently failed items
/// (matching <see cref="FailedCount"/>), most recently attempted first.</param>
public sealed record MediaDurationProbeProgress(
    int TotalCount,
    int ProbedCount,
    int PendingCount,
    int FailedCount,
    DateTime? LastActivityAt,
    int ProbedLastHour,
    IReadOnlyList<MediaDurationProbeRecentItem> RecentItems,
    IReadOnlyList<MediaDurationProbeDetailItem> PendingItems,
    IReadOnlyList<MediaDurationProbeDetailItem> FailedItems);

/// <summary>
/// One entry of the rolling list in <see cref="MediaDurationProbeProgress.RecentItems"/>.
/// </summary>
/// <param name="Name">The media item's file name.</param>
/// <param name="SourceName">The name of the media source the item belongs to.</param>
/// <param name="ProbedAt">Timestamp of the last probe attempt.</param>
/// <param name="Duration">The determined duration, or <c>null</c> when the attempt failed.</param>
/// <param name="Attempts">The failed-attempt counter after that attempt.</param>
public sealed record MediaDurationProbeRecentItem(
    string Name,
    string SourceName,
    DateTime ProbedAt,
    TimeSpan? Duration,
    int Attempts);

/// <summary>
/// One entry of the expandable detail lists <see cref="MediaDurationProbeProgress.PendingItems"/>
/// and <see cref="MediaDurationProbeProgress.FailedItems"/>: enough to identify the affected file
/// and judge its state (attempt counter plus timestamp of the last attempt).
/// </summary>
/// <param name="MediaItemId">Identity of the media item — the file name is not unique, so
/// per-entry actions (e.g. resetting a failed probe) address the item by its id.</param>
/// <param name="Name">The media item's file name.</param>
/// <param name="SourceName">The name of the media source the item belongs to.</param>
/// <param name="LastAttemptAt">Timestamp of the most recent probe attempt, or
/// <see langword="null"/> when the item was never probed yet.</param>
/// <param name="Attempts">The failed-attempt counter so far.</param>
public sealed record MediaDurationProbeDetailItem(
    long MediaItemId,
    string Name,
    string SourceName,
    DateTime? LastAttemptAt,
    int Attempts);
