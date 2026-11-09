using VideoWebPlayer.Data;
using VideoWebPlayer.Services;
using Xunit;

namespace VideoWebPlayer.Tests;

/// <summary>
/// Unit tests for <see cref="ProgramScheduleAgeWindow"/>: the threshold mapping onto the
/// configured stages (12/16/18), the night window crossing midnight, the exact boundary
/// times, the handling of unknown ratings per mode and the schema validation.
/// </summary>
public sealed class ProgramScheduleAgeWindowTests
{
    private static readonly TimeZoneInfo Tz = TimeZoneInfo.Local;
    private static readonly ProgramScheduleAgeWindowSettings W = ProgramScheduleAgeWindowSettings.Default;
    private static readonly ProgramScheduleAgeWindowRule Restrictive =
        new(W, ProgramScheduleUnknownRatingMode.RestrictToNightWindow);
    private static readonly ProgramScheduleAgeWindowRule Unrestricted =
        new(W, ProgramScheduleUnknownRatingMode.Unrestricted);

    private static DateTime Utc(int hour, int minute = 0)
        => TimeZoneInfo.ConvertTimeToUtc(new DateTime(2026, 10, 24, hour, minute, 0, DateTimeKind.Unspecified), Tz);

    [Fact]
    public void Default_SchemaValues()
    {
        Assert.Equal(new TimeOnly(20, 0), W.Fsk12Earliest);
        Assert.Equal(new TimeOnly(22, 0), W.Fsk16Earliest);
        Assert.Equal(new TimeOnly(23, 0), W.Fsk18Earliest);
        Assert.Equal(new TimeOnly(6, 0), W.NightEnd);
        Assert.True(W.IsValid());
    }

    [Theory]
    [InlineData(0, 12, true)]    // FSK 0 — always allowed
    [InlineData(6, 12, true)]
    [InlineData(11, 12, true)]
    [InlineData(12, 12, false)]  // FSK 12 — not before 20:00
    [InlineData(12, 20, true)]   // window opens exactly at Fsk12Earliest
    [InlineData(13, 20, true)]   // MPAA PG-13 maps to the FSK-12 window (largest threshold <= minAge)
    [InlineData(15, 21, true)]
    [InlineData(16, 21, false)]  // FSK 16 — not before 22:00
    [InlineData(16, 22, true)]
    [InlineData(17, 22, true)]   // MPAA R maps to the FSK-16 window
    [InlineData(18, 22, false)]  // FSK 18 — not before 23:00
    [InlineData(18, 23, true)]
    [InlineData(18, 5, true)]    // inside the night window (before NightEnd)
    [InlineData(18, 6, false)]   // NightEnd exactly — the window is closed again
    [InlineData(16, 4, true)]    // FSK 16 airs inside the night window too
    [InlineData(12, 2, true)]
    public void IsAllowedAt_DefaultSchema(int minAge, int hour, bool expected)
        => Assert.Equal(expected,
            ProgramScheduleAgeWindow.IsAllowedAt(minAge, Utc(hour), Restrictive));

    [Fact]
    public void IsAllowedAt_UnknownRating_RestrictToNightWindow()
    {
        Assert.False(ProgramScheduleAgeWindow.IsAllowedAt(null, Utc(12), Restrictive));
        Assert.True(ProgramScheduleAgeWindow.IsAllowedAt(null, Utc(23), Restrictive));
        Assert.True(ProgramScheduleAgeWindow.IsAllowedAt(null, Utc(4), Restrictive));
        Assert.False(ProgramScheduleAgeWindow.IsAllowedAt(null, Utc(6), Restrictive));
    }

    [Fact]
    public void IsAllowedAt_UnknownRating_Unrestricted()
        => Assert.True(ProgramScheduleAgeWindow.IsAllowedAt(null, Utc(12), Unrestricted));

    [Fact]
    public void IsAllowedAt_CustomWindow()
    {
        var custom = new ProgramScheduleAgeWindowRule(
            new ProgramScheduleAgeWindowSettings(new TimeOnly(19, 0), new TimeOnly(21, 0), new TimeOnly(22, 30), new TimeOnly(5, 0)),
            ProgramScheduleUnknownRatingMode.RestrictToNightWindow);

        Assert.True(ProgramScheduleAgeWindow.IsAllowedAt(12, Utc(19), custom));
        Assert.False(ProgramScheduleAgeWindow.IsAllowedAt(16, Utc(20), custom));
        Assert.True(ProgramScheduleAgeWindow.IsAllowedAt(16, Utc(21), custom));
        Assert.False(ProgramScheduleAgeWindow.IsAllowedAt(18, Utc(22), custom));
        Assert.True(ProgramScheduleAgeWindow.IsAllowedAt(18, Utc(22, 30), custom));
        Assert.True(ProgramScheduleAgeWindow.IsAllowedAt(18, Utc(4), custom));
        Assert.False(ProgramScheduleAgeWindow.IsAllowedAt(18, Utc(5), custom));
    }

    [Fact]
    public void IsAllowedAt_NullUtcKind_IsConverted()
    {
        // The helper converts to local time itself — a UTC-typed instant behaves identically.
        var utcTyped = new DateTime(Utc(12).Ticks, DateTimeKind.Utc);
        Assert.Equal(
            ProgramScheduleAgeWindow.IsAllowedAt(18, Utc(12), Restrictive),
            ProgramScheduleAgeWindow.IsAllowedAt(18, utcTyped, Restrictive));
    }

    [Theory]
    [InlineData(21, 20, 22, 6, false)]  // Fsk12 > Fsk16 — not monotone
    [InlineData(20, 22, 21, 6, false)]  // Fsk16 > Fsk18 — not monotone
    [InlineData(20, 22, 23, 20, false)] // NightEnd not before the first window
    [InlineData(20, 22, 23, 6, true)]
    public void IsValid_SchemaChecks(int h12, int h16, int h18, int end, bool expected)
        => Assert.Equal(expected,
            new ProgramScheduleAgeWindowSettings(new TimeOnly(h12, 0), new TimeOnly(h16, 0), new TimeOnly(h18, 0), new TimeOnly(end, 0)).IsValid());

    [Fact]
    public void CountBlockedAt_CountsOnlyBlocked()
    {
        // At 12:00 with restrictive unknown mode 12, 16, 18 and the unrated entry are blocked.
        Assert.Equal(4, ProgramScheduleAgeWindow.CountBlockedAt(
            new int?[] { 0, 6, 12, 16, 18, null }, Utc(12), Restrictive));
        Assert.Equal(3, ProgramScheduleAgeWindow.CountBlockedAt(
            new int?[] { 0, 6, 12, 16, 18, null }, Utc(12), Unrestricted));
    }
}
