using VideoWebPlayer.Components.Pages.Admin;
using VideoWebPlayer.Data;
using Xunit;

namespace VideoWebPlayer.Tests.Components;

/// <summary>
/// Unit tests for <see cref="TVSenderAdmin.BuildScheduleDisplayItems"/>: the pure display
/// grouping that collapses gapless runs of <c>Scheduled</c> clip-fill slots of the same rule
/// into a single <see cref="TVSenderAdmin.ScheduleClipBlockItem"/> (from 2 clips on), while
/// everything else stays a <see cref="TVSenderAdmin.ScheduleSlotItem"/>. The grouping is
/// internal and tested directly via <c>InternalsVisibleTo</c> (same pattern as the
/// <c>internal static</c> helpers in <c>TVShowDetails.razor</c>).
/// </summary>
public sealed class ScheduleDisplayItemsTests
{
    private static readonly DateTime Base = new(2026, 10, 28, 20, 0, 0, DateTimeKind.Utc);

    private static ProgramSlot Clip(long id, int startMinute, int durationMinutes = 5, long? ruleId = 1)
        => new()
        {
            Id = id,
            Start = Base.AddMinutes(startMinute),
            End = Base.AddMinutes(startMinute + durationMinutes),
            MediaItemId = id,
            ProgramRuleId = ruleId,
            RuleType = ProgramRuleType.MoviePrimeTime,
            IsClipFill = true
        };

    private static ProgramSlot Regular(long id, int startMinute, int durationMinutes = 60, long? ruleId = 1)
        => new()
        {
            Id = id,
            Start = Base.AddMinutes(startMinute),
            End = Base.AddMinutes(startMinute + durationMinutes),
            MediaItemId = id,
            ProgramRuleId = ruleId,
            RuleType = ProgramRuleType.MoviePrimeTime
        };

    [Fact]
    public void BuildScheduleDisplayItems_ConsecutiveClips_FormSingleBlock()
    {
        var clips = new[] { Clip(1, 0), Clip(2, 5), Clip(3, 10) };

        var items = TVSenderAdmin.BuildScheduleDisplayItems(clips);

        var block = Assert.Single(items);
        var clipBlock = Assert.IsType<TVSenderAdmin.ScheduleClipBlockItem>(block);
        Assert.Equal(new long[] { 1, 2, 3 }, clipBlock.Clips.Select(c => c.Id).ToArray());
    }

    [Fact]
    public void BuildScheduleDisplayItems_SingleClip_StaysSlotItem()
    {
        var items = TVSenderAdmin.BuildScheduleDisplayItems(new[] { Clip(1, 0) });

        var item = Assert.Single(items);
        var slotItem = Assert.IsType<TVSenderAdmin.ScheduleSlotItem>(item);
        Assert.Equal(1, slotItem.Slot.Id);
    }

    [Fact]
    public void BuildScheduleDisplayItems_NonClipSlot_SplitsRun()
    {
        var slots = new ProgramSlot[]
        {
            Clip(1, 0), Clip(2, 5),
            Regular(3, 10),
            Clip(4, 70), Clip(5, 75)
        };

        var items = TVSenderAdmin.BuildScheduleDisplayItems(slots);

        Assert.Equal(3, items.Count);
        var blockA = Assert.IsType<TVSenderAdmin.ScheduleClipBlockItem>(items[0]);
        Assert.Equal(new long[] { 1, 2 }, blockA.Clips.Select(c => c.Id).ToArray());
        var middle = Assert.IsType<TVSenderAdmin.ScheduleSlotItem>(items[1]);
        Assert.Equal(3, middle.Slot.Id);
        var blockB = Assert.IsType<TVSenderAdmin.ScheduleClipBlockItem>(items[2]);
        Assert.Equal(new long[] { 4, 5 }, blockB.Clips.Select(c => c.Id).ToArray());
    }

    [Fact]
    public void BuildScheduleDisplayItems_RuleChange_SplitsBlock()
    {
        // Lückenlose Zeitkette, aber die Programmregel wechselt mitten im Lauf.
        var slots = new ProgramSlot[]
        {
            Clip(1, 0, ruleId: 1), Clip(2, 5, ruleId: 1),
            Clip(3, 10, ruleId: 2), Clip(4, 15, ruleId: 2)
        };

        var items = TVSenderAdmin.BuildScheduleDisplayItems(slots);

        Assert.Equal(2, items.Count);
        var blockA = Assert.IsType<TVSenderAdmin.ScheduleClipBlockItem>(items[0]);
        Assert.Equal(new long[] { 1, 2 }, blockA.Clips.Select(c => c.Id).ToArray());
        var blockB = Assert.IsType<TVSenderAdmin.ScheduleClipBlockItem>(items[1]);
        Assert.Equal(new long[] { 3, 4 }, blockB.Clips.Select(c => c.Id).ToArray());
    }

    [Fact]
    public void BuildScheduleDisplayItems_TimeGap_SplitsBlock()
    {
        // Gleiche Regel, aber eine dokumentierte Restlücke (Start != prev.End) trennt den Lauf.
        var slots = new ProgramSlot[]
        {
            Clip(1, 0), Clip(2, 5),
            Clip(3, 20), Clip(4, 25)
        };

        var items = TVSenderAdmin.BuildScheduleDisplayItems(slots);

        Assert.Equal(2, items.Count);
        var blockA = Assert.IsType<TVSenderAdmin.ScheduleClipBlockItem>(items[0]);
        Assert.Equal(new long[] { 1, 2 }, blockA.Clips.Select(c => c.Id).ToArray());
        var blockB = Assert.IsType<TVSenderAdmin.ScheduleClipBlockItem>(items[1]);
        Assert.Equal(new long[] { 3, 4 }, blockB.Clips.Select(c => c.Id).ToArray());
    }

    [Fact]
    public void BuildScheduleDisplayItems_DisplacedClip_RendersSingle_AndSplits()
    {
        var displaced = Clip(3, 10);
        displaced.Status = ProgramSlotStatus.Displaced;
        var slots = new ProgramSlot[]
        {
            Clip(1, 0), Clip(2, 5),
            displaced,
            Clip(4, 15), Clip(5, 20)
        };

        var items = TVSenderAdmin.BuildScheduleDisplayItems(slots);

        Assert.Equal(3, items.Count);
        Assert.IsType<TVSenderAdmin.ScheduleClipBlockItem>(items[0]);
        // Der verdrängte Clip-Slot ist reine Traceability-Anzeige — Einzelkarte, kein Block-Beitritt.
        var middle = Assert.IsType<TVSenderAdmin.ScheduleSlotItem>(items[1]);
        Assert.Equal(ProgramSlotStatus.Displaced, middle.Slot.Status);
        Assert.IsType<TVSenderAdmin.ScheduleClipBlockItem>(items[2]);
    }

    [Fact]
    public void ScheduleClipBlockItem_LessThanTwoClips_Throws()
    {
        // Die Block-Invariante (>= 2 Clips, darunter Einzelkarte) wird im Record erzwungen,
        // statt nur implizit am Konstruktionsort FlushClipRun zu gelten.
        Assert.Throws<ArgumentNullException>(() => new TVSenderAdmin.ScheduleClipBlockItem(null!));
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new TVSenderAdmin.ScheduleClipBlockItem(Array.Empty<ProgramSlot>()));
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new TVSenderAdmin.ScheduleClipBlockItem(new[] { Clip(1, 0) }));
    }
}
