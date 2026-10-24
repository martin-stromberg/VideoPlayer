using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using VideoWebPlayer.Data;
using Xunit;

namespace VideoWebPlayer.Tests.Services.Backups;

/// <summary>
/// Ensures that database backups taken before the genre multi-selection feature (tables
/// <c>ProgramRuleGenres</c>, <c>ProgramRuleMediaSources</c>) can still be restored: a legacy
/// genre rule keeps working via its <see cref="ProgramRule.MaterialId"/> fallback, and a backup
/// of the current version restores both tables completely.
/// </summary>
public sealed class VideoWebPlayerBackupDataTests_ProgramRuleLinks
{
    [Fact]
    public async Task ReadFromAsync_LegacyBackupWithoutProgramRuleLinks_RestoresSuccessfully()
    {
        var ct = TestContext.Current.CancellationToken;
        using var connection = new SqliteConnection("Data Source=file:backuptest-no-rulelinks?mode=memory&cache=shared");
        await connection.OpenAsync(ct);
        var (db, backup, _) = await LegacyBackupArchiveBuilder.CreateSeededBackupAsync(connection, ct);
        await using var _ = db;

        // A legacy genre rule carries only MaterialId — no join rows in the old backup.
        var channel = new TVChannel { Name = "TV" };
        db.TVChannels.Add(channel);
        var source = await db.MediaSources.FirstOrDefaultAsync(ct) ?? new MediaSource { Name = "Q", Path = "/q", Host = "h", Port = 22 };
        if (source.Id == 0)
            db.MediaSources.Add(source);
        var genre = new Genre { Name = "Action", MediaSource = source };
        db.Genres.Add(genre);
        await db.SaveChangesAsync(ct);
        db.ProgramRules.Add(new ProgramRule
        {
            TVChannelId = channel.Id,
            Name = "Genreregel",
            RuleType = ProgramRuleType.GenreRotation,
            Recurrence = ProgramRuleRecurrence.Daily,
            TimeOfDay = new TimeOnly(20, 0),
            EpisodeCount = 1,
            MaterialType = ProgramRuleMaterialType.Genre,
            MaterialId = genre.Id
        });
        await db.SaveChangesAsync(ct);

        using var legacyStream = await LegacyBackupArchiveBuilder.RemoveTablesAsync(
            backup, new[] { "ProgramRuleGenres", "ProgramRuleMediaSources" }, ct);
        Assert.Null(await LegacyBackupArchiveBuilder.ReadTableColumnsAsync(legacyStream, "ProgramRuleGenres", ct));
        Assert.Null(await LegacyBackupArchiveBuilder.ReadTableColumnsAsync(legacyStream, "ProgramRuleMediaSources", ct));

        // Must not throw even though the backup lacks both tables added by this feature.
        var exception = await Record.ExceptionAsync(async () => await backup.ReadFromAsync(legacyStream, ct));

        Assert.Null(exception);
        Assert.Empty(await db.ProgramRuleGenres.AsNoTracking().ToListAsync(ct));
        Assert.Single(await db.ProgramRules.AsNoTracking().Where(r => r.MaterialType == ProgramRuleMaterialType.Genre).ToListAsync(ct));
    }

    [Fact]
    public async Task ReadFromAsync_CurrentBackup_RestoresProgramRuleLinks()
    {
        var ct = TestContext.Current.CancellationToken;
        using var connection = new SqliteConnection("Data Source=file:backuptest-rulelinks?mode=memory&cache=shared");
        await connection.OpenAsync(ct);
        var (db, backup, _) = await LegacyBackupArchiveBuilder.CreateSeededBackupAsync(connection, ct);
        await using var _ = db;

        var channel = new TVChannel { Name = "TV" };
        db.TVChannels.Add(channel);
        var source = await db.MediaSources.FirstOrDefaultAsync(ct) ?? new MediaSource { Name = "Q", Path = "/q", Host = "h", Port = 22 };
        if (source.Id == 0)
            db.MediaSources.Add(source);
        var genre = new Genre { Name = "Action", MediaSource = source };
        db.Genres.Add(genre);
        await db.SaveChangesAsync(ct);
        var rule = new ProgramRule
        {
            TVChannelId = channel.Id,
            Name = "Genreregel",
            RuleType = ProgramRuleType.GenreRotation,
            Recurrence = ProgramRuleRecurrence.Daily,
            TimeOfDay = new TimeOnly(20, 0),
            EpisodeCount = 1,
            MaterialType = ProgramRuleMaterialType.Genre,
            MaterialId = genre.Id
        };
        db.ProgramRules.Add(rule);
        await db.SaveChangesAsync(ct);
        db.ProgramRuleGenres.Add(new ProgramRuleGenre { ProgramRuleId = rule.Id, GenreId = genre.Id });
        db.ProgramRuleMediaSources.Add(new ProgramRuleMediaSource { ProgramRuleId = rule.Id, MediaSourceId = source.Id });
        await db.SaveChangesAsync(ct);

        using var stream = new MemoryStream();
        await backup.WriteToAsync(stream, ct);
        stream.Position = 0;
        db.ProgramRuleGenres.RemoveRange(db.ProgramRuleGenres);
        db.ProgramRuleMediaSources.RemoveRange(db.ProgramRuleMediaSources);
        await db.SaveChangesAsync(ct);

        await backup.ReadFromAsync(stream, ct);

        Assert.Single(await db.ProgramRuleGenres.AsNoTracking().ToListAsync(ct));
        Assert.Single(await db.ProgramRuleMediaSources.AsNoTracking().ToListAsync(ct));
    }
}
