namespace VideoWebPlayer.Services;

/// <summary>
/// One HLS segmentation job for a broadcast slot: which input to transcode, from which position,
/// for how long and into which segment directory.
/// </summary>
/// <param name="SlotId">The <see cref="Data.ProgramSlot"/> identifier this job belongs to.</param>
/// <param name="Input">The media input (local file path or piped stream).</param>
/// <param name="PositionSeconds">The playback position inside the media to start at.</param>
/// <param name="RemainingSeconds">The maximum output duration (the slot's remaining air time).</param>
/// <param name="SegmentSeconds">The target HLS segment length in seconds.</param>
/// <param name="SegmentDirectory">The directory segments and the playlist are written to.</param>
/// <param name="FfmpegPath">The resolved ffmpeg executable path.</param>
public sealed record LiveStreamJob(
    long SlotId,
    LiveStreamInput Input,
    double PositionSeconds,
    double RemainingSeconds,
    int SegmentSeconds,
    string SegmentDirectory,
    string FfmpegPath);
