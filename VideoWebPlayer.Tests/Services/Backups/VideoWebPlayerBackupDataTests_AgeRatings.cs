using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using VideoWebPlayer.Tests.Helpers;
using Xunit;

namespace VideoWebPlayer.Tests.Services.Backups;

/// <summary>
/// Backups taken before the age rating columns existed must restore with the
/// columns defaulting to <see langword="null"/>.
/// </summary>
public sealed class VideoWebPlayerBackupDataTests_AgeRatings
{
    [Fact]
    public async Task ReadFromAsync_LegacyBackupWithoutAgeRatingColumns_RestoresSuccessfully()
    {
        var ct = TestContext.Current.CancellationToken;
        using var connection = new SqliteConnection("Data Source=file:backuptest-no-agerating?mode=memory&cache=shared");
        await connection.OpenAsync(ct);
        var (db, backup, userId) = await LegacyBackupArchiveBuilder.CreateSeededBackupAsync(connection, ct);
        await using var dbLifetime = db;

        var columnNames = new[] { "AgeRating", "AgeRatingRaw", "AgeRatingMinAge", "AgeRatingClassifiedAt" };
        using var legacyStream = await LegacyBackupArchiveBuilder.RemoveColumnsAsync(backup, "Movies", columnNames, ct);
        using var legacyStream2 = await LegacyBackupArchiveBuilder.RemoveColumnsAsync(legacyStream, "TVShows", columnNames, ct);

        var exception = await Record.ExceptionAsync(async () => await backup.ReadFromAsync(legacyStream2, ct));

        Assert.Null(exception);
        Assert.Equal(userId, (await db.Users.FirstAsync(ct)).Id);
        Assert.All(await db.Movies.ToListAsync(ct), m =>
        {
            Assert.Null(m.AgeRating);
            Assert.Null(m.AgeRatingRaw);
            Assert.Null(m.AgeRatingMinAge);
            Assert.Null(m.AgeRatingClassifiedAt);
        });
        Assert.All(await db.TVShows.ToListAsync(ct), s =>
        {
            Assert.Null(s.AgeRating);
            Assert.Null(s.AgeRatingRaw);
            Assert.Null(s.AgeRatingMinAge);
            Assert.Null(s.AgeRatingClassifiedAt);
        });
    }
}
