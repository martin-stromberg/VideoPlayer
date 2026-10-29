namespace VideoWebPlayer.Services;

/// <summary>
/// Internal diagnosis of why the reconcile validation (<c>ReconcileWindow.Validate</c> /
/// <c>AiringReproducible</c>) rejected an airing: the deepest check stage any hypothesis
/// reached before failing. The values are stage-ordered — a later check stage carries the
/// higher value — so the reporting sink can keep the most informative (deepest) reason by
/// simple maximum. Diagnosis/logging only; never part of a decision.
/// </summary>
internal enum AiringInvalidReason
{
    /// <summary>No rejection — the airing is reproducible.</summary>
    None = 0,

    /// <summary>The producing rule no longer exists.</summary>
    RuleMissing,

    /// <summary>The producing rule exists but is inactive.</summary>
    RuleInactive,

    /// <summary>The rule has no occurrence inside the loaded window that could anchor the airing.</summary>
    NoOccurrenceAnchor,

    /// <summary>
    /// Occurrences exist in the window, but none can anchor the (remaining) airing: its
    /// reconstructed start lies before every usable occurrence — e.g. a marathon-long airing
    /// reaching past the maximum lookback — or earlier chain links already consumed every
    /// candidate at or before it.
    /// </summary>
    OutsideOccurrenceWindow,

    /// <summary>Clip-aware pre-filter: the airing's slot chain is not gaplessly linked.</summary>
    ClipChainBroken,

    /// <summary>Clip-aware pre-filter: the airing carries no title slot (clips only).</summary>
    TitlelessAiring,

    /// <summary>
    /// An airing slot is not part of the rule's current expansion — the flat tail check tests
    /// the tail's head item, the interleaved check every tail slot against its strand.
    /// </summary>
    ItemNotInExpansion,

    /// <summary>The strand order/contiguity of a tail slot is broken.</summary>
    StrandSequenceBroken,

    /// <summary>A shifted real start lacks invader evidence covering the nominal occurrence start.</summary>
    AnchorWithoutInvaderEvidence,

    /// <summary>No hypothesis puts the head pick on the observed ring position / start-strand offset.</summary>
    HeadPickInconsistent,

    /// <summary>The re-simulated pick sequence does not match the airing's written slots.</summary>
    SlotSequenceMismatch,

    /// <summary>A real prefix matched but the merged remainder failed the recursive check.</summary>
    RemainderNotReproducible
}
