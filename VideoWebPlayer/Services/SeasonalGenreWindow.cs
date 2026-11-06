namespace VideoWebPlayer.Services;

/// <summary>Evaluates annually recurring, inclusive UTC date windows.</summary>
public static class SeasonalGenreWindow
{
    /// <summary>Gets the exclusive expiry of the currently active window.</summary>
    /// <param name="startDate">The configured inclusive first day.</param>
    /// <param name="endDate">The configured inclusive last day.</param>
    /// <param name="nowUtc">The server's current UTC date/time.</param>
    /// <returns>The exclusive UTC expiry, or null for an inactive or invalid window.</returns>
    public static DateTime? GetExpiry(DateTime? startDate, DateTime? endDate, DateTime nowUtc)
    {
        if (startDate is null || endDate is null)
            return null;
        var start = startDate.Value.Date;
        var end = endDate.Value.Date;
        if (end < start)
            return null;
        while (end < nowUtc.Date)
        {
            if (end.Year == 9999 || start.Year == 9999)
                return null;
            start = start.AddYears(1);
            end = end.AddYears(1);
        }
        if (start > nowUtc.Date || end == DateTime.MaxValue.Date)
            return null;
        return DateTime.SpecifyKind(end.AddDays(1), DateTimeKind.Utc);
    }
}
