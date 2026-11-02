using VideoWebPlayer.Client.Models;
using VideoWebPlayer.Components.Pages;
using Xunit;

namespace VideoWebPlayer.Tests.Components;

/// <summary>
/// Unit tests for <see cref="LiveTv.BuildUpcomingDisplayItems"/>: the same display grouping
/// the admin "Sendeplan" applies (<see cref="TVSenderAdmin.BuildScheduleDisplayItems"/>) —
/// gapless runs of clip-fill guide entries of the same rule collapse into a single
/// <see cref="LiveTv.UpcomingClipBlockItem"/> (from 2 clips on), while everything else stays
/// an <see cref="LiveTv.UpcomingEntryItem"/>. The guide answers only scheduled slots, so —
/// unlike the admin view — no status split is tested here.
/// </summary>
public sealed class LiveTvUpcomingDisplayItemsTests
{
    private static readonly DateTimeOffset Base = new(2026, 11, 2, 20, 0, 0, TimeSpan.Zero);

    private static DtoProgramGuideEntry Clip(long id, int startMinute, int durationMinutes = 5, long? ruleId = 1)
        => new()
        {
            SlotId = id,
            StartUtc = Base.AddMinutes(startMinute),
            EndUtc = Base.AddMinutes(startMinute + durationMinutes),
            MediaItemId = id,
            ProgramRuleId = ruleId,
            IsClipFill = true
        };

    private static DtoProgramGuideEntry Regular(long id, int startMinute, int durationMinutes = 60, long? ruleId = 1)
        => new()
        {
            SlotId = id,
            StartUtc = Base.AddMinutes(startMinute),
            EndUtc = Base.AddMinutes(startMinute + durationMinutes),
            MediaItemId = id,
            ProgramRuleId = ruleId
        };

    [Fact]
    public void BuildUpcomingDisplayItems_ConsecutiveClips_FormSingleBlock()
    {
        var clips = new[] { Clip(1, 0), Clip(2, 5), Clip(3, 10) };

        var items = LiveTv.BuildUpcomingDisplayItems(clips);

        var block = Assert.Single(items);
        var clipBlock = Assert.IsType<LiveTv.UpcomingClipBlockItem>(block);
        Assert.Equal(new long[] { 1, 2, 3 }, clipBlock.Clips.Select(c => c.SlotId).ToArray());
    }

    [Fact]
    public void BuildUpcomingDisplayItems_SingleClip_StaysEntryItem()
    {
        var items = LiveTv.BuildUpcomingDisplayItems(new[] { Clip(1, 0) });

        var item = Assert.Single(items);
        var entryItem = Assert.IsType<LiveTv.UpcomingEntryItem>(item);
        Assert.Equal(1, entryItem.Entry.SlotId);
    }

    [Fact]
    public void BuildUpcomingDisplayItems_NonClipEntry_SplitsRun()
    {
        var entries = new[]
        {
            Clip(1, 0), Clip(2, 5),
            Regular(3, 10),
            Clip(4, 70), Clip(5, 75)
        };

        var items = LiveTv.BuildUpcomingDisplayItems(entries);

        Assert.Equal(3, items.Count);
        var blockA = Assert.IsType<LiveTv.UpcomingClipBlockItem>(items[0]);
        Assert.Equal(new long[] { 1, 2 }, blockA.Clips.Select(c => c.SlotId).ToArray());
        var middle = Assert.IsType<LiveTv.UpcomingEntryItem>(items[1]);
        Assert.Equal(3, middle.Entry.SlotId);
        var blockB = Assert.IsType<LiveTv.UpcomingClipBlockItem>(items[2]);
        Assert.Equal(new long[] { 4, 5 }, blockB.Clips.Select(c => c.SlotId).ToArray());
    }

    [Fact]
    public void BuildUpcomingDisplayItems_RuleChange_SplitsBlock()
    {
        // Lückenlose Zeitkette, aber die Programmregel wechselt mitten im Lauf.
        var entries = new[]
        {
            Clip(1, 0, ruleId: 1), Clip(2, 5, ruleId: 1),
            Clip(3, 10, ruleId: 2), Clip(4, 15, ruleId: 2)
        };

        var items = LiveTv.BuildUpcomingDisplayItems(entries);

        Assert.Equal(2, items.Count);
        var blockA = Assert.IsType<LiveTv.UpcomingClipBlockItem>(items[0]);
        Assert.Equal(new long[] { 1, 2 }, blockA.Clips.Select(c => c.SlotId).ToArray());
        var blockB = Assert.IsType<LiveTv.UpcomingClipBlockItem>(items[1]);
        Assert.Equal(new long[] { 3, 4 }, blockB.Clips.Select(c => c.SlotId).ToArray());
    }

    [Fact]
    public void BuildUpcomingDisplayItems_TimeGap_SplitsBlock()
    {
        // Gleiche Regel, aber eine dokumentierte Restlücke (StartUtc != prev.EndUtc) trennt den Lauf.
        var entries = new[]
        {
            Clip(1, 0), Clip(2, 5),
            Clip(3, 20), Clip(4, 25)
        };

        var items = LiveTv.BuildUpcomingDisplayItems(entries);

        Assert.Equal(2, items.Count);
        var blockA = Assert.IsType<LiveTv.UpcomingClipBlockItem>(items[0]);
        Assert.Equal(new long[] { 1, 2 }, blockA.Clips.Select(c => c.SlotId).ToArray());
        var blockB = Assert.IsType<LiveTv.UpcomingClipBlockItem>(items[1]);
        Assert.Equal(new long[] { 3, 4 }, blockB.Clips.Select(c => c.SlotId).ToArray());
    }

    [Fact]
    public void UpcomingClipBlockItem_LessThanTwoClips_Throws()
    {
        // Die Block-Invariante (>= 2 Clips, darunter Einzelkarte) wird im Record erzwungen,
        // statt nur implizit am Konstruktionsort FlushClipRun zu gelten.
        Assert.Throws<ArgumentNullException>(() => new LiveTv.UpcomingClipBlockItem(null!));
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new LiveTv.UpcomingClipBlockItem(Array.Empty<DtoProgramGuideEntry>()));
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new LiveTv.UpcomingClipBlockItem(new[] { Clip(1, 0) }));
    }
}
