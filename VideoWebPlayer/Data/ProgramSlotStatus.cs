namespace VideoWebPlayer.Data
{
    /// <summary>
    /// The lifecycle status of a <see cref="ProgramSlot"/>: whether it is part of the effective
    /// schedule or has been displaced by a schedule re-evaluation (kept for traceability).
    /// </summary>
    public enum ProgramSlotStatus
    {
        /// <summary>The slot is part of the effective schedule.</summary>
        Scheduled = 0,

        /// <summary>
        /// The slot was displaced by a schedule re-evaluation (rule or media change). It is kept
        /// as history but no longer occupies air time and does not block replacement slots.
        /// </summary>
        Displaced = 1
    }
}
