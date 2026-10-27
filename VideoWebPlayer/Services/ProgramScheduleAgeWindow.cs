using VideoWebPlayer.Data;

namespace VideoWebPlayer.Services;

/// <summary>
/// Evaluates the youth-protection window schema (<see cref="ProgramScheduleAgeWindowSettings"/>)
/// for one schedulable item: a title is allowed when its normalized minimum age maps to a
/// window that is open at the slot's local start time. The minimum age is already normalized
/// by <see cref="Utils.AgeRatingNormalizer"/> — this class only maps it onto the configured
/// stages and never parses ratings itself.
/// </summary>
public static class ProgramScheduleAgeWindow
{
    /// <summary>
    /// Whether an item with the given normalized minimum age may start airing at
    /// <paramref name="utcStart"/>. The check runs against the local time of the slot start
    /// (<see cref="TimeZoneInfo.Local"/> — same basis as <c>ProgramRule.TimeOfDay</c>);
    /// a started item may always run past the window edge ("a started item always
    /// completes").
    /// </summary>
    /// <param name="minAge">The normalized minimum age of the title, or <see langword="null"/>
    /// when the rating is unknown — including clips, trailers and fillers that carry no
    /// rating at all; the rule's <see cref="ProgramScheduleAgeWindowRule.UnknownMode"/>
    /// decides then.</param>
    /// <param name="utcStart">The UTC start of the slot to check.</param>
    /// <param name="rule">The youth-protection rule: window schema plus unknown-rating mode.</param>
    public static bool IsAllowedAt(
        int? minAge, DateTime utcStart, ProgramScheduleAgeWindowRule rule)
    {
        var windows = rule.Windows;
        TimeOnly earliest;
        if (minAge is null)
        {
            if (rule.UnknownMode == ProgramScheduleUnknownRatingMode.Unrestricted)
                return true;
            earliest = windows.Fsk18Earliest;
        }
        else if (minAge.Value >= 18)
        {
            earliest = windows.Fsk18Earliest;
        }
        else if (minAge.Value >= 16)
        {
            earliest = windows.Fsk16Earliest;
        }
        else if (minAge.Value >= 12)
        {
            earliest = windows.Fsk12Earliest;
        }
        else
        {
            return true; // below the lowest configured stage — always allowed
        }

        var local = TimeOnly.FromDateTime(TimeZoneInfo.ConvertTime(utcStart, TimeZoneInfo.Local));
        return local >= earliest || local < windows.NightEnd;
    }

    /// <summary>
    /// How many of the given minimum ages are blocked at <paramref name="utcStart"/> — the
    /// counting basis of the rule diagnosis ("kein altersgerechtes Material im Fenster").
    /// </summary>
    public static int CountBlockedAt(
        IEnumerable<int?> minAges, DateTime utcStart, ProgramScheduleAgeWindowRule rule)
        => minAges.Count(minAge => !IsAllowedAt(minAge, utcStart, rule));
}
