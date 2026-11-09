using VideoWebPlayer.Data;
using VideoWebPlayer.Tests.Helpers;
using Xunit;

namespace VideoWebPlayer.Tests;

/// <summary>
/// Tests for the <c>DateTimeKind</c> of <see cref="ProgramSlot"/> time values materialized from
/// SQLite: the provider stores DateTime as kind-less text, so the entity type configuration
/// declares the values UTC on read (Issue #260) — the precondition for <c>ToLocalTime()</c>
/// actually converting. Runs against real in-memory SQLite via <see cref="ProgramScheduleTestDb"/>;
/// the InMemory provider would keep the seeded objects and never exercise the conversion.
/// </summary>
public sealed class ProgramSlotTests_DateTimeKind
{
    private static readonly DateTime StartUtc = new(2026, 10, 24, 18, 30, 0, DateTimeKind.Utc);
    private static readonly DateTime EndUtc = StartUtc.AddMinutes(90);

    [Fact]
    public async Task ReadSlot_AfterSaveAndReload_ExposesUtcKind()
    {
        var ct = TestContext.Current.CancellationToken;
        using var fixture = await ProgramScheduleTestDb.CreateAsync("slot-kind-utc", ct);
        var channel = await fixture.GetChannelAsync(ct);
        var item = await fixture.CreateMediaItemAsync(duration: TimeSpan.FromMinutes(90));
        await fixture.CreateSlotAsync(channel.Id, StartUtc, EndUtc, item.Id);

        var read = Assert.Single(await fixture.SlotsAsync(ct));

        Assert.Equal(DateTimeKind.Utc, read.Start.Kind);
        Assert.Equal(DateTimeKind.Utc, read.End.Kind);
        Assert.Equal(DateTimeKind.Utc, read.CreatedAt.Kind);
        Assert.Equal(StartUtc, read.Start);
        Assert.Equal(EndUtc, read.End);
    }

    [Fact]
    public async Task ReadSlot_ToLocalTime_ConvertsInNonUtcZone()
    {
        var ct = TestContext.Current.CancellationToken;
        using var fixture = await ProgramScheduleTestDb.CreateAsync("slot-kind-local", ct);
        var channel = await fixture.GetChannelAsync(ct);
        var item = await fixture.CreateMediaItemAsync(duration: TimeSpan.FromMinutes(90));
        await fixture.CreateSlotAsync(channel.Id, StartUtc, EndUtc, item.Id);

        var read = Assert.Single(await fixture.SlotsAsync(ct));

        // The admin page calls ToLocalTime() on the read value: it must produce the local
        // wall time of the UTC instant — verified against the machine zone and a fixed
        // non-UTC zone without DST (Tokyo, UTC+9: 18:30 UTC is 03:30 the next day).
        var local = read.Start.ToLocalTime();
        Assert.Equal(DateTimeKind.Local, local.Kind);
        Assert.Equal(TimeZoneInfo.ConvertTimeFromUtc(read.Start, TimeZoneInfo.Local), local);

        var tokyo = TimeZoneInfo.FindSystemTimeZoneById("Asia/Tokyo");
        Assert.Equal(new DateTime(2026, 10, 25, 3, 30, 0),
            TimeZoneInfo.ConvertTimeFromUtc(read.Start, tokyo));
    }

    [Fact]
    public async Task ReadSlot_KindAwareApis_TreatValueAsUtc()
    {
        var ct = TestContext.Current.CancellationToken;
        using var fixture = await ProgramScheduleTestDb.CreateAsync("slot-kind-offset", ct);
        var channel = await fixture.GetChannelAsync(ct);
        var item = await fixture.CreateMediaItemAsync(duration: TimeSpan.FromMinutes(90));
        await fixture.CreateSlotAsync(channel.Id, StartUtc, EndUtc, item.Id);

        var read = Assert.Single(await fixture.SlotsAsync(ct));

        // The fragility of Unspecified: kind-aware consumers would read the value as local
        // time. With the declared kind these are exact round-trips on every machine.
        Assert.Equal(TimeSpan.Zero, new DateTimeOffset(read.Start).Offset);
        Assert.Equal(read.Start, read.Start.ToUniversalTime());
        Assert.Equal(new DateTimeOffset(read.Start, TimeSpan.Zero), new DateTimeOffset(read.Start));
    }
}
