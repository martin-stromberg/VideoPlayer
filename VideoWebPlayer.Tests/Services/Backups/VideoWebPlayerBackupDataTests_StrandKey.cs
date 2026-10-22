using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using VideoWebPlayer.Data;
using VideoWebPlayer.Tests.Helpers;
using Xunit;

namespace VideoWebPlayer.Tests.Services.Backups;

/// <summary>
/// Backups taken before <c>ProgramRuleCursors.StrandKey</c> existed (Schritt 23, series
/// interleaving) must restore with the column staying <see langword="null"/> — the cursor
/// then applies to the rule's single overall strand as before.
/// </summary>
public sealed class VideoWebPlayerBackupDataTests_StrandKey
{
    [Fact]
    public async Task ReadFromAsync_LegacyBackupWithoutStrandKeyColumn_RestoresSuccessfully()
    {
        var ct = TestContext.Current.CancellationToken;
        using var connection = new SqliteConnection("Data Source=file:backuptest-no-strandkey?mode=memory&cache=shared");
        await connection.OpenAsync(ct);
        var (db, backup, userId) = await LegacyBackupArchiveBuilder.CreateSeededBackupAsync(connection, ct);
        await using var dbLifetime = db;

        var channel = new TVChannel { Name = "Testsender" };
        db.TVChannels.Add(channel);
        var rule = new ProgramRule
        {
            TVChannel = channel,
            Name = "Serienfenster",
            RuleType = ProgramRuleType.GenreRotation,
            Recurrence = ProgramRuleRecurrence.Daily,
            TimeOfDay = new TimeOnly(12, 0),
            EpisodeCount = 2,
            MaterialType = ProgramRuleMaterialType.Genre,
            MaterialId = 1,
            Priority = 5,
            IsActive = true
        };
        db.ProgramRules.Add(rule);
        db.ProgramRuleCursors.Add(new ProgramRuleCursor
        {
            ProgramRule = rule,
            StrandKey = "show:1",
            LastContentType = ProgramCursorContentType.TVShowEpisode,
            LastContentId = 7,
            UpdatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync(ct);

        using var legacyStream = await LegacyBackupArchiveBuilder.RemoveColumnsAsync(backup, "ProgramRuleCursors", new[] { "StrandKey" }, ct);

        var exception = await Record.ExceptionAsync(async () => await backup.ReadFromAsync(legacyStream, ct));

        Assert.Null(exception);
        // The seeded entities are still tracked — clear the change tracker so the query reads
        // the actually-restored row, not the in-memory snapshot from seeding.
        db.ChangeTracker.Clear();
        Assert.Equal(userId, (await db.Users.FirstAsync(ct)).Id);
        var cursor = await db.ProgramRuleCursors.SingleAsync(ct);
        Assert.Null(cursor.StrandKey);
        Assert.Equal(7, cursor.LastContentId);
    }
}
