using VideoWebPlayer.Client.Models;
using VideoWebPlayer.Data;

namespace VideoWebPlayer.Services;

/// <summary>
/// The program guide ("Programmauskunft") of the automated TV channel: a purely time-based,
/// stateless read service over the <see cref="ProgramSlot"/> schedule. Every answer is a function
/// of the given point in time and the persisted schedule only — it does not depend on viewers or
/// on process state and is correct immediately after a server restart. Only
/// <see cref="ProgramSlotStatus.Scheduled"/> slots are reported; displaced slots are schedule
/// history, not program.
/// </summary>
public interface IProgramGuideService
{
    /// <summary>
    /// Answers "what is on right now and what comes next" for <paramref name="utcNow"/>: the
    /// scheduled slot covering the point in time (with its progress inside the video and its
    /// remaining duration) plus the next scheduled slot after it.
    /// </summary>
    /// <param name="utcNow">The point in time to answer for.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The current broadcast (or <see langword="null"/> during a schedule gap) and the next one.</returns>
    Task<DtoProgramGuideNow> GetNowAsync(DateTimeOffset utcNow, CancellationToken cancellationToken = default);

    /// <summary>
    /// Answers the bounded program overview starting at <paramref name="utcNow"/>: the currently
    /// running slot (if any) plus the scheduled slots starting within the preview window, ordered
    /// by start time. The preview is deliberately capped by
    /// <see cref="ProgramGuideService.MaxPreviewHours"/> — the full schedule remains an
    /// administrative view.
    /// </summary>
    /// <param name="utcNow">The point in time the overview starts at.</param>
    /// <param name="preview">How far ahead to look; must be positive and at most
    /// <see cref="ProgramGuideService.MaxPreviewHours"/> hours.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The program entries covering <c>[utcNow, utcNow + preview)</c>, ordered by start.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The preview window is not positive or exceeds the maximum.</exception>
    Task<DtoProgramGuideEntry[]> GetUpcomingAsync(DateTimeOffset utcNow, TimeSpan preview, CancellationToken cancellationToken = default);
}
