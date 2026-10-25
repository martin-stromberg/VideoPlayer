using VideoWebPlayer.Data;

namespace VideoWebPlayer.Services;

/// <summary>
/// Administration of placeholder clip groups ("Platzhalter-Clip-Gruppen") of the automated TV
/// channel: named groups of unclassified clip <see cref="MediaItem"/>s that can be assigned to
/// program rules to fill the remainder of a slot-based airing below the rule's placeholder grace
/// period. The reserved system group <see cref="ProgramClipGroup.TrailerGroupName"/> is filled
/// automatically and rejects every manual change. All operations enforce administrator rights
/// server-side; every mutation notifies the schedule worker.
/// </summary>
public interface IProgramClipGroupService
{
    /// <summary>
    /// Returns all clip groups including the reserved system group "Trailer".
    /// </summary>
    Task<List<ProgramClipGroup>> GetGroupsAsync(string userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a new clip group. The name must be non-empty, at most
    /// <see cref="ProgramClipGroup.NameMaxLength"/> characters, unique (case-insensitive) and must
    /// not equal the reserved <see cref="ProgramClipGroup.TrailerGroupName"/>.
    /// </summary>
    Task<ProgramClipGroup> CreateGroupAsync(string userId, string name, CancellationToken cancellationToken = default);

    /// <summary>
    /// Renames a clip group. System groups cannot be renamed; the same name rules as
    /// <see cref="CreateGroupAsync"/> apply.
    /// </summary>
    Task<ProgramClipGroup> RenameGroupAsync(string userId, long groupId, string name, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a clip group including its clip memberships and rule assignments. System groups
    /// cannot be deleted.
    /// </summary>
    Task DeleteGroupAsync(string userId, long groupId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the member count per group in a single grouped query — the administration list
    /// shows a count per group without loading every member row.
    /// </summary>
    Task<IReadOnlyDictionary<long, int>> GetGroupCountsAsync(string userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the member clips of a group with duration and probe status (clips without a probed
    /// <see cref="MediaItem.Duration"/> are listed but excluded from scheduling).
    /// </summary>
    Task<List<ProgramClipGroupClip>> GetGroupClipsAsync(string userId, long groupId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns unclassified video media items of all sources (no movie/episode link,
    /// <see cref="Utils.VideoFileExtensions"/> name check) as assignable clip candidates,
    /// filtered by optional name search and media source.
    /// </summary>
    Task<List<ProgramClipGroupCandidate>> GetClipCandidatesAsync(
        string userId, string? search, long? mediaSourceId, int skip, int take, CancellationToken cancellationToken = default);

    /// <summary>
    /// Adds media items to a group. Every item must exist, be unclassified and a video file;
    /// items already in the group are skipped. System groups reject additions.
    /// </summary>
    Task AddClipsToGroupAsync(string userId, long groupId, IReadOnlyCollection<long> mediaItemIds, CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes a single clip from a group. System groups reject removals.
    /// </summary>
    Task RemoveClipFromGroupAsync(string userId, long groupId, long mediaItemId, CancellationToken cancellationToken = default);
}

/// <summary>
/// A member clip of a <see cref="ProgramClipGroup"/> as shown in the group administration.
/// </summary>
public sealed record ProgramClipGroupClip(
    long MediaItemId,
    string Name,
    string SourceName,
    TimeSpan? Duration,
    DateTime? DurationProbedAt,
    int DurationProbeAttempts);

/// <summary>
/// An assignable clip candidate (unclassified video media item) as shown in the clip picker.
/// </summary>
public sealed record ProgramClipGroupCandidate(
    long MediaItemId,
    string Name,
    long MediaSourceId,
    string SourceName,
    TimeSpan? Duration,
    DateTime? DurationProbedAt,
    int DurationProbeAttempts);
