using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using VideoWebPlayer.Data;
using Xunit;

namespace VideoWebPlayer.Tests.Services.Backups;

/// <summary>
/// Ensures that database backups taken before the program-rule validity feature (the nine
/// <c>ProgramRules</c> columns <c>ValidityMode</c>, <c>ValidFrom</c>, <c>ValidTo</c>,
/// <c>ValidFromDay</c>, <c>ValidToDay</c>, <c>ValidWeekdays</c>, <c>ValidHolidayAnchor</c>,
/// <c>ValidHolidayBeforeDays</c>, <c>ValidHolidayAfterDays</c>) can still be restored: legacy
/// rules come back as <see cref="ProgramRuleValidityMode.Unrestricted"/> with all window fields
/// <see langword="null"/>.
/// </summary>
public sealed class VideoWebPlayerBackupDataTests_ProgramRuleValidity
{
    private static readonly string[] ValidityColumns =
    [
        nameof(ProgramRule.ValidityMode),
        nameof(ProgramRule.ValidFrom),
        nameof(ProgramRule.ValidTo),
        nameof(ProgramRule.ValidFromDay),
        nameof(ProgramRule.ValidToDay),
        nameof(ProgramRule.ValidWeekdays),
        nameof(ProgramRule.ValidHolidayAnchor),
        nameof(ProgramRule.ValidHolidayBeforeDays),
        nameof(ProgramRule.ValidHolidayAfterDays)
    ];

    [Fact]
    public async Task ReadFromAsync_LegacyBackupWithoutValidityColumns_RestoresRulesAsUnrestricted()
    {
        var ct = TestContext.Current.CancellationToken;
        using var connection = new SqliteConnection("Data Source=file:backuptest-rulevalidity?mode=memory&cache=shared");
        await connection.OpenAsync(ct);
        var (db, backup, _) = await LegacyBackupArchiveBuilder.CreateSeededBackupAsync(connection, ct);
        await using var _ = db;

        var channel = new TVChannel { Name = "TV" };
        db.TVChannels.Add(channel);
        var source = await db.MediaSources.FirstOrDefaultAsync(ct) ?? new MediaSource { Name = "Q", Path = "/q", Host = "h", Port = 22 };
        if (source.Id == 0)
            db.MediaSources.Add(source);
        await db.SaveChangesAsync(ct);
        var movie = new Movie { Name = "Film", MediaSourceId = source.Id };
        db.Movies.Add(movie);
        await db.SaveChangesAsync(ct);
        db.ProgramRules.Add(new ProgramRule
        {
            TVChannelId = channel.Id,
            Name = "Altregel",
            RuleType = ProgramRuleType.MoviePrimeTime,
            Recurrence = ProgramRuleRecurrence.Daily,
            TimeOfDay = new TimeOnly(20, 0),
            EpisodeCount = 1,
            MaterialType = ProgramRuleMaterialType.Movie,
            MaterialId = movie.Id
        });
        await db.SaveChangesAsync(ct);

        using var legacyStream = await LegacyBackupArchiveBuilder.RemoveColumnsAsync(backup, "ProgramRules", ValidityColumns, ct);
        var columns = await LegacyBackupArchiveBuilder.ReadTableColumnsAsync(legacyStream, "ProgramRules", ct);
        Assert.NotNull(columns);
        Assert.All(ValidityColumns, c => Assert.DoesNotContain(c, columns));

        var exception = await Record.ExceptionAsync(async () => await backup.ReadFromAsync(legacyStream, ct));

        Assert.Null(exception);
        var restored = await db.ProgramRules.AsNoTracking().SingleAsync(r => r.Name == "Altregel", ct);
        Assert.Equal(ProgramRuleValidityMode.Unrestricted, restored.ValidityMode);
        Assert.Null(restored.ValidFrom);
        Assert.Null(restored.ValidTo);
        Assert.Null(restored.ValidFromDay);
        Assert.Null(restored.ValidToDay);
        Assert.Null(restored.ValidWeekdays);
        Assert.Null(restored.ValidHolidayAnchor);
        Assert.Null(restored.ValidHolidayBeforeDays);
        Assert.Null(restored.ValidHolidayAfterDays);
    }
}
