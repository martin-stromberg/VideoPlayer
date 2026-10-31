using VideoWebPlayer.Services;
using Xunit;

namespace VideoWebPlayer.Tests;

/// <summary>
/// Tests for the <see cref="AiringInvalidReason"/> diagnosis enum: the reporting sink keeps
/// the most informative (deepest) failure by numeric maximum, so the declared stage order —
/// a later check stage carries the higher value — is a contract worth pinning.
/// </summary>
public sealed class ProgramScheduleServiceTests_InvalidReason
{
    [Fact]
    public void StageOrdering_IsStrictlyIncreasing()
    {
        Assert.Equal(
            new[]
            {
                AiringInvalidReason.None,
                AiringInvalidReason.RuleMissing,
                AiringInvalidReason.RuleInactive,
                AiringInvalidReason.NoOccurrenceAnchor,
                AiringInvalidReason.OutsideOccurrenceWindow,
                AiringInvalidReason.ClipChainBroken,
                AiringInvalidReason.TitlelessAiring,
                AiringInvalidReason.ItemNotInExpansion,
                AiringInvalidReason.StrandSequenceBroken,
                AiringInvalidReason.AnchorWithoutInvaderEvidence,
                AiringInvalidReason.HeadPickInconsistent,
                AiringInvalidReason.SlotSequenceMismatch,
                AiringInvalidReason.RemainderNotReproducible
            },
            Enum.GetValues<AiringInvalidReason>());
    }
}
