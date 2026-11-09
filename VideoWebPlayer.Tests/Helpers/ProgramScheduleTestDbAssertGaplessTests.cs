using VideoWebPlayer.Data;
using Xunit;

namespace VideoWebPlayer.Tests.Helpers;

/// <summary>
/// Counter-proof for <see cref="ProgramScheduleTestDb.AssertGapless"/>: the helper must
/// actually fire on gaps and overlaps — not just accept correct chains.
/// </summary>
public sealed class ProgramScheduleTestDbAssertGaplessTests
{
    private static ProgramSlot Slot(int startMin, int endMin)
        => new() { Start = new DateTime(2026, 10, 24, 12, 0, 0, DateTimeKind.Utc).AddMinutes(startMin), End = new DateTime(2026, 10, 24, 12, 0, 0, DateTimeKind.Utc).AddMinutes(endMin) };

    private static readonly DateTime From = new(2026, 10, 24, 12, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime To = new(2026, 10, 24, 13, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void AssertGapless_GaplessChain_Passes()
    {
        var slots = new List<ProgramSlot> { Slot(0, 30), Slot(30, 60) };
        ProgramScheduleTestDb.AssertGapless(slots, From, To);
    }

    [Fact]
    public void AssertGapless_OverlappingSlots_Fails()
    {
        var slots = new List<ProgramSlot> { Slot(0, 40), Slot(30, 60) };
        Assert.ThrowsAny<Exception>(() => ProgramScheduleTestDb.AssertGapless(slots, From, To));
    }

    [Fact]
    public void AssertGapless_SlotContainedInPredecessor_Fails()
    {
        var slots = new List<ProgramSlot> { Slot(0, 50), Slot(20, 30), Slot(50, 60) };
        Assert.ThrowsAny<Exception>(() => ProgramScheduleTestDb.AssertGapless(slots, From, To));
    }

    [Fact]
    public void AssertGapless_GapBetweenSlots_Fails()
    {
        var slots = new List<ProgramSlot> { Slot(0, 30), Slot(40, 60) };
        Assert.ThrowsAny<Exception>(() => ProgramScheduleTestDb.AssertGapless(slots, From, To));
    }

    [Fact]
    public void AssertGapless_FirstSlotStraddlingWindowStart_Passes()
    {
        var slots = new List<ProgramSlot> { Slot(-10, 30), Slot(30, 60) };
        ProgramScheduleTestDb.AssertGapless(slots, From, To);
    }

    [Fact]
    public void AssertGapless_CoverageEndsBeforeTo_Fails()
    {
        var slots = new List<ProgramSlot> { Slot(0, 50) };
        Assert.ThrowsAny<Exception>(() => ProgramScheduleTestDb.AssertGapless(slots, From, To));
    }
}
