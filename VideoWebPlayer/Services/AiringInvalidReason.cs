namespace VideoWebPlayer.Services;

/// <summary>
/// Internal diagnosis of why the reconcile validation (<c>ReconcileWindow.Validate</c> —
/// the head anchor <c>AnchorProvable</c> plus the title-wise slot check
/// <c>SlotStillValid</c>) rejected an airing. The values are stage-ordered — a later
/// check stage carries the higher value — so the reporting sink can keep the most
/// informative (deepest) reason by simple maximum. Some members belonged to the removed
/// re-simulation and are only retained for that stage contract — they are never produced
/// by the title-wise validation. Diagnosis/logging only; never part of a decision.
/// </summary>
internal enum AiringInvalidReason
{
    /// <summary>No rejection — the airing stayed valid.</summary>
    None = 0,

    /// <summary>The producing rule no longer exists.</summary>
    RuleMissing,

    /// <summary>The producing rule exists but is inactive.</summary>
    RuleInactive,

    /// <summary>The rule has no occurrence inside the loaded window that could anchor the airing.</summary>
    NoOccurrenceAnchor,

    /// <summary>
    /// Retained for the stage ordering — the removed hypothesis check reported it when no
    /// occurrence could anchor the airing; the title-wise validation folds that case into
    /// <see cref="NoOccurrenceAnchor"/> and <see cref="AnchorWithoutInvaderEvidence"/>.
    /// </summary>
    OutsideOccurrenceWindow,

    /// <summary>
    /// Retained for the stage ordering — the removed clip-aware pre-filter reported it;
    /// <c>GroupAirings</c> groups are gapless by construction, so the title-wise
    /// validation never produces it.
    /// </summary>
    ClipChainBroken,

    /// <summary>Clip-aware pre-filter: the airing carries no title slot (clips only).</summary>
    TitlelessAiring,

    /// <summary>A slot's content is not part of the rule's current expansion.</summary>
    ItemNotInExpansion,

    /// <summary>An ordered strand's ring contiguity inside the group is broken.</summary>
    StrandSequenceBroken,

    /// <summary>
    /// A shifted real start lacks invader evidence covering the nominal occurrence start
    /// and lies outside the unwritten headroom.
    /// </summary>
    AnchorWithoutInvaderEvidence,

    /// <summary>
    /// Retained for the stage ordering — the removed re-simulation reported it; the
    /// title-wise validation has no pick hypothesis to verify.
    /// </summary>
    HeadPickInconsistent,

    /// <summary>
    /// A slot fails the title-wise check: its stored runtime differs from the content's
    /// runtime, or the content may no longer start at the written start under the current
    /// age window.
    /// </summary>
    SlotSequenceMismatch,

    /// <summary>
    /// Retained for the stage ordering — the removed recursive re-simulation reported it;
    /// the title-wise validation checks every slot individually.
    /// </summary>
    RemainderNotReproducible
}
